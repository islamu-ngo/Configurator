# ISLAMU Configurator

An MIT-licensed offline configuration preparation utility for self-hosted
applications. A bounded data-only target definition describes fields, validation
and output. The target application remains responsible for accepting and
applying the resulting artifact.

The repository separates a reusable Core, protected native Artifacts and shared
Presentation from four adapters: machine CLI, Terminal.Gui TUI, Avalonia Desktop
and public-only static Blazor WebAssembly. Native answer-derived output stays
restricted; the browser accepts public metadata only.

## Development

Install .NET SDK 10.0.302. `global.json` selects Microsoft.Testing.Platform;
`Directory.Packages.props` owns package versions. See [PROJECTS.md](PROJECTS.md)
for targeted commands and [AGENTS.md](AGENTS.md) for contribution boundaries.
`Configurator.slnx` registers all seven product owners, focused suites, schema
generators, engineering tools and dependency probes. Root build configuration
also applies to file-based engineering commands.

The audited `ISLAMU.Terminal.Gui` downstream package uses the local feed under
`eng/release/dependencies/terminal-gui`; its identity and component notices are
preserved. Do not substitute upstream Terminal.Gui. Signed release publication
is fail-closed until Configurator has its own trust authority.

See [standalone packaging](docs/public/packaging.md) for native executable paths,
supported packaging RIDs, Linux-only protected exports and the independent static
browser output. CI assembles unsigned validation packages without deployment.

## Client entry points

| Client | Start from the checkout | Packaged entry point |
| --- | --- | --- |
| Machine CLI | `dotnet run --project src/Configurator.Cli --configuration Release -- doctor --machine` | `islamu-configurator` |
| Terminal | `dotnet run --project src/Configurator.Terminal --configuration Release -- --definition examples/minimal-self-hosted/setup-definition.json` | `islamu-configurator-terminal` |
| Desktop | `dotnet run --project src/Configurator.Desktop --configuration Release -- /absolute/path/setup.json /absolute/path/output-directory` | `islamu-configurator-desktop` |
| Public browser | Publish `Configurator.Browser`, then serve its complete `wwwroot/` | `/` catalogue and `/launch/<familyId>` |

The CLI registers `catalogue list|show|describe`, `artifact
create|open|validate|format|diff|coverage|export|import|preview`, `env
render|validate`, and `doctor`. Use `--help` for operation-specific options and
`--machine` for the closed JSON envelope. Select a local definition explicitly;
there is no implicit Event target or remote apply command. See the
[CLI guide](docs/public/cli.md), [terminal guide](docs/public/terminal.md) and
[desktop guide](docs/public/desktop.md).

The bundled neutral public family can be opened at
`/launch/69cdb122-41d2-4fab-9b82-7929a889c726?appVersion=1.0&entry=setup&locale=en`.
This route selects registered public data locally and pins its revision/digest;
it carries no credentials or target authority. See
[launch integration](docs/public/launch-integration.md) and the
[public browser guide](docs/public/browser.md) for selector bounds and hosting.

## License and provenance

ISLAMU-owned transferred material is MIT under the Project Steward's explicit
direction. Third-party components retain their own terms. See
[LICENSE](LICENSE), [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and the
[foundation inventory](docs/internal/extraction/FOUNDATION.md).
