# Configurator documentation

Configurator prepares candidate configuration files from public, data-only
definitions and private local answers. The target application independently
validates, authorizes and applies those files.

Choose a task below. You do not need to read the entire corpus. The
[knowledge map](internal/KNOWLEDGE_MAP.md) explains how current contracts,
decisions, strategy, proposals and historical evidence relate.

## Use or integrate Configurator

| Your task | Start here | Continue when needed |
|---|---|---|
| Understand the product | [Overview](public/overview.md) | [Glossary](public/glossary.md), [architecture](internal/ARCHITECTURE.md) |
| Prepare your first files | [Runnable quickstart](public/getting-started.md) | [Native output and recovery](public/native-output.md) |
| Automate local preparation | [CLI commands and results](public/cli.md) | [Machine contract](internal/CLI_CONTRACT.md) |
| Fill a form | [Terminal](public/terminal.md), [Desktop](public/desktop.md), [Browser](public/browser.md) | [Support and evidence](internal/STATUS.md) |
| Understand presence, null and defaults | [Definition mental model](public/definitions.md) | [Exact reference](public/definition-reference.md), [cookbook](public/definition-cookbook.md) |
| Add configuration support to an application | [Adoption tutorial](public/integrating.md) | [Maintainer lifecycle](public/integrate-your-tool.md), [maintained examples](../examples/README.md) |
| Look up syntax, versions or limits | [Definition reference](public/definition-reference.md) | [Schema catalog](../schemas/README.md), [conformance profile](internal/DEFINITION_CONFORMANCE_PROFILE.md) |
| Understand portability and conformance | [Definition standard](public/definition-standard.md) | [Qualification obligations](internal/DEFINITION_PROFILE.md) |
| Compare or reuse an existing configuration | [Import and compare](public/import-and-compare.md) | [Implementation and qualification status](internal/STATUS.md), [technical contract](internal/IMPORT_COMPARISON.md) |
| Diagnose a failure | [Troubleshooting](public/troubleshooting.md) | [Safe publication recovery](public/native-output.md) |
| Build or package a client | [Packaging guide](public/packaging.md) | [Project commands](../PROJECTS.md), [technical packaging contract](internal/PACKAGING.md) |
| Link an application to the browser | [Launch integration](public/launch-integration.md) | [Launch trust boundary](internal/LAUNCH_CONTRACT.md) |
| Distribute public definitions | [Optional store](public/definition-store.md) | [Store contribution instructions](../store/README.md) |
| Compare architectural approaches | [Comparison](public/comparison.md) | [Direction and tradeoffs](internal/VISION_AND_APPROACH.md) |

## Contribute or investigate a mechanism

| Your task | Required route | Result |
|---|---|---|
| Make a first contribution | [First contribution](internal/FIRST_CONTRIBUTION.md) → [contributor recipes](internal/CONTRIBUTOR_RECIPES.md) | Complete a small source-grounded change with proportional verification |
| Learn the developer architecture | [Developer guide](internal/DEVELOPER_GUIDE.md) → [project map](../PROJECTS.md) | Follow dependencies, data flow and ownership |
| Change a definition assertion | [Conformance profile](internal/DEFINITION_CONFORMANCE_PROFILE.md) → [mechanism map](internal/CORE_DEFINITIONS.md) → [Core contract](internal/CORE_DEFINITION_CONTRACT.md) | Change the shared semantic owner, not a client-specific evaluator |
| Fix typing, edits or consent | [Presentation state](internal/PRESENTATION_STATE.md) → [Browser UI](internal/BROWSER_UI.md) → [launch/custody contract](internal/LAUNCH_CONTRACT.md) | Preserve input revision, operation generation and secret custody |
| Change machine output | [CLI contract](internal/CLI_CONTRACT.md) → [schema catalog](../schemas/README.md) → [project map](../PROJECTS.md) | Coordinate emitter, generated schema and independent consumers |
| Fix native save or close | [Protected artifacts](internal/PROTECTED_ARTIFACTS.md) → [Terminal](internal/TERMINAL_CLIENT.md) or [Desktop](internal/DESKTOP_CLIENT.md) | Preserve physical lifetime and installed-file facts after cancellation |
| Edit documentation | [Writing guide](internal/DOCUMENTATION_STYLE_GUIDE.md) → [documentation policy](internal/DOCUMENTATION_ARCHITECTURE.md) → [knowledge checks](../eng/knowledge/README.md) | Correct the owner and public counterpart; use proportional checks |
| Run tests or inspect coverage | [Testing](internal/TESTING.md) → [project commands](../PROJECTS.md) | Distinguish executed tests from measured coverage |
| Package or release | [Packaging](internal/PACKAGING.md) → [release runbook](internal/RELEASE_RUNBOOK.md) → [release tooling](../eng/release/README.md) | Keep local validation separate from publication authority |
| Understand a design decision | [Decision index](internal/adr/README.md) | Find rationale, supersession and delivery status |
| Locate provider-responsibility reasoning | [I-VSD assessment catalog](../islamic-value-sensitive-design/README.md) | Find governance, consultations and workstream reports with their recorded evidence limits |
| Inspect earlier evidence | [Support index](internal/STATUS.md) → [operations records](internal/OPERATIONS.md) or [historical reports](internal/reports/README.md) | Identify the actual revision, host, scenario and evidence limits |
| Work as an AI agent | [AGENTS.md](../AGENTS.md) → [matching intent](../.agents/contract/intents.yaml) | Load only the applicable rules, skills and headings |

## Follow authority, not export order

`public/` explains use and adoption. `internal/` contains engineering contracts,
procedures, decision records and evidence, whose kinds must remain distinct.
The [documentation ownership table](internal/DOCUMENTATION_ARCHITECTURE.md#primary-documentation-and-owners)
assigns accountability. The [disagreement procedure](internal/DOCUMENTATION_ARCHITECTURE.md#resolve-disagreements)
explains how to reconcile conflicting claims without treating either a bug or
an unimplemented plan as the intended contract.

The [support index](internal/STATUS.md) is the entry point for qualification.
Buildable pre-release packages are not official releases. Source inspection,
unit tests, actual-host exercises, accessibility assessment and target acceptance
are different evidence. Exact totals stay in dated records, not this navigation
page.

For bounded AI retrieval or offline review, use the
[original-content context profiles](../eng/knowledge/README.md). Full history
remains available without forcing every contributor to load it.
