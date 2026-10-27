# Native artifact output

Configurator prepares files; it never applies configuration to an application.
Answer-derived content is restricted, regardless of the definition's field labels.
Native private bytes cannot be sent to stdout or to a public writer. The
[browser](browser.md) has a separate deliberate copy/download custody boundary;
it does not use this native writer or promise its filesystem protections.

## Before saving

1. Run the client's validation or the CLI's read-only `artifact bundle-check`.
2. Choose a new destination you control. For the CLI bundle workflow, create an
   empty owner-only directory as shown in [getting started](getting-started.md#3-save-to-a-new-private-directory).
3. Check [platform and filesystem requirements](packaging.md#cli-operating-system-boundaries).
   `doctor` reports host capability, not proof that a particular destination is safe.

## What protected publication guarantees

On the supported Linux path, the writer prepares an anonymous file and publishes
it with create-only, no-replacement semantics. Existing destinations are never
overwritten. Competing writers have at most one winner. Unsafe directories,
changed targets or missing filesystem guarantees reject publication before
installation. Cancellation can stop work, but cannot undo an installation that
has already happened.

Use the Linux package for your CPU architecture. The writer selects that
architecture's native flags rather than assuming x64 values on Arm64.
Cross-building a package does not establish protected-save qualification on
another host; the platform and filesystem checks still apply.

Installation and verification are different facts. A successful create-only
namespace operation establishes that the prepared file was installed. A later
inspection can fail or find a different file at that name. The receipt retains
the installation fact, reports that verification did not succeed, and stops
before publishing the next artifact. It does not claim the destination still
contains the prepared bytes. Even successful immediate verification cannot
promise continued integrity after another process changes the destination.

Restricted saves on unsupported hosts fail closed. A desktop framework's ability
to start on Windows or macOS does not prove protected-save support on those hosts.
There is no unprotected fallback.

## Bundles and partial completion

The shared bundle API renders selected files from one answer revision and checks
their combined size. It checks every destination before creating the first
file. A later destination that already exists or is unsafe prevents publication
from starting. A destination can still change after that check: files already
written stay written, and later files are left unwritten. Per-file results
identify that committed prefix without exposing values or private paths.
Cancellation after a file is written has the same partial-result behavior. A
bundle is not an atomic transaction and Configurator never deletes a written
file as a rollback.

## Recover before retrying

Keep the original receipt or the client's save-attempt history. Inspect each
artifact outcome and the destination before choosing what to do next:

| Outcome | Meaning and next action |
| --- | --- |
| `written` | The file was published. Keep it; a later failure or cancellation does not undo it. Post-installation inspection failure remains `written`, with installation `established` and verification `failed`; inspect the destination before retrying. |
| `not-attempted` | Publication of this artifact was not attempted. Resolve the earlier failure before preparing another attempt. |
| `unknown` | Publication is not established either way. Inspect the destination; never interpret this as proof that no file exists. |
| `conflict` | Publication was refused before installation because the destination already existed or changed. Inspect it and choose a fresh destination rather than overwriting it. |
| `blocked` or `failed` | The boundary rejected the operation or an I/O operation failed. Review the diagnostic and any other artifacts' outcomes. |
| `none` | The operation did not request publication, such as a read-only check. |
| `planned` | A dry run planned the output but did not publish it. |

These are CLI `writeStatus` spellings, not a substitute for the independent
installation and verification facts. Interactive clients present the same
established facts in their own status views. An overall error is not proof that
every file failed, and an established write is not target acceptance. Cleanup
failure is independent of a written file. A broken receipt channel can leave
missing or partial JSON even after publication.

Do not blindly replay a write command, delete an uncertain file, weaken directory
permissions or switch to an ordinary writer to force success. Keep existing
files, resolve the cause, and select a fresh destination when appropriate. See
[CLI exit codes](cli.md#machine-contract-and-exit-codes).

## Interactive save history and readiness

The [terminal client](terminal.md) keeps a history of the newest 16 completed
save attempts, plus the attempt still running. Each attempt identifies declared
artifacts and their established outcomes without retaining file content or
private paths. Clearing answers, changing inputs or cancelling a save does not
erase this history. It also does not make an old save ready to run again. The
history is cleared when the session is disposed.

The [Desktop client](desktop.md#save-safely) also retains the newest 16 settled
attempts plus its running attempt independently of current preparation. Its
Publication facts view keeps known installation, verification and cleanup facts
after editing or cancellation. Native work retains its borrowed buffers and busy
gate until the consumer and cleanup physically settle. Logical cancellation
cannot promise that no file was written, and a stale receipt cannot enable Save.

An interrupted save can leave some files written and others unattempted. While
publication is still running, an outcome that has not arrived remains unknown;
it does not prove that no file was created. Closing the terminal allows up to
two seconds for the operation to settle and reports remaining uncertainty if
that budget expires. A second save stays disabled while the first is draining.
An observation or cleanup failure is reported separately from established file
outcomes: a file already marked `Written` stays written, but the overall save
does not claim success. Check the destination before retrying an uncertain
attempt; existing files will be refused, never overwritten automatically.

Inspecting another artifact's metadata in the terminal preserves readiness when
answers and the selected output set are unchanged. Changing the selected set or
answers requires fresh preparation and cancels stale publication. Destination
changes, where the client exposes them, likewise require fresh preparation.

Choose a new destination, review the definition's apply/restart consequences,
and use the target's own validated import or startup process. Local preparation
does not grant tenant, administrator or setup authority.
