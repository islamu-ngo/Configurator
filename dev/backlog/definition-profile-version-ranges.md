# Explicitly declared application-version range schemes

Last Updated: 2026-10-28 Europe/Brussels
Status: Conditional extension; exact application-version matching is delivered.
Owners: Core identity/selection and maintainer tooling maintainers.
Trigger: A named adopter needs a declared compatibility range that exact
application-version strings cannot reasonably express, with target-owned
compatibility evidence and a specified version scheme.

## Outcome and current boundary

Select a bounded explicit scheme only after its ordering, grammar and compatibility
meaning are agreed. Today's application versions are exact declared strings,
not inferred SemVer, arbitrary constraints or a promise of target compatibility.
Definition authoring/runtime v2, machine v3, private answers v1, locale, profile,
family/revision and target application versions remain independent axes.
The [v2 ADR](../../docs/internal/adr/ADR-definition-standard-v2.md) records
this decision; exact matching is not an incomplete v2 feature.

## Boundaries

- Do not guess a version scheme from punctuation, coerce unknown versions or add
  v1 aliases/fallback. Undeclared/unsupported range semantics must reject safely.
- Retain canonical content identity and conflict memory. A compatibility
  declaration changes admitted content and needs an appropriate new revision;
  it cannot rewrite retained releases or upgrade active workspaces silently.
- A matching range selects declared metadata, not proof of target acceptance,
  migration safety, apply permission, signing or publication authenticity.
- No target API lookup, telemetry, answer-bearing request, new dependency,
  executable predicate or generic constraint-language framework is selected.
- Native custody, other-host qualification, new formats, Event delivery and
  publication/signers retain their independent owners.

## Acceptance criteria

1. A decision specifies one declared scheme, bounded grammar and evaluation
   rules, including prerelease/build identifiers if applicable, inclusive and
   exclusive endpoints, unsupported input and overlap/selection behavior.
   Ambiguity must have a declared deterministic resolution or reject.
2. Independent vectors cover exact boundary inclusion, invalid/unknown schemes,
   overlapping declarations, nonmatching versions and resource limits. No
   lexical or numeric approximation substitutes for the selected scheme.
3. Source compiler, runtime admission, discovery and launch selection use the
   same Core authority. Existing exact-string behavior has explicit unchanged
   cases; unsupported consumers refuse required semantics before answers.
4. Canonical identity includes range declarations. Tests prove pinned active
   sessions and previously admitted conflicts cannot be bypassed by changed
   ranges, discovery metadata or removal from a registry.
5. Generated schemas/capability metadata, machine handshake where affected and
   public/internal selection guidance change coherently with exact executed
   owner checks. Target-owned compatibility evidence is reported separately.

## Related ownership

[Target/adopter trials](definition-profile-target-adoption-trials.md) supplies
compatibility evidence, not inferred range support.
[Event delivery](guided-configuration-delivery-event.md) must revalidate its
actual declared selectors; this backlog does not migrate them.
