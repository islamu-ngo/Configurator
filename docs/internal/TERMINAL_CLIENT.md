# Definition-driven Terminal.Gui adapter

The client uses official Terminal.Gui with an exact central package pin and
generated locks. Its published package retains reviewed embedded-asset notices;
see [the dependency decision and history](TERMINAL_GUI_DEPENDENCY.md).
It references Core, Artifacts and Presentation; no Event wire
DTOs, built-in catalogue, HTTP client or implicit application selection remain.

## Data and control flow

`SetupTerminalProgram` rejects redirected stdin, stdout or stderr before loading
a definition (`interactive-terminal-required`, exit 4). This is an interactive
surface; scripts and redirected streams use the CLI instead. `--help` is handled
before the interactive-stream gate.

`SetupTerminalDefinitionSelection` reads bounded local files, admits JSON/YAML
through `SetupDefinitionCodec`, or explicitly resolves a family and optional
application version through `SetupDefinitionRegistry`. Definition bytes are
cleared after admission. Registry ambiguity is Core's decision, not a newest-
revision heuristic in the terminal.

`SetupTerminalApplication` owns one `DefinitionFormModel` and one presentation
workspace. `SetupTerminalFieldEditor` exposes object/list container slots,
expands object properties into scalar leaves and edits lists one scalar item
at a time. It serializes only the edited
object/list answer back to the parent-owned typed field input. It is not a raw
definition or whole-answer JSON editor. Visibility, effective sensitivity,
snapshots, input revisions, activation and validation remain parent-owned.
Nested leaves use the strongest parent sensitivity to prevent disclosure.

Container Supply/Clear actions call the shared form's `SupplyObject`,
`SupplyList` and `SetValue` gates. Browsing a blank child leaves an absent
object absent; deliberate empty containers retain `{}` or `[]` until cleared.
Requiredness, answer budgets and secret consent remain shared responsibilities.
`DefinitionScalarEditing` handles scalar conversion. An untouched empty append
draft is abandoned without adding a value, including boolean/numeric lists;
an existing empty string item remains a value. Item navigation cannot enter an
append position at the list limit, including a zero-capacity list. Bound feedback
does not prevent field navigation or editing existing items.

`SetupTerminalWindow` uses native focusable controls with keyboard buttons,
bounded layout and English/Arabic resource parity. Small terminals hide editing
controls rather than overlapping them. The current editor is committed before
validation, import, preparation or navigation; type errors prevent that transition.
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
refreshing the selected editor. `SetupTerminalFieldEditor.Read` first asks
`DefinitionFormModel.ReadDraftText` for the exact scalar or selected list-item
path, then falls back to committed content. The shared custody fence controls
draft access; the window restores protected text only into its existing masked
buffer and retains the path's invalid state until correction. View changes do
not edit accepted answers, origins or row identities. Denied drafts stay cleared,
and reading an untouched or nonexistent append position does not supply an item.

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

The import field accepts a local file for the artifact at the cursor, whether or
not that artifact is selected for output. `SetupTerminalImportOperation` starts
one observed read and returns from the Import callback immediately. Synchronous
file open/stat runs at the owned background boundary; the bounded file body uses
asynchronous reads. Its captured definition digest, artifact ID, form revision
and editor generation fence admission. Uncommitted edits, import-path or target
changes, clear and disposal invalidate the read; a replacement import, preparation
or save remains disabled until that physical read and cancellation callbacks drain.
The path field clears when the attempt starts.

Completion dispatch uses pinned `IApplication.Invoke`, with a second lifetime
and generation check inside the callback. Only then does
`DefinitionFormModel.ImportAsync` receive the already-read bytes on the UI owner;
its Core mapping admits supported direct copies and rejects conflicting,
transformed or inactive inputs. Form admission has no pending I/O at that point.
The read owner zeroes buffers on every exit, including canceled and rejected late
reads, and retires cancellation sources after their callbacks complete. Successful
import refreshes and focuses the editor. Failures expose stable codes, never file
contents, exception messages or paths. The adapter does not rely on a terminal
synchronization context being installed.

Saving executes through `SetupPresentationWorkspace.ExecuteAsync`, which
owns operation generations and cancellation. The bundle publisher prepares all
destinations through `IProtectedArtifactWriter` before committing any file; each
commit is create-only and protected. Existing targets and unsupported platforms
fail closed. A later commit failure can leave a committed prefix, so the window
shows a partial-save notice, counts and each artifact ID/status through a
keyboard-operated Next output result button. The operator reviews created files
before retrying; no automatic deletion or overwrite occurs. No private output
is placed in the workspace's public output.

Publication has a physical lifetime independent of workspace readiness.
`SetupTerminalArtifactOperation` rejects overlapping publication and retains one
current receipt plus the newest 16 settled attempts, each bounded to 64 declared
outcomes. `SetupTerminalPublicationReceipt` copies its collection and contains
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
