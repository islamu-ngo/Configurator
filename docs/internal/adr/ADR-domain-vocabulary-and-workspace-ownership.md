# Explicit domain vocabulary and workspace mutation ownership

> **Status:** Accepted design; implementation qualification in progress
> **Date:** 2026-11-02
> **Owners:** Core and Presentation
> **Source Anchors:** [Core contract](../CORE_DEFINITION_CONTRACT.md), [Presentation state](../PRESENTATION_STATE.md)

## Context

Closed definition values were represented as strings across admission,
cryptography and client rendering. Interactive clients also repeated map-entry
decisions. Reorganizing these responsibilities must preserve canonical identity,
secret consent and stale-result rejection.

## Decision

Core owns closed domain vocabularies and explicit maps to their public tokens.
CLR names do not define wire spelling: values such as `RS256`, `P-256` and
`pkcs8-pem` retain their declared spelling. Unknown tokens and undefined enum
values reject at the owning boundary. Open identifiers, user keys and schema
keywords remain strings. A naming refactor does not itself justify changing a
public token or canonical digest.

Core also owns pure schema eligibility and immutable answer replacement.
CLI consumes those operations through Core; it does not depend on interactive
Presentation state. Presentation retains consent, accepted answers, logical
editor identity and operation generations.

Presentation has one accepted-state mutation authority. Its existing editing
state and metadata index remain collaborators, not independently synchronized
copies of the workspace. Shared mutation checks map limits, duplicate keys and
key validity before accepting changes. Clients display the outcome and own
their native clipboard, dialogs, dispatcher and shutdown behavior.

Secret, import and workspace generations remain distinct. Adoption revalidates
the relevant generation and logical target under the same gate as mutation;
callbacks and asynchronous work remain outside that critical section.
Read-only comparison does not invalidate unrelated prepared output.

The form's object/Monitor synchronization is retained because field inputs and
notification paths share that gate. Replacing only one declaration with
`System.Threading.Lock` would create different locking behavior through its
object aliases. A future migration requires the entire synchronization domain
and behavioral concurrency evidence.

## Consequences and verification

Explicit mappings require deliberate additions when a vocabulary expands, but
keep wire meaning independent from implementation names. Shared pure operations
reduce duplicated policy without introducing a new application assembly or
interactive dependency into CLI. Shared mutation prevents clients from granting
different editing authority.

Required evidence includes independent token literals, numeric/default rejection,
canonical JSON/YAML equivalence, structural path equality, stale completion and
map-limit regressions. This accepted decision is not a test or package receipt;
[Operations](../OPERATIONS.md) records executed qualification separately.
