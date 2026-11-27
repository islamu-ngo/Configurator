# Provider-Responsibility Assessment: Progressive-Disclosure Workspace Utility Dock, Clean Form Terminal Boundary & Guided Entry (`Configurator.Browser`)

> Configurator authority: follow [AGENTS.md](../../AGENTS.md) and [PROJECTS.md](../../PROJECTS.md). Source-system backend examples remain reference-only; absent Event runtime layers and deployment policies do not apply.

Last Updated: 2026-11-27

## Review Metadata
- Mode: planning
- Subject: Removing the stacked bottom utility/privacy accordion pile below `Previous section` / `Next section` in `Configurator.Browser` and replacing it with a semantic `<div role="group">` controls wrapper, sticky `44px` outermost trailing `WorkspaceUtilityRail` (`role="toolbar" aria-orientation="vertical"`, `aria-expanded`+`aria-controls`-only with `.FocusAsync()`, narrow `MudMenu` `Tools` trigger), metadata-driven single-active `DockScope.Workspace` End Dock (`WorkspaceUtilityDescriptor`, atomic `DockLayoutState.OpenExclusive`, `PersistState: false`), always-mounted `CustodyAnchor` + header secret posture chip & 1-click `Revoke` + `.closest()` capture-phase and synchronous in-process Developer JS retirement, scoped `WeakRef` focus stack, untouched-session onboarding micro-cue, slim 1-line local-custody footer, and compile-time zero-string `ReadOnlyMemory<byte>` `BrowserProfileVaultController`
- Workstream: browser-workspace-utilities
- Report kind: planning-assessment
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-11-27
- Reviewed input: CTO-refined triad `dev/active/browser-workspace-utilities/` (`plan.md` SHA-256 `e1756bb0f8a922b01fb8073a5f962041b43c2b6f99fec86372bce42501fb8731`, `context.md` SHA-256 `85d4434b3546abbff73d112d38660149821c5b7f9ff5e9cbdaadef83250ff342`, `tasks.md` SHA-256 `7c2ccd0097d3377083155d6ab241abde850e6025929b66ccbc625c0ff53638f4`) over `main` `HEAD` `61df8bd` (building on completed & archived `browser-navigation-separation` at `43b2bf2`, `54132be`, `623bbca`)
- Supersedes: none

## Executive Summary

**Practical recommendation:** Proceed with removing the seven stacked utility, entry, locale, consent, and privacy blocks beneath `Previous section` / `Next section` in `ConfigurationEditor.razor` (and unwrapping non-form landmarks from the outer `<fieldset>` into `<div role="group">`) and consolidating their real capabilities into a calm, progressive-disclosure workbench: (1) a `44px` sticky trailing-edge `WorkspaceUtilityRail` (collapsed by default on "Start fresh", `aria-expanded`+`aria-controls` without conflicting `aria-pressed`, wrapping roving tabindex with `.FocusAsync(preventScroll: true)`, hidden at `<= 60rem` in favor of a compact header `MudMenu` `Tools` trigger) driving an atomically switched (`DockLayoutState.OpenExclusive`), single-active, `PersistState: false` resizable `DockScope.Workspace` End Dock (`Inspector` with `"This workspace"` overview, `Document JSON`, `Import`, `Profiles`, and `Trust & Privacy`); (2) an always-scannable `Secrets` trust chip plus compact 1-click `[data-action="revoke-secret-entry"]` promoted directly into `WorkspaceHeader` paired with an always-mounted `<section>` `CustodyAnchor` for `ConfigurationDeveloper` (`.closest()` capture-phase + synchronous in-process JS retirement) and a scoped `WeakRef` focus stack so clicking locked fields opens `Trust & Privacy` beside the field without scroll-jumping or focus clobbering; (3) an auto-dismissing onboarding micro-cue only while the form is completely untouched (`InputRevision == 0`); (4) removal of the duplicate bottom `Language` accordion (`LanguagePicker` in the topbar and `Settings` remain canonical); (5) an ultra-slim 1-line local-custody status footer (`~28px`); and (6) lifting `ProfileWorkerClient` lifecycle state out of `ConfigurationVaultPanel` (`IDisposable`-only view) into an editor-scoped `BrowserProfileVaultController` (`IAsyncDisposable`, compile-time `ReadOnlyMemory<byte>` zero-string passphrase boundary, pre-await wipe, direct `Form.InputChanged` unmounted retirement, and teardown before `_form?.Dispose()`) so utility switching or responsive dock<->overlay transitions never destroy active encrypted profile custody.

**Who is affected and why this matters:** Operators configuring complex definitions currently encounter ~600px of form-like `<details>` accordions dumped below the last field (`Start fresh / Use an existing configuration / Open a saved profile`, near-empty `this workspace`, `Start from an existing configuration`, `Portable saved profiles`, duplicate `Language`, `Review workspace secret consent`, and `Privacy and export limits`). Clicking a locked password field violently scrolls operators away from their field to the bottom of the page, while returning users must scroll past an entire form just to discover file import or profile restore. Consolidating non-form tools into an on-demand right-side workbench respects operator attention (`Ihsan`), keeps privacy posture visibly honest (`Sidq`), protects worker/secret custody (`Amanah`), and safeguards keyboard, RTL, and mobile users (`'Adl`).

**Central mechanism and confidence limit:** Custody safety hinges on decoupling `BrowserProfileVaultController` from Blazor panel mount/unmount cycles while clearing unsubmitted passphrase strings on unmount/revocation, keeping secret entry strictly opt-in per workspace (`DefinitionSessionPrivacy.BrowserSecretsEnabled`) with focus restoration back to the triggering password input, accounting for the `44px` desktop rail inside `WorkspaceMinimumContentWidth` (`+44px` above `960px`) so `DockLayoutState` protects `>= 420px` form inputs, and updating both bUnit suites and live Playwright qualification checks (`Configurator.BrowserQualification`). Automated tests and Chromium checks do not certify physical screen-reader behavior across operating systems (`IVSD-F005`).

## Scope

In scope for this planning review:
- Extracting `BrowserProfileVaultController` from `ConfigurationVaultPanel` so worker custody, comparison results, and recovery verification survive End-Dock tab switches, closing/reopening the dock, and desktop<->mobile overlay transitions while clearing transient unsubmitted passphrase text on unmount/revocation.
- Expanding `BrowserDockPanels` (`DockScope.Workspace`, `DockSide.End`) to five cohesive panels (`Inspector`, `DocumentJson`, `Import`, `Profiles`, `TrustPrivacy`), folding the `"This workspace"` overview into `ConfigurationInspectorPane` when no field is inspected, and wrapping all three stages (`Configure`, `Review`, `Files`) in `<DockLayoutHost Scope="DockScope.Workspace">`.
- Adding `WorkspaceUtilityRail.razor(.css)` (`44px` sticky desktop trailing edge toolbar, `44×44px` targets, tooltips, status badges, RTL-mirrored, hidden at `<= 60rem`), a mobile/narrow header `Tools` trigger, header-promoted `Secrets` consent chip (`[data-workspace-consent-chip]`), untouched-session onboarding micro-cue, `Ctrl+K` quick actions in `WorkspaceSearch`, and a quiet 1-line `WorkspaceStatusFooter`.
- Removing all 7 bottom accordion/entry blocks (including duplicate `#configuration-locale` `Language` `<details>`), migrating `Configurator.Browser.Tests` and `Configurator.BrowserQualification` probes cleanly, and syncing `docs/public/browser.md`, `docs/internal/BROWSER_UI.md`, and `src/Configurator.Browser/DESIGN.md`.

Out of scope:
- `Configurator.Desktop` (`MainWindow.axaml`) and `Configurator.Terminal`.
- Mobile full-screen section-index -> detail drill-down (remains a separate future increment).
- Any change to Core definition admission, cryptographic vault format (`JWE · A256GCM · PBES2-HS512+A256KW`), offline PWA cache exclusion, or native artifact saving.

## Claim Boundary

This assessment evaluates **provider-controlled software design responsibilities** under Sunni Islamic ethical principles (`Amanah`, `Sidq`, `'Adl`, `La darar`, `Ihsan`). It is **not** a religious-legal ruling (`fatwa`), Sharia certification, or human assistive-technology certification.

## Common Overlooked Failures And Outcomes

1. **Unmounting `ConfigurationVaultPanel` on End-Dock Tab Switch Terminating Live `ProfileWorkerClient`:** Because `DockSideHost` renders only `ActiveEntry` and `DockOverlayHost` mounts separately on narrow viewports, hosting stateful `ConfigurationVaultPanel` directly inside a dock `RenderFragment` without extracting `BrowserProfileVaultController` would call `DisposeAsync()`, killing unlocked profiles mid-session.
2. **Unsubmitted Passphrase Leakage in Long-Lived Controllers:** Extracting a session controller must NOT keep raw typed passphrase strings alive after closing the `Profiles` panel, completing/cancelling an operation, or revoking secret consent.
3. **Hiding `Privacy and export limits` Too Deeply:** Removing bottom disclosures without surfacing a visible header trust chip and quiet 1-line custody/output footer (`Local in this tab · N declared outputs · Privacy & limits`) would reduce transparency about transient browser limits.
4. **Right Utility Rail (`44px`) Unaccounted for in `DockLayoutState` Width Math:** Adding a `44px` desktop rail without bumping `WorkspaceMinimumContentWidth` above `960px` erodes the protected `420px` form canvas floor on `1280px` laptops.
5. **Icon-Only "Mystery Meat" Edge Rail Failing Keyboard/Accessibility Heuristics:** Tool rail icons without explicit accessible names, tooltips, visible focus outlines, `44×44px` targets, or mobile fallback exclude keyboard and touch users.

## Findings

| Finding ID | Title | Lifecycle | Severity | Claim type | Mitigation ID |
|---|---|---|---|---|---|
| `IVSD-F001` | Profile Worker Custody & Passphrase Clearing Across Dock Tab Switching & Overlay Transitions | Open | Critical | Security & custody design | `IVSD-M001` |
| `IVSD-F002` | Explicit Per-Workspace Secret Consent Visibility, In-Context Focus Return & Non-Regressing Revocation | Open | Critical | Privacy & consent boundary | `IVSD-M002` |
| `IVSD-F003` | Accessible Utility Rail (`44px`), `Ctrl+K` Discoverability, RTL Symmetry & `>= 420px` Width Floor | Open | High | Accessibility & justice gap | `IVSD-M003` |
| `IVSD-F004` | Convention-Over-Configuration Onboarding, Duplicate Removal & Honest Passive Trust Disclosure | Open | High | Truthfulness & usability design | `IVSD-M004` |
| `IVSD-F005` | Qualification Probe Continuity & Honest Evidence Boundaries Across Automated vs. Human AT Checks | Open | Medium | Governance & candor | `IVSD-M005` |

### IVSD-F001: Profile Worker Custody & Passphrase Clearing Across Dock Tab Switching & Overlay Transitions
- **Concrete situation & mechanism (`IVSD-M001`):** `ConfigurationVaultPanel` currently owns `_worker`, `_recoveryWorker`, `_fields`, `_recoveryReference`, `_comparisonExport`, and `_operation`. Moving `Profiles` into the right-side single-active End Dock without extracting `BrowserProfileVaultController` (owned by `ConfigurationEditor` and keyed to `_form`) would silently dispose the worker whenever the operator switches utilities, closes the dock, or resizes across `760px`. Extracting `BrowserProfileVaultController` (`IAsyncDisposable`, compile-time zero-string `ReadOnlyMemory<byte>` passphrase boundary, direct `Form.InputChanged` unmounted worker + `_recoveryReference` retirement, `IsBusy` wired into `HasWork`, and pre-form teardown before `_form?.Dispose()`) preserves live worker custody across utilities/stages/overlays while keeping all 6 raw typed passphrase drafts strictly view-local on `ConfigurationVaultPanel` (`IDisposable`-only, wiped synchronously before `await`, on `CredentialsCleared`, and on `Dispose()`).
- **Principle, domain & stakeholder:** `Amanah` (Stewardship) & `La darar` (Preventing Silent Work/Key Loss); affects operators using portable `.cfgvault` encrypted profiles.
- **Mapping:** Scenarios `S02`, `S03`; `TASK-1.1`, `TASK-1.2`, `TASK-2.2`, `TASK-2.3`.

### IVSD-F002: Explicit Per-Workspace Secret Consent Visibility, In-Context Focus Return & Non-Regressing Revocation
- **Concrete situation & mechanism (`IVSD-M002`):** Secret entry remains strictly locked by default per workspace. Promoting BOTH `[data-workspace-consent-chip]` (`Secrets locked` / `Secrets unlocked`) AND compact 1-click `[data-action="revoke-secret-entry"]` (visible whenever unlocked outside all disclosures across every stage) directly into `WorkspaceHeader`, binding `ConfigurationDeveloper` to an always-mounted `<section>` `CustodyAnchor` with capture-phase `.closest('[data-action="revoke-secret-entry"]')` and synchronous in-process `IJSInProcessObjectReference.InvokeVoid("retire", ...)` retirement before `form.SetSecretEntryEnabled(false)`, hosting `#secret-consent` + `RevokeSecrets` + `Privacy and export limits` inside `Trust & Privacy`, and upgrading `accessibility.js` to a scoped `WeakRef` focus stack with `preventScroll: true` removes violent scroll jumps, eliminates overlay focus clobbering, preserves synchronous revocation clearing, and keeps zero consent/answer data in `localStorage` or URLs.
- **Principle, domain & stakeholder:** `Amanah` (Custody) & `Sidq` (Transparent Opt-In); affects self-hosting administrators entering credentials offline.
- **Mapping:** Scenarios `S03`, `S04`; `TASK-1.1`, `TASK-1.3`, `TASK-2.1`, `TASK-2.2`, `TASK-2.3`.

### IVSD-F003: Accessible Utility Rail (`44px`), `Ctrl+K` Discoverability, RTL Symmetry & `>= 420px` Width Floor
- **Concrete situation & mechanism (`IVSD-M003`):** `WorkspaceUtilityRail` renders `44×44px` native `<button>` controls inside `role="toolbar" aria-orientation="vertical"` with wrapping roving `tabindex` calling `.FocusAsync(preventScroll: true)`, localized `aria-label`, `title`, `aria-expanded` + `aria-controls` (no conflicting `aria-pressed`), status indicators, and WCAG high-contrast focus rings; drives atomic single-active `DockLayoutState.OpenExclusive` switching (`PersistState: false`, no 0px flicker or overlay focus-trap teardown); mirrors logically to `inline-end` in Arabic `RTL`; bumps `WorkspaceMinimumContentWidth` by `+44px` above `960px` (`834`/`850`/`818`/`810`/`464`/`420`) without stale `ClearMinimumContentWidth` on stage switches; hides completely at `<= 60rem` where a header `MudMenu` `Tools` trigger opens `DockOverlayHost` modal sheets (deleting legacy `ConfigurationInspectorPane.IsMobileSheet` duplication); and registers Browser utility commands in `Ctrl+K` via `BrowserWorkspaceCommand` (plus wiring `WorkspaceCommandId.OpenDocumentEditor`).
- **Principle, domain & stakeholder:** `'Adl` (Equitable Access) & `Ihsan` (Craftsmanship); affects keyboard, touch, low-vision, laptop (`1280px`), and Arabic RTL users.
- **Mapping:** Scenarios `S02`, `S05`, `S06`; `TASK-1.4`, `TASK-1.5`, `TASK-2.1`, `TASK-2.2`, `TASK-3.2`.

### IVSD-F004: Convention-Over-Configuration Onboarding, Duplicate Removal & Honest Passive Trust Disclosure
- **Concrete situation & mechanism (`IVSD-M004`):** Removing the redundant bottom `Language` accordion (`#configuration-locale`, duplicated by topbar `LanguagePicker` and `Settings -> Language`), removing duplicate `#workspace-advanced-options` mode/inspector/consent buttons, replacing the outer landmark-wrapping `<fieldset class="configuration-editor__controls">` with `<div role="group">`, folding `"This workspace"` into `ConfigurationInspectorPane`, replacing bottom `[Start fresh / Use existing / Open saved profile]` buttons with a dismissible 1-line untouched-session onboarding micro-cue (`InputRevision == 0`, auto-hiding upon first edit), and anchoring an understated 1-line `WorkspaceStatusFooter` (`Local in this tab · N declared outputs · Privacy & limits` -> opens `Trust & Privacy`) ends the form cleanly at `Previous section` / `Next section` without concealing essential custody or import capabilities.
- **Principle, domain & stakeholder:** `Sidq` (Truthfulness) & `Ihsan` (Clarity); affects both first-time guided users and returning operators.
- **Mapping:** Scenarios `S01`, `S04`, `S05`; `TASK-2.1`, `TASK-2.2`, `TASK-2.3`, `TASK-3.2`.

### IVSD-F005: Qualification Probe Continuity & Honest Evidence Boundaries Across Automated vs. Human AT Checks
- **Concrete situation & mechanism (`IVSD-M005`):** Updating `Configurator.BrowserQualification` (`QualificationChecks.cs`, `CompoundRootBenchmarks.cs`) alongside `Configurator.Browser.Tests`, paired docs (`docs/public/browser.md`, `docs/internal/BROWSER_UI.md`, `src/Configurator.Browser/DESIGN.md`), and explicitly separating non-browser bUnit + compiled architecture checks from the separate Post-Implementation Published-Browser Qualification Lane and external human AT walkthroughs keeps repository qualification claims truthful.
- **Principle, domain & stakeholder:** `Sidq` (Epistemic Honesty); affects maintainers and reviewers relying on verification evidence.
- **Mapping:** Scenarios `S01`–`S06`; `TASK-3.1`, `TASK-3.2`, Post-Implementation Published-Browser Lane.

## Recommendations

- **Selected approach:** Unified Right-Side `WorkspaceUtilityRail` (`44px` collapsed edge stripe on desktop `> 60rem`, header `Tools` trigger `<= 60rem`) + 5-tab `DockScope.Workspace` End Dock (`Inspector`, `Document`, `Import`, `Profiles`, `Trust & Privacy`) backed by `BrowserProfileVaultController`, header-promoted `Secrets` chip, untouched-session onboarding micro-cue, removal of duplicate bottom `Language` and all 7 bottom accordions, and a quiet 1-line local-custody status footer.
- **Rejected alternatives:**
  1. *Always-open permanent 4th right sidebar alongside left App Nav + Form Nav + Form:* Rejected because 4 permanent columns (`256 + 256 + 420 + 280 = 1,212px+`) plus optional `Inspector`/`JSON` creates 5-column crowding on standard `1280px–1440px` desktops and violates calm single-column form focus.
  2. *Putting `Import`, `Profiles`, and `Trust & Privacy` exclusively inside blocking modal dialogs:* Rejected because `Import` comparison diffs, `Profiles` diffs, and `Trust & Privacy` consent benefit greatly from side-by-side non-blocking context beside form fields on desktop (while automatically becoming modal sheets via `DockOverlayHost` on mobile/narrow screens!).
  3. *Hiding `Import`, `Profiles`, and `Privacy` solely inside the `Advanced` dropdown:* Rejected because `Import`, `Profiles`, and `Trust & Privacy` are primary returning-user and custody workflows (not esoteric developer switches), risking "mystery meat" discoverability failure.

## Stakeholders

- **First-time operators ("Start fresh"):** Gain a calm, focused form ending cleanly at `Previous section` / `Next section` with zero bottom accordion sprawl.
- **Returning operators (`Import` / `.cfgvault` profiles):** Gain an immediate untouched-session micro-cue plus 1-click access to side-by-side `Import` and `Profiles` docks on any workflow stage (`Configure`, `Review`, `Files`).
- **Security-conscious administrators:** See secret lock posture continuously in `WorkspaceHeader` and unlock/revoke secrets right beside locked password fields without losing scroll position.
- **Keyboard, touch, low-vision & Arabic RTL users:** Benefit from `44×44px` labeled rail buttons, `Ctrl+K` commands, logical RTL mirroring, and automatic `DockOverlayHost` focus-trapped sheets on narrow screens.

## I-VSD Principles And Domains

- **Amanah (Stewardship):** Worker lifecycle extracted cleanly (`BrowserProfileVaultController`); passphrases cleared on view unmount/revocation; zero `localStorage`/URL leakage; secret entry stays strictly opt-in.
- **Sidq (Truthfulness):** Continuous header secret status pill + quiet 1-line local-custody footer (`declared outputs · Privacy & limits`); updated qualification probes and honest verification claims.
- **'Adl (Equitable Justice) & Ihsan (Care):** Eliminates 600px bottom clutter, duplicate `Language` selector, and scroll-jumping consent jumps while preserving `>= 420px` form canvas and `WCAG 2.2 AA` touch/keyboard/reflow invariants.

## Validation Gaps

- Physical screen-reader (`NVDA`/`Orca`/`VoiceOver`) walkthrough in EN & AR RTL remains an external human/AT gate beyond automated bUnit and Playwright Chromium verification (`IVSD-F005`).

## Escalation Needed

- None. All decisions remain within UI progressive disclosure, transient custody preservation, and WAI-ARIA/accessibility engineering (`AGENTS.md` §5, `QUICK_REFERENCE.md`).

## Evidence Reviewed

- `src/Configurator.Browser/Components/Configuration/{ConfigurationEditor,WorkspaceHeader,ConfigurationVaultPanel,ConfigurationInspectorPane,ConfigurationFieldHelp,ConfigurationNextActionsPanel}.razor(.cs/.css)`
- `src/Configurator.Browser/Components/Docking/{DockLayoutHost,DockSideHost,DockOverlayHost,DockTabStrip}.razor(.css)`
- `src/Configurator.Browser/Services/Docking/{BrowserDockPanels,DockPanelDescriptor,DockLayoutState}.cs`
- `src/Configurator.Browser/Layout/{NavMenu,WorkspaceSearch}.razor`
- `tests/Configurator.Browser.Tests/` (bottom panel, `#secret-consent`, `#configuration-locale`, and dock test callers)
- `eng/configurator/Configurator.BrowserQualification/{QualificationChecks,CompoundRootBenchmarks}.cs`
- `docs/public/browser.md`, `docs/internal/BROWSER_UI.md`, `src/Configurator.Browser/DESIGN.md`
- User live screenshot (`media_1795642847648_42e3e07e.png`)

## Missing Evidence

- Live rendered Chromium layout/scroll/focus verification of the 5-tab right dock, `44px` rail, and clean bottom boundary (executed directly in `TASK-3.2`).

## Context Inventory

- Active checkout on `main` `HEAD` `03051e3` (clean except unrelated pre-existing `M src/Configurator.Browser/Dockerfile.dockerignore`).
- Prior `browser-navigation-separation` workstream already completed (`43b2bf2`, `54132be`, `623bbca`) and archived at `dev/zarchive/browser-navigation-separation/`.
- Running Blazor Release host daemon `task-1451` (`https://localhost:7145/` & `http://localhost:5145/`).

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
|---|---|---|---|---|
| 2026-11-25 | `none` | `draft` | Initial I-VSD planning intake over `HEAD` `03051e3` and live workspace screenshot | `i-vsd-browser-workspace-utilities.md` (`planning` intake) |
| 2026-11-25 | `draft` | `current` | Completed `browser-workspace-utilities` triad revalidated across `plan.md`, `context.md`, and `tasks.md` (`IVSD-F001..F005` mapped cleanly) | `plan.md` (`4b7cf9c6...`), `context.md` (`8437a2a0...`), `tasks.md` (`48ea8f16...`) -> `plan-aligned` |
| 2026-11-27 | `stale` | `current` | Revalidated against 2026-11-26 Senior CTO-refined triad over `HEAD` `61df8bd`; strengthened `IVSD-F001..F005` mitigations (compile-time `ReadOnlyMemory<byte>` passphrase boundary, pre-await wipe, `CustodyAnchor` + capture-phase `.closest()` & synchronous in-process JS retirement, scoped `WeakRef` focus stack, atomic `OpenExclusive`, WAI-ARIA `aria-expanded`-only vertical rail with `.FocusAsync()`, `<div role="group">` controls wrapper, narrow `MudMenu` `Tools`) | `plan.md` (`e1756bb0...`), `context.md` (`85d4434b...`), `tasks.md` (`7c2ccd00...`) -> `plan-aligned` |

## Planning Handoff
- Workstream: `browser-workspace-utilities`
- Status: `current` (`plan-aligned`)
- Reviewed input: CTO-refined triad `dev/active/browser-workspace-utilities/browser-workspace-utilities-{plan,context,tasks}.md` (`e1756bb0...`, `85d4434b...`, `7c2ccd00...`) over `HEAD` `61df8bd`
- Findings and mitigations: `IVSD-F001 -> IVSD-M001`, `IVSD-F002 -> IVSD-M002`, `IVSD-F003 -> IVSD-M003`, `IVSD-F004 -> IVSD-M004`, `IVSD-F005 -> IVSD-M005`
- Required plan mappings:
  - `IVSD-F001` / `IVSD-M001` -> Scenarios `S02`, `S03`; `TASK-1.1`, `TASK-1.2`, `TASK-2.2`, `TASK-2.3`
  - `IVSD-F002` / `IVSD-M002` -> Scenarios `S03`, `S04`; `TASK-1.1`, `TASK-1.3`, `TASK-2.1`, `TASK-2.2`, `TASK-2.3`
  - `IVSD-F003` / `IVSD-M003` -> Scenarios `S02`, `S05`, `S06`; `TASK-1.4`, `TASK-1.5`, `TASK-2.1`, `TASK-2.2`, `TASK-3.2`
  - `IVSD-F004` / `IVSD-M004` -> Scenarios `S01`, `S04`, `S05`; `TASK-2.1`, `TASK-2.2`, `TASK-2.3`, `TASK-3.2`
  - `IVSD-F005` / `IVSD-M005` -> Scenarios `S01`–`S06`; `TASK-3.1`, `TASK-3.2`, Post-Implementation Published-Browser Lane
- Escalations required before: None (`implementation` gate requires Phase 3 Playwright probe parity + paired docs separation of automated vs published-browser vs human AT checks per `IVSD-F005`)
- Refresh triggers: Retaining typed passphrases inside `BrowserProfileVaultController` past view unmount, removing explicit per-workspace secret consent opt-in or header 1-click revoke, hiding privacy/export limits with no visible entry point, or omitting `44px` rail width from `DockLayoutState` floor math.
