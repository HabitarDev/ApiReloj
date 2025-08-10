import { pool } from "../../db/client";
import { getCapabilities, searchEvents } from "../utils/httpClient";

const PAGE = 30;          // ajústalo según capabilities
const WINDOW_S = 300;     // 5 min
const OVERLAP_S = 60;     // 60 s

let lastEventTime = new Date(0).toISOString(); // TODO persistir en DB

export async function pollOnce() {
    await getCapabilities(); // sanity check

    const end = new Date();
    const start = new Date(end.getTime() - WINDOW_S * 1000);
    const safeStart = new Date(Math.max(Date.parse(lastEventTime) - OVERLAP_S*1000, start.getTime()));

    let pos = 0;
    while (true) {
        const body = {
            AcsEventCond: {
                searchID: `poll_${Date.now()}`,
                searchResultPosition: pos,
                maxResults: PAGE,
                startTime: safeStart.toISOString(),
                endTime: end.toISOString()
                // filtros opcionales: major/minor DECIMAL, attendanceStatus
            }
        };

        const { data } = await searchEvents(body);
        const rows = data?.AcsEvent?.AcsEventInfo || data?.AcsEvent || [];

        // TODO mapear e insertar idempotente en hik_events (PK device_sn+serial_no)
        // await pool.query('INSERT ... ON CONFLICT DO NOTHING', [...]);

        if (rows.length < PAGE) break;
        pos += rows.length;
        // lastEventTime = último evento confirmado (persistir luego)
    }
}
