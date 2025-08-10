// src/poller/services/eventPoller.ts
import { db } from "../../db/client";
import { getCapabilities, searchEvents } from "../utils/httpClient";

const PAGE = 30;          // ajústalo según capabilities
const WINDOW_S = 300;     // 5 min
const OVERLAP_S = 60;     // 60 s
const MAX_PAGES = 1000;   // guardarraíl anti-loop

// Tipado mínimo y tolerante del payload del equipo
type AcsEventList = any[];
type SearchResp =
    | { AcsEvent?: { AcsEventInfo?: AcsEventList } }
    | { AcsEvent?: AcsEventList }
    | Record<string, unknown>;

let lastEventTime = new Date(0).toISOString(); // TODO persistir en DB (tabla checkpoints)

export async function pollOnce() {
    await getCapabilities(); // sanity check contra el equipo

    const end = new Date();
    const start = new Date(end.getTime() - WINDOW_S * 1000);
    // solapamiento para no perder eventos en borde de ventana
    const safeStart = new Date(
        Math.max(Date.parse(lastEventTime) - OVERLAP_S * 1000, start.getTime())
    );

    let pos = 0;

    for (let page = 0; page < MAX_PAGES; page++) {
        const body = {
            AcsEventCond: {
                searchID: `poll_${Date.now()}`,
                searchResultPosition: pos,
                maxResults: PAGE,
                startTime: safeStart.toISOString(),
                endTime: end.toISOString(),
                // filtros opcionales: major/minor (DECIMAL), attendanceStatus
            },
        };

        const { data } = await searchEvents<SearchResp>(body);

        // Normalizamos posibles variantes del firmware
        const rows: AcsEventList =
            (data as any)?.AcsEvent?.AcsEventInfo ??
            (data as any)?.AcsEvent ??
            [];

        if (rows.length === 0) break;

        // TODO mapear e insertar idempotente en hik_events (PK device_sn+serial_no)
        // for (const ev of rows) {
        //   const deviceSn = ev.deviceSN ?? ev.devSN ?? "unknown";
        //   const serialNo = Number(ev.serialNo ?? ev.serialNumber);
        //   const eventTimeUtc = new Date(ev.eventTime ?? ev.time).toISOString();
        //   await db.query(
        //     `INSERT INTO hik_events(device_sn, serial_no, event_time_utc, employee_no, name, major, minor, attendance_status, raw)
        //      VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)
        //      ON CONFLICT (device_sn, serial_no) DO NOTHING`,
        //     [deviceSn, serialNo, eventTimeUtc, ev.employeeNoString ?? null, ev.name ?? null, ev.major ?? null, ev.minor ?? null, ev.attendanceStatus ?? null, ev]
        //   );
        //   lastEventTime = eventTimeUtc; // y además persistirlo en una tabla de checkpoints
        // }

        pos += rows.length;
        if (rows.length < PAGE) break;
    }
}
