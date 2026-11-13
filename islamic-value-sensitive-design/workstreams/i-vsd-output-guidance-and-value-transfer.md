# Islamic Value-Sensitive Design Assessment: Output Guidance and Cross-Form Value Transfer

## Review Metadata

- **Assessment ID:** `IVSD-WORKSTREAM-OUTPUT-GUIDANCE-AND-VALUE-TRANSFER`
- **Mode:** Planning gate and integrated cross-client/schema responsibility review
- **Lifecycle state:** `current`
- **Plan alignment:** `plan-aligned`; implementation awaiting explicit authorization
- **Target workstream:** [`dev/active/output-guidance-and-value-transfer/`](../../dev/active/output-guidance-and-value-transfer/output-guidance-and-value-transfer-plan.md)
- **Evidence cutoff:** `2026-11-13` (repository working tree built atop Workstream 1 `definition-schema-redesign` runtime schema baseline)
- **Last Updated:** 2026-11-13 (literal metadata parity: Last Updated: 2026-11-13)
- **Affected layers:** `Configurator.Core`, `Configurator.Presentation`, `Configurator.Browser`, `Configurator.Desktop`, `Configurator.Terminal`, `Configurator.Cli`, `schemas/`, `examples/`, `store/`, `docs/`
- **Clean-room external references:** GOV.UK Task List pattern, W3C WCAG 2.2 AA (`SC 1.4.1`, `SC 2.4.7`, `SC 4.1.3`), Nielsen Norman Group Form Prefill / Copy-From heuristics, OpenAPI 3.1 `links` workflow separation, RFC 6901 JSON Pointer
- **Supersedes:** None
- **Superseded by:** None

---

## Executive Summary

When a self-hosting operator finishes exporting or copying one configuration artifact (most commonly the default environment variable output), multi-output software definitions such as `islamu-event` still leave related follow-up artifacts unconfigured (`configuration-manifest`, `operator-identity`, `tenant-package`, `legal-drafts`), while overlapping organizational fields exist across isolated output forms. Conversely, single-output software (such as `minimal-self-hosted`, `secret-generation`, and `grafana-13.2.2`) or simultaneous multi-artifact bundles finish in a single preparation step and must not confront operators with empty, synthetic, or coercive "next step" noise.

Applying Islamic Value-Sensitive Design (I-VSD) makes clear that **helpful workflow continuity must never cross into hidden state mutation, status-quo coercion, sensitivity downgrading, or browser custody leakage**. Specifically:

1. **Truthful post-export agency (`Sidq`, `Bayan`):** Show `"Proposed next action"` cues strictly after a verified export action completes AND only when active schema-admitted follow-up outputs exist under live workspace predicates (`proposedWhen`). Single-output and simultaneous-output software show zero next-action chrome.
2. **SSOT first, explicit opt-in transfer second (`Sidq`, `Ihsan`, anti-status-quo-bias):** Invariant shared facts belong once in a shared root schema node and project via `outputs.*.projection` `copy`. Only independently editable per-task fields use `x-configurator.suggestedFrom`; those links remain inert until the user previews exact non-empty candidates, reviews what will change (excluding manually authored values by default), and explicitly confirms transfer.
3. **Fail-closed sensitivity floor and browser secret custody (`Amanah`, `Tajassus` avoidance, `La Darar`):** No cross-form link may downgrade sensitivity (`source.sensitivity <= target.sensitivity` fails closed on `secret -> public`, `secret -> sensitive`, and `sensitive -> public`), no browser transfer may surface secret candidates without active per-workspace `BrowserNoSecrets` consent, and revocation or answer modification clears transient transfer/completion cues cleanly.
4. **Equitable cross-client and localization parity (`Adl`, `Ihsan`, `Haqq al-'Ibad`):** Browser, Avalonia Desktop, Terminal, and CLI share one headless Presentation tracker/transfer engine with full English (`en`) and Arabic RTL (`ar`) parity, closing existing Desktop/Terminal output-selector gaps while preserving historical immutable store releases (`store/islamu-event/releases/1.json`, `2.json`, `3.json`).

---

## Scope

### In scope
- **Post-export workflow continuity (`presentation.outputSelector.options[].nextActions[]`):** Conditional `proposedWhen` next-action declarations inside `outputSelector` options, transient session export completion & edit-staleness tracking inside `Configurator.Presentation`, and post-export `"Proposed next action"` UI across Browser, Desktop, and Terminal (plus CLI definition inspection/lint reporting).
- **Schema-declared cross-form value transfer (`x-configurator.suggestedFrom` + Guided Transition Transfer):** Strict data-only reference declarations supporting scalar/compatible fields with optional `when` predicates, acyclic graph admission, strict sensitivity-floor enforcement (`src.Sensitivity <= dst.Sensitivity`), per-field suggestion adoption, and opt-in transition transfer previews when moving to a proposed output task.
- **Canonical definition & dual-documentation parity:** Clean-slate `0.0.1` schema/code/client design across `Configurator.*`, regeneration of `schemas/configurator-authoring.schema.json` and `schemas/setup-definition.schema.json`, live adoption in `examples/islamu-event/configurator.json` and `store/islamu-event/`, and dual documentation updates (`docs/public/`, `docs/internal/`).

### Out of scope
- Persistent cross-session browser `localStorage`/`IndexedDB`/server storage of export history, answers, or prepared secrets.
- Cross-definition / multi-application SaaS state orchestration outside the currently open offline Configurator workspace.
- Silent auto-population of editable target answers, automatic navigation away from exported files without user click, or runtime target apply/deployment verification (target authority stays in the deployed software).
- Alterations to frozen historical store bundles `store/islamu-event/releases/1.json`, `2.json`, `3.json`, or `docs/internal/universal-configurator.md` / `docs/internal/go-to-market.md`.

### Non-goals
- Replacing single-source-of-truth (SSOT) shared projections (`outputs.*.projection` `copy`) with duplicate fields linked via `suggestedFrom`.
- Inventing arbitrary imperative scripting, Turing-complete workflows, or template string interpolation (`<`, `>`, `${`, `{{`) inside definition JSON.

---

## Claim Boundary

This assessment evaluates **provider and tool-authoring responsibility** within an offline configuration utility (`Configurator` `0.0.1`). It does not issue fiqh rulings, legal compliance advice (`legal-drafts`), cryptographic certification, or target-host acceptance guarantees. All safety claims below apply strictly to **offline schema admission, immutable in-memory workspace state transitions, local Browser/Desktop/Terminal/CLI custody boundaries, and clean-room UX patterns** defined in [`output-guidance-and-value-transfer-plan.md`](../../dev/active/output-guidance-and-value-transfer/output-guidance-and-value-transfer-plan.md). Real runtime behavior requires executing the verification suites in `tasks.md` after implementation.

---

## Findings

### IVSD-F001 — Dishonest or noisy post-export prompts erode agency and trap single-output or assistive-technology operators
- **Severity:** High
- **Principles affected:** `Sidq` (Truthfulness), `Bayan` (Clarity), `La Darar` (Harm avoidance — cognitive/assistive harm)
- **Failure mechanism:** Unconditional "Next step" banners on single-output env-only software (`minimal-self-hosted`, `secret-generation`, `grafana-13.2.2`) create phantom obligations ("finish step 2" when nothing remains). Conversely, showing a "Completed" check on a stale export whose backing answers were subsequently edited misleads operators into deploying stale files, while programmatic focus jumps away from the user's export controls hijack keyboard/screen-reader context (`WCAG 2.2 SC 4.1.3` / `SC 2.4.7`).
- **Repository anchors:** `src/Configurator.Browser/Components/Configuration/ConfigurationFilesPanel.razor`, `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.Publication.cs`, `src/Configurator.Desktop/Views/ArtifactPublicationView.axaml`, `src/Configurator.Desktop/ViewModels/ArtifactPublisherViewModel.cs`.

### IVSD-F002 — Silent cross-form prefilling or auto-copy violates user intent and spreads stale or unverified organizational claims
- **Severity:** High
- **Principles affected:** `Sidq` (Truthfulness), `Ihsan` (Craft & informed care), `Amanah` (Trustworthy stewardship of operator assertions)
- **Failure mechanism:** Automatically copying editable answers across distinct task trees (e.g., copying bootstrap operator values into `operator-identity`'s publishable document, or overwriting fields an operator intentionally customized, or hiding missing inputs behind HTML `placeholder` text) fabricates consent, breaks separation between infrastructure defaults and formal assertions, and violates [`CORE_DEFINITION_CONTRACT.md`](../../docs/internal/CORE_DEFINITION_CONTRACT.md)'s non-defaulting metadata invariant. Meanwhile, omitting `suggestedFrom` or clear SSOT guidance tempts authors to duplicate identical data across trees without linkage or transfer clarity.
- **Repository anchors:** `src/Configurator.Core/Definitions/DefinitionFieldPresentationMetadata.cs`, `src/Configurator.Presentation/Form/DefinitionFormModel.cs`, `examples/islamu-event/configurator.json`.

### IVSD-F003 — Cross-form value links without strict sensitivity monotonicity and consent-gating risk secret exfiltration and privacy downgrade
- **Severity:** Critical
- **Principles affected:** `Amanah` (Fiduciary custody), `Tajassus` avoidance (Non-intrusion / data minimization), `La Darar` (Security harm prevention)
- **Failure mechanism:** If a definition (or malformed contribution) points a `public` or `sensitive` target's `suggestedFrom` toward a `secret` source (e.g., an API token or signing key) or points a `public` target toward a `sensitive` source (`identity` PII), transfer preview or adoption downgrades classification and could leak restricted values into unredacted previews, `.env` comments, or lower-clearance artifacts. Similarly, retaining Browser secret-derived transfer previews after `BrowserNoSecrets` consent is revoked or persisting export histories across workspace resets violates the Browser Custody Boundary in [`AGENTS.md`](../../AGENTS.md) and [`.agents/rules/privacy-and-pii.md`](../../.agents/rules/privacy-and-pii.md).
- **Repository anchors:** `src/Configurator.Core/Definitions/DefinitionAdmission.cs`, `src/Configurator.Presentation/Secrets/DefinitionSessionPrivacy.cs`, `src/Configurator.Presentation/Form/DefinitionFormModel.cs`.

### IVSD-F004 — Unbounded or cyclic workflow and suggestion graphs invite ReDoS, infinite recursion, schema mismatch, or markup injection
- **Severity:** High
- **Principles affected:** `La Darar` (DoS/crash/corruption harm prevention), `Sidq` (Deterministic admission)
- **Failure mechanism:** Allowing cycles in `suggestedFrom` chains (`A -> B -> A`) or self/ancestor/descendant container references causes non-terminating evaluation or incoherent partial structures. Allowing incompatible leaf types (`string` -> `integer`, wider numeric bounds/unrestricted string into narrower `enum`/`minLength`/`maxLength`/`pattern`) produces guaranteed invalid target drafts. Allowing unvalidated free-form expressions or HTML/template tokens in `nextActions` titles/descriptions creates UI confusion or injection risk.
- **Repository anchors:** `src/Configurator.Core/Definitions/DefinitionAdmission.cs`, `src/Configurator.Core/Definitions/DefinitionSchema.cs`, `src/Configurator.Core/Definitions/Authoring/DefinitionPredicateLowering.cs`.

### IVSD-F005 — Client asymmetry (Desktop & Terminal lacking OutputSelector parity) and Arabic/English drift exclude non-Browser operators
- **Severity:** Medium-High
- **Principles affected:** `Adl` (Fairness / accessibility equivalence), `Ihsan` (Excellence across platforms), `Haqq al-'Ibad` (Rightful continuity of historical artifacts)
- **Failure mechanism:** Inspection shows `ConfigurationWorkspaceView.axaml` completely lacks an `OutputSelector` task control (and `ArtifactPublicationView.axaml` uses `SelectOutput` solely for folder picking), forcing Desktop operators to hunt raw root fields or miss multi-output paths such as `configuration-manifest` and `operator-identity` entirely. Treating Browser as the sole rich workflow client disenfranchises offline Linux/Desktop air-gapped and Terminal operators, while hardcoded English transition labels break Arabic (`ar`) RTL usability. Furthermore, breaking schema changes that retroactively invalidate immutable store releases `1.json`–`3.json` would breach store reproducibility (`BuildDefinitionStore.cs`).
- **Repository anchors:** `src/Configurator.Desktop/Views/ConfigurationWorkspaceView.axaml`, `src/Configurator.Desktop/Views/ArtifactPublicationView.axaml`, `src/Configurator.Terminal/Views/TerminalWindow.cs`, `store/islamu-event/releases/{1,2,3}.json`.

---

## Recommendations

### IVSD-M001 — Conditional, staleness-aware "Proposed next action" continuity (`Maps to IVSD-F001`)
- **Action:** Model `nextActions[]` explicitly on `DefinitionOutputOption` (`targetOption`, optional `title`, `description`, `proposedWhen`, `priority`) only inside multi-option `outputSelector` blocks. Track per-option export completions (`LastExportedInputRevision` vs live `Form.InputRevision`, clearing stale flags when answers change) inside `Configurator.Presentation`. Render the **"Proposed next action"** region strictly after a verified copy/download/save succeeds AND at least one candidate passes `proposedWhen` + export status filtering; keep single-output and simultaneous-output software 100% free of post-export next-action controls; announce completion in pre-rendered `role="status"` (`aria-live="polite"`) without stealing focus into the banner.
- **Target phases:** Phases 1, 2, 3, 4 | **Verification:** Core admission tests + Presentation state/staleness tests + Browser/Desktop/Terminal ARIA & non-appearance tests on single-output examples (`minimal-self-hosted`, `secret-generation`, `grafana-13.2.2`).

### IVSD-M002 — SSOT-first authoring rule plus inert `suggestedFrom` & opt-in Guided Transition Transfer (`Maps to IVSD-F002`)
- **Action:** Codify Authoring Rule #1 (**SSOT `projection copy` for single shared facts**) in documentation and linter advisories; restrict `x-configurator.suggestedFrom` (`{ "pointer": "/...", "when"?: Predicate }`) to independently editable sibling task subtrees. Keep `suggestedFrom` strictly inert (zero auto-fill). Expose two explicit user-initiated paths: (a) per-field *"Suggest from [Source Title]"* button/preview when source is active/present and non-empty, and (b) explicit opt-in checkboxes in a **Transfer matching settings** review inside the transition banner/confirmation step, excluding any target field where the user has already provided a non-default explicit answer unless manually checked. Keep transferred targets fully editable (`NN/g` rule).
- **Target phases:** Phases 1, 2, 3, 4 | **Verification:** Core lint/admission tests + Presentation no-auto-fill/no-overwrite tests + `islamu-event` golden artifact invariance test.

### IVSD-M003 — Compile-time sensitivity monotonicity + runtime custody revocation (`Maps to IVSD-F003`)
- **Action:** Enforce monotonic sensitivity floor in `DefinitionAdmission`: permit `suggestedFrom` ONLY when `source.Sensitivity <= target.Sensitivity` (`public <= sensitive <= secret`), rejecting `secret -> public`, `secret -> sensitive`, and `sensitive -> public` with deterministic Core diagnostic codes. In `Configurator.Presentation`, block evaluation/preview/transfer of `Sensitive`/`Secret` candidates unless active workspace privacy consent permits them (`BrowserNoSecrets` blocks `secret` suggestions even to `secret` targets, and blocks reading masked/withheld secret fields), scrub/clear `ExportedTaskReceipts` and pending transfer caches synchronously on `ClearWorkspace`, consent revocation, workspace replacement, or disposal, and ensure zero diagnostic/URL/telemetry leakage of suggestion values or pointers containing user answers.
- **Target phases:** Phases 1, 2, 3 | **Verification:** Pre-fix failing Red tests for sensitivity downgrade (`secret->public`, `sensitive->public`, `secret->sensitive`) + `BrowserNoSecrets` consent gating + revocation wipe + secret redaction checks.

### IVSD-M004 — Closed admission grammar, acyclic DAG validation, schema compatibility & bounded predicates (`Maps to IVSD-F004`)
- **Action:** Keep `x-configurator` and `outputSelector` key schemas closed (`AdditionalProperties = false` in `DefinitionSchema.cs`). Validate `nextActions`: `targetOption` exists in `outputSelector.options`, is not self (`targetOption != currentOption`), has no dead-end cycle ambiguity (warn on symmetric loop or bound priority ordering), and compiles `proposedWhen` via existing bounded `DefinitionPredicateLowering`. Validate `suggestedFrom`: source pointer resolves to an admitted scalar/leaf value, no self/ancestor/descendant overlap, acyclic `suggestedFrom` graph (`suggestion-cycle` rejection), exact structural/type/constraint compatibility (`suggestion-incompatible`), and existing markup/placeholder safety rules.
- **Target phases:** Phase 1 | **Verification:** `DefinitionAdmissionTests`, `DefinitionAuthoringTests`, `DefinitionSchemaGenerator --check`, `CoreArchitectureTests` (no mutable collections, no ambient calls, no public `Message` members, no `yield`).

### IVSD-M005 — Unified cross-client UX (`Desktop`, `Browser`, `Terminal`, `Cli`), `en`/`ar` RTL parity, & optional runtime evolution (`Maps to IVSD-F005`)
- **Action:** Surface first-class `OutputSelector` + post-export `ProposedNextActions` + explicit transfer controls across **Desktop** (`ConfigurationWorkspaceView.axaml`, `ArtifactPublicationView.axaml`, `ArtifactPublisherViewModel.cs`), **Browser** (`ConfigurationFilesPanel.razor`, `ConfigurationFieldActions.razor`, `ConfigurationEditor*.cs`), **Terminal** (`TerminalWindow.cs`, `.Publication.cs`, `.Editing.cs`), and **`Cli`** (`definition inspect`, `definition lint --quality`). Author full English and Arabic (`en`/`ar`) localization across `PresentationStrings`, `BrowserStrings`, `DesktopStrings`, `TerminalStrings`, plus `examples/islamu-event/configurator.json`. Keep new JSON keys optional on runtime `DefinitionSchema` (activating `"presentation.next-actions"` and `"values.suggested-from"` capabilities only when present) so historical immutable `store/islamu-event/releases/{1,2,3}.json` continue passing `BuildDefinitionStore.cs` untouched.
- **Target phases:** Phases 1, 2, 3, 4 | **Verification:** Full `Browser.Tests`, `Desktop.Tests`, `Terminal.Tests`, `Cli.Tests`, `BuildDefinitionStore.cs`, `ValidateDefinitionDocumentation.cs`, `Core.Tests`, `Presentation.Tests`, `Architecture.Tests`.

---

## Stakeholders

| Stakeholder group | Needs & expectations | Harm avoided by mitigations |
|---|---|---|
| **Single-output self-hosters** | Clean completion without phantom multi-step prompts (`minimal-self-hosted`, `grafana-13.2.2`) | Eliminates false incomplete anxiety and accidental workspace navigation (`IVSD-M001`) |
| **Multi-output operators** (e.g. `islamu-event`) | Clear conditional next-step path (`environment` -> `configuration-manifest` / `operator-identity` / `tenant-package`) + optional reuse of verified identity inputs | Eliminates forgotten companion artifacts and tedious error-prone manual re-entry (`IVSD-M001`, `IVSD-M002`) |
| **Privacy-conscious Browser operators** | Shared-device custody protection; zero unconsented secret leakage or persistence | Guarantees sensitivity floor, active consent gates, and instant wipe on revocation (`IVSD-M003`) |
| **Keyboard, screen-reader & Arabic RTL operators** | Non-disruptive `role="status"` announcements, logical focus flow, full Arabic translations across all clients | Avoids WCAG focus theft, untranslated English labels, and Desktop second-class exclusion (`IVSD-M001`, `IVSD-M005`) |
| **Definition authors & maintainers** | Clear SSOT vs transfer authoring rules, compile-time link verification, historical compatibility | Prevents subtle runtime schema mismatches, cycles, or store release breakage (`IVSD-M002`, `IVSD-M004`, `IVSD-M005`) |

---

## I-VSD Principles And Domains

| I-VSD Principle / Domain | Operationalization in this workstream | Verified design invariant |
|---|---|---|
| **Sidq (Truthfulness & Honesty)** | Next-action cards appear only when real active tasks remain; stale exports re-flag when inputs change; transfers are never spoofed via `placeholder` or silent auto-fill. | Export completion compares live `Form.InputRevision` vs exported revision (`IVSD-M001`, `IVSD-M002`). |
| **Amanah (Fiduciary Stewardship & Custody)** | Source-to-target value links never downgrade confidentiality; secret transfers cannot execute without active per-workspace consent and clear on revocation. | `src.Sensitivity <= dst.Sensitivity` verified at schema admission + runtime consent checks (`IVSD-M003`). |
| **Bayan (Clarity & Intelligibility)** | Next-action titles, transfer source labels, field count previews, and skip explanations clearly state *what* moves *where* and *why* before any value is written. | Explicit preview list showing `field -> target`, skipping user-edited targets unless selected (`IVSD-M002`). |
| **La Darar (Prevention of Harm)** | Closed schemas, bounded DAGs (`suggestion-cycle`), leaf type/bounds parity, `proposedWhen` reuse of bounded AST, and WCAG focus preservation prevent crashes/injections/confusion. | Fail-closed `DefinitionAdmission` checks with zero executable scripts or RegEx expansion (`IVSD-M004`). |
| **Adl & Ihsan (Justice, Parity & Craftsmanship)** | Desktop, Browser, Terminal, and CLI share one truthful engine; English & Arabic (`ar` RTL) receive parity; SSOT keeps clean architecture with minimal maintenance debt. | Shared headless `Configurator.Presentation` projections + complete `.resx` parity across all clients (`IVSD-M005`). |

---

## Common Overlooked Failures And Outcomes

1. **Silent overwrite of deliberate target edits:** When switching from `environment` to `operator-identity` twice, naive auto-copy overwrites customized `legalName` in `operatorIdentity`. **Outcome prevented:** transfer skips any target field with an existing explicit user value by default, listing skipped conflicts transparently in the preview UI.
2. **Stale "Exported ✓" badge after subsequent edit:** Operator exports `.env`, toggles `hosting.configurationManifest.mode` to `Bootstrap` or changes `operatorId`, yet `.env` still reads "Exported". **Outcome prevented:** per-option `IsExportCurrent` automatically invalidates to stale/needs-re-export whenever `InputRevision` advances past `ExportedInputRevision`.
3. **Browser "Copy failed / cancelled" still promoting Next Action:** Calling `ClipboardUnavailableStatus` (`copied == false` or `exposed == false`) yet triggering `"Proposed next action"`. **Outcome prevented:** completion settles ONLY on verified `copied == true`, `downloaded == true`, or settled Desktop/Terminal `ProtectedArtifactPublicationReceipt` success.
4. **Cross-task sensitivity laundering (`public <= sensitive <= secret` violation):** Linking a non-secret public target field to a `sensitive` or `secret` field lets unredacted previews expose protected content. **Outcome prevented:** rejected at compile/admission time (`suggestion-sensitivity-downgrade`).
5. **Breaking Desktop folder picker (`Name="SelectOutput"`):** Reusing the folder-picker control name in `ArtifactPublicationView.axaml` breaks existing Desktop tests. **Outcome prevented:** Desktop task switcher uses distinct controls (`Name="OutputSelector"` / `Name="ProposedNextActions"`), preserving `Name="SelectOutput"` (`SelectOutputDirectoryCommand`).

---

## Validation Gaps

Until Phases 1–4 are executed and verified on disk:
- C# unit/invariant coverage for `DefinitionAdmission` sensitivity-floor rejection, `suggestion-cycle` DAG detection, and `DefinitionOutputNextAction` localization admission does not yet exist (`Phase 1` Red -> Green).
- Presentation revision-staleness (`IsExportCurrent`), bulk-transfer non-overwrite guarantees, and `BrowserNoSecrets` revocation clearing are unimplemented (`Phase 2`).
- Cross-client UI automation tests verifying Playwright/bUnit WCAG status announcements, Desktop `OutputSelector` + post-save proposals, Terminal shortcuts, and non-appearance on single-output examples (`minimal-self-hosted`, `secret-generation`, `grafana-13.2.2`) remain pending (`Phase 3` & `Phase 4`).

---

## Escalation Needed

None blocking plan approval. Two explicit execution-start coordination checks are embedded directly in `tasks.md` Prerequisites (`PRE-1`, `PRE-2`):
1. Confirm baseline landing status of Workstream 1 (`definition-schema-redesign`, currently uncommitted in the working tree) and whether `store/islamu-event/releases/4.json` should be updated in-place before publishing or incremented to `5.json`.
2. Confirm Tier 2 `Create-Change` ULID changelog fragment allocation only if any breaking public contract adjustment (`outputSelector` behavior or `schemas/*.schema.json` bump beyond additive optional fields) requires a release entry before commit.

---

## Evidence Reviewed

- [`AGENTS.md`](../../AGENTS.md) and [`.agents/contract/intents.yaml`](../../.agents/contract/intents.yaml) — critical invariants (Independent Runtime, Data-Only Definitions, Browser Custody Boundary, Protected Output, Dual Documentation Parity).
- [`.agents/rules/privacy-and-pii.md`](../../.agents/rules/privacy-and-pii.md) and [`.agents/rules/blazor-client.md`](../../.agents/rules/blazor-client.md) — consent-gated secret entry, zero answer persistence/telemetry, `IAsyncDisposable` lifecycle rules.
- [`docs/internal/CORE_DEFINITION_CONTRACT.md`](../../docs/internal/CORE_DEFINITION_CONTRACT.md) and [`docs/internal/PRESENTATION_STATE.md`](../../docs/internal/PRESENTATION_STATE.md) — non-defaulting metadata invariant, `DefinitionFormModel` snapshot isolation.
- Live source inspections: `DefinitionSchema.cs`, `DefinitionAdmission.cs`, `DefinitionPresentation.cs`, `DefinitionOutputSelector.cs`, `DefinitionOutputOption.cs`, `DefinitionFieldPresentationMetadata.cs`, `DefinitionProfile.cs`, `Authoring/*`, `DefinitionFormModel.cs`, `DefinitionFieldValidation.cs`, `ConfigurationFilesPanel.razor`, `ConfigurationConfigurePanel.razor`, `ConfigurationEditor.Publication.cs`, `ArtifactPublisherViewModel.cs`, `ConfigurationWorkspaceView.axaml`, `ArtifactPublicationView.axaml`, `TerminalWindow.cs`, `examples/islamu-event/configurator.json`, `CoreArchitectureTests.cs`.

---

## Missing Evidence

- Live runtime execution traces (zero product code has been modified in this planning workstream).
- Downstream operator usability telemetry (out of scope by design; Browser telemetry is strictly forbidden by `AGENTS.md` §5 Rule 4).

---

## Context Inventory

- Workstream Triad:
  - [`output-guidance-and-value-transfer-plan.md`](../../dev/active/output-guidance-and-value-transfer/output-guidance-and-value-transfer-plan.md)
  - [`output-guidance-and-value-transfer-tasks.md`](../../dev/active/output-guidance-and-value-transfer/output-guidance-and-value-transfer-tasks.md)
  - [`output-guidance-and-value-transfer-context.md`](../../dev/active/output-guidance-and-value-transfer/output-guidance-and-value-transfer-context.md)
- Related active workstream coordination:
  - `dev/active/definition-schema-redesign/` (Workstream 1 — live uncommitted working-tree prerequisite baseline)
  - `dev/active/configuration-workspace-redesign/` (Workstream 2 — queued workspace shell redesign; interfaces aligned cleanly via headless Presentation models)

---

## Review Lifecycle

- **Created:** `2026-11-13` (Planning intake & architectural design review)
- **Status transition rule:** Remains `current` / `plan-aligned` while awaiting implementation. Any post-planning change to sensitivity rules, `suggestedFrom` semantics, Browser consent handling, or `nextActions` custody guarantees MUST trigger an updated I-VSD revalidation before PR merge.

---

## Planning Handoff

| Finding / Mitigation ID | Plan Requirement / Scenario | Implementing Phase | Verification Gate / Assertion |
|---|---|---|---|
| `IVSD-F001` / `IVSD-M001` | `R01`, `R02`, `R03`, `R09`, `R10` (`S01`, `S02`, `S03`, `S04`, `S11`, `S12`) | Phase 1, Phase 2, Phase 3, Phase 4 | No next-action banner for env-only defs; conditional `proposedWhen` matches live form state; export staleness flips `IsExportCurrent`; `role="status"` announces without stealing focus |
| `IVSD-F002` / `IVSD-M002` | `R04`, `R05`, `R06` (`S05`, `S06`, `S07`, `S08`) | Phase 1, Phase 2, Phase 3, Phase 4 | `suggestedFrom` never auto-populates; bulk transfer skips manually authored targets unless selected; transferred fields remain editable; SSOT rule enforced; golden `islamu-event` outputs unchanged |
| `IVSD-F003` / `IVSD-M003` | `R07`, `R08` (`S09`, `S10`) | Phase 1, Phase 2, Phase 3 | Pre-fix Red test fails on `secret->public`/`sensitive->public`/`secret->sensitive`; `BrowserNoSecrets` blocks secret suggestions; revocation/clear/dispose scrubs export & transfer state |
| `IVSD-F004` / `IVSD-M004` | `R04`, `R05`, `R08` (`S13`, `S14`) | Phase 1, Phase 4 | Closed JSON schema (`DefinitionSchemaGenerator --check`); `suggestion-cycle` and `suggestion-incompatible` fail closed; `CoreArchitectureTests` passes without mutable collections/ambient calls/`Message` members |
| `IVSD-F005` / `IVSD-M005` | `R09`, `R11`, `R12` (`S11`, `S12`, `S14`) | Phase 2, Phase 3, Phase 4 | Full Desktop `OutputSelector` + `ProposedNextActions`; Terminal parity; `en`/`ar` RTL parity; `BuildDefinitionStore.cs` preserves historical `releases/1..3.json` |
