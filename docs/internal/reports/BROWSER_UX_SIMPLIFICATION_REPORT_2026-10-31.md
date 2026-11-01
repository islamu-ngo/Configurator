# Browser UX simplification and Developer view

> **Audience:** Maintainers | Designers | Contributors | AI agents
> **Kind:** Historical assessment
> **Status:** Point-in-time investigation and proposed redesign; not implemented or approved for execution
> **Owner:** Browser
> **Evaluation Date:** 2026-10-31
> **Last Verified:** 2026-10-31
> **Inspected Revision:** `c4add1508b71f288835747cd79d2d1eed05072ce`
> **Source Anchors:** `src/Configurator.Browser/`, `src/Configurator.Presentation/`, `examples/`, `tests/Configurator.Browser.Tests/`
> **Review basis:** Ten supplied screenshots, bounded source and test inspection, and public primary-source research; no application, product build, product tests, or usability study executed

## Executive recommendation

Rebuild the browser experience around **editing configuration**, rather than
operating a generic schema renderer. Open the Event workspace with **Environment
file selected**, show editable inputs immediately, replace short-choice
dropdowns with visible options, group settings into understandable sections, and
give the workspace a persistent desktop navigation rail. Add an equally
first-class **Developer view** for fast, schema-aware bulk editing.

The screenshots show a product that makes the operator manage its internal
machinery: artifact selectors, feature selectors, disclosure levels, field
expansion, validation terminology, workflow navigation, preparation modes, and
privacy explanations all compete before meaningful configuration begins.
Changing colors or shrinking margins alone would leave that problem intact.

The recommended experience has:

1. A clear project and output context, with a useful default.
2. A compact form whose relevant inputs are already visible.
3. Plain-language sections and labels, with exact technical keys available.
4. Form and Developer views over one authoritative answer workspace.
5. Separate Configure, Review, and Files panels, rather than three destinations
   embedded in one enormous page.
6. A small number of prominent actions and contextual, actionable feedback.
7. The existing offline, consent, sensitivity, and stale-operation protections,
   expressed at the point where they matter.

Backward compatibility is not a design constraint for this proposal. Retire
obsolete UI contracts, generated presentation conventions, and tests of
intentionally removed behavior. Preserve semantic correctness and custody
invariants, not the current interaction structure.

## Contents

- [Evidence and limits](#evidence-and-limits)
- [Screenshot findings](#screenshot-findings)
- [Why the current implementation produces this experience](#why-the-current-implementation-produces-this-experience)
- [What to learn from Coolify](#what-to-learn-from-coolify)
- [Recommended information architecture](#recommended-information-architecture)
- [Default selection and value semantics](#default-selection-and-value-semantics)
- [Form controls and compact editing](#form-controls-and-compact-editing)
- [Navigation and visual system](#navigation-and-visual-system)
- [Content and discovery](#content-and-discovery)
- [Review, preparation, and export](#review-preparation-and-export)
- [Developer view specification](#developer-view-specification)
- [Accessibility, mobile, localization, and performance](#accessibility-mobile-localization-and-performance)
- [Implementation boundaries and deliberate breaking changes](#implementation-boundaries-and-deliberate-breaking-changes)
- [Priorities and acceptance criteria](#priorities-and-acceptance-criteria)
- [Research source register](#research-source-register)
- [Evidence ledger and verification](#evidence-ledger-and-verification)

## Evidence and limits

This is an expert inspection, not a measured usability score or a full
accessibility audit. Evidence labels used below:

- **Screenshot:** directly visible in the supplied image.
- **Source:** confirmed in the inspected checkout, with a file and line.
- **Inference:** an explanation or likely user effect, not a recorded participant
  finding.
- **Proposal:** recommended future behavior, including numerical design targets.

The first eight images show Configurator; the final two show the user's Coolify
comparison. Screenshot browser zoom, exact CSS viewport, device scale, and build
identity are unknown. Differences in apparent text size between images cannot be
treated as measured CSS sizes. No timings, clickstream recordings, contrast
measurements, or assistive-technology results were collected.

The repository was clean on `main` at the revision above before report authoring.
The available code graph was built on October 15, 2026, at a different revision;
it was not used as proof of current source behavior. Direct source reads supplied
the implementation evidence. The October 23 redesign report is historical
context, not proof that its proposed experience was delivered successfully.

Screenshots can contain private configuration. No screenshot values, identities,
connection strings, or credentials are reproduced here or used in research
queries. External research was limited to public documentation and generic
interaction questions. There was no inspection of Coolify implementation code
and no copying of its assets, styling, or implementation.

## Screenshot findings

Severity below describes task friction, not a security-vulnerability rating.
P0 blocks the proposed experience, P1 materially improves task completion, and
P2 refines a sound foundation.

| ID | Image | Observed problem | Practical effect and recommended change | Priority |
| --- | --- | --- | --- | --- |
| UX-01 | 1 | Large introductory hero dominates a utility homepage. | Move directly to choosing a project or opening a file; shorten introductory content. | P1 |
| UX-02 | 1 | The filled primary button appears to have no readable text. | Treat as a rendering/contrast defect candidate. Verify computed colors and rendered text in the actual build; do not assert the cause from the screenshot. | P0 |
| UX-03 | 1-3 | Home, Store, and Open repeat overlapping entry choices. | Offer one clear starting surface with project selection and file/link alternatives. | P1 |
| UX-04 | 1-2 | Desktop navigation ends far above the bottom of the body. | Make it a body-height rail between header and footer, not a content-height dropdown. | P0 |
| UX-05 | 1-7 | Menu, appearance, and direction receive disproportionate prominence. | Keep navigation visible on desktop and move appearance/direction into an accessible settings surface. | P1 |
| UX-06 | 1-3 | Large cards and repeated trust qualifications consume scanning space. | Keep project purpose and primary action visible; put detailed provenance in a consistent secondary location. | P1 |
| UX-07 | 2 | Finding a project requires understanding “definition” and “revision.” | Lead with the application/project; surface revision identity when it is consequential. | P1 |
| UX-08 | 3 | File and URL onboarding contains substantial technical instruction. | Use compact methods, concise labels, and contextual failure recovery; retain file and source constraints where needed. | P1 |
| UX-09 | 4 | A required output choice begins as “Not supplied.” | Select Environment file for the Event profile immediately. | P0 |
| UX-10 | 4 | Several meta-controls precede the first useful answer. | Start with task context and fields; demote display preferences and optional import. | P0 |
| UX-11 | 4-7 | Configure, Review, and Prepare remain in one long document. | Make them real panels with distinct content and retained workspace state. | P0 |
| UX-12 | 5 | Boolean choices require opening repeated dropdowns. | Show Yes / No / Use default or Not set as visible radio options when those meanings apply. | P0 |
| UX-13 | 5 | Raw `select.feature.*` identifiers are shown as labels. | Author task language and capability names; retain exact IDs only as secondary technical information. | P0 |
| UX-14 | 5 | Many implementation-oriented feature switches precede the settings. | Group optional capabilities around user goals and explain what enabling each adds. | P0 |
| UX-15 | 5 | The same offline-selection explanation repeats per row. | Explain the distinction once for the group; reserve row hints for differences. | P1 |
| UX-16 | 6-7 | Ordinary optional settings require “Configure…” before an input appears. | Show the input immediately inside the selected section. | P0 |
| UX-17 | 6-7 | Long uppercase names dominate the hierarchy. | Put a meaningful label first in Form view and the exact key second; keep key-first presentation in Developer view. | P0 |
| UX-18 | 6-7 | Host validator/restart/startup metadata repeats across fields. | Replace repeated machine terminology with useful section guidance and contextual technical details. | P1 |
| UX-19 | 6-7 | Similar help and overflow icons repeat throughout. | Keep consistent named help; show uncommon actions contextually without hiding inputs or critical reset semantics. | P1 |
| UX-20 | 4-7 | A narrow centered workspace leaves substantial desktop space unused. | Use a task-oriented workspace width with bounded field lengths; do not stretch every input to the entire screen. | P1 |
| UX-21 | 4-7 | One large surface contains almost every type of content. | Separate context, section navigation, editable form, status, and actions through layout and hierarchy. | P0 |
| UX-22 | 4, 7 | Required readiness can read “1 of 1” after an artifact choice. | State exactly what is ready; also communicate optional configuration and target validation separately. | P0 |
| UX-23 | 8 | Review shows a large heading container without useful summary content. | Show an explicit ready state or actionable findings; omit empty ornamental panels. | P0 |
| UX-24 | 8 | Complete, draft, clear, copy, download, and reveal actions accumulate. | Separate preparation from export and move destructive actions away from the primary path. | P0 |
| UX-25 | 8 | Clipboard failure is detached from the export control. | Place error and Download recovery next to the affected file. | P1 |
| UX-26 | 8 | Success, privacy, target validation, and readiness share dense prose. | Use a clear file state, concise next action, and one secondary boundary explanation. | P1 |
| UX-27 | 9 | Coolify presents more settings in a structured, aligned surface. | Adopt the functional goal of fast scanning, not its exact table or decoration. | P1 |
| UX-28 | 10 | Bulk text editing is a visible alternative to individual controls. | Add a schema-aware Developer view integrated with the same workspace. | P0 |
| UX-29 | 1-8 | The experience provides weak spatial cues about current task. | Keep project, selected output, current section, and workflow panel identifiable together. | P0 |
| UX-30 | All | Mobile, keyboard, screen-reader, and RTL behavior cannot be established. | Include actual-surface validation in redesign acceptance; screenshots are insufficient evidence. | P0 |

These are related problems, not thirty independent cosmetic tickets. The main
clusters are **poor task defaults, an implementation-shaped information model,
hidden editing, weak workspace structure, and confused completion/export states**.

## Why the current implementation produces this experience

### Inputs are deliberately hidden, not merely styled poorly

`ConfigurationField.razor:6-12` branches on
`Form.IsEditorVisible(Path)` and substitutes a heading, description, and
“Configure” button when false. `ConfigurationField.razor.cs:139-140` changes
expansion state rather than providing a different editing model.

`DefinitionFormModel.cs:191-219` makes visibility depend on activation, correction,
required descendants, essential decisions, promotions, local expansion,
experience membership, and disclosure. The machinery has legitimate semantic
and performance uses, but its current presentation transfers too much control
work to the user.

**Recommendation:** preserve activation and consent as real constraints. Make
eligible scalar controls visible in the selected section. Move progressive
disclosure to optional *groups* and supplementary help, rather than requiring
one click per ordinary field. Required, invalid, and correction-target controls
must remain discoverable.

GOV.UK distinguishes supplementary disclosure from information most users need
[S03]. This supports moving disclosure away from routine editing; it does not
require eliminating all expandable content.

### One renderer treats booleans and enumerations as dropdowns

`ConfigurationField.razor:155-186` routes unmasked boolean and enum values through
`AppSelectField`, including an explicit absence option. The implementation
correctly distinguishes absent from false; the control choice is the friction.

**Recommendation:** visible radio groups for short choices, a real checkbox or
switch only for true binary semantics, and searchable selection for genuinely
long lists. A component library is an implementation tool, not a reason every
choice needs the same interaction.

GOV.UK's select guidance warns against defaulting to selects and distinguishes
settings from questions [S02]. Its radio guidance also warns that preselection
can hide an unanswered question [S01]. Therefore, default the *output setting*,
not every substantive configuration answer.

### Workflow buttons change focus, not the page structure

`ConfigurationEditor.razor.cs:165-173` changes the active workflow stage, schedules
focus, and validates on Review. `ConfigurationEditor.razor:154-216` renders
configuration, review, and preparation in sequence; those stages are not mutually
exclusive panels. Review effects are conditional, but the surrounding form
remains present.

**Recommendation:** actual workspace panels. Configure contains editing; Review
contains readiness and effects; Files contains generation/export. Reuse one
workspace object across them. A progress indicator must not impersonate a
multi-screen process when it is only a jump link. USWDS makes this distinction
for linear step indicators [S05].

### The sidebar is implemented as an expanding menu

`NavMenu.razor.css:1-7` positions the navigation absolutely below a sticky header.
It sets a maximum height, not a body-filling height.
`MainLayout.razor.css:1-3` lays out the application vertically without a desktop
navigation column. This agrees with the screenshot's partial-height menu.

The existing [Browser UI contract](../BROWSER_UI.md#interface-refinement) explicitly
describes a shell without a reserved permanent sidebar. The user's desired
body-height desktop rail is therefore a **proposed design change**, not merely a
failure to implement the existing document. Update that owner during a later
implementation; do not silently claim the current contract already requires it.

### Section support exists, but does not guarantee a useful definition

`ConfigurationEditor.razor:154-184` can render section navigation and only the
selected section. `ConfigurationSectionFields.razor.cs:18-19,47-57` stages newly
editable root controls in batches while retaining mounted controls.

It would be incorrect to conclude that the code has no sections or that removing
all mount boundaries is necessary. The supplied experience still behaves like a
large undifferentiated form. The definition's authored sections, experiences,
labels, ordering, and activation selectors must be reviewed together with the
renderer.

The independent definition investigation establishes the missing link:

- The Event authoring source has no authored `presentation` layout. The fallback
  in `DefinitionFormModel.cs:45-66` places unlisted fields in one Configure
  section and creates a default experience. This is not principally a missing
  CSS separator.
- The output answer at
  `examples/islamu-event/configurator.json:15938-15952` declares five choices
  but no `initialValue`. Being first in an enum or an entry-point list does
  not initialize an answer.
- Environment selection exposes 22 feature booleans plus deployment and
  standalone-topology selectors. Their initial values are false, but each is
  still a visible standard-detail decision. The authoring pattern is visible
  at `examples/islamu-event/configurator.json:16123-16160`.
- Titles such as `select.feature.agent-browser-config` are explicitly authored
  titles. The renderer is not inventing those strings. The same source uses
  uppercase environment keys as titles.

Fixing labels, layout, initialization, and capability grouping at their authoring
owner is therefore necessary alongside changing controls. Existing choice
presentation can label enum values independently of their machine identity;
`DefinitionFormModel.cs:421-463` already provides that separation.

### Review has an empty-success presentation path

`ConfigurationReview.razor:65-94` can render “Review preparation” whenever a
validation result exists, while the body is mostly conditional on problems.
Successful validation outside the active effects view can consequently leave
little useful content in that box. This is source-consistent with image 8, not a
freshly executed reproduction.

**Recommendation:** define populated success, blocked, draft-eligible, and
preparing states explicitly. The absence of errors is not itself a well-designed
review screen.

## What to learn from Coolify

The useful comparison is **work per setting**, not whether Coolify has more
features or more attractive colors.

In the supplied comparison, technical keys are aligned, controls and actions
share predictable positions, search is near the data, and Developer view supports
bulk work. Coolify's own documentation confirms `.env` bulk editing and explains
that saving can create, update, and remove entries; it also documents restrictions
for locked secrets and multiline values [S07].

### Transferable functional ideas

- Put the task surface before introductory prose.
- Make scanning and editing many values efficient.
- Give experienced operators exact keys and bulk editing.
- Keep search, filters, and view switching near the workspace.
- Make normal and text editing alternate access paths, not separate products.
- Present a consistent row rhythm and visible current context.

### Things not to copy

Do not import deployment controls, production/preview environments, build/runtime
flags, server management, secret-manager connections, arbitrary-key storage, or
Coolify's save/redeploy model. Configurator prepares local candidates from an
admitted definition; it is not an infrastructure dashboard.

Nor should compactness mean an unreadable spreadsheet, hidden values everywhere,
tiny targets, or an extra edit icon before every input. The goal is more useful
information per screen **with less work**, not maximum row count.

Carbon's data-table guidance supports consistent density, sufficient width,
contextual actions, and supplementary row expansion [S06]. It does not establish
that a table is the best editor for nested objects or every guided workflow.

## Recommended information architecture

### One primary journey, with direct access for experienced operators

**Choose project -> Configure -> Review -> Files**

Opening a pinned definition link can begin directly at Configure. Opening an
existing configuration is an alternative starting action inside that workspace,
not a new conceptual product.

On desktop:

```text
+-----------------------------------------------------------------------+
| ISLAMU Configurator | Event / Environment file | Find setting | Settings |
+--------------------+--------------------------------------------------+
| Projects           | Configure          Review          Files         |
|                    +--------------------------------------------------+
| CONFIGURATION      | Form / Developer       Import existing            |
| Basics             |                                                  |
| Database           | Database                                         |
| Identity           | Human label          [editable value           ] |
| Email              | Exact key            concise, specific hint      |
| Integrations       |                                                  |
| Advanced           | Human label          ( ) Yes ( ) No (*) Default  |
|                    |                                                  |
| Local to this tab  | Section status             Review configuration  |
+--------------------+--------------------------------------------------+
| Help / privacy                  Source / version                       |
+-----------------------------------------------------------------------+
```

This is a conceptual arrangement, not a rendered mockup or a fixed pixel
specification. Section examples must follow actual application semantics.
Do not fabricate database or identity sections for definitions that lack them.

### Separate three different kinds of navigation

1. **Application navigation:** projects, open definition, help.
2. **Workspace navigation:** relevant configuration sections.
3. **Workflow panels:** Configure, Review, Files.

Keep these visually and semantically distinct. Do not show three competing
sidebars or two identical search fields. The desktop rail can contain a small
application area and the current project's section tree separated by a heading.
The workflow panels belong in the workspace header.

### Avoid mandatory wizard friction

Operators should be able to edit sections in their own order. A novice may use
“Next section,” but experts must be able to jump directly to a setting, switch
views, or inspect Review without completing a theatrical sequence.

The macro journey is stable; conditional settings change within Configure.
Do not turn each newly active field into another workflow step. Review should
remain reachable even when incomplete, so it can explain what needs attention.

### Establish one source of truth for the selected output

The output selector is a small workspace setting:

```text
Prepare: (*) Environment file  ( ) Configuration manifest
```

Show friendly descriptions and filenames where helpful. The selected output
controls which questions and files are relevant. It is not an ordinary field
buried among application answers.

Event also declares Tenant package, Operator identity, and Legal drafts.
Environment and Configuration manifest can be the prominent common choices;
keep the other declared tasks available through a clearly named output/task
chooser. Do not reduce the underlying definition to two outputs by accident.

## Default selection and value semantics

### Default Environment file for the Event profile

On a new Event workspace, Environment file should already be selected and its
relevant fields should be present. The user should not have to answer “Artifact
to prepare” before seeing the product.

This default belongs in the first-party profile or its authoring/generation
owner, then flows through shared semantics. Do not hard-code the English string
`environment` in a generic Browser component. Other projects may legitimately
prefer a different output.

Specifically, the current contract initializes through
`x-configurator.initialValue`, not JSON Schema `default`
(`DefinitionValidator.cs:266-272`). Adding only a descriptive schema default
would not fix the initial experience. Output selection must update the shared
`/operation` answer that controls projection activation, not merely a
Browser-local selected-file variable.

A default is not permission to override an explicit user selection, an admitted
launch choice, or a successfully imported value. The eventual contract should
define precedence once and test it across entry paths.

### Preserve distinctions without making users manage JSON terminology

| Semantic state | Suggested UI language | Required behavior |
| --- | --- | --- |
| Absent, with a declared effective default | Use default | Show the effective result separately from an explicit answer. |
| Absent, no effective default | Not set | Do not silently serialize a value. |
| Explicit false | No / Disabled | Remain distinct from absence. |
| Explicit true | Yes / Enabled | Remain distinct from absence. |
| Explicit empty string | Empty value | Remain distinct from missing input where the schema allows it. |
| Explicit null | Null, with explanation | Offer only for nullable values. |
| Invalid draft | Needs correction | Preserve what the user typed without making it an accepted typed answer. |
| Secret omitted for draft preparation | Complete locally | Do not represent it as ready or as a literal placeholder secret. |

These are proposed labels, not new wire values. Localize them and test their
meaning. An absent optional boolean does not automatically mean “No”; a default
can make the effective value true.

The current [Core contract](../CORE_DEFINITION_CONTRACT.md#effective-values-and-known-semantic-limits)
distinguishes descriptive `default`, effective `initialValue`, and inert
`suggestedValue`. Explicit omission suppresses initialization; Reset removes
the override and suppression so initialization can apply again. “Use default”
must mean that latter operation where an initial value exists, not an explicit
omission. For a merely descriptive target default, say that the target decides
the value rather than displaying an invented Configurator value.

### Defaults should remove decisions, not conceal consequential choices

Use explicit authored defaults for benign settings. Do not preselect secret
consent, invent host capabilities, enable integrations requiring credentials, or
populate unknown production endpoints. Do not mark a suggested example as an
accepted answer. Distinguish target defaults from Configurator-supplied defaults
when the definition can express that distinction.

Changing Environment file to Manifest should:

1. Preserve compatible answers and the pinned definition.
2. Recompute activation and requiredness through the shared model.
3. Identify newly required work and any omitted output effects.
4. Invalidate prepared files.
5. Keep focus on the selection unless a deliberate navigation action follows.

Do not clear the entire workspace or silently discard hidden values.

## Form controls and compact editing

### Choose controls by meaning and choice count

| Field | Recommended control | Important exception |
| --- | --- | --- |
| Boolean with meaningful absence/default | Visible radio group, often three options | Do not merge absence and false. |
| Explicit two-state independent capability | Checkbox, or switch if immediate on/off behavior is clear | An indeterminate checkbox is not a substitute for an unexplained third choice. |
| Two to roughly five short mutually exclusive options | Radios or radio-styled segments | Wrap or stack when labels are long; the cutoff is a design heuristic. |
| Many known options | Searchable select/combobox | Provide keyboard and screen-reader behavior; do not show hundreds of radios. |
| Ordinary string, URL, identifier, number | Visible inline input | Match input purpose and preserve invalid drafts. |
| Long text | Textarea | Do not force multiline text into a one-line password-like field. |
| Secret scalar | Masked control after explicit consent | Reveal is deliberate, scoped, and reversible. |
| Nested object | Named field group | Show useful child fields; do not require “Supply object” for routine editing. |
| Repeated records | Compact repeated groups or a suitable table | Stable item identity and accessible add/remove controls. |
| Arbitrary admitted map | Key/value rows | Only where the definition permits additional keys. |

### Show the selected section's fields immediately

“Optional” means the value may be omitted; it does not mean the editor should be
hidden. An empty visible control can still represent absence until a real edit
occurs. Removing the Configure button must not accidentally supply empty
strings or empty containers just because components mounted.

Advanced *groups* can be collapsed by default, with a title, useful description,
and count. Searching for a hidden setting or following an error must open its
group and focus the right control. Do not show an endless list of collapsed
per-field buttons as a substitute for grouping.

For objects and lists, normal child editing should establish an admitted value
when needed. Preserve explicit actions for unusual semantic states such as an
empty object/list, omission, and null without making them prerequisites for
routine use.

### Compactness should be the working default

Use a coherent compact form, not a compressed comfortable form:

- Align labels, technical keys, inputs, status, and secondary actions.
- Keep most ordinary rows to a label/key line and a control line, or an aligned
  desktop label/value layout when labels are short.
- Put long URLs, connection strings, and multiline fields on full-width rows.
- Use two columns only for independent, short fields with a clear reading order.
- Remove per-field boilerplate and decorative wrappers before shrinking text.
- Let errors and genuinely useful hints expand the row naturally.

Suggested starting tokens, to validate in a prototype: 14-16 px readable form
text, approximately 36-40 px desktop control height, 8-12 px related-item gaps,
and 24-32 px between semantic groups. These are proposals, not measured current
values or accessibility requirements. Touch layouts should use larger hit areas.

### Keep useful operations discoverable

Reset to default, omit, null, reveal, and technical details have different
meanings. Give the most relevant operation a visible affordance when it matters;
keep rare operations in a named menu. Do not place an identical always-prominent
help icon, overflow icon, and long explanatory paragraph on every row.

Keyboard focus must reveal any action that pointer hover reveals. Touch users
need an always-available route. A label or action menu must not reveal a secret
or mutate an answer as a side effect of opening it.

## Navigation and visual system

### Make the desktop rail fill the body

Use a real application layout with a header, body containing rail and main
workspace, and footer. The rail spans the body from header to footer even when
its links are short. Keep the header/footer independent rather than estimating
their heights in fragile CSS calculations.

For long workspaces, allow the rail's content to remain reachable without
introducing several competing scroll containers. Prefer one main workspace
scroll plus a bounded navigation overflow where necessary. The editor's own
text area is an intentional exception in Developer view.

Keep the rail stable during section changes. On mobile it becomes a drawer or
section chooser with explicit close, Escape behavior, sensible focus return,
and no background interaction if implemented as a modal.

### Establish a visual hierarchy

Use three clear surface roles: application background, workspace surface, and
interactive field. Avoid nested cards around every heading. Use subtle borders
for grouping, strong enough control boundaries for editing, and an accent
reserved for primary actions/current state.

The page should answer these at a glance:

- Which project and output am I configuring?
- Which section am I in?
- What needs attention?
- What is the next action?

Current section and selected option need more than color: position, label,
shape, or indicator must reinforce them. Hover is not selection. Focus is not
an error. Disabled is not the same as secret-deferred.

### Improve typography and technical content

Use a modest project title and stronger section labels, rather than repeatedly
large headings inside nested cards. Keep explanatory paragraphs to a readable
line length while giving editing surfaces enough width.

Human labels should use sentence case. Technical keys belong in a monospace
secondary line or a dedicated Developer view column. Preserve their exact bytes;
do not truncate away distinguishing suffixes without an accessible way to read
them. Do not uppercase ordinary interface prose to imitate environment keys.

### Simplify the header and footer

Keep brand/project context, setting search, and an accessible Settings action.
Move theme, density, and automatic direction into Settings or a compact
preferences menu. Preserve System theme as a useful preference, not another
decision required before configuring.

Keep help/privacy and source/version in a quiet footer. During active editing,
the action bar belongs to the workspace, not to a large website-style footer.
At short viewport heights, avoid stacking so many sticky elements that the
form becomes unusable.

## Content and discovery

### Replace machinery-shaped language

Suggested conceptual copy, not accepted translation resources:

| Current wording | Proposed direction |
| --- | --- |
| Artifact to prepare | Output / Prepare |
| environment | Environment file (`event.env`) |
| select.feature.database-config | Database configuration |
| CONTROL_PLANE_MAXIMUM_TENANT_COUNT | Maximum tenants |
| Host validator: absolute-uri | Enter a complete URL, including `https://` |
| Host restart: deployment; startup-owned: true | Requires target restart, where that is the actual declared effect |
| Not supplied | Use default / Not set, according to semantics |
| Detail in this profile | Advanced settings, if this can replace the underlying choice honestly |
| Review preparation | Ready to prepare / Settings to fix |
| Clear answers, comparison and output | Clear workspace, away from the primary path |

Do not mechanically prettify every key and assume the UX is solved.
`CONTROL_PLANE_MANAGED_MODE`, for example, needs actual domain explanation and
typed semantics; splitting underscores cannot determine its meaning.

### Turn feature selection into capability selection

The visible `select.feature.*` rows look like internal configuration switches.
Operators should instead see meaningful capabilities grouped around a task:
database, identity, email, integrations, and other sections actually supported by
the definition.

Show what a choice adds, what it requires, and whether it affects the selected
output. Explain once that these are offline choices, not detected deployment
capabilities. Do not expose every internal module toggle just because the
generator can enumerate one.

Profiles can supply a useful starting selection, but profiles must be named by
purpose rather than imply unverified production readiness. Show the resulting
changes before replacing explicit choices. Neither a profile nor a visual toggle
should grant target authorization.

### Consolidate project discovery

Use one project list with bundled/store entries identified clearly and local
file/public URL alternatives nearby. A card needs project name, purpose, output
types, and one primary action. When several revisions exist, expose a sensible
explicit revision choice without inserting a revision screen for the only
available revision.

Do not label an unsigned publisher claim “Verified.” Put identity, revision,
digest, source, and trust explanation in a consistent inspectable panel. This
reduces repeated warnings without hiding the boundary.

### Make setting search a direct route to work

Search human labels, exact keys, descriptions, and admitted aliases. Results
should show section, field, and meaningful state: required, invalid, inactive,
or advanced. Search must not depend on currently mounted DOM rows.

Selecting a result opens its section and focuses its control. For inactive
settings, explain the controlling choice and offer navigation to it; do not
silently enable a capability. For denied secrets, offer consent recovery without
granting it automatically.

Use small optional filters such as Needs attention, Changed, and Advanced.
Show active filters and a recovery action for no results. Keep answer values
out of public metadata search, logs, URL parameters, and telemetry. No extra
search-state copy is needed in the header.

### Put privacy information at the right moment

Keep a concise “Local to this tab” message visible. Explain secret consent when
the user first reaches a secret or imports potentially secret-bearing content.
Retain a persistent way to inspect/revoke consent, and summarize its consequences
before revocation.

Do not replace explicit consent with preselection or treat Developer view as
implicit permission. Do not put several paragraphs of privacy text before every
ordinary input. Detailed guarantees and limitations belong in one consistent
help location and at the relevant custody transfer.

## Review, preparation, and export

### Review should show decisions and remaining work

Recommended structure:

1. Ready to prepare, or a concise count of settings to fix.
2. Selected output and files expected under current activation.
3. Missing/invalid ordinary fields, linked directly to correction.
4. Missing secrets with clear choices: enter here or prepare a deferred draft.
5. Changed/defaulted/omitted settings grouped by section.
6. Target validation/restart effects, only where actually declared.

Do not display every optional omission at equal weight. Let the user inspect
them when needed. Review must not reveal restricted values incidentally.
Masking should not conceal whether a value is absent, deferred, or present.

A correction should return the user to Review after editing. If that edit
activates new required settings, show the new work first. GOV.UK's check-answers
pattern supports contextual change actions and return-to-review behavior [S04].

### Readiness must be truthful

“All required answers ready” is not “the application is correctly configured.”
An artifact-selector-only denominator is particularly misleading when hundreds
of optional values remain.

Prefer a small status such as “No required fields missing” with a separate
summary of configured optional settings and unresolved secrets. Show a
percentage only when it communicates a stable, meaningful task denominator.
Do not make optional fields mandatory merely to produce a more impressive
progress bar.

Preparation success is local artifact validation/composition, never target
acceptance or application of configuration.

### Use one primary action per state

| State | Primary action | Secondary route |
| --- | --- | --- |
| Configuring | Review configuration | Jump to next section, optional |
| Blocked by ordinary errors | Fix settings | Inspect details |
| Ordinary settings valid, secrets missing | Complete secrets or Prepare draft, clearly explained | Inspect deferred paths |
| Ready | Prepare files | Return to edit |
| Prepared | Download selected file | Copy / Reveal |
| Clipboard failure | Download file | Retry copy when useful |
| Workspace needs clearing | Clear workspace in separate menu | Cancel; confirmation only where real work would be lost |

The exact prominence of draft preparation should follow the remaining work,
not make users choose between two unexplained preparation modes at all times.
Do not silently downgrade a requested complete file to a draft.

### Give Files its own useful surface

For each prepared artifact, show filename, format, draft/complete state, and
specific unresolved paths where applicable. Keep Download primary and Copy/
Reveal secondary. Copy errors belong to that file and must not include values.
Reveal has a clear Hide action.

After any relevant edit, invalidate stale files synchronously and explain that
they must be prepared again. A route change or delayed completion must not
restore old output.

When the Clipboard API is unavailable, make Download useful immediately.
Do not require a failed copy attempt to discover the alternative. A successful
browser download remains a deliberate release of private content, not a claim
of protected filesystem permissions.

## Developer view specification

### Product purpose

Developer view is **a definition-aware configuration editor**, not a generic
code playground and not merely a textarea containing the generated output.
It should let an experienced operator paste, inspect, edit, validate, and apply
many settings quickly while keeping Form view and preparation semantically
consistent.

Expose **Form / Developer** beside the configuration surface. Do not add
separate “Expert,” “Advanced,” “Raw,” and “Developer” modes for overlapping
purposes.

### Two documents have different jobs

1. **Canonical answer editing:** edit the existing private answer envelope
   admitted by the current definition: `answerContractVersion`, `values`, and
   `omitted`. Its JSON representation covers booleans, null, nested objects,
   lists, exact strings, and explicit suppression without inventing a new
   answer format.
2. **Selected artifact editing:** edit the `.env` or manifest representation
   only where an admitted, sufficiently unambiguous inverse mapping exists.
   Show coverage and unsupported regions explicitly.

Start with a canonical JSON answer document plus a constrained Environment
file representation for the Event profile where mapping supports it. This is
one Developer view with a document/representation choice, not two independent
answer stores.

The envelope is defined by
`SetupAnswerSnapshotCodec.cs:8-54` and the
[private answer schema](../../../schemas/configurator-answers-v1.schema.json).
A `values`-only editing surface may be offered as a convenient projection, but
it must preserve omitted pointers out of band and explain that scope. It cannot
claim to be a complete snapshot or flatten supplied and effective initialized
values together.

Exact source paths also matter: `/env.KEY` can name one root property containing
a literal dot, not a nested `env` object. Preserve admitted JSON Pointer
semantics; do not derive paths by splitting technical keys on punctuation.

Do not promise arbitrary raw-file round-tripping from a forward output template.
A composed value may combine multiple answer fields; constants may be read-only;
some answers may not appear in that artifact. Repeated keys, flattening, and
conditional outputs can also prevent a reliable inverse.

`DefinitionImportProjection.cs:27-84` already compiles a bounded inverse and
rejects unsupported or ambiguous shapes. Use that capability evidence, not the
mere presence of a `.env` artifact. The Event Environment projection includes
conditional content; full-file editable coverage is therefore **not established**
by this investigation. Its qualification or a deliberately constrained
representation is a delivery requirement, not work the editor may silently skip.

### Editing transaction and synchronization

The shared form remains the authoritative accepted workspace. The editor holds
a transient working document because partly typed text is not always valid JSON
or valid configuration.

Recommended explicit transaction:

1. Create the document from the current accepted revision and selected scope.
2. Let typing remain local to the working document; do not reformat on each key.
3. Parse and show diagnostics without mutating accepted answers.
4. **Apply changes** validates a complete candidate through shared semantics.
5. On success, update the workspace atomically, retire prepared output, and mark
   the editor synchronized.
6. On failure, retain the text and the prior accepted workspace. Show exact
   lines/paths and value-safe recovery guidance.

This deliberate Apply boundary is appropriate for a bulk operation; it must
not reappear as one extra “Configure” click per field.

Switching back to Form with unapplied text offers Apply, Discard text changes,
or Stay in Developer view. It must not silently erase invalid text or imply
that it was accepted. Do not block switching when the document is unchanged.

While a document is dirty, other answer-changing actions must either be
unavailable with an explanation or go through an explicit resolution flow.
Capture form identity, input revision, selected representation, and operation
generation. Reject stale parse/apply results rather than merging against a
different workspace invisibly.

### Essential editing capabilities

| Capability | Expected behavior |
| --- | --- |
| Syntax highlighting and line numbers | Help scan a document without making color the only diagnostic signal. |
| Local completion | Offer admitted keys, enum choices, and valid structure; never remote suggestions. |
| Hover/help | Explain human label, expected type, requirement, and output mapping without exposing private values. |
| Diagnostics | Clickable line/column and canonical field path; errors never echo credentials. |
| Find | Search within the private local document only; do not log or persist queries/results. |
| Format | Explicit action; preserve caret reasonably and never format invalid text destructively. |
| Undo/redo | Session-local, bounded, and retired on custody changes. |
| Change summary | Added/changed/removed/defaulted counts and a masked preview before application. |
| Coverage | State which fields can be edited and which are read-only, omitted, or unsupported. |
| Form navigation | From a diagnostic/key, open the exact field in Form view. |
| Keyboard access | Reach actions and leave the editor without a trap. |
| Offline operation | Bundled engine, languages, schemas, and workers; no runtime CDN or language server. |

Useful later capabilities include section folding, local multi-cursor editing,
safe duplicate detection, and an explicit sample template. These should not
delay the core parse/apply/diagnostic/custody contract. No remote AI completion,
terminal execution, package installation, or external secret resolver belongs
in this view.

### Environment syntax must be a defined codec

Publish the supported `.env` dialect instead of assuming all dotenv consumers
agree. The editor must use the same owning codec as import/composition, not
implement a second parser in the Browser.

Specify handling for:

- Comments, whitespace, blank lines, CRLF/LF, and encoding/BOM.
- Empty values versus omitted keys.
- Quoted values, escapes, literal `#`, literal `$`, and `=` inside values.
- Multiline values and whether the selected artifact supports them.
- Duplicate keys and case sensitivity.
- Unknown keys, read-only keys, and conditional mappings.
- Exact scalar conversion, especially boolean, number, and null.
- Maximum bytes, keys, depth, and diagnostic count.

Do not execute interpolation, shell commands, includes, file references, or
network lookups. Unknown or ambiguous input must produce an explicit
disposition, not disappear silently. Prefer rejection for ambiguous duplicates
over an invisible last-value-wins rule.

### Deletion and scope must be visible

Deleting a line cannot have an undocumented meaning. Distinguish:

- **Merge represented edits:** absent keys retain current answers.
- **Replace represented scope:** absent represented keys are omitted/reset as
  explicitly summarized.

Recommended default for pasting/importing Environment text is merge, with an
explicit replacement action and removal summary. Canonical answer-document
editing may replace its declared scope, provided omission effects are stated.
This intentional difference from Coolify's documented synchronization behavior
protects answers not represented in the chosen artifact.

Applying a selected artifact must not erase answers belonging only to another
artifact, hidden conditional fields, or unrelated sections.

### Secrets require a separate editor design decision

A literal full-document editor can reveal secrets in its model, rendered text,
clipboard, undo history, browser accessibility tree, completion data, and
diagnostics. Password-style CSS is not sufficient masking for that model.

Recommended first delivery:

1. Developer view edits public fields and non-secret structure.
2. Restricted scalar values remain protected, read-only spans or are omitted
   from that representation with an explicit coverage list.
3. Secret entry and reveal use dedicated controls under workspace consent.
4. A bulk paste that could contain secrets is a deliberate private-input
   operation: require consent before reading clipboard/file content through
   application APIs or accepting a potentially secret-bearing editor input.
5. Do not focus an unrestricted bulk-paste surface while consent is denied.
   An ordinary public-field input remains usable without that consent.

A future full-private-document mode would need explicit reveal intent and a
reviewed custody lifecycle. It must not be implied by merely selecting Developer
view. Secret consent alone is not an instruction to reveal every secret.

Never serialize display masks as values. Do not interpret `********`, a fixed
placeholder, or a “deferred” token as a real credential or as an authenticated
reference. Keep protected spans outside the editable value representation and
resolve them by trusted local editor state, not user-supplied text.

On revocation, clear secret-bearing pending text, editor models, undo/redo,
diagnostics, diff buffers, accepted secret leaves, and prepared artifacts.
Reject late callbacks. Recreating the editor must not restore retired content.
“Clear” means retiring application-owned references; it is not a promise of
physical erasure from a managed runtime or from an external clipboard.

### Editor engine recommendation

Evaluate **CodeMirror 6** first against a small vertical prototype. Its documented
Tab behavior is designed to avoid a keyboard trap; its API documentation exposes
editor state and accessibility-related facilities [S10]. That is evidence of
available mechanisms, not proof that our configuration will be accessible.

Evaluate **Monaco** only if concrete language-service needs justify its footprint
and platform constraints. Its official site explicitly says mobile browsers are
not supported [S11]. Do not make a desktop-oriented editor the only editing path.

A plain textarea is a useful accessible baseline or narrowly scoped fallback,
but by itself does not satisfy the requested schema-aware editor experience.
Do not ship a textarea labeled “Developer view” and claim the whole proposal
complete.

No package is selected or approved by this report. Compare real published
compressed transfer size, startup work, memory, mobile/IME behavior, screen-reader
support, offline worker closure, CSP requirements, and exact licenses/notices.
Avoid unsupported numerical bundle-size claims from reputation alone.

## Accessibility, mobile, localization, and performance

### Accessibility is part of the interaction contract

Target WCAG 2.2 AA across the whole workflow. Applicable requirements include
keyboard operation and no traps, associated labels/grouping, visible and
unobscured focus, error identification, reflow, status communication, and
contrast. Minimum pointer targets generally use 24 by 24 CSS pixels with stated
exceptions; 44 px is a useful larger touch-design target, not the universal AA
minimum [S08]. WAI explains the role of programmatic control grouping [S09].

For this redesign, verify:

- Radio groups announce a meaningful legend and support expected arrow-key
  behavior; selection is not represented by generic unlabeled buttons.
- Labels activate controls, errors are associated, and hint/error IDs remain
  valid after section changes.
- Search, section switching, review correction, and return navigation preserve
  purposeful focus without stealing it during typing.
- Drawer/dialog closure restores focus; hidden panels are not keyboard targets.
- Live announcements summarize meaningful state changes, not every keystroke.
- All functionality works without hover or drag.
- The editor supports screen-reader operation and a documented exit path.
- Compact mode remains readable under text enlargement and forced colors.

GOV.UK's validation pattern recommends preserving input and making errors
actionable rather than interrupting incomplete typing [S12]. For Configurator,
use a quiet editing state and show blocking errors on Apply, Review, or Prepare.
Syntax feedback in Developer view may be live, but do not announce every
transient parser error or treat a partial keystroke as submitted configuration.

### Responsive behavior must preserve the task

At narrow widths, stack labels and inputs; do not squeeze a desktop matrix into
a horizontal scroll. Use a section chooser and a clear current-section heading.
Developer view can offer an explicit expanded editor surface, with a reachable
route back to Form.

Test at 320, 375, 768, 1280, and 1920 CSS px, portrait/landscape, short heights,
text enlargement, and browser zoom. These are proposed coverage points, not
claims that the current screenshots meet or fail specific criteria.

The soft keyboard must not cover the focused input or Apply action. Avoid
several nested scroll areas and fixed bottom bars that obscure controls.
Do not rely on a resize handle as the only way to make a text editor usable.

### Arabic, RTL, and technical text

Keep layout logical and mirrored where appropriate. Human descriptions and
navigation should follow the interface language. Isolate technical keys, URLs,
file paths, and serialized documents in a stable left-to-right text context
without forcing the surrounding Arabic interface into LTR.

Avoid alphabetical key order as the only information architecture. Translated
titles may wrap and need more space. Test mixed Arabic/Latin input, IME
composition, caret movement, error navigation, and copy behavior on actual
controls. Default Automatic direction should be an understandable preference,
not a required onboarding choice.

### Do not trade fewer clicks for an unresponsive page

The current source contains incremental mounting and revision/generation fences.
Those mechanisms should not be discarded just to make inputs visible.
Group settings better, mount the selected working section, retain control
identity, and isolate preference/search rendering before considering heavier
virtualization.

Do not eagerly create hundreds of editors, action menus, or syntax engines.
Preload/bundle necessary public editor assets as part of the offline app,
without answering through a network service or fetching a new definition during
live answers. Keep answer-bearing editor buffers out of service workers.

Measure separately:

- First editable section ready.
- Pointer/key input to visible control response.
- Section navigation to usable fields.
- Feature selection to stable dependent controls.
- Developer engine activation, parsing, and Apply.
- Preparation, cancellation acknowledgment, and actual operation settlement.

The web.dev INP guidance uses 200 ms as its good threshold at the 75th percentile
of field page visits [S13]. That is not the same measurement as a laboratory
p95 for one control or proof of all asynchronous work completing.

Proposed local goals are input feedback p95 under 100 ms and section/view changes
p95 under 200 ms on a declared reference device and representative definition.
These are unmeasured candidate goals, not permission to relax existing stricter
budgets. Retain raw slow interactions and maxima; do not discard inconvenient
outliers to declare success. Do not add production telemetry to obtain evidence.

## Implementation boundaries and deliberate breaking changes

### Keep the architecture that already protects correct behavior

| Owner | Responsibility in the proposed design |
| --- | --- |
| Definition authoring/integration owner | Meaningful labels, sections, capability choices, output default, descriptions, and declared mapping coverage. |
| Core | Admission, canonical identity, types, activation, defaults, validation, codecs, composition, and bounded inverse capability. |
| Presentation | One accepted answer workspace, invalid drafts where applicable, provenance, section/view state, readiness, and operation generations. |
| Browser | Shell, controls, focus, local working document, accessible editor integration, and deliberate export. |
| Native clients | Consume shared semantic changes where affected; no requirement to imitate the Browser layout. |
| Target application | Independently validates, authorizes, and applies prepared configuration. |

Form edit -> shared typed answer/draft -> shared activation/validation ->
projection -> visible controls/review -> composition -> explicit browser export.

Developer edit -> transient document -> bounded parse/candidate validation ->
revision-fenced atomic workspace application -> the same projection/composition
path. Neither route creates a second semantic evaluator.

### Change the definition experience as well as the Browser

A better shell cannot invent absent domain explanations, correct a string that
should be typed as boolean, or determine useful groups from arbitrary technical
identifiers. Repair the first-party authoring source/generator and regenerate
compiled definitions through their owner. Do not hand-edit generated projections
or teach the Browser a special list of Event keys.

The current file format, compact authoring model, and existing presentation
metadata should be evaluated for sufficient expression before adding more
schema features. No executable definition plugin or remote presentation code
is needed.

### Intentionally retire these interaction contracts

- No selection as the mandatory initial Event output state.
- Per-field Configure buttons for ordinary eligible scalar editing.
- Dropdowns as the universal boolean/short-enum renderer.
- A content-height desktop navigation dropdown as the primary workspace rail.
- Workflow buttons that only jump within an always-rendered giant form.
- Raw generator identifiers as normal end-user labels.
- Per-field boilerplate as the default way to explain host effects.
- Empty review-success containers.
- Destructive clearing beside the routine primary preparation action.

Update tests that explicitly encode removed behavior. Preserve or strengthen
tests for omission/default distinctions, consent, masking, invalid drafts,
identity, generation fences, and output retirement. An old UI assertion is not
a backward-compatibility commitment; an invariant is not expendable because
the product is unreleased.

### Documentation affected by eventual implementation

Update [public Browser guidance](../../public/browser.md) and
[Browser UI](../BROWSER_UI.md) together, and shared
[Presentation state](../PRESENTATION_STATE.md) or
[import/comparison](../IMPORT_COMPARISON.md) only where behavior changes.
Definition-authoring changes require their public/technical counterparts and
regenerated examples. This report itself is not a replacement living contract.

## Priorities and acceptance criteria

### Delivery order

| Increment | Deliverable | Exit evidence |
| --- | --- | --- |
| A: task model and definition quality | Environment default, meaningful sections/labels, capability grouping, explicit state/default language. | Source-owner semantic tests and reviewed synthetic Event journey. |
| B: working shell and direct controls | Full-body desktop rail, real panels, immediate inputs, visible short choices, compact layout. | Actual desktop/mobile and keyboard journeys, not CSS-source assertions. |
| C: completion and recovery | Useful review, focused correction, clean preparation/file states, contextual export errors. | Complete, blocked, deferred-secret, cancellation, and failed-copy journeys. |
| D: Developer view | Typed document, mapping coverage, diagnostics, atomic Apply, protected-value model, offline engine. | Cross-view equivalence, invalid/stale edit rejection, lifecycle/privacy tests, actual editor accessibility. |
| E: qualification | Measured performance and usability with relevant devices, languages, and assistive technology. | Retained evidence tied to build, definition digest, device, scenario, and unresolved limitations. |

Developer view is a central deliverable, not optional polish. Its security and
mapping design can begin alongside the shell, but should not block delivering
the simpler form first. Conversely, finishing the shell does not complete the
requested redesign direction.

### Observable acceptance scenarios

These are proposed requirements for a later approved implementation, not test
results from this investigation.

| ID | When | Then |
| --- | --- | --- |
| AC-01 | A new Event workspace opens without explicit output override. | Environment file is selected and its first section is usable without an output-selection click. |
| AC-02 | The user selects Manifest. | Applicable fields update, compatible answers remain, new requirements are explained, and prepared output retires. |
| AC-03 | An eligible ordinary scalar appears in the selected section. | Its input is visible and editable without Configure or per-field expansion. |
| AC-04 | A boolean supports absence, false, and true. | Visible options preserve all three meanings with meaningful labels and keyboard semantics. |
| AC-05 | A short enum is shown. | Its options can be inspected without opening a dropdown. |
| AC-06 | A defaulted field is untouched. | Its origin/effective value is distinguishable from an explicitly supplied answer. |
| AC-07 | An input mounts but receives no edit. | It does not accidentally supply an empty string/object/list. |
| AC-08 | The desktop page has little or much content. | The rail spans the body between header and footer and does not cover workspace content. |
| AC-09 | Configure, Review, or Files is selected. | The selected panel is identifiable; inactive panel controls do not clutter or intercept focus. |
| AC-10 | Search selects an advanced field. | Its section/group opens and the actual control receives focus. |
| AC-11 | Search finds an inactive field. | The controller is explained; selection does not silently enable it. |
| AC-12 | A section changes. | Answers, invalid drafts, consent, pinned identity, and useful selection state remain intact. |
| AC-13 | Review has no blocking errors. | A populated ready summary replaces the empty heading box. |
| AC-14 | Review has ordinary errors. | Linked correction preserves input and returns to review after completion. |
| AC-15 | Required secrets are missing. | Complete preparation is not offered as successful; deferred draft requirements are explicit. |
| AC-16 | A prepared file is edited or consent revoked. | Old bytes disappear from the UI and cannot be restored by delayed work. |
| AC-17 | Clipboard permission is denied/unavailable. | The affected file provides a value-safe message and working Download alternative. |
| AC-18 | The workspace contains real work and Clear is invoked. | Destructive effects are explicit and separate from ordinary preparation. |
| AC-19 | Developer view opens. | Representation, scope, mapping coverage, and pending/accepted state are clear. |
| AC-20 | Invalid document text is applied. | Text remains editable; no partial answer update or misleading success occurs. |
| AC-21 | Valid represented changes are applied. | The resulting typed workspace, validation, and artifacts match equivalent Form edits. |
| AC-22 | A `.env` mapping is ambiguous/unsupported. | The editor explains the boundary instead of guessing or deleting unrepresented answers. |
| AC-23 | A key is omitted/deleted. | Merge/replace semantics and removal effects are explicit before application. |
| AC-24 | An editor diagnostic completes after replacement/edit/revocation. | It cannot change the current workspace or restore retired private text. |
| AC-25 | A protected value exists. | Developer view never replaces it with a literal mask or reveals it by switching view. |
| AC-26 | Consent is revoked with private editor history present. | Application-owned private working text/history/diff/diagnostic references retire along with secrets/output. |
| AC-27 | The editor is used by keyboard or screen reader. | Editing, diagnostics, Apply, cancellation, and exit work without traps. |
| AC-28 | Width, text size, theme, direction, or input method changes. | Controls remain legible/reachable and state is preserved. |
| AC-29 | A loaded offline workspace is edited and prepared. | No answer-bearing requests, persisted private buffers, or remote language services occur. |
| AC-30 | A long operation runs or is cancelled. | Feedback is prompt, current work remains responsive, and obsolete completion cannot publish. |

### Quantitative validation proposal

Use declared synthetic tasks, not private deployment values:

1. New Event Environment setup.
2. Switch to Manifest without losing compatible work.
3. Configure ten optional scalars.
4. Change ten boolean/short-enum values.
5. Find and correct an obscure setting.
6. Import/compare an existing file under explicit consent.
7. Prepare a deferred-secret draft and explain what remains.
8. Apply a multi-setting Developer edit, then correct malformed text.
9. Revoke consent during pending work.

Count actions needed for the task, not arbitrary navigation clicks. For ten
collapsed scalar fields, the current pattern adds ten expansion actions before
editing; the proposal removes those ten. For ten pointer-driven boolean changes
that currently require opening a menu and selecting an option, visible choices
can remove ten menu-open actions. These are structural action counts, not a
measured time-saving percentage.

Compare completion, correction success, hesitation, and task time against both
the present build and a simple key/value baseline. Do not assume a sophisticated
form outperforms that baseline merely because it has more features.

Proposed formative study: 8-12 representative operators across less experienced
and technical users, including assistive-technology participants. Seek at least
90% unaided completion on the core tasks, no unexplained data loss, and correct
understanding of draft versus complete and preparation versus apply. With a
small sample, report actual participant/task counts and qualitative failures,
not statistically strong population claims or an “enterprise-grade” score.

## Research source register

All sources were accessed on October 31, 2026. Sources are public primary
documentation; no search snippet alone establishes a proposed API integration.
Recommendations above are independently designed for Configurator.

| ID | Source | What it supports; limit |
| --- | --- | --- |
| S01 | [GOV.UK: Radios](https://design-system.service.gov.uk/components/radios/) | Single-choice semantics, grouping, and caution about preselection; not a mandate to leave output preferences unset. |
| S02 | [GOV.UK: Select](https://design-system.service.gov.uk/components/select/) | Consider alternatives to selects and distinguish settings from substantive questions. |
| S03 | [GOV.UK: Details](https://design-system.service.gov.uk/components/details/) | Disclose supplementary information, not what most users need. |
| S04 | [GOV.UK: Check answers](https://design-system.service.gov.uk/patterns/check-answers/) | Useful review/change/return behavior; backend submission examples do not imply a Configurator backend. |
| S05 | [USWDS: Step indicator](https://designsystem.digital.gov/components/step-indicator/) | Linear multi-screen progression versus nonlinear navigation. |
| S06 | [IBM Carbon: Data table guidelines](https://www.carbondesignsystem.com/building-blocks/core/components/data-table/guidelines) | Consistent density, width, contextual operations, and supplementary expansion; not a universal form prescription. |
| S07 | [Coolify: Environment variables](https://coolify.io/docs/applications/configuration/environment-variables) | Documented normal/bulk editing behavior and limitations; current docs do not prove every behavior of the screenshot's installed version. |
| S08 | [W3C: WCAG 2.2](https://www.w3.org/TR/WCAG22/) | Accessibility criteria; not evidence of this product's conformance. |
| S09 | [W3C WAI: Grouping controls](https://www.w3.org/WAI/tutorials/forms/grouping/) | Programmatic relationships and understandable grouped controls. |
| S10 | [CodeMirror: Tab handling](https://codemirror.net/examples/tab/), [reference manual](https://codemirror.net/docs/ref/) | Keyboard exit and editor/accessibility API mechanisms; no dependency approval or application conformance claim. |
| S11 | [Microsoft: Monaco Editor](https://microsoft.github.io/monaco-editor/) | Official editor capabilities and stated mobile-support limitation; no unmeasured bundle-size comparison. |
| S12 | [GOV.UK: Recover from validation errors](https://design-system.service.gov.uk/patterns/validation/) | Preserve inputs and provide actionable error summaries/field feedback; do not transplant server-specific architecture. |
| S13 | [Google web.dev: INP](https://web.dev/articles/inp) | Responsiveness measurement definition and field threshold; not interchangeable with per-control laboratory latency. |

### Source-free functional handoff

An implementer needs only the behavioral requirements, repository owner map,
acceptance scenarios, constraints, and source identities in this report.
Third-party code, screenshots/assets, copied prose, internal structures, and
distinctive visual composition are not implementation inputs.

The proposed layout follows Configurator's local workflow, existing shared
state ownership, admitted definitions, and custody requirements. Common
controls and standards constrain some choices; that does not authorize copying
an external product's discretionary expression. Any eventual editor dependency
needs its own exact-version terms, notice, closure, and distribution review.

## Evidence ledger and verification

Paths below are relative to repository root. Line references describe the
inspected revision, not a promise that they remain stable after implementation.

| Evidence | Inspected anchor | Status |
| --- | --- | --- |
| Per-field Configure branch | `src/Configurator.Browser/Components/Configuration/ConfigurationField.razor:6-12` | Source inspected; screenshot corroboration. |
| Boolean/enum select | `src/Configurator.Browser/Components/Configuration/ConfigurationField.razor:155-186` | Source inspected; not executed. |
| Disclosure visibility policy | `src/Configurator.Presentation/Presentation/DefinitionFormModel.cs:191-219` | Source inspected; not executed. |
| Field sensitivity/typing and callback fencing | `src/Configurator.Browser/Components/Configuration/ConfigurationField.razor.cs:155-213,242-279` | Source inspected; not a complete security audit. |
| Workflow focus/state transition | `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs:158-173` | Source inspected; not executed. |
| Selected-section rendering plus always-present stages | `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor:154-216` | Source inspected; screenshot corroboration. |
| Incremental mounting | `src/Configurator.Browser/Components/Configuration/ConfigurationSectionFields.razor.cs:18-19,47-96` | Source inspected; no fresh timing. |
| Content-height menu | `src/Configurator.Browser/Layout/NavMenu.razor.css:1-7`; `src/Configurator.Browser/Layout/MainLayout.razor.css:1-3` | Source inspected; screenshot corroboration. |
| Review's conditional success content | `src/Configurator.Browser/Components/Configuration/ConfigurationReview.razor:65-94` | Source inspected; hypothesis matches supplied image. |
| Current form, workflow, and density styling | `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.css:1-46` | Source inspected; no computed-style measurement. |
| Homepage primary button has source text | `src/Configurator.Browser/Pages/Catalogue.razor:12-13` | Source inspected; blank screenshot appearance still needs runtime diagnosis. |
| Output choices and missing initialization | `examples/islamu-event/configurator.json:15938-15952`; `src/Configurator.Core/Definitions/DefinitionValidator.cs:266-272` | Source inspected; shared output answer must drive activation. |
| Technical feature labels and false initial values | `examples/islamu-event/configurator.json:16123-16160` | Source inspected; broader selector inventory supplied by independent read-only investigation. |
| Fallback single-section experience | `src/Configurator.Presentation/Presentation/DefinitionFormModel.cs:45-66` | Source inspected; Event source has no authored presentation layout. |
| Answer/default/omission semantics | `docs/internal/CORE_DEFINITION_CONTRACT.md:185-211`; `src/Configurator.Presentation/Presentation/DefinitionEditorState.cs:100-120` | Contract and owning source inspected. |
| Bounded artifact inverse | `src/Configurator.Core/Definitions/DefinitionImportProjection.cs:27-84` | Source inspected; full Event Environment reverse editing remains unqualified. |
| Existing preparation/export tests | `tests/Configurator.Browser.Tests/BrowserConfigurationWorkflowTests.cs:9-77` | Assertions inspected, not run. |
| Current architecture and intended constraints | [Browser UI](../BROWSER_UI.md), [project boundaries](../../../PROJECTS.md), [quick reference](../QUICK_REFERENCE.md) | Documents inspected; not runtime qualification. |

Only this report and its navigation entry are intended deliverables. Product
source, tests, schemas, dependencies, strategy, and earlier report bodies are
unchanged. No branch, worktree, commit, publication, or deployment was created.
Documentation-check results are recorded in the final task handoff; product
checks were intentionally not run for this report-only request.
