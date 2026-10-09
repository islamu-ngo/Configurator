# Native artifact output

Configurator prepares files; it never applies configuration to an application.
Answer-derived content is restricted, regardless of the definition's field labels.
Private bytes cannot be sent to stdout or exported through the browser.

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
file as a rollback. Native client controls for selecting and retrying bundles
are still in development; current save commands handle one artifact at a time.

Choose a new destination, review the definition's apply/restart consequences,
and use the target's own validated import or startup process. Local preparation
does not grant tenant, administrator or setup authority.
