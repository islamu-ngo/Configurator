# Documentation style guide

> **Audience:** Contributors | AI agents
> **Kind:** Guide
> **Status:** Current writing guidance; documentation policy remains authoritative
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-30
> **Source Anchors:** `docs/internal/DOCUMENTATION_ARCHITECTURE.md`, `docs/internal/GOVERNANCE.md`, `eng/configurator/ValidateDefinitionDocumentation.cs`
> **Review basis:** Policy and checker source inspection on 2026-10-30; no validation command executed

Write so a reader can complete a task, understand the mechanism and judge the
evidence without reconstructing a conversation. Start with what the reader will
achieve, then explain prerequisites, steps, expected results and limits.

This guide applies the existing
[documentation policy](DOCUMENTATION_ARCHITECTURE.md); it does not introduce
another authority registry. Use the [developer guide](DEVELOPER_GUIDE.md) for
orientation, [First contribution](FIRST_CONTRIBUTION.md) for a tutorial and
[worked recipes](CONTRIBUTOR_RECIPES.md) for owner-specific examples.

## Give each page one job

Choose the reader's question using the
[intent model](DOCUMENTATION_ARCHITECTURE.md#intent-model). A tutorial teaches a
path once; a how-to completes a task; reference supplies exact facts; explanation
connects mechanisms and decisions. Link to a reference table rather than placing
every option in a tutorial.

Use descriptive task headings such as "Recover after interrupted publication."
Avoid headings that depend on plan IDs or imply an outcome not yet established.
Open a section with its conclusion, then support it with the relevant mechanism.
Use lists for parallel choices or ordered steps, and tables for comparisons.
Do not remove important exceptions or technical detail to shorten a page.

Keep public pages useful without internal C# knowledge. Internal pages should
name real classes, owners, state transitions and rejection paths. The
[public/internal parity protocol](DOCUMENTATION_ARCHITECTURE.md#dual-documentation-parity-protocol)
owns when both sides must change; do not duplicate whole manuals or raw
configuration between them.

## Point to authority rather than restating it

Use the [ownership registry](DOCUMENTATION_ARCHITECTURE.md#primary-documentation-and-owners)
to identify one accountable owner. Use [PROJECTS.md](../../PROJECTS.md) for
project boundaries and commands, [Testing](TESTING.md) for verification policy,
[Governance](GOVERNANCE.md) for coding conventions, and
[Status](STATUS.md) for routes to scoped qualification evidence.

Product claims link to their owning source or technical contract. A design
decision, proposal, guide and dated execution report answer different questions.
Resolve contradictions using the
[disagreement procedure](DOCUMENTATION_ARCHITECTURE.md#resolve-disagreements),
not document age alone or an assumption that source code cannot contain a bug.
Preserve historical report bodies and original attribution.

## Make metadata describe the actual review

Follow the [metadata policy](DOCUMENTATION_ARCHITECTURE.md#metadata-policy) for
the required fields and
[natural header conventions](GOVERNANCE.md#header--file-metadata-policy-natural-metadata--code-retirement)
for their format. New internal primary docs put natural blockquote metadata
immediately below the title. Source anchors must resolve to real files or
directories.

Use `Last Verified` for the date the cited source was checked. Add a review basis
when the distinction matters: "source inspection; no client execution" tells a
reader more than an unexplained fresh date. A current guide can describe an
unqualified platform. An accepted design can remain unimplemented. State that
boundary at the affected claim, not only in a distant footer.

Do not add synthetic comment headers. Do not normalize an old assessment's
metadata into a new approval or execution record.

## Distinguish examples from receipts

Apply the [example and evidence policy](DOCUMENTATION_ARCHITECTURE.md#examples-evidence-and-context-exports)
at each example's introduction:

| Label | Reader may assume | Writer must supply |
| --- | --- | --- |
| Runnable current | Complete command/input for the described environment | Working directory, prerequisites, effect, success signal and safe rerun |
| Partial source or result | An excerpt to insert or interpret in a named context | Owner, insertion point, dependencies and omitted context |
| Conceptual | A teaching sketch, not accepted syntax or a receipt | Intended lesson and the real implementation/reference route |
| Historical | A record of a past candidate or attempt | Original cutoff, host and limits without renewed qualification |

"Runnable current" describes completeness, not proof of execution. Separately
state whether the example was source-inspected or run, and on which date.
An abbreviated JSON object is not a valid input file. A sample successful
message is an expected signal, not observed stdout. Do not execute arbitrary
Markdown fences as a verification strategy.

For command blocks, name the shell and repository-root assumption. Copy exact
arguments from the inspected native owner. Product tests use the current MTP
selector form from [Testing](TESTING.md#run-commands), with actual class names
and `--minimum-expected-tests 1`. Do not leave a selector placeholder that looks
executable. A test count is not a coverage percentage or platform qualification.

## Use synthetic data with an explicit purpose

Use obviously invented values such as `sample-service`, `synthetic-private-key`
or `synthetic-inspection-fault`. Reuse inspected maintained fixture identity
when extending that fixture; a demonstration GUID is not publisher trust.
Do not open private fixtures or copy real credentials, connection strings,
private paths, customer configuration or answer bytes into prose.

If an example needs a placeholder, name it outside the runnable block and state
the allowed replacement. Use existing fixture builders for code excerpts.
Do not present angle-bracket tokens as literal CLI arguments. Public definition
source and private answer files are different inputs; identify which is shown.

A diagnostic example should retain a stable code and safe coordinates, not an
exception message or private value. For a private-value read used by a test,
explain that it is local test evidence and must not become machine output.

## Explain time, failure and custody in order

For an asynchronous flow, describe who captures state, what invalidates it, who
owns the bytes during work and who may publish completion. Include rejection
and cancellation paths. If a diagram helps, keep equivalent numbered text next
to it so the page works without Mermaid rendering.

Use precise terms:

- Preparation produces candidate bytes; it does not grant target acceptance.
- Input revision identifies answer state; operation generation fences current
  completion; physical lifetime determines when borrowed storage can be cleared.
- Native installation and immediate verification are independent facts.
  Cancellation or a failed receipt does not establish rollback.
- Browser export is deliberate custody transfer, not native protected save.

Link the responsible contract rather than inventing a stronger guarantee.
Write recovery steps for commands that create files: preserve established
outcomes, avoid overwriting conflicts, and use a fresh private destination where
the native-output procedure requires one.

## Review the page as a reader

Follow [documentation-only review](DOCUMENTATION_ARCHITECTURE.md#review-a-documentation-only-change)
and the [native checker limits](../../eng/knowledge/README.md#bounded-link-checking-and-honest-failures).
Check explicit local paths, supported heading fragments, balanced fences,
metadata, whitespace and the route from task to owner to evidence. A link
checker cannot decide whether the explanation is correct.

Record docs impact under the
[existing contract](DOCUMENTATION_ARCHITECTURE.md#docs-impact-contract).
Report executed checks with their exact result; label omitted checks and why.
Pure prose needs no product build or tests. Keep full historical-audit failures
separate from current validation. Do not fix them by weakening checks or
rewriting immutable evidence.
