# Browser Production V1 - Planning Assessment

Last Updated: 2026-10-19

## Review Metadata

- Mode: planning
- Subject: ISLAMU Configurator anonymous public static browser
- Workstream: browser-production-v1
- Report kind: implementation-planning assessment
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-10-19
- Reviewed input: product commit `37bfa9b492f54f0e746150edc9a624ad668774b3`, revision-2 plan/tasks, context, required V1-V7 backlog and unchanged production-readiness consultation; exact working-file hashes below
- Supersedes: none; this assessment carries the consultation findings into an implementation workstream
- Planning artifact revision: revision 2 independently revalidated on 2026-10-19; revision-1 hashes and mapping retained as historical evidence
- CTO review: Applied & Aligned (2026-10-19); Split Applied for triad revision 2; this assessment independently validates its provider-responsibility mappings, not technical execution
- User approval: explicit implementation authorization in the current checkout, not a worktree; no commits. This revalidation edits only this report; triad/product edits, publication and deployment are outside this task

## Executive Summary

The plan must make local configuration preparation correct, consent effective, controls visible and the public delivery process supportable. It must not equate passing unit tests with readiness to receive operator secrets on a publicly hosted origin.

The October 19 audit supplies seventeen stable finding/mitigation pairs. They remain open, even when a plan maps them to work. The latest user instruction explicitly authorizes implementation in the current checkout without commits; this bounded independent review changes only the assessment. Authorization does not establish that implementation, deployment or risk resolution has occurred.

The user has confirmed that published-browser, privacy, accessibility, update and rollback qualification must be included before release, overriding the planning resource's generic non-browser-only restriction. The user then selected their existing **Coolify installation** and requested a **Dockerfile/image**. The plan therefore packages the static app in an unprivileged image, while Coolify retains domain, TLS, proxy and deployment responsibility. No separate host provisioning or deployment-control service is introduced.

The revision-1 triad supplied fifteen requirements, observable S01-S28 scenarios, ten implementation phases, twenty-five implementation tasks and explicit candidate/operating gates. Its mapping table and hashes below remain historical. The October 19 CTO split moves answer integrity and privacy into two active phases with six substantive tasks, retaining every other mandatory launch outcome in V1-V7. Independent revalidation of the exact revision-2 plan/tasks and backlog now supports **current / plan-aligned**: all seventeen pairs retain scenarios, responsible owners and implementation or activation gates. No finding is resolved, waived, accepted as residual risk or declared non-applicable by this disposition.

The split preserves the provider's duties while making the immediate correction reviewable. S29 requires old/faulting export cleanup not to damage replacement output; S30 distinguishes genuine edit intent from refresh/navigation; S31 preserves admitted empty enum choices; S32 retires every detached artifact before any cleanup await. Required controlled regressions and native caller checks address these boundaries in the active slice. Real browser custody, accessibility, image/worker recovery, actual-origin and human/operating acceptance remain mandatory later evidence. Plan alignment removes the stale moral-mapping gate only; it is neither runtime closure nor certification or release approval.

## Scope

The workstream prepares an anonymous public static WebAssembly v1 over the existing Core and Presentation boundaries. All seventeen audit findings are intake inputs. Local-only transient answers, per-workspace secret consent, optional independent definition sources, deliberate restricted exports and target-owned acceptance remain constraints.

No accounts, backend answer processing, answer persistence, telemetry, target authentication, remote apply, mandatory central store, compatibility aliases or inherited Event publication authority are introduced by this assessment.

The shared packet includes current contracts, verified source flows, test owners, package/CI evidence and the exact prior local reproduction identities. Product HEAD still matches freshly fetched `origin/main`; planning has not switched branches or pulled changes into the checkout.

## Claim Boundary

This assessment provides I-VSD design reasoning and implementation traceability. It does not establish stakeholder, deployed-system, security-certification, accessibility-conformance or religious-legal outcomes.

The audit's runtime reproductions, source-supported races and missing operational evidence retain their original evidence levels. No product tests have been rerun merely to author this plan. No absent record is silently converted into a claim that a control does not exist outside the reviewed repository.

## Findings

The identifiers are imported from the [production-readiness consultation](../consultations/i-vsd-browser-production-readiness-detailed-audit.md), not reassigned. Its expanded evidence and causal analysis remain foundational inputs. Every finding below is open and has a proposed mitigation, not an implemented resolution.

### IVSD-F001: Consent denial must not be cancelled by an ordinary edit

An edit during awaited export cleanup can abandon consent revocation before secrets are denied. Users must be able to withdraw secret entry independently of unrelated work.

- Traceability: high; source-supported privacy defect; Trust/Rights/Non-Harm; technical/design; secret-entering operators; provider controls consent ordering; audit E03.
- IVSD-M001: synchronous denial and recursive secret clearing before awaited cleanup; owner Browser/Presentation maintainers; next validation is a controlled interleaving regression; privacy release review required.

### IVSD-F002: Invalid edit intent must revoke old export authority

An invalid nested draft can change the visible form without changing the typed snapshot, leaving old output or a pending import eligible. The provider must invalidate on meaningful edit intent, not only accepted answer mutations.

- Traceability: high; source-supported stale-output defect; Trust/Truthfulness/Non-Harm; technical/design; operators; provider controls edit/output fencing; audit E03/E04.
- IVSD-M002: fence exports and pending imports on invalid/rejected edits; owner Browser maintainer; next validation is an invalid-edit and gated-import regression; privacy review required.

### IVSD-F003: Successful imports must preserve explicit empty values

The published app reported successful import but omitted an explicit optional empty string from prepared JSON. That changes the user's configuration decision without notice.

- Traceability: high; reproduced fidelity defect; Truthfulness/Trust/Non-Harm; technical/design; operators and target maintainers; provider controls typed presence; audit E04/E08.
- IVSD-M003: distinguish typed presence from raw edit text across import, editing and composition; owner Presentation with all direct callers; next validation is independent round-trip vectors; target acceptance remains separate.

### IVSD-F004: Optional containers need a reachable omission decision

Supplying an optional container must not force users to keep it, complete unwanted required children or clear unrelated answers. Explicit emptiness must remain distinguishable from absence.

- Traceability: medium; reproduced correction dead end; Rights/Excellence/Non-Harm; design/technical; operators; provider controls omission affordances; audit E04/E08.
- IVSD-M004: explicit container omission with preserved unrelated state and output invalidation; owner Browser/Presentation; next validation covers object/list presence and omission; no scholarly escalation.

### IVSD-F005: Primary actions must remain visibly readable

The current Home filled link had identical text and background colors in settled light and dark modes. Accessible DOM text alone did not make that action usable.

- Traceability: high; reproduced contrast defect; Justice/Excellence/Non-Harm; design/evaluation; all users, especially low-vision users; provider controls CSS layers/theme; audit E05/E08.
- IVSD-M005: owning-layer foreground treatment and rendered variant/state checks; owner UI maintainer; next validation measures current rendered contrast; accessibility acceptance required.

### IVSD-F006: Correction must identify the failed location and rule

Generic root-level constraint errors can leave an operator unable to correct nested fields without inspecting JSON. Value-free diagnostics should still expose safe field paths and applicable rules.

- Traceability: medium; reproduced generic error and source-supported location loss; Excellence/Justice/Truthfulness; design/technical; first-time and assistive-technology users; provider controls diagnostic structure; audit E04/E05/E08.
- IVSD-M006: structured paths/rules and exact correction focus; owner Core/Browser; next validation corrects representative nested/bounded values without secret echo; accessibility review required.

### IVSD-F007: Preparation recovery must perform a fresh readiness decision

Memoized failed initialization makes the visible Start retry ineffective after a transient worker problem. Recovery must remain fail-closed without becoming permanently stuck.

- Traceability: medium; source-supported recovery defect; Trust/Excellence/Non-Harm; technical/design/operational; restricted-network operators; provider controls readiness lifecycle; audit E03/E05.
- IVSD-M007: explicit clean-state retry or truthful supported reload recovery; owner offline maintainer; next validation restores readiness after initial failure; no authority bypass.

### IVSD-F008: Delivered dependencies require current servicing review

The reviewed runtime/package pins trail the identified current .NET 10 servicing release. A clean NuGet report does not prove every shipped runtime advisory is covered.

- Traceability: high release gate; verified version gap, not asserted exploitability; Trust/Non-Harm/Excellence; technical/operational; public users; provider controls delivered runtime; audit E01/E02/E07/X01/X02.
- IVSD-M008: approved serviced toolchain/runtime, native lock generation and exact closure/advisory review; owner build/release; next validation binds updated versions to packages and notices; security disposition required where applicable.

### IVSD-F009: The actual host must enforce delivery policy

Local HTTP preview and meta CSP do not qualify public TLS, framing protection, cache policy, MIME types, compression or selective fallback.

- Traceability: high; missing operational evidence; Trust/Non-Harm; technical/operational; public users; provider controls hosting policy; audit E05/E06/E08.
- IVSD-M009: versioned target-host configuration and actual response/boot evidence; owner hosting lead; next validation depends on the selected hosting target; release approval required.

### IVSD-F010: Promotion and installed-client recovery require real ownership

An immutable package is useful only if approved bytes reach the public origin and failed updates/rollbacks can be handled without replacing live private work.

- Traceability: high; missing release-operating evidence; Trust/Promise-Keeping/Non-Harm; operational/governance/technical; active/offline operators; provider controls promotion and recovery; audit E02/E05/E06.
- IVSD-M010: retained candidate identity, exact-byte promotion and worker-aware update/rollback rehearsal; owner release/hosting; next validation is exact-candidate qualification; independently authorized publication remains mandatory.

### IVSD-F011: Browser qualification must be release-bound and repeatable

The current visual defect escaped owner tests. Source, package and component evidence must not be substituted for browser, export, custody and assistive-technology outcomes.

- Traceability: high; assurance gap with observed escaped defect; Excellence/Justice/Truthfulness; evaluation/design/operational; supported users; provider controls release qualification; audit E02/E06/E07/E08.
- IVSD-M011: focused published-app automation plus human accessibility acceptance and bounded support claims; owner Browser QA; user explicitly approved this qualification scope; next validation must bind the exact candidate.

### IVSD-F012: Public-service commitments need sustainable responders

An undefined acknowledgement period does not establish effective vulnerability response. Repository ownership and a CI definition do not prove an operating mailbox, protected promotion or emergency recovery capacity.

- Traceability: high; policy defect/missing evidence; Trust/Promise-Keeping/Non-Harm/Avoiding Spying; operational/governance; users/reporters/volunteers; provider controls staffing and promises; audit E02/E06.
- IVSD-M012: primary/backup roles, achievable response targets, recurring advisory review, access controls and answer-free incident/availability practice; owner steward/operations; next validation is an owned rehearsal; jurisdiction-specific obligations need qualified advice.

### IVSD-F013: Secret entry needs an informed hosted-code trust choice

People arriving through direct launch links may not read the repository FAQ. Masking and local processing do not remove trust in delivered code, the origin, browser or extensions.

- Traceability: medium; communication gap, not measured comprehension; Truthfulness/Rights/Trust/Avoiding Deception; design/strategic/evaluation; first-time operators; provider controls consent explanations; audit E01/E03/E05.
- IVSD-M013: concise in-app trust guidance and deferred-secret/native alternatives beside consent; owner product/UI; next validation checks comprehension and accessible presentation; no certification claim.

### IVSD-F014: Import limitations and conflicts must be actionable

Unsupported inverses are legitimate, but discovering them only after file selection and receiving unnamed conflict instructions creates avoidable trial and error.

- Traceability: medium; source-supported limitation; Truthfulness/Excellence/Rights; design/technical; returning operators; provider controls import assessment; audit E04/E05.
- IVSD-M014: pre-selection eligibility and safe conflicting paths with atomic rejection; owner Core/Browser; next validation covers unsupported, conflicting and secret-bearing inputs; no universal inverse requirement.

### IVSD-F015: Destructive clearing must be deliberate

Current Clear immediately destroys transient work while route departure is protected. No hidden persistence or secret undo buffer should be introduced to repair this interaction.

- Traceability: medium; observed behavior/source-supported missing confirmation; Rights/Non-Harm/Excellence; design; operators; provider controls discard confirmation; audit E03/E05/E08.
- IVSD-M015: confirmation for populated work, cancellation preservation and useful post-clear focus; owner UI; next validation covers confirm/cancel and output custody; no additional retention authority.

### IVSD-F016: Public-cache limits and connection changes need recovery

An eight-definition cache with no selective removal and generic failure messages becomes difficult to maintain as revisions change. Current-state refresh must not imply network permission during answer custody.

- Traceability: medium; source-supported limitation; Promise-Keeping/Excellence/Rights; technical/design/operational; offline operators; provider controls public-cache management; audit E03/E05.
- IVSD-M016: bounded selective public-definition removal, specific status and connection/readiness refresh; owner offline maintainer; next validation covers capacity, replacement and reconnect; answers remain excluded.

### IVSD-F017: Supported limits and language claims require evidence

Finite definition bounds alone do not prove acceptable startup, input or composition behavior on supported devices. RTL direction does not establish a translated interface.

- Traceability: medium; performance/stakeholder evidence gap; Excellence/Justice/Truthfulness; evaluation/design/strategic; mobile/low-resource/RTL users; provider controls support claims; audit E01/E03/E06/E08.
- IVSD-M017: measured worst-case supported workflows and explicit device/browser/language scope; owner performance/product; next validation establishes budgets and first-time completion evidence; accessibility/local-language expertise where claimed.

## Recommendations

Retain the existing client-to-Presentation/Core direction and solve the defects in their owning modules. Plan invariant-first regressions for privacy races and typed presence. Ordinary presentation changes need risk-proportionate behavioral/rendered evidence rather than blanket test scaffolding.

Reject backend custody, answer telemetry, persistence, mandatory Store dependence, compatibility shims and warning-only substitutions for enabled faulty privacy behavior. No additional design pattern is justified merely by an enterprise-quality label.

The user-approved launch qualification is at the late image-packaging/release boundary, not every inner-loop task. Coolify hosting is resolved; actual operator account/hostname/personnel inputs remain named activation gates, not assumed evidence.

The Dockerfile must use the existing public publication owner, preserve notices and manifest integrity, exclude private/build-host material and contain no application answer service. Its unprivileged static listener on port 8080 is a concrete proposed contract, not a claim that a container has been built. Health means listener/essential-file availability; it does not certify browser processing or offline readiness.

Test-only browser automation is isolated in a new native engineering owner, with exact dependency/terms review and named per-engine checks. This is a proportionate response to the escaped platform defects, not a new product runtime dependency.

Revision 2 supersedes the original implementation sequence, not the audit obligations. Execute only the active answer/privacy slice under its authorization; V1-V7 must each be re-baselined into their own approved, I-VSD-aligned workstream before execution. Backlog heading-level dependencies and additional CTO constraints take precedence over retained original phase dependencies, dated version candidates, count-only qualification arguments and distant native-check timing. V3's independent contrast correction need not wait for V2 diagnostics. Native changed-editor verification belongs at the active slice exit; final V7 platform acceptance remains separate.

No substantive requirement conflict was found. The triad's awaiting-approval/stale wording records the earlier CTO handoff and is superseded for this review by the latest user authorization and this bound assessment. Its future commit commands are not executable authority: the explicit no-commits instruction controls. Parent metadata may subsequently record this result without implying product work. Do not turn a material behavior, owner, mitigation or activation-gate change into a metadata-only update.

## Stakeholders

Operators, people represented by configuration metadata, definition publishers, target maintainers, keyboard/assistive-technology users, low-resource/offline users and volunteer maintainers are affected. ISLAMU controls delivered code, hosted origin, default consent, export capabilities, public caching, promotion and maintenance promises. Target applications retain semantic acceptance and apply authority; users retain custody after deliberate export.

## I-VSD Principles And Domains

Trust applies to code delivery, privacy decisions and operation promises. Truthfulness applies to successful imports, complete/draft status and qualification claims. Rights and Non-Harm apply to revocation, omission, correction and destructive clearing. Justice and Excellence require usable controls and supported-device evidence. Avoiding Spying constrains both app telemetry and separately governed host logs.

All six domains are relevant: strategic portability, design decisions, technical invariants, operational recovery, governance authority and evaluation evidence. No browser finance, AI inference or moderation capability is planned. Organizational funding/contracts and religious-legal matters are not evaluated by inference.

## Common Overlooked Failures And Outcomes

An image can be healthy while its WASM boot, CSP, stored worker or download is broken. A Coolify rebuild does not inherit qualification for different bytes. Origin rollback cannot recall disconnected code. Empty values and absent answers must remain distinct in native callers as well as the browser. Invalid drafts must fence imports even when typed answers do not change. Neither rendered masking nor planning alignment proves effective secret withdrawal.

The intended outcome is a deliberate, correct operator workflow delivered through a small supportable image. The plan makes each risk testable or an explicit activation gate; it does not establish that the outcome has occurred.

## Validation Gaps

Future implementation tests, actual image publication, published-browser qualification, stakeholder acceptance and live operations have not occurred in this revalidation. Revision-1 planning validation checked all seventeen mapping pairs, ten phases and twenty-five implementation tasks; that remains historical evidence, not the active sequence. The independent revision-2 review compares all seventeen pairs against plan Section 9, active Phase Red/tasks 1.1-1.3 and 2.1-2.3, slice-exit checks, and every V1-V7 outcome and retained task packet. Its mapping dispositions and exact identities are recorded below; it does not run the planned product checks.

Evidence level is design validation and plan/task traceability only. The source-supported privacy interleavings still require observed Red assertions followed by passing implementation evidence. S29-S32 strengthen proposed acceptance without retroactively becoming reproduced defects. Framework boundary fakes cannot prove real Blob/clipboard/download behavior. V7 must reject any missing, duplicated, unknown, failed, wrong-candidate or unreported Q01-Q16 result for each selected engine and candidate; a count of sixteen, a healthy container or historical owner-test totals cannot substitute for that proof.

The earlier audit's exact executed checks remain prior-session baseline evidence for the same commit, not new planning execution. Planning has not rerun product tests.

## Escalation Needed

Before any public promotion, the steward must assign actual Coolify/registry authority, responsible people, an achievable response window and credentials through authorized channels. Human accessibility, actual-origin and security acceptance remain release duties. These activation inputs do not authorize an executor to invent accounts, publish an image or deploy.

No religious-legal ruling is required for the proposed engineering corrections. Future finance, contracts, sponsorship or religious certification claims require qualified Sunni scholarly authority and appropriate specialist evidence.

## Evidence Reviewed

- [Current production-readiness consultation](../consultations/i-vsd-browser-production-readiness-detailed-audit.md), with its original E01-E09/X01-X06 registers and exact package/test evidence.
- Current `main` and fetched `origin/main` match commit `37bfa9b492f54f0e746150edc9a624ad668774b3`.
- Planning, I-VSD integration, intent registry, contribution/commit contracts and shared Presentation state contract.
- Shared-model consumer inventory across Presentation, Browser, Desktop and Terminal; no direct CLI consumer of the inspected form API was found.
- Context7 bUnit controlled asynchronous JS interop and Microsoft standalone deployment documentation; Anysearch official W3C/Microsoft source discovery.
- Context7 `/coollabsio/coolify-docs` and Anysearch official Dockerfile/health documentation: `https://coolify.io/docs/applications/builds/dockerfile` and `https://coolify.io/docs/applications/configuration/health-checks`. Interface facts used: repository build context, reachable internal listener/port, final-stage health command and documented non-Compose HEALTHCHECK precedence.
- Official unprivileged Nginx image interface: `https://hub.docker.com/r/nginxinc/nginx-unprivileged`; no upstream Dockerfile or expressive implementation copied.
- Microsoft downloads: `https://dotnet.microsoft.com/en-us/download/dotnet/10.0`, identifying SDK 10.0.402 and runtime 10.0.13 on October 19, 2026.
- Microsoft.Playwright primary metadata: `https://www.nuget.org/packages/Microsoft.Playwright/1.63.0` and official release notes; only test/tool dependency admission is proposed.
- Actual release scope/policy registry and `ChangeWorkflowCommand` allocation/create/preflight behavior; five IDs allocated through the existing native executable, with no fragments or commits.

### Exact reviewed planning inputs

These SHA-256 identities bind the initial revision-1 design review. Later task-status-only updates do not themselves change the moral design; a material scope, authority or behavior change requires revalidation.

| Input | SHA-256 |
| --- | --- |
| `dev/active/browser-production-v1/browser-production-v1-plan.md` | `ceb228f1783e09c8f4c0c10949d796cab335b15f162c436a62a6f0810824ade0` |
| `dev/active/browser-production-v1/browser-production-v1-tasks.md` | `6bf58f32e02de2b907ce11d85b97737b3ec2cc7d4864366de9dc9313dc20e1e0` |
| `dev/active/browser-production-v1/browser-production-v1-context.md` | `b3f254d430c6c9f2dcf6986daea172a8e5a9239a2b435a13a195d329e8d85f90` |

### Independent revision-2 revalidation inputs

Captured with native `sha256sum` before this report edit and before any parent metadata updates. Plan/tasks bytes identify the reviewed design; context is the contemporaneous handoff, not a substitute for those inputs. This reviewer did not edit any triad file, backlog or consultation.

| Input | SHA-256 |
| --- | --- |
| `dev/active/browser-production-v1/browser-production-v1-plan.md` | `f47c95acfa2ae3e8b0f611f29677758740f4a08577d687e02f105f7702192467` |
| `dev/active/browser-production-v1/browser-production-v1-tasks.md` | `d5b955b485a709fa27580d7815dabf9b086d5061ef1f539a8c954f2178b27b61` |
| `dev/active/browser-production-v1/browser-production-v1-context.md` | `9420e9ec108b4202d6c198350aef71b808499fb1a9e29dcb0e5fab3b148c1ea3` |
| `dev/backlog/browser-production-v1-followups.md` | `00262c2cf0ad152c162886aa142e39d33e95d55cb1955d74a24db335fa3ae7da` |
| `islamic-value-sensitive-design/consultations/i-vsd-browser-production-readiness-detailed-audit.md` | `15d5c0e2dda3ae15279fb397f6848c1124c3d51bc4a20f4a71af1d2b4ae41065` |
| This assessment before revalidation | `8468a35bf75df8db6d4c2b1fda990f7d7c1f8698d9ef8f9bcc8563e40ad9332e` |

Reviewed directly: the routed I-VSD integration, report, readable-report, scope and evidence-level contracts; all seventeen consultation finding/mitigation explanations; revision-2 triad; required follow-up dependency/override table and retained behavioral, architecture, operational and task/qualification packets. Native checkout inspection confirmed branch `main` and product HEAD `37bfa9b492f54f0e746150edc9a624ad668774b3`. Earlier external research, fetched-upstream comparison and runtime audit checks above remain attributed historical evidence; they were not repeated or newly verified here. Exact dependency versions remain admission candidates requiring current verification by V1/V5/V7, not a new assertion of latest availability.

## Missing Evidence

Actual operating personnel/access, accepted support window, deployed candidate qualification, controlled race regressions, actual image digests/terms review and new stakeholder/operational validation. Current plan coverage is not substituted for these outcomes.

## Context Inventory

Repository source/contracts/tests, the unchanged consultation, existing release tooling, current upstream comparison and a bounded read-only caller inventory supplied one shared packet. No live hosting account, secret, mailbox, production configuration or hosted issue/support system was inspected.

## Planning Handoff

- Workstream: browser-production-v1
- Status: current
- Reviewed input: revision-2 plan/tasks at the exact SHA-256 identities above, contemporaneous context, mandatory follow-up backlog and unchanged consultation
- Findings and mitigations: IVSD-F001 through IVSD-F017 map one-to-one to IVSD-M001 through IVSD-M017
- Required plan mappings: all seventeen revision-2 pairs independently validated in the current table below; the revision-1 table remains historical. Deferral to V1-V7 is mandatory launch work, not risk acceptance or a waiver
- Escalations required before: public image publication/deployment and release activation for actual operator authority, supported-platform evidence and operational acceptance; each follow-up needs its own approved re-baseline before execution. No scholarly ruling is needed for the scoped engineering corrections
- Implementation handoff: stale I-VSD mapping gate satisfied for the bound revision-2 design; implementation explicitly authorized in the existing checkout, no worktree and no commits. Establish the tasks' baseline and controlled Phase Red before product edits. Execution status remains owned by tasks/context, not this report
- Refresh triggers: changed consent, answer presence, export, public-cache, trust, host, support, runtime or release behavior; material plan/task rewrite

### Historical revision-1 finding-to-plan mapping

| Finding / mitigation | Scenarios | Tasks | Required disposition |
| --- | --- | --- | --- |
| IVSD-F001 / IVSD-M001 | S06-S07 | 3.1-3.2 | Implement with controlled privacy regression |
| IVSD-F002 / IVSD-M002 | S04-S05 | 2.1-2.2, 3.1-3.2 | Implement shared edit intent and browser fencing |
| IVSD-F003 / IVSD-M003 | S01-S02 | 2.1-2.3 | Implement typed round-trip fidelity |
| IVSD-F004 / IVSD-M004 | S03 | 2.2-2.3 | Implement explicit omission |
| IVSD-F005 / IVSD-M005 | S12 | 5.1, 10.2 | Fix ownership and measure rendered states |
| IVSD-F006 / IVSD-M006 | S08-S09 | 4.1-4.3 | Implement safe actionable correction |
| IVSD-F007 / IVSD-M007 | S15-S16 | 7.1-7.2 | Implement fail-closed readiness recovery |
| IVSD-F008 / IVSD-M008 | S19 | 1.1-1.2, 8.1, 9.1 | Service/review actual delivery inputs |
| IVSD-F009 / IVSD-M009 | S20-S21 | 8.1-8.3, 10.2-10.3 | Implement container policy; actual Coolify-edge acceptance before launch |
| IVSD-F010 / IVSD-M010 | S23-S25 | 8.2-8.3, 10.2-10.3 | Exact-image deployment and worker-aware recovery |
| IVSD-F011 / IVSD-M011 | S22-S26 | 10.1-10.3 | Named machine checks and human activation gates |
| IVSD-F012 / IVSD-M012 | S19, S27 | 9.1-9.2, 10.3 | Named operating duties and accepted response/recovery |
| IVSD-F013 / IVSD-M013 | S13 | 6.1, 10.3 | Informed choice and comprehension acceptance |
| IVSD-F014 / IVSD-M014 | S10-S11 | 4.2-4.3 | Explain eligibility and safe conflicts |
| IVSD-F015 / IVSD-M015 | S14 | 6.2 | Deliberate Clear without persistence |
| IVSD-F016 / IVSD-M016 | S16-S18 | 7.1-7.3 | Bounded public-cache management |
| IVSD-F017 / IVSD-M017 | S28 | 10.2-10.4 | Measured supported limits; optional translation graduates explicitly |

### Current revision-2 finding-to-plan mapping

Every row is **mapping validated / finding open**. Plan Section 9 supplies the scenario and destination; active tasks or the linked [required follow-up record](../../dev/backlog/browser-production-v1-followups.md) supply concrete work and acceptance. The original finding narratives, principles, stakeholders, owners, evidence levels and escalation boundaries above remain applicable. No row claims implementation success.

| Finding / mitigation | Scenarios | Active task or mandatory follow-up | Disposition evidence and remaining acceptance |
| --- | --- | --- | --- |
| IVSD-F001 / IVSD-M001 | S06-S07, S29, S32 | Phase Red; 2.1-2.3 | Active: synchronous denied policy, recursive clearing, import fence and ALL-owner retirement before any await; held-cleanup/public-edit, fault, replacement and two-artifact byte/capability assertions required. Browser/Presentation owners; privacy release acceptance remains open. |
| IVSD-F002 / IVSD-M002 | S04-S05, S30 | Phase Red; 1.1-1.3; 2.1-2.3 | Active: one mutation owner records invalid/rejected user intent even without changed JSON, but not programmatic refresh; held-import and old reveal/copy/download invalidation plus native no-op regressions required. Presentation/Browser owners. |
| IVSD-F003 / IVSD-M003 | S01-S02, S31 | 1.1-1.3; native slice-exit checks | Active: cloned typed presence independent of display text; literal empty-string/list/default vectors and distinct empty-enum identity; migrate Browser/Desktop/Terminal direct editors together. Independent round-trip/output and actual changed controls remain unproved. |
| IVSD-F004 / IVSD-M004 | S03 | 1.1-1.3; slice-exit surface checks | Active: explicit scalar/object/list omission preserves siblings, consent and supplied empty-container distinctions while invalidating output. Accessible names/focus and native/Browser behavior require actual evidence. |
| IVSD-F005 / IVSD-M005 | S12 | V3 original 5.1; V7 10.2-10.3 / Q14 | Mandatory follow-up: owning CSS-layer fix and actual theme/state/keyboard/phone/desktop checks in its increment; independent of V2 diagnostics. Final settled rendered contrast and human accessibility acceptance cannot be inferred from DOM assertions. |
| IVSD-F006 / IVSD-M006 | S08-S09 | V2 original 4.1-4.3 | Mandatory follow-up: immutable structured public paths/rule codes, bounded deterministic diagnostics and exact nested/duplicate correction focus; real Core and all result consumers migrate. Values remain excluded; supported-constraint usability/accessibility acceptance remains open. |
| IVSD-F007 / IVSD-M007 | S15-S16 | V4 original 7.1-7.2 | Mandatory follow-up: controlled first-failure recovery and generation-fenced clean-state readiness recheck; no fetch permission during answer custody or forced reload. Actual worker/cache recovery remains V7 Q10-Q13. |
| IVSD-F008 / IVSD-M008 | S19 | V1 original 1.1-1.2; V5 8.1; V6 9.1 | Mandatory launch gate: exact currently admitted stable SDK/runtime/package and OCI closure, generated locks/notices and separate runtime/base-image advisory review; V1 independent of repair, not postponed out of launch. Current 10.0.302 authority stays until its owner changes it. No exploitability or fresh version-admission claim. |
| IVSD-F009 / IVSD-M009 | S20-S21 | V5 original 8.1-8.3; V7 10.2-10.3 / Q16 | Mandatory follow-up: versioned static-host policy, complete Store/free packages, nonroot 8080, real missing-asset/health/header/MIME/encoding checks; actual Coolify HTTPS/TLS/redirect/HSTS and framing/CSP acceptance before launch. Listener health alone is insufficient. |
| IVSD-F010 / IVSD-M010 | S23-S25 | V5 original 8.2-8.3; V7 10.2-10.3 / Q13 | Mandatory follow-up: tested immutable image promotion without rebuild, known-good retention, coherent assets/cache policy and multi-workspace update/rollback rehearsal; no forced live activation or recall promise. Actual hosting/promotion authority remains external and separately authorized. |
| IVSD-F011 / IVSD-M011 | S22-S26 | V7 original 10.1-10.3 | Mandatory release gate: exact Q01-Q16 SET for every engine and candidate, negative harness cases for missing/duplicate/unknown/failed/wrong/unreported evidence, actual downloads/clipboard/custody sinks and worker journeys. Real screen-reader/zoom/device/actual-origin acceptance is separate from engine automation. |
| IVSD-F012 / IVSD-M012 | S19, S27 | V6 original 9.1-9.2; V7 10.3 | Mandatory operating gate: read-only recurring advisory lane, real primary/backup responders and approved achievable response/escalation window, access/log-retention policy and synthetic incident/known-good recovery rehearsal. Steward/hosting acceptance, not invented staffing or 24/7 promises. |
| IVSD-F013 / IVSD-M013 | S13 | V3 original 6.1; V7 10.3 | Mandatory follow-up: optional per-workspace consent with origin/browser/extension trust and native/deferred-secret alternatives beside entry; accessible complete help and first-time comprehension remain required. Preserve consultation warning that ordinary settings can also be confidential; masking is not delivered-code protection. |
| IVSD-F014 / IVSD-M014 | S10-S11 | V2 original 4.2-4.3 | Mandatory follow-up: one inverse-assessment owner explains unsupported selection and safe conflicting paths, atomically preserving answers on rejection; unsupported/supported/conflicting/secret-bearing vectors. No universal inverse or automatic merge requirement is invented. |
| IVSD-F015 / IVSD-M015 | S14 | V3 original 6.2 | Mandatory follow-up: native confirmation through existing JS boundary; cancel preserves answers/consent/output, confirm clears with useful focus. No persistence/secret undo buffer or new dialog framework. |
| IVSD-F016 / IVSD-M016 | S16-S18 | V4 original 7.1-7.3 | Mandatory follow-up: specific eight-slot/ninth-refusal status, selective public-definition removal/replacement and current connection/readiness refresh; keep unrelated public entries and exclude answers/output. Controlled owner checks plus V7 real cache/storage evidence remain open. |
| IVSD-F017 / IVSD-M017 | S28 | V7 original 10.2-10.4 / Q15 | Mandatory release gate: predeclared provisional measurement criteria, then approved measured startup/transfer/input/render/preparation/memory limits on supported device classes and representative first-time completion. English/RTL is not translation; optional language work cannot replace supported-user evidence. |

### Mandatory V1-V7 preservation check

The backlog's top-level dependency/CTO constraints govern retained original packets. Original phase numbers below identify evidence, not an instruction to resume the superseded ten-phase sequence. All seven destinations are **mapping validated / not executed here**, with accountable roles retained but actual staffing unverified.

| Follow-up | Preserved obligation / task evidence | Dependency and gate disposition |
| --- | --- | --- |
| V1 Serviced inputs | F008/M008; original 1.1-1.2 and engineering minima | Independent admission work; actual stable artifacts, closure/advisories and native locks before launch. Recheck dated version candidates; do not override authoritative pins in the active repair. |
| V2 Safe correction/import | F006/M006, F014/M014; original 4.1-4.3 | Follows primary form semantics; bounded immutable value-free paths and atomic rejection across actual consumers. Dedicated re-baseline required; no Core redesign smuggled into active slice. |
| V3 Visible actions and deliberate choices | F005/M005, F013/M013, F015/M015; original 5.1, 6.1-6.2 | Contrast can ship independently of diagnostics; consent explanation/Clear remain separable outcomes. Render/focus evidence belongs in each changed increment, with final accessibility/comprehension acceptance in V7. |
| V4 Offline readiness/cache recovery | F007/M007, F016/M016; original 7.1-7.3 | Preserve primary custody and establish Red first; clean-state retry/selective public deletion never authorizes answer-time network activity. Actual worker/storage proof remains required. |
| V5 Coolify image | F009/M009, F010/M010 and F008/M008 delivered inputs; original 8.1-8.3 | V1 and changed behaviors before candidate qualification; independent Dockerfile authoring permitted only under its own scope. Distinct Store/free roots, notices, digests, health and headers; no provisioning/push/deploy authority. |
| V6 Security operations | F012/M012 and F008/M008 recurring review; original 9.1-9.2 | Advisory lane independent; actual people, accepted response/log/access policies and worker-aware recovery before activation. No fabricated account access or service commitment. |
| V7 Release qualification | F011/M011, F017/M017 and final F005/F009/F010/F012/F013 acceptance; original 10.1-10.4 | Exact candidate after relevant slices; exact check-ID set per engine/image plus negative runner cases. Actual-origin, human accessibility/device/comprehension and operating gates remain explicit blockers until evidenced. |

Coverage result: 17/17 stable finding/mitigation pairs and 7/7 mandatory follow-up destinations accounted for; four pairs active and thirteen assigned required follow-ups, with cross-cutting release gates retained. No non-applicability or risk-acceptance disposition is used. S01-S07/S29-S32 remain active, S08-S28 remain launch requirements, and Q01-Q16 remain exact-candidate qualification requirements. This is a semantic mapping review, not a claim that every future implementation detail or acceptance test already exists.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
| --- | --- | --- | --- | --- |
| 2026-10-19 | none | draft / changes-required | Planning requested; launch qualification accepted; hosting decision and completed triad pending | Shared packet at named commit |
| 2026-10-19 | draft | current / plan-aligned | User selected Coolify; completed revision-1 requirements, tasks, commit contracts and all seventeen mappings revalidated | Exact three input hashes above; implementation and activation gates remain open |
| 2026-10-19 | current / plan-aligned | stale / changes-required | CTO split applied to triad revision 2; active/deferred task mappings changed and S29-S32 added | Historical reviewed hashes preserved; planning-mode revalidation of final revision-2 plan/tasks and required launch follow-ups pending; no implementation or user approval granted |
| 2026-10-19 | stale / changes-required | current / plan-aligned | Independent planning-mode revalidation of revision 2, all seventeen pairs and mandatory V1-V7; latest user explicitly authorizes current-checkout implementation without commits | Exact pre-parent-update plan/tasks/context/backlog/consultation hashes above; historical revision-1 evidence retained; every finding remains open and runtime/human/operator/release evidence remains pending |
