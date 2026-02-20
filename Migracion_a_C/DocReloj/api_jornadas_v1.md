# API Jornadas V1

## 1. Objetivo
Consultar jornadas laborales derivadas automáticamente de `AccessEvents`.

## 2. Endpoint
- Método: `GET`
- Ruta: `/Jornadas`

## 3. Query params opcionales
- `residentialId`: `int`
- `clockSn`: `string`
- `employeeNumber`: `string`
- `statusCheck`: `OK | INCOMPLETE | ERROR`
- `statusBreak`: `OK | INCOMPLETE | ERROR`
- `fromUtc`: `DateTimeOffset` ISO-8601
- `toUtc`: `DateTimeOffset` ISO-8601
- `updatedSinceUtc`: `DateTimeOffset` ISO-8601
- `limit`: `int` (default `100`)
- `offset`: `int` (default `0`)

## 4. Reglas de comportamiento
1. Si no envías filtros, devuelve jornadas paginadas.
2. Si envías filtro temporal, `fromUtc` y `toUtc` son obligatorios juntos.
3. El rango temporal es inclusivo `[fromUtc, toUtc]`.
4. `limit` debe ser `> 0`.
5. `offset` debe ser `>= 0`.
6. Si envías `residentialId`, se resuelven los `clockSn` desde los relojes de ese residencial.
7. Si envías `residentialId + clockSn` y el reloj no pertenece al residencial, devuelve `200` con lista vacía.
8. Orden de salida: `UpdatedAt DESC`, luego `StartAt DESC`.

## 5. Ejemplos de request
```http
GET /Jornadas?limit=100&offset=0
GET /Jornadas?employeeNumber=123&limit=50&offset=0
GET /Jornadas?statusCheck=INCOMPLETE&limit=100&offset=0
GET /Jornadas?fromUtc=2026-02-18T00:00:00Z&toUtc=2026-02-18T23:59:59Z&limit=100&offset=0
GET /Jornadas?updatedSinceUtc=2026-02-18T12:00:00Z&limit=100&offset=0
GET /Jornadas?residentialId=1&limit=100&offset=0
GET /Jornadas?residentialId=1&clockSn=DS-K1T321MFWX...&limit=100&offset=0
```

## 6. Respuesta 200 (ejemplo)
```json
[
  {
    "jornadaId": "01JMYK4ECF3K3G29M4S9Q0M8AG",
    "employeeNumber": "123",
    "clockSn": "DS-K1T321MFWX20221217V030900ENAA7937545",
    "startAt": "2026-02-18T12:00:00+00:00",
    "breakInAt": "2026-02-18T15:00:00+00:00",
    "breakOutAt": "2026-02-18T15:30:00+00:00",
    "endAt": "2026-02-18T21:00:00+00:00",
    "statusCheck": "OK",
    "statusBreak": "OK",
    "updatedAt": "2026-02-18T21:00:00+00:00"
  }
]
```

## 7. Códigos de respuesta y error
- `200 OK`: consulta exitosa (puede devolver `[]`).
- `400 Bad Request`: filtros inválidos (rango de fechas, paginación, status inválido).
- `404 Not Found`: `residentialId` inexistente.
- `409 Conflict`: reservado para conflictos de negocio.
- `422 Unprocessable Entity`: regla de negocio no clasificable como conflicto.
- `500 Internal Server Error`: error inesperado.

## 8. Notas operativas
1. `statusCheck` y `statusBreak` aceptan `OK`, `INCOMPLETE`, `ERROR`.
2. La creación/actualización de jornadas no se expone por API; sucede al insertarse eventos de acceso.
3. El worker periódico marca como `ERROR` jornadas abiertas vencidas por timeout.
