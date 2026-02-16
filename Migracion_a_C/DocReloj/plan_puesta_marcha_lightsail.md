# Plan de Puesta en Marcha End-to-End (Lightsail + Heartbeats + Push Hikvision)

## 1. Resumen
Objetivo: dejar funcionando la prueba integrada actual en una instancia Lightsail Ubuntu 22.04:
1. API corriendo.
2. Postgres operativo.
3. PC Windows enviando heartbeats validos.
4. Reloj Hikvision enviando eventos push.
5. Validacion end-to-end.

Decisiones de este plan:
1. API en host (dotnet) + Postgres en Docker.
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
3. Si usas UFW:
```bash
sudo ufw allow 22/tcp
sudo ufw allow 8080/tcp
sudo ufw enable
sudo ufw status
```

## 4. Instalar dependencias en Ubuntu 22.04
1. Base:
```bash
sudo apt update
sudo apt install -y git curl ca-certificates gnupg lsb-release jq
```
2. Docker + Compose:
```bash
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker $USER
```
3. Reingresar sesion SSH.
4. Dotnet SDK:
```bash
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install -y dotnet-sdk-10.0
dotnet --info
```

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

## 6. Publicar API y levantarla como servicio
1. Build/publish:
```bash
cd /opt/apireloj/Migracion_a_C/WebApplication1
dotnet restore WebApplication1.sln
dotnet publish WebApplication1/WebApplication1.csproj -c Release -o /opt/apireloj/publish
```
2. Crear servicio `systemd`:
```bash
sudo tee /etc/systemd/system/apireloj.service > /dev/null << 'EOF'
[Unit]
Description=ApiReloj
After=network.target docker.service
Wants=docker.service

[Service]
WorkingDirectory=/opt/apireloj/publish
ExecStart=/usr/bin/dotnet /opt/apireloj/publish/WebApplication1.dll
Environment=ASPNETCORE_URLS=http://0.0.0.0:8080
Environment=ConnectionStrings__Default=Host=127.0.0.1;Port=5432;Database=apireloj;Username=apireloj;Password=apireloj
Restart=always
RestartSec=5
User=ubuntu

[Install]
WantedBy=multi-user.target
EOF
```
3. Activar:
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
