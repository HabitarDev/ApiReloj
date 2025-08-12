// src/poller/utils/httpClient.ts
import fs from "fs";
import path from "path";
import https from "https";
import axios, { AxiosInstance, AxiosResponse } from "axios";
import crypto from "crypto";

const INSECURE = process.env.RELOJ_TLS_INSECURE === "1";

// HOST puede venir con o sin esquema (acepta "https://IP:PORT" o "IP:PORT")
const RAW_HOST = process.env.RELOJ_HOST || "";
export const BASE_URL = RAW_HOST.startsWith("http") ? RAW_HOST : `https://${RAW_HOST}`;

// CA opcional (solo si NO estás en modo inseguro)
const CA_PATH =
    process.env.RELOJ_CA_CERT ||
    path.resolve(process.cwd(), "src/poller/certs/hikvision.crt");

// Agent TLS (seguro o inseguro)
const httpsAgent = INSECURE
    ? new https.Agent({ rejectUnauthorized: false, checkServerIdentity: () => undefined })
    : (() => {
        const opts: https.AgentOptions = { rejectUnauthorized: true };
        try { opts.ca = fs.readFileSync(CA_PATH); } catch {}
        return new https.Agent(opts);
    })();

// Axios base (dejamos validateStatus para poder leer el 401 del reto Digest)
const client: AxiosInstance = axios.create({
    baseURL: BASE_URL,
    httpsAgent,
    timeout: 15000,
    validateStatus: () => true,
});

const USER = process.env.RELOJ_USER || "admin";
const PASS = process.env.RELOJ_PASS || "";

// --- helpers digest ---
const md5 = (s: string) => crypto.createHash("md5").update(s).digest("hex");

function parseDigest(h: string): Record<string, string> {
    // toma el primer “Digest ...”
    const m = /Digest\s+(.*)/i.exec(h) || [];
    const params: Record<string, string> = {};
    (m[1] || h)
        .split(/,\s*/)
        .map(x => x.trim())
        .forEach(kv => {
            const mm = /(\w+)=("?)(.+?)\2$/.exec(kv);
            if (mm) params[mm[1]] = mm[3];
        });
    return params;
}

async function requestWithDigest(
    method: "GET" | "POST",
    urlPathAndQuery: string,            // ¡usar path + query, sin baseURL!
    body?: any,
    extraHeaders: Record<string, string> = {}
): Promise<AxiosResponse<any>> {

    // 1) primer intento para obtener WWW-Authenticate
    const first = await client.request({
        method,
        url: urlPathAndQuery,
        data: body,
        headers: { Accept: "application/json", ...extraHeaders },
    });

    const www = first.headers["www-authenticate"] as string | undefined;
    if (first.status !== 401 || !www || !/Digest/i.test(www)) {
        // o ya respondió 200, o falló por otra causa
        return first;
    }

    // 2) calculamos el header Digest y reintentamos
    const d = parseDigest(www);
    const realm = d.realm || "";
    const nonce = d.nonce || "";
    const qop = (d.qop || "auth").split(",")[0].trim();
    const opaque = d.opaque;

    const uri = urlPathAndQuery;                     // incluir ?format=json etc.
    const cnonce = crypto.randomBytes(8).toString("hex");
    const nc = "00000001";

    const ha1 = md5(`${USER}:${realm}:${PASS}`);
    const ha2 = md5(`${method}:${uri}`);
    const response = qop
        ? md5(`${ha1}:${nonce}:${nc}:${cnonce}:${qop}:${ha2}`)
        : md5(`${ha1}:${nonce}:${ha2}`);

    let auth =
        `Digest username="${USER}", realm="${realm}", nonce="${nonce}", uri="${uri}", ` +
        (qop ? `qop=${qop}, nc=${nc}, cnonce="${cnonce}", ` : "") +
        `algorithm=MD5, response="${response}"`;
    if (opaque) auth += `, opaque="${opaque}"`;

    return client.request({
        method,
        url: urlPathAndQuery,
        data: body,
        headers: {
            Accept: "application/json",
            Authorization: auth,
            ...(method === "POST" ? { "Content-Type": "application/json" } : {}),
            ...extraHeaders,
        },
    });
}

// --- API helpers públicos ---
export async function getCapabilities() {
    // GET /ISAPI/AccessControl/AcsEvent/capabilities?format=json
    // (endpoint documentado por Hikvision). :contentReference[oaicite:0]{index=0}
    return requestWithDigest("GET", "/ISAPI/AccessControl/AcsEvent/capabilities?format=json");
}

export async function searchEvents(body: any) {
    // POST /ISAPI/AccessControl/AcsEvent?format=json (consulta de eventos). :contentReference[oaicite:1]{index=1}
    return requestWithDigest("POST", "/ISAPI/AccessControl/AcsEvent?format=json", body);
}
