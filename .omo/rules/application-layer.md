---
name: application-layer
description: Historical/reference-only Event application-layer guidance; never binds Configurator product paths.
paths:
  - "reference-only/application-layer/**"
related_skills: []
related_docs: [AGENTS.md, PROJECTS.md, docs/internal/GOVERNANCE.md, docs/internal/QUICK_REFERENCE.md]
minimum_tests: []
related_intents: []
---

# Historical Event Rule: application-layer

Twin copies at `.agents/rules/application-layer.md` and `.omo/rules/application-layer.md` are byte-identical.

## Reference-Only Scope

The reserved `reference-only/application-layer/**` glob matches no Configurator path.
The original body below is retained verbatim from Event revision
`c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38` for historical engineering reference.
Its original paths, docs, intents, commands, libraries and mandatory language
are source-system facts, not Configurator instructions or active links. Do not
introduce Event HAL, EF, BFF, payment, identity or scheduling requirements.
Target authority remains [AGENTS.md](../../AGENTS.md), [PROJECTS.md](../../PROJECTS.md)
and [Governance](../../docs/internal/GOVERNANCE.md).

## Original Rule Body (Inactive)

```markdown

<!-- ABOUTME: Path-scoped rules for Explore.Application CQRS handlers, requests, DTOs, and validators. -->
<!-- ABOUTME: Twin copy at .omo/rules/application-layer.md. When modifying this file, update both paths. -->

# Application Layer Rules

## Applies To
- `src/Explore.Application/**/*.cs`

## Path-Specific Constraints
- **Handler Purity**: Handlers must stay single-purpose (one Command/Query per handler). No mixing of read/write concerns.
- **Contract Adherence**: Queries return DTOs shaped for the consumer; Commands return `BaseCommandResponse<TId>`.
- **Dependency Direction**: Reference Domain-only contracts and abstractions; never reach "outward" into API, Blazor, or persistence implementation details.
- **Cancellation**: Always pass `CancellationToken` through to all async calls (repository, cache, etc.).
- **Domain Rules Belong Here**: a rule that validates or normalizes command input must run in the handler, not at a transport boundary. A rule enforced only in a controller is bypassed by MCP tools and internal callers. Report the outcome as a `FailureCode` on the response; the API layer decides its HTTP shape.
- **Identity Semantics**: `Explore.Application.Authentication.PlatformIdentityPrincipalExtensions` owns the `sub -> nameidentifier -> sid -> internal_user_id` chain and provider-account reconstruction. `IUserContext` delegates to it. Add identity semantics there, never in a second place — three divergent chains previously coexisted and disagreed.
- **Record Requests And Results**: Follow the [primary record-selection policy](../../docs/internal/GOVERNANCE.md#primary-record-selection-policy). Concrete handwritten native CQS requests default to sealed records; choose positional versus nominal form for construction safety. `BaseCommandResponse<T>` and its concrete result descendants are immutable records created through valid-state factories. Current `UserId`/`TenantId` never comes from a body; a legitimate target ID still requires server authorization.
- **Native Operations**: Use exactly one `ICommand`, `ICommand<TResult>` or `IQuery<TResult>` contract with Task-based `ExecuteAsync`/`QueryAsync`. Inject closed handler interfaces, not concrete handlers or a generic sender. Authorization wraps timing for all three shapes; preserve handler-owned authority, manual validation and transaction ownership. External mediator packages are prohibited.
- **Published Collections**: Every collection-bearing record exposes a serializer-compatible read-only/immutable shape and snapshots mutable input. Preserve JSON arrays/objects, PATCH presence, HAL extension data, and base64 bytes; do not relabel a mutable `List`, array, dictionary, or set as immutable.

## Must Read
- [docs/internal/QUICK_REFERENCE.md#critical-rules](../../docs/internal/QUICK_REFERENCE.md#critical-rules) (Rules #1, #2, #5, #11)
- [docs/internal/ARCHITECTURE.md](../../docs/internal/ARCHITECTURE.md)

## Verification
- Build: `dotnet build --configuration Release --verbosity quiet`
- Tests: `Event.Application.UnitTests`, `Event.Architecture.Tests`

## Related
- Intents: `add-cqrs-handler`, `add-get-endpoint`, `add-write-endpoint`, `update-repository-query`
- Agents: `architect-agent.md`, `backend-engineer-agent.md`, `quality-verifier-agent.md`
- Rules: `api-controllers.md`, `efcore-persistence.md`, `domain.md`
```
