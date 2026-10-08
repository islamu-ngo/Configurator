---
name: outbox-pattern
description: "Reference-only source-system guidance; do not activate for Configurator offline engine, clients or repository tooling."
type: reference
enforcement: inform
priority: high
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


<!-- Transactional outbox pattern skill for reliable at-least-once event delivery. -->
<!-- Covers OutboxMessage entity, OutboxProcessor, retry/dead-letter, and specialized variants. -->

# Outbox Pattern

## Non-Inferable Rules

1. OutboxMessage uses UUID v7 (`Guid` in C#, `uuidv7()` in PostgreSQL) — not auto-increment.
2. `TryMarkAsProcessing` uses optimistic concurrency — returns `bool`, no exceptions on contention.
3. Retry delay formula: `InitialRetryDelaySeconds × 2^retryCount`, capped at `MaxRetryDelaySeconds`.
4. After `MaxRetries` exhausted, status transitions to `DeadLettered` — messages stay in DB indefinitely for monitoring.
5. `LoggingOutboxMessageDispatcher` is the default no-op — real dispatchers must be registered to replace it.
6. Three specialized outbox variants exist: general `OutboxMessage`, `PolicyChangeOutbox` (settings), `PdsSyncOutbox` (AT Protocol federation). Each has its own entity and repository.
7. OutboxProcessor polls on a `PeriodicTimer` — not event-driven. Default interval: 5 seconds.
8. Dispatchers must be idempotent — at-least-once delivery means duplicates are possible.

## Resources

- `resources/entity-model.md` — OutboxMessage fields, status lifecycle, EF configuration
- `resources/processor-configuration.md` — OutboxProcessor settings, retry strategy, dead-letter
- `resources/dispatcher-patterns.md` — IOutboxMessageDispatcher, specialized variants
- `resources/database-schema.md` — Indexes, queries, database conventions

## Related

- `docs/internal/OUTBOX_PATTERN.md`
- `docs/internal/ARCHITECTURE.md` (Background Services section)
- `.agents/skills/dotnet-efcore-guidelines/SKILL.md`
- `.agents/skills/clean-architecture-rules/SKILL.md`
