# API Poll Backfill V1

Contrato vigente al 1 de septiembre de 2026.

## Objetivo

Recuperar eventos históricos y cubrir huecos que el push no haya entregado. Poll y push escriben el mismo modelo, usan la misma idempotencia y activan la misma reconstrucción de jornadas.

## Seguridad administrativa

Los cuatro endpoints `/admin/poll/*` requieren `X-Api-Key` y la IP fija del backend.

## ISAPI utilizado

```http
POST http(s)://{Residential.IpActual}:{Reloj.Puerto}/ISAPI/AccessControl/AcsEvent?format=json
```

Se usa HTTPS cuando el puerto es `443` o `8443`; en los demás puertos se usa HTTP. Si existen `ISAPI_USER` e `ISAPI_PASSWORD`, el cliente responde al challenge Digest.

Condiciones principales enviadas:

- `searchID` único por ventana;
- `searchResultPosition` para paginar;
- `maxResults` configurable;
- `startTime` y `endTime`;
- `timeReverseOrder=false`;
- `isAttendanceInfo=true`.

## Cursores y ventanas

### Sin `LastPollEvent`

1. Consulta desde `BootstrapStartUtc` hasta ahora con un resultado en orden ascendente.
2. Si encuentra historia, divide desde el evento más antiguo hasta ahora.
3. Si no hay eventos, fija el cursor en ahora y termina.

### Con cursor

- Si el gap es mayor a `WindowMinutes`, procesa ventanas consecutivas desde el cursor.
- Si el gap es menor o igual, vuelve a consultar la ventana reciente `[now-window, now]` como seguridad.

El cursor avanza al final de cada ventana completada. Si una página o ventana falla, esa ventana no se considera completada.

`MaxWindowsPerRun` limita catch-ups excesivos y provoca error si se supera.

## Paginación e idempotencia

Mientras ISAPI devuelva `responseStatusStrg=MORE`, el cliente incrementa `searchResultPosition` por `numOfMatches`. Cada página se ingiere por `DeviceSn + SerialNumber`.

Métricas:

- `inserted`: evento nuevo;
- `duplicates`: ya existía;
- `ignored`: no utilizable para acceso/jornadas.

## Ejecutar corrida manual

```http
POST /admin/poll/run
Content-Type: application/json
X-Api-Key: <secreto>
```

Body opcional:

```json
{
  "residentialId": "RES-001",
  "relojId": "CLOCK-001"
}
```

El controlador fuerza `trigger=manual`. Sin filtros se consideran todos los relojes. Sólo puede existir una corrida por proceso; una segunda recibe `409`.

Respuesta resumida:

```json
{
  "runId": "d42f...",
  "trigger": "manual",
  "startedAtUtc": "2026-07-15T10:00:00Z",
  "finishedAtUtc": "2026-07-15T10:00:05Z",
  "status": "ok",
  "error": null,
  "totalClocks": 2,
  "totalWindows": 3,
  "totalPages": 4,
  "inserted": 10,
  "duplicates": 2,
  "ignored": 1,
  "clocks": []
}
```

Estados de corrida: `running`, `ok`, `partial_error`, `error`. Los estados por reloj son `pending`, `ok`, `skipped` y `error`. Un reloj no configurado puede quedar `skipped` dentro del detalle sin convertir toda la corrida en error. Si fallan algunos relojes el run termina en `partial_error`; si fallan todos termina en `error`.

Invariantes temporales:

- `running` implica `finishedAtUtc: null`;
- un estado terminal implica `finishedAtUtc` no nulo;
- un resultado terminal nunca conserva relojes `pending`.

Antes de la primera llamada ISAPI, ApiReloj resuelve todos los candidatos y persiste atómicamente el snapshot completo con cada reloj en `pending`. El conjunto de `relojId` no cambia durante la corrida. Después de cada reloj se reemplaza su resultado y se persisten las métricas, por lo que el detalle representa progreso durable y no sólo el resultado final.

## Estado actual

```http
GET /admin/poll/status
```

Devuelve si hay una corrida en curso y el resumen de la última conocida. Después de un reinicio, se hidrata desde el último registro persistido.

Durante el arranque, luego de aplicar migraciones y antes de iniciar los workers, todos los runs persistidos todavía en `running` se recuperan idempotentemente como `error`: los relojes `pending` pasan a `error`, se completa `finishedAtUtc` y se registra que el proceso anterior fue interrumpido.

## Historial

```http
GET /admin/poll/runs?status=partial_error&limit=50&offset=0
GET /admin/poll/runs/{runId}
```

`status` sólo acepta `running`, `ok`, `partial_error` o `error`; `limit` debe ser positivo y `offset` no negativo.

## Worker

`BackfillPollWorker` ejecuta corridas con `trigger=scheduled` según `WorkerIntervalMinutes`. `RunOnStartup` decide si ejecuta inmediatamente al arrancar. Los únicos triggers aceptados son `manual` y `scheduled`; históricos `startup` se normalizan a `scheduled` por migración.

El semáforo evita dos corridas simultáneas dentro de un proceso. Como no es un lock distribuido, el despliegue soportado exige exactamente una réplica de ApiReloj.

## Configuración

```json
{
  "BackfillPolling": {
    "WorkerIntervalMinutes": 30,
    "WindowMinutes": 30,
    "MaxResultsPerPage": 30,
    "HttpTimeoutSeconds": 30,
    "RunOnStartup": false,
    "BootstrapStartUtc": "2000-01-01T00:00:00Z",
    "MaxWindowsPerRun": 5000
  }
}
```

## Errores

| Código | Caso |
|---:|---|
| `200` | Consulta o corrida completada. |
| `400` | Filtros, estado, limit, offset o runId inválidos. |
| `401` | API key inválida. |
| `403` | IP no autorizada. |
| `404` | Run inexistente. |
| `409` | Ya hay una corrida en el proceso. |
| `429` | Límite de concurrencia. |
| `500` | Fallo general inesperado. |

Los errores de un reloj se capturan en el resultado y normalmente producen `partial_error`, sin abortar los relojes siguientes.

Para el primer despliegue o luego de una migración se recomienda `RunOnStartup=false`; se habilita sólo después del smoke controlado si la operación lo requiere.
