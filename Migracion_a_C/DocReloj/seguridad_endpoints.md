# Seguridad de endpoints

**Estado:** vigente al 1 de septiembre de 2026.

ApiReloj aplica autorización cerrada por defecto. Todo endpoint nuevo queda bajo la política `Backend` salvo que declare explícitamente `Heartbeat` o `ResidentialPush`.

## Matriz de acceso

| Consumidor | Endpoints | Política | Reglas |
|---|---|---|---|
| Backend | Todos excepto heartbeat y push | `Backend` | Un único header `X-Api-Key` válido y conexión desde `Security:Backend:AllowedIp`. |
| Emisor heartbeat | `POST /Residential/heartbeat` | `Heartbeat` | JSON válido, device asociado al residencial, HMAC-SHA256 válida, timestamp reciente y no reutilizado. |
| Reloj Hikvision | `POST /AccessEvents/push/{relojId}` | `ResidentialPush` | IP origen igual a `Residential.IpActual`, reloj existente y `DeviceSn` configurado. |

No se usa CORS como mecanismo de seguridad. CORS sólo gobierna navegadores; no autentica backend, servicios ni relojes.

## Backend

Cada llamada debe incluir:

```http
X-Api-Key: <secreto-largo-y-aleatorio>
```

La API compara la clave de manera resistente a diferencias de tiempo y normaliza direcciones IPv4 mapeadas a IPv6. Una API key ausente o incorrecta produce `401`. Una API key válida desde otra IP produce `403`.

Configuración:

```text
Security__Backend__ApiKey=<secreto-largo-y-aleatorio>
Security__Backend__AllowedIp=<ip-fija-del-backend>
```

La aplicación valida ambos valores al arrancar. En Production no inicia si faltan o son inválidos.

## Heartbeat

El contrato del emisor no cambió:

```json
{
  "deviceId": "DEVICE-001",
  "residentialId": "RES-001",
  "timeStamp": 1784067600,
  "signature": "a3f5..."
}
```

La cadena firmada es:

```text
timeStamp|deviceId|residentialId
```

La firma es HMAC-SHA256 en hexadecimal usando `Device.SecretKey`. La autenticación lee el body con buffering y lo restaura antes del model binding, por lo que el emisor continúa enviando los valores en JSON.

Antes de ejecutar el controlador se valida:

1. Método POST y `Content-Type: application/json`.
2. IP remota disponible y body dentro del tamaño máximo.
3. Campos obligatorios.
4. Timestamp Unix válido dentro de la desviación permitida.
5. Device y residencial existentes y relacionados.
6. Firma HMAC.

La aceptación final es atómica. El timestamp debe ser mayor que `Device.LastAcceptedHeartbeatTimestamp`; su avance se confirma en la misma transacción que `Device.LastSeen` y `Residential.IpActual`. Un replay criptográficamente válido responde `204` sin modificar datos.

Resultados principales:

- `204`: heartbeat válido aceptado o replay válido ignorado.
- `401`: formato de autenticación, identidad, timestamp o firma inválidos.
- `429`: límite por IP o concurrencia agotado.

Opciones:

```text
Security__Heartbeat__AllowedClockSkewSeconds=300
Security__Heartbeat__MaximumBodySizeBytes=8192
Security__Heartbeat__PermitLimitPerIp=600
Security__Heartbeat__RateWindowSeconds=60
Security__Heartbeat__GlobalConcurrencyLimit=200
```

## Push Hikvision

La política obtiene `relojId` de la ruta, carga el reloj y compara la IP observada con la IP actual de su residencial. La autenticación falla si el reloj o residencial no existen, falta `DeviceSn`, la IP no es válida o no coincide.

Después de autenticar, si el payload ISAPI contiene `deviceID`, el servicio comprueba que coincida con el `DeviceSn` configurado. Si Hikvision omite ese campo opcional, la atribución se apoya en la IP y el `relojId` autenticados.

Una autenticación de push fallida produce `401`. No se revela si falló la ruta, el reloj, el residencial o la IP.

## Secretos y respuestas

`Device.SecretKey` se recibe únicamente al crear el device. Nunca se serializa desde `/Device` ni dentro de `/Residential`.

Los secretos no deben almacenarse en archivos versionados. Compose obtiene la API key y la IP autorizada desde `BACKEND_API_KEY` y `BACKEND_ALLOWED_IP`; usar `.env.sample` como plantilla. Para una Application creada desde Dockerfile en Dokploy se debe usar `.env.sample.dokploy`, que contiene los nombres ASP.NET efectivos.

## Red, HTTPS y proxies

En producción se debe usar HTTPS. ApiReloj toma la IP de `HttpContext.Connection.RemoteIpAddress` después de procesar únicamente `X-Forwarded-For` y `X-Forwarded-Proto` provenientes de proxies o redes explícitamente confiables.

Configuración Dokploy/Traefik mínima:

```text
Security__Proxy__Enabled=true
Security__Proxy__ForwardLimit=1
Security__Proxy__KnownNetworks__0=<CIDR-real-de-la-red-Traefik>
```

También puede declararse una IP individual con `Security__Proxy__KnownProxies__0`. Si el proxy está habilitado y no existe al menos una IP o red válida, la aplicación no inicia. Los headers se procesan antes de HTTPS, rate limiting y autorización, por lo que las políticas observan el cliente real sin aceptar suplantaciones desde orígenes no confiables.

No se debe activar `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`; esa alternativa no expresa la lista de confianza exigida por este despliegue.
