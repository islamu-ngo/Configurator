# Definition-driven Terminal.Gui adapter

> **Audience:** Contributors | Integrators | AI agents
> **Kind:** Living contract
> **Status:** Typed offline adapter with shared secret operations; local Linux process qualification recorded below
> **Owner:** Terminal
> **Last Verified:** 2026-11-04
> **Source Anchors:** `src/Configurator.Terminal/`, `src/Configurator.Presentation/`
> **Review basis:** Source and direction review at `97c5a1789545b73a9536a4b9d4fa934976afb16c`; deployment-scope Release tests and Linux PTY qualification on 2026-11-04, recorded below.

The client uses official Terminal.Gui with an exact central package pin and
generated locks. Its published package retains reviewed embedded-asset notices;
see [the dependency decision and history](TERMINAL_GUI_DEPENDENCY.md).
It references Core, Artifacts and Presentation; no Event wire
DTOs, built-in catalogue, HTTP client or implicit application selection remain.

## Workbench direction and current limits

The [framework and workbench decision](adr/ADR-terminal-framework-and-workbench.md)
retains official Terminal.Gui. Browser-first development supports a README
Configure link into static WASM; the TUI direction remains a long-lived operator
and developer workbench, including large nested definitions. The framework
choice does not authorize an exact layout or imply delivered Browser parity.

| Area | Current adapter | Planned direction or limit |
|---|---|---|
| Form workspace | One selected field editor, typed nested containers/maps/variants, help and correction navigation. | Evaluate richer navigation, field-specific controls, density and contextual panes while preserving keyboard reachability. |
| Secret custody | Shared generation/derivation, masked buffers, guarded reveal/copy and explicit clearing. | Preserve these controls through every editor; immutable strings and clipboard history cannot be universally erased. |
| Local files | Bounded import, comparison, deliberate Fill fields, preparation and create-only protected publication. | File pickers only select paths; they do not provide protected save. |
| Developer editing | No full terminal document editor. | Reuse `DefinitionDocumentSession` for public-scope answer projections where applicable. This is not arbitrary definition/schema editing. |
| Comparison | Imported configuration/artifact against the selected admitted definition and current workspace. | Schema-to-schema structural comparison and migration inference need separate design. |
| Providers | No secret-store connector or network operation. | Eventual opt-in native Infisical/Azure Key Vault REST workflows require separate connector/privacy design and authority. |

Browser has richer renderer/density and developer-editor integration source;
that observation does not complete its delivery qualification. Its shared
document contract edits a canonical public-scope answer envelope or bounded
Environment projection, with detached parse candidates and one revision-fenced
Apply. A future terminal editor must preserve that contract; schema authoring
needs separate admission and must not change an active session's frozen identity.
See [document ownership](PRESENTATION_STATE.md#bounded-document-sessions-and-atomic-apply).

The ADR owns the balanced toolkit comparison, reconsideration triggers and next
vertical-slice qualification. Provider direction follows the existing
[secret-store backlog](../../dev/backlog/universal-configurator-followups.md#secret-store-inspection-and-completion),
starting with one concrete pilot. No HTTP/native authority enters Core or Browser;
the offline baseline and target acceptance/apply boundaries remain intact.

## Data and control flow

### Deployment context and eligible destinations

`TerminalWindow.Deployment` renders the admitted compatible recipe/scope pairs
before field navigation, with the current pair marked and translated metadata
resolved through the shared form. Native focusable buttons preserve Tab,
Shift+Tab and Enter interaction within the scrollable window. A choice first
commits the current typed field and calls
`DefinitionFormModel.SelectConfigurationContext` once. The terminal has no
document editor or independent recipe state.

`TerminalArtifactOperation` derives its available destinations from
`DefinitionFormModel.EligibleArtifacts`. On input change it removes ineligible
selections, moves an obsolete current destination to the declared primary or
first eligible destination, and invalidates existing preparation. Ordinary
edits preserve an intentional empty output selection. Explicit selection of an
ineligible artifact is refused. Core remains responsible for scope readiness
and recipient isolation; the adapter retains its captured revision/generation
checks and existing protected bundle publication and receipts.

### Workspace language and status

`TerminalStrings` resolves chrome and native publication guidance using the
form's explicit locale, with English fallback. A workspace language change
refreshes the window title, static controls, validation guidance and selected
receipt without changing process culture or another workspace. Shared import,
validation and secret guidance comes from `PresentationStrings`.
Publication status remains backed by typed installation, verification and
cleanup facts. User guidance translates those facts; adapter diagnostic codes
remain separate from rendered prose in `StatusCode`.

The window retains one state owner, with editing, import, publication, lifecycle
and secret-dialog behavior in cohesive partial files. Preparation captures an
immutable answer snapshot and selected outputs before composing on a worker.
Its generation/revision checks reject readiness after edits, clearing, selection
changes or disposal. The dispatcher observes completion, and close cancellation
includes preparation in the existing bounded drain. Composition bytes remain
scoped to the worker's disposable bundle.
`TerminalApplication` configures a maximum loop rate of 120 iterations per
second before initialization. Dispatch and drawing can occupy separate
iterations; the shorter cap interval leaves room for terminal output within
the feedback budget. This setting alone is not latency evidence.
`TerminalPreparationOutcome` separates `Ready`, `Invalid`, `Cancelled`, `Retired`
and `Failed`. The publication caller renders cancellation and retirement
guidance separately from invalid drafts and implementation failures.

The content scrolls within an 80×24 outer terminal. Relative button columns and
focus-driven scrolling keep controls reachable after resize. Masked controls
return Tab, Shift+Tab, Escape and Alt shortcuts to the owner; these keys cannot
be appended to the private buffer. Explicit copy/cut/paste/history commands keep
their existing blocked behavior.

### Schema-driven secret actions

`TerminalWindow.Secrets` delegates generation and derivation to
`DefinitionFormModel.GenerateSecretAsync`, capturing the selected leaf path.
Presentation owns eligibility, revision/editor identity fencing, cancellation
and application; the terminal never implements a cryptographic profile. Field
help uses `GetSecretValidation` for the same safe policy messages as other
clients. Ordinary editing remains possible when a value violates a policy.

Generate/Regenerate, Show/Hide and Copy secret are separate keyboard buttons.
Visibility changes suppress editor change notifications and do not change the
answer. `SecretTextField` continues blocking normal copy/cut/context/undo
commands even while revealed. Copy invokes the current application's native
`IClipboard` only after its capability check; failure preserves the value and
does not attempt an OSC52 escape-sequence fallback.
The current Linux clipboard integration also requires `DISPLAY`; installed
clipboard tooling alone did not establish a reachable desktop in PTY testing.
Headless sessions fail explicitly before invoking the native clipboard.
Reveal renders control characters, including PEM line breaks, as spaces to keep
terminal control sequences inert. The independent private buffer retains exact
characters; copying or committing a generated/imported PEM retains its newlines.

Entered derivation input uses a separate masked modal buffer. Generated original
passwords use a masked result modal with deliberate reveal/copy and dismissal.
Character/UTF-8 working buffers are bounded and cleared with
`CryptographicOperations.ZeroMemory`; managed strings and clipboard history
cannot receive an erasure guarantee. The terminal closes the transient result
through the shared form when its result modal is dismissed.

The existing Terminal.Gui package's `IApplication.Clipboard`/`IClipboard`
interfaces supply native capability and transfer status. Interface research:
[Terminal.Gui API](https://tui-cs.github.io/Terminal.Gui/api/Terminal.Gui.App.IClipboard.html)
(accessed 2026-11-02); no external implementation source was copied.

### Session and editing

`TerminalProgram` rejects redirected stdin, stdout or stderr before loading
a definition (`interactive-terminal-required`, exit 4). This is an interactive
surface; scripts and redirected streams use the CLI instead. `--help` is handled
before the interactive-stream gate.

`TerminalDefinitionSelection` reads bounded local files, admits JSON/YAML
through `DefinitionCodec`, or explicitly resolves a family and optional
application version through `DefinitionRegistry`. Definition bytes are
cleared after admission. Registry ambiguity is Core's decision, not a newest-
revision heuristic in the terminal.

`TerminalApplication` owns one `DefinitionFormModel` and one presentation
workspace. `TerminalFieldEditor` exposes object/list/map/variant slots,
expands selected object properties and typed map values into scalar leaves, and
edits scalar lists one item at a time. Each mutation uses a literal shared form
path through `EditValue`, `SelectChoice`, `SetValue` or the shared container
operations; there is no separately serialized adapter answer tree. It is not a raw
definition or whole-answer JSON editor. Visibility, effective sensitivity,
snapshots, input revisions, activation and validation remain parent-owned.
Nested leaves use the strongest parent sensitivity to prevent disclosure.

Typed enum choices use ordinal-to-`JsonElement` identity; duplicate or translated
labels remain distinct. Core selects discriminator branches, not a terminal
combinator evaluator. Branch changes replace only the selected provider payload
through `SelectVariant`; shared same-branch reselection preserves existing
children. Map entries use exact key segments with bounded `DefinitionPath`
validation and shared answer budgets. Keys are entered through the existing
masked buffer and rendered as entry ordinals in navigation. Public status never
serializes dynamic keys. Shared node states supply current conditional
requiredness and sensitivity.

Null, reset, omission and suggestion remain separate shared operations.
`initialValue` is effective initialization, not a supplied answer or descriptive
JSON Schema `default`. Required children never create absent ancestors. Existing
rows may be temporarily invalid while correcting them; readiness remains Core's
decision.

Container Supply/Clear actions call the shared form's `SupplyObject`,
`SupplyList` and `SetValue` gates. Browsing a blank child leaves an absent
object absent; deliberate empty containers retain `{}` or `[]` until cleared.
Requiredness, answer budgets and secret consent remain shared responsibilities.
`DefinitionScalarEditing` handles scalar conversion. An untouched empty append
draft is abandoned without adding a value, including boolean/numeric lists;
an existing empty string item remains a value. Item navigation cannot enter an
append position at the list limit, including a zero-capacity list. Bound feedback
does not prevent field navigation or editing existing items.

`TerminalWindow` uses native focusable controls with keyboard buttons,
bounded layout and English/Arabic resource parity. Small terminals hide editing
controls rather than overlapping them. The current editor is committed before
ordinary validation, preparation or editor navigation; type errors prevent that
transition. Comparison and finding/schema explanation explicitly bypass these
committing paths.
Choice cycling first commits the visible draft, so invalid input remains visible
and valid pending input becomes the starting choice. Removing an existing list
item requires pressing Remove item twice; navigation or editing cancels the
confirmation. After deletion, selection moves to the next surviving item, the
previous item at the end, or the empty container. Field selection is retained by
its root/path identity when the available slots change, with a bounded fallback
when that location disappears. Confirmation/status text contains no answer value.
Private editors hold bullets only in `TextField.Text`; clearable character
buffers hold the input. Clipboard/context/history commands are blocked.

Optional-editor and experience changes dispatch back to the window owner before
refreshing the selected editor. `TerminalFieldEditor.Read` first asks
`DefinitionFormModel.ReadDraftText` for the exact scalar or selected list-item
path, then falls back to committed content. The shared custody fence controls
draft access; the window restores protected text only into its existing masked
buffer and retains the path's invalid state until correction. View changes do
not edit accepted answers, origins or row identities. Denied drafts stay cleared,
and reading an untouched or nonexistent append position does not supply an item.

The locale action calls `SelectLocale` for admitted public metadata only.
`GetEditorPresentation` and `GetVariantTitle` supply translated titles and choice
labels with base fallback; shipped action resources remain culture-selected.
`TerminalStrings.FormatHelp` assembles descriptions, instructions, units,
choice explanations, credential acquisition guidance and static links.
`HelpPages` bounds the visible two-line page, preserves multiline navigation,
and replaces control characters without interpreting terminal escapes or markup.
No guidance fetch occurs. RTL alignment preserves logical glyph order rather
than reversing Latin fallback or technical text. Full RTL shaping and platform accessibility require
separate surface evidence.

Next correction resolves typed locations through `TryResolveCorrection` and
`FocusCorrection`, then selects the corresponding visible slot or scalar-list
item. Safe current coordinates are resolved locally; stale coordinates cannot
restore an earlier answer revision. The native editor receives focus without
putting private map keys or values into diagnostics.

Ordinary correction feedback and null/reset/suggestion affordances consume
shared `GetFieldValidation` facts. English/Arabic feedback includes applicable
authored bounds and public allowed values without echoing supplied answers;
protected descendants withhold suggestions and allowed-value disclosure.
Activation, readonly metadata and workspace consent determine the available
actions rather than a separate Terminal policy.

The artifact cursor cycles admitted outputs; a separate keyboard button toggles
each output in the selected set. At least one must be selected. `PrepareAsync`
checks `DefinitionBundleComposer` over one immutable answer snapshot, then keeps
only its input revision and selected count. Private composed bytes are not kept
between UI actions. The scoped Core bundle is disposed after preparation and
after publication, clearing its restricted byte storage. Input or selection
changes invalidate readiness and
deactivate/reactivate the workspace to cancel in-flight saves and discard old
results. Cursor-only artifact inspection preserves readiness; changing the import
target still cancels a pending import. On save, the operation recomposes the selected bundle from the current
revision and passes it to `ProtectedArtifactBundlePublisher` in the workspace
generation. Declared filenames stay inside the selected working directory; the
definition's filename is never treated as an arbitrary path authority.

Preparation captures the pinned definition, snapshot and selection before
dispatching work. The worker returns an immutable `TerminalPreparationOutcome`;
the window adopts it on its owner thread only while cancellation, generation
and revision still match. Adoption cannot be repeated. Save retains physical
ownership of its scoped bundle until the consumer settles, even if the window
has already retired the operation. These boundaries separate owner notification
from cleanup rather than relying on a late UI callback to release bytes.

`TerminalRootEditor` uses the empty `DefinitionPath` for admitted root maps and
variants. Its slot carries literal coordinates and logical identity rather than
a fabricated ordinary field input. Root omission and array-row field reset are
unavailable according to shared action facts; collection removal remains explicit.

The import field accepts a local file for the artifact at the cursor, whether or
not that artifact is selected for output. `TerminalImportOperation` starts
one observed read and returns from the action callback immediately. Compare is
the default action; Fill fields is a separate deliberate action. Synchronous
file open/stat runs at the owned background boundary; the bounded file body uses
asynchronous reads. Its captured definition digest, artifact ID, form revision
and editor generation fence admission. Uncommitted edits, import-path or target
changes, clear and disposal invalidate the read; a replacement import, preparation
or save remains disabled until that physical read and cancellation callbacks drain.
The path field clears when the attempt starts.

Completion dispatch uses pinned `IApplication.Invoke`, with a second lifetime
and generation check inside the callback. Only then does
the shared form receive already-read bytes on the UI owner. Both purposes use
cooperative `CompareAsync`; explicit Fill then awaits strict
`AdoptImportComparisonAsync`. Comparison never routes
through the applying handler, `CommitField`, `MoveField`, `MoveItem` or current
correction navigation. Form admission has no pending I/O at that point.
The read owner zeroes buffers on every exit, including canceled and rejected late
reads, and retires cancellation sources after their callbacks complete. Successful
autofill refreshes and focuses the editor. Comparison instead updates only report
controls and subscribes to `ImportComparisonChanged`, not ordinary `ViewChanged`.
Its summary separates compatibility, coverage, inventory and eligibility;
Next finding walks the retained immutable inventory without another answer tree.
Severity words accompany findings in monochrome. `ExplainImportFinding` provides
declared schema metadata without resolving imported ordinals into current rows.
No report action writes the active editor or manufactures workspace entities.
Failures expose stable codes, never file
contents, exception messages or paths. The adapter does not rely on a terminal
synchronization context being installed for physical read dispatch.

Fill fields after comparison calls tracked `StartAdoption` on the same import
owner, skips physical acquisition and awaits `AdoptImportComparisonAsync`, which rechecks the
current shared revision, consent, conflicts and drafts. Refusal retains existing
work and explains separate-copy repair/reimport, not partial recovery.
The production cooperative callback suspends the current slice with
`Task.Yield` and checks cancellation on both sides of that boundary. It does not
wait for the terminal's timer cadence or rely on `Invoke`, which executes inline
on the owner thread. The operation retains its applying
lease and completion until the suspended work and input cleanup settle. Closing
the window drains this same completion for acquisition, comparison and adoption.
Cancellation and purpose/artifact changes retire shared comparison ownership
as well as the physical attempt generation. The source file is never overwritten.
Consumed import paths clear their TextField history immediately. Explicit
`ClearPrivateState` also clears ordinary-editor history after resetting the form;
Compare does not clear that current draft history. Read-only comparison status
and lifecycle feedback discard their history after replacement. This prevents
discarded text from reappearing through Undo and prevents independent copied
control cells from growing after the original input/proposal owners have retired.
See [assessment authority](IMPORT_COMPARISON.md) for observation and location
domains. [Operations](OPERATIONS.md) routes the actual published-process
observations and separately scoped native Core resource measurements. Neither
headless assertions nor those measurements qualify every host or assistive tool.

Saving executes through `PresentationWorkspace.ExecuteAsync`, which
owns operation generations and cancellation. The bundle publisher prepares all
destinations through `IProtectedArtifactWriter` before committing any file; each
commit is create-only and protected. Existing targets and unsupported platforms
fail closed. A later commit failure can leave a committed prefix, so the window
shows a partial-save notice, counts and each artifact ID/status through a
keyboard-operated Next output result button. The operator reviews created files
before retrying; no automatic deletion or overwrite occurs. No private output
is placed in the workspace's public output.

Publication has a physical lifetime independent of workspace readiness.
`TerminalArtifactOperation` rejects overlapping publication and retains one
current receipt plus the newest 16 settled attempts, each bounded to 64 declared
outcomes. `TerminalPublicationReceipt` copies its collection and contains
only attempt/artifact identifiers, immutable `ProtectedArtifactCommitResult`
facts and safe completion flags. Installation, immediate verification and cleanup
remain separate; `TargetChanged` with `InstallationEstablished` is not a failed
attempt to create a file. Established results are recorded before workspace
delivery. Cancellation, edits and private
clear retain history without restoring answers, bytes or preparation; final
operation disposal clears history. Unknown pending outcomes differ from settled
unattempted ones. Observer and cleanup failures remain visible alongside known
installation facts, including installed-but-unverified outcomes. Per-file cleanup
failures survive cancellation even before the final bundle result is delivered.
Success requires verified installations and no cleanup failure. Rendering counts
installations, marks verification failure and explicitly disclaims continued
integrity; it never claims the destination still contains the expected file.
The read-only, focusable status field supports horizontal keyboard navigation so
long receipts remain inspectable without expanding the minimum terminal size.
Receipt navigation occupies a stable action row. Actions are disabled until
their receipt facts exist, rather than revealing controls during publication;
this avoids invalidating the unchanged public editor's layout on the first save.
Field refresh assigns public text only when it changes, preserving the current
caret and avoiding an empty-and-reload cycle for unchanged values. Protected
editors still clear their sensitive buffers before replacing their contents.
The window coalesces receipt refresh through its weak owner
dispatcher and checks lifetime again inside the callback. Save history cycles
attempts; outcome navigation only inspects that receipt and cannot change exit
status or re-enable Save. Imports, preparation and another save remain disabled
while physical publication drains, even after the workspace is no longer busy.

The UI retains public fields but clears protected model inputs after settlement,
explicit clearing, close, disposal and handled termination signals. Navigating
between fields clears the transient editor, not the masked multi-field answer
set. Immutable Core snapshots/.NET strings are not claimed to be zeroizable.

Native definition and import reads share `BoundedFileInput` in the existing
Artifacts assembly. Each file retains its 4 MiB limit; the registry still admits
at most 4,096 definition files, with no new aggregate registry budget. Definition
selection completes synchronously before UI creation while its native acquisition
runs on the worker. Interactive imports keep the event owner available during
open/stat/read, retain the physical task through cancellation/disposal and clear
owned storage only after consumption. Length changes, empty/oversized input and
late cancelled results cannot replace answers. The removed local `ReadFileAsync`
is not retained as a compatibility alias.

Save tasks have an explicit completion observer; propagated workspace faults
produce a value-free failure status. Close, framework quit and signal requests immediately cancel
through the thread-safe request-only workspace/import seams and clear the locked
secret buffer. Form/control cleanup runs on the UI owner. Closing stops accepting
commands while the event loop remains available for at most two seconds of
physical settlement. At the deadline, unresolved publication is marked unknown,
UI subscriptions detach and normal application teardown resumes. Static completion
observers and queued callbacks use guarded weak window references; late reads
retain their own cleanup responsibility without retaining Views. Managed cleanup
after process termination is not guaranteed. These deterministic lifetime checks
do not certify actual OS-signal delivery or terminal restoration.

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

### Deployment scope qualification (2026-11-04)

The untouched Terminal suite passed 150 tests. Before adapter implementation,
the focused native adapter run passed 47 tests and failed both new scope
invariants on the missing early configuration controls. After implementation,
the Release project build completed with zero warnings/errors, and the complete
built MTP executable passed 153 tests with zero failures/skips:

```bash
dotnet build src/Configurator.Terminal/Configurator.Terminal.csproj --configuration Release --verbosity quiet --no-restore -m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false
dotnet run --project tests/Configurator.Terminal.Tests/Configurator.Terminal.Tests.csproj --configuration Release --no-build -- --minimum-expected-tests 153
```

The new tests exercise compatible pair selection as one revision, narrowed
readiness and destination refusal, a scope switch during held preparation with
an inactive synthetic secret retained, and an actual Linux protected save with
independent expected bytes, no unrelated recipient file, and mode `0600`.

The actual Release DLL was also launched through an owned Linux PTY using a
synthetic worker/dashboard definition. At 110×36, Tab/Enter selected
dashboard-only: the visible fields and destinations changed from two to one.
Alt+E prepared without a worker credential; Alt+S produced the declared
dashboard JSON with independently checked content and mode `0600`, while no
worker file existed. Keyboard navigation reached Close and exited successfully.
At 90×30, a second session switched locale to Arabic, rendered the translated
recipe/scope titles and RTL metadata, and selected the same narrowed scope via
Tab/Enter; closing without saving returned the documented non-success outcome.

The PTY wrapper used `script --quiet --return --command` around `stty` and the
Release DLL with `TERM=xterm-256color`; direct output-capturing invocation was
correctly refused as `interactive-terminal-required`. No Event process,
answer-bearing network request or ordinary-file save fallback was involved.
These checks do not qualify screen readers, braille, Arabic shaping, other
operating systems, or the later published distribution package.

### Architecture and convention qualification (2026-11-02)

The Release Terminal test project built with zero warnings/errors; its complete
MTP suite passed 150 tests with zero failures or skips. The suite includes
controlled suspended-preparation transitions, workspace locale independence,
80×24 English/Arabic focus and nonoverlap, map bounds, and masked-key propagation.

The actual Linux x64 Release DLL was exercised in an owned PTY at 80×24 in
English and Arabic, resized to 110×36 and back, and separately reduced to 40×12
before recovery. Locale switching refreshed chrome and artifact selection text.
Nine synthetic masked characters remained nine when Tab moved focus. Keyboard
Clear removed them; keyboard Close restored the terminal and returned exit 4
(no successful save). The undersized view showed its resize notice and hid
editing. No output was saved; captured terminal output contained no synthetic
input plaintext. This qualifies that local built DLL, not a published package,
other hosts, screen readers, Braille or complete Arabic glyph shaping.

The candidate DLL SHA-256 was
`21dd362308a54dc1024296fa4d18c0225a770c7241de405494e3cfcd20e4cd7b`.
The build used `-m:1 /p:UseSharedCompilation=false /nodeReuse:false`; the suite
used `--no-build -- --minimum-expected-tests 1`. Earlier failed candidates exposed
a resource-manifest mismatch, lost literal diagnostic segments and swallowed
masked-field navigation keys; the final suite and PTY run above include their
corrections. Historical qualification paragraphs below retain their original
candidate limits.

The subsequently published Linux x64 executable
`artifacts/configurator/terminal/linux-x64/islamu-configurator-terminal`, SHA-256
`c8faf924f6f7e1cd363a4bda35f7ea122aac7a6fef528975a9cf8ceb36e9228c`,
also passed separate English/Arabic 80×24 → 110×36 → 80×24 PTY runs. Tab and
Shift+Tab preserved nine masked characters; keyboard Clear removed them and
Close restored the terminal with exit 4. No output was saved and no synthetic
input plaintext appeared in captured output. This qualifies that packaged
Linux process for these interactions, beyond the earlier built-DLL checks.

Run from Configurator:

```bash
TMPDIR="$HOME/.cache/agent-tmp" dotnet build src/Configurator.Terminal/Configurator.Terminal.csproj --configuration Release --verbosity quiet
TMPDIR="$HOME/.cache/agent-tmp" dotnet test --project tests/Configurator.Terminal.Tests/Configurator.Terminal.Tests.csproj --configuration Release -- --minimum-expected-tests 1
```

The correction slice was additionally exercised through the actual published
Linux x64 executable in an owned PTY. At 110x36 outer cells, a rejected public
integer draft survived optional-editor and experience changes. A rejected
protected integer retained two masked cells through those changes and resizing;
Clear private state removed them. No output was saved; normal close returned the
window's no-successful-save disposition, exit 4.

The viewport guard measures the content area: an 80x26 outer PTY hid editing,
while 82x28 exposed the controls. The near-minimum footer still clipped part of
the limitation text, so this is not full minimum-layout, locale or AT
qualification. Those obligations remain in the Terminal hardening follow-up.
Safe synthetic evidence is retained at
`.tmp/cross-platform-quality-terminal-drafts/qualification.json`; the parent
workstream still owns solution-wide and additional-host assurance.
