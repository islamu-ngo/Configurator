# Browser UI/UX redesign - Mandatory follow-up slices

Last Updated: 2026-10-23 Europe/Brussels
Status: required redesign backlog; not implemented, approved or qualified.
Owner: Browser/Presentation maintainers; actual assignment before activation.
Origin: Senior CTO split of browser-ui-ux-redesign revision 1 at Configurator e10037e7cd79f18f2625c03e39a241a3a9012f2c.
Primary slice: [editor foundation decision](../../docs/internal/adr/ADR-browser-workspace-presentation.md), from the locally tracked revision-2 execution plan.

## Why the complete redesign is split

Revision 1 combined 28 implementation tasks across nine phases, including independently useful journeys, tables, palettes, review/export and entry routes. The primary slice fixes the immediate search/field/help experience and qualifies the Mud conversion before multiplying its render paths. These follow-ups remain mandatory for the user's complete whole-app redesign. Deferred means another reviewable PR, not optional scope, accepted risk or permission to call the UI/UX program finished. No compatibility requirement is introduced.

Original phase/task IDs, declarative packets and proposed paths below are retained planning evidence, NOT the active execution sequence or authorization. Re-baseline each slice into its own current triad and I-VSD-aligned task mapping before execution. Supersede obsolete serialized dependencies: appearance and entry routes do not wait for property tables. Native change identifiers were allocated, not reserved; check uniqueness and replace materially changed packets before committing. Preserve exact unaffected requirements rather than executing this whole archive as one mega-PR.

The primary shared-detail, application-bar search and compact field/help
implementation is committed. Its qualification remains open; this statement
does not promote any dependent slice or certify the whole program. Use the
[runtime evidence and limits](../../docs/internal/OPERATIONS.md#browser-custody-and-offline-verification)
and the workspace decision above rather than relying on ignored session files.

## Required slices, owners and promotion gates

| Slice | Original tasks | Depends on | Acceptance / I-VSD |
| --- | --- | --- | --- |
| J - Journeys and focused stages | 4.1-4.3 | Qualified primary | Guided/Sections/continuous are genuinely different; existing task profile separate from layout/detail; current stage only; IVSD-F003/F004/M003/M004. |
| T - Property table and collection representation | 5.1-5.3 | J | All six journey/layout combinations preserve absence, raw drafts, row IDs and output; masked summaries and accessible mobile labels; IVSD-F002/F003/M002/M003. |
| A - Local appearance and customization | 6.1-6.3 | Qualified primary; show J/T selectors only once delivered | Keyboard preset/radio controls; bounded local preferences; independent light/dark/forced-color contrast proof; IVSD-F001/F002/F006/M001/M002/M006. |
| R - Review, export and safe departure | 7.1-7.3 | J; independent of T/A | Focused relevant review, precise correction/return, deliberate export, cancel/late-dialog integration; IVSD-F003/F004/F005/M003/M004/M005. |
| E - Entry routes and contextual help/offline composition | 8.1-8.3,9.1 | Qualified primary; independent of T/A | Home/Store/Open/Launch loading/empty/rejected recovery, accurate JSON/YAML copy, help without departure, unchanged offline/consent authority; IVSD-F005/F006/M005/M006. |
| Program acceptance | Original 9.2-9.4 | All delivered slices | Final source-bound whole-app visual/custody/performance and stakeholder evidence; revalidate all IVSD-F001-F006 and IVSD-M001-M006. |

The active plan retains S01-S28 as stable program identifiers and adds S29-S34. Original findings UX-01-UX-26 remain required. Each slice needs public/internal documentation parity, a phase-owned literal commit packet, nonempty owner checks and real rendered qualification on its final candidate; no intermediate browser sweeps or arbitrary broad suite repetition.

## Additional CTO constraints on archived packets

- **Worst Break:** revoked secrets or obsolete output revived by remount/stale completion. Reuse primary Phase Red 3.R and extend it through every new journey/layout before implementation. Use exact completion gates, real internal owners and external boundaries only; no sleeps, polling, private-field manipulation or Received-count assertions.
- **J Red:** establish the missing journey/stage contract first, then implement. Continue may explain missing ordinary answers but MUST NOT trap Back/revisit or secret-deferred draft work. Authored profile changes never change input defaults or grant consent.
- **T Red:** split original 5.1 into failing form/table round-trip and row-deletion specifications before renderer Green. A property-table is new product work, not something already supplied by Event. Only the active representation mounts controls.
- **A Red:** split original 6.1 into admission/generation tests before parser/service/JS changes. Primary AppearanceState remains mode/direction-only. Broader closed schema is a deliberate later change; reset obsolete preferences if simpler, never add aliases or legacy parsers. Contrast interpolation percentages below are candidates, NOT verified engineering facts. Independently test required pairs and rendered selected/focus/error/disabled states; preset cards use keyboard radio/button semantics, not clickable paper. Persisting per-definition IDs, queries, consent, private values or output is forbidden.
- **R Red:** BrowserNavigationStateTests.LateConfirmationCannotDiscardReplacementEditor already exists. Do not add a duplicate service test or claim that guard is broken. Introduce a controlled component-level themed-dialog race and preserve the service's generation ordering. No hidden secret undo copy.
- **E:** Event API persistence, HAL, BFF, auth, docking/analytics and server palette generation are not Configurator conventions to import. No hosting/deployment change is required; the user's selected static Coolify hosting remains a separate authority.
- Qualify each delivered slice; final program acceptance additionally covers every promised journey/layout and route. Use pairwise theme/density/direction cases where independent, plus adversarial combinations. Keep the existing latency budgets and samples unchanged. Missing physical-device/speech/stakeholder evidence stays explicit; no fabricated best-UX claim.
- Revalidate exact revised plan/tasks/backlog I-VSD bindings before promotion. The revision-1 assessment is stale for this split. CTO alignment never grants user execution or commit approval.

## Retained original scenarios

These are full-program obligations, not all acceptance gates of the primary PR. New active S29-S34 and precise primary boundaries are in the revision-2 plan.

### Original S01-S28

All requirements below are observable. `Snn` identifies a scenario for tasks and I-VSD mappings.

| ID | Requirement and WHEN / THEN scenario |
| --- | --- |
| S01 | The interface SHALL expose setting search at the top. WHEN an active workspace opens on desktop, THEN search is visible without expansion; on phone a named top search action opens it. |
| S02 | Search SHALL cover complete admitted metadata, not answers. WHEN a query matches a field outside the current section/page, THEN the result is reachable with its section and state; searching a synthetic answer yields no answer match or persisted query. |
| S03 | Search/correction SHALL reach the exact destination. WHEN a result refers to a nested row, hidden optional field, inactive field or denied secret, THEN the correct field/ancestor/controller/consent action is revealed and focused without enabling settings or secrets; returning to search retains its query/facets in the same shared owner. |
| S04 | Help SHALL not move the form. WHEN short help opens by hover/focus, THEN neighboring controls keep their geometry; click/tap opens rich help, Escape dismisses, and links remain operable without tooltip focus traps. |
| S05 | Essential instructions/errors SHALL remain accessible. WHEN help is closed or a field is invalid, THEN its label, units/essential consequence and corrective error remain available without hover. |
| S06 | Comfortable/Compact SHALL change presentation only. WHEN density changes, THEN gaps/control density change consistently without clipping text, shrinking touch affordances below the accepted design or altering values. On the standard short-label fixture at 1440x900, the first editable question SHALL be visible without scrolling; repeated keys/omit buttons/help paragraphs SHALL not dominate the initial viewport. |
| S07 | Guided SHALL present meaningful steps. WHEN a user continues/revisits, THEN only the chosen step's editor is presented, position is clear, required readiness is accurate and deferred secrets do not become complete. Back does not validate destructively. |
| S08 | Sections SHALL permit direct navigation. WHEN a section is chosen, THEN its title/purpose and fields appear with concise status; unchanged/invalid drafts survive navigation. |
| S09 | All sections SHALL be a genuine continuous form. WHEN selected, THEN all applicable groups can be reached by document scrolling and section links, without a selected-section gate or Load more substitution. |
| S10 | Form/Property table SHALL edit the same configuration. WHEN layout changes with valid and invalid values, THEN state, provenance, row identities, consent and valid prepared output are preserved. |
| S11 | Detail level SHALL be independent of journey/layout. WHEN Essential/Standard/All active changes, THEN optional disclosure follows the selected level while required/error correction remains reachable and inactive settings are not enabled. |
| S12 | Primitive editing SHALL preserve meaning. WHEN absence, false, zero, an allowed empty enum/string or incomplete number is entered, THEN each remains distinct and invalid draft text survives blur/remount until corrected or deliberately omitted. |
| S13 | Collections SHALL preserve identity. WHEN a preceding row is removed or display ordering changes, THEN surviving rows retain their values, drafts and correction targets; displayed sorting does not reorder output. |
| S14 | View changes SHALL preserve private-state authority. WHEN rapid mode changes overlap pending input, import or preparation, THEN only the current workspace can accept completion; view-only changes do not become edits. |
| S15 | Consent SHALL remain per workspace. WHEN secrets are disabled or revoked, THEN denied inputs are absent, secret leaves/previews/output clear before asynchronous retirement, and late events/reconsent cannot restore them. |
| S16 | Personalization SHALL persist only bounded UI choices. WHEN unknown/duplicate/oversized/answer-shaped preference data is supplied, THEN it is rejected; storage denial leaves editing usable and no answers, queries, filenames or consent are written. |
| S17 | Appearance SHALL update without reload. WHEN a preset, mode, direction or valid custom accent is applied, THEN the current workspace remains mounted, values/output survive, and system mode follows system preference changes. |
| S18 | Theme customization SHALL not produce supported unreadable states. WHEN custom neutral/brand colors fail contrast checks, THEN preview explains the issue and Apply is unavailable; high-contrast modes override incompatible custom colors. |
| S19 | Review SHALL summarize relevant work. WHEN opened, THEN grouped origin/readiness/correction states are visible without leaking values; Change and Return preserve position and target the correct control. |
| S20 | Progress SHALL distinguish location/readiness/output/acceptance. WHEN conditional requirements change or required count is zero, THEN the message remains truthful; visiting a step never implies validated output or target acceptance. |
| S21 | Export SHALL be deliberate and truthful. WHEN complete/draft preparation is requested, THEN ordinary errors block both where appropriate, deferred-secret obligations are explicit, and reveal/copy/download require explicit actions. |
| S22 | Destruction/departure SHALL be understandable. WHEN clear/discard/internal departure is requested, THEN a themed confirmation explains loss; cancel preserves work, confirm fences old work, and no hidden undo copy retains secrets. |
| S23 | Entry routes SHALL explain next actions. WHEN Home/Store/project/Open loads, fails, has no results or admits a source, THEN concise identity and recovery are visible; JSON/YAML/local/public-link capabilities remain accurate. |
| S24 | Accessibility SHALL cover the whole flow. WHEN used with keyboard, touch, 320px reflow, actual zoom, RTL or assistive technology, THEN required search/help/edit/review/export paths remain operable with visible focus and meaningful labels/status. |
| S25 | Feedback SHALL remain responsive. WHEN existing or new control journeys are measured on the frozen reference matrix, THEN unchanged existing budgets hold; new mode-switch final-content p95 is <=100ms at 1x and <=400ms at 4x, with max feedback <50ms/<200ms and no warmed 1x long task >50ms. |
| S26 | Offline/loss feedback SHALL match custody. WHEN offline preparation is incomplete, an update waits, or the user closes/leaves, THEN the interface describes what is retained/lost; no update reloads a live workspace or emits answer-bearing requests/cache/messages. |
| S27 | Customization SHALL be discoverable and reversible. WHEN opened from a live form, THEN journey/layout/detail/density/width/key/help choices are explained and immediately usable; Reset presentation leaves configuration unchanged. |
| S28 | Authored grouping SHALL remain authoritative. WHEN descriptions or nested object groups exist, THEN readable section/subgroup context is presented without invented target rules, arbitrary executable metadata or a new schema requirement. |


## Retained target architecture for later slices

### 5.6 Three journeys and two layouts

`ConfigurationJourney` composes:

- Guided: bounded current section with step progress and Back/Continue; revisit is non-linear, validation explains incomplete state without blocking secret-deferred work.
- Sections: direct navigation and one mounted section.
- All sections: all applicable sections in one document with real headings/anchors and document scrolling. No selected-section gate, page substitute or silent threshold downgrade.

`ConfigurationPropertyTable` uses MudTable with local templated rows, grouped labels/state/actions and the same field adapter. Start with one active row editor plus compact read-only value-state summaries; sensitive values remain masked. Complex objects open a focused side editor; primitive lists stay compact and homogeneous object lists get explicit row columns. No spreadsheet keyboard/paste-range feature.

Form and property-table layouts work within each journey. Mobile projects table rows into labeled stacked rows while preserving the preferred desktop layout. Display sorting never changes output order.

Continuous form starts with full active-section mounting and focused render boundaries. Large-form acceptance is mandatory. Only if profiling proves necessary may section-level deferred mounting be introduced, with anchored section placeholders, on-demand search/focus mounting and no lost keyboard reachability. That is a renderer optimization within this contract, not permission to ship a partial continuous mode.

### 5.7 Local appearance and customization

Extend `AppearanceState` with bounded global preferences: preset `neutral|teal|custom`; optional 7-character hex neutral and brand seeds per light/dark palette; density `comfortable|compact`; preferred journey `guided|sections|continuous`; layout `form|table`; detail `essential|standard|all`; width `focused|wide`; showKeys boolean; help `contextual|inspector`; textScale `normal|large`; motion `system|reduced`. Preserve themeMode/direction.

Use the existing appearance key with a closed schema and a 2048-character maximum, depth 2, no free-form names, unknown properties or duplicates. Omitted new optional properties have normal defaults; this is one schema, not separate legacy parsers or key aliases. Serialize one canonical bounded shape. No per-definition preference keys or persisted row/field IDs.

`AppearancePalette` (new pure Browser helper) starts from reviewed neutral/teal base palettes. Like Event's interface, custom controls expose neutral and brand colors, but generation is entirely local. Derive neutral surfaces by fixed sRGB interpolation toward white for light mode and black for dark mode; keep semantic warning/error/success colors controlled. Start with 96%/99% white canvas/surface and 90%/84% black canvas/surface, respectively. Choose dark/light text and control boundaries to meet the contrast roles; reject a result that cannot satisfy all required pairs rather than silently applying it. Validate brand-on-surface text at 4.5:1, primary action text at 4.5:1, and required control/focus indicators at 3:1 using linearized sRGB relative luminance. High contrast ignores custom seeds. Test fixed independent color-pair examples, not expectations calculated by the production helper.

Show a synthetic preview of fields, selected navigation, actions, error and focus states, not live answer content. This provides actual neutral/brand customization without importing Event's server palette generator, adding a color-library dependency or allowing arbitrary CSS/theme objects.

A `WorkspaceCustomization` drawer exposes presentation choices immediately, with Reset presentation; `AppearanceSettings` exposes mode/presets/accent/direction/text/motion. Both remain inside the active shell without route departure.

### 5.8 Review/export and loss

Render Configure, Review and Export as actual distinct stage panels over one editor owner. Do not leave the full editor above Review. Review consumes value-free entries, groups by section and filters attention/changed/deferred; it does not read answer values. Technical bindings live in the inspector. Correction carries origin stage/position and returns predictably.

The Export panel retains existing preparation/export methods. Clear/discard is separated from primary preparation, confirmed with a standard dialog. Internal departure uses the same dialog conventions while `NavigationLock` retains external unload protection. Capture the owner/generation around a pending dialog; a late confirmation must not discard a replacement editor.

### 5.9 Route simplification and help

Home reduces hero/preamble, Store has concise filters/cards/recovery, project revision detail becomes a summary plus details, Open offers local/public source tabs with preserved draft URL and admission semantics. Update JSON/YAML copy consistently.

Help is task-oriented, accessible inside the workspace inspector and as a route. Offline/update readiness uses existing policy; no network probes while answers are live. Short local-only/unsaved status is always clear without paragraphs at every field.


## Retained deferred task packets

The common native packet protocol is in the active tasks file; each later approved triad MUST inline its own complete inspection/staging/path-limited commit/verification recipe. Do not execute these archived packets directly. Native scope registry, hook/preflight and material-divergence rules still apply.

## Phase 4: Real configuration journeys

Dependencies: phase 3. Effort: L.

- [ ] **4.1 Specify mode/state transitions** in existing experience/navigation/workflow tests; verify repeated switching with invalid drafts, prepared output and denied/consented secrets preserves the same form and rejects stale callbacks (S07-S11,S14-S15).
- [ ] **4.2 Build actual journeys** with NEW `Services/WorkspaceViewState.cs` and `ConfigurationJourney.razor`/CSS. Adapt editor/navigation/section projection: Guided current step, Sections direct selection, genuine continuous sections, independent detail, section descriptions and truthful progress (S07-S11,S20,S28). Only one active rendering path mounts editors.
- [ ] **4.3 Separate Configure/Review/Export panels** while retaining editor lifetime; verify stage selection is no longer document scrolling past unrelated editors, and correction/return remains functional. Update DESIGN, BROWSER_UI, LAUNCH_CONTRACT, public guide and fragment (S19-S21).
- [ ] **4.V Verify phase 4** with Browser commands; require cross-journey state/semantics coverage, not just three selector values.
- [ ] **4.C Commit phase 4 when authorized**; verify every promised journey is implemented, with large-form runtime qualification still explicit.

```bash
subject='feat(browser): offer guided section and continuous editing journeys'
body='One selected-section view does not suit every operator. Add independent journey and detail controls with real continuous sections and focused workflow panels while keeping one pinned form and truthful readiness.'
footer='Change-Id: CHG-01M5MBH1S6PE6EJ60J1YMPN1D0'
owned=(
  src/Configurator.Browser/Services/WorkspaceViewState.cs
  src/Configurator.Browser/Components/Configuration/ConfigurationJourney.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationJourney.razor.css
  src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs
  src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.css
  src/Configurator.Browser/Components/Configuration/ConfigurationNavigation.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationNavigation.razor.css
  src/Configurator.Browser/Components/Configuration/ConfigurationSection.cs
  tests/Configurator.Browser.Tests/BrowserExperienceWorkflowTests.cs
  tests/Configurator.Browser.Tests/BrowserConfigurationNavigationTests.cs
  tests/Configurator.Browser.Tests/BrowserConfigurationWorkflowTests.cs
  docs/public/browser.md
  docs/internal/BROWSER_UI.md
  docs/internal/LAUNCH_CONTRACT.md
  src/Configurator.Browser/DESIGN.md
  docs/internal/releases/changes/CHG-01M5MBH1S6PE6EJ60J1YMPN1D0.yaml
)
```

Changelog: public feature; nonbreaking; message override: Not overridden.

## Phase 5: Property tables and collections

Dependencies: phase 4. Effort: L.

- [ ] **5.1 Add property-table editing** with NEW `ConfigurationPropertyTable.razor`/CSS and NEW `BrowserPropertyTableTests.cs`; wire it to the same journey/field owner. Verify form/table round trips preserve invalid drafts, absence, provenance, output and masked state; no development-only DataGrid enum/API or second answer model (S10,S12-S15).
- [ ] **5.2 Improve collection rows and mobile projection** in table/field/inspector files; verify stable surviving row IDs, exact error targeting, complex-row editor focus and a labeled narrow-screen layout without output reordering (S03,S13,S24).
- [ ] **5.3 Document the layouts and fragment**; verify all three journeys support both layouts, including clear mobile adaptation and no spreadsheet/bulk-paste promise.
- [ ] **5.V Verify phase 5** with Browser commands; require table/collection/experience behavior to pass.
- [ ] **5.C Commit phase 5 when authorized**; verify the table is a complete alternate view, not a separate configuration workflow.

```bash
subject='feat(browser): edit configuration through compact property tables'
body='Operators need a dense overview without losing typed semantics. Add a property-table renderer and compact collection editing over the shared form, with stable row identities and a readable mobile projection.'
footer='Change-Id: CHG-01M5MBH23C0KVFGAAX8WVW8J93'
owned=(
  src/Configurator.Browser/Components/Configuration/ConfigurationPropertyTable.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationPropertyTable.razor.css
  src/Configurator.Browser/Components/Configuration/ConfigurationJourney.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationField.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationField.razor.cs
  src/Configurator.Browser/Components/Configuration/ConfigurationField.razor.css
  src/Configurator.Browser/Components/Configuration/ConfigurationInspector.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor
  tests/Configurator.Browser.Tests/BrowserPropertyTableTests.cs
  tests/Configurator.Browser.Tests/BrowserCollectionWorkflowTests.cs
  tests/Configurator.Browser.Tests/BrowserExperienceWorkflowTests.cs
  docs/public/browser.md
  docs/internal/BROWSER_UI.md
  src/Configurator.Browser/DESIGN.md
  docs/internal/releases/changes/CHG-01M5MBH23C0KVFGAAX8WVW8J93.yaml
)
```

Changelog: public feature; nonbreaking; message override: Not overridden.

## Phase 6: Appearance and customization

Dependencies: phase 5. Effort: L.

- [ ] **6.1 Specify and implement preference admission** in existing AppearanceState/service/JS and tests: reject unknown/duplicate/nested/oversized values, restrict enum/hex choices, retain normal defaults for optional properties and no answer-bearing sink. Verify late reads cannot overwrite a newer choice and storage denial keeps the workspace usable (S16-S17).
- [ ] **6.2 Deliver local theme management** with NEW `Services/AppearancePalette.cs` and `Components/Appearance/AppearanceSettings.razor`/CSS; update ThemeQuickSwitcher and theme service. Verify neutral/teal/custom previews, separate neutral/brand colors, five modes, OS changes, contrast rejection against independently specified pairs, direction/text/motion and reset without remote services or reload (S17-S18,S24).
- [ ] **6.3 Deliver workspace customization** with NEW `Configuration/WorkspaceCustomization.razor`/CSS; wire editor and view state to global preferences. Verify journey/layout/detail/density/width/key/help choices update immediately and resetting presentation preserves configuration/output (S10,S16,S27). Update browser docs and fragment.
- [ ] **6.V Verify phase 6** with Browser commands; require admission, palette and mounted-workspace preservation assertions.
- [ ] **6.C Commit phase 6 when authorized**; verify theme and presentation settings share one closed answer-free preference contract.

```bash
subject='feat(browser): personalize appearance and editing without saving answers'
body='Header selects provide little control over the interface. Add local theme presets, contrast-checked neutral and brand colors, and workspace customization with immediate preview and a closed preference contract that excludes configuration data.'
footer='Change-Id: CHG-01M5MBH2W2XRQX68R9B8TKP7CE'
owned=(
  src/Configurator.Browser/Services/AppearanceState.cs
  src/Configurator.Browser/Services/AppearanceThemeService.cs
  src/Configurator.Browser/Services/AppearancePalette.cs
  src/Configurator.Browser/Services/WorkspaceViewState.cs
  src/Configurator.Browser/wwwroot/browser-appearance.js
  src/Configurator.Browser/Layout/ThemeQuickSwitcher.razor
  src/Configurator.Browser/Layout/ThemeQuickSwitcher.razor.css
  src/Configurator.Browser/Layout/MainLayout.razor
  src/Configurator.Browser/Components/Appearance/AppearanceSettings.razor
  src/Configurator.Browser/Components/Appearance/AppearanceSettings.razor.css
  src/Configurator.Browser/Components/Configuration/WorkspaceCustomization.razor
  src/Configurator.Browser/Components/Configuration/WorkspaceCustomization.razor.css
  src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs
  src/Configurator.Browser/_Imports.razor
  src/Configurator.Browser/wwwroot/css/tokens.css
  tests/Configurator.Browser.Tests/BrowserAppearanceTests.cs
  tests/Configurator.Browser.Tests/BrowserShellTests.cs
  tests/Configurator.Browser.Tests/BrowserExperienceWorkflowTests.cs
  docs/public/browser.md
  docs/internal/BROWSER_UI.md
  src/Configurator.Browser/DESIGN.md
  docs/internal/releases/changes/CHG-01M5MBH2W2XRQX68R9B8TKP7CE.yaml
)
```

Changelog: public feature; nonbreaking; message override: Not overridden. The shared preference admission makes these controls one coherent outcome; do not add theme catalog/accounts/cloud profile work.

## Phase 7: Review, export and safe departure

Dependencies: phase 6. Effort: L.

- [ ] **7.1 Protect destructive-action races before UI changes**: extend existing navigation/workspace/export tests for late confirmation after replacement, cancel preserving work and revocation during pending export. Add NEW `Common/WorkspaceDiscardDialog.razor`/CSS and adapt the guard using existing BrowserNavigationState fences; verify no hidden private undo copy (S14-S15,S21-S22).
- [ ] **7.2 Recompose review and export** in review/editor files: grouped relevant states, attention/changed/deferred filters, exact correction/return, technical bindings in inspector, separated clear action and deliberate per-file operations. Verify values remain masked/value-free and zero-required/draft/readiness states are truthful (S19-S21).
- [ ] **7.3 Update consequence/recovery guidance and fragment** in browser/launch/presentation docs; verify native external-unload behavior and browser filename/permission limitations remain accurate.
- [ ] **7.V Verify phase 7** with Browser commands; require review, export, navigation and workspace lifecycle tests.
- [ ] **7.C Commit phase 7 when authorized**; verify review-to-export and departure are one coherent custody-preserving flow.

```bash
subject='feat(browser): guide review and export with explicit loss decisions'
body='Review and export need focused screens and understandable recovery. Group readiness and corrections, separate destructive actions, and use themed confirmations while retaining explicit export and stale-workspace fencing.'
footer='Change-Id: CHG-01M5MBH3DH5GV9HE4E03CB9AD9'
owned=(
  src/Configurator.Browser/Components/Common/WorkspaceDiscardDialog.razor
  src/Configurator.Browser/Components/Common/WorkspaceDiscardDialog.razor.css
  src/Configurator.Browser/Layout/WorkspaceNavigationGuard.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationReview.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationReview.razor.css
  src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs
  src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.css
  src/Configurator.Browser/Components/Configuration/ConfigurationInspector.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationDiagnosticText.cs
  tests/Configurator.Browser.Tests/BrowserConfigurationReviewTests.cs
  tests/Configurator.Browser.Tests/BrowserNavigationStateTests.cs
  tests/Configurator.Browser.Tests/BrowserWorkspaceLifecycleTests.cs
  tests/Configurator.Browser.Tests/BrowserExportLifecycleTests.cs
  docs/public/browser.md
  docs/internal/BROWSER_UI.md
  docs/internal/LAUNCH_CONTRACT.md
  docs/internal/PRESENTATION_STATE.md
  docs/internal/releases/changes/CHG-01M5MBH3DH5GV9HE4E03CB9AD9.yaml
)
```

Changelog: public feature; nonbreaking; message override: Not overridden.

## Phase 8: Home, Store and source entry

Dependencies: phase 7. Effort: M/L.

- [ ] **8.1 Unify Home and Store** in existing Catalogue/Store/StoreProject/card files; verify concise identity/revision summaries, discoverable search/filter reset and loading/empty/unavailable recovery without authentication claims (S23-S24).
- [ ] **8.2 Simplify local/public opening and launch identity** in Open/Launch; verify source-switch drafts survive, admission remains before answers, retained-public-definition consent stays separate and a source failure offers safe local-file recovery (S22-S23).
- [ ] **8.3 Update entry guidance and fragment**; verify JSON/YAML/URL/revision/compatibility descriptions match current owners and layout parity covers every route.
- [ ] **8.V Verify phase 8** with Browser commands; require store/source/release-contract tests and unchanged trust behavior.
- [ ] **8.C Commit phase 8 when authorized**; verify the definition-discovery-to-workspace entry flow is complete.

```bash
subject='feat(browser): simplify finding and opening configuration definitions'
body='Definition entry should present a clear next action instead of repeated metadata. Align Home, Store, revision review and local or public source entry with the workspace design while preserving admission and public-retention boundaries.'
footer='Change-Id: CHG-01M5MBH3K72YMHC4TH7WJZYEHP'
owned=(
  src/Configurator.Browser/Pages/Catalogue.razor
  src/Configurator.Browser/Pages/Catalogue.razor.css
  src/Configurator.Browser/Pages/Store.razor
  src/Configurator.Browser/Pages/Store.razor.css
  src/Configurator.Browser/Pages/StoreProject.razor
  src/Configurator.Browser/Pages/StoreProject.razor.css
  src/Configurator.Browser/Pages/Open.razor
  src/Configurator.Browser/Pages/Open.razor.css
  src/Configurator.Browser/Pages/Launch.razor
  src/Configurator.Browser/Pages/Launch.razor.css
  src/Configurator.Browser/Components/Store/StoreProjectCard.razor
  src/Configurator.Browser/Components/Store/StoreProjectCard.razor.css
  tests/Configurator.Browser.Tests/BrowserDefinitionStoreTests.cs
  tests/Configurator.Browser.Tests/BrowserSourceEntryTests.cs
  tests/Configurator.Browser.Tests/BrowserReleaseContractTests.cs
  docs/public/browser.md
  docs/internal/BROWSER_UI.md
  docs/internal/releases/changes/CHG-01M5MBH3K72YMHC4TH7WJZYEHP.yaml
)
```

Changelog: public feature; nonbreaking; message override: Not overridden. No Event source or target-definition authoring changes belong in this packet.


## Retained task-oriented help outcome

- [ ] **9.1 Deliver task-oriented help and honest offline/loss feedback** in Help, Footer, inspector and editor, using existing offline state only. Verify no new answer-time network probe, no automatic update/reload over live work and consistent discard/export/filename guidance (S05,S23-S24,S26).

### Packet A: Help and offline behavior

```bash
subject='feat(browser): explain workspace tasks and offline readiness in context'
body='Operators need useful help without leaving live work or assuming it is saved. Add task-oriented contextual guidance and accurate offline, update and loss feedback using the existing local custody policy.'
footer='Change-Id: CHG-01M5MBH3SD2TTN5EGRCTPSP4XA'
owned=(
  src/Configurator.Browser/Pages/Help.razor
  src/Configurator.Browser/Layout/Footer.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationInspector.razor
  src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor
  tests/Configurator.Browser.Tests/BrowserOfflineWorkflowTests.cs
  tests/Configurator.Browser.Tests/BrowserConfigurationWorkflowTests.cs
  docs/public/browser.md
  docs/internal/BROWSER_UI.md
  docs/internal/releases/changes/CHG-01M5MBH3SD2TTN5EGRCTPSP4XA.yaml
)
```

Changelog: public feature; nonbreaking; message override: Not overridden.


## Report coverage map

| Audit findings | Owning tasks |
| --- | --- |
| UX-01, UX-14 | 2.1,4.2 |
| UX-02, UX-03 | 2.2 |
| UX-04, UX-08, UX-09, UX-10, UX-11 | 3.1-3.3,5.1-5.2 |
| UX-05 | 3.2 |
| UX-06, UX-07, UX-12, UX-13 | 1.2,4.2-4.3 |
| UX-15, UX-16 | 5.1,6.1-6.3 |
| UX-17, UX-18, UX-19, UX-20, UX-21 | 7.1-7.3 |
| UX-22 | 8.1-8.2 |
| UX-23, UX-24 | 2.1,3.2,5.2,7.2,9.3 |
| UX-25 | 6.3,9.1 |
| UX-26 | 3.1,4.2,5.1,9.2-9.3 |


## Knowledge graduation and final program evidence

At actual implementation, publish an ADR for the delivered view/answer ownership decisions, not a decision falsely marked implemented during this review. Record non-obvious lessons in the existing presentation/client journal. Graduate translated language packs with owner, trigger and resource/culture/translation acceptance. No separate CTO feedback file is created. Preserve historical report findings and I-VSD revision-1 hashes; newly revalidated reports bind exact replacement identities.
