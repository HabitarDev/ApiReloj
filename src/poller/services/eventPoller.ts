// src/poller/services/eventPoller.ts
import { db } from "../../db/client";
import { getCapabilities, searchEvents } from "../utils/httpClient";

// ── Configuración del poller (ajustable por .env) ─────────────────────────────
const PAGE = 30;             // tamaño de página (ver /AcsEvent/capabilities)
const WINDOW_S = 300;        // ventana: 5 min
const OVERLAP_S = 60;        // solapamiento: 60 s
const MAX_PAGES = 1000;      // guardarraíl anti-loop
const LOCK_KEY = 42001;      // clave fija para pg_advisory_lock

// Filtros requeridos por tu equipo (decimal)
const MAJOR = Number(process.env.RELOJ_MAJOR ?? 5);   // Other events
const MINOR = Number(process.env.RELOJ_MINOR ?? 38);  // Fingerprint Matched (0x26 → 38)
const TZ_OFFSET = process.env.RELOJ_TZ_OFFSET ?? "+00:00"; // evita 'Z'
const ATT_STATUS = process.env.RELOJ_ATT_STATUS || undefined; // opcional

if (Number.isNaN(MAJOR) || Number.isNaN(MINOR)) {
    throw new Error("RELOJ_MAJOR/RELOJ_MINOR inválidos en .env (deben ser decimales).");
}

// ── Tipos lazos para tolerar variantes de firmware ────────────────────────────
type AcsEventList = any[];
type SearchResp =
    | { AcsEvent?: { AcsEventInfo?: AcsEventList } }
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

// ── Loop de polling ───────────────────────────────────────────────────────────
export async function pollOnce() {
    // Evita dos pollers simultáneos
    const { rows } = await db.query<{ got: boolean }>(
        "SELECT pg_try_advisory_lock($1) AS got",
        [LOCK_KEY]
    );
    if (!rows[0]?.got) {
        console.warn("Otro poller está activo; salto este tick");
        return;
    }

    try {
        // Sanity check (capabilities / límites)
        await getCapabilities();

        const end = new Date();
        const start = new Date(end.getTime() - WINDOW_S * 1000);

        const fromCheckpoint = await loadLastEventTime();
        const baseStart = fromCheckpoint ? new Date(fromCheckpoint) : new Date(0);
        const safeStart = new Date(
            Math.max(baseStart.getTime() - OVERLAP_S * 1000, start.getTime())
        );

        let pos = 0;

        for (let page = 0; page < MAX_PAGES; page++) {
            const body: any = {
                AcsEventCond: {
                    searchID: `poll_${Date.now()}`,
                    searchResultPosition: pos,
                    maxResults: PAGE,
                    startTime: toOffset(safeStart), // ISO con offset (no 'Z')
                    endTime: toOffset(end),
                    major: MAJOR,                   // requeridos por el equipo
                    minor: MINOR,
                },
            };
            if (ATT_STATUS) body.AcsEventCond.attendanceStatus = ATT_STATUS;

            const { data } = await searchEvents(body);
            const rowsAny: AcsEventList =
                (data as any)?.AcsEvent?.AcsEventInfo ??
                (data as any)?.AcsEvent ??
                [];

            if (!Array.isArray(rowsAny) || rowsAny.length === 0) break;

            // Insertar en orden temporal para que el checkpoint avance correctamente
            const normalized = rowsAny.map(normalize).sort((a, b) =>
                a.eventTimeUtc.localeCompare(b.eventTimeUtc)
            );

            for (const ne of normalized) {
                await insertEvent(ne);
            }

            pos += rowsAny.length;
            if (rowsAny.length < PAGE) break;
        }
    } finally {
        await db.query("SELECT pg_advisory_unlock($1)", [LOCK_KEY]);
    }
}
