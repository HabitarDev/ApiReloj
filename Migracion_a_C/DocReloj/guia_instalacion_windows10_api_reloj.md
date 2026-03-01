# Guía de Instalación y Puesta en Marcha
## API Reloj en Windows 10 + PostgreSQL en Docker Desktop

## 0) Estado del sistema (actualizado)
Los 2 bloqueantes que estaban documentados al inicio de esta guía ya fueron resueltos en código:

1. `PUT /Reloj` ya persiste cambios (`Puerto` y `DeviceSn`).
- Referencia: `Migracion_a_C/WebApplication1/Service/RelojServicess/RelojMantenimientoService.cs`.

2. `UsersControllers` ya tiene servicios registrados en DI.
- Referencias:
  - `Migracion_a_C/WebApplication1/WebApplication1/Controllers/UsersControllers.cs`
  - `Migracion_a_C/WebApplication1/WebApplication1/Program.cs`
  - `Migracion_a_C/WebApplication1/Models/WebApi/Users/FromBack/ModifiUserDtoFromBack.cs`

Regla de esta guía:
1. La guía ahora es ejecutable end-to-end con el estado actual del repo.
2. Igualmente se mantienen checks explícitos para detectar regresiones en estos puntos.

---

## 1) Objetivo y alcance
Objetivo: dejar el sistema operativo en una máquina nueva de Windows 10 con:
1. API corriendo en Windows.
2. PostgreSQL corriendo en Docker Desktop.
3. Esquema de BD creado por migraciones EF.
4. Flujo real de alta de maestros (`Residential`, `Device`, `Reloj`) y pruebas de heartbeat/push/poll.
5. Validaciones de lectura por `AccessEvents` y `Jornadas`.

Fuera de alcance:
1. Hardening productivo avanzado (reverse proxy, certificados, monitoreo centralizado).
2. Deploy como servicio Windows de la API (en esta guía se ejecuta por consola para pruebas).

---

## 2) Tecnologías del repo
1. API: ASP.NET Core Web API (.NET 10).
2. Persistencia: Entity Framework Core 10 + Npgsql.
3. Base de datos: PostgreSQL 16 (contenedor Docker).
4. Integración reloj: ISAPI Hikvision (Digest opcional por `ISAPI_USER`/`ISAPI_PASSWORD`).
5. Workers:
   - `JornadaStatusWorker`
   - `BackfillPollWorker`

Rutas clave:
1. Solución: `Migracion_a_C/WebApplication1/WebApplication1.sln`
2. Proyecto startup API: `Migracion_a_C/WebApplication1/WebApplication1/WebApplication1.csproj`
3. Proyecto migraciones: `Migracion_a_C/WebApplication1/DataAcces/DataAcces.csproj`
4. Compose DB: `Migracion_a_C/docker-compose.yml`
5. Variables compose: `Migracion_a_C/.env`

---

## 3) Arquitectura de ejecución elegida
Ejecución local definida para esta guía:
1. API en Windows host.
2. PostgreSQL en contenedor Docker Desktop.
3. Relojes/dispositivos de la LAN consumen la API en `http://<IP_WINDOWS>:8080`.

Flujo funcional esperado:
1. Heartbeat actualiza `Residential.IpActual`.
2. Push del reloj ingesta eventos con idempotencia.
3. Poll/backfill completa históricos usando `LastPollEvent`.
4. `GET /AccessEvents` y `GET /Jornadas` consultan BD local.

---

## 4) Prerrequisitos externos
### 4.1 Requisitos de máquina base (ya disponibles según contexto)
1. Windows 10.
2. Docker Desktop.
3. Git.
4. WSL + Ubuntu (no obligatorio para esta guía, pero instalado).

### 4.2 Requisitos adicionales a instalar
1. .NET SDK 10 en Windows (obligatorio para `dotnet run` y `dotnet ef`).
2. Herramienta `dotnet-ef` (global o local).

### 4.3 Requisitos de red y entorno real
1. El reloj Hikvision debe ser accesible desde la máquina Windows por IP/puerto.
2. El emisor real de heartbeat debe poder llegar a `http://<IP_WINDOWS>:8080/Residential/heartbeat`.
3. Firewall de Windows debe permitir entrada por puerto `8080` desde la LAN.

### 4.4 Requisitos funcionales externos
1. Debe existir un emisor real de heartbeat (servicio/dispositivo externo).
2. Debes conocer credenciales ISAPI del reloj si requiere Digest.

---

## 5) Configuración requerida
## 5.1 Configuración de base de datos (Docker compose)
Archivo: `Migracion_a_C/.env`

Valores base sugeridos:
```env
POSTGRES_USER=apireloj
POSTGRES_PASSWORD=apireloj
POSTGRES_DB=apireloj
POSTGRES_PORT=5432
```

## 5.2 Connection string de la API
Archivo: `Migracion_a_C/WebApplication1/WebApplication1/appsettings.json`

Valor por defecto actual:
```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=apireloj;Username=apireloj;Password=apireloj"
}
```

Si cambias usuario/password/puerto en `.env`, debes alinear este valor.

## 5.3 Variables de entorno ISAPI (opcional según reloj)
Usadas por:
1. `UserService`
2. `HikvisionAcsEventClient`

Variables:
1. `ISAPI_USER`
2. `ISAPI_PASSWORD`

Ejemplo PowerShell (sesión actual):
```powershell
$env:ISAPI_USER = "admin"
$env:ISAPI_PASSWORD = "tu_password"
```

Persistente (nueva sesión):
```powershell
setx ISAPI_USER "admin"
setx ISAPI_PASSWORD "tu_password"
```

## 5.4 Puertos
1. API: `8080` (recomendado para pruebas LAN).
2. PostgreSQL host: `5432` (mapeado desde Docker).

---

## 6) Preparación de entorno en Windows 10
## 6.1 Instalar .NET SDK 10
Opciones:
1. Instalador oficial de .NET 10 SDK (recomendado si `winget` falla).
2. `winget` (si disponible en tu equipo).

Verificación:
```powershell
dotnet --info
dotnet --version
```

## 6.2 Instalar dotnet-ef
```powershell
dotnet tool install --global dotnet-ef --version 10.*
```

Si ya estaba:
```powershell
dotnet tool update --global dotnet-ef --version 10.*
```

Verificación:
```powershell
dotnet ef --version
```

## 6.3 Verificar Docker Desktop
```powershell
docker --version
docker compose version
docker info
```

---

## 7) Levantar PostgreSQL con Docker
Desde PowerShell:
```powershell
cd C:\ruta\al\repo\ApiReloj\Migracion_a_C
docker compose up -d postgres
docker compose ps
docker logs apireloj-postgres --tail 100
```

Criterio OK:
1. Contenedor `apireloj-postgres` en estado `running`.
2. Sin errores de inicialización en logs.

---

## 8) Migración de base de datos (EF Core)
Regla oficial: usar migraciones de `DataAcces/Migrations` (no `migration.sql`).

Desde PowerShell:
```powershell
cd C:\ruta\al\repo\ApiReloj\Migracion_a_C\WebApplication1
dotnet ef database update --project .\DataAcces\DataAcces.csproj --startup-project .\WebApplication1\WebApplication1.csproj
```

Verificación de tablas:
```powershell
docker exec -it apireloj-postgres psql -U apireloj -d apireloj -c "\dt"
```

Debe incluir al menos:
1. `Residentials`
2. `Devices`
3. `Relojes`
4. `AccessEvents`
5. `Jornadas`

---

## 9) Arranque de la API
Desde PowerShell:
```powershell
cd C:\ruta\al\repo\ApiReloj\Migracion_a_C\WebApplication1\WebApplication1
$env:ASPNETCORE_URLS = "http://0.0.0.0:8080"
dotnet run
```

Verificaciones:
1. API responde en `http://localhost:8080`.
2. Desde otra PC en LAN: `http://<IP_WINDOWS>:8080/Residential` (si firewall habilitado correctamente).

Firewall Windows (si aplica):
```powershell
netsh advfirewall firewall add rule name="ApiReloj 8080" dir=in action=allow protocol=TCP localport=8080
```

---

## 10) Matriz de endpoints operativos actuales
### 10.1 Maestros y heartbeat
1. `GET /Residential`
2. `GET /Residential/{id}`
3. `POST /Residential`
4. `POST /Residential/heartbeat`
5. `GET /Device`
6. `GET /Device/{id}`
7. `POST /Device`
8. `GET /Reloj`
9. `GET /Reloj/{id}`
10. `POST /Reloj`
11. `PUT /Reloj` (actualiza `Puerto` y `DeviceSn`)

### 10.2 Eventos y jornadas
1. `POST /AccessEvents/push/{relojId}`
2. `GET /AccessEvents`
3. `GET /Jornadas`

### 10.3 Backfill admin
1. `POST /admin/poll/run`
2. `GET /admin/poll/status`

### 10.4 Usuarios (estado actual)
1. `POST /UsersControllers`
2. `PUT /UsersControllers`
3. `DELETE /UsersControllers`

Nota: operativos con DI registrado en `Program.cs`.

---

## 11) Carga de datos maestros iniciales
Orden obligatorio:
1. `Residential`
2. `Device`
3. `Reloj`

## 11.1 Crear Residential
```http
POST /Residential
Content-Type: application/json

{
  "idResidential": 1,
  "ipActual": "0.0.0.0"
}
```

## 11.2 Crear Device
```http
POST /Device
Content-Type: application/json

{
  "_deviceId": 1001,
  "_secretKey": "MI_SECRETO_HEARTBEAT",
  "_lastSeen": null,
  "_residentialId": 1
}
```

## 11.3 Crear Reloj
```http
POST /Reloj
Content-Type: application/json

{
  "_idReloj": 1,
  "_puerto": 80,
  "_residentialId": 1
}
```

## 11.4 Carga de `DeviceSn` del reloj
Se realiza por API con `PUT /Reloj`.

Ejemplo:
```http
PUT /Reloj
Content-Type: application/json

{
  "_idReloj": 1,
  "_puerto": 80,
  "_deviceSn": "DS-K1T321MFWX20221217V030900ENAA7937545"
}
```

---

## 12) Pruebas funcionales end-to-end
## 12.1 Smoke test de infraestructura
1. Docker postgres activo.
2. Migraciones aplicadas.
3. API levantada sin error al iniciar.

## 12.2 Heartbeat válido (firma HMAC correcta)
Ejemplo PowerShell para generar firma y enviar heartbeat:
```powershell
$deviceId = 1001
$residentialId = 1
$secret = "MI_SECRETO_HEARTBEAT"
$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$message = "$timestamp|$deviceId|$residentialId"
$keyBytes = [Text.Encoding]::UTF8.GetBytes($secret)
$msgBytes = [Text.Encoding]::UTF8.GetBytes($message)
$hmac = New-Object System.Security.Cryptography.HMACSHA256($keyBytes)
$hash = $hmac.ComputeHash($msgBytes)
$signature = ($hash | ForEach-Object { $_.ToString("x2") }) -join ""

$body = @{
  DeviceId = $deviceId
  ResidentialId = $residentialId
  TimeStamp = $timestamp
  Signature = $signature
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri "http://localhost:8080/Residential/heartbeat" -ContentType "application/json" -Body $body
```

Validar:
1. `GET /Residential/1` actualiza `ipActual` con IP origen del heartbeat.
2. `GET /Device/1001` actualiza `_lastSeen`.

Comportamiento adicional relevante:
1. Si la firma es invalida, el endpoint mantiene respuesta `204` y no actualiza estado (no-op silencioso).
2. Si hay inconsistencia de datos (`Residential`/`Device` inexistente o no relacionados), hoy puede devolver error (no se procesa como no-op).

## 12.3 Push desde reloj (autorizado por IP)
Precondiciones:
1. `Reloj` existe.
2. `Reloj.DeviceSn` cargado.
3. `Residential.IpActual` coincide con IP origen del push.

Endpoint:
1. `POST /AccessEvents/push/{relojId}`

Resultado esperado:
1. `inserted` o `duplicate` o `ignored` según payload.

## 12.4 Poll manual admin
Ejecución:
```http
POST /admin/poll/run
Content-Type: application/json

{
  "residentialId": 1,
  "relojId": 1
}
```

Estado:
```http
GET /admin/poll/status
```

Validar:
1. Métricas (`Inserted`, `Duplicates`, `Ignored`).
2. Estado corrida (`ok`, `partial_error`, `error`).

Alcance de observabilidad actual:
1. Estas metricas corresponden a la corrida de poll (status/run), no a metricas globales de toda la API.
2. Para push hoy la observabilidad es por logs crudos (sin indexado/categorizacion propia de errores y sin contadores globales persistentes).

## 12.5 Consultas de lectura
### AccessEvents
```http
GET /AccessEvents?limit=100&offset=0
GET /AccessEvents?residentialId=1&fromUtc=2026-02-15T00:00:00Z&toUtc=2026-02-15T23:59:59Z&limit=100&offset=0
```

### Jornadas
```http
GET /Jornadas?limit=100&offset=0
GET /Jornadas?residentialId=1&statusCheck=INCOMPLETE&limit=100&offset=0
```

---

## 13) Troubleshooting y diagnóstico
## 13.1 Error de conexión a DB
Síntoma:
1. API no inicia o falla al consultar.

Checks:
1. `docker compose ps`
2. Puerto `5432` libre/visible.
3. `ConnectionStrings:Default` alineada con `.env`.

## 13.2 `dotnet ef` falla
Checks:
1. `dotnet --version` y `dotnet ef --version`.
2. Ejecutar comando desde `Migracion_a_C/WebApplication1`.
3. Confirmar `--project` y `--startup-project` correctos.

## 13.3 Heartbeat rechazado o sin efecto
Checks:
1. `DeviceId` existente.
2. `ResidentialId` correcto.
3. Firma HMAC calculada como `"{timestamp}|{deviceId}|{residentialId}"`.
4. `secretKey` exacta del `Device`.

Resultados esperables segun causa:
1. Firma invalida: `204` no-op (sin cambios en `ipActual`/`LastSeen`).
2. Datos inexistentes o relacion invalida (`Device` no pertenece a `Residential`): puede responder con error segun manejo global de excepciones.

## 13.4 Push rechazado (401/422/404)
Checks:
1. `Reloj` existe.
2. `Residential` existe.
3. IP origen del push coincide con `Residential.IpActual`.
4. `Reloj.DeviceSn` no vacío.

## 13.5 Poll con errores
Checks:
1. `Residential.IpActual` reachable desde Windows.
2. `Reloj.Puerto` correcto.
3. `ISAPI_USER` / `ISAPI_PASSWORD` correctos si el reloj exige Digest.
4. Endpoint ISAPI habilitado en reloj.

## 13.6 Histórico de bloqueantes (resueltos)
1. Persistencia de update en `PUT /Reloj`: resuelto.
2. Registro DI de `UsersControllers`: resuelto.
3. Riesgo de binding de `ModifiUserDtoFromBack` por ctor parametrizado: resuelto con ctor vacío.

Si alguno de estos comportamientos falla en pruebas, tratarlo como regresión.

---

## 14) Checklist de Go-Live local
Marca cada punto como `OK` antes de declarar entorno listo:

1. `[ ]` Docker Desktop operativo.
2. `[ ]` Postgres levantado (`apireloj-postgres` running).
3. `[ ]` Migraciones EF aplicadas sin error.
4. `[ ]` Tablas clave creadas (`Residentials`, `Devices`, `Relojes`, `AccessEvents`, `Jornadas`).
5. `[ ]` API levantada en `http://0.0.0.0:8080`.
6. `[ ]` Firewall Windows permite inbound TCP 8080.
7. `[ ]` `Residential`, `Device` y `Reloj` creados por API.
8. `[ ]` Heartbeat real actualiza IP y `LastSeen`.
9. `[ ]` Push llega y persiste eventos (`inserted/duplicate`).
10. `[ ]` Poll manual ejecuta y devuelve métricas coherentes.
11. `[ ]` `GET /AccessEvents` devuelve resultados esperados.
12. `[ ]` `GET /Jornadas` devuelve resultados esperados.
13. `[ ]` `PUT /Reloj` persiste cambios de `_puerto` y `_deviceSn`.
14. `[ ]` Endpoints `UsersControllers` responden sin error de DI.

Regla final:
1. Si 13 o 14 están en `NO`, tratar como regresión y no cerrar la puesta en marcha.

---

## 15) Apéndice de referencias
1. `Migracion_a_C/DocReloj/infra_hibrida.md`
2. `Migracion_a_C/DocReloj/api_poll_backfill_v1.md`
3. `Migracion_a_C/DocReloj/api_access_events_v1.md`
4. `Migracion_a_C/DocReloj/api_jornadas_v1.md`
5. `Migracion_a_C/DocReloj/plan_puesta_marcha_lightsail.md`
