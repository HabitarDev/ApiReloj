# API completa de ApiReloj V1

**Contrato revisado contra código:** 1 de septiembre de 2026.

Este documento es la referencia rápida de todos los endpoints activos. Las guías especializadas de la misma carpeta amplían eventos, jornadas, polling y seguridad.

## 1. Convenciones

### URL base

```text
https://<host-api>
```

No existe prefijo `/api` ni `/api/v1`.

### JSON

ASP.NET Core serializa propiedades con camelCase salvo que el nombre del DTO comience con `_`, caso en el que se conserva. Los IDs maestros son string.

### Fechas

- Fechas HTTP: ISO 8601 con `Z` u offset.
- Heartbeat: Unix epoch en segundos.
- El servidor normaliza eventos a UTC.

### Seguridad

| Familia | Autenticación |
|---|---|
| Heartbeat | HMAC en el body original; no usa API key. |
| Push | IP actual del residencial del reloj; no usa API key. |
| Todas las demás | `X-Api-Key` válido + IP fija del backend. |

Errores de seguridad comunes:

- `401`: credencial o autenticación de flujo inválida.
- `403`: backend autenticado desde IP no permitida.
- `429`: rate limit o concurrencia agotada.

### Errores de aplicación

El filtro global devuelve `ProblemDetails` para excepciones de aplicación:

```json
{
  "type": "about:blank",
  "title": "Argumento inválido",
  "status": 400,
  "detail": "..."
}
```

Mapeo principal: `400` argumento, `404` no encontrado, `409` conflicto, `422` regla de negocio y `500` inesperado. Residential, Device y Reloj usan `404` para inexistentes o dependencias ausentes y `409` para duplicados.

## 2. Mapa de endpoints

| Consumidor | Método y ruta | Propósito |
|---|---|---|
| Emisor | `POST /Residential/heartbeat` | Actualizar IP y última actividad. |
| Reloj | `POST /AccessEvents/push/{relojId}` | Ingestar evento ISAPI. |
| Backend | `GET /AccessEvents` | Consultar eventos locales. |
| Backend | `GET /Jornadas` | Consultar jornadas derivadas. |
| Backend | `POST /admin/jornadas/rebuild` | Encolar reconstrucción. |
| Backend | `GET /admin/jornadas/projection-states` | Diagnosticar proyecciones. |
| Backend | `POST /admin/poll/run` | Ejecutar polling manual. |
| Backend | `GET /admin/poll/status` | Estado del polling. |
| Backend | `GET /admin/poll/runs` | Historial de corridas. |
| Backend | `GET /admin/poll/runs/{runId}` | Detalle de corrida. |
| Backend | `POST /UsersControllers` | Alta en todos los relojes del residencial. |
| Backend | `PUT /UsersControllers` | Modificación en todos los relojes. |
| Backend | `DELETE /UsersControllers` | Baja en todos los relojes. |
| Backend | `GET /Residential` | Listar residenciales. |
| Backend | `GET /Residential/{id}` | Obtener residencial. |
| Backend | `POST /Residential` | Crear residencial. |
| Backend | `GET /Device` | Listar devices sin secretos. |
| Backend | `GET /Device/{id}` | Obtener device sin secreto. |
| Backend | `POST /Device` | Crear device con secreto write-only. |
| Backend | `GET /Reloj` | Listar relojes. |
| Backend | `GET /Reloj/{id}` | Obtener reloj. |
| Backend | `POST /Reloj` | Crear reloj. |
| Backend | `PUT /Reloj` | Actualizar puerto y DeviceSn. |

## 3. Heartbeat

### `POST /Residential/heartbeat`

```http
Content-Type: application/json
```

```json
{
  "deviceId": "DEVICE-001",
  "residentialId": "RES-001",
  "timeStamp": 1784067600,
  "signature": "a3f5..."
}
```

La firma es hexadecimal HMAC-SHA256 sobre:

```text
timeStamp|deviceId|residentialId
```

Validaciones previas al controlador:

- body JSON y tamaño permitido;
- device y residencial existentes y relacionados;
- timestamp dentro de la ventana configurada;
- firma correcta.

La mutación final es atómica y sólo acepta timestamps mayores al último aceptado.

| Código | Significado |
|---:|---|
| `204` | Aceptado, o replay válido ignorado sin cambios. |
| `401` | Contenido, identidad, timestamp o firma inválidos. |
| `429` | Límite por IP o global. |

El emisor conserva este body; no debe migrar la autenticación a headers.

## 4. Push ISAPI

### `POST /AccessEvents/push/{relojId}`

Content-Type aceptados:

- `application/json`
- `application/xml`
- `text/xml`
- `multipart/form-data`

En multipart se buscan campos `Event_Type`, `event_type`, `eventType`, `EventType` o `event`; si no aparecen, se usa el primer valor no vacío. Los archivos indican `hasPicture`, pero no se guardan como binario dentro del evento.

La IP origen debe coincidir con el residencial del reloj. Si el payload incluye `deviceID`, debe coincidir con el `DeviceSn` configurado.

Respuesta `200`:

```json
{
  "status": "inserted",
  "reason": null,
  "eventType": "AccessControllerEvent",
  "serialNo": 123456,
  "deviceSn": "CLOCK-SN-01",
  "eventTimeUtc": "2026-07-15T11:00:00Z"
}
```

`status` puede ser `inserted`, `duplicate` o `ignored`. Un evento insertado y la solicitud de reconstrucción de jornadas se conservan en una sola transacción.

| Código | Significado |
|---:|---|
| `200` | Payload procesado, incluso duplicate o ignored. |
| `400` | Body vacío, multipart sin payload o parseo inválido. |
| `401` | Reloj/IP/residencial no autorizados. |
| `422` | Regla de negocio. |
| `429` | Límite global de concurrencia. |
| `500` | Error inesperado. |

## 5. Eventos

### `GET /AccessEvents`

Requiere autenticación backend.

Query params:

- `residentialId: string?`
- `deviceSn: string?`
- `employeeNumber: string?`
- `major: int?`
- `minor: int?`
- `attendanceStatus: string?`
- `fromUtc: DateTimeOffset?`
- `toUtc: DateTimeOffset?`
- `limit: int = 100`
- `offset: int = 0`

`fromUtc` y `toUtc` deben venir juntos. Respuesta:

```json
[
  {
    "_deviceSn": "CLOCK-SN-01",
    "_serialNumber": 10001,
    "_eventTimeUtc": "2026-07-15T11:00:00Z",
    "_timeDevice": "2026-07-15T08:00:00-03:00",
    "_employeeNumber": "EMP-7",
    "_major": 5,
    "_minor": 38,
    "_attendanceStatus": "checkIn",
    "_raw": "{...}",
    "_residentialId": "RES-001"
  }
]
```

Cuando se especifica `residentialId`, el filtro se aplica directamente sobre la pertenencia persistida al ingerir el evento. No depende de que el reloj siga existiendo o conserve su residencial actual. El orden estable es `EventTimeUtc`, `SerialNumber` y `DeviceSn`, todos descendentes. Históricos sin pertenencia demostrable permanecen en `__legacy__`.

Ver `api_access_events_v1.md`.

## 6. Jornadas

### `GET /Jornadas`

Query params:

- `residentialId: string?`
- `clockSn: string?`
- `employeeNumber: string?`
- `statusCheck: OK | INCOMPLETE | ERROR`
- `statusBreak: OK | INCOMPLETE | ERROR | NO_BREAK`
- `projectionStatus: READY`
- `includeDeleted: bool = false`
- `fromUtc`, `toUtc`
- `updatedSinceUtc`
- `limit: int = 100`
- `offset: int = 0`

Respuesta abreviada:

```json
[
  {
    "jornadaId": "01J2...",
    "employeeNumber": "EMP-7",
    "residentialId": "RES-001",
    "clockSn": "CLOCK-SN-01",
    "startAt": "2026-07-15T11:00:00Z",
    "breakInAt": null,
    "breakOutAt": null,
    "endAt": null,
    "statusCheck": "INCOMPLETE",
    "statusBreak": "NO_BREAK",
    "warnings": [],
    "errors": [],
    "projectionStatus": "READY",
    "revision": 1,
    "isDeleted": false,
    "startDeviceSn": "CLOCK-SN-01",
    "startSerialNumber": 100,
    "breakInDeviceSn": null,
    "breakInSerialNumber": null,
    "breakOutDeviceSn": null,
    "breakOutSerialNumber": null,
    "endDeviceSn": null,
    "endSerialNumber": null,
    "createdAt": "2026-07-15T11:00:02Z",
    "updatedAt": "2026-07-15T11:00:02Z"
  }
]
```

Las jornadas son eventualmente consistentes y reconstruibles. Para sincronización usar `updatedSinceUtc`, revisiones e `includeDeleted=true`.

### `POST /admin/jornadas/rebuild`

```json
{
  "employeeNumber": "EMP-7",
  "residentialId": "RES-001",
  "fromUtc": "2026-07-01T00:00:00Z"
}
```

Devuelve `202`:

```json
{
  "status": "queued",
  "employeeNumber": "EMP-7",
  "residentialId": "RES-001",
  "dirtyFromUtc": "2026-07-01T00:00:00Z"
}
```

### `GET /admin/jornadas/projection-states`

Query: `status`, `limit=100`, `offset=0`. Estados: `PENDING`, `PROCESSING`, `READY`, `ERROR`.

```json
[
  {
    "employeeNumber": "EMP-7",
    "residentialId": "RES-001",
    "dirtyFromUtc": null,
    "status": "READY",
    "requestedRevision": 2,
    "appliedRevision": 2,
    "attempts": 0,
    "lastError": null,
    "nextAttemptAtUtc": null,
    "updatedAtUtc": "2026-07-15T11:00:02Z"
  }
]
```

Ver `api_jornadas_v1.md` y `procesamiento_jornadas_concurrente.md`.

## 7. Poll y backfill

### `POST /admin/poll/run`

Body opcional:

```json
{
  "residentialId": "RES-001",
  "relojId": "CLOCK-001"
}
```

Ejecuta la corrida sincrónicamente y devuelve `BackfillPollRunResultDto`. Una corrida concurrente dentro del mismo proceso produce `409`.

El snapshot completo de relojes se persiste en `pending` antes de llamar ISAPI y cada resultado se guarda al avanzar. En estado `running`, `finishedAtUtc` es `null`; un run terminal tiene fecha y ningún reloj pendiente. Al arrancar, los runs interrumpidos se recuperan idempotentemente como `error`.

### `GET /admin/poll/status`

Devuelve `BackfillPollStatusDto` con la ejecución actual y última corrida conocida.

### `GET /admin/poll/runs`

Query:

- `status`: `running`, `ok`, `partial_error`, `error`.
- `limit=50`.
- `offset=0`.

### `GET /admin/poll/runs/{runId}`

Devuelve detalle y métricas por reloj. Un run inexistente responde `404`.

Ver `api_poll_backfill_v1.md`.

## 8. Usuarios y proxy ISAPI

Los tres endpoints hacen fan-out a todos los relojes del residencial y devuelven el mismo DTO recibido si todos terminan correctamente. Usan Digest si están disponibles `ISAPI_USER` e `ISAPI_PASSWORD`.

No hay rollback distribuido: un error posterior no revierte relojes ya actualizados.

### `POST /UsersControllers`

```json
{
  "_employeeNo": "EMP-7",
  "_name": "Ana Pérez",
  "_userType": "normal",
  "_beginTime": "2000-01-01T00:00:00",
  "_endTime": "2037-12-31T23:59:59",
  "_enable": true,
  "_timeType": "local",
  "_residentialId": "RES-001"
}
```

ISAPI: `PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json`.

### `PUT /UsersControllers`

Usa el mismo shape; los campos del DTO de modificación son obligatorios. ISAPI: `UserInfo/Modify?format=json`.

### `DELETE /UsersControllers`

```json
{
  "_employeeNo": "EMP-7",
  "_residentialId": "RES-001"
}
```

ISAPI: `PUT /ISAPI/AccessControl/UserInfoDetail/Delete?format=json` con `mode=byEmployeeNo`.

Una dependencia maestra inexistente responde `404`. Un fallo inesperado de red o ISAPI responde `500` sin exponer secretos ni stack trace.

## 9. Residential

### `GET /Residential`

Devuelve `ResidentialDto[]`:

```json
[
  {
    "_idResidential": "RES-001",
    "_ipActual": "203.0.113.10",
    "_relojes": [],
    "_devices": []
  }
]
```

Los devices anidados nunca incluyen `_secretKey`.

### `GET /Residential/{id}`

Devuelve un `ResidentialDto`. Si no existe responde `404`.

### `POST /Residential`

```json
{
  "idResidential": "RES-001",
  "ipActual": "0.0.0.0"
}
```

Devuelve el residencial creado con `200`. Un ID duplicado responde `409`.

## 10. Device

### `GET /Device` y `GET /Device/{id}`

Devuelven `DeviceResponseDto`, sin secreto:

```json
{
  "_deviceId": "DEVICE-001",
  "_lastSeen": "2026-07-15T11:00:00Z",
  "_residentialId": "RES-001"
}
```

### `POST /Device`

```json
{
  "_deviceId": "DEVICE-001",
  "_secretKey": "secreto-hmac",
  "_lastSeen": null,
  "_residentialId": "RES-001"
}
```

La respuesta omite `_secretKey`. El backend debe tratar el secreto como write-only.

## 11. Reloj

### `GET /Reloj` y `GET /Reloj/{id}`

```json
{
  "_idReloj": "CLOCK-001",
  "_puerto": 80,
  "_residentialId": "RES-001",
  "_deviceSn": "CLOCK-SN-01"
}
```

### `POST /Reloj`

```json
{
  "_idReloj": "CLOCK-001",
  "_puerto": 80,
  "_residentialId": "RES-001"
}
```

### `PUT /Reloj`

```json
{
  "_idReloj": "CLOCK-001",
  "_puerto": 80,
  "_deviceSn": "CLOCK-SN-01"
}
```

Es el camino administrativo para configurar el DeviceSn usado por push, poll y jornadas.

## 12. Flujo de aprovisionamiento recomendado

1. Crear Residential.
2. Crear Device y custodiar su secreto.
3. Crear Reloj.
4. Actualizar Reloj con DeviceSn.
5. Configurar el emisor heartbeat sin cambiar su body.
6. Configurar el push del reloj con `/AccessEvents/push/{relojId}`.
7. Verificar `GET /AccessEvents`.
8. Verificar `GET /admin/jornadas/projection-states` y `GET /Jornadas`.
9. Ejecutar poll manual si se requiere backfill.

Todos los pasos administrativos llevan `X-Api-Key` y deben salir desde la IP autorizada.

## 13. Compatibilidad y límites

- Rutas legacy activas; no existe `/api/v1`.
- Push y poll conservan ISAPI.
- Heartbeat conserva su body JSON.
- `GET /AccessEvents` y `GET /Jornadas` leen sólo BD local.
- El backend calcula horas y remuneraciones; ApiReloj entrega datos semiprocesados.
- Las jornadas pueden cambiar por backfill y deben sincronizarse por revisión.
- Tras Traefik sólo se procesan `X-Forwarded-For` y `X-Forwarded-Proto` desde proxies o redes configurados explícitamente, con un límite de saltos acotado.
- ApiReloj requiere exactamente una réplica porque la exclusión mutua de poll es por proceso.
- No existen callbacks desde ApiReloj hacia QUIEVO.
