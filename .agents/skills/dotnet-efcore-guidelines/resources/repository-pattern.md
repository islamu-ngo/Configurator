> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Repository usage rules for EF Core in this project. -->
<!-- Emphasizes entity returns and handler-side mapping. -->

# Repository Pattern (Lean)

## Rules
- Repositories live in Persistence; interfaces in Application.
- Repositories return entities, not DTOs.
- Handlers map entities → DTOs with Mapperly.
- Do not return `IQueryable` from repositories.

## Related
- [dbcontext-patterns.md](dbcontext-patterns.md)
