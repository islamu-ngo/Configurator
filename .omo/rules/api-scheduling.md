---
name: api-scheduling
description: Historical/reference-only Event api-scheduling guidance; never binds Configurator product paths.
paths:
  - "reference-only/api-scheduling/**"
related_skills: []
related_docs: [AGENTS.md, PROJECTS.md, docs/internal/GOVERNANCE.md, docs/internal/QUICK_REFERENCE.md]
minimum_tests: []
related_intents: []
---

# Historical Event Rule: api-scheduling

Twin copies at `.agents/rules/api-scheduling.md` and `.omo/rules/api-scheduling.md` are byte-identical.

## Reference-Only Scope

The reserved `reference-only/api-scheduling/**` glob matches no Configurator path.
The original body below is retained verbatim from Event revision
`c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38` for historical engineering reference.
Its original paths, docs, intents, commands, libraries and mandatory language
are source-system facts, not Configurator instructions or active links. Do not
introduce Event HAL, EF, BFF, payment, identity or scheduling requirements.
Target authority remains [AGENTS.md](../../AGENTS.md), [PROJECTS.md](../../PROJECTS.md)
and [Governance](../../docs/internal/GOVERNANCE.md).

## Original Rule Body (Inactive)

```markdown

<!-- ABOUTME: Path-scoped rules for Quartz jobs, scheduler composition, and API-hosted background services. -->
<!-- ABOUTME: Twin copies live at .agents/rules/api-scheduling.md and .omo/rules/api-scheduling.md; update both paths. -->

# API Scheduling And Background Work Rules

## Applies To
- `src/Explore.API/Scheduling/**/*.cs`
- `src/Explore.API/BackgroundServices/**/*.cs`
- `src/Explore.API/Extensions/QuartzSchedulerExtensions.cs`

## Path-Specific Constraints
- **One Scheduling Authority**: Quartz.NET is the platform scheduler. Do not introduce a second scheduling concept — no bespoke timer-loop base class, no new scheduling package. TickerQ was removed; do not reintroduce it or code that assumes it.
- **A Job Is One Pass**: an `IJob` performs a single iteration and nothing else. Enablement, initial delay, interval, cancellation, exception containment, and per-execution DI scope belong to the scheduler, not to the job body.
- **Register Through The Helpers**: add a periodic sweep with `AddSweepJob<TJob>` and a cron job with `AddCronJob<TJob>` in `QuartzSchedulerExtensions`. A disabled sweep is not registered at all, so a turned-off feature leaves no dormant trigger for operators to puzzle over.
- **`[DisallowConcurrentExecution]`**: required on sweeps that must not overlap. It preserves the sequential guarantee the old `while` loops had — a slow pass delays the next one rather than running beside it.
- **Names Are Operational Contract**: job identifiers live in `Explore.Application.Contracts.Scheduling.ScheduledJobNames`. They appear in scheduler rows, the status endpoint, and operator alerting; renaming one orphans its persisted trigger. Log completion as `Scheduled job {JobName} completed.` so alerting matches on `JobName`, not prose.
- **Pointer-Only Payloads**: a `JobDataMap` carries durable identifiers only. Message content and transport data stay in the application database so a stale scheduler row can never resend real content. A malformed payload is a poison message: log and drop it rather than retrying forever.
- **Scheduler-Neutral Application Layer**: Quartz types stay inside `Explore.API`. Application code depends on `IScheduledJobRegistry` / `ScheduledJobDescriptor`, never on `Quartz`.
- **Keep Durable Semantics Below The Wrapper**: retry, fencing, leases, and outbox handling belong in the service the job calls. Never "simplify" them into the scheduling layer.

## Justified Exceptions
Not every hosted service is a periodic sweep, and forcing one into the scheduler changes its meaning:

| Kind | Why it stays a hosted service |
|---|---|
| `OutboxProcessor` | Durable side-effect authority; its fencing and retry are coupled to its own loop |
| `ManagedControlPlaneRegistrationWorker` | Retry-until-registered bootstrap that returns on success — a recurring trigger would never stop |
| Queue/event-driven drains, startup gates | Not interval-driven at all |

These are semantic categories, not a source allowlist. `QuartzSchedulerCompositionTests`
proves the registered recurring-job manifest and queue-drain boundaries through
runtime composition; owning outbox and bootstrap tests prove the listed
non-periodic services keep their distinct behavior.

## Operator Impact
Any change to a job's identifier, log event name, health-check name, metric name, or configuration key is an operator-visible change and must be documented in `docs/internal/OPERATIONS.md` in the same slice. Self-hosters alert on these.

## Must Read
- [docs/internal/OPERATIONS.md](../../docs/internal/OPERATIONS.md) — scheduled job catalog and the maintenance-sweep upgrade note
- [docs/internal/QUICK_REFERENCE.md#critical-rules](../../docs/internal/QUICK_REFERENCE.md#critical-rules) (Rule #27)
- [docs/internal/OUTBOX_PATTERN.md](../../docs/internal/OUTBOX_PATTERN.md)

## Verification
- Build: `dotnet build --configuration Release --verbosity quiet`
- Tests: `Event.Persistence.IntegrationTests` owns outbox, retention, and idempotency semantics — the behavior a sweep actually drives. `Event.API.IntegrationTests` covers scheduler composition.
- The scheduler is disabled in the `Testing` environment, so a job's work must be reachable through its service for testing; do not rely on the scheduler running under test.

## Related
- Intents: `schedule-background-work` (primary), `add-cqrs-handler`, `external-infrastructure-bootstrap`
- Rules: `application-layer.md`, `efcore-persistence.md`
- Skills: `outbox-pattern`, `clean-architecture-rules`
```
