// src/poller/logger.ts
import pino from "pino";
import fs from "fs";
import path from "path";

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

export const log = pino(
    { level: process.env.LOG_LEVEL || "info", base: undefined },
    pino.destination({ dest: file, append: true, mkdir: false, sync: false })
);

// (opcional) captura errores no manejados
process.on("unhandledRejection", (err) => log.error({ err }, "unhandledRejection"));
process.on("uncaughtException", (err) => { log.fatal({ err }, "uncaughtException"); process.exit(1); });
