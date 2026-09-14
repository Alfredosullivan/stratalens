# CLAUDE.md — Stratalens

Este proyecto tiene su especificación completa en `SDD/`. Antes de modificar código, leer en este orden:

1. [`SDD/PRODUCT.md`](SDD/PRODUCT.md) — qué es el producto, para quién, qué queda fuera de alcance.
2. [`SDD/ARCHITECTURE.md`](SDD/ARCHITECTURE.md) — stack, capas, decisiones técnicas (ADRs).
3. [`SDD/RULES.md`](SDD/RULES.md) — restricciones que el código nunca debe violar.
4. [`SDD/TASKS.md`](SDD/TASKS.md) — roadmap por fases y la tarea atómica actual.
5. [`SDD/INSTRUCTIONS.md`](SDD/INSTRUCTIONS.md) — contrato operativo: cómo trabajar en este repo (una tarea a la vez, en orden de dependencias; no modificar decisiones de `ARCHITECTURE.md` sin aprobación; nunca marcar una tarea `done` sin cumplir su criterio de aceptación).

El documento original de visión de producto (`Contexto maestro — Software de visualización de arquitectura full-stack y observabilidad.md`, en la raíz) es la fuente de la que salió `SDD/`. Consultarlo solo para contexto histórico/ampliado — las decisiones vinculantes viven en `SDD/`, no ahí.

Este archivo se apoya además en las instrucciones globales de `C:\Users\alfre\CLAUDE.md` (perfil de Carlos, Modo Enseñanza Activa, Método Mixto, stack general). Ninguna de las dos se sustituye: las globales definen *cómo* trabajamos juntos, `SDD/` define *qué* es este producto específico.
