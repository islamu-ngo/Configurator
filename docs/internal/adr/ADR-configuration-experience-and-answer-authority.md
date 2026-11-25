# Configuration experience and answer authority

Date: 2026-10-20 (Accepted: 2026-11-24)
Status: Accepted; implemented across Core, Presentation, CLI, Browser, Desktop, and Terminal with executed empirical qualification boundaries below.
Owners: Core, Presentation and client maintainers.

## Context

The current Guided visibility policy retains protected/consequence-bearing
editors, while operators need a small route, discoverable advanced settings,
useful secret explanations and a review of actual configuration effects.
Experience selection, filtering and help must not become answer mutations,
secret consent or target administration authority.

The current source seams are `FieldDefinition`, `DefinitionValidator`,
`DefinitionFormModel` and Browser `ConfigurationEditor`/`ConfigurationField`;
Desktop/Terminal directly consume the display contract. These are the offline
Configurator owners, not Event backend layers.

## Decision

1. Core admits bounded data-only experiences, existing help and structured public
   guidance/acquisition metadata into canonical definition identity. It continues
   to own sensitivity, activation, validation and composition.
2. Presentation owns the one projection of sections, literal correction paths,
   stable row/draft state, provenance, progress and metadata discovery. View
   changes preserve answer revision and valid prepared bytes.
3. Adapters render that projection. Required/error reachability and masked review
   cannot be defeated by local disclosure or filters. Progress counts actual
   active obligations, not declared optional fields.
4. Secret entry remains per-workspace consented. Password-manager compatibility
   uses genuine password-purpose hints and stable public identities, not hidden
   credential copies, fake target logins, submitting forms or vendor vault APIs.
   Extension-held copies are outside application-local custody guarantees.
5. Generation is a separate capability with admitted profiles and cryptographic
   entropy. Issuer credentials and cryptographic trust are not random-string
   formats. Additional file formats need target-specific contracts.
6. Replace obsolete visibility/mode contracts directly. Do not preserve aliases,
   upcasters or duplicate client policy for a pre-release interface.

## Consequences and alternatives

Canonical help edits require a new definition revision even if emitted bytes are
unchanged. Direct callers must migrate together. Central draft/row ownership is
required before rendering fewer subtrees safely.

A Browser-only predicate is rejected because clients would disagree on hidden
requirements and effects. YAML syntax comments are rejected as runtime-help
authority because they are not serialized application data. Executable templates,
remote answer-processing and generic "generate any credential" are rejected
because they grant authority absent from this offline utility.

The proposal adds admission/projection work but keeps adapters thin and avoids
new services, packages and answer storage. Actual UX/accessibility/manager
quality still requires candidate-specific evidence; design review is not proof.

## Delivery and verification

The configuration-experience and answer-authority boundaries above are delivered and verified across the offline runtime, clients, and dogfooded target definitions (local working-tree closure `2026-11-24`; remaining external human/hardware and signing gates stay explicitly bounded below):

1. **Core admission, cooperative single-pass preparation, and authoring engines:**
   - Single-pass cooperative `DefinitionPreparationSession` with read-only `JsonElement` validation, streaming canonical `Utf8JsonWriter` + `ZeroingByteBufferWriter` (zeroing staged bytes on cancellation/revocation), and admission-time precomputed `DefinitionNode.{RequiredSet,HasOptionalDescendant}` (`Configurator.Core.Tests` `994/994`).
   - Value-safe numeric parser coordinates (`CompositionSourceLocation` / `DefinitionAuthoringDiagnostic`: 1-based line, 1-based Unicode-scalar column, 0-based UTF-8 byte offset including BOM) stripping raw parser messages, unknown keys, snippets, and private paths (`DefinitionAuthoringDiagnosticTests` `6/6`).
   - Expanded `G12` conditional-requiredness and unannotated-enum quality linting (`DefinitionQualityLinter`, `DefinitionQualityGuidanceTests` `9/9`), semantic revision diff (`DefinitionRevisionComparison`, `3/3`), and bounded data-only synthetic fixture execution (`DefinitionFixtureRunner` + generated `schemas/configurator-fixtures.schema.json`, `4/4`).
2. **Presentation projection & Browser `4x` CPU closure:**
   - Presentation owns immutable workspace snapshots, generation fencing, precomputed `FieldStatesById` / `OptionalMissingCount`, and Store conformance metadata (`DefinitionStoreConformance`).
   - Published static Browser container `sha256:6f56bbed3df0006fb0183567e4669c29fb968b89112cd2f7ea1d9db6305a5a53` (`payloadDigest` `59eb19ae44c0f85a92bf5be3dc9f3596a218c0f2e5871e08e6908aa10272c91f`) empirically closed Track A (`MeasureInteractiveWork.cs` `56/56` cohorts exit `0`; `QF01` `8/8` `passed: true`, see [Browser v1 qualification Section 7](../releases/browser-v1-qualification.md#7-track-a-4x-performance-tail-closure-addendum-2026-11-24)).
3. **Canonical `0.0.1` CLI grammar, delivery verification & Store evidence:**
   - Removed deprecated `env`, `catalogue show`, `artifact open|format|export|preview`, and `definition schema` routes without compatibility shims; delivered `--version`, `info`, `schema list|export`, paged/digest-fenced `definition describe`, `definition diff`, `definition test`, `doctor --require <CAPABILITY>`, and explicit `artifact bundle-create --draft` (`Configurator.Cli.Tests` `421/421`, including outside-checkout `CliInstalledAdoptionTests`).
   - Added generic unsigned `eng/configurator/DefinitionDelivery.cs` (`--self-test` exit `0`, explicit `trustBoundary: "unsigned-local-verification-only"`) and bounded Store conformance categories (`format-only`, `fixture-verified`, `target-runtime-verified`) surfaced in `StoreProject.razor` and `StoreProjectDetailsDialog.razor` (`BrowserDefinitionStoreTests` `20/20`).
4. **Target dogfooding & independent authority:**
   - **ISLAMU Event `0.0.1` rev `8`** (`familyId` `f79259e8-af23-49b0-8d8d-6e371cc96080`, `digest` `1c5c02326014708b23053526ce7e7d4402539fad983d4e16b9539d247ea2e286`): `0` `G12` bilingual advisories, `"acquisition": "local"` enabled strictly on `20` first-party operator secrets while locking `13` third-party issuer credentials to `"existing"`, immutable `store/islamu-event/releases/1..7.json` preserved alongside `8.json`, 3-way `cmp` byte equality and `10/10` cross-repo expected-vector parity with sibling `../Event` (`ConfiguratorDefinitionArtifactTests` `9/9`, `ConfiguratorDefinitionArchitectureTests` `6/6`), and zero Event assembly dependency in Configurator (`StandaloneArchitectureTests` `31/31`).
   - **Grafana OSS `13.2.2`** (`familyId` `c9091324-8fc2-42ed-a659-95b708d4c0a4` rev `2`, `digest` `bb583ec230dbfd0f2a9f0488d6ea3beef9e5ec64d66d4f872c82c132bf34448d`): verified transiently against pinned `grafana/grafana@sha256:ac461fb352abc50da10a51c7d02462e9c05488f11f53f14b3ad79a8145f638a0` (`/api/health` HTTP `200`, verified `.env` + provisioned Graphite datasource) and promoted into logo-free MIT `store/grafana-oss/` (`BrowserStorePackageTests` `2/2`, `DependencyNoticeTests` `2/2`).

**Retained external and future boundaries:**
- Target validation, runtime acceptance, and server apply authority remain strictly with each target application.
- Browser Track B external human/hardware gates (physical screen-reader/braille `en`/`ar`, manual `200%`/`400%` browser zoom, physical macOS/iOS Safari, live Coolify HTTPS edge), non-Linux protected-save host qualification, and independent cryptographic signing authority remain explicitly open (see [Status and evidence](../STATUS.md) and [Configuration experiences journal](../../../dev/_journal/domains/configuration-experiences.md)).
- Future formats and optional extensions remain tracked separately in [secret generation](../../../dev/backlog/configuration-secret-generation.md), [delivery/Event adoption](../../../dev/backlog/guided-configuration-delivery-event.md), and [future formats](../../../dev/backlog/configuration-format-expansion.md).
