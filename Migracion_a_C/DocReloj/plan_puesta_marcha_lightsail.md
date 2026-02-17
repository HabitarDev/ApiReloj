# Plan de Puesta en Marcha End-to-End (Lightsail + Heartbeats + Push Hikvision)

## 1. Resumen
Objetivo: dejar funcionando la prueba integrada actual en una instancia Lightsail Ubuntu 22.04:
1. API corriendo.
2. Postgres operativo.
3. PC Windows enviando heartbeats validos.
4. Reloj Hikvision enviando eventos push.
5. Validacion end-to-end.

Decisiones de este plan:
1. API en host (binario self-contained publicado desde tu PC) + Postgres en Docker.
2. Exposicion HTTP directa en `:8080`.
3. Reloj y PC heartbeat salen por la misma IP publica.
4. Configuracion del reloj por UI, verificando por ISAPI.
5. Se asume que existira endpoint update de reloj para setear `DeviceSn`.
6. Se asume que existira endpoint GET eventos para validacion funcional.

## 2. Endpoints necesarios
1. Ya existentes:
   - `POST /Residential`
   - `POST /Device`
   - `POST /Reloj`
   - `POST /Residential/heartbeat`
   - `POST /AccessEvents/push/{relojId}`
2. Deben estar para la prueba:
   - `PUT /Reloj/{id}` para setear `_deviceSn`.
   - `GET` de eventos almacenados (segun diseno que implementes).

## 3. Preparar Lightsail y red
1. Abrir puertos inbound: `22` y `8080`.
2. No abrir `5432` publico.
3. Si ya abriste puertos por UI de Lightsail, este paso de firewall local puede dejarse como opcional.
4. Si usas UFW:
```bash
sudo ufw allow 22/tcp
sudo ufw allow 8080/tcp
sudo ufw enable
sudo ufw status
```
Explicacion de comandos:
1. `sudo ufw allow 22/tcp`: habilita SSH al servidor.
2. `sudo ufw allow 8080/tcp`: habilita acceso HTTP a la API.
3. `sudo ufw enable`: activa UFW.
4. `sudo ufw status`: muestra reglas activas.

Nota Lightsail UI:
1. Security groups (Networking) de Lightsail y UFW son capas distintas.
2. Si en UI ya abriste `22/8080`, igual puedes usar UFW como capa adicional.
3. Si no quieres doble capa de firewall en esta etapa, puedes omitir UFW.

## 4. Instalar dependencias en Ubuntu 22.04
1. Base:
```bash
sudo apt update
sudo apt install -y git curl ca-certificates gnupg lsb-release jq
```
Explicacion de comandos:
1. `sudo apt update`: actualiza el indice de paquetes disponibles.
2. `sudo apt install -y ...`: instala dependencias base:
   - `git`: clonar y actualizar el repo.
   - `curl`: descargar archivos y probar endpoints HTTP.
   - `ca-certificates`: certificados raiz para conexiones HTTPS confiables.
   - `gnupg`: gestionar llaves GPG de repositorios externos.
   - `lsb-release`: detectar version/codename de Ubuntu (ej. jammy).
   - `jq`: parsear/inspeccionar JSON desde consola.

Nota Lightsail UI:
1. Esta instalacion de paquetes no se hace por UI de Lightsail; se hace por SSH en la instancia.

2. Docker + Compose:
```bash
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker $USER
```
Explicacion linea por linea:
1. `sudo install -m 0755 -d /etc/apt/keyrings`:
   - crea la carpeta donde se guardan llaves de repositorios APT.
2. `curl -fsSL ... | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg`:
   - descarga la llave oficial de Docker y la guarda en formato que APT puede usar para verificar firmas.
3. `echo "deb ... docker.list"`:
   - agrega el repositorio oficial de Docker para tu arquitectura y version de Ubuntu.
4. `sudo apt update`:
   - recarga indice de paquetes incluyendo el repo de Docker recien agregado.
5. `sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin`:
   - instala motor Docker, cliente CLI, runtime `containerd`, buildx y plugin de `docker compose`.
6. `sudo usermod -aG docker $USER`:
   - agrega tu usuario al grupo `docker` para ejecutar Docker sin `sudo`.
   - requiere cerrar y reabrir sesion para tomar efecto.

Nota Lightsail UI:
1. Docker tampoco se instala por UI de Lightsail; se instala por SSH.
2. Lo que si haces por UI es abrir puertos y administrar DNS/IP estaticas/snapshots.
3. Reingresar sesion SSH.
4. .NET SDK en Lightsail NO es obligatorio en este plan.
5. Motivo: el `publish` se hace en tu PC y subes un binario self-contained a la instancia.
6. Solo instala .NET en Lightsail si mas adelante quieres compilar/publicar directamente desde el servidor.

## 5. Clonar repo y levantar Postgres en Docker
1. Clonar:
```bash
git clone <TU_REPO_URL> /opt/apireloj
cd /opt/apireloj/Migracion_a_C
```
2. Crear `.env` de compose:
```bash
cat > .env << 'EOF'
POSTGRES_PORT=5432
POSTGRES_USER=apireloj
POSTGRES_PASSWORD=apireloj
POSTGRES_DB=apireloj
EOF
```
3. Levantar DB:
```bash
docker compose up -d postgres
docker compose ps
docker logs -f apireloj-postgres
```

## 6. Publicar en tu PC y desplegar en Lightsail
1. En Lightsail, validar arquitectura del servidor (para elegir RID correcto):
```bash
uname -m
```
2. Equivalencias:
   - `x86_64` -> usar RID `linux-x64`.
   - `aarch64` -> usar RID `linux-arm64`.
3. En tu PC (Rider o consola), hacer `publish` self-contained.
4. Consola (ejemplo para `linux-x64`):
```bash
cd ~/RiderProjects/ApiReloj/Migracion_a_C/WebApplication1
dotnet publish WebApplication1/WebApplication1.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish-lightsail
```
5. Rider UI (alternativa):
   - Click derecho en proyecto `WebApplication1` -> `Publish...`
   - Configuration: `Release`
   - Deployment mode: `Self-contained`
   - Runtime: `linux-x64` o `linux-arm64` segun `uname -m`
   - Output folder: `publish-lightsail`
6. En Lightsail, preparar carpeta destino:
```bash
sudo mkdir -p /opt/apireloj/publish
sudo chown -R ubuntu:ubuntu /opt/apireloj
```
7. Desde tu PC, copiar artefactos:
```bash
scp -i C:/Users/<TU_USUARIO>/RiderProjects/LightsailDefaultKey-us-east-2.pem -r ./publish-lightsail/* ubuntu@<LIGHTSAIL_IP>:/opt/apireloj/publish/
```
8. En Lightsail, dar permisos de ejecucion al binario:
```bash
chmod +x /opt/apireloj/publish/WebApplication1
```
9. Crear servicio `systemd`:
```bash
sudo tee /etc/systemd/system/apireloj.service > /dev/null << 'EOF'
[Unit]
Description=ApiReloj
After=network.target docker.service
Wants=docker.service

[Service]
WorkingDirectory=/opt/apireloj/publish
ExecStart=/opt/apireloj/publish/WebApplication1
Environment=ASPNETCORE_URLS=http://0.0.0.0:8080
Environment=ConnectionStrings__Default=Host=127.0.0.1;Port=5432;Database=apireloj;Username=apireloj;Password=apireloj
Restart=always
RestartSec=5
User=ubuntu

[Install]
WantedBy=multi-user.target
EOF
```
10. Activar:
```bash
sudo systemctl daemon-reload
sudo systemctl enable apireloj
sudo systemctl start apireloj
sudo systemctl status apireloj
journalctl -u apireloj -f
```

## 7. Alta inicial en API (Residential, Device, Reloj)
1. Crear Residential:
```http
POST http://<LIGHTSAIL_IP>:8080/Residential
Content-Type: application/json

{
  "idResidential": 1,
  "ipActual": "<IP_PUBLICA_DE_SALIDA_DEL_SITIO>"
}
```
2. Crear Device:
```http
POST http://<LIGHTSAIL_IP>:8080/Device
Content-Type: application/json

{
  "_deviceId": 1001,
  "_secretKey": "MI_SECRETO_HEARTBEAT",
  "_lastSeen": null,
  "_residentialId": 1
}
```
3. Crear Reloj:
```http
POST http://<LIGHTSAIL_IP>:8080/Reloj
Content-Type: application/json

{
  "_idReloj": 1,
  "_puerto": 80,
  "_residentialId": 1
}
```

## 8. Obtener DeviceSn real del reloj (Postman)
1. Desde red con acceso al reloj:
```http
GET http://<IP_PRIVADA_RELOJ>/ISAPI/System/deviceInfo
Authorization: Digest (admin/password)
```
2. Extraer el valor que usaras como `DeviceSn` (ID/serial estable reportado por ese endpoint).

## 9. Setear DeviceSn en API por update de reloj
1. Llamar endpoint update:
```http
PUT http://<LIGHTSAIL_IP>:8080/Reloj/1
Content-Type: application/json

{
  "_idReloj": 1,
  "_puerto": 80,
  "_residentialId": 1,
  "_deviceSn": "VALOR_OBTENIDO_EN_DEVICEINFO"
}
```
2. Validar con `GET /Reloj/1`.

## 10. Configurar servicio de heartbeat en Windows
1. URL destino: `http://<LIGHTSAIL_IP>:8080/Residential/heartbeat`.
2. Payload:
```json
{
  "DeviceId": 1001,
  "ResidentialId": 1,
  "TimeStamp": 1730000000,
  "Signature": "HEX_HMAC_SHA256"
}
```
3. Firma exacta:
   - String base: `"<TimeStamp>|<DeviceId>|<ResidentialId>"`
   - HMAC SHA-256 con `secretKey` del Device
   - Salida en HEX
4. Frecuencia sugerida: cada 10-30 segundos.
5. Verificar que `ipActual` del residencial quede correcto.

## 11. Configurar push del reloj Hikvision (UI + chequeo ISAPI)
1. Verificar capacidades:
```http
GET /ISAPI/Event/notification/httpHosts/capabilities
```
2. Configurar host de notificacion en el reloj:
   - Host/IP: `<LIGHTSAIL_IP>`
   - Puerto: `8080`
   - URI: `/AccessEvents/push/1`
   - Protocol: `HTTP`
   - Event type: `AccessControllerEvent`
   - Parameter format: `JSON`
3. Habilitar el host.
4. Test opcional:
```http
POST /ISAPI/Event/notification/httpHosts/<hostID>/test
```

## 12. Validacion end-to-end
1. Heartbeat valido actualiza residencial/IP.
2. Push valido retorna `200` con `inserted` o `duplicate`.
3. Push de tipo no soportado retorna `200` con `ignored`.
4. Duplicado no se vuelve a insertar.
5. Consultar endpoint GET de eventos que implementes para validar persistencia.

Si todavia no existe GET de eventos, validar por SQL:
```sql
SELECT "DeviceSn","SerialNumber","EventTimeUtc","EmployeeNumber","Major","Minor","AttendanceStatus"
FROM "AccessEvents"
ORDER BY "EventTimeUtc" DESC
LIMIT 50;
```

## 13. Criterio de exito
1. API estable en `systemd`.
2. Postgres estable en Docker.
3. Heartbeats continuos y firma correcta.
4. `ipActual` correcto para allowlist.
5. `DeviceSn` cargado en reloj registrado.
6. Push insertando eventos.
7. Idempotencia funcionando (`duplicate` en reenvios).
8. Endpoint GET de eventos devolviendo resultados esperados.
