# Infra hibrida: proxy + cache de eventos (v1)

## Objetivo
Convertir ApiReloj en un proxy hibrido:
- Para eventos de acceso: suscribir, recibir y almacenar en BD local.
- Para comandos (crear persona, etc.): pasamanos hacia el reloj correcto.
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

### 2) Eventos por push (tiempo real)
1. Reloj envia eventos al servidor (httpHosts o alertStream).
2. ApiReloj parsea el payload (XML/JSON o multipart).
3. ApiReloj normaliza y guarda evento en BD con idempotencia.

### 3) Backfill por poll (cada 30 min)
1. Cada 30 min ApiReloj consulta cada reloj.
2. Determina la ventana a pedir:
   - Si no hay last_event_time: buscar evento mas antiguo disponible y usar su hora como inicio.
   - Si hay last_event_time y el gap <= 30 min: pedir [now-30m, now].
   - Si hay last_event_time y el gap > 30 min: pedir en ventanas de 30 min desde last_event_time hasta now.
3. Guarda eventos con idempotencia (no duplica).

### 4) Consultas del backend
1. Backend pide eventos por empleado/rango.
2. ApiReloj consulta la BD local.
3. Responde sin consultar al reloj.

### 5) Comandos del backend (proxy)
1. Backend llama un endpoint local (ej. CreatePerson).
2. ApiReloj resuelve IP/puerto del reloj por residential.
3. ApiReloj reenvia la request a ISAPI.
4. ApiReloj devuelve la respuesta al backend.

## Endpoints propuestos (contratos normalizados)
### A) Backend -> ApiReloj (publicos)

#### Crear persona (normalizado)
**POST** `/api/v1/residentials/{residentialId}/relojes/{relojId}/persons`

Body normalizado (desde backend):
```json
{
  "employeeNo": "123",
  "name": "Juan Perez",
  "userType": "normal"
}
```

Traduccion a ISAPI (ejemplo):
```
PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json
{
  "UserInfo": {
    "employeeNo": "123",
    "name": "Juan Perez",
    "userType": "normal"
  }
}
```

Notas:
- Solo se normaliza persona (sin huellas, sin cara).
- `userType` segun guia: normal, visitor, blacklist, administrators.
- El reloj puede aceptar `employeeNoString`; ApiReloj debe ajustar segun capabilities.

#### Modificar persona (normalizado)
**PUT** `/api/v1/residentials/{residentialId}/relojes/{relojId}/persons/{employeeNo}`

Body normalizado:
```json
{
  "name": "Juan Perez",
  "userType": "normal"
}
```

Traduccion a ISAPI (ejemplo):
```
PUT /ISAPI/AccessControl/UserInfo/Modify?format=json
{
  "UserInfo": {
    "employeeNo": "123",
    "name": "Juan Perez",
    "userType": "normal"
  }
}
```

#### Eliminar persona (normalizado)
**DELETE** `/api/v1/residentials/{residentialId}/relojes/{relojId}/persons/{employeeNo}`

Traduccion a ISAPI (ejemplo):
```
PUT /ISAPI/AccessControl/UserInfoDetail/Delete?format=json
{
  "UserInfoDetail": {
    "mode": "byEmployeeNo",
    "EmployeeNoList": [
      { "employeeNo": "123" }
    ]
  }
}
```

Opcional en ISAPI:
- `operateType`: byTerminal / byOrg / byTerminalOrg
- `terminalNoList` / `orgNoList`

#### Consultar eventos (desde BD local)
**GET** `/api/v1/residentials/{residentialId}/events`

Query sugerida:
- `from`, `to` (ISO 8601)
- `employeeNo`
- `deviceSn`
- `attendanceStatus`
- `verifyMode`
- `limit`, `offset`

Respuesta (ejemplo):
```json
{
  "items": [
    {
      "deviceSn": "DS-K1T...",
      "serialNo": 987654,
      "eventTimeUtc": "2025-09-01T12:00:00Z",
      "employeeNo": "123",
      "major": 5,
      "minor": 38,
      "attendanceStatus": "checkIn"
    }
  ]
}
```

### B) Reloj -> ApiReloj (ingesta push)
**POST** `/api/v1/ingest/hikvision/events`
- Recibe eventos del reloj (XML/JSON o multipart).
- Inserta en BD con idempotencia.

### C) Heartbeat
**POST** `/api/v1/heartbeat`
```json
{
  "DeviceId": 1,
  "ResidentialId": 10,
  "TimeStamp": 1730000000,
  "Signature": "HEX_HMAC"
}
```

### D) Admin / Jobs (interno)
- **POST** `/api/v1/admin/poll/run` (opcional por residential/reloj)
- **GET** `/api/v1/admin/poll/status`

## Push vs Poll (resumen)
- Push: baja latencia, menor carga del reloj, depende de reachability desde reloj.
- Poll: mas robusto para backfill, requiere reachability desde servidor al reloj.
- Decision: usar ambos. Push como fuente principal y poll cada 30 min para backfill.

## Backfill cada 30 min (logica)
- Periodicidad: 30 min.
- Ventana base: 30 min.
- Si el gap es grande, iterar en ventanas de 30 min hasta alcanzar now.
- Paginacion: usar searchID, searchResultPosition, maxResults.

Pseudo:
```
if !last_poll_event:
  last_poll_event = get_oldest_event_time()

gap = now - last_poll_event
if gap <= 30m:
  poll(now-30m, now)
else:
  for window in [last_poll_event, now] step 30m:
    poll(window.start, window.end)
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
- Logs de requests/errores y tiempos.
- Contadores: eventos recibidos, insertados, duplicados, errores.
- Registros de polling: paginas, duracion, status ISAPI.

## Riesgos y mitigaciones
- Buffer del reloj limitado: solo se recupera lo que el reloj retiene.
- Caidas largas: si ApiReloj cae mas tiempo que el buffer del reloj, se perderan eventos.
- Duplicados: controlados por idempotencia.
- Drift de hora: usar event_time y normalizar a UTC.

## Notas de implementacion
- Resolver device_sn via /ISAPI/System/deviceInfo.
- Guardar raw para re-procesos futuros.
- Filtrar eventos de asistencia en lectura (o guardar solo los relevantes).
- Para obtener el evento mas antiguo disponible: POST /ISAPI/AccessControl/AcsEvent?format=json
  con startTime muy antiguo, endTime=now, timeReverseOrder=false y maxResults=1.
- `Last_Push_Event` se actualiza solo al recibir push.
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
- `EventsQueryDto`: from, to, employeeNo, attendanceStatus, limit, offset.
- `EventsResponseDto`: items[], total?, paging?.

## Decisiones acordadas
- Modo hibrido: push + poll.
- Backfill cada 30 min en ventanas de 30 min.
- Guardado permanente.
- Idempotencia: device_sn + serial_no.
- Si no hay last_event_time, iniciar desde el evento mas antiguo disponible.
- Estado de polling en Reloj: Last_Push_Event y Last_Poll_Event.
