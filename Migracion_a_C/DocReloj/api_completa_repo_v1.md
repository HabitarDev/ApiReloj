# API Completa del Repo (V1)

## 1. Objetivo de esta guia
Esta guia documenta el contrato HTTP real vigente del repo.

Su objetivo es que cualquier developer pueda integrar la API sin tener que leer controllers ni services.

Incluye:
- Endpoints operativos de infra hibrida.
- Endpoints de lectura.
- Endpoints admin de backfill.
- Endpoints de usuarios.
- Endpoints administrativos de maestros.

No documenta como activas las rutas futuras `/api/v1/...`.

## 2. Convenciones generales
### Base URL
Depende del entorno donde se despliegue la API.

Ejemplos:
- `http://localhost:5000`
- `https://mi-servidor:puerto`

### Formato base
- La mayoria de endpoints usan JSON.
- El push de reloj puede recibir:
  - XML
  - JSON
  - multipart/form-data

### Fechas
- Cuando un endpoint usa fechas explicitas, el repo trabaja con `DateTimeOffset` y UTC.
- En heartbeat, el `TimeStamp` llega como epoch seconds (`long`).

### Respuestas
- Las respuestas suelen devolver DTOs directos o listas directas.
- No hay un envelope global de respuesta para toda la API.

### Manejo global de errores
El repo usa `GlobalExceptionFilter`.

Mapeo general:
- `UnauthorizedAccessException` => `401`
- `KeyNotFoundException` => `404`
- `InvalidOperationException` con mensaje de conflicto => `409`
- `ArgumentException` con mensaje de conflicto => `409`
- `InvalidOperationException` normal => `422`
- `ArgumentException` con mensaje de inexistente => `404`
- `ArgumentException` normal => `400`
- `Exception` generica => `500`

Nota importante:
- Algunos endpoints administrativos siguen lanzando `Exception` generica para duplicados o inexistentes.
- En esos casos, el comportamiento real termina en `500`, no en `404` o `409`.

## 3. Mapa rapido de endpoints
### Operacion hibrida
- `POST /Residential/heartbeat`
- `POST /AccessEvents/push/{relojId}`

### Lecturas desde BD
- `GET /AccessEvents`
- `GET /Jornadas`

### Backfill admin
- `POST /admin/poll/run`
- `GET /admin/poll/status`
- `GET /admin/poll/runs`
- `GET /admin/poll/runs/{runId}`

### Usuarios / proxy
- `POST /UsersControllers`
- `PUT /UsersControllers`
- `DELETE /UsersControllers`

### Maestros
- `GET /Residential`
- `GET /Residential/{id}`
- `POST /Residential`
- `GET /Device`
- `GET /Device/{id}`
- `POST /Device`
- `GET /Reloj`
- `GET /Reloj/{id}`
- `POST /Reloj`
- `PUT /Reloj`

## 4. Notas de routing
En ASP.NET Core, muchos controllers usan `[Route("[controller]")]`.

Eso implica:
- `AccessEventsController` => `/AccessEvents`
- `ResidentialController` => `/Residential`
- `DeviceController` => `/Device`
- `RelojController` => `/Reloj`
- `JornadasController` => `/Jornadas`

Nota especial:
- `UsersControllers` termina en `Controllers`, no en `Controller`.
- Por eso la ruta real vigente es `/UsersControllers`.

## 5. Endpoints de operacion hibrida
## 5.1 POST /Residential/heartbeat
### Objetivo
Recibir un heartbeat firmado para actualizar:
- `IpActual` del residencial
- `LastSeen` del device

### Metodo
`POST`

### URI
`/Residential/heartbeat`

### Headers
- `Content-Type: application/json`

### Body request
```json
{
  "deviceId": 100,
  "residentialId": 10,
  "timeStamp": 1740830400,
  "signature": "A1B2C3..."
}
```

Campos:
- `deviceId` (`int`)
- `residentialId` (`int`)
- `timeStamp` (`long`, epoch seconds)
- `signature` (`string`, HMAC SHA256 en hex)

### Response esperada
- Sin body.

### Codigos posibles
- `204` heartbeat procesado correctamente
- `204` no-op si la firma es invalida
- `500` inconsistencia no controlada actual, por ejemplo:
  - residential inexistente
  - device inexistente
  - device no perteneciente al residential

### Notas de comportamiento
- La IP usada para actualizar `IpActual` sale de la IP remota real de la request.
- Si la firma es valida, se persiste la nueva IP.
- Si la firma no es valida, no hace cambios.
- El detalle del emisor esta en `DocHeartBeat/README.md`.

## 5.2 POST /AccessEvents/push/{relojId}
### Objetivo
Recibir eventos del reloj en tiempo real e insertarlos en la BD local.

### Metodo
`POST`

### URI
`/AccessEvents/push/{relojId}`

### Headers
Segun formato de envio del reloj:
- `Content-Type: application/json`
- `Content-Type: application/xml`
- `Content-Type: text/xml`
- `Content-Type: multipart/form-data`

### Path params
- `relojId` (`int`)

### Body request
El body no es un DTO del backend sino el payload enviado por el reloj.

Puede ser:
- XML `EventNotificationAlert`
- JSON equivalente
- multipart con payload y opcionalmente imagen

### Response esperada
DTO `PushIngestResultDto`:
```json
{
  "status": "inserted",
  "reason": null,
  "eventType": "AccessControllerEvent",
  "serialNo": 123456,
  "deviceSn": "ABC123",
  "eventTimeUtc": "2026-03-01T12:00:00Z"
}
```

### Estados funcionales posibles
- `inserted`
- `duplicate`
- `ignored`

### Codigos posibles
- `200` request procesada (incluye inserted, duplicate e ignored)
- `400` payload vacio o mal parseado
- `401` push no autorizado
- `404` reloj inexistente, si la autorizacion o contexto no puede resolverse de esa forma
- `422` regla de negocio invalida
- `500` error inesperado

### Notas de comportamiento
- Este endpoint esta pensado para el reloj, no para integracion manual habitual.
- Corre un filtro de autorizacion antes de la logica principal.
- Si el evento se inserta, puede disparar generacion/actualizacion de jornadas.
- La deduplicacion se hace por `(DeviceSn, SerialNumber)`.

## 6. Endpoints de lectura desde BD
## 6.1 GET /AccessEvents
### Objetivo
Consultar eventos almacenados en PostgreSQL local.

### Metodo
`GET`

### URI
`/AccessEvents`

### Headers
- `Accept: application/json`

### Query params
- `residentialId` (`int?`)
- `deviceSn` (`string?`)
- `employeeNumber` (`string?`)
- `major` (`int?`)
- `minor` (`int?`)
- `attendanceStatus` (`string?`)
- `fromUtc` (`DateTimeOffset?`)
- `toUtc` (`DateTimeOffset?`)
- `limit` (`int`, default `100`)
- `offset` (`int`, default `0`)

### Reglas
- `fromUtc` y `toUtc` deben venir juntos.
- `fromUtc <= toUtc`.
- `limit > 0`.
- `offset >= 0`.
- `attendanceStatus` se compara case-insensitive.
- Todos los filtros se combinan con `AND`.

### Response esperada
Lista de `AccesEventDto`:
```json
[
  {
    "_deviceSn": "ABC123",
    "_serialNumber": 10001,
    "_eventTimeUtc": "2026-03-01T12:00:00Z",
    "_timeDevice": "2026-03-01T12:00:00-03:00",
    "_employeeNumber": "123",
    "_major": 5,
    "_minor": 38,
    "_attendanceStatus": "checkIn",
    "_raw": "{\"SchemaVersion\":\"v1\",\"Source\":\"push\",\"Format\":\"xml\",\"ContentType\":\"application/xml\",\"HasPicture\":false,\"CapturedAtUtc\":\"2026-03-01T12:00:01Z\",\"Payload\":\"<EventNotificationAlert>...</EventNotificationAlert>\"}"
  }
]
```

### Codigos posibles
- `200` consulta correcta (incluye lista vacia)
- `400` argumentos invalidos
- `404` `residentialId` inexistente
- `500` error inesperado

### Notas de comportamiento
- Este endpoint consulta solo BD local.
- No consulta al reloj en tiempo real.
- Si se usa `residentialId`, internamente filtra por los `DeviceSn` de los relojes del residential.

## 6.2 GET /Jornadas
### Objetivo
Consultar jornadas derivadas desde `AccessEvents`.

### Metodo
`GET`

### URI
`/Jornadas`

### Headers
- `Accept: application/json`

### Query params
- `residentialId` (`int?`)
- `clockSn` (`string?`)
- `employeeNumber` (`string?`)
- `statusCheck` (`string?`)
- `statusBreak` (`string?`)
- `fromUtc` (`DateTimeOffset?`)
- `toUtc` (`DateTimeOffset?`)
- `updatedSinceUtc` (`DateTimeOffset?`)
- `limit` (`int`, default `100`)
- `offset` (`int`, default `0`)

### Reglas
- `fromUtc` y `toUtc` deben venir juntos.
- `fromUtc <= toUtc`.
- `limit > 0`.
- `offset >= 0`.

### Response esperada
Lista de `JornadaDto`:
```json
[
  {
    "jornadaId": "01H...",
    "employeeNumber": "123",
    "clockSn": "ABC123",
    "startAt": "2026-03-01T08:00:00Z",
    "breakInAt": "2026-03-01T12:00:00Z",
    "breakOutAt": "2026-03-01T12:30:00Z",
    "endAt": "2026-03-01T17:00:00Z",
    "statusCheck": "OK",
    "statusBreak": "OK",
    "updatedAt": "2026-03-01T17:00:00Z"
  }
]
```

### Codigos posibles
- `200` consulta correcta (incluye lista vacia)
- `400` argumentos invalidos
- `404` `residentialId` inexistente
- `422` regla de negocio
- `500` error inesperado

### Notas de comportamiento
- Este endpoint tambien consulta solo BD local.
- No existe endpoint de escritura manual de jornadas.

## 7. Endpoints de backfill / administracion operativa
## 7.1 POST /admin/poll/run
### Objetivo
Disparar manualmente una corrida de poll/backfill.

### Metodo
`POST`

### URI
`/admin/poll/run`

### Headers
- `Content-Type: application/json`

### Body request
Body opcional.

Si se envia, usa `BackfillPollRunRequestDto`:
```json
{
  "residentialId": 1,
  "relojId": 10,
  "trigger": "manual"
}
```

Nota:
- El controller fuerza `trigger = "manual"` para este endpoint.

### Response esperada
`BackfillPollRunResultDto`

Ejemplo resumido:
```json
{
  "runId": "abc123",
  "trigger": "manual",
  "startedAtUtc": "2026-03-01T12:00:00Z",
  "finishedAtUtc": "2026-03-01T12:00:03Z",
  "status": "ok",
  "error": null,
  "totalClocks": 2,
  "totalWindows": 1,
  "totalPages": 1,
  "inserted": 10,
  "duplicates": 2,
  "ignored": 0,
  "clocks": []
}
```

### Codigos posibles
- `200` corrida ejecutada
- `400` argumentos invalidos
- `409` ya hay una corrida en ejecucion
- `500` error inesperado

### Notas de comportamiento
- Usa lock global para evitar solapamiento.
- Persiste inicio y cierre de la corrida.

## 7.2 GET /admin/poll/status
### Objetivo
Consultar el estado actual y el resumen de la ultima corrida conocida.

### Metodo
`GET`

### URI
`/admin/poll/status`

### Headers
- `Accept: application/json`

### Body request
- No aplica.

### Response esperada
`BackfillPollStatusDto`
```json
{
  "isRunning": false,
  "lastRunId": "abc123",
  "lastTrigger": "scheduled",
  "lastStartedAtUtc": "2026-03-01T12:00:00Z",
  "lastFinishedAtUtc": "2026-03-01T12:00:03Z",
  "lastStatus": "ok",
  "lastError": null,
  "lastTotalClocks": 2,
  "lastInserted": 10,
  "lastDuplicates": 2,
  "lastIgnored": 0
}
```

### Codigos posibles
- `200` consulta correcta
- `500` error inesperado

## 7.3 GET /admin/poll/runs
### Objetivo
Listar historico de corridas de poll.

### Metodo
`GET`

### URI
`/admin/poll/runs`

### Headers
- `Accept: application/json`

### Query params
- `status` (`string?`)
- `limit` (`int`, default `50`)
- `offset` (`int`, default `0`)

### Response esperada
Lista de `BackfillPollRunSummaryDto`

### Codigos posibles
- `200` consulta correcta
- `400` query invalida
- `500` error inesperado

### Notas de comportamiento
- `status` permite filtrar por valores esperados como:
  - `running`
  - `ok`
  - `partial_error`
  - `error`

## 7.4 GET /admin/poll/runs/{runId}
### Objetivo
Obtener el detalle completo de una corrida puntual.

### Metodo
`GET`

### URI
`/admin/poll/runs/{runId}`

### Headers
- `Accept: application/json`

### Path params
- `runId` (`string`)

### Response esperada
`BackfillPollRunResultDto`

### Codigos posibles
- `200` consulta correcta
- `400` `runId` invalido
- `404` run inexistente
- `500` error inesperado

## 8. Endpoints de usuarios / proxy
Nota general:
- Aunque estos endpoints no tienen `[FromBody]` explicito, con `[ApiController]` y tipos complejos el binding esperado es por body JSON.
- Operan por residencial y hacen fan-out a todos los relojes del residencial.

## 8.1 POST /UsersControllers
### Objetivo
Crear un usuario en todos los relojes del residencial indicado.

### Metodo
`POST`

### URI
`/UsersControllers`

### Headers
- `Content-Type: application/json`

### Body request
`CreateUserDtoFromBack`
```json
{
  "_employeeNo": "123",
  "_name": "Juan Perez",
  "_userType": "normal",
  "_beginTime": "2000-01-01T00:00:00",
  "_endTime": "2037-12-31T23:59:59",
  "_enable": true,
  "_timeType": "local",
  "_residentialId": 1
}
```

### Response esperada
Devuelve el mismo DTO enviado.

### Codigos posibles
- `200` operacion completada
- `500` error de proxy, reloj o datos inexistentes

### Notas de comportamiento
- Usa credenciales `ISAPI_USER` y `ISAPI_PASSWORD` si existen.
- La request al reloj se hace via ISAPI `UserInfo/SetUp?format=json`.

## 8.2 PUT /UsersControllers
### Objetivo
Modificar un usuario en todos los relojes del residencial indicado.

### Metodo
`PUT`

### URI
`/UsersControllers`

### Headers
- `Content-Type: application/json`

### Body request
`ModifiUserDtoFromBack`
```json
{
  "_employeeNo": "123",
  "_name": "Juan Perez",
  "_userType": "normal",
  "_beginTime": "2000-01-01T00:00:00",
  "_endTime": "2037-12-31T23:59:59",
  "_enable": true,
  "_timeType": "local",
  "_residentialId": 1
}
```

### Response esperada
Devuelve el mismo DTO enviado.

### Codigos posibles
- `200` operacion completada
- `500` error de proxy, reloj o datos inexistentes

### Notas de comportamiento
- La request al reloj se hace via ISAPI `UserInfo/Modify?format=json`.

## 8.3 DELETE /UsersControllers
### Objetivo
Borrar un usuario en todos los relojes del residencial indicado.

### Metodo
`DELETE`

### URI
`/UsersControllers`

### Headers
- `Content-Type: application/json`

### Body request
`DeleteUserDtoFromBack`
```json
{
  "_employeeNo": "123",
  "_residentialId": 1
}
```

### Response esperada
Devuelve el mismo DTO enviado.

### Codigos posibles
- `200` operacion completada
- `500` error de proxy, reloj o datos inexistentes

### Notas de comportamiento
- La request al reloj se hace via ISAPI `UserInfoDetail/Delete?format=json`.

## 9. Endpoints administrativos de maestros
Nota general:
- Estos endpoints sirven para cargar y consultar maestros locales.
- No son el trafico principal del reloj, pero son necesarios para configurar el entorno.

## 9.1 GET /Residential
### Objetivo
Listar residenciales.

### Metodo
`GET`

### URI
`/Residential`

### Headers
- `Accept: application/json`

### Response esperada
Lista de `ResidentialDto`
```json
[
  {
    "_idResidential": 1,
    "_ipActual": "192.168.1.10",
    "_relojes": [],
    "_devices": []
  }
]
```

### Codigos posibles
- `200` consulta correcta
- `500` error inesperado

## 9.2 GET /Residential/{id}
### Objetivo
Consultar un residential por id.

### Metodo
`GET`

### URI
`/Residential/{id}`

### Path params
- `id` (`int`)

### Response esperada
`ResidentialDto`

### Codigos posibles
- `200` consulta correcta
- `500` si el residential no existe en la implementacion actual

## 9.3 POST /Residential
### Objetivo
Crear un residential.

### Metodo
`POST`

### URI
`/Residential`

### Headers
- `Content-Type: application/json`

### Body request
`CrearResidentialRequest`
```json
{
  "idResidential": 1,
  "ipActual": "192.168.1.10"
}
```

### Response esperada
`ResidentialDto`

### Codigos posibles
- `200` creado y retornado
- `400` `idResidential` invalido
- `500` si ya existe en la implementacion actual

## 9.4 GET /Device
### Objetivo
Listar devices.

### Metodo
`GET`

### URI
`/Device`

### Response esperada
Lista de `DeviceDto`

### Codigos posibles
- `200` consulta correcta
- `500` error inesperado

## 9.5 GET /Device/{id}
### Objetivo
Consultar un device por id.

### Metodo
`GET`

### URI
`/Device/{id}`

### Path params
- `id` (`int`)

### Response esperada
`DeviceDto`

### Codigos posibles
- `200` consulta correcta
- `500` si el device no existe en la implementacion actual

## 9.6 POST /Device
### Objetivo
Crear un device.

### Metodo
`POST`

### URI
`/Device`

### Headers
- `Content-Type: application/json`

### Body request
`DeviceDto`
```json
{
  "_deviceId": 100,
  "_secretKey": "mi-clave",
  "_lastSeen": null,
  "_residentialId": 1
}
```

### Response esperada
`DeviceDto`

### Codigos posibles
- `200` creado y retornado
- `400` datos invalidos
- `500` si el residential no existe o el device ya existe en la implementacion actual

## 9.7 GET /Reloj
### Objetivo
Listar relojes.

### Metodo
`GET`

### URI
`/Reloj`

### Response esperada
Lista de `RelojDto`

### Codigos posibles
- `200` consulta correcta
- `500` error inesperado

## 9.8 GET /Reloj/{id}
### Objetivo
Consultar un reloj por id.

### Metodo
`GET`

### URI
`/Reloj/{id}`

### Path params
- `id` (`int`)

### Response esperada
`RelojDto`

### Codigos posibles
- `200` consulta correcta
- `500` si el reloj no existe en la implementacion actual

## 9.9 POST /Reloj
### Objetivo
Crear un reloj.

### Metodo
`POST`

### URI
`/Reloj`

### Headers
- `Content-Type: application/json`

### Body request
`CrearRelojRequest`
```json
{
  "_idReloj": 10,
  "_puerto": 80,
  "_residentialId": 1
}
```

### Response esperada
`RelojDto`

### Codigos posibles
- `200` creado y retornado
- `400` datos invalidos
- `500` si el residential no existe o el reloj ya existe en la implementacion actual

## 9.10 PUT /Reloj
### Objetivo
Actualizar puerto y `DeviceSn` de un reloj existente.

### Metodo
`PUT`

### URI
`/Reloj`

### Headers
- `Content-Type: application/json`

### Body request
`ActualizarRelojRequest`
```json
{
  "_idReloj": 10,
  "_puerto": 80,
  "_deviceSn": "ABC123"
}
```

### Response esperada
`RelojDto`

### Codigos posibles
- `200` actualizado y retornado
- `400` datos invalidos
- `500` si el reloj no existe en la implementacion actual

### Notas de comportamiento
- En V1 actual, este es el camino manual para cargar `DeviceSn`.

## 10. Modelos y notas utiles de integracion
### `AccesEventDto`
Campos relevantes de lectura:
- `_deviceSn`
- `_serialNumber`
- `_eventTimeUtc`
- `_timeDevice`
- `_employeeNumber`
- `_major`
- `_minor`
- `_attendanceStatus`
- `_raw`

### `JornadaDto`
Campos relevantes:
- `JornadaId`
- `EmployeeNumber`
- `ClockSn`
- `StartAt`
- `BreakInAt`
- `BreakOutAt`
- `EndAt`
- `StatusCheck`
- `StatusBreak`
- `UpdatedAt`

### `_raw` de AccessEvents
`_raw` no guarda el XML/JSON original "suelto".

Guarda un envelope JSON serializado con esta forma conceptual:
- `SchemaVersion`
- `Source`
- `Format`
- `ContentType`
- `HasPicture`
- `CapturedAtUtc`
- `Payload`

## 11. Flujos recomendados de integracion
### Alta inicial de entorno
1. Crear `Residential`.
2. Crear `Device`.
3. Crear `Reloj`.
4. Ejecutar `PUT /Reloj` para cargar `DeviceSn` si todavia no esta configurado.

### Operacion normal
1. El servicio de heartbeat llama `POST /Residential/heartbeat`.
2. El reloj llama `POST /AccessEvents/push/{relojId}`.
3. El backend consulta `GET /AccessEvents` y `GET /Jornadas`.

### Reconciliacion / backfill
1. Consultar `GET /admin/poll/status`.
2. Ejecutar `POST /admin/poll/run` si se necesita una corrida manual.
3. Revisar `GET /admin/poll/runs`.
4. Inspeccionar detalle con `GET /admin/poll/runs/{runId}`.

### Usuarios
1. El backend usa `UsersControllers`.
2. Debe asumir fan-out por residential.
3. No debe asumir operacion puntual por reloj.

## 12. Limitaciones y notas de integracion
- Las rutas activas son las legacy actuales del repo.
- Las rutas futuras `/api/v1/...` no estan implementadas.
- `UsersControllers` es el endpoint real y opera por residential.
- El push de eventos es un endpoint pensado para ser llamado por el reloj.
- Varias validaciones de maestros aun terminan en `500` por el uso actual de `Exception` generica.
- `GET /AccessEvents` y `GET /Jornadas` leen BD local, no consultan al reloj en linea.

## 13. Ver tambien
Para detalle especializado, ver:
- `infra_hibrida.md`
- `api_access_events_v1.md`
- `api_poll_backfill_v1.md`
- `api_jornadas_v1.md`
- `DocHeartBeat/README.md`
