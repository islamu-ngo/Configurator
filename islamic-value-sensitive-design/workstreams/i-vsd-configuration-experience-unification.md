# Provider-Responsibility Assessment: Configuration Experience Unification Across Browser, Desktop, Terminal, and Authoring Quality

> Configurator authority: follow [AGENTS.md](../../AGENTS.md) and [PROJECTS.md](../../PROJECTS.md). Source-system backend examples remain reference-only; absent Event runtime layers and deployment policies do not apply.

Last Updated: 2026-11-16

## Review Metadata
- Mode: planning
- Subject: Unified single-workspace configuration experience, DOM-bounded continuous rendering, keyboard/screen-reader accessibility, Desktop/Terminal parity, and Event revision 7 quality guidance
- Workstream: configuration-experience-unification
- Report kind: planning-assessment
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-11-16
- Reviewed input: Completed triad `dev/active/configuration-experience-unification/` (`plan.md` SHA-256 `be7c3b13fd7e3f89467691332fe48e0f9908b2fcdb3d4ac154b2d34064fc2557`, `context.md` SHA-256 `25a665025c76d46a7220d15c7e5f1b5d86ea41333f637224c9da3562821a2ebe`, `tasks.md` SHA-256 `a99634a4ff6ecd333f89a9548f311c6d24629f4e0a06482118c89b0aecf7a8da`) + [coverage-gaps report](../../dev/report/configuration-experience-plan-coverage-gaps-2026-11-15.md) (`96cc5d5912060c4058222894c0a05ba35e5928e892c81b9d5dd39ddd45e0bfe`) over working-tree HEAD `8f3f4c3cca94d54e48ec661f6d7071bdeb334f53`
- Supersedes: [Compact-only configuration experience assessment](i-vsd-configuration-experience.md)

## Executive Summary

**Practical recommendation:** Proceed with planning the greenfield `configuration-experience-unification` redesign to unify Configurator's split Browser modes into a single continuous-or-step workspace with DOM-bounded semantic rendering, fix blocking accessibility and readiness honesty bugs, bring Desktop and Terminal to first-class parity, and resolve all `143` authoring quality lint warnings in the maintained ISLAMU Event definition (`revision 7`). Every optimization, virtualization window, search palette, and lint expansion **must** keep transient browser secret custody inside `DefinitionFormModel`, guarantee zero misleading "100% Ready" claims when optional settings fail validation, preserve equitable English/Arabic RTL and keyboard access, and keep all interaction telemetry offline and unpersisted.

**Who is affected and why this matters:** Self-hosting administrators and non-technical community stewards configuring Event or other tools in Browser, Desktop, and Terminal currently face a split Browser mental model (`Guided` vs `Compact`), a `2.15 s` / `67.4 ms`-p95 frame-budget bottleneck when scanning 600 settings in Compact AOT, a blank screen on zero search matches, keyboard jump shortcuts (`F7`/`F8`/`F9`) that focus headings instead of inputs, misleading 100% readiness bars despite invalid optional settings, secondary table/button walls in Desktop and Terminal, and `143` undocumented required settings or enum choices in the reference Event definition. Left unaddressed on lower-end devices or with assistive technology, slow UI freezes and confusing guidance push operators toward editing fragile raw `.env` files by hand or misconfiguring TLS/administrator credentials.

**Central mechanism and confidence limit:** Architectural safety rests on holding one authoritative `DefinitionFormModel` in memory (`Presentation`) while rendering lightweight stateless view windows in Browser (`<= 1,400` active DOM nodes via section-anchored hydration + semantic HTML5 controls), virtualized `SplitView` lists in Desktop, and an `80×24` Master-Detail `StatusBar` workbench in Terminal. Following full revalidation against the completed `configuration-experience-unification` triad (recorded hashes under Reviewed input), this assessment is `current` and `plan-aligned`: every open finding (`IVSD-F001`–`IVSD-F008`) maps to concrete Section 3 scenarios and invariant-first `Red -> Green` tasks across Phases 1–8. `plan-aligned` status verifies planning coverage, not implemented runtime behavior or steward physical assistive-technology sign-off.

## Scope

In scope for this planning review:
- Unifying `Configurator.Presentation` workspace view/layout contracts (cleanly replacing mixed stage/layout enums and `CompactPresentation` forks in `0.0.1` pre-release) and fixing expansion, focus target resolution, and readiness summary calculations.
- Redesigning `Configurator.Browser` into one persistent left-rail workspace (step selector or AWS Cloudscape-style `IntersectionObserver` scrollspy TOC), DOM-bounded semantic row rendering (`< 1,400` active DOM nodes, `< 50 ms` 1x frame tasks), GOV.UK/Cloudscape `ErrorSummary`, WAI-ARIA 1.2 `role="combobox"` search palette, minority-only field labeling, and progressive Catalogue/Open/Vault flows.
- Upgrading `Configurator.Desktop` (`SplitView` + virtualized containers + unified field styling) and `Configurator.Terminal` (`80×24` master-detail + 5-key `StatusBar` + scrollable help + dual Vim/CUA list navigation) to idiomatic native parity.
- Expanding `DefinitionQualityLinter`, adding opt-in CI strictness (`definition lint --quality --strict`), resolving all `143` Event warnings in `revision 7` (`examples/islamu-event/` and `store/islamu-event/`), and updating paired public/internal docs.

Out of scope:
- Any server-side session storage, user accounts, SaaS hosted Store curation rules, live target deployment/apply transport, or telemetry endpoints.

## Claim Boundary

This assessment evaluates **provider-controlled software design responsibilities** under Sunni Islamic ethical principles (Amanah / trustworthy stewardship, Sidq / truthfulness, 'Adl / equitable justice, La darar / non-harm, Ihsan / clarity and care). It is **not** a religious-legal ruling (fatwa), a Sharia-compliance certificate, or runtime/assistive-technology certification. Because Configurator runs 100% offline with zero network analytics or live target apply, provider responsibility centers on truthful readiness communication, local secret custody, fair accessibility across languages/disabilities/hardware, and clear non-deceptive authoring guidance.

## Common Overlooked Failures And Outcomes

1. **Performance virtualization silently dropping unparsed user edits or surviving secret revocation:** If virtualized Browser rows or Avalonia recycled containers store temporary input drafts or revealed secrets inside view components, scrolling off-screen either discards a user's half-typed hostname/password or leaves secret characters cached in component memory after `BrowserNoSecrets` consent revocation.
2. **"100% Ready" green progress masking broken optional integrations:** Showing `100%` or a full progress meter when `0` required fields remain missing—while an optional webhook URL, SMTP relay, or S3 setting has a validation error—misleads operators into exporting broken artifacts and discovering failures only after deployment.
3. **Banner blindness and silent filter lockouts:** Repeating `"Optional"` and `"Not set"` pills on 95% of healthy rows trains operators to skip reading field tags altogether, while filtering to `"Show matches only"` on a term with zero visible matches blanks the workspace with no recovery control.
4. **Assistive-technology and keyboard traps under continuous scroll:** Using `aria-live` impulsively on every filter keystroke floods screen readers; focusing `<h2 tabindex="-1">` on `F7`/`F8` leaves keyboard users stranded two tabs away from the broken input; omitting `<bdi dir="ltr">` in Arabic combobox rows scrambles hyphenated wire tokens and JSON Pointers.

## Findings

| Finding ID | Title | Lifecycle | Severity | Claim type | Mitigation ID |
|---|---|---|---|---|---|
| `IVSD-F001` | Transient Secret And Draft Custody Across Virtualized/Windowed Rows | Open | Critical | Security & custody design | `IVSD-M001` |
| `IVSD-F002` | Truthful Readiness, Diagnostic Progress, And Explicit Section Review | Open | High | Truthfulness & UX gap | `IVSD-M002` |
| `IVSD-F003` | Equitable Keyboard, Screen-Reader, Arabic RTL, And Zero-Match Accessibility | Open | High | Accessibility & justice gap | `IVSD-M003` |
| `IVSD-F004` | Inclusive Performance Under Throttled Hardware (`<= 1,400` Nodes, `< 50 ms`) | Open | High | Performance & exclusion risk | `IVSD-M004` |
| `IVSD-F005` | Non-Deceptive Field Labeling And Complete Event Revision 7 Authoring Guidance | Open | High | Authoring & clarity gap | `IVSD-M005` |
| `IVSD-F006` | Mode/Surface Parity Without Accidental Work Loss Across Browser, Desktop, Terminal | Open | Medium | Usability & harm prevention | `IVSD-M006` |
| `IVSD-F007` | Zero Surveillance Or Answer Leakage In Scrollspy, Search Palette, And Benchmarks | Open | Critical | Privacy boundary | `IVSD-M007` |
| `IVSD-F008` | Explicit Evidence Boundary Between Automated Checks And Human/OS Qualification | Open | Medium | Governance & candor | `IVSD-M008` |

### IVSD-F001: Transient Secret And Draft Custody Across Virtualized/Windowed Rows

**Concrete situation:** Removing heavyweight per-row DOM wrappers and adding section/list virtualization means UI row components continuously unmount and remount during scrolling, filtering, and section switching across Browser and Desktop. If a user enters an unparsed numeric draft or partial secret in a scalar row and scrolls past it, losing that draft causes silent data corruption; conversely, if a virtualized component retains secret characters in local DOM attributes or component state after the operator disables secret entry (`BrowserNoSecrets`), sensitive credentials leak across the workspace lifecycle.

**Provider-controlled choice:** Configurator maintainers control where draft strings, reveal flags, and secret custody live. Users expect scroll and view switches to be lossless visual viewport operations, not destructive edits.

**Mechanism:** `DefinitionFormModel` must remain the sole synchronous in-memory custodian of accepted answers, unparsed raw drafts (`_editing.RootInput.DraftTexts`), and `DefinitionSessionPrivacy` across all clients. Row components (`@key` keyed by stable `EditorIdentity`) read initial state from the form on mount, forward edits under `(Form, Path, InputRevision, ControlGeneration)` fencing, maintain zero independent answer copies across unmounts, and clear transient DOM input values synchronously on disposal or secret-consent revocation.

**Ethical reasoning (`Amanah` / Trustworthy Stewardship & `La darar` / Non-harm):** Entrusting credentials and partial setup work to a local offline utility obligates the tool never to lose entered work silently on scroll nor retain revoked secrets behind the user's back.

**Recommendation, trade-offs, and evidence limits:** Enforce component statelessness across unmounts (`IVSD-M001`) rather than caching component instances offscreen (`display: none`), because offscreen caching defeats the `< 1,400` DOM node budget. Source inspection confirms `DefinitionFormModel` already tracks drafts in C#, while Browser row unmount/remount and revocation under virtualization require adversarial Red-phase bUnit tests before shipping.

| Traceability field | Value |
|---|---|
| Finding / lifecycle / severity | `IVSD-F001` / open / critical |
| Claim type | Security & custody design constraint |
| Principles / domains | `Amanah`, Non-harm / Privacy, Custody, Technical architecture |
| Stakeholders | Self-hosting administrators, security operators |
| Controlled decision | Single-source state ownership across virtualized UI containers |
| Evidence & level | Source inspection (`DefinitionFormModel.cs`, `ConfigurationSectionFields.razor.cs`); planning-time invariant |
| Mitigation | `IVSD-M001`: Stateless `@key` rows, generation fencing, synchronous unmount/revocation buffer clearing |
| Owner / next validation | Presentation & Browser maintainers; adversarial Red/Green bUnit scroll-and-revoke tests |
| Escalation | None (engineering custody invariant) |

### IVSD-F002: Truthful Readiness, Diagnostic Progress, And Explicit Section Review

**Concrete situation:** Current Header progress (`ConfigurationEditor.razor` and `WorkspaceProgressSummary`) can render a 100% progress bar and green visual tone even when optional fields hold validation errors, and `SetSectionReviewed` in `DefinitionFormModel` has no UI caller while section status badges conflate `"Opened"` with reviewed readiness.

**Provider-controlled choice:** Maintainers define readiness math, progress bar fill semantics, status labels, and whether optional blocking errors halt export or remain clearly flagged before artifact generation.

**Mechanism:** Update `WorkspaceProgressSummary` to report exact required readiness alongside `OptionalInvalidCount` and `IsReadyForExport` (`MissingRequired == 0 && DeferredSecretCount == 0 && TotalInvalidCount == 0`), disallow `100%` / `"Ready"` styling whenever `TotalInvalidCount > 0`, expose an explicit `Mark section reviewed` toggle wired to `SetSectionReviewed`, clear `HasNewlyRelevantSettings` cleanly when the operator inspects newly revealed settings, and retain explicit incomplete-draft labeling for secret-deferred exports.

**Ethical reasoning (`Sidq` / Truthfulness & `Amanah`):** Claiming `"100% Ready"` while known validation errors remain in supplied optional fields misleads an operator and undermines trust in pre-deployment validation.

**Recommendation, trade-offs, and evidence limits:** Always distinguish `"All required set (N issues in optional settings)"` from `"Ready"` (`IVSD-M002`). Rejected alternative: keeping binary required-only green meters while hiding optional errors behind a collapsible drawer. Verified against live `SectionStatusProjection.cs` (`ConfigurationEditor.razor` currently computes percentage solely from required counts).

| Traceability field | Value |
|---|---|
| Finding / lifecycle / severity | `IVSD-F002` / open / high |
| Claim type | Truthfulness & readiness accuracy gap |
| Principles / domains | `Sidq`, `Amanah` / UX, Validation truthfulness |
| Stakeholders | Operators, reviewers checking export readiness |
| Controlled decision | Readiness projection & visual indicator semantics |
| Evidence & level | Source inspection (`SectionStatusProjection.cs`, `ConfigurationEditor.razor`, gap report §6/§21) |
| Mitigation | `IVSD-M002`: Separate `RequiredReady` from `TotalInvalidCount`/`IsReadyForExport` + explicit `SetSectionReviewed` UI |
| Owner / next validation | Presentation, Browser, Desktop maintainers; Presentation/Browser/Desktop tests |
| Escalation | None |

### IVSD-F003: Equitable Keyboard, Screen-Reader, Arabic RTL, And Zero-Match Accessibility

**Concrete situation:** Operators relying on keyboard-only navigation, screen readers, Arabic RTL localization, or `320 px` viewports face four concrete barriers: (1) filtering to zero matches in Compact renders a blank area with no explanation or clear-filter CTA (`ConfigurationCompactWorkspace.razor`), (2) `F7`/`F8`/`F9` jumps focus section `<h2 tabindex="-1">` headings instead of the offending control, (3) `WorkspaceSearch` uses custom buttons rather than a WAI-ARIA 1.2 `role="combobox"` with `aria-activedescendant`, and (4) Arabic technical wire tokens lack consistent `<bdi dir="ltr">` isolation in search results while mobile inspectors push primary configuration offscreen.

**Provider-controlled choice:** Maintainers control ARIA roles, focus management targets, empty-state recovery controls, RTL bidirectional markup (`<bdi>`), and responsive container layout breakpoints.

**Mechanism:** Resolve exact field coordinates (`DefinitionPath`) in `ResolveNextActionableTarget` and scroll/hydrate the target window before focusing the interactive control; implement `ErrorSummary` above fields with direct jump links; upgrade `WorkspaceSearch` to WAI-ARIA `role="combobox"` + `ArrowUp`/`ArrowDown`/`Enter`/`Escape` + `<bdi dir="ltr">`; add explicit zero-match filter diagnosis and reset CTA; convert narrow-viewport inspector to a modal/sheet drawer.

**Ethical reasoning (`'Adl` / Equitable Justice & `Ihsan` / Craft Excellence):** Software intended for diverse global Muslim communities and self-hosters must not exclude blind, motor-impaired, Arabic-speaking, or mobile operators through missing focus targets or bidirectional text corruption.

**Recommendation, trade-offs, and evidence limits:** Build ARIA contracts and `bdi` isolation directly into core UI primitives (`IVSD-M003`). Note an explicit validation boundary: bUnit and Desktop Headless tests verify rendered attributes, focus dispatch calls, and logical CSS (`inset-inline-*`), whereas actual speech synthesis, Braille output, and physical screen-reader workflows remain an open human/AT qualification gate.

| Traceability field | Value |
|---|---|
| Finding / lifecycle / severity | `IVSD-F003` / open / high |
| Claim type | Accessibility & linguistic equity gap |
| Principles / domains | `'Adl`, `Ihsan` / Accessibility, Localization, Responsive UX |
| Stakeholders | Disabled operators, keyboard power users, Arabic RTL users, mobile users |
| Controlled decision | Focus routing, WAI-ARIA combobox contract, RTL isolation, responsive sheet layout |
| Evidence & level | Source inspection (`WorkspaceSearch.razor`, `ConfigurationEditor.razor.cs`, `ConfigurationCompactWorkspace.razor`, gap report `G06`/`G07`/`G08`/`G11`) |
| Mitigation | `IVSD-M003`: Jump-to-control focus, `ErrorSummary`, `role="combobox"` search, empty-state CTA, `<bdi>`, mobile sheet |
| Owner / next validation | Presentation, Browser, Desktop, Terminal maintainers; bUnit/NativeHost EN+AR tests; physical AT remains open gate |
| Escalation | Steward human/AT testing before WCAG conformance claims |

### IVSD-F004: Inclusive Performance Under Throttled Hardware (`<= 1,400` Nodes, `< 50 ms`)

**Concrete situation:** Live AOT measurements on `600` settings (`OPERATIONS.md` `2026-11-16`, candidate `c489b3e1`) failed the warmed `1x` frame-task gate (`67.4 ms` p95, `13/40` samples `> 50 ms`, `2154.5 ms` complete mount p95) because 600 heavyweight MudBlazor field subtrees (~18,000 DOM nodes) mount at once and `hidden="true" inert="true"` retains filtered/collapsed nodes in the live tree.

**Provider-controlled choice:** Maintainers choose between heavy library wrappers per input row versus lightweight native semantic controls inside bounded viewport windows (`<= 1,400` active DOM nodes).

**Mechanism:** Replace simultaneous 600-subtree MudBlazor field wrapping in continuous mode with lightweight semantic HTML5 row controls (`<input>`, `<select>`, `<button role="switch">`) inside section-anchored `<Virtualize>` / `IntersectionObserver` hydration windows (`max <= 1,400` DOM nodes), removing non-matching items cleanly instead of `hidden`+`inert`, and reserving status pill vertical space (`min-block-size`) to eliminate first-edit CLS.

**Ethical reasoning (`'Adl` & `La darar` / Non-harm):** Heavy DOM bloat locks out operators using modest community laptops or budget phones under throttled CPU, driving them to unsafe shortcuts outside Configurator.

**Recommendation, trade-offs, and evidence limits:** Adopt semantic HTML5 row controls + viewport section windowing (`IVSD-M004`), retiring `CompactPresentation` dual markup cleanly (`0.0.1` breaking freedom). Rejected alternative: relaxing the `50 ms` `MeasureInteractiveWork` budget. Re-qualification requires freshly publishing Browser AOT and validating full `compact-101` and `compact-600` cohorts in `MeasureInteractiveWork.cs`.

| Traceability field | Value |
|---|---|
| Finding / lifecycle / severity | `IVSD-F004` / open / high |
| Claim type | Performance & hardware-inclusion defect |
| Principles / domains | `'Adl`, Non-harm / Browser AOT responsiveness, INP |
| Stakeholders | Operators on constrained laptops/phones, large-schema deployers |
| Controlled decision | Lightweight semantic controls & DOM-bounded section/list virtualization |
| Evidence & level | Recorded AOT measurements (`OPERATIONS.md`, `c489b3e1...`, `67.4 ms` p95 `13/40 > 50 ms`) |
| Mitigation | `IVSD-M004`: DOM budget `<= 1,400` nodes, `0` hidden-inert bloat, CLS elimination, AOT `MeasureInteractiveWork` gate |
| Owner / next validation | Browser maintainers; AOT `PublishConfiguratorBrowser` + `MeasureInteractiveWork` `1x`/`4x` passing cohorts |
| Escalation | None |

### IVSD-F005: Non-Deceptive Field Labeling And Complete Event Revision 7 Authoring Guidance

**Concrete situation:** Two opposite clarity failures compound operator errors: visually, the current Browser UI repeats `"Optional"` and `"Not set"` pills on almost every clean row (`ConfigurationField.razor`), drowning out truly required fields; semantically, running `definition lint --source examples/islamu-event/configurator.json --quality` produces `143` warnings (`required-setting-missing-guidance` and `choice-missing-guidance`) including core security settings like `bootstrapAdministrator.provider`, `bootstrapAdministrator.subject`, `bootstrapAdministrator.bindingGeneration`, `database.provider`, and `database.erasure.tlsMode`.

**Provider-controlled choice:** Maintainers control both default chrome density (highlighting exceptions via minority badge + VS Code-style left accent stripe rather than labeling defaults repeatedly) and the completeness of reference definition guidance shipped with Event.

**Mechanism:** Flip `ConfigurationField` and Desktop `FieldTemplates` to mark only minority `Required` fields and modified/imported/error accent stripes cleanly; extend `DefinitionQualityLinter` with `secret-missing-acquisition-guidance`, `choice-duplicate-explanation`, `description-missing-format-hint`, and `section-high-required-density`, add `--strict` CLI support for CI hygiene, and author `examples/islamu-event/configurator.json` (`revision 7`) + `store/islamu-event/` clearing all warnings in English and Arabic.

**Ethical reasoning (`Sidq`, `Amanah`, `Ihsan`):** Supplying clear, accurate consequences for security and infrastructure choices honors operators' trust and prevents uninformed misconfigurations.

**Recommendation, trade-offs, and evidence limits:** Resolve all `143`+ advisories directly in Event `revision 7` rather than muting rules (`IVSD-M005`). Human authoring ensures accuracy without fabricating target capabilities that Event lacks. Verified by live CLI execution on 2026-11-16 (`diagnosticsTotal: 143`, exit code `0`).

| Traceability field | Value |
|---|---|
| Finding / lifecycle / severity | `IVSD-F005` / open / high |
| Claim type | Operator clarity & reference-definition quality gap |
| Principles / domains | `Sidq`, `Amanah`, `Ihsan` / UX clarity, Authoring quality, Bilingual docs |
| Stakeholders | Event deployers, definition authors, maintainers |
| Controlled decision | Minority-only field badges, linter expansion, Event `revision 7` zero-warning completion |
| Evidence & level | Live CLI `definition lint --quality` execution (`143` warnings); source inspection (`ConfigurationField.razor`, `DefinitionQualityLinter.cs`) |
| Mitigation | `IVSD-M005`: Minority badge + modified stripe, new structural lint rules, `--strict`, Event `revision 7` |
| Owner / next validation | Core, CLI, Browser, Desktop, and Event-example maintainers; Core/CLI tests + `--check-examples` + `--strict` lint |
| Escalation | None |

### IVSD-F006: Mode/Surface Parity Without Accidental Work Loss Across Browser, Desktop, Terminal

**Concrete situation:** Today, Desktop still carries a separate `CompactSettingsTableView` + 3-mode splitter with four crowded header buttons (`Open`/`Configure`/`Review`/`Settings`), and Terminal still swaps between a 15-button Guided pane and a raw `TerminalCompactTableView` with 3-line paginated `[Help 1/3]` text instead of a clean master-detail + 5-key `StatusBar` workbench (`80×24`). Meanwhile Browser's `Catalogue` and `Open` pages interleave SHA-256 verification metadata with primary getting-started calls to action.

**Provider-controlled choice:** Maintainers decide whether Desktop and Terminal remain second-class maintenance forks or adopt clean native idioms (`Avalonia SplitView` + virtualized form/table; `Terminal.Gui v2` Master-Detail + `StatusBar`) over the unified `Presentation` contract.

**Mechanism:** Break and remove `CompactPresentation`, `DesktopConfigureViewMode`, `CompactSettingsTableView`, and `TerminalCompactTableView` cleanly in `0.0.1`; introduce `ConfigureLayoutMode` (`SectionBySection`, `AllSections`) cleanly separated from `WorkflowStage`; adopt Avalonia `SplitView` + bounded `Grid` virtualization on Desktop and `80×24` Master-Detail + 5-key `StatusBar` + scrollable help on Terminal; simplify `Catalogue`/`Open`/`Vault` progressive disclosure in Browser while retaining all cryptographic digests inside expandable verification details.

**Ethical reasoning (`Amanah` & Non-harm):** Operators choosing Terminal over SSH or Desktop over native OS keyrings should receive the same truthfulness, keyboard ergonomics, and low cognitive friction as Browser users, without risking unparsed edit loss when switching views.

**Recommendation, trade-offs, and evidence limits:** Commit to clean zero-compat cross-surface parity (`IVSD-M006`), deleting dead fork components instead of maintaining dual UI paths. Preserves native Desktop `0600` create-only publication and Terminal headless signal/custody invariants.

| Traceability field | Value |
|---|---|
| Finding / lifecycle / severity | `IVSD-F006` / open / medium |
| Claim type | Cross-client maintainability & usability parity gap |
| Principles / domains | `Amanah`, Non-harm, `Ihsan` / Cross-platform UX, Clean Architecture |
| Stakeholders | Browser, Desktop, and Terminal operators; repository maintainers |
| Controlled decision | Clean removal of legacy table/split forks and unified native client idioms |
| Evidence & level | Source inspection (`MainWindow.axaml`, `DesktopNavigationViewModel.cs`, `TerminalWindow.cs`, `Catalogue.razor`, `Open.razor`) |
| Mitigation | `IVSD-M006`: Unified `Presentation` contract, Avalonia `SplitView`, Terminal `80×24` Master-Detail, Browser progressive disclosure |
| Owner / next validation | Presentation, Browser, Desktop, Terminal maintainers; per-owner suites + `NativeHost` D01-D33 + Linux PTY `80×24` |
| Escalation | None |

### IVSD-F007: Zero Surveillance Or Answer Leakage In Scrollspy, Search Palette, And Benchmarks

**Concrete situation:** Adding JS `IntersectionObserver` scrollspy/windowing, `WorkspaceSearch` `role="combobox"` filtering, `MeasureInteractiveWork` instrumentation hooks, and rich field-constraint hints expands client-side state transitions and JS-to-.NET interop calls. Without strict boundaries, opaque IDs, search queries, dynamic map keys, or temporary secret drafts could enter browser `localStorage`/`sessionStorage`, PWA caches, console logs, or DOM attributes.

**Provider-controlled choice:** Maintainers control JS module state, search indexing projections, storage boundaries, and diagnostic logging.

**Mechanism:** Restrict `IntersectionObserver` scrollspy and section windowing to opaque admitted section IDs only; restrict `WorkspaceSearch` and `CompactPropertyRow.MatchesQuery` to static public schema metadata (never answer values, drafts, or private dynamic map keys); forbid `localStorage`/`sessionStorage`/cookies/network beacons for workspace telemetry or layout state; verify in bUnit and published AOT checks that zero answer-bearing data crosses worker/network/cache seams.

**Ethical reasoning (`Amanah` & Privacy):** Offline privacy guarantees cannot remain true only at the submit button while search bars, scrollspy handlers, or performance measurements silently retain or leak user inputs.

**Recommendation, trade-offs, and evidence limits:** Keep session layout preferences in volatile WASM memory cleanly fenced by workspace generation (`IVSD-M007`), with explicit Red-phase tests checking private map key exclusion and storage/network absence.

| Traceability field | Value |
|---|---|
| Finding / lifecycle / severity | `IVSD-F007` / open / critical |
| Claim type | Privacy & telemetry boundary invariant |
| Principles / domains | `Amanah`, Privacy / Browser JS interop, Search indexing, Caches |
| Stakeholders | Operators configuring sensitive deployments in shared or audited browsers |
| Controlled decision | Zero persistence/telemetry in new scrollspy, combobox, and virtualization code |
| Evidence & level | Source inspection (`CompactPropertyRow.cs`, `workspace-interactions.js`, `configuration-rendering.js`, `privacy-and-pii.md`) |
| Mitigation | `IVSD-M007`: Metadata-only search/scrollspy, opaque IDs, zero storage/cookies/network requests, Red/Green leakage tests |
| Owner / next validation | Browser & Presentation maintainers; privacy adversarial tests + published-browser cache/request inspection |
| Escalation | None |

### IVSD-F008: Explicit Evidence Boundary Between Automated Checks And Human/OS Qualification

**Concrete situation:** Passing TUnit, bUnit, Avalonia Headless, `Desktop.NativeHost`, and synthetic `MeasureInteractiveWork` cohorts demonstrates code invariants and Linux x64 reference behavior, but cannot establish blind screen-reader usability (especially Arabic RTL NVDA/JAWS/VoiceOver navigation), physical mobile touch ergonomics, Windows/macOS protected-save creation (`0600`/ACL), or target server acceptance.

**Provider-controlled choice:** Maintainers decide how release readiness, documentation, and UI badges present verification coverage vs remaining manual gates.

**Mechanism:** Record exact tested scopes and remaining physical AT, mobile, cross-OS, and target-acceptance gates in `docs/internal/STATUS.md`, `docs/internal/OPERATIONS.md`, and this assessment without overclaiming pass status from automated suites alone.

**Ethical reasoning (`Sidq` / Candor & Humility):** Honest attribution prevents deploying organizations from treating unit test green lights as universal accessibility or production host guarantees.

**Recommendation, trade-offs, and evidence limits:** Maintain strict verification separation (`IVSD-M008`), preserving unresolved manual AT/cross-OS gates prominently until steward-conducted testing closes them.

| Traceability field | Value |
|---|---|
| Finding / lifecycle / severity | `IVSD-F008` / open / medium |
| Claim type | Governance & qualification honesty boundary |
| Principles / domains | `Sidq`, `Amanah` / Verification reporting, Operations, Documentation |
| Stakeholders | Project stewards, self-hosting deployers, accessibility auditors |
| Controlled decision | Truthful qualification scoping in STATUS/OPERATIONS and planning handoff |
| Evidence & level | Source inspection (`STATUS.md`, `OPERATIONS.md`, `AGENTS.md` §5.18) |
| Mitigation | `IVSD-M008`: Explicit distinction between automated/Linux-host passes and unverified AT/cross-OS/target runtime gates |
| Owner / next validation | Release/Documentation maintainers; doc checks + `STATUS.md` / `OPERATIONS.md` updates at owning phases |
| Escalation | Steward scheduling of human/AT review prior to non-draft support claims |

## Recommendations

1. **Adopt a clean `0.0.1` unification break across `Presentation`, `Browser`, `Desktop`, and `Terminal` (`IVSD-M001`, `IVSD-M004`, `IVSD-M006`)** rather than patching `CompactPresentation` or adding backwards-compatibility shims over `r2`'s split components.
   - *Rejected alternative 1:* Keeping `ConfigurationCompactWorkspace` and `CompactSettingsTableView` alongside `Guided` via CSS/XAML tweaks and a third compatibility mode. Rejected because `r2` proved dual rendering paths double maintenance burden, keep 600 MudBlazor nodes alive (`67.4 ms` p95 failure), and perpetuate desktop/mobile divergence.
   - *Rejected alternative 2:* Replacing Browser configuration entirely with a flat raw JSON/table grid without section landmarks or inline field guidance. Rejected because non-technical operators lose section context, `h2` screen-reader landmarks, and contextual inline explanations (`'Adl`, `Ihsan`).
2. **Enforce DOM-bounded lightweight semantic row controls (`<= 1,400` active nodes) + pure C# form ownership (`IVSD-F001`, `IVSD-M004`, `IVSD-M007`)** so scroll virtualization never compromises edit retention or secret revocation.
3. **Prioritize P0 safety/honesty/accessibility fixes upfront (`IVSD-F002`, `IVSD-F003`)** before cosmetic polish: zero-match recovery banner, exact control focus resolution (`F7`/`F8`/`F9`), truthful error-aware progress bar, `ErrorSummary`, WAI-ARIA `role="combobox"`, and `<bdi dir="ltr">` RTL isolation.
4. **Resolve all `143` Event quality lint warnings in `revision 7` (`IVSD-M005`)** across both `examples/islamu-event/` and `store/islamu-event/`, paired with opt-in CI strictness (`definition lint --quality --strict`).

## Stakeholders

- **Self-hosting community administrators (technical & non-technical):** Need accurate consequences, honest readiness status, clear error jump paths, and responsive tools on modest hardware.
- **Blind, low-vision, and keyboard-only operators:** Depend on `role="combobox"`, semantic HTML5 controls, `F7`/`F8` direct control focus, unambiguous live status announcements, and high-contrast/forced-colors legibility.
- **Arabic RTL operators (`ar` locale):** Need lossless bidirectional isolation (`<bdi dir="ltr">`) around wire keys, JSON pointers, URLs, and paths across Browser, Desktop, and Terminal.
- **Definition authors & ISLAMU maintainers:** Need clear structural lint advisories without fragile prose scoring, plus a single unified cross-client UI architecture free of duplicate `r2` mode forks.
- **Downstream target-host deployers (unrepresented at design time):** Depend on Configurator never impersonating target validation/apply authority or claiming "100% Ready" over invalid optional settings.

## I-VSD Principles And Domains

| Sunni Ethical Principle | Domain Applied | Concrete Design Consequence |
|---|---|---|
| **Amanah** (Trustworthy Stewardship) | Custody, state ownership, authoring guidance | Form state stays single-owner across virtualized scrolling; consent revocation clears secrets synchronously; Event rev 7 explains security/TLS implications clearly. |
| **Sidq** (Truthfulness & Candor) | Readiness UI, qualification boundaries | Optional validation errors block `"100% Ready"` meters; `"Opened"` never poses as `"Reviewed"`; automated suites do not claim human AT sign-off. |
| **'Adl** (Equitable Justice) | Accessibility, RTL, device hardware equity | WCAG 2.2 AA targets, keyboard control focus, Arabic `<bdi>` isolation, Desktop/Terminal first-class ergonomics, and `< 50 ms` throttled responsiveness. |
| **La darar wa la dirar** (Non-Harm) | Privacy, recovery, zero data loss | Zero search/scroll telemetry or persistent answer caching; zero-match filters preserve input and explain recovery; mode switches never lose unparsed drafts. |
| **Ihsan** (Excellence & Consideration) | Information architecture, density | Minority-only labeling eliminates badge fatigue; Terminal `80×24` declutters button walls into clear Master-Detail + 5-key footer. |

## Validation Gaps

1. **Physical Assistive Technology & Arabic Screen-Reader UX:** Not runnable via automated bUnit/Headless/MTP suites; requires steward-coordinated NVDA/VoiceOver/Orca verification in `en` and `ar`.
2. **Real Browser AOT Memory, Frame Timing, and Scroll-Window CLS:** Previous `600`-setting Compact AOT measured `67.4 ms` p95 (red); new semantic windowed architecture requires fresh `PublishConfiguratorBrowser` + `MeasureInteractiveWork.cs` qualification in Phase 3 (`1x`/`4x`, `101` & `600` cohorts).
3. **Cross-OS Protected-Save Hosts (`Windows`/`macOS`):** Beyond scope of UI unification; Linux `0600` create-only behavior remains the verified native reference host.
4. **Desktop NativeHost Visual Matrix (`D01`–`D33`) & Terminal Linux PTY (`80×24`):** Must be re-executed after Phases 6 & 7 UI refactors.

## Escalation Needed

- **Scholarly / Fiqh escalation:** None required. No contested religious-legal ruling, monetization contract, or community moderation policy is introduced by this offline configuration UI/lint unification. *(Future hosted/curated Store monetization or paid placement would trigger separate scholarly & governance review).*
- **Steward / Human-AT escalation gate:** Before claiming external WCAG / AT conformance beyond structural ARIA/contrast automation, maintainers must schedule physical screen-reader walkthroughs in English and Arabic across Browser (`AllSections` & `ErrorSummary`), Desktop (`SplitView`), and Terminal (`80×24` Master-Detail).

## Evidence Reviewed

- `dev/report/configuration-experience-plan-coverage-gaps-2026-11-15.md` (`2026-11-16`, `56,659` bytes; 22 audit areas and `G01`–`G12`).
- `docs/internal/STATUS.md` & `docs/internal/OPERATIONS.md` (`2026-11-16` 600-setting Compact AOT run `c489b3e1...`, `67.4 ms` p95, `13/40` `> 50 ms`, `2154.5 ms` complete).
- Live CLI execution: `dotnet run --project src/Configurator.Cli/Configurator.Cli.csproj --configuration Release --no-build -- definition lint --source examples/islamu-event/configurator.json --quality --machine --detail diagnostics` (`exitCode: 0`, `diagnosticsTotal: 143`).
- Source inspections: `src/Configurator.Presentation/Form/DefinitionFormModel.cs`, `DefinitionEditorState.cs`, `SectionStatusProjection.cs`, `WorkspaceNavigationProjection.cs`, `WorkspacePresentationOptions.cs`, `CompactPropertyRow.cs`; `src/Configurator.Browser/Components/Configuration/*`, `Layout/*`, `Pages/*`, `wwwroot/{css,js}/*`; `src/Configurator.Desktop/{Views,ViewModels,Resources}/*`; `src/Configurator.Terminal/{Views,Controls}/*`; `src/Configurator.Core/Definitions/Authoring/DefinitionQualityLinter.cs`.
- Clean-room industry standards research (`2026-11-16`): Microsoft Learn `.NET 10` Blazor WASM virtualization/`ShouldRender`/`WasmStripILAfterAOT`, W3C WAI-ARIA APG `combobox`/`aria-activedescendant`, Avalonia `SplitView`/`ItemsRepeater` virtualization, Terminal.Gui v2 `StatusBar`/`Shortcut`/`TableView`, GOV.UK `ErrorSummary`, AWS Cloudscape `AnchorNavigation`/`Flashbar`, Google INP DOM budget (`< 1,400` nodes).

## Missing Evidence

- Post-unification AOT `MeasureInteractiveWork.cs` output, `Desktop.NativeHost` `D01`–`D33` captures in `en`/`ar`, and Terminal `80×24` PTY recordings do not yet exist (executed during implementation Phases 3, 6, 7).
- Physical screen-reader/assistive-technology observations in English and Arabic remain an external steward gate beyond automated assertions (`IVSD-F008`).

## Context Inventory

- Workstream relationship: Supersedes Compact-only `dev/active/configuration-experience/` (`r2`, 29/30 done with 61 uncommitted working-tree files + 1 untracked test `DefinitionQualityGuidanceTests.cs` + quarantined `TrustedVerifierPackagingTests`).
- Repository baseline: SDK `10.0.404`, pre-release `0.0.1` breaking changes explicitly authorized by user and `AGENTS.md` §3, offline WASM + native Linux/Desktop/Terminal/CLI targets, MIT license posture.

## Planning Handoff

- Workstream: `configuration-experience-unification`
- Status: current
- Disposition: plan-aligned
- Reviewed input: `configuration-experience-unification-plan.md` (`be7c3b13fd7e3f89467691332fe48e0f9908b2fcdb3d4ac154b2d34064fc2557`), `configuration-experience-unification-context.md` (`25a665025c76d46a7220d15c7e5f1b5d86ea41333f637224c9da3562821a2ebe`), `configuration-experience-unification-tasks.md` (`a99634a4ff6ecd333f89a9548f311c6d24629f4e0a06482118c89b0aecf7a8da`)
- Findings and mitigations: `IVSD-F001` -> `IVSD-M001`, `IVSD-F002` -> `IVSD-M002`, `IVSD-F003` -> `IVSD-M003`, `IVSD-F004` -> `IVSD-M004`, `IVSD-F005` -> `IVSD-M005`, `IVSD-F006` -> `IVSD-M006`, `IVSD-F007` -> `IVSD-M007`, `IVSD-F008` -> `IVSD-M008`
- Required plan mappings: `IVSD-F001`/`M001` -> Scenarios `S02`, `S04` (`TASK-1.1`, `TASK-3.1`–`TASK-3.3`); `IVSD-F002`/`M002` -> Scenario `S01` (`TASK-1.1`, `TASK-1.2`, `TASK-2.1`, `TASK-2.2`, `TASK-4.1`, `TASK-4.2`); `IVSD-F003`/`M003` -> Scenarios `S05`–`S09` (`TASK-1.2`, `TASK-3.1`, `TASK-3.2`, `TASK-4.1`, `TASK-4.2`, `TASK-6.2`, `TASK-7.2`); `IVSD-F004`/`M004` -> Scenario `S03` (`TASK-3.1`–`TASK-3.3`); `IVSD-F005`/`M005` -> Scenarios `S09`, `S13`, `S14` (`TASK-4.2`, `TASK-6.2`, `TASK-8.1`, `TASK-8.2`); `IVSD-F006`/`M006` -> Scenarios `S02`, `S10`–`S12` (`TASK-1.2`, `TASK-5.1`–`TASK-7.2`); `IVSD-F007`/`M007` -> Scenarios `S04`, `S07` (`TASK-2.1`, `TASK-3.1`, `TASK-4.1`); `IVSD-F008`/`M008` -> all `VERIFY-*`, `TASK-3.3`, `TASK-6.3`, `TASK-8.3`, `STATUS.md`, `OPERATIONS.md`
- Escalations required before: planning approval (user review of triad) | release (physical AT walkthrough in `en` and `ar`; cross-OS Desktop/Terminal host validation)
- Refresh triggers: Any change to transient browser secret custody across virtualized rows, readiness/validation calculation rules, zero-telemetry search/scrollspy boundary, Desktop/Terminal removal vs retention of native features, or Event revision 7 public guidance scope.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
|---|---|---|---|---|
| 2026-11-16 | None | draft | Initial I-VSD planning intake for `configuration-experience-unification` | Supplied evidence packet + live CLI lint (`143`) + `configuration-experience-plan-coverage-gaps-2026-11-15.md` |
| 2026-11-16 | draft | current | Revalidated against completed triad (`configuration-experience-unification-plan.md`, `-context.md`, `-tasks.md`) | SHA-256 bound (`be7c3b13...`, `25a66502...`, `a99634a4...`); verified Section 9 `IVSD-F001`–`F008` mappings |
