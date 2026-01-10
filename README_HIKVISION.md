# Documentación del Proyecto: Receptor de Asistencias HIKVISION

Este documento detalla todos los pasos necesarios para poner en funcionamiento un sistema que recibe registros de asistencia desde un reloj biométrico HIKVISION modelo DS-K1T321MFWX y los almacena en una base de datos PostgreSQL alojada en un servidor Linux.

---

## 🧰 Requisitos del servidor (instalaciones básicas)

### 1. Sistema Operativo
- Ubuntu Server 22.04 (o similar)

### 2. Actualización inicial del sistema (desde cualquier ruta)
```bash
sudo apt update && sudo apt upgrade -y
```

### 3. Node.js y npm (desde cualquier ruta)
```bash
curl -fsSL https://deb.nodesource.com/setup_18.x | sudo -E bash -
sudo apt install -y nodejs
```

### 4. PostgreSQL (desde cualquier ruta)
```bash
sudo apt install -y postgresql postgresql-contrib
```

### 5. pm2 (gestor de procesos Node.js) (desde cualquier ruta)
```bash
sudo npm install -g pm2
```

### 6. Dependencias del proyecto (desde `hikvision-server/`)
```bash
npm install express dotenv pg
npm install --save-dev typescript ts-node @types/node @types/express
npx tsc --init
```

---

## ⚙️ Configuración del sistema

### 1. Swap para ampliar memoria virtual (desde cualquier ruta)
```bash
sudo fallocate -l 1G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
```
Para hacerlo permanente:
```bash
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

### 2. Apertura de puertos (ej. en Amazon Lightsail)
- Abrir puerto `3000` para tráfico TCP.

---

## 🛠 Configuración de PostgreSQL

### 1. Crear usuario y base de datos (desde cualquier ruta)
```bash
sudo -u postgres psql
CREATE USER admin WITH PASSWORD 'NyM=15091503';
CREATE DATABASE resi OWNER admin;
\q
```

### 2. Editar archivo `pg_hba.conf` (desde cualquier ruta)
```bash
sudo nano /etc/postgresql/14/main/pg_hba.conf
```
Agregar al final:
```
host    all             all             0.0.0.0/0               md5
```

### 3. Editar archivo `postgresql.conf` (desde cualquier ruta)
```bash
sudo nano /etc/postgresql/14/main/postgresql.conf
```
Modificar:
```
listen_addresses = '*'
```

### 4. Reiniciar PostgreSQL
```bash
sudo systemctl restart postgresql
```

---

## 📦 Estructura del Proyecto

```
hikvision-server/
├── src/
│   ├── index.ts
│   ├── routes/
│   │   └── hikvision.ts
│   └── db/
│       └── client.ts
├── dist/ (generado tras compilar)
├── .env
├── tsconfig.json
├── package.json
```

---

## 🔌 Configuración del Reloj HIKVISION (modo HTTP Listening)

### 1. Acceder vía navegador: `http://[IP_RELOJ]`
- Usuario: `admin`
- Contraseña: configurada previamente

### 2. Enviar configuración vía ISAPI (Digest Auth)
Usar Postman o curl:
```bash
curl --digest -u admin:[CONTRASEÑA] -X PUT http://[IP_RELOJ]/ISAPI/Event/notification/httpHosts/1 -d @config.xml --header "Content-Type: application/xml"
```

### 3. Contenido del archivo `config.xml`:
```xml
<HttpHostNotification version="2.0" xmlns="http://www.isapi.org/ver20/XMLSchema">
  <id>1</id>
  <url>http://[IP_SERVIDOR]:3000/eventos/hikvision</url>
  <protocolType>HTTP</protocolType>
  <addressingFormatType>ipaddress</addressingFormatType>
  <ipAddress>[IP_SERVIDOR]</ipAddress>
  <portNo>3000</portNo>
  <parameterFormatType>JSON</parameterFormatType>
  <httpAuthenticationMethod>digest</httpAuthenticationMethod>
  <uploadPicture>false</uploadPicture>
  <SubscribeEvent>
    <heartbeat>30</heartbeat>
    <eventMode>all</eventMode>
    <EventList>
      <Event>
        <type>AccessControllerEvent</type>
        <minorEvent>75,76</minorEvent>
        <pictureURLType>binary</pictureURLType>
      </Event>
    </EventList>
  </SubscribeEvent>
</HttpHostNotification>
```

---

## 🏃 Compilar y ejecutar el servidor

### 1. Compilar el código (desde `hikvision-server/`)
```bash
npx tsc
```

### 2. Ejecutar servidor
```bash
node dist/index.js
```

### (alternativa en desarrollo)
```bash
npx ts-node src/index.ts
```

---

## 🌀 Ejecución permanente con PM2

### 1. Iniciar con PM2 (desde `hikvision-server/`)
```bash
pm2 start dist/index.js --name hikvisio
```

### 2. Guardar y configurar para reinicio automático
```bash
pm2 save
pm2 startup
# copiar y ejecutar el comando que aparece luego
```

---

## po✅ Verificación del sistema

Desde Postman o el propio reloj, enviar:
```
POST http://[IP_SERVIDOR]:3000/eventos/hikvision
```

Body de ejemplo:
```json
{
  "eventTime": "2025-07-28T02:45:12",
  "employeeNoString": "1234",
  "attendanceStatus": "CheckIn"
}
```

---

## 🧪 Consultar registros en PostgreSQL

```bash
sudo -u postgres psql
\c resi
SELECT * FROM asistencia;
```

---

## 🔍 Logs del servidor

```bash
pm2 logs hikvisio
```

---

## 🧹 Comandos útiles PM2

```bash
pm2 list
pm2 restart hikvisio
pm2 delete hikvisio
```

---

## 🕵️ Verificar configuración del reloj

```bash
curl --digest -u admin:[PASS] http://[IP_RELOJ]/ISAPI/Event/notification/httpHosts
```

---

## Confirmaciones

- El reloj debe responder con:
```xml
<statusCode>1</statusCode>
<statusString>OK</statusString>
```
- Se puede verificar que se envió correctamente y que los eventos llegaron revisando:
    - Logs del servidor (`pm2 logs`)
    - Consultas en PostgreSQL (`SELECT * FROM asistencia;`)

---

**Fin del documento**
