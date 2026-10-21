# PROJECTS.md - Project Boundaries & Verification Map

> **Audience:** Contributors | Developers | AI Agents
> **Status:** Authoritative
> **Primary Solution:** [`Configurator.slnx`](Configurator.slnx)
> **Source Anchors:** [`AGENTS.md`](AGENTS.md), [`AI_REVIEW.md`](AI_REVIEW.md), [`docs/internal/QUICK_REFERENCE.md`](docs/internal/QUICK_REFERENCE.md), [`docs/internal/OPERATIONS.md`](docs/internal/OPERATIONS.md)

---

## 1. First-Use Project Map Workflow

`PROJECTS.md` is the authoritative map of verified solution structure, project responsibilities, protected paths, and targeted verification commands.

1. **Targeted Verification Default**: AI agents and developers must target verification against the specific affected project(s) by default. Broader solution-level builds are justified only by shared build settings, cross-project dependencies, solution wiring changes or workstream exit gates.
2. **Never Invent Placeholders**: Every project entry below corresponds to an actual `.csproj`. Never create or modify projects merely to satisfy a placeholder. Register only ready owners in the solution.
3. **No Secrets or Credentials**: Never record passwords, tokens, API keys or real connection strings in this document or any commit. Reproduction uses synthetic data or approved injected authorities.

---

## 2. Solution Structure & Project Roles

The product dependency direction is clients -> Presentation/Artifacts -> Core.
Browser references browser-safe Presentation and Core, never native Artifacts,
and ships public static data. No sibling Event
checkout, backend contracts or running service may be a build input.

| Owner | Project directory |
| --- | --- |
| Bounded definition engine | `src/Configurator.Core` |
| Protected native writes | `src/Configurator.Artifacts` |
| Shared workspace and operation generations | `src/Configurator.Presentation` |
| Machine CLI | `src/Configurator.Cli` |
| Terminal.Gui client | `src/Configurator.Terminal` |
| Avalonia Desktop client | `src/Configurator.Desktop` |
| Static Blazor WASM with transient local configuration | `src/Configurator.Browser` |
| Optional container transport serving only published public files | `src/Configurator.Browser.Host` |

Corresponding focused suites live in `tests/Configurator.<owner>.Tests`.
`Configurator.slnx` registers all seven product owners and their suites, the
optional static host, architecture suite, engineering tools, schema generators
and dependency probes.
The static host has no product project references and processes no answers.
Its HTTP behavior is qualified through the actual container; the existing
Browser package suite continues to check the static publication independently.

### Engineering & Dependency Owners

| Project Path | Role & Responsibilities |
|---|---|
| `eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj` | Intent validation and repository-native workflow policy. |
| `eng/agent-workflow/tests/Configurator.AgentWorkflow.Tests/Configurator.AgentWorkflow.Tests.csproj` | Focused workflow/intent contract tests. |
| `eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj` | Commit/change provenance, bounded release inputs, rendering and signed-evidence validation. |
| `eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj` | Generic release invariants using local inert fixtures, not Event publication authority. |
| `eng/configurator/Configurator.DefinitionSchemaGenerator/Configurator.DefinitionSchemaGenerator.csproj` | Definition schema generation and freshness checks. |
| `eng/configurator/Configurator.CommandSchemaGenerator/Configurator.CommandSchemaGenerator.csproj` | Generic machine command schema generation and freshness checks. |
| `eng/configurator/probes/communitytoolkit/Configurator.DependencyProbes.CommunityToolkit.csproj` | Approved MVVM dependency closure. |
| `eng/configurator/probes/dependency-injection/Configurator.DependencyProbes.DependencyInjection.csproj` | Approved DI dependency closure. |
| `eng/configurator/probes/yamldotnet/Configurator.DependencyProbes.YamlDotNet.csproj` | Approved YAML dependency closure. |

---

## 3. Test Projects & Coverage Targets

| Test Project | Boundary Under Test | Verification Scope |
|---|---|---|
| `tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj` | Definition admission, canonical identity, validation and composition | Fast in-memory invariants |
| `tests/Configurator.Artifacts.Tests/Configurator.Artifacts.Tests.csproj` | Restricted native writes, conflicts and unsupported-host rejection | Affected protected-write class/host |
| `tests/Configurator.Presentation.Tests/Configurator.Presentation.Tests.csproj` | Immutable workspaces and stale-result fencing | Affected shared-operation class |
| `tests/Configurator.Cli.Tests/Configurator.Cli.Tests.csproj` | Command vocabulary, machine envelopes and artifact workflows | Affected CLI behavior |
| `tests/Configurator.Terminal.Tests/Configurator.Terminal.Tests.csproj` | Native terminal controls and transient input lifecycle | Affected headless/native surface |
| `tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj` | Avalonia controls, selected state and protected-save workflow | Affected headless/native surface |
| `tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj` | Real classic Desktop lifetime, X11 controls, imports, protected writes and Event conformance | Separate executable; Linux x64/X11 only, named nonempty guards |
| `tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj` | Launch pinning, secret consent, transient custody, drafts, exports and offline policy | Affected bUnit/registry behavior; published-platform QA separately |
| `tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj` | Product boundaries, actual published packages and policy gates | Workstream packaging/closure exit |

Select tests by the changed behavior, not by the number of files. These scopes
are not promises of measured latency. No Event database, API or provider matrix
belongs to an offline Configurator verification run.

---

## 4. Protected Paths (Explicit Task Authority Required)

The following paths represent sensitive boundaries or generated artifacts.
Modify them only when the developer's assigned task authorizes that scope;
existing task authorization does not require a second routine approval.

| Protected Path | Nature of Protection | Authorized Procedure |
|---|---|---|
| `schemas/`, `eng/configurator/generated/` | Generated schemas and contract ratchets | Fix the owning model/generator, regenerate natively and run its freshness check. Do not hand-edit the output. |
| `**/packages.lock.json` | Generated dependency closure | Change central versions/actual references, regenerate with native restore and verify the resolved closure. |
| `eng/configurator/dependency-notices/` | Reviewed supplemental notices for embedded dependency assets | Review exact package content and terms before updating a supplement; publish only with its matching restored package. |
| `.ci/**`, `.github/workflows/**`, `eng/release/**` | CI/release automation and provenance | Use assigned engineering scope, preserve read-only fork execution and fail-closed publication. No credential/trust transfer. |
| `LICENSE`, `THIRD-PARTY-NOTICES.md`, `docs/internal/legal/**` | Licensing and third-party obligations | Preserve Steward MIT authority boundaries and every component's own terms/provenance. |
| `docs/internal/universal-configurator.md`, `docs/internal/go-to-market.md` | User-owned design/strategy documents | Preserve unmodified unless the user explicitly assigns their editing. |

---

## 5. Targeted Verification Commands

Run from this repository with SDK 10.0.302:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet build eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release --verbosity quiet
dotnet run --project eng/agent-workflow/src/Configurator.AgentWorkflow/Configurator.AgentWorkflow.csproj --configuration Release -- validate-intents .agents/contract/intents.yaml
dotnet test --project eng/agent-workflow/tests/Configurator.AgentWorkflow.Tests/Configurator.AgentWorkflow.Tests.csproj --configuration Release
dotnet build eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release --verbosity quiet
dotnet test --project eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj --configuration Release
```

For a product change, replace the project path with the affected owner, build in
Release, then run that owner's suite with MTP's `--project`. Do not run all
product suites for tooling-only changes. Protected-save, definition admission
and launch changes require invariant tests as well as their client surface.

Terminal.Gui uses the official NuGet package, an exact central version and
generated dependency locks. Packaging retains reviewed notices for its embedded
grammar/theme assets. No downstream archive, patch or upstream rebuild is owned
here. See `docs/internal/TERMINAL_GUI_DEPENDENCY.md` for the decision and history.

### Inner Loop - Fast Subtask Slicing

For the concrete affected in-memory class, use the MTP `--treenode-filter`
syntax shown in the testing guide. Do not add sleeps, polling delays, database
containers or network I/O to an algorithmic inner-loop test.

### Phase Exit - Target Project Build & Test

```bash
dotnet build src/Configurator.Core/Configurator.Core.csproj --configuration Release --verbosity quiet
dotnet test --project tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1
```

Use the actual affected owner from the tables above. Required security/privacy
invariants establish Red before the relevant fix; preserve unchanged passes and
run coherent source/caller/docs/regression slices continuously to phase exit.

### Plan Exit & PR Readiness

```bash
dotnet restore Configurator.slnx --locked-mode
dotnet build Configurator.slnx --configuration Release --verbosity quiet
dotnet format Configurator.slnx --verify-no-changes --verbosity quiet
```

Run relevant suites once at exit, with nonempty test-count guards. Pure prose
changes use link/anchor/format checks, not product builds. Tooling-only changes
use their native engineering owner; no unrelated product or Event suites.

## Standalone packaging

Desktop native qualification runs separately from MTP/headless tests:

```bash
dotnet build tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --verbosity quiet
dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release --no-build -- --expected-rid linux-x64 --minimum-expected-checks 10
```

Its thirteen required named scenarios reject missing/wrong native backend,
failed checks and incomplete counts. It does not qualify manual native dialogs,
assistive technology, a published payload or installers; those require separate
evidence. See [Desktop qualification](docs/internal/DESKTOP_CLIENT.md#native-qualification).

`eng/configurator/Configurator.Release.proj` owns `PublishConfigurator` and
`PublishConfiguratorBrowser`. Native outputs live under
`artifacts/configurator/<surface>/<rid>/`; static browser output is
`artifacts/configurator/browser/wwwroot/`. See the
[public packaging guide](docs/public/packaging.md) and
[technical anchor](docs/internal/PACKAGING.md) for commands and host limits.

`eng/configurator/package-desktop-arch.sh` freshly publishes and assembles a local
unsigned Desktop `.pkg.tar.zst` using native `makepkg`. It requires x86_64 Arch
and the documented build tools. Its focused native owner check is
`bash eng/configurator/test-desktop-arch-package.sh <older-archive> <newer-archive>`;
this uses real pacman in a read-only-host user namespace and checks install,
upgrade, removal, payload equivalence, operator-data preservation and installed
X11/Wayland-XWayland launches. It requires a Wayland session with XWayland,
`bubblewrap`, `strace` and `xorg-server-xvfb`. Run
`bash -n` on both scripts and `desktop-file-validate` on the desktop entry.
Broader unchanged product suites are not package assembly checks. X11 and
Wayland/XWayland installed launch evidence is separate from release qualification.

`tests/Configurator.Architecture.Tests` verifies compiled product boundaries,
evaluated release contracts and actual package closure. Publish all three Linux
x64 surfaces and Browser before running its complete current suite. The
Windows x64, macOS arm64 and Linux arm64 actual-host package and restricted-save
matrix is Planned; cross-publishing does not establish host behavior. A package
build is not evidence that every product or engineering suite passes; record
their actual commands and outcomes separately.

---

## 6. Verification Evidence Report Contract

Every task completion, milestone report or PR description MUST provide a
concrete **Verification Evidence Report**:

1. **Commands Executed & Exact Results**: State each command run, exit code,
   test counts (passed, failed, skipped) and execution duration.
2. **Checks Not Executed & Technical Justification**: Explicitly list relevant
   checks omitted and why; an engineering pass does not imply a product pass.
3. **Zero Fictitious Evidence**: Never state, imply or suggest that a build or
   test suite passed if the command was not actually executed in the session.

Pre-existing unrelated failures are findings, not an invitation to weaken tests
or repair another workstream. Reproduce/record the evidence proportionally,
quarantine it in task context and continue the assigned deliverable.
