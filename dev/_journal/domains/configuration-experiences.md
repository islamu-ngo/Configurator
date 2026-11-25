# Configuration Experiences, CLI Adoption & Dogfooding Findings

Last Updated: 2026-11-24 Europe/Brussels

Use [journal guidance](../README.md), [the finding template](../FINDING_TEMPLATE.md), and [promotion rules](../PROMOTION_RULES.md) for recording and promotion.

## Durable Findings

[2026-11-24 Europe/Brussels] — Eliminate redundant validation/reparse before adding WASM cooperative yields

**Context**: While closing Track A (`4x` CPU throttled `QF01` preparation `214.1ms`/`224.4ms > 200ms`, `few-root-deep-tree` `4x` reverse `407.1ms > 400ms`, and `compact-600` first-use/GC feedback outliers) against the published static Browser AOT WASM container (`Phase 1`).

**Symptom / Observation**: Adding naive `await Task.Yield()` alone inside preparation could not overcome the baseline `4x` latency because `DefinitionArtifactComposer` re-validated already-validated answers and performed quadruple JSON serialize/reparse cycles per artifact, while `ConfigurationField` and `ConfigurationReview` walked subtrees repeatedly on mount/review transitions (`> 600` nodes).

**Root Cause**: Validation and canonical JSON output each walked and re-serialized intermediate `JsonDocument`/`JsonNode` trees independently, while `DefinitionNode` lacked precomputed `RequiredSet` and `HasOptionalDescendant` flags and `ConfigurationReview` rendered all effects synchronously on the first frame.

**Resolution**: Introduced single-pass cooperative `DefinitionPreparationSession` with read-only `JsonElement` validation, streaming canonical `Utf8JsonWriter` backed by `ZeroingByteBufferWriter` (zeroing staged bytes via `CryptographicOperations.ZeroMemory` on cancellation/revocation), admission-time precomputed `DefinitionNode.{RequiredSet,HasOptionalDescendant}`, O(1) `DefinitionFormFacts.{FieldStatesById,OptionalMissingCount}`, generation/privacy/disposal fencing across every `await` in `ConfigurationEditor.Publication.cs`, and two-phase `nextFrame` effects hydration (`> 128` nodes) in `ConfigurationReview.razor`. Verified empirically against rebuilt container `sha256:6f56bbed3df0006fb0183567e4669c29fb968b89112cd2f7ea1d9db6305a5a53` (`payloadDigest` `59eb19ae44c0f85a92bf5be3dc9f3596a218c0f2e5871e08e6908aa10272c91f`): `MeasureInteractiveWork.cs` passed `56/56` cohorts (`exit 0`, `few-root-deep-tree` `4x` reverse `p95=377.0ms`, `compact-600` `1x` first-use `35.5ms` / `4x` `fbMax=187.1ms`) and `QF01` passed `8/8` (`wide-nested-object` `4x p95=140.5ms`, `large-map-list` `4x p95=166.4ms`).

**Why This Matters for Future Work**: Under Mono WASM's non-generational GC, eliminating intermediate allocations and redundant passes yields far larger tail-latency gains than scheduling alone, and every cooperative yield boundary in a secret-bearing UI MUST re-check `_generation`, `_disposed`, and `SessionPrivacy` before adopting staged bytes.

**References**:
- `src/Configurator.Core/Artifacts/DefinitionPreparationSession.cs`
- `src/Configurator.Core/Artifacts/ZeroingByteBufferWriter.cs`
- `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.Publication.cs`
- `tests/Configurator.Browser.Tests/Components/BrowserPreparationYieldFencingTests.cs`
- [`docs/internal/releases/browser-v1-qualification.md`](../../../docs/internal/releases/browser-v1-qualification.md#7-track-a-4x-performance-tail-closure-addendum-2026-11-24)

**Promotion Consideration**:
- [x] Candidate for ADR / `MAJOR_DECISIONS.md` (`ADR-single-pass-preparation-ownership.md` and `ADR-configuration-experience-and-answer-authority.md`)

---

[2026-11-24 Europe/Brussels] — Parser coordinates must count UTF-8 BOM bytes and Unicode scalar columns without forwarding raw parser text

**Context**: While implementing value-safe parser coordinates (`CompositionSourceLocation` and `DefinitionAuthoringDiagnostic`) across JSON and YAML authoring parsers (`Phase 2`) and surfacing them in machine CLI diagnostics (`Phase 3`).

**Symptom / Observation**: Raw `System.Text.Json.JsonException` and `YamlDotNet.Core.YamlException` messages often quote offending tokens, member names, or input fragments, while naive `string.IndexOf` / UTF-16 `char` offsets drift on surrogate pairs (emoji), multi-byte UTF-8 Arabic text, CRLF line endings, and leading 3-byte UTF-8 BOMs (`EF BB BF`).

**Root Cause**: Parser exceptions expose UTF-16 character counts or raw text excerpts rather than canonical UTF-8 byte offsets and Unicode scalar (`Rune`) columns, creating both editor jump drift on bilingual definitions and a secret-leakage channel if a user passes private answers to an authoring command.

**Resolution**: Added `CompositionSourceLocation` (`Line`, 1-based `Rune`-counted `Column`, 0-based original UTF-8 `ByteOffset` inclusive of any 3-byte BOM) and `DefinitionAuthoringDiagnostic` (`code`, known RFC 6901 `path`, numeric location only), stripping all raw parser exception messages, unknown keys, snippets, and private paths. Verified with `DefinitionAuthoringDiagnosticTests` (`6/6`), `CliDiscoveryAndGrammarTests` (`13/13`), and `CoreArchitectureTests`.

**Why This Matters for Future Work**: Any diagnostic crossing an authoring or CLI machine boundary must emit strictly closed code tokens, known schema pointers, and numeric offsets (`Rune` columns + BOM-inclusive UTF-8 byte offsets) rather than forwarding exception `.Message` strings.

**References**:
- `src/Configurator.Core/Composition/CompositionSourceLocation.cs`
- `src/Configurator.Core/Definitions/Authoring/DefinitionAuthoringDiagnostic.cs`
- `tests/Configurator.Core.Tests/Definitions/DefinitionAuthoringDiagnosticTests.cs`
- [`docs/internal/CLI_CONTRACT.md`](../../../docs/internal/CLI_CONTRACT.md)

**Promotion Consideration**:
- [x] Candidate for `docs/internal/QUICK_REFERENCE.md` (already reinforced in privacy/diagnostic invariants)

---

[2026-11-24 Europe/Brussels] — Conditional requiredness and unannotated enum choices hid 88 Event bilingual guidance gaps

**Context**: While extending `DefinitionQualityLinter` to close coverage gap `G12` (`Phase 2`) and auditing `examples/islamu-event/configurator.json` rev `7` and `examples/grafana-13.2.2/configurator.yaml` rev `1` (`Phases 5–6`).

**Symptom / Observation**: Before `G12`, `definition lint --quality --strict` reported `0` advisories on Event rev `7` and Grafana rev `1`, yet inspecting conditionally activated subtrees revealed `88` missing `en`/`ar` operator explanations in Event (required fields inside optional feature objects, `requiredWhen` leaves, `if`/`then`/`else`/`dependentRequired` branches, and unannotated `enum` choices) plus `1` unguided required leaf (`/datasource/name`) in Grafana.

**Root Cause**: Earlier quality linting checked only unconditionally required top-level paths and skipped container-local `required` members inside optional objects, conditional clauses, and enum choice labels without guidance.

**Resolution**: Extended `DefinitionQualityLinter.cs` to traverse container-local `required`, `requiredWhen`, `if`/`then`/`else`/`dependentRequired`, and `enum` choices (`choice-missing-guidance`) without changing runtime admission, then authored concise bilingual `en`/`ar` explanations on Event rev `8` (`0` advisories) and Grafana rev `2` (`0` advisories), pinned by `DefinitionQualityGuidanceTests` (`9/9`), `EventDeploymentContractTests` (`7/7`), and `GrafanaDefinitionConformanceTests` (`3/3`).

**Why This Matters for Future Work**: Top-level requiredness linting misses most real-world deployment toggles because optional feature blocks carry their own required fields; `--quality --strict` must evaluate local requiredness under every conditional activation path.

**References**:
- `src/Configurator.Core/Definitions/Authoring/DefinitionQualityLinter.cs`
- `tests/Configurator.Core.Tests/Definitions/DefinitionQualityGuidanceTests.cs`
- `examples/islamu-event/configurator.json`
- `examples/grafana-13.2.2/configurator.yaml`

**Promotion Consideration**:
- [x] Stays in journal only (linter now enforces this automatically in CI and conformance suites)

---

[2026-11-24 Europe/Brussels] — Local CSPRNG secret profiles belong strictly on first-party operator secrets, never third-party issuer tokens

**Context**: While advancing `islamu-event` to `0.0.1` rev `8` (`Phase 5`) and opting first-party deployment secrets into Configurator's local generation capability (`"acquisition": "local"`).

**Symptom / Observation**: `examples/islamu-event/configurator.json` declares `33` sensitive secret leaves split cleanly between `20` first-party operator-owned secrets (`/platform/setupSecret`, `/localBootstrap/password`, and self-hosted Postgres/Scylla/Redis/container admin passwords) and `13` third-party issuer credentials (Keycloak client secrets, Stripe keys, Svix keys, Infisical tokens, S3 credentials, SMTP password, AI provider API keys).

**Root Cause**: Offering local random generation on third-party issuer tokens would generate syntactically valid random strings that fail external provider authentication on startup and mislead operators about credential ownership.

**Resolution**: Enabled `"acquisition": "local"` with CSPRNG `generation: { length: 32 }` strictly on the `20` first-party operator secrets, locked all `13` third-party issuer secrets to `"acquisition": "existing"` with issuer acquisition guidance, and pinned exact policy invariants in `EventDeploymentContractTests.cs` (`7/7`).

**Why This Matters for Future Work**: Every target definition adopting local secret generation should partition sensitive fields explicitly into operator-minted secrets (`local`) vs. external-issuer credentials (`existing`) and enforce that boundary in conformance tests.

**References**:
- `examples/islamu-event/configurator.json`
- `tests/Configurator.Core.Tests/Definitions/EventDeploymentContractTests.cs`
- [`docs/internal/adr/ADR-configuration-experience-and-answer-authority.md`](../../../docs/internal/adr/ADR-configuration-experience-and-answer-authority.md)

**Promotion Consideration**:
- [x] Candidate for skill update: `configurator-integrate` (already reflected in authoring guidance)

---

[2026-11-24 Europe/Brussels] — Cross-repo Event parity and Configurator.Release.proj 5-schema packaging boundary

**Context**: While migrating sibling `../Event/configurator/definitions/islamu-event/` off legacy `v1` rev `9` onto unified `0.0.1` rev `8` (`Phase 5`) and adding `schemas/configurator-fixtures.schema.json` (`Phase 2`).

**Symptom / Observation**: (1) Compiling handwritten YAML in `../Event/configurator/definitions/islamu-event/configurator.yaml` via the standalone CLI reproduced `examples/islamu-event/definition.json` and `store/islamu-event/releases/8.json` byte-for-byte (`3-way cmp` exit `0`) and matched all `10/10` expected target files without adding any project or assembly reference to `../Event`. (2) `eng/configurator/Configurator.Release.proj` explicitly packages `5` operator schemas (`configurator-authoring`, `setup-definition`, `configurator-answers`, `configurator-cli`, `definition-store-curation`) pinned by `ReleaseContractTests` and `StandalonePackageTests`, whereas `schemas/configurator-fixtures.schema.json` is an engineering/conformance schema exported directly via `islamu-configurator schema export --kind fixtures`.

**Root Cause**: Separating canonical compiled output identity from repository build graphs keeps Configurator 100% standalone (`StandaloneArchitectureTests` `31/31`), and keeping engineering schemas outside the 5-schema release archive preserves deterministic operator-package contracts (`docs/internal/PACKAGING.md`).

**Resolution**: Verified 3-way `cmp` equality + `10/10` cross-repo expected-vector `cmp` via `CONFIGURATOR_EVENT_ARTIFACT_DIRECTORY`, executed `../Event` MTP wire (`9/9`) and architecture (`6/6`) suites with zero DB startup while preserving untouched dirty docs in `../Event`, and exposed `fixtures` through `schema list|export` without breaking the 5-schema operator release archive.

**Why This Matters for Future Work**: New maintainer/engineering schemas under `schemas/` must be checked against `docs/internal/PACKAGING.md` and `ReleaseContractTests` before altering `Configurator.Release.proj`, and cross-repo definition parity should always be proven via standalone CLI compilation + `cmp` rather than build-time coupling.

**References**:
- `examples/islamu-event/definition.json`
- `store/islamu-event/releases/8.json`
- `eng/configurator/Configurator.Release.proj`
- [`docs/internal/PACKAGING.md`](../../../docs/internal/PACKAGING.md)

**Promotion Consideration**:
- [x] Stays in journal only (boundaries already captured in `PACKAGING.md` and `StandaloneArchitectureTests`)

---

[2026-11-24 Europe/Brussels] — Clean-room external Store promotion requires Dockerignore allowlisting and strict trademark/license isolation

**Context**: While executing the live transient `grafana/grafana:13.2.2` Docker runtime proof and promoting `store/grafana-oss/` into the bundled Browser Store (`Phase 6`).

**Symptom / Observation**: Adding a new Store project directory under `store/grafana-oss/` works in local MSBuild (`PublishConfiguratorBrowser`), yet `src/Configurator.Browser/Dockerfile.dockerignore` uses an explicit allowlist of Store subdirectories (`!store/islamu-event/...`, `!store/minimal-self-hosted/...`); omitting `!store/grafana-oss/...` would silently drop the new Store project during Docker image builds. Simultaneously, `grafana/grafana:13.2.2` is AGPLv3 and carries trademarked logos that must never enter an MIT repository.

**Root Cause**: Hardened container builds default-deny repository paths via `.dockerignore` allowlists, and clean-room target definitions must distinguish external runtime verification (transient pinned container) from bundled repository content (independently authored MIT YAML + omitted `iconDataUri`).

**Resolution**: Verified `.env` + Graphite `datasource.yaml` provisioning against transient pinned `grafana/grafana@sha256:ac461fb352abc50da10a51c7d02462e9c05488f11f53f14b3ad79a8145f638a0` (`/api/health` HTTP `200`, clean `docker rm -f`), promoted logo-free MIT `store/grafana-oss/` with bounded `target-runtime-verified` evidence, added `!store/grafana-oss/...` to `src/Configurator.Browser/Dockerfile.dockerignore`, and verified `BrowserStorePackageTests` (`2/2`), `BrowserDefinitionStoreTests` (`20/20`), and `DependencyNoticeTests` (`2/2`).

**Why This Matters for Future Work**: Whenever a new bundled project is added under `store/<slug>/`, update `src/Configurator.Browser/Dockerfile.dockerignore` in the same increment, omit third-party trademarked logos unless explicitly cleared under MIT/CC terms, and scope `conformance.claimBoundary` strictly to the vectors actually tested.

**References**:
- `store/grafana-oss/curation.json`
- `src/Configurator.Browser/Dockerfile.dockerignore`
- `tests/Configurator.Architecture.Tests/BrowserStorePackageTests.cs`
- [`docs/internal/DEFINITION_STORE.md`](../../../docs/internal/DEFINITION_STORE.md)
- [`docs/internal/OPERATIONS.md`](../../../docs/internal/OPERATIONS.md)

**Promotion Consideration**:
- [x] Candidate for skill update: `configurator-integrate` (Store promotion checklist)
