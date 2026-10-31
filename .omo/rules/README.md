# Path-Scoped Rules

This is the shared index for `.agents/rules/` and `.omo/rules/`: 15 rules,
`_schema.md` and this README form 17 byte-identical twin pairs. Actual
Configurator paths and [AGENTS.md](../../AGENTS.md)/[PROJECTS.md](../../PROJECTS.md)
govern; historical Event layers never bind target paths.

## How To Use These Rules

| Principle | Meaning |
|---|---|
| Intent first | [intents.yaml](../../.agents/contract/intents.yaml) is primary; path rules refine it |
| Governing docs win | Target [Quick Reference](../../docs/internal/QUICK_REFERENCE.md) and [Governance](../../docs/internal/GOVERNANCE.md) refine the root contract |
| Cross-reference only | Rules point at governing docs; they must not duplicate them |
| Surgical context | Keep rules specific to file paths, not whole-project summaries |
| Reuse loaded context | Must-read lists name evidence locations, not whole-file reloads or unconditional resource cascades |

See [`_schema.md`](_schema.md) before adding or editing any rule file.

## Indexed Rules

| Rule | Paths | Focus |
|---|---|---|
| [blazor-client.md](blazor-client.md) | `src/Configurator.Browser/**/*.cs`, `**/*.razor`, `**/*.css` under that owner | Static browser custody, pinned sources, public caches, parameters, immutable snapshots, BEM/scoped CSS and accessibility |
| [tests.md](tests.md) | `tests/**/*.cs`, `eng/**/tests/**/*.cs` | Independent oracle, public mocks, deterministic async, bUnit, invariant disposition and quarantine |
| [privacy-and-pii.md](privacy-and-pii.md) | `src/Configurator.{Core,Artifacts,Presentation,Cli,Terminal,Desktop,Browser}/**/*.cs`; browser Razor | Explicit owner globs in rule: sensitivity floors, generation fencing, protected writes and value-safe public boundary |
| [work-criticality-matrix.md](work-criticality-matrix.md) | `src/**/*.cs`, `tests/**/*.cs`, `eng/**/*.cs`, `src/**/*.razor`, `.agents/contract/intents.yaml` | Proportional intake, model capability, data-flow exploration and adversarial tests |
| [ip-clean-room.md](ip-clean-room.md) | `src/**/*`, `tests/**/*`, `eng/**/*`, `docs/**/*`, `dev/active/**/*`, `.agents/**/*`, `**/*.csproj`, `**/packages.lock.json`, `Directory.*.props`, `NuGet.Config`, `global.json`, `THIRD-PARTY-NOTICES.md` | Clean handoff, source register, SSO, first-party MIT transfer and third-party notices |

### Inactive Historical Rules

Each original body is preserved verbatim in a fenced reference block, with no
active tests, skills or intents. Only the reserved `reference-only/<name>/**`
glob is declared. That namespace has no target files and must not be used for
product work. Original paths/links/commands inside the block are historical
source-system facts, not live target requirements.

| Rule | Inactive glob | Historical focus |
|---|---|---|
| [api-controllers.md](api-controllers.md) | `reference-only/api-controllers/**` | Controller/route contracts |
| [api-hateoas.md](api-hateoas.md) | `reference-only/api-hateoas/**` | HAL policies and registration |
| [api-scheduling.md](api-scheduling.md) | `reference-only/api-scheduling/**` | Quartz/background work |
| [application-layer.md](application-layer.md) | `reference-only/application-layer/**` | CQS, cancellation, immutable collections |
| [auth-trust-boundaries.md](auth-trust-boundaries.md) | `reference-only/auth-trust-boundaries/**` | BFF/identity/tenant authority |
| [blazor-server.md](blazor-server.md) | `reference-only/blazor-server/**` | BFF/YARP/SSR |
| [domain.md](domain.md) | `reference-only/domain/**` | Domain purity/value semantics |
| [efcore-persistence.md](efcore-persistence.md) | `reference-only/efcore-persistence/**` | EF queries/configuration |
| [efcore-migrations.md](efcore-migrations.md) | `reference-only/efcore-migrations/**` | Generated migrations/seed parity |
| [payments-commerce.md](payments-commerce.md) | `reference-only/payments-commerce/**` | Payment/outbox/provider invariants |

## Layout Notes

- Actual product roots are `src/Configurator.*`; engineering roots are `eng/`.
- Do not add HAL, EF, BFF, payments or scheduling to satisfy an inactive body.
- [Documentation Architecture](../../docs/internal/DOCUMENTATION_ARCHITECTURE.md)
  separates technical anchors from operator guides; [docs/README.md](../../docs/README.md)
  indexes them. No blanket redirect to the extraction FOUNDATION.

## Why Twin Copies?

OmO discovers/injects rules from `.omo/rules/` through its `rules-injector`
hook. Its rules engine scans `.omo/rules/`, `.claude/rules/`, `.cursor/rules/`
and `.github/instructions/`, not `.agents/rules/`. Keep `.agents/rules/` as the
explicit contract/intent route and `.omo/rules/` as the discovery twin.
Claude Code's native discovery directory is `.claude/rules/`, absent here;
do not claim it automatically scans `.agents/rules/`. GitHub instructions
route explicitly through `applyTo` to the canonical rules.

## Synchronization Contract

- Update both physical twins, including README and schema, in the same change.
- **Never use symlinks**: realpath-based deduplication collapses candidates.
- Twin notes name both paths; no harness-specific policy fork.

## How OmO Matches Rules

| Field | Behavior |
|---|---|
| `paths:` or `globs:` | Picomatch patterns against the edited file's relative path |
| `description:` | Human-readable rule summary |
| `alwaysApply: true` | Inject on every prompt, irrespective of path; not used for inactive rules |
| Negative globs (`!pattern`) | Exclude matching paths |

Ranking is source priority (`.omo/rules` priority 0), then directory distance,
then character budget: 12K per rule and 40K total. Oversized rules are truncated;
keep task retrieval bounded instead of duplicating policy. Inactive bodies
must never acquire active globs or `alwaysApply`.
