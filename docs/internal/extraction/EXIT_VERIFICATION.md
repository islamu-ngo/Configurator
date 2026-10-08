# Standalone extraction verification

All destination commands ran from Configurator with SDK 10.0.302 and
`TMPDIR="$HOME/.cache/agent-tmp"`. This record distinguishes product readiness
from the separately gated Event source cutover. No remote publication, pull
request, deployment, branch switch or worktree was performed.

## Product and engineering results

The seven Release product suites passed with nonzero minimum-count guards:
Core: 63, Artifacts: 14, CLI: 42, Presentation: 20, Terminal: 35, Desktop: 23,
Browser: 57. Architecture: 35 passed after refreshing actual packages.
Commands used Microsoft.Testing.Platform, for example:

```bash
dotnet test --project tests/Configurator.Terminal.Tests/Configurator.Terminal.Tests.csproj --configuration Release --no-restore --verbosity quiet -- --minimum-expected-tests 1
dotnet test --project tests/Configurator.Cli.Tests/Configurator.Cli.Tests.csproj --configuration Release --no-restore --verbosity quiet -- --minimum-expected-tests 1
dotnet test --project tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj --configuration Release --no-restore --verbosity quiet -- --minimum-expected-tests 1
dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --no-restore --verbosity quiet -- --minimum-expected-tests 1
```

The independent privacy review executed Core, Artifacts, Presentation and Browser
through the same explicit project command, in a scoped owner loop. AgentWorkflow
passed 2 cases. The complete release-engineering command passed 405 cases, zero
failures/skips in 6m 35.757s:

```bash
dotnet test --project eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1
```

The full run preceded two additional access-token regression cases. Those both
failed before restoring the detector; afterward ReleaseInputPolicyTests passed
24 cases and ReleaseArtifactPolicyTests passed 11, using the exact class filter:

```bash
dotnet test --project eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj --configuration Release --no-restore --verbosity quiet -- --treenode-filter "/*/*/*ReleaseInputPolicyTests/*" --minimum-expected-tests 1 --no-progress
dotnet test --project eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj --configuration Release --no-restore --verbosity quiet -- --treenode-filter "/*/*/*ReleaseArtifactPolicyTests/*" --minimum-expected-tests 1 --no-progress
```

The policy rejects both access-token and output-token assignments. Raw rejected
YAML scans remain behind the existing byte limit. Repository-history tests use
Configurator-owned fragment inventory rather than Event publication IDs. Inert
workflow/script fixtures preserve generic release invariants without installing
active publication or importing signer authority.

## Packaging and closure

Actual CLI, terminal and desktop Linux-x64 packages and independent static WASM
output were refreshed with:

```bash
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator -p:ConfiguratorSurface=cli -p:ConfiguratorRid=linux-x64 -verbosity:minimal
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator -p:ConfiguratorSurface=terminal -p:ConfiguratorRid=linux-x64 -m:1 -nr:false -verbosity:minimal
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfigurator -p:ConfiguratorSurface=desktop -p:ConfiguratorRid=linux-x64 -m:1 -nr:false -verbosity:minimal
dotnet msbuild eng/configurator/Configurator.Release.proj -t:PublishConfiguratorBrowser -m:1 -nr:false -verbosity:minimal
dotnet run .ci/scripts/write-artifact-checksums.cs -- artifacts/configurator artifacts/configurator/checksums.sha256
```

All commands exited 0. Package tests inspected those outputs, including original
dependency notices, schemas, runtime contracts and absence of Event/test assembly
leaks. An earlier combined refresh hit the monitor deadline during Desktop; only
unfinished packages were resumed with serialized MSBuild and a bounded release
budget, then the final terminal correction was republished.

Every authored csproj was evaluated with
`dotnet msbuild "$project" -getProperty:AssemblyName,RootNamespace -verbosity:quiet`.
All 25 evaluated assembly/namespace identities use Configurator.*.

```bash
dotnet run .ci/scripts/validate-action-pins.cs -- .github/workflows
dotnet run .ci/scripts/validate-dependency-license-policy.cs -- .
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator --configuration Release --no-build -- --check schemas/setup-definition-v1.schema.json
dotnet run --project eng/configurator/Configurator.CommandSchemaGenerator --configuration Release --no-build -- --check
dotnet run eng/configurator/GenerateConfiguratorRatchets.cs -- --check
```

These passed: 3 external Action pins, 83 admitted package/version pairs and all
schema/command/ratchet freshness checks. The complete restored owner/probe audit
used `dotnet list "$project" package --vulnerable --include-transitive --format
json --no-restore` followed by the native vulnerability validator for each
runtime, test, schema tool, engineering owner and probe; all exited 0.

## Real surfaces and Event handoff

The actual browser revision-4 Event launch rendered at 1440x1000 and 390x844
without horizontal overflow. Selecting an artifact reveals only active public
enum/boolean controls. The terminal rendered visibly usable one-row buttons
at 120x40 after disabling the native shadow with null; initialized viewport
height and nonoverlap are covered. Avalonia was observed on an isolated virtual
display with distinct footer controls and selected mode/artifact values.

All five current Event artifact outputs from the compiled CLI matched expected
bytes with native cmp, and Event DefinitionExporter --verify accepted each.
Event Wire conformance passed 13 cases; relocated SetupLive client tests passed
48. Exporter --check and --check-environment exited 0 with 361 environment
definitions. Native and browser share revision 4 and canonical digest
33661343436560b8004903d6efe569c6e3342d278f3fae6e9df49fad9e0e65bf.

## Limits and separate gates

Final destination integration passed locked restore, whole-solution Release
build and read-only formatting with zero warnings/errors:

```bash
dotnet restore Configurator.slnx --locked-mode --verbosity quiet
dotnet build Configurator.slnx --configuration Release --no-restore --verbosity quiet -m:1 -nr:false
dotnet format Configurator.slnx --no-restore --verify-no-changes --verbosity quiet
```

The native commit guard reserves `releases/changes/` for governed fragments.
Release input documentation therefore lives at `releases/README.md`; an absent
change directory in a fresh checkout is a valid empty inventory, not imported
Event history. The inventory class is reverified after that adaptation.

Event source cutover verification is recorded separately, not inferred from
destination success. The whole Event Release solution build succeeded after
cutover in 5m 32.83s with zero errors and 2,650 warnings. Sampled warning owners
in Application, Persistence and BFF are unchanged; this extraction did not
audit or repair the entire inherited warning inventory. The architecture suite
passed 660 of 661 cases; its new CI guard incorrectly rejected Event
fork-safe Codecov wiring. The guard now permits only the optional CODECOV_TOKEN
contract and its guarded expression, with negative mutations for unguarded
access, literal tokens and other token names. Coverage outcome names are not
secret context. The affected three-case class passed after correction, preserving
the 660 unchanged full-suite passes. Native `preflight-staged --target develop` passed with
one populated operator-impact fragment and the exact source-cutover packet.
No database/provider matrix ran because backend semantics did
not change. Non-Linux runtime execution/protected writes, screen readers,
braille, RTL shaping and managed-string erasure are unverified capabilities.
Missing editor LSP servers and C# diagnostic timeouts were not bypassed by
installing dependencies; actual builds/tests supplied compiler evidence.
Signed publication remains fail-closed until Configurator has its own authority.

Final native JSON validation and byte-identical agent rule twins passed. Local
link targets from all owned Event cutover and selected destination graduation
guides were checked: 236 references, one unrelated historical target missing.
The Mailpit provenance link in Event SELF_HOSTING.md predates extraction
(git blame at 7975687c5 attributes it to 5d2069e171 on 2026-09-08) and points
to an ignored earlier task artifact. It is quarantined without changing
unrelated self-hosting history. Extraction-owned handoff/ADR/backlog targets
exist. The authoritative active triad exists only in Configurator.
