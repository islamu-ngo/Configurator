# ADR: Definition Runtime Boundary

- Status: Accepted
- Date: 2026-10-08
- Decision owners: Configurator Core, Artifacts, Presentation, and adapter maintainers

## Context

Configurator is an offline configuration-preparation utility. It must let
application authors describe configuration workflows without importing an
application's assemblies or acquiring that application's administrative
authority. Its clients also have materially different privacy and host
capabilities: a native Linux client can protect restricted artifact output,
while the static browser can display public information only.

Without a fixed boundary, a target definition could become an executable
extension, a host could accidentally be treated as an apply authority, or a
public renderer could be given private metadata merely because it presents the
same workflow. These outcomes would make the standalone utility depend on
target runtime behavior and would undermine its confidentiality guarantees.

## Decision

### Definitions are bounded data, not runtime extensions

Configurator Core admits only bounded, typed, declarative definitions. A
definition describes fields, constraints, activation, projections, artifact
data, and display metadata from the vocabulary that Core validates. It cannot
introduce executable code, plugins, arbitrary validators, remote launch URLs,
or a new interpretation vocabulary at runtime.

The canonical identity of an admitted definition is:

1. a stable family GUID identifying the definition family;
2. a revision identifying a particular compatible definition revision; and
3. a digest over the canonical definition bytes identifying the exact selected
   content.

Human-facing names, publisher labels, and a family GUID are selection metadata,
not proof of trust, curation, or administrative authority. A client pins the
selected revision and digest for its active operation; it does not silently
substitute a different definition.

### Sensitivity cannot be demoted by a renderer

Definition admission preserves a sensitivity floor. A projection or renderer
may not relabel private or restricted input as public to make it convenient to
display. A client therefore receives only the subset of data appropriate to its
surface, while active required fields and their constraints retain their
semantics across essential, guided, and advanced views.

The browser is a static public-metadata adapter. Its definition projection is
limited to public enum and boolean choices and other admitted public metadata.
It receives no secret entry, private free-text values, restricted artifact
bytes, native-output capability, token, storage, telemetry, or host
administration channel. A browser launch resolves an exact compatible
family/revision/digest selection locally; it does not fetch a remote definition
or confer trust on the family GUID or publisher label.

### Artifact protection is a native capability, not host authority

Configurator Artifacts is the only boundary that writes protected output. On
the verified Linux path it uses create-only, no-replace protected output so a
restricted artifact cannot overwrite an existing path or fall back to an
unprotected save. Windows and macOS restricted-save support remains disabled
until each host has its own evidence and acceptance decision; unsupported hosts
fail closed.

Preparing an artifact never applies it. The target host remains responsible for
semantic acceptance, enrollment, authorization, live administration, restart,
and any apply operation. Definitions may communicate consequences, but neither
the definition nor a Configurator client gains host apply authority.

### Configurator remains independent of Event

All runtime projects use `Configurator.*` ownership and have no Event assembly
dependency. Event, like any target, can supply generated definition data and
retain its own wire parsing and host validation. Target-specific transport,
backend validation, tenancy, enrollment, and live adapters stay in the target
repository rather than entering Configurator.

## Consequences

- A definition can be shared across CLI, terminal, desktop, and browser
  adapters without granting a client more data or authority than its boundary
  permits.
- Target integration is explicit data exchange and host-side validation, not a
  transitive code dependency or automatic apply workflow.
- The supported protected-output claim is intentionally Linux-specific.
  Expanding it requires host-specific evidence rather than a permissive
  fallback.
- A future hosted publisher/catalog capability must establish its own
  provenance, digest, cache, and conformance controls before it can distribute
  definitions; it is not part of this offline runtime.

## Related Records

- [Foundation authority map](../extraction/FOUNDATION.md)
- [Native protected-save host evidence backlog](../../../dev/backlog/configurator-native-host-evidence.md)
- [Publisher definition distribution backlog](../../../dev/backlog/publisher-definition-distribution.md)
