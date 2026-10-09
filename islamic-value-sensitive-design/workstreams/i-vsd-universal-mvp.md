# Universal Configurator MVP - I-VSD Planning Assessment

Last Updated: 2026-10-09

## Review Metadata

- Mode: planning
- Subject: A reusable offline configuration utility for self-hosted applications beyond ISLAMU Event
- Workstream: universal-mvp
- Report kind: Provider-responsibility planning assessment
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-10-09
- Reviewed input: Configurator working tree on `main`; strategy and repository contracts in E01-E07; `dev/active/universal-mvp/` plan, tasks and context as read on 2026-10-09 at SHA-256 `80ef11e5de7d8e4069b7ec291349e785af7af92338463ae0e6fa842558c155e9`, `7dad95f72ffb7675436f3b33efed2246630eef9001a59ef699f59c781e182fdd` and `292531eaa1b9a4e6e91fbac02f7e0bdf00ddbcac2ffb7f7f95dba17c1e55c0df`, respectively. Later execution-status-only edits do not change this design assessment; material behavior, authority or mitigation edits trigger revalidation.
- Supersedes: none

## Executive Summary

Self-hosters should be able to use a local definition to understand required choices and prepare application-native configuration for software other than ISLAMU Event. The maintainer controls what definitions can do, which answers cross a public browser boundary, how native files are written, and what the interface says has actually been verified. Those decisions create duties even when Configurator runs offline and never receives the operator's data.

The recommended MVP keeps definitions bounded and data-only, uses the existing independent Core and shared presentation model, and treats Event as one example rather than a privileged target. `.env`, JSON and YAML output can broaden usefulness, provided answer-derived bytes remain restricted and target acceptance remains with the target application. The browser remains a partial public-choice handoff. No secret manager, target apply, test runner, arbitrary remote launch, mandatory catalog or telemetry is needed to establish the core workflow.

Four findings require plan coverage: private output protection, truthful readiness and target authority, usable cross-platform surfaces with precise capability claims, and neutral definition provenance. This revision covers raw static Browser resource bytes as well as visible controls, one Artifacts-owned bundle publication path, one canonical neutral family/revision across Browser and native examples, and a source-free Grafana candidate whose target behavior remains unproved. The completed triad maps each finding to observable scenarios and owned tasks, so this assessment is current and plan-aligned. Its MVP target is CLI, Terminal and Desktop on `linux-x64`, `linux-arm64`, `osx-arm64` and `win-x64`, plus static Browser; missing host proof blocks completion. The present repository verifies some extraction boundaries and documents Linux-only protected writes. Plan alignment does not establish the proposed universal MVP, protected writes on macOS/Windows, stakeholder usability, or target conformance for other applications.

## Scope

Covered: provider decisions for locally admitted definitions, answer handling, native `.env`/JSON/YAML preparation, public browser transfer, CLI/TUI/Desktop interaction, supported-host claims, example provenance and documentation. Stakeholders include self-hosting operators, application definition authors, people whose data appears in configuration, downstream package users and contributors.

Excluded from this MVP assessment: secret-manager integration, private browser entry, remote schema or catalog fetching, application administration/apply, testing runners and instance orchestration, AI custody of secrets, promoted publisher trust, default telemetry and religious-legal conclusions. The strategy documents discuss some of these as possible later capabilities; they are not current product contracts.

## Claim Boundary

This is provider-responsibility design reasoning based on repository and user-supplied planning evidence. A strategy recommendation is not an implemented behavior. Existing tests and the earlier extraction report support only their named scope; they do not prove the proposed JSON/YAML or cross-platform MVP. I-VSD does not certify Islamic compliance, legal rights, security, accessibility or operational outcomes. Qualified authorities retain religious-legal and legal determinations where those arise.

## Findings

### IVSD-F001: Native answers must stay private across every output format

An operator may enter database credentials, addresses or deployment details while preparing `.env`, JSON or YAML. If a new renderer or output kind sends those answers to stdout, a browser template, diagnostics or an ordinary file, it can expose information the operator entrusted to Configurator. Hiding a field in Browser controls cannot protect actual private answers, tokens or private paths accidentally included in the raw static definition resource: users and intermediaries can read those bytes directly. The maintainer controls field admission, classification, resource packaging, buffer lifecycle and save behavior; a definition author must not be able to turn an answer-derived artifact public by choosing a format or label.

The current browser accepts only active public enum/boolean choices and describes its download as a partial template. Native answer-derived content is restricted. The protected writer has verified Linux create-only behavior and rejects unsupported hosts rather than falling back to a weaker save. The proposed shared Artifacts bundle publisher adds another custody boundary: it must reject predictable destination conflicts before any file commits and identify an already committed prefix accurately if a later race, failure or cancellation occurs. It cannot promise whole-bundle rollback or expose private paths and values through results. This is a practical application of **Trust/Amanah** to configuration custody, **Rights of People** to privacy and property, and **Non-Harm** to avoiding exposure or overwrite. It also supports **Avoiding Spying** by keeping the MVP offline without default collection. Adding output formats or host support changes the risk at the artifact boundary and needs invariant and host evidence, not merely a UI build.

| Field | Assessment |
| --- | --- |
| Lifecycle / severity / claim type | open / high / preservation and expansion obligation |
| Principles / domains | Trust/Amanah, Rights of People, Non-Harm, Avoiding Spying / technical, design, operational, evaluation |
| Stakeholders | Operators; people identified in private configuration; downstream adopters |
| Provider-controlled decision | Raw Browser resource contents, input exposure, output sensitivity, public transfer vocabulary, shared bundle publication, native save guarantees and diagnostics |
| Evidence / validation level | E01-E03, E05 / implementation traceability for existing boundaries; design reasoning for new formats and hosts |
| Mitigation | **IVSD-M001:** keep raw bundled Browser definition bytes free of actual private answers, tokens and private paths; keep answer-derived output restricted by default and public transfer limited to active public choices; make new renderers pass the same sensitivity and no-stdout rules; publish native bundles through one Artifacts-owned preflight/create-only path with exact partial outcomes or fail closed; exclude telemetry and ordinary secret echoes |
| Owner / next validation | Core, Artifacts, Browser and native-client maintainers; Phase 1 raw-resource and cross-surface assertions, bundle public-seam privacy/race tests and actual supported-host filesystem checks before claiming protected export |
| Escalation boundary | Private browser entry, agent-visible secrets, remote definition authority or an unsupported-host save fallback requires renewed security/privacy review before implementation |

### IVSD-F002: Prepared files and public templates need honest readiness labels

A generated file may be syntactically valid yet rejected by the target application, require a restart, or omit answers that only a native client can collect. If the interface calls it “ready” without naming the level of readiness, an operator can mistakenly deploy an incomplete or unsafe configuration. A browser template is even narrower: it carries selected public choices, not a complete native configuration. The provider controls these labels and the diagnostics that accompany output; the target application controls semantic acceptance and administrative authorization.

The existing contracts already separate local preparation from target apply and identify the browser transfer as partial. The universal MVP must carry that distinction through `.env`, JSON and YAML, CLI machine envelopes, TUI/Desktop status and public guidance. **Truthfulness/Sidq** and **Promise-Keeping** apply because users act on readiness claims. **Trust/Amanah** applies because Configurator is entrusted with explaining what its validation did and did not establish. A local validator can check admitted constraints; it cannot grant permission or prove a target consumed the result.

| Field | Assessment |
| --- | --- |
| Lifecycle / severity / claim type | open / high / behavioral and communication obligation |
| Principles / domains | Truthfulness/Sidq, Promise-Keeping, Trust/Amanah / design, technical, operational, evaluation |
| Stakeholders | Self-hosters, automation callers and application maintainers |
| Provider-controlled decision | Readiness states, machine output, error wording, previews and documentation claims |
| Evidence / validation level | E01-E03, E04-E06 / existing-contract traceability and unimplemented design reasoning |
| Mitigation | **IVSD-M002:** distinguish definition admission, locally complete preparation, partial public template and target acceptance; show missing native-only answers and target apply/restart consequences; retain target-owned semantic validation and authorization; never present generation as deployment or conformance proof |
| Owner / next validation | Core, Presentation, client and documentation maintainers; contract scenarios for incomplete input, rejected target assumptions and identical readiness meaning across surfaces |
| Escalation boundary | Any future target apply, claimed target-tested conformance or host authorization requires a separate authority and evidence review |

### IVSD-F003: A cross-platform promise must match usable workflows on each host

Different operators need machine-readable CLI use, keyboard-driven terminal work or a desktop interface. A binary that starts on a host is not a functional configuration workflow if its required protected-save step fails there. Conversely, quietly writing an unprotected file to satisfy a cross-platform slogan would transfer risk to the operator. The maintainer controls packages, interaction design and public claims, including whether a supported host can complete the native workflow safely.

The current package guide lists CLI/TUI RIDs across Linux, macOS and Windows, Desktop only on `linux-x64`, and Linux-only protected output. The corrected plan makes all four existing native RIDs mandatory for CLI, Terminal and Desktop MVP completion. Each target RID needs actual host execution and protected-save evidence. An unsupported filesystem must fail closed, but a missing target OS proof keeps the MVP incomplete; it does not silently shrink the promised matrix. Accessible focus, keyboard operation, clear validation and non-color-only state are part of usefulness, especially when the workflow is an operator's only way to prepare configuration. **Justice/Adl** concerns who bears the usability burden; **Excellence/Ihsan** and **Truthfulness/Sidq** require evidence and accurate limits instead of inferring support from a framework or RID.

| Field | Assessment |
| --- | --- |
| Lifecycle / severity / claim type | open / high / usability and supported-host obligation |
| Principles / domains | Justice/Adl, Excellence/Ihsan, Truthfulness/Sidq / design, technical, operational, evaluation |
| Stakeholders | Operators on Linux, macOS and Windows; keyboard, screen-reader and localized-interface users |
| Provider-controlled decision | Surface packaging, protected-save host support, interaction semantics and advertised platform matrix |
| Evidence / validation level | E02-E03, E05 / documented current capability; no new-host or stakeholder validation |
| Mitigation | **IVSD-M003:** prove complete CLI, Terminal and Desktop native flows on each of the four target RIDs and the separate public Browser flow; preserve safe refusal on unverified filesystems; keep any unproved target RID as an open MVP blocker; test real focus/keyboard/status behavior and update public and technical guides together |
| Owner / next validation | Artifacts, CLI/TUI/Desktop, packaging and documentation maintainers; actual-host package/safe-save evidence for all four target RIDs and accessibility evaluation for claimed surfaces |
| Escalation boundary | Missing target-host proof blocks MVP completion; a proposed platform reduction requires an explicit user scope decision, while an unsafe save fallback requires security/privacy review |

### IVSD-F004: Universal definitions need neutral ownership and provenance

An Event-specific example is useful evidence, but if the format or bundled guidance quietly assumes Event concepts, other maintainers must imitate the wrong application. A family GUID, digest or publisher label can identify content without proving who authored or endorsed it. A copied source file or asset can also carry rights that the repository's MIT license does not grant. The provider controls bundled definitions, displayed provenance, package notices and the distinction between format validity and publisher trust.

The repository contract says Core is independent, definitions are bounded data, and publisher labels/GUIDs are not authentication. The native neutral sample and bundled Browser entry currently claim the same family/revision while differing in canonical content. An operator following a pinned handoff can therefore be shown an identity that describes two different definitions. The Phase 1 remedy is one public-safe canonical source for both surfaces, with a distinct Browser identity only as an honest temporary fallback that leaves the cross-surface MVP gate open. The proposed Grafana OSS v13.2.2 example is a candidate, not target-tested proof: official release, Docker-setting and provisioning-YAML facts still need an exact-release check, and the definition/output must be independently authored without copied upstream expression. **Truthfulness/Sidq** requires honest identity, provenance and curation labels; **Rights of People** protects contributors and rightsholders; **Justice/Adl** and **Promise-Keeping** support a genuinely usable path for non-Event maintainers without mandatory central approval. A neutral or Grafana vector proves only its tested local format behavior unless the target actually accepts it.

| Field | Assessment |
| --- | --- |
| Lifecycle / severity / claim type | open / medium / neutrality, trust-label and rights obligation |
| Principles / domains | Truthfulness/Sidq, Rights of People, Justice/Adl, Promise-Keeping / strategic, governance, technical, evaluation |
| Stakeholders | Definition authors, operators choosing definitions, contributors, third-party rightsholders and maintainers |
| Provider-controlled decision | Canonical family/revision binding across surfaces, definition vocabulary, bundled examples, provenance labels, notices and authoring claims |
| Evidence / validation level | E01-E02, E04-E07 / repository/strategy traceability; no external publisher or non-Event target validation |
| Mitigation | **IVSD-M004:** use one canonical public-safe neutral source for the native sample and Browser or assign a distinct identity until parity is proved; demonstrate local non-Event authoring and deterministic output without target-specific Core branches; verify exact Grafana release/interface facts and independently author the example; label family/revision/digest as identity rather than endorsement; preserve third-party identity, terms and notices; distinguish example, validated definition and target-tested claims |
| Owner / next validation | Core, Browser, example/documentation, dependency and release maintainers; Phase 1 canonical-digest assertion, Phase 2 clean-room Grafana review, independent non-Event vectors and package-closure checks |
| Escalation boundary | Unclear redistribution rights block the affected asset; promoted publisher trust or signed publication needs independent Configurator authority before activation |

## Recommendations

Use the existing offline Core and client boundaries as the MVP foundation. Require one complete operator path for local definitions and each advertised native output kind, with the same privacy and readiness rules at every surface. Verify the raw Browser resource as well as the rendered public-choice controls. Have Artifacts preflight every selected private destination before the first create-only commit, then report the exact committed prefix if publication stops. Keep the browser's public partial transfer explicit. Complete the four target native RIDs with actual host and protected-write evidence; reject unsupported filesystems safely without dropping a target OS from the MVP. Then teach exact filesystem limits in public and technical docs. Unify the neutral definition's canonical identity and review the independently authored Grafana candidate's facts and rights before claiming non-Event breadth.

Rejected alternatives: calling a partially packaged host “fully supported” without protected output shifts an avoidable confidentiality burden to users; weakening save protection to force parity conflicts with the existing restricted-output contract. A central catalog or publisher badge is unnecessary for local MVP usefulness and would imply authority that has not been established. A test runner, secret manager or target-apply layer would expand custody and operational duties without proving the core configuration handoff.

## Stakeholders

- Self-hosting operators who decide configuration and control their own files.
- People named or represented in private deployment configuration.
- Application maintainers and independent definition authors who need a neutral format.
- Automation callers that depend on stable machine-readable readiness and errors.
- Keyboard, screen-reader and localized-interface users across supported hosts.
- Contributors, third-party rightsholders and Configurator maintainers responsible for claims and redistribution.

## I-VSD Principles And Domains

The findings apply Trust/Amanah to custody and authority, Truthfulness/Sidq to support/readiness/provenance claims, Rights of People to privacy and intellectual property, Non-Harm to exposure and overwrite, Justice/Adl to accessible participation, Promise-Keeping to documented capability, Excellence/Ihsan to reliable workflows, and Avoiding Spying to an offline no-telemetry baseline. Strategic/governance review concerns neutral ownership and release claims; design/technical review concerns answer flow and interaction; operational/evaluation review concerns evidence on real hosts and with affected users.

Payment, riba, sponsorship, moderation, ranking and religious-content judgments are outside this utility MVP. No religious ruling follows from these design heuristics.

## Common Overlooked Failures And Outcomes

- A JSON/YAML renderer emits a secret in a machine error or terminal preview even though `.env` uses protected saves.
- A browser copy or export path accepts public free text or private selections through a permissive import.
- A static Browser definition embeds an actual secret or private path while its rendered controls correctly hide the field.
- A native bundle writes the first private file before detecting a predictable conflict in the second, or reports cancellation as if the first commit never happened.
- A macOS/Windows binary is documented as a full workflow even though restricted output still rejects there.
- A valid definition is presented as endorsed because it has a stable GUID or digest.
- A generated artifact is called “ready to deploy” despite missing target-owned validation, authorization or restart guidance.
- A non-Event example passes generic parser tests while the real Core still contains Event-only assumptions.
- Two neutral resources claim one family/revision with different canonical content, or a Grafana format example is described as target-tested without a target run.

## Validation Gaps

The revised universal-MVP plan and task mappings have been reviewed, but no builds, tests, package publishes, host filesystem experiments, accessibility studies, stakeholder interviews or non-Event target runs were performed for this report. Existing extraction results are historical and bounded. The strategy documents contain external citations, but this assessment did not independently verify those sources or the proposed Grafana exact-release facts; its source-free artifacts and redistribution rights remain Phase 2 gates. Privacy, usability and host claims need implementation and stakeholder/operational evidence at their respective gates. All four target native RIDs remain mandatory; missing protected-save or runner evidence keeps the MVP incomplete, while an unsupported filesystem fails closed.

## Escalation Needed

No religious-legal decision is needed for the bounded MVP. Escalate unclear component or example rights before redistribution to the Steward and qualified legal counsel. Escalate any proposal for private browser entry, remote definition authority, new secret custody, target apply or unverified protected-save claims to security/privacy review before expanding the plan. A material scope choice that changes these duties must be resolved in planning rather than hidden as a deferrable detail.

## Evidence Reviewed

| ID | Repository evidence and what it establishes |
| --- | --- |
| E01 | `AGENTS.md` and `docs/internal/QUICK_REFERENCE.md`: offline data-only Core, public browser boundary, protected native output, target authority, no telemetry and IP limits. |
| E02 | `PROJECTS.md`: seven product owners, dependency direction, protected paths, test owners and release/package gates. |
| E03 | `docs/public/browser.md`, `docs/public/native-output.md`, `docs/public/packaging.md`, `docs/internal/LAUNCH_CONTRACT.md`, `docs/internal/PROTECTED_ARTIFACTS.md`: current public transfer, Linux-only protected-save and advertised package limits. |
| E04 | `dev/active/universal-configurator.md`, sections on semantic definitions, data-only admission, output kinds and MVP sequencing: user-owned strategy direction, not implemented behavior. |
| E05 | `islamic-value-sensitive-design/workstreams/i-vsd-configurator-extraction.md`: historical extraction findings and bounded verification evidence. |
| E06 | `dev/active/expand.md`: user-owned testing/scenario expansion idea, explicitly outside the configuration-preparation MVP proposed here. |
| E07 | `dev/active/go-to-market.md` executive judgment: independent maintainer-owned definitions and optional discovery as strategy, not current publisher authority. |
| E08 | `dev/active/universal-mvp/universal-mvp-plan.md` §§3, 5–6, 9–10; `universal-mvp-tasks.md` Phases 1–2, 4–6, 9; and `universal-mvp-context.md` review state, as bound by the three SHA-256 values in Review Metadata. These establish S1.2a raw-resource/canonical identity, S2.4 bundle preflight/partial outcomes, S3/S4 host/browser boundaries, Grafana candidate/clean-room gates and owned work; they are planned safeguards, not execution evidence. |

## Missing Evidence

Source-grounded inventory of Event-only leftovers; a Phase 1 failing-then-passing canonical neutral digest and raw Browser resource privacy check; working non-Event definition vectors and target acceptance; exact-release Grafana source/provenance and redistribution review; actual JSON/YAML output, bundle preflight/partial-save and sensitivity tests; protected-write evidence on each proposed host; desktop packages beyond Linux; accessibility and stakeholder feedback. `docs/internal/universal-configurator.md` and `docs/internal/go-to-market.md` named by older references are absent in this checkout; the strategy texts presently reside under `dev/active/`.

## Context Inventory

The user asks for a multi-phase, repository-grounded move from Event extraction toward a common utility, a functional MVP across platforms, repository/architecture cleanup and public/internal documentation. They explicitly defer secret-manager integration and other non-core extensions and permit breaking changes in pre-release development. The current repository contract already owns the privacy and authority limits above. The corrected plan defines “all platforms” as the four existing native RIDs for CLI/Terminal/Desktop plus static Browser. An unsafe fallback cannot satisfy that goal, and missing target-host proof blocks completion.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
| --- | --- | --- | --- | --- |
| 2026-10-09 | none | draft | Shared evidence and strategy assessed; plan/task mapping pending | E01-E07 |
| 2026-10-09 | draft | current | Completed plan/task/context triad revalidated against all four findings | E08; scenarios S1.1, S3.1-S3.3, S4.1-S4.3, S5.1-S5.2 and mapped tasks |
| 2026-10-09 | current | current | Corrected target matrix revalidated; all four native RIDs mandatory, unsupported filesystems fail closed | E08; S3.3, S5.1 and tasks 4.1-4.4, 9.1 |
| 2026-10-09 | current | stale | CTO refinement added S1.2a raw-resource privacy/canonical identity and clarified shared Artifacts bundle publication and Grafana provenance | Revised plan/tasks/context before E08 revision binding |
| 2026-10-09 | stale | current | Planning-mode revalidation of all four findings against the exact revised triad; mappings and provider boundaries align | E08 and reviewed-input SHA-256 values; no implementation or host proof |

## Planning Handoff

- Workstream: universal-mvp
- Status: current
- Disposition: plan-aligned
- Reviewed input: repository contracts, strategy documents and exact revised `dev/active/universal-mvp/` plan/task/context triad at the SHA-256 values in Review Metadata
- Findings and mitigations: IVSD-F001 -> IVSD-M001; IVSD-F002 -> IVSD-M002; IVSD-F003 -> IVSD-M003; IVSD-F004 -> IVSD-M004
- Required plan mappings:
  - IVSD-F001 / IVSD-M001: S1.2a, S2.4-S2.5, S3.1-S3.4 and S4.2; tasks 1.1-1.3, 2.1-2.2, 3.1-3.3, 4.1-4.4, 5.1, 6.1-6.3 and 9.1. Phase 1 checks raw static Browser bytes; Phase 2 assigns shared preflight/create-only publication and partial-result semantics to Artifacts. Plan §10 additionally forbids secrets in argv, URLs, stdout, fixtures and logs.
  - IVSD-F002 / IVSD-M002: S1.1, S4.1-S4.2 and S5.2; tasks 1.2, 2.1-2.3, 5.1-5.4, 6.1-6.3, 7.1, 8.1-8.2.
  - IVSD-F003 / IVSD-M003: S4.1-S4.3 and S5.1; tasks 4.1-4.4, 5.2-5.4, 8.2 and 9.1. All four target RIDs require actual-host proof for CLI, Terminal and Desktop; an unproved RID blocks MVP exit, while an unsupported filesystem fails closed.
  - IVSD-F004 / IVSD-M004: S1.1-S1.2a and S5.2; tasks 1.1-1.3, 2.3, 6.1-6.3, 8.1-8.2 and 9.2. Phase 1 repairs the same-family/revision neutral digest and retains first-party provenance; 2.3 verifies exact Grafana facts and independent authorship before example use; 9.2 checks distribution notices and license closure.
- Escalations required before: implementation for new trust/custody/host-protection scope; redistribution for unclear rights; release for unsupported capability or publisher-trust claims.
- Refresh triggers: changed MVP scope, browser private entry, remote fetch/catalog authority, secret custody, target apply, telemetry, supported-host/output claims, provenance/publisher labels, or edits to a mapped mitigation or scenario.
