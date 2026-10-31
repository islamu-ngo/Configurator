# Status And Evidence Index

> **Audience:** Contributors | Integrators | AI agents
> **Status:** Mixed; current implementation routes and bounded historical evidence
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-30
> **Source Anchors:** `docs/internal/CLI_CONTRACT.md`, `docs/internal/IMPORT_COMPARISON.md`, `docs/internal/OPERATIONS.md`, `docs/internal/adr/`

This index separates claims that require different evidence. It routes to the
owning record without repeating test totals or creating a new acceptance gate.
The date above records a documentation check, not a fresh product qualification.
Use the [knowledge map](KNOWLEDGE_MAP.md) for document lifecycle and the
[current procedure entry](OPERATIONS.md#current-procedure-entry) for execution.

## Implemented scope

| Scope | Current owner and disposition | Evidence boundary |
| --- | --- | --- |
| Bounded definition v2, compilation and evaluation | [Conformance profile](DEFINITION_CONFORMANCE_PROFILE.md), [Core contract](CORE_DEFINITION_CONTRACT.md), [maintainer authoring](MAINTAINER_AUTHORING.md); delivered bounded profile | [Definition v2 qualification](OPERATIONS.md#definition-v2-qualification) and [final delivery](OPERATIONS.md#final-definition-delivery-and-responsibility) describe the recorded candidate, not arbitrary target acceptance |
| Shared workspace, Browser and native ownership | [Presentation](PRESENTATION_STATE.md), [Browser](BROWSER_UI.md), [Desktop](DESKTOP_CLIENT.md), [Terminal](TERMINAL_CLIENT.md) | [Cross-platform program qualification](OPERATIONS.md#cross-platform-quality-program-qualification-2026-10-27) retains delivered mechanisms and open duties |
| Ordinary configuration import and comparison | [Import/comparison](IMPORT_COMPARISON.md); implementation in progress, client/package qualification pending | [Accepted assessment design](adr/ADR-artifact-import-comparison.md) is not delivery or actual-client evidence |
| Machine CLI | [CLI contract](CLI_CONTRACT.md); machine v4 implemented, package qualification pending | Historical v3 qualification below cannot qualify v4; authoring/runtime v2 and private answers v1 are separate version axes |

## Package admission

[Packaging](PACKAGING.md) owns admitted surfaces, RIDs, package closure and
publication commands. [Definition v2 qualification](OPERATIONS.md#definition-v2-qualification)
and [cross-platform final packages](OPERATIONS.md#final-packages-and-actual-surfaces)
record actual local publications and their checks. Cross-publication, package
admission and execution on the target host are distinct. No prior package
result closes the pending import/comparison or machine v4 qualification.

## Executed surface and host

| Surface | Implemented capability | Observed host/scenario | Identity and execution record | Limit |
| --- | --- | --- | --- | --- |
| Desktop | Native interactive configuration and publication | Linux x64 X11 native guards, including English/Arabic observations | [Definition-v2 qualification](OPERATIONS.md#definition-v2-qualification), [retained identity](OPERATIONS.md#qa-matrix-and-retained-identity) | Does not qualify Terminal, every picker, another host or assistive technology |
| Terminal | Keyboard configuration and native publication | Actual published process at its recorded terminal dimensions, synthetic protected output | [Definition-v2 Terminal record](OPERATIONS.md#definition-v2-qualification), [final delivery](OPERATIONS.md#final-definition-delivery-and-responsibility) | Desktop X11 guard totals do not describe this process; full bidirectional shaping and assistive technology remain separate |
| Browser | Static local workspace and deliberate export | Published AOT desktop/mobile layouts, offline, RTL and custody exercises | [Definition-v2 Browser record](OPERATIONS.md#definition-v2-qualification), [final packages](OPERATIONS.md#final-packages-and-actual-surfaces) | Browser exports do not establish native file protection; recorded package identity matters |
| CLI | Offline machine preparation and publication | Standalone Linux x64 complete/incomplete and create-only conflict scenarios | [Recorded CLI qualification](OPERATIONS.md#definition-v2-qualification), [final delivery](OPERATIONS.md#final-definition-delivery-and-responsibility) | Earlier command-version/package evidence does not qualify current v4 additions |
| Browser performance | Revision-bound editing and rendering | Reference-host indexed-edit measurements | [Frozen candidate](OPERATIONS.md#frozen-indexed-edit-candidate-2026-10-23), [committed editor](OPERATIONS.md#committed-aot-editor-candidate-2026-10-24) | Candidate-specific, not a latency promise for later packages or devices |
| Additional native hosts/installers | See package-admission contract | Pending actual-host duties | [Host evidence follow-up](../../dev/backlog/configurator-native-host-evidence.md), [required duties](../../dev/backlog/cross-platform-quality-required-followups.md) | Cross-publication and Linux x64 passes are not execution on another host |

## Source/package identity and evidence availability

Source revisions, executable digests and Browser asset inventories identify
different things. Retain their relationships as recorded in
[native retained identity](OPERATIONS.md#qa-matrix-and-retained-identity),
[cross-platform final packages](OPERATIONS.md#final-packages-and-actual-surfaces)
and [v2 final delivery](OPERATIONS.md#final-definition-delivery-and-responsibility).
Do not substitute a source revision for an executed payload hash or relabel an
earlier cohort as the final package.

Tracked prose preserves the recorded observations and references. Paths under
`.tmp/`, `.omo/evidence/`, `TestResults/` and `artifacts/`, session handles and
temporary screenshots can refer to ignored, overwritten or unavailable local
material. Their present availability and original bytes were not inspected by
this documentation correction. A citation to that record is not independent
reproduction. Evidence reuse must distinguish the durable narrative, retained
identity and actually available underlying artifact; missing material stays an
explicit evidence limit rather than an inferred pass.

## Protected save

[Protected artifacts](PROTECTED_ARTIFACTS.md) owns create-only native publication,
installation/verification/cleanup facts and unsupported-host refusal.
[Native ownership qualification](OPERATIONS.md#native-ownership-qualification-2026-10-27)
and [v2 qualification](OPERATIONS.md#definition-v2-qualification) provide bounded
Linux evidence. Other hosts require their own
[host evidence](../../dev/backlog/configurator-native-host-evidence.md).
Deliberate Browser copy/download is not protected native save, and cancellation
does not retract an established installation.

## Accessibility

[Committed AOT editor evidence](OPERATIONS.md#committed-aot-editor-candidate-2026-10-24)
records named focus, reflow, RTL and actual browser-zoom observations;
[final surfaces](OPERATIONS.md#final-packages-and-actual-surfaces) and
[v2 qualification](OPERATIONS.md#definition-v2-qualification) record later scoped
inspection. These are not whole-product WCAG certification, screen-reader speech,
complete translation review or stakeholder usability evidence.
[Required duties](../../dev/backlog/cross-platform-quality-required-followups.md)
and [independent adoption trials](../../dev/backlog/definition-profile-target-adoption-trials.md)
retain those gaps.

## Releases

[Release runbook](RELEASE_RUNBOOK.md) and [release tooling](../../eng/release/README.md)
own local validation, immutable inputs and independent publication authority.
Local package production and engineering checks do not establish signing,
promotion, deployment, remote CI success or a published release. Historical
release-input failures remain in their dated Operations records; this index
neither repairs nor reclassifies release inputs.

## Target acceptance

[Definition runtime boundary](adr/ADR-definition-runtime-boundary.md) and the
[Core contract](CORE_DEFINITION_CONTRACT.md) retain target-owned semantic
validation and authorization. Complete preparation, synthetic byte equality,
Event reference projections and a completed comparison cannot establish that
an application accepted the result. [Independent target trials](../../dev/backlog/definition-profile-target-adoption-trials.md)
and [Event delivery](../../dev/backlog/guided-configuration-delivery-event.md)
remain separate evidence owners. No target apply/admin authority follows from
this index or any package pass.
