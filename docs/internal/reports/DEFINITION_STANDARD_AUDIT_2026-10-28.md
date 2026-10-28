# Definition Standard and Adoption Audit

> **Audience:** Adopters | Contributors | Integrators | AI agents
> **Status:** Point-in-time assessment and proposed direction, not an implemented replacement contract
> **Owner:** Core
> **Last Verified:** 2026-10-28
> **Evaluation Date:** 2026-10-28
> **Inspected Revision:** `f9589e6e6cc6df5c5daa19d3f526c5ad78a0bd79`
> **Source Anchors:** `src/Configurator.Core/Definitions/`, `src/Configurator.Core/Artifacts/`, `src/Configurator.Presentation/Presentation/`, `docs/internal/CORE_DEFINITION_CONTRACT.md`, `docs/internal/MAINTAINER_AUTHORING.md`

## Decision and evidence boundary

Do not freeze the current v1 vocabulary as the adopter standard yet. Preserve
the offline, bounded, single-authority architecture, but redesign the public
configuration model around familiar schema concepts, one recursive field model,
explicit value states, and portable interaction guarantees.

The user accepted this direction and requested documentation-first planning
without backward compatibility. That authorizes current documentation
corrections and an implementation plan, not execution of the product redesign.
New property names and behaviors in this report are proposals.

The original conversation audit inspected the working tree based on `5b4d6f0`
on October 27. Upstream and this checkout subsequently converged at the revision
above; the intervening changes affected documentation, not the inspected
product/test/schema-generator owners. This report retains the analysis and
refreshes official-source research on October 28. Immediate documentation
corrections accompany it; the code-level limitations remain.

Evidence consists of source inspection, generated-schema ownership, examples,
existing test assertions, and primary-source documentation. No product builds,
tests, browser/native interactions, or target-application acceptance checks were
executed for this audit. Existing tests establish intended assertions, not fresh
passing evidence. No adoption-time, performance, or usability measurements were
collected.

## Current architecture

Maintainer source, compiled runtime definition, editor meta-schema, target
application JSON Schema, private answers, and prepared artifacts are different
contracts. A generated JSON Schema describing Configurator's definition grammar
does not make the configuration validator a general JSON Schema implementation.

The authoring compiler lowers bounded source and calls the shared runtime codec.
Core owns admission, effective validation, and composition. Presentation owns
workspace state and projections; clients adapt interaction. Native Artifacts
owns protected publication. Browser admission happens before answer collection
and its answers/output remain transient.

Verified anchors:

- `Definitions/Authoring/DefinitionAuthoringCompiler.cs:12` and `:66`:
  source lowering ends in runtime admission.
- `Definitions/SetupDefinitionCodec.cs:20`: closed bounded definition admission.
- `Definitions/DefinitionValidator.cs:97`: effective values, activity, and readiness.
- `Artifacts/DefinitionArtifactComposer.cs:100`: one validated composition owner.
- `Presentation/DefinitionFormModel.cs:170` under the Presentation project:
  presentation consumes Core facts.

Relative Core paths above are under `src/Configurator.Core/`. The public
[current reference](../../public/definition-reference.md) describes implemented
properties and limitations; it does not publish the proposals below as usable syntax.

## Strengths to preserve

1. **One evaluation authority:** source compilation, machine preparation, and
   interactive preparation converge on Core rather than independent validators.
2. **Bounded data-only definitions:** syntax, references, expansion, and projection
   work have resource limits; definitions cannot install or execute plugins.
3. **Strong sensitivity floors:** descendants cannot be demoted by public labels,
   and public definitions cannot carry private defaults/recommendations.
4. **Immutable identity:** family/revision/content conflicts reject; active
   workspaces retain admitted identity instead of silently upgrading.
5. **Preparation is not administration:** output does not grant target acceptance,
   filesystem authority, authentication, or permission to apply configuration.
6. **Honest draft and custody boundaries:** secret deferral remains incomplete;
   browser export differs from native create-only protected writes.
7. **Deterministic authoring:** map insertion does not determine identity;
   source revision editing preserves supported syntax rather than rewriting prose.

These mechanisms are maintainability and responsibility assets. A redesign
should extend their owner boundaries, not introduce an orchestration framework,
generic cache, second answer tree, or second semantic authority.

## Findings

### DS-01: Public examples contradicted admission

At the inspected revision, the primary integration example used
`opaque-token`/`create`, which `SetupDefinitionCodec.cs:156` rejects. The schema
catalog added `$schema` to runtime `schemaVersion`, rejected by
`SetupDefinitionSourceCodec.cs:22`. Schema-import examples incorrectly retained
defaults/public classifications and omitted required outputs. Some field examples
declared defaults without a public context.

**Consequence:** an adopter can follow the principal guide and fail immediately.
**Direction:** correct today's prose now, then generate complete examples from
executable fixtures during implementation. Do not test-pin narrative prose.
The companion documentation corrections address these statements, not the
underlying standard-design findings.

### DS-02: Default and omission semantics are not uniform

`DefinitionValidator.cs:134` applies root defaults to missing answers.
`ObjectValid` at `:421` does not recursively insert nested defaults.
`DefinitionCollectionItem.cs:12` in Presentation seeds those defaults when an
interactive container is created. Root omission also restores the effective
default; presence tests intentionally retain that behavior.

**Consequence:** `default` means both effective fallback and creation-time seeding,
and an apparent omission cannot suppress a root default.
**Direction:** distinguish descriptive target defaults, inserted initial values,
opt-in suggestions, reset, omission, explicit null, and empty supplied values.
Resolve them centrally without automatically instantiating optional containers.

### DS-03: Collections cannot reuse the complete field model

`DefinitionVocabulary.cs:5` and `DefinitionSchemaImporter.cs:186` split `list`,
`itemType`, and object-item `properties`. Scalar item bounds/enums and nested
arrays cannot be represented uniformly.

**Consequence:** ordinary configuration shapes need rejection or special-case
keywords. **Direction:** use recursive `items`; model nullable values, typed maps,
and bounded provider alternatives deliberately. A full unrestricted schema
language is not necessary to remove this representational defect.

### DS-04: Target names are coupled to internal IDs

Root keys can receive deterministic hashes, but nested keys must satisfy ASCII
identifier grammar (`DefinitionAuthoringCompiler.cs:219` and `:244`). Generic
normalization/canonicalization also rejects case-folded/NFC collisions.

**Consequence:** legitimate target keys such as `_internal`, `1st`, and
case-distinct names can be unrepresentable.
**Direction:** separate internal identity, target keys, display text, and output
locations. Preserve exact target keys and define path escaping independently of
filesystem rules. JSON Pointer supplies concrete JSON addressing, not wildcards
or implicit row-relative selection [SR-03].

### DS-05: Applicability, requiredness, visibility, and emission need separate contracts

Nested conditions reject; root activation and conditional-requiredness edges
share cycle detection (`SetupDefinitionCodec.cs:82`, `:213`). `present` examines
effective non-null values, including defaults and empty strings
(`DefinitionValidator.cs:299`).

**Consequence:** repeated records cannot select their own dependent fields, and
mutual requirements can be mistaken for activation cycles.
**Direction:** separate graph-dependent applicability from validation
relationships and UI-only disclosure. Define missing/null/invalid/deferred truth
tables and bounded nested scopes. Hidden must never silently mean valid.

### DS-06: Validation names can misdescribe their meaning

Admission knows validator names but does not reject every incompatible field
type. `absolute-uri` is actually credential-free HTTP(S), field `format` is
descriptive, required strings reject whitespace, and length counts UTF-16 code
units (`DefinitionValidator.cs:335` and `:400`).

**Consequence:** admitted required fields can be unsatisfiable and familiar names
can imply different semantics.
**Direction:** type-check assertions; prefer ordinary numeric constraints to
redundant aliases; distinguish annotation from assertion; specify Unicode and
numeric domains without rounding or silent schema weakening [SR-01, SR-02].

### DS-07: Output shape failures can survive definition checks

Projection admission checks operation structure/references, while dotenv and
Markdown enforce shape during composition (`SetupDefinitionCodec.cs:338`,
`DefinitionArtifactComposer.cs:336`, `:392`).

**Consequence:** maintainers discover a statically knowable incompatibility only
after collecting valid answers.
**Direction:** type-check mappings at compilation. Syntax acceptance, semantic
admission, projection validity, readiness, composition, publication, and target
acceptance must remain distinct claims.

### DS-08: IDE success does not provide actionable compiler recovery

The authoring schema reuses nullable runtime shapes and exposes nested
conditions; final admission is stricter. CLI handlers collapse definition
exceptions to `definition-source-invalid` at `$.source`
(`DefinitionAuthoringSchema.cs:14`, `SetupCliDefinitionHandlers.cs:96`).

**Consequence:** a large source can pass editor checks and then fail without the
affected property or safe correction.
**Direction:** encode cheap structural rules in editor schemas and retain bounded
codes, definition locations, and public-source spans through lowering. Never
echo answer values, arbitrary input keys, or private filesystem paths.

### DS-09: Schema import lacks a complete transformation ledger

The importer accepts annotations that are discarded or descriptive-only:
defaults disappear, format becomes help, and lifecycle annotations do not become
equivalent native behavior (`DefinitionSchemaImporter.cs:100`, `:173`).

**Consequence:** successful scaffolding can conceal loss of important author intent.
**Direction:** report preserved, converted, advisory-only, discarded, and
unsupported concepts by safe location. Keep unsupported assertions fail-closed;
acknowledgment must not become a bypass.

### DS-10: Presentation portability is currently overstated

Browser consumes authored journeys, rich guidance, and correction navigation.
Desktop/Terminal inspected forms are flatter; recommendation actions, list
minimum editing policy, and protected choices differ.

Evidence: `ConfigurationEditor.razor:112` in Browser,
`FormFieldFactory.cs:17` and `ListFieldViewModel.cs:15` in Desktop,
`SetupTerminalFieldEditor.cs:29` in Terminal.

**Consequence:** an adopter cannot infer a portable interaction promise from a
property's admission alone.
**Direction:** define required semantic capabilities, optional presentation
fallbacks, common essential guidance, and early refusal of unsupported required
features. Equivalent validity does not require identical layouts.

### DS-11: Journeys require bookkeeping without equivalent expressive power

Every root belongs to one section and every experience lists every section,
with root-only layout (`SetupDefinitionCodec.cs:224`).

**Consequence:** adding a field requires maintaining several complete structures.
**Direction:** derive a neutral journey when omitted, support sections without
mandatory multi-journey machinery, define reachable fallback placement, and
preserve explicit order. Required/error work must remain reachable in every view.

### DS-12: Locale declaration does not localize definitions

`locales` is a list, but titles/help are scalar strings consumed directly
(`SetupDefinition.cs:45`, Presentation `DefinitionFormIndex.cs:17`).

**Consequence:** translated client chrome can be mistaken for translated
configuration requirements.
**Direction:** define default locale, translations, fallback, choice/error text,
and direction without changing machine identities or output values [SR-05].

### DS-13: Guidance restrictions exclude ordinary technical documentation

Text admission rejects controls/angle brackets; instructions exclude additional
markers; links reject query and fragment delimiters
(`SetupDefinitionCodec.cs:272`, `:385`).

**Consequence:** multiline instructions and ordinary documentation anchors are
unavailable even when displayed as inert text.
**Direction:** permit bounded safe plain text and static fragments deliberately.
Rendering safety must not introduce answer interpolation, prefetch, opener/referrer
transfer, arbitrary HTML, or executable metadata.

### DS-14: Generation and import are different capabilities

The inverse accepts unconditional root-object direct copies, not every admitted
projection (`DefinitionImport.cs:70`). Formats are dotenv/JSON/YAML/Markdown.

**Consequence:** users with existing configuration encounter a narrower workflow
than new-generation users.
**Direction:** report importability/rejection reasons; prove inverses for bounded
reversible mappings only. Preserve conflict/provenance rules and never silently
drop unknown operator settings. TOML/INI and lossless comment editing remain
separate format-expansion work.

### DS-15: Identity and compatibility need portable specifications

Versions are exact strings (`SetupDefinitionRegistry.cs:25`). The custom
canonicalization profile uses bounded exact decimals and .NET serialization
(`DefinitionJson.cs:58`), not RFC 8785.

**Consequence:** undocumented version or serializer assumptions become cross-tool
breaking changes.
**Direction:** distinguish authoring/runtime/profile/definition/application
versions, publish canonical vectors, and define digest coverage. Do not claim
authorship from a self-computed hash. Consider ranges only under an explicit
version scheme; JCS is not an automatic substitute for the current numeric model
[SR-04].

### DS-16: Evolution needs an explicit semantic-versus-advisory policy

Unknown properties currently reject (`DefinitionAuthoringCompiler.cs:18`).

**Consequence:** typos are caught, but harmless organization metadata needs
sidecars and unsupported future behavior has no capability vocabulary.
**Direction:** reject unknown semantic requirements, admit only bounded inert
annotations under an explicit namespace, and define optional presentation
fallbacks. Metadata must never acquire execution or retrieval authority.

## Industry comparison and functional lessons

There is no single source here that supplies Configurator's whole standard.
The conclusion is a design inference across independent public interfaces, not
a measured product ranking.

| Source | Observed interface fact | Transferable requirement |
|---|---|---|
| JSON Schema [SR-01, SR-02] | Shared type/constraint/annotation vocabulary; defaults are annotations | Familiar semantics and an exact support matrix |
| JSON Pointer [SR-03] | Concrete JSON locations with defined escaping | One explicit path syntax; separate row scopes |
| JSON Forms [SR-05] | Data validation, UI rules, and localization have separate interfaces | Visibility is not applicability; translations are first-class |
| react-jsonschema-form [SR-06] | Default/optional-container population has explicit policies | Decide initialization before shipping clients |
| VS Code [SR-07] | Labels/descriptions/order/deprecation enrich configuration editing | Stable typed choices with human explanations |
| Backstage [SR-08] | Schema-driven multi-step input and preview/editing workflow | Fast author-preview-correct feedback; no action engine transfer |
| Helm [SR-09] | Validation applies to final effective values | Define the exact post-resolution value being validated |
| Terraform [SR-10] | Nullability, defaults, sensitivity, and retention are distinct | Masking is not a custody guarantee |
| Kubernetes CRDs [SR-11] | Structural schemas and validation work are budgeted | Bound schema compilation and answer evaluation |
| Home Assistant [SR-12] | Selectors express input intent beyond primitive type | Units and constrained editing should not arbitrarily alter stored values |
| JsonSchema.Net [SR-13] | A general-purpose build/evaluate library is a viable alternative to investigate | Library adoption requires proof of this product's budgets, decimal semantics, offline closure, licensing, and WASM/AOT behavior |

No third-party renderer, action framework, source, test corpus, or distinctive
schema organization is adopted by this comparison.

## Recommended design and rejected alternatives

Prefer a bounded standards-aligned configuration profile, separate Configurator
interaction/privacy/output descriptors, and one Core-owned admitted model.
Keep the common source small; generated runtime material may be explicit.

| Alternative | Strongest argument | Why it is not the preferred direction |
|---|---|---|
| Polish the custom v1 language | Least immediate change; existing interpreter and clients | Retains avoidable vocabulary, item-model, and import translation burden |
| Unrestricted JSON Schema plus arbitrary extensions | Broad ecosystem coverage | Unbounded semantic/rendering surface; no implied safe offline/plugin authority |
| Bounded profile lowered into the existing owner | Familiar schemas, explicit limits, one authority | Requires deliberate conformance work; must not market unsupported keywords as supported |
| Add a general-purpose evaluator immediately | Avoid reimplementing standard assertions | Public docs alone do not qualify package closure, cost limits, exact numbers, safe errors, or all target publications; no dependency is selected by this audit |

The public contract should separate identity/compatibility, configuration data,
Configurator behavior/presentation, and output mapping. One authoring file can
contain all four; four responsibilities do not require four files.

### Mandatory, conditional, and quality requirements

Mandatory grammar should cover contract identity, project identity, configuration
shape, and meaningful outputs. Tooling should generate lineage/revision data.
Feature-dependent requirements cover item schemas, credential context, localized
fallback, and required capabilities. Quality lint covers useful labels, meaningful
help, acquisition instructions, and deprecation guidance.

Do not make every property mandatory to compensate for weak tooling. An optional
property needs a defined omission behavior; explicit null must not be used as an
undocumented synonym for omission.

### Naming candidates

These names are proposed, not admitted today:

| Current | Proposed direction |
|---|---|
| `list` / `itemType` | `array` / recursive `items` |
| String-like `enum` type / `choices` | Base type plus `enum`; separate option descriptions |
| Overloaded `default` | Standard descriptive default versus explicit inserted initial value |
| `recommended` | Opt-in `suggestedValue` |
| `activeWhen` | Applicability wording when validation/output participate |
| `disclosure` / essential-vs-required ambiguity | Detail level independent of requiredness |
| `experiences` | Concrete journeys/views terminology |
| `applySemantics`, `restart` | Advisory target-update/restart requirements, with unknown distinct from none |

Keep `familyId` and `revision` if their lineage meaning remains useful. Renaming
every technical word is not simplification.

### Value-state decisions

Specify never supplied, explicit reset, explicit omission, explicit null, empty
string, whitespace, false, zero, empty containers, absent optional containers,
inactive values, and missing draft secrets. Do not instantiate optional objects
merely because a descendant has an initial value. Suggestions are never silent
answers. Resolve effective values once in the shared owner.

## Adopter and user experience

Users need explainable choices, why a field became required, where to obtain a
credential, what is omitted, and what remains incomplete. Typed option values must
not be reconstructed from display text. Advanced required work remains reachable.
Temporary invalid editing states need a consistent recovery policy.

Adopters need initialize/import -> author -> preview -> correct safe diagnostics
-> prepare synthetic vectors -> validate with the actual target -> distribute.
Manual GUID allocation, exhaustive layout bookkeeping, and generic document-wide
errors should not be prerequisites for a first useful integration.

The documentation package should contain a short tutorial, public normative
specification, property reference, cookbook, schema support/transformation matrix,
client capability matrix, diagnostic catalog, and version/conformance guide.
Public procedures and internal technical anchors update together; the portable
adoption skill follows the finished contract rather than encoding workarounds.

## Review questions to settle

### Data model

1. Is the public model a bounded JSON Schema profile or a distinct language?
2. Can the same schema describe a root, property, item, or map value?
3. Are target keys independent from internal IDs and labels?
4. Are absent, null, empty, and defaulted values distinguishable?
5. Does requiredness describe presence separately from nonblank validity?
6. Can ordinary provider alternatives and row-local conditions be represented?

### Effective values

7. Is a default descriptive, inserted, or fallback behavior?
8. Does omission suppress initialization or restore it?
9. When are nested initial values materialized?
10. Do conditions inspect raw or effective values?
11. What does invalid/deferred input mean to a condition?
12. Which relationships require a dependency graph and which are validation only?

### Interaction

13. Which guarantees are mandatory across interactive clients?
14. Which hints have a safe fallback?
15. Are option identity, label, and explanation separate?
16. Can the user understand why a question appeared?
17. Are localization, fallback, direction, and invariant output specified?
18. Can a user correct an invalid collection without being trapped by controls?

### Outputs and existing configuration

19. Can compilation prove serializer compatibility?
20. Which mappings have a demonstrable inverse?
21. How are conflicts and unknown imported keys handled?
22. Does a descriptor advise the target or grant an action?
23. Are restart needs known, unknown, or explicitly absent?
24. Are preparation, draft readiness, publication, and target acceptance distinct?

### Evolution and adoption

25. Which version axis governs each document and consumer?
26. What happens to unknown semantic requirements?
27. Can inert annotations be added without redefining execution?
28. Can another implementation reproduce the digest?
29. Can an adopter succeed with public docs and the standalone CLI alone?
30. Which actual integrations prove that the profile is sufficient?

## Conformance and release evidence

Use independent expected results for source equivalence, recursive value states,
condition truth tables, paths/escaping, assertion compatibility, projection shape,
draft readiness, privacy floors, and canonical identity. Do not mirror production
algorithms in expected values or pin prose with product tests.

Compile complete public examples. Check structural schemas against their owning
generator. Compare equivalent Core, machine, and interactive results. Exercise
actual changed client surfaces at final workstream qualification; a build or
bUnit result is not published-browser or native-host proof.

Representative integrations should include flat environment variables, nested
JSON/YAML, optional objects, constrained arrays, object lists, typed maps,
provider alternatives, credentials, multiple outputs, existing-file import,
drafts, localization, and explicit compatibility. New formats are separately
qualified, never assumed.

Measure local adopter trials: time to first useful artifact, error recovery,
concepts needed for simple cases, manual import repairs, understanding of
omission/default/draft behavior, and target acceptance. These are proposed
evaluation measures, not measurements or telemetry added by this work.

## Documentation now versus after implementation

**Correct now:** misleading examples, actual source/runtime discriminators,
mandatory/optional v1 properties, effective-value limitations, import losses,
client differences, digest/authority wording, and this retained research.

**Publish as draft design only:** replacement vocabulary, recursive profile,
new value-state policies, localization shape, capability/evolution policy,
and proposed conformance behavior.

**Update with the owning implementation slice:** generated schemas, admitted
examples, command receipts/diagnostics, client instructions, import promises,
output mappings, canonical vectors, package/store declarations, and technical
source anchors. A future documentation phase may promote verified draft sections
to current reference; it must not postpone truthful docs for already changed code.

**After standard qualification:** improve the portable integration skill.
Target-specific Event migration/delivery remains the separately recorded
[delivery/adoption work](../../../dev/backlog/guided-configuration-delivery-event.md).
Native public-file physical custody remains its own
[follow-up](../../../dev/backlog/cli-public-authoring-custody.md); this schema
redesign must not claim it is solved.

## Source register and clean functional handoff

Sources below are public standards or official interface documentation.
Observations are paraphrased functional requirements; no external implementation,
tests, assets, or distinctive expressive organization is transferred.

| ID | Primary source | Access date | Evidence used |
|---|---|---|---|
| SR-01 | [JSON Schema specification](https://json-schema.org/specification) and [validation](https://json-schema.org/draft/2020-12/json-schema-validation) | 2026-10-27; refreshed 2026-10-28 | Shared vocabulary, presence, length, numeric and format boundaries |
| SR-02 | [JSON Schema annotations](https://json-schema.org/understanding-json-schema/reference/annotations) | 2026-10-28 | Defaults and descriptive annotations are not automatic insertion |
| SR-03 | [RFC 6901](https://www.rfc-editor.org/rfc/rfc6901.html) | 2026-10-27 | Concrete pointer escaping and evaluation |
| SR-04 | [RFC 8785](https://www.rfc-editor.org/rfc/rfc8785.html) | 2026-10-27 | Canonicalization alternative and numeric-model distinction |
| SR-05 | [JSON Forms UI schema](https://jsonforms.io/docs/uischema/), [rules](https://jsonforms.io/docs/uischema/rules), [i18n](https://jsonforms.io/docs/i18n/) | 2026-10-27; rules refreshed 2026-10-28 | Separate UI effects and translation interfaces |
| SR-06 | [react-jsonschema-form form properties](https://rjsf-team.github.io/react-jsonschema-form/docs/api-reference/form-props/) | 2026-10-27 | Explicit optional/default-population policies |
| SR-07 | [VS Code contribution points](https://code.visualstudio.com/api/references/contribution-points) | 2026-10-28 | Configuration option metadata and bounded support |
| SR-08 | [Backstage templates](https://backstage.io/docs/features/software-templates/writing-templates/) | 2026-10-27 | Schema-driven multi-step authoring and feedback |
| SR-09 | [Helm charts](https://helm.sh/docs/topics/charts/) | 2026-10-27 | Effective-values validation |
| SR-10 | [Terraform variables](https://developer.hashicorp.com/terraform/language/block/variable) | 2026-10-27 | Separate null/default/sensitivity/retention concepts |
| SR-11 | [Kubernetes CRDs](https://kubernetes.io/docs/tasks/extend-kubernetes/custom-resources/custom-resource-definitions/) | 2026-10-27 | Structural constraints and bounded validation work |
| SR-12 | [Home Assistant selectors](https://www.home-assistant.io/docs/blueprint/selectors/) | 2026-10-27 | Semantic editing intent |
| SR-13 | [JsonSchema.Net basics](https://docs.json-everything.net/schema/basics/) | 2026-10-28 | General-purpose evaluator considered, not selected or package-qualified |

Context7 documentation requests during the refresh hit its monthly quota;
official web documentation was used instead. The library-page extractor returned
navigation/truncated content, so no detailed API, version, package closure,
performance, or AOT claim is based on that extraction.
