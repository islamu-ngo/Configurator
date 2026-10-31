---
name: implement-tasks
description: "Load when executing or resuming an approved Configurator plan in dev/active/<task> or an explicitly assigned worktree; not for plan authorship or review."
type: workflow
enforcement: suggest
priority: high
---

## Rules

- Execute the approved scope in the assigned checkout. No automatic fetch,
  checkout, branch, worktree, commit, rebase, push or PR follows from this skill.
  Existing task authorization controls each operation; a planned commit message
  alone is not authorization to record history.
- Resume the existing authoritative task folder; do not move or duplicate its
  plan/context/tasks. Preserve unrelated edits and local configuration.
- On cold resume, read current context, tasks and the plan once to recover
  cross-phase contracts. Thereafter read only the active heading or changed
  evidence. Follow existing plain or task-prefixed filenames.
- Keep `tasks.md` as the execution ledger, `context.md` as bounded current state,
  and `plan.md` as architecture/behavior. Check off work when completed; findings
  go to context or authorized backlog, not invented phases.
- Preserve I-VSD IDs, mitigations, report freshness and plan/task mappings.
  Material changes to provider responsibility or scope require revalidation
  through the existing I-VSD integration contract, not silent removal.

## Workflow

1. Resolve the first unchecked approved task, intent, literal owned files and
   observable acceptance. Reuse the context ledger and verify current source.
2. Establish the relevant baseline once before product edits. For admission,
   privacy, concurrency and state-machine fixes, prove a runtime-failing
   invariant before implementing the smallest coherent source/caller/test slice.
3. Use sliced in-memory checks during implementation, then the affected-owner
   Release build/check from `PROJECTS.md` at phase exit. Prose needs proportional
   metadata/link/whitespace checks, not product suites.
4. Fix direct regressions and minor mechanical caller ripples. Quarantine proven
   baseline failures; do not switch checkouts to prove them without authorization.
   For structural scope expansion, return a self-contained decision brief.
5. At workstream exit, run only relevant schema/package/boundary checks once.
   Cluster widespread failures by cause rather than adding individual tasks.
6. Graduate durable decisions/findings to their authorized owners. If commits or
   PRs are explicitly requested, use the existing release/commit contracts,
   literal owned paths and hooks. Do not rewrite history or force-push by default.
7. Report delivered behavior, mechanisms, exact evidence, unverified surfaces
   and residual issues. Stop when all approved acceptance conditions hold.

## Resources

- [Context engineering](../../CONTEXT_ENGINEERING.md) - retrieval, evidence and handoff discipline.
- [Project map](../../../PROJECTS.md) - actual owners and native verification.
- [I-VSD integration](../i-vsd/resources/integration-contract.md) - report/plan revalidation when responsibility or mitigation changes.
- [Commit conventions](../conventional-commit/SKILL.md) - only for authorized history work or planned message authorship.
- [Historical Event execution](resources/event-execution-reference.md) - preserved hub/spoke, failure-triage, lifecycle and deployment detail; not an active target gate.

## Verification

Check the approved task's actual behavior and affected owner, record exact
commands/results, and state omitted checks. Never infer a pass from a plan,
syntax parser, stale output or historical execution recipe.
