# Instalación de DeviceHeartbeatService

**Alcance:** guía operativa para el proyecto externo DeviceHeartbeatService. ApiReloj no contiene su ejecutable ni código fuente.

## Datos necesarios

Solicitar al administrador:

- URL HTTPS completa: `https://<host>/Residential/heartbeat`;
- `DeviceId` string;
- `ResidentialId` string;
- secreto HMAC;
- intervalo de envío.

Los tres valores de identidad deben coincidir exactamente con ApiReloj.

## Configuración

En el proyecto/artefacto del emisor, configurar:

```json
{
  "Device": {
    "SecretKey": "SECRETO_HEARTBEAT",
    "DeviceId": "DEVICE-001",
    "ResidentialId": "RES-001",
    "HeartbeatUrl": "https://api-reloj/Residential/heartbeat",
    "IntervalSeconds": 30
  }
}
```

El emisor debe generar un timestamp Unix nuevo en cada iteración y firmar `timeStamp|deviceId|residentialId` con HMAC-SHA256.

## Preparación del equipo

1. Sincronizar fecha y hora con NTP.
2. Verificar resolución DNS y acceso HTTPS a ApiReloj.
3. Instalar el certificado raíz necesario si se usa una PKI privada.
4. Proteger ACL del directorio que contiene el secreto.
5. Confirmar que no haya otro servicio enviando con la misma identidad.

## Publicación e instalación

Los comandos exactos dependen del repositorio externo. Para un Worker Service .NET 8 típico:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

Copiar el contenido publicado a una ruta permanente, por ejemplo:

```text
C:\Services\DeviceHeartbeatService\
```

Crear el servicio desde una consola elevada, ajustando el nombre real del ejecutable:

```cmd
sc create DeviceHeartbeatService binPath= "C:\Services\DeviceHeartbeatService\DeviceHeartbeatService.exe" start= auto
sc start DeviceHeartbeatService
sc query DeviceHeartbeatService
```

No mover ni eliminar los archivos mientras el servicio esté registrado.

## Verificación

1. Confirmar que el proceso permanece iniciado.
2. Revisar los logs propios del emisor.
3. Desde el backend autenticado consultar `GET /Device/{deviceId}` y verificar `_lastSeen`.
4. Consultar `GET /Residential/{residentialId}` y verificar `_ipActual`.

La verificación contra ApiReloj requiere `X-Api-Key` e IP autorizada; el emisor heartbeat no necesita esa credencial.

## Resultados esperados

- `204`: aceptado o replay válido.
- `401`: identidad, asociación, firma o tiempo inválidos.
- `429`: exceso de frecuencia.

El servicio puede continuar sin consumir el body de respuesta. Se recomienda registrar códigos no exitosos para instalación y soporte.

## Troubleshooting

### No inicia

- confirmar ruta del ejecutable y `appsettings.json`;
- revisar Event Viewer y logs del proyecto externo;
- verificar permisos de lectura del secreto.

### ApiReloj devuelve `401`

- comparar IDs carácter por carácter;
- confirmar secreto;
- verificar NTP y zona horaria del sistema;
- comprobar el orden exacto de la cadena firmada;
- generar hexadecimal sin guiones.

### ApiReloj devuelve `429`

- aumentar intervalo;
- revisar si existen instalaciones duplicadas;
- coordinar límites con el administrador.

### No se actualiza la IP

- confirmar que el timestamp sea nuevo;
- revisar que se esté alcanzando la URL correcta;
- comprobar que la IP observada por ApiReloj sea la esperada.

## Gestión

```cmd
sc stop DeviceHeartbeatService
sc start DeviceHeartbeatService
sc delete DeviceHeartbeatService
```

Para rotar el secreto: detener el servicio, actualizar primero el Device en el procedimiento administrativo acordado, cambiar configuración de forma coordinada y volver a iniciar. ApiReloj actualmente sólo expone alta de Device, por lo que una rotación puede requerir procedimiento de datos/administración adicional.
