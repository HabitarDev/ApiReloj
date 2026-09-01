# API Poll Backfill V1

Contrato vigente al 15 de julio de 2026.

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

Estados de corrida: `running`, `ok`, `partial_error`, `error`. Un reloj no configurado puede quedar `skipped` dentro del detalle sin convertir toda la corrida en error.

## Estado actual

```http
GET /admin/poll/status
```

Devuelve si hay una corrida en curso y el resumen de la última conocida. Después de un reinicio, se hidrata desde el último registro persistido.

## Historial

```http
GET /admin/poll/runs?status=partial_error&limit=50&offset=0
GET /admin/poll/runs/{runId}
```

`status` sólo acepta `running`, `ok`, `partial_error` o `error`; `limit` debe ser positivo y `offset` no negativo.

## Worker

`BackfillPollWorker` ejecuta corridas con `trigger=scheduled` según `WorkerIntervalMinutes`. `RunOnStartup` decide si ejecuta inmediatamente al arrancar.

## Configuración

```json
{
  "BackfillPolling": {
    "WorkerIntervalMinutes": 30,
    "WindowMinutes": 30,
    "MaxResultsPerPage": 30,
    "HttpTimeoutSeconds": 30,
    "RunOnStartup": true,
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
