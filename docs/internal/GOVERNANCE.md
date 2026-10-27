# Code Conventions & Governance

> **Audience:** Contributors | AI agents
> **Status:** Implemented engineering policy
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-08
> **Source Anchors:** `AGENTS.md`, `PROJECTS.md`, `Directory.Build.props`, `src/`

This document preserves the reusable governance sections from Event revision
`c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38`, adapted to Configurator's offline
owners. Backend repository, HTTP, authentication and database rules are not
Configurator requirements.

## Critical Rules Summary

Read [QUICK_REFERENCE.md](QUICK_REFERENCE.md) for the invariant router and
[../../AGENTS.md](../../AGENTS.md) for the contribution contract. Source code
must be self-documenting; dependencies retain their own terms; checks target
the affected owner and report actual evidence.

## Naming Conventions

### General Rules

| Element | Convention | Example |
|---------|------------|---------|
| Public members | PascalCase | `Title`, `Generation` |
| Private fields | _camelCase with underscore | `_workspace`, `_writer` |
| Parameters | camelCase | `definition`, `outputPath` |
| Constants | PascalCase | `DefaultPageSize` |
| Interfaces | IPascalCase | Interface names in the owning project |
| Async methods | Suffix with Async | `SaveAsync()` |

## File Organization

### Solution Structure

`Configurator.slnx` registers the product owners, focused tests and engineering
tools. Keep production source under `src/Configurator.<owner>/`, focused suites
under `tests/Configurator.<owner>.Tests/`, and persistent engineering tools in
`eng/`. Do not place developer utilities in `.ci/scripts/`, which is for CI
pipelines. Follow the existing folder and naming conventions within each owner
rather than inventing another feature framework.

### Header & File Metadata Policy (Natural Metadata & Code Retirement)

The source repository previously mandated a two-line `ABOUTME:` comment summary
on source files under the theoretical premise that agents would inspect the top
two lines before loading full files. In practice, modern AI agent workflows
discover code via knowledge graphs, text search, and architecture folder/naming
conventions rather than sequential file-peeking. The synthetic comment prefix
generated text-search pollution, comment rot, and tooling friction.

#### 1. Retirement of ABOUTME for Source Code

- **No Headers on Code**: The mandatory `ABOUTME:` convention is retired for all
  source code (`.cs`, `.razor`, `.css`, `.csproj`, shell scripts).
- **Agent Instruction**: AI agents must never generate `ABOUTME:` comments in new
  or modified source code files.
- **Legacy Files**: Existing legacy `ABOUTME:` lines in source code do not require
  immediate churn and may be cleaned up opportunistically.
- **Standard Code Documentation**: Use idiomatic C# XML documentation comments
  (`/// <summary>`) for complex public APIs, invariants, or non-inferable
  architectural behavior.

#### 2. Natural Documentation Metadata

Documentation files rely on standard, human-readable metadata structures rather
than synthetic comment tags:

| Documentation Boundary | Format | Location | Purpose |
|---|---|---|---|
| Public Documentation (`docs/public/**`) | YAML frontmatter `description: "..."` where used | Top of file | Page subtitle and search summary; exempt from AI comment tags |
| Internal Engineering Docs (`docs/internal/**`) | Markdown blockquote metadata (`> **Audience:** ...`) | Immediately below title | Audience, status, owner, verification date and source anchors |
| Agent Skills (`.agents/skills/**/SKILL.md`) | YAML frontmatter `description: "..."` | Top of file | Just-in-time harness discovery |
| Architectural Decision Records (`docs/internal/adr/**`) | Standard ADR title and context metadata | Top of file | Context, options and decisions |

## Layer Responsibilities

### What Goes Where

| Concern | Owner | Location |
|---------|-------|----------|
| Bounded data-only definition admission and evaluation | Core | `src/Configurator.Core/` |
| Protected native output | Artifacts | `src/Configurator.Artifacts/` |
| Immutable workspace state and operation generations | Presentation | `src/Configurator.Presentation/` |
| Machine command-line adapter | CLI | `src/Configurator.Cli/` |
| Terminal.Gui interaction | Terminal | `src/Configurator.Terminal/` |
| Avalonia interaction | Desktop | `src/Configurator.Desktop/` |
| Static Blazor WebAssembly with transient local answers | Browser | `src/Configurator.Browser/` |

### Dependency Rules

Clients depend on Presentation/Artifacts and Core; Browser references
browser-safe Presentation/Core, never native Artifacts, and ships public static
data. Public caches contain no answers or output. No sibling Event checkout, backend contract or
running service is a build input. The [project map](../../PROJECTS.md) owns
project references and protected paths.

`StandaloneArchitectureTests` checks the exact first-party assembly-reference
set in each of the eight authored runtime owners, including the static host.
The sets are deliberate
contracts, not a discovered inventory of whatever happens to build:

| Compiled owner | Complete allowed `Configurator.*` references |
|---|---|
| Core | None |
| Artifacts | Core |
| Presentation | Core |
| CLI | Core, Artifacts |
| Terminal | Core, Artifacts, Presentation |
| Desktop | Core, Artifacts, Presentation |
| Browser | Core, Presentation |
| Browser.Host | None |

Core owns the definition contracts without depending on adapters. Artifacts
adds protected native writes over Core; Presentation adds shared workspace
behavior without native writer authority. CLI consumes Core and Artifacts
directly, not the interactive Presentation owner. Terminal and Desktop consume
all three shared owners, including compiled Core references reached through
transitive project references. Browser consumes only the browser-safe pair;
its separate native-framework exclusion remains enforced. No client may depend
on another client. The optional static Browser host remains transport
infrastructure, with no configuration-engine or native-artifact reference
authority. Its separate empty set prevents a future hosting change from silently
adding another engine or writer owner.

The guard rejects both extra and missing first-party references. Its canaries
persist synthetic assemblies in memory with fields typed to real first-party
contracts, then pass their PE metadata through the same verifier as product
assemblies. Each owner has an accepted allowed-only control and a forbidden
repository-local edge: Core to Artifacts, Artifacts to Presentation,
Presentation to Artifacts, CLI to Presentation, Terminal or Desktop to CLI,
and Browser or Browser.Host to Artifacts. The negative assertion requires the specific rejected
edge, so an unrelated failure cannot satisfy it. No source text or class-name
inventory supplies the boundary evidence.

This assembly-edge policy does not remove the deliberate Linux directory-source
acquisition exception inside Core's composition owner. The existing Core guard
qualifies I/O calls by reviewed assembly and exact
`Configurator.Core.Composition` namespace; its separate public-contract checks
remain authoritative. This is not permission for a native Artifacts dependency,
arbitrary ambient I/O, or a claim that the directory path is browser portable.
Relocation remains a focused follow-up. Compiled edge checks also do not prove
published package closure; that gate runs separately against actual publications.

Local preparation never grants a host's apply or admin authority. Keep the
definition/runtime boundary in the
[ADR](adr/ADR-definition-runtime-boundary.md), admission contract in
[CORE_DEFINITION_CONTRACT.md](CORE_DEFINITION_CONTRACT.md), and output protection
in [PROTECTED_ARTIFACTS.md](PROTECTED_ARTIFACTS.md).

## Design Principles

### SOLID Application

| Principle | How We Apply It |
|-----------|-----------------|
| Single Responsibility | Core evaluates definitions; Artifacts protects writes; clients adapt interaction |
| Open/Closed | Extend the bounded declarative contract through reviewed owner changes, not executable definition plugins |
| Liskov Substitution | Adapters honor the same admitted-definition and protected-output contracts |
| Interface Segregation | Small, focused contracts at actual owner seams |
| Dependency Inversion | Policy remains in its owner; client infrastructure does not enter Core |

### Additional Principles

| Principle | Application |
|-----------|-------------|
| DRY | Share workspace and validation behavior across native clients |
| KISS | One responsibility per class |
| YAGNI | Don't add abstractions until needed |
| Explicit > Implicit | Clear initialization; obvious dependencies |
| Fail Fast | Validate early; return errors immediately |

## Pattern Selection Guide

### When to Use Each Pattern

| Scenario | Pattern | Reasoning |
|----------|---------|-----------|
| Definition admission or evaluation | Existing Core contracts | One bounded data-only authority |
| Native output | Artifacts boundary | No insecure save fallback |
| Cross-client workspace behavior | Presentation state | Shared generation fencing and immutable state |
| Client interaction | Surface adapter | Keep framework-specific mechanics out of Core |

### When NOT to Use Patterns

Do not add generic decorators for one-off logic, abstractions without a second
real use, or a backend repository/handler framework to the offline utility.
Keep single-use capability logic in its existing owner.

## Primary Record-Selection Policy

Use records for handwritten contracts whose meaning is immutable data with value
equality. Apply changes by actual owner, preserving Core, Artifacts, Presentation
and client boundaries rather than importing Event's backend migration sequence.

### Selection

- Use a positional record for a short, stable contract with semantically distinct
  parameters.
- Use a nominal record with `init`/`required` members for long or optional
  contracts, named-construction safety or attributes.
- Use a `readonly record struct` only for a small, self-contained value with
  correct copy/value semantics; use a sealed record class for reference-bearing
  value data where appropriate.

### Intentional Classes

Retain classes for services, mutable UI/component state and framework-owned
shapes requiring mutation or identity. Do not turn adapters or resource lifecycles
into value records merely to satisfy a syntactic preference.

### Immutability, Equality, and Logging

Records are shallowly immutable. Published collection-bearing handwritten
contracts must expose read-only/immutable members and defensively copy mutable
inputs. Verify consumed equality and caller-mutation isolation. Record-generated
diagnostic printing is not a privacy boundary: never interpolate private answers,
tokens or free text into logs. Immutable state is distinct from public-safe state.

### Trusted Authority

Definition labels and identities are not authentication evidence. Prepared
configuration is not host acceptance, apply permission or administration
authority. Preserve the separate target authority in the launch contract.

### Executable Contract Classification And TDD

Derive contract-policy evidence from compiled types, serialization behavior and
generator-owned machine contracts, not historical source inventories or prose.
Before behavioral conversions, test consumed equality, one-fact `with` variants,
caller-mutation isolation, construction/serialization and relevant trust
boundaries. Do not invent Configurator equivalents of absent Event generated
client manifests or migration baselines.

## Code Quality Standards

### Required in Every File

- File-scoped namespace for C#.
- All necessary using statements preserved.
- Async suffix on async methods.
- CancellationToken passed through where the contract supports cancellation.
- Fix compiler/analyzer diagnostics rather than suppressing them.
- Format only touched files; avoid unrelated whitespace churn.

## CI/CD Governance

Keep required workflow/job names stable unless branch protection is migrated in
the same change. Required checks must be always-present or have a documented
no-op path; do not make a path-skipped workflow required without a wrapper gate.
Regenerate schema and dependency-lock artifacts through their documented native
generator, never by hand. Forge approval, branch restrictions, secrets, scanning
and push protection are repository/organization settings, not product config.

Retained release validators do not prove publication activation. See
[RELEASE_RUNBOOK.md](RELEASE_RUNBOOK.md) for local fixture execution, evidence
chaining and the independent Configurator authority requirement.

## Related Documentation

- [Quick Reference](QUICK_REFERENCE.md)
- [Documentation Architecture](DOCUMENTATION_ARCHITECTURE.md)
- [Testing](TESTING.md)
- [Operations](OPERATIONS.md)
- [IP Governance](legal/IP_GOVERNANCE.md)

## AI Contribution Routing

Every change routes through the Contribution Contract before editing. Resolve
intent, authoritative rules, must-read files, allowed paths, minimum tests,
documentation impact, review evidence and actions requiring approval from
[AGENTS.md](../../AGENTS.md) and
[the intent registry](../../.agents/contract/intents.yaml).

### Reader-First Technical Communication

Commit messages, PR descriptions, planning summaries, progress/completion reports,
and feedback or decision requests must lead with concrete meaning: who is
affected, what changes, and why it matters. Then explain the mechanism, naming
the important components, contracts, constraints, and evidence. Internal
engineering work should state its engineering benefit rather than inventing an
end-user benefit.

Explain technical terms in context; do not make the developer translate internal
vocabulary or open a plan to understand the outcome or choice. Improve readability
by adding explanations, not by deleting terminology, mechanisms, rationale,
constraints, or evidence. Prefer a longer complete account over a shorter account
that loses information. Preserve exact fields, commands, recovery requirements,
security/privacy consequences, and unverified work. Organize final verification
rather than copying an execution diary; length follows the substance, not an
arbitrary brevity target.

Use the shared
[reader-first writing guide](../../.agents/skills/conventional-commit/resources/reader-first-writing.md)
for commit examples, PR structure, reporting, decision briefs, and the
meaning/technical-detail review.

### Context Maintenance

Retrieve the smallest relevant heading, symbol or bounded range once. Reuse
unchanged context; reread only when source changes or a concrete decision lacks
evidence. Keep task-owned working memory under `dev/active/<workstream>/`;
durable reusable findings belong in `dev/_journal/`. Benchmark scenarios in
`.agents/benchmarks/cold-start-tasks.yaml` measure cold-start agent success.

If a path rule conflicts with authoritative target documentation, reconcile it
under the root contract. Historical Event-only examples never override
Configurator owners or introduce absent runtime requirements.
