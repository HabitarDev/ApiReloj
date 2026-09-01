# IDs string: base de datos, heartbeat y relojes

Contrato vigente al 15 de julio de 2026.

## Identificadores

`Residential.IdResidential`, `Device.DeviceId` y `Reloj.IdReloj` son strings de hasta 128 caracteres. Esto permite usar identificadores generados por HABITAR, incluidos `cuid`, sin conversión numérica.

Consecuencias para clientes:

- enviarlos como JSON string;
- no parsearlos como entero;
- conservar mayúsculas, minúsculas y caracteres exactamente;
- codificar `relojId` como segmento de URL cuando corresponda.

## Migraciones EF

La migración `20260429005313_MaestrosIdsString` convirtió las claves y relaciones maestras a `varchar(128)`. Las migraciones posteriores agregan la cola de jornadas y el timestamp antireplay.

ApiReloj ejecuta automáticamente todas las migraciones pendientes mediante `Database.Migrate()` al arrancar. Si PostgreSQL no está disponible o una migración falla, la aplicación no inicia.

Para operación manual o diagnóstico también se puede ejecutar desde `Migracion_a_C/WebApplication1`:

```powershell
dotnet ef database update --project .\DataAcces\DataAcces.csproj --startup-project .\WebApplication1\WebApplication1.csproj
```

No se deben editar tablas manualmente para reemplazar migraciones.

## Heartbeat

Endpoint:

```http
POST /Residential/heartbeat
Content-Type: application/json
```

```json
{
  "deviceId": "cm02abcdef1234567890xyz",
  "residentialId": "cm01abcdef1234567890xyz",
  "timeStamp": 1784067600,
  "signature": "a3f5..."
}
```

Los IDs se firman como texto, sin conversiones:

```text
canonical = timeStamp + "|" + deviceId + "|" + residentialId
signature = HEX(HMAC_SHA256(UTF8(secretKey), UTF8(canonical)))
```

La comparación de identidad es exacta. El device debe pertenecer al residencial. El timestamp debe ser reciente y mayor al último aceptado para modificar datos.

Resultados:

- `204`: aceptado o replay válido sin cambios;
- `401`: contenido, asociación, timestamp o firma inválidos;
- `429`: rate limit.

El body del emisor histórico no necesita cambios.

## Alta de Device y secreto

```http
POST /Device
X-Api-Key: <secreto-backend>
Content-Type: application/json
```

```json
{
  "_deviceId": "cm02abcdef1234567890xyz",
  "_secretKey": "secreto-hmac",
  "_lastSeen": null,
  "_residentialId": "cm01abcdef1234567890xyz"
}
```

`_secretKey` es write-only. La respuesta contiene ID, `lastSeen` y residencial, pero nunca el secreto. El backend debe custodiarlo o entregarlo al emisor durante el aprovisionamiento.

## Ruta de push

```text
https://<host-api>/AccessEvents/push/<relojId-url-encoded>
```

`relojId` identifica la configuración interna y no es necesariamente igual a `DeviceSn`. El reloj se configura para enviar a esa URL; ApiReloj obtiene desde BD su residencial, IP permitida y DeviceSn esperado.

Si el ID contiene caracteres reservados, el configurador debe aplicar percent-encoding al segmento. Se recomiendan IDs URL-safe.

## Relaciones necesarias

Orden recomendado de aprovisionamiento:

1. Crear `Residential`.
2. Crear `Device` asociado al residencial.
3. Crear `Reloj` asociado al residencial.
4. Configurar `DeviceSn` con `PUT /Reloj`.
5. Instalar el emisor heartbeat con los IDs y secreto exactos.
6. Configurar la URL de push en el reloj.

Todos los pasos administrativos requieren autenticación de backend.
