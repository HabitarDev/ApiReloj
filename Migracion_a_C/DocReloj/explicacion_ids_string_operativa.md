# IDs string en maestros — Qué implica en BD, heartbeat y relojes

Este documento amplía en detalle tres temas que suelen generar dudas después del cambio a identificadores **string** (compatibles con `cuid()` / Prisma en HABITAR):

1. La **migración Entity Framework + PostgreSQL**: por qué era necesaria y qué garantiza.
2. El **cliente de heartbeat** (servicio Windows documentado en `DocHeartBeat/`): cómo debe serializar JSON y cómo debe construir la cadena del HMAC.
3. Las **URLs de push** en los relojes Hikvision: por qué el segmento `/push/{relojId}` ya no puede ser un entero “pequeño” y qué hay que reconfigurar en campo.

Los ejemplos usan IDs ficticios tipo `cm01ejemplo...`; en tu entorno serán los **mismos strings** que genere Prisma en HABITAR.

---

## 1. Migración EF + PostgreSQL

### 1.1 Qué problema resolvía el código sin migración

En C#, las entidades `Residential`, `Device` y `Reloj` ya usan propiedades **`string`** para PK y FK. Entity Framework traduce eso a columnas en PostgreSQL. Si la base **siguiera** con columnas `integer` y la aplicación intentara leer/escribir **strings**, ocurriría al menos uno de estos síntomas:

- Fallos al **materializar** entidades (lector espera texto y encuentra números).
- Fallos al **guardar** (tipo incompatible en INSERT/UPDATE).
- Errores en **JOINs** implícitos entre tablas si una FK quedara en tipo distinto a la PK referenciada.

Por tanto, **la migración no es “opcional para documentación”**: es lo que alinea el **esquema físico** de PostgreSQL con el **modelo** que usa la aplicación.

### 1.2 Qué hace la migración `MaestrosIdsString`

En el proyecto `DataAcces/Migrations/` existe la migración **`MaestrosIdsString`** (`20260429005313_MaestrosIdsString.cs`), generada con:

```bash
dotnet ef migrations add MaestrosIdsString --project Migracion_a_C/WebApplication1/DataAcces/DataAcces.csproj --startup-project Migracion_a_C/WebApplication1/WebApplication1/WebApplication1.csproj --context SqlContext
```

En el método `Up`:

- Cambia el tipo de estas columnas de **`integer`** a **`character varying(128)`** (con `maxLength: 128`):
  - `Residentials.IdResidential`
  - `Devices.DeviceId`, `Devices.ResidentialId`
  - `Relojes.IdReloj`, `Relojes.ResidentialId`

EF Core emitió la advertencia de “posible pérdida de datos” porque, en general, **convertir int → varchar en una tabla ya poblada** puede requerir reglas de conversión (por ejemplo, prefijo `"legacy-" + id`). En tu caso indicaste que **aún no hay despliegue con datos reales**, así que suele aplicarse sobre bases **vacías** o se recrea el esquema desde cero.

### 1.3 Cómo aplicar la migración en un entorno

Desde la máquina donde corre la API (con cadena de conexión válida en `appsettings`):

```bash
dotnet ef database update --project Migracion_a_C/WebApplication1/DataAcces/DataAcces.csproj --startup-project Migracion_a_C/WebApplication1/WebApplication1/WebApplication1.csproj --context SqlContext
```

(O equivalente con rutas absolutas.) Esto ejecuta **todas** las migraciones pendientes, incluida `MaestrosIdsString`.

**Nota:** El comando solo aplicará cambios si PostgreSQL está **levantado** y la cadena de conexión (`ConnectionStrings:Default` en `appsettings`) es válida. Si la base no existe aún, créala antes o deja que tu proceso de despliegue la cree; EF actualiza el esquema dentro de esa base.

### 1.4 Si en el futuro ya hubiera datos enteros en producción

Ese escenario **no** es el tuyo ahora; solo como referencia: un `ALTER COLUMN ... TYPE varchar` directo puede fallar o producir conversiones implícitas indeseadas. Ahí se usa estrategia documentada en el plan de integración (columna nueva, backfill, cutover). No requiere cambiar código de dominio adicional si ya trabajáis solo con strings en la API.

---

## 2. Cliente de heartbeat (JSON + HMAC)

### 2.1 Qué contrato cumple la API

El endpoint real es:

`POST /Residential/heartbeat`

El cuerpo JSON se deserializa a `HeartBeatDto` con:

- `deviceId`: **string** (antes podía documentarse como número; ahora es el mismo identificador que la BD de ApiReloj / HABITAR).
- `residentialId`: **string**.
- `timeStamp`: **long** (epoch **segundos** UTC).
- `signature`: hex de **HMAC-SHA256**.

La firma **no** se calcula sobre el JSON completo. El servidor reconstruye esta cadena UTF-8:

```text
{timeStamp}|{deviceId}|{residentialId}
```

Es decir: tres partes unidas por el carácter **`|`** (pipe), **sin espacios extras**, donde:

- `timeStamp` es la representación decimal habitual del entero largo (ej. `1740830400`).
- `deviceId` y `residentialId` son los **mismos strings** que van en el JSON (misma casing; comparación de firma es sobre bytes exactos del string).

Luego se calcula `HMACSHA256(secretKey, utf8_bytes(cadena))` y se compara con `signature` en hex.

### 2.2 Por qué importa que JSON use strings

Si el emisor antiguo mandaba:

```json
"deviceId": 100,
"residentialId": 10
```

el serializador JSON **numérico** puede producir una firma coherente solo si el lado servidor interpretaba también números. Ahora el modelo C# usa **string**: los valores deben ir entre comillas en JSON:

```json
{
  "deviceId": "cm02abcdef1234567890xyz",
  "residentialId": "cm01abcdef1234567890xyz",
  "timeStamp": 1740830400,
  "signature": "..."
}
```

La cadena firmada pasa a ser por ejemplo:

```text
1740830400|cm02abcdef1234567890xyz|cm01abcdef1234567890xyz
```

Si el servicio Windows (`DeviceHeartbeatService`) sigue armando la firma concatenando **enteros**, la firma **no coincidirá** con lo que calcula ApiReloj (que concatena strings). Hay que actualizar el **código del servicio** (si existe en otro repositorio) para:

1. Leer `DeviceId` y `ResidentialId` como **strings** en configuración (o convertir explícitamente a string antes de firmar).
2. Serializar el JSON con esos mismos strings.
3. Calcular HMAC sobre exactamente ` $"{timestamp}|{deviceId}|{residentialId}" ` con esos valores string.

### 2.3 Documentación del servicio en este repo

Las guías bajo `DocReloj/DocHeartBeat/` describen el comportamiento del binario; deben mantenerse **alineadas** con la URL correcta (`/Residential/heartbeat`) y con ejemplos JSON en string.

### 2.4 Código del ejecutable DeviceHeartbeatService (fuera de ApiReloj)

Este repositorio solo contiene **documentación** del servicio Windows; el proyecto compilable puede vivir en otro árbol de código. Para que el sistema sea coherente end-to-end:

1. **Modelo de configuración**: si `appsettings.json` deserializa `DeviceId` y `ResidentialId` como `int`, hay que cambiarlo a **`string`** (o serializar a string antes de firmar y antes de armar el JSON).
2. **Generación de firma**: la línea que concatena `timestamp`, `deviceId` y `residentialId` debe usar los **mismos strings** que se envían en el body (sin pasar por formato numérico que elimine ceros a la izquierda en IDs no numéricos — con cuids no aplica, pero sí evita confusiones).
3. **Serialización JSON**: el body debe emitir `deviceId` y `residentialId` como **strings JSON** (`"..."`), no como números, para coincidir con lo que deserializa ApiReloj (`HeartBeatDto`).
4. **Nombres de propiedad**: ApiReloj acepta el casing habitual de ASP.NET Core (**camelCase** en JSON: `deviceId`, `residentialId`, `timeStamp`, `signature`). Si el cliente envía PascalCase, suele funcionar por configuración tolerante, pero lo recomendado es camelCase como en los ejemplos actualizados.

Si el ejecutable no se actualiza, tendrás **firmas inválidas** o **400/422** por binding, aunque la API y la BD estén correctas.

---

## 3. Relojes y URL de push `/AccessEvents/push/{relojId}`

### 3.1 Rol del `relojId` en la ruta

El reloj Hikvision envía el **body** del evento (XML/JSON/multipart) por HTTP hacia ApiReloj. La ruta incluye un segmento:

`/AccessEvents/push/{relojId}`

Ese valor debe coincidir **exactamente** con el campo **`Reloj.IdReloj`** almacenado en PostgreSQL para ese equipo.

Antes, si los IDs eran enteros “secuenciales”, era natural configurar en el reloj algo como `/AccessEvents/push/10`.

Ahora **`IdReloj` es un string opaco** (p.ej. un `cuid` generado por HABITAR al alta del reloj). La URL deja de ser un número corto y pasa a ser del estilo:

```text
https://api-ejemplo.com/AccessEvents/push/cm03relojabc1234567890xyz
```

### 3.2 Por qué hay que “reconfigurar” en el reloj

Los terminales guardan el **host**, **ruta** y a veces **puerto** en la configuración de notificación HTTP (`httpHosts`, `Request_URI`, etc.). Si quedó guardada una ruta con el ID numérico viejo:

- Las peticiones llegarían a `/push/10` mientras la BD tiene `IdReloj = "cm03..."` → el filtro de autorización **no** encuentra el reloj (404 / 401 según caso).

No es un bug de la API: es **desalineación de configuración** entre lo grabado en el reloj y el maestro en BD.

### 3.3 Relación con la seguridad del push

La API valida (entre otras cosas) que la IP del reloj coincida con `Residential.IpActual` (actualizada por heartbeat). El `relojId` en ruta sirve para cargar el `Reloj`, su `ResidentialId` y comprobar pertenencia y `DeviceSn`. Todo eso sigue igual en lógica; solo cambia el **tipo/formato** del identificador.

### 3.4 Codificación en URL

Los `cuid` suelen ser seguros para path segment sin encoding adicional. Si en el futuro usarais caracteres especiales, habría que **URL-encode** el segmento al configurar el reloj; hoy no es el caso típico de Prisma.

---

## 4. Resumen rápido

| Tema | Idea clave |
|------|------------|
| **Migración BD** | Sin columnas `varchar` para PK/FK, PostgreSQL y EF no coinciden con el modelo string → errores en runtime. |
| **Heartbeat** | Misma cadena para JSON y HMAC: `{epoch}|{deviceId string}|{residentialId string}`; actualizar servicio emisor si antes firmaba enteros. |
| **Push URL** | El último segmento de la URL debe ser **exactamente** el `IdReloj` string guardado en BD. |

---

## 5. Referencias en este repo

- Contrato HTTP actualizado: `api_completa_repo_v1.md`
- Flujo general: `guia_funcionamiento_general_repo_v1.md`
- Infra híbrida (push/poll): `infra_hibrida.md`
- Migración EF generada: `Migracion_a_C/WebApplication1/DataAcces/Migrations/20260429005313_MaestrosIdsString.cs`
- Heartbeat (servicio Windows): `DocHeartBeat/README.md`, `DocHeartBeat/instalacion.md`
