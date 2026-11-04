# Deployment-aware configuration — I-VSD planning assessment

Last Updated: 2026-11-03 Europe/Brussels

## Review Metadata

- Mode: planning
- Subject: Schema-defined deployment recipes, early component scope and environment destinations
- Workstream: deployment-aware-configuration
- Report kind: Provider-responsibility planning assessment
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-11-03
- Reviewed input: completed deployment-aware-configuration plan/context/tasks revision 1, Section 9 and task coverage mappings; shared investigation at Configurator `97c5a1789545b73a9536a4b9d4fa934976afb16c`; Event `b6ad1594b7` plus observed working-tree changes; user correction requiring component selection before editing
- Assessment revision: 1
- Supersedes: none; predecessor browser UX findings retain their own identities and evidence
- User approval: planning requested; product implementation not authorized by this report
- CTO review: not reviewed
- Planning revalidation: completed against triad revision 1 on 2026-11-03; all six finding/mitigation pairs have named scenarios and owned tasks or phase verification; findings remain open pending implementation evidence

## Executive Summary

An operator configuring only an API should not be asked for the Blazor host's
credentials or blocked by its unfinished settings. The recommended design starts
with all applicable components selected, exposes software-defined component scopes
before the form, and makes the chosen scope govern validation and prepared output.
For Event, Standalone remains the default deployment recipe. Separate-image and
Compose recipes can offer all, API-only and Blazor-only scopes; Standalone offers
its combined host. Detail levels change disclosure, not these obligations.

This gives maintainers responsibility for defining accurate component boundaries,
shared prerequisites, supported recipe/scope combinations and output mappings.
Selecting Blazor with an externally operated API must require its connection
address, not pull in API database or administrator credentials. Scope narrowing
must exclude unrelated values from complete and draft output while making clear
that previously entered inactive values remain in transient workspace memory.

The report recommends one admitted data contract and one Core effective-value
policy, shared by every client. Changes to recipe or scope must invalidate stale
preparation, import and save work. Role credentials must remain separated when
exports use the same runtime key for different recipients. Recipe defaults and
export labels must explain current target prerequisites rather than promise that
a prepared file proves a deployable or accepted system.

This assessment supports planning design reasoning and source traceability. It does
not approve implementation, qualify Event startup, establish user benefit, or
certify religious, security or accessibility outcomes. The completed revision-1
triad implements every mitigation in its proposed behavior, architecture and task
ownership, so the disposition is plan-aligned. The six findings remain open;
execution evidence must establish whether the planned mitigations work.

## Scope

Included: schema admission and public metadata, defaults, early component scope,
provider-dependent requirements, privacy of inactive answers, operation lifetime,
import context, destination-specific export, public guidance, accessible client
parity and maintainable offline boundaries.

Excluded: target administration/apply, hosted publication, secret-provider trust
activation, changes to Event runtime, licensing decisions, telemetry, monetization,
ranking and religious-content decisions. Event is first-party source evidence;
Configurator does not acquire an Event runtime dependency. Existing configuration
manifest behavior remains invariant.

## Claim Boundary

The principles below use the repository's selected I-VSD meanings. Amanah means
responsible stewardship of entrusted answers and preparation promises; Sidq means
truthful descriptions of scope, prerequisites and readiness. Non-Harm and Rights
of People support avoiding credential exposure and preserving deliberate user
choices. Adl and Ihsan require equivalent usable access and maintainable outcomes.

Evidence level 2 is coherent design validation. Level 3 is implementation
traceability from inspected source and existing test ownership, not executed
tests. Levels 4 and 5—stakeholder and operational validation—are absent. No
religious-legal ruling or certification is made.

## Findings

### IVSD-F001 — Component choice must reduce actual configuration work

An API-only operator can be burdened with unrelated Blazor settings even if a
later export button hides them. Asking for unnecessary credentials creates extra
custody and makes an apparently scoped experience misleading. The user explicitly
requires default all-applicable editing with an early software-defined narrower
scope. The provider controls that flow and the requiredness calculation; users
cannot repair an export-only implementation from the interface.

**IVSD-M001:** Declare compatible finite recipe/scope selections in the definition,
initialize Standalone plus all-applicable once through Core, and apply scope to
effective validation and output before rendering. Include declared shared
prerequisites. Do not infer a managed dependency from a connection reference:
Blazor-only may use an existing API without collecting its database credentials.
Basic/Advanced may hide optional detail but must reveal active required work.

- Lifecycle / severity: open / high.
- Claim type / validation: foreseeable design risk; levels 2 and 3.
- Principles / domains: Sidq, Amanah and Ihsan; design, technical, evaluation.
- Stakeholders / controlled decision: component operators and maintainers; initial
  selection, scope definition and requiredness.
- Evidence: E01–E04; selected-artifact composition currently validates the whole
  active definition, so export audience alone cannot supply early scope.
- Owner / next validation: Core and Presentation maintainers; prove API-only
  readiness with unfinished Blazor values, default all behavior, shared
  requirements and invalid recipe/scope rejection through public entrypoints.
- Escalation: an implementation that requires unrelated secrets or broadens
  scope silently requires correction before implementation acceptance.

### IVSD-F002 — Hidden values remain entrusted data

Switching from all components to API-only can leave previously entered BFF
credentials in memory. Current workspace snapshots intentionally retain inactive
supplied answers for switch-back. This is useful and avoids input loss, but hiding
a field does not clear it. A draft completion placeholder can also reveal that an
unselected credential exists if scope is applied inconsistently.

**IVSD-M002:** Preserve supplied shared and inactive values only within the current
transient workspace, describe retention accurately, and exclude out-of-scope
values and completion placeholders from every environment export. Consent
revocation must clear secret leaves across active and inactive branches and retire
prepared output. No answer-derived browser cache, persistent browser storage,
request or telemetry is added; deliberate restricted export remains available.

- Lifecycle / severity: open / critical.
- Claim type / validation: privacy invariant and foreseeable extension risk;
  levels 2 and 3.
- Principles / domains: Amanah, Rights of People, Non-Harm and avoiding Tajassus;
  design and technical.
- Stakeholders / controlled decision: local operators and people whose service
  credentials they administer; retention, masking, consent and artifact contents.
- Evidence: E03, E05 and E09; inactive-answer retention and recursive privacy
  boundaries exist, but no new component-scope tests have executed.
- Owner / next validation: Presentation and Browser maintainers; failing
  invariant tests using synthetic sentinels across scope switches, draft/complete
  outputs, revoke and actual browser cache/network boundaries.
- Escalation: persistence or answer-bearing transport is outside this plan and
  requires renewed provider-responsibility and user-scope review.

### IVSD-F003 — Exports must preserve recipient and authority boundaries

An API and BFF can use identically named runtime variables for different
identities. Combining those files into one runnable environment can overwrite an
identity or expose a privileged credential to another process. Compose inputs
also have names that are translated before reaching a process. Prefix filtering
cannot establish those relationships.

**IVSD-M003:** Use admitted per-destination projections from one answer tree,
explicit consumer/surface metadata and tested literal serializers. Shared
non-secret answers may feed several consumers; independent credentials remain
independent. Reject conflicting keys. Copy-all preserves named file boundaries;
multi-file download preserves individual files. Compose all produces its single
input file. No export action implies signer promotion or target authority.

- Lifecycle / severity: open / critical.
- Claim type / validation: security/privacy design risk; levels 2 and 3.
- Principles / domains: Amanah, Rights of People and Non-Harm; technical,
  operational and governance.
- Stakeholders / controlled decision: self-hosters, service operators and target
  maintainers; destination mapping, serializer and aggregate export behavior.
- Evidence: E06–E08; Event documents direct-process versus orchestration identity
  inputs; the existing codec rejects some legitimate credential characters.
- Owner / next validation: Core and client export maintainers; prove API/BFF
  separation, duplicate rejection, output dialect round trips, restricted archive
  ownership and zero unrelated values in each export.
- Escalation: changes to external trust activation or credential distribution
  beyond local preparation require renewed review before release.

### IVSD-F004 — A scope change must revoke stale work immediately

If a preparation, import comparison or save started for all components completes
after an API-only selection, the new workspace could display or publish BFF
credentials. Similarly, silently replacing an incompatible scope on a recipe
change would alter the user's intent without a deliberate choice. Recipe and
scope form one valid context, not unrelated browser view fields.

**IVSD-M004:** Route context edits through the existing revision/generation owner.
Accept a valid transition atomically and revoke old output eligibility before
asynchronous cleanup. Reject incompatible recipe/scope transitions without
changing answers or current context and explain valid alternatives. Imports and
document edits use explicit captured context and reject conflicts or stale
revisions; never infer deployment intent from a few file keys.
Already installed native files and bytes deliberately copied or downloaded cannot
be recalled. Preserve truthful installation receipts and revoke eligibility for
new adoption/exposure rather than promising destructive rollback.

- Lifecycle / severity: open / critical.
- Claim type / validation: concurrency/privacy invariant; levels 2 and 3.
- Principles / domains: Non-Harm, Amanah and Rights of People; technical and
  design.
- Stakeholders / controlled decision: operators switching recipes/components;
  transition atomicity, operation validity and import interpretation.
- Evidence: E03–E05; existing input mutation retires documents/imports and Browser
  output, while inverse import currently supports one scalar selector only.
- Owner / next validation: Core/Presentation and native/browser adapter
  maintainers; bounded event-driven race tests for prepare, import, edit, revoke
  and save, including immutable old-state preservation on invalid selection.
- Escalation: no acceptance while a stale completion can expose wrong-scope
  output; do not suppress the failing invariant or narrow its test oracle.

### IVSD-F005 — Defaults and readiness claims require target evidence

Standalone is a simpler host arrangement, but it is not evidence that external
session or key authority requirements disappear. Current Event and Configurator
catalogues differ, and current target files include uncommitted corrections.
Operators could export a syntactically valid file that omits a prerequisite or
uses a Compose-only alias for direct runtime configuration.

**IVSD-M005:** Reconcile public Event metadata against owning binders and record
reviewed target revision/version and default origin. Distinguish inherited image
defaults, deliberate overrides, external prerequisites and complete/deferred
scope readiness. Preserve the manifest contract. Label component-only Compose
exports as scoped inputs, not a complete Compose deployment. A prepared artifact
never claims target acceptance or successful deployment.

- Lifecycle / severity: open / high.
- Claim type / validation: documentation/contract drift and bounded-claim risk;
  levels 2 and 3.
- Principles / domains: Sidq, Promise-Keeping, Amanah and avoiding deception;
  operational, design and governance.
- Stakeholders / controlled decision: self-hosters and maintainers; source
  inventory, prerequisite copy, default selection and qualification claims.
- Evidence: E01, E06 and E08; mandatory secret-provider selection and
  replica-dependent setup-secret behavior differ from coarse inclusion flags.
- Owner / next validation: Event-definition maintainers; reconcile actual field
  coverage and independent expected outputs, distinguish working-tree/published
  facts, and verify qualified guidance before release.
- Escalation: missing authoritative target semantics blocks the affected output
  mapping; it does not authorize guessed defaults or fabricated green evidence.

### IVSD-F006 — Equivalent scope control must remain accessible and portable

A schema-defined API-only flow would offer unequal practical access if it existed
only in one browser widget or required memorizing technical keys in another
client. A complicated filter that moves focus, hides required fields or loses
selection under locale changes imposes additional burden on keyboard, Arabic/RTL
and assistive-technology users. Provider maintenance choices also determine
whether other software definitions can use the feature without Event-specific
branches.

**IVSD-M006:** Keep public metadata and effective behavior in Core, selection and
operation state in Presentation, and client adapters thin. Render labelled early
selectors, explain available combinations, keep focus and status coherent, and
preserve typed values across English/Arabic changes. Provide matching CLI,
Terminal and Desktop semantics. Use existing wrappers and standard library
capabilities; no new backend, dependency or telemetry is justified by this flow.

- Lifecycle / severity: open / high.
- Claim type / validation: accessibility, portability and maintenance risk;
  levels 2 and 3.
- Principles / domains: Adl, Ihsan and Promise-Keeping; design, technical and
  evaluation.
- Stakeholders / controlled decision: keyboard/AT users, Arabic/RTL users,
  machine-client users and future definition authors; control semantics,
  localization, dependency direction and public schema.
- Evidence: E02, E03 and E09; existing disclosure/localization and immutable
  workspace owners are reusable; new selections have no rendered evidence yet.
- Owner / next validation: Presentation and client maintainers; public-contract
  client parity plus actual keyboard, focus, narrow/wide, RTL and export checks.
- Escalation: accessibility qualification remains bounded to executed surfaces;
  automation cannot be described as certification or stakeholder validation.

## Recommendations

Adopt IVSD-M001–IVSD-M006 together. Keep one declared answer context, one
effective-value interpretation and destination-specific projections. The
completed revision-1 plan mapping is recorded in Planning Handoff below. Preserve finding IDs when
implementation evidence is added; an implemented mitigation does not itself
close its stakeholder or operational evidence gap.

Rejected alternatives: export-only audience selection does not reduce form or
validation burden; UI-only hiding leaves Core/CLI behavior inconsistent; silently
resetting incompatible context changes intent; deleting all inactive values loses
switch-back work; duplicating answers for each destination permits divergence;
concatenating files mixes role identities; inventing defaults from names or old
catalogue flags creates false readiness; introducing an Event backend or new
dependency undermines the offline boundary without meeting a required need.

## Stakeholders

| Stakeholder | Benefit sought | Responsibility or exposure |
|---|---|---|
| Full-stack self-hoster | Useful Standalone/all default and complete applicable view | Must still provision actual target prerequisites |
| API-only operator | API configuration without BFF credential collection | Shared API prerequisites remain visible |
| Blazor-only operator | Configure connection to an existing API | External API ownership does not grant its secret authority |
| Keyboard, AT and Arabic/RTL users | Equivalent early selection, correction and export | New controls need rendered/manual evidence |
| Definition maintainers | Reusable metadata with bounded admission | Own accurate target mapping, provenance and defaults |
| Configurator maintainers | One enforceable offline behavior across clients | Own custody, operation lifetime and truthful readiness |
| People relying on configured services | Avoid downstream credential leakage or misconfiguration | Indirectly affected; not represented by empirical research here |

## I-VSD Principles And Domains

Trust and Rights constrain answer retention, scope and recipient authority.
Non-Harm supports stale-result fencing and correct serialization. Truthfulness
and Promise-Keeping require precise prerequisite/readiness claims. Justice and
Excellence require accessible client parity and maintainable ownership.

Strategic scope is limited to avoiding unnecessary service/dependency burden.
Design and technical domains apply directly; operational and governance domains
apply to source freshness and release claims; evaluation remains explicitly
incomplete. Riba, monetization, religious-content moderation and contested
religious obligations are not implicated by the reviewed change.

## Validation Gaps

No new product behavior has been implemented or tested. New scope/default,
consumer isolation, import-context, serialization, stale-operation and
accessibility scenarios require execution in their affected owners. Source
inspection is not a passing suite. No comparative usability study, production
incident evidence, target startup/login qualification, independent security audit
or assistive-technology qualification was reviewed for this workstream.

## Escalation Needed

No religious-legal decision or new scholarly escalation is identified. Technical
acceptance must stop if the selected scope cannot be isolated safely, a role
credential reaches the wrong output, or a stale operation remains eligible.
Missing authoritative Event semantics blocks that mapping until its owner
resolves it. Publication, trust activation, answer persistence or target apply
would exceed this report and require a fresh user-scope/provider review.

## Evidence Reviewed

| ID | Locator and inspected meaning | Evidence limit |
|---|---|---|
| E01 | User request and correction: default all applicable form values, early software-defined deployment/component selection, Standalone default recipe, breaking changes permitted | Product direction; not stakeholder outcome research |
| E02 | `src/Configurator.Core/Definitions/DefinitionProfile.cs:17`, `DefinitionAdmission.cs:14`, `DefinitionPresentation.cs:5`: closed capabilities and admitted data-only metadata; journeys are presentation | Source inspection at Configurator HEAD above |
| E03 | `src/Configurator.Presentation/Form/DefinitionFormModel.cs:103,819,833,936`: answer-bound selection, inactive snapshot retention and shared revision invalidation | Existing behavior traceability; no new scope implementation |
| E04 | `src/Configurator.Core/Artifacts/DefinitionArtifactComposer.cs:35,60`, `DefinitionBundleComposer.cs:8`: selected outputs still use complete active-definition validation | Explains export-only scope limitation |
| E05 | `src/Configurator.Core/Definitions/DefinitionImportProjection.cs:49,75`, `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs:395`: inverse-selector limitation and input invalidation | New multi-selector behavior remains planned |
| E06 | Event `src/Explore.Blazor/Extensions/HttpClientExtensions.cs:37`, `.env.example:69`, `docker-compose.yml:340,599`: combined transport and explicit runtime/Compose identity mappings | Read-only target evidence; Event has local changes |
| E07 | `src/Configurator.Core/Dotenv/DotenvCodec.cs:291,297,312`: current value rejection/quoting; `src/Configurator.Browser/Components/Configuration/ConfigurationFiles.razor:29`: explicit per-file copy/download | New dialects/aggregate export unimplemented |
| E08 | Event `src/Explore.ServiceDefaults/Secrets/SecretAuthoritySelection.cs:25`, `src/Explore.Infrastructure/Services/SetupSecretProvider.cs:57,146`; maintained Event source `examples/islamu-event/configurator.json:14587,14636` | Authoritative target reading; no target runtime executed |
| E09 | `AGENTS.md` invariants 2–7, `.agents/rules/privacy-and-pii.md`, `PROJECTS.md` ownership; I-VSD principles/evidence, UX/defaults and architecture resources | Governing constraints and design reasoning, not certification |
| E10 | Completed [plan](../../dev/active/deployment-aware-configuration/deployment-aware-configuration-plan.md) revision 1 Sections 3, 5, 7 and 9; [tasks](../../dev/active/deployment-aware-configuration/deployment-aware-configuration-tasks.md) revision 1 Phases 1–5 and coverage table; [context](../../dev/active/deployment-aware-configuration/deployment-aware-configuration-context.md) revision 1 decisions, risks and authority | Revalidated behavior/task alignment only; checkboxes are unexecuted |

Target locators beginning `Event` refer to the separately inspected first-party
checkout and are evidence coordinates, not build or runtime inputs. External
technical research in the implementation plan must retain its own primary-source
URLs and evidence dates; this report does not import third-party implementation.

## Missing Evidence

Missing are new runtime tests, generated-contract freshness,
consumer/dialect qualification, real-surface accessibility evidence, actual target
startup evidence, stakeholder feedback and longitudinal operational data. None is
inferred from the present source inventory.

## Context Inventory

The shared packet covers Configurator Core, Presentation, current clients,
maintained Event example/store content, generation owners and Event deployment
source, plus the completed revision-1 triad's scenario/architecture/task mappings.
Existing browser UX and schema-secret assessments remain separate; this
report does not reapprove their incomplete evidence. The planner owns the triad;
this report owns provider-responsibility analysis and freshness only.

## Common Overlooked Failures And Outcomes

Narrowing the displayed form without narrowing validation; importing a file under
the wrong scope; retaining wrong-scope draft placeholders; treating a referenced
external service as a managed dependency; exporting Compose aliases directly;
merging independent identities; presenting component-only readiness as whole
deployment readiness; moving focus after a selector change without explanation;
and silently resetting an incompatible selection are all foreseeable failures.
Avoiding them is a planned outcome to verify, not a measured result.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
|---|---|---|---|---|
| 2026-11-03 | none | draft / ready-for-planning | Shared investigation and corrected early-scope requirement | Configurator HEAD and user direction named above; final triad mapping pending |
| 2026-11-03 | draft / ready-for-planning | current / plan-aligned | Revalidated completed triad revision 1 | E10; six finding/mitigation pairs mapped below; all findings remain open |

## Planning Handoff

- Workstream: deployment-aware-configuration
- Status: current
- Reviewed input: completed deployment-aware-configuration plan/context/tasks revision 1 and shared packet, revalidated 2026-11-03
- Findings and mitigations: IVSD-F001 → IVSD-M001; IVSD-F002 → IVSD-M002;
  IVSD-F003 → IVSD-M003; IVSD-F004 → IVSD-M004; IVSD-F005 → IVSD-M005;
  IVSD-F006 → IVSD-M006
- Required plan mappings: all six pairs are owned by the following revision-1
  scenarios/tasks; no finding is deferred to an unowned future phase.
- Escalations required before: affected implementation acceptance for unsafe
  scope/recipient/stale-output behavior; release for missing target truth claims;
  renewed planning for out-of-scope authority/custody changes.
- Refresh triggers: changes to default recipes/scopes, dependency semantics,
  inactive retention or consent, export recipients/dialects, import context,
  operation invalidation, external services, target qualification claims,
  accessibility/parity commitments, or mapped scenarios/tasks.
- Approval state: planning requested; user approval for implementation is awaiting
  a subsequent instruction; CTO not reviewed. No product implementation,
  publication or target-authority approval follows from planning alignment.

| Finding / mitigation | Observable scenarios | Named implementation or verification ownership |
|---|---|---|
| IVSD-F001 / IVSD-M001 | S01–S04, S10, S18: initialized and early component scope, connection-only dependencies, display separation, scope completeness and generic software support | TASK-1.1–1.3, TASK-3.3, TASK-4.2 |
| IVSD-F002 / IVSD-M002 | S06, S16: inactive secret retention/exclusion, revoke and offline custody | TASK-3.1–3.3, TASK-4.1–4.3 |
| IVSD-F003 / IVSD-M003 | S08, S09, S14: recipient-preserving exports, literal dialects and bounded admitted metadata | TASK-2.1–2.3, TASK-4.3 |
| IVSD-F004 / IVSD-M004 | S05, S07, S11, S17: atomic context, stale result fences, explicit import context and fail-closed input | TASK-1.1–1.2, TASK-3.1–3.2, TASK-4.1 |
| IVSD-F005 / IVSD-M005 | S10, S12, S13: scoped readiness claims, target-backed prerequisites/defaults and manifest invariance | TASK-2.1–2.3, VERIFY-5 |
| IVSD-F006 / IVSD-M006 | S04, S15, S18: display/locale separation, accessible client parity and generic schema support | TASK-3.3, TASK-4.2–4.3, TASK-5.1–5.3, VERIFY-5 |

The revalidation checked that scope is enforced before obligations through the
existing effective-value kernel; global malformed-input checks remain active;
ordinary answer keys are the sole machine selection channel; inactive secrets
and old operations cannot re-enter newly eligible output; destination metadata
does not grant runtime authority; and client/native/browser evidence is assigned
without inheriting predecessor qualification. The reviewed plan retains
first-party Event as read-only evidence and requires an independently maintained
current mapping inventory before coverage claims. These are design commitments,
not passing implementation results.
