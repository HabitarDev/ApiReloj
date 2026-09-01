# Paquete de documentación para integración externa

**Carpeta contractual:** `DocReloj/DocParaImplementadores/`
**Última revisión contra código:** 1 de septiembre de 2026.

Los once archivos de este directorio forman un paquete autocontenido para desarrolladores que no tienen acceso al repositorio. No es necesario consultar `DocsDeCreacion` ni documentos de planificación.

## Contenido y función

| Orden | Archivo | Función |
|---:|---|---|
| 1 | `guia_funcionamiento_general_repo_v1.md` | Explica para qué sirve ApiReloj y cómo se conectan heartbeat, push, poll, eventos y jornadas. |
| 2 | `seguridad_endpoints.md` | Define quién puede llamar a cada endpoint y qué credenciales debe enviar. |
| 3 | `api_completa_repo_v1.md` | Contrato HTTP completo: rutas, headers, cuerpos, respuestas y errores. |
| 4 | `api_access_events_v1.md` | Detalle de filtros y representación de eventos. |
| 5 | `api_jornadas_v1.md` | Detalle de jornadas, revisiones, tombstones y reconstrucción. |
| 6 | `procesamiento_jornadas_concurrente.md` | Garantías de orden, transacción, reintentos y concurrencia. |
| 7 | `api_poll_backfill_v1.md` | Poll ISAPI, cursores y endpoints administrativos. |
| 8 | `explicacion_ids_string_operativa.md` | IDs string, migraciones, HMAC y configuración de rutas. |
| 9 | `compatibilidad_quievo_repo_readiness.md` | Matriz contractual contra QUIEVO y decisiones finales de compatibilidad. |
| 10 | `despliegue_dokploy_traefik.md` | Despliegue seguro, variables, migraciones, smoke y rollback. |
| 11 | `Paquete_documentacion_integracion_agente_externo.md` | Este índice. |

## Cambios que afectan al backend

Los bodies principales del backend no cambiaron. Sí cambiaron los requisitos de acceso y algunos modelos de respuesta:

- Todos los endpoints destinados al backend requieren `X-Api-Key` y la IP fija autorizada.
- `GET/POST /Device` nunca devuelven `_secretKey`; la clave es write-only.
- `GET /Jornadas` incluye estado de proyección, revisión y tombstones.
- Existen endpoints administrativos para reconstruir jornadas e inspeccionar la cola.
- El heartbeat mantiene exactamente su body original; no usa headers nuevos.
- El push conserva ruta, JSON, XML y multipart de ISAPI.
- `GET /AccessEvents` aplica aislamiento por el `ResidentialId` persistido en cada evento.
- Un poll `running` devuelve `finishedAtUtc: null`; un resultado terminal siempre tiene fecha y ningún reloj pendiente.
- Inexistentes responden `404`, duplicados `409` y argumentos inválidos `400` mediante `ProblemDetails`.
- No se agregaron callbacks ni tráfico de ApiReloj hacia QUIEVO.

## Regla de autoridad

Este paquete describe el contrato vigente. Los documentos `plan_*` y `DocsDeCreacion` son antecedentes históricos y no deben utilizarse para implementar clientes.
