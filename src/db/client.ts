// src/db/client.ts
import 'dotenv/config';
import { Pool } from 'pg';

const conn = process.env.PG_URL;
if (!conn) {
    throw new Error('PG_URL no está definida en .env');
}

export const db = new Pool({ connectionString: conn });

// Utilidad opcional para probar conexión
export async function assertDb(): Promise<boolean> {
    const r = await db.query<{ ok: number }>('select 1 as ok');
    return r.rows[0]?.ok === 1;
}
