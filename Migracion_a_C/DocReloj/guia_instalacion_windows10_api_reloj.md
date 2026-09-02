# Instalación y puesta en marcha de ApiReloj en Windows

**Revisada contra código y Compose:** 15 de julio de 2026.

## 1. Alcance

Esta guía cubre dos formas de ejecutar ApiReloj:

1. API desde .NET en Windows, con PostgreSQL accesible externamente.
2. API mediante el Compose incluido, con PostgreSQL accesible desde el contenedor.

El `docker-compose.yml` actual contiene solamente el servicio `api`; no crea PostgreSQL.

## 2. Requisitos

- .NET 10 SDK para ejecución local.
- PostgreSQL compatible con Npgsql/EF Core 10.
- Docker Desktop sólo si se usará Compose.
- Credenciales ISAPI del reloj si exige Digest.
- API key del backend e IP fija desde la que se harán llamadas administrativas.

Proyectos:

- solución: `Migracion_a_C/WebApplication1/WebApplication1.sln`;
- startup: `WebApplication1/WebApplication1.csproj`;
- migraciones: `DataAcces/DataAcces.csproj`;
- pruebas: `Service.Tests/Service.Tests.csproj`.

## 3. PostgreSQL

Crear una base y usuario, por ejemplo:

```text
database: apireloj
username: apireloj
password: <secreto>
port: 5432
```

La connection string se configura como:

```text
ConnectionStrings__Default=Host=localhost;Port=5432;Database=apireloj;Username=apireloj;Password=<secreto>
```

Al arrancar, la API ejecuta `Database.Migrate()`. No hace falta ejecutar `dotnet ef database update` en el flujo normal. Si la base no está disponible o una migración falla, la API no inicia.

Para diagnóstico manual:

```powershell
cd C:\ruta\ApiReloj\Migracion_a_C\WebApplication1
dotnet tool restore
dotnet tool run dotnet-ef database update --project .\DataAcces\DataAcces.csproj --startup-project .\WebApplication1\WebApplication1.csproj
```

Tablas funcionales principales:

- `Residentials`
- `Devices`
- `Relojes`
- `AccessEvents`
- `Jornadas`
- `JornadaProjectionStates`
- `BackfillPollRuns`
- `__EFMigrationsHistory`

## 4. Configuración de seguridad

Variables obligatorias fuera de Development:

```powershell
$env:Security__Backend__ApiKey = "un-secreto-largo"
$env:Security__Backend__AllowedIp = "203.0.113.20"
```

`AllowedIp` debe ser la IP que ApiReloj observa como origen del backend. La API no inicia si la clave está vacía o la IP es inválida.

Valores heartbeat por defecto:

```text
AllowedClockSkewSeconds=300
MaximumBodySizeBytes=8192
PermitLimitPerIp=600
RateWindowSeconds=60
GlobalConcurrencyLimit=200
```

Para producción usar HTTPS. Si se agrega un reverse proxy, configurar forwarded headers y proxies conocidos antes de utilizar la IP reenviada.

## 5. Credenciales ISAPI

Opcionales si el reloj no exige Digest:

```powershell
$env:ISAPI_USER = "admin"
$env:ISAPI_PASSWORD = "<password-reloj>"
```

Las usan polling y los endpoints de usuarios.

## 6. Ejecutar localmente

Development incluye una credencial local:

```text
X-Api-Key: development-backend-key
AllowedIp: 127.0.0.1
```

Arranque:

```powershell
cd C:\ruta\ApiReloj\Migracion_a_C\WebApplication1\WebApplication1
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:8080"
dotnet run
```

OpenAPI se publica sólo en Development y también requiere autenticación backend.

Smoke test:

```powershell
curl.exe -H "X-Api-Key: development-backend-key" http://127.0.0.1:8080/Residential
```

Una llamada sin header debe devolver `401`.

## 7. Ejecutar con Compose

Copiar la plantilla:

```powershell
cd C:\ruta\ApiReloj\Migracion_a_C
Copy-Item .env.sample .env
```

Variables:

```env
API_PORT=8080
DB_NAME=apireloj
DB_USER=apireloj
DB_PASSWORD=change-me
BACKEND_API_KEY=replace-with-a-long-random-secret
BACKEND_ALLOWED_IP=203.0.113.20
```

Compose conecta a PostgreSQL mediante `host.docker.internal:5432`. PostgreSQL debe aceptar conexiones desde Docker y tener firewall/configuración apropiados.

```powershell
docker compose up -d --build api
docker compose ps
docker compose logs api
```

Criterios de arranque:

- las opciones de seguridad validan;
- PostgreSQL responde;
- migraciones se aplican;
- el contenedor queda healthy a nivel de proceso, sin excepciones de startup.

## 8. Aprovisionamiento inicial

Todas estas llamadas deben llevar `X-Api-Key` y salir desde la IP autorizada.

### Residential

```http
POST /Residential
Content-Type: application/json
X-Api-Key: <secreto>

{
  "idResidential": "RES-001",
  "ipActual": "0.0.0.0"
}
```

### Device

```http
POST /Device
Content-Type: application/json
X-Api-Key: <secreto>

{
  "_deviceId": "DEVICE-001",
  "_secretKey": "SECRETO_HEARTBEAT",
  "_lastSeen": null,
  "_residentialId": "RES-001"
}
```

La respuesta no devuelve el secreto.

### Reloj

```http
POST /Reloj
Content-Type: application/json
X-Api-Key: <secreto>

{
  "_idReloj": "CLOCK-001",
  "_puerto": 80,
  "_residentialId": "RES-001"
}
```

Configurar DeviceSn:

```http
PUT /Reloj
Content-Type: application/json
X-Api-Key: <secreto>

{
  "_idReloj": "CLOCK-001",
  "_puerto": 80,
  "_deviceSn": "CLOCK-SN-01"
}
```

## 9. Probar heartbeat

El body permanece compatible con DeviceHeartbeatService:

```powershell
$deviceId = "DEVICE-001"
$residentialId = "RES-001"
$secret = "SECRETO_HEARTBEAT"
$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$canonical = "$timestamp|$deviceId|$residentialId"
$hmac = [System.Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($secret))
$signature = ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)) | ForEach-Object { $_.ToString("x2") }) -join ""
$body = @{ deviceId=$deviceId; residentialId=$residentialId; timeStamp=$timestamp; signature=$signature } | ConvertTo-Json
curl.exe -i -X POST -H "Content-Type: application/json" --data $body http://127.0.0.1:8080/Residential/heartbeat
```

Esperado:

- firma válida nueva: `204` y actualiza estado;
- mismo heartbeat repetido: `204` sin segunda mutación;
- firma o timestamp inválidos: `401`;
- exceso de tasa: `429`.

## 10. Probar backend

```powershell
$headers = @("X-Api-Key: development-backend-key")
curl.exe -H $headers[0] "http://127.0.0.1:8080/AccessEvents?limit=10&offset=0"
curl.exe -H $headers[0] "http://127.0.0.1:8080/Jornadas?includeDeleted=true&limit=10&offset=0"
curl.exe -H $headers[0] "http://127.0.0.1:8080/admin/jornadas/projection-states"
curl.exe -H $headers[0] "http://127.0.0.1:8080/admin/poll/status"
```

## 11. Push real

Precondiciones:

1. Reloj existente con DeviceSn.
2. `Residential.IpActual` actualizado por heartbeat.
3. URL del reloj: `http(s)://<api>/AccessEvents/push/CLOCK-001`.
4. IP origen del reloj igual a la IP registrada.

Resultados funcionales: `inserted`, `duplicate` o `ignored`. Una IP incorrecta produce `401`.

## 12. Poll manual

```powershell
$body = '{"residentialId":"RES-001","relojId":"CLOCK-001"}'
curl.exe -X POST -H "X-Api-Key: development-backend-key" -H "Content-Type: application/json" --data $body http://127.0.0.1:8080/admin/poll/run
```

Revisar luego historial, métricas y estados de jornadas.

## 13. Pruebas del repositorio

Las tres integraciones PostgreSQL eliminan datos de `AccessEvents`, `Jornadas` y
`JornadaProjectionStates`. Nunca deben apuntar a `habitar-postgres`, una base de
desarrollo compartida ni producción. El código exige dos protecciones:

1. el nombre de la base debe terminar en `_tests`;
2. `APIRELOJ_ALLOW_DESTRUCTIVE_TESTS` debe valer `true`.

Ejemplo reproducible desde la raíz del repositorio:

```powershell
cd C:\ruta\ApiReloj

docker run --name apireloj-postgres-tests --rm -d -p 55432:5432 `
  -e POSTGRES_DB=apireloj_tests `
  -e POSTGRES_USER=apireloj_tests `
  -e POSTGRES_PASSWORD=test-only-password `
  postgres:16-alpine

$env:APIRELOJ_TEST_CONNECTION = "Host=127.0.0.1;Port=55432;Database=apireloj_tests;Username=apireloj_tests;Password=test-only-password"
$env:APIRELOJ_ALLOW_DESTRUCTIVE_TESTS = "true"

dotnet tool restore
dotnet restore Migracion_a_C/WebApplication1/WebApplication1.sln `
  -p:NuGetAudit=true -p:NuGetAuditMode=all `
  -p:WarningsAsErrors=NU1901%3BNU1902%3BNU1903%3BNU1904
dotnet build Migracion_a_C/WebApplication1/WebApplication1.sln -c Release --no-restore

dotnet tool run dotnet-ef database update `
  --project Migracion_a_C/WebApplication1/DataAcces/DataAcces.csproj `
  --startup-project Migracion_a_C/WebApplication1/WebApplication1/WebApplication1.csproj `
  --connection "$env:APIRELOJ_TEST_CONNECTION" `
  --configuration Release --no-build

dotnet test --project Migracion_a_C/WebApplication1/Service.Tests/Service.Tests.csproj --configuration Release --no-build --no-restore
```

El último comando es exactamente el utilizado por CI. Debe informar `22` pruebas,
`0` fallidas y `0` omitidas. `global.json` selecciona Microsoft Testing Platform,
que es el runner de xUnit v3 usado por este repositorio.

Validaciones adicionales equivalentes a CI:

```powershell
dotnet list Migracion_a_C/WebApplication1/WebApplication1.sln package --vulnerable --include-transitive --no-restore
docker compose --env-file Migracion_a_C/.env.sample -f Migracion_a_C/docker-compose.yml config --quiet
docker build --tag apireloj-local-check .
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1 `
  -ConnectionString $env:APIRELOJ_TEST_CONNECTION -BackendApiKey "local-smoke-key"
docker stop apireloj-postgres-tests
```

El smoke automatizado verifica seguridad backend, heartbeat con el body vigente,
reutilización idempotente, firma inválida, push por IP residencial, deduplicación,
cola persistente y una jornada entre dos relojes del mismo residencial.

GitHub ejecuta la misma secuencia en `.github/workflows/ci.yml` para cada pull
request y para pushes a `main`, `develop` y ramas `codex/**`.

Las interacciones de red con un reloj Hikvision físico no se pueden simular con
fidelidad en CI. Antes de producción se debe completar
`DocReloj/aceptacion_manual_isapi.md` y conservar sus evidencias.

## 14. Troubleshooting

### La API no inicia

- validar connection string;
- verificar PostgreSQL y migraciones;
- revisar API key e IP permitida;
- confirmar valores numéricos de heartbeat.

### Backend recibe `401`

- falta `X-Api-Key`, hay múltiples valores o no coincide.

### Backend recibe `403`

- la API key es válida, pero `RemoteIpAddress` no coincide con `AllowedIp`.

### Push recibe `401`

- reloj/residencial inexistente;
- falta DeviceSn;
- IP actual vacía o inválida;
- IP origen distinta.

### Jornadas demoran o fallan

- consultar `/admin/jornadas/projection-states`;
- revisar `Attempts`, `LastError` y `NextAttemptAtUtc`;
- encolar `/admin/jornadas/rebuild` si se requiere intervención.

## 15. Go-live

- [ ] PostgreSQL respaldado y accesible.
- [ ] Migraciones aplicadas por un arranque exitoso.
- [ ] API key productiva rotada y no versionada.
- [ ] IP fija del backend correcta.
- [ ] HTTPS activo.
- [ ] Residential, Device y Reloj aprovisionados.
- [ ] Secreto heartbeat custodiado.
- [ ] Heartbeat actualiza IP y LastSeen.
- [ ] Push real autorizado e idempotente.
- [ ] Poll ejecuta contra ISAPI.
- [ ] Proyecciones llegan a READY.
- [ ] Backend procesa revisiones y tombstones.
- [ ] CI ejecuta las 22 pruebas sin fallos ni omisiones.
- [ ] Aceptación manual ISAPI completada con un reloj real.
