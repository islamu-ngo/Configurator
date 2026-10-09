# Documentation Hub

Welcome to the Configurator documentation hub. To provide the best possible
experience for adopters and engineers, documentation is separated into two
distinct tracks:

```text
docs/
├── README.md               <-- Documentation Router
├── public/                 <-- Public Documentation for Adopters and Integrators
└── internal/               <-- Engineering Brain for Contributors and AI Agents
```

This router preserves the dual-track structure from Event revision
`c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38` with actual Configurator guides.
No Event hosted portal or publication activation is implied.

## Choose Your Path

| I am a... | Goal | Go To |
|---|---|---|
| Adopter | Prepare configuration offline | [Getting started](public/getting-started.md), then [CLI](public/cli.md), [Terminal](public/terminal.md), [Desktop](public/desktop.md) or [Browser](public/browser.md) |
| Definition author | Supply bounded declarative input | [Definitions](public/definitions.md) |
| Integrator | Hand prepared configuration to a target without granting apply authority | [Launch integration](public/launch-integration.md) |
| Developer contributor | Change C# owners or client interaction | [PROJECTS.md](../PROJECTS.md), [Governance](internal/GOVERNANCE.md), [Testing](internal/TESTING.md) |
| AI coding agent | Plan, implement and verify invariants | [AGENTS.md](../AGENTS.md), [Quick Reference](internal/QUICK_REFERENCE.md) |

## 1. Public Documentation (`docs/public/`)

This track is curated for clarity, actionable guidance and adoption without
exposing internal framework mechanics:

- [Getting started](public/getting-started.md): neutral offline first run with checked example files.
- [Neutral example](../examples/minimal-self-hosted/README.md): definition, synthetic answers and expected outputs.
- [CLI](public/cli.md): machine command usage and results.
- [Terminal](public/terminal.md): Terminal.Gui interaction.
- [Desktop](public/desktop.md): Avalonia workflow.
- [Browser](public/browser.md): transient complete/draft preparation, deliberate export and public-cache offline hosting.
- [Definitions](public/definitions.md): bounded data-only input.
- [Native output](public/native-output.md): protected save procedures.
- [Launch integration](public/launch-integration.md): target handoff and authority limits.
- [Packaging](public/packaging.md): platform commands and hosting limits.

## 2. Internal Engineering Documentation (`docs/internal/`)

This track is the engineering brain for maintainers and AI coding agents. It
contains technical truth, owner boundaries, state contracts and repository
constraints:

- [Quick Reference](internal/QUICK_REFERENCE.md): invariant router.
- [Governance](internal/GOVERNANCE.md): naming, ownership, metadata and contribution rules.
- [Definition contract](internal/CORE_DEFINITION_CONTRACT.md): admission/evaluation model.
- [CLI contract](internal/CLI_CONTRACT.md): machine interface.
- [Presentation state](internal/PRESENTATION_STATE.md): immutable workspace and operation generations.
- [Terminal client](internal/TERMINAL_CLIENT.md) and [Desktop client](internal/DESKTOP_CLIENT.md): surface adapters.
- [Protected artifacts](internal/PROTECTED_ARTIFACTS.md): create-only native protection.
- [Launch contract](internal/LAUNCH_CONTRACT.md): preparation versus apply boundary.
- [Packaging](internal/PACKAGING.md): evaluated release contracts and package closure.
- [Testing](internal/TESTING.md) and [Operations](internal/OPERATIONS.md): owner-scoped execution and evidence.
- [Release Runbook](internal/RELEASE_RUNBOOK.md) and [release tooling](../eng/release/README.md): local validation and conditional signed flow.
- [IP Governance](internal/legal/IP_GOVERNANCE.md): clean-room and dependency procedures.
- [Definition/runtime ADR](internal/adr/ADR-definition-runtime-boundary.md): architectural decision.
- [Transfer provenance](internal/extraction/PROVENANCE.md): authorized first-party transfer and exclusions.

## Dual-Documentation Parity & Separation Protocol

To prevent public guidance and technical truth from drifting:

> Any change impacting external behavior (definition contracts, CLI results,
> native saves, client workflows, launch integration or packaging) must update
> both applicable tracks in the same pull request:
> 1. Public guide: adopter-friendly task steps, commands and interaction.
> 2. Technical anchor: actual C# ownership, admitted contracts, invariants,
>    concurrency and recovery mechanisms.
> Both fulfill distinct responsibilities without duplicating content or
> cluttering each other's audience.

See [Documentation Architecture](internal/DOCUMENTATION_ARCHITECTURE.md) for
intent categories, document ownership, metadata, source anchors, docs-impact
evidence and the actual public/internal twin matrix.
