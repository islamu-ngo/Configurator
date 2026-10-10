# Maintainer sources compile into canonical distribution definitions

Status: accepted by the approved maintainer authoring implementation request.
Date: 2026-10-10. Owners: Core, maintainer tooling and Contributor Experience.
Implementation anchor: [maintainer authoring contract](../MAINTAINER_AUTHORING.md).

## Context

Expanded runtime definitions repeat identifiers, titles, projections and project facts.
Authoring directly in that envelope makes ordinary integrations unnecessarily verbose;
duplicating expansion in clients would split numeric, privacy and identity semantics.

## Decision

Keep one closed YAML/JSON source contract and one pure Core compiler. Normalize syntax
through the bounded composition parser, derive deterministic identities from semantic keys,
expand existing field/condition/projection vocabulary and reuse runtime admission. Compile
before answer collection. Retain canonical runtime JSON as the immutable public distribution
and revision-history boundary. Native adapters own ordinary source edits/public file writes;
answer-derived protected output retains its separate restricted writer.

## Consequences

Independent and optional store integrations share one handwritten source. All clients can
load local sources without services, plugins or new dependency layers. Remote loading stays
runtime-only. Schema describes structure rather than establishing privacy, publisher trust
or target authority. No universal schema engine, expression language, executable extension
or backward compatibility layer is introduced. Exact numeric policy and deterministic
ordering make syntax/comments irrelevant to canonical identity while meaningful source
changes require a new revision. Source locks and digest checks coordinate Configurator
edits, but cannot eliminate the last replacement race with external editors.
