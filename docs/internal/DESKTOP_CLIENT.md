# Definition-driven Avalonia Desktop

Desktop remains on the centrally pinned Avalonia 12.1.3 packages. It references
Presentation and Artifacts, with Core types available through those references.
It has no target-specific DTO, catalogue, manifest, or identity workflow.

## Entry and adapter API

`App.OnFrameworkInitializationCompleted` resolves `DesktopShellViewModel` and
`MainWindow` from an application-owned dependency injection container. The
container supplies an isolated `IMessenger`, the native protected writer and
`IStorageDialogService`; its disposal closes the workspace and releases child
subscriptions. Storage provider lookup is deferred until a picker is requested,
avoiding a dependency cycle between the window and its viewmodel. The desktop lifetime accepts zero,
one, or two positional arguments: explicit local definition path and optional
existing output directory. No definition is selected by default.

The root coordinates generations, busy state, cancellation and value-free
status. It exposes three focused child viewmodels rather than forwarding their
operations:

- `Loader.LoadDefinitionAsync(CancellationToken)`: read the explicit local path within
  the 4 MiB source ceiling, admit JSON or YAML, and clear obsolete state. The read
  accepts at most one extra byte to detect a file growing past the ceiling.
- `Loader.LoadDefinition(ReadOnlyMemory<byte>, bool yaml = false)`: admit a supplied
  local document through `SetupDefinitionCodec`; useful for host integration and
  tests without a filesystem read.
- `Workspace.Form`, `Workspace.Fields`, `Workspace.DisplayMode`,
  `Loader.DefinitionPin` and `Publisher` artifact properties: typed editing and
  byte-free metadata. `Form` is local
  editor state containing answers, not a public/remote projection.
- `Workspace.Validate()`, publisher preparation/save operations and root
  `Cancel()`: validation, snapshot preparation,
  consumptive protected publication, and invalidation.
- `Publisher.ExistingFilePath`, `Publisher.ImportFileAsync()`: read a bounded local existing file for
  the selected declared JSON, YAML, or dotenv artifact. Shared
  `DefinitionFormModel.ImportAsync` admits supported inverse projections and
  commits imported answers only while the original revision is current.
- Publisher artifact selection, bundle preparation and saving: select declared outputs, compose a deterministic bundle,
  and publish it through the shared protected bundle publisher. The selected
  single artifact remains a separate preparation action.
- Loader definition path and publisher output directory/file name: operator-selected local
  inputs. `Status` contains value-free diagnostic codes and IDs; `StatusHelp`
  provides correction/retry guidance without values, paths or raw exceptions.
- `IStorageDialogService.SelectAsync(kind, token)`: a Desktop-only native selection boundary.
  `AvaloniaStorageDialogService` uses the window's `StorageProvider` for one local
  file or existing folder, checks capability and disposes storage items. It accepts only host-free local file
  URIs while its captured generation remains current. Cancellation, unavailable
  providers and nonlocal selections preserve answers/preparation. The accepted
  definition/import path feeds the existing bounded reader; folder selection
  sets the destination. No picker stream writes output. Typed paths remain.

## Editing and presentation flow

`DefinitionFormModel` owns a frozen admitted definition, typed field input text,
input revision, validation, and cloned snapshots. Desktop's `FormFieldFactory`
projects inputs into string, enum, numeric, boolean, object and list viewmodels.
Compiled `DataTemplate` bindings render native inputs and recursive collections
from that tree. Structured viewmodels serialize their local values into the shared typed input; the operator
does not edit JSON. An untouched blank nested control remains absent, so it
cannot create a false conflict during existing-file import. An invalid child
scalar invalidates the whole structured input rather than silently omitting it.

`MainWindow` declaratively hosts fields in an `ItemsControl`. Bindings update
visibility, errors and values without rebuilding the form on each keystroke.
Field synchronization distinguishes model refresh from operator edits so that
imported controls cannot create a second answer revision or invalidate a
successfully imported status afterward.
Essential mode is a desktop narrowing of Guided: it retains required, invalid,
protected, consequential, and essentially disclosed active fields. Advanced
shows all active fields. Shared Core validation always sees all answers,
independent of disclosure. Effective parent sensitivity masks all child controls;
protected scalar values use masked input rather than exposing choices through
an unmasked selection popup.

Native control names and help are set through Avalonia AutomationProperties.
Tab order follows the logical form, access keys use native buttons, and keyboard
shortcuts bind to commands through native key bindings. Validation focuses errors;
list mutation preserves usable focus through declarative focus requests and a
view-owned focus behavior. The RTL switch binds FlowDirection. Headless control
tests check actual focus and key routing, not just label/property presence.
Selection buttons return focus after cancellation/failure; successful intake
focuses the first visible field. Closing disposes the shell, fencing outstanding
selection as well as private saves. Code-behind contains initialization and
window lifecycle disposal only; it does not assemble fields or handle actions.

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

## Native qualification

`tests/Configurator.Desktop.NativeHost` initializes the actual App/classic
lifetime on the main thread, checks the X11 platform handle and Linux x64 RID,
and requires all thirteen distinct named scenarios. Each async action subscribes
before triggering and has a bounded timeout; startup/shutdown also has a
watchdog. Its synthetic generic definition and actual Event definition exercise
independent outputs, supported imports, invalid nested values, inactive bundle
selection, create-only permissions/conflicts/recovery, private sinks and close.
Event cases enter declared fields for one/two/three tenants and check required
manifest identity and nested objects. They do not imply arbitrary document
editing, a supported inverse for the manifest projection or target acceptance.

The MVVM refactor qualification passed thirteen checks on Linux x64/X11 and
173 Desktop MTP cases on 2026-10-11. Earlier direct Artifacts boundary
qualification passed 26 cases on 2026-10-09. These are
separate from published-payload and human/assistive-technology evidence. Missing
human/AT or full isolated-payload coverage keeps packaging readiness unqualified.
Exact commands, source/payload identities and omissions live in the task's
scoped evidence; [OPERATIONS](OPERATIONS.md#desktop-readiness-evidence) records
the verification boundary. No new release platform or installer is admitted.

Synthetic visual evidence can be captured with:

```bash
xvfb-run -a dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release -- --expected-rid linux-x64 --minimum-expected-checks 13 --capture-directory "$PWD/.tmp/desktop-mvvm-qa"
```

The harness captures loaded/edited workspaces at 960x800 and the 600x540 minimum,
in light/dark themes, at the top and footer. It subscribes to native resize and
animation-frame signals before rendering PNGs; captures contain synthetic
fixture values only. The workspace has one vertical scroll owner so a shorter
window cannot collapse the editable region behind fixed header/footer panes.
