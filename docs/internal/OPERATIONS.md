# Operations

> **Audience:** Contributors | AI agents
> **Status:** Implemented local engineering procedures; publication not activated
> **Owner:** Packaging/Release
> **Last Verified:** 2026-10-08
> **Source Anchors:** `AGENTS.md`, `PROJECTS.md`, `global.json`, `eng/agent-workflow/`, `eng/release/`

This selective transfer uses the AI-agent operational section from Event
revision `c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38`, with local verification
instructions anchored to Configurator owners. Event's backend operations,
databases, API/BFF, Aspire startup and production trust are not imported.
Configurator prepares artifacts offline; it does not apply target configuration
or acquire host administration authority.

## Operational Runbooks

- [Packaging technical anchor](PACKAGING.md) and [adopter guide](../public/packaging.md)
  own native/static output commands and platform limits.
- [Protected artifacts](PROTECTED_ARTIFACTS.md) and [native output guide](../public/native-output.md)
  own create-only save, privacy and failure behavior.
- [Launch contract](LAUNCH_CONTRACT.md) owns the local handoff boundary.
- [Release runbook](RELEASE_RUNBOOK.md) owns generated notes and local evidence.

Do not use service-recovery or direct-database instructions from Event as
Configurator operating procedures.

## Verification Policy

Use SDK 10.0.302 and Microsoft.Testing.Platform. Set the current workstation
temporary root before native tool/test execution:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
```

Run the targeted Release build and affected classes at coherent phase exit as
described in [TESTING.md](TESTING.md#the-3-ring-progressive-verification-model).
Preserve passing evidence; rerun only failed/newly affected checks. Broader
affected-owner and architecture/package checks run once at workstream exit.

Documentation-only changes verify local targets, anchors, formatting and
whitespace. They do not run product builds or .NET test suites. Never broaden a
tooling-only change into product suites or an Event database matrix.

### Verification Evidence

For each completion, report exact commands and outcomes, including exit status
and executed test selections. Record omitted checks and why; distinguish package
creation, test execution and measured coverage. Do not say a build or test passed
when it was not executed. Explicit tests omitted from an ordinary run are not
evidence for their behavior.

Report unrelated failures without absorbing them into the task. Prove baseline
origin where available and quarantine pre-existing rot in task-owned context.
No warning suppression, hidden skip or test weakening is an acceptable repair.

## Full Project Test List

The [project map](../../PROJECTS.md) owns project commands and package preconditions.
[TESTING.md](TESTING.md#test-projects) maps all product and engineering suites.
Use `dotnet test --project <affected-test-project.csproj> --configuration Release`
with actual paths, not a solution-level run or an absent source-system owner.

## AI Agent Operational Context

AI-agent workflow rules are not runtime operations. Keep them in
[AGENTS.md](../../AGENTS.md) and
[the context-engineering contract](../../.agents/CONTEXT_ENGINEERING.md) so
adopters do not have to scan agent tooling while using the utility.

The read-only `eng/agent-workflow` guard owns no state and never executes Git.
Use `validate-intents .agents/contract/intents.yaml` for YAML syntax and
`validate-commit -- git commit ... -- <literal-files>` to reject broad commit
pathspecs. Validation is not authorization to commit.

```bash
dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release -- \
  validate-intents .agents/contract/intents.yaml
dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release -- \
  validate-commit -- git commit --only -m "message" -- src/ExactFile.cs docs/ExactFile.md
```

The literal file examples illustrate validation grammar, not actual product
paths to stage. Select only owned paths when committing is explicitly authorized.
Never switch the shared checkout, create a worktree or alter another contributor's
changes contrary to the current task.

ReleaseEngineering separately validates commit syntax, immutable Change-Ids and
local release evidence. See its [operating instructions](../../eng/release/README.md).
No credentials, Event signer lists or machine MCP settings are transfer inputs.
