# Developer guide

> **Audience:** Contributors | AI agents
> **Kind:** Guide
> **Status:** Current orientation; source-inspected, not a runtime qualification
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-30
> **Source Anchors:** `PROJECTS.md`, `global.json`, `src/Configurator.Core/Definitions/DefinitionValidator.cs`, `src/Configurator.Presentation/Presentation/DefinitionFormModel.cs`, `src/Configurator.Artifacts/ProtectedArtifactBundlePublisher.cs`
> **Review basis:** Source and contract inspection on 2026-10-30; no build, test or client execution

Use this guide to locate the owner of a change before opening an editor. A good
contribution changes one observable behavior, preserves the boundaries around it,
and supplies evidence from the layer that owns that behavior.

For a guided first change, follow [First contribution](FIRST_CONTRIBUTION.md).
For a concrete task, choose a [worked recipe](CONTRIBUTOR_RECIPES.md).
For writing, use the [documentation style guide](DOCUMENTATION_STYLE_GUIDE.md).
[Contributing](../../CONTRIBUTING.md) owns contribution expectations;
[AGENTS.md](../../AGENTS.md) is the required entrypoint for coding agents.

## Understand what the product does

Configurator prepares candidate configuration artifacts from public, bounded,
data-only definitions and private local answers. It does not apply those
artifacts to a target application. The target still owns semantic acceptance,
authorization and activation.

Keep these objects distinct when describing or changing a flow:

| Object | Role | Custody consequence |
| --- | --- | --- |
| Maintainer source | Compact authoring-v2 YAML/JSON describing fields and outputs | Public source is not an answer file |
| Admitted definition | Canonical runtime content pinned by family, revision and digest | Labels and family IDs do not authenticate a publisher |
| Answer snapshot | Private values at one input revision, including explicit omissions | Never public cache or diagnostic content |
| Prepared artifact | Answer-derived candidate bytes | Restricted even when its definition uses public fields |
| Publication receipt | Installation, verification, completion and cleanup facts | Does not restore answers or prove target acceptance |

The [Core contract](CORE_DEFINITION_CONTRACT.md) owns admission and semantics.
The [Presentation contract](PRESENTATION_STATE.md) owns workspace state.
[Protected artifacts](PROTECTED_ARTIFACTS.md) owns native installation facts.
The [public glossary](../public/glossary.md) provides adopter-facing terminology.

## Follow the data to its owner

The [project map](../../PROJECTS.md) is the authoritative project and command
inventory. Use this smaller table to choose where to start, not as a second
project registry.

| Change | Start in | Inspect with |
| --- | --- | --- |
| Definition validity or composition | `src/Configurator.Core/Definitions/` and `src/Configurator.Core/Artifacts/` | `tests/Configurator.Core.Tests/Definitions/` |
| Workspace edits, snapshots or stale-result rejection | `src/Configurator.Presentation/Presentation/` | `tests/Configurator.Presentation.Tests/` |
| Protected create-only writes | `src/Configurator.Artifacts/` | `tests/Configurator.Artifacts.Tests/` |
| Machine invocation or JSON result | `src/Configurator.Cli/` | `tests/Configurator.Cli.Tests/` |
| Rendered browser interaction | `src/Configurator.Browser/Components/Configuration/` | `tests/Configurator.Browser.Tests/` |
| Desktop save and physical lifetime | `src/Configurator.Desktop/ViewModels/` | `tests/Configurator.Desktop.Tests/` |
| Terminal interaction and publication history | `src/Configurator.Terminal/` | `tests/Configurator.Terminal.Tests/` |
| Knowledge checks | `eng/configurator/ValidateDefinitionDocumentation.cs`, `eng/knowledge/` | Native self-tests documented by their owners |

Core does not depend on clients, native filesystem adapters or Event services.
Presentation supplies shared editing and immutable workspace state. Native
clients use Artifacts for protected output; Browser consumes browser-safe
Presentation/Core without native Artifacts. CLI consumes Core/Artifacts directly,
not Presentation. The optional Browser host serves public static files and
does not process answers.

An ordinary edit follows this sequence:

1. A source codec admits a definition before answer collection. An active form
   retains that admitted identity.
2. An editor makes a deliberate mutation through `DefinitionFormModel`.
   Core validation determines effective values and validity; a renderer does not
   implement its own requiredness or assertion evaluator.
3. Preparation captures an answer snapshot. Input revisions and operation
   generations determine whether a completion may become current.
4. A native adapter selects a destination and publishes restricted bytes through
   the protected writer. Browser export instead requires a deliberate user
   action and makes no native protected-save guarantee.
5. Edits or cancellation invalidate eligibility for current output. They do not
   undo a native installation. Receipts preserve established physical facts
   independently of current readiness.

See [Core mechanisms](CORE_DEFINITIONS.md), [CLI contract](CLI_CONTRACT.md),
[Browser ownership](BROWSER_UI.md), [Desktop ownership](DESKTOP_CLIENT.md) and
[Terminal ownership](TERMINAL_CLIENT.md) for their full flows and rejection paths.

## Prepare only the environment your change needs

Read [global.json](../../global.json) and [project commands](../../PROJECTS.md).
The inspected checkout pins SDK **10.0.302**, disables SDK roll-forward, and
selects Microsoft.Testing.Platform. Native command examples assume a POSIX shell
at the repository root. Use `TMPDIR="$HOME/.cache/agent-tmp"` with an existing,
writable cache directory. Do not replace the SDK pin or dependency locks to
make a local environment pass.

A prose-only change needs no product build or suite. A Core assertion needs an
in-memory owner test, not a running client or Event service. Browser Release
publication has additional workload prerequisites; native-host and package
checks have their own prerequisites. Follow [Testing](TESTING.md) and
[Packaging](PACKAGING.md) when your scope reaches those surfaces, rather than
installing every workload for an unrelated contribution.

Dependencies use central versions and generated locks. Schemas and other
generated outputs must be regenerated from their owning source. Consult
[Governance](GOVERNANCE.md) for conventions and [Quick reference](QUICK_REFERENCE.md)
for invariant routing.

## Choose evidence that answers the claim

Start with the smallest named test class in the relevant
[recipe](CONTRIBUTOR_RECIPES.md). Establish a failing invariant before fixing
domain, concurrency, privacy or trust behavior. Complete the coherent
source/caller/test/docs slice, then use the affected-owner phase-exit checks
from [Testing](TESTING.md#the-3-ring-progressive-verification-model).

| Evidence | What it establishes | What it does not establish |
| --- | --- | --- |
| Source inspection | The implementation and test assertions currently present | That they compile or pass |
| Focused behavioral test | The selected invariant in its test environment | Every owner or actual host |
| Component/headless test | Rendered semantics or adapter lifetime at that seam | Published browser performance or real desktop interaction |
| Actual-host exercise | Recorded scenario on the identified package and host | Other platforms or permanent integrity |
| Coverage report | Measured lines/branches for its bound run | Acceptance, correctness or unmeasured suites |

Use [Status](STATUS.md) to find qualification evidence and
[Operations](OPERATIONS.md) for dated execution records. A current guide's
inspection date does not renew those records.

## Finish a reviewable change

Check the applicable [AI review checklist](../../AI_REVIEW.md). Explain the
observed problem, changed owner, mechanism and observable result. Record exact
commands, exit codes, selected counts and omitted checks. Keep private values,
raw exception contents and private paths out of shared evidence.

For documented behavior, update the public guide and technical anchor together
under the [documentation policy](DOCUMENTATION_ARCHITECTURE.md). Record docs
impact as Updated, Not needed or Deferred with a concrete reason. Do not rewrite
historical reports to make them look current. Preserve unrelated shared edits.
Commits, branch changes and external publication require their own authorization.
