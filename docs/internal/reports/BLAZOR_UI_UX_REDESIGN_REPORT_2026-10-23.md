# Configurator Blazor UI and UX: audit and redesign recommendations

> **Date:** October 23, 2026
> **Audience:** Product owner, designers and Blazor contributors
> **Status:** Research and recommendations, not an implemented design or release qualification
> **Scope:** The whole Configurator browser application, with ISLAMU Event as the first-party design reference
> **Compatibility policy:** Pre-release. Replace poor presentation contracts rather than preserve them through compatibility layers.

## Reading guide

- [Assessment and findings](#1-executive-assessment), [evidence limits](#2-evidence-coverage-and-limitations), [improvement inventory](#3-findings-and-improvement-inventory).
- [Event conventions](#4-what-follow-event-conventions-should-mean), [experience model](#5-product-model-separate-the-different-meanings-of-experience), [shell](#6-information-architecture-and-shell).
- [Density and colors](#7-density-spacing-and-visual-hierarchy), [MudBlazor strategy](#8-mudblazor-component-strategy), [field design](#9-field-semantics-and-better-input-design), [help](#10-help-without-layout-movement).
- [Sectioning](#11-sectioning-and-questionnaire-quality), [search](#12-search-as-first-class-navigation), [journeys](#13-guided-section-and-continuous-page-journeys), [form/table layouts](#14-form-and-property-table-layouts).
- [Themes and personalization](#15-theme-management-personalization-and-customization), [review/export](#16-review-validation-and-export), [every route](#17-improvements-across-every-route).
- [Accessibility/mobile/RTL](#18-responsive-design-accessibility-and-localization), [performance](#19-performance-is-part-of-the-ux-contract), [architecture](#20-architecture-and-concrete-ownership).
- [Delivery sequence](#21-recommended-delivery-sequence), [acceptance criteria](#22-verification-and-acceptance-criteria), [anti-patterns](#23-what-should-not-be-done), [sources](#24-source-register), [completion and gaps](#25-audit-completion-and-remaining-evidence).

## 1. Executive assessment

**Configurator needs a coherent workspace redesign, not another round of isolated spacing changes.** Its functional capabilities are substantially ahead of the interface that exposes them. Configuration, discovery, disclosure, consent, validation, review and export are individually represented, but they compete for the same vertical space and do not read as one deliberate product.

The strongest source-backed problems are:

1. **The first screen spends too much space explaining and controlling the form before presenting useful questions.** Page identity, workspace identity, output notices, privacy disclosure, workflow navigation, answer progress, experience selection, optional disclosure, secret consent and optional import all precede the field region. [C2]
2. **Field rendering accumulates visual overhead.** Each field can carry a label, required/optional annotation, large control, omission action, help, credential guidance, expandable instructions, error and expandable definition key. Objects and lists recursively repeat that structure. [C3]
3. **Search is treated as advanced functionality instead of primary navigation.** It is hidden inside an initially closed disclosure below the workspace controls. Opening it inserts controls and a potentially long result list above the form. [C4]
4. **The workflow appearance overstates the separation between stages.** Configure, Review and Prepare are navigation buttons, but the editor, review region and preparation region remain parts of one document. Selecting a stage changes state and focus, rather than presenting a clean stage-specific screen. [C2, C5]
5. **An authored experience is not a complete interaction mode.** Existing experiences select disclosure, section membership and two navigation values. The browser still mounts one selected section. There is no general continuous-page mode, property-table mode or independently selectable density. [C2, C6]
6. **MudBlazor is present but does not govern the whole control language.** Providers, icons and button/card wrappers coexist with extensively styled native inputs, selects, checkboxes, disclosures and action buttons. This is not automatically wrong, but the visual and interaction conventions are not unified. [C1-C4, C7]
7. **Theme infrastructure exists, but personalization is rudimentary.** Current preferences support five appearance modes and direction; there is no Event-like preset/customization experience or workspace layout preference model. [C8]
8. **Sectioning exists structurally but needs better presentation and authoring.** The UI has section navigation, but descriptions and subgroup relationships are underused, while nested fieldsets dominate complex objects. [C2, C3, C6, C9]

The recommended destination is an **Event-aligned, compact, customizable configuration workspace**:

- Persistent, prominent search in the top application bar.
- A compact workspace header and clear section navigation.
- Independent controls for **journey**, **field layout**, **detail level** and **density**.
- Guided steps, section-based editing and a genuine continuous-page experience.
- Standard forms and a compact property-table experience, with appropriate collection editors.
- Contextual help that does not push the form around.
- Event-style theme presets, quick switching and a customization drawer, implemented locally.
- A focused review and export flow with understandable status and recovery.
- One shared answer model across every presentation.

**Do not equate redesign with making everything smaller.** Reduce repeated chrome, unnecessary actions and wasted whitespace first. Preserve readable labels, touch targets, visible focus, error recovery and the distinction between an omitted value, an empty value and an explicit false value.

## 2. Evidence, coverage and limitations

### 2.1 What was inspected

The audit inspected the source of every browser route: Home, Definition Store, Store project, Open configuration, Launch/workspace and Help. It also inspected the shell, appearance state/service, global and isolated styling, dynamic fields, section navigation, discovery, review, exports, navigation-loss guard, shared experience/projection owners and relevant existing test specifications.

The Event comparison covered its actual Blazor shell, top-bar search, theme menu, appearance settings, customization drawer, wrappers, token conventions, table and wizard examples, errors, dialogs and accessibility mechanisms. The main reference components were independently read in the parent investigation; the broader Event evidence was collected in a separate read-only investigation.

Recorded repository identities:

| Repository | Branch / revision | Qualification |
| --- | --- | --- |
| Configurator | `main`, `e10037e7cd79f18f2625c03e39a241a3a9012f2c` | Working tree was clean at intake. |
| Event | `develop`, `fefcea5f16e3bd723e22076d505a65c696c4e40b` | HEAD recorded; Event working-tree cleanliness was not established. References describe inspected files. |

Configurator centrally pins **MudBlazor 9.7.0**, not an assumed latest release. Recommendations use that baseline. Relevant APIs were checked against its installed XML documentation in addition to current official documentation. [C10, M1]

### 2.2 Visual evidence

Three retained final-package screenshots were opened and inspected:

- `artifacts/browser-performance/evidence/final-indexed-edit-review-light-desktop.png`
- `artifacts/browser-performance/evidence/final-indexed-edit-review-light-390.png`
- `artifacts/browser-performance/evidence/final-indexed-edit-correction-dark-rtl-320.png`

They show the synthetic scale fixture, not a complete production-like Event questionnaire. They visibly illustrate the large repeated outlined actions, substantial vertical separation, review beneath the editor and deep nesting on narrow screens. The RTL capture is particularly useful for seeing how object/list boundaries consume editing width.

These are **retained evidence from the earlier frozen candidate**, not fresh screenshots produced by this audit. Their provenance is recorded in `docs/internal/OPERATIONS.md:399-487`. They support observations about those captured states; they do not establish current behavior across every route.

A fresh browser session was attempted. The BrowserSkill daemon was available, but no browser extension was connected. No browser configuration was changed and no substitute browser was launched. Consequently:

- No fresh hover, keyboard, focus, zoom, animation or touch behavior was certified.
- No new contrast ratios or layout-shift measurements were taken.
- No new usability study or screen-reader session was conducted.
- Product builds and test suites were not run for this report-only task.

### 2.3 How to read the findings

- **Observed source behavior:** supported by a concrete source reference.
- **Visual observation:** limited to the named retained screenshots.
- **Recommendation:** a proposed design decision, not a claim that implementation exists.
- **Verification requirement:** work a future implementation must demonstrate.

Priorities below are product/design priorities, not assertions of exploitable security vulnerabilities. `P0` means foundational to the redesign; `P1` means required for a coherent first delivery; `P2` means valuable after the core experience works.

The code graph was available but built against an older revision. Current source, rather than stale graph results, supplied the findings.

## 3. Findings and improvement inventory

| ID | Priority | Current problem and evidence | Recommended improvement |
| --- | --- | --- | --- |
| UX-01 | P0 | Workspace controls and explanations create a long preamble before editing. [C2] | Compress identity, readiness and workspace tools into a small header and toolbar; move secondary detail into named overlays. |
| UX-02 | P0 | Search is initially hidden under "Advanced settings and search." [C4] | Put a persistent setting-search entry in the appbar; keep advanced facets secondary. |
| UX-03 | P0 | Opening search inserts results above the editor. [C2, C4] | Use an anchored result surface or dedicated search panel without moving the question being edited. |
| UX-04 | P0 | Field margins declare 1.25rem above and 2rem below, with more spacing inside each field. [C11] | Introduce explicit density tokens and a measured field rhythm; remove repeated structural overhead. |
| UX-05 | P0 | Help and keys use inline disclosures; opening them changes document height. [C3] | Short tooltip on hover/focus, click/tap help popover, and a stable inspector for longer content. |
| UX-06 | P0 | Guided and section experiences share the same selected-section rendering. [C2, C6] | Implement distinct journey renderers and an actual continuous-page option. |
| UX-07 | P0 | Workflow stages share a long document; stage selection focuses headings. [C2, C5] | Separate workflow stage presentation from section navigation and answer progress. |
| UX-08 | P1 | Native and Mud controls use different conventions. [C3, C7] | Adopt Event-like thin wrappers and component defaults; qualify heavier controls against the existing performance gates. |
| UX-09 | P1 | Boolean and enum fields share a generic select with "Not supplied." [C3] | Choose controls by semantics and choice count while preserving explicit absence. |
| UX-10 | P1 | Every optional editor can add a prominent "Omit" button. [C3] | Represent use/omit state compactly; place reset and advanced actions in a consistent menu. |
| UX-11 | P1 | Recursive object/list fieldsets consume width and multiply borders. [C3, screenshots] | Flatten visual nesting, use subsection headings and compact collection summaries. |
| UX-12 | P1 | Section descriptions exist in shared metadata but are not carried by the browser's `EditorSection` projection. [C5, C6] | Render useful authored descriptions and expose meaningful subgroups. |
| UX-13 | P1 | Section navigation repeats several progress sentences per item. [C9] | Compact title/count/status, with additional detail available on demand. |
| UX-14 | P1 | The desktop shell reserves a 15rem application sidebar alongside the configuration section navigation. [C11] | Give editing priority; use a collapsible application rail and a context-specific section panel. |
| UX-15 | P1 | Header space is occupied by two full preference selects. [C8] | Event-style theme icon/menu plus a separate appearance/customization surface. |
| UX-16 | P1 | No independently selectable form/table layout or density exists in inspected presentation owners. [C6, C8] | Add orthogonal layout and density preferences rather than more monolithic "experiences." |
| UX-17 | P1 | Review is a tall list of status text, repeated Change buttons and output-binding disclosures. [C12] | Use compact grouped review rows, filters and an inspector for technical detail. |
| UX-18 | P1 | Explanations repeatedly restate privacy and target-acceptance boundaries. [C2, C12-C14] | Keep a concise persistent status and explain consequences at consent/export actions; retain full detail in accessible help. |
| UX-19 | P1 | Diagnostic messages can fall back to generic instructions and append codes to all explanations. [C15] | Use specific corrective text from admitted constraints; move codes into expandable technical detail. |
| UX-20 | P1 | Complete preparation, draft preparation and clearing share one action region. [C2] | Establish one primary next action; separate destructive clearing and explain draft readiness. |
| UX-21 | P1 | Internal departure uses a native JavaScript confirmation. [C16] | Use a themed confirmation dialog with precise loss language; retain native external-unload protection. |
| UX-22 | P1 | Store/project/open flows expose repeated identity and compatibility detail before editing. [C13, C14] | Use concise definition summaries and a technical-details panel without weakening admission. |
| UX-23 | P1 | Mobile reflow exists, but nested structures and repeated actions remain lengthy. [C11, screenshots] | Design mobile as a specific interaction model, not only a stacked desktop grid. |
| UX-24 | P1 | Persistent progress and status can update while the user types. [C2, C5] | Separate quiet typing feedback from meaningful announcements; verify actual speech output. |
| UX-25 | P2 | Help is a short static route; no searchable presentation settings experience exists. [C8, C17] | Add task-oriented help and a searchable customization surface. |
| UX-26 | P1 | A component-library conversion could undo recent scale improvements. [C18] | Treat performance as a redesign acceptance criterion, not a later optimization phase. |

These findings reinforce each other. Reducing margins alone leaves the hidden search, repeated action buttons and confused stage model intact. Replacing inputs alone leaves the same information architecture and can increase rendering cost.

## 4. What follow Event conventions should mean

Event is a useful first-party reference because its interface has reusable mechanisms, not merely a nicer palette.

| Convention | Event evidence | Configurator direction |
| --- | --- | --- |
| Search in the appbar | `E1` | Reuse the prominence, outlined/dense treatment and responsive search entry. Search settings locally rather than navigate to Event endpoints. |
| Quick appearance menu | `E2` | Replace two large header selects with mode/preset controls and a Full settings entry. |
| Preset cards and custom colors | `E3` | Provide local preset selection, preview and bounded custom palette controls. |
| Three-tier tokens and ordered CSS layers | `E4` | Preserve the existing transferred foundation; add missing semantic density/control/layout tokens instead of another parallel system. |
| Thin field wrappers | `E5` | Standardize outlined inputs, labels, errors, density, attributes and callbacks in Configurator-owned wrappers. |
| Customization drawer | `E6` | Offer journey, layout, density, detail, key visibility and width controls with immediate preview and reset. |
| Collection view choices | `E6` | Apply the principle of selectable representations; design property tables and collection editors for configuration data. |
| Dense responsive tables | `E7` | Use clear column labels and a readable narrow-screen projection for suitable collections. |
| Step-based form with review | `E8` | Use meaningful configuration steps, validation feedback and review/correction return. |
| Searchable settings / View all | `E9` | Make customization easy to find and inspect without leaving a live configuration workspace. |
| Common loading, error and dialog patterns | `E10` | Reuse consistent composition, recovery and focus conventions, without server/network assumptions. |
| Focus, reduced motion and forced-colors mechanisms | `E11` | Carry the mechanisms into browser-safe local UI and qualify their rendered behavior. |

### 4.1 Important differences not to hide

Event's appbar search is context-sensitive **event search**, not an application-wide command palette. It appears in selected workspaces and routes to event search pages. Configurator should adopt its placement and usability, not claim to reuse an already-existing generic command/search service. [E1]

Event implements **Single Row, Detailed List and Compact Grid** for event collections. Those are not a generic dynamic-form/table switch. The configuration property-table experience is new product work. No app-wide user-selectable density preference was established in the inspected Event appearance/settings owners. [E6]

Event's appearance service loads resolved themes and profiles through APIs. Custom palette generation is server-backed; its synchronous preview path is not a ready-made offline palette generator. Configurator needs the same quality of theme interaction with an independently local implementation. [E3, E12]

Do not import Event authentication, BFF services, generated API clients, tenant/actor scopes, administrative theme authority, server persistence, cookies or Interactive Server/Auto hosting. Configurator remains static Blazor WebAssembly with local transient answers. [C1, C19]

Also do not copy incidental inconsistencies. For example, the inspected Event search advertises a Command-key shortcut in placeholder text while the shown hotkey binds Control. Configurator should implement and test the actual supported platform shortcuts. [E1]

**Parity should be judged by a written checklist and side-by-side rendered comparison**, not "we used MudBlazor" or "the colors are close."

## 5. Product model: separate the different meanings of experience

The current experience model bundles concepts that should be independent. A user can want advanced fields but still prefer steps; another can want essential fields in a continuous page; an expert can want a compact table without changing which fields apply.

### 5.1 Recommended independent axes

| Axis | Options | What changes | What must not change |
| --- | --- | --- | --- |
| Journey | Guided steps / Sections / All sections | Navigation and page composition | Answers, applicability, validation and output semantics |
| Field layout | Form / Property table | How an editable question is displayed | Parsed types, omission, sensitivity and provenance |
| Detail level | Essential / Standard / All active settings | Initial disclosure of optional work | Required/correction reachability and actual activation |
| Density | Comfortable / Compact | Padding, gaps and supported control density | Text readability, accessible targets and errors |
| Content width | Focused / Wide | Editing measure and column availability | Reading order and semantic grouping |
| Help | Contextual / Inspector open | Where extended help appears | Availability to keyboard, touch and assistive technology |
| Technical detail | Friendly labels / Show keys | Extra metadata visibility | Canonical identity and literal field paths |

Recommended initial behavior:

- Start unfamiliar multi-section definitions in **Guided steps**, **Form**, **Standard**, **Comfortable**.
- Respect an explicit saved global presentation preference when applicable.
- For a small single-section definition, avoid a ceremonial multi-step navigation bar.
- Make Compact and Property table easy to select, not hidden in developer settings.
- Keep inactive settings in an explicit inspection filter, not mixed into "All active settings."
- Keep layout switching available while editing; do not turn it into a new workspace.

The definition can suggest a task-oriented starting experience. It must not override user appearance preferences or define executable presentation behavior.

### 5.2 Task profiles are different from display modes

Authored profiles such as "Local development" and "Production preparation" can determine a useful order and initial disclosure. They should not silently manufacture answers or change defaults merely because the user chose a different visual layout.

The UI should make three questions separate:

1. **What task am I doing?**
2. **How do I want to navigate and display it?**
3. **Which settings actually apply to my answers?**

The last question remains owned by the admitted definition and shared evaluator, not by a layout component.

### 5.3 Mode-switch contract

Switching any view-only preference must preserve:

- Valid answers and invalid draft text.
- Explicit absence, defaults and imported/manual provenance.
- Stable logical identities for surviving collection rows.
- Secret consent and masking policy.
- Prepared output when no answer or definition changed.
- Search query/filter state unless the user explicitly resets it.

Move focus only when the action requires it. Selecting a layout should normally leave focus in its selector. Selecting a result or correction should focus the actual destination. The existing performance work already demonstrates why gratuitous heading focus is not harmless. [C18]

## 6. Information architecture and shell

### 6.1 Desktop structure

Proposed composition:

```text
+-----------------------------------------------------------------------+
| Menu  ISLAMU Configurator  [ Search settings...       Ctrl/Cmd K ]  Theme |
+-----------------------------------------------------------------------+
| App rail | Definition / revision             Local-only   Customize   |
|          | Configure / Review / Export        18 of 24 required ready  |
|          +------------------------------------------------------------+
|          | Sections       | Current section             | Help*       |
|          | General   4/4  | Title and short purpose      |             |
|          | Network   3/5  | Compact question rows        |             |
|          | Database  2/4 |                              |             |
|          | ...            | Previous       Continue     |             |
+-----------------------------------------------------------------------+
* Optional inspector; not a permanently reserved empty column.
```

This is a functional wireframe, not an approved visual mockup.

The application navigation and section navigation must not look like competing versions of the same menu. Use a compact application rail or collapsible drawer for Home, Store, Open and Help. Give section navigation the larger share of contextual information.

Use a compact workspace identity row: definition title, revision, local-only state, and a Details action. Put digests, publisher claims, output declarations and compatibility metadata in a properly named details panel.

Replace the large, always-visible preference selects with an Event-style quick theme menu. Put full customization in a drawer or dialog that does not navigate away and trigger workspace-loss confirmation.

### 6.2 Navigation hierarchy

Keep these visibly distinct:

- **Application navigation:** Home, Store, Open, Help.
- **Workflow stage:** Configure, Review, Export.
- **Configuration section:** Network, Database, Email and other authored groups.
- **Presentation controls:** journey, layout, density, detail.
- **Workspace operations:** import, clear, discard.

Do not use the same tab/step treatment for all five.

### 6.3 Scrolling and actions

Keep document scrolling as the normal vertical scroll owner. A sticky section navigator is useful on wide screens if it stays within the available viewport. Avoid a form scrollbar nested inside a page scrollbar.

A compact sticky workspace toolbar is reasonable, but it needs explicit height accounting and scroll offsets. Do not stack a fixed appbar, sticky workflow strip, sticky toolbar and bottom action bar until only a small viewport remains.

Previous/Continue should be near the edited group in Guided mode. A bottom action region can become sticky only after keyboard, small-height viewport, zoom and virtual-keyboard testing proves it does not cover content. That would intentionally revise the current design document's prohibition on fixed action bars; it should not be introduced accidentally. [C20]

## 7. Density, spacing and visual hierarchy

### 7.1 Fix the sources of wasted space

The field stylesheet currently declares `margin-block: var(--isl-space-5) var(--isl-space-8)`: nominally 20px above and 32px below at a 16px root size. A field can also contain a 44px-minimum omission action, a help disclosure, a definition-key disclosure, label spacing and object padding. Global sections and summaries add further rhythm. [C11]

Do not simply add these values together and claim a measured field height: margin collapse, flex/grid formatting and nested structure affect the rendered result. The declaration and repeated controls nevertheless explain why reducing one outer margin is insufficient.

Recommended order:

1. Remove unnecessary per-field actions from the primary line.
2. Move nonessential explanations out of document flow.
3. Reduce nested card/fieldset decoration.
4. Define field-row and group spacing explicitly.
5. Apply density consistently across native exceptions and Mud components.
6. Measure real rendered heights with short, long, invalid and translated content.

### 7.2 Proposed design targets

These are starting targets for a prototype, **not MudBlazor defaults or accessibility standards**.

| Element | Comfortable starting target | Compact starting target |
| --- | --- | --- |
| Gap between related field rows | 16-20px | 8-12px |
| Label to control | 6-8px | 4-6px |
| Gap between meaningful groups | 24-32px | 20-24px |
| Section padding on desktop | 20-24px | 16-20px |
| Main control hit area | About 44px or larger | Preserve an adequate target even when visual chrome is denser |
| Repeated collection/table row | Content-driven | Content-driven; no fixed height that clips errors |
| Header and toolbar | Content-driven compact rows | Same hierarchy, less unused padding |

Compact mode must not mean small low-contrast text. Keep the body typography readable and allow labels, errors and localized text to wrap.

Use width appropriate to the value: a port number should not occupy the same measure as a connection endpoint. Short related controls can share a row on wide screens; long URLs, multiline values, nested objects and explanatory content need full width. Preserve DOM reading order when columns collapse.

### 7.3 Color and surface hierarchy

Keep one clear action accent and restrained semantic status colors. The current palette already provides light/dark and high-contrast variants; the issue is not the absence of theme machinery. [C8]

Specify roles rather than scatter color choices:

- Canvas: low-emphasis background.
- Workspace: primary editing surface.
- Subsection: spacing and heading first, border only when useful.
- Selected section: tonal background plus current-state semantics.
- Primary action: one dominant filled treatment.
- Secondary action: outlined or text treatment, consistently.
- Metadata: readable secondary text, not low-opacity microtext.
- Error: text and icon/semantics as well as color.

Do not fill the workspace with accent-colored outlines on every repeated action. The retained screenshots show how this competes with actual field content.

Measure text, controls, focus, selection, disabled and validation states in every supported palette. A "high contrast" option is a design intent, not proof that the whole application meets contrast requirements.

### 7.4 Concrete Event palette baseline

The inspected Event fallback theme is quieter and more neutral than Configurator's normal teal-accented theme:

| Role | Event built-in light | Event built-in dark | Current Configurator normal light / dark |
| --- | --- | --- | --- |
| Primary action | `#18181B` | `#FAFAFA` | `#0F766E` / `#5EEAD4` |
| Canvas | `#F5F5F7` | `#1A1A1A` | `#F5F5F7` / `#16191D` |
| Surface | `#FFFFFF` | `#242424` | `#FFFFFF` / `#202429` |
| Main text | `#18181B` | `#FAFAFA` | `#18181B` / `#FAFAFA` |
| Secondary text | `#404040` | `#A1A1AA` | `#52525B` / `#A1A1AA` |
| Input line | `#A1A1AA` | `#3F3F46` | `#A1A1AA` / `#71717A` |

Sources: Event `AppearanceThemeService.cs:16-82` [E12] and Configurator `AppearanceThemeService.cs:83-135` [C8]. Event can resolve a different profile through its backend; this table is its inspected fallback, not proof of the currently active user's theme.

For direct visual alignment, use the reviewed Event neutral theme as the initial baseline and make a teal variant an explicit preset rather than hard-wiring teal as the only normal accent. Match typography rhythm, radii, borders, action emphasis and state colors as a system, then compare actual screens. The copied values alone do not establish contrast or usability.

Event's typography lists Inter with system fallbacks, while Configurator currently uses system fonts. A font-family declaration does not establish that a licensed local font asset is loaded. Keep the current system stack unless a separately reviewed, locally shipped font is deliberately added; remote font fetching is unnecessary. [E12, C8]

## 8. MudBlazor component strategy

### 8.1 Adopt conventions before counting components

Use Event's outlined fields, compact search, thin wrappers, standardized actions, menus and dialogs. Keep tokens and component defaults centralized. MudBlazor provides the building blocks; it does not decide information architecture or make a dynamic form usable by itself. [E1-E5, W1]

Recommended component choices:

| Need | Recommended starting point | Important qualification |
| --- | --- | --- |
| App shell | `MudLayout`, `MudAppBar`, `MudDrawer`, `MudMainContent`, or equivalent established Event shell composition | Choose the simpler structure; do not recreate Event docking/tenant workspaces unnecessarily. |
| Ordinary text | Configurator wrapper over `MudTextField<T>` | Preserve raw invalid draft text and existing shared parsing semantics. |
| Short enum | `MudSelect<T>` | Keep null/absence distinct from an empty-string choice. |
| Large public choice set | `MudAutocomplete<T>` with local admitted options | No answer-bearing remote lookup; verify option and focus behavior. |
| Explicit yes/no | Labeled radio/segmented choices or suitable nullable control | A simple two-state switch is unsuitable when "not supplied" is a meaningful third state. |
| Immediate preference toggle | `MudSwitch<bool>` | Appropriate for appearance preferences; configuration semantics may differ. |
| Guided journey | `MudStepper` with a product-owned state contract | Step completion comes from shared readiness, not merely a Next click. |
| Repeated object collection | `MudTable<T>` or a deliberately scoped `MudDataGrid<T>` | Use only the features needed; support complex row editing separately. |
| Short explanation | `MudTooltip` | Keyboard focus and touch access still need deliberate design. |
| Rich contextual help | `MudPopover` or a help drawer/dialog | Links and focusable content are not tooltip content. |
| Feedback | `MudAlert`, inline status, restrained snackbar | Errors need persistent corrective content; a toast is not enough. |
| Progress | Determinate/indeterminate progress matching actual operation state | No artificial percentage for unknown work. |
| Customization | Drawer/dialog, menus and toggle groups | Do not navigate away from the active workspace. |

### 8.2 Thin wrapper responsibilities

A field wrapper should standardize:

- Outlined appearance and supported density.
- Label, required state, accessible description and error connection.
- Start/end adornments and help trigger.
- Clear/reset/omit affordance placement.
- Input-purpose metadata, masking and safe attribute forwarding.
- Explicit value/change callbacks.

It should not own a second answer dictionary, implement definition validation, infer target defaults or become a universal component with dozens of unrelated rendering modes.

Event wrappers are precedents, not files to copy without inspection. For example, the inspected Event `AppTextField` assigns to its `Value` parameter in its callback. Configurator should retain its own parameter/state conventions rather than import that detail mechanically. [E5, C19]

### 8.3 Exact-version caution

Current online documentation and Context7 can expose development-branch APIs. In this audit, a development result included `DataGridEditMode.Inline`, but installed **9.7.0** XML exposed `Cell` and `Form`, not that additional enum member. Do not build a redesign proposal around an unverified newer API. A property table can use templates or `MudTable` without requiring an upgrade. [M1]

The installed documentation does establish `Margin`, `HelperTextOnFocus`, `MudStepper.NonLinear`, `OnPreviewInteraction`, tooltip hover/focus/click controls and DataGrid virtualization. API availability is not proof of the desired rendered UX. [M1]

### 8.4 Performance-aware adoption

The current native scalar editors were a deliberate part of the browser performance work, not an accidental failure to discover MudBlazor. [C18]

Therefore:

- Use Mud components for persistent shell/search/customization first.
- Prototype the actual dynamic-field workload before replacing every leaf.
- Prefer mounting only the active editor for a property-table row.
- Keep a narrow native-control exception if a measured library control fails a required budget; style and interaction parity still apply.
- Do not introduce a broad generic rendering/cache framework.

The goal is Event-level consistency and usability with retained responsiveness, not maximum component-library usage.

## 9. Field semantics and better input design

### 9.1 Public scalar values

For ordinary strings, provide a visible label, an appropriate input width and a short hint only when it helps answer correctly. Place examples in hints or admitted example metadata, not as substitute labels.

For numbers, preserve partially typed and invalid drafts such as a leading minus sign until validation can explain the issue. Test parsing and culture deliberately; changing to a numeric component must not silently reinterpret values.

Use a multiline editor only where the admitted contract supports suitable text. Do not infer new target semantics from a field name such as `json`, `certificate` or `url`.

### 9.2 Boolean and enumeration choices

An optional boolean has at least three meaningful states: absent, true and false. A defaulted boolean also has provenance. The existing select preserves this distinction, though its presentation is generic. [C3]

Better choices include:

- A compact "Use this setting" control plus Yes/No.
- Three clearly labeled choices: Use default / Yes / No, only if "Use default" accurately represents the definition contract.
- An explicit unset choice in a nullable component.

Do not implement a switch that maps untouched to false. Do not relabel absence as "default" when omission and an authored default have different meanings.

Short enums can use compact selects or radio choices when comparison matters. Large sets can use local search. An empty string may be a legitimate enum value and must not collide with the unset sentinel.

### 9.3 Optionality and reset

Replace repeated large "Omit X" buttons with a consistent state/action pattern:

- Compact supplied/omitted/defaulted indicator.
- Clear labeled action where removing the value is common.
- Overflow menu for less frequent actions.
- Explicit "Reset to authored default" only when a default exists and that operation is supported.

Omit, clear, reset and collapse are different actions. Collapsing a row must never clear it. Clearing text must not implicitly create an empty object or empty list.

### 9.4 Objects and lists

Prefer a subsection heading, short description and compact children over a border around every recursive level.

For lists:

- Show a useful row summary and item count.
- Use stable row identity independent of current index.
- Add one item in a predictable place and focus its first editable control.
- Remove only the intended row and place focus meaningfully afterward.
- Explain uniqueness and maximum-item constraints before the user hits them.
- Offer a table for homogeneous objects and a drawer/dialog for complex rows.
- Keep a card/form projection on narrow screens.

Do not add reorder handles unless order is meaningful and the underlying operation is supported. If introduced, provide keyboard alternatives; do not rely on dragging alone.

### 9.5 Secrets and sensitive values

Keep explicit per-workspace secret consent. Do not turn it into a permanent appearance preference or enable it through an "Expert" mode.

Present a concise locked-state explanation at the relevant group with an Enable secret entry action. Avoid repeating a paragraph for every unavailable secret leaf.

Keep declared password-manager purposes and masking. Do not add per-field reveal controls casually: sensitivity policy and revocation behavior must be designed and tested. Never put sensitive values into tooltips, row summaries, search results, copyable keys or diagnostics.

Changing themes, density or layout must not revoke consent or clear answers. Revoking consent intentionally must continue to clear secret leaves and prepared output. [C3, C18, C19]

## 10. Help without layout movement

The user's complaint about expanding information is well founded in the field structure. The right replacement is **contextual help with multiple input methods**, not hover-only instructions.

### 10.1 Three levels of help

1. **Essential inline hint:** short information needed to answer correctly, such as units, a required format or a consequence that changes the decision.
2. **Short contextual explanation:** a tooltip on a named help trigger, available on hover and keyboard focus.
3. **Extended help:** a click/tap popover or stable inspector containing paragraphs, examples and public documentation links.

Use the same help trigger location across form, table and guided modes. A section-level help action can explain a whole group.

### 10.2 Interaction contract

- Hovering/focusing help must not reflow the document.
- Touch users can open the same information deliberately.
- Escape dismisses an open help surface where appropriate.
- Focus returns to its trigger after a modal help surface closes.
- Overlays stay within the viewport and do not cover the field being explained unnecessarily.
- Opening help must not change answers, validation state or workflow stage.
- Essential instructions and validation errors remain available outside tooltips.
- Long help can be pinned to an inspector on desktop; use a sheet/dialog on narrow screens.

W3C requires relevant hover/focus content to be dismissible, hoverable and persistent, subject to the criterion's exceptions. Tooltips do not receive focus; content with links or other focusable elements needs a different interaction pattern. [W2, W3]

`HelperTextOnFocus` can reduce visual repetition, but use it cautiously. If appearing helper text changes row height, it recreates the movement problem. Reserve a small stable hint/error slot where justified, and place extended help outside normal flow.

Do not replace every `<details>` element. Details remain useful for deliberate technical inspection or large optional content when document expansion is expected. Replace the routine per-field expansion that interrupts editing.

## 11. Sectioning and questionnaire quality

The interface cannot repair poor authored grouping by applying CSS. Both rendering and definition presentation need attention.

### 11.1 Section hierarchy

Use a stable structure:

```text
Definition
  Section: Database
    Group: Connection
      Host, Port, Database name
    Group: Credentials
      Username, Password
    Group: Connection options
      Pooling, Timeouts, TLS
```

This is an illustrative information hierarchy, not proposed literal field IDs or a required section list.

Each section should explain its purpose in one or two useful sentences. Subgroups need headings that describe user tasks, not merely nested JSON container names.

Current Core metadata already carries section and experience descriptions. The browser's simplified section projection discards the section description, while the shared form experience does not expose the Core experience description. This is a concrete opportunity before inventing a large new authoring vocabulary. [C5, C6]

### 11.2 Navigation content

A section item should usually show:

- Title.
- Compact required readiness, such as `3/5`.
- An error/deferred indicator when relevant.
- A clear selected state.

Keep the fuller explanation in accessible text or a help surface. Do not display "Current section" plus several progress sentences on every row if a compact visual state and programmatic current marker communicate it adequately.

Show all applicable sections in navigation even when detail filters hide some optional controls. Explain when hidden sections contain corrections. Search/correction can reveal the appropriate field without silently changing the user's whole detail preference.

### 11.3 Authored titles and guidance

Humanize at authoring time:

- Use task-language labels.
- Include units and constraints.
- Explain consequences of enabling a feature.
- Identify where externally issued values come from.
- Add useful search synonyms.
- Keep examples synthetic and safe.

Mechanical key formatting is only a fallback. It cannot infer the right phrase or provide a meaningful explanation. Current title formatting appropriately preserves authored titles; preserve that principle. [C15]

If new display hints are needed, add a bounded declarative vocabulary, validate it, and regenerate owning schemas. Do not accept arbitrary HTML, CSS, component names or executable templates from definitions.

## 12. Search as first-class navigation

### 12.1 Placement and scope

Use one prominent top-bar search entry. Within a workspace, its primary purpose is **Find a setting**. Outside a workspace, it can search the public Store or application destinations, with an explicit scope label.

Do not silently mix project search and setting search in one ambiguous result list. A small scope selector or clearly separated result groups is sufficient; a large universal command framework is unnecessary.

On desktop, follow Event's outlined, dense appbar search convention. On mobile, keep a visible search action in the top bar that opens a focused search surface. Never bury it in an Advanced accordion. [E1]

### 12.2 Result behavior

Show:

- Friendly title.
- Section/subgroup breadcrumb.
- Required/optional and active/inactive state.
- Relevant match context from admitted metadata.
- A direct destination action.

On selection:

1. Resolve the literal field path.
2. Open/select the correct section or step.
3. Reveal the needed ancestors.
4. Focus the actual field or consent recovery action.
5. Preserve a way back to search.

For an inactive setting, show why it is inactive and offer navigation to its controlling question. Search must not enable it.

For an empty result, offer clear query, clear filters or inspect inactive settings. Consolidate the current three large reset actions into a clearable search field and compact filter/reset affordances. [C4]

### 12.3 Keyboard and privacy

- Support Ctrl-K and Cmd-K deliberately where appropriate, without claiming a shortcut that is not implemented.
- Support predictable result navigation, Escape and focus restoration.
- If implementing a combobox, implement its actual ARIA pattern rather than assigning roles to an arbitrary list.
- Keep the query transient.
- Search metadata, never answer values.
- Do not put queries into analytics, URLs, persistence or worker messages.
- Keep results locally computed from the admitted definition.

Persistent search can still have lazily created results. Keep the entry visible while mounting the expensive result graph only when needed. Preserve complete ranking/facet counts before paging; do not search only the mounted page. [C4, C18]

## 13. Guided, section and continuous-page journeys

### 13.1 Guided steps

Use a small number of meaningful sections, not one step for every scalar leaf. W3C recommends logical groups and progress information; GOV.UK recommends starting with focused pages and combining them when user research supports it. This does not justify forcing a configuration operator through hundreds of one-field pages. [W4, W5]

A step should have:

- A clear title and short purpose.
- A manageable set of related fields.
- Current section progress.
- Back and Continue.
- Correction feedback when needed.
- A way to revisit earlier work.

Use non-linear navigation for revisiting and inspecting steps. Avoid imprisoning users in a step because a secret is deliberately deferred. "Continue with incomplete settings" and final preparation validation can coexist if readiness is clearly stated.

If a particular flow truly requires a decision before later questions can be meaningful, explain that dependency rather than enforcing an opaque disabled Next button.

Do not mark a step complete just because the user clicked Continue. Derive readiness from shared applicable required leaves and show deferred secrets separately. [C6, M1]

### 13.2 Section-based workspace

This is the focused editing mode: one section mounted, persistent section navigation, direct search and review correction.

It suits large professional configurations and preserves the current performance-friendly bounded rendering model. Improve the presentation rather than remove this option.

### 13.3 All sections / continuous page

This must be a real long-page experience with section headings and all applicable input groups reachable by scrolling. It must not be a renamed selected-section experience.

Provide:

- Table of contents / section jumps.
- Search and optional-detail controls.
- Stable headings and sensible scroll offsets.
- Clear section boundaries.
- Review/export navigation that does not require scrolling past every field.

Start with an honest fully rendered implementation for small and medium definitions. Qualify large definitions before choosing a rendering strategy.

For very large definitions, section-level deferred mounting can preserve the continuous-page experience only if scrolling, search, correction, keyboard access and state retention still work. Do not sell a sequence of "Load more" buttons as "all fields on one page." If full immediate mounting cannot meet the required budget, report that limitation and solve the renderer explicitly.

Avoid blanket virtualization of variable-height editable fields. Focus, validation messages, help, browser Find and screen-reader navigation complicate it. Pagination remains a reasonable choice for a table collection, but is not equivalent to the promised continuous form.

### 13.4 Dynamic applicability

When a controlling answer changes:

- Update local dependent content predictably.
- Keep the focused control stable if it still exists.
- Explain newly applicable required work.
- If the focused control disappears, place focus on a meaningful stable destination.
- Recalculate progress honestly.
- Do not silently clear unrelated data to make the visual layout simpler.

Some layout movement is inherent when the user deliberately adds a row or enables a configuration group. Distinguish that meaningful change from help text opening or repeated status announcements that unnecessarily move the page.

## 14. Form and property-table layouts

### 14.1 Standard form

Use the same question components in Guided, Sections and All sections. A form can use two columns for short related inputs when space allows, but should not become a dashboard of unrelated cards.

Provide consistent label alignment, help trigger placement, optionality and error positioning. Do not use floating labels as the only durable label where values are complex or tables are dense.

### 14.2 Property-table experience

Recommended columns:

| Setting | Value / editor | State | Help / actions |
| --- | --- | --- | --- |
| Human label, optional key/breadcrumb | Typed editable control | Required, defaulted, omitted, invalid or deferred | Contextual help and overflow actions |

Guidelines:

- Group rows by section/subgroup.
- Keep labels visible while editing.
- Commit changes through the same form owner.
- Avoid embedding a full heavyweight editor tree in every visible cell.
- Keep per-cell validation visible and associated with the correct control.
- Allow column width adjustment only if it serves real content.
- Use a side editor for complex objects rather than squeezing arbitrary recursion into one cell.
- Provide direct navigation from an error summary to the row.
- Preserve custom search across the complete model even if rows are paged.

This is a **property table**, not an invitation to build a spreadsheet. Spreadsheet-style arrow-key editing, copy/paste ranges and bulk operations are separate features with significant semantics and privacy implications. They are not required for the first redesign.

### 14.3 Collection tables

Homogeneous object lists can use domain-relevant columns. Primitive lists can use a small one-value-per-row editor. Complex nested items need an explicit row editor.

Keep row identities stable across sort/filter/remove. Sorting the displayed table must not reorder the configuration output unless the user explicitly performs a supported reorder operation.

Prefer a simple `MudTable` when templated rows are sufficient. Use DataGrid only when its sorting/filtering/editing features justify its complexity. Keep any data source local; the existence of `ServerData` APIs is not a reason to send answers anywhere. [M1, W1]

### 14.4 Narrow screens

Do not force an editable multi-column property grid into 320px. Offer labeled stacked rows/cards and a clear way to switch to form layout. Preserve the user's preferred desktop mode rather than overwrite it whenever the viewport changes.

Horizontal scrolling can be appropriate for genuinely two-dimensional data, but the primary configuration workflow should remain usable without it. [W8]

## 15. Theme management, personalization and customization

### 15.1 Event-like appearance experience

Provide a quick menu with:

- System, Light, Dark, Light high contrast, Dark high contrast.
- Reviewed first-party presets with miniature previews.
- Current selection indication.
- Customize appearance.
- Full presentation settings.

A fuller appearance surface should offer:

- Preset cards.
- Accent / brand color and neutral surface options.
- Preview of input, button, navigation, error and selected states.
- Direction.
- Typography size preference within tested bounds.
- Reduced-motion preference, while respecting the operating system.
- Reset appearance.

Custom colors should produce a complete local palette through reviewed logic or a small reviewed palette set. Do not allow arbitrary colors to create unreadable text/control pairs and call the result a supported theme.

Avoid adding remote fonts, assets or a theme marketplace. System fonts already exist and satisfy the offline architecture. [C8, E3]

### 15.2 Workspace customization drawer

Keep frequently changed choices in a quick toolbar; put the full set in a drawer:

- Journey.
- Field layout.
- Detail level.
- Density.
- Focused/wide content.
- Show definition keys.
- Help inspector preference.
- Reset presentation.

Use small examples or previews where words alone are ambiguous. Explain that these options change the interface, not the target configuration.

Changing a preference should update the current workspace immediately. Do not reload, reconstruct the form model or require navigating away.

### 15.3 Persistence boundary

Current `AppearanceState` and JavaScript deliberately accept only `themeMode` and `direction`. Adding preferences requires an explicit new bounded contract on both sides; arbitrary extra settings currently reject. [C8]

Persist only global presentation preferences such as density, journey preference, layout, reviewed palette identifiers and bounded color choices. Keep these separate from:

- Answer values and invalid drafts.
- Secrets and consent.
- Prepared artifacts and previews.
- Search queries.
- Imported content and filenames.
- Workspace-specific field paths, expansion history or selected private content.

Preference storage failure must leave the tab usable. "Settings retained on this browser" is different from "configuration saved." Do not introduce autosave of answers, even encrypted, as a casual UX improvement.

No compatibility adapter is required for the current development-only preference shape. A clear versioned replacement with safe defaults is preferable to accumulating legacy parsing branches.

### 15.4 Preset scope

Keep these names unambiguous:

- **Theme preset:** colors/typography.
- **Presentation preset:** journey/layout/density/detail.
- **Definition experience:** authored task organization.
- **Answer template:** configuration data, outside presentation preferences.

Never blend them into one "profile" object that can accidentally persist answers.

## 16. Review, validation and export

### 16.1 Review is a useful screen, not a status dump

Organize review by section with compact rows and filters:

- Needs attention.
- Changed / imported.
- Deferred secrets.
- Ready/defaulted.
- Optional omitted and inactive inspection as secondary filters.

Show origin, applicability and readiness clearly. The current review intentionally reports state without exposing values; do not silently replace it with an unmasked answer table. [C12]

Useful first-delivery improvements do not require showing values: clearer summaries, grouped status, better labels and direct correction are enough. Any proposal to display public answer values requires an explicit sensitivity-aware policy and tests, not an assumption that "public" means all descendants are safe.

Keep technical output bindings, keys and diagnostic codes in an inspector rather than a repeated disclosure on every row.

### 16.2 Validation timing

- Do not show a wall of red errors on first load.
- Validate an edited field at an understandable interaction boundary.
- Show section-level errors on Continue where relevant.
- Run strict shared validation before complete preparation.
- Keep invalid drafts intact so the user can correct them.
- Keep errors visible until corrected; do not rely on snackbars.
- Connect summary items to exact fields/rows and provide Return to review.

Use specific instructions such as "Enter a whole number from X to Y" when those constraints are admitted and available. Do not invent target rules in the browser. Preserve value-free diagnostics.

### 16.3 Separate progress concepts

Display these independently:

1. Where the user is in the workflow.
2. How many applicable required answers are ready.
3. Whether files have been prepared.
4. Whether the target has accepted/applied them.

Configurator can report the first three. It cannot infer the fourth. With zero required answers, say so rather than manufacture a misleading percentage. Deferred secrets remain visible. [C6, C19]

### 16.4 Export flow

Use an export summary with:

- Declared and actually prepared files.
- Complete versus draft state.
- Missing-secret completion obligations.
- Per-file deliberate Download, Copy and Reveal actions.
- Clear preparation/error/cancellation status.
- A concise privacy consequence at the action.

Use a primary preparation action and a secondary draft route; explain why complete preparation is blocked and offer the relevant correction. Keep draft preparation available when it truthfully defers missing secrets.

Do not add Download all by inventing a new unreviewed archive workflow. A batch export can be considered later if the artifact owner supports it and custody is explicit.

Keep output hidden until deliberate reveal. An edit must continue to invalidate stale prepared output; view-only changes must not. If clearing output is necessary, communicate it without a noisy status sentence after every keystroke.

### 16.5 Destructive actions and departure

Move Clear answers away from the primary preparation button. Use a confirmation with:

- A precise title.
- What will be lost.
- Keep editing.
- Clear answers and output.

Use a themed internal-navigation dialog following Event conventions. External browser unload still needs the browser's supported native mechanism; a MudDialog cannot reliably replace it. [C16, E10]

Undo is not automatically appropriate for clearing secrets because revocation/clearing intentionally retires private state. Do not retain an invisible recovery copy to make the interface feel safer.

## 17. Improvements across every route

### 17.1 Home

Make the first task obvious: choose an available definition or open one.

- Show bundled definitions with concise title, purpose and Configure action.
- Keep Open file / public link and Browse Store as clear alternatives.
- Reduce the hero's vertical footprint.
- Make offline readiness understandable.
- Keep publisher/trust detail available without repeating a warning on every card.
- Do not show "Resume saved configuration" because the browser intentionally does not save answers.

Current Home has useful task entry points and a three-part explanation; preserve that clarity while reducing ceremony. [C13]

### 17.2 Definition Store

- Search at the top, aligned with the shell convention.
- Clear tag/filter state with compact chips and one reset affordance.
- Consistent card height/action alignment without clipping descriptions.
- A useful empty state and local-file fallback.
- Loading skeletons that resemble actual cards, not a layout-changing spinner.
- Compact revision/compatibility summaries.

The Store already has separate loading, unavailable, empty and no-match paths. Improve their consistency rather than claim those states are absent. [C14]

### 17.3 Store project and revision selection

Replace the current metadata-heavy sequence with:

1. Project summary.
2. Clearly labeled revision selector and declared compatibility.
3. Compact selected-revision summary.
4. Review/admit and Open workspace.
5. Technical details on demand.

Keep canonical pinning and the distinction between definition revision and application version. Never imply that Store inclusion authenticates the publisher.

### 17.4 Open configuration

Use two clear source choices: Local file and Public link. A tab or segmented choice can reduce simultaneous form height, provided it preserves the entered URL and does not imply loading an unselected source.

- Keep file format/size help near the chooser.
- Make URL/CORS failure explain the local-file alternative.
- Keep optional digest/selectors in a compact secondary section.
- Show selected-definition identity in a stable summary.
- Separate retain-public-definition consent from opening the workspace.
- Keep source-loading progress and cancellation clear.

The current local JSON/YAML support should be represented consistently in Help; its inspected copy still refers to local JSON only. This is a documentation inconsistency, not a reason to change the loader. [C14, C17]

### 17.5 Workspace launch

Avoid a large launch header followed by another large editor header. Show definition identity once and use the workspace stage/header underneath it.

For rejected launches, lead with a plain explanation and a useful recovery action. Keep technical rejection codes available, but not as the only useful information. [C13]

### 17.6 Help and offline lifecycle

Provide task-oriented topics:

- Open a definition.
- Choose a layout.
- Find a setting.
- Understand omitted/defaulted values.
- Import an existing artifact.
- Handle secrets.
- Review and export.
- Prepare for offline use.
- Understand what closing/reloading loses.

Show offline readiness separately from network connectivity. Explain when the public app or definition needs preparation. Offer updates only when applying them cannot replace a live workspace; never refresh a form to activate a UI change.

Keep consent/export explanations near their decisions and retain a complete reference. Shortening repeated copy must not remove consequential information.

## 18. Responsive design, accessibility and localization

### 18.1 Responsive behavior

Define behavior at content pressure points, not only inherited framework breakpoints:

- Wide desktop: optional app rail, sections, editor, optional help inspector.
- Ordinary desktop/tablet: collapse the inspector first, then reduce navigation width.
- Narrow tablet: section picker or temporary section drawer.
- Phone: single-column questions, top search action, full-width help/customization surface.
- Short viewport / virtual keyboard: keep the current field and essential navigation accessible.

Keep a visible section/step location on phones without filling half the screen with progress controls. Do not copy Event's several breakpoint values as universal constants; its appbar, workspace rail and docking have different responsibilities. [E1, E11]

### 18.2 Accessibility acceptance baseline

Use WCAG 2.2 AA as the baseline and distinguish it from stronger product preferences:

- Normal text contrast at least 4.5:1; qualifying large text at least 3:1. [W6]
- Required non-text control/state contrast at least 3:1 against adjacent colors, with the criterion's applicable exceptions. [W7]
- Reflow at an equivalent 320 CSS-pixel width for ordinary content; do not make the main form a horizontal-scrolling surface. [W8]
- Target Size (Minimum) has a 24-by-24 CSS-pixel basis with specified exceptions/spacing rules. Prefer approximately 44px touch targets where practical; do not incorrectly label 44px as the WCAG AA minimum. [W9]
- Keyboard focus must not be entirely hidden by authored content under Focus Not Obscured (Minimum). Aim higher by keeping the whole focused field visible. [W10]
- Hover/focus help meets dismissible/hoverable/persistent requirements where applicable. [W2]
- Visible labels, programmatic names and connected errors/help.
- Logical heading structure and landmarks.
- Predictable dialog focus and restoration.
- No keyboard traps or positive-tabindex navigation.
- Reduced-motion and forced-colors behavior.

MudBlazor component names and automated scanners are not accessibility certification.

### 18.3 Announcements and focus

Current progress and result counts use live regions, and editor status can change on input. These mechanisms need speech testing rather than automatic removal. [C2, C4, C5]

Recommendations:

- Do not announce a long progress summary after each character.
- Announce meaningful completion, validation and applicability changes.
- Avoid announcing a destination that focus movement already communicates.
- Keep search focus while results update.
- Focus the exact correction target, not just a section title.
- Ensure new/removed collection rows have predictable focus outcomes.

### 18.4 RTL and localization

Direction support is not translation. The current UI and many diagnostics are English literals, while locale selection is explicitly not a translation guarantee. [C3, C8, C13]

If multilingual UI is a target, make it a real workstream:

- Resource-backed UI labels and messages.
- Authored localized labels/help where supported.
- Logical CSS properties.
- Appropriate number/date input rules without changing canonical values.
- Bidirectional isolation for filenames, keys, URLs and digests.
- Long-label and text-expansion tests.
- Manual Arabic/RTL review, including tables and progress direction.

Do not persist answers or fetch translations containing private values.

## 19. Performance is part of the UX contract

The older memory of multi-second lag is not the current evidence baseline. The repository now records a frozen indexed-edit candidate on **October 23, 2026** with 92 passing directional rows and 3,680 measured samples. Maximum recorded feedback was 47.8ms at 1x CPU and 183.9ms at 4x; no warmed 1x long task exceeded 50ms. These are prior reference-host measurements, not measurements made by this audit or guarantees for all devices. [C18]

That improvement does not make the layout good. It does establish responsiveness that a redesign must preserve.

### 19.1 Preserve the successful mechanisms

- One shared evaluation authority.
- Form-owned indexes and revision facts.
- Stable input/row identity.
- Selective rendering and bounded mounted collections.
- Lazy discovery/review graphs.
- Complete ranking before result paging.
- Focus behavior that does not add unnecessary interop/render work.

Avoid undoing these by mounting every possible mode at once, adding nested wrapper hierarchies or cloning answers into table/wizard models.

### 19.2 Measure the right journey

Measure input-to-requested-rendered-content, not only event-handler completion or a spinner appearing. Include:

- Theme and density changes.
- Layout/journey changes.
- Search open, query, result selection and return.
- Section/step navigation.
- Boolean activation/deactivation.
- Optional disclosure.
- Object/list add/remove/edit.
- Validation correction and return.
- Review entry.
- Preparation/reveal/revocation.

Keep cold/first-use separate from warmed measurements. Preserve all outliers, package/definition identities, viewport, CPU conditions and actual scenario state.

### 19.3 Rendering recommendations

Microsoft's Blazor guidance supports avoiding unnecessary expensive subtree rendering and using virtualization for suitable large lists. It does not make virtualization a universal solution for complex editable forms. [W11]

Use profiling to identify the actual cost. Do not add caching/scheduling layers because the form looks busy. Do not move answers into web workers, storage or requests. Keep immediate control feedback honest and bounded.

Measure movement separately from latency: a fast help expansion can still be disruptive. Do not rely solely on cumulative layout shift, since user-triggered movement is not fully represented by that metric. For this product, explicitly measure whether opening help moves the active question or its neighboring controls.

## 20. Architecture and concrete ownership

### 20.1 Keep the existing dependency direction

```text
Browser shell and presentation preferences
                  |
Browser workspace / journey and layout renderers
                  |
Shared DefinitionFormModel and immutable projections
                  |
Core definition admission, applicability, validation and composition
```

The UI should change substantially without adding Event runtime dependencies or another evaluator.

### 20.2 Recommended responsibility boundaries

| Owner | Responsibility in the redesign |
| --- | --- |
| `Layout/MainLayout`, `NavMenu` | Appbar, top search placement, responsive app navigation, providers and landmarks |
| `Layout/ThemeQuickSwitcher` | Compact quick appearance interaction |
| `Services/AppearanceState`, `AppearanceThemeService`, `browser-appearance.js` | Bounded presentation preferences, local palette resolution and immediate theme application |
| `Components/Common` | Small reusable field/action/dialog/help conventions |
| `ConfigurationEditor` | Workspace composition, workflow stage and layout selection; not validation |
| Journey/layout components | Guided, section, continuous and property-table presentation over one form |
| `ConfigurationField` or its focused successors | Typed editor adapters, help/label/error composition, collection focus |
| `ConfigurationDiscovery` | Local metadata search, facets, ranking presentation and navigation |
| `ConfigurationReview` | Readiness/origin summary and correction navigation |
| `BrowserArtifactExport` and existing operation owner | Deliberate output handling and invalidation |
| Presentation form/projections | Shared answers, drafts, row identities, applicability facts and operation/view distinctions |
| Core presentation metadata | Only bounded authored grouping/display hints that genuinely need cross-client authority |

These are responsibilities, not a requirement to create one new service or class per table row.

### 20.3 Simplify rather than preserve

Once replacements work:

- Remove the hidden advanced-search entry point.
- Remove redundant per-field key/help disclosures replaced by the inspector.
- Remove duplicate native/Mud action styling where a single convention suffices.
- Replace the overloaded experience UI with explicit axes.
- Retire obsolete layout code and stale tests for intentionally replaced behavior.
- Update the current design contract and public guide alongside implementation.

Do not keep aliases or old rendering modes solely for compatibility. Preserve behavioral invariants, not accidental markup.

### 20.4 Definition changes

Prefer using existing section/experience descriptions before expanding the contract. If new subgroup/layout hints are necessary, admit a small declarative vocabulary and regenerate schemas natively.

Do not hand-edit generated schemas, dependency locks or ratchets. A browser-only appearance preference does not belong in the Core definition contract merely because it is configurable.

## 21. Recommended delivery sequence

This is a proposed sequence, not authorization or a claim that implementation has started.

| Increment | Scope and likely owners | Exit evidence |
| --- | --- | --- |
| 1. Reference and design foundation | Confirm Event parity checklist; specify tokens, density, control/help states and representative real definitions | Desktop/mobile visual proposals showing ordinary, optional, nested, secret-denied and invalid fields; explicit component/version choices |
| 2. Shell and visible search | `MainLayout`, `NavMenu`, quick appearance, discovery presentation | Search visible immediately; keyboard/touch opening and result navigation; no form movement or answer loss |
| 3. Field rhythm and contextual help | Common wrappers, `ConfigurationField`, CSS/token owners | Compact/comfortable comparison; stable help; boolean/enum/omit semantics retained; nested layouts improved |
| 4. Sections and true journeys | Editor composition and shared metadata projections as needed | Guided, Sections and genuine All sections; independent detail control; mode-switch state preservation |
| 5. Property-table and collections | Layout renderer and focused collection editors | Same answers/drafts in both layouts; stable rows; keyboard errors and narrow projection; measured scale cost |
| 6. Appearance and customization | Local appearance service/state/JS; settings drawer | Event-like presets/customization; immediate changes; bounded preference storage; no answer persistence |
| 7. Review/export and secondary routes | Review/preparation, Home/Store/Open/Help, departure dialog | Coherent end-to-end completion and recovery; destructive actions separated; truthful draft/export states |
| 8. Workstream qualification | Affected owner tests and actual published browser | Full scenario matrix, Event visual comparison, performance/custody/accessibility evidence and remaining limits |

Do not polish secondary Store cards while the editor still lacks the intended layout model. Conversely, do not leave shell and editor on visibly different design systems after a "form redesign."

Keep coherent source/caller/test/doc slices together. Use affected-owner checks at increment boundaries and the full required published-browser matrix at final qualification rather than repeatedly running unrelated suites.

### 21.1 Decisions recommended now

The report recommends these defaults rather than leaving them unresolved:

1. Event is the visual/convention reference; Configurator remains offline/static.
2. Search belongs in the top bar.
3. Journey, layout, detail and density are independent.
4. All three journeys and both field layouts are real supported behaviors.
5. Contextual help supports hover, focus and deliberate touch/click.
6. Appearance is local-only, answer-free and immediately applied.
7. One form/evaluation authority serves every view.
8. Breaking presentation changes are acceptable; no compatibility layers are planned.
9. Existing measured feedback budgets are preserved, not relaxed to accommodate a new component tree.

## 22. Verification and acceptance criteria

### 22.1 Behavioral matrix

| Area | Required observable result |
| --- | --- |
| Top search | Immediately discoverable on desktop and via a visible top action on phone; searches the complete admitted metadata selection |
| Help | Opens without changing document geometry; works with hover, keyboard and touch; extended links are operable |
| Layout switching | Form/table changes preserve valid answers, invalid drafts, omission, provenance, row identity and consent |
| Journey switching | Guided/Sections/All sections preserve the same answer state and do not reconstruct a fresh workspace |
| Detail level | Optional disclosure never hides the only correction path or enables inactive settings |
| Section navigation | Current location and progress are clear; selected destination/focus is visible |
| Dynamic applicability | Newly required work is explained; disappearing controls have safe focus handling |
| Collections | Add/remove/edit affects the intended row; surviving IDs/drafts remain stable |
| Secrets | Explicit per-workspace consent; masking inherited; revocation clears secret leaves and prepared output |
| Review | Useful grouped state and direct correction/return; values remain governed by sensitivity policy |
| Export | Complete/draft states truthful; deliberate copy/download/reveal; editing invalidates stale output |
| Appearance | Immediate local changes with no reload or workspace loss; safe behavior when storage is unavailable |
| Departure | Cancel preserves work; confirm discards through the existing operation-generation boundary |
| Offline/update | No answer-bearing requests/caches/messages; update activation does not replace live work |

### 22.2 Visual and accessibility matrix

Cover Home, Store, Store project, Open and every workspace journey/layout. Include default, loading, empty, unavailable, invalid, secret-denied, consented, prepared and revealed states where applicable.

At minimum:

- Desktop 1440px and a wider desktop.
- Narrow 390px and 320px reflow.
- Light/dark, supported high-contrast palettes and representative custom presets.
- LTR/RTL.
- Long authored labels, paths, enum choices and help.
- Large text and actual browser zoom.
- Reduced motion and forced colors.
- Keyboard-only operation and screen-reader speech on an agreed supported combination.
- Touch/virtual-keyboard checks on a physical device.
- Scrolled start/end states for sticky elements.

Do not claim an entire theme or route passes from a single screenshot. Keep fresh screenshots after the final rendered change and identify untested combinations.

### 22.3 Test placement

Use existing behavioral seams:

- Browser tests for shell, appearance, experience/workflow, discovery, review, collections, navigation and credential behavior.
- Presentation tests when shared draft/view/row/projection contracts change.
- Core tests only when admitted definition vocabulary or validation changes.
- Architecture/package tests when dependencies, packaging or custody boundaries change.
- Published-browser measurement for rendering, keyboard/focus, performance, offline/storage/network and actual exports.

Relevant existing suites include `BrowserExperienceWorkflowTests`, `BrowserConfigurationDiscoveryTests`, `BrowserConfigurationNavigationTests`, `BrowserConfigurationReviewTests`, `BrowserCollectionWorkflowTests`, `BrowserAppearanceTests`, `BrowserSecretBoundaryTests` and `BrowserExportLifecycleTests`.

Do not pin CSS/prose with tests. Do not replace actual screen behavior with class-name inventories. Async tests should subscribe to exact events/state changes before triggering actions, with bounded timeouts rather than fixed sleeps.

### 22.4 Product success measures

Collect these through local synthetic testing and consented usability sessions, not production telemetry:

- First-attempt success finding a named setting.
- Time and number of actions to find, edit and return from review.
- Completion and error-recovery success for a representative configuration.
- Ability to distinguish omitted/defaulted/false and complete/draft states.
- Number of visible useful questions in the initial editing viewport.
- Scroll distance and action count for repeated-object editing.
- Unintended active-field movement when opening help.
- Keyboard-only completion and mode-switch continuity.
- Measured input-to-rendered-feedback/content latency across minimal, real Event and scale fixtures.
- Preference changes that preserve workspace/output state.

Do not promise a percentage usability improvement without a baseline study. Establish targets after observing representative operators. A smaller form is not automatically a better form.

## 23. What should not be done

- Do not make hover the only route to important information.
- Do not put every field in a card or every optional setting behind another accordion.
- Do not turn "advanced" into the only meaning of experience.
- Do not call selected-section rendering "continuous page."
- Do not assume all Mud components are faster, more accessible or automatically consistent.
- Do not recreate a spreadsheet when a property table is sufficient.
- Do not invent a generic scheduler, cache hierarchy, plugin renderer or second evaluator.
- Do not import Event hosting/auth/tenant/API authority.
- Do not persist answers to deliver "personalization."
- Do not preserve obsolete presentation contracts for backward compatibility.
- Do not weaken privacy, readiness or performance contracts to make a visual prototype appear complete.
- Do not claim old screenshots, green component tests or a package build establish fresh whole-app visual quality.

## 24. Source register

### 24.1 Configurator sources

All `C` references are relative to this repository. Line references identify the inspected source at intake; later edits may move them.

| Ref | Source and relevant evidence |
| --- | --- |
| C1 | `src/Configurator.Browser/Layout/MainLayout.razor:4-20`: providers, skip link and shell composition. `PROJECTS.md:23-45`: browser ownership and dependency direction. |
| C2 | `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor:4-102`: preamble, progress, controls, consent/import and discovery. `:109-139`: selected-section mounting and guided buttons. `:148-183`: review, preparation and artifacts in the same document. |
| C3 | `src/Configurator.Browser/Components/Configuration/ConfigurationField.razor:5-73`: collapsed fields, object/list recursion and row operations. `:77-118`: denied secrets, selects and scalar input. `:120-160`: omit/help/guidance/error/key UI. `ConfigurationField.razor.cs:68-101`: enum identity, sensitivity, drafts and credential purpose. |
| C4 | `src/Configurator.Browser/Components/Configuration/ConfigurationDiscovery.razor:4-30`: closed disclosure, search and reset controls. `:63-115`: results, inactive recovery and paging. `:122-151`: initially closed state and disclosure behavior. |
| C5 | `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs:84-115`: section projection and workflow selection. `:116-143`: correction and return. `:174-190`: input/view notifications. `:387-403`: heading/focus behavior. |
| C6 | `src/Configurator.Presentation/Presentation/DefinitionFormModel.cs:38-53`: sections/experiences. `:63-81`: view selection. `DefinitionFormProjection.cs:5-46`: experience/section/progress shapes. `src/Configurator.Core/Definitions/DefinitionPresentation.cs:36-71`: descriptions/navigation. `DefinitionSchema.cs:73-75` and `SetupDefinitionCodec.cs:250`: admitted `guided` / `sections` navigation. |
| C7 | `src/Configurator.Browser/Components/Common/AppButton.razor:1-23`, `AppButton.razor.css:1-2`; `wwwroot/css/components.css:8-22`: wrapper and native action/control styling. |
| C8 | `src/Configurator.Browser/Layout/ThemeQuickSwitcher.razor:4-25`; `Services/AppearanceState.cs:6-37`; `Services/AppearanceThemeService.cs:17-75,78-151`; `wwwroot/browser-appearance.js:1-36`: current preferences, palette ownership and persistence boundary. |
| C9 | `src/Configurator.Browser/Components/Configuration/ConfigurationNavigation.razor:1-30`: section labels and repeated progress/current state. |
| C10 | `Directory.Packages.props:20`: central MudBlazor 9.7.0 version. |
| C11 | `src/Configurator.Browser/Components/Configuration/ConfigurationField.razor.css:1-20`; `ConfigurationEditor.razor.css:1-41`; `Layout/MainLayout.razor.css:1-7`; `Layout/NavMenu.razor.css:1-22`; `wwwroot/css/tokens.css:2-18,36-57`; `base.css:1-15`: spacing, shell dimensions and repeated control/disclosure rhythm. |
| C12 | `src/Configurator.Browser/Components/Configuration/ConfigurationReview.razor:4-60`: value-free review list, output details and pager. `:63-95`: corrective diagnostics and secret/draft recovery. |
| C13 | `src/Configurator.Browser/Pages/Catalogue.razor:4-42`; `Pages/Launch.razor:7-25`: Home and workspace identity/error presentation. |
| C14 | `src/Configurator.Browser/Pages/Store.razor:7-62`; `Pages/StoreProject.razor:10-81`; `Pages/Open.razor:11-133`: entry/discovery/revision/source flows. |
| C15 | `src/Configurator.Browser/Components/Configuration/ConfigurationDiagnosticText.cs:9-44`: title fallback and diagnostic mapping. |
| C16 | `src/Configurator.Browser/Layout/WorkspaceNavigationGuard.razor:7-25`: NavigationLock and native internal confirmation. |
| C17 | `src/Configurator.Browser/Pages/Help.razor:1-30`: static help and presentation description. |
| C18 | `docs/internal/OPERATIONS.md:399-487`: frozen indexed-edit candidate, exact inventory, 92-row/3,680-sample qualification and limits. `dev/_journal/domains/presentation-and-clients.md:142-225`: rendering/focus lessons. `docs/internal/BROWSER_UI.md:86-137`: selected mounting, native scalar rationale and view/correction behavior. |
| C19 | `AGENTS.md`, section 5, invariants 2-7; `.agents/rules/blazor-client.md`, Path-Specific Constraints; `docs/internal/QUICK_REFERENCE.md:67-75`: static WASM, shared ownership, privacy and target authority. |
| C20 | `src/Configurator.Browser/DESIGN.md`, sections 4-8: current scroll/action-bar, guidance, motion and qualification policy. This is a current design contract to revise deliberately, not proof of good UX. |

### 24.2 Event first-party sources

All `E` references are relative to `/home/amir/ISLAMU/Github/Event/`. They establish local implementation precedents, not runtime certification.

| Ref | Source |
| --- | --- |
| E1 | `src/Explore.Blazor.Client/Layout/NavMenu.razor:90-130`; `NavMenu.razor.cs:90-98,118-158,185`; `NavMenu.razor.css:604-623`; `tests/Explore.Blazor.Client.Tests/Layout/NavMenuWorkspaceTests.cs:29-89`: top/contextual search, shortcuts and mobile entry. |
| E2 | `src/Explore.Blazor.Client/Layout/ThemeQuickSwitcher.razor:8-103,133-155`: preset/mode menu, customization entry and profile selection. |
| E3 | `src/Explore.Blazor.Client/Pages/User/Components/SettingsAppearance.razor:24-180`: modes, direction, preset cards and custom colors/preview. |
| E4 | `src/Explore.Blazor/wwwroot/css/layers.css:1-23`; `tokens.css:7-34,44-120,138-142,168-195`; `docs/internal/DESIGN_SYSTEM.md:7-27`: actual layers/tokens and policy distinction. |
| E5 | `src/Explore.Blazor.Client/Components/Common/AppTextField.razor:4-63`, `AppTextField.razor.css:1-7`; `AppButton.razor:1-33`: thin outlined fields and shared actions. |
| E6 | `src/Explore.Blazor.Client/Pages/Events/Components/EventListCustomizationDrawer.razor:4-113`; `Pages/Events/EventList.razor.cs:643-677,776-798,980-989`: collection layout preferences, information controls, reset and persistence. |
| E7 | `src/Explore.Blazor.Client/Pages/Admin/Components/AdminOrganizationTable.razor:5-16,54-117`: dense responsive table and empty/action states. |
| E8 | `src/Explore.Blazor.Client/Pages/Organizations/CreateOrganization.razor:42,116,176`; `CreateOrganization.razor.cs:170-184`: form steps, review and advancement validation. |
| E9 | `src/Explore.Blazor.Client/Pages/User/Components/SettingsLayout.razor:21-84`; `Pages/User/Settings.razor:6-7`: sectioned/searchable settings and server-rendered ownership. |
| E10 | `src/Explore.Blazor.Client/Shared/ErrorState.razor:1-29`; `Components/Forms/FormSubmissionGuard.razor:3-13`; `AppValidationSummary.razor:5-15,22-37`; `Services/DialogOptionsFactory.cs:12-42`; `Components/Common/AppDialogShell.razor:1-29`: recovery, validation and dialog composition. |
| E11 | `src/Explore.Blazor.Client/Layout/MainLayout.razor:6-63`; `Components/Shell/AppWorkspaceRail.razor:3-97`, `.razor.css:156-203`; `Components/Docking/DockLayoutHost.razor:15-48`; `src/Explore.Blazor.Client/wwwroot/js/accessibility.js:66-124`; `src/Explore.Blazor/wwwroot/css/utilities.css:118-185`: shell, responsive navigation and accessibility mechanisms. |
| E12 | `src/Explore.Blazor.Client/Services/AppearanceThemeService.cs:16-101,118-160,224-242,304-363`; `UserSettingsService.cs:16-19,32-78,92-122`; `Services/Shell/ShellPreferencesService.cs:14-67`: fallback palettes/typography, API/local persistence distinctions and backend palette resolution. |

### 24.3 Official web and API research

Accessed October 23, 2026. Sources were used for API facts, functional guidance and standards, not copied visual compositions or third-party implementation. URLs are retained verbatim for reproducibility.

| Ref | Source / URL | Use and evidence limit |
| --- | --- | --- |
| W1 | MudBlazor Text Field / DataGrid / layout and theme API documentation: `https://mudblazor.com/components/textfield`, `https://mudblazor.com/components/datagrid`, `https://mudblazor.com/api/MudDrawer`, `https://mudblazor.com/api/MudAppBar`, `https://mudblazor.com/api/MudThemeProvider` | Official search results and Context7 `/websites/mudblazor` / `/mudblazor/mudblazor`. Direct DataGrid extraction returned the site's unhandled-error shell; no claim of a full rendered documentation review. |
| W2 | W3C, Understanding SC 1.4.13: `https://www.w3.org/WAI/WCAG22/Understanding/content-on-hover-or-focus.html` | Official search content establishes dismissible, hoverable and persistent requirements and exceptions. Direct extraction was unavailable. |
| W3 | WAI-ARIA APG, Tooltip Pattern: `https://www.w3.org/WAI/ARIA/apg/patterns/tooltip/` | Official search content distinguishes non-focusable tooltips from content requiring a non-modal dialog. |
| W4 | W3C, Multi-page Forms: `https://www.w3.org/WAI/tutorials/forms/multi-page/` | Official search content supports logical grouping, progress and optional-stage handling. |
| W5 | GOV.UK Service Manual, Structuring forms: `https://www.gov.uk/service-manual/design/form-structure` | Full readable page retrieved. Focused pages are a starting point; research can justify combining questions for repeat/internal tasks. This report does not adopt its autosave or analytics suggestions. |
| W6 | W3C, Contrast (Minimum): `https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html`; WCAG 2.2: `https://www.w3.org/TR/WCAG22/` | Official search content and normative reference for contrast thresholds. |
| W7 | W3C, Non-text Contrast: `https://www.w3.org/WAI/WCAG21/Understanding/non-text-contrast.html` | Official explanatory search content for control/state contrast and adjacent-color interpretation; criterion retained in WCAG 2.2. |
| W8 | W3C, Reflow: `https://www.w3.org/WAI/WCAG21/Understanding/reflow.html` | Official search content for 320 CSS-pixel equivalent reflow; criterion retained in WCAG 2.2. |
| W9 | W3C, Target Size (Minimum): `https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html` | Official search content for 24px basis and spacing exception. |
| W10 | W3C, Focus Not Obscured (Minimum): `https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html` | Official search content for the "not entirely hidden" AA requirement. |
| W11 | Microsoft, ASP.NET Core Blazor rendering performance best practices, .NET 10 view: `https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/rendering?view=aspnetcore-10.0` | Full readable documentation retrieved; use of rendering boundaries and suitable virtualization, not a mandate to virtualize every editable field. |
| M1 | Installed MudBlazor 9.7.0 XML documentation: `/home/amir/.nuget/packages/mudblazor/9.7.0/lib/net10.0/MudBlazor.xml` | Input margin/help: lines 1967-1970, 2044-2047; grid modes: 9609-9616; virtualization: 13335-13361, 13535-13540; stepper: 23858-23860, 23919-23921; tooltip: 28202-28221. Local API evidence, not external implementation reuse. |

Research tooling had partial failures: some W3C extraction paths returned no content, the direct MudBlazor page returned an error shell, and the alternate Tavily extractor reported a plan limit. The cited official search content, successful direct retrievals, Context7 API references and installed package documentation supplied the evidence actually used. Failed retrievals are not represented as reviewed pages.

### 24.4 Provenance and scope

Event is the user-requested first-party reference. Configurator's existing `docs/internal/legal/IP_GOVERNANCE.md:14-35` records the Steward-authorized first-party transfer boundary; it does not grant rights over third-party assets or code.

This report transfers no implementation, package, font, icon set, remote asset, credentials or publication authority. Recommendations are independently written functional proposals over repository-native mechanisms. Any implementation transfer should record exact first-party source provenance and retain actual dependency terms/notices. No excluded Evolve demo material was used.

## 25. Audit completion and remaining evidence

Delivered: a source-grounded whole-app audit, Event convention mapping, current official research, independent experience model, concrete component/layout/help/theme recommendations, per-route improvements, ownership, delivery order and acceptance matrix.

Not delivered or claimed: product changes, a final visual design, fresh browser QA, new performance measurements, contrast certification, a usability study or passing test results from this session.

The main conclusion is actionable now: **rebuild the browser presentation around a compact Event-aligned shell, visible top search, explicit independent presentation controls, stable contextual help and one shared configuration state.** Preserve the substantial functionality and recently qualified responsiveness while replacing the interface structures that make that functionality difficult to use.
