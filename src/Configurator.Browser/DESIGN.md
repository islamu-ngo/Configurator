# Browser workspace design

> **Last Verified:** 2026-11-18
> **Source Anchors:** `wwwroot/css/tokens.css`, `Services/AppearanceThemeService.cs`, `Components/Docking/`, `Components/Settings/`

This contract extracts the first-party Event-aligned workspace system. The owning
values remain in `wwwroot/css/tokens.css`, `base.css`, `components.css`,
`mudblazor-overrides.css`, and `Services/AppearanceThemeService.cs`; this document does
not define a second palette. No external visual assets or unapproved fonts are added.

## 1. Identity and hierarchy

An offline operator workspace: quiet surfaces, readable questions and explicit
custody. Configuration work leads; trust details and optional explanations remain
available without dominating the first screen. Distinguish Configure, Review and
Prepare from answer completion and from the target application's acceptance.
Never turn a collapsed setting into an omitted answer.

## 2. Color

| Role | Existing semantic token |
| --- | --- |
| Main canvas | `--isl-color-background` |
| Workspace surface | `--isl-color-surface` |
| Grouped controls and selected navigation | `--isl-color-surface-muted` |
| Primary action and focus | `--isl-color-primary`, `--isl-color-accent`, `--isl-focus-ring` |
| Main and secondary text | `--isl-color-text`, `--isl-color-text-secondary` |
| Neutral separation and control boundary | `--isl-color-border`, `--isl-color-control-border` |
| Error and success | `--isl-color-error`, `--isl-color-success` |

Tokens follow the shared Event 8-preset system (`Enterprise Blue` default,
`Emerald Green`, `Abyssal Dark`, `Pure White`, `Classic White`, `Classic Dark`,
`Light High Contrast`, `Dark High Contrast`) plus the Custom `Natural + Brand`
HSL palette generator across **System**, **Light**, **Dark**, **Light HC**, and
**Dark HC** modes (`ThemePresetCatalog` / `ThemePaletteGenerator`). State has a
text label or semantic attribute as well as color. Rounded panels never use an
accent stripe. Preserve visible keyboard focus rings and WCAG contrast across all
ordinary and high-contrast palettes.

## 3. Typography

Use `--isl-font-family-primary` and the existing responsive
`--isl-typography-h1-size` through `--isl-typography-h6-size` scale. Page identity
uses the shell's h1; the workspace heading uses h4 sizing and a section heading
uses h5 sizing. Form labels use weight 600; secondary explanations retain the
existing body rhythm. Progress numbers use tabular figures. Keep explanation
measure near 65 characters, allow long admitted titles/keys to wrap and never
make a tooltip the sole label or instruction.

## 4. Spacing and layout

Use the existing quarter-rem `--isl-space-1` through `--isl-space-16` scale,
`--isl-space-page`, `--isl-space-section`, `--isl-content-width` and radius tokens.
Browser mechanics such as percentages, intrinsic grid tracks and container
queries are not new design tokens.

The shell uses a frosted `64px` topbar (`NavMenu`) with brand identity,
`ThemeQuickSwitcher`, `LanguagePicker`, and in-place **Settings** toggle. During
active `/launch/{FamilyId}` sessions, **Settings** (`SettingsWorkspace`) mounts in
place over a `hidden`/`inert` editor container with a top-left **← Return to
configuration** button so answers, drafts, and secret consent survive round-trips.

Configuration views pair a resizable global Start dock (`240–460px`, default `256px`
hosting top workspace links, a bottom-pinned `Local workspace` custody card, short-viewport vertical scrolling, and a visible Close control in overlay mode) with an independent sticky `13–16rem` form section rail inside `ConfigurationConfigurePanel` (`padding: var(--isl-space-1)` focus-safe scrolling, collapsing into a compact wrapped chip bar at `<= 60rem`; mobile full-screen section drill-down stays deferred) and Tabbed End dock (`Inspector (F2)` |
`Document (JSON)`, `300–620px`, default `360px`) coordinated by `DockLayoutState` priority allocation,
enforcing a density-aware central content floor (`790px` Comfortable / `806px` Spacious / `774px` Compact / `766px` Dense while persistent desktop form nav renders above `960px`, else `420px`) so active form inputs stay `>= 420px`, with automatic overlay degradation
at `<= 760px` or tight split widths. `DockResizeHandle` mirrors pointer/keyboard
deltas in RTL (`RightToLeft`), and overlay close unmounts synchronously without
stale-drawer animations. Home **Catalogue** cards (`Store`) render 2-per-row
compact landscape tiles (`~124px` tall, left `76×76` validated PNG logo or
monogram, bottom-right actions, and `ⓘ` `StoreProjectDetailsDialog` modal). Every
grid child can shrink (`min-inline-size: 0`), preventing horizontal scrollbars at
320/390 pixels, long labels, and enlarged text.

## 5. Reusable primitives and states

Workspace detail is Essential, Standard or All active in the selected profile,
with an explicit Follow experience choice. Density supports the 4-step workspace
density scale; CSS variables change field/group gaps and control height, never
font size, answers or validation. Coarse-pointer controls retain 44px targets.
`WorkspaceHeader` renders a single compact primary row containing the definition title, tabular progress + attention action, inline `[data-status-slot="reserved"]` (`CLS = 0`), active secret-unlocked pill when enabled, workflow stage buttons (`Configure`, `Review`, `Files`), and a right-aligned **Advanced** disclosure toggle (`[data-action="toggle-advanced-options"]`, displaying an override count badge when non-default modes are active), while `Launch` keeps a screen-reader `<h1>` without stacking a second hero block above the workspace so the active section navigation, layout switcher, section heading, and first editable questions begin near the top of `1366x768` and `1440x900` screens. Secondary controls (recipe/scope chip `#configuration-context`, secret-consent jump chip, **Output task**, Form/Developer mode switching, schema inspector `F2`, experience/detail profile, density, Humanized/Technical naming `Ctrl+Shift+K`, Undo/Redo `Ctrl+Z`/`Ctrl+Shift+Z`, `?` shortcuts, and `[data-digest-disclosure]` source details) stay tucked inside the `hidden`/`inert` `#workspace-advanced-options` panel until expanded. `ConfigurationConfigurePanel` hosts the sticky focus-safe left section rail (`ConfigurationNavigation` with `aria-current="step"` or `IntersectionObserver` scrollspy `aria-current="location"`, visible restrained `Reviewed` indicator without colored accent stripes, and tabular progress counts) beside `.configuration-configure-panel__main` containing the segmented `[Section by section | All sections]` layout switcher (`[data-layout-switcher]`, flush with Column 2 item #1 and preserving active scrollspy section back to `Section by section`), and returning-user entry actions (`[data-returning-entry]`) alongside `details.configuration-editor__view-options` render after the primary questions. Optional
field omission moves into a named row-action menu; omitting a container
explicitly names its nested answers and remains distinct from collapsing
optional detail. Wide viewports (`>= 1280px`) render schema inspection in a
docked `ConfigurationInspectorPane`, while secret generation uses the modal
`ConfigurationSecretGeneratorDialog` with explicit plaintext password dismissal.

- **AppButton:** existing Mud wrapper, explicit button type, filled primary,
  outlined secondary and text navigation variants. One dominant action per
  workflow region. Preserve hover, pressed, focus, disabled and busy behavior.
- **AppCard/workspace:** existing outlined surface with semantic headings.
- **AppTextField:** thin full-width outlined string-bound MudInput, including numeric
  drafts and masked credentials. The shared form alone parses and retains drafts;
  bubbling raw input/change events keep current revision and mounted-control fences.
  Labels, full errors, credential purpose and stable input identity remain explicit.
  The wrapper renders its connected scalar error directly, without another field container.
  No numeric sanitizer, second answer model or per-character focus operation.
  Separate content with spacing and neutral boundaries, not nested card stacks.
- **Choice fields:** outlined MudSelect for public enum and nullable boolean
  values, sharing Core parsing and the mounted-control callback fence. Not supplied,
  explicit false and admitted empty string remain different values.
  AppSelectField refreshes parameters and revision-bound callbacks even when
  unchanged visual output is skipped; it retains no additional answer snapshot.
- **Field actions:** a named native help trigger uses the packaged Mud icon and
  opens the existing workspace-owned inspector without a per-field tooltip.
  The omission menu mounts on first use and remains a normal MudMenu afterward.
  Cold activation and menu close explicitly focus its activator; open state is
  part of the render signature so `aria-expanded` remains current.
- **Workflow navigation:** a compact ordered Configure/Review/Prepare landmark
  targeting real sections. It describes location, not fabricated readiness.
- **Section navigation:** title, independently counted tabular-nums required-answer progress,
  correction/deferred status, visible restrained `Reviewed` indicator (with tonal active surface and no colored accent stripe), and a current-section indication. Buttons wrap
  labels (compacting to a wrapped chip bar at `<= 60rem`); current state has text and `aria-current`. Empty/inactive sections do
  not invent work.
- **Editor:** connected label, required/optional state, current input, short help
  and error. Local optional disclosure uses a labeled button with
  `aria-expanded`; required/error ancestors stay reachable. In `SectionBySection` layout (`ConfigureLayoutMode.SectionBySection`), only the selected
  section mounts editors; in `AllSections` layout (`ConfigureLayoutMode.AllSections`), all applicable
  sections render in a single continuous vertical flow (`ConfigurationCompactWorkspace`) under one `ConfigurationMountPlan`,
  sharing the persistent left section rail (with `IntersectionObserver` scrollspy `aria-current="location"`), non-disposing group collapse (`hidden`
  + `inert`), and explicit per-section **Mark reviewed** state (`SetSectionReviewed`). Review and Files always navigate to their dedicated top-level stages while preserving active drafts across layout switches. The shared form owns drafts and row identity.
  Large sections mount additional root fields in bounded batches while retaining
  connected controls. A busy region means editors are still mounting, not that
  answers are complete. Correction can prioritize its current field. Edits,
  view changes, replacement and disposal invalidate older continuations; no
  pending batch may resurrect a retired editor.
- **Guidance:** inert short help plus deliberate details for instructions and
  admitted public HTTPS links. A named, keyboard/touch-accessible help button
  opens the metadata-only inspector in the existing MudBlazor dialog provider;
  the tooltip contains no interactive links. Dialog Escape/Close restores its
  trigger, disposal closes only its own dialog, and document layout does not
  expand beneath the edited question. External links use no opener/referrer authority.
- **Workspace search:** outlined, immediate metadata search in the appbar with
  a visible label; a named phone entry opens the same search. Only the active
  editor registers its query/actions/results. Results mount on demand, retain
  shared facets/paging and expose ordinary labeled buttons, not a partial ARIA
  combobox. Search text is transient even when an operator types private text.
  Escape/Close restores the connected keyboard invoker, or the query/phone
  trigger for pointer-opened search; outside dismissal leaves the clicked
  destination focused and clears the old return target. The phone query
  overlays its retained trigger space without changing header height.
  Explicit correction closes
  results and focuses the exact field/controller/consent destination.
  Register/release and queued input are generation-qualified so an older
  editor cannot clear or retarget its replacement.
- **Answer progress:** real required obligations, missing/invalid/deferred counts
  and no percentage when there are no required answers. Ordinary navigation
  never alters answers or prepared output.

The actual synthetic workspace is the state harness for these primitives:
default, expanded, corrected, empty, invalid, secret-denied and consented states.

## 6. Interaction and motion

Use the established control feedback and native disclosure behavior. No entrance
animation, timer, artificial progress, scroll hijacking or moving target is
introduced. Workflow/section navigation moves focus to the destination heading;
search/filter updates retain focus in their own control. Respect reduced-motion
preferences if motion is subsequently added.

## 7. Surfaces

Keep the existing neutral outlined workspace and muted control/navigation
surfaces. Use `--isl-card-border` and the existing small/medium/large radii.
Do not add decorative shadows, gradients, imagery or another accent. Selected
navigation uses a tonal surface and current-state text, not a colored edge.

## 8. Accessibility and qualification

Preserve landmarks, heading order, connected labels, keyboard access, logical
CSS properties, honest status announcements and explicit secret consent.
Collapsed/filtered UI cannot conceal the only correction route. No hidden
answer-bearing control is retained for a password manager.

Qualification includes real desktop/mobile light/dark captures, narrow reflow,
enlarged text, keyboard/focus and contrast checks on the current candidate.
Component tests do not certify screen-reader behavior, extension compatibility
or a usability study. Those limits remain explicit in the workstream evidence.
The existing Home filled-anchor contrast defect is outside the workspace slice;
workspace filled buttons were separately observed with a white foreground.
