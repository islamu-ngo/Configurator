# Native CLI Adoption and Trustworthy Automation - I-VSD Review

Last Updated: 2026-10-15 Europe/Brussels

## Review Metadata

- Mode: planning
- Subject: Configurator CLI authoring, preparation, filesystem custody and agent adoption
- Workstream: cli-native-adoption
- Report kind: provider-responsibility design review
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-10-15
- Reviewed input: cli-native-adoption; CLI evidence at `74e994bf855098bc1018bb4790bd5327f853bba3`, documentation reconciliation at `775c652a02584cac994498a2cc5f4daee2bfdce5`
- Supersedes: none

## Executive Summary

Maintainers should be able to use one portable skill and native CLI without
trusting an agent's guesses, losing concurrent source edits, or receiving a
failure that conceals a successful private-file write. The CLI already keeps
preparation separate from target acceptance and uses strong Linux protected
publication. The workstream must preserve those boundaries while correcting
reporting, public authoring filesystem operations and adoption feedback.

This review identifies eight provider responsibilities. None is a claim that
software admission establishes religious, legal, operational or target approval.
Implementation and native-host evidence remain outstanding.

## Scope

The scope is public integration authoring, local answer preparation, command
discovery, bounded machine results, native filesystem custody, cancellation,
portable agent guidance, packaging and adopter CI. There is no target apply,
target authentication, answer-bearing networking, telemetry or executable
definition authority.

The user selected create-only revision candidates on 2026-10-15. Normal source
editing and candidate adoption remain contributor-controlled.

## Claim Boundary

This is an engineering/provider-responsibility assessment using the repository's
I-VSD framework. It is not a fatwa, legal opinion, Shariah certification, security
certification or release approval. Findings derive from observable consequences
and the repository's privacy, stewardship, transparency and accountability
principles. No new disputed scholarly proposition is needed for these changes.

The 2026-10-14 audit ran 206 passing CLI/Core/Artifacts tests and reproduced a
large-receipt publication defect. Those are historical audit results, not tests
run during this planning session and not proof of the proposed fixes.

## Findings

### IVSD-F001 - Callers need truthful publication outcomes

- Lifecycle: accepted; severity: high; claim type: implementation-traceability
  evidence and provider design obligation, not operational certification.
- Principle/domain: trust, transparency and non-harm / technical design and
  accountability. Provider-controlled decision: result bounds and publication
  reporting. Owner/next validation: CLI maintainer, P1 native maximal receipt and
  written-prefix cases. Escalation boundary: the scope changes listed below.
- Classification: provider obligation; Concern.
- Affected stakeholders: maintainers, operators, CI owners and downstream users.
- Evidence: artifact publication precedes receipt serialization in
  `src/Configurator.Cli/SetupCliArtifactHandlers.cs`; bounded serialization and
  generic error fallback are in `SetupCliMachineOutput.cs` and
  `SetupCliApplication.cs`. The audit observed exit 74 after a file was written.
- Consequence: an agent may retry, misreport completion, or mishandle an existing
  restricted artifact. Cleanup failure and cancellation must not erase a known
  publication outcome.
- IVSD-M001: plan a bounded mandatory publication ledger before commit; preserve
  known outcomes, cancellation and cleanup separately; never truncate ledger
  entries or append a second envelope after partial stdout delivery.
- Acceptance mapping: plan R01-R03, scenarios S01-S05; tasks P1 and P3.
- Residual risk: process death or a closed output channel can prevent delivery;
  no receipt design guarantees delivery or rollback in that situation.

### IVSD-F002 - Ignoring unrelated credentials is better than inspecting them

- Lifecycle: accepted; severity: medium; claim type: implementation-traceability
  evidence plus design validation.
- Principle/domain: data minimization and informed control / privacy and UX.
  Stakeholders: CI owners, maintainers and operators. Provider decision: which
  environment/input surfaces are consumed. Owner/next validation: CLI maintainer,
  P3 native ambient-environment and non-disclosure cases. Escalation boundary:
  any newly consumed private environment surface.
- Classification: privacy and minimization; Concern.
- Evidence: `Program.cs` enumerates ambient environment names, and
  `SetupCliArgumentPreflight.cs` rejects forbidden substrings. The audit observed
  `GITHUB_TOKEN` blocking a local doctor command.
- Consequence: adoption friction encourages ad hoc wrappers and environment
  manipulation without improving the no-target-credential-consumption boundary.
- IVSD-M002: consume no ambient target configuration; remove ambient-name
  enumeration, retain closed options and explicit private input channels, and
  never echo rejected values or private paths. Harmless file/key names must not
  be rejected merely for containing words such as token.
- Acceptance mapping: R06, S12-S13; P3.
- Residual risk: the CLI cannot stop a caller deliberately putting private
  material into a field explicitly declared public.

### IVSD-F003 - Contributors retain custody of source edits

- Lifecycle: accepted; severity: high; claim type: source-traced concurrency risk
  and design validation; adversarial interleavings were not reproduced.
- Principle/domain: stewardship and non-harm / technical design and user control.
  Stakeholders: contributors and operators. Provider decision: mutation and
  captured-filesystem authority. Owner/next validation: CLI/Artifacts maintainers,
  P2 native races and candidate preservation. Escalation boundary: weakening the
  selected no-source-overwrite or no-redirect contracts.
- Classification: preservation of user work and authority; Concern.
- Evidence: `DefinitionSourceFiles.cs` performs pathname checks before later
  reads/moves and uses hash-then-overwrite replacement. The user chose candidates.
- Consequence: a competing save can be lost; a substituted path can redirect
  authoring I/O; concurrent default-format initialization can create ambiguity.
- IVSD-M003: use identity-bound public-authoring I/O, create-only candidates,
  root-scoped kernel-held initialization exclusion, and no silent filesystem
  fallback. Do not claim to control nonparticipating editors.
- Acceptance mapping: R04-R05, S07-S11; P2.
- Residual risk: another authorized process can subsequently move/delete an
  already published public file; a captured read is not an editor transaction.

### IVSD-F004 - Agents need understandable and honest machine state

- Lifecycle: accepted; severity: medium; claim type: implementation-traceability
  evidence plus design validation.
- Principle/domain: transparency and accountability / AI tooling and UX.
  Stakeholders: contributors, AI-tool users and target owners. Provider decision:
  discovery, diagnostic disclosure and readiness claims. Owner/next validation:
  CLI maintainer, P1/P4 contract and native discovery cases. Escalation boundary:
  public projection of private values or a claim of target authorization.
- Classification: transparency and informed control; Concern.
- Evidence: machine help contains no capability body; `--version` is not
  registered; source errors collapse to `definition-source-invalid`.
- Consequence: agents guess commands, remove useful facts to satisfy admission,
  or confuse preparation success with target acceptance.
- IVSD-M004: expose installed build/contracts/capabilities, bounded structural
  source diagnostics and output discovery; separate command completion,
  preparation, publication and unassessed target acceptance.
- Acceptance mapping: R01, R07-R09, S14-S20; P1 and P4.
- Residual risk: useful diagnostics cannot establish that the integration captures
  all consequential target semantics.

### IVSD-F005 - Platform claims must match actual evidence

- Lifecycle: accepted; severity: medium; claim type: implementation-traceability
  evidence, with native host validation missing.
- Principle/domain: honesty and equitable access / operations and product claims.
  Stakeholders: adopters on every advertised host. Provider decision: support
  labels and fallback policy. Owner/next validation: release/Artifacts maintainers,
  P6 actual-host matrix. Escalation boundary: claiming qualification without
  native evidence or adding an ordinary restricted-save fallback.
- Classification: honest access and reliability claims; Concern.
- Evidence: the release owner admits four CLI RIDs; current native CI/package
  qualification is Linux x64. Protected publication remains Linux-only.
- Consequence: an adopter may mistake publishability for support or infer
  protection from an executable merely starting.
- IVSD-M005: qualify all four declared CLI targets on actual hosts; test public
  authoring everywhere and explicit protected-write refusal on Windows/macOS;
  state filesystem capability limits and never add an ordinary-file fallback.
- Acceptance mapping: R11-R12, S24-S26; P6.
- Residual risk: hosted-runner/filesystem evidence is scoped to the tested host,
  not every filesystem or machine of the same operating system.

### IVSD-F006 - The agent assists; the contributor and target remain accountable

- Lifecycle: accepted; severity: medium; claim type: design validation and
  implementation traceability; no multi-project stakeholder study is claimed.
- Principle/domain: informed agency and accountability / AI/provider responsibility.
  Stakeholders: contributors, maintainers and downstream users. Provider decision:
  skill workflow and compatibility claims. Owner/next validation: CLI/skill
  maintainers, P5/P6 independent installed scenario. Escalation boundary:
  automatic apply or substituting agent confidence for target acceptance.
- Classification: human agency and responsibility; Concern.
- Evidence: `skills/configurator-integrate/SKILL.md` requires codebase research,
  native checks and independent synthetic expectations; the installed package
  scenario does not yet exercise that entire lifecycle.
- Consequence: compilation can be misrepresented as integration correctness or
  target authorization, especially if expected output comes from the same
  implementation under test.
- IVSD-M006: retain one portable skill, source anchors and explicit missing facts;
  test a complete installed-tool scenario outside the checkout with independently
  specified bytes and target-owned validation. No target-specific exporter.
- Acceptance mapping: R10-R11, S21-S24; P5-P6.
- Residual risk: automated evidence cannot prove an agent's understanding or
  authorize applying configuration to a target.

### IVSD-F007 - Cancellation must not conceal effects or retain avoidable custody

- Lifecycle: accepted; severity: medium; claim type: source-traced risk and design
  validation; the new process-lifetime behavior is not implemented.
- Principle/domain: stewardship and user control / privacy and operations.
  Stakeholders: operators and CI owners. Provider decision: cancellation/cleanup
  ownership and reporting. Owner/next validation: CLI maintainer, P2/P3 exact-event
  interruption and written-prefix cases. Escalation boundary: promises of rollback,
  complete managed-memory erasure or guaranteed delivery after process death.
- Classification: user control, privacy and accountability; Concern.
- Evidence: native preparation receives `CancellationToken.None`; cleanup failure
  currently throws before known bundle statuses are returned.
- Consequence: cancellation may not stop pending work; an interrupted existence
  lock can obstruct future work; error handling can misstate written prefixes.
- IVSD-M007: propagate one command cancellation signal; observe it before commit,
  preserve established writes, release process-owned locks on process exit and
  define recoverable public staging behavior. Minimize owned raw input buffers
  without promising erasure of all managed values.
- Acceptance mapping: R02-R05, S03-S11; P2-P3.
- Residual risk: synchronous parsing has bounded but nonzero cancellation latency;
  forced termination can prevent a receipt and cleanup.

### IVSD-F008 - Installation must not borrow authority or disguise provenance

- Lifecycle: accepted; severity: medium; claim type: implementation-traceability
  evidence and design validation; promoted authenticity is not established.
- Principle/domain: rights, transparency and accountability / governance and
  distribution. Stakeholders: adopters, component owners and release stewards.
  Provider decision: package/provenance claims. Owner/next validation: release
  maintainer, P5/P6 resource/checksum parity and promotion backlog. Escalation
  boundary: new signing authority, external promotion or changed component rights.
- Classification: rights, provenance and institutional responsibility; Concern.
- Evidence: packages preserve schemas, skill/resources and notices; unsigned
  validation packages are not promoted releases. Configurator has no inherited
  Event signing/publication authority.
- Consequence: installation advice can become a false authenticity claim or
  quietly omit licensing/contract resources.
- IVSD-M008: provide a version-pinned offline-verifiable archive workflow,
  package resource equality and checksum checks, SHA-pinned CI actions and
  accurate preview/support labels. Preserve notices and third-party identities.
  Signed publication awaits separately established Configurator authority.
- Acceptance mapping: R11-R13, S24-S27; P5-P6 and Knowledge Graduation.
- Residual risk: a checksum obtained from the same untrusted source is integrity
  evidence, not independent publisher authentication.

## Recommendations

Address outcome integrity first, then source custody and process boundaries,
then agent-facing discovery/repair, then packaged adoption and native-host
qualification. Keep public diagnostics structurally useful while private answer
diagnostics remain value-free. Do not add remote apply, telemetry, secret flags,
compatibility aliases or target-specific wrappers.

## Stakeholders

- Maintainers author and review the public integration and its source evidence.
- Contributors retain source edit/candidate adoption authority.
- Operators control private answer files and deliberate artifact publication.
- CI owners control pinned tool installation and synthetic validation.
- Target applications own semantic acceptance, authorization and apply.
- Configurator maintainers own truthful tool contracts and support claims.
- Downstream users bear consequences of incorrect target configuration.

## I-VSD Principles And Domains

The relevant repository domains are privacy/data governance, technical design,
AI/provider responsibility, UX/defaults and governance/accountability. The
operational principles are stewardship of entrusted data and user work, harm
prevention, understandable claims, contributor control and truthful evidence.
The detailed source is `.agents/skills/i-vsd/resources/principles-and-domains.md`;
this review does not invent a new religious ruling from those principles.

## Validation Gaps

No implementation is present. Required evidence includes maximal receipts,
publication/cancellation interleavings, native filesystem identity races,
candidate preservation, kernel-lock recovery, privacy-safe diagnostics, packaged
complete/incomplete adoption and actual-host qualification. Acceptance mappings
were revalidated against the completed triad: eight findings map to R01-R13,
S01-S27 and concrete task phases/commit scope. Plan alignment is not a claim that
those implementation checks have run or that the concerns are resolved.

## Escalation Needed

No scholarly escalation is presently required for the engineering mitigations.
Reopen this assessment if implementation adds telemetry, remote authority,
private-default exposure, executable definition vocabulary, new publication
authority or a weaker protected-save fallback. Such changes are outside this plan.

## Evidence Reviewed

- `AGENTS.md`, `PROJECTS.md`, `docs/internal/QUICK_REFERENCE.md`.
- Current CLI application, preflight, settings, source helpers and serializers.
- Core authoring/identity and Artifacts publication ownership.
- CLI audit dated 2026-10-14 and its explicitly historical executed evidence.
- Current public/technical CLI, authoring and packaging guidance.
- Portable integration skill and native package/contract tests.
- Shared planning packet:
  [plan](../../dev/active/cli-native-adoption/cli-native-adoption-plan.md),
  [tasks](../../dev/active/cli-native-adoption/cli-native-adoption-tasks.md),
  [context](../../dev/active/cli-native-adoption/cli-native-adoption-context.md).
- Official Spectre documentation via Context7 and filesystem/CI documentation via
  Tavily, cited in the plan's research ledger.

## Missing Evidence

Implementation/test results, cross-platform native qualification, promoted
release provenance and a representative multi-project adoption study are absent.
Their absence is not treated as a pass.

## Context Inventory

Repository evidence is authoritative for current mechanics. User decisions fix
the skill-plus-CLI model, no compatibility burden and create-only revision
candidates. Prior terminal/publication work informs overlap but does not establish
new CLI evidence. No private answers, credentials or external issue contents were
used. No separate copy of the repository investigation is maintained here.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
| --- | --- | --- | --- | --- |
| 2026-10-15 | none | draft / ready-for-planning | CLI implementation-plan intake | Current source packet and explicit candidate decision |
| 2026-10-15 | draft / ready-for-planning | current / plan-aligned | Complete triad and independent-review corrections | Requirements/scenario/task mappings and artifact seal passed; implementation outstanding |

This review remains planning evidence, not implementation or release approval.
