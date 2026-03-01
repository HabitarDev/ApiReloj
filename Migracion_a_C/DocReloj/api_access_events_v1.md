# API AccessEvents V1

## 1. Objetivo
Consultar eventos de acceso almacenados localmente en la tabla `AccessEvents`.

## 2. Endpoint
- Metodo: `GET`
- Ruta: `/AccessEvents`

## 3. Query Params (opcionales)
| Parametro | Tipo | Default | Descripcion |
|---|---|---|---|
| `residentialId` | `int` | `null` | Filtra eventos de un residencial (resuelto por relojes vinculados y `deviceSn`). |
| `deviceSn` | `string` | `null` | Filtra por serial del reloj. |
| `employeeNumber` | `string` | `null` | Filtra por empleado. |
| `major` | `int` | `null` | Filtra por major de evento. |
| `minor` | `int` | `null` | Filtra por minor de evento. |
| `attendanceStatus` | `string` | `null` | Filtra por estado de asistencia (case-insensitive). |
| `fromUtc` | `DateTimeOffset` | `null` | Inicio de rango temporal (ISO 8601). |
| `toUtc` | `DateTimeOffset` | `null` | Fin de rango temporal (ISO 8601). |
| `limit` | `int` | `100` | Tamano de pagina. Debe ser `> 0`. |
| `offset` | `int` | `0` | Desplazamiento para paginacion. Debe ser `>= 0`. |

## 4. Reglas de Comportamiento
1. Si no se envia ningun filtro, devuelve eventos globales paginados.
2. Si se usa filtro temporal, `fromUtc` y `toUtc` son obligatorios juntos.
3. El rango temporal es inclusivo: `[fromUtc, toUtc]`.
4. Si se usa `residentialId`, el sistema busca los relojes del residencial y agrega eventos por cada `deviceSn`.
5. Si se envia `residentialId + deviceSn`, el `deviceSn` debe pertenecer al residencial.
6. Si `deviceSn` no pertenece al residencial, la respuesta es `200` con lista vacia.
7. Orden de salida: `eventTimeUtc DESC`, luego `serialNumber DESC`.
8. Los filtros se combinan con `AND`.
9. `attendanceStatus` se compara sin distinguir mayusculas/minusculas.

## 5. Ejemplos de Uso

### 5.1 Todos (paginado)
```http
GET /AccessEvents?limit=100&offset=0
```

### 5.2 Por rango de fechas
```http
GET /AccessEvents?fromUtc=2026-02-15T00:00:00Z&toUtc=2026-02-15T23:59:59Z&limit=100&offset=0
```

### 5.3 Por residencial
```http
GET /AccessEvents?residentialId=1&limit=100&offset=0
```

### 5.4 Por residencial + empleado
```http
GET /AccessEvents?residentialId=1&employeeNumber=123&limit=100&offset=0
```

### 5.5 Por residencial + deviceSn + rango
```http
GET /AccessEvents?residentialId=1&deviceSn=DS-K1T...&fromUtc=2026-02-15T00:00:00Z&toUtc=2026-02-15T23:59:59Z&limit=100&offset=0
```

### 5.6 Por major + minor
```http
GET /AccessEvents?major=5&minor=38&limit=100&offset=0
```

### 5.7 Por attendanceStatus
```http
GET /AccessEvents?attendanceStatus=checkin&limit=100&offset=0
```

## 6. Respuesta Exitosa

### 200 OK
Devuelve una lista de `AccesEventDto`. Puede ser vacia si no hay resultados.

```json
[
  {
    "_deviceSn": "DS-K1T...",
    "_serialNumber": 987654,
    "_eventTimeUtc": "2026-02-15T12:34:56Z",
    "_timeDevice": "2026-02-15T09:34:56-03:00",
    "_employeeNumber": "123",
    "_major": 5,
    "_minor": 38,
    "_attendanceStatus": "checkIn",
    "_raw": "{\"SchemaVersion\":\"v1\",\"Source\":\"push\",\"Format\":\"xml\",\"ContentType\":\"application/xml\",\"HasPicture\":false,\"CapturedAtUtc\":\"2026-02-24T18:10:00Z\",\"Payload\":\"<EventNotificationAlert>...</EventNotificationAlert>\"}"
  },
  {
    "_deviceSn": "DS-K1T...",
    "_serialNumber": 987655,
    "_eventTimeUtc": "2026-02-15T12:36:10Z",
    "_timeDevice": "2026-02-15T09:36:10-03:00",
    "_employeeNumber": "123",
    "_major": 5,
    "_minor": 38,
    "_attendanceStatus": "checkOut",
    "_raw": "{\"SchemaVersion\":\"v1\",\"Source\":\"poll\",\"Format\":\"json\",\"ContentType\":\"application/json\",\"HasPicture\":false,\"CapturedAtUtc\":\"2026-02-24T18:40:00Z\",\"Payload\":\"{\\\"major\\\":5,\\\"minor\\\":38,\\\"time\\\":\\\"2026-02-15T09:36:10-03:00\\\"}\"}"
  }
]
```

## 6.1 Contrato de `_raw` (V1)
1. `_raw` se guarda siempre como un **JSON envelope valido** para compatibilidad con columna `jsonb`.
2. Campos del envelope (serializacion actual):
   1. `SchemaVersion` (`v1`)
   2. `Source` (`push` o `poll`)
   3. `Format` (`json`, `xml`, `unknown`)
   4. `ContentType`
   5. `HasPicture`
   6. `CapturedAtUtc`
   7. `Payload` (contenido original textual)
3. Este contrato aplica para eventos nuevos ingestados por push y por poll.

## 7. Errores y Codigos

### 400 Argumento invalido
Casos tipicos:
1. `fromUtc` sin `toUtc`.
2. `toUtc` sin `fromUtc`.
3. `fromUtc > toUtc`.
4. `limit <= 0`.
5. `offset < 0`.
6. `major < 0`.
7. `minor < 0`.

### 404 No encontrado
Caso tipico:
1. `residentialId` inexistente.

### 409 Conflicto
No esperado para consulta normal de lectura en este endpoint.

### 422 Regla de negocio
Error de negocio no clasificado como conflicto.

### 500 Error interno
Error inesperado no controlado.

## 8. Notas Operativas
1. `employeeNumber` y `deviceSn` son comparaciones exactas (no fuzzy).
2. `major` y `minor` son comparaciones exactas por igualdad.
3. `attendanceStatus` se filtra case-insensitive.
4. `_raw` ya no depende del formato entrante: siempre viaja como envelope JSON `v1`.
5. Esta documentacion cubre solo AccessEvents V1 (consulta desde BD local).
6. No incluye endpoints de push, personas ni backfill.
