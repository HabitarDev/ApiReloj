# API Jornadas V1

> **ARCHIVO HISTORICO.** No usar como contrato. La documentación vigente está en `../DocParaImplementadores/api_jornadas_v1.md` y `../DocParaImplementadores/procesamiento_jornadas_concurrente.md`.

La documentación operativa vigente está en:

`DocReloj/DocParaImplementadores/api_jornadas_v1.md`

La implementación actual usa una proyección persistente y concurrente por `employeeNumber + residentialId`; reemplaza la derivación incremental por reloj que describía el plan original.
