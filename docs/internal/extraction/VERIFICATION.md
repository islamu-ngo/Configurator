# Repository Foundation Verification

All commands ran from Configurator with `TMPDIR="$HOME/.cache/agent-tmp"`.
No product suite, Event edit, worktree, branch creation or repository commit was
performed. This report covers foundation ownership, not completed extraction.

## Observed successful commands

| Exact command | Outcome |
| --- | --- |
| `dotnet --version` | `10.0.302`, exit 0 |
| `dotnet build eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release --verbosity quiet` | Exit 0 |
| `dotnet build eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release --verbosity quiet` | Exit 0 after unsuppressed analyzer fixes |
| `dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release --no-build -- validate-intents .agents/contract/intents.yaml` | `intents_yaml_valid`, exit 0; validates bounded single-document YAML, not every semantic schema condition |
| `dotnet test --project eng/agent-workflow/tests/Configurator.AgentWorkflow.Tests/Configurator.AgentWorkflow.Tests.csproj --configuration Release --verbosity minimal --no-progress` | 2 passed, 0 failed; 497 ms execution |
| `dotnet test --project eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj --configuration Release --verbosity minimal --no-progress --treenode-filter "/*/*/CommitPolicyTests/*"` | Full engineering test project compiled; selected 42 passed, 0 failed; 375 ms execution |
| `dotnet test --project eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj --configuration Release --no-build --verbosity minimal --no-progress --treenode-filter "/*/*/SecurityChangeSelectionTests/*"` | 17 passed, 0 failed; 305 ms execution |
| `dotnet run .agents/hooks/BuildCheck.cs -- eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj` | Scoped Release build succeeded, exit 0; hook returns build failure rather than advisory success |
| `dotnet build eng/configurator/probes/communitytoolkit/Configurator.DependencyProbes.CommunityToolkit.csproj --configuration Release --verbosity quiet` | Build succeeded, exit 0 |
| `dotnet build eng/configurator/probes/dependency-injection/Configurator.DependencyProbes.DependencyInjection.csproj --configuration Release --verbosity quiet` | Build succeeded, exit 0 |
| `dotnet build eng/configurator/probes/yamldotnet/Configurator.DependencyProbes.YamlDotNet.csproj --configuration Release --verbosity quiet` | Build succeeded, exit 0 |
| `dotnet build eng/release/dependencies/terminal-gui/probe/Configurator.DependencyProbes.TerminalGui.csproj --configuration Release --verbosity quiet` | Build succeeded, exit 0 |
| `dotnet restore eng/release/dependencies/terminal-gui/probe/Configurator.DependencyProbes.TerminalGui.csproj --force-evaluate --verbosity quiet` | Exit 0; restored exact approved closure metadata through central authorities |
| `dotnet run eng/release/dependencies/terminal-gui/VerifyTerminalGuiPackage.cs -- --write` | Generated approved package evidence/SBOM, exit 0 |
| `dotnet run eng/release/dependencies/terminal-gui/VerifyTerminalGuiPackage.cs -- --check` | Verified exact approved archive and 21 components, no TextMate graph, exit 0 |
| `dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release --no-build -- sync-public-changelog --check` | Expected exit 2: `changelog_trusted_bundle_invalid`; absent Configurator trust fails closed |
| `sh -n .githooks/commit-msg .githooks/pre-commit` | Exit 0 |
| `bash -n eng/release/dependencies/terminal-gui/BuildTerminalGuiPackage.sh` | Exit 0 |
| `git diff --check` | Exit 0 |
| `git check-ignore eng/release/dependencies/terminal-gui/feed/ISLAMU.Terminal.Gui.2.4.17-islamu.1.nupkg` | Expected exit 1; audited archive is not ignored |

Native `install -D -m 0644` transferred the approved binary archive. Its observed
SHA-256 was `d61e31f90f58875bc8e8632a96b1882eb718b265f66da0309c816ca9510049ec`.
Source/destination approval JSON SHA-256 matched
`bcd58324fbc2a7a2493abd4be1f97f483baa6a49b5fae177c1c0e51a5e5cafef`.

Native `cmp` verified every `.agents/rules/*.md` against its `.omo/rules` twin.
The excluded example-site directory and Event trust directory are absent.
Native-read inspection checked 347 harness files: required skill metadata fields
and the 250-line limit passed; local Markdown resource links resolved outside
fenced example content. The 502 selected source harness entries are all accounted
for, including 154 explicit corpus exclusions. LSP found no errors in the touched
C# tooling and corrected probe files.

## Failures resolved during verification

- Paginated source-read assembly initially inserted blank boundary lines, causing
  some whole-file native move patches to reject. The reader preserved exact
  newlines, removed harness appendices, and all remaining moves were completed.
- Strict analyzers exposed a repeated runtime asset array plus 12 inherited
  engineering-test issues. Shared constant expectations/serializer options,
  invariant date parsing and guaranteed fixture disposal fixed them without
  suppressions or weaker assertions.
- Dotted project identities initially entered two probe class identifiers.
  Project/namespace identities remain Configurator.*, while ordinary class names
  became `ProbeViewModel`, `ProbeComposition` and `ProbeService`; both build.
- Building the dependency probe regenerated three approved `CentralTransitive`
  records as ordinary `Transitive` records when their central declarations were
  missing. Runtime bytes were identical, but the unchanged closure ratchet
  correctly rejected metadata drift. Adding the source 10.0.10 declarations for
  Configuration.Abstractions, DependencyInjection.Abstractions and
  Logging.Abstractions and restoring natively recovered the exact approved hash.
- Copied generated evidence had an extra final LF. The native evidence generator
  restored exact serialization without changing approval or package hashes.
- The inherited `[Rr]elease/` ignore rule hid `eng/release`, despite a package
  exception. Explicitly unignoring that engineering directory made the audited
  feed discoverable without unignoring build outputs.

## Checks not run and actual dependencies

No product suites or solution build ran: those belong to parent/Core and ready
product registration, and are explicitly outside foundation scope.

The complete release provider/workflow/rendering suite was not run. Its retained
tests consume parent-owned `.ci` scripts/`.github` workflows and an independently
supplied renderer/promotion authority. The selected native policy suite and full
test-project compilation are passing; that is not a claim that provider
publication is activated or all release integration tests pass.

The Terminal.Gui upstream rebuild was not run: native exact-archive transfer
preserved the authoritative ZIP hash, and the approved verifier checked its
package/assembly/patch/closure ratchets. NuGet pack is not byte reproducible.

The gitleaks executable is unavailable locally; configuration uses defaults
without Event's blanket directory allowlists, but no secret-scan pass is claimed.
Directory-level LSP selected unavailable Biome for mixed probe files; targeted
C# LSP and actual native builds supplied verification instead. No package was
installed merely to satisfy that unrelated editor-tool dependency.

Exact destination paths are in `foundation-changed-paths.txt`; transfer,
non-transfer and parent Event-adapter coverage dispositions are in
`transfer-manifest.json`. The source Event tests remain untouched for parent
relocation; their copied Configurator versions were not portable coverage.
