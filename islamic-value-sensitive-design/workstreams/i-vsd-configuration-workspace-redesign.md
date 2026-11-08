# Configuration workspace, rich schema widgets, and dual-UX redesign — Provider-responsibility assessment

Last Updated: 2026-11-08

## Review Metadata
- Mode: planning
- Subject: Cross-surface configuration workspace redesign, Material 3 4-step density/theming, 2D section status and review coverage, `Humanized` vs. `Technical` dual-UX naming mode, rich schema widgets, consent-safe power-user Compact table, Desktop/Terminal document-session editing, schema-bounded modal secret generation, and public definition exploration
- Workstream: configuration-workspace-redesign
- Report kind: implementation-planning assessment
- Report revision: workspace-redesign-ivsd-r2
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-11-08
- Reviewed input: `configuration-workspace-redesign` plan/context/tasks triad revision 2 (sequenced second after `definition-schema-redesign` rev 2) against repository `HEAD` `d7b2bfe` and `dev/report/ISLAMU_Configurator_UI_UX_Redesign_Review.md`
- Supersedes: `workspace-redesign-ivsd-r1` (in-place update absorbing all interactive `Presentation`, `Browser`, `Desktop`, and `Terminal` UI/Dual-UX responsibilities transferred from `i-vsd-definition-schema-redesign.md` while delegating prerequisite Core schema/CLI/`islamu-event` `revision: 4` compilation to `i-vsd-definition-schema-redesign.md` `schema-redesign-ivsd-r2`); prior workstream reports retain their recorded historical scope and evidence

## Executive Summary

Operators and engineers preparing self-hosted software configurations across the static WebAssembly Browser client, the native Avalonia Desktop client, and the Terminal.Gui workbench currently face competing layout chrome, conflated section completion badges, coarse visual density, inline-only fixed secret generation, hidden field metadata in Desktop (`AutomationProperties.HelpText` only), raw enum/map-key quirks in Terminal, and no fast power-user property grid or native document-session editor.

Per explicit user instruction, work is sequenced across two complementary workstreams with zero data loss:
- **Prerequisite Workstream 1 (`definition-schema-redesign` `rev-2`, executed FIRST — governed by `[i-vsd-definition-schema-redesign.md](i-vsd-definition-schema-redesign.md)`):** Delivers non-UI `Configurator.Core` humanization (`DefinitionDisplayText`), `DefinitionChoice`, `OrderedPropertyNames`, `DefinitionFieldPresentationMetadata` (`placeholder`, `defaultText`, `deprecationMessage`, `widget`, `rows`, `syntax`, `booleanLabels`, `itemTitlePointer`, composite `uniqueBy`), `DefinitionSectionGroup`, 23 asserted `O(n)` domain formats, CLI `definition answer-schema` + `definition lint --quality`, and de-flattened `islamu-event` canonical `revision: 4`.
- **This Workstream 2 (`configuration-workspace-redesign` `rev-2`, executed SECOND):** Evaluates ISLAMU's provider-controlled UI, presentation, privacy, and accessibility responsibilities across seven connected capabilities:
  1. Separating **configuration state** (`Invalid`, `MissingRequired`, `UnresolvedApplicability`, `SecretDeferred`, `RequiredComplete`, `OptionalUnset`, `OutsideSelectedScope`, `PendingEvaluation`) from **transient review coverage** (`NotVisited`, `Opened`, `Reviewed`, `NewlyRelevantSettings`).
  2. Recomposing Browser, Desktop, and Terminal shells into connected workspace layouts with Material Design 3 tonal palettes, a four-step density scale (`Spacious`, `Comfortable`, `Compact`, `Dense`), genuine `wizard` vs. `sections` navigation modes, and bidirectional-safe (`dir="ltr"`) monospace technical tokens in Arabic (`rtl`).
  3. Replacing disabled startup form walls with explicit four-stage workspace readiness (`BeforeEntry`, `Preparing`, `PreparationFailed`, `Ready`) and honest empty-section explanations.
  4. Introducing a KeePass-inspired **modal secret generator dialog** across Browser (`MudDialog`), Desktop (`Window.ShowDialog`), and Terminal (`Dialog`) over a shared transient session bounded strictly by schema cryptographic constraints and the 128-bit free-draw entropy floor.
  5. Providing a **consent-safe Power-User Compact Property Table** (`BrowserNoSecrets` compatible in Browser, plus Desktop and Terminal compact tables), **Desktop and Terminal `DefinitionDocumentSession` text editors** fenced by `OperationGeneration`, and a **metadata-only side inspector** over the single form authority without plaintext leaks or secondary answer stores.
  6. Rendering **Dual-UX `WorkspaceNamingMode` (`Humanized` ↔ `Technical`)** and **rich schema field semantics** (`placeholder`, non-materializing `"If omitted: <defaultText>"`, `deprecationMessage`, inline `units` and `choice.explanation`, `multiline`/`markdown`/`code`, `radio`, `slider`, tri-state/`switch` booleans with explicit `Omit`, dynamic `itemTitlePointer`/`uniqueBy` row titles, and collapsible section `groups`) truthfully across all three clients, paired with separated **Review** and **Files** stages.
  7. Expanding isolated public **Definition Exploration** (`Configure` vs. `Explore definition`) and qualifying all three interactive surfaces end-to-end against Workstream 1's de-flattened `islamu-event` `revision: 4` while preserving clean-room MIT provenance.

The updated `configuration-workspace-redesign` triad (`rev-2`) has been revalidated against all seven findings (`IVSD-F001` through `IVSD-F007`) and mitigations (`IVSD-M001` through `IVSD-M007`). `Report status: current` and `Disposition: plan-aligned` mean every identified UI and custody responsibility maps to concrete RFC 2119 behavioral scenarios (`S01`–`S19`) and invariant-first tasks. This disposition does **not** grant user execution approval, certify WCAG 2.2 AA or screen-reader conformance, prove WASM AOT latency budgets prior to runtime measurement, or issue any religious-legal ruling.

## Scope

Provider-controlled runtime secret-generation option bounds (`Configurator.Core.Secrets`), workspace/field/table/status projections (`Configurator.Presentation`), and interactive UX, accessibility, density, widgets, compact property grids, document-session editors, modal secret generation, and public schema exploration across `Configurator.Browser`, `Configurator.Desktop`, and `Configurator.Terminal`, plus UI/presentation documentation (`docs/public/{browser,desktop,terminal}.md`, `docs/internal/{PRESENTATION_STATE,BROWSER_UI,DESKTOP_CLIENT,TERMINAL_CLIENT,LAUNCH_CONTRACT}.md`, ADRs). Non-UI schema keywords, format validators, CLI commands, and `islamu-event` `revision: 4` authoring belong to prerequisite Workstream 1 (`definition-schema-redesign`). No backend API, telemetry, cloud storage, remote target apply, or new third-party NuGet runtime package is proposed.

## Claim Boundary

Evidence reviewed consists of static repository source inspection at commit `d7b2bfe`, test/ratchet contracts in `PROJECTS.md`, `dev/report/ISLAMU_Configurator_UI_UX_Redesign_Review.md`, official framework/accessibility documentation, and clean-room functional UI observations (`2026-11-07` and `2026-11-08`). No planned product changes were executed, compiled, timed in a browser, or tested with external human participants or screen readers during planning. No Sharia-compliance, halal/haram, or legal license opinion is issued.

## Findings

### IVSD-F001: Single-badge section completion can deceive operators about unreviewed defaults or target acceptance

When a definition supplies valid `initialValue` defaults for required fields in a section, validation alone sees zero missing obligations. If the sidebar renders a single green "Complete" indicator—or conversely paints unvisited optional sections in alarming warning orange—an operator can reasonably conclude either that uninspected defaults were verified for their community's deployment, or that every optional integration must be filled before export. Similarly, a generic green "Success" banner on the Files screen obscures the critical difference between in-memory byte preparation, an unprotected browser download, and create-only native file installation.

The maintainer controls status vocabulary, badge precedence, progress denominators, and export-stage messaging in `Configurator.Presentation` and all three interactive clients. Under **Sidq (truthfulness)**, **Bayan (clear, non-deceptive disclosure)**, and **Amanah (trustworthy stewardship)**, the tool must represent only facts it actually established.

- **Mechanism & Mitigation (`IVSD-M001`)**: Project two independent per-section dimensions from `Configurator.Presentation`: (1) `SectionConfigurationStatus` prioritizing `Invalid` (including invalid optional drafts) > `MissingRequired` > `UnresolvedApplicability` > `SecretDeferred` > `RequiredComplete` > `OptionalUnset` > `OutsideSelectedScope` > `PendingEvaluation`; and (2) transient workspace-scoped `SectionReviewCoverage` (`NotVisited`, `Opened` only upon deliberate section presentation, `Reviewed` via explicit acknowledgment, and `HasNewlyRelevantSettings` downgrading `Reviewed` to `Opened` when controller changes expose new fields). Display separate denominators (`Required work` vs. `Sections opened`), keep unvisited optional sections advisory during Review (never a hidden export gate), and distinguish `Complete candidate`, `Incomplete draft`, `Stale after edits`, `Browser custody transfer`, and `Native per-file publication` outcomes in Files.
- **Lifecycle / severity / claim type**: accepted / high / truthfulness and UX state constraint
- **Principles / domains**: Truthfulness (Sidq), Avoiding Deception (Gharar/Tadlis avoidance), Trust (Amanah) / UX, architecture, operations
- **Stakeholders**: Self-hosting operators, community administrators, downstream end users affected by misconfigured defaults
- **Controlled decision**: Shared `SectionStatusProjection`, progress denominators, Review advisories, and Files outcome vocabulary
- **Evidence / validation**: `E01`, `E02`, `E05`, `E07`; static source traceability (`DefinitionFormFacts.cs`, `DefinitionViewState.cs`), not runtime proof
- **Linked mitigation**: `IVSD-M001`
- **Owner / next validation**: Presentation, Browser, Desktop, and Terminal maintainers; invariant-first `SectionStatusProjection` tests before renderer changes
- **Escalation**: None unless a product policy proposes blocking export on unvisited optional sections

### IVSD-F002: Interactive modal secret generation can weaken cryptographic floors or leak transient candidates if unconstrained

Moving from a fixed one-click `Generate` button to a KeePass-style customizable Secret Generator modal (length slider, character-group toggles, look-alike exclusion, byte length, asymmetric curve/modulus choices, live candidate preview) gives operators visibility and control. However, if the UI allows disabling required character classes, reducing length below the 128-bit free-draw entropy floor, violating target `ForbiddenCharacters`, auto-committing unapproved previews on backdrop click, or retaining unzeroed candidate buffers when Browser secret consent is revoked, the dialog becomes a vulnerability and custody leak.

The maintainer controls `SecretGenerator` bounds in `Configurator.Core`, candidate buffer lifetime in `Configurator.Presentation`, and modal dialog lifecycles in Browser (`MudDialog`), Desktop (`Window`), and Terminal (`Dialog`). Under **Amanah (custody of entrusted secrets)** and **La Darar (non-harm)**, interactive customization must never generate weak or target-invalid material or outlive workspace consent.

- **Mechanism & Mitigation (`IVSD-M002`)**: Implement `SecretGenerationBounds` and `SecretGenerationOptions` inside `Configurator.Core.Secrets` so every slider, toggle, look-alike filter, and custom exclusion is clamped against admitted `DefinitionSecretConstraints` and enforces `>= max(128, MinEntropyBits)` free-draw entropy using unbiased `RandomNumberGenerator` rejection sampling. Manage modal state through `DefinitionSecretGeneratorSession` in `Configurator.Presentation`, holding candidate `SecretValue` buffers that commit **only** on explicit `Apply`, never auto-write to clipboard, and zero synchronously via `CryptographicOperations.ZeroMemory` on `Cancel`, `Escape`, external field edit, workspace replacement, or `DisableSecretEntry` consent revocation (`BackdropClick = false`). Never fabricate local generation for `Acquisition == Issuer` or `ConnectionString` secrets.
- **Lifecycle / severity / claim type**: accepted / critical / cryptographic floor and secret custody constraint
- **Principles / domains**: Trust (Amanah), Non-Harm (La Darar), Truthfulness / security, privacy, UX
- **Stakeholders**: Operators provisioning databases/keys/passwords and end users whose data those secrets protect
- **Controlled decision**: `SecretGenerator` option bounds, `DefinitionSecretGeneratorSession` zeroization/fencing, and modal dialog dismissal rules
- **Evidence / validation**: `E01`, `E03`, `E04`, `E05`; inspected `SecretGenerator.cs`, `DefinitionSecretOperation.cs`, `ConfigurationSecretActions.razor`, `SecretActionsView.axaml`, `TerminalWindow.Secrets.cs`
- **Linked mitigation**: `IVSD-M002`
- **Owner / next validation**: Core, Presentation, and client maintainers; Red invariant tests for entropy/constraint clamping and held-dialog revocation races
- **Escalation**: Security review before adding any future wordlist passphrase profile or new cryptographic primitive

### IVSD-F003: Material 3 visual polish, four-step density (`Spacious` to `Dense`), rich widgets, and Arabic RTL technical tokens must remain accessible to all operators

Adopting Material Design 3 tonal surfaces, adding four density levels (`Spacious`, `Comfortable`, `Compact`, `Dense`), introducing richer widgets (`multiline`, `markdown`, `code`, `radio`, `switch`, `slider`, collapsible `groups`), and rendering monospace wire keys/JSON pointers alongside Arabic (`rtl`) labels improve clarity and scan speed. Yet dark tonal palettes can fail contrast, sticky headers can obscure focused inputs at 200%–400% zoom, `Dense` mode can shrink controls below operable hit bounds or clip Arabic ascenders/descenders, and un-isolated LTR technical tokens (`env.PUBLIC_BASE_URL`, `/ai/provider`, `OPENAI_COMPATIBLE`) corrupt visual order inside RTL layouts.

The maintainer controls CSS/AXAML tokens, density math, widget keyboard semantics, bidi isolation, breakpoint transitions, and native OS high-contrast precedence. Under **Adl (justice and equitable access)** and **Ihsan (craftsmanship that serves people)**, visual and widget richness must strengthen rather than degrade accessibility.

- **Mechanism & Mitigation (`IVSD-M003`)**: Build Light and Dark tonal ramps on quiet neutral-tinted surfaces with verified linearized sRGB contrast (`>= 4.5:1` normal text, `>= 3:1` active control boundaries/focus indicators), preserving native OS high-contrast override in Desktop (`D25`) and explicit high-contrast modes in Browser. Keep status and deprecation meaning dual-coded with icon shape + visible text. Scale `WorkspaceDensity` (`Spacious`, `Comfortable`, `Compact`, `Dense`) on the 4px grid without shrinking body font size or violating WCAG 2.2 AA `24×24` CSS px minimum target size in `Dense` (`44–48px` in `Comfortable`/`Spacious`). Wrap monospace technical identifiers, JSON pointers, env bindings, and wire enum tokens in explicit LTR bidi isolation (`dir="ltr"` / `FlowDirection.LeftToRight`) during Arabic (`rtl`) presentation. Disable header stickiness on short viewports (`max-height: 36rem`), collapse sidebars/inspectors cleanly (`320px` Browser reflow, `600×540` Desktop minimum, `76×20` Terminal guard), and ensure every new widget (`multiline`, `radio`, `switch`, `slider`, collapsible `groups`) is 100% keyboard-operable.
- **Lifecycle / severity / claim type**: accepted / high / accessibility, inclusive design, and RTL bidi constraint
- **Principles / domains**: Justice (Adl), Excellence (Ihsan), Non-Harm / UX, accessibility, localization
- **Stakeholders**: Low-vision, keyboard-only, motor-impaired, high-zoom, small-screen, and Arabic-speaking operators
- **Controlled decision**: M3 token contrast pairs, 4-step density dimensions, widget ARIA/keyboard contracts, LTR technical-token isolation in RTL, and responsive breakpoints
- **Evidence / validation**: `E03`, `E04`, `E05`, `E07`; inspected `tokens.css`, `ThemePalettes.axaml`, `DesignTokens.axaml`, `FieldTemplates.axaml`, `TerminalWindow.cs`; source traceability only
- **Linked mitigation**: `IVSD-M003`
- **Owner / next validation**: Browser, Desktop, and Terminal maintainers; automated contrast/reflow/bidi/headless checks plus real X11 (`D25`, `D27` in `en` and `ar`)
- **Escalation**: Qualified accessibility/AT evaluation before claiming formal WCAG 2.2 AA conformance

### IVSD-F004: Relocating scope controls, opening a consent-safe Power-User Compact Table, adding Desktop/Terminal document editors, and rendering a side inspector must not leak secrets, bypass consent, or corrupt drafts

Moving deployment recipe/scope selectors to the workspace header, introducing a high-density Power-User Compact Property Table across Browser/Desktop/Terminal, exposing `DefinitionDocumentSession` text editing in Desktop and Terminal, and placing extended help in a right-hand side inspector create four subtle privacy and state-integrity hazards: (1) demoting consequential recipe/scope mutations into view-only UI state; (2) forcing premature Browser secret consent just to scan/edit public variables in the Compact Table—or conversely allowing secret cells, dynamic private map keys, or masked values to leak into unconsented cells, table search, sorting, or tooltips; (3) allowing stale `DefinitionDocumentSession` text commits in Desktop or Terminal to overwrite newer form edits or retain unzeroed buffers; and (4) converting unset optional booleans to `false` when rendering `widget: "switch"`, or turning the side inspector into a plaintext value preview or focus thief.

The maintainer controls `DefinitionFormModel` binding, `CompactPropertyRow` consent gating, `DefinitionDocumentSession` fencing, boolean/widget controls, and inspector contracts. Under **Tajassus avoidance (preventing unintended exposure of private data)** and **Amanah**, power-user speed must uphold exact custody and state boundaries.

- **Mechanism & Mitigation (`IVSD-M004`)**: Bind the relocated workspace-header scope summary (`Deployment: <Recipe> · <Scope> — Change`) directly to `DefinitionFormModel.SelectConfigurationContext`. Project `CompactPropertyRow` over the exact same `DefinitionFormModel` and logical row identities (`@key` / `FieldProjectionCollection`) so `ConfigurationCompactTable` opens **without** requiring premature Browser secret consent (`BrowserNoSecrets` safe: `public` cells inspect/edit inline while `secret` rows render locked `IsSecretLocked == true` until `EnableSecretEntry()`, `sensitive` and `secret` values stay masked `••••••••`, private values and dynamic private map keys are excluded from search/sort/tooltips, and `DisableSecretEntry()` purges secret leaves immediately). Fence Desktop `DocumentEditorView` and Terminal `TerminalDocumentDialog` via `DefinitionDocumentSession` + `OperationGeneration` (`document-stale` rejection on concurrent edits, single-use `DefinitionDocumentCandidate` zeroization, `<= 256 KiB` terminal cap, zero OSC52). Preserve tri-state `Unset / Yes / No` (and require an explicit `Omit / Reset` control on `widget: "switch"` whenever absence is distinct), and keep the side inspector strictly bound to `DefinitionEditorPresentation` public metadata with zero answer reads and zero focus stealing.
- **Lifecycle / severity / claim type**: accepted / critical / privacy, secret-consent boundary, and state-authority constraint
- **Principles / domains**: Avoiding Unwanted Disclosure, Trust (Amanah), Non-Harm / architecture, privacy, UX
- **Stakeholders**: Power-user developers/SREs, operators editing sensitive/secret values, and multi-component administrators
- **Controlled decision**: Header scope binding, consent-safe `CompactPropertyRow` projection, `DefinitionDocumentSession` generation fencing, tri-state/switch omission controls, and metadata-only inspector
- **Evidence / validation**: `E01`–`E05`; inspected `DefinitionConfigurationContext.cs`, `DefinitionDocumentSession.cs`, `ConfigurationField.razor`, `ConfigurationDeveloper.razor`, `FieldTemplates.axaml`, `TerminalFieldEditor.cs`
- **Linked mitigation**: `IVSD-M004`
- **Owner / next validation**: Presentation, Browser, Desktop, and Terminal maintainers; Red consent-gating, form/table round-trip, stale-document-generation, and sentinel non-leakage tests
- **Escalation**: Reassess if an unconsented full-document raw secret buffer is ever proposed in Browser

### IVSD-F005: Startup readiness gates and public Definition Exploration must remain strictly separated from private workspace custody

Before `BrowserOfflinePolicy` finishes preparing local public assets, Browser currently renders a disabled form alongside a `Start workspace` button and status text, while Store unavailability uses a warning style that can look like an engine failure on intentionally Store-free builds. Meanwhile, operators need a Lexicon Garden-style **Explore definition** view to inspect supported application versions, settings, and outputs before starting configuration. If fixing the startup experience bypasses the readiness gate, or if the public Definition Explorer shares URL state, search terms, or network fetches with an active private workspace, private answers or dynamic map keys could cross the public boundary.

The maintainer controls startup state transitions, `/explore` and `/store` routing, `WorkspaceNavigationGuard`, and Store catalog error presentation. Under **Amanah** and **Sidq**, readiness protection must become clearer without weakening the pre-answer custody gate.

- **Mechanism & Mitigation (`IVSD-M005`)**: Render four explicit workspace startup states (`BeforeEntry` with public Explore link and single `Start local workspace` action, `Preparing` with honest stage feedback, `PreparationFailed` with value-safe diagnostics and recovery without silent workspace reload, and `Ready` with calm `Secret entry off` pill)—mounting zero editable answer controls before `Ready`. Keep `Explore definition` (`Overview`, `Settings`, `Outputs`, `Source & revisions` in Browser and Desktop `Open` stage) strictly read-only over admitted public `Definition` metadata, excluding active workspace answers/queries from URLs and retaining `WorkspaceNavigationGuard` discard confirmation before departing a live workspace. Present intentional Store omission neutrally alongside bundled/local entry, reserving retry banners for configured Store fetch failures, and label sources factually (`Bundled`, `Local file`, `Selected public source`) rather than `Verified` or `Official`.
- **Lifecycle / severity / claim type**: accepted / high / custody boundary and source-truthfulness constraint
- **Principles / domains**: Trust (Amanah), Truthfulness (Sidq), Avoiding Deception / security, privacy, UX
- **Stakeholders**: Operators evaluating definitions prior to adoption, offline/air-gapped operators, and third-party definition publishers
- **Controlled decision**: Startup state machine, public explorer isolation, factual source badges, and Store-free presentation
- **Evidence / validation**: `E03`, `E05`, `E07`; inspected `ConfigurationEditor.razor.cs`, `BrowserOfflinePolicy.cs`, `Catalogue.razor`, `StoreProject.razor`, `DefinitionLoaderView.axaml`
- **Linked mitigation**: `IVSD-M005`
- **Owner / next validation**: Browser and Desktop maintainers; startup-gate custody invariants and Store-free/explorer isolation tests
- **Escalation**: None within static offline discovery; publisher-signature trust remains governed separately by `AGENTS.md` Rule 19

### IVSD-F006: Dual-UX naming modes (`Humanized` vs. `Technical`), non-materializing `defaultText`, Desktop/Terminal metadata surfacing, and end-to-end `islamu-event` `revision: 4` rendering must stay truthful and consistent across clients

Prerequisite Workstream 1 (`definition-schema-redesign`) delivers Core `DefinitionDisplayText` humanization, `DefinitionChoice`, `OrderedPropertyNames`, `DefinitionFieldPresentationMetadata`, `DefinitionSectionGroup`, `definition lint --quality`, and de-flattened `islamu-event` `revision: 4`. However, interactive clients currently exhibit four UI-layer truthfulness and parity defects: (1) Browser has a partial root-only `ConfigurationDiagnosticText.Title` helper (`ConfigurationDiagnosticText.cs:9-28`) that diverges from Desktop and Terminal, and operators switching to humanized titles could lose sight of exact `.env`/JSON wire identifiers unless a first-class `Humanized ↔ Technical` toggle exists; (2) rendering `defaultText` inside an input's editable value rather than a distinct `"If omitted: ..."` caption would deceive operators into thinking an unset field was populated; (3) Desktop buries `Instructions`, `Units`, `Explanation`, and `Links` exclusively inside `AutomationProperties.HelpText` (`FieldViewModel.cs:136-146`), while Terminal displays raw JSON enum tokens instead of `choice.label` (`TerminalFieldEditor.cs:52-62`) and hides map keys behind `"Map entry 1"` (`TerminalRootEditor.cs:32-33`); and (4) `navigation: "wizard" | "sections"` is currently a UI no-op (`ConfigurationConfigurePanel.razor:27`).

Under **Ihsan (excellence in helpfulness)**, **Bayan (clarity)**, and **Sidq**, all three clients must surface schema guidance visibly, honor wire-identifier transparency for power users, and render `islamu-event` `revision: 4` faithfully with zero Event-specific UI branches.

- **Mechanism & Mitigation (`IVSD-M006`)**: Delete Browser's partial `ConfigurationDiagnosticText.Title` in favor of shared `DefinitionEditorPresentation` over Core `DefinitionDisplayText`; add `WorkspaceNamingMode` (`Humanized` default with subtle monospace technical key, vs. `Technical` leading with raw `SCREAMING_SNAKE_CASE` keys, JSON pointers, and wire enum tokens like `OPENAI_COMPATIBLE`); render `defaultText` strictly as non-editable `"If omitted: <defaultText>"` helper text and `deprecationMessage` as visible warning callouts; surface `Units`, `Placeholder` (with public `Examples[0]` fallback), `Explanation`, `Instructions`, `Links`, dynamic `itemTitlePointer`/`uniqueBy` headers, and collapsible `DefinitionFormSectionGroup`s visibly across Browser, Desktop (`FieldTemplates.axaml`), and Terminal (fixing `TerminalFieldEditor` choice labels and `TerminalRootEditor` map keys); implement distinct `wizard` vs. `sections` navigation cues; and verify all 16 sections of `islamu-event` `revision: 4` end-to-end in Phase 7.
- **Lifecycle / severity / claim type**: accepted / medium / cross-client comprehension, Dual-UX transparency, and UI parity constraint
- **Principles / domains**: Excellence (Ihsan), Clarity (Bayan), Truthfulness (Sidq) / Presentation, Browser, Desktop, Terminal UX
- **Stakeholders**: Guided community administrators, SRE/developer power users, and Desktop/Terminal operators
- **Controlled decision**: Shared `WorkspaceNamingMode`, visible field/choice/group metadata rendering across all 3 clients, non-materializing `defaultText` UI, and `wizard` vs. `sections` behavior
- **Evidence / validation**: `E02`–`E06`; inspected `ConfigurationDiagnosticText.cs`, `ConfigurationConfigurePanel.razor`, `FieldViewModel.cs`, `TerminalFieldEditor.cs`, `TerminalRootEditor.cs`
- **Linked mitigation**: `IVSD-M006`
- **Owner / next validation**: Presentation, Browser, Desktop, and Terminal maintainers; cross-surface parity tests and Phase 7 `BrowserEventManifestWorkflowTests` + `NativeHost` qualification over `islamu-event` `revision: 4`
- **Escalation**: None (prerequisite schema/store `revision: 4` governance tracked in `i-vsd-definition-schema-redesign.md` `IVSD-F001`/`IVSD-F004`)

### IVSD-F007: External visual/workflow inspiration and UI framework choices must preserve clean-room MIT provenance and dependency closure

This redesign draws functional and visual inspiration from user-supplied screenshots and public specifications—Google Account settings, Dank Material Shell (`dux`, GPL-licensed), KeePassXC's password generator dialog (GPL-licensed C++/Qt), Coolify's Normal/Developer variable views, VS Code Settings UI / Firefox `about:config` property tables, RJSF / Home Assistant / Backstage / NixOS widget semantics, Lexicon Garden's schema explorer, GOV.UK's task list, and Material Design 3—and evaluated whether to introduce `CodeBeam.MudBlazor.Extensions` alongside `MudBlazor 9.7.0`. Copying third-party QML/C++/CSS/TS/Razor source or expressive SSO, or introducing an unvetted extension package into an MIT-distributed AOT WebAssembly utility, would jeopardize outbound MIT licensing (`AGENTS.md` Rule 10) and deterministic AOT security boundaries.

The maintainer controls clean-room research boundaries (`docs/internal/legal/IP_GOVERNANCE.md`) and `Directory.Packages.props`. Under **Haqq al-'Ibad (respecting others' rights and license terms)** and **Amanah**, ISLAMU must design independently and keep its dependency closure minimal and auditable.

- **Mechanism & Mitigation (`IVSD-M007`)**: Enforce strict clean-room isolation: inspect zero source code, QML, C++, TypeScript, stylesheets, or assets from Dank Material Shell, KeePassXC, Coolify, Lexicon Garden, Google Account, VS Code, RJSF, Home Assistant, or Backstage; retain only sanitized functional specifications and AFC/SSO comparisons in the workstream triad; never embed third-party screenshots in tracked files; and reject adding `CodeBeam.MudBlazor.Extensions` or `Material.Avalonia` (implementing all dialogs, widgets, and compact tables directly over already-installed MIT-licensed `MudBlazor 9.7.0`, `Avalonia 12.1.3`, and `Terminal.Gui 2.5.0` with zero new NuGet packages). Verify lockfile and license compliance via `.ci/scripts/validate-dependency-license-policy.cs`.
- **Lifecycle / severity / claim type**: accepted / high / IP provenance and dependency-governance constraint
- **Principles / domains**: Rights of People (Haqq al-'Ibad), Trust (Amanah), Honesty / legal/IP governance, build/dependencies
- **Stakeholders**: Upstream open-source authors, ISLAMU Project Steward, and downstream MIT recipients
- **Controlled decision**: Clean-room functional handoff, independent UI/SSO design, and zero-new-package implementation
- **Evidence / validation**: `E03`, `E04`, `E08`; inspected `Directory.Packages.props`, `.ci/policy/dependency-licenses.json`, `docs/internal/legal/IP_GOVERNANCE.md`
- **Linked mitigation**: `IVSD-M007`
- **Owner / next validation**: All workstream implementers and reviewers; AFC/SSO checklist completion and `validate-dependency-license-policy.cs` pass
- **Escalation**: Qualified legal review if any third-party asset, wordlist, or new package is ever proposed

### Traceability Summary Index

| ID | Title | Lifecycle | Severity | Mitigation | Primary Owners |
| --- | --- | --- | --- | --- | --- |
| `IVSD-F001` | Truthful two-dimensional section status, review coverage, and output states | `accepted` | High | `IVSD-M001` | Presentation, Browser, Desktop, Terminal |
| `IVSD-F002` | Schema-bounded modal secret generation and zeroed transient custody | `accepted` | Critical | `IVSD-M002` | Core, Presentation, Browser, Desktop, Terminal |
| `IVSD-F003` | Accessible Material 3 tonal palettes, 4-step density, rich widgets, zoom/reflow, and RTL bidi | `accepted` | High | `IVSD-M003` | Browser, Desktop, Terminal |
| `IVSD-F004` | Consequential header scope, consent-safe Compact table, fenced Document editors, and metadata-only inspector | `accepted` | Critical | `IVSD-M004` | Presentation, Browser, Desktop, Terminal |
| `IVSD-F005` | Explicit startup readiness states and isolated public Definition Explorer | `accepted` | High | `IVSD-M005` | Browser, Desktop |
| `IVSD-F006` | `Humanized` vs. `Technical` naming transparency, visible field/choice/group metadata, and `islamu-event` `revision: 4` UI parity | `accepted` | Medium | `IVSD-M006` | Presentation, Browser, Desktop, Terminal |
| `IVSD-F007` | Clean-room AFC/SSO independence and zero-new-package MIT dependency closure | `accepted` | High | `IVSD-M007` | All owners / Architecture |

## Common Overlooked Failures And Outcomes

1. **Background render counted as "Opened"**: If incremental section pre-calculation, search-result rendering, or Compact-table row projection marks sections `Opened`, operators see `Sections opened: 16 of 16` without ever entering them (`IVSD-M001` restricts `RecordSectionOpened` to explicit active-section presentation).
2. **Stale review badge after controller switch**: An operator marks `Identity` as `Reviewed` while OIDC is off, then enables Keycloak in `Essentials`; if `Identity` stays `Reviewed`, newly required client settings go unnoticed (`IVSD-M001` tracks active field sets per section and flags `HasNewlyRelevantSettings` while downgrading `Reviewed -> Opened`).
3. **Boolean `switch` converting absence to `false`**: Rendering `widget: "switch"` as a pure binary toggle silently writes `false` over an omitted optional setting, overriding target application defaults (`IVSD-M004` uses explicit 3-state `Unset / Yes / No` segmented controls by default and retains a keyboard-accessible `Omit / Reset` action on `switch` whenever absence is distinct).
4. **Compact Table demanding premature Browser secret consent or leaking secret state**: Gating the Compact Table like full-document `ConfigurationDeveloper` would force power users to enable secret custody just to inspect public ports/URLs, while leaving `secret` cells unlocked before consent would violate `BrowserNoSecrets` (`IVSD-M004` keeps `public` cells editable without consent while locking/masking `secret` cells until `EnableSecretEntry()`).
5. **Secret candidate or document buffer surviving modal dismissal or consent revocation**: Holding a generated password or document text in an unfenced property leaves secret material accessible after revocation or concurrent edit (`IVSD-M002` and `IVSD-M004` enforce `DefinitionSecretGeneratorSession` and `DefinitionDocumentSession` `OperationGeneration` fencing and synchronous zeroization).
6. **Corrupted LTR technical tokens in Arabic (`rtl`) mode**: Displaying `env.PUBLIC_BASE_URL` or `/ai/provider` without `dir="ltr"` flips punctuation order in RTL (`IVSD-M003` enforces explicit LTR bidi isolation on technical identifiers).

## Recommendations

1. **Complete Workstream 1 (`definition-schema-redesign` `rev-2`) first, then implement shared `Presentation` projections in Phase 1 of this workstream** (`SectionStatusProjection`, `WorkspaceDensity`, `WorkspaceNamingMode`, `WorkspaceLayoutMode`, `CompactPropertyRow`, enriched `DefinitionEditorPresentation`, `DefinitionFormSectionGroup`, and `DefinitionSecretGeneratorSession`) so Browser, Desktop, and Terminal consume one authoritative model.
   - *Rejected alternative*: Computing visited badges, humanization fallbacks, or compact-row consent rules independently inside Razor, Avalonia, and Terminal.Gui (rejected: causes semantic and security drift, as seen in Browser's partial `ConfigurationDiagnosticText.Title`).
2. **Host the KeePass-style Secret Generator in first-party modal dialogs (`MudDialog` in Browser, `Window.ShowDialog` in Desktop, `Dialog` in Terminal) over `MudBlazor 9.7.0`, `Avalonia 12.1.3`, and `Terminal.Gui 2.5.0` without adding `CodeBeam.MudBlazor.Extensions`**.
   - *Rejected alternative*: Adding `CodeBeam.MudBlazor.Extensions` or `MudPopover` (rejected: unnecessary third-party JS/trimming risk and `320px` / `400%` zoom clipping).
3. **Provide a Two-Tier Power-User Experience**: a **consent-safe Compact Property Table** (`Humanized ↔ Technical` wire keys/tokens, `<Virtualize>`/paging, instant metadata filter) across all clients **plus** `OperationGeneration`-fenced **`DefinitionDocumentSession` editors** in Desktop (`DocumentEditorView`) and Terminal (`TerminalDocumentDialog`), complementing Browser's secret-consented `ConfigurationDeveloper` view.
   - *Rejected alternative*: Exposing unconsented raw full-document editing in Browser (rejected: violates `BrowserNoSecrets` custody contract).
4. **Deliver cohesive Material Design 3 tonal tokens and four densities (`Spacious`, `Comfortable`, `Compact`, `Dense`) across Browser and Desktop while keeping TUI responsive by character dimensions**, preserving OS high-contrast precedence, `>= 24×24` CSS px hit minimums in `Dense`, and LTR bidi isolation on wire tokens in Arabic.

## Stakeholders

- **Self-hosting community operators and system administrators**: Primary users preparing `.env`, JSON, and YAML artifacts locally through guided sections, human-readable titles, inline units/explanations, and truthful review/export states.
- **Power-user developers and SREs**: Engineers scanning 100–600+ settings via `Technical` wire keys (`SCREAMING_SNAKE_CASE`, `OPENAI_COMPATIBLE`), Compact property tables, and Desktop/Terminal document-session buffers.
- **Accessibility-reliant and multilingual operators**: Users depending on keyboard navigation, screen readers, OS high-contrast modes, 200%–400% zoom, `320px` narrow viewports, or Arabic (`ar`) RTL layout with intact LTR technical identifiers.
- **Downstream community members and upstream open-source authors**: People whose personal data depends on strong secrets and reviewed defaults, and upstream authors whose MIT/GPL boundaries are respected via clean-room design.

## I-VSD Principles And Domains

- **Amanah (Trust & Custody)**: Governs transient browser secret consent, consent-safe Compact Table `IsSecretLocked` gating, `DefinitionDocumentSession` generation fencing, modal secret-generator zeroization, pre-entry readiness gating, and honest export custody messaging (`IVSD-F001`, `IVSD-F002`, `IVSD-F004`, `IVSD-F005`).
- **Sidq & Bayan (Truthfulness & Clear Explanation)**: Governs separating configuration validity from review coverage, non-materializing `"If omitted: <defaultText>"` guidance, `Humanized ↔ Technical` wire transparency, factual Store source labels, and explicit empty-section reasons (`IVSD-F001`, `IVSD-F005`, `IVSD-F006`).
- **Adl (Justice & Equitable Access)**: Governs contrast-verified Material 3 palettes, safe `Dense` target minimums, keyboard/focus restoration across all widgets, reflow, and English/Arabic RTL parity with LTR token isolation (`IVSD-F003`).
- **La Darar (Non-Harm)**: Governs enforcing schema cryptographic floors during interactive secret customization and preserving exact tri-state/switch/collection answer semantics across view switches (`IVSD-F002`, `IVSD-F004`).
- **Ihsan (Responsible Craftsmanship)**: Governs calm workspace hierarchy, visible inline Desktop/Terminal field guidance, and end-to-end `islamu-event` `revision: 4` usability (`IVSD-F003`, `IVSD-F006`).
- **Haqq al-'Ibad (Rights of Others)**: Governs clean-room AFC/SSO independence and zero-new-package MIT dependency discipline (`IVSD-F007`).
- **Non-applicable domains**: Monetization/riba, algorithmic feed ranking, AI content generation, user-generated content moderation, and multi-tenant server persistence do not apply to this offline single-operator configuration utility.

## Validation Gaps

1. **No runtime execution in planning (plus host SDK prerequisite noted)**: All codebase evidence (`E01`–`E08`) was gathered via static inspection at `d7b2bfe` (`global.json` pinned SDK `10.0.302` at `d7b2bfe` while `/usr/share/dotnet/sdk` has `10.0.403`; an uncommitted working-tree bump to `10.0.403`, observed 2026-11-08 and made outside this assessment, must be confirmed before Workstream 1 execution); TUnit, bUnit, Avalonia Headless, `NativeHost` (30 X11 checks in `en`/`ar`), Terminal, and Architecture suites execute during implementation phases.
2. **Browser AOT interactive timing budgets**: `MeasureInteractiveWork.cs` budgets (`< 50 ms` / `< 200 ms` feedback, `<= 100 ms` / `<= 400 ms` content `p95` at `1x`/`4x` CPU across 9 journeys + 5 large collection fixtures + `CompactTable` virtualization) must be measured on actual published Release AOT payloads in Phase 7.
3. **Real assistive technology and physical devices**: Automated ARIA/AutomationPeer tests and Linux X11 `NativeHost` checks do not substitute for manual NVDA/JAWS/VoiceOver/Orca screen-reader testing, Windows/macOS native host runs, or formative operator usability sessions.

## Escalation Needed

- **Scholarly escalation**: None required.
- **Specialist escalation before external claims**:
  - Do not claim formal WCAG 2.2 AA certification or cross-platform screen-reader qualification until specialist assistive-technology testing completes on Browser, Windows/macOS/Linux Desktop, and Terminal.
  - Escalate to legal/IP review if future scope proposes bundling a third-party passphrase wordlist dataset or adding any dependency outside `.ci/policy/dependency-licenses.json`.

## Evidence Reviewed

- `E01`: `src/Configurator.Core/Secrets/{SecretGenerator,SecretValue,SecretValueValidator}.cs`, `src/Configurator.Core/Definitions/{DefinitionSecret*,DefinitionSection,DefinitionExperience,DefinitionNode,FieldDefinition}.cs` (`d7b2bfe`, plus Workstream 1 Core contract in `dev/active/definition-schema-redesign/`).
- `E02`: `src/Configurator.Presentation/Form/{DefinitionFormModel,DefinitionViewState,DefinitionFormFacts,DefinitionFormProjection,DefinitionFormIndex,DefinitionEditorPresentation,DefinitionConfigurationContext}.cs`, `Discovery/DefinitionDiscovery.cs`, `Secrets/DefinitionSecretOperation.cs`, `Document/DefinitionDocumentSession.cs`, `Import/DefinitionImportState.cs` (`d7b2bfe`).
- `E03`: `src/Configurator.Browser/Configurator.Browser.csproj`, `Layout/{MainLayout,NavMenu,WorkspaceSearch,Footer}.razor*`, `Components/Configuration/{ConfigurationEditor,ConfigurationConfigurePanel,ConfigurationDiagnosticText,ConfigurationField,ConfigurationSecretActions,ConfigurationInspector,ConfigurationReviewPanel,ConfigurationFilesPanel,ConfigurationDeveloper}.razor*`, `Pages/{Catalogue,StoreProject,Open,Launch,Help}.razor`, `Services/{AppearanceState,AppearanceThemeService,BrowserOfflinePolicy}.cs`, `wwwroot/css/{layers,tokens,mudblazor-overrides,components}.css` (`d7b2bfe`).
- `E04`: `src/Configurator.Desktop/Configurator.Desktop.csproj`, `App.axaml`, `Resources/{ThemePalettes,DesignTokens,DesktopTheme}.*`, `Views/{MainWindow,ConfigurationWorkspaceView,ConfigurationDiscoveryView,FieldTemplates,SecretActionsView,ArtifactPublicationView,ImportComparisonView,DefinitionLoaderView}.axaml`, `ViewModels/{DesktopShellViewModel,DesktopNavigationViewModel,FormWorkspaceViewModel,ArtifactPublisherViewModel}.cs`, `ViewModels/Fields/{FieldViewModel,StringFieldViewModel,BooleanFieldViewModel,ListFieldViewModel}.cs`, `tests/Configurator.Desktop.NativeHost/Program.cs` (`d7b2bfe`).
- `E05`: `src/Configurator.Terminal/Configurator.Terminal.csproj`, `Views/TerminalWindow*.cs`, `Controls/{SecretTextField,TerminalSecretBuffer,TerminalFieldEditor,TerminalRootEditor}.cs` (`d7b2bfe`).
- `E06`: `tests/Configurator.Browser.Tests/Configuration/BrowserEventManifestWorkflowTests.cs`, `eng/configurator/{MeasureInteractiveWork,BuildInteractiveWorkFixtures,ValidateDefinitionDocumentation}.cs` (`d7b2bfe`).
- `E07`: `dev/report/ISLAMU_Configurator_UI_UX_Redesign_Review.md`, `docs/internal/{PRESENTATION_STATE,BROWSER_UI,DESKTOP_CLIENT,TERMINAL_CLIENT,LAUNCH_CONTRACT}.md`, `docs/internal/adr/{ADR-browser-workspace-presentation,ADR-browser-developer-editing}.md`.
- `E08`: `Directory.Packages.props`, `.ci/policy/dependency-licenses.json`, `.ci/scripts/validate-dependency-license-policy.cs`, `docs/internal/legal/IP_GOVERNANCE.md`, plus clean-room public documentation specifications consulted on `2026-11-07` and `2026-11-08` (Material Design 3, MudBlazor v9, Avalonia 12, Terminal.Gui v2, W3C WCAG 2.2 AA & WAI-ARIA APG, Google Account / Dank Material Shell `dux` / KeePassXC / Coolify / Lexicon Garden / GOV.UK / VS Code Settings UI / RJSF v5 / Home Assistant Selectors / Backstage / NixOS `defaultText` UI concepts).

## Missing Evidence

- Executed test runs and compiler outputs for the proposed changes (deferred by contract to implementation phases after Workstream 1 completes).
- Post-redesign Release WASM AOT bundle sizes and `MeasureInteractiveWork.cs` timing cohorts on an idle host.
- Real assistive-technology (Orca, NVDA, VoiceOver) and Windows/macOS Desktop rendering observations.
- Formative human operator task-completion observations (`dev/report/ISLAMU_Configurator_UI_UX_Redesign_Review.md` §15).

## Context Inventory

- **Repository baseline**: `main` at `d7b2bfe`.
- **Sequential two-workstream execution order (per explicit user instruction, 2026-11-08)**:
  1. **FIRST — `dev/active/definition-schema-redesign/` (`rev-2`)**: Non-UI `Configurator.Core` schema/humanizer/validator/compiler upgrades, `schemas/*.schema.json`, CLI `definition answer-schema` + `definition lint --quality`, `examples/*` migration, and `islamu-event` canonical `revision: 4` (`store/islamu-event/releases/4.json`).
  2. **SECOND — `dev/active/configuration-workspace-redesign/` (`rev-2`, this assessment)**: Full interactive `Presentation`, `Browser`, `Desktop`, and `Terminal` redesign consuming Workstream 1's Core models and `islamu-event` `revision: 4`.
- **Related prior workstreams**: `artifact-import-comparison`, `deployment-aware-configuration`, `schema-driven-secrets`, `codebase-architecture-and-conventions`, and initial `avalonia-desktop-ui-ux-redesign` (`D01`–`D30`) are committed on `main`; `browser-ux-simplification` and `enterprise-architecture-quality` retain preserved render-gate invariants and open timing notes in `dev/active/`.

## Planning Handoff
- Workstream: configuration-workspace-redesign
- Status: current
- Reviewed input: `configuration-workspace-redesign` plan/context/tasks triad revision 2 (`2026-11-08`, sequenced after `definition-schema-redesign` rev 2)
- Findings and mitigations: `IVSD-F001 -> IVSD-M001`, `IVSD-F002 -> IVSD-M002`, `IVSD-F003 -> IVSD-M003`, `IVSD-F004 -> IVSD-M004`, `IVSD-F005 -> IVSD-M005`, `IVSD-F006 -> IVSD-M006`, `IVSD-F007 -> IVSD-M007`
- Required plan mappings:
  - `IVSD-F001 / IVSD-M001` -> Scenarios `S01`–`S03`, `S12`; Phases 1, 2, 4, 5, 6 (`TASK-1.3`, `TASK-1.4`, `TASK-2.2`, `TASK-4.2`, `TASK-5.2`, `TASK-6.1`)
  - `IVSD-F002 / IVSD-M002` -> Scenarios `S08`, `S09`; Phases 1, 3, 5, 6 (`TASK-1.1`, `TASK-1.2`, `TASK-3.1`, `TASK-3.3`, `TASK-5.3`, `TASK-6.2`)
  - `IVSD-F003 / IVSD-M003` -> Scenarios `S04`, `S14`, `S17`, `S18`; Phases 1, 2, 3, 5, 6, 7 (`TASK-1.4`, `TASK-2.1`, `TASK-2.2`, `TASK-3.2`, `TASK-5.1`, `TASK-5.2`, `TASK-6.1`, `TASK-7.2`)
  - `IVSD-F004 / IVSD-M004` -> Scenarios `S05`, `S07`, `S10`, `S11`, `S18`, `S19`; Phases 1, 2, 3, 4, 5, 6 (`TASK-1.3`, `TASK-1.4`, `TASK-2.2`, `TASK-3.2`, `TASK-4.1`, `TASK-5.2`, `TASK-6.1`)
  - `IVSD-F005 / IVSD-M005` -> Scenarios `S06`, `S13`; Phases 2, 4, 5 (`TASK-2.3`, `TASK-4.3`, `TASK-5.2`)
  - `IVSD-F006 / IVSD-M006` -> Scenarios `S15`, `S17`, `S18`; Phases 1, 2, 3, 5, 6, 7 (`TASK-1.4`, `TASK-2.2`, `TASK-3.2`, `TASK-5.2`, `TASK-6.1`, `TASK-7.1`)
  - `IVSD-F007 / IVSD-M007` -> Scenario `S16`; All phases, Clean-Room AFC/SSO gate, and Phase 7 (`TASK-7.2`, `VERIFY-7`)
- Escalations required before: release (specialist AT/accessibility qualification before WCAG conformance claims; security/legal review only if future scope adds wordlists or new third-party packages)
- Refresh triggers: Any change that (1) merges configuration validity with review coverage or blocks export on unvisited optional sections, (2) allows secret generation below schema/128-bit floors or retains unzeroed secrets across revocation, (3) unlocks secret cells in Browser `CompactTable` without workspace secret consent or drops `OperationGeneration` fencing on `DefinitionDocumentSession`, (4) materializes `defaultText` into answers or converts unset `switch` booleans to `false` without an `Omit` path, (5) adds `CodeBeam.MudBlazor.Extensions` or another third-party dependency, or (6) exposes private answers/keys in Compact table search/sort, inspector views, or public explorer URLs.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
| --- | --- | --- | --- | --- |
| 2026-11-07 | `none` | `draft` | Integrated `/implementation-plan` intake for `configuration-workspace-redesign` | `E01`–`E08` repository and clean-room evidence packet |
| 2026-11-07 | `draft` | `current` (`plan-aligned`, `r1`) | Completed triad (`rev-1`) revalidated against `IVSD-F001`..`IVSD-F007` and `IVSD-M001`..`IVSD-M007` | Full scenario and Red/Green task mappings verified in Plan §9 and Tasks Phases 1–7 |
| 2026-11-08 | `current` (`r1`) | `current` (`plan-aligned`, `r2`) | User merged all interactive UI/Dual-UX/Rich-Widget/Compact-Table/Document-Editor aspects from `definition-schema-redesign` into `configuration-workspace-redesign` (sequenced second after non-UI `definition-schema-redesign` `rev-2`) | Scenarios `S01`–`S19` and Phases 1–7 (`rev-2`) revalidated with zero data loss alongside sister report `i-vsd-definition-schema-redesign.md` (`schema-redesign-ivsd-r2`) |
