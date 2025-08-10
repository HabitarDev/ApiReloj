import fs from "fs";
import https from "https";
import axios, { AxiosRequestConfig, AxiosResponse } from "axios";
import AxiosDigestAuth from "@mhoc/axios-digest-auth";

// HTTPS agent: valida contra el CA exportado del equipo
export const httpsAgent = new https.Agent({
    ca: fs.readFileSync("src/poller/certs/hikvision.crt"),
    rejectUnauthorized: true,
});

// OJO: no pasamos nuestra instancia de axios al constructor de Digest
// (evita choques de tipos entre axios de tu proyecto y el axios que usa la lib)
const digest = new AxiosDigestAuth({
    username: process.env.RELOJ_USER!,
    password: process.env.RELOJ_PASS!,
});

const baseURL = process.env.RELOJ_HOST!;

// Helper con tipado estable: añadimos baseURL/httpsAgent y casteamos la respuesta
async function digestRequest<T = any>(
    cfg: AxiosRequestConfig
): Promise<AxiosResponse<T>> {
    const req: AxiosRequestConfig = { baseURL, httpsAgent, ...cfg };
    // La lib devuelve tipos incompatibles con axios@1.x → cast seguro
    const res = (await digest.request(req as any)) as unknown as AxiosResponse<T>;
    return res;
}

// --- Endpoints ---

export async function getCapabilities(): Promise<AxiosResponse<any>> {
    return digestRequest({
        method: "GET",
        url: "/ISAPI/AccessControl/AcsEvent/capabilities?format=json",
    });
}

export async function searchEvents<T = any>(
    body: unknown
): Promise<AxiosResponse<T>> {
    return digestRequest<T>({
        method: "POST",
        url: "/ISAPI/AccessControl/AcsEvent?format=json",
        // headers de axios@1 (la lib usa otra firma, por eso casteamos):
        headers: { "Content-Type": "application/json" } as any,
        data: body,
    });
}
