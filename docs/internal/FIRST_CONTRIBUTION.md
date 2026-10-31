# First contribution

> **Audience:** Contributors | AI agents
> **Kind:** Guide
> **Status:** Current tutorial; proposed example, not an executed contribution
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-30
> **Source Anchors:** `CONTRIBUTING.md`, `PROJECTS.md`, `docs/internal/DOCUMENTATION_ARCHITECTURE.md`, `eng/configurator/ValidateDefinitionDocumentation.cs`
> **Review basis:** Source and command inspection on 2026-10-30; tutorial commands were not executed

Make your first change a small explanation that a reader can verify against an
existing contract. You will identify the owner, improve one paragraph, check the
affected document and hand a reviewer the evidence. This tutorial uses a proposed
documentation edit, so it requires neither product changes nor a running client.

Read [Contributing](../../CONTRIBUTING.md) for contribution and licensing
expectations. Use the [developer guide](DEVELOPER_GUIDE.md) if the product's
owners are unfamiliar. For a code contribution instead, choose the matching
[worked recipe](CONTRIBUTOR_RECIPES.md) and follow its larger verification scope.

## 1. Define one reader problem

Use this synthetic practice task: "A contributor mistakes a test assertion for
proof that the test ran." The desired result is a sentence that distinguishes
source inspection from an execution receipt, with a direct route to the policy.

Before editing a shared checkout, inspect the target and existing changes using
your editor or authorized read-only tools. Do not replace another contributor's
paragraph or revert their work. Confirm the task's literal edit scope. A
permission to improve documentation does not authorize product, CI or history
changes.

For a larger behavioral proposal, discuss the intended outcome under
[Contributing](../../CONTRIBUTING.md) before implementation. An already approved,
fully specified task does not require another routine approval.

## 2. Locate the authority and edit owner

The [documentation policy](DOCUMENTATION_ARCHITECTURE.md#source-anchor-policy)
owns evidence distinctions. [Testing](TESTING.md#test-execution-versus-measured-coverage)
owns test/coverage interpretation. Contributor Experience owns this tutorial and
the other contributor guides. The native checker is
[`ValidateDefinitionDocumentation.cs`](../../eng/configurator/ValidateDefinitionDocumentation.cs);
it checks structure and links, not truth.

Read those bounded sections, then choose an explanatory page rather than
changing the policy merely to improve wording. For this practice task the
explanatory owner is [Developer guide](DEVELOPER_GUIDE.md), "Choose evidence that
answers the claim." Inspect it before editing: if it already answers the
question, do not manufacture a redundant change.

## 3. Write the smallest useful clarification

**Conceptual example edit, not an instruction to append duplicate prose.** If a
guide says only "Check the related test," replace that ambiguity with:

> Inspect the related test to identify its invariant. Record a pass only after
> running the selected command and retaining its exit code and test counts.

Link the explanation to
[test execution versus measured coverage](TESTING.md#test-execution-versus-measured-coverage).
Do not paste the full testing policy into the guide. Preserve mechanisms and
limits: inspection identifies assertions; execution establishes outcomes;
coverage requires separate measured evidence.

Follow the [style guide](DOCUMENTATION_STYLE_GUIDE.md). Internal primary docs use
natural metadata immediately below their title. Update `Last Verified` only
when source anchors were actually checked, and identify the review basis. A
grammar correction does not renew a source or runtime verification date.

If you change operator-facing behavior rather than explanatory wording, use the
[dual-documentation protocol](DOCUMENTATION_ARCHITECTURE.md#dual-documentation-parity-protocol)
to update its public guide and technical anchor in the same increment.

## 4. Check the actual changed document

**Runnable current checker command; source-inspected, not executed here.**
Prerequisites: POSIX shell at the repository root, SDK 10.0.302 from
[`global.json`](../../global.json), an existing writable agent temporary directory,
and the changed guide present in this checkout.

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet run --file eng/configurator/ValidateDefinitionDocumentation.cs -- \
  . docs/internal/DEVELOPER_GUIDE.md
```

The checker accepts explicit operands, including new untracked documents. Its
source prints `DOC_CHECK files=1 local_links=... errors=0` and returns 0 when the
selected document passes. The link count depends on the document; do not copy an
invented count into your handoff. Rerunning reads the document again without
changing it, although `dotnet run` can create local build/cache outputs.

For this prose change, do not run product tests or a product build. Inspect
headings, fenced excerpts, metadata, trailing whitespace and the actual reader
route as well. The checker does not request external URLs, validate reference
links as a full renderer would, or prove semantic accuracy. Its
[documented limits](../../eng/knowledge/README.md#bounded-link-checking-and-honest-failures)
remain part of the evidence.

If it reports `LINK_MISSING` or `ANCHOR_MISSING`, inspect the actual target or
heading and correct the link. If the source contradicts the claimed behavior,
follow the [disagreement procedure](DOCUMENTATION_ARCHITECTURE.md#resolve-disagreements);
do not change an intended contract merely to agree with a bug. Keep unrelated
historical failures separate rather than weakening the checker.

## 5. Hand the reviewer a self-contained result

Describe the reader problem and the corrected explanation before listing files.
Use this **conceptual handoff template**, replacing its fields with observations:

```text
Result: Contributors can distinguish source assertions from executed evidence.
Changed owner: the explanatory paragraph in docs/internal/DEVELOPER_GUIDE.md.
Docs impact: Updated; no operator-facing behavior changed.
Inspection: named source/contract sections and inspection date.
Executed: exact command, exit code and actual DOC_CHECK result.
Not executed: product suites/builds, because the change is prose only.
Limits: local links/metadata do not prove runtime behavior or external URLs.
```

If you did not run the command, write "Not executed" rather than filling in a
successful result. Use [AI review](../../AI_REVIEW.md) for final scope and evidence
checks, and [Contributing](../../CONTRIBUTING.md) for submission expectations.
Do not commit, switch branches, publish or send messages on another person's
behalf without the applicable authorization.

Your first contribution is complete when the explanation solves the stated
reader problem, its sources and links are accurate, proportional checks are
recorded honestly, and the reviewer can understand the result without opening a
plan or session transcript.
