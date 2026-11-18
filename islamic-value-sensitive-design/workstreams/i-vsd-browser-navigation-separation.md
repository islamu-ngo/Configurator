# Provider-Responsibility Assessment: Separating Global App Navigation from Persistent Sticky Form Section Navigation (`Configurator.Browser`)

> Configurator authority: follow [AGENTS.md](../../AGENTS.md) and [PROJECTS.md](../../PROJECTS.md). Source-system backend examples remain reference-only; absent Event runtime layers and deployment policies do not apply.

Last Updated: 2026-11-18

## Review Metadata
- Mode: planning
- Subject: Decoupling `Configurator.Browser` global App Navigation (`NavMenu` in `DockScope.Shell`) from persistent, independently scrollable Form Section Navigation (`ConfigurationNavigation` in `ConfigurationConfigurePanel`), with density-aware cross-scope `420px` form-canvas width protection (`790px` Comfortable / `806px` Spacious / `774px` Compact / `766px` Dense), priority-ordered dock allocation, overlay drawer Close affordance, top-aligned `WORKSPACE` drawer links, `ResizeObserver` header clearance, and `SettingsWorkspace`-parity `<= 60rem` responsive stacking
- Workstream: browser-navigation-separation
- Report kind: planning-assessment
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-11-18
- Reviewed input: CTO-refined triad `dev/active/browser-navigation-separation/` (`plan.md` SHA-256 `91897e0b97741f4ab90071979622d2fa7be3ea5c01d7ed3f98d7d7aa588f6317`, `context.md` SHA-256 `378894d5c4cb10afa08d1e8f2bb51a003ded60f5295b12a5eb9a2b5b49d716e6`, `tasks.md` SHA-256 `a79629ec43d420153984a06a7b5536b3f6a7a5342022fb5aa63b9a35a6646161`) over verified `main` baseline (`668/668` Browser tests)
- Supersedes: none

## Executive Summary

**Practical recommendation:** Proceed with separating `Configurator.Browser` navigation into three distinct desktop regions—collapsible global App Navigation (`NavMenu`, `256px` default `DockScope.Shell` Start drawer with an explicit overlay Close button), always-visible sticky Form Section Navigation (`ConfigurationNavigation` inside `ConfigurationConfigurePanel` with its own focus-safe vertical scrollbar), and the main Form Canvas (plus optional right-side Tabbed End dock for `Inspector` / `Document JSON`)—while enforcing priority-ordered, density-aware `420px` minimum form-canvas width protection (`790px`/`806px`/`774px`/`766px`) in `DockLayoutState`, dynamic `ResizeObserver` sticky header clearance, and synchronized public/internal docs (`docs/public/browser.md`, `docs/internal/BROWSER_UI.md`, `src/Configurator.Browser/DESIGN.md`), leaving mobile full-screen section index drill-down to a cleanly isolated follow-up pass.

**Who is affected and why this matters:** Operators configuring multi-section definitions (such as `ISLAMU Event` with 8+ sections) currently lose form section links whenever they collapse the global **Menu** button (`#application-navigation`), encounter nested `<nav>` landmarks (`ConfigurationNavigation` portaled via `<SectionOutlet>` inside `NavMenu`), see an awkward blank void above bottom-pinned `WORKSPACE` links in `Settings`, face an unclosable overlay `NavMenu` when degraded (`1280px` 4-panel or `<= 760px`) because `.nav-menu--docked .nav-menu__close` hides its Close button, and risk squeezing form inputs below `420px` (`~330px` at `1060px` under naive `692px` math ignoring `90px–106px` outer shell/editor padding) or triggering circular double-collapse at `1050px–1140px`. Screen-reader, keyboard-only, low-vision (`400%` zoom / `320px` reflow), and Arabic RTL operators are directly affected if sticky landmarks clip `:focus-visible` outlines, duplicate accessible names, obscure headings behind wrapped headers (`WCAG 2.4.11`), or trigger horizontal overflow.

**Central mechanism and confidence limit:** Safety and usability rest on keeping `<ConfigurationNavigation>` strictly in-memory inside `ConfigurationConfigurePanel` (no answer-bearing URL fragments or storage keys), eliminating `<nav>`-inside-`<nav>` nesting with unique landmark labels, bounding sticky vertical scrolling (`max-block-size` + `overscroll-behavior-y: contain` + `scrollbar-gutter: stable` + inner focus padding `--isl-space-1`), updating `--isl-shell-header-actual-height` via header `ResizeObserver`, un-sticking cleanly at `<= 60rem` (`960px`) into a compact wrapped pill bar for WCAG `1.4.10` Reflow, and coordinating `DockScope.Shell` / `DockScope.Workspace` via cycle-free descending `ResponsivePriority` allocation and `Unregister` scope-floor cleanup so `StartNav` (`ResponsivePriority: 10`) or `End` (`ResponsivePriority: 20`) degrades to overlay cleanly without double-collapse or compressing inputs below `420px`. Automated bUnit and CSS checks do not establish physical screen-reader or cross-browser AT certification; live Chromium inspection (`TASK-3.2`) stays a required verification gate.

## Scope

In scope for this planning review:
- Removing `<SectionOutlet SectionName="configuration-sections">`, `.nav-menu__sections`, and `CloseSectionNavigation()` from `NavMenu.razor(.css)` so `#application-navigation` exclusively serves top-aligned `.nav-menu__workspace` links (`Choose a project`, `Open a local file or public link`, `Help`) with bottom-pinned `.nav-menu__footer` (`Local workspace` custody notice), short-viewport `overflow-y: auto`, and a visible `.nav-menu__close` control whenever `DockEntry` renders inside `DockOverlayHost` (`MainLayout.OverlayCloseRequested` restoring focus to `#nav-menu-toggle`).
- Adjusting `BrowserDockPanels.StartNav` `DefaultWidth` (`310` -> `256` px) and `tokens.css` (`16rem` / `256px` + `--isl-sticky-top-offset`), replacing `DockLayoutState`'s asymmetric checks with a cycle-free descending `ResponsivePriority` allocator and `Unregister` scope-floor cleanup, and computing density-aware `WorkspaceMinimumContentWidth` (`790px` Comfortable / `806px` Spacious / `774px` Compact / `766px` Dense, else `420px`) in `ConfigurationEditor` so form inputs always keep `>= 420px`.
- Moving `<ConfigurationNavigation>` directly inside `ConfigurationConfigurePanel.razor(.css)` as a sticky, focus-safe, independently scrollable desktop column (`> 60rem`) beside `.configuration-configure-panel__main` (containing segmented `[data-layout-switcher]` + `.configuration-configure-panel__fields`), preserving `ActiveScrollspySectionId` across `AllSections -> SectionBySection`, displaying a restrained visible `Reviewed` indicator without colored accent stripes (`DESIGN.md`), and transitioning to a compact wrapped pill bar at `<= 60rem`.
- Deleting dead `syncCompactToolbarClearance` / `.configuration-compact-workspace__toolbar` code in `configuration-rendering.js`, adding `.nav-menu__header.navbar` `ResizeObserver` (`--isl-shell-header-actual-height`) and jump-safe scrollspy (`rootMargin` alignment + bottom reachability + container-only `syncActiveNavScroll`), aligning `BrowserFixture.SectionNavigation<T>`, `BrowserDockLayoutTests`, `BrowserShellTests`, and `BrowserConfigurationNavigationTests`, and syncing dual documentation (`docs/public/browser.md`, `docs/internal/BROWSER_UI.md`, `src/Configurator.Browser/DESIGN.md`).

Out of scope:
- `Configurator.Desktop` (`MainWindow.axaml`), which already places global stage tabs in the top bar and section selection in `SplitView.Pane`.
- Mobile full-page section-index -> full-screen section detail drill-down (`<= 47.5rem`), explicitly deferred by user agreement to a follow-up mobile UX increment enabled cleanly by this decoupling.
- Any Core definition semantics, answer persistence, browser storage additions, profile worker protocol changes, or Event backend dependencies.

## Claim Boundary

This assessment evaluates **provider-controlled software design responsibilities** under Sunni Islamic ethical principles (`Amanah` / trustworthy stewardship, `Sidq` / truthfulness, `'Adl` / equitable justice, `La darar` / non-harm, `Ihsan` / clarity and care). It is **not** a religious-legal ruling (`fatwa`), a Sharia-compliance certificate, or human assistive-technology certification.

## Common Overlooked Failures And Outcomes

1. **Nested `<nav>` Landmarks Masked by Portal Refactoring:** Leaving wrapper `<nav>` around `<ConfigurationNavigation>` or unnamed dual `<nav>` elements confuses screen-reader landmark lists.
2. **Simultaneous 4-Column Squeeze (`StartNav + FormNav + FormFields + EndDock`):** Checking only `420px` inside `DockScope.Workspace` when `FormNav` (`~256px` + gap) occupies ~`272px` allows `1280px` viewports to compress inputs to `~274px` unless `DockLayoutState` coordinates cross-scope `MinimumContentWidth`.
3. **`overflow: hidden` Ancestor Breaking `position: sticky` or `<Virtualize>`:** Adding overflow clipping on layout wrappers silently disables both sticky form navigation and continuous `IntersectionObserver` section virtualization.
4. **Sticky Topbar Covering Scrolled Section Headings:** Jumping to a section or focusing headings without matching `scroll-margin-block-start` (`4rem` desktop, `7.25rem` navbar wrap `<= 60rem`) scrolls target headings beneath the frosted header.
5. **Unbounded Sticky Rail Overflow at 400% Zoom / 320px:** Retaining sticky height constraints below `60rem` or on short viewports traps content or causes 2D scrolling (`WCAG 1.4.10`).

## Findings

| Finding ID | Title | Lifecycle | Severity | Claim type | Mitigation ID |
|---|---|---|---|---|---|
| `IVSD-F001` | Transient Answer & Secret Custody Across Independent Navigation Tiers & Settings Overlay | Open | Critical | Security & custody design | `IVSD-M001` |
| `IVSD-F002` | Equitable Landmark Hierarchy, Sticky Focus Clearance, Arabic RTL Parity, And WCAG 1.4.10 Reflow | Open | High | Accessibility & justice gap | `IVSD-M002` |
| `IVSD-F003` | Non-Deceptive Progress And 4-Column Canvas Floor Protection Against Input Crushing | Open | High | Truthfulness & harm prevention | `IVSD-M003` |
| `IVSD-F004` | Honest Verification Limits Between Automated bUnit Checks And Live Chromium/AT Qualification | Open | Medium | Governance & candor | `IVSD-M004` |

### IVSD-F001: Transient Answer & Secret Custody Across Independent Navigation Tiers & Settings Overlay
- **Concrete situation & mechanism (`IVSD-M001`):** Decoupling `#application-navigation` (`NavMenu`) from `<ConfigurationNavigation>` must not change `DefinitionFormModel`’s in-memory custody or introduce URL hash/query state or `localStorage` entries for active section positions, answers, or secret consent. Moving `<ConfigurationNavigation>` out of `MainLayout`'s Start dock into `<ConfigurationConfigurePanel>` ensures global `Settings` in-place overlay (`hidden`/`inert` `<main>`) continues to hide/preserve both form navigation and form fields cleanly without calling `_editor?.Invalidate()`, while navigating away via `#application-navigation` (`Choose a project`, `Open...`, `Help`) retains the existing `WorkspaceNavigationGuard` discard boundary.
- **Principle, domain & stakeholder:** `Amanah` (Custody), Privacy & Custody Architecture; affects self-hosting administrators entering credentials offline.
- **Mapping:** Plan Scenarios `S01`, `S04`; `TASK-1.2`, `TASK-2.2`, `TASK-3.1`.

### IVSD-F002: Equitable Landmark Hierarchy, Sticky Focus Clearance, Arabic RTL Parity, And WCAG 1.4.10 Reflow
- **Concrete situation & mechanism (`IVSD-M002`):** Previously, rendering `<ConfigurationNavigation>` inside `#application-navigation` nested `<nav>` inside `<nav>`, removed section navigation whenever `Menu` collapsed, left overlay `StartNav` drawers without a visible internal Close button (`.nav-menu--docked .nav-menu__close` was hidden), and clipped focus indicators / short viewports via `overflow: hidden`. Rendering `<ConfigurationNavigation>` as a sibling navigation landmark inside `.configuration-configure-panel__nav` establishes clean, uniquely labeled `<nav>` landmarks (`aria-label="Application navigation"` vs `aria-label="Sections"`), exposes `.nav-menu__close` inside `DockOverlayHost` with consistent focus return to `#nav-menu-toggle` via `MainLayout.OverlayCloseRequested`, allows short-viewport scrolling (`overflow-y: auto`), adds focus-ring-safe inner rail padding (`--isl-space-1`) + `scrollbar-gutter: stable` + container-only `syncActiveNavScroll`, binds sticky vertical positioning and `scroll-margin-block-start` to live `ResizeObserver`-tracked `--isl-shell-header-actual-height` (`WCAG 2.4.11`), mirrors columns logically for Arabic RTL (`dir="rtl"`), and collapses to a compact non-sticky wrapped chip bar at `@media (max-width: 60rem)` for WCAG `1.4.10` Reflow (`320px` / `400%` zoom).
- **Principle, domain & stakeholder:** `'Adl` (Equitable Justice) & `Ihsan` (Care), Accessibility & Localization; affects screen-reader, keyboard-only, low-vision, and Arabic RTL operators.
- **Mapping:** Plan Scenarios `S01`, `S02`, `S03`, `S04`, `S06`, `S07`; `TASK-1.3`, `TASK-2.1`, `TASK-2.2`, `TASK-2.3`, `TASK-3.1`, `TASK-3.2`.

### IVSD-F003: Non-Deceptive Progress And 4-Column Canvas Floor Protection Against Input Crushing
- **Concrete situation & mechanism (`IVSD-M003`):** Adding a persistent `13–16rem` Form Section Navigation column inside `DockScope.Workspace` alongside `StartNav` (`NavMenu`) and optional `Inspector`/`Document (JSON)` (`End` dock, `360px` default) would squeeze usable form-field width to `~274px`–`330px` at `1060px–1280px` under naive `420px`/`692px` math because `MainLayout` (`40px`) + `ConfigurationEditor` (`48px–64px` + `2px` border) consume `90px–106px` outside `DockScope.Workspace`, and `DockLayoutState`'s asymmetric `Unchecked` check would circularly double-collapse both `StartNav` and `Inspector` between `1050px` and `1140px`. Slimming `StartNav` (`310` -> `256` px, `tokens.css` `16rem`/`256px`), top-aligning `.nav-menu__workspace` links above a bottom-pinned `.nav-menu__footer` trust card, placing segmented `[data-layout-switcher]` in `.configuration-configure-panel__main`, replacing `DockLayoutState`'s asymmetric checks with cycle-free descending `ResponsivePriority` allocation and `Unregister` scope-floor cleanup, and computing density-aware `WorkspaceMinimumContentWidth` (`790px` Comfortable / `806px` Spacious / `774px` Compact / `766px` Dense while persistent Form Nav renders above `960px`, else `420px`) ensures `StartNav` (`ResponsivePriority: 10`) then `End` (`ResponsivePriority: 20`) degrade cleanly to overlay drawers without double-collapse or compressing inputs below `420px`, while keeping a visible `Reviewed` indicator and tabular progress numbers truthful without colored accent stripes (`DESIGN.md`).
- **Principle, domain & stakeholder:** `Sidq` (Truthfulness) & `La darar` (Non-harm), Usability & Trustworthy UI; affects laptop (`1050px`–`1366px`) and multi-panel power users.
- **Mapping:** Plan Scenarios `S04`, `S05`; `TASK-1.1`, `TASK-1.2`, `TASK-1.3`, `TASK-2.1`, `TASK-2.2`.

### IVSD-F004: Honest Verification Limits Between Automated bUnit Checks And Live Chromium/AT Qualification
- **Concrete situation & mechanism (`IVSD-M004`):** bUnit tests run without a real CSS layout engine, so automated C# tests cannot measure sticky scroll pinning, independent scrollbar behavior, `ResizeObserver` header tracking, `IntersectionObserver` scrollspy hitboxes, `400%` zoom reflow, or real screen-reader announcement order. Plan execution pairs bUnit behavioral checks with live WASM Playwright/Chromium viewport inspection (`1440px`, `1366px`, `1280px`, `1050px`, `960px`, `390px`, `320px` in EN & AR RTL + Light/Dark HC) and updates `docs/public/browser.md`, `docs/internal/BROWSER_UI.md`, and `src/Configurator.Browser/DESIGN.md` honestly without conflating automated DOM checks with physical assistive-technology certification.
- **Principle, domain & stakeholder:** `Sidq` (Epistemic Honesty), Assurance & Governance; affects maintainers, reviewers, and operators relying on support claims.
- **Mapping:** Plan Scenarios `S05`, `S06`, `S07`; `TASK-3.2`.

## Recommendations

- **Selected approach:** Implement decoupled 3-column desktop architecture using intrinsic CSS Grid + `position: sticky` inside `ConfigurationConfigurePanel`, slimmer `256px` top-aligned `NavMenu` Start dock with overlay Close control, cycle-free priority-ordered `DockLayoutState` allocation with density-aware `790px`–`806px` canvas protection while persistent Form Nav renders above `960px`, `ResizeObserver` dynamic header clearance, and `SettingsWorkspace`-parity `<= 60rem` compact wrapped chip stacking, synced across `docs/public/browser.md`, `docs/internal/BROWSER_UI.md`, and `src/Configurator.Browser/DESIGN.md`.
- **Rejected alternatives:**
  1. *Nesting another resizable `DockLayoutHost` `Start` dock inside `DockScope.Workspace`:* Rejected because `SettingsWorkspace` already establishes lightweight sticky CSS Grid (`minmax(11–15rem)`) for section rails, whereas nested `DockLayoutHost` instances double resize splitters, complicate `IntersectionObserver` roots, and add unwarranted `localStorage` persistence chrome for non-closable form sections.
  2. *Keeping `NavMenu` collapsed automatically on `/launch` entry:* Rejected because silently overriding user-opened `StartNav` breaks predictability; responsive priority overlay (`ResponsivePriority: 10`) only triggers when horizontal budget actually drops below the safe `420px` field-canvas floor.
  3. *Implementing mobile full-screen section drill-down immediately in this workstream:* Deferred per user alignment; decoupling `<ConfigurationNavigation>` out of `NavMenu` cleanly unlocks that follow-up mobile phase without shell coupling.

## Stakeholders

- **End users / self-hosting operators:** Need uninterrupted access to section navigation (`Essentials`, `Hosting...`) while scrolling long forms or hiding global workspace chrome, plus clear mobile reflow.
- **Assistive-technology, keyboard & low-vision users:** Rely on distinct sibling `<nav>` landmarks, preserved focus flow when toggling `Menu`, sticky heading clearance, and clean `<= 60rem` / `400%` reflow.
- **Arabic RTL operators:** Require full mirror symmetry across 3 columns, scrollbar gutters, and overlay drawers.
- **Maintainers & QA engineers:** Rely on truthful `DockLayoutState` layout math, clean `NavMenu` separation, and accurate test/doc contracts.

## I-VSD Principles And Domains

- **Amanah (Stewardship):** Preserves strict in-memory answer custody (`DefinitionFormModel`) and zero answer-bearing storage/URL state across independent shell/section/Settings transitions.
- **Sidq (Truthfulness):** Keeps `DockLayoutState` floor math, section progress badges, and verification scope claims empirically accurate.
- **'Adl (Equitable Justice) & Ihsan (Care):** Eliminates `<nav>` nesting, prevents cramped input columns on `1280px` screens, preserves sticky reading context, and aligns `NavMenu` cleanly on empty routes (`Settings`, `Catalogue`).

## Validation Gaps

- Physical screen-reader (`NVDA`/`Orca`/`VoiceOver`) walkthrough across EN and AR RTL remains an external human/AT gate beyond bUnit and Chromium DOM inspection (`IVSD-F004`).

## Escalation Needed

- None. All provider-responsibility aspects stay strictly within UI layout geometry, WAI-ARIA landmarks, transient answer custody, and documented test/doc verification (`AGENTS.md` §5, `QUICK_REFERENCE.md`). No scholarly/legal (`fiqh`) determination is required.

## Evidence Reviewed

- `src/Configurator.Browser/Layout/MainLayout.razor` & `MainLayout.razor.css`
- `src/Configurator.Browser/Layout/NavMenu.razor` & `NavMenu.razor.css`
- `src/Configurator.Browser/Components/Docking/{DockLayoutHost,DockSideHost,DockPanelHost}.razor(.css)`
- `src/Configurator.Browser/Services/Docking/{BrowserDockPanels,DockLayoutState,LocalStorageDockLayoutPersistence}.cs`
- `src/Configurator.Browser/Components/Configuration/{ConfigurationEditor,ConfigurationConfigurePanel,ConfigurationNavigation}.razor(.cs/.css)`
- `src/Configurator.Browser/Components/Settings/SettingsWorkspace.razor(.css)` & `/home/amir/ISLAMU/Github/Event/src/Explore.Blazor.Client/Pages/User/Components/SettingsLayout.razor(.css)`
- `src/Configurator.Browser/wwwroot/{css/tokens.css,css/utilities.css,js/configuration-rendering.js}`
- `src/Configurator.Desktop/Views/MainWindow.axaml` & `ConfigurationWorkspaceView.axaml`
- `tests/Configurator.Browser.Tests/{Fixtures/BrowserFixture.cs,Docking/BrowserDockLayoutTests.cs,Navigation/BrowserConfigurationNavigationTests.cs,Navigation/BrowserShellTests.cs}`
- `docs/internal/BROWSER_UI.md` & `src/Configurator.Browser/DESIGN.md`

## Missing Evidence

- Live rendered Chromium layout/scroll geometry measurements for the new 3-column/`1280px` 4-panel configurations (`TASK-3.2` will capture and verify directly against running Release WASM output).

## Context Inventory

- Active checkout on `main` holding committed Wave 1 + uncommitted Wave 2 (`configuration-experience-unification`) + sticky-dock refinements (`DockSideHost.razor.css`, `DockPanelHost.razor.css`, `NavMenu.razor.css`) + live local Release host (`task-1285` on `https://localhost:7145`).
- No competing active workstream in `dev/active/` addresses separating global `NavMenu` from `ConfigurationNavigation`.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
|---|---|---|---|---|
| 2026-11-18 | `none` | `draft` | Initial I-VSD planning intake over inspected codebase evidence | `i-vsd-browser-navigation-separation.md` (`planning` intake) |
| 2026-11-18 | `draft` | `current` | Completed `browser-navigation-separation` triad revalidated across `plan.md`, `context.md`, and `tasks.md` (`IVSD-F001..F004` mapped cleanly) | `plan.md` (`31afca1e...`), `context.md` (`b109ae53...`), `tasks.md` (`7fd6250f...`) -> `plan-aligned` |
| 2026-11-18 | `stale` | `current` | Post-CTO audit planning revalidation (`790px`–`806px` density-aware floor, priority dock allocator, `NavMenu` overlay Close affordance, `ResizeObserver` header clearance, `TASK-1.3`, `docs/public/browser.md` parity) | `plan.md` (`91897e0b...`), `context.md` (`378894d5...`), `tasks.md` (`a79629ec...`) -> `plan-aligned` |

## Planning Handoff
- Workstream: `browser-navigation-separation`
- Status: `current` (`plan-aligned`)
- Reviewed input: CTO-refined triad `dev/active/browser-navigation-separation/browser-navigation-separation-{plan,context,tasks}.md` (`91897e0b...`, `378894d5...`, `a79629ec...`)
- Findings and mitigations: `IVSD-F001 -> IVSD-M001`, `IVSD-F002 -> IVSD-M002`, `IVSD-F003 -> IVSD-M003`, `IVSD-F004 -> IVSD-M004`
- Required plan mappings:
  - `IVSD-F001` / `IVSD-M001` -> Scenarios `S01`, `S04`; `TASK-1.2`, `TASK-2.2`, `TASK-3.1`
  - `IVSD-F002` / `IVSD-M002` -> Scenarios `S01`, `S02`, `S03`, `S04`, `S06`, `S07`; `TASK-1.3`, `TASK-2.1`, `TASK-2.2`, `TASK-2.3`, `TASK-3.1`, `TASK-3.2`
  - `IVSD-F003` / `IVSD-M003` -> Scenarios `S04`, `S05`; `TASK-1.1`, `TASK-1.2`, `TASK-1.3`, `TASK-2.1`, `TASK-2.2`
  - `IVSD-F004` / `IVSD-M004` -> Scenarios `S05`, `S06`, `S07`; `TASK-3.2`
- Escalations required before: None (`implementation` gate requires Phase 3 live Chromium inspection per `IVSD-F004`)
- Refresh triggers: Any change to answer custody/URL persistence, addition of browser storage keys, removal of `<= 60rem` responsive collapse, overlay Close affordance, or density-aware cross-scope `420px` field-canvas floor protection, or scope expansion to `Configurator.Desktop` / mobile drill-down.
