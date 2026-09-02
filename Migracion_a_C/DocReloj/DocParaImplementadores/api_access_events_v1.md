# API AccessEvents V1

Contrato vigente al 1 de septiembre de 2026.

## Objetivo

Consultar eventos de acceso ya persistidos en PostgreSQL. Este endpoint nunca consulta un reloj en tiempo real.

## Seguridad

```http
X-Api-Key: <secreto-del-backend>
```

La llamada debe originarse desde la IP fija configurada para el backend.

## Endpoint

```http
GET /AccessEvents
```

## Query params

| Parámetro | Tipo | Default | Regla |
|---|---|---:|---|
| `residentialId` | `string?` | — | Debe existir. Filtra directamente el `ResidentialId` persistido en cada evento. |
| `deviceSn` | `string?` | — | Coincidencia exacta. |
| `employeeNumber` | `string?` | — | Coincidencia exacta. |
| `major` | `int?` | — | Coincidencia exacta. |
| `minor` | `int?` | — | Coincidencia exacta. |
| `attendanceStatus` | `string?` | — | Comparación case-insensitive. |
| `fromUtc` | `DateTimeOffset?` | — | Debe enviarse junto con `toUtc`. Inclusivo. |
| `toUtc` | `DateTimeOffset?` | — | Debe enviarse junto con `fromUtc`. Inclusivo. |
| `limit` | `int` | `100` | Mayor que cero. |
| `offset` | `int` | `0` | Mayor o igual a cero. |

Todos los filtros se combinan con `AND` en PostgreSQL. Si se combinan `residentialId` y `deviceSn`, sólo coinciden filas que contienen ambos valores, independientemente de la relación actual del reloj.

Orden estable:

1. `EventTimeUtc` descendente.
2. `SerialNumber` descendente.
3. `DeviceSn` descendente.

## Ejemplos

```bash
curl -H "X-Api-Key: $API_KEY" "https://api-reloj/AccessEvents?limit=100&offset=0"
```

```bash
curl -H "X-Api-Key: $API_KEY" "https://api-reloj/AccessEvents?residentialId=RES-001&employeeNumber=EMP-7&fromUtc=2026-07-01T00:00:00Z&toUtc=2026-07-31T23:59:59Z"
```

## Respuesta `200`

```json
[
  {
    "_deviceSn": "CLOCK-SN-01",
    "_serialNumber": 10001,
    "_eventTimeUtc": "2026-07-15T11:00:00Z",
    "_timeDevice": "2026-07-15T08:00:00-03:00",
    "_employeeNumber": "EMP-7",
    "_major": 5,
    "_minor": 38,
    "_attendanceStatus": "checkIn",
    "_raw": "{\"SchemaVersion\":\"v1\",\"Source\":\"push\",\"Format\":\"json\",\"ContentType\":\"application/json\",\"HasPicture\":false,\"CapturedAtUtc\":\"2026-07-15T11:00:01Z\",\"Payload\":\"{...}\"}",
    "_residentialId": "RES-001"
  }
]
```

Una consulta correcta sin coincidencias devuelve `200` y `[]`.

## Contrato de `_raw`

`_raw` es un string que contiene JSON válido. No contiene el XML o JSON original suelto, sino un envelope con:

| Campo | Significado |
|---|---|
| `SchemaVersion` | Versión del envelope. |
| `Source` | `push` o `poll`. |
| `Format` | Formato original detectado. |
| `ContentType` | Content-Type recibido o generado. |
| `HasPicture` | Indica si el multipart incluyó archivo. |
| `CapturedAtUtc` | Momento de captura por ApiReloj. |
| `Payload` | Payload original. |

El consumidor debe deserializar primero la respuesta y luego, si necesita el contenido original, deserializar `_raw` como un segundo JSON.

## Errores

| Código | Caso |
|---:|---|
| `400` | Rango incompleto, fechas invertidas, `limit <= 0` u `offset < 0`. |
| `401` | API key ausente o incorrecta. |
| `403` | IP de backend no autorizada. |
| `404` | `residentialId` inexistente. |
| `409` | Conflicto detectado por el filtro global. |
| `422` | Regla de negocio inválida. |
| `429` | Límite global de concurrencia agotado. |
| `500` | Error inesperado. |

Los errores de aplicación se representan como `ProblemDetails`. Las respuestas generadas directamente por autenticación/autorización pueden no tener el mismo cuerpo.

## Notas de integración

- `_residentialId` se fija durante la ingesta y no cambia si el reloj se elimina o reasigna.
- Los datos heredados cuya pertenencia no puede demostrarse usan `__legacy__`; no se asignan al residencial actual del reloj y quedan fuera de consultas de tenants reales.
- Las fechas de filtro son UTC o deben incluir offset explícito.
- La idempotencia de ingesta es `DeviceSn + SerialNumber`.
- La consulta es apta para reconciliación incremental, pero no posee cursor propio; usar fechas y paginación estable.
