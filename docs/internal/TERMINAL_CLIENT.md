# Definition-driven Terminal.Gui adapter

The client uses official Terminal.Gui with an exact central package pin and
generated locks. Its published package retains reviewed embedded-asset notices;
see [the dependency decision and history](TERMINAL_GUI_DEPENDENCY.md).
It references Core, Artifacts and Presentation; no Event wire
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
validation, import, preparation or navigation; type errors prevent that transition.
Private editors hold bullets only in `TextField.Text`; clearable character
buffers hold the input. Clipboard/context/history commands are blocked.

The artifact cursor cycles admitted outputs; a separate keyboard button toggles
each output in the selected set. At least one must be selected. `PrepareAsync`
checks `DefinitionBundleComposer` over one immutable answer snapshot, then keeps
only its input revision and selected count. Private composed bytes are not kept
between UI actions. The scoped Core bundle is disposed after preparation and
after publication, clearing its restricted byte storage. Input, selection or
cursor changes invalidate readiness and
deactivate/reactivate the workspace to cancel in-flight saves and discard old
results. On save, the operation recomposes the selected bundle from the current
revision and passes it to `ProtectedArtifactBundlePublisher` in the workspace
generation. Declared filenames stay inside the selected working directory; the
definition's filename is never treated as an arbitrary path authority.

The import field accepts a local file for the artifact at the cursor, whether or
not that artifact is selected for output. A bounded read goes through
`DefinitionFormModel.ImportAsync`; its Core mapping admits only supported direct
copies and rejects conflicting, transformed or inactive inputs without changing
answers. The temporary adapter-owned read buffer is zeroed, and the path field
is cleared after the attempt. Successful import refreshes the current editor and
focuses it. Status contains bounded result codes, never file contents or paths.

Saving executes through `SetupPresentationWorkspace.ExecuteCommand`, which
owns operation generations and cancellation. The bundle publisher prepares all
destinations through `IProtectedArtifactWriter` before committing any file; each
commit is create-only and protected. Existing targets and unsupported platforms
fail closed. A later commit failure can leave a committed prefix, so the window
shows a partial-save notice, counts and each artifact ID/status through a
keyboard-operated Next output result button. The operator reviews created files
before retrying; no automatic deletion or overwrite occurs. No private output
is placed in the workspace's public output.
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
