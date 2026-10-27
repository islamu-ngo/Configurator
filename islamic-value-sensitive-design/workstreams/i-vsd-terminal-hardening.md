# Terminal hardening provider responsibility review

Last Updated: 2026-10-14 Europe/Brussels

## Review Metadata

- Mode: plan-review
- Subject: Reliable private configuration editing and truthful native publication in the TUI
- Workstream: terminal-hardening
- Report kind: implementation planning assessment
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-10-14
- Reviewed input: Configurator `4758afa115f92ab0cfef5b79a9913864ce9090c5`, the working-tree Terminal CTO review and historical observations, and terminal-hardening plan/tasks/context **revision 2** plus `dev/backlog/terminal-hardening-follow-ups.md`, dated 2026-10-14. Revision-1 mappings became stale on scope split; affected findings were re-evaluated below before restoring plan-aligned status.
- Supersedes: none

## Executive Summary

Operators need to configure an admitted definition without navigation changing their answers, an import freezing the terminal, or cancellation obscuring files already created. The maintainer controls those interaction and recovery mechanisms. Private inputs and restricted output remain entrusted to a local utility, so usability changes must preserve bounded admission, masking, transient custody, generation fencing and create-only protected writes.

The recommended design retains the existing offline owners, makes container presence deliberate, settles asynchronous operations predictably, and preserves value-free historical publication receipts after cancellation. It also improves keyboard access, Arabic interaction and guidance without claiming unverified accessibility or host protection. No answer persistence, telemetry, clipboard relaxation, remote target apply or new trust authority is proposed.

The revised plan maps every mitigation either to four active correctness slices or to an independently owned follow-up. Bounded historical receipts and a two-second graceful-close budget preserve known facts while labelling unresolved in-flight work unknown; neither permits stale private output to return. Native PTY/event-loop/signal/restoration evidence is mandatory before a repaired terminal-workflow support claim. This current, plan-aligned assessment supports review of revision 2, not implementation approval, stakeholder validation, scholarly approval or product certification.

## Scope

Active scope: faithful typed/container editing, shared operation lifetime/private clearing, bounded asynchronous import/shutdown and historical protected publication accounting. Affected owners are Terminal, Presentation and Artifacts, with Browser/Desktop/CLI caller regressions. Follow-up scope: registry bounds, field guidance, viewport/resources, destination/culture, protected Unicode input, measured projections, CI and native/accessibility qualification. These remain open risks with named owners; moving them out of the active PRs does not implement their mitigations. Core changes require their own bounded re-baseline.

Existing browser consent, public caches, target application acceptance and operating-system protected-save support remain separate boundaries. The workstream must regression-check shared form behavior without silently changing browser custody or expanding supported-host claims.

## Claim Boundary

This is provider-responsibility design reasoning for an offline software utility. Normative principles guide tradeoffs; repository inspection and audit probes establish implementation traceability and local behavior only. No religious-legal, legal compliance, comprehensive security or assistive-technology certification is supplied.

## Findings

### IVSD-F001: Usability changes must preserve private answer custody

The operator supplies credentials and private organizational configuration to local controls. More convenient navigation, search, import and preferences must not move answers into clipboard history, command arguments, logs, preference files or public output. Clearing mutable editor buffers does not prove erasure of immutable managed strings.

The maintainer controls capture, retention, display and cleanup. Amanah means stewardship of the entrusted answers; Non-Harm and avoiding unjustified collection apply to unnecessary copies and persistence. Retaining the current strongest-root masking policy costs convenience for public siblings but provides a coherent safety floor. A leaf-preserving policy or protected paste capability is outside this workstream.

- Lifecycle / severity / claim: accepted / high / privacy design constraint
- Domains: technical, design, operations
- Stakeholders: operators and people represented in their configuration
- Controlled decision: answer retention, diagnostics, masking and private clear
- Evidence: E01, E02, E03; implementation traceability with local audit observations
- Mitigation: IVSD-M001 — preserve transient custody and conservative masking; batch private clear with one revision/invalidation; no private values in diagnostics or public projections
- Owner / next validation: Presentation and Terminal; clearing, stale-result and synthetic-canary scenarios
- Escalation: any persistence, clipboard relaxation, leaf-sensitivity demotion or answer-bearing transport requires fresh scope/trust review

### IVSD-F002: Cancellation must honestly account for irreversible publication

The publisher can commit one file before cancellation stops later files. The audit reproduced a remaining file with a discarded TUI receipt. An operator can then retry without understanding the existing target. Calling the operation cancelled must not suggest rollback or absence of filesystem effects.

Truthfulness and Promise-Keeping require an accurate account of each attempt. Rights of People and Non-Harm apply to keeping the operator in control of created files without automatic overwrite or deletion. A value-free historical receipt records what the writer established while generation fencing continues to reject stale answers and preparation.

- Lifecycle / severity / claim: accepted / high / recovery and honest-status constraint
- Domains: technical, operations, design
- Stakeholders: operators and downstream configuration consumers
- Controlled decision: receipt delivery, historical/current status and retry behavior
- Evidence: E01, E03, E04; source trace and controlled real-writer probe
- Mitigation: IVSD-M002 — preserve immutable per-attempt artifact/status receipts; distinguish pending, cancelled and settled outcomes; never resurrect private state or claim rollback
- Owner / next validation: Terminal and Artifacts; cancellation at each commit prefix and superseded receipt scenarios
- Escalation: deleting created files, insecure retry or accepting stale private output requires re-evaluation before implementation

Revision-2 revalidation: one physical publication drains before another starts; receipt history holds one active attempt and at most 16 settled attempts of at most 64 declared outcomes, with copied value-free collections. Evicting the oldest receipt does not delete files or imply they no longer exist. A two-second graceful-close timeout reports in-flight work unknown and detaches Views; tasks own observed cleanup while the process remains alive, but exit may prevent managed cleanup. Observer/cleanup faults cannot erase established Written facts. Guides must explain retention limits/manual recovery without promising persistence, crash recovery or timed filesystem completion. These accepted tradeoffs still need real boundary tests/native evidence.

### IVSD-F003: Browsing must not make hidden configuration decisions

The audit found absent optional objects materialized by blank-child navigation, required empty objects with no supply action, list append traps and choice cycling from an invisible old value. Operators cannot exercise meaningful choice when navigation changes semantics or legal values are unreachable.

Rights of People concerns intentional control of their configuration; Excellence concerns complete and predictable editing. The maintainer owns the editing representation and can use existing shared form operations rather than asking operators to understand raw JSON. Explicit presence and draft rules preserve the engine's distinction between omission and supplied containers.

- Lifecycle / severity / claim: accepted / high / configuration agency constraint
- Domains: design, technical
- Stakeholders: operators and definition authors
- Controlled decision: container presence, scalar/list drafts and error navigation
- Evidence: E01, E02; source and reproduced native .NET probe cases
- Mitigation: IVSD-M003 — explicit supply/clear/absence; harmless blank append abandonment; visible-draft choice semantics; actionable value-free diagnostics and stable field identity
- Owner / next validation: Presentation and Terminal; presence, navigation, bounds and activation scenarios
- Escalation: changed default/omission semantics requires scenario and stakeholder-impact review; backward-compatibility shims are not required in this development workstream

### IVSD-F004: Keyboard and language support claims need a usable complete workflow

An 80-by-26 terminal did not meet its advertised minimum. Active mnemonics collide, Arabic resource lookup can fall back to English, and assistive-technology support is not qualified. These failures distribute interaction burden unevenly and make capability claims unreliable.

Justice concerns access for keyboard, small-terminal and Arabic users. Excellence requires coherent navigation and error repair. Truthfulness requires separating resource availability and framework portability from actual rendering, assistive-technology and host qualification. Actual user feedback remains missing.

- Lifecycle / severity / claim: accepted / medium / inclusive interaction and support-claim constraint
- Domains: design, evaluation, operations
- Stakeholders: keyboard users, Arabic users, small-terminal users and users of assistive technology
- Controlled decision: dimensions, reflow, focus, mnemonics, help and stated support
- Evidence: E01, E05, E06; source plus exact-size native observation, not broad accessibility validation
- Mitigation: IVSD-M004 — a tested outer-size contract, unique shortcuts, useful focus, navigable help/errors and direct locale resource parity; retain explicit unqualified support labels
- Owner / next validation: Terminal and documentation owners; complete culture/dimension/keyboard scenarios
- Escalation: broad accessibility, RTL-shaping or non-Linux support claims require corresponding actual evidence

Revision-2 revalidation: viewport, mnemonic, language/resource and actual assistive work is separately owned in the persistent backlog. Existing barriers for Arabic, keyboard and small-terminal operators remain unresolved until those deliveries pass. Native workflow qualification is release-blocking for the relevant advertised workflow, not an optional exception. Splitting delivery improves reviewability but cannot justify broad support wording while known accessibility defects remain.

### IVSD-F005: Local discovery must remain bounded and independent

Registry selection reads and admits all candidate definitions before family resolution, with per-file/count limits but no practical aggregate budget. A large local directory can consume disproportionate work. Better discovery must preserve canonical conflict checks and must not introduce remote answer custody or target administration.

Non-Harm concerns avoidable resource exhaustion; Amanah and portability concern keeping preparation independent and inspectable. The maintainer controls local bounds and error semantics. Skipping unrelated malformed definitions would change the registry contract and cannot be disguised as a performance optimization.

- Lifecycle / severity / claim: accepted / medium / resource and authority constraint
- Domains: technical, strategic
- Stakeholders: operators, definition authors and maintainers
- Controlled decision: aggregate registry limits, admission and dependencies
- Evidence: E01, E07; source-confirmed bounds, unmeasured resource cost
- Mitigation: IVSD-M005 — a documented aggregate registry budget with fail-closed admission/conflict semantics; no new network/plugin authority or dependency without review
- Owner / next validation: Terminal and Core boundary tests; budget/conflict scenarios and workload measurements
- Escalation: changed source authority, ignored conflicts or package/licensing changes requires fresh admission/provenance review

### IVSD-F006: Passing test counts must not become unsupported quality claims

All current Terminal tests passed while actual terminal execution still exposed a freeze and incorrect size behavior. The harness bypasses the production event loop, and package inventory does not qualify interactive launch. Green counts therefore cannot justify comprehensive UX, privacy or host support claims.

Truthfulness governs the claim-evidence relationship. Excellence and Promise-Keeping govern the test effort and maintainable ownership needed to keep the utility usable. Maintainers control qualification scope, CI evidence retention and documentation. The testing-scope decision requested during planning must be reflected explicitly rather than treated as implied approval.

- Lifecycle / severity / claim: accepted / high / evidence and qualification constraint
- Domains: evaluation, governance, operations
- Stakeholders: operators, adopters and maintainers
- Controlled decision: deterministic regressions, explicitly deferred native qualification and evidence publication
- Evidence: E01, E06, E08; executed audit suite results and actual terminal failures
- Mitigation: IVSD-M006 — scenario-bound regressions, exact execution evidence, retained owner reports, explicit native/host qualification limits and knowledge graduation
- Owner / next validation: test/CI and Terminal maintainers; execute mapped deterministic tasks after implementation approval and obtain separate native qualification before broader release claims
- Escalation: native PTY/OS-signal/published-interactive qualification requires a bounded promoted implementation plan and actual evidence before relevant release claims. No release qualification is inferred from deterministic tests, package inventory or planning alignment.

Revision-2 revalidation: new tests execute to Green in their owning slice before commit, including shared form/workspace and Terminal receipt cases formerly postponed to later features. The verified check-then-invalidate source race is stated as a source finding, not a newly reproduced failure. Cancellation-source lifetime and queued callbacks get dedicated adversarial cases; no unexecuted current test counts are claimed. CI retention remains a separate open mitigation, with structured reports and canary checks required before upload changes.

### IVSD-F007: Output and display choices must be deliberate without hidden persistence

The destination is currently the launch working directory, and culture follows the environment without an owned selection route. Deliberate local destination and language choices can improve operator control, but a preference system must not silently retain private paths or answer state.

Rights of People applies to intentional custody transfer. Avoiding Deception applies to showing the selected outputs and available protection before commitment. Avoiding unjustified collection applies to keeping display choices session-local. The target application's authority and semantic acceptance stay with the target.

- Lifecycle / severity / claim: accepted / medium / operator choice and defaults constraint
- Domains: design, technical
- Stakeholders: operators and target application maintainers
- Controlled decision: destination, locale and output review
- Evidence: E01, E02, E05; verified current application/selection behavior
- Mitigation: IVSD-M007 — explicit local destination and en/ar language choice; bounded session-only settings; output/host review; no automatic overwrite, apply or preference persistence
- Owner / next validation: Terminal and guides; destination/culture/unsupported-host scenarios
- Escalation: persistent preferences containing paths, answers or artifacts requires new privacy scope

## Recommendations

Implement the active correctness portions of IVSD-M001/M002/M003/M006/M007 first; promote the remaining portions and IVSD-M004/M005 through the backlog's named independent slices. No finding is marked resolved by this split. Reject convenience through answer persistence, clipboard/history weakening or ordinary-file fallback. Retain conservative mixed-object masking and local registry validity semantics. Prefer bounded native interactions and admitted metadata over a plugin/preference framework.

Retain the pinned framework and repository owners; no new dependency/provenance risk is selected. Preserve generation fencing alongside independent bounded publication accounting. Rejected alternatives are accepting stale private results, unbounded/persisted receipt history, automatic rollback/delete/overwrite, premature source disposal, a speculative command/settings framework, and claiming full TUI quality from headless passes. The separate mandatory native qualification lane preserves the claim/evidence boundary without making current deterministic checks pretend to prove driver behavior.

## Stakeholders

Operators bear re-entry and recovery costs; people represented in answers bear disclosure risk; definition authors depend on faithful typed semantics; Arabic/keyboard/assistive-technology users bear interaction barriers; maintainers bear unclear state and test ownership; target application maintainers retain acceptance/apply authority.

## I-VSD Principles And Domains

Amanah, Truthfulness, Rights of People, Non-Harm, Justice, Excellence, Promise-Keeping and avoidance of deception/unjustified collection apply through the concrete consequences above. Financial/riba rules, moderation, content ranking, federation, advertising and scholarly religious-content judgments are not changed by this offline TUI workstream. No theological conclusion is needed to choose its technical mechanisms.

## Validation Gaps

No stakeholder interview, formal assistive-technology evaluation, complete Arabic rendering qualification, non-Linux native host proof, memory-forensic erasure guarantee or measured performance result is available. The original audit's controlled local probes do not provide published-package qualification.

## Escalation Needed

No unresolved provider-responsibility decision blocks review of the revised correctness design. Product implementation still requires user authorization. Native PTY/OS-signal/published-interactive qualification and deferred accessibility/resource fixes gate their relevant support claims. Escalate answer retention, clipboard policy changes, authority expansion, insecure fallback, cross-host promises or materially changed receipt/presence policy. No religious-legal ruling is requested.

## Evidence Reviewed

| ID | Evidence | Level and limit |
| --- | --- | --- |
| E01 | [Terminal CTO review](../../docs/internal/reports/TERMINAL_CTO_REVIEW.md), source revision and executed observations | Implementation traceability and local probes; not stakeholder/operational certification. |
| E02 | `DefinitionFormModel`, `SetupTerminalFieldEditor`, `SetupTerminalWindow`, `SetupTerminalApplication` | Shared custody, presence, draft and local interaction source. |
| E03 | `SetupPresentationWorkspace`, `SetupTerminalArtifactOperation` | Generation, fault and cleanup source plus audit probes. |
| E04 | `ProtectedArtifactBundlePublisher`, `ProtectedArtifactWriter`, `UnixProtectedArtifactWriter` | Native protected/create-only and partial-prefix contract; Linux audit proof only. |
| E05 | `docs/public/terminal.md`, `docs/public/native-output.md`, `docs/internal/TERMINAL_CLIENT.md`, `PRESENTATION_STATE.md`, `PACKAGING.md` | Current public/technical promises and identified inconsistencies. |
| E06 | Terminal test classes, `StandalonePackageTests`, CI workflow | Current tests and evidence retention; no claim of measured coverage. |
| E07 | `SetupTerminalDefinitionSelection`, `SetupDefinitionRegistry`, `SetupCompositionLimits` | Per-file/count source bounds and canonical conflict checks. |
| E08 | Audit execution: 38 Terminal, 36 Presentation, 26 Artifacts passed; native dimension/import observations | Historical evidence at the same unchanged product revision, not newly executed planning tests. |
| E09 | Revision-2 triad and `dev/backlog/terminal-hardening-follow-ups.md`; bounded source/graph review at the same HEAD | Four active correctness slices; stable scenario IDs; atomic settlement, owned source retirement, two-second close, 16-settled-attempt history and same-slice test gates. Design/source traceability only. |

## Missing Evidence

Implemented mitigations, regression/caller results, independent revision-2 implementation review, actual published-terminal qualification and user/assistive feedback. Current status means mappings and provider tradeoffs match the revised design; deferred work remains unresolved and native evidence is still required.

## Context Inventory

One shared evidence packet from the completed audit and bounded planning follow-up is reused. The checkout remains on `main`; fetched `origin/main` matches HEAD. Existing review/foundation changes and user-owned strategy files remain preserved. No overlapping terminal-hardening triad was found.

## Common Overlooked Failures And Outcomes

- Clearing a pending draft must not restore an older valid answer on retry.
- Historical publication receipts must not re-enable save or revive private bytes.
- Deletion and harmless browsing must not accidentally supply absent containers.
- Search, diagnostic or preference state must not become a second answer store.
- Locale fallback and mocked unsupported-host behavior must not be counted as native proof.
- A late signal/result must not mutate disposed controls or turn cancellation into claimed rollback.

## Planning Handoff

- Workstream: terminal-hardening
- Status: current
- Reviewed input: unchanged product revision, Terminal CTO review and terminal-hardening triad revision 2 plus persistent follow-up backlog, dated 2026-10-14
- Findings/mitigations: IVSD-F001 → IVSD-M001 through IVSD-F007 → IVSD-M007
- Required mappings: verified in plan Section 9 — F001/M001→active S08–S09/S11–S13/S35–S37 and 2.1–2.3/3.1–3.3, deferred S18/S28/S30 guidance/options/input backlog rows; F002/M002→active S14–S17/S38 and 4.1–4.3; F003/M003→active S01–S06 and 1.1–1.3, deferred S18–S21 guidance row; F004/M004→deferred S22–S25/S28 viewport/resources/options/native-accessibility rows; F005/M005→deferred S29/S31 registry/measured-projection rows; F006/M006→active S07/S10/S14/S35–S38 owning Red/exit tasks, deferred S32–S33 CI/native rows, S34 durable backlog/Phase-4 knowledge packet; F007/M007→active S16–S17/4.3 and deferred S26–S28 destination/culture row.
- Escalations required before: implementation authorization; actual native evidence before relevant release support claims; fresh review for changed custody/authority or material scope. No additional user decision was required to finish this review.
- Refresh triggers: answer retention, display/clipboard/sensitivity policy, publication authority/accounting, support claims, destination preferences or qualification scope changes

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
| --- | --- | --- | --- | --- |
| 2026-10-14 | None | Draft / ready-for-planning | Integrated implementation-plan request and reused audit packet | E01–E08; task mapping pending. |
| 2026-10-14 | Draft / ready-for-planning | Current / plan-aligned | Completed nine-phase/34-scenario triad and mapping review; applied explicit deterministic scope with native release-claim follow-up | Plan Section 9 and exact tasks; no implementation or operational proof claimed. |
| 2026-10-14 | Current / plan-aligned (revision 1) | Stale / changes-required | CTO split changes mitigation/task ownership and adds bounded close/history/source-lifetime decisions | Revision-2 triad and backlog; prior approval/alignment does not transfer automatically. |
| 2026-10-14 | Stale / changes-required | Current / plan-aligned (revision 2) | Re-evaluated F001–F007 against active/deferred ownership, retention/close tradeoffs and mandatory native support gate | E09 and revised Planning Handoff/plan Section 9; all findings remain accepted/pending implementation. |
