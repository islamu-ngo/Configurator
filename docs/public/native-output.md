# Native artifact output

Configurator prepares files; it never applies configuration to an application.
Answer-derived content is restricted, regardless of the definition's field labels.
Native private bytes cannot be sent to stdout or to a public writer. The
[browser](browser.md) has a separate deliberate copy/download custody boundary;
it does not use this native writer or promise its filesystem protections.

On the supported Linux path, the writer prepares an anonymous file and publishes
it with create-only, no-replacement semantics. Existing destinations are never
overwritten. Competing writers have at most one winner. Unsafe directories,
changed targets, cancellation or missing filesystem guarantees reject publication.

Restricted saves on unsupported hosts fail closed. A desktop framework's ability
to start on Windows or macOS does not prove protected-save support on those hosts.
There is no unprotected fallback.

The shared bundle API renders selected files from one answer revision and checks
their combined size. It checks every destination before creating the first
file. A later destination that already exists or is unsafe prevents publication
from starting. A destination can still change after that check: files already
written stay written, and later files are left unwritten. Per-file results
identify that committed prefix without exposing values or private paths.
Cancellation after a file is written has the same partial-result behavior. A
bundle is not an atomic transaction and Configurator never deletes a written
file as a rollback.

The [terminal client](terminal.md) keeps a history of the newest 16 completed
save attempts, plus the attempt still running. Each attempt identifies declared
artifacts and their established outcomes without retaining file content or
private paths. Clearing answers, changing inputs or cancelling a save does not
erase this history. It also does not make an old save ready to run again. The
history is cleared when the session is disposed.

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
