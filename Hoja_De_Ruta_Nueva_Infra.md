# Hikvision Poller (HTTPS + Digest) — Hoja de Ruta Técnica (v2)

> Terminal: **HIKVISION DS-K1T321MFWX** (Pro Series, multibiométrico)
> Objetivo: pasar de **push** a **polling** seguro sobre **HTTPS + Digest**, almacenar eventos en **PostgreSQL** y mantener trazabilidad.

---

## 🌐 Arquitectura (propuesta)

> **Aclaración importante:** PostgreSQL **no** se comunica con el router/NAT. El único flujo que atraviesa el NAT es el **poller ⇄ terminal** por **HTTPS**. El poller se comunica con PostgreSQL por red local/VPC.

```text
[hikvision-poller (Node.js)]
   ├─→ PostgreSQL (5432, LAN/VPC)
   └─→ HTTPS 8443→443 (Digest + TLS con CA)
         └─→ Router F660 (NAT)
               └─→ Terminal DS-K1T321MFWX (/ISAPI)
```

**Notas rápidas**

* PostgreSQL **no** se conecta al router; solo el **poller** habla con ambos.
* El NAT solo afecta el camino **poller ⇄ terminal**.
* Todo el tráfico hacia el terminal es **HTTPS (Digest)** validado con la **CA exportada**.

**Decisiones clave**

* **Poller** dedicado (servicio PM2) en vez de webhook push.
* **Digest obligatorio** y **TLS verificado** con el **CA exportado del equipo**.
* Polling **paginado** con *checkpoint* (idempotencia) para no reprocesar.

---

## 🔌 Endpoints ISAPI a usar

| # | Descripción             | Método | Ruta                                                     |
| - | ----------------------- | :----: | -------------------------------------------------------- |
| 1 | Capacidades de búsqueda |   GET  | `/ISAPI/AccessControl/AcsEvent/capabilities?format=json` |
| 2 | Búsqueda de eventos     |  POST  | `/ISAPI/AccessControl/AcsEvent?format=json`              |

**Cuerpo (ejemplo para #2)**

```json
{
  "AcsEventCond": {
    "searchID": "poll_2025-08-09_12:00",
    "searchResultPosition": 0,
    "maxResults": 30,
    "startTime": "2025-08-09T00:00:00+00:00",
    "endTime":   "2025-08-09T23:59:59+00:00",
    "major": 4,
    "minor": 38,
    "attendanceStatus": "checkIn"
  }
}
```

**Notas de contrato**

* `searchID`, `searchResultPosition` y `maxResults` **requeridos**.
* Fechas **ISO‑8601** con zona (la guía las documenta como **UTC**).
* `major`/`minor` se **envían en decimal** (p.ej. `0x26` → **38**).
* `attendanceStatus` aceptados: `checkIn`, `checkOut`, `breakOut`, `breakIn`, `overtimeIn`, `overTimeOut`.

**(Opcional, deshabilitado) Listeners push**
`/ISAPI/Event/notification/httpHosts` — mantener sin configurar salvo que se solicite.

---

## ⏱️ Polling y paginación

**¿Qué es *polling*?** Pedir periódicamente al dispositivo los eventos nuevos. El cliente (poller) inicia la comunicación, no el equipo.

**¿Qué es *paginación*?** Dividir un resultado grande en varias "páginas" usando `searchResultPosition` (offset) y `maxResults` (límite) dentro del `AcsEventCond`.

**¿Para qué sirve?**

* Evitar perder eventos cuando el buffer del equipo es limitado.
* Controlar carga de red/CPU.
* Reintentar ante estados **Device Busy (2)** sin duplicar registros.

**Algoritmo propuesto**

1. **Arranque**: `GET /ISAPI/AccessControl/AcsEvent/capabilities?format=json` para conocer límites (p. ej. `maxResults`).
2. *(Opcional)* Estimar volumen: `POST /ISAPI/AccessControl/AcsEventTotalNum?format=json` con la misma ventana de tiempo para saber cuántos eventos hay.
3. **Ventana** `[T0, T1]`: usar el último checkpoint (por ejemplo `T0 = last_event_time - 60s` para **solapamiento seguro**).
4. **Bucle de páginas**:

   * Enviar `POST /ISAPI/AccessControl/AcsEvent?format=json` con:

      * `searchID` único (por ejemplo `poll_<fecha>`),
      * `searchResultPosition = pos` (arranca en 0),
      * `maxResults = N` (dentro del rango de *capabilities*),
      * `startTime/endTime` en **UTC**,
      * filtros opcionales `major/minor` **en decimal** (el catálogo está en **hex**; ej.: `0x26` ⇒ `38` "Fingerprint Matched").
   * Insertar en BD con PK `(device_sn, serial_no)`.
   * `pos += resultados_devueltos`.
   * Si `resultados_devueltos < N`, **fin de página**.
5. **Checkpoint**: guardar `serial_no` y `event_time_utc` del último evento confirmado.

**Valores de asistencia** (si se usa `attendanceStatus`): `checkIn`, `checkOut`, `breakOut`, `breakIn`, `overtimeIn`, `overTimeOut`.

---

## 🧱 Esquema de datos sugerido (PostgreSQL)

```sql
CREATE TABLE IF NOT EXISTS hik_events (
  device_sn        text        NOT NULL,
  serial_no        bigint      NOT NULL,
  event_time_utc   timestamptz NOT NULL,
  employee_no      text,
  name             text,
  major            int,
  minor            int,
  attendance_status text,
  raw              jsonb       NOT NULL,
  PRIMARY KEY (device_sn, serial_no)
);

CREATE INDEX IF NOT EXISTS ix_hik_events_time ON hik_events(event_time_utc);
CREATE INDEX IF NOT EXISTS ix_hik_events_employee ON hik_events(employee_no);
```

> **Idempotencia**: clave primaria `(device_sn, serial_no)` evita duplicados aunque el poller reintente.

---

## 🔐 Cliente HTTP (Node)

> **Todo el tráfico es HTTPS**. Usamos el certificado **CA exportado del dispositivo** para validar TLS y **Digest** para autenticación.

* **TLS (HTTPS)**: `https.Agent` con `ca: fs.readFileSync('certs/hikvision.crt')` y `rejectUnauthorized: true`. La `RELOJ_HOST` debe ser `https://<ip_o_dns>:8443` (NAT hacia 443 del equipo).
* **Digest**: usar una librería que implemente RFC 7616 (por ejemplo `@mhoc/axios-digest-auth` o similar) sobre un cliente HTTP (axios/got/fetch) reutilizando el `https.Agent` anterior.
* **Timeouts**: conexión 5 s, respuesta 15 s.
* **Retries con backoff**: reintentar 2–4 veces para **Device Busy (2)**; abortar en **Invalid Operation (4)**/**Invalid Content (6)** y loguear.
* **Circuit‑breaker / Job‑lock**: evitar solapar corridas y saturar el equipo.

**Ejemplo (TypeScript, axios + digest):**

```ts
import fs from 'fs';
import https from 'https';
import axios from 'axios';
import Digest from '@mhoc/axios-digest-auth';

const httpsAgent = new https.Agent({
  ca: fs.readFileSync('src/certs/hikvision.crt'),
  rejectUnauthorized: true,
});

const digest = new Digest({
  username: process.env.RELOJ_USER!,
  password: process.env.RELOJ_PASS!,
});

const client = axios.create({ baseURL: process.env.RELOJ_HOST, httpsAgent, timeout: 15000 });

export async function postAcsEvent(body: unknown) {
  return digest.request(client, {
    method: 'post',
    url: '/ISAPI/AccessControl/AcsEvent?format=json',
    headers: { 'Content-Type': 'application/json' },
    data: body,
  });
}
```

---

## 🧩 Estructura del servicio

```text
src/
├─ certs/
│  └─ hikvision.crt
├─ utils/
│  ├─ httpClient.ts      # HTTPS + Digest + CA + retries
│  └─ logger.ts          # pino/winston
├─ services/
│  └─ eventPoller.ts     # orquesta capabilities + búsqueda + paginado + insert + checkpoint
├─ db/
│  └─ client.ts          # Pool PG
└─ index.ts              # arranque, cron/interval, /health local
```

---

## ⚙️ Variables de entorno

| Clave          | Ejemplo                       | Descripción                           |
| -------------- | ----------------------------- | ------------------------------------- |
| RELOJ\_HOST    | `https://190.134.247.11:8443` | Host + puerto público (NAT hacia 443) |
| RELOJ\_USER    | `admin`                       | Usuario del dispositivo               |
| RELOJ\_PASS    | `••••••••`                    | Contraseña del dispositivo            |
| PG\_URL        | `postgres://…`                | Cadena de conexión PostgreSQL         |
| POLL\_INTERVAL | `60000`                       | Intervalo en ms entre ventanas        |

---

## 🛡️ Seguridad operativa

* Rotar `admin` y restringir por firewall las IPs que pueden llegar al `:8443`.
* No exponer HTTP plano; sólo **HTTPS**.
* Mantener **`httpHosts` vacío** si no se usa push.
* Monitoreo básico: métricas de latencia, tasa de errores, eventos/minuto.

---

## 🧪 Pruebas rápidas

```bash
# Capabilities (LAN o a través de NAT 8443→443)
curl --digest -u "$RELOJ_USER:$RELOJ_PASS" \
  --cacert ./certs/hikvision.crt \
  "$RELOJ_HOST/ISAPI/AccessControl/AcsEvent/capabilities?format=json"

# Búsqueda mínima
curl --digest -u "$RELOJ_USER:$RELOJ_PASS" \
  --cacert ./certs/hikvision.crt \
  -H "Content-Type: application/json" \
  -d '{"AcsEventCond":{"searchID":"poll_test","searchResultPosition":0,"maxResults":10}}' \
  "$RELOJ_HOST/ISAPI/AccessControl/AcsEvent?format=json"
```

---

## 🧰 Operación (PM2)

`ecosystem.config.js`

```js
module.exports = {
  apps: [{
    name: "hikvision-poller",
    script: "dist/index.js",
    env: { NODE_ENV: "production" }
  }]
};
```

---

## 🚧 Roadmap incremental

**Fase 0 — Infra & Certificados**

* Exportar **CA** del dispositivo y guardarla en `src/certs/hikvision.crt`.
* Crear `.env.example` con `RELOJ_HOST=https://<IP_PUBLICA>:8443`, `RELOJ_USER`, `RELOJ_PASS`, `PG_URL`, `POLL_INTERVAL`.
* Verificar NAT `8443→443` y **lista blanca de IPs**.
  **Criterio de aceptación:** `curl --digest --cacert certs/hikvision.crt "$RELOJ_HOST/ISAPI/.../capabilities?format=json"` retorna **200**.

**Fase 1 — Cliente HTTPS + Digest**

* `utils/httpClient.ts`: `https.Agent` con `ca`, digest, **timeouts**, **retries con backoff**, logging.
* Endpoint `/health` local que haga un `GET .../capabilities` y reporte latencia.
  **Criterio:** dos requests consecutivos OK con certificados válidos y digest funcionando.

**Fase 2 — Esquema y migraciones**

* Crear tabla `hik_events` (ver sección *Esquema de datos*) + índices.
* Migración inicial (por ejemplo con `node-pg-migrate`/`knex`).
  **Criterio:** `SELECT COUNT(*) FROM hik_events;` ejecuta; PK evita duplicados.

**Fase 3 — Núcleo del Poller**

* Leer **capabilities** al arranque.
* *(Opcional)* `AcsEventTotalNum` para estimar volumen.
* Implementar ventana deslizante `[T0,T1]` con **solapamiento 60 s**.
* Paginación con `searchID`, `searchResultPosition`, `maxResults`.
* Insert idempotente y **checkpoint dual** (`serial_no`, `event_time_utc`).
  **Criterio:** al menos 100 eventos descargados sin duplicados tras reiniciar el servicio.

**Fase 4 — Operación**

* Archivo `ecosystem.config.js` (PM2), logs con rotación, métricas básicas.
* **Job‑lock** (por ejemplo `pg_advisory_lock`) para asegurar un poll activo.
  **Criterio:** reinicios no generan corridas en paralelo; logs muestran una sola instancia activa.

**Fase 5 — Filtros y mapping de negocio**

* Soporte de filtros `attendanceStatus`, `major/minor` (decimal) y mapping a tu dominio (ingreso/salida/descanso...).
  **Criterio:** query de reporte por `employee_no` y rango devuelve totales coherentes.

**Fase 6 — Pruebas y *hardening***

* Pruebas LAN/WAN; simulación de **2 – Device Busy**, **4 – Invalid Operation**, **6 – Invalid Content**.
* Validación de TLS (fallar si el cert no es confiable); desactivar HTTP plano.
  **Criterio:** dashboard de métricas estable; 0 duplicados en 24 h.

**Fase 7 — Limpieza push**

* Verificar que `httpHosts` esté vacío si no se usa push.
  **Criterio:** `GET /ISAPI/Event/notification/httpHosts` sin hosts configurados.

---

## 🩹 Troubleshooting (códigos típicos)

* **1 – OK**
* **2 – Device Busy** → reintentar con *backoff*, verificar ancho de banda.
* **3 – Device Error** → revisar estado del dispositivo; logs.
* **4 – Invalid Operation** → método HTTP incorrecto o permisos insuficientes.
* **6 – Invalid Content** → JSON inválido o parámetros fuera de rango.
* **401 – Unauthorized** → asegurar **Digest** activo.

---

## 📎 Anexos útiles

* Catálogo de tipos de evento (hex) para mapeo a **decimal** cuando se filtra por `minor` — ej. `0x26` = **38** (Fingerprint Matched).
* Ejemplos de `attendanceStatus` reconocidos por el firmware.

---

> Este documento **no cambia contenido técnico** respecto al anterior; solo mejora el **formato** y cierra correctamente los bloques de código para evitar que el resto del texto quede monoespaciado.
