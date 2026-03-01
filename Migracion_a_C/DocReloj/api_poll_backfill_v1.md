# API Poll Backfill V1

## 1. Objetivo
Documentar el backfill automatico de eventos de acceso por poll ISAPI, su cursor y endpoints de administracion.

## 2. Endpoint ISAPI base
- Metodo: `POST`
- Ruta reloj: `/ISAPI/AccessControl/AcsEvent?format=json`
- Uso: buscar eventos por ventana temporal con paginacion.

## 3. Reglas de cursor
1. Cursor real: `Reloj.LastPollEvent`.
2. `LastPushEvent` no decide cursor de poll.
3. Si `LastPollEvent == null`:
   - Se busca oldest disponible en reloj.
   - Si existe oldest: bootstrap desde oldest hasta `now`.
   - Si no existe oldest: `LastPollEvent = now`.
4. Si `LastPollEvent != null`:
   - `gap <= 30m`: seguridad `[now-30m, now]`.
   - `gap > 30m`: catch-up por ventanas de 30 min.

## 4. Paginacion
- Request:
  - `searchID`
  - `searchResultPosition`
  - `maxResults`
- Response:
  - `responseStatusStrg`
  - `numOfMatches`
  - `InfoList`
- Regla:
  - Continuar mientras `responseStatusStrg == "MORE"`.
  - Avanzar `searchResultPosition += numOfMatches`.

## 5. Endpoints admin (ApiReloj)

### 5.1 Ejecutar corrida manual
- Metodo: `POST`
- Ruta: `/admin/poll/run`

Body opcional:
```json
{
  "residentialId": 1,
  "relojId": 10
}
```

Comportamiento:
1. Si no se envian filtros: corre para todos los relojes.
2. Si ya hay corrida en ejecucion: error de conflicto.

### 5.2 Consultar estado
- Metodo: `GET`
- Ruta: `/admin/poll/status`

Respuesta ejemplo:
```json
{
  "isRunning": false,
  "lastRunId": "e4d9e07f9a0d4db385cf8ab2f42a0a8d",
  "lastTrigger": "scheduled",
  "lastStartedAtUtc": "2026-02-20T16:00:00Z",
  "lastFinishedAtUtc": "2026-02-20T16:01:12Z",
  "lastStatus": "ok",
  "lastError": null,
  "lastTotalClocks": 4,
  "lastInserted": 120,
  "lastDuplicates": 16,
  "lastIgnored": 2
}
```

### 5.3 Listar historial de corridas
- Metodo: `GET`
- Ruta: `/admin/poll/runs`

Query params opcionales:
- `status` (`running`, `ok`, `partial_error`, `error`)
- `limit` (default `50`)
- `offset` (default `0`)

Respuesta ejemplo:
```json
[
  {
    "runId": "e4d9e07f9a0d4db385cf8ab2f42a0a8d",
    "trigger": "scheduled",
    "startedAtUtc": "2026-02-20T16:00:00Z",
    "finishedAtUtc": "2026-02-20T16:01:12Z",
    "status": "ok",
    "error": null,
    "totalClocks": 4,
    "totalWindows": 4,
    "totalPages": 12,
    "inserted": 120,
    "duplicates": 16,
    "ignored": 2
  }
]
```

### 5.4 Consultar corrida puntual
- Metodo: `GET`
- Ruta: `/admin/poll/runs/{runId}`

Respuesta ejemplo:
```json
{
  "runId": "e4d9e07f9a0d4db385cf8ab2f42a0a8d",
  "trigger": "manual",
  "startedAtUtc": "2026-02-20T16:00:00Z",
  "finishedAtUtc": "2026-02-20T16:01:12Z",
  "status": "partial_error",
  "error": null,
  "totalClocks": 2,
  "totalWindows": 3,
  "totalPages": 6,
  "inserted": 40,
  "duplicates": 5,
  "ignored": 1,
  "clocks": [
    {
      "relojId": 10,
      "deviceSn": "DS-K1T...",
      "status": "ok",
      "note": null,
      "error": null,
      "cursorBefore": "2026-02-20T15:00:00Z",
      "cursorAfter": "2026-02-20T16:00:00Z",
      "windowsProcessed": 2,
      "pagesProcessed": 5,
      "inserted": 38,
      "duplicates": 5,
      "ignored": 1
    },
    {
      "relojId": 11,
      "deviceSn": "DS-K1T-2...",
      "status": "error",
      "note": null,
      "error": "timeout",
      "cursorBefore": "2026-02-20T15:30:00Z",
      "cursorAfter": "2026-02-20T15:30:00Z",
      "windowsProcessed": 0,
      "pagesProcessed": 0,
      "inserted": 0,
      "duplicates": 0,
      "ignored": 0
    }
  ]
}
```

## 6. Criterio de idempotencia
- PK en `AccessEvents`: `(DeviceSn, SerialNumber)`.
- Resultado esperado en corrida:
  - eventos nuevos -> `inserted`
  - repetidos -> `duplicates`
  - invalidos/parsing fallido -> `ignored`

## 7. Configuracion (`appsettings.json`)
```json
"BackfillPolling": {
  "WorkerIntervalMinutes": 30,
  "WindowMinutes": 30,
  "MaxResultsPerPage": 30,
  "HttpTimeoutSeconds": 30,
  "RunOnStartup": true,
  "BootstrapStartUtc": "2000-01-01T00:00:00Z",
  "MaxWindowsPerRun": 5000
}
```

## 8. Errores esperados
- `400`: argumentos invalidos en request manual.
- `404`: filtros a recursos inexistentes (si aplica por validacion superior).
- `409/422`: corrida concurrente o regla de negocio.
- `500`: error inesperado.
- Si falla la persistencia inicial de corrida (`PersistStartedRun`), la corrida falla en modo fail-fast.
- Aun ante ese error, el lock global de corrida se libera para permitir nuevas ejecuciones.

## 9. Persistencia y reinicios
1. Cada corrida se persiste en tabla `BackfillPollRuns`.
2. `GET /admin/poll/status` se puede hidratar desde la ultima corrida persistida tras restart.
3. `GET /admin/poll/runs` y `GET /admin/poll/runs/{runId}` dan trazabilidad durable (no solo en memoria).
4. La persistencia de cierre y actualizacion de estado en memoria se ejecutan en best-effort; si fallan, se loguea error y no se retiene el lock.

## 10. Checklist operativo
1. `Reloj.DeviceSn` configurado.
2. `Residential.IpActual` correcto.
3. Credenciales digest (`ISAPI_USER`, `ISAPI_PASSWORD`) definidas si el reloj lo requiere.
4. Reachability de red desde ApiReloj al reloj (`ip:puerto`).
5. Verificar `status` en `/admin/poll/status`.
6. Verificar historial en `/admin/poll/runs`.
