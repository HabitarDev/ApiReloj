# Plan de diagnostico de capacidades y formatos ISAPI (Reloj Hikvision)

## 1. Objetivo
Este documento define un flujo unico para:
1. Diagnosticar capacidades reales del reloj (segun firmware actual).
2. Identificar formato real de `UserInfo` para crear/modificar personas.
3. Validar formato real del push (`httpHosts`) y su configuracion.
4. Dejar trazabilidad de respuestas reales + interpretacion tecnica.

## 2. Como trabajar este archivo
1. Tu ejecutas una consulta en Postman.
2. Me pasas request/response.
3. Yo agrego la respuesta en este archivo y explico que significa cada campo relevante.
4. Marcamos el paso como `completado`.

Estados posibles por paso:
- `pendiente`
- `completado`
- `bloqueado`

## 3. Precondiciones
1. Base URL reloj: `http://<IP_RELOJ>` o `https://<IP_RELOJ>`.
2. Auth: `Digest Auth` (usuario/contrasena del reloj).
3. Headers por defecto:
   - `Accept: application/json, application/xml`
   - `Content-Type: application/json` cuando el body sea JSON.
4. Si usas HTTPS con certificado self-signed, desactivar SSL verification en Postman.

## 4. Matriz de pasos
| Paso | Metodo | Endpoint | Objetivo | Estado |
|---|---|---|---|---|
| 1 | GET | `/ISAPI/AccessControl/capabilities` | Capacidades base de AccessControl | completado |
| 2 | GET | `/ISAPI/AccessControl/UserInfo/capabilities?format=json` | Capacidades exactas de UserInfo | completado |
| 3 | GET | `/ISAPI/AccessControl/UserInfo/Count?format=json` | Cantidad total de usuarios | completado |
| 4 | POST | `/ISAPI/AccessControl/UserInfo/Search?format=json` | Listar usuarios y deducir schema real | completado |
| 5 | GET | `/ISAPI/Event/notification/httpHosts/capabilities` | Capacidades push/listening | completado |
| 6 | GET | `/ISAPI/Event/notification/httpHosts` | Ver hosts push configurados | completado |
| 7 | GET | `/ISAPI/Event/notification/httpHosts/{hostId}` | Validar host push puntual | completado |

## 5. Diccionario rapido de capabilities (interpretacion)
Notas:
1. `true` = soportado por firmware.
2. `false` = no soportado.
3. Si no aparece el nodo, usualmente implica no soportado o no aplicable al modelo.

Campos relevantes esperados:
1. `isSupportUserInfo`:
   - `true`: el reloj soporta gestion de personas (`UserInfo`).
2. `isSupportUserInfoDetailDelete`:
   - `true`: soporta borrado de personas por `UserInfoDetail/Delete`.
3. `EmployeeNoInfo`:
   - `employeeNo`: limites de longitud de person ID.
   - `characterType`: tipos de caracteres permitidos.
   - `isSupportCompress`: modo de compresion/normalizacion del ID (si aplica).
4. `supportFunction` en `UserInfo/capabilities`:
   - contiene `get`: soporta busqueda/listado.
   - contiene `setUp`: soporta upsert (`SetUp`).
   - contiene `post`: soporta alta estricta (`Record`).
   - contiene `put`: soporta modificacion (`Modify`).
5. `maxRecordNum`:
   - maximo de usuarios soportados por el dispositivo.

## 6. Paso 1 - AccessControl capabilities
Request:
- Metodo: `GET`
- URL: `/ISAPI/AccessControl/capabilities`
- Body: ninguno

Que mirar:
1. `isSupportUserInfo`
2. `isSupportUserInfoDetailDelete`
3. `EmployeeNoInfo`
4. (si aparece) nodos de eventos: `isSupportAcsEvent`, `isSupportEventStorageCfg`, etc.

### Respuesta recibida
```xml
<AccessControl version="2.0" xmlns="http://www.isapi.org/ver20/XMLSchema">
    <isSupportModuleStatus>true</isSupportModuleStatus>
    <isSupportSNAPConfig>true</isSupportSNAPConfig>
    <isSupportIdentityTerminal>true</isSupportIdentityTerminal>
    <isSupportM1CardEncryptCfg>true</isSupportM1CardEncryptCfg>
    <isSupportDeployInfo>true</isSupportDeployInfo>
    <isSupportFaceCompareCond>true</isSupportFaceCompareCond>
    <isSupportRemoteControlDoor>true</isSupportRemoteControlDoor>
    <isSupportUserInfo>true</isSupportUserInfo>
    <EmployeeNoInfo>
        <employeeNo min="1" max="32"></employeeNo>
        <characterType opt="any"></characterType>
    </EmployeeNoInfo>
    <isSupportCardInfo>true</isSupportCardInfo>
    <isSupportFDLib>true</isSupportFDLib>
    <isSupportUserInfoDetailDelete>true</isSupportUserInfoDetailDelete>
    <isSupportFingerPrintCfg>true</isSupportFingerPrintCfg>
    <isSupportFingerPrintDelete>true</isSupportFingerPrintDelete>
    <isSupportCaptureFingerPrint>true</isSupportCaptureFingerPrint>
    <isSupportDoorStatusWeekPlanCfg>true</isSupportDoorStatusWeekPlanCfg>
    <isSupportVerifyWeekPlanCfg>true</isSupportVerifyWeekPlanCfg>
    <isSupportCardRightWeekPlanCfg>true</isSupportCardRightWeekPlanCfg>
    <isSupportDoorStatusHolidayPlanCfg>true</isSupportDoorStatusHolidayPlanCfg>
    <isSupportVerifyHolidayPlanCfg>true</isSupportVerifyHolidayPlanCfg>
    <isSupportCardRightHolidayPlanCfg>true</isSupportCardRightHolidayPlanCfg>
    <isSupportDoorStatusHolidayGroupCfg>true</isSupportDoorStatusHolidayGroupCfg>
    <isSupportVerifyHolidayGroupCfg>true</isSupportVerifyHolidayGroupCfg>
    <isSupportUserRightHolidayGroupCfg>true</isSupportUserRightHolidayGroupCfg>
    <isSupportDoorStatusPlanTemplate>true</isSupportDoorStatusPlanTemplate>
    <isSupportVerifyPlanTemplate>true</isSupportVerifyPlanTemplate>
    <isSupportUserRightPlanTemplate>true</isSupportUserRightPlanTemplate>
    <isSupportDoorStatusPlan>true</isSupportDoorStatusPlan>
    <isSupportCardReaderPlan>true</isSupportCardReaderPlan>
    <isSupportClearPlansCfg>true</isSupportClearPlansCfg>
    <isSupportEventCardLinkageCfg>true</isSupportEventCardLinkageCfg>
    <isSupportClearEventCardLinkageCfg>true</isSupportClearEventCardLinkageCfg>
    <isSupportAcsEvent>true</isSupportAcsEvent>
    <isSupportAcsEventTotalNum>true</isSupportAcsEventTotalNum>
    <isSupportEventOptimizationCfg>true</isSupportEventOptimizationCfg>
    <isSupportAcsWorkStatus>true</isSupportAcsWorkStatus>
    <isSupportDoorCfg>true</isSupportDoorCfg>
    <isSupportCardReaderCfg>true</isSupportCardReaderCfg>
    <isSupportAcsCfg>true</isSupportAcsCfg>
    <isSupportMaskDetection>true</isSupportMaskDetection>
    <isSupportGroupCfg>true</isSupportGroupCfg>
    <isSupportClearGroupCfg>true</isSupportClearGroupCfg>
    <isSupportMultiCardCfg>true</isSupportMultiCardCfg>
    <isSupportAntiSneakCfg>true</isSupportAntiSneakCfg>
    <isSupportCardReaderAntiSneakCfg>true</isSupportCardReaderAntiSneakCfg>
    <isSupportClearAntiSneakCfg>true</isSupportClearAntiSneakCfg>
    <isSupportClearAntiSneak>true</isSupportClearAntiSneak>
    <isSupportAttendanceStatusModeCfg>false</isSupportAttendanceStatusModeCfg>
    <isSupportAttendanceStatusRuleCfg>false</isSupportAttendanceStatusRuleCfg>
    <FactoryReset>
        <isSupportFactoryReset>true</isSupportFactoryReset>
        <mode opt="full,basic,part"></mode>
    </FactoryReset>
    <isSupportCaptureFace>true</isSupportCaptureFace>
    <isSupportFaceRecognizeMode>true</isSupportFaceRecognizeMode>
    <isSupportNFCCfg>true</isSupportNFCCfg>
    <isSupportRFCardCfg>true</isSupportRFCardCfg>
    <isSupportUserDataImport>false</isSupportUserDataImport>
    <isSupportUserDataExport>false</isSupportUserDataExport>
    <isSupportMaintenanceDataExport>true</isSupportMaintenanceDataExport>
    <isSupportLockTypeCfg>true</isSupportLockTypeCfg>
    <isSupportKeyCfgAttendance>true</isSupportKeyCfgAttendance>
    <isSupportAsyncImportDatas>true</isSupportAsyncImportDatas>
    <isSupportAsyncImportPic>true</isSupportAsyncImportPic>
    <isSupportAttendanceWeekPlan>true</isSupportAttendanceWeekPlan>
    <isSupportClearAttendancePlan>true</isSupportClearAttendancePlan>
    <isSupportAttendanceStatusPlan>true</isSupportAttendanceStatusPlan>
    <isSupportAttendanceMode>true</isSupportAttendanceMode>
    <isSupportAttendancePlanTemplate>true</isSupportAttendancePlanTemplate>
    <isSupportAttendancePlanTemplateList>true</isSupportAttendancePlanTemplateList>
    <isSupportEncryption>true</isSupportEncryption>
    <isSupportFingerDataEncryption>true</isSupportFingerDataEncryption>
    <isSupportClearPictureCfg>true</isSupportClearPictureCfg>
    <isSupportEventStorageCfg>true</isSupportEventStorageCfg>
    <isSupportCardVerificationRule>true</isSupportCardVerificationRule>
    <isSupportClearUserAuthenticationTimes>true</isSupportClearUserAuthenticationTimes>
    <isSupportSearchModelFailRecord>true</isSupportSearchModelFailRecord>
</AccessControl>
```

### Analisis Codex
Estado:
1. Paso `completado`.

Lectura clave para este proyecto:
1. Personas soportadas:
   - `isSupportUserInfo=true`.
   - `EmployeeNoInfo.employeeNo min=1 max=32`.
   - `EmployeeNoInfo.characterType=any`.
2. Borrado de personas soportado:
   - `isSupportUserInfoDetailDelete=true`.
3. Eventos de acceso soportados:
   - `isSupportAcsEvent=true`.
   - `isSupportAcsEventTotalNum=true`.
   - `isSupportEventStorageCfg=true`.
4. Credenciales soportadas:
   - Tarjeta, huella y cara: `true`.
5. Restriccion detectada:
   - `isSupportUserDataImport=false` y `isSupportUserDataExport=false`.
   - Implica que la import/export masiva de usuarios no iria por ese camino de capability.

Interpretacion capability por capability:
| Campo | Valor | Significado operativo |
|---|---|---|
| `isSupportModuleStatus` | `true` | Soporta consulta de estado de modulos del sistema de acceso. |
| `isSupportSNAPConfig` | `true` | Soporta configuracion SNAP asociada al control de acceso. |
| `isSupportIdentityTerminal` | `true` | El equipo se comporta como terminal de identidad/control de acceso. |
| `isSupportM1CardEncryptCfg` | `true` | Soporta configuracion de cifrado para tarjetas M1. |
| `isSupportDeployInfo` | `true` | Soporta informacion/configuracion de despliegue del equipo. |
| `isSupportFaceCompareCond` | `true` | Soporta condiciones de comparacion facial configurables. |
| `isSupportRemoteControlDoor` | `true` | Soporta apertura/cierre remoto de puerta por API. |
| `isSupportUserInfo` | `true` | Soporta gestion de personas (`UserInfo`). |
| `EmployeeNoInfo.employeeNo` | `min=1,max=32` | El `employeeNo` de persona debe respetar longitud 1..32. |
| `EmployeeNoInfo.characterType` | `any` | `employeeNo` admite cualquier tipo de caracter soportado por firmware. |
| `isSupportCardInfo` | `true` | Soporta gestion de tarjetas. |
| `isSupportFDLib` | `true` | Soporta libreria facial (Face Data Library). |
| `isSupportUserInfoDetailDelete` | `true` | Soporta borrado de persona con endpoint `UserInfoDetail/Delete`. |
| `isSupportFingerPrintCfg` | `true` | Soporta configuracion/gestion de huellas. |
| `isSupportFingerPrintDelete` | `true` | Soporta borrado de huellas. |
| `isSupportCaptureFingerPrint` | `true` | Soporta captura de huella desde el dispositivo/API. |
| `isSupportDoorStatusWeekPlanCfg` | `true` | Soporta plan semanal de estado de puerta. |
| `isSupportVerifyWeekPlanCfg` | `true` | Soporta plan semanal de modos de verificacion. |
| `isSupportCardRightWeekPlanCfg` | `true` | Soporta plan semanal de permisos de tarjetas/usuarios. |
| `isSupportDoorStatusHolidayPlanCfg` | `true` | Soporta plan de feriados para estado de puerta. |
| `isSupportVerifyHolidayPlanCfg` | `true` | Soporta plan de feriados para verificacion. |
| `isSupportCardRightHolidayPlanCfg` | `true` | Soporta plan de feriados para permisos. |
| `isSupportDoorStatusHolidayGroupCfg` | `true` | Soporta grupos de feriados para estado de puerta. |
| `isSupportVerifyHolidayGroupCfg` | `true` | Soporta grupos de feriados para verificacion. |
| `isSupportUserRightHolidayGroupCfg` | `true` | Soporta grupos de feriados para permisos de usuario. |
| `isSupportDoorStatusPlanTemplate` | `true` | Soporta plantillas de planes de estado de puerta. |
| `isSupportVerifyPlanTemplate` | `true` | Soporta plantillas de planes de verificacion. |
| `isSupportUserRightPlanTemplate` | `true` | Soporta plantillas de permisos de usuario. |
| `isSupportDoorStatusPlan` | `true` | Soporta planes de estado de puerta. |
| `isSupportCardReaderPlan` | `true` | Soporta planificacion/configuracion por lector. |
| `isSupportClearPlansCfg` | `true` | Soporta limpieza/reset de configuraciones de planes. |
| `isSupportEventCardLinkageCfg` | `true` | Soporta vinculacion evento-tarjeta. |
| `isSupportClearEventCardLinkageCfg` | `true` | Soporta limpiar vinculaciones evento-tarjeta. |
| `isSupportAcsEvent` | `true` | Soporta busqueda/consulta de eventos de acceso. |
| `isSupportAcsEventTotalNum` | `true` | Soporta obtener cantidad total de eventos de acceso. |
| `isSupportEventOptimizationCfg` | `true` | Soporta configuracion de optimizacion de eventos. |
| `isSupportAcsWorkStatus` | `true` | Soporta consulta de estado operativo del modulo ACS. |
| `isSupportDoorCfg` | `true` | Soporta configuracion de puertas. |
| `isSupportCardReaderCfg` | `true` | Soporta configuracion de lectores. |
| `isSupportAcsCfg` | `true` | Soporta configuracion general de access control. |
| `isSupportMaskDetection` | `true` | Soporta deteccion de uso de mascarilla. |
| `isSupportGroupCfg` | `true` | Soporta gestion de grupos. |
| `isSupportClearGroupCfg` | `true` | Soporta limpiar configuracion de grupos. |
| `isSupportMultiCardCfg` | `true` | Soporta reglas de multi-tarjeta. |
| `isSupportAntiSneakCfg` | `true` | Soporta configuracion anti-passback (anti-sneak). |
| `isSupportCardReaderAntiSneakCfg` | `true` | Soporta anti-passback a nivel lector. |
| `isSupportClearAntiSneakCfg` | `true` | Soporta limpieza de config anti-passback. |
| `isSupportClearAntiSneak` | `true` | Soporta limpiar estado/registros anti-passback. |
| `isSupportAttendanceStatusModeCfg` | `false` | No soporta ese modo especifico de configuracion de estado de asistencia. |
| `isSupportAttendanceStatusRuleCfg` | `false` | No soporta reglas de estado de asistencia por ese endpoint/capability. |
| `FactoryReset.isSupportFactoryReset` | `true` | Soporta reseteo de fabrica. |
| `FactoryReset.mode` | `full,basic,part` | Modos de reset de fabrica disponibles. |
| `isSupportCaptureFace` | `true` | Soporta captura facial. |
| `isSupportFaceRecognizeMode` | `true` | Soporta configuracion de modo de reconocimiento facial. |
| `isSupportNFCCfg` | `true` | Soporta configuracion NFC. |
| `isSupportRFCardCfg` | `true` | Soporta configuracion de tarjetas RF. |
| `isSupportUserDataImport` | `false` | No soporta importacion de datos de usuario por esa funcion. |
| `isSupportUserDataExport` | `false` | No soporta exportacion de datos de usuario por esa funcion. |
| `isSupportMaintenanceDataExport` | `true` | Soporta export de datos de mantenimiento. |
| `isSupportLockTypeCfg` | `true` | Soporta configuracion de tipo de cerradura. |
| `isSupportKeyCfgAttendance` | `true` | Soporta configuraciones de teclas relacionadas a asistencia. |
| `isSupportAsyncImportDatas` | `true` | Soporta importacion asincrona de datos. |
| `isSupportAsyncImportPic` | `true` | Soporta importacion asincrona de imagenes. |
| `isSupportAttendanceWeekPlan` | `true` | Soporta plan semanal de asistencia. |
| `isSupportClearAttendancePlan` | `true` | Soporta limpiar planes de asistencia. |
| `isSupportAttendanceStatusPlan` | `true` | Soporta planes de estado de asistencia. |
| `isSupportAttendanceMode` | `true` | Soporta modos de asistencia. |
| `isSupportAttendancePlanTemplate` | `true` | Soporta plantilla de planes de asistencia. |
| `isSupportAttendancePlanTemplateList` | `true` | Soporta listado de plantillas de asistencia. |
| `isSupportEncryption` | `true` | Soporta funciones de cifrado en flujos/configs relacionados. |
| `isSupportFingerDataEncryption` | `true` | Soporta cifrado de datos biometrico de huella. |
| `isSupportClearPictureCfg` | `true` | Soporta configuracion de limpieza de imagenes. |
| `isSupportEventStorageCfg` | `true` | Soporta configurar almacenamiento de eventos. |
| `isSupportCardVerificationRule` | `true` | Soporta reglas de verificacion de tarjeta. |
| `isSupportClearUserAuthenticationTimes` | `true` | Soporta limpiar contadores de autenticacion de usuario. |
| `isSupportSearchModelFailRecord` | `true` | Soporta consulta de registros de fallos de modelo/algoritmo. |

Implicaciones directas para endpoints pasamanos:
1. Se puede avanzar con `CreatePerson` (UserInfo), `ModifyPerson` y `DeletePerson`.
2. Antes de crear endpoint final, falta Paso 2 (`UserInfo/capabilities`) para definir:
   - si usamos preferentemente `SetUp` (upsert) o `Record` (alta estricta),
   - y el schema exacto soportado por este firmware.

---

## 7. Paso 2 - UserInfo capabilities
Request:
- Metodo: `GET`
- URL: `/ISAPI/AccessControl/UserInfo/capabilities?format=json`
- Body: ninguno

Que mirar:
1. `supportFunction`
2. `maxRecordNum`
3. Restricciones de campos de `UserInfo` (si el firmware las devuelve)

### Respuesta recibida
```json
{
    "UserInfo": {
        "supportFunction": {
            "@opt": "post,delete,put,get,setUp"
        },
        "UserInfoSearchCond": {
            "maxResults": {
                "@min": 1,
                "@max": 30
            },
            "EmployeeNoList": {
                "maxSize": 30,
                "employeeNo": {
                    "@min": 1,
                    "@max": 32
                }
            },
            "fuzzySearch": {
                "@min": 0,
                "@max": 128
            },
            "isSupportNumOfFace": true,
            "isSupportNumOfFP": true,
            "isSupportNumOfCard": true,
            "hasFace": {
                "@opt": [
                    true,
                    false
                ]
            },
            "hasCard": {
                "@opt": [
                    true,
                    false
                ]
            },
            "hasFingerprint": {
                "@opt": [
                    true,
                    false
                ]
            }
        },
        "UserInfoDelCond": {
            "EmployeeNoList": {
                "maxSize": 30,
                "employeeNo": {
                    "@min": 1,
                    "@max": 32
                }
            }
        },
        "employeeNo": {
            "@min": 1,
            "@max": 32
        },
        "name": {
            "@min": 0,
            "@max": 128
        },
        "userType": {
            "@opt": "normal,visitor,blackList"
        },
        "closeDelayEnabled": "true,false",
        "Valid": {
            "enable": "true,false",
            "beginTime": {
                "@min": 1,
                "@max": 32
            },
            "endTime": {
                "@min": 1,
                "@max": 32
            },
            "timeRangeBegin": "2000-01-01T00:00:00",
            "timeRangeEnd": "2037-12-31T23:59:59",
            "timeType": {
                "@opt": "local"
            }
        },
        "maxBelongGroup": 4,
        "belongGroup": {
            "@min": 1,
            "@max": 32
        },
        "password": {
            "@min": 0,
            "@max": 8
        },
        "doorRight": {
            "@min": 1,
            "@max": 1
        },
        "RightPlan": {
            "maxSize": 1,
            "doorNo": {
                "@min": 1,
                "@max": 1
            },
            "maxPlanTemplate": 4,
            "planTemplateNo": {
                "@min": 1,
                "@max": 255
            }
        },
        "maxOpenDoorTime": {
            "@min": 0,
            "@max": 255
        },
        "openDoorTime": {
            "@min": 0,
            "@max": 255
        },
        "localUIRight": "true,false",
        "userVerifyMode": {
            "@opt": "cardAndPw,card,fp,fpAndPw,fpOrCard,fpAndCard,fpAndCardAndPw,faceOrFpOrCardOrPw,faceAndFp,faceAndPw,faceAndCard,face,faceAndFpAndCard,faceAndPwAndFp,fpOrface,cardOrFace,cardOrFaceOrFp"
        },
        "maxRecordNum": 3000,
        "gender": {
            "@opt": "male,female,unknown"
        },
        "purePwdVerifyEnable": true,
        "PersonInfoExtends": {
            "maxSize": 1,
            "value": {
                "@min": 0,
                "@max": 64
            }
        }
    }
}
```

### Analisis Codex
Estado:
1. Paso `completado`.

Lectura clave para este proyecto:
1. Soporte total de operaciones de persona:
   - `supportFunction=@opt: post,delete,put,get,setUp`.
   - Con esto tienes alta (`post`), modificacion (`put`), borrado (`delete`), consulta (`get`) y upsert (`setUp`).
2. Recomendacion para endpoint pasamanos `CreatePerson`:
   - Preferir `SetUp` como estrategia base (upsert) para evitar conflicto si el usuario ya existe.
   - Dejar `Record` como opcion estricta si necesitas semantica "fallar si existe".
3. Capacidad total del reloj:
   - `maxRecordNum=3000` usuarios.
4. Restricciones de identificador:
   - `employeeNo` longitud 1..32.
   - Es consistente con el Paso 1 (`EmployeeNoInfo`).
5. Paginacion de busqueda:
   - `UserInfoSearchCond.maxResults` maximo 30 por request.
   - `EmployeeNoList.maxSize=30` para busqueda/borrado por lote.
6. Filtros de inventario biometrico:
   - `hasFace`, `hasCard`, `hasFingerprint` soportados.
   - `isSupportNumOfFace/FP/Card=true` indica que el reloj soporta conteos por usuario.

Restricciones de campos relevantes (`UserInfo`) para normalizacion:
1. `name`: 0..128.
2. `userType`: `normal|visitor|blackList`.
3. `password`: 0..8.
4. `gender`: `male|female|unknown`.
5. `Valid`:
   - `enable=true|false`.
   - rango temporal permitido: `2000-01-01T00:00:00` a `2037-12-31T23:59:59`.
   - `timeType=local`.
6. `belongGroup`:
   - hasta 4 grupos por usuario (`maxBelongGroup=4`).
7. `doorRight` / `RightPlan`:
   - este reloj reporta alcance de puerta `1..1` y `RightPlan.maxSize=1`.
   - Implica dispositivo de una sola puerta para permisos directos.
8. `openDoorTime`:
   - rango 0..255.
9. `localUIRight` y `closeDelayEnabled`:
   - booleano (`true|false`).
10. `userVerifyMode`:
   - enum amplio soportado (tarjeta, huella, rostro y combinaciones).

Implicaciones directas para tu API:
1. DTO normalizado V1 recomendado para crear/modificar:
   - `employeeNo` (req)
   - `name` (opt)
   - `userType` (opt, default `normal`)
2. Campos avanzados se pueden exponer en V2:
   - `Valid`, `userVerifyMode`, `doorRight/RightPlan`, `password`, `gender`, `belongGroup`.
3. Para listados desde reloj:
   - siempre paginar en bloques de hasta 30.

Siguiente paso operativo:
1. Ejecutar Paso 4 (`POST /ISAPI/AccessControl/UserInfo/Search?format=json`) para extraer el formato real de `UserInfo` en registros existentes.

---

## 8. Paso 3 - User count
Request:
- Metodo: `GET`
- URL: `/ISAPI/AccessControl/UserInfo/Count?format=json`
- Body: ninguno

Que mirar:
1. `userNumber` (total de usuarios cargados)

### Respuesta recibida
```json
{
    "UserInfoCount": {
        "userNumber": 87,
        "bindFaceUserNumber": 1,
        "bindFingerprintUserNumber": 81,
        "bindCardUserNumber": 0
    }
}
```

### Analisis Codex
Estado:
1. Paso `completado`.

Lectura clave para este proyecto:
1. Inventario total:
   - `userNumber=87` usuarios registrados en el reloj.
2. Cobertura de credenciales:
   - `bindFingerprintUserNumber=81` (muy alta adopcion de huella).
   - `bindFaceUserNumber=1` (muy baja adopcion de rostro).
   - `bindCardUserNumber=0` (sin tarjetas vinculadas actualmente).
3. Consistencia con capacidades previas:
   - El reloj soporta cara/huella/tarjeta (Paso 1), pero en la base real hoy predominan huellas.

Implicaciones directas para tu API:
1. En el endpoint de consulta de usuarios/eventos, conviene exponer flags/contadores de biometria para observabilidad.
2. Para pruebas de `CreatePerson` y luego validacion en eventos, priorizar casos con huella porque es el flujo operativo real.
3. En el Paso 4 (`UserInfo/Search`), esperamos 87 resultados totales; con `maxResults=30`, deberian salir en 3 paginas:
   - pagina 1: posicion 0, max 30
   - pagina 2: posicion 30, max 30
   - pagina 3: posicion 60, max 30

---

## 9. Paso 4 - User search (descubrir formato real de usuario)
Request:
- Metodo: `POST`
- URL: `/ISAPI/AccessControl/UserInfo/Search?format=json`
- Body sugerido (intento A):
```json
{
  "UserInfoSearchCond": {
    "searchID": "1",
    "searchResultPosition": 0,
    "maxResults": 30
  }
}
```

Si falla el intento A, probar intento B:
```json
{
  "UserInfoSearchCond": {
    "searchID": "1",
    "searchResultPosition": 0,
    "maxResults": 30,
    "EmployeeNoList": []
  }
}
```

Objetivo de este paso:
1. Obtener `UserInfo` reales del reloj para ver campos concretos.
2. Derivar el body minimo para `SetUp/Record/Modify`.

Que mirar:
1. Nombre exacto del nodo de salida (ej: `UserInfoSearch`, `UserInfoList`, etc.).
2. Campos reales de cada usuario (ej: `employeeNo`, `name`, `userType`, otros).
3. Campos que siempre vienen vs campos opcionales.

### Respuesta recibida
```json
{
  "UserInfoSearch": {
    "searchID": "1",
    "responseStatusStrg": "MORE",
    "numOfMatches": 30,
    "totalMatches": 87,
    "UserInfo": [
      {
        "employeeNo": "1",
        "name": "Martin",
        "userType": "normal",
        "closeDelayEnabled": false,
        "Valid": {
          "enable": false,
          "beginTime": "2025-03-16T00:00:00",
          "endTime": "2035-03-16T23:59:59",
          "timeType": "local"
        },
        "belongGroup": "",
        "password": "",
        "doorRight": "1",
        "RightPlan": [
          {
            "doorNo": 1,
            "planTemplateNo": "1"
          }
        ],
        "maxOpenDoorTime": 0,
        "openDoorTime": 0,
        "localUIRight": true,
        "gender": "unknown",
        "numOfCard": 0,
        "numOfFP": 2,
        "numOfFace": 1,
        "PersonInfoExtends": [
          {
            "value": ""
          }
        ],
        "faceURL": "http://192.168.1.7:80/LOCALS/pic/enrlFace/0/0000000002.jpg@WEB000000000010"
      },
      {
        "employeeNo": "2",
        "name": "nahuel",
        "userType": "normal",
        "closeDelayEnabled": false,
        "Valid": {
          "enable": false,
          "beginTime": "2000-01-01T00:00:00",
          "endTime": "2037-12-31T23:59:59",
          "timeType": "local"
        },
        "belongGroup": "",
        "password": "",
        "doorRight": "1",
        "RightPlan": [
          {
            "doorNo": 1,
            "planTemplateNo": "1"
          }
        ],
        "maxOpenDoorTime": 0,
        "openDoorTime": 0,
        "localUIRight": true,
        "gender": "unknown",
        "numOfCard": 0,
        "numOfFP": 2,
        "numOfFace": 0,
        "PersonInfoExtends": [
          {
            "value": ""
          }
        ]
      }
    ]
  }
}
```

### Analisis Codex
Estado:
1. Paso `completado`.

Lectura clave para este proyecto:
1. Metodo confirmado:
   - Esta operacion es `POST` (no `GET`) para `UserInfo/Search`.
2. Paginacion confirmada:
   - `responseStatusStrg="MORE"` indica que hay mas paginas.
   - `numOfMatches=30` y `totalMatches=87` coincide con el Paso 3 (`Count`).
3. Siguiente paginado para inventario completo:
   - Pagina 2: `searchResultPosition=30`, `maxResults=30`.
   - Pagina 3: `searchResultPosition=60`, `maxResults=30`.
4. Schema real observado de `UserInfo`:
   - `employeeNo` (string), `name`, `userType`, `closeDelayEnabled`.
   - `Valid.enable/beginTime/endTime/timeType`.
   - `belongGroup`, `password`.
   - `doorRight`, `RightPlan[].doorNo/planTemplateNo`.
   - `maxOpenDoorTime`, `openDoorTime`, `localUIRight`, `gender`.
   - `numOfCard`, `numOfFP`, `numOfFace`.
   - `PersonInfoExtends[].value`.
   - `faceURL` opcional (solo aparece en algunos usuarios con rostro).
5. Nuance de tipos a considerar en DTOs:
   - `doorRight` y `planTemplateNo` vienen como string en respuesta (`"1"`), aunque capabilities los publica con rango numerico.
   - Conviene parsear de forma tolerante (string o numero) en mapeo/normalizacion.

Implicaciones directas para tu API:
1. El endpoint pasamanos de `CreatePerson/ModifyPerson` V1 puede mantenerse normalizado con:
   - `employeeNo`, `name`, `userType`.
2. Para no perder compatibilidad con firmware real, al mapear respuesta desde reloj:
   - tratar `RightPlan` como opcional,
   - tolerar strings numericos,
   - tolerar campos vacios (`belongGroup`, `password`).
3. El campo `faceURL` no debe asumirse presente en todos los usuarios.

### Actualizacion recibida - Pagina 2 (`searchID=2`, `searchResultPosition=30`)
Request:
```json
{
  "UserInfoSearchCond": {
    "searchID": "2",
    "searchResultPosition": 30,
    "maxResults": 30
  }
}
```

Resumen de respuesta:
```json
{
  "UserInfoSearch": {
    "searchID": "2",
    "responseStatusStrg": "MORE",
    "numOfMatches": 30,
    "totalMatches": 87,
    "UserInfo": [
      {
        "employeeNo": "32",
        "name": "SHEILALI ANGELOTTI",
        "userType": "normal"
      },
      {
        "employeeNo": "61",
        "name": "",
        "userType": "normal",
        "Valid": {
          "enable": true,
          "beginTime": "2000-01-01T00:00:00",
          "endTime": "2037-12-31T23:59:59"
        }
      }
    ]
  }
}
```

Analisis de la pagina 2:
1. Confirma continuidad del mismo schema detectado en pagina 1.
2. Rango de usuarios recibido: `employeeNo` 32..61 (30 registros).
3. Sigue apareciendo `doorRight: "1"` y `RightPlan` de 1 puerta.
4. Continuan `numOfCard=0` y variaciones de `numOfFP` (0..3), `numOfFace=0`.
5. Caso borde detectado:
   - `employeeNo=61` con `name=""` y `Valid.enable=true`.
   - Implica que `name` no puede asumirse obligatorio ni no-vacio en datos reales.
6. Como `responseStatusStrg="MORE"`, todavia falta la ultima pagina.

### Actualizacion recibida - Pagina 3 (`searchID=3`, `searchResultPosition=60`)
Request enviado:
```json
{
  "UserInfoSearchCond": {
    "searchID": "3",
    "searchResultPosition": 60,
    "maxResults": 30
  }
}
```

Respuesta reportada:
```json
{
  "UserInfoSearch": {
    "searchID": "3",
    "responseStatusStrg": "OK",
    "numOfMatches": 27,
    "totalMatches": 87,
    "UserInfo": [
      {
        "employeeNo": "62",
        "name": "",
        "userType": "normal",
        "Valid": {
          "enable": true,
          "beginTime": "2000-01-01T00:00:00",
          "endTime": "2037-12-31T23:59:59",
          "timeType": "local"
        }
      },
      {
        "employeeNo": "88",
        "name": "Claudia Santana",
        "userType": "normal",
        "Valid": {
          "enable": false,
          "beginTime": "2026-02-15T00:00:00",
          "endTime": "2036-02-15T23:59:59",
          "timeType": "local"
        }
      }
    ]
  }
}
```

Analisis de la pagina 3:
1. Subpaso cerrado correctamente:
   - `responseStatusStrg="OK"` indica fin de paginacion.
2. Cierre matematico consistente:
   - pagina 1: 30
   - pagina 2: 30
   - pagina 3: 27
   - total: `87` (coincide con `UserInfo/Count`).
3. Schema se mantiene estable y compatible con paginas anteriores.
4. Variaciones reales detectadas que hay que contemplar:
   - `name` puede venir vacio.
   - `Valid.enable` puede ser `true` o `false`.
   - aparece `userVerifyMode` en algunos usuarios (ej. `fp`) como campo opcional.
5. Observacion de inventario:
   - hay `employeeNo` hasta `88` aunque el total sea 87, por lo que no se debe asumir secuencia continua sin huecos.

Siguiente paso operativo actualizado:
1. Diagnostico de `UserInfo/Search` completo.
2. Continuar con el Paso 5: `GET /ISAPI/Event/notification/httpHosts/capabilities`.

---

## 10. Paso 5 - Push capabilities (httpHosts)
Request:
- Metodo: `GET`
- URL: `/ISAPI/Event/notification/httpHosts/capabilities`
- Body: ninguno

Que mirar:
1. `protocolType` soportado (`HTTP`, `HTTPS`, `EHome`).
2. `parameterFormatType` soportado (`XML`, `JSON`, etc.).
3. `SubscribeEventCap.heartbeat` (rango min/max).
4. Eventos soportados en `EventList` (confirmar `AccessControllerEvent`).

### Respuesta recibida
```xml
<HttpHostNotificationCap version="2.0" xmlns="http://www.isapi.org/ver20/XMLSchema">
    <hostNumber>2</hostNumber>
    <urlLen max="128"/>
    <protocolType opt="HTTP,HTTPS,EHome"/>
    <addressingFormatType opt="ipaddress,hostname"/>
    <hostName/>
    <ipAddress opt="ipv4"/>
    <portNo min="0" max="65535"/>
    <userNameLen min="1" max="32"/>
    <passwordLen min="8" max="16"/>
    <httpAuthenticationMethod opt="MD5digest,none,basic"/>
    <SubscribeEventCap>
        <heartbeat min="1" max="30"/>
        <eventMode opt="all,list"/>
        <EventList>
            <Event>
                <type>AccessControllerEvent</type>
                <minorAlarm opt="0x404,0x405,0x40a,0x40b,0x40c,0x40d,0x40e,0x406,0x407,0x40f,0x410"/>
                <minorException opt="0x26,0x27,0x400,0x407,0x408,0x423,0x424,0x428,0x429,0x409,0x40a,0x40f,0x410"/>
                <minorOperation opt="0x50,0x5a,0x5d,0x5e,0x70,0x71,0x76,0x77,0x79,0x7a,0x7b,0x7e,0x86,0x87,0xf0,0xf1,0x137,0x138,0x400,0x401,0x402,0x403,0x404,0x405,0x406,0x407,0x40a,0x40b,0x40c,0x40e,0x419,0x41a,0x421,0x422,0x42f,0x430,0x431,0x432,0x433,0x2601,0x41f,0x420"/>
                <minorEvent opt="0x1,0x6,0x7,0x8,0x9,0xa,0xb,0xc,0xd,0xe,0xf,0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x18,0x19,0x1a,0x1b,0x1c,0x1f,0x20,0x21,0x22,0x23,0x24,0x25,0x26,0x27,0x31,0x33,0x4b,0x4c,0x50,0x68,0x72,0x73,0x74,0x75,0x82,0x84,0x8e,0x97,0x98,0x9b,0xa4,0xbd,0xb5,0xc1,0xbe,0xbf,0xc0,0x9f,0xa0,0x2,0x3,0x4,0x5,0x28,0x29,0x2a,0x2b,0x2c,0x2d,0x2e,0x2f,0x30,0x36,0x37,0x38,0x39,0x3a,0x3b,0x3c,0x3d,0x3e,0x3f,0x40,0x41,0x42,0x43,0x44,0x99,0x9a,0xa8,0xa2,0xa9,0x77"/>
            </Event>
        </EventList>
    </SubscribeEventCap>
</HttpHostNotificationCap>
```

### Analisis Codex
Estado:
1. Paso `completado`.

Lectura clave para este proyecto:
1. Capacidad de destinos push:
   - `hostNumber=2`.
   - El reloj solo permite hasta 2 `httpHosts` configurados simultaneamente.
2. Protocolos soportados:
   - `protocolType=HTTP,HTTPS,EHome`.
   - Para tu API actual aplican `HTTP` o `HTTPS` (no `EHome`).
3. Direccionamiento soportado:
   - `addressingFormatType=ipaddress,hostname`.
   - `ipAddress opt=ipv4` (no anuncia ipv6 en este capability).
4. Limites tecnicos:
   - `urlLen max=128`.
   - `portNo 0..65535`.
   - si hay auth: `userNameLen 1..32`, `passwordLen 8..16`.
5. Autenticacion HTTP soportada:
   - `httpAuthenticationMethod=MD5digest,none,basic`.
   - Compatible con tu enfoque actual sin auth (`none`) y evolucion futura a `MD5digest`.
6. Configuracion de suscripcion:
   - `heartbeat min=1 max=30`.
   - `eventMode=all|list`.
   - evento soportado para esta integracion: `AccessControllerEvent`.
7. Filtrado fino por tipo de evento:
   - el equipo permite restringir por listas de `minorAlarm`, `minorException`, `minorOperation`, `minorEvent`.
   - esto habilita reducir ruido desde el reloj si luego quieres filtrar en origen.
8. Observacion:
   - en esta respuesta no aparece `parameterFormatType`; por tanto hay que confirmar formato real (XML/JSON/multipart) en los pasos 6 y 7 observando configuracion efectiva del host.

Implicaciones directas para tu API/proxy:
1. Restriccion de arquitectura:
   - con `hostNumber=2`, conviene reservar 1 slot productivo y 1 slot de pruebas/backup.
2. Seguridad:
   - V1 puede seguir con `none`, pero ya esta validado que firmware soporta migrar a `MD5digest`.
3. Operacion:
   - `heartbeat` no puede superar 30 segun capability del equipo.

Siguiente paso operativo:
1. Ejecutar Paso 6: `GET /ISAPI/Event/notification/httpHosts` para ver como quedo configurado realmente el host activo.

---

## 11. Paso 6 - Lista de hosts push configurados
Request:
- Metodo: `GET`
- URL: `/ISAPI/Event/notification/httpHosts`
- Body: ninguno

Que mirar:
1. `id`
2. `url` (debe apuntar a `/AccessEvents/push/{relojId}`)
3. `ipAddress` / `hostName`
4. `portNo`
5. `protocolType`
6. `parameterFormatType`
7. `SubscribeEvent.eventMode`, `SubscribeEvent.heartbeat`, `EventList`

### Respuesta recibida
```xml
<?xml version="1.0" encoding="UTF-8"?>
<HttpHostNotificationList version="2.0" xmlns="http://www.isapi.org/ver20/XMLSchema">
    <HttpHostNotification>
        <id>1</id>
        <url>/AccessEvents/push/100001</url>
        <protocolType>HTTP</protocolType>
        <parameterFormatType></parameterFormatType>
        <addressingFormatType>ipaddress</addressingFormatType>
        <ipAddress>18.221.52.251</ipAddress>
        <portNo>8080</portNo>
        <httpAuthenticationMethod>none</httpAuthenticationMethod>
        <SubscribeEvent>
            <heartbeat>30</heartbeat>
            <eventMode>all</eventMode>
            <EventList>
                <Event>
                    <type>AccessControllerEvent</type>
                    <minorAlarm></minorAlarm>
                    <minorException></minorException>
                    <minorOperation></minorOperation>
                    <minorEvent></minorEvent>
                    <pictureURLType>binary</pictureURLType>
                </Event>
            </EventList>
        </SubscribeEvent>
    </HttpHostNotification>
    <HttpHostNotification>
        <id>2</id>
        <url></url>
        <protocolType>EHome</protocolType>
        <parameterFormatType></parameterFormatType>
        <addressingFormatType>ipaddress</addressingFormatType>
        <ipAddress>0.0.0.0</ipAddress>
        <portNo>0</portNo>
        <httpAuthenticationMethod>none</httpAuthenticationMethod>
    </HttpHostNotification>
</HttpHostNotificationList>
```

### Analisis Codex
Estado:
1. Paso `completado`.

Lectura clave para este proyecto:
1. Slot productivo identificado:
   - `id=1` apunta a tu API: `18.221.52.251:8080`.
   - `url=/AccessEvents/push/100001` coincide con tu endpoint esperado por reloj.
2. Protocolo y auth actuales:
   - `protocolType=HTTP`.
   - `httpAuthenticationMethod=none`.
3. Suscripcion configurada:
   - `heartbeat=30` (maximo permitido por capabilities).
   - `eventMode=all`.
   - `Event.type=AccessControllerEvent`.
4. Formato/adjuntos:
   - `pictureURLType=binary` indica que el reloj puede enviar imagen en binario (multipart).
   - `parameterFormatType` viene vacio; esto refuerza que la API receptora debe tolerar variaciones de payload.
5. Slot adicional:
   - `id=2` esta ocupado por `EHome` con `0.0.0.0:0` (practicamente sin destino util).
   - Como `hostNumber=2`, no queda un tercer slot libre.

Implicaciones directas para tu API/proxy:
1. La configuracion activa ya esta alineada con el flujo push que diseñaste.
2. Si quieres un segundo destino HTTP de backup/pruebas, primero deberias reciclar o reconfigurar el `id=2`.
3. Mantener parser tolerante en endpoint push (XML/JSON/multipart + adjuntos opcionales) sigue siendo decision correcta.

Siguiente paso operativo:
1. Ejecutar Paso 7 sobre `id=1`: `GET /ISAPI/Event/notification/httpHosts/1` para confirmar detalle puntual del host productivo.

---

## 12. Paso 7 - Host push puntual
Request:
- Metodo: `GET`
- URL: `/ISAPI/Event/notification/httpHosts/{hostId}`
- Body: ninguno

Que mirar:
1. Mismos puntos del paso 6, pero validando un host especifico.
2. Confirmar que coincide con lo esperado por la API actual.

### Respuesta recibida
```xml
<!-- hostId=2 -->
<?xml version="1.0" encoding="UTF-8"?>
<HttpHostNotification version="2.0" xmlns="http://www.isapi.org/ver20/XMLSchema">
    <id>2</id>
    <url></url>
    <protocolType>EHome</protocolType>
    <parameterFormatType></parameterFormatType>
    <addressingFormatType>ipaddress</addressingFormatType>
    <ipAddress>0.0.0.0</ipAddress>
    <portNo>0</portNo>
    <httpAuthenticationMethod>none</httpAuthenticationMethod>
</HttpHostNotification>

<!-- hostId=1 -->
<?xml version="1.0" encoding="UTF-8"?>
<HttpHostNotification version="2.0" xmlns="http://www.isapi.org/ver20/XMLSchema">
    <id>1</id>
    <url>/AccessEvents/push/100001</url>
    <protocolType>HTTP</protocolType>
    <parameterFormatType></parameterFormatType>
    <addressingFormatType>ipaddress</addressingFormatType>
    <ipAddress>18.221.52.251</ipAddress>
    <portNo>8080</portNo>
    <httpAuthenticationMethod>none</httpAuthenticationMethod>
    <SubscribeEvent>
        <heartbeat>30</heartbeat>
        <eventMode>all</eventMode>
        <EventList>
            <Event>
                <type>AccessControllerEvent</type>
                <minorAlarm></minorAlarm>
                <minorException></minorException>
                <minorOperation></minorOperation>
                <minorEvent></minorEvent>
                <pictureURLType>binary</pictureURLType>
            </Event>
        </EventList>
    </SubscribeEvent>
</HttpHostNotification>
```

### Analisis Codex
Estado:
1. Paso `completado`.

Lectura clave de esta consulta puntual:
1. `hostId=1` confirmado como host productivo correcto para tu API:
   - `protocolType=HTTP`, `ipAddress=18.221.52.251`, `portNo=8080`.
   - `url=/AccessEvents/push/100001`.
   - `SubscribeEvent` activo con `heartbeat=30`, `eventMode=all`, `type=AccessControllerEvent`.
   - `pictureURLType=binary` (puede enviar adjuntos binarios).
2. `hostId=2` confirmado como slot no operativo para tu flujo HTTP:
   - `protocolType=EHome`.
   - `ipAddress=0.0.0.0` y `portNo=0`.
   - `url` vacia.
3. No hay bloque `SubscribeEvent` en `id=2`, consistente con slot no configurado para push de eventos.
4. Queda totalmente validado lo visto en Paso 6.

Siguiente paso operativo:
1. Diagnostico de `httpHosts` completo (pasos 5, 6 y 7 cerrados).

---

## 13. Resultado esperado del diagnostico
Al cerrar los 7 pasos deberiamos tener:
1. Matriz real de capacidades del firmware para Persons.
2. Formato real de usuario devuelto por `UserInfo/Search`.
3. Contrato minimo y contrato recomendado para endpoint pasamanos:
   - `CreatePerson` (hacia todos los relojes de un residencial).
   - `ModifyPerson`.
   - `DeletePerson`.
4. Checklist de compatibilidad por reloj (si algun modelo/firmware cambia campos).

---

## 14. Contrato final propuesto (pasamanos Persons) - validado contra documentacion

Base de validacion usada:
1. Capacidades reales del reloj obtenidas en este diagnostico (pasos 1 a 7).
2. `isapi_summary.md`.
3. `extracted/ISAPI Developer Guide_FingerPrint Terminals_Pro Series.txt` (flujo de personas + delete process).

### 14.1 Decision de diseño (API propia)
Objetivo funcional acordado:
1. El backend envia una sola request por residencial.
2. ApiReloj reenvia la operacion a todos los relojes del residencial (fan-out).
3. ApiReloj responde resultado agregado por reloj.

Endpoints backend -> ApiReloj (normalizados):
1. `POST /api/v1/residentials/{residentialId}/persons`
2. `PUT /api/v1/residentials/{residentialId}/persons/{employeeNo}`
3. `DELETE /api/v1/residentials/{residentialId}/persons/{employeeNo}`

Notas:
1. Esto evita que el backend tenga que conocer cada `relojId`.
2. Si luego quieres operacion puntual por reloj, se puede agregar variante `.../relojes/{relojId}/...`.

### 14.2 Campos normalizados V1 (desde backend)
`CreatePerson` body (minimo):
```json
{
  "employeeNo": "123",
  "name": "Juan Perez",
  "userType": "normal"
}
```

`ModifyPerson` body (minimo):
```json
{
  "name": "Juan Perez",
  "userType": "normal"
}
```

Reglas validadas:
1. `employeeNo`: requerido en create, longitud `1..32`.
2. `name`: opcional, `0..128`, puede venir vacio.
3. `userType` soportado por este reloj/capability real: `normal|visitor|blackList`.
4. En V1 no se mandan huellas/cara/tarjetas desde backend.

### 14.3 Mapeo a ISAPI por operacion

#### A) Crear persona (recomendado: SetUp por idempotencia funcional)
Request a reloj:
```http
PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json
```
```json
{
  "UserInfo": {
    "employeeNo": "123",
    "name": "Juan Perez",
    "userType": "normal"
  }
}
```

Motivo:
1. `SetUp` actua como apply/upsert (si existe edita, si no existe crea).
2. Esta soportado en capabilities reales (`supportFunction` incluye `setUp`).

#### B) Modificar persona
Request a reloj:
```http
PUT /ISAPI/AccessControl/UserInfo/Modify?format=json
```
```json
{
  "UserInfo": {
    "employeeNo": "123",
    "name": "Juan Perez",
    "userType": "normal"
  }
}
```

Motivo:
1. Flujo explicito de edicion.
2. Soportado en capabilities reales (`supportFunction` incluye `put`).

#### C) Eliminar persona
Request a reloj:
```http
PUT /ISAPI/AccessControl/UserInfoDetail/Delete?format=json
```
```json
{
  "UserInfoDetail": {
    "mode": "byEmployeeNo",
    "EmployeeNoList": [
      { "employeeNo": "123" }
    ]
  }
}
```

Regla importante de documentacion:
1. Esta llamada inicia el proceso de borrado, no garantiza finalizacion inmediata.
2. Si necesitas confirmacion fuerte por reloj:
   - consultar `GET /ISAPI/AccessControl/UserInfoDetail/DeleteProcess?format=json`.
3. Segun la guia, borrar persona tambien borra credenciales vinculadas (card/fingerprint/face/iris).

### 14.4 Estrategia de orquestacion fan-out (ApiReloj)
Para cada endpoint:
1. Obtener `Residential` y lista de `Reloj` vinculados.
2. Resolver destino por reloj (`Residential.IpActual` + `Reloj.Puerto`).
3. Ejecutar llamada ISAPI por cada reloj (Digest Auth).
4. Acumular resultados por reloj en una respuesta agregada.

Respuesta agregada sugerida:
```json
{
  "residentialId": 1,
  "employeeNo": "123",
  "operation": "create",
  "totals": {
    "total": 2,
    "ok": 2,
    "failed": 0
  },
  "results": [
    {
      "relojId": 100001,
      "status": "ok",
      "httpStatus": 200
    }
  ]
}
```

### 14.5 Validaciones de negocio y compatibilidad
Pre-validaciones en ApiReloj:
1. `Residential` existe y tiene `IpActual`.
2. Hay al menos 1 reloj vinculado.
3. `employeeNo` cumple `1..32`.
4. `userType` dentro de `normal|visitor|blackList`.

Validaciones de compatibilidad por reloj (recomendadas):
1. Cachear por reloj `UserInfo/capabilities`.
2. Antes de operar, verificar `supportFunction`:
   - create: `setUp` (o `post` si algun reloj no soporta `setUp`).
   - modify: `put`.
3. Para delete, verificar `isSupportUserInfoDetailDelete=true`.

### 14.6 Decision final sobre SetUp vs Record
Recomendacion V1:
1. `CreatePerson` del backend -> usar `SetUp` siempre.
2. Dejar `Record` solo como opcion tecnica futura (modo estricto "fallar si ya existe").

Justificacion:
1. Menos friccion operativa en fan-out multi-reloj.
2. Evita errores por desalineacion de estado entre relojes de un mismo residencial.

### 14.7 Checklist operativo previo a implementar endpoints
1. Corregir en docs internas cualquier referencia a `userType=blacklist` o `administrators` para este modelo; usar la capability real (`blackList`).
2. Confirmar credenciales Digest por reloj en configuracion segura (no hardcode).
3. Definir politica de status HTTP de la API agregada:
   - `200` si todos ok.
   - `207` si parcial.
   - `4xx/5xx` si falla total.
4. Mantener logs por reloj: endpoint ISAPI, status HTTP, error payload.
