# Schema-driven secrets — I-VSD planning assessment

Last Updated: 2026-11-02 Europe/Brussels

## Review Metadata

- Mode: planning.
- Subject/workstream: `schema-driven-secrets`.
- Report kind: feature planning/provider-responsibility assessment.
- Report status: current.
- Disposition: plan-aligned.
- Evidence cutoff: 2026-11-02.
- Reviewed input: planning baseline HEAD `4f130d11203bdda3da826a1c5de0fc8aa3911329`, design revision `secrets-plan-r3` in the [plan](../../dev/active/schema-driven-secrets/schema-driven-secrets-plan.md) and [tasks](../../dev/active/schema-driven-secrets/schema-driven-secrets-tasks.md), and the direct-checkout implementation evidence recorded on 2026-11-02 in [context](../../dev/active/schema-driven-secrets/schema-driven-secrets-context.md).
- Supersedes: none. User implementation approval: Granted for direct-checkout execution on 2026-11-02; no Git publication authorization. CTO review: Applied & Aligned (2026-11-02).

## Executive Summary

The proposed feature lets operators generate and validate secrets locally using public definitions. It can reduce unsafe manual generation and make password/key requirements understandable across CLI, Terminal, Desktop and Browser. It also creates predictable risks: generated values can bypass existing consent, stale work can restore a cleared secret, expensive policy settings can freeze a device, and a success message can imply authority the utility does not possess.

The plan addresses these risks through one admitted secret contract, shared validation, explicit generation, bounded cryptographic work, workspace revision checks, and deliberate reveal/copy. Agent guidance is separated into public definition authoring and explicitly authorized private local operations. The selected mechanisms are traceable to Trust, Non-Harm, Truthfulness, Rights of People and Justice. Implementation checks now provide bounded evidence for these mechanisms; device, accessibility, target and operational outcomes remain separate obligations.

`plan-aligned` means every material finding below has a scenario/task owner in revision `secrets-plan-r3`. It does not mean the implementation exists, that selected packages execute safely in every browser, that users have validated the UX, or that any religious/legal/security certification has been granted. Senior CTO review (`secrets-plan-r3`) further strengthened sensitivity monotonicity across `$ref` merges and all 9 cross-layer sensitivity callers, eliminated forbidden/redundant external crypto packages in favor of `.NET 10` BCL + a single portable `BouncyCastle.Cryptography` 2.7.0 dependency, and specified exact RFC 7518 JWK, PHC, NUL-byte, and `ZeroMemory` invariants.

## Scope

Provider-controlled choices: definition policy, supported algorithms/formats, resource ceilings, local generation, consent, masking/reveal/copy, validation communication, CLI output, dependency distribution, documentation and agent workflows. Stakeholders include operators, definition authors, target administrators, people whose data is protected by the resulting configuration, assistive-technology/Arabic users, maintainers, and third-party component authors.

The target application retains credential issuance/acceptance, authentication, authorization and actual deployment. Clipboard services and browser/OS memory are outside full Configurator control. The user explicitly permits breaking Configurator changes during development; that does not remove these external responsibility boundaries.

## Claim Boundary

This is provider-responsibility design reasoning using the repository's I-VSD framework. It is not a fatwa, a religious-legal conclusion, a license certification, an implementation audit, or proof of operational outcomes. Evidence includes source inspection and primary-source research; new feature tests, stakeholder interviews and production experience are absent. Findings are accepted design obligations, not resolved incidents.

## Findings

### IVSD-F001 — Generation can bypass entrusted custody or restore a cleared secret

An operator who declines browser secret entry or revokes consent must not receive a newly generated secret later from an outstanding task. Nested fields and reordered rows make this particularly subtle: matching a stale array index can write a secret into a different editor. A copied value also leaves the workspace's control.

Configurator controls eligibility, the sensitivity floor, transient state, revision checks and the clarity of Copy/Show actions. Amanah and Rights of People apply because the operator entrusts private material under explicit local-custody expectations. Non-Harm supports preventing unintended exposure rather than merely hiding an input widget.

- Lifecycle: accepted; severity: high; claim type: design risk with current implementation traceability.
- Domains: technical, design, operational. Stakeholders: operators and affected target users.
- Evidence: plan E01/E05/E06/E07/E08/E09/E13; existing consent/clear and operation code, source inspection only.
- IVSD-M001: central `DefinitionSensitivityPolicy` consumed by all 9 sensitivity callers across `Core`, `Presentation`, `Browser`, `Desktop`, and `Terminal`, monotonic `$ref` sensitivity merging, explicit consent-gated generation, before/after workspace/revision/editor identity checks, `CryptographicOperations.ZeroMemory` transient cleanup, deliberate separate reveal/copy, and honest clipboard/immutable-memory limitations. CLI generated-input hash/digest recipes require an explicit restricted original-password destination; save it before the answer, preserve a successful sidecar on later failure, and report the written prefix safely instead of losing the original credential or claiming multi-file atomicity.
- Owner/next validation: Core/Presentation and client implementers; S02/S09/S16/S17/S23; tasks 1.2, 6.1–6.3, 7.1–7.4, 8.1–8.3.
- Trade-off: copied values cannot be retracted; library work may finish after cancellation, but its result must be discarded. Neither masking nor buffer zeroing promises complete memory erasure.
- Escalation: any proposal for server generation, answer persistence, worker transport or weaker consent requires a new provider-boundary decision and report refresh before implementation.

### IVSD-F002 — Local validity can be mistaken for security or target acceptance

A long encoded key may contain too few decoded bytes. A syntactically valid external token may not be recognized by its issuer. A password hash does not reveal whether the original password met the stated character rules. Fast digests and password-storage hashes have materially different purposes.

Truthfulness/Sidq and Avoiding Deception apply to the exact claims shown to operators and agents. Configurator controls algorithm/encoding metadata, diagnostic wording and readiness presentation; target administrators control actual acceptance. Clear distinctions reduce uncertainty without pretending the local tool can verify a remote authority.

- Lifecycle: accepted; severity: high; claim type: design risk supported by standards/API research and source traceability.
- Domains: design, technical, governance. Stakeholders: operators, definition authors, target administrators.
- Evidence: plan E01/E03/E04/E10 and primary source register in plan 5.4; no target execution evidence.
- IVSD-M002: explicit byte/character/format semantics (including RFC 7518 §6.2 fixed-width unsigned EC JWK coordinates, RFC 7518 §6.3 minimal unsigned `Base64urlUInt` RSA CRT parameters, Argon2id PHC unpadded Base64, and bcrypt `U+0000` NUL-byte rejection); separate password-hash/digest kinds and transient input policy; independent vectors; no KDF on ordinary hash-format validation; incomplete draft and unknown target acceptance labels retained.
- Owner/next validation: Core/CLI/docs; S11–S15/S21; tasks 2.2, 4.1–4.3, 5.1–5.2, 11.1.
- Trade-off: exact target formats require public contract evidence from the author. Unrecognized targets must remain unsupported instead of receiving a guessed serialization.
- Escalation: new assertions of target acceptance, algorithm certification or religious compliance require the relevant independent authority before publication.

### IVSD-F003 — Definition-controlled work can exhaust the operator's device

A public definition that requests excessive hash memory, huge strings or extreme key sizes can hang or exhaust a local device. Unbounded password retries can do the same even when no attacker intended it. A slow task can create confusing repeated clicks and stale writes.

Non-Harm and Ihsan apply because resource budgets and predictable failure are provider-controlled. Local execution avoids a server transfer but does not make a computation harmless. Operators with lower-memory devices are disproportionately affected by careless defaults.

- Lifecycle: accepted; severity: high; claim type: prospective design risk.
- Domains: technical, design, evaluation. Stakeholders: operators on all device classes, support maintainers.
- Evidence: plan E01/E03 and primary algorithm documentation; device measurements missing.
- IVSD-M003: application-owned ceilings, policy feasibility before collection, unavailable-profile refusal before validators exist, bounded unbiased generation, size checks before parsing/allocation, one expensive operation per workspace, explicit cancellation status and actual AOT latency/memory qualification. A test-only engineering probe supplies early primitive evidence; phase 8 separately exercises the published product.
- Owner/next validation: Core/Presentation; S01/S10/S13/S14; tasks 1.1–1.2, 3.1–3.2, 4.1–4.2, 4.V, 5.V, 6.1–6.2.
- Trade-off: a fixed ceiling may reject a target's unusually expensive format. Failure must be explicit; lowering security parameters silently is rejected. Performance evidence may require a visible ceiling/profile redesign.
- Escalation: unbounded work, weaker security floors or remote offload require plan/report revision before adoption.

### IVSD-F004 — Unclear or inaccessible validation shifts avoidable burden to users

Generic errors make an operator guess which rule failed; color-only feedback, unlabelled icon buttons or English-only messages compound that burden. Announcing a secret or narrating every keystroke to assistive technology creates a different kind of exposure.

Justice/Adl and Excellence/Ihsan apply to equal access to correction and control. Configurator controls its field messages, keyboard behavior, focus, localization and what it announces. The plan should preserve technical detail while explaining the practical action the operator can take.

- Lifecycle: accepted; severity: medium; claim type: source-grounded UX gap and planned mitigation.
- Domains: design, evaluation, operational. Stakeholders: screen-reader, keyboard, Arabic/RTL and novice users.
- Evidence: plan E07/E08/E09: current generic errors and masking controls; no stakeholder/accessibility assessment performed.
- IVSD-M004: shared rule identifiers and localized public requirements, interaction-aware inline errors, navigable summary, explicit button names/busy states, focus preservation, restrained status announcements, actual native/browser/PTY checks.
- Owner/next validation: Presentation and all clients; S05/S20; tasks 2.3, 8.2, 9.2, 10.2, 11.2.
- Trade-off: more informative feedback requires disciplined localization and safe location mapping. Passing component tests cannot substitute for actual assistive-technology evaluation.
- Escalation: scope reduction that removes a client or accessible controls requires explicit user review and I-VSD refresh.

### IVSD-F005 — Agent automation can turn assistance into secret collection

A skill that learns target requirements from `.env` or puts generated values in chat crosses the intended boundary even if the local CLI is secure. The portable integration skill serves public authoring; the machine skill serves explicit local operations. Combining those roles carelessly creates unnecessary access and exposure.

Amanah, Rights of People and Avoiding Spying/Tajassus apply to the information an agent requests, reads and transmits. The provider controls skill routing, command examples, machine diagnostics and defaults. No agent needs real answers to author a public policy.

- Lifecycle: accepted; severity: high; claim type: design risk with existing skill-scope evidence.
- Domains: governance, technical, design. Stakeholders: operators, integrators, agent users and target users.
- Evidence: plan E10/E11; portable/internal skills and CLI output rules inspected.
- IVSD-M005: preserve separate authoring/private-operation routes, inspect public target facts only, use synthetic exercises, require explicit private local inputs/destinations (including the original-password sidecar when generated before hashing), no plaintext argv/stdout/chat, value-free receipts, no ambient secret discovery and no automatic regeneration after partial publication.
- Owner/next validation: CLI and agent-context contributors; S18/S19/S22/S23; tasks 7.1–7.4, 12.1–12.3.
- Trade-off: an agent can verify a private operation locally without reproducing the secret in its answer. Convenience does not justify widening collection.
- Escalation: any proposed remote-agent secret transfer or skill expansion into target administration requires a separate scope/provider decision.

### IVSD-F006 — Library and package claims need rights and real-platform evidence

A package described as managed or compatible with a framework is not automatically proven in the published AOT browser. A permissive project license does not erase a dependency's terms. Calling a draft profile supported before executing it breaks an engineering promise to integrators.

Rights of People, Promise-Keeping and Truthfulness apply to third-party component authors and operators relying on support claims. Configurator controls dependency selection, notices, package closure and the language used in guides/skills.

- Lifecycle: accepted; severity: medium; claim type: dependency/distribution and evidence-boundary risk.
- Domains: strategic, technical, governance, evaluation, operational. Stakeholders: component authors, maintainers, downstream distributors and users.
- Evidence: plan source register and dependency gates (E14/E15); selected package is not installed/qualified by this task.
- IVSD-M006: single third-party portable crypto dependency (`BouncyCastle.Cryptography` 2.7.0) paired with `.NET 10` BCL primitives (eliminating `Microsoft.Extensions.Identity.Core` which violates `SetupCoreArchitectureTests` and redundant `BCrypt.Net-Next`), exact package identity/closure/terms/notices, source-free implementation handoff, current advisory check, independent vectors, actual AOT/native package tests and explicit separation of local tests from host/target qualification. The named test-only crypto probe is excluded from product publication and never substitutes for testing the actual product's trimmed AOT payload.
- Owner/next validation: crypto/packaging/docs owners; S12/S22; tasks 4.2–4.3, 4.V, 5.2, 5.V, 8.V, 10.3, 11.3.
- Trade-off: `BouncyCastle.Cryptography` 2.7.0 adds payload/review burden but supplies one portable primitive path for Argon2id, `OpenBsdBCrypt`, and RSA/EC without introducing multiple third-party closures. Package maturity informs selection; it is not runtime or legal certification.
- Escalation: ambiguous/incompatible terms require qualified distribution/legal review; altered platform support or alternative crypto providers require plan/report revalidation.

## Recommendations

Implement the six mitigations through the mapped behavioral slices. Keep the central validator authoritative for UI, import, generated results and readiness; ship deliberate generation with existing custody rather than treating a button as the whole feature.

Rejected alternatives: server generation would expand custody; per-client policy engines would drift; arbitrary generator scripts would turn public definitions into execution authority; silent algorithm/cost fallback would misrepresent results; blanket clipboard prohibition would remove explicitly requested functionality instead of supporting informed action; a new overlapping authoring skill would duplicate the existing canonical workflow. Technical library alternatives and their strongest cases are recorded in plan 5.4, not re-decided here.

## Stakeholders

| Stakeholder | Interest / responsibility |
| --- | --- |
| Local operators | Understand and control secret creation, correction, visibility and export |
| Definition authors | Declare public target policy and exact representations without shipping real credentials |
| Target administrators/providers | Accept, authorize and apply configuration; issue external tokens |
| People protected by target configuration | Avoid harm from weak, exposed or misapplied credentials |
| Arabic/RTL, keyboard and assistive-technology users | Equivalent usable correction/control, no unnecessary disclosure |
| Maintainers/support contributors | Reproduce failures safely without private workspaces or overstated evidence |
| Third-party authors/distributors | Preserve component terms, identity, notices and accurate support claims |

No stakeholder interviews, support logs or operational outcomes were reviewed. These interests are reasoned design inputs, not measured user preferences.

## I-VSD Principles And Domains

Framework source: [principles and domains](../../.agents/skills/i-vsd/resources/principles-and-domains.md). Amanah concerns entrusted custody; Sidq concerns exact validity/support claims; Adl concerns equitable correction; La Darar concerns bounded resource/security harm; Rights of People concerns consent and component rights; Ihsan concerns coherent implementation and verification; Tajassus concerns unnecessary agent collection; Promise-Keeping concerns portability/support claims.

All six domains were considered. Strategic: preserve an offline utility rather than create a credential service. Design: informed generation/reveal/copy and useful feedback. Technical: shared admission/validation and stale-result prevention. Operational: honest support limits and value-safe diagnostics. Governance: separate target/provider/agent authority and component rights. Evaluation: distinguish planned checks from executed and stakeholder evidence. Finance, riba, moderation, religious-content ranking and federation are not affected by this feature.

## Validation Gaps

The planning review originally contained no new runtime evidence. The implementation
now has recorded Core algorithm/interoperability checks, shared workspace
privacy/concurrency checks, native definition/schema generation, CLI behavior
checks and bounded terminal exercises. See the [execution context](../../dev/active/schema-driven-secrets/schema-driven-secrets-context.md)
for exact commands, failures and subsequent passes, and the
[cryptography contract](../../docs/internal/SECRET_CRYPTOGRAPHY.md) for dependency
and independent-vector evidence. This updates evidence availability without
closing the six accepted obligations or claiming that source tests qualify every
published client. The [final candidate record](../../docs/internal/OPERATIONS.md#schema-driven-secrets-qualification-2026-11-02)
adds actual published Browser AOT, 37-profile crypto probe, Linux packages and
English/Arabic native X11 evidence. The subsequent user-directed
[0.0.1 development baseline](../../docs/internal/OPERATIONS.md#fixed-development-baseline-001-2026-11-02)
preserves the custody/identity boundaries, requalifies current packages and resolves
all 27 obsolete Artifacts fixture failures. Incomplete historical pre-fix Red
evidence, other-device performance, native clipboard
availability, assistive technology, stakeholder studies and operational outcomes
remain explicit limits. The [evidence ladder](../../.agents/skills/i-vsd/resources/evidence-and-validation-levels.md)
still governs each claim.

## Escalation Needed

No scholarly determination is required for the present technical feature. Do not label it religiously compliant or certified. Concrete future escalations are defined per finding: new custody/authority decisions, incompatible license terms, weakened security floors, unsupported client scope reductions, or religious/legal claims. Ordinary in-scope implementation does not require repeated permission.

## Evidence Reviewed

- User's research request, explicit no-backward-compatibility clarification, implementation plan request, and Senior .NET CTO review request.
- Working-tree source evidence E01–E16 at clean `main` HEAD `4f130d11203bdda3da826a1c5de0fc8aa3911329`, precisely listed in [plan section 2](../../dev/active/schema-driven-secrets/schema-driven-secrets-plan.md#2-source-grounded-current-state).
- Primary-source research register in [plan section 5.4](../../dev/active/schema-driven-secrets/schema-driven-secrets-plan.md#54-crypto-choices-and-source-register), reviewed 2026-11-02.
- Complete `secrets-plan-r3` behavioral requirements/scenarios, twelve phase exits, Worst-Break invariant specifications, task ledger and ID mappings; planning approval and runtime completion remain separate.
- Repository contribution contract, quick reference, documentation/ownership policy, existing secret custody/protected-output boundaries, and portable/internal Configurator skills.
- Implementation source review and recorded 2026-11-02 checks in the task context: typed sensitivity, bounded validators, generated-output validation, workspace revisions/editor identities, sidecar-first native publication, and separate public-policy/private-operation skills. No provider-authority, network-custody or mitigation-scope change was identified by this revalidation; stable finding/mitigation mappings remain applicable.

## Missing Evidence

Final published-product/profile coverage and package closure, browser/device
resource measurements, assistive-technology results, target-format verification
for each future real integration, stakeholder observations and operational
support evidence remain to be assessed at their owning gates. The native crypto
dependency record and advisory snapshot now exist; they are neither legal
certification nor a promise that future advisories cannot appear. Missing
evidence belongs to named tasks and qualification records, never a fabricated pass.

## Context Inventory

Available: current checkout, source/test definitions, graph locators with noted stale symbol ranges, public documentation, selected primary web facts and explicit user direction. Not inspected: real secrets/answers, external target deployments, credential stores, logs containing private data, or production users. No external communication/publication occurred.

## Common Overlooked Failures And Outcomes

Overlooked failures include secret metadata under local references, a moved list row receiving an old result, imported hashes triggering costly work, Unicode character limits differing from bcrypt bytes or embedded NUL bytes, EC JWK leading-zero coordinate truncation, accidental double hashing, hash/plaintext Copy confusion, clipboard history surviving workspace clear, and agent examples encouraging private-file discovery.

The desired outcome is a consistent local workflow: operators can understand failed rules, generate valid material deliberately and retain control over exposure; definition authors can describe exact target expectations; agents can help without collecting credentials. These are expected benefits, not measured outcomes.

## Review Lifecycle

| Date | Previous status | New status | Trigger / evidence |
| --- | --- | --- | --- |
| 2026-11-02 | None | draft | Shared source/research packet and user intent established |
| 2026-11-02 | draft | current / plan-aligned | Complete r1 scenarios/tasks checked against all six findings and mitigations; all mapped below, no unowned material branch |
| 2026-11-02 | current r1 | stale | Independent review identified CLI original-password loss, premature profile acceptance and missing early AOT entrypoint |
| 2026-11-02 | stale | current / plan-aligned r2 | Revalidated explicit protected sidecar/partial delivery (S23), phase-1 refusal, executable test-only probe and separate product evidence against updated tasks and findings |
| 2026-11-02 | current r2 | current / plan-aligned r3 | Senior CTO review refined dependency architecture (single `BouncyCastle.Cryptography` 2.7.0 + `.NET 10` BCL, eliminating forbidden `Microsoft.Extensions.Identity.Core` and redundant `BCrypt.Net-Next`), closed all 9 cross-layer sensitivity callers and `$ref` merge rules, and tightened RFC 7518 JWK / PHC / NUL-byte / `ZeroMemory` invariants |

Refresh after material changes to secret kinds, costs/algorithms, crypto provider, sensitivity, consent, transient retention, copy/reveal, agent permissions, target authority, accessibility/client coverage or mapped tasks. Formatting and evidence-location corrections do not invalidate the review. A material rewrite must update reviewed revision and re-evaluate affected findings, not just change the date.

## Planning Handoff

- Workstream: `schema-driven-secrets`; status current; disposition plan-aligned.
- Reviewed input: `secrets-plan-r3` plan/tasks behavior and current source packet at HEAD `4f130d11203bdda3da826a1c5de0fc8aa3911329`; context is resume memory only.
- Findings/mitigations: IVSD-F001 → IVSD-M001; IVSD-F002 → IVSD-M002; IVSD-F003 → IVSD-M003; IVSD-F004 → IVSD-M004; IVSD-F005 → IVSD-M005; IVSD-F006 → IVSD-M006.
- Required mappings: F001/M001 → S02/S09/S16/S17/S23 and 1.2/6.1–6.3/7.1–7.4/8.1–8.3; F002/M002 → S11–S15/S21 and 2.2/4.1–4.3/5.1–5.2/11.1; F003/M003 → S01/S10/S13/S14 and 1.1–1.2/3.1–3.2/4.1–4.2/4.V/5.V/6.1–6.2; F004/M004 → S05/S20 and 2.3/8.2/9.2/10.2/11.2; F005/M005 → S18/S19/S22/S23 and 7.1–7.4/12.1–12.3; F006/M006 → S12/S22 and 4.2–4.3/4.V/5.2/5.V/8.V/10.3/11.3.
- Escalations before implementation/release: only the concrete changed-boundary, uncertain-rights or failed-qualification conditions above. No current scholarly escalation.
- User implementation approval: Granted for direct-checkout execution on 2026-11-02; CTO review: Applied & Aligned (2026-11-02). Neither is granted by this report.
