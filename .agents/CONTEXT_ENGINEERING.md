# Context Engineering Contract

> **Audience:** Contributors | AI agents
> **Kind:** Living contract
> **Status:** Current Configurator retrieval and working-memory guidance
> **Owner:** Agent Context
> **Last Verified:** 2026-10-30
> **Source Anchors:** `AGENTS.md`, `PROJECTS.md`, `.agents/contract/intents.yaml`, `.agents/benchmarks/`
> **Review basis:** Repository routing and documentation reconciliation

Give the contributor the smallest decision-complete working set without
discarding engineering knowledge. [AGENTS.md](../AGENTS.md) owns contribution
authority; [PROJECTS.md](../PROJECTS.md) owns actual project and verification
boundaries. This document explains how to retrieve and retain their context.

## Human readability and bounded retrieval

Human guidance needs mechanisms, examples, rationale, exceptions and uncertainty.
Do not shorten it to meet a token target. Instead use descriptive headings,
focused source maps and task routes so an agent can retrieve the relevant part.
Skill entrypoints select workflows; detailed templates and checklists belong in
linked resources, loaded only for the current decision.

Configurator is unreleased. Do not preserve obsolete routes, aliases or adapters
for imagined compatibility commitments. Historical material remains available
as evidence; it does not constrain the current implementation merely because
it was once copied into a skill.

## Context ledger and retrieval order

Keep an in-session ledger of `path + heading/symbol + revision`. Reuse injected
instructions and unchanged evidence. Read again only after the source changes,
a decision lacks enough evidence, or a finding contradicts the earlier reading.

1. Match the task in [the intent registry](contract/intents.yaml), retrieving
   only relevant entries.
2. Locate the responsible owner with the [project map](../PROJECTS.md) or
   [documentation task hub](../docs/README.md).
3. Use a covered knowledge graph or language-server symbol query for code
   relationships; otherwise use bounded native reads and literal searches.
   Never invent graph coverage or results.
4. Read the smallest useful heading, symbol and representative test. For a new
   mechanism, inspect one relevant sibling pattern before designing another.
5. Expand only when a named decision remains unresolved. A complete audit can
   require the full corpus; an ordinary fix should not.

For cross-owner behavior, trace actual input through Core admission/evaluation,
Presentation revision/generation ownership, preparation and the native or
browser output boundary. Configurator has no Event API, BFF, database, payment,
tenant or outbox chain to reconstruct.

## Authority and disagreement

Classify a document before relying on it: living contract, guide, procedure,
decision, proposal, historical assessment, execution record or generated
reference. Compare scope, owner and reviewed revision. Neither a current bug nor
an approved but unimplemented plan establishes intended behavior.

Follow the [disagreement procedure](../docs/internal/DOCUMENTATION_ARCHITECTURE.md#resolve-disagreements):
record documentation drift, implementation nonconformance or an unresolved
design choice, then repair the correct owner. Preserve user strategy, historical
records, provenance, reviewed hashes and I-VSD finding identities.

## Budgets and delegation

The existing [cold-start scenarios](benchmarks/cold-start-tasks.yaml) own
`context_budget`: additional bootstrap bytes, discovery/result limits, duplicate
unchanged bytes and full-registry reads. Do not duplicate or relax those numbers
here. Keep large logs outside the prompt and return exact outcomes, first
actionable failures and evidence handles.

Use economical discovery for bounded inventories, balanced implementation for
settled work, and stronger reasoning for unresolved trust/privacy or cross-owner
decisions. Delegate an independent track when it reduces elapsed work; do not
delegate atomic lookups or duplicate another track's inspection. Give each track
literal ownership, its question/deliverable, result bound and stop condition.
The parent retains decisions, integration and validation of its own work.

## Verification scope

Resolve criticality through the matched intent and
[criticality rule](rules/work-criticality-matrix.md). Ask only material questions
not already answered by current authority. Approved, settled work continues.

| Work | Evidence needed |
|---|---|
| Pure prose/navigation | Links, anchors, applicable metadata and whitespace |
| Agent routing/rules | Relevant schema/intent references, discovery and physical twin parity |
| Executable knowledge tooling | Native self-tests and exercised commands |
| Definition/command examples | Explicit maintained synthetic fixtures and owning generator/consumer checks |
| Product behavior | Affected-owner baseline, invariant tests and real surface where behavior changed |

Use sliced in-memory invariants in the inner loop, an affected-owner Release
build/check at phase exit, and relevant final package/boundary gates once.
These are scopes, not latency promises. Admission, custody, concurrency and
state-machine fixes establish a failing invariant first. Tests use independent
oracles and pre-armed completion signals, not sleeps, prose assertions or mocks
that remove the behavior being tested.

Fix attributable failures. Quarantine unrelated failures with their evidence;
do not switch or create a checkout to prove a baseline without authorization.
No database/provider matrix or product suite follows from a documentation edit.
Exact commands, exits and omitted checks belong in the handoff. A passing parser
does not prove routing semantics, and a source/test read is not an executed pass.

## Workstream and handoff state

Use one existing task-owned `dev/active/<task>/` context and ledger. Cold resume
may need one full-plan read to recover cross-phase contracts; routine execution
uses the current context, active task and referenced heading.

- Plan: behavior, architecture, decisions and phase boundaries.
- Tasks: approved work and atomic verification criteria.
- Context: quick resume, blockers, evidence, owned paths and unrelated edits.

Keep context within the existing 200–300-line / 15-KB working-memory bound.
Compress completed milestones to checkpoints, graduate durable findings to the
journal and decisions to their owners, and use Git history rather than commit
dumps. A plan or recalled memory is not permission for an external/destructive
action. Do not create branches, worktrees, commits or publication by implication.

Questions and reports must be actionable without opening the plan: describe the
feature, choice, consequence, recommendation and immediate next step. Avoid bare
phase identifiers. Preserve existing approval and continue ordinary in-scope work.

## Multi-harness rule architecture

[AGENTS.md](../AGENTS.md) is the policy entrypoint. `CLAUDE.md`, `.cursorrules`
and GitHub Copilot instructions point to it, not to rival policies.
`.agents/rules/` and `.omo/rules/` contain physical byte-identical twins,
including their README and schema. Edit both; validate with native `cmp`.
Do not replace them with symlinks or read both unchanged copies into context.

Harness discovery priority is not authority. Path globs, `alwaysApply` and
descriptions determine discovery; the contribution contract determines what
governs. Deeper applicable path rules refine their scope without overriding
explicit user instructions. Historical backend rules use nonmatching
`reference-only` paths and cannot activate absent services.

Use native read/apply_patch and Bash/.NET execution. No ad-hoc Python/JavaScript
task scripts, secret-bearing fixture discovery or machine-local configuration
exports. Persistent engineering tools belong in `eng/`; `.ci/scripts/` remains
pipeline-owned. Optional hash-aware editing is a harness capability, not a
repository prerequisite or an excuse to replace native tools.

## Research, parity and measurement

Repository evidence comes first. External research answers a named unresolved
API, standard or design question and follows the repository's clean-room and
private-data boundary. Retain recoverable source identity without copying
restricted expression or inventing unresolved citation provenance.

The [documentation policy](../docs/internal/DOCUMENTATION_ARCHITECTURE.md) owns
public/internal parity. Update the affected operator explanation and technical
anchor together; no Event website synchronization or hosted publication follows.

Use the [native knowledge inventory and profiles](../eng/knowledge/README.md)
for original-content snapshots with paths, revision, status and exclusions.
Current profiles do not silently promote strategy or historical instructions.
[Cold-start benchmarks](benchmarks/README.md) measure navigation, correctness
and context consumption in fresh sessions; parsing scenario YAML does not run
them. Record unmeasured metrics rather than claiming a budget pass.

## Historical depth

The complete prior context guidance, including Event graph shapes, criticality
tables, provider matrices, evidence artifacts and harness examples, remains in
[the historical context reference](reference/event-context-engineering.md).
It explains provenance and earlier mechanisms. Its source-relative links and
runtime requirements are not current Configurator instructions.
