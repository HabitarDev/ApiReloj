import "dotenv/config";
import { pollOnce } from "./services/eventPoller";

const INTERVAL = Number(process.env.POLL_INTERVAL || 60000);
let running = false;

async function tick() {
    if (running) return;
    running = true;
    try { await pollOnce(); } catch (e) { console.error(e); }
    finally { running = false; }
}

setInterval(tick, INTERVAL);
console.log("Poller iniciado (src/poller/index.ts)");
