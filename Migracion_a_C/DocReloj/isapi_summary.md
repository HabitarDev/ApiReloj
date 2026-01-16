# ISAPI (Hikvision) - Resumen para Pasamanos API

Este resumen está basado en los textos extraídos de la documentación ISAPI en `DocReloj/extracted/`.

## Autenticación y headers generales

- **Auth**: ISAPI utiliza **HTTP Digest Authentication**. La guía incluye ejemplos de `WWW-Authenticate: Digest ...` y uso de `HTTPDigestAuth` en clientes HTTP.【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L505-L516】【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L1683-L1694】
- **Headers**: Los mensajes suelen ser **XML o JSON**. El `Content-Type` típico en requests es `application/xml; charset="UTF-8"` (aunque muchos endpoints se usan con `?format=json`).【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L519-L526】

## Gestión de Persons (crear, modificar, eliminar)

### Verificar soporte del dispositivo

- **URL**: `GET /ISAPI/AccessControl/capabilities`
- **Nota**: `isSupportUserInfo = true` indica soporte de person management. `EmployeeNo` (person ID) es el identificador principal y se valida por `EmployeeNoInfo` en capabilities.【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L4692-L4707】

### Crear / Aplicar persona (upsert)

- **URL**: `PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json`
- **Verbo**: `PUT`
- **Soporte**: `GET /ISAPI/AccessControl/UserInfo/capabilities?format=json` debe incluir `supportFunction` con `setUp`.【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L4768-L4786】

### Crear persona (alta explícita)

- **URL**: `POST /ISAPI/AccessControl/UserInfo/Record?format=json`
- **Verbo**: `POST`
- **Soporte**: `supportFunction` debe incluir `post`.【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L4792-L4807】

### Modificar persona

- **URL**: `PUT /ISAPI/AccessControl/UserInfo/Modify?format=json`
- **Verbo**: `PUT`
- **Soporte**: `supportFunction` debe incluir `put`.【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L4817-L4835】

### Eliminar persona

- **URL**: `PUT /ISAPI/AccessControl/UserInfoDetail/Delete?format=json`
- **Verbo**: `PUT`
- **Body esperado** (ejemplo oficial):

```json
{
  "UserInfoDetail": {
    "mode": "all",
    "EmployeeNoList": [
      { "employeeNo": "test" }
    ],
    "operateType": "byTerminal",
    "terminalNoList": [1,2,3,4],
    "orgNoList": [1,2,3,4]
  }
}
```

- **Progreso de borrado**: `GET /ISAPI/AccessControl/UserInfoDetail/DeleteProcess` devuelve `status` y `percent` (0–100).【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L29112-L29255】

## Consulta de eventos de acceso (rango de fechas)

### Verificar soporte

- **URL**: `GET /ISAPI/AccessControl/capabilities`
- **Nota**: `isSupportAcsEvent = true` indica soporte de búsqueda de eventos.
- **Capacidades**: `GET /ISAPI/AccessControl/AcsEvent/capabilities?format=json`.【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L5069-L5079】

### Buscar eventos por rango

- **URL**: `POST /ISAPI/AccessControl/AcsEvent?format=json`
- **Verbo**: `POST`
- **Body esperado** (ejemplo oficial):

```json
{
  "AcsEventCond": {
    "searchID": "test",
    "searchResultPosition": 0,
    "maxResults": 30,
    "major": 1,
    "minor": 1024,
    "startTime": "1970-01-01T00:00:00+08:00",
    "endTime": "1970-01-01T00:00:00+08:00",
    "cardNo": "test",
    "name": "test",
    "employeeNoString": "test",
    "timeReverseOrder": true,
    "picEnable": true,
    "isAttendanceInfo": true
  }
}
```

- **Campos clave**: `startTime`, `endTime`, `major`, `minor`, `employeeNoString`, `picEnable`, `isAttendanceInfo`.【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L12802-L12941】

## Filtros recomendados (solo eventos de huella + ingreso/egreso)

### 1) Filtrar por Major/Minor

- Las categorías principales son: Alarm (0x1), Exception (0x2), Operation (0x3), Additional Info (0x4), Other (0x5).【F:DocReloj/extracted/Access Control Event Types and Event Linkage Types.txt†L7-L10】
- Eventos de huella relevantes (Major = 0x5):
  - `0x26` Fingerprint Matched
  - `0x28` Card + Fingerprint Authentication Completed
  - `0x2e` Fingerprint + Password Authentication Completed
  - `0x45` Employee ID + Fingerprint Authentication Completed
  - (y otras variantes combinadas con PIN/cara).【F:DocReloj/extracted/Access Control Event Types and Event Linkage Types.txt†L656-L742】

### 2) Filtrar por lector

- `cardReaderKind = 4` indica **módulo de huellas** (fingerprint).【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L12531-L12541】

### 3) Filtrar por modo de verificación

- `currentVerifyMode` expone el modo: `fp`, `fpAndPw`, `fpOrCard`, `fpAndCard`, etc. Filtra los que contengan `fp`.【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L12728-L12748】

### 4) Ingreso/Egreso (check-in/check-out)

- El campo `attendanceStatus` puede indicar `checkIn`, `checkOut`, etc. (si `isAttendanceInfo=true`).【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L12751-L12760】【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L12929-L12935】

## Notas adicionales

- La guía confirma que la gestión de persons incluye **credenciales** (tarjetas, huellas, rostros e iris).【F:DocReloj/extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt†L4675-L4682】
- En esta extracción, el esquema exacto de `UserInfo` (campos detallados del JSON) no aparece en las secciones visibles; es probable que esté en secciones posteriores o en otro documento de esquema/SDK.
