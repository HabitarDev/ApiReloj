// src/poller/logger.ts
import pino from "pino";
import fs from "fs";
import path from "path";

/** Usa LOG_TZ_OFFSET si está, si no RELOJ_TZ_OFFSET, si no UTC */
const TZ_OFFSET = process.env.LOG_TZ_OFFSET || process.env.RELOJ_TZ_OFFSET || "+00:00";

/** ISO local correcto (sin milisegundos) con offset tipo ±HH:MM */
function isoNoMsLocal(d: Date, offset = TZ_OFFSET): string {
    const sign = offset.startsWith("-") ? -1 : 1;
    const [hh, mm] = offset.slice(1).split(":").map(Number);
    const offsetMin = sign * (hh * 60 + (mm || 0));

    // “Local” = UTC + offset (sin tocar el instante real)
    const local = new Date(d.getTime() + offsetMin * 60_000);

    const pad = (n: number) => String(n).padStart(2, "0");
    const y = local.getUTCFullYear();
    const M = pad(local.getUTCMonth() + 1);
    const D = pad(local.getUTCDate());
    const h = pad(local.getUTCHours());
    const m = pad(local.getUTCMinutes());
    const s = pad(local.getUTCSeconds());

    return `${y}-${M}-${D}T${h}:${m}:${s}${offset}`;
}

function resolveLogDir(): string {
    const preferred = process.env.LOG_DIR || "/var/log/apireloj";
    try {
        fs.mkdirSync(preferred, { recursive: true });
        fs.accessSync(preferred, fs.constants.W_OK);
        return preferred;
    } catch {
        const fallback = path.resolve(process.cwd(), "logs");
        fs.mkdirSync(fallback, { recursive: true });
        return fallback;
    }
}

const DIR = resolveLogDir();
const file = path.join(DIR, "poller.log");

/**
 * Importante:
 * - Sobrescribimos "timestamp" para que el campo "time" sea ISO con offset y sin ms.
 * - Pino requiere que esta función devuelva una cadena que empiece con coma.
 */
export const log = pino(
    {
        level: process.env.LOG_LEVEL || "info",
        base: undefined,
        timestamp: () => `,"time":"${isoNoMsLocal(new Date())}"`,
    },
    pino.destination({ dest: file, append: true, mkdir: false, sync: false })
);

// (opcional) captura errores no manejados
process.on("unhandledRejection", (err) => log.error({ err }, "unhandledRejection"));
process.on("uncaughtException", (err) => {
    log.fatal({ err }, "uncaughtException");
    process.exit(1);
});
