# Definition schema, authoring compiler, and CLI redesign — Provider-responsibility assessment

Last Updated: 2026-11-08

## Review Metadata
- Mode: planning
- Subject: Non-UI clean-slate v0.0.1 Configurator schema evolution, deterministic Core Title Case + acronym humanization, unified choices and property ordering, rich non-value field/widget schema admission, asserted ReDoS-free domain formats, hierarchy/predicate/output de-flattening, definition-bound IDE Answer Schema CLI, authoring quality lint, and `islamu-event` canonical `revision: 4` renewal
- Workstream: definition-schema-redesign
- Report kind: implementation-planning assessment
- Report revision: schema-redesign-ivsd-r2
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-11-08
- Reviewed input: `definition-schema-redesign` plan/context/tasks triad revision 2 (strictly non-UI scope, sequenced before `configuration-workspace-redesign` rev 2) against repository `HEAD` `d7b2bfe` and 12 clean-room external specification benchmarks
- Supersedes: `schema-redesign-ivsd-r1` (in-place scope refinement transferring interactive `Presentation`, `Browser`, `Desktop`, and `Terminal` UI responsibilities to `i-vsd-configuration-workspace-redesign.md` `workspace-redesign-ivsd-r2`)

## Executive Summary

Today, ISLAMU Configurator's core definition engine and maintained examples carry five structural and authoring limitations: (1) `Configurator.Core` performs zero identifier humanization when `title` or `choice.label` is omitted, while `examples/islamu-event` hardcodes 344 raw `SCREAMING_SNAKE_CASE`/JSONPath titles and `"Host validator:..."` diagnostics; (2) `DefinitionNode` sorts object properties ordinally for canonical hashing without preserving authored visual field order (`propertyOrder` / `order`); (3) `x-configurator.choices` requires 1:1 duplication of `enum`, and schema nodes lack structured non-value metadata (`placeholder`, `defaultText`, `deprecationMessage`, `widget`, `rows`, `syntax`, `booleanLabels`, `itemTitlePointer`, composite `uniqueBy`); (4) `format` is silently inert unless `x-configurator.assertFormat: true` is added and supports only 4 formats; and (5) single-segment section pointer rules, non-reusable `applicableWhen` ASTs, index-fragile choice translations, and restrictive `outputs.bindings` inflate `examples/islamu-event/configurator.json` to 27,582 lines (including 19 synthetic `select.feature.*` booleans and 2 omitted journey sections), while command-line/IDE developers lack a definition-bound Answer JSON Schema generator.

Per explicit user instruction, work is split sequentially with zero data loss:
- **Workstream 1 (`definition-schema-redesign`, this report — executed FIRST):** Strictly **non-UI** `Configurator.Core` schema/humanizer/validator/compiler upgrades, generated `schemas/*.schema.json`, `Configurator.Cli` (`definition answer-schema` + `definition lint --quality`), `examples/*` migration, `islamu-event` canonical `revision: 4` renewal (`store/islamu-event/releases/4.json`), and non-UI schema/CLI/Store documentation.
- **Workstream 2 (`configuration-workspace-redesign` — executed SECOND):** All interactive `Configurator.Presentation`, `Browser`, `Desktop`, and `Terminal` UI capabilities, including `WorkspaceNamingMode` (`Humanized` ↔ `Technical`), rich widget rendering, consent-safe Compact Power-User Table, Desktop/Terminal document-session editors, Material 3 4-step density, 2D section status/review coverage, public Definition Explorer, and KeePass-style modal secret generation (governed by `i-vsd-configuration-workspace-redesign.md` `workspace-redesign-ivsd-r2`).

This assessment revalidates the non-UI `definition-schema-redesign` triad (`rev-2`) against five provider-responsibility findings (`IVSD-F001` through `IVSD-F005`) and mitigations (`IVSD-M001` through `IVSD-M005`). `Report status: current` and `Disposition: plan-aligned` confirm every non-UI responsibility maps to concrete RFC 2119 scenarios and Red/Green tasks; it does not grant user execution approval, runtime proof, or any religious-legal ruling.

## Scope

Provider-controlled schema admission, canonical JSON identity, deterministic `DefinitionDisplayText` humanization, `DefinitionChoice` and `OrderedPropertyNames` Core models, sensitivity-checked `DefinitionFieldPresentationMetadata` admission, linear-time `DefinitionFormatValidator`, authoring compilation (`DefinitionAuthoringCompiler`), schema import (`DefinitionSchemaImporter`), CLI `definition answer-schema` and `definition lint --quality`, generated `schemas/*.schema.json`, maintained `examples/*`, `store/islamu-event` release renewal, and non-UI `docs/{internal,public}/*`. Interactive `Presentation`, `Browser`, `Desktop`, and `Terminal` UI changes are explicitly out of scope here and owned by `configuration-workspace-redesign`.

## Claim Boundary

Evidence reviewed consists of static repository inspection at `HEAD` `d7b2bfe`, contracts in `PROJECTS.md`, `examples/islamu-event/provider-contract-inventory.md`, and clean-room public specification research on 2026-11-08 across 12 ecosystems. No product builds or tests were executed during planning (`global.json` pins `.NET SDK 10.0.302` while `/usr/share/dotnet/sdk` currently has `10.0.403`, recorded as a pre-Phase-1 prerequisite). No Sharia/fiqh ruling or legal opinion is issued.

## Findings

### IVSD-F001: Core identifier humanization, `defaultText`, and `format` rules must clarify meaning without mutating wire tokens, materializing fake defaults, or faking validation

Converting untitled `SCREAMING_SNAKE_CASE` keys and enum values into `Screaming Snake Case` or `OpenAI Compatible` in `Configurator.Core`, adding `defaultText`, and expanding `format` help operators understand technical settings. However, three Core-level truthfulness hazards must be prevented: (1) humanization must never overwrite `FieldDefinition.Name` / `DefinitionChoice.Value` wire identifiers or mangle symbolic literals (`>=1.2`, `0.0.0.0/0`, `/var/lib/app`, MIME types); (2) `defaultText` must explain dynamic target behavior when omitted without injecting a static JSON value into `AnswerSnapshot` or `DefinitionBundleComposer` outputs; and (3) `format` declarations (`hostname`, `https-url`, `port`, `cidr`) must validate automatically rather than silently accepting malformed inputs when `assertFormat` is forgotten.

Under **Sidq (truthfulness)** and **Bayan (clear disclosure)**, Core metadata must distinguish human display hints from wire data and enforce declared format promises.

- **Mechanism & Mitigation (`IVSD-M001`)**: Implement `DefinitionDisplayText` in `Configurator.Core` using Title Case + a static canonical technical/brand acronym table (`API`, `URL`, `OIDC`, `JWKS`, `TLS`, `OpenAI`, `PostgreSQL`, `ASP.NET`, etc.) applied only when explicit `title` / `choice.label` is absent, passing non-identifier symbolic strings through unchanged, and preserving `TechnicalName` and `WireToken` verbatim alongside `DisplayTitle` and `DisplayLabel`. Keep `defaultText` strictly non-materializing in `DefinitionValidator` and `DefinitionBundleComposer`, and retire `x-configurator.assertFormat` so all 23 admitted Configurator `format` tokens validate automatically.
- **Lifecycle / severity / claim type**: accepted / high / schema truthfulness and wire-integrity constraint
- **Principles / domains**: Truthfulness (Sidq), Clear Disclosure (Bayan), Avoiding Deception / Core schema semantics, validation
- **Stakeholders**: Self-hosting operators, power-user engineers, downstream target applications, definition maintainers
- **Controlled decision**: `DefinitionDisplayText` tokenizer/acronym rules, separate display vs. wire properties on `FieldDefinition`/`DefinitionChoice`, non-materializing `defaultText`, and asserted-by-default `format`
- **Evidence / validation**: `E01`, `E03`, `E04`; inspected `FieldDefinition.cs`, `DefinitionNodeReader.cs`, `examples/grafana-13.2.2/configurator.yaml`
- **Linked mitigation**: `IVSD-M001`
- **Owner / next validation**: Core maintainers; Red `DefinitionDisplayTextTests` and `DefinitionFormatValidatorTests` before implementation
- **Escalation**: None

### IVSD-F002: New schema UI hints (`placeholder`, `defaultText`, `choices`, `deprecationMessage`) and CLI `definition answer-schema` export must uphold strict secret isolation

Admitting richer presentation hints in `Configurator.Core` and adding `islamu-configurator definition answer-schema` for IDE autocomplete creates potential leakage channels if `secret` nodes admit literal sample values in `placeholder`/`examples`/`default`, if localization overrides target non-public subtrees, or if `definition answer-schema` accepts live workspace/answer files and echoes private values into `.vscode/` schema artifacts.

Under **Amanah (trustworthy custody)** and **Tajassus avoidance**, public definition schemas and generated IDE answer schemas must remain 100% free of secret exemplars and private answers.

- **Mechanism & Mitigation (`IVSD-M002`)**: Enforce in `DefinitionNodeReader` that `sensitivity` defaults to `"sensitive"` when omitted, never downgrades beneath ancestor floors, and rejects `default`, `const`, `examples`, `initialValue`, `suggestedValue`, and credential-bearing `placeholder` values on non-public/`secret` subtrees, while keeping `<`, `>`, and control characters forbidden across all human-facing text keywords. Build `DefinitionAnswerSchemaGenerator` and `islamu-configurator definition answer-schema` as a pure function of an admitted **public `Definition`** (accepting zero `--answers` or workspace inputs), marking `secret` leaves `writeOnly: true` with zero sample values, and keeping all Core/CLI exception metadata value-free.
- **Lifecycle / severity / claim type**: accepted / critical / secret isolation and public-projection boundary
- **Principles / domains**: Trust (Amanah), Privacy / Tajassus avoidance, Non-Harm (La Darar) / Core admission, CLI tooling
- **Stakeholders**: Maintainers, developers editing private answer files in VS Code/Neovim/JetBrains, end users protected by deployment secrets
- **Controlled decision**: Sensitivity-floor rules on `DefinitionFieldPresentationMetadata` and pure-definition input contract of `definition answer-schema`
- **Evidence / validation**: `E01`, `E02`; inspected `DefinitionNodeReader.cs:425-443`, `DefinitionSensitivityPolicy.cs`, `DefinitionSchema.cs:22-26`
- **Linked mitigation**: `IVSD-M002`
- **Owner / next validation**: Core and CLI maintainers; Red sensitivity-floor and `DefinitionAnswerSchemaAndLintTests` invariants
- **Escalation**: None

### IVSD-F003: Expanded domain `format` assertions, reusable `predicates`, and expressive `outputs` must remain non-executable, acyclic, and linear-time (`O(n)` ReDoS-free)

Adding 19 new domain formats (`https-url`, `origin`, `ipv4`, `ipv6`, `ip-address`, `cidr`, `port`, `slug`, `env-key`, `bcp47-language-tag`, `iso3166-alpha2`, `uuid`, `semver`, `duration`, `byte-size`, `unix-path`, `relative-path`, `oci-image`, `sha256-hex`, `cron-5`), root `predicates`, and `includeBindings`/`bindObject` output shorthands must remain safe on untrusted definitions loaded in offline Browser WASM or CLI pipelines. Admitting arbitrary user `pattern` regexes, cyclic predicate references, or unbounded binding expansions could trigger catastrophic backtracking (ReDoS), stack overflows, or memory exhaustion.

Under **La Darar (non-harm)** and `AGENTS.md` Rule 2 (**Data-Only Definitions**), definition admission and validation must execute in deterministic bounded time and memory.

- **Mechanism & Mitigation (`IVSD-M003`)**: Keep JSON Schema `pattern` **forbidden**. Implement all 23 admitted formats in `DefinitionFormatValidator` using hand-written `O(n)` span/character parsers (zero backtracking regexes) within existing scalar length budgets. Enforce topological cycle detection (`predicate-cycle`) and max depth `<= 16` on root `predicates`, preserve `propertyOrder`/`order` determinism without breaking canonical JSON hashing, and lower `includeBindings`/`bindObject` through existing `CompositionBudget` limits.
- **Lifecycle / severity / claim type**: accepted / high / denial-of-service immunity and bounded-admission constraint
- **Principles / domains**: Non-Harm (La Darar), Trust (Amanah) / security, Core admission and validation
- **Stakeholders**: All CLI and client users evaluating third-party or remote definitions
- **Controlled decision**: Closed `O(n)` `DefinitionFormatValidator`, forbidden `pattern`, acyclic `predicates`, and `CompositionBudget`-bounded compiler lowering
- **Evidence / validation**: `E01`, `E04`; inspected `DefinitionNode.cs`, `DefinitionNodeReader.cs`, `CompositionLimits.cs`, `DefinitionAuthoringCompiler.cs`
- **Linked mitigation**: `IVSD-M003`
- **Owner / next validation**: Core maintainers; Red `64 KiB` pathological format tests and cyclic-predicate rejection tests
- **Escalation**: Security review if user-authored regular expressions are ever proposed

### IVSD-F004: Value-keyed choice localization, authoring quality lint, and `islamu-event` `revision: 4` de-flattening must preserve Arabic parity, complete section reachability, and target contract fidelity

Reordering enum choices currently misaligns numeric localization pointers (`/x-configurator/choices/1/label`), while `islamu-event` (`revision: 3`) omits `"deployment"` and `"recipient-authorities"` (40 fields) from `presentation.journeys[0]`, diverges in canonical digest between `examples/` and `store/`, carries 344 raw identifier titles / `"Host validator:..."` strings, and buries settings behind 19 synthetic `select.feature.*` booleans. De-flattening `islamu-event` without golden output checks could also alter emitted `.env` or manifest files expected by Event hosts.

Under **Adl (equitable multilingual support)**, **Ihsan (craftsmanship in operator guidance)**, and **Sidq**, authoring modernization must improve human clarity in both English and Arabic while keeping emitted target artifacts faithful.

- **Mechanism & Mitigation (`IVSD-M004`)**: Allow authoring `localization` overrides to address choices by wire `value` (`.../choices[value=OPENAI_COMPATIBLE]/label`), compiled deterministically to runtime pointers, and extend localization admission to `placeholder`, `defaultText`, `deprecationMessage`, and `booleanLabels/*`. Add `islamu-configurator definition lint --quality` (`raw-identifier-title`, `verbatim-choice-label`, `jsonpath-description`, `exporter-diagnostic-description`, `missing-deprecation-message`, `missing-section-description`, `required-secret-missing-guidance`, `unreferenced-journey-section`). Author `islamu-event` canonical **`revision: 4`** restoring `"deployment"` and `"recipient-authorities"` to `journeys[0]`, retiring the 19 fake `select.feature.*` booleans in favor of collapsible section `groups` and reusable `predicates`, providing clear English and Arabic (`ar`) guidance verified against `provider-contract-inventory.md`, and proving golden `DefinitionBundleComposer` output equivalence across standalone, split API/Blazor, and compose fixtures.
- **Lifecycle / severity / claim type**: accepted / high / localization stability, operator comprehension, and target-artifact fidelity
- **Principles / domains**: Justice (Adl), Excellence (Ihsan), Truthfulness (Sidq) / localization, CLI lint, definition authoring
- **Stakeholders**: Community administrators (English and Arabic) deploying ISLAMU Event and third-party maintainers using `definition lint`
- **Controlled decision**: Value-keyed choice localization lowering, `definition lint --quality` rules, and golden-verified `islamu-event` `revision: 4` renewal
- **Evidence / validation**: `E01`, `E02`, `E03`; inspected `DefinitionAdmission.cs:362-400`, `examples/islamu-event/configurator.json`, `store/islamu-event/configurator.json`, `provider-contract-inventory.md`
- **Linked mitigation**: `IVSD-M004`
- **Owner / next validation**: Core, CLI, and definition maintainers; localization-reordering tests, `definition lint --quality` zero-warning check, and golden bundle equivalence tests
- **Escalation**: Verify closed enum sets strictly against `examples/islamu-event/provider-contract-inventory.md`

### IVSD-F005: Clean-room specification independence and immutable Store release history (`releases/1..3.json`)

Drawing schema inspiration from 12 external ecosystems and renewing `islamu-event` in `store/` requires strict respect for third-party IP licenses and Configurator's canonical revision immutability (`AGENTS.md` Rules 3 & 10).

Under **Haqq al-'Ibad (rights of others)** and **Amanah**, no external code or prose may be copied and published Store digests must never be mutated in place.

- **Mechanism & Mitigation (`IVSD-M005`)**: Use only public interface/specification concepts recorded in the clean-room register (`E04`), implement all C# classes and schema descriptions from scratch with **zero new NuGet packages**, and publish `store/islamu-event/releases/4.json` alongside byte-identical `releases/1..3.json` verified by `BuildDefinitionStore.cs` and `.ci/scripts/validate-dependency-license-policy.cs`.
- **Lifecycle / severity / claim type**: accepted / high / IP provenance and canonical Store immutability
- **Principles / domains**: Rights of Others (Haqq al-'Ibad), Trust (Amanah) / legal/IP governance, Store integrity
- **Stakeholders**: Upstream open-source authors, MIT recipients, operators pinned to `islamu-event` revisions `1..3`
- **Controlled decision**: Clean-room first-party implementation, zero added dependencies, and append-only Store release `4.json`
- **Evidence / validation**: `E02`, `E03`, `E04`; inspected `IP_GOVERNANCE.md`, `.ci/policy/dependency-licenses.json`, `BuildDefinitionStore.cs`, `store/islamu-event/releases/`
- **Linked mitigation**: `IVSD-M005`
- **Owner / next validation**: All phase owners; `BuildDefinitionStore.cs`, `validate-dependency-license-policy.cs`, and `Configurator.Architecture.Tests` in Phase 4
- **Escalation**: Legal/IP escalation only if third-party schemas or new packages are ever proposed

## Common Overlooked Failures And Outcomes

1. **Alphabetical Scrambled Properties Returning After JSON Canonicalization**: Because `DefinitionProfile.CanonicalizeJson` sorts object keys lexicographically for SHA-256 digests, relying on YAML property order without persisting `propertyOrder`/`order` in `x-configurator` loses visual field order upon compilation (`Decision 5.2` stores explicit ordering metadata).
2. **Humanizer Mangling Symbolic Enum Tokens**: Naive title-casing corrupts `>=1.2`, `0.0.0.0/0`, or `/var/lib/data` (`IVSD-M001` skips strings with symbolic operators, slashes, colons, or leading digits).
3. **Silent Arabic Translation Drift on Enum Edits**: Inserting an option at index `0` shifts `/x-configurator/choices/1/label` (`IVSD-M004` compiles value-keyed choice translations to exact runtime pointers).
4. **Subtle `.env` / Manifest Drift During `islamu-event` De-Flattening**: Replacing 9,938 lines of explicit projections could drop an environment key unless guarded by pre/post `DefinitionBundleComposer` golden fixture comparisons (`IVSD-M004`).

## Recommendations

1. **Execute `definition-schema-redesign` (Phases 1–4) completely before starting `configuration-workspace-redesign`** so interactive UI work builds directly on finalized `DefinitionDisplayText`, `DefinitionChoice`, `OrderedPropertyNames`, `DefinitionFieldPresentationMetadata`, `DefinitionSectionGroup`, and `islamu-event` `revision: 4` contracts.
   - *Rejected alternative*: Mixing Browser/Desktop/Terminal UI tasks into `definition-schema-redesign` (rejected per explicit user instruction: UI belongs in `configuration-workspace-redesign`).
2. **Enforce Asserted-by-Default `O(n)` Domain Formats and Acyclic `predicates` in Core (Phase 2)** while keeping `pattern` forbidden.
   - *Rejected alternative*: Retaining `x-configurator.assertFormat` or admitting backtracking regexes (rejected: error-prone and ReDoS-vulnerable).
3. **Deliver Pure-Definition `definition answer-schema` and `definition lint --quality` in CLI (Phase 3)** before migrating examples in Phase 4 so `lint --quality` actively verifies the migrated `examples/*` and `islamu-event` `revision: 4`.

## Stakeholders

- **Definition maintainers and integrators**: Gain concise single-source `choices`, `propertyOrder`, nested section pointers, reusable `predicates`, expressive `outputs`, `definition lint --quality`, and documented meta-schemas.
- **CLI and external-IDE power users**: Gain target-specific VS Code/Neovim/JetBrains autocomplete and validation via `islamu-configurator definition answer-schema` and optional `"omitted"` answer envelopes.
- **Self-hosting operators (English and Arabic)**: Gain accurate humanized labels, honest asserted format validation, non-materializing `defaultText`, and complete 16-section `islamu-event` `revision: 4` definitions consumed immediately by all clients (and rendered with full rich widgets/Compact Table in `configuration-workspace-redesign`).

## I-VSD Principles And Domains

- **Sidq & Bayan (Truthfulness & Clarity)**: Wire-preserving humanization, non-materializing `defaultText`, asserted `format` validation (`IVSD-F001`, `IVSD-F004`).
- **Amanah & Tajassus Avoidance (Custody & Privacy)**: Sensitivity floors on new metadata and secret-free pure-definition `answer-schema` export (`IVSD-F002`).
- **La Darar (Non-Harm)**: Linear-time `O(n)` format parsers, forbidden `pattern`, acyclic `predicates`, bounded output lowering (`IVSD-F003`).
- **Adl & Ihsan (Justice & Craftsmanship)**: Value-keyed choice localization, complete `en`/`ar` `islamu-event` `revision: 4` guidance, golden artifact equivalence (`IVSD-F004`).
- **Haqq al-'Ibad (Rights of Others)**: Clean-room specification independence, zero added packages, immutable `store/islamu-event/releases/1..3.json` (`IVSD-F005`).
- **Non-applicable / Sister-Workstream Domains**: Interactive UI rendering, Browser secret-consent table gating, modal secret generator dialogs, and WCAG visual contrast/density tokens are governed by `[i-vsd-configuration-workspace-redesign.md](i-vsd-configuration-workspace-redesign.md)` (`workspace-redesign-ivsd-r2`). Monetization/riba, ranking, AI generation, moderation, and server-side multi-tenancy do not apply.

## Validation Gaps

1. **No runtime execution during planning (host SDK mismatch noted)**: Evidence (`E01`–`E04`) comes from static repository inspection at `d7b2bfe`; `global.json` pinned `10.0.302` at `d7b2bfe` vs. installed `/usr/share/dotnet/sdk/10.0.403`; an uncommitted working-tree bump to `10.0.403` (observed 2026-11-08, made outside this assessment) must be confirmed prior to `TASK-1.1`.
2. **Interactive UI rendering verification**: Verified in the subsequent `configuration-workspace-redesign` workstream (`Browser.Tests`, `Desktop.Tests`, `NativeHost`, `Terminal.Tests`, `MeasureInteractiveWork.cs`), except for mechanical path updates in `BrowserEventManifestWorkflowTests.cs` if `islamu-event` paths nest in Phase 4.

## Escalation Needed

- **Scholarly escalation**: None required.
- **Specialist escalation**: Security/IP review only if user-supplied regular expressions (`pattern`) or new third-party NuGet dependencies are ever proposed.

## Evidence Reviewed

- `E01` (Core & Schemas): `src/Configurator.Core/Definitions/{FieldDefinition,DefinitionNode,DefinitionNodeReader,DefinitionAdmission,DefinitionDeploymentAdmission,DefinitionSchema,DefinitionSection,DefinitionExperience,DefinitionPresentation,DefinitionProfile,DefinitionVocabulary,DefinitionSensitivityPolicy}.cs`, `Authoring/{DefinitionAuthoringCompiler,DefinitionAuthoringSchema,DefinitionAuthoringCodec,DefinitionSchemaImporter}.cs`, `eng/configurator/Configurator.DefinitionSchemaGenerator/Program.cs`, `schemas/*.schema.json` (`d7b2bfe`).
- `E02` (CLI & Tooling): `src/Configurator.Cli/Commands/Definitions/{CliDefinitionCommandModule,CliDefinitionHandlers}.cs`, `eng/configurator/{BuildDefinitionStore,RenderCommandReference,ValidateDefinitionDocumentation}.cs`, `.ci/scripts/validate-dependency-license-policy.cs` (`d7b2bfe`).
- `E03` (Examples, Store & Docs): `examples/{minimal-self-hosted,secret-generation,grafana-13.2.2}/configurator.yaml`, `examples/islamu-event/{configurator.json,definition.json,provider-contract-inventory.md}`, `store/islamu-event/{configurator.json,curation.json,releases/1..3.json}`, `docs/internal/{CORE_DEFINITION_CONTRACT,DEFINITION_CONFORMANCE_PROFILE,MAINTAINER_AUTHORING,DEFINITION_STORE}.md`, `docs/public/{definitions,definition-standard,definition-reference,definition-cookbook,cli-reference}.md` (`d7b2bfe`).
- `E04` (Clean-Room External Specifications, 2026-11-08): Official public specifications for JSON Schema Draft 2020-12, OpenAPI 3.1, VS Code `contributes.configuration`, RJSF v5, Eclipse JSON Forms, Home Assistant Selectors, Backstage Templates (`v1beta3`), Rancher `questions.yml`, RunTipi `config.json`, Kubernetes CRD OpenAPI v3 (`x-kubernetes-*`) & OpenShift OLM, Apple ProfileManifests, Chrome Enterprise `policy_templates.json`, NixOS `lib.mkOption`, and Apple Pkl.

## Missing Evidence

- Executed compiler, `Core.Tests`, `Cli.Tests`, `BuildDefinitionStore.cs`, and `Architecture.Tests` outputs (deferred to Phases 1–4 execution).

## Context Inventory

- **Repository baseline**: `main` at `d7b2bfe`.
- **Sequential two-workstream architecture (per explicit user instruction, 2026-11-08)**:
  1. `dev/active/definition-schema-redesign/` (`rev-2`) — strictly non-UI Core, Authoring, Schemas, CLI, Examples, Store `revision: 4`, and Schema/CLI docs (**executed FIRST**).
  2. `dev/active/configuration-workspace-redesign/` (`rev-2`) — all interactive Presentation, Browser, Desktop, and Terminal workspace, Dual-UX (`Humanized` ↔ `Technical`), Rich Widget, Compact Power-User Table, Document Editor, Explorer, and KeePass Modal Secret Generator UI work (**executed SECOND**).

## Planning Handoff
- Workstream: definition-schema-redesign
- Status: current
- Reviewed input: `definition-schema-redesign` plan/context/tasks triad revision 2 (`2026-11-08`, non-UI scope)
- Findings and mitigations: `IVSD-F001 -> IVSD-M001`, `IVSD-F002 -> IVSD-M002`, `IVSD-F003 -> IVSD-M003`, `IVSD-F004 -> IVSD-M004`, `IVSD-F005 -> IVSD-M005`
- Required plan mappings:
  - `IVSD-F001 / IVSD-M001` -> Scenarios `S01`, `S02`, `S05`, `S07`; Phases 1, 2 (`TASK-1.1`, `TASK-1.2`, `TASK-2.1`, `TASK-2.2`)
  - `IVSD-F002 / IVSD-M002` -> Scenarios `S06`, `S09`, `S10`; Phases 2, 3 (`TASK-2.1`, `TASK-2.2`, `TASK-3.1`, `TASK-3.2`)
  - `IVSD-F003 / IVSD-M003` -> Scenarios `S04`, `S07`, `S08`, `S11`; Phases 1, 2 (`TASK-1.3`, `TASK-1.4`, `TASK-2.1`–`TASK-2.4`)
  - `IVSD-F004 / IVSD-M004` -> Scenarios `S03`, `S12`, `S13`; Phases 1, 2, 3, 4 (`TASK-1.3`, `TASK-1.4`, `TASK-2.3`, `TASK-2.4`, `TASK-3.3`, `TASK-4.1`, `TASK-4.2`)
  - `IVSD-F005 / IVSD-M005` -> Scenario `S14`; All phases and Phase 4 (`TASK-4.2`–`TASK-4.4`, `VERIFY-4`)
- Escalations required before: release (security/IP review only if arbitrary regex `pattern` or new third-party packages are proposed; UI/WCAG escalations tracked in `i-vsd-configuration-workspace-redesign.md`)
- Refresh triggers: Any change that (1) alters wire tokens during humanization or materializes `defaultText` into outputs, (2) admits secret exemplars or workspace answers into `definition answer-schema`, (3) introduces backtracking regexes or cyclic predicates, (4) drops English/Arabic or journey completeness in `islamu-event` `revision: 4`, or (5) mutates `store/islamu-event/releases/1..3.json` in place.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
| --- | --- | --- | --- | --- |
| 2026-11-08 | `none` | `current` (`plan-aligned`, `r1`) | Initial planning intake | `E01`–`E04` evidence packet |
| 2026-11-08 | `current` (`r1`) | `current` (`plan-aligned`, `r2`) | User partitioned non-UI schema/CLI/authoring work (`definition-schema-redesign`, first) from interactive UI work (`configuration-workspace-redesign`, second) | Non-UI scenarios `S01`–`S14` and Phases 1–4 revalidated here; UI findings transferred with zero loss to `i-vsd-configuration-workspace-redesign.md` (`r2`) |
