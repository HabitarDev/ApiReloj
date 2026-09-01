# Procesamiento concurrente de jornadas

Este documento describe las garantías internas que afectan a consumidores y operación.

## Flujo transaccional

1. Push o poll normaliza un evento.
2. En una única transacción PostgreSQL se intenta insertar el evento.
3. Si fue nuevo, se actualiza el cursor correspondiente y se hace upsert de `JornadaProjectionState` para empleado + residencial.
4. Si ya existía, no se crea trabajo duplicado por esa ingesta.
5. `JornadaProcessingWorker` reclama una clave disponible.
6. Carga todos los eventos de esa clave y los ordena cronológicamente.
7. Reconstruye, reconcilia revisiones y tombstones, y marca la proyección `READY` en una transacción.

Así se evita el fallo original donde un evento podía quedar persistido sin que la jornada se actualizara y un reintento lo descartara como duplicado.

## Orden determinista

Los eventos se ordenan por:

1. `EventTimeUtc` ascendente.
2. `SerialNumber` ascendente.
3. `DeviceSn` ascendente.

Un backfill antiguo puede llegar después de eventos recientes: la reconstrucción completa produce el mismo resultado que si todos hubieran llegado en orden.

## Identidad y concurrencia

La unidad de exclusión es `EmployeeNumber + ResidentialId`. Esto permite entrada y salida por distintos relojes del mismo residencial.

PostgreSQL reclama trabajos con `FOR UPDATE SKIP LOCKED`:

- dos workers no procesan la misma fila simultáneamente;
- distintas claves pueden procesarse en paralelo;
- múltiples instancias de la API pueden compartir la cola;
- un reinicio no pierde pendientes.

Si llega otro evento mientras se procesa una clave, `RequestedRevision` avanza. La proyección sólo queda al día cuando `AppliedRevision` alcanza la revisión solicitada.

## Reintentos

Un fallo revierte la reconstrucción y marca la clave `ERROR`, incrementa `Attempts`, conserva `LastError` y programa `NextAttemptAtUtc` con backoff exponencial limitado. `MaxAttempts` evita reintentos infinitos.

## Reconciliación

Cada jornada tiene una identidad estable basada en su evento inicial. Al reconstruir:

- una fila sin cambios conserva revisión;
- una fila modificada incrementa `Revision`;
- una nueva comienza en revisión 1;
- una fila que deja de existir se conserva como `IsDeleted=true` e incrementa revisión.

Los tombstones permiten que el backend elimine resultados previamente importados.

## Consistencia para consumidores

La fuente durable es `AccessEvents`; `Jornadas` es una proyección eventualmente consistente. El backend debe:

- usar `updatedSinceUtc` para sincronización incremental;
- incluir tombstones con `includeDeleted=true`;
- aplicar solamente revisiones mayores;
- no calcular definitivamente mientras exista una proyección pendiente relevante;
- poder recalcular horas o remuneración si una jornada cambia.

## Reconstrucción dirigida

`POST /admin/jornadas/rebuild` vuelve a ensuciar una clave. No escribe jornadas manualmente ni altera eventos fuente.
