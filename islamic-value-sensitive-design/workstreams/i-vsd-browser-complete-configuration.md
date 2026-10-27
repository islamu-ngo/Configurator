# Complete browser configuration — Provider responsibility review

Last Updated: 2026-10-09

## Review Metadata

- Mode: plan-review.
- Subject: complete, locally evaluated browser configuration with voluntary secret entry.
- Workstream: `browser-complete-configuration`.
- Report kind: feature responsibility review.
- Report status: current.
- Disposition: plan-aligned.
- Evidence cutoff: 2026-10-09.
- Reviewed input: explicit implementation approval and working-tree changes over `de0665e05fc3981c7fb7e9cdd01a9c04f57c934b`; approved triad revision 1.
- Supersedes: none; this workstream changes the browser assumptions of the earlier universal-MVP assessment without replacing that assessment's other findings.

## Executive Summary

Self-hosters should be able to follow one application README link, configure their software and obtain its files without installing another interface. ISLAMU should offer that complete browser experience with secret entry disabled by default, voluntary session-only secret entry and usable draft files when secrets are deferred. The user's trust decision does not remove the provider's responsibility to prevent hidden transmission, retention or misleading readiness claims.

The working tree now replaces the partial browser with shared typed editing, opt-in secret custody, complete and deferred-secret preparation, explicit export, bounded source loading and public-only offline caches. The user authorized full implementation in this checkout; production publication and Git history remain separate. Core, Presentation, Browser, native callers and Event conformance have executed evidence. Actual published-browser checks cover consent, exact exports, offline relaunch, public caches, bounded tenants and a waiting update with two live workspaces. Final duplicate-row error association passed Red/Green, the full Browser suite and the latest published package. Plan alignment describes correspondence to the approved design; the separate evidence below establishes the stated implementation checks without certifying production outcomes.

## Scope

Covered: launch definition pointers, full fields, optional secret custody, deliberate copy/download, offline PWA assets, bounded tenant editing, stale-operation cleanup, accessible controls and truthful setup/readiness claims. Native protected saves and Event host authorization remain separate owners.

## Claim Boundary

This is software-provider responsibility reasoning, not a fatwa, religious certification, legal opinion, comprehensive security audit or evidence of stakeholder outcomes. Executed evidence establishes only the stated checks on this working tree and local published package. ISLAMU's nonprofit identity can inform a user's trust choice but is not cryptographic evidence about supplied code, definitions or custody.

## Findings

### IVSD-F001 — Users need a complete low-friction configuration path

At the original baseline, a README visitor encountered unavailable text, number and secret controls and needed a native interface to obtain configuration artifacts. That created avoidable friction for self-hosters. Promise-keeping and excellence require the advertised browser experience to support all admitted field types and artifact outputs; the user may decide to defer secrets without losing ordinary configuration work.

- Lifecycle: resolved; scope: this implementation; severity: medium; claim type: original implementation gap.
- Principles/domains: Promise-Keeping, Ihsan; product, UX, technical architecture.
- Stakeholders: self-hosters, application maintainers, users relying on the resulting deployment.
- Provider-controlled decision: completeness of the web interface and truthful readiness labels.
- Evidence: E01–E04, E09–E10; validation level: shared-engine tests and actual browser output vectors.
- Mitigation IVSD-M001: full definition-driven controls and complete artifacts, with explicitly incomplete secret-deferred drafts.
- Owner/next validation: Core/Presentation/Browser maintainers; preserve independent output and rendered-interaction coverage on changes.
- Escalation: implementation review before release; no religious-legal escalation identified.

### IVSD-F002 — Voluntary secret entry requires bounded custody

An operator may trust ISLAMU's serving origin and enter a password. ISLAMU still controls whether that value reaches URLs, diagnostics, stores, caches or network requests. Amanah and privacy require a clear opt-in for this session, masked controls, no silent reveal, no automatic clipboard writes and invalidation of late work after edits, trust revocation, navigation or disposal. Downloads and clipboard content intentionally leave the application's memory after an explicit user action; their subsequent lifetime cannot be guaranteed by the app.

- Lifecycle: resolved; scope: provider-controlled application sinks; residual hosted-browser/export custody accepted and disclosed; severity: high; claim type: exposure risk.
- Principles/domains: Amanah, Rights of People, Non-Harm, Avoiding Spying; privacy, technical and operations.
- Stakeholders: operators and people whose deployment credentials or private configuration could be exposed.
- Provider-controlled decision: consent, memory ownership, export and all application output sinks.
- Evidence: E03–E06, E09–E11; validation level: deterministic custody/race tests, independent source review and actual browser sink observations.
- Mitigation IVSD-M002: session-local consent and secret gating, no answer persistence/transmission, explicit export, generation fencing and owned-buffer disposal; preserve native restrictions.
- Owner/next validation: Browser/Presentation maintainers; repeat sentinel/race/platform checks when custody or serving code changes.
- Escalation: bounded independent privacy review completed; serving-origin and new custody changes require refreshed review before publication.

### IVSD-F003 — “Offline” must not promise more than the browser can enforce

WASM computes locally; a PWA adds cached assets and can relaunch offline after preparation. Neither fact alone proves that a hosted browser makes no requests. Service-worker update checks and browser-managed activity have separate lifecycles. Truthfulness requires distinguishing blocked application connections from device-level disconnection. A truly disconnected machine supplies the strongest no-network guarantee; an online app must not claim it controls browser extensions, the operating system or a compromised serving origin.

- Lifecycle: accepted; implemented controls and explicit browser-managed traffic limits; severity: high; claim type: architectural claim risk.
- Principles/domains: Sidq, Amanah, Non-Harm; operations, technical, evaluation.
- Stakeholders: cautious self-hosters and enterprise operators.
- Provider-controlled decision: explicit offline state, cache scope and network claims.
- Evidence: E07, E09–E11; validation level: policy tests and published offline relaunch/cache/request observations.
- Mitigation IVSD-M003: cache only the verified static app and deliberately selected public-safe definitions; preload before answers, reject application fetches in offline/answer sessions, defer update application until answers are cleared, test the published app offline and document browser-controlled limits.
- Owner/next validation: Browser/packaging maintainers; repeat actual cache/update/request observations for runtime or hosting changes.
- Escalation: release is blocked if application requests carry answers or offline relaunch depends on the network.

### IVSD-F004 — Definition pointers cannot grant publisher or target authority

Fetching a definition from an upstream application reduces dependence on an ISLAMU catalogue, but the document remains untrusted declarative input. An identity label, family GUID or digest establishes neither publisher authenticity nor target acceptance. The provider must bound the fetch and admission and preserve the pinned document rather than silently replacing it mid-session.

- Lifecycle: resolved; scope: implemented admission and authority boundaries; external endpoint/CORS deployment remains unverified; severity: high; claim type: trust-boundary risk.
- Principles/domains: Amanah, Sidq, Rights of People; technical, portability and governance.
- Stakeholders: maintainers, self-hosters and deployment owners.
- Provider-controlled decision: source fetch, digest checks, admission and UI authority claims.
- Evidence: E02–E04, E08–E11; validation level: hostile admission, identity/selector and stale-source tests; source review.
- Mitigation IVSD-M004: credential-free bounded HTTPS JSON retrieval before collecting answers, exact selector checks, canonical digest pinning, local-file alternative, no server proxy, no code/plugins/remote references or target connection/apply.
- Owner/next validation: Browser/Core maintainers; hostile source fixtures and stale-fetch races retained; deployment owner verifies the published CORS endpoint before advertising its link.
- Escalation: bounded independent source privacy review completed; new authenticated/remote launch authority would require fresh review.

### IVSD-F005 — Tenant editing must remain bounded and accessible

Operators should not need a development exporter to add a tenant. A repeatable editor should explain and enforce admitted limits and retain keyboard/focus access. A removed row must not remain in outputs or revive through a late import. Event's maximum of 256 manifest tenants is a host ceiling, not proof that every maximum-size setup fits Configurator's independent resource budgets.

- Lifecycle: resolved; scope: bounded typed editing and exercised keyboard/ARIA behavior; screen-reader and stakeholder validation remain absent; severity: medium; claim type: correctness and access risk.
- Principles/domains: Adl, Ihsan, Non-Harm; access, UX and technical.
- Stakeholders: small and larger self-hosters, assistive-technology users, target operators.
- Provider-controlled decision: editing, bounded collection admission and completeness claims.
- Evidence: E05, E08–E10; validation level: generic collection/native editing tests, independently accepted Event artifacts and actual published row/focus/error interactions.
- Mitigation IVSD-M005: bounded object-list support with add/remove controls, per-item validation, declared ordinal tenant-name uniqueness, snapshot invalidation and independent 0/1/2/3/limit/over-limit vectors. The user explicitly confirmed tenant editing in the 2026-10-09 intake answer.
- Owner/next validation: Core/Presentation/Browser maintainers and Event exporter owner; collection invariants and accessible interactions.
- Escalation: refresh this report if the confirmed tenant scope or field/cache custody changes materially.

## Recommendations

Deliver the browser as an equal configuration interface over the shared deterministic engine. Keep secret entry opt-in and provide secret-deferred files with explicit missing-value locations. Make the selected public-safe definition available offline deliberately, while never caching session answers or output. Include bounded tenant editing as explicitly selected by the user.

Rejected alternatives: mandatory native handoff fails the requested low-friction path; silent browser secret entry removes informed choice; a server-side definition proxy creates unnecessary authority and infrastructure; treating downloads as protected native writes overstates assurance; automatically activating a new PWA version in a live answer session risks unexpected code replacement.

## Stakeholders

Self-hosters decide where to enter credentials and receive usable files. Application maintainers own public definition metadata and semantic validity at the target. ISLAMU maintainers own the renderer, source loading, custody and honest capability descriptions. People served by configured applications bear consequences of wrong settings or leaked credentials. No stakeholder interview or usability study has been conducted for this workstream.

## I-VSD Principles And Domains

Following the existing universal-MVP review's selected principles: Amanah concerns custody and authority; Sidq concerns truthful offline/readiness/provenance claims; Rights of People and Avoiding Spying concern private data; Non-Harm concerns leakage and invalid output; Adl concerns accessible participation; Promise-Keeping and Ihsan concern a complete, reliable configuration path. These are design lenses, not religious-legal conclusions.

## Validation Gaps

The implemented engine and clients have executed checks recorded in E09. Actual local published-browser checks in E10 cover clipboard/download, consent/revocation, offline reload and fresh tab, local definition retention, cache contents and network sinks. Identified draft/duplicate error associations, final-row focus and Event active-bundle/empty-object issues were corrected and retested. Physical cross-platform testing, screen readers, stakeholder outcomes, production hosting and authenticated Event apply remain absent. Toolbar zoom was ignored by the headless browser; narrow reflow and 200% text enlargement are separate observations. Persisted BFCache was not forced. A real source endpoint returned 404; controlled browser admission/transport tests do not certify a deployed endpoint. The two-tab waiting update preserved live state; post-clear activation in that original context was not demonstrated, while a fresh context verified the final app version.

## Escalation Needed

Bounded independent privacy/source review and published offline/cache checks completed with no remaining concrete application finding. Production origin, externally hosted definition availability/CORS and any future authority/custody changes require their own evidence. No unresolved religious-legal question was identified. Deployment/domain choice and external Event writes remain separately authorized operations.

## Evidence Reviewed

- E01: explicit user request for one-link, self-sufficient browser setup and voluntary secret/offline modes.
- E02: `AGENTS.md`, matching intents, `PROJECTS.md`, `AI_REVIEW.md`, Quick Reference, path rules and Foundation at the commit above.
- E03: `docs/public/browser.md`, `docs/public/launch-integration.md`, `docs/internal/LAUNCH_CONTRACT.md`, and the supplied `docs/internal/reports/ISLAMU_CONFIGURATOR_AND_EVENT_READINESS_REPORT.md`.
- E04: `dev/active/universal-configurator.md` browser secret/artifact/offline headings; `dev/active/go-to-market.md` direct source handoff and voluntary browser-secret headings. Both are user-owned and preserved.
- E05: Core definition codec/validator/composer, shared form/workspace contracts, browser editor/template and affected test owners; independently counted 432 top-level Event fields: 285 public, 89 sensitive, 58 secret; 48 public enum/boolean choices.
- E06: native restricted-output and shared generation/owned-buffer contracts; existing browser boundary/lifecycle tests inspected, not run.
- E07: official [Blazor PWA documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/progressive-web-app?view=aspnetcore-10.0), [CSP connection control](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/connect-src), [service-worker update behavior](https://developer.mozilla.org/en-US/docs/Web/API/ServiceWorkerRegistration/update); retrieved with Firecrawl 2026-10-09. Functional API facts only; no external implementation source retained.
- E08: bounded read-only Event evidence: exporter `Program.cs`/`DefinitionBuilder.cs`, README, manifest schema and `ConfigurationManifestV1Alpha2.MaximumTenantCount = 256`; no Event test or modification.
- E09: current-session evidence under `.omo/evidence/20261009-browser-complete-configuration/`: Core 97/97; Presentation 36/36; Browser 81/81; Terminal 36/36 plus two focused row/focus tests; Desktop application 31/31. Core emitted eight actual Event artifacts; Event tests passed 15/15 and its independent exporter accepted all eight. Generated schema and capability freshness checks passed; locked solution restore, Release build and full solution formatting passed with zero build warnings/errors. Actual Browser package passed 1/1; compiled dependency closure passed seven owner rows. These are actual checks, separate from E05–E08 historical inspection.
- E10: actual Chromium on locally published static WASM: exact minimal and all-field output bytes with explicit clipboard/download, deferred-secret markers, consent revocation, offline reload and fresh tab, deliberate definition retention, bounded rows, Event 0/1/2/3 tenant outputs, connected ordinary errors, final-row keyboard focus, supported/conflicting imports and cache/storage/network sentinels. An update installed a waiting worker while two live workspaces retained their answers and consent. Controlled browser transport exercised credential/referrer omission, redirect rejection, 4 MiB overflow without Content-Length and a 10-second timeout. The real public source attempt returned 404; successful external admission is established only against a controlled HTTPS/CORS response. See `platform-published-qa.md` and its final narrow screenshot for actual observations and limits.
- E11: independent privacy source review of Core conditions, nested consent, aggregate budgets, source generation/admission, export invalidation and worker allowlists. Identified causal requiredness, native nested rows, budget recovery and source loading replacement issues received owner fixes and regressions. Official API research used Microsoft .NET 10 PWA and MDN Fetch facts without copying external implementation.

## Missing Evidence

An approved production origin, successful real remote admission, physical platform/screen-reader checks, stakeholder outcomes and authenticated target acceptance remain absent. Final package and known row accessibility corrections have passed their stated checks. Event's revision-5 definition contains 427 top-level fields, five artifacts and bounded object lists; its eight actual Core outputs passed independent wire/semantic acceptance. This does not establish deployment or apply authority. Consent remains a choice; missing evidence must not be replaced with confidence.

## Context Inventory

No server, identity provider, telemetry service, target API, secret-manager integration, account requirement or native save change was added. The existing desktop-packaging workstream and its report are unrelated and preserved. A change to remote code activation, telemetry, answer persistence or live apply changes this assessment materially.

## Common Overlooked Failures And Outcomes

Late uploads after revocation, secrets hidden inside object lists, cached output URLs, service-worker network fallback, private source URLs in errors, invalid JSON placeholder types, implicit ready labels on secret-deferred files, and silent definition updates all need explicit disposition. Browser-managed strings, clipboard history and downloaded files prevent a forensic-erasure guarantee.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence |
| --- | --- | --- | --- | --- |
| 2026-10-09 | none | draft | Integrated planning intake | E01–E08; triad revision 1 pending |
| 2026-10-09 | draft | current / plan-aligned | User confirmed tenant editing; completed triad mappings and independent design corrections revalidated | Triad revision 1; B01–B13 and tasks below |
| 2026-10-09 | current / planning | current / implementation revalidated | Explicit implementation approval; full owners, native callers, Event outputs, source review and actual published platform evidence | E09–E11; latest cache `configurator-app-8oPgRM97`; execution summary |

## Revision-bound plan mappings

Reviewed artifacts: `dev/active/browser-complete-configuration/browser-complete-configuration-plan.md`, `browser-complete-configuration-tasks.md`, `browser-complete-configuration-context.md`, all revision 1 dated 2026-10-09. Plan §9, the tasks mapping table and this report agree:

| Finding / mitigation | Named scenario and task mapping | Disposition |
| --- | --- | --- |
| IVSD-F001 / IVSD-M001 | B01 full controls, B03 deferred drafts, B04 strict complete output; shared draft tasks 1.1–1.3 and full workspace 2.1–2.4 | Implemented; E09/E10 output and interaction evidence |
| IVSD-F002 / IVSD-M002 | B02 consent, B05 sinks, B06 invalidation, B07 export; privacy/export tasks 2.1–2.5 and recursive collection tasks 4.1–4.3 | Implemented; E09–E11 custody, sink and review evidence; export limits retained |
| IVSD-F003 / IVSD-M003 | B09 offline, B10 public cache and updates; offline policy/runtime tasks 5.1–5.4 | Implemented; E10 actual offline/cache/two-tab waiting evidence; browser traffic limits accepted |
| IVSD-F004 / IVSD-M004 | B08 pinned source, B13 authority; source tasks 3.1–3.3 and Event data/link 4.4 | Implemented; E09–E11 hostile/controlled transport and authority evidence; deployed endpoint unverified |
| IVSD-F005 / IVSD-M005 | B11 bounded/unique tenants, B12 accessible controls; collection tasks 4.1–4.4, controls 2.3 and actual runtime 5.4 | Implemented; E09/E10 bounded vectors and actual focus/connected errors; screen readers unverified |

## Implementation Handoff

- Workstream: `browser-complete-configuration`.
- Status: current; disposition: plan-aligned.
- Reviewed input: approved triad revision 1 and implemented working tree, revalidated against owner tests, bounded independent review and published Chromium evidence.
- Findings and mitigations: IVSD-F001 → IVSD-M001; IVSD-F002 → IVSD-M002; IVSD-F003 → IVSD-M003; IVSD-F004 → IVSD-M004; IVSD-F005 → IVSD-M005.
- Required mappings: satisfied by the revision-bound table above, plan §9 and completed execution ledger; scoped findings resolved or residual limits explicitly accepted.
- Escalations required before: publication of a production origin or new source endpoint; neither deployment nor Git history was authorized or performed.
- Refresh triggers: changed secret default/custody/export, answer persistence, networking/cache/update policy, remote definition authority, tenant scope, target apply, telemetry or accessibility controls.
