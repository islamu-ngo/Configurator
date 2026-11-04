# Deployment context and recipient destinations

Status: Accepted architecture for deployment-aware configuration, 2026-11-04.

## Context

Preparing one component previously required every active setting in the
workspace. Selecting an export file could neither reduce those obligations nor
describe which recipient should receive independent credentials. A presentation
filter alone would leave the Core validation and machine clients inconsistent.

## Decision

Keep one accepted answer tree and the existing Core evaluator. Optional closed
deployment metadata declares recipes, compatible component scopes, eligible
destinations and explicit prerequisites. Recipe and scope are ordinary public
finite answers initialized through the existing `initialValue` semantics.
Core resolves scope before requiredness; dependency closure includes relevant
controls and presence relationships while structural ancestors do not activate
unrelated siblings. Global structural admission and operation budgets remain
mandatory. Evaluation state belongs to the operation, never the shared definition.

Destinations retain the existing projection vocabulary and add public usage
metadata: recipients, surface and explicit environment dialect. Consumer subsets
cannot extend selected scope. Docker run environment files and Compose inputs
have different literal contracts; Core uses a closed dialect choice rather than
a serializer plugin interface. Preparation remains separate from target
semantic validation, authorization and apply.

Presentation owns atomic answer mutations, input revisions and operation
generations. Clients render the shared context and use deliberate output actions.
Complete and draft preparation derive eligibility from the captured admitted
definition and answer snapshot. An artifact outside that context rejects even
if requested directly. Retained inactive secrets remain transient and cannot
become effective solely because an old export or callback requested them.

## Consequences and qualification

Definitions can omit deployment metadata. Authors of scoped definitions must
declare every non-emitted prerequisite and keep independent role identities
separate. Shared mappings reuse one answer; a component that calls an external
service does not thereby require that service's administrative credentials.
Changing scope is a real answer edit; detail, locale and export selection remain
view state. No compatibility reader, alternate evaluator or new dependency is
introduced.

Core regression tests qualify local admission, recipient exclusion, readiness
and shared-definition isolation. Actual Docker/Compose literal consumption,
published Browser interactions, memory limits and native host behavior require
their own recorded checks; this decision does not certify those surfaces.
Already released clipboard or download bytes cannot be recalled.

Source anchors:

- [Deployment model](../../../src/Configurator.Core/Definitions/DefinitionDeployment.cs)
- [Scope resolution](../../../src/Configurator.Core/Definitions/DefinitionScopeResolver.cs)
- [Validation session](../../../src/Configurator.Core/Definitions/DefinitionValidationSession.cs)
- [Artifact composition](../../../src/Configurator.Core/Artifacts/DefinitionArtifactComposer.cs)
- [Core contract](../CORE_DEFINITION_CONTRACT.md)
- [Public reference](../../public/definition-reference.md#deployment-context-and-destinations)
