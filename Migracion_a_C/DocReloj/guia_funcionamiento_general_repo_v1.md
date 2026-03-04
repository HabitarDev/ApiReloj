# Guia de Funcionamiento General del Repo (V1)

## 1. Objetivo y alcance
ApiReloj funciona como un proxy hibrido entre el backend y los relojes Hikvision.

En el estado actual del repo, el sistema combina estas responsabilidades:
- Pasamanos de comandos hacia relojes.
- Recepcion de heartbeat para resolver la IP actual del residencial.
- Ingesta de eventos de acceso por push.
- Backfill automatico por poll.
- Persistencia local en PostgreSQL.
- Lectura desde BD local para consultas del backend.
- Generacion de jornadas derivadas a partir de AccessEvents.

La idea principal es desacoplar al backend del reloj para que:
- las lecturas operativas salgan desde la BD local,
- el reloj siga empujando eventos en tiempo real,
- exista un mecanismo de recuperacion ante huecos,
- y el backend use esta API como punto unico de integracion.

## 2. Arquitectura general
### Componentes
- Backend consumidor.
- ApiReloj.
- PostgreSQL local.
- Relojes Hikvision (ISAPI).
- Servicio externo de heartbeat.
- Workers internos:
  - `BackfillPollWorker`
  - `JornadaStatusWorker`

### Diagrama mental rapido
```text
Backend ----HTTP----> ApiReloj ----HTTP----> Relojes Hikvision
                          |
                          +----> PostgreSQL

Servicio Heartbeat ----HTTP----> ApiReloj
Reloj ----push----> ApiReloj ----insert/upsert----> PostgreSQL
ApiReloj ----poll----> Reloj ----reconciliacion----> PostgreSQL
```

### Idea operativa
- El backend no deberia consultar eventos en vivo al reloj.
- Los eventos se consolidan localmente en PostgreSQL.
- El push cubre el tiempo real.
- El poll cubre backfill y seguridad.
- Los comandos de usuarios se reenvian al reloj desde la API.

## 3. Modelo funcional por dominios
### `Residential`
Representa un residencial y guarda la IP actual a la que hay que hablarle para llegar a sus relojes.

Rol funcional:
- Punto de agrupacion de `Reloj`.
- Punto de agrupacion de `Device`.
- Base de resolucion de destino para el proxy a ISAPI.

Campos funcionales relevantes:
- Identificador del residencial.
- `IpActual`.

### `Device`
Representa el emisor autorizado de heartbeat.

Rol funcional:
- Aporta la `SecretKey` usada para validar HMAC.
- Permite decidir si un heartbeat pertenece al residencial correcto.
- Guarda `LastSeen`.

### `Reloj`
Representa un reloj Hikvision configurado en la API.

Rol funcional:
- Define el puerto del reloj.
- Permite guardar `DeviceSn`.
- Guarda cursores de integracion:
  - `LastPushEvent`
  - `LastPollEvent`

### `AccessEvents`
Es la entidad de persistencia local de eventos de acceso.

Rol funcional:
- Cache local durable de eventos.
- Fuente de verdad para `GET /AccessEvents`.
- Input para generacion de jornadas.

Campos funcionales relevantes:
- `DeviceSn`
- `SerialNumber`
- `EventTimeUtc`
- `EmployeeNumber`
- `Major`
- `Minor`
- `AttendanceStatus`
- `Raw`

Nota:
- `Raw` se guarda en `jsonb` como envelope JSON valido.

### `Jornada`
Es una vertical derivada.

Rol funcional:
- Agrupa una secuencia laboral a partir de `AccessEvents`.
- Se consulta por `GET /Jornadas`.
- Se completa automaticamente al insertar eventos.

### `BackfillPollRunLog`
Es la auditoria durable de corridas de poll.

Rol funcional:
- Guarda historial de corridas.
- Permite inspeccion post-restart.
- Soporta `GET /admin/poll/runs` y `GET /admin/poll/runs/{runId}`.

## 4. Pasamanos / proxy de comandos
### Que hace
La API recibe comandos del backend y los reenvia a los relojes via ISAPI.

En V1 actual, el pasamanos expuesto esta centrado en operaciones de usuarios:
- `POST /UsersControllers`
- `PUT /UsersControllers`
- `DELETE /UsersControllers`

### Como resuelve destino
1. Toma el `Residential` indicado por el request.
2. Usa `IpActual` del residencial.
3. Recorre todos los `Relojes` asociados.
4. Arma la URL ISAPI para cada reloj segun su puerto.
5. Reenvia la operacion a cada reloj.

### Fan-out actual
Este es un detalle importante del comportamiento real:
- Hoy `UsersControllers` opera por residencial.
- No opera contra un reloj puntual.
- La API hace fan-out a todos los relojes del residencial.

Consecuencias:
- Una sola request puede impactar varios relojes.
- Si falla uno de los relojes, la operacion global puede fallar aunque otros ya hayan aplicado el cambio.
- El comportamiento es intencional para V1.

### Limite actual
No existe aun un endpoint para operar por `relojId` puntual.

Eso queda como extension futura recomendada, pero no forma parte del contrato actual del repo.

## 5. Recepcion de heartbeats
### Endpoint real
- `POST /Residential/heartbeat`

### Que recibe
Recibe un heartbeat emitido por el servicio externo documentado en `DocHeartBeat/README.md`.

Payload funcional:
- `DeviceId`
- `ResidentialId`
- `TimeStamp`
- `Signature`

### Como valida
1. Busca el `Residential`.
2. Busca el `Device`.
3. Verifica que el device pertenezca al residencial.
4. Recalcula la firma HMAC con:
   - `timeStamp`
   - `deviceId`
   - `residentialId`
   - `secretKey`
5. Si coincide, se aprueba el heartbeat.

### Efectos si la firma es valida
- Actualiza `IpActual` del residencial con la IP remota real de la request.
- Actualiza `LastSeen` del device usando el timestamp recibido.

### Efectos si la firma NO es valida
- El comportamiento actual es no-op.
- El endpoint responde sin body.
- En el runtime real, la accion es `void`, por lo que el exito/no-op se observa como `204 No Content`.

### Dependencias con el resto del sistema
El heartbeat es clave porque:
- permite saber a que IP enviar comandos ISAPI,
- impacta el proxy de usuarios,
- impacta cualquier otra integracion que dependa de `IpActual`.

## 6. Recepcion de eventos de acceso (push)
### Endpoint real
- `POST /AccessEvents/push/{relojId}`

### Que hace
Recibe el push del reloj y lo inserta en la BD local con idempotencia.

### Seguridad de ingreso
Antes de procesar el evento:
- corre `AuthorizationPushFilter`,
- valida que la IP remota sea la esperada,
- valida que el `relojId` corresponda,
- valida que el `DeviceSn` del evento coincida con el reloj configurado.

### Formatos soportados
El endpoint soporta:
- `application/json`
- `application/xml`
- `text/xml`
- `multipart/form-data`

En multipart:
- intenta extraer el payload del evento desde keys conocidas,
- detecta si llego imagen adjunta.

### Reglas funcionales
1. Se parsea el payload.
2. Se valida que el tipo de evento sea `AccessControllerEvent`.
3. Si no lo es, se responde `ignored`.
4. Si falta `serialNo`, se responde `ignored`.
5. Se normaliza a `AccesEventDto`.
6. Se inserta con idempotencia por:
   - `DeviceSn`
   - `SerialNumber`
7. Si fue insertado:
   - se actualiza `LastPushEvent` del reloj,
   - se dispara la logica de jornadas.
8. Si era duplicado:
   - no se reinserta,
   - pero el push sigue respondiendo OK con estado `duplicate`.

### Persistencia de `Raw`
`Raw` se guarda como JSON valido en columna `jsonb`, usando un envelope con propiedades PascalCase:
- `SchemaVersion`
- `Source`
- `Format`
- `ContentType`
- `HasPicture`
- `CapturedAtUtc`
- `Payload`

Esto aplica tanto para push como para poll.

## 7. Backfill / poll automatico
### Endpoints y worker
El backfill tiene dos caras:
- worker periodico (`BackfillPollWorker`)
- endpoints admin:
  - `POST /admin/poll/run`
  - `GET /admin/poll/status`
  - `GET /admin/poll/runs`
  - `GET /admin/poll/runs/{runId}`

### Objetivo
Cubrir huecos de eventos y mantener una ventana de seguridad aunque el push exista.

### Cursor real
El cursor de poll es `Reloj.LastPollEvent`.

`LastPushEvent` existe solo como observabilidad del flujo push.
No mueve el cursor del backfill.

### Regla de bootstrap
Si `LastPollEvent == null`:
- el sistema consulta el evento mas viejo disponible,
- arranca desde ahi,
- aunque exista `LastPushEvent`.

Esto asegura que una instalacion que ya recibio push reciente igual pueda backfillear historia vieja si nunca corrio el worker.

### Regla de gap
Si `LastPollEvent != null`:
- si el gap es menor o igual a la ventana configurada, se corre una ventana de seguridad;
- si el gap es mayor, se hace catch-up por ventanas sucesivas hasta alcanzar `now`.

### Paginacion
Cada ventana usa:
- `searchID`
- `searchResultPosition`
- `maxResults`

Si la respuesta indica `MORE`, sigue pidiendo paginas hasta completar la ventana.

### Idempotencia
Los eventos obtenidos por poll usan la misma tabla `AccessEvents` y la misma regla de deduplicacion que push.

Esto permite convivencia push + poll sin duplicados persistidos.

### Persistencia de corridas
Cada corrida queda guardada en `BackfillPollRunLog`.

Esto permite:
- conocer estado de la ultima corrida,
- listar historico,
- inspeccionar detalle por reloj,
- no perder observabilidad al reiniciar la app.

### Lock de corrida
Existe un lock global para evitar corridas concurrentes.

El estado actual del repo ya contempla:
- liberacion segura del lock,
- cierre best-effort de observabilidad,
- y fail-fast si falla el inicio de corrida.

## 8. Consultas desde BD local
### Principio general
Las consultas del backend se hacen contra PostgreSQL local.

En el diseño actual:
- leer eventos no requiere preguntarle al reloj,
- leer jornadas tampoco requiere preguntarle al reloj.

### Endpoints principales
- `GET /AccessEvents`
- `GET /Jornadas`

### Beneficio operativo
Esto:
- reduce acoplamiento con disponibilidad del reloj,
- simplifica consultas repetidas,
- permite filtros y paginacion sobre datos locales,
- y desacopla lectura de la capa ISAPI.

## 9. Jornadas (vertical derivada)
### Rol dentro del repo
`Jornada` no es el nucleo de infra hibrida, pero es una vertical completa ya implementada.

### Como se alimenta
- Se deriva automaticamente desde `AccessEvents`.
- No existe endpoint de escritura de jornadas.
- Solo hay consulta por `GET /Jornadas`.

### Saneamiento
Existe un worker (`JornadaStatusWorker`) que marca jornadas incompletas vencidas como error segun timeout configurable.

### Valor funcional
Permite exponer al backend una vista laboral mas interpretable que el evento crudo.

## 10. Observabilidad y trazabilidad
### Que queda persistido
- Eventos de acceso en `AccessEvents`.
- Jornadas en `Jornadas`.
- Corridas de backfill en `BackfillPollRunLog`.

### Que queda principalmente en logs
Fuera del historial de poll, la observabilidad del sistema esta basada principalmente en logs crudos.

Eso significa:
- no hay una capa propia de metricas persistentes globales,
- no hay indexado/categorizacion interna de errores,
- no hay un tablero propio de contadores acumulados del sistema.

Esto no se trata como un bug en V1.
Es simplemente el alcance actual del repo.

### Trazabilidad practica
Para diagnostico:
- `AccessEvents` guarda `Raw`.
- `BackfillPollRunLog` guarda resumen y detalle de cada corrida.
- Los controllers y services emiten logs utiles para reconstruir flujo.

## 11. Mapa de codigo para mantenimiento
### Si falla heartbeat
Mirar:
- `WebApplication1/Controllers/ResidentialController.cs`
- `Service/ResidentialServicess/ResidentialService.cs`
- `Service/ResidentialServicess/ResidentialMantenimientoService.cs`
- `DocReloj/DocHeartBeat/README.md`

### Si falla push
Mirar:
- `WebApplication1/Controllers/AccessEventsController.cs`
- `WebApplication1/Filters/AuthorizationPushFilter.cs`
- `Service/AccesEventsServicess/AccesEventMantentimientoService.cs`
- `Service/AccesEventsServicess/AccesEventEntityService.cs`

### Si falla poll / backfill
Mirar:
- `WebApplication1/Controllers/AdminPollController.cs`
- `WebApplication1/Workers/BackfillPollWorker.cs`
- `Service/BackfillServicess/BackfillPollMantenimientoService.cs`
- `Service/BackfillServicess/HikvisionAcsEventClient.cs`

### Si falla la lectura de eventos
Mirar:
- `Models/WebApi/AccessEventsQueryDto.cs`
- `Service/AccesEventsServicess/AccesEventValidationService.cs`
- `DataAcces/Repositories/AccessEventsRepository.cs`

### Si falla la vertical de jornadas
Mirar:
- `WebApplication1/Controllers/JornadasController.cs`
- `WebApplication1/Workers/JornadaStatusWorker.cs`
- `Service/JornadaServicess/*`

### Si falla el proxy de usuarios
Mirar:
- `WebApplication1/Controllers/UsersControllers.cs`
- `Service/UserServicess/UserService.cs`
- `Models/WebApi/Users/*`

### Si falla configuracion de maestros
Mirar:
- `WebApplication1/Controllers/ResidentialController.cs`
- `WebApplication1/Controllers/DeviceController.cs`
- `WebApplication1/Controllers/RelojController.cs`
- `Service/ResidentialServicess/*`
- `Service/DeviceServicess/*`
- `Service/RelojServicess/*`

## 12. Limitaciones y decisiones de V1
Estas son decisiones o limites vigentes del repo:
- Las rutas legacy actuales son el contrato operativo.
- Las rutas REST normalizadas `/api/v1/...` quedan como objetivo futuro, no implementado.
- Usuarios por `relojId` puntual no estan implementados.
- `UsersControllers` trabaja por residencial y hace fan-out.
- `verifyMode` no se expone como filtro de lectura; la clasificacion operativa actual usa `major` y `minor`.
- La observabilidad global sigue basada mayormente en logs crudos, salvo el historial durable de poll.
- La app prioriza compatibilidad y cierre funcional de V1, no una version final de API publica "perfecta".

## 13. Referencias rapidas
Para detalle especializado, ver tambien:
- `infra_hibrida.md`
- `api_access_events_v1.md`
- `api_poll_backfill_v1.md`
- `api_jornadas_v1.md`
- `DocHeartBeat/README.md`

## 14. Resumen operativo rapido
Si alguien necesita un resumen en 30 segundos:
- El heartbeat resuelve la IP actual del residencial.
- El push del reloj ingresa eventos en tiempo real.
- El poll rellena huecos y aporta seguridad.
- El backend lee `AccessEvents` y `Jornadas` desde PostgreSQL local.
- Los comandos de usuarios se reenvian desde la API hacia todos los relojes del residencial.
- Las jornadas se derivan automaticamente desde los eventos.
