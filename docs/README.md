# Configurator documentation

Configurator turns a reviewed definition and local answers into configuration
files. It does not deploy them, contact a target application, or grant permission
to administer that application.

## Start with your task

| I want to… | Start here |
| --- | --- |
| Try a complete example with synthetic answers | [Prepare a configuration offline](public/getting-started.md) |
| Automate preparation or read machine results | [CLI guide](public/cli.md) |
| Fill in a form interactively | [Terminal](public/terminal.md), [Desktop](public/desktop.md) or [Browser](public/browser.md) |
| Add configuration support to my own tool | [Integrate your tool](public/integrate-your-tool.md) |
| Understand a definition or reuse an existing file | [Definitions](public/definitions.md) |
| Recover from a failed or interrupted save | [Native output and recovery](public/native-output.md) |
| Build or copy a standalone package | [Packaging](public/packaging.md) |
| Link a target application to Configurator | [Launch integration](public/launch-integration.md) |
| Browse or contribute optional public definitions | [Definition Store](public/definition-store.md) |
| Contribute code | [Project map](../PROJECTS.md), then [testing](internal/TESTING.md) |

## Understand the workflow

1. **Review a public definition.** It declares questions, validation and output
   mappings; publisher labels and family identifiers are not proof of trust.
2. **Supply local answers.** Keep real credentials out of public definitions,
   command-line values and example files.
3. **Check, then prepare files.** A successful local check is not target acceptance.
   See the [neutral example](../examples/minimal-self-hosted/README.md).
4. **Validate and apply with the target's own tools.** Configurator never performs
   this final step for you.

> [!IMPORTANT]
> A package that builds or starts is not necessarily able to save restricted
> files. The native protected writer is Linux-only, with actual-host evidence on
> Linux x64; Arm/musl qualification remains pending. Saving also depends on the
> destination filesystem. macOS and Windows protected saves reject without an
> ordinary-file fallback. Browser copy/download is a separate user-controlled
> export, not a native protected save. See [platform boundaries](public/packaging.md#cli-operating-system-boundaries)
> and the [safety and trust FAQ](../README.md#faq).

## User guides and technical references

Use `public/` for tasks, commands and recovery. Use `internal/` to understand the
contracts and mechanisms behind them; you do not need the internal track for a
first run.

| Topic | User guide | Technical reference |
| --- | --- | --- |
| Definitions and validation | [Definitions](public/definitions.md) | [Core definition contract](internal/CORE_DEFINITION_CONTRACT.md) |
| Maintainer source and revisions | [Integrate your tool](public/integrate-your-tool.md) | [Maintainer authoring](internal/MAINTAINER_AUTHORING.md) |
| Commands, help and result envelopes | [CLI](public/cli.md) | [CLI contract](internal/CLI_CONTRACT.md) |
| Interactive terminal | [Terminal](public/terminal.md) | [Terminal client](internal/TERMINAL_CLIENT.md) |
| Desktop application | [Desktop](public/desktop.md) | [Desktop client](internal/DESKTOP_CLIENT.md) |
| Browser trust, consent and offline use | [Browser](public/browser.md) | [Launch contract](internal/LAUNCH_CONTRACT.md), [presentation state](internal/PRESENTATION_STATE.md), [browser UI](internal/BROWSER_UI.md) |
| Protected output | [Native output](public/native-output.md) | [Protected artifacts](internal/PROTECTED_ARTIFACTS.md) |
| Target handoff | [Launch integration](public/launch-integration.md) | [Launch contract](internal/LAUNCH_CONTRACT.md) |
| Standalone packages and hosting | [Packaging](public/packaging.md) | [Packaging contract](internal/PACKAGING.md) |
| Optional public catalogue | [Definition Store](public/definition-store.md) | [Store contract](internal/DEFINITION_STORE.md) |

## Contributor reading paths

| Task | Read in this order |
| --- | --- |
| Make a code change | [Project map](../PROJECTS.md) → [Quick Reference](internal/QUICK_REFERENCE.md) → affected technical reference above → [Testing](internal/TESTING.md) |
| Understand ownership and design | [Governance](internal/GOVERNANCE.md) → [definition/runtime decision](internal/adr/ADR-definition-runtime-boundary.md) → [CLI runtime decision](internal/adr/ADR-cli-command-runtime.md) |
| Reproduce a verification or package check | [Operations](internal/OPERATIONS.md) → [Packaging](internal/PACKAGING.md) |
| Work on releases or provenance | [Release Runbook](internal/RELEASE_RUNBOOK.md), [release tooling](../eng/release/README.md), [IP Governance](internal/legal/IP_GOVERNANCE.md), [transfer provenance](internal/extraction/PROVENANCE.md) |
| Investigate Terminal.Gui dependency history | [Dependency decision](internal/TERMINAL_GUI_DEPENDENCY.md) |
| Improve documentation | [Documentation architecture](internal/DOCUMENTATION_ARCHITECTURE.md) |
| Work as an AI coding agent | [AGENTS.md](../AGENTS.md) → matching intent and path rules |

Public guides explain what to do; technical references explain why it works and
where it is enforced. Changes to externally visible behavior update both in the
same increment. Keep historical reports distinct from current contracts and
label planned or unqualified behavior explicitly. The
[documentation architecture](internal/DOCUMENTATION_ARCHITECTURE.md) owns this
policy and its public/internal reference matrix.
