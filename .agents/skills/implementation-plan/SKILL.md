---
name: implementation-plan
description: "Load when creating, updating or re-baselining a repository-grounded Configurator implementation plan and dev/active/<task> triad; not for product implementation or reviewing an existing plan."
type: workflow
enforcement: block
priority: high
---

## Rules

- Plan in the current assigned checkout without automatic Git synchronization,
  branch switching or worktree creation. Execution topology remains an explicit
  implementation decision, never a planning prerequisite.
- Keep one task-owned plan/context/tasks triad, using its existing filenames.
  The plan owns architecture, behavior and phase boundaries; tasks own granular
  checkboxes/verification; context owns quick resume, decisions and blockers.
- Preserve the full I-VSD planning mechanism: shared repository evidence,
  stakeholder/provider analysis, stable `IVSD-Fnnn`/`IVSD-Mnnn` IDs, mitigation
  mappings, freshness, refresh triggers and qualified escalation. Reports remain
  under `islamic-value-sensitive-design/`, not duplicated into the triad.
- A planning request supplies normal I-VSD intake agreement. Resolve material
  questions from repository evidence first; ask only choices that affect scope,
  responsibility, architecture or task ordering. Do not defer foundational
  decisions into an "open questions" section.
- Define observable SHALL/MUST and WHEN/THEN scenarios before implementation
  design. Label behavioral versus non-behavioral changes; code symbols belong
  in architecture, not a substitute for behavior.
- Configurator is unreleased: migrate real callers cleanly, without obsolete
  aliases or compatibility layers. Do not plan absent backend services.

## Workflow

1. Resolve the relevant intent and existing workstream. Reuse injected context
   and read only sources needed to establish current owners, flows and limits.
2. Build one evidence packet for technical and I-VSD intake. Distinguish verified
   paths/symbols, design decisions, assumptions, planned behavior and unknowns.
3. Apply I-VSD `planning` mode and resolve material intake branches. For a real
   architecture fork, record alternatives, practical consequences and rationale.
4. Write scenarios, current/future architecture and coherent phase slices with
   exact owned paths, dependencies, acceptance and proportionate verification.
5. Sequence failing privacy/concurrency/state-machine invariants before fixes;
   use sliced Core/Presentation or other actual owner tests, not Event projects.
   Plan a targeted Release build/check at phase exit and relevant final
   schema/package/boundary checks once. Prose needs no product build or suite.
6. Record release/docs impact and declarative commit metadata where requested;
   planned messages do not authorize Git operations. Include applicable
   knowledge graduation, without creating an unrelated catch-all phase.
7. Revalidate I-VSD findings against the completed triad. Every material ID maps
   to a scenario/task, justified non-applicability or named escalation gate.
   Keep report path, status/disposition and approval state consistent.
8. Present a self-contained plan brief with changed behavior, mechanisms,
   choices, checks and unresolved risks. Stop before product implementation.

## Resources

Retrieve only the resource for the current stage; do not preload the library.

- [Resource index](resources/index.md) - investigation, templates and quality gates.
- [Investigation workflow](resources/investigation-workflow.md) - shared evidence packet and intake sequence.
- [Plan template](resources/plan-template.md) - complete planning sections.
- [Quality gates](resources/quality-gates.md) - final triad review.
- [I-VSD](../i-vsd/SKILL.md) and [integration contract](../i-vsd/resources/integration-contract.md) - planning evaluation and final revalidation.
- [Grill-me](../grill-me/SKILL.md) - only material unresolved choices.
- [Historical Event planning](resources/event-planning-reference.md) - preserved original rationale and examples, not active target requirements.

## Verification

Validate links, source references, triad/I-VSD mappings, metadata and whitespace.
Planning does not execute the planned product suites or prove runtime behavior.
