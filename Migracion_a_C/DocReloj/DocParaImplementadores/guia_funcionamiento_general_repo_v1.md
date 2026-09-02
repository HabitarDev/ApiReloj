# Guía de funcionamiento general de ApiReloj V1

**Contrato revisado contra código:** 1 de septiembre de 2026.

## 1. Propósito

ApiReloj es la capa de integración entre el backend de negocio y relojes de acceso Hikvision. Conserva eventos en PostgreSQL, recupera huecos mediante ISAPI, deriva jornadas laborales y reenvía comandos de usuarios hacia los relojes.

El backend de negocio puede calcular horas, sueldo y sanciones consumiendo jornadas semiprocesadas. ApiReloj no calcula remuneraciones.

## 2. Actores y responsabilidades

| Actor | Responsabilidad |
|---|---|
| Emisor heartbeat | Informa periódicamente la IP pública actual del residencial. |
| Reloj Hikvision | Envía eventos por push y recibe comandos ISAPI de usuarios. |
| ApiReloj | Autentica, conserva eventos, ejecuta poll, reconstruye jornadas y sirve consultas. |
| Backend | Administra maestros, usuarios, polling y reconstrucciones; consume eventos y jornadas. |
| PostgreSQL | Fuente persistente de eventos, cursores, corridas, jornadas y cola de proyección. |

## 3. Seguridad por flujo

ApiReloj es cerrada por defecto:

- Backend: API key `X-Api-Key` y una IP fija autorizada.
- Heartbeat: HMAC en el body original, timestamp reciente y antireplay.
- Push: IP actual del residencial correspondiente al `relojId`.

El heartbeat y el push no necesitan la API key del backend. Ver `seguridad_endpoints.md`.

## 4. Modelo principal

### Residential

Representa un residencial. Su `IpActual` se actualiza mediante heartbeat y se utiliza para:

- autenticar push;
- consultar relojes con ISAPI;
- enviar altas, modificaciones y bajas de usuarios.

### Device

Representa al equipo que envía heartbeat. Pertenece a un residencial y conserva:

- clave HMAC secreta;
- `LastSeen`;
- último timestamp aceptado para impedir replay.

La clave es write-only y no se devuelve por API.

### Reloj

Representa un reloj Hikvision dentro de un residencial. Mantiene:

- identificador string;
- puerto;
- `DeviceSn`;
- `LastPushEvent`;
- `LastPollEvent`.

### AccessEvent

Es el registro inmutable normalizado desde push o poll. Su identidad idempotente es `DeviceSn + SerialNumber`. Conserva el payload original dentro de un envelope JSON `_raw` y fija `ResidentialId` durante la ingesta; reasignar o borrar el reloj no cambia esa pertenencia.

### Jornada

Es una proyección reconstruible de los eventos de un empleado dentro de un residencial. Incluye marcas, advertencias, errores, revisión, estado de proyección y tombstone.

### JornadaProjectionState

Es la cola persistente de reconstrucción por `EmployeeNumber + ResidentialId`. Registra suciedad, revisiones, intentos, próximos reintentos y errores.

### BackfillPollRunLog

Conserva el inicio, cierre, estado, métricas y detalle por reloj de cada corrida de polling.

## 5. Flujo de heartbeat

Endpoint: `POST /Residential/heartbeat`.

1. El emisor manda `deviceId`, `residentialId`, `timeStamp` y `signature` en JSON.
2. La autenticación valida contenido, asociación, HMAC y ventana temporal.
3. En una transacción, ApiReloj intenta avanzar el timestamp antireplay.
4. Si avanza, actualiza `Device.LastSeen` y `Residential.IpActual`.
5. Si es un replay válido, no modifica datos.
6. Ambos casos válidos responden `204`.

Una petición inválida responde `401`. El emisor actual no necesita interpretar el body de la respuesta y no debe cambiar su payload.

## 6. Flujo de push

Endpoint: `POST /AccessEvents/push/{relojId}`.

1. La política comprueba que la IP origen sea la del residencial del reloj.
2. El controlador acepta JSON, XML, `text/xml` o multipart con imagen opcional.
3. El servicio verifica que sea un `AccessControllerEvent` y que tenga `serialNo`.
4. Si el payload trae `deviceID`, debe coincidir con el `DeviceSn` configurado.
5. Se normaliza el evento y se intenta insertar por `DeviceSn + SerialNumber`.
6. Si se inserta, en la misma transacción se actualiza el cursor push y se encola la clave empleado + residencial para jornadas.
7. El worker reconstruye las jornadas posteriormente.

El resultado funcional puede ser `inserted`, `duplicate` o `ignored`; los tres se devuelven con `200` cuando la petición fue procesable.

## 7. Flujo de poll y backfill

`BackfillPollWorker` ejecuta corridas programadas. Los endpoints `/admin/poll/*` permiten operación manual y diagnóstico.

Antes de consultar ISAPI se persiste el snapshot completo de candidatos en estado `pending`. El progreso y las métricas se guardan después de cada reloj. Si el proceso termina abruptamente, el siguiente arranque recupera idempotentemente el run como `error`, completa su fecha y convierte pendientes en errores.

Por reloj:

1. Usa `Residential.IpActual`, puerto y credenciales Digest opcionales.
2. Consulta `/ISAPI/AccessControl/AcsEvent?format=json`.
3. Si no existe cursor, busca el evento más antiguo desde `BootstrapStartUtc`.
4. Divide el rango en ventanas configurables.
5. Pagina con `searchResultPosition`, `maxResults` y el mismo `searchID` por ventana.
6. Inserta idempotentemente cada página.
7. Avanza `LastPollEvent` solamente al completar una ventana.

Si el gap es pequeño se vuelve a consultar una ventana reciente como red de seguridad. Los duplicados no dañan el estado.

## 8. Procesamiento de jornadas

El evento y la solicitud de proyección se guardan en la misma transacción. Por eso un fallo posterior no deja un evento imposible de reprocesar.

`JornadaProcessingWorker`:

1. reclama una clave con `FOR UPDATE SKIP LOCKED`;
2. carga todos los eventos del empleado y residencial;
3. ordena por `EventTimeUtc`, luego `SerialNumber` y finalmente `DeviceSn`;
4. reconstruye desde cero las jornadas de esa clave;
5. reemplaza la proyección y marca la revisión aplicada en una transacción;
6. reintenta errores con backoff y deja diagnóstico persistente.

Esto permite eventos históricos fuera de orden, múltiples instancias y reinicios. Dos workers pueden procesar empleados distintos, pero una clave se serializa mediante el lock de PostgreSQL.

### Reglas laborales representadas

- Ámbito: empleado + residencial, aunque entre por un reloj y salga por otro.
- Una pausa máxima por jornada.
- Duración máxima rígida de 24 horas.
- Dobles marcas: se conserva la primera y se genera advertencia.
- Un evento posterior a una jornada vencida no se adjunta a ella.
- Las jornadas son consistentes eventualmente, normalmente en segundos.

El backend debe consumir `revision`, `projectionStatus`, `updatedAt` e `isDeleted`, especialmente para cálculos recalculables.

## 9. Proxy de usuarios

`POST`, `PUT` y `DELETE /UsersControllers` reciben operaciones del backend y hacen fan-out a todos los relojes del residencial.

Las rutas ISAPI utilizadas son:

- alta: `UserInfo/SetUp?format=json`;
- modificación: `UserInfo/Modify?format=json`;
- baja: `UserInfoDetail/Delete?format=json`.

Si están definidas, `ISAPI_USER` e `ISAPI_PASSWORD` habilitan autenticación Digest.

Limitación vigente: no existe transacción distribuida entre relojes. Si algunos aceptan una operación y otro falla, el endpoint devuelve error, pero no revierte los relojes ya modificados.

## 10. Consultas locales

- `GET /AccessEvents`: consulta eventos persistidos; no contacta al reloj.
- `GET /Jornadas`: consulta la proyección persistida; no reconstruye en línea.
- `GET /admin/poll/*`: estado e historial de polling.
- `GET /admin/jornadas/projection-states`: diagnóstico de proyecciones.

## 11. Workers

| Worker | Función |
|---|---|
| `BackfillPollWorker` | Recupera eventos históricos y cubre huecos. |
| `JornadaProcessingWorker` | Reconstruye jornadas pendientes con orden y reintentos. |
| `JornadaStatusWorker` | Encola nuevamente las claves con jornadas incompletas vencidas. |

## 12. Migraciones y arranque

Al iniciar, ApiReloj ejecuta `Database.Migrate()`. Si PostgreSQL no está accesible o una migración falla, la aplicación no inicia. Esta decisión evita ejecutar código contra un esquema desactualizado.

Después de migrar se recuperan los poll runs huérfanos y recién entonces se habilitan los hosted workers. También se valida la configuración de seguridad durante el arranque.

## 13. Compatibilidad ISAPI

El rediseño de seguridad y jornadas no cambió:

- body del heartbeat;
- ruta del push;
- JSON, XML o multipart del push;
- endpoint y paginación del poll;
- rutas de usuarios;
- deduplicación;
- conservación de `_raw`.

`residentialId` es metadata interna: ISAPI no necesita enviarlo en los eventos porque ApiReloj lo fija desde el reloj autenticado o consultado al ingerir. Los históricos anteriores sin pertenencia demostrable quedan en `__legacy__`, sin inferirse a partir de la relación actual.

## 14. Limitaciones conocidas

- La IP se toma de `RemoteIpAddress` después de procesar forwarded headers exclusivamente desde proxies o redes confiables configurados explícitamente.
- El fan-out de usuarios puede quedar parcialmente aplicado.
- La exclusión mutua del poll es por proceso; el despliegue soportado requiere una réplica.
- No hay cálculo de horas, salario ni sanciones en este repositorio.
- No hay versionado de rutas `/api/v1`; las rutas documentadas son las activas.

## 15. Mapa de mantenimiento

- Arranque y DI: `WebApplication1/Program.cs`.
- Controladores: `WebApplication1/Controllers/`.
- Políticas: `WebApplication1/Security/`.
- Ingesta: `Service/AccesEventsServicess/`.
- Proyección: `Service/JornadaServicess/`.
- Poll: `Service/BackfillServicess/`.
- Persistencia: `DataAcces/Repositories/` y `DataAcces/Migrations/`.
- Pruebas: `Service.Tests/`.
