# Documentation architecture migration

> **Audience:** Maintainers | Contributors | AI agents
> **Kind:** Execution record
> **Status:** Documentation migration record; product qualification remains separately scoped
> **Owner:** Contributor Experience
> **Evaluation Date:** 2026-10-30
> **Last Verified:** 2026-10-30
> **Inspected Revision:** `acc25866ba7ead9b87967a94210beb854072c244` plus the shared working tree
> **Source Anchors:** `docs/internal/DOCUMENTATION_ARCHITECTURE.md`, `eng/knowledge/`, `.agents/benchmarks/documentation-navigation.yaml`

## Result and boundary

The correction makes current authority, reader tasks, mechanisms and evidence
independently discoverable. It changes documentation, agent guidance and native
knowledge tooling, not product behavior. Work occurred in the existing `main`
checkout without commits, branch/worktree operations or publication.

Concurrent import/comparison source, tests, client contracts and public guides
were already modified at intake. Their edits were preserved. Machine v4 and
import/comparison implementation in progress were established from the current
owners rather than overwritten with the supplied review's older snapshot.

The inventory is a generated per-path view, not another normative registry.
`eng/knowledge/Knowledge.cs inventory` records each tracked Markdown candidate
and explicitly selected new knowledge/configuration file with purpose, owner,
kind, status, authority, preservation, disposition, selection reason and content hash.
Inferred fields and missing metadata remain labeled; a category assignment does
not certify every sentence. Private, ignored, active and generated inputs are
excluded before content reads.

## Contradiction register

| Finding | Disposition and mechanism |
|---|---|
| DOC-01: shared Presentation and Core purity overstatements | Corrected architecture and vision. CLI uses Core/Artifacts directly; interactive clients use Presentation. The bounded Linux directory-source exception is explicit beside the purity boundary, with its relocation follow-up. |
| DOC-02: glossary and private-draft semantics | Replaced field-ID answer identity, obsolete type terminology and sanitized-draft claims. Exact keys, pointer references, presence, initialization, omission/reset, nullable values and restricted incomplete drafts now agree. |
| DOC-03: first-run and recovery examples | Corrected answer envelope, classified receipt excerpts and command templates, current bundle syntax and neutral identity. Ran the maintained neutral example and conflict recovery; generic templates are not claimed executed. |
| DOC-04: overstated guarantees | Candidate preparation replaces target-acceptance promises; data-only definitions do not eliminate all supply-chain risk. Owned-buffer clearing is not universal managed-memory erasure. Publication is ordered per-file create-only, with possible installed prefixes. |
| DOC-05: support and evidence | Added STATUS with separate Desktop X11, Terminal process, Browser and CLI rows, package/identity/availability, save, accessibility, release and target boundaries. Exact totals remain in execution records. Historical v3 results do not qualify v4. |
| DOC-06: wrong documentation owners | Existing policy table now assigns CLI, Browser, Terminal and Desktop contracts to their boundary owners; Presentation owns shared workspace and assessment coordination with Core review. |
| DOC-07: living manuals mixed with chronology | Added current procedure entry routes in Operations/Testing and clearly identified retained observations. No dated execution record, failure, inventory digest or unresolved duty was removed. Removed session-relative qualification prose from current references. |
| DOC-08: strategy, history and proposed work | Added knowledge/ADR indexes and evidence-backed strategy relationships. Protected strategy and report bodies remain unchanged; unresolved external citation tokens and absent decision evidence remain explicit. |
| Additional: machine-v3 drift | Public references, native CLI guidance and portable adoption now follow machine v4 independently of authoring/runtime v2 and answer v1. The earlier setup-assistant skill is explicitly reference-only. |
| Additional: active Event workflow conflicts | Current planning/execution/context guidance no longer mandates `develop`, topology changes, automatic commits or absent Event services/provider matrices. Original depth remains in reference-only resources. |
| Additional: current inverse descriptions | Reconciled direct-copy-only summaries with the current bounded structural/list/repeat inverse. Comparison, strict autofill, readiness and pending client/package qualification are kept separate. |
| Additional: credential-generation ambiguity | Source review found Core's declared-environment `LocalSecretGenerator`; the vision distinguishes that API from an unproved rendered action for every credential descriptor. |

There is no claim that documentation repair closes implementation, host,
accessibility, target or I-VSD findings. Their existing owners retain them.

## Path and heading migration map

| Previous location/heading | Disposition and destination | Retained substantive content |
|---|---|---|
| `docs/README.md` audience lists, parity table and repeated qualification totals | Replaced by task routes and authority guidance; ownership stays in DOCUMENTATION_ARCHITECTURE, evidence routes in STATUS | All operator/contributor domains remain reachable, including the newly explicit import/comparison route |
| `ARCHITECTURE.md` component/pipeline/purity sections | Corrected in place | Assembly edges, separate interactive/CLI flows, publication boundary and bounded I/O exception |
| `VISION_AND_APPROACH.md` goals and surface claims | Corrected in place, with maintained direction separated from implementation/evidence | Product goals, tradeoffs, strategy links and target authority |
| `public/glossary.md` | Rewritten against current contracts | Expanded foundational definitions rather than misleading simplification |
| `public/definition-standard.md` duplicated full grammar | Consolidated into conformance/portability explanation; exact syntax remains in `definition-reference.md`, detailed semantics in `definitions.md`, normative rules in DEFINITION_CONFORMANCE_PROFILE | Exact names, canonical identity, presence, privacy, capabilities, inverse limits and evidence obligations remain explained; property/format tables have named owners |
| Version tables scattered through introductions | Public lookup in `definition-reference.md#which-document-are-you-editing`; CLI owns wire schema | Independent source/runtime/answer/command/family/application version axes |
| Repeated owner-suite/X11 totals in living references | Linked through STATUS to Operations | Original dated totals and limitations remain in Operations |
| `CORE_DEFINITIONS.md` mechanism listings | Extended with five complete operation traces | Source admission; edit invalidation; comparison/adoption; interrupted publication; consent denial |
| `PRESENTATION_STATE.md` generation discussion | Added stale-save installation scenario in place | Input revision, operation eligibility, physical lifetime, retained receipt and cleanup |
| `OPERATIONS.md`, `TESTING.md` chronological records | Records retained in place; current procedure routes precede them | Failed attempts, later corrections, exact commands, candidate identities and limitations |
| `extraction/FOUNDATION.md` current overview | Added current-owner routing without relocating provenance | Transfer authority, exclusions and original records |
| `.agents/CONTEXT_ENGINEERING.md` mixed target/Event guidance | Current contract rewritten; complete prior text retained in `.agents/reference/event-context-engineering.md` with explicit historical boundary | Graph examples, criticality/provider tables, evidence artifacts, harness details and earlier amendments remain inspectable |
| Planning/execution skill bodies | Current routers plus `resources/event-planning-reference.md` and `resources/event-execution-reference.md` | Original workflow detail, templates, lifecycle/failure handling and I-VSD relationships retained |
| `.agents/contract/intents.yaml` documentation routed through provenance | Added ordinary documentation intent and corrected existing routes; schema permits actual root authorities | Licensing/provenance remains distinct, with its original review chain |
| Repomix full-export configuration | Preserved unchanged; `.repomix/README.md` routes to native profiles | Existing full export remains available; new profiles select original content with revision/status manifests |
| Protected strategy, report bodies, dependency approval records, provenance, governed fragments and I-VSD records | Preserved; companion indexes only | No historical disposition, hash, finding identity or qualification was rewritten to make a check pass |

No repository-wide rename or compatibility aliases were introduced. Historical
resource links retain their original source-relative meaning and are audited
as such; current routes point to the current owners.

## Executed verification

Environment: Linux x64 workstation, SDK `10.0.302`,
`TMPDIR="$HOME/.cache/agent-tmp"`. Native tool commands below ran from the
repository root. Tool compilation is not a product-suite pass.

| Check | Executed result |
|---|---|
| Initial native documentation baseline | `ValidateDefinitionDocumentation.cs` over 86 selected files: 799 local links, 84 errors, exit 1. The Git wildcard included historical records. |
| Knowledge tool first compilation | Failed on CA1869, CA1862, IL2026 and IL3050. Fixed owning code by removing reflective configuration deserialization and unnecessary normalization; no suppressions. |
| Classification invariant Red | Runtime failure at self-test case 26, exit 134: strategy was eligible for current context. |
| Classification Green checkpoint | 31 synthetic cases, zero errors, exit 0; current context excludes strategy, correct owner categories and decision-index routing are tested. |
| Markdown validator self-test | 19 cases, exit 0, including link-only mode retaining broken-anchor and unclosed-fence failures. Default metadata validation was not weakened. |
| Definition schema freshness | `dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator --configuration Release -- --check`: current, exit 0. |
| Maintained source/runtime pairs | Same generator with `--no-build -- --check-examples`: agreement 3/3, exit 0. No family renewal or generated-byte edits. |
| Machine schema freshness | `dotnet run --project eng/configurator/Configurator.CommandSchemaGenerator --configuration Release -- --check`: current, exit 0. |
| Agent YAML | Native AgentWorkflow `validate-intents` accepted intents, cold-start scenarios and documentation-navigation YAML. This proves bounded YAML syntax only, not full JSON-Schema semantics or benchmark execution. |
| Rule twins | Native `cmp` passed for all 17 `.agents/rules/*.md` / `.omo/rules/*.md` pairs. |
| Current knowledge checkpoint | `Knowledge.cs links current`: 353 Markdown files, zero failed batches, exit 0. Links/fences include deep active resources; natural metadata is checked only under its applicable document contract. |
| Retained audit checkpoint | `Knowledge.cs links audit`: 471 Markdown files, 14 failed batches, exit 1; 443 findings. This deliberately applies primary-document checks to all history/legacy formats and preserves broken historical references. It is not a clean full-corpus assertion. |
| Original-content profiles checkpoint | Inventory classified 493 paths; current, definitions, clients, agents and audit profiles all generated, exit 0. Later profile narrowing and this record are identified by subsequent manifests. |

The final settled-input command results are recorded in the completion section.
Generated TSV manifests and logs live in `.tmp/knowledge/` and are session-local,
not committed evidence. They include source revision, working-tree content hashes,
selected/excluded paths and reasons. A revision alone does not claim an atomic,
clean-commit snapshot.

### Runnable quickstart and safe rerun

The actual Release CLI DLL (`src/Configurator.Cli/bin/Release/net10.0/Configurator.Cli.dll`)
ran both `artifact bundle-check` and `artifact bundle-create` with:

```text
--definition examples/minimal-self-hosted/setup-definition.json
--artifact environment --artifact configuration --artifact configuration-yaml
--input examples/minimal-self-hosted/answers/complete.json --machine
```

Create additionally used a new `mktemp -d` destination under TMPDIR via
`--output`. Both commands exited 0. Selected v4 receipt facts were checked with
`jq`: ready local preparation, unassessed target acceptance and correct
none/written installation facts. The read-only receipt correctly retained
incomplete host readiness; it did not indicate a missing synthetic answer.

Native `cmp` matched `.env`, `service.json` and `service.yaml` against all three
independent expected files; `stat -c %a` returned `600` for each. Repeating create
against the same directory exited 4; all three existing files still matched.
The compound verification exited 0. This is a bounded synthetic Linux example,
not every command template, clean-machine installation, client or package test.

## Fresh-context navigation exercises

Two independent fresh reader sessions started at README/docs hub and answered
five questions each without the expected answers or migration report. Both
reported no unnecessary documents loaded. The first used nine relevant paths,
the second eight; their source/test anchors were navigation, not executed tests.
One first-reader file-list entry abbreviated PROJECTS to a wrong absolute parent
path; its answer cited the correct repository route, which was verified locally.

| Exercise | Observed answer and route | Result/remaining ambiguity |
|---|---|---|
| Product | README → hub: preparation, local answers, data-only definitions, independent target apply/acceptance | Correct |
| Requiredness | Hub → conformance profile → Core mechanism map → PROJECTS | Correct owner/tests; exact regression depends on requested behavior |
| Presence | Hub → definitions → Core contract | Correct default/initial/suggestion/omission/reset/null/container distinctions |
| Draft | README FAQ → Core mechanisms/contract | Correct restricted and incomplete classification; not public |
| Interrupted save | Hub → protected artifacts → native recovery | Correct installed-prefix, unknown-outcome and no-rollback interpretation; exact UI differs by client |
| Verification | Hub → mechanism map → PROJECTS | Initially found correct Core suite but not a concrete class selector. PROJECTS was improved with SDK, build prerequisite and actual `DefinitionProfileConformanceTests` slice; locally rechecked, not falsely reported as an independent rerun |
| Support | Hub → STATUS | Correct separation of Desktop, Terminal, Browser, package, save and release evidence; exact identities remain in linked records |
| Older guidance | Hub → knowledge map → disagreement procedure | Correct lifecycle/owner/revision resolution; no “newest/code always wins” shortcut |
| Docs-only | Hub → documentation policy → knowledge-check route | Correct counterpart and proportional checks; no product or external-link pass inferred |
| Import/comparison | Hub → STATUS → import contract | Correct implementation-in-progress and pending client/package qualification, not merely proposal or universal delivery |

These are ten completed qualitative navigation exercises, with one concrete
retrieval improvement. Token/byte/caching metrics were not captured, so no
context-budget PASS, speedup percentage or original six-scenario benchmark
success is claimed. The reusable scenarios are in
`.agents/benchmarks/documentation-navigation.yaml`.

## Remaining evidence limits

- Immutable reports retain retired/outside-root links, legacy metadata and one
  known fence defect. The report index gives current replacement routes.
- Whole-audit metadata failures also include agent/vendor/reference formats
  governed by different schemas; they are retained results, not hidden waivers.
- User strategy's conversation-local citation tokens remain unresolved.
  The companion map does not invent URLs, approvals or strategic dispositions.
- Temporary raw historical evidence availability was not requalified.
- JSON/YAML language servers were unavailable; native JSON/YAML commands,
  C# diagnostics/self-tests and manual routing/schema review supplied the
  relevant checks. No tooling dependencies were installed.
- Product-wide suites, native/browser UI exercises, accessibility/translation
  studies, remote CI, official publication and target acceptance were not run.
- External references and competitor/version claims were not freshly researched
  in this implementation; comparison guidance identifies that boundary.

The existing documentation policy's reader-purpose model and the approved
review supplied the design requirements. No external prose, implementation,
asset or dependency was incorporated by this correction.

## Completion verification

The settled-source run completed with exit 0 using:

```bash
export TMPDIR="$HOME/.cache/agent-tmp" MSBUILDTERMINALLOGGER=off
dotnet run --file eng/knowledge/Knowledge.cs -- --self-test
dotnet run --file eng/knowledge/Knowledge.cs -- links current
dotnet run --file eng/knowledge/Knowledge.cs -- inventory
for profile in current definitions clients agents audit; do
  dotnet run --file eng/knowledge/Knowledge.cs -- context "$profile"
done
dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj \
  --configuration Release --no-build -- validate-intents .agents/contract/intents.yaml
dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj \
  --configuration Release --no-build -- validate-intents .agents/benchmarks/documentation-navigation.yaml
jq -e . .agents/contract/schema.json
git --no-pager diff --check
```

- Knowledge self-test: **34 cases, zero errors**.
- Current corpus: **354 Markdown files, 1,870 local links, zero errors**.
- Inventory: **494 candidate paths**, with **479 eligible** and **15 excluded**
  according to explicit manifest reasons.
- Original-content profiles: current **86**, definitions **75**, clients **89**,
  agents **94**, audit **479** included files.
- Both changed YAML inputs parsed successfully; contribution-schema JSON parsed.
  Manual review reconciled the eight intent IDs, their referenced owners and the
  ten navigation scenarios. This is not an automated full-schema proof.
- Whitespace check exited 0. C# diagnostics were clear; actual native compilation
  and self-tests, not the diagnostic tool alone, established executable success.
- Read-only Git comparison confirmed protected strategy, immutable report bodies,
  extraction provenance/verification records and dependency approval records
  were byte-unchanged against the starting revision. Concurrent product work is
  outside this assertion and was not reverted.

Manifest inspection caught an additional classification defect: incidental
“historical” wording in the current STATUS index incorrectly excluded it.
A synthetic regression failed at case 31 with exit 134 before the fix.
Classification now reads lifecycle prefixes rather than arbitrary words;
mixed/current evidence indexes remain in current retrieval. Context profiles
were narrowed separately from link checking: deep resources remain checked but
are loaded on demand rather than all entering a default context.

The completion record itself and generated manifests are checked/refreshed
after recording these outcomes. Historical audit limitations remain explicitly
non-green; no warning suppression, removed test or rewritten approval record
was used to obtain current-check success.

The final retained-history audit included **472 Markdown files**, with
**443 findings in 14 failing batches**, exit 1. The separate completion-record
check covered four documents and 101 local links with zero errors, exit 0.
The inventory's final authority column explicitly distinguishes existing
governing owners from subordinate guidance, decisions, strategy and evidence;
its additional synthetic regression passed with the final manifest refresh:
**35 knowledge cases, zero errors**, exit 0. The final two-document check
(tooling guide and this record) covered six local links with zero errors.
Inventory and all five profiles regenerated successfully with the counts above;
the final whitespace check also exited 0.
