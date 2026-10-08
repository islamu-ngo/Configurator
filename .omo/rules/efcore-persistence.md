---
name: efcore-persistence
description: Historical/reference-only Event efcore-persistence guidance; never binds Configurator product paths.
paths:
  - "reference-only/efcore-persistence/**"
related_skills: []
related_docs: [AGENTS.md, PROJECTS.md, docs/internal/GOVERNANCE.md, docs/internal/QUICK_REFERENCE.md]
minimum_tests: []
related_intents: []
---

# Historical Event Rule: efcore-persistence

Twin copies at `.agents/rules/efcore-persistence.md` and `.omo/rules/efcore-persistence.md` are byte-identical.

## Reference-Only Scope

The reserved `reference-only/efcore-persistence/**` glob matches no Configurator path.
The original body below is retained verbatim from Event revision
`c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38` for historical engineering reference.
Its original paths, docs, intents, commands, libraries and mandatory language
are source-system facts, not Configurator instructions or active links. Do not
introduce Event HAL, EF, BFF, payment, identity or scheduling requirements.
Target authority remains [AGENTS.md](../../AGENTS.md), [PROJECTS.md](../../PROJECTS.md)
and [Governance](../../docs/internal/GOVERNANCE.md).

## Original Rule Body (Inactive)

```markdown

<!-- ABOUTME: Apply when editing Explore.Persistence repositories, DbContext, filters, or entity configurations outside migrations. -->
<!-- ABOUTME: Twin copy at .omo/rules/efcore-persistence.md. When modifying this file, update both paths. -->

# EF Core Persistence Rules

## Applies To
- `src/Explore.Persistence/**/*.cs` (excluding Migrations)

## Path-Specific Constraints
- **Context Pooling**: Use property injection for scoped context data; do not add scoped dependencies to `ExploreDbContext` constructor.
- **Mapping Logic**: Favor `IEntityTypeConfiguration<T>` classes over smearing mapping rules across the `DbContext` or repositories.
- **Explicit Tracking**: Use `AsNoTracking()` for all read-only query flows.
- **Specification Pattern**: Use the `EventQuerySpecification` builder for all complex queries; avoid ad-hoc LINQ in repositories.

## Must Read
- [docs/internal/QUICK_REFERENCE.md#critical-rules](../../docs/internal/QUICK_REFERENCE.md#critical-rules) (Rules #1, #12, #17)
- [docs/internal/MULTI_TENANCY.md](../../docs/internal/MULTI_TENANCY.md)

## Verification
- Build: `dotnet build --configuration Release --verbosity quiet`
- Tests: `Event.Persistence.IntegrationTests`, `Event.Architecture.Tests`

## Related
- Intents: `update-repository-query`, `add-ef-migration`, `add-cqrs-handler`
- Agents: `backend-engineer-agent.md`, `quality-verifier-agent.md`
- Rules: `application-layer.md`, `domain.md`, `efcore-migrations.md`
```
