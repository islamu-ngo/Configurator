> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.

<!-- ABOUTME: Completion and self-review gates for implementation-plan workstreams. -->
<!-- ABOUTME: Prevents unverified claims, vague tasks, artifact drift, and accidental implementation claims. -->

# Quality Gates

## Intake Gate (I-VSD & Grill-Me)

- `islamic-value-sensitive-design/workstreams/i-vsd-<task-name>.md` exists, contains `Last Updated: YYYY-MM-DD`, and is linked from the task-owned plan, context, and tasks files.
- The I-VSD report follows `planning` mode, names its workstream, is `current`, and has a `plan-aligned` disposition after the completed triad was revalidated.
- The I-VSD report traces applicable principles, stakeholders, provider-controlled decisions, evidence, stable finding/mitigation IDs, uncertainty, refresh triggers, and escalation boundaries.
- Every material `IVSD-*` ID maps in plan Section 9 to a named scenario/task, explicit non-applicability, or named escalation gate; the same report path and status appears in plan, context, and tasks.
- Architectural, product, failure-mode, and edge-case ambiguities were first resolved from repository evidence, then decided through `grill-me` with recommendations rather than filled with assumptions.
- Any conditional `robin-neutral` technology or architecture comparison is recorded in plan Section 5 and remains separate from the I-VSD assessment.

## Evidence Gate

- Every existing path, project, symbol, route, contract, setting, and test named in current state was verified by search and relevant file reads.
- Missing items are labeled not found or new; they are never described as implemented.
- Evidence entries separate repository facts, source-derived constraints, decisions, assumptions, and unknowns.
- Current-state strengths and pain points both cite concrete evidence.
- Related active or paused work was checked for overlap and conflicts.

## Contract Gate

- Every relevant implementation intent was captured.
- **Change Classification** is explicitly declared (`Behavioral Delta` vs `Non-Behavioral Delta`).
- Intent docs, skills, rules, scope, tests, docs impact, acceptance criteria, and forbidden moves are reflected in the plan.
- The **Release, Changelog, And Phase Commit Strategy** is classified. Every phase has planning-authored declarative commit metadata (type, scope, title, description, changelog treatment, trailers, and commit paths) validated against `conventional-commit`; no placeholder remains. For large phases (touching dozens or hundreds of files across multiple concerns), planning sequences multiple atomic commit contracts rather than one monolithic umbrella commit.
- Security, authorization, privacy, abuse, multi-tenancy, federation, localization, accessibility, product, observability, operations, and migration are each classified with rationale.
- Clean Architecture ownership and API/HAL/BFF trust boundaries are explicit where applicable.
- **Greenfield Breaking Change Freedom**: Backward-compatibility shims, deprecated route aliases, and legacy adapters are forbidden in pre-v1. Cleanly break and replace obsolete structures.

## Executability & Invariant-First Gate

- The plan reports current state before proposed future state.
- **Behavior vs. Code Separation**: Section 3 defines observable system behavior using RFC 2119 keywords (`SHALL`/`MUST`) and concrete `WHEN`/`THEN` scenarios without polluting behavioral contracts with internal class names or library choices.
- **Invariant-First Slicing**: High-criticality slices (money, concurrency races, aggregate state transitions, security boundaries) sequence authoring failing invariant tests (Task N.1: Red Phase) directly bound to named Section 3 Scenarios *before* implementing production logic (Task N.2: Green Phase). Standard feature/CQRS orchestration and UI slices implement directly and verify against public contracts.
- **Strict Quality Over Quantity in Tests**: Tests MUST NOT use mock-mirroring (`NSubstitute.Received(1)` on internal repositories/caches), framework-testing boilerplate (testing EF Core cancellation), or raw C#/CSS text scraping. Tests assert observable outcomes, state transitions, and RFC 7807 ProblemDetails.
- **Atomic Verification Criteria**: Every single task checkbox in `tasks.md` states its concrete verification assertion (`- [ ] N.M ... and verify ...`).
- **Strict Deferrable Open Questions**: Unknowns in Section 2.6 contain only genuinely deferrable details; non-deferrable branches were resolved during intake.
- Every task names exact files or a bounded investigation that will discover them.
- Every task includes observable acceptance criteria, dependencies, effort, and required guidance.
- Phases are reviewable slices with rollback or failure-diagnosis guidance.
- **Clean Architecture Slicing & 3-Ring Progressive Verification**:
  - **Ring 1 (Inner Loop)**: Fast in-memory class slices at the actual Core, Presentation or other affected owner; no timing promises or Event project names.
  - **Ring 2 (Phase Exit)**: One affected-owner Release build/check from `PROJECTS.md`. Prose-only phases use links, metadata and whitespace instead.
  - **Ring 3 (Workstream Exit)**: Relevant solution wiring, generated schema/ratchet freshness and actual package/boundary checks once; no Event database/provider matrix.
- **Yak-Shaving Quarantine Rule**: Do not absorb unrelated test rot. Establish pre-existence from available untouched-baseline evidence without unauthorized branches/worktrees; otherwise label attribution unverified. Record and quarantine proven failures, then proceed within scope.
- Tests are specified against public contracts (native CQS requests, HTTP routes, ProblemDetails RFC 7807, database states) rather than private implementation details.
- When history work is requested, phases contain self-sufficient atomic commit contracts following `conventional-commit`, with literal owned paths. Execute only under explicit task authorization in the assigned checkout; never force a feature branch or change unrelated edits.
- Message overrides are limited to explicit user-driven outcome changes, atomic phase splits, material implementation divergence, changed breaking/change-fragment classification, or a planned message that became factually false. Stylistic preference is never sufficient.
- Only an allowed override loads `conventional-commit`; every resulting actual contract repeats the declarative schema before any commit executes.
- Phase-attributable failures block the commit and must be resolved before phase completion.
- Plan real-surface checks when changed user-visible behavior requires them;
  do not ban browser/native QA or add unrelated service startup. No Aspire
  requirement exists for the offline utility.

## Continuity Gate

- The stable task name and `Last Updated: YYYY-MM-DD Europe/Brussels` appear in all three files.
- Plan, context, and tasks agree on status, current priority, next action, blockers, task ids, decisions, risks, and verification.
- Plan, context, and tasks agree on I-VSD path, status/disposition, CTO-review status, and user-approval status.
- **Dev-Doc Triad Single Responsibility**: `plan.md` defines architectural phase exit criteria without embedding granular task execution checklists, checkboxes (`- [ ]`), or session handoffs; `tasks.md` is the sole hot execution ledger; `context.md` is the ephemeral session memory and handoff log.
- Context puts resume state and blockers near the top.
- Tasks are checkable and mirror the plan's phases.
- The implementation-agent contract makes `tasks.md` the hot ledger, batches task updates at phase milestones, and separates implementation completion, verification disposition, and the phase commit.
- Every phase records exact owned paths and proportionate verification. Include commit metadata/checks only when requested; do not require a clean shared checkout by removing unrelated edits.
- Executive summaries, planned commit messages, and decision briefs pass the [shared readability check](../../conventional-commit/resources/reader-first-writing.md#readability-check): concrete meaning first, sufficient technical detail next.
- Context and plan update triggers are narrow enough to prevent documentation churn, while a dated handoff remains mandatory before pause or transfer.
- Resume guidance reads tasks first and only relevant plan sections, avoiding repeated full-workstream rereads.

## Scope Gate

- Planning artifacts are the only product of this workflow.
- Runtime/application code was not changed.
- The response does not claim the feature or refactor was implemented.
- Unknowns that materially affect design are visible and assigned to investigation or user review.
- The workstream is ready for user correction/approval before implementation begins.
- No artificial test-only, documentation-only, QA-only, reporting-only, or verification-only phase was added.
- No final-only catch-all commit task or separate commit-only session is required.
  Commit execution still requires explicit task authorization; plan approval
  alone does not authorize branch changes, upstream writes or history rewrites.

## Verification

Planning artifacts in `dev/active/<task-name>` are gitignored local working memory; they are accessed and verified directly using native harness file tools by deterministic path.

For prose-only skill changes, check metadata, links, and readability, then run:

```bash
git diff --check -- .agents/skills .agents/contract
```

Validate changed machine-consumed manifests with their repository parser; run product tests only when executable behavior changed.

Run the planned implementation test suite only during implementation, not while producing the plan. Record known baseline failures honestly in context and tasks.

## Final Response (Self-Contained Executive Plan Brief)

The final response presenting the plan MUST be a **Self-Contained Executive Plan Brief** so the user can understand, review, and approve the plan directly in chat without being forced to open `dev/active/<task-name>/`:

```text
Created/updated implementation plan for `<task-name>`:
- dev/active/<task-name>/<task-name>-plan.md
- dev/active/<task-name>/<task-name>-context.md
- dev/active/<task-name>/<task-name>-tasks.md

### Executive Summary & Architectural Approach
<Concrete problem and what will change for users/operators or engineering work.
Then explain the core technical approach and its important constraint.>

### Phase Roadmap
- **Phase 1: <Descriptive Name>** — <Concrete result, then components and behavior to verify>
- **Phase 2: <Descriptive Name>** — <Concrete result, then components and behavior to verify>
- ...

### Key Decisions & Trade-offs
- <Choice and practical consequence, followed by the technical rationale>

### Potential Risks & Hardest Unknowns
<Short evidence-grounded paragraph naming the hardest unresolved area or edge case.>

### Recommended Next Step & Approval Request
<Explicit call for approval or specific direction to proceed with Phase 1, with options if applicable.>
```

Do not say implementation started. If re-baselining, summarize what materially changed in the planning artifacts.
