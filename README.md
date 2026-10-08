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
During extraction, projects enter `Configurator.slnx` only after their owner
verifies readiness. Root build configuration already applies to unregistered
projects.

The audited `ISLAMU.Terminal.Gui` downstream package uses the local feed under
`eng/release/dependencies/terminal-gui`; its identity and component notices are
preserved. Do not substitute upstream Terminal.Gui. Signed release publication
is fail-closed until Configurator has its own trust authority.

## License and provenance

ISLAMU-owned transferred material is MIT under the Project Steward's explicit
direction. Third-party components retain their own terms. See
[LICENSE](LICENSE), [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and the
[foundation inventory](docs/internal/extraction/FOUNDATION.md).
