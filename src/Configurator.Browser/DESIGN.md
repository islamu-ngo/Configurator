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

## 5. Reusable primitives and states

- **AppButton:** existing Mud wrapper, explicit button type, filled primary,
  outlined secondary and text navigation variants. One dominant action per
  workflow region. Preserve hover, pressed, focus, disabled and busy behavior.
- **AppCard/workspace:** existing outlined surface with semantic headings.
  Separate content with spacing and neutral boundaries, not nested card stacks.
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
- **Guidance:** inert short help plus deliberate details for instructions and
  admitted public HTTPS links. External links use no opener/referrer authority.
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
