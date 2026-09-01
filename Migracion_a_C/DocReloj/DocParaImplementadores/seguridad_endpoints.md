# Seguridad de endpoints

Esta es la copia autocontenida para integradores del documento `../seguridad_endpoints.md`, vigente al 15 de julio de 2026.

## Matriz

| Consumidor | Endpoint | Requisito |
|---|---|---|
| Backend | Todos excepto heartbeat y push | `X-Api-Key` válido + IP fija configurada. |
| Emisor heartbeat | `POST /Residential/heartbeat` | Body JSON actual, asociación device-residencial, HMAC, timestamp reciente y antireplay. |
| Reloj | `POST /AccessEvents/push/{relojId}` | IP igual a `Residential.IpActual` del reloj y `DeviceSn` configurado. |

## Backend

```http
X-Api-Key: <secreto>
```

- Falta o error de API key: `401`.
- API key válida desde una IP distinta: `403`.
- Límite global de concurrencia agotado: `429`.

## Heartbeat

El body no cambió:

```json
{
  "deviceId": "DEVICE-001",
  "residentialId": "RES-001",
  "timeStamp": 1784067600,
  "signature": "a3f5..."
}
```

La firma es HMAC-SHA256 hexadecimal sobre `timeStamp|deviceId|residentialId`. Un heartbeat válido actualiza atómicamente timestamp aceptado, `LastSeen` e IP. Un replay válido responde `204` sin mutar estado. Un heartbeat inválido responde `401`; el rate limit responde `429`.

## Push

El reloj no envía API key. ApiReloj autoriza comparando la IP origen con el residencial asociado al `relojId`. Si el payload incluye `deviceID`, también debe coincidir con el `DeviceSn` configurado. Un fallo devuelve `401` sin revelar la causa concreta.

## Secretos

`_secretKey` es write-only: se acepta en `POST /Device` y nunca aparece en respuestas. En producción se exige HTTPS.
