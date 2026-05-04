# Paquete de documentación — agente externo (sin acceso al repo ApiReloj)

Este archivo describe **qué incluir** al pasar contexto a un agente de IA o equipo que implementará el cliente (**p. ej. HABITAR**) sin clonar este repositorio.

**Última revisión contra código:** mayo 2026 (contrato HTTP, filtros de error, DTOs y migración `MaestrosIdsString`).

---

## 1. Conjunto mínimo recomendado (autocontenido práctico)

| Archivo | Ubicación bajo `DocReloj/` | Rol |
|--------|---------------------------|-----|
| Guía funcional | `guia_funcionamiento_general_repo_v1.md` | Arquitectura híbrida, heartbeat vs push vs poll, proxy usuarios, limitaciones V1. |
| Contrato HTTP único | `DocsDeCreacion/api_completa_repo_v1.md` | Endpoints, ejemplos JSON, queries, códigos de error, flujos de integración. |
| IDs string / BD / heartbeat / push URL | `explicacion_ids_string_operativa.md` | Migración EF, HMAC, `relojId` string en ruta de push. |
| Lectura eventos | `DocsDeCreacion/api_access_events_v1.md` | Reglas finas de `GET /AccessEvents`, errores, envelope `_raw`. |
| Lectura jornadas | `DocsDeCreacion/api_jornadas_v1.md` | `GET /Jornadas`, filtros, estados. |
| Admin poll / backfill | `DocsDeCreacion/api_poll_backfill_v1.md` | Cursor, ventana configurable, endpoints `/admin/poll/*`, idempotencia. |

Con estos **seis** documentos un implementador puede:

- Provisionar maestros con IDs **string** alineados a Prisma/`cuid()`.
- Entender por qué el heartbeat actualiza `IpActual` y cómo afecta al **push** y al poll.
- Consumir lecturas (`AccessEvents`, `Jornadas`) y operar backfill manual.
- Enviar comandos de usuarios vía `UsersControllers` sabiendo el **fan-out** por residencial.

---

## 2. Qué sigue siendo opcional u externo

- **`infra_hibrida.md`**: útil si el agente debe razonar sobre despliegue (red, relojes, workers); no es estrictamente necesario solo para llamadas HTTP desde el back.
- **`DocHeartBeat/*`**: necesario si también implementan el **servicio Windows** emisor de heartbeat; si solo consumen ApiReloj desde Node, basta el contrato en `api_completa` + `explicacion_ids`.
- **Autenticación global de la API**: en la configuración típica del repo **no** hay JWT/API key documentados en estos archivos para todos los endpoints; si en tu entorno hay **gateway** o reglas adicionales, documentarlas **fuera** de este paquete o en variables de entorno del despliegue.
- **OpenAPI**: `Program.cs` expone OpenAPI en desarrollo; un agente sin repo no lo verá salvo que compartas el artefacto o la URL.

---

## 3. Coherencia verificada (mayo 2026)

- IDs de **Residential / Device / Reloj** en contratos y migración alineados a **string** (`varchar(128)`).
- `api_poll_backfill_v1.md`: ejemplos de `relojId` en JSON actualizados a **string** (antes figuraban enteros obsoletos).
- Reglas de ventana de poll: documentadas como **`BackfillPolling:WindowMinutes`** (no solo “30” fijo).
- **Errores HTTP:** se corrigió en código un caso donde mensajes con `inexistente` podían clasificarse mal como conflicto por la subcadena `existente` (`GlobalExceptionFilter`), de modo que `Residential inexistente` en lecturas coincide con **404** documentado.

---

## 4. Verificación interna del repo

Para mantenimiento en ApiReloj, el estado frente al pedido HABITAR y compatibilidad está resumido en:

`ComuicacionConOtroAgente/Verificacion_cumplimiento_SolicitudDeModificacion_y_compatibilidad.md`

(no es obligatorio enviarlo al agente del back si ya entregás el paquete de seis archivos anteriores).
