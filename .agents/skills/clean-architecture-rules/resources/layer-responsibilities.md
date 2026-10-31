> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- Decide which layer owns a piece of code. -->
<!-- Use this as a quick placement guide. -->

# Layer Responsibilities

Historical backend placement guide only. The target uses Core, Presentation,
Artifacts and clients, not the absent layers listed below. Preserve the
separation reasoning without creating repositories, HTTP or email services.

## Decision Guide
- **Domain**: business rules, entities, value objects.
- **Application**: use cases, commands/queries, handlers, DTOs, validators.
- **Persistence**: EF Core, DbContext, repositories, migrations.
- **Infrastructure**: external services (email, storage, integrations).
- **API/Blazor**: HTTP endpoints and UI only.

## Quick Examples
- Domain: `{Entity}`, `{EntityStatus}`
- Application: `Create{Entity}Command`, `{Entity}Dto`
- Persistence: `{DbContext}`, `{Entity}Repository`
- Infrastructure: `EmailService`
- API/Blazor: `{Entity}Controller`, `{Entities}List.razor`

**Related**: `dependency-rules.md`.
