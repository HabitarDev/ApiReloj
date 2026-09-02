# API Jornadas V1

Contrato vigente al 1 de septiembre de 2026.

## Propósito

Exponer jornadas semiprocesadas para que el backend calcule horas, sueldos, sanciones u otras reglas. Las jornadas son una proyección reconstruible de `AccessEvents`, no el registro fuente.

## Seguridad

Todos los endpoints de este documento requieren:

```http
X-Api-Key: <secreto-del-backend>
```

y conexión desde la IP fija autorizada.

## Consultar jornadas

```http
GET /Jornadas
```

### Query params

| Parámetro | Tipo | Default | Valores/regla |
|---|---|---:|---|
| `residentialId` | `string?` | — | Residencial existente. |
| `clockSn` | `string?` | — | DeviceSn principal de la jornada. |
| `employeeNumber` | `string?` | — | Empleado exacto. |
| `statusCheck` | `string?` | — | `OK`, `INCOMPLETE`, `ERROR`. |
| `statusBreak` | `string?` | — | `OK`, `INCOMPLETE`, `ERROR`, `NO_BREAK`. |
| `projectionStatus` | `string?` | — | Actualmente sólo `READY`. |
| `includeDeleted` | `bool` | `false` | Incluye tombstones. |
| `fromUtc` | `DateTimeOffset?` | — | Debe venir con `toUtc`. |
| `toUtc` | `DateTimeOffset?` | — | Debe venir con `fromUtc`. |
| `updatedSinceUtc` | `DateTimeOffset?` | — | Permite sincronización incremental. |
| `limit` | `int` | `100` | Mayor que cero. |
| `offset` | `int` | `0` | Mayor o igual a cero. |

Los estados se normalizan a mayúsculas. Los filtros se combinan con `AND`.

### Respuesta

```json
[
  {
    "jornadaId": "01J2...",
    "employeeNumber": "EMP-7",
    "residentialId": "RES-001",
    "clockSn": "CLOCK-SN-01",
    "startAt": "2026-07-15T11:00:00Z",
    "breakInAt": "2026-07-15T15:00:00Z",
    "breakOutAt": "2026-07-15T15:30:00Z",
    "endAt": "2026-07-15T20:00:00Z",
    "statusCheck": "OK",
    "statusBreak": "OK",
    "warnings": [],
    "errors": [],
    "projectionStatus": "READY",
    "revision": 2,
    "isDeleted": false,
    "startDeviceSn": "CLOCK-SN-01",
    "startSerialNumber": 100,
    "breakInDeviceSn": "CLOCK-SN-02",
    "breakInSerialNumber": 101,
    "breakOutDeviceSn": "CLOCK-SN-02",
    "breakOutSerialNumber": 102,
    "endDeviceSn": "CLOCK-SN-03",
    "endSerialNumber": 103,
    "createdAt": "2026-07-15T11:00:02Z",
    "updatedAt": "2026-07-15T20:00:02Z"
  }
]
```

### Consumo incremental recomendado

El backend debe conservar `jornadaId + revision` y consultar periódicamente con `updatedSinceUtc` e `includeDeleted=true`.

- Una revisión mayor reemplaza la versión anterior.
- `isDeleted=true` elimina lógicamente la jornada del consumidor.
- `projectionStatus=READY` indica que esa fila fue producida por una reconstrucción completada.

No se debe asumir que una jornada es inmutable después de cerrada: un evento histórico puede corregirla.

## Solicitar reconstrucción

```http
POST /admin/jornadas/rebuild
Content-Type: application/json
X-Api-Key: <secreto-del-backend>
```

```json
{
  "employeeNumber": "EMP-7",
  "residentialId": "RES-001",
  "fromUtc": "2026-07-01T00:00:00Z"
}
```

`fromUtc` es opcional y se guarda como punto de suciedad/auditoría. La reconstrucción vigente carga todos los eventos de la clave empleado + residencial para garantizar un resultado determinista.

Respuesta `202 Accepted`:

```json
{
  "status": "queued",
  "employeeNumber": "EMP-7",
  "residentialId": "RES-001",
  "dirtyFromUtc": "2026-07-01T00:00:00Z"
}
```

## Consultar estados de proyección

```http
GET /admin/jornadas/projection-states?status=ERROR&limit=100&offset=0
```

Estados persistidos: `PENDING`, `PROCESSING`, `READY`, `ERROR`.

```json
[
  {
    "employeeNumber": "EMP-7",
    "residentialId": "RES-001",
    "dirtyFromUtc": "2026-07-01T00:00:00Z",
    "status": "ERROR",
    "requestedRevision": 4,
    "appliedRevision": 3,
    "attempts": 2,
    "lastError": "...",
    "nextAttemptAtUtc": "2026-07-15T11:01:00Z",
    "updatedAtUtc": "2026-07-15T11:00:50Z"
  }
]
```

Detalles actuales del endpoint:

- `limit` se limita silenciosamente al rango `1..1000`.
- `offset` negativo se trata como cero.
- `status` se compara exactamente con el valor persistido; usar mayúsculas.

## Reglas de reconstrucción

- Identidad de procesamiento: empleado + residencial.
- Orden: `EventTimeUtc`, `SerialNumber`, `DeviceSn`.
- Un empleado puede entrar por un reloj y salir por otro del mismo residencial.
- Una sola pausa por jornada.
- Duración máxima rígida de 24 horas.
- La primera doble marca se conserva y las posteriores generan warning.
- Una marca posterior a una jornada vencida comienza o afecta otra jornada; no se agrega a la vencida.

Códigos de issues posibles:

- `DUPLICATE_CHECK_IN_IGNORED`
- `DUPLICATE_CHECK_OUT_IGNORED`
- `DUPLICATE_BREAK_IN_IGNORED`
- `DUPLICATE_BREAK_OUT_IGNORED`
- `SECOND_BREAK_IGNORED`
- `MISSING_CHECK_IN`
- `MISSING_CHECK_OUT`
- `MISSING_BREAK_IN`
- `MISSING_BREAK_OUT`
- `MAXIMUM_DURATION_EXCEEDED`

## Consistencia

La inserción del evento y el encolado son atómicos. La jornada es eventualmente consistente: puede demorarse algunos segundos después de un push o poll. Los errores se reintentan con backoff y quedan visibles en `projection-states`.

## Errores

| Código | Caso |
|---:|---|
| `200` | Consulta correcta. |
| `202` | Reconstrucción encolada. |
| `400` | Query, estado, body o rango inválido. |
| `401` | API key inválida. |
| `403` | IP no autorizada. |
| `404` | Residencial inexistente en consultas. |
| `422` | Regla de negocio. |
| `429` | Límite de concurrencia. |
| `500` | Error inesperado. |

## Compatibilidad ISAPI

ISAPI no conoce `residentialId`. ApiReloj lo deriva del reloj autenticado en push o consultado en poll. No cambian payloads, rutas, paginación, deduplicación ni `_raw`.
