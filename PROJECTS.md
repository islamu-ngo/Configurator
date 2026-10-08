# Configurator Project Boundaries

The product dependency direction is clients -> Presentation/Artifacts -> Core.
Browser references Core only and ships public static data. No sibling Event
checkout, backend contracts or running service may be a build input.

| Owner | Project directory |
| --- | --- |
| Bounded definition engine | `src/Configurator.Core` |
| Protected native writes | `src/Configurator.Artifacts` |
| Shared workspace and operation generations | `src/Configurator.Presentation` |
| Machine CLI | `src/Configurator.Cli` |
| Terminal.Gui client | `src/Configurator.Terminal` |
| Avalonia Desktop client | `src/Configurator.Desktop` |
| Static public Blazor WASM | `src/Configurator.Browser` |

Corresponding focused suites live in `tests/Configurator.<owner>.Tests`.
`Configurator.slnx` registers all seven product owners and their suites, the
architecture suite, engineering tools, schema generators and dependency probes.

## Engineering commands

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

The approved Terminal.Gui package is reconstructed by its retained native
generator, not replaced with upstream Terminal.Gui. See
`eng/release/dependencies/terminal-gui/dependency-review.md`.

## Standalone packaging

`eng/configurator/Configurator.Release.proj` owns `PublishConfigurator` and
`PublishConfiguratorBrowser`. Native outputs live under
`artifacts/configurator/<surface>/<rid>/`; static browser output is
`artifacts/configurator/browser/wwwroot/`. See the
[public packaging guide](docs/public/packaging.md) and
[technical anchor](docs/internal/PACKAGING.md) for commands and host limits.

`tests/Configurator.Architecture.Tests` verifies compiled product boundaries,
evaluated release contracts and actual package closure. Publish all three Linux
surfaces and browser before running its complete suite. A package build is not
evidence that every product or engineering suite passes; record their actual
commands and outcomes separately.
