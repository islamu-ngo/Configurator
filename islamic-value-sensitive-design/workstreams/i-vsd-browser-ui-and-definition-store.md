# Browser interface and optional definition store review

Last Updated: 2026-10-10

## Review Metadata

- Mode: planning
- Subject: Event-style browser interface and optional community definition store
- Workstream: browser-ui-and-definition-store
- Report kind: provider responsibility design review
- Report revision: 2
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-10-10
- Reviewed input: browser-ui-and-definition-store implementation working tree based on `7a244ee1af60`, user-approved optional `store/` design, revision 1 triad and recorded owner/package/published-browser evidence
- Supersedes: none

## Executive Summary

The interface should make configuration understandable while leaving users free to obtain definitions from local files, independent project repositories, or the community store. Store availability and membership must never determine whether the engine can process an otherwise admissible definition. Community entries are public definition metadata and versioned declarative files, never saved private answers.

The implementation keeps WebAssembly processing local, makes version and provenance limits visible, and provides a clear consent choice for secrets. Owner tests, real packages and bounded browser scenarios now support the technical mitigations described below. The current plan-aligned review preserves its provider-responsibility claims; it does not establish user satisfaction, operational moderation capacity, legal permission for future contributed assets, or religious certification.

## Scope

Browser navigation, appearance, grouped forms, validation and export guidance, public store contributions, source/version selection, static packaging, and privacy boundaries. The user chose an optional `store/` folder in Configurator initially. Content placement remains portable; creating or publishing a remote repository is outside the local implementation authorization.

## Claim Boundary

Evidence levels: design validation and traceability to inspected code/documentation and local rendered screenshots. No stakeholder research or operational audit has been performed. Community review and content hashes must not be represented as publisher authentication or target compatibility certification. No religious-legal ruling is requested or made.

## Findings

### IVSD-F001 Store independence protects operator control

Operators and independent publishers must be able to configure software without joining an ISLAMU catalogue. Requiring store membership, a working discovery service, or an account would contradict the user's portability requirement and expose private projects unnecessarily. Promise-Keeping and Rights of People apply to maintaining usable independent distribution and local import.

The provider controls startup dependencies, navigation defaults, packaging, and admission. **IVSD-M001:** load store discovery only when requested, support absent or invalid store assets, and use the same admission/validation rules for store, direct-link, and local-file sources. A package with no store must retain the complete configuration workflow.

- Lifecycle: accepted; severity: high; claim type: design recommendation.
- Domain: strategic and technical; stakeholders: operators, independent publishers, self-hosters.
- Evidence: E01 user request; E02 source/launch contracts; E03 strategy; validation level: design/implementation traceability.
- Owner/next validation: source and store implementation tasks; prove store-free startup, direct link, and local import independently.
- Escalation: changes making the store mandatory require renewed user and provider review.

### IVSD-F002 Versions and review labels must be truthful

A newer definition revision is not necessarily compatible with a newer application release. Publisher text, a family GUID, a repository URL, and acceptance of a PR do not authenticate a publisher or prove host acceptance. Operators can make unsafe configuration choices when these concepts are collapsed into a single “verified” badge.

Sidq and Avoiding Deception apply to the provider's labels and selection defaults. **IVSD-M002:** distinguish definition revision, canonical digest, declared application versions, content source, and documented review evidence. Explicitly select a version; retain that identity in the workspace; never silently upgrade it. Contribution checks reject conflicting content for an existing family/revision.

- Lifecycle: accepted; severity: high; claim type: design recommendation.
- Domain: design, technical, governance; stakeholders: operators, publishers, maintainers.
- Evidence: E01, E02, E04 registry history and source tests; validation level: design/implementation traceability.
- Owner/next validation: store version selection and source admission tasks; metadata/content mismatch and stale-source tests.
- Escalation: authenticated publisher or certified compatibility claims require separately established authority/evidence.

### IVSD-F003 Useful navigation must not expose or silently destroy work

Theme changes, section navigation, catalogue requests, and contributions must not carry answer values. Navigation must not surprise an operator by abandoning a partially completed workspace. A community PR that includes real default secrets would publicly disclose them even if the browser later masks the controls.

Amanah, Non-Harm, and Avoiding Spying apply to the data entrusted to the provider. **IVSD-M003:** keep answer/output state transient, retain existing secret consent and operation generations, block definition/store loading during live answer work, and ask explicitly before discarding a workspace. Contributions use public data-only definitions, synthetic fixtures, plain metadata, licensing review, and no executable hooks. No analytics or answer-bearing requests are added.

- Lifecycle: accepted; severity: high; claim type: design recommendation.
- Domain: technical, operational, governance; stakeholders: operators, contributors, reviewers.
- Evidence: E02 source/custody/offline contracts; E04 privacy/concurrency tests; validation level: design/implementation traceability.
- Owner/next validation: navigation/lifecycle and store validator tasks; late-response, revocation, cache/request canary checks.
- Escalation: persistence, authenticated private remote sources, or telemetry would require a new scope and review.

### IVSD-F004 Accessibility and understandable errors are part of configuration quality

Long ungrouped forms, technical field identifiers, and raw error codes burden newcomers and users relying on keyboards, narrow screens, or assistive technology. The provider controls those labels and interaction patterns. Ihsan and Adl apply to avoiding needless exclusion and making consequential settings intelligible.

**IVSD-M004:** reuse the Event-style accessible shell/theme conventions, render existing groups as navigable sections, retain validation of hidden active fields, provide linked correction summaries, and distinguish complete output from drafts. Verify labels, focus restoration, contrast, forced colors, reduced motion, RTL reflow, and mobile nested collections. A UI library alone is not accessibility proof.

- Lifecycle: accepted; severity: high; claim type: design recommendation.
- Domain: design and evaluation; stakeholders: novice operators and disabled users.
- Evidence: E05 screenshots/report; E06 Event first-party layout/theme/design sources; validation level: local rendering/design traceability.
- Owner/next validation: shell, form, and final rendered acceptance tasks.
- Escalation: unresolved accessibility failures remain delivery blockers; no certification claims from automated tests alone.

### IVSD-F005 Open contributions need consistent review and retained rights

Anyone may propose an entry through a PR, but inclusion cannot mean automatic execution, authenticity, or approval of third-party expression. The provider controls merge decisions, metadata claims, contribution instructions, and the rights retained in distributions.

Adl, Amanah, and Rights of People apply to consistent review and respect for contributor/rightsholder ownership. **IVSD-M005:** document objective admission, public-data, provenance, license, version, and fixture criteria; preserve terms; provide a documented correction/dispute route through the repository; sort discovery neutrally by name rather than invented popularity or sponsored ranking. Avoid storing personal information beyond deliberately public contribution metadata.

- Lifecycle: accepted; severity: medium; claim type: design recommendation.
- Domain: governance and operational; stakeholders: contributors, maintainers, rightsholders, operators.
- Evidence: E01 PR workflow request; E07 IP governance; validation level: design/implementation traceability.
- Owner/next validation: store authoring/validator and contribution documentation tasks.
- Escalation: uncertain ownership or license rights go to the Steward/qualified reviewer before inclusion.

## Recommendations

Implement the five mitigations as acceptance criteria. Rejected alternatives: mandatory hosted discovery (breaks independence), account-backed answers (changes custody), executable community templates (violates data-only admission), inferred verified badges (overstates evidence), and fabricated popularity scores (introduces unsupported ranking).

## Stakeholders

Operators using hosted or disconnected browsers; independent/private project publishers; public contributors; maintainers reviewing PRs; users with accessibility needs; self-hosters; third-party rightsholders. Maintainers are responsible for their own distribution decisions; publishers retain responsibility for target-specific semantics.

## I-VSD Principles And Domains

Applicable: Amanah, Sidq, Adl, Non-Harm, Rights of People, Promise-Keeping, Ihsan, Avoiding Deception, Avoiding Spying. All six domains are considered through portability, UX, custody, review/operations, contribution rules, and evaluation. Finance/riba, religious-content adjudication, AI profiling, and paid ranking are outside this workstream.

## Validation Gaps

The Browser owner suite passed 124/124 and Presentation 36/36. Store self-checks
passed 26/26; two real static packages passed four store inventory tests and two
Browser package closure rows, with seven compiled dependency closure rows.
Published-browser checks exercised optional-store failure, independent local and
HTTPS entry, offline reopening, narrow screens, keyboard focus, appearance,
canonical mismatch, consent revocation, clipboard export and synthetic cache
sentinels. MudBlazor's MIT terms and first-party transfer provenance are recorded,
and the native license policy passed for 86 package/version pairs.

No usability study, screen-reader audit or store moderation history was measured.
The isolated browser harness closes on downloads even for a standalone synthetic
Blob, so filename dispatch is observed but saved-file completion is unverified.
400% reflow uses the equivalent narrow CSS viewport, not a native browser zoom
control. These technical checks remain separate from stakeholder outcomes.

## Escalation Needed

No scholarly decision gates this software plan. Third-party licensing uncertainty requires Steward/qualified review before shipping. Authenticated trust labels, account/persistence features, monetization, or a mandatory store invalidate this assessment and require revalidation.

## Evidence Reviewed

- E01: user requests for UI parity, optional versioned community PR store, local files, and independent repository links; explicit choice of an optional `store/` folder in this repository initially.
- E02: `docs/public/browser.md`, `docs/internal/LAUNCH_CONTRACT.md`, `docs/internal/PRESENTATION_STATE.md`, `AGENTS.md` custody/source/identity constraints.
- E03: `dev/active/universal-configurator.md` executive verdict, optional catalogue, repository separation, and deep-link sections.
- E04: `BrowserDefinitionPointer`, `BrowserDefinitionSource`, `BrowserDefinitionRegistry`, `BrowserLaunchSelection`, `DefinitionFormModel`; owning Browser/Presentation test fixtures and assertions inspected.
- E05: `.tmp/ui-ux-audit/report.md` and 23 locally captured screenshots.
- E06: Event checkout `072f2b6db`, `Layout/MainLayout`, `NavMenu`, `Footer`, `ThemeQuickSwitcher`, `Components/Shell`, `AppearanceThemeService`, `AppearanceState`, design-system document.
- E07: Configurator IP governance, transfer provenance, public-data and testing rules.
- E08: completed [plan](../../dev/active/browser-ui-and-definition-store/browser-ui-and-definition-store-plan.md), [tasks](../../dev/active/browser-ui-and-definition-store/browser-ui-and-definition-store-tasks.md), and [context](../../dev/active/browser-ui-and-definition-store/browser-ui-and-definition-store-context.md), revision 1, revalidated against E01–E07. Findings remain accepted design obligations; none is marked implemented or resolved.
- E09: [Operations](../../docs/internal/OPERATIONS.md), final Browser/Presentation owner results, native store self-checks, actual package inventory and compiled closure checks, published localhost browser screenshots and synthetic custody scenarios. Local logs are under `.tmp/browser-ui-and-definition-store/` and `.omo/evidence/20261010-browser-ui-and-definition-store/`; evidence limits are stated above. Findings remain accepted with their existing owners because operational/user outcomes are not established.

## Missing Evidence

Operational governance outcomes, future hosting origin, evidence for future contributed project compatibility, stakeholder accessibility feedback and completed download verification in a supported interactive browser. Initial content location is settled by the user; no separate repository URL is needed for this plan.

## Context Inventory

The shared evidence packet covers the runtime/source paths, audit, user-owned strategy, version identity, tests, packaging, and first-party UI transfer. No Coolify source or third-party product assets were collected; the user's folder/PR analogy is a functional requirement.

## Common Overlooked Failures And Outcomes

An empty store must not prevent opening a file. A changed external file must not relabel an admitted revision. A Store link must not silently clear answers. Group navigation must not hide required corrections. A theme change must not serialize the workspace. A community definition must not inject HTML/scripts or request target access. A successful draft must not be presented as complete or target accepted.

## Planning Handoff

- Workstream: browser-ui-and-definition-store.
- Status: current; disposition: plan-aligned; revision: 2.
- Reviewed input: E01–E09, including the revision 1 triad and its delivered implementation evidence.
- Findings and mitigations: IVSD-F001 → IVSD-M001; F002 → M002; F003 → M003; F004 → M004; F005 → M005.
- Required plan mappings: the table below agrees with plan Section 9 and the named tasks. Each mitigation has an observable scenario and an implementation/verification owner.
- Escalations required before: uncertain third-party rights before merge/shipping; material scope changes before implementation.
- Refresh triggers: changed source authority, persistence/telemetry, mandatory store, ranking/monetization, review claims, accessibility/consent defaults, or mapped task removal.

| Finding / mitigation | Scenario | Task owner | Revalidation result |
| --- | --- | --- | --- |
| IVSD-F001 / IVSD-M001 | S02, S05, S09 | 2.1–2.3, 4.1 | Source-absent native publish, both actual packages, independent local/HTTPS opening and optional-403 workspace readiness observed in E09 |
| IVSD-F002 / IVSD-M002 | S03–S05 | 2.1–2.3 | Canonical metadata checks and conflict tests passed; explicit revision review and real digest mismatch observed; provenance labels preserve claim limits |
| IVSD-F003 / IVSD-M003 | S01, S06, S08–S09 | 1.2, 2.4, 3.3, 4.1–4.2 | Preference isolation, controlled generations and discard tests passed; actual revocation/cache/request sentinel checks observed; export completion limit retained |
| IVSD-F004 / IVSD-M004 | S01, S07–S09 | 1.1–1.3, 3.1–3.3, 4.2 | Navigation/privacy tests passed; row correction and recovery focus, mobile/RTL/themes and distinct draft/complete states observed; assistive-technology outcomes unmeasured |
| IVSD-F005 / IVSD-M005 | S03–S04 | 2.1, 2.2, 4.3 | Native admission self-checks, license policy, retained first-party/third-party terms and public contribution criteria reviewed; future rights/moderation outcomes remain owner responsibilities |

The completed triad preserves these responsibilities and has no unresolved provider-controlled choice that changes the task structure. Plan alignment does not grant implementation approval, prove the tests pass, or establish legal/religious certification.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence |
| --- | --- | --- | --- | --- |
| 2026-10-09 | none | draft | Planning intake | E01–E07 |
| 2026-10-09 | draft | current | User chose initial folder; completed revision 1 triad revalidated | E01–E08; plan-aligned |
| 2026-10-10 | current | current | Revalidated unchanged provider boundaries against implementation; optional worker coupling corrected and evidence limits retained | E09; report revision 2, plan-aligned |
