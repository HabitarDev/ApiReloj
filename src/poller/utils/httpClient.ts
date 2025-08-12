// src/poller/utils/httpClient.ts
import fs from "fs";
import path from "path";
import https from "https";
import axios from "axios";
import AxiosDigestAuth from "@mhoc/axios-digest-auth";
import crypto from "crypto";
import type { AxiosResponse } from "axios";  // <— NUEVO

const INSECURE = process.env.RELOJ_TLS_INSECURE === "1";

// BaseURL: acepta RELOJ_HOST con o sin esquema
const RAW_HOST = process.env.RELOJ_HOST || "";        // ej: 167.62.x.y:8443 o https://167.62.x.y:8443
const BASE_URL = RAW_HOST.startsWith("http") ? RAW_HOST : `https://${RAW_HOST}`;

// CA solo para modo seguro
const CA_PATH =
    process.env.RELOJ_CA_CERT ||
    path.resolve(process.cwd(), "src/poller/certs/hikvision.crt");

// https.Agent según modo
const httpsAgent = INSECURE
    ? new https.Agent({
        rejectUnauthorized: false,
        checkServerIdentity: () => undefined,
    })
    : new https.Agent({
        ca: fs.readFileSync(CA_PATH),
        rejectUnauthorized: true,
    });

// Nuestra instancia de axios (para timeout y pinning)
const client = axios.create({
    baseURL: BASE_URL,
    httpsAgent,
    timeout: 15000,
});

// Digest: pasamos el axios instance como any para evitar choque de tipos
export const digest = new AxiosDigestAuth({
    username: process.env.RELOJ_USER || "admin",
    password: process.env.RELOJ_PASS || "",
    axios: client as any,
});

// Pinning opcional (recomendado si usas INSECURE)
const EXPECTED_FP = (process.env.RELOJ_TLS_FP || "").toLowerCase();
client.interceptors.response.use((resp) => {
    if (INSECURE && EXPECTED_FP) {
        const cert: any = resp?.request?.socket?.getPeerCertificate?.(true);
        if (!cert || !cert.raw) throw new Error("TLS pinning: certificado ausente");
        const got = crypto.createHash("sha256").update(cert.raw).digest("hex");
        if (got !== EXPECTED_FP) {
            throw new Error(`TLS pin mismatch: got=${got} expected=${EXPECTED_FP}`);
        }
    }
    return resp;
});

// ---- Helpers: tipo mínimo para evitar choque de Axios ----
type HttpResponse<T> = { data: T };

// Capabilities
export async function getCapabilities(): Promise<HttpResponse<any>> {
    const resp = await digest.request({
        method: "GET",
        url: "/ISAPI/AccessControl/AcsEvent/capabilities?format=json",
    } as any);
    // devolvemos un shape con { data } sin tipar con AxiosResponse
    return resp as unknown as HttpResponse<any>;
}

// Búsqueda de eventos
export async function searchEvents<T = any>(body: any): Promise<HttpResponse<T>> {
    const resp = await digest.request({
        method: "POST",
        url: "/ISAPI/AccessControl/AcsEvent?format=json",
        headers: { "Content-Type": "application/json" },
        data: body,
    } as any);
    return resp as unknown as HttpResponse<T>;
}