# Codebase Architecture and .NET Conventions — I-VSD Planning Assessment

Last Updated: 2026-11-02 Europe/Brussels

## Review Metadata

- **Mode:** planning revalidation with completed implementation evidence.
- **Subject / workstream:** `codebase-architecture-and-conventions`.
- **Report kind:** Provider-responsibility planning assessment.
- **Report revision:** `arch-conventions-ivsd-r3`.
- **Report status:** `closed`.
- **Disposition:** `plan-aligned`.
- **Evidence cutoff:** 2026-11-02.
- **Reviewed input:** `arch-conventions-r2` [plan](../../dev/active/codebase-architecture-and-conventions/codebase-architecture-and-conventions-plan.md) and [tasks](../../dev/active/codebase-architecture-and-conventions/codebase-architecture-and-conventions-tasks.md), with [context](../../dev/active/codebase-architecture-and-conventions/codebase-architecture-and-conventions-context.md). The original review used `main` at `4f130d11203bdda3da826a1c5de0fc8aa3911329`; this checkpoint reviews the current working tree and execution records below, with observed HEAD `11a097fae82075fca6a91e8f91d2b69dea56023f`. Neither Git object identifies uncommitted/untracked implementation content.
- **Supersedes:** The r2 assessment revision at this same path; stable finding/mitigation IDs, valid reasoning and lifecycle history retained below.
- **User direction:** Full implementation and required local package publication directly in the existing checkout are authorized. No commit, remote release or trust activation authority follows from this report; it provides no religious/legal certification.

## Executive Summary

The refined plan preserves the provider's responsibilities to protect local secrets, make configuration guidance understandable in English and Arabic, keep supported clients accessible, and report partial writes and qualification limits truthfully. All five findings and mitigations remain accepted and assigned to concrete r2 scenarios and implementation increments. This assessment is now current for r2; the stale r1 task mapping is replaced.

The revalidation changes several mechanisms that previously overstated safety. A universal kebab-case converter cannot preserve explicit schema/crypto tokens; a partial System.Threading.Lock migration can split synchronization; a copyable disposable buffer cannot establish exclusive ownership; and WebAssembly scoped services do not automatically end at navigation. The revised plan uses explicit wire maps, one mutation authority, existing disposable reference ownership and explicit editor lifetimes. It retains the CLI's pure Core/Artifacts boundary while sharing actual pure operations.

`plan-aligned` means the mitigations address the identified provider-controlled risks and retain execution owners. All seven product-owner suites plus the 83-case Architecture suite passed: 2,037 tests, with actual Linux native, published Browser and both Docker variants verified. This report is closed with all five findings accepted under continuing ownership and the limitations below. Closure is not complete accessibility, other-platform or religious certification.

## Scope

Covered decisions include bounded definition admission, secret ownership, consent/generation fencing, native protected output, machine diagnostics, en/ar/RTL guidance, keyboard/assistive access, Desktop themes, Terminal responsiveness, Browser interop/public caches, maintainability, build/probe truthfulness and evidence-backed package claims. All eight runtime owners and their test/engineering support are considered, including the current uncommitted schema-driven-secret work.

The review does not change cryptographic algorithms/cost ceilings, add telemetry or backend persistence, introduce target administration, activate signers or alter outbound licensing. It considers portability and breaking changes as pre-release engineering decisions while retaining accurate migration documentation and maintained fixture/schema consistency.

## Claim Boundary

This applies the repository's I-VSD framework to provider-mediated design choices. It is not a fatwa, Sharia certification, legal opinion, penetration test or evidence of ethical outcomes. The original review used static repository/graph inspection and official interface documentation (L2). This checkpoint also consumes physically executed compiler/behavioral evidence (L3), actual Linux Desktop/Terminal checks and an isolated Desktop instrumentation run (L4 for their exercised candidates). Published Browser, final native packages, assistive-technology and stakeholder qualification remain separate claims.

## Findings

### IVSD-F001 — Supported-language guidance and accessible presentation must remain understandable

Operators who depend on Arabic, RTL or assistive descriptions need the same meaningful secret, import and publication guidance as English operators. Fragmented resources and ambient-culture lookups can leave a workspace partly untranslated or change another workspace's presentation. Safety guidance is affected, not merely cosmetic consistency.

The provider controls resource completeness, explicit locale propagation, refresh notifications and rendered accessibility. r2 retains full en/ar localization, Desktop high contrast and Browser/Terminal keyboard/focus work. It distinguishes provider UI prose from machine codes and definition-authored content, so neither wire values nor user content is accidentally translated.

- **Lifecycle / severity / claim:** accepted / high / source-supported localization and accessibility risk; original numeric string counts are historical observations, not a complete current inventory.
- **Principles / domains:** Adl (fair access), Ihsan (quality), Rights of People; Design, Technical, Evaluation.
- **Stakeholders:** Arabic/RTL operators, keyboard/screen-reader/low-vision users, maintainers.
- **Provider decision:** Resource ownership, explicit locale, rendered direction/focus/automation and semantic themes.
- **Evidence / level:** Plan E07/E13–E17; DefinitionSecretValidation, DesktopActionText, SetupTerminalText, Browser import/inspector/diagnostic components, Desktop views; L2 static evidence only.
- **IVSD-M001:** Shared interactive Presentation resources plus surface catalogs, CLI invocation culture, en/ar fallback, reactive independent workspace locales, semantic accessibility text, light/dark/high-contrast themes and real rendered checks. Avoid exact-prose tests.
- **Owner / next validation:** S03/S04/S12/S14/S15; increments 4B, 5B, 6A, 7B–7C, 8A–8C completed within the recorded source/native/Browser scope. Client owners retain locale/accessibility regression duties; affected-user and assistive studies remain unqualified rather than implied.
- **Escalation:** Removing a supported locale or accessibility outcome changes scope and requires user direction plus revalidation. Arabic/assistive usability claims require affected-user evidence.

### IVSD-F002 — Secret and output ownership must survive domain typing and shared-operation refactors

A stale result adopted after revocation or into a shifted row can make a secret available outside the operator's intended workspace. Incorrect wire conversion can also reject legal definitions or accept unintended values. Partial native output is another distinct risk: a failed second write does not undo a successfully created sidecar.

r1 treated shared Presentation consumption, universal enum conversion and a new SensitiveBuffer abstraction as automatically safer. Revalidation rejects that inference. Core already has explicit wire mappings and SecretValue reference ownership; CLI comparison delegates to DefinitionImport, while only pure eligibility/traversal/snapshot operations need sharing. One accepted-state/generation owner must retain the final adoption check.

- **Lifecycle / severity / claim:** accepted / high / architecture and refactoring risk with source-verified ownership mechanisms; exploitability is not runtime-proven by this review.
- **Principles / domains:** Amanah (stewardship), La Darar (avoid harm), Sidq (accurate claims), Rights of People; Technical, Operational, Evaluation.
- **Stakeholders:** Operators, people whose credentials/configuration are prepared, downstream target users, maintainers.
- **Provider decision:** Admission maps, shared pure operations, immutable path identity, secret lifetime, interactive consent and factual protected-write outcomes.
- **Evidence / level:** Plan E05/E06/E08/E10/E18/E20/E21; DefinitionNode.JsonType, SecretKeyMaterial, SecretGenerator.SecretValue, DefinitionSecretOperation, DefinitionImportState, CliSecretHandlers/CliArtifactHandlers and protected writers; L2.
- **IVSD-M002:** Explicit legal token maps and invalid/numeric/default rejection; immutable structural path keys; Core reuse for pure snapshot operations; Presentation's single mutation authority with stable editor identity and generations; existing reference ownership/finally clearing; create-only output and accurate partial receipts. Do not claim managed-string/clipboard erasure or atomic multi-file rollback.
- **Owner / next validation:** S01/S02/S05/S06/S08/S09/S10; 2A–2D, 4A, 5A–5B and native adapter integration completed. Core/Presentation/Artifacts owners retain controlled stale-completion, buffer-lifetime, conflicting-write and sidecar regressions for future changes.
- **Escalation:** Any weakening of consent, sensitivity, bounded admission, create-only native output or target authority requires revalidation and an explicit decision; architectural cleanup provides no such permission.

### IVSD-F003 — Responsiveness and lifecycle cleanup must be proven at the real client boundary

Blocked terminal dispatch, inaccessible layouts and retained Browser editor state can prevent operators from completing or cancelling sensitive work. Disposable service registration alone does not prove cleanup: WebAssembly scoped services can survive navigation. Desktop generator conversion can also bypass existing disposal guards or omit notifications from immutable snapshot changes.

The provider must select lifecycle mechanisms that match the actual host. r2 requires responsive Terminal operations, semantic Desktop themes and guarded reactive bindings, and explicit Browser editor/callback ownership. Browser internal navigation dialogs and native unload protection remain distinct behaviors.

- **Lifecycle / severity / claim:** accepted / high / source-backed lifecycle/accessibility risk; an actual interop leak or assistive usability failure is not established solely by code shape.
- **Principles / domains:** La Darar, Adl, Ihsan; Design, Technical, Evaluation.
- **Stakeholders:** Terminal operators, Browser keyboard users, Desktop users with low vision or assistive tools.
- **Provider decision:** Dispatcher affinity, cancellation/drain, guarded notifications, theme tokens and explicit JS handle/subscription release.
- **Evidence / level:** Plan E11–E17; Terminal window/selection, Desktop FieldViewModel/StringFieldViewModel and App/views, Browser editor/field components; official Blazor lifetime and MVVM interface guidance in plan §2.3; L2.
- **IVSD-M003:** Deterministic responsiveness/cancellation tests, relative/scrollable terminal layout, behavior-preserving generator use, asynchronous request completion, accessible semantic themes, component/workspace-owned JS handles and disposal-initialization race tests. Cache only module code at app lifetime.
- **Owner / next validation:** S11–S16; 6B, 7A–7C, 8B–8C completed with headless/bUnit and actual native/published evidence. Client owners must preserve the recorded keyboard, focus, layout and lifecycle checks; other devices/assistive tools remain outside the qualification.
- **Escalation:** Dropped host/locale/accessibility support or changed consent/export behavior requires review; no such reduction is proposed.

### IVSD-F004 — Quality claims must reflect applicable checks and equivalent probe measurements

Maintainers rely on the meaning of a build or memory-probe pass. Text-anchor instrumentation can break during harmless restructuring, while changing observation points can make an apparently improved measurement incomparable. A passing analyzer build does not prove runtime privacy, accessibility or native support.

r1's claim that SDK analysis was absent or that every suggestion represented a suppressed defect was too broad. The checkout already enables Recommended SDK analysis and warnings-as-errors. r2 retains strict attributable-warning fixes while introducing applicable rules deliberately. Probes must remain test-only, retain the same observation definitions and not themselves keep secret data alive.

- **Lifecycle / severity / claim:** accepted / medium / verified measurement-maintenance risk; blanket moral claims about diagnostic configuration are withdrawn.
- **Principles / domains:** Sidq, Amanah, Ihsan; Governance, Technical, Evaluation.
- **Stakeholders:** Maintainers, contributors, package consumers and people relying on published privacy claims.
- **Provider decision:** Analyzer policy, build configuration, probe inclusion/lifetime and qualification reporting.
- **Evidence / level:** Plan E01/E02/E23; Directory.Build.props, global.json, both import probe targets and ownership/native probe drivers; L2.
- **IVSD-M004:** Pin applicable SDK analysis, enforce selected rules with fixes and no suppressions; typed conditional/internal seams before moves; equivalent raw-buffer/task/proposal/read/clear observations; normal-package exclusion; exact commands and measured/unmeasured distinctions. No mandatory 10.0-all or Sonar dependency without justification.
- **Owner / next validation:** S07; 1A–1B and ordinary-package verification at 8D completed. Engineering owners retain observation equivalence, release exclusion and honest candidate-specific reporting. Native probes continue to run through instrumented clients, not as standalone files.
- **Escalation:** A weakened invariant, hidden diagnostics or misleading package/measurement claim requires correction before qualification. Analyzer selection alone is not religious approval or disapproval.

### IVSD-F005 — Cross-owner cleanup must preserve offline boundaries and truthful execution ownership

Repository-wide naming, file moves and decomposition affect clients, generators, reflection allowlists, fixtures and packages. The most dangerous omission is a newly reachable authority or a stale generation check, not an imperfect folder name. A field-only Lock migration is a concrete example: form Monitor calls and object aliases could stop sharing the intended synchronization domain.

The current checkout includes active uncommitted implementation and must be analyzed as a whole. That does not justify destructive resets or broad commits. r2 preserves inward dependency rules, replaces mixed phase-wide changes with bounded increments, and requires literal caller-complete commit packets before their execution. It permits breaking changes while requiring coherent schema/example/doc migration rather than aliases or silent inconsistency.

- **Lifecycle / severity / claim:** accepted / high / source-supported architectural refactoring risk.
- **Principles / domains:** Amanah, Promise-Keeping, Rights of People, Sidq; Strategic, Technical, Operational, Governance.
- **Stakeholders:** All operators, maintainers, contributors and integrators consuming maintained definitions or packages.
- **Provider decision:** Assembly graph, shared mutation gate, ambient authority, generated-output ownership, migration documentation and qualification boundaries.
- **Evidence / level:** Plan E03/E04/E08/E19–E24; StandaloneArchitectureTests, CoreArchitectureTests, DefinitionFormModel/DefinitionFieldInput, current working-tree state and generator/project map; L2.
- **IVSD-M005:** Preserve CLI→Core/Artifacts and Browser→Core/Presentation, no native I/O in Presentation or answers in Host; keep one coherent mutation gate; include all current source/callers in migrations; preserve existing behavioral tests and add missing Red counterexamples; independently verify generators/packages/native hosts; freeze atomic literal commit paths before execution and retain approval boundaries.
- **Owner / next validation:** S01/S02/S05/S06/S08/S09/S10/S16; phases 2–5 and 8B–8D completed, including Browser retirement, package and container checks. Maintainers retain dependency/custody gates. Commit packets are conditional and N/A for this execution; a future authorized commit requires exact owned paths.
- **Escalation:** New runtime authority, answer-bearing transport, supported-outcome removal, or weakened boundary requires a new decision and revalidation. No tenant/HAL/outbox architecture is introduced.

## Implementation Evidence Checkpoint

The implementation retains the r2 provider boundaries and all five mitigation
IDs. Source, package and actual runtime evidence below close the approved scope;
candidate identities and unqualified surfaces prevent those results from being
presented as unrestricted product or accessibility certification.

The Browser's full 395-case pass was followed by a supported `GetState` input
correction and 14 passing shell cases, a repeated subset rather than extra tests.
The corrected normal Store/no-Store outputs contain 393/384 files; fresh Chrome
verified the Store output's boot, search, focus and absence of probe exports.
Architecture passed all 83 cases against current packages. Earlier broader
cache/offline observations retain their recorded candidate identities.

The no-Store Docker variant passed read-only, unprivileged HTTP/security checks
and actual Chrome boot. Its optional catalogue is intentionally absent; handled
missing-index resource errors are recorded rather than described as a clean
console. Built-in definitions remain available, custody starts disabled, full
globalization is present, probe exports are absent and browser storage is empty.
The Store-enabled image also passed the same hardened HTTP checks and fresh
Chrome boot: its two-project index returned HTTP 200/JSON, full ICU and native
WASM identity matched, probe exports were absent, storage was empty and the
page reported no console warning or error.

| Finding / mitigation | Implemented mechanism and reviewed evidence | Qualification limits and continuing ownership |
|---|---|---|
| IVSD-F001 / IVSD-M001 | Explicit workspace/invocation locale resources replace ambient guidance. Shared localization checks passed 5/5; final suites passed Presentation 307/307, CLI 270/270, Desktop 297/297 and Browser 395/395. Actual Linux Desktop checks passed 21/21 in each of English and Arabic. [Source and owner qualification](../../docs/internal/OPERATIONS.md#architecture-and-conventions-qualification-2026-11-02), [Desktop qualification](../../docs/internal/DESKTOP_CLIENT.md#architecture-and-conventions-qualification-2026-11-02). | Client owners retain locale and accessibility duties; no affected-user usability or other-platform study is implied. |
| IVSD-F002 / IVSD-M002 | Explicit enum maps, immutable path keys, shared pure Core operations and one Presentation mutation gate are implemented. Undefined-enum and map/ancestor-bound failures were reproduced before fixes. Final Core 469/469, Artifacts 66/66 and Presentation 307/307 passed. [Source and output qualification](../../docs/internal/OPERATIONS.md#architecture-and-conventions-qualification-2026-11-02). | Output owners retain the stated custody limits. Artifacts observes native buffer zeroing; SecretValue private character clearing is source-reviewed, while public copy/disposal behavior is tested. No string/clipboard erasure or multi-file rollback is claimed. |
| IVSD-F003 / IVSD-M003 | Typed Desktop selection/status, responsive Terminal preparation, explicit Browser module retirement and owner-cancelled dialogs are implemented. Browser late-module disposal had runtime Red before its fix; its final suite passed 395/395. Terminal passed 150/150 and actual en/ar 80×24 keyboard, resize, secret-clear and close checks. Desktop passed 297/297 plus all 21 native scenarios in each locale. [Browser evidence](../../docs/internal/OPERATIONS.md#architecture-and-conventions-qualification-2026-11-02), [Desktop qualification](../../docs/internal/DESKTOP_CLIENT.md#architecture-and-conventions-qualification-2026-11-02), [Terminal qualification](../../docs/internal/TERMINAL_CLIENT.md#architecture-and-convention-qualification-2026-11-02). | Client and engineering owners retain candidate-specific qualification. Native evidence is limited to the exercised Linux PTY/X11; it does not establish screen-reader or other-platform outcomes. |
| IVSD-F004 / IVSD-M004 | SDK analysis is pinned to 10.0 Recommended with build style enforcement. Final locked restore, format verification and solution build passed with zero warnings/errors. Conditional typed probe hooks replace text rewriting. Eight ordinary compiled owners excluded probe code, all 83 package/Architecture checks pass, and separate Desktop, Terminal and Browser candidates each completed twenty observed ownership/clear cycles. Browser's initial stale-element automation error remains separate from successful observed cycles. [Build and probe qualification](../../docs/internal/OPERATIONS.md#architecture-and-conventions-qualification-2026-11-02). | Client and engineering owners retain candidate-specific qualification. Diagnostic observations do not establish a release performance budget or a clean-console claim for the diagnostic Browser candidate. |
| IVSD-F005 / IVSD-M005 | Core/Artifacts and client names, feature folders, callers, reflection rules and tests migrated directly. Inward dependencies and the shared Monitor domain remain the design contract. Schemas, four maintained examples, command reference, ratchets and notices have recorded passes. Native and corrected normal Browser packages passed all 83 Architecture cases; actual client checks bind to their recorded identities. [Migration and package evidence](../../docs/internal/OPERATIONS.md#architecture-and-conventions-qualification-2026-11-02), [ownership ADR](../../docs/internal/adr/ADR-domain-vocabulary-and-workspace-ownership.md). | No remaining scoped gate; preserve dependency boundaries and candidate identity when qualifying future changes. |

All five findings remain **accepted**, with the same owners and escalation
boundaries. No mitigation has been removed or weakened, and no new target,
network, telemetry or publication authority is granted.

## Recommendations

Accept the scoped implementation and preserve its single mutation owner, explicit wire maps and custody boundaries in future changes. Retain real invariant failures and resulting passes, comparable probe observations, and actual client evidence. Reopen assessment if provider authority or a mapped mitigation changes; do not extend the recorded qualification to untested hosts or assistive outcomes.

Rejected alternatives, with reasons:

- Universal kebab-case conversion: it does not represent explicit schema/crypto tokens and can hide numeric/case admission differences.
- CLI→Presentation solely to eliminate apparent duplication: pure Core operations address demonstrated duplication without adopting interactive custody state.
- Blanket Lock/generator/DI conversion or strict class-length tests: syntax adoption is not evidence of safe synchronization, notifications or maintainability.
- Copyable disposable secret buffer and automatic “scoped means disposed” reasoning: both obscure actual resource lifetime.
- Omitting uncommitted source from review, or committing the entire dirty checkout: neither follows the user's architectural scope or preserves contribution ownership.
- Backward-compatibility aliases: unnecessary for the authorized pre-release posture; migrate maintained consumers directly instead.

## Stakeholders

| Stakeholder | Provider responsibility |
|---|---|
| English/Arabic operators | Equal access to safe, understandable guidance and factual write outcomes |
| Keyboard, screen-reader and low-vision users | Focus, names/help, RTL, contrast and usable resizing on supported surfaces |
| People protected by prepared secrets | Bounded custody, explicit consent, stale-result rejection and no answer-bearing network/cache |
| Maintainers/contributors | Inspectable ownership, applicable checks, preserved work and honest evidence |
| Definition authors/integrators | Consistent maintained schema/examples/CLI contracts and clear breaking migration |

## I-VSD Principles And Domains

Use the repository's [principles/domain definitions](../../.agents/skills/i-vsd/resources/principles-and-domains.md). Amanah supports custody and authority ownership; Sidq supports truthful probe/save/platform claims; Adl supports supported-language and assistive access; La Darar supports preventing stale disclosure/data loss; Rights of People supports consent and contribution preservation; Promise-Keeping supports maintained boundaries and transparent migration; Ihsan supports maintainability and evidence beyond surface conventions.

Strategic, Design, Technical, Operational, Governance and Evaluation domains are considered through those mechanisms. No payment/riba, moderation, ranking, AI decision or federation change is proposed. Avoiding Spying is served by retaining the no-telemetry/answer-bearing-transport boundary. No new business-model or scholarly conclusion follows from this refactor.

## Validation Gaps

All seven product-owner suites, Architecture, actual native/probe checks and both Docker variants provide evidence within their recorded identities. Real native file-dialog completion remains unqualified in the isolated Desktop environment, and no affected-user Arabic, screen-reader or high-contrast usability study is claimed. Other operating systems/RIDs and physical devices are unqualified. A later authorized commit or remote release still requires separate authority; local package preparation does not supply it.

## Escalation Needed

No religious-legal conclusion is requested or issued. No new scholarly question was identified by these software-boundary refinements. Reopen assessment for any consent/export/authority, supported locale/surface, telemetry, dependency/licensing or mitigation/task-map change. Product implementation and commit authority remain with the user; this current report does not grant either.

## Evidence Reviewed

- `arch-conventions-r2` plan §2.1, §3, §5, §7.1, §9 and §14.2; tasks phase increments and verification/commit boundaries; current context.
- Full current-checkout evidence packet at the HEAD above, including uncommitted secrets; specific source/graph observations are retained in plan §2.1 and context's retrieval ledger.
- Original r1 audit and assessment findings retained as provenance; changed technical interpretations are explained in F002–F005 rather than carried forward as verified defects.
- Official interface facts and dated URLs in plan §2.3; no third-party implementation expression is incorporated into this report or proposed code.
- Implementation execution records linked in the checkpoint above: final-source results for all seven product-owner suites; passing schema/example checks; runtime Red cases and fixes; actual Desktop/Terminal scenarios; isolated Desktop probe observations and ordinary binary exclusion. These records retain earlier failed builds and candidate identities rather than replacing them with later passes.

## Missing Evidence

- No approved implementation gate remains. Evidence is still absent for real native file-dialog completion in the isolated environment, other OS/RIDs and physical devices; none is inferred from current passes.
- Broad offline/cache observations remain bound to their earlier recorded candidates; fresh corrected-package Chrome checks establish the exercised delta, not a new exhaustive accessibility audit.
- Affected-user and assistive usability evidence beyond the executed Desktop en/ar and Terminal keyboard/layout scenarios.

## Context Inventory

Inspected through the shared review packet: contribution/project/governance/test contracts, exact triad, original assessment, bounded source and representative tests for each material ownership claim, graph with explicit repository root, and official interface documentation. No production environment, user secrets, telemetry, stakeholder interviews or sibling implementation is evidence for this assessment.

## Common Overlooked Failures And Outcomes

1. Generic enum serialization can accept numeric/case variants while changing non-kebab wire values; test admission semantics and canonical identity independently.
2. Splitting a large form into controllers can split synchronization; preserve one atomic adoption gate and logical editor identity.
3. Disposal clears only owned buffers; strings, clipboard copies and app-scoped callbacks need honest custody limits and explicit retirement.
4. Parser errors happen before CLI command validation; machine envelope safety needs the outer process boundary.
5. A failed second write can leave an established first output; receipts describe facts, not assumed rollback.
6. bUnit/headless passes do not prove actual native protection, PWA caches, keyboard/assistive behavior or Host container support.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
|---|---|---|---|---|
| 2026-11-02 | None | draft | Original repository architecture audit | r1 E01–E24 packet |
| 2026-11-02 | draft | current / plan-aligned | Original eight-phase plan mapped | arch-conventions-r1 and IVSD-F001–F005 |
| 2026-11-02 | current | stale | CTO revision changed dependencies, custody mechanisms and mappings | arch-conventions-r2; stale state recorded in triad |
| 2026-11-02 | stale | current / plan-aligned | User requested refresh; all five findings/mitigations re-evaluated and remapped | arch-conventions-ivsd-r2 against arch-conventions-r2; implementation remains unverified |
| 2026-11-02 | current / plan-aligned | current / plan-aligned | Authorized implementation retains r2 provider boundaries; execution evidence checkpoint reviewed | arch-conventions-ivsd-r3; four owner suites and isolated probe evidence recorded, remaining client/package qualification open |
| 2026-11-02 | current / plan-aligned | current / plan-aligned | Source/native and Browser ownership evidence advanced without changing provider boundaries | All seven owner suites, latest Browser shell tests, final source gate and all ownership probes recorded; final normal Browser/Host/Architecture closure remains open |
| 2026-11-02 | current / plan-aligned | closed / plan-aligned | Approved implementation and scoped package/runtime qualification completed | 2,037 tests, source/generated checks, three-client probes, native packages, normal Browser and both Docker variants; findings accepted with continuing owners and explicit limits |

## Planning Handoff

- **Workstream:** `codebase-architecture-and-conventions`.
- **Status / disposition:** `closed` / `plan-aligned`.
- **Reviewed input:** `arch-conventions-r2` plan/tasks and the implementation checkpoint; report `arch-conventions-ivsd-r3`.
- **Findings and mitigations:** IVSD-F001→IVSD-M001; IVSD-F002→IVSD-M002; IVSD-F003→IVSD-M003; IVSD-F004→IVSD-M004; IVSD-F005→IVSD-M005. All remain accepted, not resolved.
- **Required mappings:** F001/M001→S03/S04/S12/S14/S15 and 4B/5B/6A/7B–7C/8A–8C; F002/M002→S01/S02/S05/S06/S08–S10 and 2A–2D/4A/5A–5B; F003/M003→S11–S16 and 6B/7A–7C/8B–8C; F004/M004→S07 and 1A–1B; F005/M005→S01/S02/S05/S06/S08–S10/S16 and phases 2–5/8B–8D.
- **Escalations required before:** No approved implementation gate remains. Reopen for changed provider authority or mitigation scope; any later history operation or remote release needs separate authorization and exact owned-path closure.
- **Refresh triggers:** Changed provider authority, consent/retention/export, locales/accessibility/hosts, telemetry/network/cache policy, dependency/licensing, or any mapped mitigation/scenario/task outcome. Wording/status-only updates do not invalidate this revision.
