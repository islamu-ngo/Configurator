# Backlog: Publisher Definition Distribution

- Status: Future hosted-source and catalog work explicitly excluded from
  Configurator extraction
- Owner: Configurator publisher/distribution maintainers
- Trigger: A publisher needs to distribute definitions or a catalog beyond
  versioned definition data packaged with a Configurator release or static site.

## Outcome sought

Provide a hosted distribution path for bounded Configurator definitions without
turning definitions into executable extensions, silently changing an active
workflow, or treating a publisher label as authority. The first hosted version
must cover publisher indexes, verified provenance, digest-pinned retrieval and
cache behavior, and conformance review as one coherent capability.

## Scope

The future capability must define and implement:

1. a publisher index that identifies each definition by family GUID, revision,
   and canonical-content digest rather than by a mutable display name;
2. verified provenance that binds index entries and delivered bytes to an
   explicitly configured publisher trust decision, not to a claimed publisher
   label or family GUID;
3. retrieval that requests the exact selected digest, verifies canonical bytes
   before definition admission, and rejects a mismatch, omission, ambiguous
   revision, or substituted response;
4. a cache keyed by immutable identity and digest, with integrity revalidation,
   explicit freshness behavior, and no silent upgrade of an active pinned
   selection; and
5. conformance review that confirms every distributed definition uses the
   bounded data-only vocabulary, preserves its sensitivity floor, and exposes
   only the public projection to the browser.

## Acceptance criteria

This backlog item is complete only when all of the following are true:

- A reviewed threat model names publisher trust roots, index compromise,
  equivocation, stale cache, unavailable network, and digest-substitution
  failures, with fail-closed behavior for every unresolved case.
- The publisher-index contract carries immutable family GUID, revision, and
  digest identity plus sufficient provenance metadata for an operator to make
  a configured trust decision.
- Fetch and cache tests prove the retrieved canonical bytes match the requested
  digest before Core admits them; a cache hit cannot replace a pinned active
  digest with a newer version.
- Conformance review rejects executable extension vocabulary, unbounded remote
  launch behavior, unknown validators or projections, and public relabelling
  of non-public data.
- Browser integration remains public enum/bool metadata only. It receives no
  secret entry, private definition projection, restricted artifact bytes, or
  target-host administration channel.
- Public and internal documentation explain the configured trust source,
  offline/cache behavior, exact-selection semantics, and the continuing
  prohibition on host apply authority.

## Dependencies

- [Definition runtime boundary ADR](../../docs/internal/adr/ADR-definition-runtime-boundary.md)
- Canonical definition composition, typed schema generation, and Core admission
  invariants
- A Configurator-owned publisher trust and publication policy; Event publication
  history, signers, and release authority are not inputs
- Security review of index provenance, retrieval, cache partitioning, and
  operator trust configuration
- Conformance-test fixtures representing both public and restricted definitions

## Exclusions

This record does not add networking, a hosted catalog, background definition
refresh, remote browser fetch, or a cache to the current extraction. It does
not defer any required standalone engine, client, Linux protected-output, or
Event handoff work. It also does not add executable plugins, target-host apply
authority, or an Event assembly dependency.
