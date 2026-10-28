# Guided configuration delivery and Event adoption

Last Updated: 2026-10-28 Europe/Brussels
Status: required graduated scope; not implemented, approved or authorized.
Owners: Configurator definition/delivery maintainers; Event handwritten-definition
and wire-contract maintainers for their repository.
Origin: guided-configuration-experiences revision 1 Phases 5/6, split by CTO
revision 2. Original S21-S24 and IVSD-F006/M006 remain required here.

## Dependency and scope

Finish and verify the Configurator definition/consumer workspace first, then its
required bounded secret-generation capability, then generic delivery tooling.
**Only afterward edit Event's `configurator.yaml`.** Generation is required before
Event advertises Generate actions; issuer fields must instead explain acquisition.
Nothing here authorizes execution, another checkout, a branch, commit or remote
publication. Rebaseline into its own current I-VSD-aligned triad before work.

This split preserves the earlier Event-last user instruction and the independent
host bridge. It removes delivery from the primary UX PR, not from the roadmap.

## Definition v2 graduation boundary

The [definition v2 decision record](../../docs/internal/adr/ADR-definition-standard-v2.md)
records delivered Core admission/evaluation and the qualified bounded profile;
it does not implement this delivery program or authorize Event edits. Phase 12
qualification at `55c951d`, including Desktop 229 passed and all 19 X11 guards
in English and Arabic, is local Configurator evidence, not Event-owned acceptance.

The S21-S24 contract, D1-D3 slices, target expected-byte comparisons, required
secret-generation dependency and Event-last ordering below remain open and
independently owned. Historical commands/identity candidates must be rebaselined
against current source and selectors before execution. Current axes are
authoring/runtime v2, machine v3 and private answers v1; application versions
match exact declared strings. Fresh v2 example families do not prove migration
of Event's own identity or acceptance by its wire contracts. Preserve immutable
history and resolve the actual target-owned identity transition explicitly;
do not introduce a v1 compatibility interpreter.

Trigger: after Configurator prerequisites and required bounded secret generation
are verified, delivery and Event maintainers separately approve/rebaseline their
own slices. Acceptance still requires source/runtime/unsigned-receipt agreement,
all five target artifact families and tenant variants, independent Event-owned
expected bytes, matching separate copies/store delivery, and builds without an
Event sibling dependency. A local package or successful scaffold is insufficient.

[External target/stakeholder trials](definition-profile-target-adoption-trials.md)
track broader adopter, translation and responsibility evidence; no stakeholder,
scholarly or target-validation proof is supplied by this graduation.
[Advanced assertions](definition-profile-advanced-assertions.md) and
[version ranges](definition-profile-version-ranges.md) remain separately triggered,
not hidden prerequisites for already selected v2 behavior.

Native [public authoring custody](cli-public-authoring-custody.md) and
[directory authority](core-directory-source-boundary.md),
[other-host protected save](configurator-native-host-evidence.md),
[new formats](configuration-format-expansion.md) and
[publication/signers](publisher-definition-distribution.md) remain independent.
Unsigned receipts, source digests and hosted-schema identifiers confer no
publisher authenticity, target apply permission or release authority.

## Retained behavioral contract

| ID | Normative outcome |
| --- | --- |
| S21 | Source compilation, selected runtime bytes and unsigned receipt identity SHALL agree; mismatch rejects without replacing the previous receipt. Receipt SHALL NOT imply signatures, publisher authenticity, host validation or publication. |
| S22 | Event SHALL migrate only after the required Configurator capabilities pass; preserve family, field/output identities, target requiredness/activation/privacy and tenant bounds. |
| S23 | Newly emitted Event artifacts SHALL match independent Event-owned expected bytes for all five artifact families and tenant-count variants; target rejection boundaries remain. |
| S24 | Separate authoring/example/store copies SHALL compile to Event's selected delivery; retained revisions remain immutable and Configurator SHALL build without a sibling Event checkout. |

## Slice D1 - Generic unsigned delivery verification

NEW planned owner: `eng/configurator/DefinitionDelivery.cs`, following existing
file-based C# tooling such as BuildDefinitionStore. No target-specific exporter.
Closed NEW proposed interface, not an existing runnable command:

```text
dotnet run eng/configurator/DefinitionDelivery.cs -- --source SOURCE --definition JSON --receipt RECEIPT --source-url HTTPS --app-version VERSION --entry ENTRY --locale LOCALE --write
dotnet run eng/configurator/DefinitionDelivery.cs -- --source SOURCE --definition JSON --receipt RECEIPT --source-url HTTPS --app-version VERSION --entry ENTRY --locale LOCALE --check
dotnet run eng/configurator/DefinitionDelivery.cs -- --self-test
```

The capitalized operands describe the proposed CLI grammar, not executable
placeholder commands for the current review. Execution triad supplies real
selected paths/values after current source inspection.

Admit bounded public source and canonical JSON, compare compiled identity/bytes,
and write/check unsigned `release.json`. Explicit application-version/entry/locale
selectors must be declared by the definition; several declared versions do not
become an arbitrary refusal. Source URL is bounded credential-free public HTTPS.

Receipt contains source digest, canonical digest, family, revision, application
version, artifact count, public definition source and deterministic definition-only
launch fragment. No old wire-validation assertion, private path, signer,
publication status or Event trust authority. Validate before replacement; `--check`
is read-only. Existing Event digest/source/selector assertions retain meaning.

**Red first:** source/runtime mismatch, stale receipt, wrong selectors, invalid
URL, oversized metadata, read-only check, deterministic independent unsigned
receipt vectors and preservation of prior receipt on rejection. Then implement,
refactor and run one targeted Release build/architecture owner plus native
self-test/schema checks, satisfying actual package preconditions from PROJECTS.
No source-text/prose tests or speculative new test project.

Public/internal documentation: `docs/public/{integrate-your-tool,packaging}.md`,
`docs/internal/{MAINTAINER_AUTHORING,PACKAGING}.md`.

## Slice D2 - Event handwritten source and selected delivery

Read the current Event contract first. Historical evidence inspected revision 6
at Event `ac6e6a123c1ab9b32c16ab00e50e813bea29272f`; next revision 7 is only the
old candidate. If identity advanced, use the actual current anchor and a new
unconflicted revision. Never change a same-family/revision digest in place.

Existing selected three-file delivery:
`configurator/definitions/islamu-event/{configurator.yaml,definition.json,release.json}`.
Keep this layout; stage generated intermediates outside it.

Curate Recommended/Custom sections and essential task/feature selectors for all
five existing tasks. Keep inactive/advanced credentials collapsed and supplied
values reviewable. Add useful short help and structured acquisition instructions
for every first-party secret: purpose, when needed, where obtained, accepted
format/constraints and operational consequence. Do not add real examples/defaults.
Issuer tokens have issuer guidance; locally generatable fields declare only
implemented admitted profiles. Humanize labels without changing machine IDs.

**Red first:** source/runtime/receipt mismatch and all-operation projection/
required-secret/selector cases, using independent expected semantics. Preserve
existing target requiredness, activation, projections, tenant bounds and private
default safeguards. Green then compiles through the completed standalone CLI.
Existing native sequence (run later from Event root, after rebaseline):

```bash
islamu-configurator definition check \
  --source configurator/definitions/islamu-event/configurator.yaml --machine
islamu-configurator definition revise \
  --source configurator/definitions/islamu-event/configurator.yaml \
  --against configurator/definitions/islamu-event/definition.json --machine
islamu-configurator definition build \
  --source configurator/definitions/islamu-event/configurator.yaml \
  --against configurator/definitions/islamu-event/definition.json \
  --output artifacts/configurator-migration/definition.json --machine
```

The old selected JSON remains the anchor until build succeeds. Select the newly
generated bytes natively, not by hand-editing canonical JSON/hashes. Use the new
D1 owner to write/check the receipt. Historical selectors were application version
`0.0.0-development`, entry `configuration-manifest`, locale `en` and Event's public
`definitionSource`; revalidate them rather than inventing source publication.

Existing Event tests:
`tests/Event.Wire.Contracts.UnitTests/ConfigurationPortability/ConfiguratorDefinitionArtifactTests.cs`
and `tests/Event.Architecture.Tests/ConfiguratorDefinitionArchitectureTests.cs`.
Update receipt contract assertions for the replacement metadata, never weaken
digest/source/selector/private-default/target rejection assertions to pass.
Update `docs/public/self-hosting/setup-assistant.md`.

## Slice D3 - Configurator copies, store and independent host bridge

Existing separate inputs:
`examples/islamu-event/{configurator.json,definition.json,README.md}`,
`store/islamu-event/configurator.json`,
`tests/Configurator.Core.Tests/Definitions/ExternalDefinitionConformanceTests.cs`.
Compile/revise each source against its actual prior selected revision, then
require canonical equality with Event's new delivery. Add the new retained store
release (historical candidate `store/islamu-event/releases/7.json`); never modify
retained `5.json`/`6.json` or another published revision.

Existing native store owner:

```bash
dotnet run eng/configurator/BuildDefinitionStore.cs -- --self-test
dotnet run eng/configurator/BuildDefinitionStore.cs -- \
  --source store --output .tmp/definition-store --write
dotnet run eng/configurator/BuildDefinitionStore.cs -- \
  --source store --output .tmp/definition-store --check
```

Core `ExternalDefinitionConformanceTests` uses
`CONFIGURATOR_EVENT_ARTIFACT_DIRECTORY` as an **output-only** hook. Set it to a
task-owned temporary directory, then compare actual newly emitted bytes with
`../Event/tests/Event.Wire.Contracts.UnitTests/Fixtures/Configurator/islamu-event/expected/`
using `cmp`. Required names:
`environment.env`, `configuration-manifest.json`,
`configuration-manifest-1-tenants.json`, `configuration-manifest-2-tenants.json`,
`configuration-manifest-3-tenants.json`, `tenant-package.json`,
`operator-identity.json`, `legal-drafts.json`.

Both independent owner suites passing is insufficient without these comparisons.
Never change independent expected bytes to accept a mismatch without a separately
authorized target-semantic change. Exercise shared experience/help/progress and
discovery projection for all operations, not only composition.

At this workstream exit, execute changed Configurator owner/native adapter
classes and actual architecture/package closure once. Execute Event wire
`ConfiguratorDefinitionArtifactTests` and architecture
`ConfiguratorDefinitionArchitectureTests` through their real MTP projects with
`--minimum-expected-tests 1`. No database/backend startup or provider matrix.
Require D1/store/schema freshness, source/copy equality and owned docs/whitespace.

## Preserved phase-commit intent and path ownership

These are revision-1 packet records, not current executable contracts. The
follow-up triad must provide fresh full inspection/staging/path-limited commit
commands after current revision/policy/path verification. Candidate Change-Ids
were not native reservations; confirm allocation/uniqueness. Do not combine the
following independent outcomes into an umbrella commit:

| Original packet | Exact default subject | Metadata / literal owned paths |
| --- | --- | --- |
| P5 | `feat(definitions): verify public source and runtime deliveries` | Public governed feature, candidate `CHG-01M5AFRB000000000000000005`; D1 tool, its four public/technical docs above and matching fragment. Body: verify current source compilation/canonical bytes and bounded unsigned metadata without target/publication authority. |
| P6-E (Event) | `feat(self-hosting)!: author guided Event configuration journeys` | Public breaking feature, candidate `CHG-01M5AFRB000000000000000007`; selected YAML/JSON/receipt, Event setup guide, two Event test files above and Event fragment. Body: essential questions and deliberate optional detail through curated source and regenerated delivery. BREAKING CHANGE names actual new revision/contract, not a stale hard-coded 7. |
| P6-C (Configurator) | `feat(definitions): bundle Event guided configuration` | Public feature, candidate `CHG-01M5AFRB000000000000000006`; three example files, store source/new release, Core conformance class, `docs/public/definition-store.md`, `docs/internal/PACKAGING.md` and fragment. Body: align separate inputs/store selection and independent host vectors while retaining history. |
| P6-K | `docs(architecture): record configuration experience boundaries` | Internal docs with `Changelog: skip` and `Changelog-Reason: Internal architecture and workstream knowledge graduation.`; authority ADR, NEW `dev/_journal/domains/configuration-experiences.md`, NEW `dev/backlog/configuration-experience-followups.md`, existing Event-label backlog and task I-VSD report after its owned refresh. Body: preserve authority/delivery decisions and actual evidence limits. |

Exact matching fragment directory is `docs/internal/releases/changes/` within
each repository. Required terminal `Change-Id` matches its fragment; breaking
packets need bang and a dedicated BREAKING CHANGE footer. Native hooks remain
enabled; stage only fully owned literal paths and keep generated outputs with
their owning source. This record neither stages nor commits anything.

## Knowledge graduation and evidence limits

The [proposed authority ADR](../../docs/internal/adr/ADR-configuration-experience-and-answer-authority.md)
now exists; promote it only with implementation evidence. Add actual reusable
lessons to the journal, actionable new discoveries to owned backlog and correct
`dev/backlog/event-definition-human-labels.md` from removed exporter to handwritten
YAML ownership. Do not restore that exporter or assume an empty follow-up file
is required if no new discovery exists.

Before local plan removal, replace local-only I-VSD links with durable source/
ADR/backlog locators. Preserve all IVSD-F006/M006 and S21-S24 mappings in the
revalidated record. Actual UX/browser/extension qualification remains with the
existing Browser V7 owner; source/receipt/host-vector parity is not its substitute.
