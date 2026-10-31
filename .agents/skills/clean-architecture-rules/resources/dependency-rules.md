> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Dependency rules per layer (Clean Architecture). -->
<!-- Use this to validate layer references only. -->

# Dependency Rules

Historical Event layer reference only. For Configurator use `PROJECTS.md` and
the active skill's Core/Presentation/Artifacts/client boundaries; do not infer
that Browser may reference all layers from the composition-root example below.

## Allowed References (Required)
- **Domain** → none
- **Application** → Domain only
- **Persistence/Infrastructure** → Application + Domain
- **API/Blazor** → all layers (composition root)

## Quick Checks
- Domain must not reference EF Core, external mediator/mapping packages, or ASP.NET.
- Application must not reference Persistence/Infrastructure.

## Exceptions
- Domain may use `[ForeignKey]` only (no validation attributes).

**Related**: `layer-responsibilities.md`, `violation-examples.md`, `fix-patterns.md`.
