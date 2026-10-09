# Desktop cross-platform distribution — I-VSD planning review

Last Updated: 2026-10-09

## Review Metadata

- Mode: planning
- Subject: protected offline Desktop use and release downloads across Linux, Windows and macOS
- Workstream: desktop-cross-platform-packaging
- Report kind: provider-responsibility design review
- Report revision: 2
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-10-09
- Reviewed input: workstream revision 2, working-tree plan/tasks/context and retained dependent backlogs inspected on 2026-10-09; local HEAD `8c5125afad8e20cd4e803bd4949b7fc624d5642b`. Historical revision-1 source inspection was at `de0665e05fc3981c7fb7e9cdd01a9c04f57c934b`.
- Supersedes: none

## Executive Summary

Users need more than a GUI binary for each operating system: they need working protected saves, reliable installation, and an honest account of which hosts were tested. The current Avalonia UI is portable in structure, but protected saves and Desktop publication are Linux-only. The provider controls both that capability boundary and the download pipeline.

Revision 2 first completes the existing Linux x64 Desktop workflow: correct preparation, local file/folder selection, safe cancellation/recovery and actual published-app proof. Native selection feeds bounded admission/import and the protected writer; it never writes prepared output through picker streams. A cancelled or stale dialog must not erase work or restore obsolete preparation. These responsibilities remain with the existing Core, Presentation, Desktop and Artifacts owners.

Windows/macOS protection and host qualification, installers and governed downloads remain explicit dependent workstreams. Their privacy, accessibility, rights, signing and publication gates are retained. All D01–D14 obligations map below to active or deferred owners, so report revision 2 is **current / plan-aligned** for workstream revision 2. No design or scholarly escalation blocks the user's now-authorized implementation in the current checkout. Missing native/operator proof still blocks readiness claims; release authorities are required before the corresponding release activity. This disposition is provider-responsibility design reasoning, not religious certification, implementation correctness or operational proof.

## Scope

Active scope is existing Linux x64 Desktop functional readiness, native local selection, operation lifetime, value-free feedback and exact published-payload qualification. Deferred scope is Windows/macOS protected output and admission, installation formats, package supply chain and governed GitHub release assets. Browser behavior, target application administration, monetization, AI, religious content and remote configuration application are outside this change.

## Claim Boundary

Subsequent explicit user authorization adds a bounded unsigned Arch development
package, with X11 and Wayland/XWayland session compatibility. Native makepkg
retains the complete public payload and notices; isolated real pacman checks
prove installation, upgrade and removal preserve a synthetic owner-only operator
file. Installed launch checks cover both display routes with SDK/network absent.
No installation scripts, target apply, new answer storage, signing or publication
authority are introduced. This development slice does not satisfy or waive the
qualified-release/readiness and physical accessibility gates retained here.

Historical source inspection establishes the revision-1 gaps; current bounded source and revision-2 document inspection establishes owner/task traceability. Official-interface facts are inherited from the plan's source register, not independently fetched or claimed as Tavily/Context7 results in this pass. No product suite, installer, native host matrix, signing service or public upload was executed for this revalidation. Plan-aligned means traceable planned mitigations, not that they already work. User implementation authorization is separate evidence; this report does not authorize history, trust promotion or publication.

## Findings

### IVSD-F001: A portable window must not imply safe output on an unverified host

Operators entrust private configuration answers to the application. At the revision-1 source review Windows had an unavailable writer and macOS was rejected. Turning on saving through ordinary files would expose that entrusted information; advertising full support while leaving saving unavailable would also mislead operators. Revision 2 correctly retains those host gates and first checks the existing Linux custody chain: composition acceptance, current-answer binding, picker/import lifetime, protected publication and committed-prefix recovery.

Native dialogs introduce another asynchronous result that can arrive after replacement, editing or close. Accepting that result without checking its captured operation identity can choose a stale destination or revive obsolete work. Keeping all output in Artifacts, invalidating before commit, and preserving files already committed connects responsible custody to the actual data flow. Early Core-owned composability checks also avoid presenting output as ready when saving will predictably reject it; temporary restricted buffers must be disposed rather than exposed as previews.

- Lifecycle: accepted; severity: high; claim type: verified implementation gap.
- Principles/domains: Amanah (responsible custody), non-harm and truthfulness; architecture, privacy, product communication.
- Stakeholders: operators and people whose credentials or private configuration may appear in prepared artifacts.
- Controlled decision: when a host is declared supported and how bytes become visible on disk.
- Evidence: historical E01/E02/E03 and current E08; validation level: bounded source inspection and design/task traceability only.
- Mitigation IVSD-M001: Core-owned current single/bundle acceptance; captured/rechecked operation identity for local selection/import/preparation/save/close; no restricted previews or value/path-bearing sinks; native create-only, owner-restricted publication with permissions before bytes, destination authority/identity checks and committed-prefix preservation. Adversarial real-host tests precede qualification; unsupported filesystems fail closed without ordinary-save fallback.
- Owner/next validation: active Desktop readiness tasks 1.1–1.2, 2.1–2.2 and 3.1–3.2, with existing Presentation/Core/Artifacts invariants; deferred Artifacts/platform maintainers prove Windows and both macOS lanes before support claims.
- Escalation boundary: if a filesystem cannot meet the invariant, reject that destination instead of weakening protection.

### IVSD-F002: Download automation must preserve identifiable and complete release bytes

An operator expects a release download to correspond to the version they chose. A workflow that uploads mixed commits, silently replaces an asset, or drops dependency notices would break that expectation. A successful upload alone does not establish trustworthy software.

- Lifecycle: accepted; severity: high; claim type: proposed supply-chain risk.
- Principles/domains: Amanah, truthfulness and respecting others' rights; operations and distribution.
- Stakeholders: download users, release maintainers, dependency rightsholders and AUR maintainers.
- Controlled decision: checkout identity, build inputs, package closure, asset retries and release publication timing.
- Evidence: historical E04/E05/E06 and current E08; validation level: inherited repository/official-interface evidence plus design/task traceability.
- Mitigation IVSD-M002: exact source/fixture/payload/scenario qualification identity and executable rejection of absent/stale/mismatched/incomplete evidence before later assembly; promoted Configurator verifier authorization of the exact tag and chained release evidence before privileged jobs; independent OS code-signing custody; reviewed pinned tools; retained notices and resolved component inventory; checksums calculated after signing; complete asset manifest; attach assets to a draft before publishing; identical retries skip and conflicting bytes reject. Unsigned OS development packages do not bypass governed live-release authorization.
- Owner/next validation: Packaging/Release maintainers; actual archive inspection and an authorized rehearsal before release activation.
- Escalation boundary: unknown licenses or absent signing authority block the relevant release, not the privacy boundary.

### IVSD-F003: Platform access and installation must not demand unnecessary authority

Arch, Debian, Ubuntu, Windows and Mac users should receive an installation route appropriate to their platform. Before packaging, Linux operators need a complete workflow without SDK, runtime installation, terminal-only selection or network access. Local selectors complement typed paths rather than forcing a single interaction method. Cancellation must preserve answers/preparation, and relevant fields and corrective actions must stay reachable by keyboard with sensitive values masked. This connects user agency to non-destructive interaction and justice to usable access, while actual assistive-technology outcomes remain unproved.

Packaging must install the application rather than applying configuration to another system. Updates and removal must preserve operator-owned output. Keyboard use, focus and sensitive input masking must survive subsequent platform adaptation. Deferral preserves these responsibilities; Linux evidence cannot stand in for Windows/Mac or installed-package evidence.

- Lifecycle: accepted; severity: medium; claim type: proposed product/operations requirement.
- Principles/domains: justice, non-harm and user agency; portability, UX and support.
- Stakeholders: operators on different platforms, keyboard/assistive-technology users and local administrators.
- Controlled decision: supported matrix, install scope, desktop entry metadata, uninstall ownership and support wording.
- Evidence: historical E01/E03/E04 and current E08; validation level: bounded source inspection and design/task traceability; no stakeholder study or assistive-technology evidence.
- Mitigation IVSD-M003: bounded local definition/import/folder selection with typed-path access retained; non-destructive cancellation/nonlocal refusal; value-free actionable feedback and reachable validation/focus/RTL; actual native and no-SDK/offline published workflow. Deferred native Linux packages/portable AppImage, AUR-ready recipe separately from AUR publication, per-user Windows installation and standard macOS app bundle preserve user data and repeat the workflow on each qualified host. Bound support wording to actual OS/filesystem/accessibility evidence.
- Owner/next validation: active Desktop tasks 2.1–2.2 and 3.1–3.2; deferred Desktop/platform and packaging maintainers prove installation/removal and native interaction on selected hosts.
- Escalation boundary: accessibility outcomes remain unverified until actual platform checks; do not claim certification from headless tests.

## Recommendations

Complete Linux functional readiness and its genuine failing privacy/lifecycle invariants first; retain existing passes. Use existing Core/Presentation/Artifacts ownership rather than duplicated rules or configuration logic. Qualify each additional native host before claiming support, and reject missing or mismatched readiness evidence before later installer assembly. Require Configurator-owned signing/notarization for normal public Windows/macOS downloads and allow explicitly marked unsigned OS development packages only under the retained governed-release policy. Keep GitHub transport authority separate from the promoted signed-release verifier's trust.

Rejected alternatives: picker stream output or ordinary-file saving bypasses custody; accepting stale dialogs or treating cancellation as replacement destroys user agency; packaging only the portable GUI leaves functional proof missing; headless-only admission cannot establish native/published/assistive interaction; attaching assets only after publication fails with immutable GitHub releases; automatic AUR submission exceeds the requested authority. Build-tool terms and shipped installer/AppImage components must be reviewed individually; MIT application licensing does not relicense them.

## Stakeholders

Operators; affected credential/configuration owners; release maintainers; Arch/AUR maintainers; local administrators; dependency rightsholders; users relying on keyboard navigation and accessibility APIs.

## I-VSD Principles And Domains

Responsible custody applies to private answers and release credentials. Truthfulness applies to support claims and signing state. Justice and user agency apply to usable installation and preserving user files. Respect for others' rights applies to distribution notices and component terms. No religious-content, financial or religious-legal product decision is introduced.

## Validation Gaps

Active: preparation/lifecycle invariants, native picker cancellation/staleness, value-free actionable feedback, D01–D13 native/published proof, exact dirty-source/fixture/payload identities, and manual dialog/keyboard/RTL/scaling/assistive evidence. Native harness results, headless tests and the actual shipped binary are separate evidence. Missing proof keeps readiness unqualified. Deferred: Windows DACL/reparse races; macOS ACL/publication races; installers/AppImage runtime behavior; promoted Configurator release authority and OS signing custody; reviewed tool licenses; authorized GitHub release adapter rehearsal.

## Escalation Needed

No unresolved provider-responsibility or scholarly escalation is required before the now-authorized active Desktop implementation. The user authorizes implementation in the current checkout; this review supplies design alignment only. Actual missing host/operator proof remains a readiness gate rather than a reason to invent implementation certification.

Deferred release maintainers must independently establish promoted Configurator release-verification authority before live transport, and provision Windows signing and Apple Developer ID/notarization authorities before normal public Windows/macOS activation. Authority-free dry runs produce validation artifacts only. Unclear component rights require distribution review before affected packages ship. Those retained gates are not discharged by local readiness, unsigned development output or this report. A later religious-legal claim requires qualified scholarly authority.

## Evidence Reviewed

- E01: `src/Configurator.Desktop/Program.cs`, `Configurator.Desktop.csproj` and `ViewModels/SetupDesktopShellViewModel.cs` — platform detection and shared writer routing.
- E02: `src/Configurator.Artifacts/ProtectedArtifactWriter.cs`, `WindowsProtectedArtifactWriter.cs`, `UnixProtectedArtifactWriter.cs` — Linux-only availability, Windows stub and Linux-specific staging.
- E03: `tests/Configurator.Desktop.Tests/DesktopProtectedWriteInvariantTests.cs`, `DesktopApplicationTests.cs`, `tests/Configurator.Artifacts.Tests/ProtectedArtifactWriterTests.cs` — existing source-level test coverage and Linux gates; not executed here.
- E04: `eng/configurator/Configurator.Release.proj`, `tests/Configurator.Architecture.Tests/ReleaseContractTests.cs`, `StandalonePackageTests.cs` — present publication contract and package checks.
- E05: `.github/workflows/build-test.yml`, `docs/internal/RELEASE_RUNBOOK.md`, `docs/internal/PACKAGING.md` — unsigned validation only and independent publication custody.
- E06: source register and sanitized interface findings in [the workstream plan](../../dev/active/desktop-cross-platform-packaging/desktop-cross-platform-packaging-plan.md), Sections 2 and 5. No external implementation source is transferred.
- E07: completed revision-1 plan, [tasks](../../dev/active/desktop-cross-platform-packaging/desktop-cross-platform-packaging-tasks.md) and [context](../../dev/active/desktop-cross-platform-packaging/desktop-cross-platform-packaging-context.md). Plan Section 9 binds IVSD-F001/M001 to S01–S07 and native writer/admission tasks; F002/M002 to package/release tasks and S10/S14–S18; F003/M003 to S08–S13 and installation/native interaction tasks.
- E08: revision-2 [plan](../../dev/active/desktop-cross-platform-packaging/desktop-cross-platform-packaging-plan.md), Sections 0–12; [tasks](../../dev/active/desktop-cross-platform-packaging/desktop-cross-platform-packaging-tasks.md), pre-implementation gates and tasks 1.1–3.2/readiness exit; [context](../../dev/active/desktop-cross-platform-packaging/desktop-cross-platform-packaging-context.md), scope/ownership/evidence limits; dependent [platform qualification](../../dev/backlog/desktop-platform-qualification.md), [installable packages](../../dev/backlog/desktop-installable-packages.md) and [governed downloads](../../dev/backlog/desktop-governed-release-downloads.md). Bounded current `SetupDesktopShellViewModel` preparation/save/invalidation/disposal and `MainWindow` handler/lifetime locators confirm existing owner seams; no runtime validation was performed. Pre-execution reviewed plan SHA-256: `9f1b921d47342ceebef1126bef8e08e0177946cd1e397bd8f466163cb5ccb1c6`; tasks: `11d46117a70e0de5f0fc28aa02a222d8410a909fad1aa69c2de59079d21f98bc`. Subsequent report-link/status synchronization does not change these reviewed behavior contracts; material scope/mapping changes require fresh revalidation.
- E09: explicit user request to fully implement this workstream in the current checkout, supplied in the current session. It supersedes the triad's planning-only implementation restriction; it supplies no release/signing authority or runtime evidence.

## Missing Evidence

All implementation and operational evidence listed under Validation Gaps. No package artifacts or signing credentials were inspected. Current AppImage repository license metadata reports `NOASSERTION`; it is not a license approval.

## Context Inventory

Existing MIT offline application; Avalonia 12.1.3; .NET SDK 10.0.302; shared custody owners; existing Linux protected-save implementation; active Linux readiness separated from platform/package/release outcomes. Current working tree includes unrelated prior edits; HEAD alone is not qualification identity. This report revalidation changes only this report and performs no history, source mutation or publication.

## Common Overlooked Failures And Outcomes

Stale dialog acceptance can change a new workspace's destination; cancelled selection can unnecessarily erase prepared work; late save status can resurrect obsolete readiness; automation/status sinks can disclose private values or paths; a headless or reference harness pass can misidentify untested shipped bytes. Revision 2 addresses these with operation identity, non-destructive cancellation, Core-owned composability, value-free feedback and separately bound native/published evidence. These are planned mitigations, not observed successes.

Deferred inherited ACLs can defeat POSIX mode-only assertions; signed macOS bundles can fail when native libraries extract outside the signed bundle; AppImage can carry an incompatible runtime or depend on FUSE; an AUR recipe can point at unavailable assets; retries can replace different bytes; uninstallers can delete user output; release-created events do not run for drafts. Deferral does not remove these risks or the retained gates.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
|---|---|---|---|---|
| 2026-10-09 | none | draft / ready-for-planning | Shared audit and requirement intake | E01–E06, workstream revision 1 |
| 2026-10-09 | draft | current / plan-aligned | Completed triad mapping revalidated; implementation/custody limits preserved | E07, workstream revision 1 |
| 2026-10-09 | current | current / plan-aligned | Independent review corrections revalidated: governed tag authority and native/headless process isolation | E07, final revision 1 |
| 2026-10-09 | current | stale | CTO revision 2 split active Linux readiness from qualification/packages/downloads and changed local interaction/task mappings | E08; triad explicitly marked revision-1 review stale |
| 2026-10-09 | stale | current / plan-aligned | Revalidated revised active/deferred D01–D14 mapping, picker/access/custody responsibilities and retained release prerequisites; user implementation authorization is separately recorded | E08/E09; report revision 2, workstream revision 2; no runtime or scholarly certification |

## Planning Handoff

- Workstream: desktop-cross-platform-packaging
- Status: current / plan-aligned
- Reviewed input: Desktop functional readiness before cross-platform packaging, workstream revision 2; report revision 2.
- Findings and mitigations: IVSD-F001 → IVSD-M001; IVSD-F002 → IVSD-M002; IVSD-F003 → IVSD-M003.
- Required plan mappings: every D01–D14 row below identifies exact active task or dependent owner. IVSD-F001/M001 → active D05–D10/D13 and platform qualification; IVSD-F002/M002 → active D13 evidence identity, deferred D14/package admission and governed downloads; IVSD-F003/M003 → active D01–D04/D08/D10–D13 and deferred installed/platform interaction. Existing stable finding/mitigation IDs are preserved; none is claimed resolved by this design review.
- Escalations required before: none for authorized active implementation; actual D01–D13 evidence and adversarial privacy review before Linux readiness; real-host proof before new platform admission; component rights before affected distribution; independently promoted release/signing/notarization custody before corresponding live/public release activation.
- Refresh triggers: supported matrix, privacy guarantees, signer custody, release activation defaults, installer privileges, telemetry or user-file handling changes.

### Exact active and deferred scenario mapping

| Scenario | Finding / mitigation | Active revision-2 task and proof | Deferred responsibility |
|---|---|---|---|
| D01 Offline intake | IVSD-F003 / M003 | 2.1–2.2 local intake; 3.2 bounded JSON/YAML and visible admitted identity through native/published workflow | Platform qualification repeats intake on each host; installed packages repeat it |
| D02 Typed editing | IVSD-F001 / M001; IVSD-F003 / M003 | 1.1–1.2 preserve nested validation/readiness; 3.2 native typed editing/masking | Platform and installed workflow repetition |
| D03 Activation/disclosure | IVSD-F001 / M001; IVSD-F003 / M003 | 1.1–1.2 invalidation; 2.1–2.2 reachability/focus; 3.2 native activation/mode proof | Platform and installed workflow repetition |
| D04 Supported import | IVSD-F001 / M001; IVSD-F003 / M003 | 1.1 delayed-import custody; 2.1–2.2 bounded local import; 3.2 actual supported inverse-projection proof | Platform and installed workflow repetition; no new reverse-import authority |
| D05 Preparation correctness | IVSD-F001 / M001 | 1.1–1.2 Core-owned current single/bundle acceptance; 3.2 exact native outputs | Platform and installed workflow repetition |
| D06 Protected single save | IVSD-F001 / M001 | 1.1–1.2 current-generation/save lifecycle; 3.2 native bytes/conflicts and existing Linux Artifacts owner tests at exit | Platform qualification historical tasks 1.1–1.3/2.1–2.3 establish Windows/macOS native principal/ACL/race proof |
| D07 Bundle/recovery | IVSD-F001 / M001 | 1.1–1.2 consumptive save/reprepare and committed-prefix recovery; 3.2 real protected bundles plus owner tests | Platform qualification preserves S07 and repeats full revised workflow; installed packages repeat it |
| D08 Native selection | IVSD-F001 / M001; IVSD-F003 / M003 | 2.1–2.2 cancellation/capability/nonlocal/stale selection; 3.2 separate operator dialog evidence, not harness alone | Actual Windows/Mac native dialogs in platform qualification, then installed workflow |
| D09 Stale fencing | IVSD-F001 / M001 | 1.1–1.2 pre-commit/import/lifecycle barriers; 2.1–2.2 delayed-picker fencing; 3.2 native close/cancel | Platform and installed workflow repetition; committed files stay preserved |
| D10 Feedback/retry | IVSD-F001 / M001; IVSD-F003 / M003 | 1.1–1.2 correction/reprepare; 2.1–2.2 reachable value-free outcomes; 3.2 native sink/recovery proof | Platform and installed workflow repetition |
| D11 Native accessibility | IVSD-F003 / M003 | 2.1–2.2 control focus/RTL/masking; 3.2 separate manual keyboard/scaling/available assistive-tool evidence and explicit limits | Platform qualification and package S13 repeat native interaction; no universal accessibility certification |
| D12 Actual definition | IVSD-F003 / M003 | 3.2 full local Event definition with synthetic one/two/three-tenant independent expected fixtures through generic controls/import | Platform and installed workflow repetition; no Event runtime/apply dependency |
| D13 Published readiness | IVSD-F001 / M001; IVSD-F002 / M002; IVSD-F003 / M003 | 3.1 reject absent/incomplete native proof; 3.2 separately run exact shipped Linux payload without SDK/runtime/network and bind source/fixture/closure/scenario identity; readiness exit adversarial review | Every new host and installed format obtains its own exact payload proof; success never grants tag/signing authority |
| D14 Package admission | IVSD-F002 / M002; IVSD-F003 / M003 | No installer assembly/admission implementation in active slice; D13 supplies required evidence inputs only | Installable-packages admission contract/negative executable test before assembly and historical tasks 4.1–4.3/5.1–5.2; platform qualification supplies corresponding host proof; governed-downloads historical 6.1–6.2 gates complete matrix/final bytes/draft retry/tag authority |

Historical backlog task numbers identify retained requirements only; each dependent outcome requires its own current bounded execution triad at activation. D01–D13 remain required individually in final admission evidence; minimum harness counts cannot substitute for missing scenarios, manual interaction or shipped-binary proof.
