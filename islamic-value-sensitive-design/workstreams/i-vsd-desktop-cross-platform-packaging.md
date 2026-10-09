# Desktop cross-platform distribution — I-VSD planning review

Last Updated: 2026-10-09

## Review Metadata

- Mode: planning
- Subject: protected offline Desktop use and release downloads across Linux, Windows and macOS
- Workstream: desktop-cross-platform-packaging
- Report kind: provider-responsibility design review
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-10-09
- Reviewed input: local `main` at `de0665e05fc3981c7fb7e9cdd01a9c04f57c934b`; proposed workstream revision 1
- Supersedes: none

## Executive Summary

Users need more than a GUI binary for each operating system: they need working protected saves, reliable installation, and an honest account of which hosts were tested. The current Avalonia UI is portable in structure, but protected saves and Desktop publication are Linux-only. The provider controls both that capability boundary and the download pipeline.

The proposed design preserves local, offline operation and adds native output protection before advertising Windows/macOS support. Packages retain third-party notices and publish only after their contents, host behavior and provenance are checked. Public Windows/macOS release signing is a release gate; development packages may be explicitly unsigned. The completed triad maps all accepted findings to scenarios/tasks and retains release prerequisites, so this review is current and plan-aligned. This assessment concerns design responsibilities, not religious certification, implementation correctness or operational proof.

## Scope

Desktop native output, supported OS/architecture claims, Linux distribution formats, Windows installation, macOS app bundles, package supply chain and GitHub release assets. Browser behavior, target application administration, monetization, AI, religious content and remote configuration application are outside this change.

## Claim Boundary

Source inspection establishes current gaps. Official interface documentation informs proposed requirements. No product suite, installer, native host matrix, signing service or public upload was executed in this planning pass. A plan-aligned disposition will mean traceable planned mitigations, not that those mitigations already work.

## Findings

### IVSD-F001: A portable window must not imply safe output on an unverified host

Operators entrust private configuration answers to the application. Today Windows has an unavailable writer and macOS is rejected. Turning on saving through ordinary files would expose that entrusted information; advertising full support while leaving saving unavailable would also mislead operators.

- Lifecycle: accepted; severity: high; claim type: verified implementation gap.
- Principles/domains: Amanah (responsible custody), non-harm and truthfulness; architecture, privacy, product communication.
- Stakeholders: operators and people whose credentials or private configuration may appear in prepared artifacts.
- Controlled decision: when a host is declared supported and how bytes become visible on disk.
- Evidence: E01/E02/E03 below; validation level: source inspection only.
- Mitigation IVSD-M001: native create-only, owner-restricted publication; permissions before bytes; destination authority and identity checks; adversarial real-host tests; unsupported filesystems fail closed; no insecure fallback.
- Owner/next validation: Artifacts and Desktop maintainers; Windows and both macOS architecture lanes before support claims.
- Escalation boundary: if a filesystem cannot meet the invariant, reject that destination instead of weakening protection.

### IVSD-F002: Download automation must preserve identifiable and complete release bytes

An operator expects a release download to correspond to the version they chose. A workflow that uploads mixed commits, silently replaces an asset, or drops dependency notices would break that expectation. A successful upload alone does not establish trustworthy software.

- Lifecycle: accepted; severity: high; claim type: proposed supply-chain risk.
- Principles/domains: Amanah, truthfulness and respecting others' rights; operations and distribution.
- Stakeholders: download users, release maintainers, dependency rightsholders and AUR maintainers.
- Controlled decision: checkout identity, build inputs, package closure, asset retries and release publication timing.
- Evidence: E04/E05/E06; validation level: repository and official-interface inspection.
- Mitigation IVSD-M002: promoted Configurator verifier authorization of the exact tag and chained release evidence before privileged jobs; independent OS code-signing custody; reviewed pinned tools; retained notices and resolved component inventory; checksums calculated after signing; complete asset manifest; attach assets to a draft before publishing; identical retries skip and conflicting bytes reject. Unsigned OS development packages do not bypass governed live-release authorization.
- Owner/next validation: Packaging/Release maintainers; actual archive inspection and an authorized rehearsal before release activation.
- Escalation boundary: unknown licenses or absent signing authority block the relevant release, not the privacy boundary.

### IVSD-F003: Platform access and installation must not demand unnecessary authority

Arch, Debian, Ubuntu, Windows and Mac users should receive an installation route appropriate to their platform. Packaging must install the application rather than applying configuration to another system. Updates and removal must preserve operator-owned output. Keyboard use, focus and sensitive input masking must survive the platform adaptation.

- Lifecycle: accepted; severity: medium; claim type: proposed product/operations requirement.
- Principles/domains: justice, non-harm and user agency; portability, UX and support.
- Stakeholders: operators on different platforms, keyboard/assistive-technology users and local administrators.
- Controlled decision: supported matrix, install scope, desktop entry metadata, uninstall ownership and support wording.
- Evidence: E01/E03/E04; validation level: source inspection; no stakeholder study or assistive-technology evidence.
- Mitigation IVSD-M003: native Linux packages plus portable AppImage; AUR-ready recipe separately from AUR publication; per-user Windows installation; standard macOS app bundle; no user-data deletion; native launch and keyboard/focus checks; accurately bounded OS/filesystem claims.
- Owner/next validation: Desktop and packaging maintainers; package installation/removal and native UI evidence on the selected hosts.
- Escalation boundary: accessibility outcomes remain unverified until actual platform checks; do not claim certification from headless tests.

## Recommendations

Implement native protection first, then admit Desktop hosts and package them. Use existing Core/Presentation/Artifacts ownership, not duplicated per-platform configuration logic. Require Configurator-owned signing/notarization for normal public Windows/macOS downloads and allow explicitly marked unsigned prereleases. Keep GitHub transport authority separate from the retained signed-release verifier's promoted trust.

Rejected alternatives: ordinary-file saving on unsupported hosts violates custody; packaging only the portable GUI leaves essential functionality missing; attaching assets only after publication fails with immutable GitHub releases; automatic AUR submission exceeds the requested authority. Build-tool terms and shipped installer/AppImage components must be reviewed individually; MIT application licensing does not relicense them.

## Stakeholders

Operators; affected credential/configuration owners; release maintainers; Arch/AUR maintainers; local administrators; dependency rightsholders; users relying on keyboard navigation and accessibility APIs.

## I-VSD Principles And Domains

Responsible custody applies to private answers and release credentials. Truthfulness applies to support claims and signing state. Justice and user agency apply to usable installation and preserving user files. Respect for others' rights applies to distribution notices and component terms. No religious-content, financial or religious-legal product decision is introduced.

## Validation Gaps

Windows DACL/reparse races; macOS ACL and filesystem publication races; actual installers and AppImage runtime behavior; native GUI/accessibility behavior in a process isolated from headless tests; promoted Configurator release authority and OS signing custody; complete reviewed tool licenses; GitHub release adapter rehearsal.

## Escalation Needed

Release maintainers must independently establish promoted Configurator release-verification authority before any live release transport, and provision Windows signing and Apple Developer ID/notarization authorities before normal public Windows/macOS asset activation. Authority-free dry runs may produce validation artifacts only. Unclear component rights require the existing distribution review. No scholarly determination is needed for the current technical scope; a later religious-legal claim must be referred to qualified authority.

## Evidence Reviewed

- E01: `src/Configurator.Desktop/Program.cs`, `Configurator.Desktop.csproj` and `ViewModels/SetupDesktopShellViewModel.cs` — platform detection and shared writer routing.
- E02: `src/Configurator.Artifacts/ProtectedArtifactWriter.cs`, `WindowsProtectedArtifactWriter.cs`, `UnixProtectedArtifactWriter.cs` — Linux-only availability, Windows stub and Linux-specific staging.
- E03: `tests/Configurator.Desktop.Tests/DesktopProtectedWriteInvariantTests.cs`, `DesktopApplicationTests.cs`, `tests/Configurator.Artifacts.Tests/ProtectedArtifactWriterTests.cs` — existing source-level test coverage and Linux gates; not executed here.
- E04: `eng/configurator/Configurator.Release.proj`, `tests/Configurator.Architecture.Tests/ReleaseContractTests.cs`, `StandalonePackageTests.cs` — present publication contract and package checks.
- E05: `.github/workflows/build-test.yml`, `docs/internal/RELEASE_RUNBOOK.md`, `docs/internal/PACKAGING.md` — unsigned validation only and independent publication custody.
- E06: source register and sanitized interface findings in [the workstream plan](../../dev/active/desktop-cross-platform-packaging/desktop-cross-platform-packaging-plan.md), Sections 2 and 5. No external implementation source is transferred.
- E07: completed revision-1 plan, [tasks](../../dev/active/desktop-cross-platform-packaging/desktop-cross-platform-packaging-tasks.md) and [context](../../dev/active/desktop-cross-platform-packaging/desktop-cross-platform-packaging-context.md). Plan Section 9 binds IVSD-F001/M001 to S01–S07 and native writer/admission tasks; F002/M002 to package/release tasks and S10/S14–S18; F003/M003 to S08–S13 and installation/native interaction tasks.

## Missing Evidence

All implementation and operational evidence listed under Validation Gaps. No package artifacts or signing credentials were inspected. Current AppImage repository license metadata reports `NOASSERTION`; it is not a license approval.

## Context Inventory

Existing MIT offline application; Avalonia 12.1.3; .NET SDK 10.0.302; privacy-preserving shared owners; Linux protected-save implementation; no active live release publisher. Local `main` is 31 commits ahead of origin. Planning does not switch, pull, commit or publish.

## Common Overlooked Failures And Outcomes

Inherited ACLs can defeat POSIX mode-only assertions; signed macOS bundles can still fail when native libraries extract outside the signed bundle; an AppImage can carry an incompatible runtime or depend on FUSE; a generated AUR recipe can point at unavailable assets; a retry can silently replace different bytes; an uninstaller can delete configuration output; a release-created event does not run for drafts.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
|---|---|---|---|---|
| 2026-10-09 | none | draft / ready-for-planning | Shared audit and requirement intake | E01–E06, workstream revision 1 |
| 2026-10-09 | draft | current / plan-aligned | Completed triad mapping revalidated; implementation/custody limits preserved | E07, workstream revision 1 |
| 2026-10-09 | current | current / plan-aligned | Independent review corrections revalidated: governed tag authority and native/headless process isolation | E07, final revision 1 |

## Planning Handoff

- Workstream: desktop-cross-platform-packaging
- Status: current / plan-aligned
- Reviewed input: Desktop cross-platform support and release packaging, revision 1
- Findings and mitigations: IVSD-F001 → IVSD-M001; IVSD-F002 → IVSD-M002; IVSD-F003 → IVSD-M003.
- Required plan mappings: IVSD-F001/M001 → S01–S07 and tasks 1.1–1.3, 2.1–2.3, 3.1–3.2; IVSD-F002/M002 → S10/S14–S18 and tasks 4.1–4.3, 5.1–5.2, 6.1–6.3; IVSD-F003/M003 → S08–S13 and tasks 3.1, 4.1–4.3, 5.1–5.2. Every material ID is covered; native proof, rights review and signing custody remain future gates.
- Escalations required before: public release activation (real signing/notarization custody and dependency distribution review).
- Refresh triggers: supported matrix, privacy guarantees, signer custody, release activation defaults, installer privileges, telemetry or user-file handling changes.
