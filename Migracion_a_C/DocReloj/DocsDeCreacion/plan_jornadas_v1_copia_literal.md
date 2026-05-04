# Plan V1 (mezcla: explicación completa + clases ejemplo) — `Jornada` derivada de `AccessEvents`

## Resumen
Se agrega la feature `Jornada` para transformar eventos de acceso en jornadas laborales listas para consumo del backend.

Objetivo:
1. Una jornada agrupa la secuencia esperada: `CheckIn -> BreakIn -> BreakOut -> CheckOut`.
2. La creación/modificación de jornada no es por endpoint, sino automática al insertar `AccessEvents`.
3. Se exponen solo endpoints `GET` para consulta.
4. Se mantiene el estilo actual del repo: capas separadas, servicios síncronos, validaciones por excepción, `GlobalExceptionFilter`.

Decisiones cerradas:
1. Fechas en `DateTimeOffset` UTC.
2. `JornadaId` como `ULID` string.
3. Mapeo de tipos por `attendanceStatus`, configurable en `appsettings`.
4. Un solo break por jornada en V1.
5. Eventos huérfanos crean jornada `ERROR`.
6. En `CheckOut`, si break no está en `OK`, se fuerza `statusBreak = ERROR`.
7. Worker cada 5 minutos, timeout configurable (24h default).
8. Endpoint único de lectura: `GET /Jornadas` con filtros opcionales.
9. Si falta `CheckIn`, ese campo queda `null`.

## Reglas de negocio (máquina de estados)
1. Correlación de jornada abierta por `(employeeNumber, clockSn, statusCheck=INCOMPLETE)`.
2. `CheckIn` sin abierta: crea nueva jornada incompleta.
3. `CheckIn` con abierta: cierra la previa en error y abre una nueva.
4. `BreakIn` válido: setea `BreakInAt`, queda `statusBreak=INCOMPLETE`.
5. `BreakOut` válido: setea `BreakOutAt`, queda `statusBreak=OK`.
6. `CheckOut`: setea `EndAt`, cierra check como `OK` si hay `StartAt`; si no, `ERROR`.
7. `CheckOut` siempre revisa break: si no es `OK`, pasa a `ERROR`.
8. Evento huérfano (`BreakIn/BreakOut/CheckOut` sin abierta): crea jornada `ERROR`, con `StartAt=null`, guardando timestamp del evento en su campo correspondiente.
9. Worker marca `ERROR` jornadas abiertas con `UpdatedAt` vencido (> timeout).

## 1) Dominio

### `Dominio/Jornada.cs`
```csharp
namespace Dominio;

public class Jornada
{
    public string JornadaId { get; set; } = null!; // ULID
    public string EmployeeNumber { get; set; } = null!;
    public string ClockSn { get; set; } = null!;

    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? BreakInAt { get; set; }
    public DateTimeOffset? BreakOutAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }

    public string StatusCheck { get; set; } = JornadaStatuses.Incomplete;
    public string StatusBreak { get; set; } = JornadaStatuses.Incomplete;

    public DateTimeOffset UpdatedAt { get; set; }
}
```

### `Dominio/JornadaStatuses.cs`
```csharp
namespace Dominio;

public static class JornadaStatuses
{
    public const string Ok = "OK";
    public const string Incomplete = "INCOMPLETE";
    public const string Error = "ERROR";
}
```

## 2) DTOs y opciones (WebApi/Models)

### `Models/Dominio/JornadaDto.cs`
```csharp
namespace Models.Dominio;

public class JornadaDto
{
    public string JornadaId { get; set; } = null!;
    public string EmployeeNumber { get; set; } = null!;
    public string ClockSn { get; set; } = null!;

    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? BreakInAt { get; set; }
    public DateTimeOffset? BreakOutAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }

    public string StatusCheck { get; set; } = null!;
    public string StatusBreak { get; set; } = null!;
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### `Models/WebApi/JornadasQueryDto.cs`
```csharp
namespace Models.WebApi;

public class JornadasQueryDto
{
    public int? ResidentialId { get; set; }
    public string? ClockSn { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? StatusCheck { get; set; }
    public string? StatusBreak { get; set; }

    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public DateTimeOffset? UpdatedSinceUtc { get; set; }

    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
}
```

### `Models/WebApi/JornadaProcessingOptions.cs`
```csharp
namespace Models.WebApi;

public class JornadaProcessingOptions
{
    public const string SectionName = "JornadaProcessing";

    public int WorkerIntervalMinutes { get; set; } = 5;
    public int IncompleteTimeoutHours { get; set; } = 24;

    public JornadaAttendanceMapOptions AttendanceStatusMap { get; set; } = new();
}
```

### `Models/WebApi/JornadaAttendanceMapOptions.cs`
```csharp
namespace Models.WebApi;

public class JornadaAttendanceMapOptions
{
    public List<string> CheckIn { get; set; } = ["checkIn"];
    public List<string> BreakIn { get; set; } = ["breakIn"];
    public List<string> BreakOut { get; set; } = ["breakOut"];
    public List<string> CheckOut { get; set; } = ["checkOut"];
}
```

## 3) Repositorio (contrato)

### `IDataAcces/IJornadasRepository.cs`
```csharp
using Dominio;

namespace IDataAcces;

public interface IJornadasRepository
{
    Jornada Add(Jornada jornada);
    Jornada? GetById(string jornadaId);
    Jornada? GetOpenByEmployeeAndClock(string employeeNumber, string clockSn);

    List<Jornada> Search(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        DateTimeOffset? updatedSinceUtc = null,
        string? employeeNumber = null,
        string? clockSn = null,
        string? statusCheck = null,
        string? statusBreak = null,
        int limit = 100,
        int offset = 0);

    List<Jornada> GetOpenOlderThan(DateTimeOffset cutoffUtc, int limit = 1000);
    void Update(Jornada jornada);
}
```

## 4) DataAcces (config + implementación + contexto)

### `DataAcces/Configurations/JornadaConfig.cs`
- Tabla: `Jornadas`
- PK: `JornadaId`
- Índices:
1. `(EmployeeNumber, ClockSn, StatusCheck)`
2. `UpdatedAt`
3. `StartAt`
4. `ClockSn`

### `DataAcces/Repositories/JornadasRepository.cs`
- `GetOpenByEmployeeAndClock(...)`: busca jornada abierta.
- `Search(...)`: aplica filtros opcionales y paginación.
- `GetOpenOlderThan(...)`: base para worker de timeout.

### `DataAcces/Context/SqlContext.cs`
```csharp
public DbSet<Jornada> Jornadas => Set<Jornada>();
```

### Migración
1. Se agrega migración `AddJornadas`.
2. Crea tabla e índices definidos en config.

## 5) Contratos de service

### `IServices/IJornada/IJornadaService.cs`
```csharp
using Dominio;
using Models.Dominio;
using Models.WebApi;

namespace IServices.IJornada;

public interface IJornadaService
{
    void ProcesarEventoInsertado(AccessEvents accessEvent);
    int MarcarIncompletasVencidasComoError(DateTimeOffset nowUtc);
    List<JornadaDto> Buscar(JornadasQueryDto query);
}
```

### `IServices/IJornada/IJornadaMantenimientoService.cs`
```csharp
using Dominio;
using Models.Dominio;
using Models.WebApi;

namespace IServices.IJornada;

public interface IJornadaMantenimientoService
{
    void ProcesarEventoInsertado(AccessEvents accessEvent);
    int MarcarIncompletasVencidasComoError(DateTimeOffset nowUtc);
    List<JornadaDto> Buscar(JornadasQueryDto query);
}
```

### `IServices/IJornada/IJornadaValidationService.cs`
```csharp
using Models.WebApi;

namespace IServices.IJornada;

public interface IJornadaValidationService
{
    void ValidarBusqueda(JornadasQueryDto query);
    void ValidarStatus(string? statusCheck, string? statusBreak);
}
```

### `IServices/IJornada/IJornadaEntityService.cs`
```csharp
using Dominio;
using Models.Dominio;

namespace IServices.IJornada;

public interface IJornadaEntityService
{
    JornadaDto FromEntity(Jornada jornada);
}
```

## 6) Implementaciones de service

### `Service/JornadaServicess/JornadaEntityService.cs`
- Mapeo `Jornada -> JornadaDto`.

### `Service/JornadaServicess/JornadaValidationService.cs`
- Reglas:
1. `fromUtc` y `toUtc` juntos.
2. `fromUtc <= toUtc`.
3. `limit > 0`, `offset >= 0`.
4. `statusCheck` y `statusBreak` válidos (`OK/INCOMPLETE/ERROR`).

### `Service/JornadaServicess/JornadaMantenimientoService.cs`
Núcleo de lógica:
1. Clasifica evento por `attendanceStatus` usando `JornadaProcessingOptions`.
2. Procesa transiciones `CheckIn/BreakIn/BreakOut/CheckOut`.
3. Crea jornada huérfana `ERROR` cuando corresponde.
4. Busca jornadas por filtros opcionales.
5. Soporta filtro `residentialId` resolviendo `clockSn` desde relojes del residencial.
6. `MarcarIncompletasVencidasComoError(...)` para worker.

### `Service/JornadaServicess/JornadaService.cs`
- Orquesta validación y mantenimiento.

## 7) Worker periódico

### `WebApplication1/Workers/JornadaStatusWorker.cs`
1. Intervalo configurable (`WorkerIntervalMinutes`).
2. Ejecuta `MarcarIncompletasVencidasComoError(DateTimeOffset.UtcNow)`.
3. Loguea cantidad de jornadas marcadas.

## 8) Controller de lectura

### `WebApplication1/Controllers/JornadasController.cs`
```csharp
[HttpGet]
public ActionResult<List<JornadaDto>> Get([FromQuery] JornadasQueryDto? query)
{
    return Ok(_jornadaService.Buscar(query ?? new JornadasQueryDto()));
}
```

## 9) Integración con AccessEvents
Punto: `Service/AccesEventsServicess/AccesEventMantentimientoService.cs`.

Regla:
1. Solo procesar jornada cuando el evento fue insertado (no duplicado).

Snippet:
```csharp
var inserted = _accessEventsRepository.AddIfNotExists(accessEvent);

if (inserted)
{
    _jornadaService.ProcesarEventoInsertado(accessEvent);
}
```

## 10) Program.cs + appsettings

### `Program.cs`
1. Registra `IJornadasRepository`.
2. Registra servicios de `Jornada`.
3. Configura opciones `JornadaProcessing`.
4. Registra `JornadaStatusWorker` como hosted service.

### `appsettings.json`
```json
"JornadaProcessing": {
  "WorkerIntervalMinutes": 5,
  "IncompleteTimeoutHours": 24,
  "AttendanceStatusMap": {
    "CheckIn": [ "checkIn" ],
    "BreakIn": [ "breakIn" ],
    "BreakOut": [ "breakOut" ],
    "CheckOut": [ "checkOut" ]
  }
}
```

## 11) API pública de Jornadas (V1)

### Endpoint
`GET /Jornadas`

### Filtros opcionales
1. `residentialId`
2. `clockSn`
3. `employeeNumber`
4. `statusCheck`
5. `statusBreak`
6. `fromUtc`
7. `toUtc`
8. `updatedSinceUtc`
9. `limit` (default 100)
10. `offset` (default 0)

### Reglas de filtro
1. `fromUtc` y `toUtc` deben venir juntos.
2. `fromUtc <= toUtc`.
3. `limit > 0`.
4. `offset >= 0`.
5. `residentialId` filtra por `clockSn` de relojes del residencial.
6. `residentialId + clockSn` no perteneciente => `200 []`.
7. Orden por `UpdatedAt DESC`, luego `StartAt DESC`.

### Códigos esperados
1. `200` OK (incluye lista vacía).
2. `400` argumentos inválidos.
3. `404` residential inexistente.
4. `422` regla de negocio.
5. `500` error inesperado.
6. `409` reservado para conflicto de negocio.

## 12) Casos de prueba (funcionales)

### Transiciones
1. `CheckIn` crea jornada incompleta.
2. `BreakIn -> BreakOut` deja break en `OK`.
3. `CheckOut` con break ok cierra check en `OK`.
4. `CheckOut` con break incompleto fuerza break `ERROR`.
5. Segundo `CheckIn` cierra previa en error y abre nueva.
6. Huérfanos generan jornada `ERROR` con faltantes en `null`.

### Worker
1. Jornada abierta >24h pasa a `ERROR`.
2. Worker no inventa campos faltantes.
3. `UpdatedAt` se actualiza al marcar error.

### API
1. `GET /Jornadas` sin filtros.
2. `fromUtc` sin `toUtc` => `400`.
3. `fromUtc > toUtc` => `400`.
4. `residentialId` inexistente => `404`.
5. `residentialId + clockSn` inválido => `200 []`.
6. `updatedSinceUtc` retorna incremental.

## 13) Documentación a generar

### Archivo 1 (técnico interno)
`Migracion_a_C/DocReloj/plan_jornadas_v1.md`

### Archivo 2 (uso API)
`Migracion_a_C/DocReloj/api_jornadas_v1.md`

### Archivo 3 (copy literal)
`Migracion_a_C/DocReloj/plan_jornadas_v1_copia_literal.md`
- Snapshot 1:1 del plan técnico para compartir internamente.

## Supuestos y defaults explícitos
1. BD inicial vacía para esta feature.
2. Procesamiento de jornada solo en insert real de evento.
3. UTC en toda fecha de jornada.
4. Solo un break por jornada en V1.
5. Se mantiene naming/estilo de capas del repo.
