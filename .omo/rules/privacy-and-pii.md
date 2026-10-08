---
name: privacy-and-pii
description: Apply when editing offline sensitivity, private native output, workspace disclosure, or public browser boundaries.
paths:
  - "src/Configurator.Core/**/*.cs"
  - "src/Configurator.Artifacts/**/*.cs"
  - "src/Configurator.Presentation/**/*.cs"
  - "src/Configurator.Cli/**/*.cs"
  - "src/Configurator.Terminal/**/*.cs"
  - "src/Configurator.Desktop/**/*.cs"
  - "src/Configurator.Browser/**/*.cs"
  - "src/Configurator.Browser/**/*.razor"
related_skills: [criticality-guardrail, clean-architecture-rules]
related_docs: [AGENTS.md, docs/internal/QUICK_REFERENCE.md, docs/internal/CORE_DEFINITION_CONTRACT.md, docs/internal/PROTECTED_ARTIFACTS.md, docs/internal/PRESENTATION_STATE.md, docs/internal/LAUNCH_CONTRACT.md]
minimum_tests: []
related_intents: [definition-engine, protected-artifacts, client-interface]
---

# Privacy and PII Rules (Tier 2 - Privacy)

Twin files at `.agents/rules/privacy-and-pii.md` and `.omo/rules/privacy-and-pii.md` must remain byte-identical.

## Applies To
- Core, Artifacts, Presentation and native/browser adapter C#; browser Razor.

## Critical Rules & Invariants

| # | Rule | Correct | Wrong |
|---|---|---|---|
| 1 | **Sensitivity Floor** | Preserve the strongest nested sensitivity; all answer-derived artifacts are `Restricted`. | Trust a public label or projection to demote private answers. |
| 2 | **Authority Before Publication** | Native adapter selects the destination; one-shot create-only writer commits protected output. | Let a definition filename grant filesystem authority or retry with insecure overwrite fallback. |
| 3 | **Anti-Resurrection Fencing** | Reject late completions after edit, cancellation, deactivation or disposal through captured generations/revisions. | Restore an invalidated private preview or stale prepared output into the current workspace. |
| 4 | **Atomic Native Publication** | Own preparation/cleanup and publish without partial visible output on the supported host. | Expose a partial private file or claim unsupported-host protection from mocked integration. |
| 5 | **Public-Only Browser** | Consume admitted registered public metadata with pinned revision/digest. | Add secret input, private preview/storage, target authentication or remote apply. |
| 6 | **Zero Values in Diagnostics** | Emit stable codes and bounded metadata only. | Emit answer values, tokens, raw exception messages or path contents in output, logs or errors. |
| 7 | **Owned Buffer Lifetime** | Snapshot inputs; clear/dispose prepared output when invalidated. Mask sensitive and secret fields in native rendering. | Keep caller-mutable collections or orphaned private buffers alive after invalidation. |

Configurator has no erasure database, receipt endpoint, telemetry/redaction stack or provider outbox. Historical Event purge/receipt requirements do not bind this offline boundary.

## Path-Specific Constraints
- **Dependency Direction**: Core stays independent of Artifacts, Presentation and client adapters. Native clients consume Presentation/Artifacts over Core; browser consumes Core only.
- **Cancellation**: Pass `CancellationToken` through asynchronous preparation/publication flows; cancellation also invalidates the captured operation generation.
- **Published Collections**: Snapshot caller-owned collections and bytes before publishing read-only/immutable state. Do not relabel mutable input as immutable or let caller mutation change validated output.

## Must Read
- [Quick Reference](../../docs/internal/QUICK_REFERENCE.md)
- [Core Definition Contract](../../docs/internal/CORE_DEFINITION_CONTRACT.md)
- [Protected Artifacts](../../docs/internal/PROTECTED_ARTIFACTS.md)
- [Presentation State](../../docs/internal/PRESENTATION_STATE.md)
- [Launch Contract](../../docs/internal/LAUNCH_CONTRACT.md)

## Verification
- Target the changed owner using [PROJECTS.md](../../PROJECTS.md); run its Release build and affected suite.
- Invariant-Breakers: malicious sensitivity demotion, caller mutation, stale completion after invalidation and competing create-only saves.
- Diagnostic evidence: exercise success and failure through actual output/diagnostic sinks using distinctive synthetic sentinel values; prove values, secrets and raw exception/path contents never appear. Do not substitute a redaction framework or source-text scan for observable evidence.
- Browser evidence: prove private inputs/artifact bytes cannot enter the public projection or storage path. Supported-host writer evidence is not a cross-platform certification.

## Related
- Intents: `definition-engine`, `protected-artifacts`, `client-interface`
- Rules: [Tests](tests.md), [Criticality](work-criticality-matrix.md)
