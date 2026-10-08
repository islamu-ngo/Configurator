# Definition-driven Terminal.Gui adapter

The client retains the approved `ISLAMU.Terminal.Gui` dependency identity and
archive closure. It references Core, Artifacts and Presentation; no Event wire
DTOs, built-in catalogue, HTTP client or implicit application selection remain.

## Data and control flow

`SetupTerminalDefinitionSelection` reads bounded local files, admits JSON/YAML
through `SetupDefinitionCodec`, or explicitly resolves a family and optional
application version through `SetupDefinitionRegistry`. Definition bytes are
cleared after admission. Registry ambiguity is Core's decision, not a newest-
revision heuristic in the terminal.

`SetupTerminalApplication` owns one `DefinitionFormModel` and one presentation
workspace. `SetupTerminalFieldEditor` expands object properties into scalar
leaves and edits lists one scalar item at a time. It serializes only the edited
object/list answer back to the parent-owned typed field input. It is not a raw
definition or whole-answer JSON editor. Visibility, effective sensitivity,
snapshots, input revisions, activation and validation remain parent-owned.
Nested leaves use the strongest parent sensitivity to prevent disclosure.

`SetupTerminalWindow` uses native focusable controls with keyboard buttons,
bounded layout and English/Arabic resource parity. Small terminals hide editing
controls rather than overlapping them. The current editor is committed before
validation, preparation or navigation; type errors prevent that transition.
Private editors hold bullets only in `TextField.Text`; clearable character
buffers hold the input. Clipboard/context/history commands are blocked.

`SetupTerminalArtifactOperation.PrepareAsync` runs the shared
`DefinitionPresentationOperation` over an immutable answer snapshot. Only
value-free preparation metadata is shown. The adapter owns the restricted
prepared byte array and clears the handoff output copy. Input or artifact changes
invalidate bytes and deactivate/reactivate the workspace to cancel in-flight
saves and discard old results. Filename changes also cancel an in-flight save.
The writer receives only a simple filename rooted in the working directory;
definition filenames confer no filesystem authority.

Saving executes through `SetupPresentationWorkspace.ExecuteCommand`, which
owns operation generations and cancellation. The operation stages bytes through
`IProtectedArtifactWriter` with restricted `Configuration` policy for every
format, then checks cancellation before atomic commit. Bytes are zeroed on
success, rejection or cancellation; existing targets and unsupported platforms
fail closed. No private output is placed in the workspace's public output.
The UI retains public fields but clears protected model inputs after settlement,
explicit clearing, close, disposal and handled termination signals. Navigating
between fields clears the transient editor, not the masked multi-field answer
set. Immutable Core snapshots/.NET strings are not claimed to be zeroizable.

## Event oracle disposition

Removed terminal oracles relied on APIs Core no longer exposes. They are not
counted as passing Configurator tests and must be retained on Event's side:

| Prior oracle | Disposition / replacement |
| --- | --- |
| Platform catalogue keys and classifications | Event exporter owns those keys; generic disclosure, sensitivity and activation tests now cover the adapter. |
| Manifest/tenant source and section bytes; local document compare | Event portability/parser tests own target bytes and sections. Generic artifact vectors cover declared projections; terminal compare is not a definition-driven requirement. |
| Legal identity placeholders and approved draft DTOs | Event legal contracts own those vectors. Declared Markdown artifacts use the common restricted prepare/save path. |
| Operator identity DTO revisions and digest validation | Event identity contracts own those vectors. Generic identity JSON uses structured fields, definition admission and the protected writer. |
| Six Event workspace names and Event-specific navigation | Replaced by definition fields/disclosure/artifact navigation and native focus tests. |
| Fixed `SETUP_SECRET` generated environment output | Event owns key/entropy policy. Terminal accepts declared typed fields; no target-specific generator or placeholder substitution remains. |

Generic invariants remain active: command-line answer rejection, masked native
typing, clipboard/history blocking, bounded Unicode buffers, stale preparation
invalidation, disclosure without validation relaxation, owner-only/create-only
output, anonymous staging, pre-cancel cleanup, cancellation at an explicit commit
barrier, signal cleanup and small-viewport focus safety. Tests use observable
behavior, not source/prose scraping or sleeps.

## Targeted verification

Run from Configurator:

```bash
TMPDIR="$HOME/.cache/agent-tmp" dotnet build src/Configurator.Terminal/Configurator.Terminal.csproj --configuration Release --verbosity quiet
TMPDIR="$HOME/.cache/agent-tmp" dotnet test --project tests/Configurator.Terminal.Tests/Configurator.Terminal.Tests.csproj --configuration Release -- --minimum-expected-tests 1
```

The parent workstream owns solution registration and final real-terminal QA.
This unit does not start the interactive app during intermediate implementation.
