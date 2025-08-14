// src/poller/services/eventPoller.ts
import { db } from "../../db/client";
import { getCapabilities, searchEvents } from "../utils/httpClient";
import { log } from "../logger";

// ── Configuración del poller (ajustable por .env) ─────────────────────────────
const PAGE = 30;
const WINDOW_S = 300;
const OVERLAP_S = 60;
const MAX_PAGES = 1000;
const LOCK_KEY = 42001;

// Filtros requeridos (decimal)
const MAJOR = Number(process.env.RELOJ_MAJOR ?? 5);
const MINOR = Number(process.env.RELOJ_MINOR ?? 38);
const TZ_OFFSET = process.env.RELOJ_TZ_OFFSET ?? "+00:00";
const ATT_STATUS = process.env.RELOJ_ATT_STATUS || undefined;

if (Number.isNaN(MAJOR) || Number.isNaN(MINOR)) {
    throw new Error("RELOJ_MAJOR/RELOJ_MINOR inválidos en .env (deben ser decimales).");
}

// ── Helpers de fecha ──────────────────────────────────────────────────────────
// Hikvision exige sin milisegundos y con offset real del reloj (no 'Z')
function toDeviceIsoNoMs(d: Date, off = TZ_OFFSET): string {
    const s = d.toISOString().replace("Z", off); // 2025-08-13T16:41:57+00:00
    return s.replace(/\.\d{3}(?=[+-]\d{2}:\d{2}$)/, ""); // sin ms
}

// ── Tipos tolerantes para variantes de firmware ───────────────────────────────
type AnyList = any[];

type AttMin = {
    deviceSn: string;
    serialNo: number;
    employeeNo: string;
    attendanceStatus: string;
    timeDeviceStr: string; // viene del payload, ej: "2025-08-13T13:41:57-03:00"
    timeUtcIso: string;    // derivado de lo anterior, para checkpoint
};

// Extrae lo mínimo del evento del payload
function pickMinimal(ev: any): AttMin {
    const deviceSn =
        ev.deviceSN ?? ev.devSN ?? ev.deviceSn ?? ev.deviceSNStr ?? "unknown";

    const serialNo = Number(ev.serialNo ?? ev.serialNumber ?? ev.SN ?? 0);

    // El tiempo que viene del reloj (con offset)
    const timeDeviceStr: string =
        ev.time ?? ev.eventTime ?? ev.occurTime ?? ev.Time ?? new Date().toISOString();

    // Para checkpoint calculamos el instante en UTC ISO
    const timeUtcIso = new Date(timeDeviceStr).toISOString();

    return {
        deviceSn,
        serialNo,
        employeeNo: ev.employeeNoString ?? ev.employeeNo ?? "",
        attendanceStatus: ev.attendanceStatus ?? "",
        timeDeviceStr,
        timeUtcIso,
    };
}

async function loadLastEventTime(): Promise<string | null> {
    const r = await db.query<{ t: string | null }>(
        `SELECT MAX(last_event_time_utc)::text AS t FROM public.poller_checkpoint`
    );
    return r.rows[0]?.t ?? null;
}

async function insertAttendance(row: AttMin): Promise<void> {
    // 1) Insertar en la tabla mínima
    await db.query(
        `INSERT INTO public.attendance_events
             (device_sn, serial_no, employee_no, attendance_status, time_device)
         VALUES ($1,$2,$3,$4,$5)
             ON CONFLICT (device_sn, serial_no) DO NOTHING`,
        [row.deviceSn, row.serialNo, row.employeeNo, row.attendanceStatus, row.timeDeviceStr]
    );

    // 2) Avanzar checkpoint usando el instante UTC
    await db.query(
        `INSERT INTO public.poller_checkpoint(device_sn, last_serial_no, last_event_time_utc)
         VALUES ($1,$2,$3)
             ON CONFLICT (device_sn)
       DO UPDATE SET last_serial_no = EXCLUDED.last_serial_no,
                           last_event_time_utc = EXCLUDED.last_event_time_utc,
                           updated_at = now()`,
        [row.deviceSn, row.serialNo, row.timeUtcIso]
    );
}

// ── Loop de polling con logging a BD + archivo ────────────────────────────────
export async function pollOnce() {
    // Evita dos pollers simultáneos
    const { rows } = await db.query<{ got: boolean }>(
        "SELECT pg_try_advisory_lock($1) AS got",
        [LOCK_KEY]
    );
    if (!rows[0]?.got) {
        log.warn("Otro poller está activo; salto este tick");
        return;
    }

    // Preparamos ventana y registramos RUN
    const now = new Date();
    const end = now; // fin de la ventana
    const start = new Date(end.getTime() - WINDOW_S * 1000);

    // variables para consolidar error ISAPI (si hubiera HTTP≠200 sin throw)
    let lastStatusCode: number | null = null;
    let lastSubStatus: string | null = null;
    let lastErrorCode: number | null = null;
    let lastErrorMsg: string | null = null;

    try {
        // Sanity check (capabilities / límites)
        await getCapabilities();

        const fromCheckpoint = await loadLastEventTime();
        const baseStart = fromCheckpoint ? new Date(fromCheckpoint) : new Date(0);
        const safeStart = new Date(Math.max(baseStart.getTime() - OVERLAP_S * 1000, start.getTime()));

        // Crear run
        const runIns = await db.query<{ id: number }>(
            `INSERT INTO public.poller_runs(window_start, window_end, major, minor, attendance_status)
             VALUES ($1,$2,$3,$4,$5) RETURNING id`,
            [safeStart, end, MAJOR, MINOR, ATT_STATUS ?? null]
        );
        const runId = runIns.rows[0].id;

        let pos = 0;
        let page = 0;
        let totalSeen = 0;
        let totalInserted = 0;
        let httpFirst: number | null = null;
        let httpLast: number | null = null;
        let finalRespStatus: string | null = null;

        for (; page < MAX_PAGES; page++) {
            const startIso = toDeviceIsoNoMs(safeStart, TZ_OFFSET);
            const endIso = toDeviceIsoNoMs(end, TZ_OFFSET);

            const body: any = {
                AcsEventCond: {
                    searchID: `poll_${Date.now()}`,
                    searchResultPosition: pos,
                    maxResults: PAGE,
                    startTime: startIso,
                    endTime: endIso,
                    major: MAJOR,
                    minor: MINOR,
                },
            };
            if (ATT_STATUS) body.AcsEventCond.attendanceStatus = ATT_STATUS;

            const t0 = Date.now();
            const resp = await searchEvents(body);  // <-- mantener así para evitar TS genéricos
            const { data, status } = resp;
            const t1 = Date.now();

            if (httpFirst == null) httpFirst = status;
            httpLast = status;

            // HTTP != 200: registrar y cortar
            if (status !== 200) {
                const errBody: any = data || {};
                const sc   = (errBody?.statusCode ?? null) as number | null;
                const sstr = (errBody?.statusString ?? null) as string | null;
                const ssub = (errBody?.subStatusCode ?? null) as string | null;
                const ecode= (errBody?.errorCode ?? null) as number | null;
                const emsg = (errBody?.errorMsg ?? null) as string | null;

                lastStatusCode = sc;
                lastSubStatus  = ssub;
                lastErrorCode  = ecode;
                lastErrorMsg   = emsg ?? sstr ?? null;

                await db.query(
                    `INSERT INTO public.poller_requests
                     (run_id, page_no, position, ended_at, elapsed_ms, request_start, request_end,
                      query_start, query_end, http_status, response_status, num_of_matches, total_matches, rows_inserted, error_code, error_msg)
                     VALUES ($1,$2,$3, now(), $4, $5, $6, $7, $8, $9, NULL, NULL, NULL, 0, $10, $11)`,
                    [
                        runId, page, pos,
                        t1 - t0,
                        new Date(t0), new Date(t1),
                        safeStart, end,
                        status,
                        ecode, (emsg ?? `${sc ?? ""} ${sstr ?? ""} ${ssub ?? ""}`).trim()
                    ]
                );

                log.warn(
                    { runId, page, pos, status, sc, sstr, ssub, errorCode: ecode, errorMsg: emsg, startIso, endIso, major: MAJOR, minor: MINOR },
                    "Página con HTTP != 200; corto el run"
                );
                break;
            }

            // 200 OK: contemplar variantes de estructura
            const acs: any = (data as any)?.AcsEvent ?? {};
            const respStr: string | null = acs?.responseStatusStrg ?? null; // "OK"/"MORE"/"NO MATCH"
            const num = typeof acs?.numOfMatches === "number" ? acs.numOfMatches : undefined;
            const tot = typeof acs?.totalMatches === "number" ? acs.totalMatches : undefined;

            const rowsAny: AnyList =
                acs?.InfoList ?? acs?.AcsEventInfo ?? (Array.isArray(acs) ? acs : []);

            const minimal: AttMin[] = (Array.isArray(rowsAny) ? rowsAny : []).map(pickMinimal);

            // ordenar por tiempo del dispositivo para avanzar checkpoint en orden
            minimal.sort((a, b) =>
                new Date(a.timeDeviceStr).toISOString().localeCompare(new Date(b.timeDeviceStr).toISOString())
            );

            let inserted = 0;
            for (const r of minimal) {
                await insertAttendance(r);
                inserted++;
            }
            totalInserted += inserted;
            totalSeen += Array.isArray(rowsAny) ? rowsAny.length : 0;

            await db.query(
                `INSERT INTO public.poller_requests
                 (run_id, page_no, position, ended_at, elapsed_ms, request_start, request_end,
                  query_start, query_end, http_status, response_status, num_of_matches, total_matches, rows_inserted)
                 VALUES ($1,$2,$3, now(), $4, $5, $6, $7, $8, $9, $10, $11, $12, $13)`,
                [
                    runId, page, pos,
                    t1 - t0,
                    new Date(t0), new Date(t1),
                    safeStart, end,
                    status, respStr, num ?? null, tot ?? null,
                    inserted
                ]
            );

            log.info({ runId, page, pos, status, respStr, num, tot, inserted }, "Página procesada");

            pos += Array.isArray(rowsAny) ? rowsAny.length : 0;
            finalRespStatus = respStr ?? finalRespStatus;

            if (!Array.isArray(rowsAny) || rowsAny.length < PAGE) break;
        }

        await db.query(
            `UPDATE public.poller_runs
             SET ended_at = now(),
                 page_count = $2,
                 rows_seen = $3,
                 rows_inserted = $4,
                 http_status_first = $5,
                 http_status_final = $6,
                 response_status = $7,
                 status_code = $8,
                 sub_status = $9,
                 error_code = $10,
                 error_msg = $11
             WHERE id = $1`,
            [
                runId,
                page + 1,
                totalSeen,
                totalInserted,
                httpFirst,
                httpLast,
                finalRespStatus,
                lastStatusCode,
                lastSubStatus,
                lastErrorCode,
                lastErrorMsg
            ]
        );

        log.info(
            { runId, pages: page + 1, seen: totalSeen, inserted: totalInserted, httpFirst, httpLast, finalRespStatus, lastStatusCode, lastSubStatus, lastErrorCode, lastErrorMsg },
            "Run cerrado OK"
        );
    } catch (e: any) {
        try {
            const sc    = e?.response?.data?.statusCode ?? null;
            const ssub  = e?.response?.data?.subStatusCode ?? null;
            const ecode = e?.response?.data?.errorCode ?? null;
            const emsg  = e?.response?.data?.errorMsg ?? e?.message ?? null;

            const r = await db.query<{ id: number }>(
                `SELECT id FROM public.poller_runs WHERE ended_at IS NULL ORDER BY id DESC LIMIT 1`
            );
            const openRunId = r.rows[0]?.id;

            if (openRunId) {
                await db.query(
                    `UPDATE public.poller_runs
                     SET ended_at = now(),
                         http_status_final = $2,
                         status_code = $3,
                         sub_status = $4,
                         error_code = $5,
                         error_msg = $6,
                         error_stack = $7
                     WHERE id = $1`,
                    [openRunId, e?.response?.status ?? null, sc, ssub, ecode, emsg, e?.stack ?? null]
                );
            }

            log.error({ err: e, http: e?.response?.status, sc, ssub, ecode, emsg }, "Run con error");
        } finally {
            await db.query("SELECT pg_advisory_unlock($1)", [LOCK_KEY]);
        }
        throw e;
    }

    await db.query("SELECT pg_advisory_unlock($1)", [LOCK_KEY]);
}
