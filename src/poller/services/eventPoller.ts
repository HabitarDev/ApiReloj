// src/poller/services/eventPoller.ts
import { db } from "../../db/client";
import { getCapabilities, searchEvents } from "../utils/httpClient";
import { log } from "../logger";

// ── Configuración del poller (ajustable por .env) ─────────────────────────────
const PAGE = 30;             // tamaño de página (ver /AcsEvent/capabilities)
const WINDOW_S = 300;        // ventana: 5 min
const OVERLAP_S = 60;        // solapamiento: 60 s
const MAX_PAGES = 1000;      // guardarraíl anti-loop
const LOCK_KEY = 42001;      // clave fija para pg_advisory_lock

// Filtros requeridos por tu equipo (decimal)
const MAJOR = Number(process.env.RELOJ_MAJOR ?? 5);    // Other events
const MINOR = Number(process.env.RELOJ_MINOR ?? 38);   // Fingerprint Matched (0x26 → 38)
const TZ_OFFSET = process.env.RELOJ_TZ_OFFSET ?? "+00:00"; // evita 'Z'
const ATT_STATUS = process.env.RELOJ_ATT_STATUS || undefined; // opcional

if (Number.isNaN(MAJOR) || Number.isNaN(MINOR)) {
    throw new Error("RELOJ_MAJOR/RELOJ_MINOR inválidos en .env (deben ser decimales).");
}

// ── Tipos lazos para tolerar variantes de firmware ────────────────────────────
type AcsEventList = any[];
type SearchResp =
    | { AcsEvent?: { AcsEventInfo?: AcsEventList; InfoList?: AcsEventList; responseStatusStrg?: string; numOfMatches?: number; totalMatches?: number } }
    | { AcsEvent?: AcsEventList }
    | Record<string, unknown>;

type NormEvent = {
    deviceSn: string;
    serialNo: number;
    eventTimeUtc: string;
    employeeNo: string | null;
    name: string | null;
    major: number | null;
    minor: number | null;
    attendanceStatus: string | null;
    raw: any;
};

// ── Utilidades ────────────────────────────────────────────────────────────────
const toOffset = (d: Date, off = TZ_OFFSET) => d.toISOString().replace("Z", off);

// Normaliza un evento del payload a nuestro esquema
function normalize(ev: any): NormEvent {
    const deviceSn =
        ev.deviceSN ?? ev.devSN ?? ev.deviceSn ?? ev.deviceSNStr ?? "unknown";

    const serialNo = Number(ev.serialNo ?? ev.serialNumber ?? ev.SN ?? 0);

    const t =
        ev.eventTime ??
        ev.time ??
        ev.occurTime ??
        ev.Time ??
        new Date().toISOString();
    const eventTimeUtc = new Date(t).toISOString();

    return {
        deviceSn,
        serialNo,
        eventTimeUtc,
        employeeNo: ev.employeeNoString ?? ev.employeeNo ?? null,
        name: ev.name ?? ev.personName ?? null,
        major: typeof ev.major === "number" ? ev.major : null,
        minor: typeof ev.minor === "number" ? ev.minor : null,
        attendanceStatus: ev.attendanceStatus ?? null,
        raw: ev,
    };
}

async function loadLastEventTime(): Promise<string | null> {
    const r = await db.query<{ t: string | null }>(
        `SELECT MAX(last_event_time_utc)::text AS t FROM public.poller_checkpoint`
    );
    return r.rows[0]?.t ?? null;
}

async function insertEvent(ev: NormEvent): Promise<void> {
    await db.query(
        `INSERT INTO public.hik_events
         (device_sn, serial_no, event_time_utc, employee_no, name, major, minor, attendance_status, raw)
         VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)
             ON CONFLICT (device_sn, serial_no) DO NOTHING`,
        [
            ev.deviceSn,
            ev.serialNo,
            ev.eventTimeUtc,
            ev.employeeNo,
            ev.name,
            ev.major,
            ev.minor,
            ev.attendanceStatus,
            ev.raw,
        ]
    );

    await db.query(
        `INSERT INTO public.poller_checkpoint(device_sn, last_serial_no, last_event_time_utc)
         VALUES ($1,$2,$3)
             ON CONFLICT (device_sn)
         DO UPDATE SET last_serial_no = EXCLUDED.last_serial_no,
                             last_event_time_utc = EXCLUDED.last_event_time_utc,
                             updated_at = now()`,
        [ev.deviceSn, ev.serialNo, ev.eventTimeUtc]
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
    const end = new Date();
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
        const safeStart = new Date(
            Math.max(baseStart.getTime() - OVERLAP_S * 1000, start.getTime())
        );

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
            const startIso = toOffset(safeStart);
            const endIso = toOffset(end);

            const body: any = {
                AcsEventCond: {
                    searchID: `poll_${Date.now()}`,
                    searchResultPosition: pos,
                    maxResults: PAGE,
                    startTime: startIso,  // ISO con offset (no 'Z')
                    endTime: endIso,
                    major: MAJOR,         // requeridos por el equipo
                    minor: MINOR,
                },
            };
            if (ATT_STATUS) body.AcsEventCond.attendanceStatus = ATT_STATUS;

            const t0 = Date.now();
            const resp = await searchEvents(body);
            const { data, status } = resp;
            const t1 = Date.now();

            if (httpFirst == null) httpFirst = status;
            httpLast = status;

            // === NUEVO: manejo explícito de HTTP != 200 con log/BD y corte de ciclo ===
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

                // Registrar la página con error
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
                        ecode, (emsg ?? `${sc ?? ''} ${sstr ?? ''} ${ssub ?? ''}`).trim()
                    ]
                );

                log.warn(
                    { runId, page, pos, status, sc, sstr, ssub, errorCode: ecode, errorMsg: emsg, startIso, endIso, major: MAJOR, minor: MINOR },
                    "Página con HTTP != 200; corto el run"
                );
                break; // no continuamos paginando
            }
            // === FIN NUEVO ===

            // Estructura tolerante a variantes (200 OK)
            const acs: any = (data as any)?.AcsEvent ?? {};
            const respStr: string | null = acs?.responseStatusStrg ?? null; // "OK"/"MORE"/"NO MATCH"
            const num = typeof acs?.numOfMatches === "number" ? acs.numOfMatches : undefined;
            const tot = typeof acs?.totalMatches === "number" ? acs.totalMatches : undefined;

            const rowsAny: AcsEventList =
                acs?.InfoList ?? acs?.AcsEventInfo ?? (Array.isArray(acs) ? acs : []);

            // Insertar eventos normalizados (ordenados por tiempo)
            const normalized = (Array.isArray(rowsAny) ? rowsAny : [])
                .map(normalize)
                .sort((a, b) => a.eventTimeUtc.localeCompare(b.eventTimeUtc));

            let inserted = 0;
            for (const ne of normalized) {
                await insertEvent(ne);
                inserted++;
            }
            totalInserted += inserted;
            totalSeen += Array.isArray(rowsAny) ? rowsAny.length : 0;

            // Registrar request/página (200)
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

        // Cerrar RUN con consolidado (incluye error ISAPI si lo hubo sin throw)
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
        // Registrar error en el último run abierto (si lo hubo)
        try {
            const sc    = e?.response?.data?.statusCode ?? null;
            const ssub  = e?.response?.data?.subStatusCode ?? null;
            const ecode = e?.response?.data?.errorCode ?? null;
            const emsg  = e?.response?.data?.errorMsg ?? e?.message ?? null;

            // Buscar el run más reciente sin ended_at (opcional)
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
            // desbloquear pase lo que pase
            await db.query("SELECT pg_advisory_unlock($1)", [LOCK_KEY]);
        }
        throw e;
    }

    // desbloquear si no hubo error
    await db.query("SELECT pg_advisory_unlock($1)", [LOCK_KEY]);
}
