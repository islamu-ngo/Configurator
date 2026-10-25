# Browser workspace design

This contract extracts the existing first-party component system. The owning
values remain in `wwwroot/css/tokens.css`, `base.css`, `components.css` and
`Services/AppearanceThemeService.cs`; this document does not define a second
palette. No external visual assets, fonts or component implementation are added.

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
| Primary action and focus | `--isl-color-primary`, `--isl-focus-ring` |
| Main and secondary text | `--isl-color-text`, `--isl-color-text-secondary` |
| Neutral separation and control boundary | `--isl-color-border`, `--isl-color-control-border` |
| Error and success | `--isl-color-error`, `--isl-color-success` |

Tokens follow the existing light, dark and high-contrast Mud palettes. State has
a text label or semantic attribute as well as color. Rounded panels never use an
accent stripe. Preserve visible keyboard focus rings and input error boundaries.

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

The document remains the vertical scroll owner. The guided region uses a narrow
section sidebar and a shrinking main column at wide container sizes; at narrow
sizes it becomes one column in DOM reading order. There is no nested form
scrollbar or fixed action bar covering inputs. Every grid child can shrink
(`min-inline-size: 0`); primary content must reflow without horizontal scrolling
at 320/390 pixels, long labels and enlarged text.

Application navigation is a deliberate disclosure, not a second permanent
sidebar competing with section navigation. Its Menu trigger remains visible
on desktop and phone; Escape closes the navigation and restores that trigger.
The compact appbar is a wrapping cluster of identity/navigation, current-workspace
search and existing appearance controls. It remains sticky during deep editing
when the viewport is taller than 30rem. On shorter viewports, including browser
zoom, the header and expanded application navigation scroll with the document
so they cannot cover the editor. Open search instead fixes its entry and bounded
results within that viewport while reserving its normal header space.
On taller viewports application navigation opens beneath the header as a bounded
overlay. Search results overlay the workspace
without moving its edited question. The results region may scroll independently
because it is a bounded transient overlay, not the form's vertical scroll owner.
The viewport reserves a stable scrollbar gutter; modal scroll lock does not add
a second body padding compensation or shift the active field.
No route/hash navigation, docking framework or second answer model is involved.

## 5. Reusable primitives and states

Workspace detail is Essential, Standard or All active in the selected profile,
with an explicit Follow experience choice. Comfortable/Compact is session-only
CSS presentation: field/group gaps and control height change, never font size,
answers or validation. Coarse-pointer controls retain 44px targets. The toolbar
keeps authored experience, detail, density and consent distinct. Declared-output
explanations live in the existing privacy disclosure so the first short-fixture
question is reachable without scrolling at 1440x900. Optional field omission
moves into a named row-action menu; omitting a container explicitly names its
nested answers and remains distinct from collapsing optional detail.

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
- **Section navigation:** title, independently counted required-answer progress,
  correction/deferred status and a current-section indication. Buttons wrap
  labels; current state has text and `aria-current`. Empty/inactive sections do
  not invent work.
- **Editor:** connected label, required/optional state, current input, short help
  and error. Local optional disclosure uses a labeled button with
  `aria-expanded`; required/error ancestors stay reachable. Only the selected
  section mounts editors; the shared form owns drafts and row identity.
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
