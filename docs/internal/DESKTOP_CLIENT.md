# Definition-driven Avalonia Desktop

Desktop remains on the centrally pinned Avalonia 12.1.3 packages. It references
Presentation and Artifacts, with Core types available through those references.
It has no target-specific DTO, catalogue, manifest, or identity workflow.

## Entry and adapter API

`App.OnFrameworkInitializationCompleted` creates a
`SetupDesktopShellViewModel` and `MainWindow`. The desktop lifetime accepts zero,
one, or two positional arguments: explicit local definition path and optional
existing output directory. No definition is selected by default.

The shell exposes:

- `LoadDefinitionAsync(CancellationToken)`: read the explicit local path within
  the 4 MiB source ceiling, admit JSON or YAML, and clear obsolete state. The read
  accepts at most one extra byte to detect a file growing past the ceiling.
- `LoadDefinition(ReadOnlyMemory<byte>, bool yaml = false)`: admit a supplied
  local document through `SetupDefinitionCodec`; useful for host integration and
  tests without a filesystem read.
- `Form`, `Artifacts`, `ArtifactId`, `SelectedArtifactIds`, `DisplayMode`, `DefinitionPin`,
  `ArtifactConsequences`: typed editing and byte-free metadata. `Form` is local
  editor state containing answers, not a public/remote projection.
- `Validate()`, `PrepareAsync()`, `Preparation`, `CanPrepare`, `CanSave`,
  `SaveAsync(CancellationToken)`, `Cancel()`: validation, snapshot preparation,
  consumptive protected publication, and invalidation.
- `ExistingFilePath`, `ImportFileAsync()`: read a bounded local existing file for
  the selected declared JSON, YAML, or dotenv artifact. Shared
  `DefinitionFormModel.ImportAsync` admits supported inverse projections and
  commits imported answers only while the original revision is current.
- `SelectArtifact(id, selected)`, `PrepareBundle()`, `CanSaveBundle`,
  `SaveBundleAsync()`: select declared outputs, compose a deterministic bundle,
  and publish it through the shared protected bundle publisher. The selected
  single artifact remains a separate preparation action.
- `DefinitionPath`, `OutputDirectory`, `FileName`: operator-selected local
  inputs. `Status` contains value-free diagnostic codes and IDs.

## Editing and presentation flow

`DefinitionFormModel` owns a frozen admitted definition, typed field input text,
input revision, validation, and cloned snapshots. Desktop's `FieldEditor`
renders native TextBox/ComboBox controls, recursively renders declared object
properties, and maintains scalar list rows with add/remove buttons. Structured
controls serialize their local values into the shared typed input; the operator
does not edit JSON. An untouched blank nested control remains absent, so it
cannot create a false conflict during existing-file import. An invalid child
scalar invalidates the whole structured input rather than silently omitting it.

`MainWindow` creates controls once per admitted form and updates visibility and
errors without rebuilding on each keystroke. This preserves typing focus.
Essential mode is a desktop narrowing of Guided: it retains required, invalid,
protected, consequential, and essentially disclosed active fields. Advanced
shows all active fields. Shared Core validation always sees all answers,
independent of disclosure. Effective parent sensitivity masks all child controls;
selected protected enum/boolean values use a masked selection display.

Native control names and help are set through Avalonia AutomationProperties.
Tab order follows the logical form, access keys use native buttons, and keyboard
shortcuts route through Window KeyDown. Validation focuses errors; list mutation
preserves usable focus. The RTL switch sets FlowDirection. Headless control
tests check actual focus and key routing, not just label/property presence.

## Preparation and protected publication

For preparation, the shell captures `Form.Snapshot()` and the pinned definition,
then creates a private `SetupPresentationWorkspace` with
`DefinitionPresentationOperation`. Presentation fences completion by operation
generation; the shell additionally rejects obsolete adapter completions.
`DefinitionPreparationMetadata` is the only preparation exposed publicly.
Artifact bytes stay in the private workspace.

Save takes a private byte copy and immediately consumes and clears the
workspace output. The shell validates the simple file name and passes the
explicit directory plus name to `ProtectedArtifactWriter.PrepareAsync` with
`SetupArtifactKind.Unknown`: this is deliberately the generic restricted kind,
not a target-specific ID-to-kind mapping. Every declared artifact format uses
the same boundary. `CommitAsync` enforces native create-only publication and
Linux owner-only permissions; no definition value grants filesystem authority.
Unsupported hosts fail closed.

Bundle preparation verifies the selected IDs through `DefinitionBundleComposer`
against the current snapshot, rejecting empty, inactive or uncomposable output.
It immediately disposes the temporary bundle, retaining only an answer revision
and declared IDs, never composed bytes. Admission rejects case-insensitive
filename conflicts. Save rechecks host protection capability and composes
against the current snapshot immediately before
publication and disposes the Core bundle afterward to zero its private buffers.
Each answer, definition, destination, or selection change discards it.
Publication consumes the bundle and preflights every target before committing
any file. Its ordered statuses distinguish files already written from a failed
or unattempted remainder. A conflict leaves existing files untouched; after
resolving it, the operator prepares again. After a partial commit, the operator
leaves written outputs in place, selects only remaining outputs, and prepares
again. There is no overwrite or rollback claim. The UI displays artifact IDs
and status codes, never protected bytes or local paths. The capability notice
reflects the native writer on the current host.

Input, destination, definition, or artifact changes increment the adapter
generation, cancel pending save work, dispose preparation, and zero owned output
buffers. Save checks its captured generation before publishing status, so a
writer that completes after cancellation cannot overwrite new form state.
Save failure, cancellation, and success all consume preparation. The final
private save copy is zeroed in `finally`. Closing unloads the form and disposes
the session. .NET input strings cannot promise secure memory erasure; the form
clears references on disposal and no prepared bytes enter status or logs.

## Verification

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet build src/Configurator.Desktop/Configurator.Desktop.csproj --configuration Release --verbosity quiet
dotnet test --project tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj --configuration Release -- --maximum-parallel-tests 1
```

The focused suite exercises real Avalonia headless controls, nested and list
editing, focus/keyboard routing, disclosure-independent readiness, generic
artifact formats, definition pinning/reload, unsafe names, protected metadata,
buffer clearing, consumptive saves, cancellation, stale completion, and the
actual Linux create-only writer through the shell. Parent-owned final visual QA
and screenshots remain separate from this unit's headless verification.

See the [public workflow](../public/desktop.md) and
[Core definition contract](CORE_DEFINITION_CONTRACT.md).
