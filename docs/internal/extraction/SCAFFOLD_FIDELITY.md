# Event Scaffold Fidelity Correction

> **Audience:** Maintainer | Contributors | AI agents
> **Status:** Restored; scoped verification executed
> **Source Anchors:** `README.md`, `AGENTS.md`, `PROJECTS.md`, `transfer-manifest.json`

## Correction and method

The first extraction preserved most native implementations but unnecessarily
replaced detailed reusable scaffolding with thin summaries. The maintainer
required copying the original structure and content, changing only what the
independent Configurator actually needs.

The comparison classified all 505 agent/harness manifest items and 127
engineering/root items, with shared root entries identified by both tracks.
Recorded source blobs, native diffs and bounded section reads distinguished
content loss from necessary target adaptations. Audit detail remains in the
single task-owned context directory; this document records the durable result.

The manifest now gives explicit original source identities to restored root
files that previously had ambiguous `author-or-adapt` provenance. It accounts
individually for newly restored documentation anchors, contributor forms,
harness pointers and journal assets.

## Restored content

| Area | Original reusable content restored | Necessary target adaptation |
|---|---|---|
| README | Original centered entry, badge/link groups, audience structure, contribution routes, maintainer, community, sponsorship, acknowledgements, sustainability, contact and license organization | Actual offline clients, current repository links, MIT terms, independent packages and no invented hosted service |
| Contributor/security policies | Proposal format, atomic changes, maintainer bandwidth, AI disclosure/ownership, bug triage, scoped checks, private disclosure and commitments | Main branch, actual clients/commands, MIT provenance and current offline invariants; no Event CLA automation |
| Root contract/review/project map | Eight-question intake, authoritative artifacts, classify/load/edit/verify/teach flow, retrieval ledger, protected paths, test oracles and evidence reporting | Actual Core/Artifacts/Presentation/adapters and native engineering owners |
| Governance anchors | Natural metadata, naming, public/internal parity, fixtures, execution versus measured coverage, operational evidence, release procedures and IP AFC/SSO workflow | Selected original sections applicable to the offline utility; no backend service operation |
| Rules | Independent assertions, deterministic async, mock boundaries, invariant disposition, failure quarantine, accessible controls, privacy and criticality procedures | Actual owner paths; Event-only bodies retained verbatim as inactive historical reference |
| Benchmarks | Original scenario fields, context budgets, scoring and expected-read/path/verification vocabulary | Six actual Configurator scenarios instead of Event workflows |
| Skills/routers | Generic Markdown/image/regex/template examples, existing sibling-skill links, finding workflow and named policy destinations | Live target documents or explicitly historical Event references, not blanket FOUNDATION links |
| Contributor/harness assets | Bug/documentation forms, PR checklist, CODEOWNERS, funding metadata, Claude native import and missing Copilot pointer | Actual repository, main branch, supported clients and target licensing |
| Journal | Original recording template, domain-routing structure and promotion procedures | Target-owned indexes without copied Event finding history |
| Local state | Original portable editor, cache, crash, worktree, temporary-output and secret ignore safeguards | Retained approved feed/release exceptions and stricter target secret exclusions |

README was restored from 61 to 515 lines using the 564-line Event structure.
The contributor guide was restored from a short summary to 368 lines.
Length is evidence of removed/restored content, not an acceptance criterion.

The majority of the resource library was already retained; it was not replaced
wholesale. Native implementations, dependency versions, admission/protected-write
hardening and product code were not reverted to achieve superficial similarity.

## Intentional differences

- Core has no Event backend/domain/wire/database dependency.
- Event runtime activation, HAL, EF, BFF and payment requirements cannot become
  active Configurator requirements. Their rule bodies remain historical.
- MIT first-party transfer does not change third-party rights. Contributor
  Covenant expression and the 154 unlicensed Evolve corpus entries remain
  excluded from MIT-transferred content.
- Event credentials, production signers, trust activation and release history
  remain excluded. Generic release validation and inert fixtures remain
  supported; no publication workflow is activated.
- Documentation identifies real clients, commands, branches, dependencies and
  hosting limits. No Configurator GitBook origin, Coverity project, Weblate
  deployment or signing bot is invented.
- Restored journal indexes do not transplant Event business decisions or history.

## Verification evidence

All commands ran from Configurator unless a source repository was explicit.

- `git --no-pager diff --check`: passed after correcting copied trailing spaces.
- `jq empty docs/internal/extraction/transfer-manifest.json`: passed.
- Native uniqueness check: no duplicate nonnull destination entries.
- `cmp` on all 17 `.agents/rules` / `.omo/rules` pairs: passed.
- Exact source `git rev-parse <revision>:<path>` checks: recorded restored
  root/contributor/journal/documentation source blobs matched.
- Existing native AgentWorkflow `validate-intents` parser accepted six
  single-document YAML inputs: intents, benchmarks, three issue-form/config
  documents and funding metadata. For non-intent inputs this proves YAML
  syntax/document count only, not intent semantics.
- `dotnet eng/agent-workflow/tests/Configurator.AgentWorkflow.Tests/bin/Release/net10.0/Configurator.AgentWorkflow.Tests.dll --minimum-expected-tests 1 --no-progress`:
  2 passed, 0 failed, exit 0.
- Native rule/benchmark checks: 16 YAML/frontmatter records parsed, six original
  context budgets exact, ten historical rule bodies exact, no activating
  historical namespace or rule symlink.
- Live documentation link checks covered restored root/policy/agent destinations.
  The nine deliberately illustrative GitBook paths remain examples, not missing
  target policies. A copied PR-template relative review link was corrected to
  its real repository URL.

An oversized native link-check wrapper failed before completion and was
replaced with bounded batches; no result is inferred from that failed wrapper.
YAML/JSON language servers are not installed; native parsers supplied the
relevant checks without adding dependencies.

No product source, product tests, native engineering implementation or
dependency version changed. Product builds/suites were therefore not rerun.
The existing positive fragment-parser test already exercises one valid
structured fragment independently; a redundant test was not added.
Event remained read-only and both user strategy documents remain untouched.
