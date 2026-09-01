# Documentación de ApiReloj

**Estado:** vigente al 1 de septiembre de 2026.

Este directorio separa documentación contractual, operación y antecedentes. Para integrar otro sistema con ApiReloj se debe usar solamente el paquete `DocParaImplementadores`.

## Fuente contractual

`DocParaImplementadores/` contiene el contrato HTTP y el comportamiento observable vigente:

1. `Paquete_documentacion_integracion_agente_externo.md`: índice y orden de lectura.
2. `guia_funcionamiento_general_repo_v1.md`: propósito, arquitectura y flujos.
3. `api_completa_repo_v1.md`: todos los endpoints, seguridad, cuerpos y respuestas.
4. `api_access_events_v1.md`: consulta detallada de eventos.
5. `api_jornadas_v1.md`: contrato de jornadas y reconstrucción.
6. `procesamiento_jornadas_concurrente.md`: consistencia, orden y concurrencia.
7. `api_poll_backfill_v1.md`: polling ISAPI y administración.
8. `explicacion_ids_string_operativa.md`: IDs, migraciones, heartbeat y rutas de reloj.
9. `seguridad_endpoints.md`: copia autocontenida de las reglas de seguridad.
10. `compatibilidad_quievo_repo_readiness.md`: matriz de compatibilidad con QUIEVO y decisiones de integración.
11. `despliegue_dokploy_traefik.md`: variables, red confiable, migraciones, smoke y rollback.

## Operación del repositorio

- `seguridad_endpoints.md`: configuración de políticas y secretos.
- `guia_instalacion_windows10_api_reloj.md`: ejecución local y pruebas.
- `aceptacion_manual_isapi.md`: pruebas que requieren un reloj Hikvision real y evidencias de aceptación.
- `DocHeartBeat/`: contrato e instalación del emisor Windows de heartbeat, que vive fuera de este repositorio.
- `isapi_summary.md`: referencia resumida de rutas ISAPI Hikvision usadas por ApiReloj.

## Archivo histórico

- `DocsDeCreacion/`: planes y decisiones usadas para construir versiones anteriores.

Esos directorios no son contrato de integración. Si contradicen `DocParaImplementadores`, prevalece el paquete contractual y, finalmente, el código actual.

## Principios vigentes

- El heartbeat conserva su body JSON y firma HMAC actuales.
- El push conserva las rutas y formatos ISAPI.
- El backend debe enviar `X-Api-Key` y originar la conexión desde la IP fija configurada.
- Los eventos se conservan antes de derivar jornadas.
- Las jornadas se reconstruyen de manera asíncrona, ordenada y concurrente por empleado + residencial.
- La aplicación ejecuta migraciones EF pendientes durante el arranque.
- Los eventos se filtran por el `ResidentialId` persistido al ingerirlos, no por la relación actual de relojes.
- Los históricos sin pertenencia demostrable permanecen en la cuarentena `__legacy__`.
- Poll persiste el snapshot y el progreso por reloj y recupera runs interrumpidos al arrancar.
- En Dokploy sólo se confían forwarded headers provenientes de proxies o redes configurados explícitamente.
- ApiReloj debe desplegarse con exactamente una réplica.
