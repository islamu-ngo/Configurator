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

Choose a new destination, review the definition's apply/restart consequences,
and use the target's own validated import or startup process. Local preparation
does not grant tenant, administrator or setup authority.
