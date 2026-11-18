# Provider-Responsibility Assessment: Configuration Experience Unification, ISLAMU Event Design-System Reuse, Resizable Docking, Top-Level Settings, and Desktop–Web Parity

> Configurator authority: follow [AGENTS.md](../../AGENTS.md) and [PROJECTS.md](../../PROJECTS.md). Source-system backend examples remain reference-only; absent Event runtime layers and deployment policies do not apply.

Last Updated: 2026-11-18

## Review Metadata
- Mode: planning
- Subject: Wave 2 re-baseline for ISLAMU Event design-system reuse (8 theme presets & HSL customizer), session-preserving top-level Settings with `"← Return to configuration"`, resizable Start/End docking (`DockLayoutHost` & `GridSplitter`), and compact 2-per-row landscape Store cards with fail-closed PNG `iconDataUri` & `CC BY 4.0` attribution (building on completed Wave 1 `0ba1321..cd531cc`)
- Workstream: configuration-experience-unification
- Report kind: planning-assessment
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-11-18
- Reviewed input: Re-baselined Wave 2 triad `dev/active/configuration-experience-unification/` (`plan.md` SHA-256 `9bfa7552aa82f791857548cdb36fd7425b41a0b35ac27c14200d507441b7a119`, `context.md` SHA-256 `09f214c261637c4f142537a80b3eff45242604cc71dc7c1f818881adb4a30869`, `tasks.md` SHA-256 `1295eed4a6a9986fbec15e9b4c2408f0295a630698a8bf9ebfd10d5ee960fbf2`) + [islamu-configurator-event-parity-and-ui-improvements-2026-11-16.md](../../dev/report/islamu-configurator-event-parity-and-ui-improvements-2026-11-16.md) (`73c412c6a0ef66ad45dae4cd1abab8b0942fdd591bf04a8071a92e4b7ce2b894`) over `HEAD` `cd531cc`
- Supersedes: [Compact-only configuration experience assessment](i-vsd-configuration-experience.md)

## Executive Summary

**Practical recommendation:** Proceed with executing the Wave 2 re-baseline of `configuration-experience-unification` to port ISLAMU Event's first-party Blazor design system, 8 theme presets, HSL custom palette generator, resizable docking engine (`DockLayoutHost` / `GridSplitter`), top-level Settings studio, and compact 2-per-row Store card layout across `Configurator.Presentation`, `Configurator.Browser`, and `Configurator.Desktop`. Every ported component, local preference key, and store icon must preserve transient in-memory answer/secret custody (`DefinitionFormModel`), guarantee zero data loss when opening Settings mid-configuration (`"← Return to configuration"`), enforce fail-closed offline PNG validation (`<= 12 KB`, no external URLs or scripts) on store icons, and honor `CC BY 4.0` attribution for `islamu-logo-text-only-v2-square.png` alongside first-party provenance records in `BROWSER_UI_TRANSFER.md`.

**Who is affected and why this matters:** Self-hosting administrators, Arabic RTL operators, and assistive-technology users moving between ISLAMU Event, Configurator Browser, and Configurator Desktop currently encounter fragmented visual identities (Teal vs. Material Blue vs. Event's 8 presets), broken high-contrast surface overrides in Browser, missing manual High-Contrast selection and live string bugs (`WireKeyCopiedStatus`, `Show _optional editors`, `inputs-changed`) in Desktop, a cramped non-resizable `240px` Browser sidebar that pushes `WORKSPACE` and `Settings` off-screen, no top-level Settings page in Browser, and tall `~330px` Store cards that dump all 8 output filenames inline without logos. Reusing Event's proven UI architecture eliminates this divergence while respecting operator time, accessibility needs, and custody boundaries.

**Central mechanism and confidence limit:** Architectural safety rests on centralizing the 8 presets and pure-C# `HslColor`/`ThemePaletteGenerator` inside `Configurator.Presentation/Theming/`, hosting Browser Settings in-place via `BrowserNavigationState.IsSettingsOpen` when a `/launch/{FamilyId}` workspace is active so `<ConfigurationEditor>` is never disposed or invalidated while retaining route-exit invalidation on actual URL changes, restricting `configurator.appearance.v1` and `configurator.dock.v1` strictly to bounded layout/theme tokens, and validating `iconDataUri` fail-closed as a square PNG (`<= 12 KB`, `<= 256×256`) inside `curation.json` -> `index.json`. Following revalidation against the Wave 2 triad (`9bfa7552...`, `09f214c2...`, `1295eed4...`), this assessment is `current` and `plan-aligned` across findings `IVSD-F001`–`IVSD-F009`.

## Scope

In scope for this planning review:
- Porting ISLAMU Event's 8 system theme presets, `HslColor`, and `AppearancePaletteGenerator` into `Configurator.Presentation/Theming/` and wiring them identically into Browser (`MudTheme` + CSS `--isl-*` + `.appearance--light-hc`/`.appearance--dark-hc`) and Desktop (`DesktopTheme` + `ThemePalettes.axaml` + 5 modes).
- Porting Event's first-party Blazor common wrappers (`AppDialogShell`, `AppIconButton`, `AppSelect`), `ThemeQuickSwitcher`, `LanguagePicker`, `AccessibilityFocusService`/`AccessibilityAnnouncerService`, `SettingsLayout`/`SettingsAppearance`, and `Components/Docking/` + `Services/Docking/` into `Configurator.Browser` with zero Event backend/BFF/auth/telemetry dependencies.
- Implementing top-level Settings with in-place session preservation and a top-left **`← Return to configuration`** CTA in both Browser and Desktop, resizable Start (`310px` default, `240–460px`) and Tabbed End (`Inspector` | `Document JSON`, `360px` default, `300–620px`) docks, a 1-row top Configure Ribbon, and Desktop string/status bug fixes (`WireKeyCopiedStatus`, `Show _optional editors`, `inputs-changed`).
- Extending `curation.json` and `index.json` with fail-closed PNG `iconDataUri` + `iconLicense` + `iconAttribution`, embedding the `128×128` `ISLAMU Event` logo (`CC-BY-4.0`), and redesigning Browser and Desktop Store cards into compact 2-per-row landscape rectangles (`~124px`) with an `ⓘ` Project Details modal.

Out of scope:
- Any Event backend API, BFF (`/bff/*`), database, SignalR, user account, telemetry, or remote AI agent integration.

## Claim Boundary

This assessment evaluates **provider-controlled software design responsibilities** under Sunni Islamic ethical principles (`Amanah` / trustworthy stewardship, `Sidq` / truthfulness, `'Adl` / equitable justice, `La darar` / non-harm, `Ihsan` / clarity and care). It is **not** a religious-legal ruling (`fatwa`), a Sharia-compliance certificate, or runtime/assistive-technology certification.

## Common Overlooked Failures And Outcomes

1. **Opening top-level Settings in Browser accidentally invalidating the active `/launch/{FamilyId}` workspace:** Because `Launch.razor` calls `_editor?.Invalidate()` on `LocationChanged` and `Dispose`, navigating away from `/launch/{FamilyId}` via URL change would wipe unsaved configuration answers, unparsed drafts, and secret consent.
2. **Docking wrapper CSS (`overflow-x: hidden`) breaking Blazor `<Virtualize>` `IntersectionObserver`:** Setting `overflow-x: hidden` on `.dock-layout-host` prevents virtualized field slices from hydrating on scroll.
3. **Unvalidated Store icon strings admitting SVG scripts or remote hotlink beacons:** Allowing arbitrary URLs or `image/svg+xml` in store metadata would break offline custody and content security.
4. **Copying first-party Event files without recording provenance or preserving `CC BY 4.0` attribution on the logo asset:** Omitting `BROWSER_UI_TRANSFER.md` entries or `CC BY 4.0` notices violates repository IP governance (`AGENTS.md` §5.10).

## Findings

| Finding ID | Title | Lifecycle | Severity | Claim type | Mitigation ID |
|---|---|---|---|---|---|
| `IVSD-F001` | Transient Secret And Draft Custody Across In-Place Settings & Virtualized Rows | Open | Critical | Security & custody design | `IVSD-M001` |
| `IVSD-F002` | Truthful Readiness, Diagnostic Progress, And Deduplicated Section Review UI | Open | High | Truthfulness & UX gap | `IVSD-M002` |
| `IVSD-F003` | Equitable High-Contrast, Keyboard Dock Resizing, Arabic RTL, And Localized Desktop Strings | Open | High | Accessibility & justice gap | `IVSD-M003` |
| `IVSD-F004` | Inclusive Performance Under Resizable Docks & Virtualized Sections (`<= 1,400` Nodes, 60fps Drag) | Open | High | Performance & exclusion risk | `IVSD-M004` |
| `IVSD-F005` | Non-Deceptive Labels, Mnemonic-Free Desktop Controls & Progressive Store `ⓘ` Details Modal | Open | High | Authoring & clarity gap | `IVSD-M005` |
| `IVSD-F006` | Visual & Structural Parity Across Browser, Desktop, and ISLAMU Event Without Work Loss | Open | Medium | Usability & harm prevention | `IVSD-M006` |
| `IVSD-F007` | Zero Surveillance Or Answer Leakage In Theme/Dock Storage & Fail-Closed PNG Store Icons | Open | Critical | Privacy & content-security boundary | `IVSD-M007` |
| `IVSD-F008` | Explicit Evidence Boundary Between Automated Checks And Human/OS Qualification | Open | Medium | Governance & candor | `IVSD-M008` |
| `IVSD-F009` | First-Party Event Transfer Provenance & Creative Commons (`CC BY 4.0`) Attribution Integrity | Open | High | IP & attribution stewardship | `IVSD-M009` |

### IVSD-F001: Transient Secret And Draft Custody Across In-Place Settings & Virtualized Rows
- **Concrete situation & mechanism (`IVSD-M001`):** `DefinitionFormModel` remains the sole in-memory custodian of answers, unparsed drafts, and secret consent. Opening Settings during an active `/launch/{FamilyId}` session renders `<SettingsWorkspace HasActiveWorkspace="true" />` in-place via `BrowserNavigationState.IsSettingsOpen` while keeping `<ConfigurationEditor>` mounted (`hidden`/`inert`) so answers, drafts, and secrets are never serialized to storage or wiped by `Launch.razor`'s route-exit invalidation (`S03`), while navigating away from `/launch/{FamilyId}` to another route continues to run `_editor?.Invalidate()` (`S04`).
- **Mapping:** Scenarios `S03`, `S04`; `TASK-3.1`, `TASK-3.2`.

### IVSD-F002: Truthful Readiness, Diagnostic Progress, And Deduplicated Section Review UI
- **Concrete situation & mechanism (`IVSD-M002`):** Replace Desktop's 11× repeated `[Mark reviewed]` rail buttons and Browser's 4 stacked mono rail chips with concise 2-line section rows (title + `Ready/Required` pill + subtle issue count or `✓ Reviewed` badge) while keeping a single explicit `[Mark reviewed]` toggle in the active Section Header (`S07`).
- **Mapping:** Scenario `S07`; `TASK-4.2`, `TASK-6.2`.

### IVSD-F003: Equitable High-Contrast, Keyboard Dock Resizing, Arabic RTL, And Localized Desktop Strings
- **Concrete situation & mechanism (`IVSD-M003`):** Provide full WCAG AAA `Light High Contrast` and `Dark High Contrast` modes across both Browser (`.appearance--light-hc` / `.appearance--dark-hc`) and Desktop (`DesktopTheme`), WAI-ARIA `role="separator"` keyboard resizing (`ArrowLeft`/`ArrowRight`, `Shift+Arrow`, `Home`, `End`) with logical RTL delta inversion (`getComputedStyle`), and fix Desktop's missing `WireKeyCopiedStatus`, raw `_optional` mnemonic underscore, and `inputs-changed` status strings (`S01`, `S05`, `S06`, `S08`).
- **Mapping:** Scenarios `S01`, `S05`, `S06`, `S08`; `TASK-2.1`, `TASK-4.1`, `TASK-4.2`, `TASK-6.1`, `TASK-6.2`.

### IVSD-F004: Inclusive Performance Under Resizable Docks & Virtualized Sections (`<= 1,400` Nodes, 60fps Drag)
- **Concrete situation & mechanism (`IVSD-M004`):** Update `--dock-layout-start-width` and `--dock-layout-end-width` via `requestAnimationFrame` in `dock-resize.js` during pointer drag (committing to C# on `pointerup`), keep `.dock-layout-host` free of `overflow-x: hidden` so `<Virtualize>` `IntersectionObserver` preserves the `<= 1,400` DOM node budget, and automatically degrade side docks to `DockOverlayHost` when viewport width is `<= 760px` or center content width drops below `420px` (`S05`, `S06`).
- **Mapping:** Scenarios `S05`, `S06`; `TASK-4.1`, `TASK-4.2`.

### IVSD-F005: Non-Deceptive Labels, Mnemonic-Free Desktop Controls & Progressive Store `ⓘ` Details Modal
- **Concrete situation & mechanism (`IVSD-M005`):** Remove inline 8-filename dumps from Store card faces while making complete cryptographic digests, output filenames, license terms, and historical revisions (`1..7`) 1-click accessible via the **`ⓘ` Info** modal (`StoreProjectDetailsDialog` / Desktop details view), and strip raw WinForms `_` mnemonics from Desktop `TextBlock` strings (`S08`, `S09`).
- **Mapping:** Scenarios `S08`, `S09`; `TASK-5.1`, `TASK-6.1`, `TASK-6.2`.

### IVSD-F006: Visual & Structural Parity Across Browser, Desktop, and ISLAMU Event Without Work Loss
- **Concrete situation & mechanism (`IVSD-M006`):** Centralize the 8 Event presets and HSL generator in `Configurator.Presentation/Theming/`, align Browser's top navbar and 1-row top Configure Ribbon with Desktop and Event, provide top-left **`← Return to configuration`** in both clients' Settings views, and align Store cards to a 2-per-row compact landscape grid (`S01`–`S09`).
- **Mapping:** Scenarios `S01`–`S09`; `TASK-1.1`–`TASK-6.3`.

### IVSD-F007: Zero Surveillance Or Answer Leakage In Theme/Dock Storage & Fail-Closed PNG Store Icons
- **Concrete situation & mechanism (`IVSD-M007`):** Restrict `configurator.appearance.v1` (`<= 512` bytes) and `configurator.dock.v1` (`<= 1,024` bytes) to allowlisted presentation/geometry keys only; enforce fail-closed validation on `iconDataUri` in `BuildDefinitionStore.cs` and `DefinitionStoreIndexParser.cs` (`data:image/png;base64,` prefix, `<= 12,288` decoded bytes, 8-byte PNG signature, square `IHDR` `16..256`), rejecting external URLs or SVG scripts (`S01`, `S05`, `S10`).
- **Mapping:** Scenarios `S01`, `S05`, `S10`; `TASK-1.1`, `TASK-1.2`, `TASK-2.1`, `TASK-4.1`.

### IVSD-F008: Explicit Evidence Boundary Between Automated Checks And Human/OS Qualification
- **Concrete situation & mechanism (`IVSD-M008`):** Record executed automated verification (`Presentation.Tests`, `Browser.Tests`, `Desktop.Tests`, `Desktop.NativeHost` `en` & `ar`, `BuildDefinitionStore.cs --self-test`) distinctly from remaining manual steward AT walkthroughs and Windows/macOS host gates in `STATUS.md` and `OPERATIONS.md`.
- **Mapping:** All `VERIFY-*`, `TASK-6.3`.

### IVSD-F009: First-Party Event Transfer Provenance & Creative Commons (`CC BY 4.0`) Attribution Integrity
- **Concrete situation & mechanism (`IVSD-M009`):** Under `Amanah` and `Sidq` (honoring authorship covenants and license conditions), record every newly ported first-party Event file and its git blob SHA in `docs/internal/extraction/BROWSER_UI_TRANSFER.md`, and preserve Amir Akrari's `CC BY 4.0` attribution (`Draft ISLAMU logo by Amir Akrari, licensed under CC BY 4.0.`) for the downscaled `islamu-logo-text-only-v2-square.png` icon across `store/islamu-event/curation.json`, `THIRD-PARTY-NOTICES.md`, `docs/internal/legal/dependencies/configurator-ui.md`, and the Store `ⓘ` Project Details modal (`S09`, `S10`).
- **Mapping:** Scenarios `S09`, `S10`; `TASK-1.1`, `TASK-1.2`, `TASK-4.2`, `TASK-5.1`, `TASK-6.2`.

## Stakeholders

- **Self-hosting community administrators:** Benefit from consistent ISLAMU visual identity, fast 2-per-row Store scanning with `ⓘ` metadata inspection, above-the-fold Configure controls, and zero-loss Settings access.
- **Blind, low-vision, and keyboard-only operators:** Benefit from WCAG AAA Light/Dark High Contrast modes in both clients, keyboard-resizable `role="separator"` dock handles, roving `DockTabStrip` focus, and mnemonic-clean labels.
- **Arabic RTL operators (`ar` locale):** Benefit from logical RTL dock resizing, `<bdi dir="ltr">` isolation, and complete Arabic translations (`WireKeyCopiedStatus`, preset names, `"← Return to configuration"`).
- **Project Steward & contributors:** Benefit from direct reuse of first-party Event Blazor work (`legal/CLA.md`) and preserved `CC BY 4.0` attribution.

## I-VSD Principles And Domains

| Sunni Ethical Principle | Domain Applied | Concrete Design Consequence |
|---|---|---|
| **Amanah** (Trustworthy Stewardship) | Session custody, IP/license attribution | In-place Settings never loses unsaved answers or leaks secrets; `CC BY 4.0` logo notice and `BROWSER_UI_TRANSFER.md` provenance are preserved. |
| **Sidq** (Truthfulness & Candor) | Localization, status display, qualification | Desktop shows truthful localized status (`Key copied`, `Inputs modified`) instead of raw keys; docs keep automated vs. manual AT gates distinct. |
| **'Adl** (Equitable Justice) | High-contrast, RTL, keyboard & viewport equity | Both clients ship Light/Dark HC modes, keyboard-accessible dock splitters, RTL drag inversion, and `< 760px` / `< 420px` auto-overlay safety. |
| **La darar wa la dirar** (Non-Harm) | Fail-closed store icons, route isolation | `iconDataUri` rejects external URLs and SVG scripts fail-closed; route exit still invalidates stale editor generations. |
| **Ihsan** (Excellence & Consideration) | Unified ISLAMU craft & visual calm | 8 Event presets, Custom Theme Studio with live preview, `310px` resizable rail, 1-row top ribbon, and compact `~124px` Store cards. |

## Validation Gaps

1. **Physical Assistive Technology & Arabic Screen-Reader UX:** Requires steward-conducted NVDA/Orca/VoiceOver walkthroughs in `en` and `ar` (`IVSD-F008`).
2. **Cross-OS Protected-Save Hosts (`Windows`/`macOS`):** Linux `0600` create-only behavior remains the verified native reference host.

## Escalation Needed

- **Scholarly / Fiqh escalation:** None required.
- **Steward / Human-AT escalation gate:** Schedule physical screen-reader walkthroughs in `en` and `ar` before claiming external WCAG speech/Braille certification.

## Evidence Reviewed

- `dev/report/islamu-configurator-event-parity-and-ui-improvements-2026-11-16.md` (`2026-11-16`).
- `/home/amir/ISLAMU/Github/Event/legal/CLA.md`, `/home/amir/ISLAMU/Github/Event/assets/images/adopters/ISLAMU/LICENSE.md`, `/home/amir/ISLAMU/Github/Event/src/Explore.Blazor.Client/*`, `/home/amir/ISLAMU/Github/Event/src/Explore.Blazor/wwwroot/css/*`, `/home/amir/ISLAMU/Github/Event/src/Explore.Application/Services/{AppearancePaletteGenerator,HslColor}.cs`, `/home/amir/ISLAMU/Github/Event/src/Explore.Persistence/Seed/LookupTableSeeder.cs`.
- Configurator sources: `src/Configurator.Presentation/Store/*`, `src/Configurator.Browser/*`, `src/Configurator.Desktop/*`, `eng/configurator/BuildDefinitionStore.cs`, `eng/configurator/Configurator.BuildCapture/Program.cs`, `THIRD-PARTY-NOTICES.md`, `docs/internal/extraction/BROWSER_UI_TRANSFER.md`, `docs/internal/legal/dependencies/configurator-ui.md`.

## Planning Handoff

- Workstream: `configuration-experience-unification`
- Status: current
- Disposition: plan-aligned
- Reviewed input: `configuration-experience-unification-plan.md` (`9bfa7552aa82f791857548cdb36fd7425b41a0b35ac27c14200d507441b7a119`), `configuration-experience-unification-context.md` (`09f214c261637c4f142537a80b3eff45242604cc71dc7c1f818881adb4a30869`), `configuration-experience-unification-tasks.md` (`1295eed4a6a9986fbec15e9b4c2408f0295a630698a8bf9ebfd10d5ee960fbf2`)
- Findings and mitigations: `IVSD-F001` -> `IVSD-M001`, `IVSD-F002` -> `IVSD-M002`, `IVSD-F003` -> `IVSD-M003`, `IVSD-F004` -> `IVSD-M004`, `IVSD-F005` -> `IVSD-M005`, `IVSD-F006` -> `IVSD-M006`, `IVSD-F007` -> `IVSD-M007`, `IVSD-F008` -> `IVSD-M008`, `IVSD-F009` -> `IVSD-M009`
- Required plan mappings: `IVSD-F001`/`M001` -> Scenarios `S03`, `S04` (`TASK-3.1`, `TASK-3.2`); `IVSD-F002`/`M002` -> Scenario `S07` (`TASK-4.2`, `TASK-6.2`); `IVSD-F003`/`M003` -> Scenarios `S01`, `S05`, `S06`, `S08` (`TASK-2.1`, `TASK-4.1`, `TASK-4.2`, `TASK-6.1`, `TASK-6.2`); `IVSD-F004`/`M004` -> Scenarios `S05`, `S06` (`TASK-4.1`, `TASK-4.2`); `IVSD-F005`/`M005` -> Scenarios `S08`, `S09` (`TASK-5.1`, `TASK-6.1`, `TASK-6.2`); `IVSD-F006`/`M006` -> Scenarios `S01`–`S09` (`TASK-1.1`–`TASK-6.3`); `IVSD-F007`/`M007` -> Scenarios `S01`, `S05`, `S10` (`TASK-1.1`, `TASK-1.2`, `TASK-2.1`, `TASK-4.1`); `IVSD-F008`/`M008` -> all `VERIFY-*`, `TASK-6.3`; `IVSD-F009`/`M009` -> Scenarios `S09`, `S10` (`TASK-1.1`, `TASK-1.2`, `TASK-4.2`, `TASK-5.1`, `TASK-6.2`)
- Escalations required before: planning approval (user review of re-baselined triad) | release (physical AT walkthrough in `en` and `ar`)
- Refresh triggers: Any change to in-place Settings session custody, `iconDataUri` PNG-only validation bounds, `localStorage` allowlists, or `CC BY 4.0` / `BROWSER_UI_TRANSFER.md` attribution commitments.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
|---|---|---|---|---|
| 2026-11-16 | None | current | Initial Wave 1 I-VSD planning assessment & triad binding | Wave 1 `plan.md` (`be7c3b13...`), `context.md` (`25a66502...`), `tasks.md` (`a99634a4...`) |
| 2026-11-18 | current | current | Re-baselined for Wave 2 (ISLAMU Event design-system reuse, top-level Settings, resizable docking, compact Store cards & PNG `iconDataUri`) | Bound to Wave 2 triad (`9bfa7552...`, `09f214c2...`, `1295eed4...`) and `islamu-configurator-event-parity-and-ui-improvements-2026-11-16.md` (`73c412c6...`); added `IVSD-F009`/`M009` |
