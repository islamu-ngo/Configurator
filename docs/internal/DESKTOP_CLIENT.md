# Definition-driven Avalonia Desktop

> **Audience:** Contributors | Integrators | AI agents
> **Status:** Definition and deployment adapters; published-package and manual qualification remain separate
> **Owner:** Desktop
> **Last Verified:** 2026-11-04
> **Source Anchors:** `src/Configurator.Desktop/`, `src/Configurator.Presentation/`

Desktop remains on the centrally pinned Avalonia 12.1.3 packages. It references
Presentation and Artifacts, with Core types available through those references.
It has no target-specific DTO, catalogue, manifest, or identity workflow.

## Guided workspace composition

`DesktopNavigationViewModel` owns Open/Configure/Review selection and visual
section/page selection. `DesktopSectionViewModel` borrows references to existing
field projections; it stores no answers. `FormWorkspaceViewModel` and its
`FormFieldFactory` retain the form and all editor lifetimes when views hide or
change pages. `DefinitionFormProjection` supplies authored order and global
required/missing/invalid/deferred progress. Root maps and variants use a neutral
section rather than depending on ordinary field facades.

Root pages contain at most 50 editor references. Page clamping and section
selection never change input revision. Validation first reveals the affected
section/page, then requests native focus. The Desktop composition decision is
recorded in the [workspace ADR](adr/ADR-desktop-workspace-composition.md).

Section projections retain their identity across view and input refreshes;
localized titles, progress and borrowed editor references update in place.
This preserves native selector selection instead of replacing its item source
for every keystroke. Responsive container queries switch between the compact
section picker and an optional 224-unit rail, while the same editor surface
retains its controls. Its reading width is capped at 800 logical units.
The workspace has one primary scroll owner, including secondary chrome in
short windows; native inputs/actions have minimum rather than fixed heights.

`DesktopTheme` remains the native contrast subscription owner. Session-only
System/Light/Dark preferences select default, Latte and Macchiato resources;
native high contrast overrides them until disabled. Static startup Fluent
palettes and dynamic resource consumers avoid rebuilding the workspace when
appearance changes. These adapter contracts do not establish actual-host
accessibility or package qualification.

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
status. It exposes four focused child viewmodels rather than forwarding their
operations:

- `Loader.LoadDefinitionAsync(CancellationToken)`: read the explicit local path within
  the 4 MiB source ceiling, admit JSON or YAML, and clear obsolete state. The read
  accepts at most one extra byte to detect a file growing past the ceiling.
- `Loader.LoadDefinition(ReadOnlyMemory<byte>, bool yaml = false)`: admit a supplied
  local document through `DefinitionCodec`; useful for host integration and
  tests without a filesystem read.
- `Workspace.Form`, `Workspace.Fields`, `Workspace.SelectedExperience`,
  `Loader.DefinitionPin` and `Publisher` artifact properties: typed editing and
  byte-free metadata. `Form` is local
  editor state containing answers, not a public/remote projection.
- `Workspace.Validate()`, publisher preparation/save operations and root
  `Cancel()`: validation, snapshot preparation,
  consumptive protected publication, and invalidation.
- `ImportReview.ExistingFilePath`, `ImportReview.ImportFileAsync()`: acquire at most
  4 MiB for the selected declared artifact. The purpose defaults to Compare;
  explicit Fill fields remains a separate mutation. `CompareAsync` publishes
  shared assessment facts without validating/committing current editors or
  invalidating prepared output. `FillImportFieldsAsync(token)` awaits shared
  `AdoptImportComparisonAsync` and rechecks current revision, omissions and draft
  conflicts. Callers must await the asynchronous command.
- `Publisher.PrepareSelectionAsync(token)` and `SaveSelectionAsync(token)` are
  the user-facing operations over one eligible export selection. One file uses
  existing single-file preparation/publication and its filename override;
  multiple files use declared names and the existing bundle owner. Saving
  returns the authoritative per-file publication receipt and consumes current
  preparation. Zero selection cannot prepare. The import artifact picker
  remains a separate source-format choice.
- Loader definition path and publisher output directory/file name: operator-selected local
  inputs. `Status` contains value-free diagnostic codes and IDs; `StatusHelp`
  provides correction/retry guidance without values, paths or raw exceptions.
- `IStorageDialogService.ReadImportAsync(token)`: a mandatory provider-stream
  acquisition contract returning `StorageSelection<BoundedFileInput>`. Only a
  `Selected` outcome transfers a caller-owned input; dismissed and unavailable
  pickers return explicit statuses without exception-message interpretation.
  It reads no provider `Path`, uses the existing 4 MiB bounded reader and
  settles the stream and selected item before return. Failure, cancellation and
  selected-item disposal failure clear acquired storage. The import-review owner retains
  its physical busy lease until input cleanup finishes. There is no default
  interface implementation or local-path fallback.
  Custom implementations must add this method explicitly: migrate import
  selection from returning a URI to opening the selected provider's read
  stream under bounded acquisition. Return ownership of the bounded input only
  after the provider stream/item settle; on failure retain cleanup responsibility
  and return no input. `SelectAsync` continues to require local definition and
  output paths. This is an intentional breaking interface change, not an
  automatic adapter for old implementations.
- `IStorageDialogService.SelectAsync(kind, token)`: a Desktop-only local path selection boundary.
  Its `StorageSelection<Uri>` distinguishes selected, dismissed, nonlocal and
  unavailable outcomes. Cancellation still uses the operation token and rejects
  late provider results before transferring their value. Callers retain their
  captured generation checks before changing paths or starting a load.
  `AvaloniaStorageDialogService` uses the window's `StorageProvider` for one local
  file or existing folder, checks capability and disposes storage items. It accepts only host-free local file
  URIs while its captured generation remains current. Cancellation, unavailable
  providers and nonlocal selections preserve answers/preparation. The accepted
  definition path feeds the existing bounded reader; folder selection
  sets the destination. No picker stream writes output. Typed paths remain.

### Comparison ownership and explanations

`DesktopImportReviewViewModel` subscribes to `ImportComparisonChanged` rather than
`InputChanged` or `ViewChanged` for report refresh. Report publication and
retirement update only report bindings on the owner thread; they do not rebuild
field editors or retire preparation. Shared `DefinitionImportState` owns the
only private proposal. The Desktop view retains detached report facts, bounds
its rendered finding page, and presents file eligibility separately from
`ImportAutofillCode` for the current workspace.

`DesktopShellViewModel.ImportReview` owns acquisition, comparison/adoption
orchestration, paging, explanations and review subscriptions. It observes the
publisher's selected artifact rather than duplicating selection or answers.
`ArtifactPublicationView` hosts `ImportComparisonView` with that review owner as
its data context. Shell disposal retires review before publisher and workspace;
an in-flight read retains its physical operation lease until acquired bytes
are cleared. The publisher remains the preparation and physical publication
owner. Direct callers and the native import probe use the new owner without
forwarding compatibility members on the publisher.

Production comparison and adoption yield to `DispatcherPriority.Background`
through `Dispatcher.Yield`, using the shared Core traversal callback rather
than another evaluator or worker-owned form. The same shell operation retains
its busy lease through suspension and physical cleanup. Cancellation or an edit
invalidates publication; successful adoption permits exactly its own one input
revision increment before publishing status and requesting focus.

Purpose/source/artifact changes, cancellation, navigation and disposal call
`ClearImportComparison` so a pending read cannot restore a retired report.
`ExplainImportFinding` returns declared schema metadata/static path only;
neither imported source ordinals nor map keys select a current entity, request
field focus or issue a workspace correction token. After deliberate successful
adoption, normal fresh workspace validation owns corrections. No report value,
private unknown key, raw exception or native input path belongs in feedback.

## Editing and presentation flow

`FormWorkspaceViewModel.ConfigurationChoices` contains declared compatible
recipe/scope pairs, localized through the shared form metadata. The native
`ConfigurationContext` ComboBox appears before the field list and binds its
selection to `DefinitionFormModel.SelectConfigurationContext`; Presentation
owns the single accepted mutation and revision. The two selector fields are
represented by this picker rather than duplicate ordinary editors. Unsupported
pairs do not change accepted answers. Desktop has no Developer document editor
or separate pending document draft to resolve.

`ArtifactPublisherViewModel.Artifacts` uses the form's current eligible outputs.
Input changes reconcile single and bundle selections before their next use,
retaining only eligible IDs and choosing the admitted primary destination when
the previous selection is no longer available. Normal shell input invalidation
retires preparation and import work; existing operation generations fence late
completion and preserve truthful native publication receipts. No client-owned
scope mask or second validation path is introduced.

`DefinitionFormModel` owns a frozen admitted definition, typed field input text,
input revision, validation, and cloned snapshots. Desktop's `FormFieldFactory`
projects inputs into string, enum, numeric, boolean, object, list, typed-map and
discriminated-variant viewmodels.
Compiled `DataTemplate` bindings render native inputs and recursive collections
from those binding/focus projections; they are not another semantic answer tree.
Scalar drafts use `EditValue` with literal path segments; deliberate suggestion,
null, reset and branch actions use the shared form APIs described below. List
actions use `AddListItem` / `RemoveListItem`. The form retains
invalid nested drafts beside the last accepted values. Literal shared diagnostics
identify the failing leaf without assigning its error to healthy siblings.
Dots in field IDs are not path separators. Preparation still rejects active
invalid drafts; a retained accepted snapshot does not imply readiness.

`FieldViewModel` reads `DefinitionFormModel.GetFieldValidation` for current
masking, suggestion/null/reset eligibility and ordinary correction requirements.
Readonly or inactive locations do not offer null or suggestions. Correction
text uses shared English/Arabic resources with authored bounds and public
allowed values, without including the supplied answer. Protected descendants
withhold suggestion and allowed-value disclosure.

Object and list **Supply** actions call `SupplyObject` / `SupplyList`.
They deliberately represent `{}` / `[]`, including childless objects and
zero-capacity lists, and preserve existing contents when supplied again.
Browsing and refreshing leave absence unchanged. **Clear** removes the selected
path without reconstructing siblings; clearing an absent nested path does not
materialize its ancestors. String list-item Clear retains an empty string row;
Remove item deletes the row. Clear records omission, suppressing initialization;
it does not request default fallback.
List projections reconcile the form's value-free logical row tokens, retaining
surviving viewmodels, controls, drafts and per-path provenance across sibling
edits/removal and unrelated imports.

### Typed v2 identity and lifecycle

`EnumFieldViewModel.Choices` retains one item per declared enum ordinal, with a
leading **Not supplied** sentinel. `SelectedIndex` writes the exact declared
`JsonElement`, never a parsed label; refresh matches typed values with
`JsonElement.DeepEquals`. Shared `SelectChoice` offers the same ordinal-to-value
contract. Localized or duplicate labels cannot merge choices, and an admitted
empty string remains distinct from omission. Protected enums expose no open
choice collection and retain masked text editing; public booleans use native
Not supplied/Yes/No choice controls with stable ordinal semantics.

## Metadata discovery and deliberate feedback

`DesktopNavigationViewModel` delegates queries to
`DefinitionFormModel.SetDiscoverySelection` and pages its public
`Projection.Matches` at 25. It retains no answer index. Query-boundary failures
produce localized limit guidance; inactive selections explain applicability.
Current static locations use shared `FocusEditor` before revealing the
existing native editor. Repeating or absent locations that cannot resolve to
one current editor receive guidance rather than an invented value or entry.

`FieldViewModel.ShowError` is presentation state over existing typed validation,
not another validity/readiness flag. Field edits mark interaction locally;
`FieldFeedbackBehavior` observes native blur only while attached. Explicit
workspace validation requests guidance recursively, including unmounted rows.
Required status and global counts remain visible before this request.

Lists/maps retain all existing editor projections and expose 25-reference
visible slices. Closing a nested collection supplies an empty visual slice;
it does not dispose semantic owners. `RevealForFocus` selects only the
target's ancestor pages before the existing focus signal. The focus bridge
then rechecks input revision and logical identity after native layout.

`MapFieldViewModel` projects `AdditionalProperties` through `FormFieldFactory`.
Entries reconcile by exact ordinal key equality, retaining surviving editors
and disposing removed projections. `NewKey` is a literal segment validated by
`DefinitionPath.FromSegments`; no dot splitting, case-folding or numeric-key
reinterpretation occurs. Add is disabled for duplicate keys, invalid paths and
the `maxProperties` ceiling, falling back to
`CompositionLimits.DefaultMappingEntries` when no bound is declared.
The shared mutation boundary still enforces answer budgets; Core validation
owns `minProperties`, value constraints and readiness. Add supplies a typed
starter value and requests focus on its editor; remove omits that literal entry
and returns focus to the map key input. Dynamic keys stay local, not public
diagnostic metadata.

`VariantFieldViewModel` exposes declared ordinal choices with public titles from
`GetVariantTitle`. `DefinitionValidator.SelectVariant` selects the active schema;
the adapter does not interpret general combinators. `SelectVariant` reselects
the current branch by updating only its discriminator, preserving other data.
A different branch replaces the payload with its discriminator only; Core
resolves eligible initialization. When the selected schema changes, Desktop
disposes the previous child projections before creating the new typed children.
There is no inactive-branch answer cache, and disposed editors ignore late edits.

`FieldViewModel` exposes distinct `SetNullCommand`, `ClearCommand`,
`ResetCommand` and `ApplySuggestionCommand`. Nullable `SetNull` supplies JSON
null; `SetValue(path, null)` omits instead. `ResetValue` removes the override and
omission so `x-configurator.initialValue` becomes eligible again without copying
effective values into the supplied snapshot. `ApplySuggestion` explicitly adopts
`x-configurator.suggestedValue`; Desktop withholds its action for protected
nodes or protected descendants. Standard `default` is descriptive, not an
initialization or recommendation action. Supplied false, zero, empty strings and
containers remain values. Required or initialized children never materialize an
absent parent. These actions use the existing input revision and preparation
invalidation path, not a second answer tree.

`RootFieldViewModel` hosts the existing map or variant editor at the empty
literal `DefinitionPath`. It owns the projection, not another accepted value.
Ordinary fields retain their declared-property facade while recursive native
editors use literal configuration coordinates. The factory retires replaced
children and their callbacks, including an A-to-B-to-A branch switch.
Shared `CanOmit` disables root clearing; array rows use explicit collection
removal rather than field omission or reset.

### Refresh, localization and correction focus

`MainWindow` declaratively hosts fields in an `ItemsControl`. Bindings update
visibility, errors and values without rebuilding the form on each keystroke.
The workspace-owned `FormFieldFactory` coordinates the single field refresh
subscription for input and view changes. Each pass obtains one current root-state
lookup and groups shared diagnostics before refreshing existing projections.
Roots do not also subscribe to shared input/property events. Workspace message
notifications do not run another field pass. The factory detaches and disposes
projections before form replacement; imported controls cannot create another
answer revision or invalidate a successfully imported status afterward.
Visibility comes from shared `IsEditorVisible`: authored experience/disclosure
and optional expansion never change answers. Required/error paths remain
reachable when optional detail is collapsed. Shared Core validation always sees
all answers, independent of disclosure. Effective parent sensitivity masks child
value controls;
protected scalar values use masked input rather than exposing choices through
an unmasked selection popup.

Native control names and help are set through Avalonia AutomationProperties.
Tab order follows the logical form, access keys use native buttons, and keyboard
shortcuts run through a bubbling, unhandled-key window handler. Focused editors,
composition and popups receive input before shell commands. The application
uses the platform hotkey Command modifier for Open and Review/Prepare.
Escape dismisses the deepest latest popup before cancellation; one event
cannot also discard preparation. Validation requests focus
through `FirstError.RequestFocus`; ordinary containers prefer a visible failing
descendant. A variant with an invalid visible child reveals and focuses that
child; otherwise it signals its branch selector. Map projections can signal
their key input. `TryResolveCorrection` resolves revision-bound typed diagnostic
coordinates locally before marking nested errors; private map keys are not
serialized into status. List mutation preserves usable focus through declarative
focus requests and a view-owned focus behavior. Headless control tests check
actual focus and key routing, not just label/property presence.
Retained field adapters expose opaque automation IDs independent of paths and
answers. Operation status has one polite live region; property presence does
not establish screen-reader speech. Body, heading and title font resources
permit actual text enlargement independently of host render scaling.
Supply, Clear, suggestion, reset, null, map-add, field guidance and window chrome
resolve through `Resources/DesktopStrings` with the explicit workspace locale.
Before definition admission, provider text and default direction follow the
host UI culture. Once admitted, the explicit workspace locale owns them.
The resource accessor never changes process culture. Workspace and field refresh
notifications update visible labels and automation names; simultaneous workspaces
can retain different locales. Unsupported provider locales fall back to English.
Definition-authored translations remain separate from provider text.
Childless/zero-capacity containers have a reachable
Supply focus destination even when there is no editable descendant or enabled
Add item action.

`ThemePalettes.axaml` owns semantic surface, text, error and border brushes for
light/dark/high contrast; `DesignTokens.axaml` applies them to the window and
validation guidance. `DesktopTheme` follows the platform contrast preference,
uses a dark-inheriting high-contrast variant and releases its platform subscription
at application exit. System/Light/Dark choice is session-owned; native high
contrast takes precedence until disabled. Native Fluent focus and control styling
remain active. Actual-host rendering qualification is recorded separately from
the resource contrast tests.
Selection buttons return focus after cancellation/failure; successful intake
focuses the first visible field. Closing disposes the shell, fencing outstanding
selection as well as private saves. Code-behind owns window initialization,
lifecycle and routed keyboard integration; field behavior remains in view-models.

`FormWorkspaceViewModel.Locales` and `SelectedLocale` select admitted public
definition metadata through `SelectLocale`. `GetEditorPresentation` refreshes
titles/descriptions, enum labels, and accessible help assembled from instructions,
units, choice explanations and static guidance links, with base-text fallback.
`GetVariantTitle` localizes declared branch titles; map entry titles remain exact
local keys. `DisplayExperiences` supplies localized journey titles. This is
metadata-only `ViewChanged`: no answer revision, typed enum identity, schema pin
or prepared output changes, and no guidance retrieval is performed.

`FollowLocaleDirection` starts true, deriving `FlowDirection` from the selected
definition locale. Changing `IsRightToLeft` disables following and selects an
explicit layout override; re-enabling following resumes locale direction.
Definition locale does not set process `CurrentUICulture`, and layout changes
do not translate shipped shell/action text. Answers and drafts stay transient
inside the existing form; localized projections introduce no persistence or
publication authority.

## Preparation and protected publication

For preparation, the publisher captures `Form.Snapshot()`, the pinned definition
and selected output before dispatching work. `PrepareAsync` and
`PrepareBundleAsync` return typed `DesktopPreparationOutcome` values to the owner.
The result distinguishes `Invalid`, `Cancelled`, `Retired`, `Faulted` and `Busy`
from `Prepared`; callers must not turn a cancellation or implementation fault
into an invalid-answer diagnosis.
Cancellation, generation and input revision govern adoption; worker code does
not mutate bound UI state. The publisher retains the private
`PresentationOutcome`. Physical settlement releases worker resources even if
the owner dispatcher has stopped; owner notifications are a separate step.
`DefinitionPreparationMetadata` is the only preparation exposed publicly.
Artifact bytes stay in the private outcome.

Save takes a private byte copy and immediately consumes and clears the
workspace output. The shell validates the simple file name and passes the
explicit directory plus name to `ProtectedArtifactWriter.PrepareAsync` with
`ArtifactKind.Unknown`: this is deliberately the generic restricted kind,
not a target-specific ID-to-kind mapping. Every declared artifact format uses
the same boundary. `CommitAsync` enforces native create-only publication and
Linux owner-only permissions; no definition value grants filesystem authority.
Unsupported hosts fail closed.

`SaveAsync` and the internal save delegate return the immutable
`ProtectedArtifactCommitResult`, not a status alone. The public writer adapter
retains `EstablishedResult` after any commit exception, even without a cleanup
failure. It preserves installation, immediate verification and cleanup facts
instead of inventing an uninstalled I/O failure. A stale completion returns its
physical result without publishing stale status or restoring readiness.
`save-installed-unverified` identifies a real installation whose immediate check
failed; cleanup failures remain separate. Installed outcomes carry an explicit
current-integrity disclaimer and require manual review, not an automatic retry.

Bundle preparation verifies the selected IDs through `DefinitionBundleComposer`
against the current snapshot, rejecting empty, inactive or uncomposable output.
It immediately disposes the temporary bundle, retaining only an answer revision
and declared IDs, never composed bytes. Admission rejects case-insensitive
filename conflicts. Save rechecks host protection capability and composes
against the current snapshot immediately before
publication and disposes the Core bundle afterward to zero its private buffers.
Each answer, definition, destination, or selection change discards it.
Publication consumes the bundle and preflights every target before committing
any file. Its ordered results distinguish installation from verification and
cleanup, and a null result denotes the unattempted remainder in a settled bundle.
Failed verification stops subsequent files. A conflict leaves existing files
untouched; after resolving it, the operator prepares again. After a partial
commit, the operator leaves verified outputs in place, selects only remaining
outputs, and prepares again. Installed-but-unverified files require manual review before use or retry;
installation and successful verification never promise continued integrity.
There is no overwrite or rollback claim. Bundle success uses the full result's
`Succeeded`, including cleanup. The UI displays artifact IDs and safe result
facts, never protected bytes or local paths. The capability notice
reflects the native writer on the current host.

Input, destination, definition, or artifact changes increment the adapter
generation, cancel pending save work, dispose unused preparation, and revoke
current capabilities immediately. Save checks its captured generation before publishing status, so a
writer that completes after cancellation cannot overwrite new form state.
Save failure, cancellation, and success all consume preparation. The final
private save copy is zeroed in `finally`. Closing unloads the form and disposes
the session. .NET input strings cannot promise secure memory erasure; the form
clears references on disposal and no prepared bytes enter status or logs.

`DesktopOperation.Completion` settles only when the consumer releases its lease
after awaited work and cleanup. Shell disposal requests cancellation but never
releases that lease or its CTS. `DesktopPublicationOperation` owns the captured
single-save bytes or composed bundle until that point. Import and definition
streams clear borrowed buffers in their own await/finally scope before releasing
the lease. A boundary that ignores cancellation keeps the busy gate closed
without retaining current preparation or admitting late answers.

Definition and local import acquisition use the native `BoundedFileInput`
owner, with the existing 4 MiB per-file limit and separate admission rules.
Blocking open/stat/read runs off the dispatcher. Changed lengths, oversize and
empty inputs reject without adopting partial data. Cancellation retains the
physical task until stream disposal/clearing and the existing generation fence
rejects late results. The shared form clears consumed import bytes before the
busy-release notification; disposing the bounded owner clears its whole storage.
Controlled actual-dispatcher open/stat/read tests cover responsiveness separately
from helper-only bounds tests.

The publisher forwards the bundle writer's real outcome observer and retains
immutable `ProtectedArtifactPublicationReceipt` facts independently of generation:
at most 16 settled attempts plus one active attempt, each with at most 64 declared
outcomes. An installed prefix is visible while later work is pending. Unknown is
not unattempted. History contains only attempt IDs, declared artifact IDs and
result codes, never bytes or destination paths. Notifications marshal to the
SynchronizationContext captured at publication start and are fenced after
disposal. Physical settlement and receipt retention do not wait for visual
notification delivery. No process-global dispatcher lookup owns that operation.

`MainWindow` intercepts ordinary close before disposal. The shell shares one
close intent over the captured physical completion task; a two-second
`TimeProvider` deadline never blocks the dispatcher. Settlement closes normally
only while that same intent remains active. At the deadline the window remains
open, marks pending publication unknown and focuses the named Stay open action.
Stay open withdraws close intent but does not undo cancellation or release the
physical owner. Force close is available only after the deadline and authorizes
window closure without awaiting that physical owner.
Final closure detaches window handlers and DataContext before disposing the
workspace. Late task results retain safe facts and clear their own buffers but
cannot revive UI state. Process termination cannot guarantee managed erasure or
that no write occurred. Terminal's automatic-stop policy is not reused.
See the [ownership decision](adr/ADR-cross-platform-quality-ownership.md).

## Verification

Phase 10 typed v2 controls use the existing headless Desktop surface; commands
below are verification entrypoints, not an execution receipt for this document
update. No builds or tests were run for this documentation-only change.
Headless window/control evidence cannot qualify a physical native host,
installed payload, native assistive technology or translation quality.
Definition-standard-redesign Phase 12 retains actual-host/package qualification;
the earlier native evidence below is historical, not qualification of this
typed v2 increment.

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet build src/Configurator.Desktop/Configurator.Desktop.csproj --configuration Release --verbosity quiet
dotnet test --project tests/Configurator.Desktop.Tests/Configurator.Desktop.Tests.csproj --configuration Release -- --maximum-parallel-tests 1
```

The focused suite exercises real Avalonia headless controls, nested and list
editing, literal leaf errors, explicit empty-container presence, surviving row
control identity/focus, imported/defaulted sibling origins, exact Arabic action
resources and rendered shipped-copy equality, disclosure-independent readiness, generic
artifact formats, definition pinning/reload, unsafe names, protected metadata,
buffer clearing, consumptive saves, cancellation, stale completion, and the
actual Linux create-only writer through the shell. Parent-owned final visual QA
and screenshots remain separate from this unit's headless verification.
These checks do not establish normal/minimum-size native keyboard operation,
translation quality or native assistive-technology qualification.

See the [public workflow](../public/desktop.md) and
[Core definition contract](CORE_DEFINITION_CONTRACT.md).

### Schema-driven secret controls

PKCS#8 PEM fields enable native `TextBox.AcceptsReturn`; D21 checks a real
clipboard copy/paste round trip preserves the complete envelope and readiness.

`StringFieldViewModel` projects the shared secret operation beside its masked
editor. Generate and Regenerate are deliberate commands; the admitted Core
policy chooses the algorithm and bounds. The Presentation workspace owns the
operation revision and editor identity, so a late completion cannot write into
a replaced workspace or a shifted collection row. Inline rule feedback comes
from shared validation; the control retains invalid draft text for correction.

Show/Hide changes visibility only. Editing or disposing the editor hides the
value and clears transient derivation state. Entered derivation input and a
generated original password use separate masked controls; copying the final
hash and copying its original password are distinct actions. The thin
`ISecretClipboardService` adapter invokes Avalonia's native clipboard only
after an explicit Copy command. Clipboard failure leaves the answer intact
and reports a safe status. Clearing a workspace cannot retract clipboard
history, and immutable strings cannot promise complete memory erasure.

## Native qualification

The current executable requires twenty-four distinct names: the twenty-two
guards below plus `D23-root-map-literal-controls` and
`D24-root-variant-retirement`. The root map guard exercises literal keys, exact
typed integer/null output, create-only protected publication, reset, comparison
and deliberate import through the rendered controls. The branch guard checks
replacement, retired callbacks, masking and correction focus.

On 2026-11-05 the Release NativeHost build and all 24 guards passed separately
in English and Arabic on Linux x64/X11 after the import-review extraction.
Final local captures and receipts are under `.tmp/eaq-native-purpose-live/`.
Visual review found that replacing localized purpose strings lost selection;
stable option controls with live localized text and explicit automation names
now retain both purpose and caption. Two headless locale-switch cases passed,
and two independent reviewers confirmed all sixteen Arabic end captures.
Earlier `.tmp/eaq-native-import-final/` captures precede this caption repair.
The Desktop owner separately passed 341 tests with no failures or skips.
These are source-built native observations; installed-package, manual
assistive-technology and strict interaction-latency qualification remain separate.
The exact NativeHost command used isolated build outputs:

```bash
dotnet build tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj -c Release --verbosity quiet --tl:off --artifacts-path /tmp/configurator-eaq-native-import
CONFIGURATOR_NATIVE_UI_CULTURE=en xvfb-run -a dotnet /tmp/configurator-eaq-native-import/bin/Configurator.Desktop.NativeHost/release/Configurator.Desktop.NativeHost.dll --expected-rid linux-x64 --minimum-expected-checks 24 --capture-directory "$PWD/.tmp/eaq-native-purpose-live/en"
CONFIGURATOR_NATIVE_UI_CULTURE=ar xvfb-run -a dotnet /tmp/configurator-eaq-native-import/bin/Configurator.Desktop.NativeHost/release/Configurator.Desktop.NativeHost.dll --expected-rid linux-x64 --minimum-expected-checks 24 --capture-directory "$PWD/.tmp/eaq-native-purpose-live/ar"
```

The earlier twenty-two-name set comprised all twenty existing
guards, including `D19-typed-map-variant-null-reset-locale` and
`D20-read-only-import-comparison`, plus `D21-secret-generation-validation`
and `D22-deployment-context-destinations`. The deployment guard checks the
real native compatible-pair selector, its accessible name and focus, one
accepted revision, eligible destinations, stale preparation retirement and
switch-back, plus exact scoped output bytes and restricted file permissions.
It does not certify manual keyboard or assistive-technology use.
The secret guard exercises native masked input, invalid-value correction focus,
Generate, Show/Hide, deliberate clipboard copy, hiding after editing,
validation recovery, and separate digest/original-password copy and dismissal.
Its clipboard payload is synthetic and is cleared after
the check. This required set is not evidence of an executed current host run.
Parent-owned actual published/manual QA and comparison resource measurements
must be recorded separately from focused headless checks.

Deployment-context verification on 2026-11-04: the Desktop and NativeHost
Release builds completed with zero warnings/errors, and the complete Desktop
suite passed 304 tests without skips. The real Linux x64/X11 NativeHost passed
all 22 required checks separately under English and Arabic, using isolated
Xvfb displays. The deployment guard checked the rendered pair binding/focus,
one accepted revision, stale preparation retirement, current destinations,
exact scoped JSON output and mode `0600`, then switch-back. Local captures and
`qualification.json` records are under
`.tmp/deployment-aware-configuration/desktop-native-en/` and
`.tmp/deployment-aware-configuration/desktop-native-ar/`.
These are source-built native and headless observations, not installed-package,
manual keyboard/dialog, scaling or assistive-technology certification.

The following records earlier qualification scopes. It does not establish
Phase 10 typed-map, variant, nullable or localized-metadata behavior on a
physical native host or in an installed package.

`tests/Configurator.Desktop.NativeHost` initializes the actual App/classic
lifetime on the main thread, checks the X11 platform handle and Linux x64 RID,
and required all eighteen distinct named scenarios in that historical increment, including
`D14-physical-settlement-receipt`, `D15-close-drain-settles`,
`D16-close-deadline-unknown`, `D17-close-stay-force` and
`D18-shared-editing-presence-localized-actions`. Each async action subscribes
before triggering and has a bounded timeout; startup/shutdown also has a
watchdog. Its synthetic generic definition and actual Event definition exercise
independent outputs, supported imports, invalid nested values, inactive bundle
selection, create-only permissions/conflicts/recovery, private sinks and close.
Event cases enter declared fields for one/two/three tenants and check required
manifest identity and nested objects. They do not imply arbitrary document
editing, a supported inverse for the manifest projection or target acceptance.

The shared-editing increment passed 209 Desktop MTP cases and all eighteen
actual X11 guards in separate English and Arabic startup UI-culture cohorts.
Set the harness-only `CONFIGURATOR_NATIVE_UI_CULTURE` to `en` or `ar` before
launch; this selects touched Supply/Clear resources, not a product language
switch. A mid-run async culture change did not govern existing dispatcher
binding contexts, so the native qualification uses fresh process cultures.
D18 verifies omission versus deliberate empty containers, leaf-only errors,
surviving row/control focus and shipped action labels/names/help. Inspected
960x800 and 600x540 light/dark captures live under
`.tmp/cross-platform-quality-desktop-editing/{en,ar}`. This is real native
control evidence, not screen-reader speech, complete translation, or an
isolated published-package run.

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
xvfb-run -a dotnet run --project tests/Configurator.Desktop.NativeHost/Configurator.Desktop.NativeHost.csproj --configuration Release -- --expected-rid linux-x64 --minimum-expected-checks 21 --capture-directory "$PWD/.tmp/desktop-mvvm-qa"
```

The harness captures loaded/edited workspaces at 960x800 and the 600x540 minimum,
in light/dark themes, at the top and footer. It subscribes to native resize and
animation-frame signals before rendering PNGs; captures contain synthetic
fixture values only. The workspace has one vertical scroll owner so a shorter
window cannot collapse the editable region behind fixed header/footer panes.

### Architecture and conventions qualification (2026-11-02)

The pre-package-smoke Desktop candidate passed all 297 tests with no failures or skips after
the typed selection, explicit locale, theme and view decomposition changes.
Release builds of the test owner and NativeHost completed with zero warnings
and errors. The rebuilt NativeHost passed all 21 required scenarios separately
in English and Arabic on Linux x64/X11 using Xvfb and .NET 10.0.10 (34.24 s and
29.26 s). This includes late operation/close handling, factual publication
receipts, shared map editing and secret generation/validation.

The native captures at `.tmp/architecture-desktop-native-final/{en,ar}` bind to
NativeHost SHA-256 `673e13e7e44054693f69f91d5340541b7357aae1d8690b15c0d618e4335b5794`
and Desktop assembly SHA-256
`10b55d03ae10f9bb013ce487c2e1e291025363bd8e7394bc7faf6db048ca8aaa`.
Minimum-size Arabic capture inspection confirmed that provider-owned definition
pin guidance follows the workspace locale; definition-authored text remains
distinct. Automated tests cover light/dark/high-contrast resource contrast and
retired native preference callbacks. Manual picker operation, screen-reader
speech, other hosts/scales and isolated published-payload execution remain
separate qualification requirements; these source-harness results do not
establish them.

A subsequent actual-package smoke exposed cleared experience selection on a
locale switch and visually reordered native paths in RTL. Both received a
failing rendered regression before the fix: Desktop now retains experience
choice identities while refreshing their titles, and native path/filename
editors use LTR text direction within RTL layouts. The post-fix owner again
passed all 297 tests, and all 21 NativeHost scenarios passed in each locale.

The rebuilt Linux package, executable SHA-256
`0b0d3c52614bba05e7905a4ff009d5148c74f9a232870d6dd3eb729c10a37f9e`,
was then launched directly in an isolated Xvfb display with the maintained minimal
self-hosted definition. Actual screenshots confirmed that the nondefault Custom
experience remains selected after keyboard selection of Arabic and that the
definition path remains LTR. Ctrl+O focus and normal close (exit 0, empty process
log) also passed. Final captures are under
`.tmp/architecture-desktop-package-final/`. This establishes focused packaged
startup/locale/keyboard/close behavior; the attempted native file dialog could
not be qualified because the isolated GNOME portal lacked desktop services.
Screen-reader speech, other scales/platforms and a complete manual picker flow
remain separate evidence requirements.
