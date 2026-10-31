# Knowledge Map

> **Audience:** Contributors | Maintainers | AI agents
> **Status:** Implemented routing index; strategy dispositions remain bounded
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-30
> **Source Anchors:** `docs/internal/DOCUMENTATION_ARCHITECTURE.md`, `docs/internal/adr/`, `docs/internal/reports/README.md`, `docs/internal/extraction/FOUNDATION.md`

Use this map to choose the kind of knowledge needed, not to create a second
policy. [Documentation architecture](DOCUMENTATION_ARCHITECTURE.md) owns
documentation policy and public/technical parity. [Status](STATUS.md) owns the
route between implementation, package, host and target claims; it does not
replace their evidence.

## Kinds and lifecycle

| Kind / lifecycle | Entry and purpose | Boundary |
| --- | --- | --- |
| Current contributor authority | [AGENTS](../../AGENTS.md), [project map](../../PROJECTS.md), [quick reference](QUICK_REFERENCE.md) | Target contracts govern actual owners; transferred Event examples cannot create absent runtime layers |
| Current task guidance and reference | [Documentation hub](../README.md), [public guides](../public/), [documentation architecture](DOCUMENTATION_ARCHITECTURE.md) | Procedures explain tasks; technical anchors specify contracts; implementation status is separate |
| Current procedures with retained evidence | [Operations entry](OPERATIONS.md#current-procedure-entry), [Testing](TESTING.md), [release runbook](RELEASE_RUNBOOK.md) | Operations retains dated records in place; use current owners rather than replaying historical commands blindly |
| Current status and evidence navigation | [Status index](STATUS.md) | A source assertion, accepted design, package pass and actual-host observation are different claims |
| Accepted or proposed architecture | [ADR index](adr/README.md) | Preserve the record's explicit status and amendments; accepted design is not delivery |
| Active execution memory | `dev/active/<task>/` when present | An approved task's context/ledger is working memory, not a permanent product contract or new approval |
| Deferred or required follow-up | [Backlog](../../dev/backlog/) | Trigger and acceptance criteria define unfinished duties; a backlog entry does not authorize execution |
| Durable engineering findings | [Journal](../../dev/_journal/) | Reusable findings complement contracts, not a second status ledger |
| Point-in-time assessment | [Reports index](reports/README.md) | Immutable bodies preserve inspected state and failures, not current specifications |
| Provider-responsibility governance and assessments | [I-VSD catalog](../../islamic-value-sensitive-design/README.md) | Current root navigation separates governance, consultations and workstreams; recorded report lifecycle/cutoff is not renewed approval or implementation evidence |
| Transfer provenance | [Foundation](extraction/FOUNDATION.md), [provenance](extraction/PROVENANCE.md) | Source revisions, exclusions and dispositions explain transfer; they do not confer release, target or publisher authority |
| User strategy and research | [Universal configurator](universal-configurator.md), [go-to-market](go-to-market.md) | User-owned originals remain unchanged; relationships below do not turn recommendations into decisions |

A report recommendation can inform a proposed ADR, an accepted decision can
guide implementation, and execution can supply qualification evidence. None of
those transitions is automatic. Use the actual decision and dated execution
record; if no disposition is recorded, mark it **awaiting decision**. Keep
historical versions and evidence at their existing locations rather than
rewriting them to match the current product.

## User strategy relationships and dispositions

The two strategy documents retain broad product, adoption and research proposals.
The table maps selected consequential relationships to explicit repository
records. It does not claim that an ADR ratifies an entire strategy document.

| Strategy topic and source heading | Recorded relationship / disposition | Decision or follow-up reference |
| --- | --- | --- |
| Portable runtime, data-only definitions and target authority: [product boundary](universal-configurator.md#the-most-important-product-boundary), [data-only strategy](go-to-market.md#keep-definitions-data-only) | Accepted bounded offline preparation boundary; application apply and semantic acceptance stay target-owned | [Runtime ADR](adr/ADR-definition-runtime-boundary.md), [v2 decisions](adr/ADR-definition-standard-v2.md#decisions) |
| Closed domain model rather than arbitrary schema: [product architecture](universal-configurator.md#recommended-product-and-schema-architecture) | Accepted bounded-v2 profile, not a universal JSON Schema engine | [v2 D01](adr/ADR-definition-standard-v2.md#d01---a-bounded-profile-not-a-general-schema-engine) |
| Publisher-owned source and canonical handoff: [go-to-market judgment](go-to-market.md#executive-judgment) | Accepted one-source compilation and canonical distribution; market adoption is a separate outcome | [Maintainer authoring ADR](adr/ADR-maintainer-authoring-and-canonical-distribution.md), [authoring contract](MAINTAINER_AUTHORING.md) |
| Optional catalog and separate repositories: [architectural recommendation](universal-configurator.md#the-architectural-change-i-would-make-to-your-proposal) | Optionality accepted; initial in-repository Store explicitly chosen, separate-repository move deferred | [Store ADR](adr/ADR-optional-definition-store.md), [repository separation follow-up](../../dev/backlog/separate-definition-store-repository.md) |
| Direct README links, pinned bytes and source modes: [handoff strategy](go-to-market.md#the-deep-link-and-cross-platform-architecture-i-would-ship) | Accepted bounded source/selection design, not every proposed route, publisher index or trust service | [Browser trust ADR](adr/ADR-browser-session-trust-and-export.md), [launch contract](LAUNCH_CONTRACT.md); broader [publisher distribution backlog](../../dev/backlog/publisher-definition-distribution.md) retains historical assumptions, not current Browser custody authority |
| Browser first, higher-trust native lane and explicit secret consent: [browser strategy](go-to-market.md#browser-first-desktop-as-an-explicit-higher-trust-lane), [secret trust](go-to-market.md#treat-browser-secret-entry-as-a-distinct-trust-decision) | Accepted static Blazor WASM with per-workspace consent; the strategies' Avalonia Browser wording does not describe the selected Browser adapter | [Browser trust ADR](adr/ADR-browser-session-trust-and-export.md), [protected save status](STATUS.md#protected-save) |
| Human guidance and thin adapters: [architecture](universal-configurator.md#recommended-product-and-schema-architecture), [explanations](go-to-market.md#forms-are-not-enough-explanation-is-your-valuable-layer) | Workspace design accepted with separate runtime limits; broader experience/answer-authority ADR still explicitly proposed | [Workspace ADR](adr/ADR-browser-workspace-presentation.md), [proposed experience ADR](adr/ADR-configuration-experience-and-answer-authority.md), [accessibility status](STATUS.md#accessibility) |
| Semantic comparison and upgrade understanding: [final CTO recommendations](universal-configurator.md#my-final-cto-architecture-decision) | Bounded ordinary-export assessment design accepted; implementation in progress, client/package qualification pending; no guessed rename/release history | [Assessment ADR](adr/ADR-artifact-import-comparison.md), [implementation contract](IMPORT_COMPARISON.md), [broader workflow follow-up](../../dev/backlog/universal-configurator-followups.md#upgrade-and-effective-source-assessment) |
| External targets and evidence-driven adoption: [candidate targets](universal-configurator.md#best-manifest-bearing-self-hosted-targets-beyond-keycloak) | Trial duties recorded; a strategy's target priority is not a qualified integration | [Target trial follow-up](../../dev/backlog/definition-profile-target-adoption-trials.md), [target acceptance status](STATUS.md#target-acceptance); unrecorded individual target selection remains awaiting decision |
| Marketing execution, launch priorities and outcome promises: [adoption engine](go-to-market.md#the-adoption-and-marketing-engine) | Awaiting decision where no explicit repository decision is referenced; no campaign, launch, publisher trust or outcome guarantee inferred | No decision established by this correction; [workflow follow-ups](../../dev/backlog/universal-configurator-followups.md) bound later provider, apply and opt-in measurement intake |

### Citation provenance limits

Conversation-local `turn...search...`, `turn...view...` and `filecite` tokens
inside retained strategy prose do not identify independently resolvable sources
in this checkout. Their conversation, source URL, attachment identity, review
date and verification cannot be reconstructed from a token alone. Preserve the
original text; mark that provenance **unresolved** unless an independently
identified source record establishes it. Do not invent source mappings,
translate tokens into guessed links or treat them as current research evidence.
An independently linked ADR supplies its own decision provenance, not missing
provenance for the strategy's external claims.

## Historical validation limitations

The [2026-10-30 migration record](reports/DOCUMENTATION_MIGRATION_2026-10-30.md)
records the initial validator baseline and separate final current/audit checks.
Historical classes include outside-root/retired links, an unclosed fence and
absent modern metadata. These are preserved limitations, not grounds to mutate
immutable report bodies or weaken current-route checks.

See [classified report limitations](reports/README.md#classified-historical-validation-limitations)
for exact supplied locations. Current contracts, procedures and status must be
reached directly through the owners above, not through those broken report
links. Historical material remains useful as an inspected claim with its date,
revision and availability limits; it is not a dependency of current navigation.
