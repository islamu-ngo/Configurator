---
name: work-criticality-matrix
description: Apply proportional intake, exploration and adversarial verification to Configurator work.
paths:
  - "src/**/*.cs"
  - "tests/**/*.cs"
  - "eng/**/*.cs"
  - "src/**/*.razor"
  - ".agents/contract/intents.yaml"
related_skills: [criticality-guardrail, epistemic-mad-review, grill-me]
related_docs: [AGENTS.md, PROJECTS.md, docs/internal/QUICK_REFERENCE.md, docs/internal/OPERATIONS.md]
minimum_tests: []
related_intents: [repository-tooling, definition-engine, protected-artifacts, client-interface, browser-session-privacy, agent-context, documentation, provenance-docs]
---

# Dynamic Work Criticality Matrix

Twin files at `.agents/rules/work-criticality-matrix.md` and `.omo/rules/work-criticality-matrix.md` must remain byte-identical.

> **Applies to:** Configurator source, tests, engineering C#, Razor and intent definitions.
> **Authority:** [AGENTS.md](../../AGENTS.md), [PROJECTS.md](../../PROJECTS.md) and [Quick Reference](../../docs/internal/QUICK_REFERENCE.md). Target authority overrides historical Event examples in transferred skills.

## Rules (Correct / Wrong)

| # | Rule | Correct | Wrong |
|---|---|---|---|
| 1 | **Proactive Intake Clarification** | Ask `/grill-me`-style questions before trust/privacy code only when material requirements remain ambiguous; use established contracts for routine details. | Invent admission limits, secret disclosure or overwrite authority; force an interview for already-settled requirements. |
| 2 | **Model Tier Selection** | Use advanced reasoning for security/privacy decisions, balanced implementation and economical discovery/UI/docs. | Spend advanced capability on atomic lookups or use a weak model for unresolved privacy judgments. |
| 3 | **Testing Done Right** | Establish failing Invariant-Breakers for admission, sensitivity, generations and create-only races before fixes. | Happy-path mock assertions that cannot expose the unsafe boundary. |
| 4 | **Exploration Depth** | Trace caller/callee and data flow from input admission through snapshot, preparation, output and actual sinks for high-risk work. | Read one local file while changing a cross-owner privacy boundary. |
| 5 | **Independent Deliberation** | Review unresolved high-risk assumptions with independent evidence and target-appropriate expertise; anonymize responses/weight evidence when MAD is actually selected. | Mandate Event backend personas, provider matrices or payment review for an offline utility. |
| 6 | **Zero Diagnostic Values** | Prove stable value-free output codes through exercised sinks. | Add telemetry/redaction machinery or export private values, tokens, paths and raw exceptions. |
| 7 | **Economical Path Autonomy** | Ordinary UI/CSS/docs use bounded reads and focused checks without friction. | Heavy debate and exhaustive intake for unrelated prose or CSS. |

## Must-Reads for This Path
- [AGENTS.md](../../AGENTS.md) - actual offline authority.
- [Quick Reference](../../docs/internal/QUICK_REFERENCE.md) and [Operations](../../docs/internal/OPERATIONS.md).
- [Criticality guardrail](../../.agents/skills/criticality-guardrail/SKILL.md) and its [adversarial archetypes](../../.agents/skills/criticality-guardrail/resources/adversarial-archetypes.md), only for affected trust/privacy decisions.
- [Privacy rule](privacy-and-pii.md), only when the changed behavior touches custody or disclosure.

## Anti-Patterns (Forbidden on These Paths)
- **Uniform Trust**: Treating definition admission/protected publication like cosmetic UI.
- **Mock-Heavy Security Verification**: Mocking the exact parser, generation fence or native publication mechanism whose failure the test should expose.
- **Value Leakage**: Private data in logs, machine errors, public metadata or previews.
- **Preparation As Authority**: Treating a locally prepared artifact or publisher label as target host acceptance, remote apply or administration.
- **Unrelated Runtime Gates**: Adding databases, BFF, payment systems or Event-only reviewers because a transferred example names them.

## Verification
- Use the affected owner Release build/suite in [PROJECTS.md](../../PROJECTS.md) and [Testing](../../docs/internal/TESTING.md).
- Trust/privacy Red once before the relevant fix; one targeted phase-exit check after the coherent slice. Preserve passes and repeat only failed/newly affected checks.
- Schema/link/whitespace checks for context-only work; no product builds/tests for prose.

## Related
- Skills: [criticality-guardrail](../../.agents/skills/criticality-guardrail/SKILL.md), [epistemic-mad-review](../../.agents/skills/epistemic-mad-review/SKILL.md), [grill-me](../../.agents/skills/grill-me/SKILL.md).
- Rules: [Tests](tests.md), [IP](ip-clean-room.md).
