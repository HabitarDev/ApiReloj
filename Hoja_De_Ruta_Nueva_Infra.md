# Hoja de ruta: Polling de eventos y carga en la Base de Datos

Este documento describe los pasos para configurar el **repositorio** que extrae eventos de un reloj HIKVISION (192.168.1.7) y los carga en PostgreSQL. Incluye la verificación inicial con Postman, la estructura de carpetas y la implementación detallada.

---

## Paso 0: Listado de HTTP Listeners con Postman (Máquina Local)

1. **Abrir Postman y crear nueva petición**

    * **Name:** "Listar HTTP Listeners"

2. **Configurar método y URL**

    * **Method:** `GET`
    * **URL:** `http://192.168.1.7/ISAPI/Event/notification/httpHosts`

3. **Authorization → Digest Auth**

    * **Username:** `admin`
    * **Password:** `NyM=15091503`

4. **Headers**

   ```text
   Key:   Accept  
   Value: application/xml
   ```

5. **Send**

    * Revisar la respuesta XML con `<HttpHostNotificationList>` y cada `<id>` de listener activo.

6. **Guardar en colección** (opcional) para futuras ejecuciones.

---

## Esqueleto de carpetas y archivos

```plaintext
hikvision-poller/                # Raíz del proyecto
├── src/                         # Código fuente
│   ├── services/                # Lógica de extracción de eventos
│   │   └── eventPoller.ts       # Polling de AcsEvent y persistencia en BD
│   ├── db/                      # Conexión y consultas a PostgreSQL
│   │   └── client.ts            # Pool de PostgreSQL y helpers de inserción
│   ├── utils/                   # Utilidades genéricas
│   │   ├── httpClient.ts        # Cliente HTTP con Digest Auth y reintentos
│   │   └── logger.ts            # Logger centralizado
│   └── index.ts                 # Punto de entrada: arranca polling y health check
├── dist/                        # Código compilado (gitignore)
├── .env.example                 # Plantilla de variables de entorno
├── ecosystem.config.js          # Configuración PM2 para eventPoller
├── package.json                 # Dependencias y scripts npm
└── README.md                    # Este archivo
```

---

# Fase 1: Reversión de “push” (Servidor Remoto)

Estas operaciones se ejecutan desde el servidor remoto con `curl` o Postman, apuntando a `http://192.168.1.7`:

1. **Listar listeners activos**

   ```bash
   curl -u admin:'NyM=15091503' \
        -H 'Accept: application/xml' \
        http://192.168.1.7/ISAPI/Event/notification/httpHosts
   ```

2. **Eliminar todos los listeners**

   ```bash
   curl -X DELETE -u admin:'NyM=15091503' \
        -H 'Accept: application/xml' \
        http://192.168.1.7/ISAPI/Event/notification/httpHosts
   ```

3. **Verificar slots vacíos**

   ```bash
   curl -u admin:'NyM=15091503' \
        -H 'Accept: application/xml' \
        http://192.168.1.7/ISAPI/Event/notification/httpHosts
   ```

---

# Fase 2: Implementación del Polling (Código en `src/`)

> **Directorio de trabajo:** `hikvision-poller/src/`

1. **Crear la estructura de carpetas**

   ```bash
   mkdir -p src/services src/db src/utils
   ```

2. **`src/utils/httpClient.ts`**
   Implementa un cliente HTTP (Axios o equivalente) con Digest Auth y manejo de timeouts/reintentos.

3. **`src/utils/logger.ts`**
   Define un logger centralizado (por ejemplo, con `winston` o un wrapper de `console`).

4. **`src/db/client.ts`**

    * Inicializa el pool de PostgreSQL leyendo `PG_URL` de variables de entorno.
    * Exporta funciones genéricas para insertar eventos evitando duplicados.

5. **`src/services/eventPoller.ts`**
   Contiene la lógica de polling:

   ```ts
   import httpClient from '../utils/httpClient';
   import db from '../db/client';
   import logger from '../utils/logger';

   const url = `http://${process.env.RELOJ_HOST}/ISAPI/AccessControl/AcsEvent?format=json`;
   const auth = { user: process.env.RELOJ_USER!, pass: process.env.RELOJ_PASS! };
   let searchID = 'search_init';
   let searchResultPosition = 0;

   export async function poll() {
     try {
       const today = new Date().toISOString().slice(0,10);
       const payload = {
         AcsEventCond: {
           searchID,
           searchResultPosition,
           maxResults: 50,
           major: 5,
           minor: 38,
           startTime: `${today}T00:00:00+00:00`,
           endTime:   `${today}T23:59:59+00:00`,
           attendanceStatus: 'checkIn',
         }
       };
       const resp = await httpClient.post(url, payload, { auth });
       const events = resp.data.AcsEvent.InfoList || [];
       await db.insertEvents(events);
       searchResultPosition += events.length;
       logger.info(`Inserted ${events.length} events`);
     } catch (err) {
       logger.error('Polling error', err);
     }
   }
   ```

6. **`src/index.ts`**
   Punto de entrada:

   ```ts
   import 'dotenv/config';
   import { poll } from './services/eventPoller';
   import logger from './utils/logger';

   const interval = Number(process.env.POLL_INTERVAL) * 1000;
   poll();
   setInterval(poll, interval);

   // Opcional: health check HTTP
   import express from 'express';
   const app = express();
   app.get('/health', (_req, res) => res.send('OK'));
   app.listen(process.env.PORT || 3000, () => logger.info('Health on port', process.env.PORT));
   ```

---

# Fase 3: Despliegue e Infraestructura (Servidor Remoto)

1. **Limpieza de Nginx**

   ```bash
   sudo sed -i '/location \/api\/hikvision/,/}/d' /etc/nginx/sites-enabled/default
   sudo nginx -t && sudo systemctl reload nginx
   ```

2. **Port-Forwarding / VPN**
   Asegurar que el servidor remoto puede realizar egress a `192.168.1.7:80` ya sea vía NAT, VPN o regla en el router.

3. **Variables de entorno**

    * Copiar `.env.example` a `.env` y completar:

      ```ini
      RELOJ_HOST=192.168.1.7
      RELOJ_USER=admin
      RELOJ_PASS=NyM=15091503
      POLL_INTERVAL=15
      PG_URL=postgres://user:pass@localhost/db
      ```

4. **Instalar y compilar**

   ```bash
   npm install
   npm run build
   ```

5. **Configurar PM2**

    * `ecosystem.config.js`:

      ```js
      module.exports = {
        apps: [{
          name: 'event-poller',
          script: './dist/services/eventPoller.js',
          env: { NODE_ENV: 'production' }
        }]
      };
      ```
    * Comandos:

      ```bash
      pm2 stop all
      pm2 delete all
      pm2 start ecosystem.config.js
      pm2 save
      pm2 startup
      ```

6. **Firewall (ufw)**

   ```bash
   sudo ufw allow out to 192.168.1.7 port 80 proto tcp
   sudo ufw allow in  3000/tcp
   sudo ufw enable
   ```

7. **Verificar logs y BD**

   ```bash
   pm2 logs event-poller
   psql $PG_URL -c "SELECT * FROM events ORDER BY timestamp DESC LIMIT 5;"
   ```

8. **Documentar**

    * En `README.md`: propósito (solo polling → BD), esquema de carpetas, ejemplo de `.env`, payload de prueba en Postman y comandos PM2.
