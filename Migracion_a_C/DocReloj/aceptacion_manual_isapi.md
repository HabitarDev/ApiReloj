# Aceptación manual con reloj Hikvision ISAPI

**Revisión:** 15 de julio de 2026.

## Propósito

Este documento identifica las verificaciones que requieren hardware Hikvision,
firmware real y conectividad del residencial. No sustituyen las 22 pruebas del
repositorio ni el smoke HTTP; completan la certificación antes del go-live.

La aceptación debe hacerse en un residencial de prueba o en una ventana
controlada. No se deben copiar contraseñas, API keys ni secretos heartbeat en
capturas, logs o tickets.

## Precondiciones

- [ ] ApiReloj está desplegada con HTTPS y CI verde.
- [ ] PostgreSQL tiene respaldo y migraciones al día.
- [ ] El Residential, Device heartbeat y al menos dos Relojes están aprovisionados.
- [ ] Cada Reloj tiene `DeviceSn` igual al serial que envía ISAPI.
- [ ] Las credenciales Digest se cargaron fuera del repositorio.
- [ ] La hora, zona horaria y NTP de API, emisor heartbeat y relojes están alineados.
- [ ] Se definieron empleados de prueba sin afectar liquidaciones reales.

Para cada caso guardar fecha UTC, modelo, firmware, reloj, endpoint probado,
resultado esperado/observado y una referencia a logs sanitizados.

## A. Conectividad y autenticación ISAPI

- [ ] Consultar una ruta ISAPI de identificación/estado con Digest y confirmar `2xx`.
- [ ] Probar credenciales inválidas y confirmar que ApiReloj registra el fallo sin exponerlas.
- [ ] Verificar timeout, DNS/IP y puerto configurado para cada reloj.
- [ ] Reiniciar un reloj y confirmar recuperación del polling sin intervención manual.

## B. Heartbeat público

- [ ] Enviar el body actual `{deviceId,residentialId,timeStamp,signature}` sin headers nuevos.
- [ ] Confirmar actualización de `IpActual` y `LastSeen` desde una red externa real.
- [ ] Repetir exactamente el mismo heartbeat y confirmar ausencia de una segunda mutación.
- [ ] Enviar timestamp vencido y firma incorrecta; confirmar rechazo y ausencia de cambios.
- [ ] Confirmar que el resto de endpoints no queda público desde esa misma red.

## C. Push de eventos del reloj

- [ ] Configurar en el reloj `/AccessEvents/push/{relojId}` como destino de notificaciones.
- [ ] Recibir un evento JSON `AccessControllerEvent` real.
- [ ] Si el firmware lo usa, recibir XML real y multipart con imagen.
- [ ] Confirmar que `DeviceID` coincide con el `DeviceSn` configurado para la ruta.
- [ ] Intentar una ruta de otro reloj o una IP no registrada y confirmar rechazo.
- [ ] Reenviar una notificación idéntica y confirmar resultado `duplicate` sin duplicar datos.
- [ ] Enviar un evento no relevante y confirmar `ignored` sin crear jornada.

## D. Poll/backfill ISAPI

- [ ] Ejecutar poll manual y validar la ruta ISAPI de búsqueda ACS usada por el firmware.
- [ ] Obtener más de una página y confirmar avance de cursor sin pérdidas ni duplicados.
- [ ] Cortar la red durante una página y confirmar reintento/reanudación auditable.
- [ ] Ingresar primero eventos recientes y después un evento histórico; confirmar reconstrucción ordenada.
- [ ] Confirmar que push y poll del mismo evento conservan una sola fila.

## E. Usuarios en relojes

- [ ] Crear un empleado en todos los relojes seleccionados.
- [ ] Modificarlo y verificar el cambio en cada dispositivo.
- [ ] Eliminarlo y verificar su ausencia en cada dispositivo.
- [ ] Provocar un fallo parcial en un reloj y confirmar que la respuesta identifica cuál falló.
- [ ] Validar caracteres, longitudes e identificadores admitidos por el firmware instalado.

## F. Jornadas acordadas

- [ ] Check-in y check-out en el mismo reloj producen una jornada `OK`.
- [ ] Check-in en un reloj y check-out en otro del mismo residencial producen una sola jornada.
- [ ] Un turno nocturno que cruza medianoche permanece abierto mientras no supere 24 horas.
- [ ] Un evento posterior a las 24 horas no se asigna a la jornada vencida.
- [ ] Un único descanso completo queda registrado.
- [ ] Un segundo descanso genera la advertencia/regla definida sin crear datos incoherentes.
- [ ] Una doble marcación conserva la primera y genera advertencia.
- [ ] Un evento histórico cambia la revisión y reconstruye sólo empleado + residencial afectados.
- [ ] La cola llega a `READY`; simular un fallo y confirmar reintentos, `LastError` y recuperación.

## G. Aceptación y evidencia

- [ ] Las respuestas observadas coinciden con `DocParaImplementadores`.
- [ ] No aparecen secretos en responses, logs ni evidencias.
- [ ] No hubo pérdida ni duplicación de eventos.
- [ ] Se registraron modelo/firmware y cualquier diferencia específica del dispositivo.
- [ ] Responsable técnico de ApiReloj aprobó el resultado.
- [ ] Responsable del residencial/dispositivos aprobó conectividad y operación.

La aceptación queda **pendiente** mientras algún punto aplicable no tenga evidencia
o una excepción explícitamente aprobada. Las diferencias de firmware deben
documentarse antes de modificar el contrato general.
