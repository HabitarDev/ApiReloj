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
if !last_event_time:
  last_event_time = get_oldest_event_time()

gap = now - last_event_time
if gap <= 30m:
  poll(now-30m, now)
else:
  for window in [last_event_time, now] step 30m:
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

Checkpoint por dispositivo:
```
poller_checkpoint (
  device_sn text primary key,
  last_serial_no bigint,
  last_event_time_utc timestamptz,
  updated_at timestamptz
)
```

## Seguridad y red
- Auth: HTTP Digest.
- TLS: preferir HTTPS con CA del dispositivo.
- Push: requiere que reloj alcance el servidor (NAT o VPN).
- Poll: requiere que servidor alcance el reloj (NAT o VPN).

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

## Decisiones acordadas
- Modo hibrido: push + poll.
- Backfill cada 30 min en ventanas de 30 min.
- Guardado permanente.
- Idempotencia: device_sn + serial_no.
- Si no hay last_event_time, iniciar desde el evento mas antiguo disponible.
