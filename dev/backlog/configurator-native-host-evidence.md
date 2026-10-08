# Backlog: Native Protected-Save Host Evidence

- Status: Future work explicitly excluded from Configurator extraction
- Owner: Configurator Artifacts maintainers
- Trigger: A supported release needs restricted artifact output on a native host
  other than the verified Linux path.

## Outcome sought

Establish evidence for protected, restricted artifact saving on one specifically
named native host before enabling that host in Configurator. The current
supported behavior is create-only, no-replace protected output on Linux.
Restricted saving on Windows and macOS remains disabled and fails closed; this
record does not authorize a generic save dialog, overwrite behavior, or an
unprotected fallback.

## Scope

For each proposed host, the owning maintainer must separately:

1. document the host's file-creation, access-control, race, cancellation, and
   path-resolution guarantees;
2. identify a native primitive that preserves the restricted artifact
   confidentiality and create-only/no-replace invariant;
3. implement that host-specific adapter in `Configurator.Artifacts`, without
   changing the definition runtime boundary or granting a client host apply
   authority;
4. add deterministic adversarial tests for competing creators, existing
   destinations, directory and symlink substitution, cancellation, and cleanup;
   and
5. update the supported-host and operator guidance only after the implementation
   and evidence pass.

## Acceptance criteria

This backlog item is complete for a host only when all of the following are
true:

- A reviewed host-evidence record names the operating-system version family and
  the exact primitive relied upon, including its failure behavior.
- The native writer proves that an existing destination is never replaced and
  that a race cannot redirect restricted bytes to an attacker-controlled path.
- Unsupported variants, unavailable primitives, and ambiguous path states fail
  closed without writing an unprotected copy.
- Focused `Configurator.Artifacts` tests exercise the host-specific behavior
  with deterministic synchronization rather than timing delays.
- Packaging and public/internal documentation state the exact supported host
  boundary; no broader operating-system support is inferred from one adapter.

## Dependencies

- [Definition runtime boundary ADR](../../docs/internal/adr/ADR-definition-runtime-boundary.md)
- Existing Linux protected-output implementation and its invariant tests
- A host-specific native API review and test environment capable of exercising
  the required filesystem behaviors
- Release/package validation for the proposed runtime identifier

## Exclusions

This is not a deferral of required extraction work. It does not reopen the
verified Linux implementation, add browser file output, introduce a host apply
operation, or import Event code. It is limited to new evidence and a
host-specific protected-save capability after an explicit trigger.
