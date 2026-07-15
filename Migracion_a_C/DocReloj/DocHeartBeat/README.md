# DeviceHeartbeatService: contrato con ApiReloj

**Revisado contra ApiReloj:** 15 de julio de 2026.

DeviceHeartbeatService es un proyecto externo a este repositorio. Este documento describe únicamente el contrato que debe cumplir para actualizar la IP y última actividad de un residencial. No implica que su código fuente esté incluido aquí.

## Contrato compatible

ApiReloj conserva el contrato histórico sin agregar headers de autenticación:

```http
POST /Residential/heartbeat
Content-Type: application/json
```

```json
{
  "deviceId": "DEVICE-001",
  "residentialId": "RES-001",
  "timeStamp": 1784067600,
  "signature": "a3f5..."
}
```

## Firma

```text
canonical = timeStamp + "|" + deviceId + "|" + residentialId
signature = lowercase_hex(HMAC_SHA256(UTF8(secretKey), UTF8(canonical)))
```

- `timeStamp`: Unix epoch UTC en segundos.
- `deviceId` y `residentialId`: strings exactos, sin conversión numérica.
- `secretKey`: debe coincidir con el secreto usado en `POST /Device`.
- El hexadecimal puede recibirse en mayúsculas o minúsculas si representa bytes válidos; se recomienda minúscula.

## Validaciones del servidor

ApiReloj valida antes de actualizar:

1. POST JSON y body dentro del límite configurado.
2. Campos completos.
3. Timestamp dentro de la ventana temporal permitida; por defecto ±300 segundos.
4. Device y residencial existentes y relacionados.
5. Firma HMAC correcta.
6. Timestamp mayor al último aceptado para modificar estado.

La última validación impide replay. Timestamp aceptado, `LastSeen` e IP se actualizan en una misma transacción.

## Respuestas

- `204`: heartbeat aceptado.
- `204`: replay válido ignorado sin cambios.
- `401`: body, identidad, timestamp o firma inválidos.
- `429`: rate limit.

El emisor actual no necesita interpretar un body ni personalizar su comportamiento por código HTTP. Puede continuar con su intervalo normal. Para diagnóstico se recomienda registrar códigos no exitosos, pero ApiReloj no depende de que lo haga.

## Configuración conceptual del emisor

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

El intervalo debe ser menor al tiempo máximo aceptable sin actualización y razonable frente al rate limit del servidor.

## Aprovisionamiento previo en ApiReloj

El backend debe crear:

1. Residential.
2. Device asociado, con el mismo secreto.

`_secretKey` no se devuelve después del alta. Debe custodiarse y transferirse de manera segura al equipo que instala el emisor.

## Seguridad operativa

- Usar HTTPS en producción.
- No registrar el secreto ni la cadena HMAC con credenciales.
- Sincronizar el reloj del sistema mediante NTP; una diferencia mayor a la ventana provoca `401`.
- No reutilizar el mismo timestamp intencionalmente.
- Rotar el secreto coordinando Device y emisor.

## Diagnóstico

### `401`

- IDs distintos a los almacenados.
- Device asociado a otro residencial.
- secreto diferente;
- timestamp fuera de ventana;
- firma no hexadecimal o cadena canónica distinta.

### `429`

- intervalo demasiado pequeño;
- múltiples emisores usando la misma IP por encima del límite;
- concurrencia global agotada.

### `204` pero no cambia la IP

Puede tratarse de un replay válido: el timestamp ya fue aceptado. Enviar un timestamp Unix nuevo y reciente.
