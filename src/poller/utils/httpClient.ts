// src/poller/utils/httpClient.ts
import https from "https";
import fs from "fs";
import path from "path";

const INSECURE = process.env.RELOJ_TLS_INSECURE === "1";

// Acepta RELOJ_HOST con o sin esquema
const RAW_HOST = process.env.RELOJ_HOST || "";        // 186.48.x.x:8443 ó https://186.48.x.x:8443
const BASE_URL = RAW_HOST.startsWith("http") ? RAW_HOST : `https://${RAW_HOST}`;

// CA para modo seguro (opcional)
const CA_PATH =
    process.env.RELOJ_CA_CERT ||
    path.resolve(process.cwd(), "src/poller/certs/hikvision.crt");

// Agent según modo
let httpsAgent: https.Agent;
if (INSECURE) {
    httpsAgent = new https.Agent({
        rejectUnauthorized: false,
        // ignoramos CN/fecha en dev
        checkServerIdentity: () => undefined as any,
    });
} else {
    const opts: https.AgentOptions = { rejectUnauthorized: true };
    try { opts.ca = fs.readFileSync(CA_PATH); } catch {}
    httpsAgent = new https.Agent(opts);
}

// --- Carga perezosa de digest-fetch (ESM) desde CommonJS ---
let dfPromise: Promise<any> | null = null;
async function getDigestClient() {
    if (!dfPromise) {
        dfPromise = (async () => {
            const mod = await import("digest-fetch");         // ESM dinámico
            const DigestFetch = (mod as any).default ?? (mod as any);
            return new DigestFetch(
                process.env.RELOJ_USER || "admin",
                process.env.RELOJ_PASS || "",
                { algorithm: "MD5", basic: false }
            );
        })();
    }
    return dfPromise;
}

// Respuesta mínima para no chocar con tipos de axios
type HttpResponse<T = any> = { data: T; status: number };

async function digestJson<T = any>(
    method: "GET" | "POST",
    pathWithQuery: string,
    body?: any
): Promise<HttpResponse<T>> {
    const url = `${BASE_URL}${pathWithQuery}`;
    const df = await getDigestClient();

    const res = await df.fetch(url, {
        method,
        agent: httpsAgent as any,
        headers: {
            Accept: "application/json",
            ...(body ? { "Content-Type": "application/json" } : {}),
        },
        body: body ? JSON.stringify(body) : undefined,
    });

    const ct = res.headers.get("content-type") || "";
    const data: T = ct.includes("json") ? await res.json() : ((await res.text()) as any);
    return { data, status: res.status };
}

// ---- Endpoints ISAPI ----
export function getCapabilities(): Promise<HttpResponse<any>> {
    return digestJson("GET", "/ISAPI/AccessControl/AcsEvent/capabilities?format=json");
}

export function searchEvents<T = any>(body: any): Promise<HttpResponse<T>> {
    return digestJson("POST", "/ISAPI/AccessControl/AcsEvent?format=json", body);
}
