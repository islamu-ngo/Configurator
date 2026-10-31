# Documentation Architecture

> **Audience:** Contributors | Adopters | Integrators | AI agents
> **Kind:** Living contract
> **Status:** Current repository documentation policy
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-28
> **Source Anchors:** `README.md`, `docs/README.md`, `PROJECTS.md`, `docs/public/`, `docs/internal/`

This repository uses Markdown-first documentation as the authoritative adopter,
contributor, and agent knowledge base. The reusable structure is transferred from
Event revision `c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38`; an Event hosted portal
or GitBook synchronization is not Configurator publication authority.

## Intent Model

Use Diataxis intent categories to prevent one page from becoming a mixed manual:

| Intent | Reader Question | Use For | Example Docs |
|---|---|---|---|
| Tutorial | How do I learn the path once? | Guided first-run or first-contribution flows | `README.md`, public surface guides |
| How-to | How do I complete this task safely? | Developer procedures and runbooks | `docs/public/native-output.md`, `RELEASE_RUNBOOK.md` |
| Reference | What are the exact keys/contracts? | Stable facts, settings and commands | `CLI_CONTRACT.md`, `CORE_DEFINITION_CONTRACT.md` |
| Explanation | Why is the system designed this way? | Architecture, flows, tradeoffs and governance | `PRESENTATION_STATE.md`, ADRs, `GOVERNANCE.md` |

If a page needs two intents, split the task steps from the reference table and
link between them.

## Audience Paths

Use `README.md` as the public entry point. It should route new readers by task
and audience without forcing them into the full documentation inventory. Use
`docs/README.md` as the documentation map for readers who already know their task
or need a specific reference.

| Audience | Start Here | Then Read |
|---|---|---|
| Evaluators | `README.md` | `docs/public/getting-started.md`, then the relevant client guide |
| Local developers | `CONTRIBUTING.md`, `PROJECTS.md` | `TESTING.md`, `GOVERNANCE.md`, affected owner contract |
| Adopters | `docs/public/getting-started.md` | `definitions.md`, `native-output.md`, `packaging.md` |
| Integrators | `docs/public/integrating.md` | `definition-cookbook.md`, `integrate-your-tool.md`, `DEFINITION_CONFORMANCE_PROFILE.md`; for launch, `launch-integration.md` and `LAUNCH_CONTRACT.md` |
| Contributors | `PROJECTS.md` | `GOVERNANCE.md`, `QUICK_REFERENCE.md`, `DOCUMENTATION_ARCHITECTURE.md` |
| AI agents | `AGENTS.md` | Matching intent, path rules and task-owned context |

## Primary documentation and owners

Each primary document has one accountable owner category. Other owners review
their contributing mechanisms; shared implementation does not require an
ambiguous documentation owner. Ownership means responsibility for accuracy,
not exclusive editing rights. This table is the responsibility registry;
navigation pages and generated inventories project it rather than replace it.

| Owner | Primary documentation |
|---|---|
| Packaging/Release | `PACKAGING.md`, `OPERATIONS.md`, `RELEASE_RUNBOOK.md`, `eng/release/README.md` |
| Core | `CORE_DEFINITION_CONTRACT.md`, `CORE_DEFINITIONS.md`, `DEFINITION_CONFORMANCE_PROFILE.md`, `DEFINITION_PROFILE.md`, `MAINTAINER_AUTHORING.md`, `DEFINITION_STORE.md`, definition/runtime ADR |
| Artifacts/Security | `PROTECTED_ARTIFACTS.md`, `SECURITY.md`, technical dependency-closure evidence |
| Project Steward | `legal/IP_GOVERNANCE.md`; licensing/distribution authority remains with the Steward and existing reviewed records |
| Presentation | `PRESENTATION_STATE.md`, `IMPORT_COMPARISON.md` (Core reviews inverse/evaluation semantics) |
| CLI | `CLI_CONTRACT.md`, public CLI guide |
| Browser | `LAUNCH_CONTRACT.md`, `BROWSER_UI.md`, public browser guide |
| Terminal | `TERMINAL_CLIENT.md`, public terminal guide |
| Desktop | `DESKTOP_CLIENT.md`, public desktop guide |
| Contributor Experience | `CONTRIBUTING.md`, `PROJECTS.md`, `GOVERNANCE.md`, `TESTING.md`, `STATUS.md`, `KNOWLEDGE_MAP.md`, `docs/public/getting-started.md`, this document |
| Agent Context | `AGENTS.md`, `.agents/contract/`, `.agents/skills/`, `dev/_journal/` |

## Metadata Policy

New primary documentation and operation-critical docs must include this block
immediately below the title:

```markdown
> **Audience:** Adopters | Contributors | Integrators | AI agents
> **Kind:** Living contract | Guide | Procedure | Accepted decision | Proposal | Historical assessment | Execution record | Generated reference
> **Status:** Current, proposed, superseded, or historical; qualify implementation separately
> **Owner:** One accountable category from the ownership table
> **Last Verified:** YYYY-MM-DD
> **Source Anchors:** `path/one`, `path/two`
```

Rules:

- `Audience` can list multiple audience labels separated by `|`.
- `Kind` identifies what the reader can use the page for. `Status` describes
  lifecycle, not a universal product-support claim. An accepted decision can
  still have incomplete implementation; a current guide can describe an
  unqualified host. Label those boundaries at the affected section.
- `Owner` must use one of the owner categories in this page.
  Historical approvals and assessments retain their original attribution;
  normalization must not rewrite decision or licensing authority.
- `Last Verified` is the date source anchors were checked, not the edit date for
  grammar-only changes. State a review basis when consequential: inspected source
  revision, recorded execution, or editorial review. A fresh date does not renew
  runtime evidence.
- `Source Anchors` must point to real files or directories that prove the behavior.

Legacy docs can migrate gradually. Do not add metadata mechanically to low-value
pages without checking their source anchors.

## Source-Anchor Policy

Documentation must prefer source-grounded claims over inferred behavior:

- Product facts anchor to the actual owner under `src/Configurator.*`.
- Definition tables anchor to `CORE_DEFINITION_CONTRACT.md` and schema generators;
  packaging commands anchor to `eng/configurator/Configurator.Release.proj`.
- Testing commands anchor to `PROJECTS.md`, `global.json`, and the relevant test
  project files.
- Roadmap or future behavior must be explicitly marked `Planned` or `Draft` and
  must not be presented as implemented.

### Resolve disagreements

1. Classify both documents: living requirement, guide, decision, proposal or
   dated observation. Compare the affected owner, scope, revision and evidence.
2. Inspect the owning source and representative tests. A test assertion is not
   evidence that the test ran; an approved plan is not evidence of implementation.
3. Record the mismatch and classify it as documentation drift, implementation
   nonconformance or an unresolved design decision.
4. Correct documentation drift with its counterpart. Preserve the intended
   contract when code is wrong, and route the implementation defect to its owner.
   Ask for a decision when competing requirements genuinely remain unresolved.

Neither "newest document wins" nor "code always wins" is a sufficient rule.
Do not rewrite a historical record to make it match current behavior.

## Definition reading paths

| Page | Primary job |
|---|---|
| `public/definitions.md` | Mental model, presence and worked semantic explanations |
| `public/definition-reference.md` | Author-facing properties, versions, limits and exact lookup |
| `public/definition-standard.md` | Profile scope, portability and conformance obligations |
| `public/definition-cookbook.md` | Small solutions to bounded authoring problems |
| `public/integrating.md` | Short complete application-adoption tutorial |
| `public/integrate-your-tool.md` | Revision, distribution, schema import and maintainer lifecycle |
| `DEFINITION_CONFORMANCE_PROFILE.md` | Normative bounded semantics |
| `CORE_DEFINITION_CONTRACT.md` | Core API, custody, failure and composition contract |
| `CORE_DEFINITIONS.md` | Mechanism, source/test map and end-to-end flows |
| `DEFINITION_PROFILE.md` | Scenario obligations, evidence and residual responsibilities |
| `MAINTAINER_AUTHORING.md` | Compiler lowering, identity and public-source filesystem authority |

A public explanation must remain useful by itself. Link exact normative details
instead of maintaining multiple competing specifications or qualification totals.

## Docs Impact Contract

Every non-trivial change must record one of these outcomes in the PR or dev handoff:

| Outcome | Meaning |
|---|---|
| Updated | Docs changed in the same PR because behavior, commands, config, or operations changed |
| Not needed | The change is internal and does not affect documented behavior |
| Deferred | Docs impact exists but is intentionally split; include the follow-up path and reason |

Definition/CLI contracts, protected output, launch integration, security,
onboarding, packaging and release changes should default to `Updated` unless
proven otherwise.

## Dual-Documentation Architecture (`docs/public/` vs `docs/internal/`)

To preserve clarity for all audiences, documentation is partitioned into two
parallel tracks:

```text
docs/
├── README.md               <-- Documentation Router
├── public/                 <-- Adopters and Integrators
└── internal/               <-- Contributors and AI Coding Agents
```

1. **`docs/public/`**: Curated, task-oriented guides for using the offline CLI,
   native clients, browser and prepared artifacts. Avoid internal C# plumbing.
2. **`docs/internal/`**: Source of technical truth, owner boundaries, definition
   admission, sensitivity floors, operation generations, output protection and
   engineering contracts.

### Documentation Twin Parity Matrix & Separation of Concerns

Every public guide corresponds to a technical anchor. The twins maintain strict
Single Responsibility and do not duplicate content:

| Domain | Public Adopter Guide | Technical Source Anchor | Boundary of Separation |
|---|---|---|---|
| First run | [getting-started.md](../public/getting-started.md), [neutral example](../../examples/minimal-self-hosted/README.md) | [CORE_DEFINITION_CONTRACT.md](CORE_DEFINITION_CONTRACT.md), [CLI_CONTRACT.md](CLI_CONTRACT.md), [PROTECTED_ARTIFACTS.md](PROTECTED_ARTIFACTS.md) | Public: reproducible local steps; internal: admission, command and save contracts |
| Definitions | [definitions.md](../public/definitions.md), [definition-reference.md](../public/definition-reference.md), [definition-cookbook.md](../public/definition-cookbook.md) | [DEFINITION_CONFORMANCE_PROFILE.md](DEFINITION_CONFORMANCE_PROFILE.md), [CORE_DEFINITION_CONTRACT.md](CORE_DEFINITION_CONTRACT.md) | Public: authoring usage, bounded patterns, exact properties and capability limits; internal: implemented grammar, admission/evaluation mechanisms and source-observed limits |
| Definition standard | [definition-standard.md](../public/definition-standard.md) | [DEFINITION_PROFILE.md](DEFINITION_PROFILE.md) | Public: implemented bounded-v2 grammar and qualified pre-release scope; internal: lowering, invariants, scenario obligations, qualification and responsibility mapping |
| Maintainer authoring | [integrating.md](../public/integrating.md), [integrate-your-tool.md](../public/integrate-your-tool.md) | [MAINTAINER_AUTHORING.md](MAINTAINER_AUTHORING.md) | Public: portable adoption workflow and detailed one-source check/build/revise/distribution guidance; internal: compilation, revision and public-source custody mechanisms |
| CLI | [cli.md](../public/cli.md) | [CLI_CONTRACT.md](CLI_CONTRACT.md) | Public: commands and results; internal: machine contract and exit semantics |
| Terminal | [terminal.md](../public/terminal.md) | [TERMINAL_CLIENT.md](TERMINAL_CLIENT.md), [PRESENTATION_STATE.md](PRESENTATION_STATE.md) | Public: interaction; internal: shared state and terminal adapter |
| Desktop | [desktop.md](../public/desktop.md) | [DESKTOP_CLIENT.md](DESKTOP_CLIENT.md), [PRESENTATION_STATE.md](PRESENTATION_STATE.md) | Public: workflow; internal: Avalonia adapter and generation fencing |
| Browser | [browser.md](../public/browser.md) | [LAUNCH_CONTRACT.md](LAUNCH_CONTRACT.md), [PRESENTATION_STATE.md](PRESENTATION_STATE.md), [PACKAGING.md](PACKAGING.md) | Public: complete/draft usage, custody choices and offline hosting; internal: consent/generation fences, source admission and static closure |
| Native output | [native-output.md](../public/native-output.md) | [PROTECTED_ARTIFACTS.md](PROTECTED_ARTIFACTS.md) | Public: safe save procedure; internal: create-only protected writes and failure posture |
| Launch integration | [launch-integration.md](../public/launch-integration.md) | [LAUNCH_CONTRACT.md](LAUNCH_CONTRACT.md), [ADR](adr/ADR-definition-runtime-boundary.md) | Public: handoff; internal: preparation is not host apply/admin authority |
| Packaging | [packaging.md](../public/packaging.md) | [PACKAGING.md](PACKAGING.md) | Public: platform commands and limits; internal: evaluated release contracts and package closure |
| Optional catalogue | [definition-store.md](../public/definition-store.md) | [DEFINITION_STORE.md](DEFINITION_STORE.md) | Public: browse and contribute; internal: metadata admission, canonical snapshots and optional package generation |

### Dual-Documentation Parity Protocol

1. **Freshness**: Check the current checkout and shared edits before authoring
   either twin. Follow `AGENTS.md`; do not switch branches or assume Event's
   GitBook `develop` synchronization applies here.
2. **Adopter Projection Rule**: Write public instructions from the adopter or
   integrator perspective. Provide usable commands and UI walkthroughs. Do not
   expose internal C# class names or TUnit commands.
3. **Technical Depth Rule**: Internal docs document the full architectural reality:
   owner contracts, C# mechanisms, sensitivity and authority boundaries, state
   transitions, concurrency and recovery. Do not duplicate full user manuals.
4. **Intent Enforcement**: Changes affecting external contracts update both
   applicable twins and state the docs-impact outcome.
5. **Reference Parity**: Public command/definition examples must match admitted
   contracts and shipped schema versions. There is no Event environment-variable
   catalogue to inherit.
6. **Pure Markdown & Mermaid Rule**: Public docs use standard Markdown tables,
   GitHub alerts (`> [!NOTE]`, `> [!TIP]`, `> [!WARNING]`, `> [!IMPORTANT]`,
   `> [!CAUTION]`) and native Mermaid diagrams, not raw HTML accordions or layouts.
7. **Metadata & Header Boundary**: Public docs may use YAML `description:`
   frontmatter and never synthetic AI comment headers. Navigation is anchored
   in `docs/README.md` and the twin matrix here. Internal docs use natural
   Markdown metadata blocks.

### Bounded-v2 qualification boundary

Current definition documentation describes implemented bounded-v2 with qualified
pre-release Linux x64 and published Browser scope, not an unimplemented Draft.
Keep the detailed vocabulary, source-observed limits and scenario obligations;
do not replace them with a broad support label.

Use [support and evidence](STATUS.md) to find scoped records in
[Operations](OPERATIONS.md). Keep exact suite totals, package identities,
environments and failed attempts in those records. The
[qualification map](DEFINITION_PROFILE.md) owns scenario obligations, not a
second execution ledger. Desktop X11 scenarios and an actual Terminal process
are different evidence; neither qualifies the other surface.

General assertions beyond the bounded profile, application-version ranges,
target acceptance, other hosts, assistive technologies, translation quality,
stakeholder/usability and scholarly proof remain unqualified followups.
Public and technical twins must retain those limits together. Suite counts,
English/Arabic render observations and Browser custody exercises are not
interchangeable with target, accessibility or stakeholder evidence.

## Examples, evidence and context exports

Label each example at its introduction: **runnable current**, **partial source
or result**, **conceptual**, or **historical**. A partial JSON object is not an
accepted input file or an exact command receipt. Runnable examples identify the
working directory, prerequisites, shell/host, expected effect, success signal and
safe rerun. Use maintained synthetic fixtures; never execute arbitrary fences.

Preserve immutable assessments, user strategy, provenance manifests, reviewed
digests, governed release fragments and I-VSD finding identities. Use a companion
index when correction or relocation would alter their historical meaning.
Record unresolved conversation-local citations as unavailable provenance; do not
guess their source. Session-local evidence must be labeled as such.

Context exports select original files. Current entrypoints and contracts precede
history; every export identifies its revision, paths, exclusions and lifecycle
classification. An export is a snapshot, not a new authority. See the
[knowledge tooling](../../eng/knowledge/README.md) for inventory and profiles.

### Review a documentation-only change

- Begin with the reader's task, its prerequisites and expected result. Keep the
  documentation hub task-oriented; put complete option tables in the CLI reference.
- Distinguish public maintainer source, compiled runtime definitions, private
  answers and prepared artifacts. Do not use “configuration” ambiguously where
  the distinction affects custody or validation.
- Explain safe recovery where a command can write files. An error or cancellation
  does not imply rollback; an unknown outcome is not proof of no write.
- Keep package admission, runtime compatibility, actual-host execution and
  protected-save qualification separate. Link to packaging for the current
  platform boundary rather than reproducing a distribution/version matrix.
- Check changed local links and heading anchors, command arguments against the
  current owner/help, and whitespace. Use synthetic values for executable
  examples. Do not rebuild the product merely to verify prose.
- Preserve source contracts and historical evidence. A clearer guide must not
  silently turn a roadmap, configured CI job or old report into current support.
