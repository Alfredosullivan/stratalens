# INSTRUCTIONS.md — Contrato Operativo del Agente

> **Qué es este archivo:** a diferencia de los otros cuatro, esto no describe el producto — describe cómo debe *trabajar* el agente de IA dentro de este repositorio. Nota de nombres reales según herramienta: en Claude Code se usa `CLAUDE.md`, en GitHub Copilot va en `.github/copilot-instructions.md`. Renombra el archivo según la herramienta que uses; el contenido es el mismo.

## Antes de modificar código
1. Leer `PRODUCT.md`
2. Leer `ARCHITECTURE.md`
3. Leer `RULES.md`
4. Revisar el estado actual en `TASKS.md`

## Antes de implementar
- Explicar qué archivos van a modificarse antes de tocarlos.
- No modificar decisiones de `ARCHITECTURE.md` sin aprobación explícita — si una tarea de `TASKS.md` obliga a contradecir una decisión registrada (ej. cambiar de PostgreSQL a Neo4j), parar y proponer una nueva entrada de ADR antes de tocar código.
- No introducir dependencias nuevas (paquetes NuGet, npm) sin justificarlo.
- Ejecutar los tests relevantes después de implementar (unit tests de la capa tocada + integration tests del flujo afectado).
- Trabajar una tarea de `TASKS.md` a la vez, en el orden de sus dependencias — no adelantar `T10` si `T8`/`T9` siguen en `pending`.

## Nunca
- Crear lógica de negocio en Controllers o SignalR Hubs.
- Saltarse la capa de Application (Api → Infrastructure directo).
- Duplicar un analyzer, adapter o servicio que ya existe en vez de extenderlo.
- Marcar una tarea de `TASKS.md` como `done` sin que su criterio de aceptación se cumpla.
- Persistir código fuente crudo de un repositorio analizado (solo el resultado del análisis).
- Presentar un `Edge` inferido como si fuera un hecho confirmado — respetar siempre `Source` y `Confidence` (ver `RULES.md`).
- Analizar un repositorio que el usuario no haya conectado explícitamente vía OAuth.

## Personaliza esto

- Idioma de trabajo: español. Comentarios de código: español (regla global ya definida en `C:\Users\alfre\CLAUDE.md`, se reafirma aquí porque este proyecto maneja código de terceros y no debe mezclarse el idioma).
- Este proyecto sigue el **Modo Enseñanza Activa** y el **Método Mixto** definidos en el CLAUDE.md global (Carlos propone su enfoque primero; interrogatorio obligatorio sobre todo código que se integra; señalar puntos de valor de entrevista con 💼).
- Este es un proyecto de alcance grande (ver secciones "Fuera de alcance" de `PRODUCT.md` y ADRs de `ARCHITECTURE.md`) — resistir la tentación de adelantar funcionalidad de fases futuras (AI, multi-cloud, Time Travel, etc.) mientras la Fase 0-1 no esté cerrada.
- Antes de empezar cualquier tarea nueva de `TASKS.md`, confirmar con Carlos que el criterio de aceptación de la tarea anterior se cumplió (no asumir "probablemente funciona").
- Cuando se cierre una fase completa del roadmap (`TASKS.md`), generar el portfolio HTML del progreso solo si Carlos lo pide explícitamente ("genera el portfolio"), no de forma automática.

> Nota operativa: para que Claude Code cargue este contrato automáticamente, conviene crear un `CLAUDE.md` en la raíz del proyecto (no dentro de `SDD/`) que apunte a los 5 archivos de esta carpeta, o copiar este contenido allí. Se deja pendiente hasta que Carlos lo pida explícitamente — no se creó junto con el resto de la carpeta SDD para no exceder lo solicitado.
