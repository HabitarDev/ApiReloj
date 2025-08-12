// src/poller/logger.ts
import pino from "pino";
import fs from "fs";
import path from "path";

const LOG_DIR = process.env.LOG_DIR || "/var/log/apireloj";
const LOG_FILE = process.env.LOG_FILE || "poller.log";
fs.mkdirSync(LOG_DIR, { recursive: true });

export const log = pino({
    level: process.env.LOG_LEVEL || "info",
    base: undefined,
}, pino.destination({ dest: path.join(LOG_DIR, LOG_FILE), sync: false }));
