# Browser Launch Readiness, Static Host Operations, and Qualification — Provider Responsibility Assessment

Last Updated: 2026-11-23

## Review Metadata

- Mode: post-implementation revalidation
- Subject: ISLAMU Configurator static Browser launch readiness, offline cache recovery, host operations, security governance, and qualification closure
- Workstream: browser-launch-readiness
- Report kind: implementation-verified assessment (Phases 1–6 executed; residual `4x` `QF01` & human/hardware release gates open)
- Report status: current
- Disposition: implementation-verified (external release gates open)
- Evidence cutoff: 2026-11-23
- Reviewed input: Phases 1–6 implementation (`e19a4e5`, `4e5334c`, `400323a`, `683b577`, `674269a`, `a771e4c`, `96f9240`, `395b890`), unified container `sha256:9eb64f06533d3059fab2053b46e92be19ce9ddc42dc98fb7521171a6af3c07d9`, and [`docs/internal/releases/browser-v1-qualification.md`](../../docs/internal/releases/browser-v1-qualification.md)
- Supersedes: none; closes `IVSD-F001`–`IVSD-F004` via executed code/container/CI evidence and advances `IVSD-F005` to `partially-mitigated` with explicit remaining `4x` `QF01`, Safari hardware, physical AT, zoom, and live Coolify edge gates

## Executive Summary

The Browser static WebAssembly application completed answer-fidelity fixes, virtualized continuous editing, Islamu Event theme/dock unification, and navigation separation through commit `623bbca`. Before operators can rely on a public or Coolify-hosted deployment, five provider-controlled operational and qualification gaps remain open: the static container misses deep-link routes (`/settings`, `/explore/{FamilyId}`), `/healthz`, self-probing container health, and HTTPS asset compression; offline initialization caches transient failures indefinitely without selective cache deletion or registry byte budgeting; direct-launch secret consent does not concisely warn operators about hosted-code/extension custody alongside deferred/native alternatives, while nested import conflicts drop inner property paths; [`SECURITY.md`](../../SECURITY.md) retains placeholder responder commitments without recurring advisory monitoring; and post-Wave-2 multi-engine, containerized host, and compound-root performance qualification remain unexecuted.

Addressing these gaps reduces avoidable launch failure, operator misdirection, and unverified runtime promises without introducing user accounts, server-side answer storage, telemetry, or backwards-compatibility shims. Planning alignment (`current / plan-aligned`) certifies only that the proposed design maps every provider responsibility coherently to testable scenarios, phased tasks, and honest escalation gates; actual container runs, live Playwright sweeps, physical screen-reader/zoom evaluation (`en`/`ar`), and steward responder confirmation remain required before public promotion.

## Scope

This assessment evaluates Group 1 (`browser-launch-readiness`), spanning six vertical slices:
- Phase 1 — Static container serving, route completeness (`/settings`, `/explore`, `/healthz`), genuine HTTPS compression, strictly loopback-fenced shell-free `--healthcheck`, repaired `Dockerfile.dockerignore` build context with permanent zero-Docker & zero-SecretKit architecture guards, and Store CI unversioned schema alignment (`Configurator.Browser.Host`, `src/Configurator.Browser/Dockerfile*`, `StandaloneArchitectureTests`, `validate-definition-store.yml`);
- Phase 2 — Browser offline worker initialization retry and clean-entry partial cache-eviction repair without listener/subscription leaks, zero-shim `BrowserOfflineRetentionStatus`, WCAG 2.2 AA `/open` workspace-fenced selective deletion with WAI-ARIA APG focus choreography, and atomic `32 MiB` aggregate canonical-byte budgeting preserving 4,096 immutable conflict pins (`pwa-lifecycle.js`, `BrowserOfflinePolicy`, `Open.razor`, `BrowserDefinitionRegistry`);
- Phase 3 — Core all-5-shape nested import `answer-conflict` schema-path preservation (keeping `Diagnostic.Location == null`), shared Presentation projection (`DefinitionFormModel.ImportAutofillSchema` / `ExplainImportSchema`), Browser Compare & Autofill UI surfacing, and concise `en`/`ar` hosted-code/extension custody disclosure with deferred-draft/native links beside `#secret-consent` and `/help#custody`;
- Phase 4 — Recurring read-only full-repository NuGet/license/Action-pin advisory workflow across all `src/`, `tests/`, and `eng/` `.csproj` files, Coolify exact-digest rollback & reverse-proxy procedures, and Steward-confirmed truthful `SECURITY.md` disclosure policy (removing `"Missing ... CSP headers"` from Out of Scope, confirming best-effort 7-day receipt acknowledgement via `contact@openislamu.org` by the student maintainer prior to ASBL registration with zero guaranteed remediation timeline);
- Phase 5 — Unified `eng/configurator/Configurator.BrowserQualification` pinning `Microsoft.Playwright` (`1.65.1`, `MIT` NuGet wrapper / `Apache-2.0` Playwright driver) strictly outside `src/`, emitting `ValidateSupportQualification.cs` receipts (`Q01–Q16`) and exact 46-sample `>= 36`-cohort Chromium `1x`/`4x` CDP timings for `MeasureInteractiveWork.cs`, with fail-closed `--self-test` and CI wiring;
- Phase 6 — Single unified embedded-Store container hardening run (`src/Configurator.Browser/Dockerfile`), live `BrowserHostPolicyTests`, `BrowserStorePackageTests`, `StandalonePackageTests/BrowserPackage*`, `ProfileWorkerHostTests.mjs`, multi-engine `Q01–Q16`, compound-root `1x`/`4x` benchmarks, measure-first `QF01` preparation gate, and durable knowledge graduation (`ADR-browser-coolify-static-image.md`, journal, `STATUS.md`, `OPERATIONS.md`, backlog).

Out of scope: server-side answer custody, connected host/SecretKit connectors (permanently separated from static Browser per `ADR-self-hosted-connected-browser-and-public-static-boundary.md` and `ADR-secret-store-integration-and-credential-sources.md`), non-Linux protected file writers, and hardware/human assistive-technology certification (explicitly retained as open release blockers). Already delivered items from `i-vsd-browser-production-v1.md` (`IVSD-F001`–`IVSD-F004`, `IVSD-F015`, `ef50715` discard confirmation, `575f62b` static Host replacement) are treated as closed baseline facts rather than re-planned work.

## Claim Boundary

This assessment establishes design-level I-VSD reasoning and exact traceability between findings, behavioral contracts, tasks, and escalation boundaries at planning time. It does **not** prove runtime pass outcomes, container execution, NuGet clean-room audit status, multi-engine browser behavior, compound-root latency budgets, Coolify origin TLS readiness, assistive-technology usability, or religious-legal rulings.

## Findings

### IVSD-F001: Static Host Route Gaps, Uncompressed HTTPS, and Unprobed Container Health Undermine Public Availability

- **Lifecycle:** mitigated (`e19a4e5`, `4e5334c`, container `sha256:9eb64f06533d...`, `BrowserHostPolicyTests` `40/40`, `StandaloneArchitectureTests` `31/31`)
- **Severity / Claim Type:** high / source-supported operational & availability defect
- **Principles / Domains:** Amanah (Trust), Ihsan (Excellence), Promise-Keeping / operational, technical
- **Stakeholders:** self-hosting operators, deep-link users opening `/settings` or `/explore`, Coolify/reverse-proxy operators
- **Provider-Controlled Decision:** explicit SPA fallback routes in [`Configurator.Browser.Host`](../../src/Configurator.Browser.Host/Program.cs), genuine HTTPS compression (`EnableForHttps = true`), `/healthz` + strictly loopback-fenced `--healthcheck` (no SSRF, `ASPNETCORE_HTTP_PORTS`-aware), repaired `Dockerfile.dockerignore` with zero-Docker & zero-SecretKit guards in `StandaloneArchitectureTests`, and [`Dockerfile`](../../src/Configurator.Browser/Dockerfile) `HEALTHCHECK`
- **Evidence IDs / Validation Level:** direct source inspection (`Program.cs:15-16,44-51`, `Settings.razor:1-2`, `Explore.razor:1`, `Dockerfile:1-47`, `Dockerfile.dockerignore:66-159`)
- **Linked Mitigation:** `IVSD-M001`
- **Owner / Next Validation:** Phase 1 Host & Container maintainer; genuine `https://` `BrowserHostPolicyTests`, `StandaloneArchitectureTests` (`Dockerfile.dockerignore` & zero-SecretKit guards), and Phase 6 unified embedded-Store container run
- **Escalation Boundary:** Coolify reverse-proxy TLS, domain routing, and runtime deployment credentials stay outside this repository

Direct navigation or page refresh to `/settings`, `/settings/{Section}`, or `/explore/{FamilyId}` returns HTTP `404` under `Configurator.Browser.Host` because its SPA fallback array only maps `/`, `/open`, `/help`, `/store/{slug:nonfile}`, and `/launch/{id:nonfile}`. In addition, `Dockerfile.dockerignore` omits 8 Wave 1–2 source directories (breaking container builds), response compression omits `EnableForHttps = true` (safe here because all served bytes are identical public static assets with zero cookies or secrets, and verified over genuine `https://` rather than plain HTTP), and neither `/healthz` nor a strictly loopback-fenced curl-free container `HEALTHCHECK` exists for Coolify health supervision.

### IVSD-F002: Sticky Failed Initialization and Opaque 8-Slot Retention Trap Offline Operators

- **Lifecycle:** mitigated (`400323a`, `BrowserOfflinePolicyTests` `17/17`, `BrowserOfflineWorkflowTests` `8/8`, `BrowserDefinitionStoreTests` `12/12`, live `Q10` Chromium+Firefox passed)
- **Severity / Claim Type:** medium-high / source-supported recovery & resource-bound gap (`IVSD-F007`/`IVSD-F016` continuation)
- **Principles / Domains:** Ihsan (Excellence), Promise-Keeping, Non-Harm (Adha) / design, technical
- **Stakeholders:** offline and constrained-network operators, constrained-memory browser devices
- **Provider-Controlled Decision:** retry semantics in `pwa-lifecycle.js`, workspace-fenced deletion UI on `/open`, and aggregate canonical-byte bounds in `BrowserDefinitionRegistry`
- **Evidence IDs / Validation Level:** direct source inspection (`pwa-lifecycle.js:193-266`, `BrowserOfflinePolicy.cs:25-150`, `Open.razor:134-155,437-444`, `BrowserDefinitionRegistry.cs:46,63`)
- **Linked Mitigation:** `IVSD-M002`
- **Owner / Next Validation:** Phase 2 Browser Offline & Registry maintainer; bUnit failing-before-fixing concurrency/capacity/budget tests and live worker inspection
- **Escalation Boundary:** persistent worker quota in Safari/mobile engines remains separately bounded until tested in `P47`

Caching a failed `initializeApp()` promise indefinitely prevents recovery after transient network or worker hiccups. Similarly, hitting the 8-definition retained cap shows a generic failure notice without per-entry deletion, and uncapped canonical bytes in `BrowserDefinitionRegistry` risk memory bloat across up to 4,096 entries while requiring careful eviction rules so pinned `(FamilyId, Revision) -> Digest` conflict history is never weakened.

### IVSD-F003: Hosted-Code Custody Blind Spots and Root-Only Import Conflicts Mislead Direct-Launch Operators

- **Lifecycle:** mitigated (`683b577` / `CHG-01M84PQCD72QG5CE1AZWZCXX8S`, `DefinitionImportTests` `16/16`, `DefinitionImportComparisonTests` `26/26`, `BrowserImportComparisonTests` `16/16`, `BrowserSecretBoundaryTests` `17/17`, live `Q04`/`Q06` Chromium+Firefox passed)
- **Severity / Claim Type:** medium-high / source-supported consent transparency & correction usability gap (`IVSD-F013`/`IVSD-F014` continuation)
- **Principles / Domains:** Sidq (Truthfulness), Amanah (Trust), Avoiding Deception / design, technical, communication
- **Stakeholders:** first-time direct-launch operators, returning operators repairing conflicting configuration files
- **Provider-Controlled Decision:** consent-adjacent trust/extension disclosure, deferred/native escape links, and schema-safe nested conflict diagnostics
- **Evidence IDs / Validation Level:** direct source inspection (`BrowserStrings.resx:348,381,405`, `Help.razor:17-21`, `launch-integration.md:54-65`, `DefinitionImport.cs:203-240`, `DefinitionImportProjection.cs:468-482`, `ConfigurationEditor.Import.cs:134,272`)
- **Linked Mitigation:** `IVSD-M003`
- **Owner / Next Validation:** Phase 3 Core & Browser maintainers; synthetic secret/conflict redaction tests (`en`/`ar`) and WCAG-compliant inline focus checks
- **Escalation Boundary:** formal user comprehension testing stays open until human usability sessions run (`definition-profile-target-adoption-trials.md`)

Operators clicking a third-party README deep link see that secrets stay in browser memory, yet are not immediately reminded next to `#secret-consent` that hosted web code and browser extensions can still inspect active secrets — nor immediately pointed to secret-deferred draft exports or native CLI/Desktop builds. Meanwhile, nested JSON/YAML import conflicts discard inner declared property segments (`new("answer-conflict", descriptor.Id)`), forcing blind file inspection while privacy rules still demand never leaking raw imported private keys or values.

### IVSD-F004: Unbounded Disclosure Promises and Unscheduled Vulnerability Audits Erode Operational Trust

- **Lifecycle:** mitigated (`674269a`, `a771e4c` / `CHG-01M84Q16ZNBE1ZRCQA6WYPWXCW`, `ReleaseInputPolicyTests` `63/63`, `validate-action-pins.cs` `18` refs passed, Steward disclosure confirmation applied in `SECURITY.md`)
- **Severity / Claim Type:** high / source-supported governance & operations gap (`IVSD-F008`/`IVSD-F012` continuation)
- **Principles / Domains:** Amanah (Trust), Wafa bil-'Ahd (Promise-Keeping), Sidq (Truthfulness) / operational, governance
- **Stakeholders:** security researchers, volunteer maintainers, self-hosting operators
- **Provider-Controlled Decision:** recurring CI full-closure NuGet/license/pin workflow across all `src/`, `tests/`, and `eng/` projects, explicit Coolify exact-digest rollback procedures, removal of `SECURITY.md:24` CSP Out-of-Scope contradiction, and Steward-confirmed truthful acknowledgement commitment
- **Evidence IDs / Validation Level:** direct inspection (`SECURITY.md:24,75-76`, `.github/workflows/build-test.yml:131-170`, `.github/workflows/validate-definition-store.yml:9,21,84`) and Steward confirmation (`2026-11-23`)
- **Linked Mitigation:** `IVSD-M004`
- **Owner / Next Validation:** Phase 4 Operations/Steward + CI maintainer; workflow validator run (`validate-action-pins.cs`), `ReleaseInputPolicyTests`, and documentation link checks
- **Escalation Boundary:** **Steward Escalation Gate Confirmed (`2026-11-23`)** — Project Steward confirmed that Configurator is currently maintained by a single student prior to ASBL registration: `SECURITY.md` commits to best-effort receipt acknowledgement and initial reply via `contact@openislamu.org` within 7 days while explicitly stating there is no guaranteed remediation/resolution timeline (volunteer capacity may pause during exams, multi-project focus, or tooling limits). Never fabricate a backup team or staffed SLA.

`SECURITY.md:24` erroneously lists `"Missing ... CSP headers"` as Out of Scope despite Browser Host CSP being an enforced security invariant, while `SECURITY.md:75-76` left response expectations informal without distinguishing timely email acknowledgement (7 days best effort) from unguaranteed student-maintainer remediation capacity. Separately, `.github/workflows/validate-definition-store.yml` references a removed `-v1.schema.json` filename, and NuGet vulnerability audits lack a scheduled weekly cadence covering all `src/`, `tests/`, and `eng/` `.csproj` lock graphs.

### IVSD-F005: Unexecuted Post-Refactor Browser Matrix and Unmeasured Compound-Root Performance Risk Quiet Regressions

- **Lifecycle:** partially-mitigated (`96f9240`, `395b890`, `32/32` Chromium+Firefox `Q01–Q16` + `ValidateSupportQualification.cs` exit `0`, `36/36` core CDP cohorts & `4/4` `1x` `QF01` passed; `4x` `QF01` breaches on `wide-nested-object`/`large-map-list`, `3/20` extended scale tail outliers, Arch WebKit launch failure, and human AT/zoom/Safari/Coolify TLS release gates remain explicitly open in `docs/internal/releases/browser-v1-qualification.md`)
- **Severity / Claim Type:** high / missing runtime qualification evidence (`IVSD-F005`/`IVSD-F011`/`IVSD-F017` continuation)
- **Principles / Domains:** Ihsan (Excellence), 'Adl (Justice), Sidq (Truthfulness) / evaluation, technical
- **Stakeholders:** accessibility/keyboard users, low-resource/mobile operators, release reviewers
- **Provider-Controlled Decision:** consolidating `Configurator.BrowserQualification` (`Microsoft.Playwright` `1.65.1`, `MIT` NuGet wrapper / `Apache-2.0` Playwright driver + Chromium CDP feeding `ValidateSupportQualification.cs` and `MeasureInteractiveWork.cs` with `>= 36` cohorts and 46 samples/cohort) and enforcing a measure-first `QF01` prep budget gate
- **Evidence IDs / Validation Level:** direct inspection (`STATUS.md:29-51`, `OPERATIONS.md:2626-2660,2753`, `MeasureInteractiveWork.cs`, `ValidateSupportQualification.cs`, `BuildInteractiveWorkFixtures.cs`)
- **Linked Mitigation:** `IVSD-M005`
- **Owner / Next Validation:** Phases 5–6 Browser Qualification & Architecture maintainers; executed Playwright `Q01–Q16` (`ValidateSupportQualification.cs` receipt) + 46-sample `>= 36`-cohort compound-root AOT JSON (`MeasureInteractiveWork.cs`), unified container checks, and `validate-dependency-license-policy.cs` pass
- **Escalation Boundary:** **Release Gate** — physical screen-reader/braille (`en`/`ar`), manual `200%`/`400%` zoom, Safari/Firefox on non-Linux hosts, and live Coolify origin TLS checks require human/external verification and remain explicit non-automated limits in `STATUS.md`

Unit/bUnit green counts cannot detect live WASM contrast regressions, service-worker caching glitches, `compact-600` 4x CPU frame spikes (`>200ms` outliers noted in `OPERATIONS.md`), or deep recursive compound-root stalls. Consolidating automation into one `eng/`-only `MIT`/`Apache-2.0` .NET runner prevents diverging scripts while transparently surfacing human/hardware limits.

## Recommendations

Implement Phases 1–6 cleanly without backwards-compatibility shims:
1. Prefer strictly loopback-fenced self-executing `--healthcheck` in `Configurator.Browser.Host.dll` over adding `curl` to `mcr.microsoft.com/dotnet/aspnet:10.0.10-noble` (preserving the minimal OS dependency closure in `docs/internal/legal/dependencies/browser-container.md`) and guard `Dockerfile.dockerignore` + zero-SecretKit isolation in `StandaloneArchitectureTests`.
2. Keep `BrowserDefinitionRegistry` `(FamilyId, Revision) -> Digest` conflict history immutable while capping retained active canonical bytes atomically (`<= 32 MiB` default aggregate ceiling across `Current`), and use shared Presentation `ImportAutofillSchema` / `ExplainImportSchema` so nested conflict paths never leak dynamic map keys or answer values.
3. Unify Playwright E2E and Chromium CDP latency measurement inside `eng/configurator/Configurator.BrowserQualification` feeding `ValidateSupportQualification.cs` and `MeasureInteractiveWork.cs` (`>= 36` cohorts, 46 samples/cohort), and use empirical 1x/4x prep timings before introducing incremental `DefinitionPreparationSession` scheduling (`QF01`).
4. Rejected alternatives: NGINX sidecar (`575f62b` already retired it), separate hand-rolled `ClientWebSocket` CDP harness (`MeasureBrowserInteractions.cs` duplicates Playwright lifecycle), obsolete dual `Store=true`/`Store=false` containers (Store is embedded in `Configurator.Presentation`), and fabricated backup-team or remediation SLAs in `SECURITY.md`.

## Stakeholders

End-user operators (including low-vision, keyboard-only, Arabic RTL, and offline/constrained-device users), first-time README deep-link arrivals, returning operators migrating configs, application publishers hosting schemas, self-hosting Coolify operators, security reporters, and Configurator maintainers.

## I-VSD Principles And Domains

- **Amanah (Trust) & Promise-Keeping:** Truthful container health/routes, non-fabricated security SLAs, retryable offline state, explicit hosted-code risk visibility.
- **Sidq (Truthfulness):** Clear `CapacityReached` vs storage failures, declared conflict paths without raw secret leakage, separating automated Playwright passes from unverified physical AT/zoom claims.
- **'Adl (Justice) & Ihsan (Excellence):** Equal deep-link route reliability (`/settings`, `/explore`), compound-root `4x` CPU checks protecting slower hardware, concise EN/AR wording parity.
- **Non-Harm (Adha) & Avoiding Spying:** Zero answer persistence/logging on host/caches, zero extra container attack surface (`no curl`), zero secret values in conflict diagnostics.

## Common Overlooked Failures And Outcomes

- Enabling HTTPS compression in ASP.NET Core without verifying no user secrets/cookies share compressed responses (`Program.cs` has zero auth/cookies/user-echo, so BREACH risk is avoided; adding future auth would invalidate that assumption).
- Adding `Dockerfile HEALTHCHECK CMD curl ...` when `aspnet:10.0.10-noble` omits `curl`, causing every deployed container to become `unhealthy`.
- Deleting a retained definition while a workspace is active or letting registry byte eviction erase `(FamilyId, Revision) -> Digest` conflict pins.
- Emitting dynamic map keys inside `answer-conflict` nested `Location` paths, leaking private keys into diagnostics.
- Treating headless Chromium checks as proof of NVDA/VoiceOver/Orca (`en`/`ar`) or Apple WebKit offline storage behavior.

## Validation Gaps

Planning only — no product tests, Docker builds, Playwright runs, or NuGet audits were executed during planning. Prior Wave 2 baseline (`Core 968`, `Presentation 495`, `Browser 668`, `OPERATIONS.md:2744-2752`) covers pre-plan code; all new invariants require observed Red failures followed by Green passes and fresh container/Playwright artifacts during implementation.

## Escalation Needed

1. **Resolved (`2026-11-23`) — Phase 4 Commit 4.2 (`SECURITY.md`):** Project Steward confirmed single student maintainer status prior to ASBL registration, best-effort receipt acknowledgement and initial response via `contact@openislamu.org` within 7 days, no guaranteed remediation/resolution timeline (volunteer capacity may pause for exams, other ISLAMU projects, or tooling limits), and moving Browser Host CSP back into scope (`IVSD-F004`).
2. **Open — Before public Coolify origin promotion:** Human assistive-technology evaluation (`en`/`ar`), manual `200%`/`400%` zoom, Safari on macOS/iOS hardware, Coolify edge HTTPS/reverse-proxy response inspection, and accountable operator credentials/rollback drill (`STATUS.md:29-51`, `IVSD-F005`).
3. No religious-legal/fiqh ruling is required; no financial/monetization flows are touched.

## Evidence Reviewed

- Code: `src/Configurator.Browser.Host/Program.cs`, `src/Configurator.Browser/{Dockerfile,Pages/Settings.razor,Pages/Explore.razor,Pages/Open.razor,Pages/Help.razor}`, `src/Configurator.Browser/wwwroot/{pwa-lifecycle.js,service-worker.published.js}`, `src/Configurator.Browser/Services/{BrowserOfflinePolicy.cs,BrowserDefinitionRegistry.cs}`, `src/Configurator.Browser/Resources/BrowserStrings.{resx,ar.resx}`, `src/Configurator.Core/Definitions/{DefinitionImport.cs,DefinitionImportProjection.cs,DefinitionDiagnostic.cs,DefinitionCorrectionLocation.cs,DefinitionLocationSegment.cs}`, `src/Configurator.Browser/Components/Configuration/ConfigurationEditor.Import.cs`.
- CI/Tests/Tooling: `.github/workflows/{build-test.yml,validate-definition-store.yml}`, `.ci/policy/dependency-licenses.json`, `.ci/scripts/{validate-nuget-vulnerabilities.cs,validate-dependency-license-policy.cs}`, `tests/Configurator.Architecture.Tests/BrowserHostPolicyTests.cs`, `tests/Configurator.Browser.Tests/Offline/BrowserOfflinePolicyTests.cs`, `tests/Configurator.Core.Tests/Definitions/DefinitionImportTests.cs`, `eng/configurator/{Configurator.Release.proj,MeasureInteractiveWork.cs,BuildInteractiveWorkFixtures.cs,ServePublishedBrowser.cs}`, `Configurator.slnx`, `Directory.Packages.props`.
- Docs/Prior Reports: `AGENTS.md`, `PROJECTS.md`, `SECURITY.md`, `docs/internal/{STATUS.md,OPERATIONS.md,PACKAGING.md,TESTING.md,LAUNCH_CONTRACT.md,PRESENTATION_STATE.md}`, `docs/internal/legal/dependencies/browser-container.md`, `docs/public/{browser.md,launch-integration.md}`, `dev/backlog/{browser-production-v1-followups.md,browser-interaction-performance-followups.md,cross-platform-quality-required-followups.md}`, `islamic-value-sensitive-design/workstreams/{i-vsd-browser-production-v1.md,i-vsd-browser-interaction-performance.md}`.
- External Research (2026-11-22): `learn.microsoft.com` ASP.NET Core 10 static files / `EnableForHttps` / BREACH guidance; `dotnet-docker` curl-free container `HEALTHCHECK` self-probe pattern; `nuget.org` / `playwright.dev` `Microsoft.Playwright` `1.65.1` (`Apache-2.0`, `net10.0`) & `NewCDPSessionAsync` `Emulation.setCPUThrottlingRate`; `developer.mozilla.org` ServiceWorker `CacheStorage` quota/retry patterns.

## Missing Evidence

- Bounded `4x` throttled near-limit preparation (`QF01` `wide-nested-object` `214.1ms` / `large-map-list` `224.4ms` vs `200ms`) and extended-scale tail remediation (`few-root-deep-tree` `4x` reverse `407.1ms`, `compact-600` `1x` first-use `50.5ms` / `4x` outlier `214.2ms`).
- Real Apple WebKit / Safari execution on physical macOS and iOS hardware (Linux Playwright WebKit `v2388` failed launch on Arch due to missing Ubuntu `libicu74`/`libxml2`).
- Physical screen-reader/braille (`en`/`ar`), manual `200%`/`400%` browser UI zoom, and live Coolify origin HTTPS TLS & rollback drill (remain human/external release gates).

## Context Inventory

- Workstream triad: `dev/active/browser-launch-readiness/browser-launch-readiness-{plan,tasks,context}.md`.
- Backlog coordination: `dev/backlog/README.md` (tracks Group 1–5 organization).
- Qualification record: [`docs/internal/releases/browser-v1-qualification.md`](../../docs/internal/releases/browser-v1-qualification.md).

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
| --- | --- | --- | --- | --- |
| 2026-11-22 | none | current | Initial Group 1 implementation planning & I-VSD revalidation | Product `623bbca` + `dev/active/browser-launch-readiness/*` triad (plan-aligned) |
| 2026-11-22 | current | stale | Senior CTO audit tightened trust boundaries (`--healthcheck` SSRF fence, `Dockerfile.dockerignore` guard, CSP scope fix, shared Presentation conflict projection, zero-SecretKit guard) and split Phase 5 into Phases 5–6 | CTO-refined triad `2026-11-22` |
| 2026-11-23 | stale | current | Pre-execution I-VSD revalidation aligning 6-phase mappings, unified Store container, `MIT`/`Apache-2.0` Playwright closure, and Steward-confirmed `IVSD-F004` disclosure commitment | CTO-refined triad + Steward confirmation `2026-11-23` (plan-aligned) |
| 2026-11-23 | current | current | Post-implementation Phase 1–6 execution & live container/Playwright/CDP qualification cutoff (`IVSD-F001`–`IVSD-F004` mitigated; `IVSD-F005` partially mitigated with explicit `4x` `QF01` & external release gates) | Commits `e19a4e5`..`395b890` + [`browser-v1-qualification.md`](../../docs/internal/releases/browser-v1-qualification.md) |

## Planning Handoff

- Workstream: `browser-launch-readiness`
- Status: `current` (`plan-aligned`)
- Reviewed input: `623bbca` + CTO-refined 6-phase `dev/active/browser-launch-readiness/browser-launch-readiness-{plan,tasks,context}.md` + Steward `IVSD-F004` confirmation (`2026-11-23`)
- Findings and mitigations: `IVSD-F001 -> IVSD-M001`, `IVSD-F002 -> IVSD-M002`, `IVSD-F003 -> IVSD-M003`, `IVSD-F004 -> IVSD-M004`, `IVSD-F005 -> IVSD-M005`
- Required plan mappings:
  - `IVSD-F001 / M001` -> Scenarios `R1.1`–`R1.3` (Phase 1: `TASK-1.1`–`TASK-1.3`)
  - `IVSD-F002 / M002` -> Scenarios `R2.1`–`R2.3` (Phase 2: `TASK-2.1`–`TASK-2.3`)
  - `IVSD-F003 / M003` -> Scenarios `R3.1`–`R3.2` (Phase 3: `TASK-3.1`–`TASK-3.3`)
  - `IVSD-F004 / M004` -> Scenarios `R4.1`–`R4.2` (Phase 4: `TASK-4.1`–`TASK-4.3`; Steward disclosure terms confirmed `2026-11-23`)
  - `IVSD-F005 / M005` -> Scenarios `R5.1`, `R6.1`–`R6.2` (Phase 5: `TASK-5.1`–`TASK-5.2`; Phase 6: `TASK-6.1`–`TASK-6.3`; retains human AT/zoom/Safari/Coolify TLS release gates)
- Escalations required before: public deployment (`IVSD-F005` human AT/zoom/Safari hardware/Coolify edge TLS)
- Refresh triggers: changing Browser Host GET/HEAD static boundary or SecretKit isolation, weakening `--healthcheck` loopback fence, altering `BrowserDefinitionRegistry` conflict retention, loosening consent revocation or secret diagnostic redaction, switching automation dependency away from `MIT`/`Apache-2.0` Playwright, or changing SLA/publication commitments.
