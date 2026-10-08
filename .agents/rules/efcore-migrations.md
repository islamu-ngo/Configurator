---
name: efcore-migrations
description: Historical/reference-only Event efcore-migrations guidance; never binds Configurator product paths.
paths:
  - "reference-only/efcore-migrations/**"
related_skills: []
related_docs: [AGENTS.md, PROJECTS.md, docs/internal/GOVERNANCE.md, docs/internal/QUICK_REFERENCE.md]
minimum_tests: []
related_intents: []
---

# Historical Event Rule: efcore-migrations

Twin copies at `.agents/rules/efcore-migrations.md` and `.omo/rules/efcore-migrations.md` are byte-identical.

## Reference-Only Scope

The reserved `reference-only/efcore-migrations/**` glob matches no Configurator path.
The original body below is retained verbatim from Event revision
`c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38` for historical engineering reference.
Its original paths, docs, intents, commands, libraries and mandatory language
are source-system facts, not Configurator instructions or active links. Do not
introduce Event HAL, EF, BFF, payment, identity or scheduling requirements.
Target authority remains [AGENTS.md](../../AGENTS.md), [PROJECTS.md](../../PROJECTS.md)
and [Governance](../../docs/internal/GOVERNANCE.md).

## Original Rule Body (Inactive)

```markdown

<!-- ABOUTME: Apply when editing Explore.Persistence migration files or model snapshots. -->
<!-- ABOUTME: Twin copy at .omo/rules/efcore-migrations.md. When modifying this file, update both paths. -->

# EF Core Migration Rules

## Applies To
- `src/Explore.Persistence/Migrations/**/*.cs`

## Path-Specific Constraints
- **Generated Only**: Migration and model-snapshot files are immutable generated output. Never create or edit them by hand.
- **Fix the Source**: Correct entities, `IEntityTypeConfiguration<T>` mappings, `DbContext` setup, lookup seeding, or the repository's migration-generation extension before regenerating.
- **Development Regeneration**: Delete an unapplied development migration with `dotnet ef migrations remove` (or regenerate the reset init when explicitly authorized), then run `dotnet ef migrations add`; inspect and verify the output without patching it.
- **Reversibility**: Always provide a valid `Down` method that correctly reverses the `Up` migration.
- **Named Filters**: Verify that `SoftDelete` and tenant filters remain intact after schema changes.
- **Lookup Parity**: Keep lookup enum IDs and stable `MasterCode` values synchronized with idempotent missing-row repair in `LookupTableSeeder`.
- **Seed Ordering**: Use migration-local `InsertData` before any dependent backfill because runtime seeding runs only after all migrations finish.
- **HasData Guard**: Do not add model `HasData()` while EF Core #36682 applies; existing migration-owned seed history remains immutable.
- **History Integrity**: Never delete, rename, or rewrite migrations that have already been applied or merged; fix the model and generate a corrective migration.
- **Snapshot Accuracy**: Ensure the `ModelSnapshot` is updated and reflects the final intended model state.

## Must Read
- [docs/internal/QUICK_REFERENCE.md#auditing-and-soft-delete](../../docs/internal/QUICK_REFERENCE.md#auditing-and-soft-delete)
- [docs/internal/DOMAIN.md](../../docs/internal/DOMAIN.md)

## Verification
- Build: `dotnet build --configuration Release --verbosity quiet`
- Tests: `Event.Persistence.IntegrationTests`, `Event.Architecture.Tests`

## Related
- Intents: `add-ef-migration`
- Agents: `backend-engineer-agent.md`, `quality-verifier-agent.md`
- Rules: `efcore-persistence.md`, `domain.md`
```
