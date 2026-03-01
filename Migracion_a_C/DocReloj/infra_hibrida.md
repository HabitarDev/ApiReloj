# Infra hibrida: proxy + cache de eventos (v1)

## Objetivo
Convertir ApiReloj en un proxy hibrido:
- Para eventos de acceso: suscribir, recibir y almacenar en BD local.
- Para comandos (crear persona, etc.): pasamanos hacia el reloj o conjunto de relojes correspondiente.
- Mantener el flujo actual de heartbeats para resolver IP/puerto.

## Alcance
- Ingesta de eventos (push + poll de backfill).
- Persistencia de eventos en Postgres (guardado permanente).
- Consultas del backend contra la BD local.
- Proxy de operaciones ISAPI (crear/editar persona, etc.).
- Heartbeats para actualizar IP actual del residencial.

## Componentes
- Backend (sistema central).
- ApiReloj (servicio proxy hibrido).
- Postgres local en el servidor de ApiReloj.
- Relojes Hikvision (ISAPI).
- Servicio heartbeat (Windows) ya existente.

## Diagrama general
```
Backend ----HTTP----> ApiReloj
                        | \
                        |  \----> Reloj (ISAPI, comandos)
                        |
                        +----> Postgres (eventos)

Reloj ----push----> ApiReloj ----insert/upsert----> Postgres
ApiReloj ----poll----> Reloj (backfill cada 30 min)
```

## Flujos principales
### 1) Heartbeat (actual)
1. Servicio Windows envia heartbeat con DeviceId, ResidentialId, TimeStamp, Signature.
2. ApiReloj valida firma HMAC y actualiza IP actual del residencial.
3. Esto permite resolver a que IP/puerto se deben enviar comandos ISAPI.
4. En V1 el endpoint se mantiene compatible con el emisor actual: respuesta HTTP `204` (accion `void`) cuando la firma no valida, aplicando no-op (sin actualizar estado).

### 2) Eventos por push (tiempo real)
1. Reloj envia eventos al servidor (httpHosts o alertStream).
2. ApiReloj parsea el payload (XML/JSON o multipart).
3. ApiReloj normaliza y guarda evento en BD con idempotencia.

### 3) Backfill por poll (cada 30 min)
1. Cada 30 min ApiReloj consulta cada reloj.
2. Determina la ventana a pedir:
   - Si no hay `Last_Poll_Event`: buscar evento mas antiguo disponible y usarlo como inicio de bootstrap (ignorar `Last_Push_Event` para decidir el cursor).
   - Si hay `Last_Poll_Event` y el gap <= 30 min: pedir [now-30m, now].
   - Si hay `Last_Poll_Event` y el gap > 30 min: pedir en ventanas de 30 min desde `Last_Poll_Event` hasta now.
3. Guarda eventos con idempotencia (no duplica).

### 4) Consultas del backend
1. Backend pide eventos por empleado/rango.
2. ApiReloj consulta la BD local.
3. Responde sin consultar al reloj.

### 5) Comandos del backend (proxy)
1. Backend llama un endpoint local (ej. CreatePerson).
2. ApiReloj resuelve IP/puerto del reloj o de los relojes involucrados segun la operacion.
3. ApiReloj reenvia la request a ISAPI al destino correspondiente.
4. ApiReloj devuelve la respuesta al backend.

## Rutas reales V1 implementadas (contrato vigente)
### Backend -> ApiReloj
- `POST /UsersControllers` (crear usuario en relojes del residencial)
- `PUT /UsersControllers` (modificar usuario en relojes del residencial)
- `DELETE /UsersControllers` (eliminar usuario en relojes del residencial)
- `GET /AccessEvents` (consulta eventos desde BD local)
- `GET /Jornadas` (consulta jornadas derivadas)
- `POST /Residential/heartbeat`
- `POST /admin/poll/run`
- `GET /admin/poll/status`
- `GET /admin/poll/runs`
- `GET /admin/poll/runs/{runId}`

### Reloj -> ApiReloj
- `POST /AccessEvents/push/{relojId}`

### Nota de routing ASP.NET Core
- Los controllers con `[Route("[controller]")]` exponen como ruta base el nombre de la clase.
- Si la clase termina exactamente en `Controller`, ASP.NET Core remueve ese sufijo.
- Ejemplos:
  - `AccessEventsController` => `/AccessEvents`
  - `ResidentialController` => `/Residential`
  - `RelojController` => `/Reloj`
  - `DeviceController` => `/Device`
- Excepcion relevante en V1:
  - `UsersControllers` no termina exactamente en `Controller`, por eso la ruta expuesta queda `/UsersControllers`.

## Operacion de usuarios V1 (estado actual)
- `POST /UsersControllers`, `PUT /UsersControllers` y `DELETE /UsersControllers` operan por residencial.
- En V1 actual, las operaciones de persona no son por reloj puntual.
- El comportamiento real es fan-out:
  - la API resuelve el `Residential`,
  - recorre todos sus `Relojes`,
  - y replica la misma operacion ISAPI a cada reloj del residencial.
- Si falla uno de los relojes durante el fan-out, la operacion global puede fallar aunque otros ya hayan aplicado cambios.
- Este comportamiento es intencional en V1 y se considera valido para administracion masiva por residencial.

## Extensiones futuras recomendadas (no implementadas en V1)
- Para soportar operacion puntual por reloj, se recomienda agregar endpoints dedicados:
  - `POST /api/v1/residentials/{residentialId}/relojes/{relojId}/persons`
  - `PUT /api/v1/residentials/{residentialId}/relojes/{relojId}/persons/{employeeNo}`
  - `DELETE /api/v1/residentials/{residentialId}/relojes/{relojId}/persons/{employeeNo}`
- Estos endpoints no existen hoy y no reemplazan el fan-out actual de `UsersControllers`.

## Contrato objetivo futuro (fuera de alcance V1)
- Las rutas REST normalizadas con prefijo `/api/v1/...` siguen siendo una propuesta de diseno para una version futura.
- Referencias de diseno objetivo:
  - `POST /api/v1/residentials/{residentialId}/relojes/{relojId}/persons`
  - `PUT /api/v1/residentials/{residentialId}/relojes/{relojId}/persons/{employeeNo}`
  - `DELETE /api/v1/residentials/{residentialId}/relojes/{relojId}/persons/{employeeNo}`
  - `GET /api/v1/residentials/{residentialId}/events`
  - `POST /api/v1/heartbeat`
  - `POST /api/v1/admin/poll/run`
  - `GET /api/v1/admin/poll/status`
  - `GET /api/v1/admin/poll/runs`
  - `GET /api/v1/admin/poll/runs/{runId}`
- En V1 actual no estan implementadas.
- Cualquier integracion operativa debe usar exclusivamente las rutas listadas en `Rutas reales V1 implementadas (contrato vigente)`.

## Nota operativa heartbeat V1 (compatibilidad del emisor)
- El emisor de heartbeat actual no consume respuestas de error para reintentos/diagnostico.
- Por esa razon, `POST /Residential/heartbeat` se mantiene en modo compatible:
  - ante firma valida: actualiza `Residential.IpActual` y `Device.LastSeen`.
  - ante firma invalida: no actualiza estado y responde `204` (no-op silencioso).
  - ante inconsistencia de datos (por ejemplo `Residential`/`Device` inexistente o `Device` que no pertenece al `Residential`): hoy el flujo puede lanzar excepcion y derivar en error HTTP segun `GlobalExceptionFilter` (no se trata como no-op).
- Esta decision es intencional en V1 y evita romper el flujo del servicio emisor existente.

## Push vs Poll (resumen)
- Push: baja latencia, menor carga del reloj, depende de reachability desde reloj.
- Poll: mas robusto para backfill, requiere reachability desde servidor al reloj.
- Decision: usar ambos. Push como fuente principal y poll cada 30 min para backfill.

## Vertical adicional implementada (fuera del nucleo infra hibrida)
- El repo incluye la vertical `Jornadas` como capa derivada sobre `AccessEvents`.
- Endpoints/workers asociados:
  - `GET /Jornadas`
  - `JornadaStatusWorker` (saneamiento periodico de jornadas incompletas)
- Esta vertical no reemplaza push/poll ni el modelo de `AccessEvents`; consume los eventos ya persistidos para exponer una vista laboral agregada.

## Backfill cada 30 min (logica)
- Periodicidad: 30 min.
- Ventana base: 30 min.
- Si el gap es grande, iterar en ventanas de 30 min hasta alcanzar now.
- Paginacion: usar searchID, searchResultPosition, maxResults.

Pseudo:
```
if !last_poll_event:
  oldest = get_oldest_event_time()
  if oldest:
    for window in [oldest, now] step 30m:
      poll(window.start, window.end)
      last_poll_event = window.end
  else:
    last_poll_event = now

gap = now - last_poll_event
if gap <= 30m:
  poll(now-30m, now)
  last_poll_event = now
else:
  for window in [last_poll_event, now] step 30m:
    poll(window.start, window.end)
    last_poll_event = window.end
```

## Idempotencia y deduplicacion
- Clave unica: (device_sn, serial_no).
- En BD: primary key o unique index con esa dupla.
- Efecto: push y poll pueden insertar el mismo evento sin duplicar.
- Si no hay serial_no: loguear y decidir si se ignora o se guarda en tabla de errores.

## Persistencia (modelo sugerido)
Tabla principal (ejemplo):
```
access_events (
  device_sn        text not null,
  serial_no        bigint not null,
  event_time_utc   timestamptz not null,
  time_device      text,
  employee_no      text,
  major            int,
  minor            int,
  attendance_status text,
  raw              jsonb not null,
  primary key (device_sn, serial_no)
)
```
Indices sugeridos:
- (event_time_utc)
- (employee_no)

Estado de polling en Reloj:
- `Last_Push_Event`: ultima hora recibida por push (solo monitoreo).
- `Last_Poll_Event`: ultimo limite cubierto por backfill (cursor real del poll).

## Seguridad y red
- Auth: HTTP Digest (hacia reloj).
- TLS: preferir HTTPS con CA del dispositivo.
- Push: requiere que reloj alcance el servidor (NAT o VPN).
- Poll: requiere que servidor alcance el reloj (NAT o VPN).
- Auth para backend: opcional hoy, se puede agregar luego sin romper contratos.

## Observabilidad
- V1 actual: observabilidad basada en logs crudos de requests/errores/tiempos.
- No hay capa de metricas persistentes globales para push (recibidos/insertados/duplicados/errores agregados).
- No hay indexado ni categorizacion propia de errores dentro de la API.
- Poll/backfill si mantiene trazabilidad durable por corrida en tabla `BackfillPollRuns` (consultable por API).

## Riesgos y mitigaciones
- Buffer del reloj limitado: solo se recupera lo que el reloj retiene.
- Caidas largas: si ApiReloj cae mas tiempo que el buffer del reloj, se perderan eventos.
- Duplicados: controlados por idempotencia.
- Drift de hora: usar event_time y normalizar a UTC.

## Notas de implementacion
- V2/futuro: resolver `device_sn` via `/ISAPI/System/deviceInfo`.
- V1 actual: `DeviceSn` se carga manualmente (ej. `PUT /Reloj`).
- Guardar raw para re-procesos futuros.
- Filtrar eventos de asistencia en lectura (o guardar solo los relevantes).
- Para obtener el evento mas antiguo disponible: POST /ISAPI/AccessControl/AcsEvent?format=json
  con startTime muy antiguo, endTime=now, timeReverseOrder=false y maxResults=1.
- `Last_Push_Event` se actualiza solo al recibir push.
- `Last_Push_Event` no define el cursor de backfill.
- `Last_Poll_Event` se actualiza solo al finalizar cada ventana de poll.

## Cambios en dominio (propuesta)
- Nueva entidad `AccessEvent` (tabla access_events).
- Entidad `Reloj` agrega:
  - `DeviceSn`
  - `Last_Push_Event` (DateTimeOffset?)
  - `Last_Poll_Event` (DateTimeOffset?)
- DTOs separados del dominio para endpoints (personas, eventos, etc.).

## DTOs (fuera del dominio)
Ejemplos:
- `PersonCreateDto`: employeeNo, name, userType.
- `PersonUpdateDto`: name?, userType?
- `EventsQueryDto`: from, to, employeeNo, deviceSn, major, minor, attendanceStatus, limit, offset.
- `EventsResponseDto`: items[], total?, paging?.

## Decisiones acordadas
- Modo hibrido: push + poll.
- Backfill cada 30 min en ventanas de 30 min.
- Guardado permanente.
- Idempotencia: device_sn + serial_no.
- Si no hay `Last_Poll_Event`, iniciar bootstrap desde el evento mas antiguo disponible (aunque exista `Last_Push_Event`).
- Estado de polling en Reloj: Last_Push_Event y Last_Poll_Event.

## Implementacion V1 Push (httpHosts)
### Endpoint implementado
- `POST /AccessEvents/push/{relojId}`
- El `relojId` en path identifica de forma determinista a que reloj pertenece el evento.

### Alcance operativo V1 (configuracion manual)
- No existe endpoint de auto-suscripcion/configuracion de `httpHosts` desde ApiReloj.
- La configuracion `httpHosts` y `Request_URI` se realiza manualmente en cada reloj.
- Este comportamiento es intencional en V1 para reducir complejidad de orquestacion remota.

### Seguridad implementada
- Filtro dedicado: `AuthorizationPushFilter` aplicado al endpoint push.
- El filtro valida:
  - `relojId` existente.
  - `Residential` del reloj existente.
  - IP origen del request (`RemoteIpAddress`) igual a `Residential.IpActual`.
  - `DeviceSn` del reloj cargado.
- Si falla la validacion:
  - `401` si IP no autorizada.
  - `404` si reloj/residential no existe.
  - `422` si el reloj no tiene `DeviceSn`.

### Formatos de entrada soportados
- `application/xml`
- `application/json`
- `multipart/form-data`
- Para `multipart/form-data`:
  - Se toma la parte de evento (`Event_Type` y variantes de nombre).
  - La imagen se ignora en V1.

### Reglas de procesamiento push
- Solo se procesa `eventType = AccessControllerEvent`.
- Si llega otro `eventType` (ej. `heartBeat`), se responde `200` con estado `ignored`.
- Si falta `serialNo`, se responde `200` con estado `ignored` y razon `missing_serial_no`.
- Normalizacion:
  - `deviceSn` = `Reloj.DeviceSn`.
  - `serialNumber` = `AccessControllerEvent.serialNo`.
  - `eventTimeUtc` = `dateTime` convertido a UTC.
  - `timeDevice` = valor original de `dateTime`.
  - `employeeNumber` = prioridad `employeeNoString`, fallback `employeeNo`.
  - `major` = `majorEventType`.
  - `minor` = `subEventType`.
  - `attendanceStatus` = `attendanceStatus`.
  - `raw` = envelope JSON `v1` serializado con propiedades PascalCase:
    - `SchemaVersion`
    - `Source`
    - `Format`
    - `ContentType`
    - `HasPicture`
    - `CapturedAtUtc`
    - `Payload`
  - Se guarda como JSON valido en columna `jsonb`.
  - En V1 no se aplica naming policy camelCase; la serializacion refleja el DTO actual.

### Idempotencia y checkpoint
- Insercion por `AddIfNotExists` con PK `(device_sn, serial_no)`.
- Resultado de push:
  - `inserted`
  - `duplicate`
  - `ignored`
- `LastPushEvent` del reloj se actualiza con regla `max(actual, eventTimeUtc)` en `inserted` y `duplicate`.

### Checklist de configuracion de reloj (operacion)
1. Confirmar que el reloj soporte `httpHosts`:
   - `GET /ISAPI/Event/notification/httpHosts/capabilities`
2. Configurar host de escucha:
   - `PUT /ISAPI/Event/notification/httpHosts...`
3. Configurar `Request_URI` hacia ApiReloj por reloj:
   - `http(s)://<api-reloj>/AccessEvents/push/{relojId}`
4. Habilitar servicio de escucha en el reloj.
5. Probar envio desde reloj:
   - endpoint debe responder `200` para eventos validos/duplicados/ignorados.
6. Verificar que la IP del reloj coincida con `Residential.IpActual` en BD.

## Implementacion V1 Poll/Backfill (estado actual)

### Objetivo implementado
- Worker automatico para backfill por poll de eventos ISAPI.
- Cursor real de poll por reloj: `Reloj.LastPollEvent`.
- Convivencia push + poll con idempotencia por `(DeviceSn, SerialNumber)`.

### Worker y periodicidad
- Worker: `BackfillPollWorker`.
- Frecuencia configurable por `BackfillPolling.WorkerIntervalMinutes` (default 30).
- Puede ejecutar una corrida al inicio (`RunOnStartup=true`).

### Reglas implementadas de cursor
1. Si `LastPollEvent == null`:
   - Se busca el evento mas antiguo disponible (`timeReverseOrder=false`, `maxResults=1`).
   - Si existe oldest: se procesa desde oldest hasta `now` en ventanas de 30 min.
   - Si no existe oldest: se fija `LastPollEvent = now`.
   - Esta decision es independiente de `LastPushEvent`.
2. Si `LastPollEvent != null`:
   - Si `gap <= 30m`: ventana de seguridad `[now-30m, now]`.
   - Si `gap > 30m`: catch-up desde `LastPollEvent` hasta `now` en ventanas de 30 min.
3. `LastPushEvent` queda solo para monitoreo del push.

### Endpoint ISAPI usado por poll
- `POST /ISAPI/AccessControl/AcsEvent?format=json`
- Request con:
  - `searchID`
  - `searchResultPosition`
  - `maxResults`
  - `startTime`
  - `endTime`
  - `timeReverseOrder`
  - `isAttendanceInfo=true`
- Response esperada:
  - `responseStatusStrg`
  - `numOfMatches`
  - `totalMatches`
  - `InfoList[]`

### Paginacion implementada
- Mientras `responseStatusStrg == MORE`, continua paginando.
- Avanza `searchResultPosition += numOfMatches`.
- Finaliza cuando no hay mas matches o estado distinto de `MORE`.

### Persistencia e idempotencia
- Se reutiliza `AccessEventsRepository.AddIfNotExists`.
- Clave unica: `(DeviceSn, SerialNumber)`.
- Push y poll pueden procesar el mismo evento sin duplicar.

### Endpoints admin implementados
- `POST /admin/poll/run`
  - Lanza corrida manual (opcional filtrable por `residentialId` / `relojId`).
- `GET /admin/poll/status`
  - Devuelve estado de la ultima corrida y si hay corrida en ejecucion.
- `GET /admin/poll/runs`
  - Lista historial persistido de corridas (con filtros/paginacion).
- `GET /admin/poll/runs/{runId}`
  - Devuelve detalle de corrida (incluye resultados por reloj).

### Notas operativas
- El reloj debe tener `DeviceSn` cargado en API para poder hacer poll.
- En V1, `DeviceSn` se carga manualmente (no hay autodiscovery por `deviceInfo`).
- El `Residential.IpActual` debe estar actualizado (heartbeat) para reachability.
- Credenciales digest via `ISAPI_USER` y `ISAPI_PASSWORD` (si estan definidas).
- El estado de corridas poll no depende solo de memoria: se persiste y se recupera tras reinicios.
- Robustez de lock de corrida:
  - Si falla la persistencia inicial de corrida, la ejecucion falla en modo fail-fast.
  - La liberacion del lock global esta garantizada incluso cuando fallan persistencias de inicio/cierre.
  - La persistencia de cierre y la actualizacion de estado en memoria se ejecutan en best-effort con logging de error.
