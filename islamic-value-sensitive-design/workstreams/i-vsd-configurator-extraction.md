# ISLAMU Configurator Extraction - I-VSD Planning Assessment

Last Updated: 2026-10-08

## Review Metadata

- Mode: planning
- Subject: Standalone configuration runtime and its approved extraction from ISLAMU Event
- Workstream: configurator-extraction
- Report kind: Provider-responsibility planning assessment
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-10-08
- Reviewed input: configurator-extraction-r2 plan/task/context triad and E01-E15;
  Event working tree based on `8633a693a290afb388805616e4f893d189100c44`;
  Configurator initial checkout `468b298`
- Supersedes: none

## Implementation Evidence

The planning analysis below is retained as historical reasoning, not a current
claim that no implementation exists. This section records the observed
implementation evidence against the same finding and mitigation identifiers.
Standalone engineering readiness preceded the verified Event source cutover.
The observed scope below does not imply broader host or stakeholder evidence.

| Finding / mitigation | Current disposition | Observed implementation evidence |
| --- | --- | --- |
| IVSD-F001 / IVSD-M001 | resolved within offline scope | Browser: 57 passing cases cover bounded launch selection, pinned revision/digest, rejected private inputs and registry refresh integrity; Event live transport remains in its client, committed a6b5fba7. No launch token, target authentication or remote apply is introduced. |
| IVSD-F002 / IVSD-M002 | resolved on verified Linux host | Core: 63, Artifacts: 14 and Presentation: 20 passing cases cover sensitivity floors, competing create-only writes and generation fencing. Terminal: 35 and Desktop: 23 passing cases preserve masked editing and output invalidation. Non-Linux restricted saves remain disabled. |
| IVSD-F003 / IVSD-M003 | resolved for admitted transfer scope | File transfer manifest and explicit Steward authority separate ISLAMU ownership from third-party rights. The 154 incompatible example-corpus files are excluded; the approved Terminal.Gui archive and 21 component hashes remain intact. Native dependency policy admitted 83 package/version pairs; complete restored-owner vulnerability audit exited 0. |
| IVSD-F004 / IVSD-M004 | resolved within verified extraction scope | Three native packages and independent static browser refreshed successfully; Architecture: 35 package/closure cases passed. Actual CLI output matched all five Event vectors and Event host validators accepted them. Release engineering passed 405 cases with inert fixture closure; the later secret-boundary classes passed 24 and 11 cases. Locked destination restore, whole Release build and formatting passed before Event removed 201 literal source/tool files and updated 33 owned paths. Event Release build passed with zero errors and 2,650 warnings; its architecture suite preserved 660 unchanged passes and the corrected three-case CI class passed. Governed staged preflight accepted the populated operator-impact fragment. |

Browser rendering was observed at 1440x1000 and 390x844 without horizontal
overflow. A real 120x40 terminal exposed a zero-height button content frame;
disabling native shadows through the null contract restored visible controls,
and initialized geometry now has a regression assertion. Desktop was observed
on an isolated virtual display, with separated footer controls and selected
mode/artifact values. These checks do not establish screen-reader, braille, RTL
shaping, broad stakeholder usability or immutable managed-string erasure.

Independent privacy and architecture reviews found registry history, CLI
test-oracle and stale exporter-lock defects. The first is fixed in 6816d36, the
second in d30d1ea, and the lock was regenerated natively with Event-only project
references. Anonymized findings are recorded in
[the boundary review](../../docs/internal/extraction/boundary-review.json).
The runtime ADR and narrowly scoped future-host/publisher-distribution backlogs
preserve ownership without deferring required extraction behaviors.

No ethical outcome, legal certification, scholarly ruling, signed publication
activation or non-Linux protected filesystem guarantee is inferred from these
automated and visual checks.

## Executive Summary

Extracting the setup tools can give self-hosting operators a reusable utility
without making another application depend on Event's backend. The requested
destination already contains an MIT license. Existing native artifact protection,
public-only browser behavior, and separate UI targets provide useful boundaries
to preserve rather than rebuild.

The current engine is not yet a general-purpose runtime. It imports Event wire
contracts and compiles Event environment, tenant, identity and legal knowledge.
The selected boundary expresses target-specific work through external
definitions and Event-side validation, rather than compiled target modules in
Configurator. The question timed out and the user instructed continuation by
best judgment; this is an explicitly labeled planner default. It matches the
requested independent utility and keeps administration with the host that
grants it, while requiring replacements for existing artifact workflows.

This assessment is current and plan-aligned with revision r2. The triad maps
all four findings and mitigations to named scenarios, tasks and release gates.
Plan alignment means the design addresses the obligations; it does not prove
implementation, operational, accessibility, licensing or scholarly outcomes.

Revision r2 incorporates the destination internal design/strategy documents and
the user's explicit order and naming correction: transfer original projects
to Configurator first, then rename all owned projects to Configurator.* there.
Preserve existing destination documentation. Family GUID, compatibility,
publisher evidence and session-pinned revision/digest are distinct claims;
a GUID is not trust. Hosted catalog/distribution and browser private entry
remain separately scoped future work rather than implicit extraction features.

## Scope

Covered: extraction ownership, user-controlled configuration preparation,
CLI/TUI/Avalonia/Blazor boundaries, sensitive output, deep-link navigation,
dependency provenance, MIT redistribution, truthful documentation and safe
source removal.

Excluded: changing Event authentication, tenancy, database authority, enrollment
authorization, deployment services or its public license; introducing remote
provisioning or automated application administration; religious-legal rulings.

## Claim Boundary

Evidence is repository inspection only. Test source establishes what assertions
exist, not that they pass. Destination Git inspections establish its initial
state, not its future independence. A requested MIT license is an architectural
and governance direction, not proof of every copied file's rights.

I-VSD provides provider-responsibility reasoning. Qualified legal and scholarly
authorities retain their respective authority; this report certifies neither.

## Findings

### IVSD-F001: A launch link must not become administration authority

An operator following a link from Event must know whether they are preparing
configuration locally or granting access to their installation. Navigation
must not quietly transfer a bearer token, setup secret, tenant capability or
private artifact to another origin. The provider controls the launch URL,
definition resolver and supported behaviors; the operator cannot repair a
credential-bearing redirect after the secret has leaked.

Current source separates offline clients from the Event-generated SetupLive
adapter, and the live capability remains disabled. Preserve this distinction
when defining `/launch/<familyId>`. The recommended launch identifier selects a
bounded public target definition, not a session or privileged capability.
Event remains the authority for validating and applying Event configuration.
Unknown or hostile identifiers must reject without network authority or
credential fallback.

Trust/Amanah applies because an operator entrusts deployment configuration to
the utility. Non-Harm and Avoiding Spying require that this convenience not
create a new cross-origin credential or tracking channel.

| Field | Assessment |
| --- | --- |
| Lifecycle / severity / claim type | open / high / design obligation |
| Domains / stakeholders | technical, design, operational / operators and target administrators |
| Controlled decision | Meaning and resolution of launch identifiers; host validation ownership |
| Evidence / validation level | E05, E07-E09 / source inspection |
| Mitigation | IVSD-M001: public definition selection, no credential-bearing URLs, no administration authority from launch |
| Owner / next validation | Runtime maintainer; definition-only boundary selected, adversarial public-seam implementation tests remain required |
| Escalation boundary | New remote fetch, authentication or live apply behavior requires renewed scope and security review |

### IVSD-F002: Extraction must retain confidential artifact protections

Configuration drafts can contain secrets and operator identity even when the
utility never contacts a backend. A generic definition must not be allowed to
relabel private answers as public merely by selecting a convenient export kind.
Copying a renderer while weakening this rule would harm the very operators
using it to simplify deployment.

The existing browser accepts only a closed public format, while restricted
native output uses a create-only protected writer. Linux output fails closed
when filesystem guarantees cannot be proved; restricted Windows/macOS Save is
disabled. The maintainer controls these defaults, input lifecycle and release
claims. Extraction must preserve them, including generation fencing and
clearing prepared private output after edits, failures or disposal.

Rights of People protects privacy and property in the operator's configuration.
Non-Harm requires preserving files rather than overwriting them during a failed
save. Truthfulness/Sidq requires documenting the actual supported-host matrix
instead of describing a cross-platform UI framework as cross-platform protected
file support.

| Field | Assessment |
| --- | --- |
| Lifecycle / severity / claim type | accepted / high / preservation requirement |
| Domains / stakeholders | technical, design, operations, evaluation / operators and people represented in drafts |
| Controlled decision | Public/native artifact classification, output destinations, buffers and host support claims |
| Evidence / validation level | E02, E03, E09-E10 / inspected implementation and test source; unexecuted |
| Mitigation | IVSD-M002: preserve restricted-by-default classification, protected create-only output and browser public-only behavior |
| Owner / next validation | Core, artifact and renderer maintainers; deterministic boundary and competing-write tests |
| Escalation boundary | Expanding browser secret entry or enabling a new host's restricted Save requires new evidence and review |

### IVSD-F003: Repository reuse must preserve rights and truthful attribution

The user wants substantial repository scaffolding reused and Configurator
licensed under MIT. A copied LICENSE or renamed namespace cannot establish
ownership of every dependency, documentation example or asset. Incorrect
attribution could burden maintainers and downstream adopters with obligations
they were told did not exist.

The destination's MIT license is verified. Event's public license and covenant
remain Event-specific; they must not become accidental Configurator defaults.
For material actually transferred, record repository ownership/contribution
authority and retain third-party terms and notices. The `.agents` tree includes
a sizeable GitBook example corpus requiring an explicit provenance disposition.
The audited Terminal.Gui dependency includes a local package feed and narrow
approval closure; replacing it casually with upstream would discard reviewed
dependency constraints.

Rights of People covers intellectual property and attribution. Promise-Keeping
and Truthfulness require that the public MIT claim describe ISLAMU-owned
Configurator material without falsely granting MIT rights over every bundled
component.

| Field | Assessment |
| --- | --- |
| Lifecycle / severity / claim type | open / high / provenance and redistribution obligation |
| Domains / stakeholders | governance, strategic, technical / contributors, maintainers and adopters |
| Controlled decision | Transfer inventory, outbound metadata, notices and dependency closure |
| Evidence / validation level | E12-E13; Terminal.Gui dependency-review.md and setup-assistant-ui.md / repository records only |
| Mitigation | IVSD-M003: file-level transfer provenance, destination MIT metadata, preserved component terms, audited dependency feed |
| Owner / next validation | Project Steward and tooling maintainer; rights inventory before redistribution and destination license scan |
| Escalation boundary | Unclear contributor rights or incompatible third-party terms block that transfer/release pending qualified review |

### IVSD-F004: A standalone promise needs a working handoff before source removal

Removing the Event tools before Configurator can build and cover its workflows
would leave operators without the promised utility. A large blind copy would
also leave maintainers with Event-specific commands, nonportable paths and
release machinery that looks configured but cannot safely run.

The provider controls extraction order, source deletion, guides and inherited
agent instructions. Destination readiness must precede Event removal. The copy
inventory must distinguish reusable workflows from Event services, local
credentials and signing/deployment identities. README and both public/internal
guides must describe the same command and launch contract. Accessible keyboard,
focus and value-safe diagnostic behavior must survive renderer changes.

Promise-Keeping and Excellence/Ihsan apply to continuity and maintainability.
Justice/Adl applies to retaining usable terminal and accessible UI paths rather
than forcing every operator into one interaction model.

| Field | Assessment |
| --- | --- |
| Lifecycle / severity / claim type | accepted / medium / migration and usability obligation |
| Domains / stakeholders | operations, design, technical, evaluation / operators and future maintainers |
| Controlled decision | Extraction sequence, copied instructions, documentation and renderer parity |
| Evidence / validation level | E04, E10-E12; destination initial state / source inspection |
| Mitigation | IVSD-M004: verified destination before Event removal, complete copy/adapt/exclude ledger, renderer contract preservation and documentation parity |
| Owner / next validation | Migration and documentation maintainers; standalone builds, focused suites and source dependency closure |
| Escalation boundary | Missing replacement for an existing workflow blocks source removal; unrelated test rot remains quarantined |

## Recommendations

Implement the data-definition boundary, preserving artifact preparation while Event
owns semantic acceptance and application. The alternative of isolated compiled
target modules can preserve specialized behavior more directly, but expands
Configurator's scope and makes its general-purpose claim harder to maintain.
The timeout/default decision is recorded rather than described as user approval.
The complete r2 mapping below now fixes the affected architecture and tasks.

Retain existing privacy restrictions, dependency identities and supported-host
limits. Establish rights and dependency closure before redistribution. Copy the
requested agent workflow inventory, then adapt its governing paths and commands;
do not copy deployment identities or pretend Event-specific rules govern the
new utility.

Reject a mechanical rename as sufficient decoupling: it leaves typed Event
contracts and platform catalogues in the supposedly generic engine. Reject
immediate source deletion: a destination repository containing only a license is
not a replacement.

## Stakeholders

- Self-hosting operators preparing secrets, environment files and identity drafts.
- Event administrators retaining backend mutation and validation responsibility.
- Other application maintainers supplying definitions for the utility.
- Contributors and third-party rightsholders whose material may be copied.
- Terminal, keyboard, screen-reader and localized-interface users.
- Configurator maintainers responsible for release and support claims.

## I-VSD Principles And Domains

Applied principles: Trust/Amanah, Truthfulness/Sidq, Non-Harm, Rights of People,
Promise-Keeping, Justice/Adl, Excellence/Ihsan and Avoiding Spying.
Each finding explains its concrete connection rather than inferring outcomes
from the principle name. Strategic and governance concerns cover ownership and
independence; design and technical concerns cover artifacts and interaction;
operations and evaluation concern migration and evidence.

Payments, riba, sponsorship, moderation, ranking, appeals and religious-content
decisions are not applicable to this extraction scope.

## Common Overlooked Failures And Outcomes

- Namespace rename leaves Event.Wire.Contracts in the build closure.
- Public launch URLs carry a private configuration payload or capability.
- Definitions select a public output kind for confidential input.
- Native saves fall back to overwrite or unprotected output on unsupported hosts.
- TUI restore relies on a feed only available in the sibling Event checkout.
- Copied hooks require Event release projects or import live trust material.
- Adapted README retains unsupported platform, deployment or license claims.
- Source removal deletes backend onboarding or security assertions that belong
  to Event rather than the extracted offline utility.

## Validation Gaps

No product builds, tests, restores, publishes or operational/UI checks were run.
The generic definition format and launch contract are not implemented.
File-level transfer ownership has not been audited. Stakeholder usability and
accessibility evidence is not available. The knowledge graph was unavailable;
project/source searches supply a bounded substitute, not exhaustive proof.

## Escalation Needed

The target-workflow boundary is resolved by the recorded planner default.
Before redistribution, escalate any unclear contribution or third-party rights
to the Project Steward and qualified legal review. No religious-legal ruling is
needed for the presently bounded utility design.

## Evidence Reviewed

E01-E15 are recorded in
[the extraction context](../../dev/active/configurator-extraction/configurator-extraction-context.md).
Owning sources include the seven portable project files, nested SetupLive
project, Core composition/catalogue sources, BrowserPublicManifest and its
secret-boundary tests, the setup architecture and configuration guides,
release project, root build/NuGet settings, and dependency records.
Both repository Git states were inspected locally.
Destination sources E14/E15: docs/internal/universal-configurator.md and
docs/internal/go-to-market.md. Their architectural recommendations were read;
external research claims were not independently reverified. The existing
untracked documents remain user-owned and unchanged.
Revision-r2 design inputs additionally reviewed:

- [Plan, including scenarios S1-S18 and Section 9 mappings](../../dev/active/configurator-extraction/configurator-extraction-plan.md).
- [Task ledger, including boundary tests, cutover gates and commit contracts](../../dev/active/configurator-extraction/configurator-extraction-tasks.md).
- [Context, including the timeout/default decision and evidence limits](../../dev/active/configurator-extraction/configurator-extraction-context.md).

## Missing Evidence

Exact transfer rights inventory; destination
dependency and runtime closure; passing automated implementation evidence;
supported-host and stakeholder evaluation.

## Context Inventory

User instructions establish MIT, target location, substantial scaffolding
reuse, all four clients, `.slnx`, and Blazor rather than Avalonia web.
Repository evidence establishes existing Avalonia/Blazor projects, substantial
Event wire coupling, public-only browser output and disabled live authority.
Data-only target resolution is the selected planner default after timeout, not a
confirmed user preference or approval of implementation. All required artifact
workflows remain in scope through definitions; none is silently discarded.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence |
| --- | --- | --- | --- | --- |
| 2026-10-08 | none | draft | Shared discovery completed; boundary question pending | E01-E13 |
| 2026-10-08 | draft | current | Timeout/default resolved; completed triad revalidated | configurator-extraction-r1, S1-S18 and tasks 1.1-10.3 |
| 2026-10-08 | current | current | Destination context and user naming/order correction | configurator-extraction-r2, E01-E15, S1-S18 and tasks 1.1-10.3, including 1.4 |

## Planning Handoff

- Workstream: configurator-extraction
- Status: current
- Disposition: plan-aligned
- Reviewed input: configurator-extraction-r2 and E01-E15, 2026-10-08
- Findings and mitigations: IVSD-F001 -> IVSD-M001; IVSD-F002 -> IVSD-M002;
  IVSD-F003 -> IVSD-M003; IVSD-F004 -> IVSD-M004
- Required plan mappings:
  - IVSD-F001 / IVSD-M001: S3, S5-S6, S11-S13; tasks 2.1-2.3,
    8.1-8.3 and 9.1-9.2.
  - IVSD-F002 / IVSD-M002: S7-S10, S14-S15; tasks 2.1, 3.1-3.2,
    5.1-5.2, 6.1, 7.1 and 8.1-8.2.
  - IVSD-F003 / IVSD-M003: S16, S18; tasks 1.1-1.4, 6.1 and 10.1.
  - IVSD-F004 / IVSD-M004: S1-S2, S4, S16-S17; tasks 1.1-1.4,
    4.1-8.3 and 9.1-10.3.
- Escalations required before: unresolved transfer rights before redistribution;
  new remote authority, public secrets or unsupported private Save before
  expanding the current implementation scope.
- Refresh triggers: compiled target-module choice, browser secret entry, remote
  launch resolution, live apply, dependency replacement, new supported hosts,
  altered license/provenance treatment, or changed mitigations.
