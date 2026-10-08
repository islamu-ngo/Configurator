> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Query conventions for CQRS in this codebase. -->
<!-- Emphasizes DTO returns and repository usage. -->

# Query Patterns (Lean)

## Rules
- Query requests typically end with `Request` suffix.
- Queries return DTOs directly (no BaseCommandResponse).
- Repositories return entities; handlers map to DTOs.
- Use `AllowAnonymous` for GET unless rule says otherwise.

## Related
- [handler-patterns.md](handler-patterns.md)
