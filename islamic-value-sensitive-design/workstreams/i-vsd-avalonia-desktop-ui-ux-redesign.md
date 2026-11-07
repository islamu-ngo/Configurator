# Avalonia desktop UI/UX redesign - Provider-responsibility assessment

Last Updated: 2026-11-06 Europe/Brussels

## Review Metadata

- Mode: planning
- Subject: Accessible, guided offline desktop configuration
- Workstream: avalonia-desktop-ui-ux-redesign
- Report kind: implementation-planning assessment
- Report revision: desktop-ux-ivsd-r2
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-11-06
- Reviewed input: exact desktop-ux-r2 plan/tasks hashes below; desktop-ux-e1 with CTO evidence E11-E13; context supplies status only
- Supersedes: none; earlier MVVM and architecture assessments retain their own subjects
- User approval: explicit implementation instruction received for the current checkout; parent owns execution; no history/publication authorization inferred

## Executive Summary

The redesign should make configuration understandable without weakening local
custody or overstating what preparation achieves. People using small windows,
larger text, keyboards, Arabic, or assistive technology need equal access to
required fields and corrective actions. A prettier palette alone does not
provide that access.

The current source already contains localized resources, light/dark semantic
brushes, native high-contrast handling, shared validation facts, and protected
publication ownership. Preserve those mechanisms while replacing the flat
workspace composition and introducing deliberate appearance selection,
section navigation, contextual feedback, and output review.

The completed desktop-ux-r2 plan/tasks have been revalidated against all six findings.
Plan-aligned means the proposal addresses these responsibilities with explicit
scenarios and tasks. It grants no implementation approval, runtime
qualification, scholarly conclusion, or claim of enterprise readiness.

## Scope

Provider-controlled desktop appearance, navigation, disclosure, search, error
presentation, output selection, operation feedback, native accessibility and
their maintenance/verification contracts. No new network service, telemetry,
answer persistence, authentication, target application apply, monetization,
religious-content policy, or new package is proposed.

## Claim Boundary

Evidence levels: design reasoning and implementation traceability from inspected
source and test intentions. Earlier visual observations and recorded native
receipts remain historical. No product suite, new native candidate, stakeholder
study, screen-reader cohort, legal review, or scholarly review ran for this plan.
WCAG guidance supplies design criteria, not certification of a native app.

## Findings

### IVSD-F001: Theme choice must not exclude people who need contrast or larger controls

Users should not trade readability for the requested aesthetic. The maintainer
controls palette pairs, target sizes, focus indication and responsive placement.
The existing native contrast subscription is a useful safeguard: explicit light
or dark selection must not disable it. Low-contrast pastel decoration must not
become low-contrast text or controls.

- Lifecycle / severity / claim type: accepted / high / design constraint
- Principles / domains: Justice, Excellence, Non-Harm / design, technical, evaluation
- Stakeholders: low-vision, keyboard, motor-impaired and larger-text users
- Controlled decision: appearance precedence, semantic control states and reflow
- Evidence / validation: E01, E02, R01-R05; source traceability, not runtime proof
- Mitigation: IVSD-M001 - System/Latte/Macchiato selection with native high-contrast
  precedence, measured actual color pairs, visible focus and reachable controls
- Owner / next validation: Desktop maintainer; rendered and actual-native checks
- Escalation: accessibility specialist before any broad accessibility claim

### IVSD-F002: Navigation must preserve answers and make hidden corrections reachable

Changing sections, appearance or discovery must not discard a draft, revive a
retired editor, silently initialize a container, or change what is ready to save.
People entrust unfinished configuration to the local workspace. A root map or
root variant must not disappear merely because it has no ordinary root fields.

- Lifecycle / severity / claim type: accepted / high / state and custody constraint
- Principles / domains: Trust, Rights of People, Non-Harm / design, technical
- Stakeholders: operators with long forms, protected fields or recursive definitions
- Controlled decision: visual mounting, section selection and correction navigation
- Evidence / validation: E03-E05; inspected revision, identity and correction owners
- Mitigation: IVSD-M002 - reuse current form/editor ownership and revision-bound
  correction APIs; bound visible mounting without dropping values or obligations
- Owner / next validation: Desktop maintainer; invariant-first navigation tests
- Escalation: a proposed new answer store or weakened generation fence requires
  a new design decision and assessment before implementation

### IVSD-F003: Search and feedback must not become a new disclosure channel

Search is useful for finding settings, but indexing answer text, dynamic private
keys or artifact bytes would create another sensitive representation. Error
messages must explain correction without echoing submitted protected data.
Repeated announcements and premature errors can also make the flow harder to use.

- Lifecycle / severity / claim type: accepted / high / privacy and usability constraint
- Principles / domains: Avoiding Spying, Trust, Excellence / design, technical
- Stakeholders: people entering credentials and users of assistive technology
- Controlled decision: discovery inputs, feedback visibility and announcement content
- Evidence / validation: E04, E05, E07; value-free shared discovery/validation contracts
- Mitigation: IVSD-M003 - metadata-only bounded discovery, typed correction facts,
  deliberate feedback reveal and value-safe status announcements
- Owner / next validation: Desktop maintainer; synthetic canary and stale-target tests
- Escalation: any answer retention, telemetry or new sensitive sink requires reassessment

### IVSD-F004: Simpler output actions must not imply permission to apply or overwrite

A single clear primary action can reduce mistakes, but must not merge preparation
with filesystem installation or target acceptance. Users need to distinguish
incomplete drafts, ready output, installed files, failed verification and partial
bundle outcomes. Cancellation does not prove rollback.

- Lifecycle / severity / claim type: accepted / high / authority and truthfulness constraint
- Principles / domains: Truthfulness, Trust, Avoiding Deception, Non-Harm /
  design, technical, operations
- Stakeholders: operators, target administrators and people affected by configuration
- Controlled decision: review stage, action enablement and publication summaries
- Evidence / validation: E06, E07; current Publisher, ImportReview and shell ownership
- Mitigation: IVSD-M004 - explicit output review, separate prepare/save actions,
  deliberate import adoption and truthful existing physical-publication receipts
- Owner / next validation: Desktop maintainer; held-completion and real create-only checks
- Escalation: changed target authority or protected-writer semantics blocks this scope

### IVSD-F005: Maintainability and source provenance are duties, not pattern counts

Adding a theme package, navigation framework or parallel validation model would
increase maintenance and licensing burden without an evidenced need. The provider
can keep native controls and existing state owners, use independently designed
composition, and retain recoverable sources for palette/interface facts.

- Lifecycle / severity / claim type: accepted / medium / architecture and provenance constraint
- Principles / domains: Excellence, Trust, Promise-Keeping / strategic, technical, governance
- Stakeholders: maintainers, distributors and future integrators
- Controlled decision: dependencies, ownership and externally informed implementation
- Evidence / validation: E01-E06, E08, R01-R03; source/contract inspection
- Mitigation: IVSD-M005 - extend existing Fluent/resources/MVVM composition;
  no new package or copied external component implementation
- Owner / next validation: Desktop maintainer; dependency/source review at phase exits
- Escalation: new external assets or packages use the repository provenance owner

### IVSD-F006: Qualification and overlapping work must remain honestly attributed

The shared checkout contains active architecture and deployment work. Its earlier
tests and receipts are not proof for a redesigned payload. Desktop native count
documentation also disagrees with the current 24-name harness. Reusing old dates
or claiming other hosts from Linux captures would mislead users.

- Lifecycle / severity / claim type: accepted / medium / evidence and governance constraint
- Principles / domains: Truthfulness, Justice, Promise-Keeping / operations, governance, evaluation
- Stakeholders: reviewers, operators, accessibility users and parallel contributors
- Controlled decision: source baseline, support claims, ownership and evidence publication
- Evidence / validation: E08, E09; inspected active contexts and actual harness name set
- Mitigation: IVSD-M006 - rebase the design on current working-tree owners,
  preserve others' changes, reconcile harness docs and bind claims to new candidate evidence
- Owner / next validation: implementation lead; exact-source and actual-host evidence
- Escalation: conflicting shared-file ownership requires coordination, not reversion

## Recommendations

The completed plan carries IVSD-M001 through IVSD-M006 into observable scenarios
and implementation tasks. Preserve existing high-contrast, masking, secret-action retirement,
revision fencing, import-adoption and create-only publication mechanisms.

Rejected alternatives: a colors-only repaint leaves navigation inequity intact;
forced pastel themes discard accessibility preferences; hidden auto-save grants
unrequested filesystem authority; a framework replacement adds unproven cost.
These are design comparisons, not findings of stakeholder preference.

## Stakeholders

| Stakeholder | Benefit sought | Burden or missing evidence |
| --- | --- | --- |
| First-time operators | Guided sections and understandable next actions | Terminology needs human usability review |
| Experienced operators | Search, disclosure and fast keyboard access | Do not bury legitimate advanced operations |
| Arabic/bidirectional users | Logical order, translated chrome and LTR technical paths | Translation and mixed-script usability need native observation |
| Accessibility users | Contrast, focus, target size and scaling | Actual assistive-technology behavior remains unproved |
| Target administrators | Accurate incomplete/prepared/installed distinctions | Target validation and acceptance remain their responsibility |
| Maintainers/distributors | Small owners and reproducible evidence | Parallel changes need coordination and source identity |

## I-VSD Principles And Domains

All six domains were considered. Strategic: no monetization or dependency-led
lock-in is introduced. Design: defaults, disclosure and action hierarchy protect
deliberate choice. Technical: existing custody and operation ownership remain.
Operational: failed/unknown outcomes and host limitations remain explicit.
Governance: separate user approval, implementation and qualification authority.
Evaluation: source reasoning does not replace stakeholder or native evidence.
Religious finance, moderation, AI ranking and federation are not applicable.

## Validation Gaps

No new rendered candidate, participant study, device/IME cohort, high-DPI
qualification, screen-reader speech evidence or cross-platform run exists yet.
Each must remain separately labeled; an automated geometry check is insufficient.

## Escalation Needed

No religious-legal ruling is required by this visual/local workflow scope.
Qualified Sunni scholarly review is required if the scope later introduces a
religious-legal claim; legal/provenance review applies to new external material.
The current explicit user instruction authorizes implementation by the parent.

## Evidence Reviewed

- E01: `src/Configurator.Desktop/App.axaml`, `App.axaml.cs`, `Views/MainWindow.axaml`.
- E02: `Resources/DesktopTheme.cs`, `ThemePalettes.axaml`, `DesignTokens.axaml`.
- E03: `ViewModels/FormWorkspaceViewModel.cs`, `Fields/FormFieldFactory.cs`,
  `Fields/FieldViewModel.cs`, current recursive root handling.
- E04: `src/Configurator.Presentation/Form/DefinitionFormModel.cs`,
  `DefinitionFormProjection.cs`, `DefinitionEditorState.cs`, `DefinitionFieldValidation.cs`.
- E05: `src/Configurator.Presentation/Discovery/DefinitionDiscovery.cs`.
- E06: `ViewModels/ArtifactPublisherViewModel.cs`, `DesktopShellViewModel.cs`,
  `Views/ArtifactPublicationView.axaml`; current ImportReview composition.
- E07: current Desktop tests/NativeHost and `docs/internal/DESKTOP_CLIENT.md`,
  `docs/public/desktop.md`, `docs/internal/PRESENTATION_STATE.md`.
- E08: repository contribution/governance/planning contracts and active architecture,
  enterprise-quality, deployment-aware and Browser workstream contexts.
- E09: `tests/Configurator.Desktop.NativeHost/Program.cs` 24 exact required names;
  `PROJECTS.md` and `docs/internal/TESTING.md` count drift.
- R01: [Avalonia themes](https://docs.avaloniaui.net/docs/styling/themes).
- R02: [Avalonia responsive layouts](https://docs.avaloniaui.net/docs/layout/responsive-layouts).
- R03: [Catppuccin palette](https://catppuccin.com/palette/).
- R04: [Avalonia accessibility](https://docs.avaloniaui.net/docs/app-development/accessibility).
- R05: W3C explanations of [text contrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html),
  [non-text contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html),
  [unobscured focus](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html),
  and [target size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html).

External sources accessed 2026-11-06. Only palette, standards and official
interface facts inform the handoff; no third-party application composition,
implementation, font, image, dependency or expressive asset is transferred.

Completed reviewed artifacts:
[plan](../../dev/active/avalonia-desktop-ui-ux-redesign/avalonia-desktop-ui-ux-redesign-plan.md),
[tasks](../../dev/active/avalonia-desktop-ui-ux-redesign/avalonia-desktop-ui-ux-redesign-tasks.md),
and [context](../../dev/active/avalonia-desktop-ui-ux-redesign/avalonia-desktop-ui-ux-redesign-context.md),
historically reviewed at desktop-ux-r1. This was a semantic reviewed-input revision; source
hashes identify the non-atomic working-tree intake separately.

## Missing Evidence

Implementation/measurement gaps above are assigned to the future desktop slices,
not assumed resolved. No scholarly, legal, stakeholder or operational validation
is represented by this planning report.

## Context Inventory

Stable evidence packet: desktop-ux-e1. Source is a modified shared working tree,
not a clean committed candidate. The plan's evidence section records owner hashes.
The earlier October 30 visual review is historical and does not describe every
November 6 implementation detail.

## Common Overlooked Failures And Outcomes

High contrast can be overwritten by an explicit appearance choice; root variants
can disappear from field-only navigation; correction targets can go stale after
row removal; collapsed controls can retain sensitive rendering; output edits can
revive stale readiness; a clean-looking success badge can conceal partial writes.
The desired outcomes are explicit contrast precedence, stable owned editors,
revision-checked correction, retired sensitive surfaces, generation-bound
readiness and truthful per-file receipts.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
| --- | --- | --- | --- | --- |
| 2026-11-06 | none | draft / ready-for-planning | Shared repository and official-source intake | desktop-ux-e1 |
| 2026-11-06 | draft / ready-for-planning | current / plan-aligned | Revalidated completed scenarios, tasks, state/authority limits and qualification boundaries | desktop-ux-r1 triad; all six pairs mapped below |
| 2026-11-06 | current / plan-aligned for r1; effectively stale for r2 | current / plan-aligned | Exact-input revalidation of CTO refinements | desktop-ux-r2 hashes and assessment below |

### Exact-input revalidation: desktop-ux-r2

Native `sha256sum` on 2026-11-06 identifies these reviewed files in
`dev/active/avalonia-desktop-ui-ux-redesign/`:

- `avalonia-desktop-ui-ux-redesign-plan.md`:
  `148b0091ea44d6eac49e3baa855851b2e0f1619440c9fa6203214b76289df0f2`
- `avalonia-desktop-ui-ux-redesign-tasks.md`:
  `ddf405d9f01f4538179a684475cd9996383b70c81e23f663ad4a5c33194b4fa7`

Verdict: **plan-aligned** for these exact inputs. All six accepted pairs and
their scenario/task mappings below remain intact. F001/M001 retains contrast
precedence, actual color measurement and scaling. F002/M002 gains explicit
off-page correction identity rechecks, preserved editor lifetime and observed
recursive mounting limits. F003/M003 retains metadata-only discovery, synthetic
canaries and value-safe announcements. F004/M004 gains invariant-first held
preparation tests through the real shell and changed save command, with separate
filesystem and physical-settlement oracles. F005/M005 retains existing owners,
typed bindings, UI-thread lifetime and no new dependency. F006/M006 retains
candidate attribution and strengthens host qualification ownership.

Plan Sections 3, 7.1, 7.2 and 9 and mapped task clauses support that verdict.
TASK-6.3 retains Linux X11 and real-session AT-SPI/screen-reader evidence,
Windows x64 UI Automation/speech, DPI/text/IME and Ctrl gestures, and macOS arm64
NSAccessibility/VoiceOver, Retina/text/IME and Command gestures. Linux arm64 is
conditional on advertised support; Wayland remains unqualified by X11.
Unavailable cohorts remain pending within TASK-6.3, never silently completed
from Linux captures. Participant usability and measured latency remain unproved.

This records design traceability only. No product, native, stakeholder,
scholarly or certification evidence was produced. Subsequent material changes
require revalidation; execution-status changes alone do not.

## Planning Handoff

- Workstream: avalonia-desktop-ui-ux-redesign
- Status: current
- Reviewed input: exact desktop-ux-r2 plan/tasks above; desktop-ux-e1 and E11-E13
- Findings and mitigations: IVSD-F001 -> IVSD-M001; IVSD-F002 -> IVSD-M002;
  IVSD-F003 -> IVSD-M003; IVSD-F004 -> IVSD-M004; IVSD-F005 -> IVSD-M005;
  IVSD-F006 -> IVSD-M006
- Required plan mappings: completed in plan Section 9 and indexed below
- Escalations required before: implementation for new custody/authority scope;
  release claims for actual host/accessibility qualification
- Refresh triggers: changed appearance precedence, persistent settings/answers,
  sensitive search/preview, navigation mutation, import/save authority, new
  dependency, accessibility defaults, evidence claims or mapped tasks

| Finding / mitigation | Scenario coverage | Implementation ownership | Revalidation result |
| --- | --- | --- | --- |
| IVSD-F001 / IVSD-M001 | S01, S02, S05, S12 | TASK-1.1, TASK-1.2, TASK-1.3, TASK-3.1, TASK-6.1, TASK-6.2, TASK-6.3 | Inclusive appearance and native qualification remain required |
| IVSD-F002 / IVSD-M002 | S03, S04, S06, S10 | TASK-2.1, TASK-2.2, TASK-2.3, TASK-3.2, TASK-4.1 | State anchors precede mounting and navigation changes |
| IVSD-F003 / IVSD-M003 | S07, S08, S12 | TASK-4.1, TASK-4.2, TASK-4.3, TASK-6.2 | No sensitive search/cache or copied answer sink introduced |
| IVSD-F004 / IVSD-M004 | S09, S10, S11 | TASK-5.1, TASK-5.2, TASK-5.3 | Save remains deliberate and current-generation; receipts retain physical facts |
| IVSD-F005 / IVSD-M005 | S01-S13 constraints | TASK-1.2, TASK-2.2, TASK-6.3 | Existing owners, source-free handoff and no new dependency retained |
| IVSD-F006 / IVSD-M006 | S13 | TASK-6.3 and each phase verification | New candidate evidence and honest missing-host/AT limits retained |

Findings remain accepted design constraints, not resolved implementation
findings. No changes-required or escalation-required planning branch remains.
Future implementation cannot close these concerns by updating the report date.
