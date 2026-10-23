# Browser interaction performance - Required followup slices

Last Updated: 2026-10-22 Europe/Brussels

Status: required deferred scope, not implemented, approved or qualified.
Owner: Core/Presentation/Browser maintainers for P; hosting maintainer for H;
qualification maintainer for Q. Actual people are not assigned by this review.
Origin: CTO right-sizing of browser-interaction-performance R2 at product
`733aba5fceb41ec1c0c6337525ccdee67a8093e0`.
Primary slice: [form computation decision](../../docs/internal/adr/ADR-browser-form-computation.md).
The R3 interaction implementation and reference-host qualification are complete;
the slices below remain required and unimplemented.

## Delivery Boundary

The active slice repairs disclosure/search/Review computation and rendering.
R2 also proposed a Core evaluator, prepared-buffer ownership, Browser scheduling,
HTTPS compression and a custom CDP tool across six phases/15 major tasks.
These independent risks must not become one mega-PR.

Deferred means required outside the primary PR, not waived or complete. Preserve
original R2 scenario/task IDs below as historical traceability, not execution
order. Each slice needs its own current repository-grounded triad, I-VSD
revalidation, user approval, Red -> Green -> Refactor, exact atomic commit packets
and actual acceptance evidence. No retained message is authority to commit.

| Slice | Outcome / old task mapping | Dependency / activation gate |
| --- | --- | --- |
| P Responsive preparation | S5-S9,S12,S14; old 3.1-3.3,4.1-4.2,5.1-5.2 and preparation portion of 6.2 | Re-baseline capture/evaluation/adoption/retirement bounds and ownership; can use primary facts but must not assume they solve preparation |
| H Secure HTTPS delivery | S13; old 6.1 | Independently shippable, no dependency on P or the interaction PR; qualify real static/container transport |
| Q Repeatable qualification tooling | old 6.2-6.3 tool/broader-evidence portion | Existing tooling can measure primary slice now; admit a narrow native tool only if repeatability justifies it, without a second overlapping harness |

P and H remain required fixes for the original full goal. Q owns the retained
automation proposal; its exact mechanism may be replaced after checking the
existing production-v1 qualification backlog. Do not implement two CDP/Playwright
frameworks for the same checks. AOT and broader physical-device research are
optional investigations, not replacements for required behavior.

## P: Responsive Preparation - Retained Behavioral Specification

**S5:** A large single output shows acknowledgement before heavy work, admits
intervening input/cancellation between bounded portions and exposes success only
for the completed current operation.

**S6:** Equal immutable inputs and selections produce identical canonical bytes,
digests, output order, omission/withholding decisions, diagnostics and per-artifact
draft completion locations regardless of partitioning.

**S7:** Rejection/cancellation/fault exposes no partial output, reports truthful
status and retires private buffers.

**S8/S9:** Revocation, edits, replacement and disposal deny old capabilities
synchronously; late work cannot restore them. No private sentinels enter network,
storage, public caches, worker messages, logs or evidence.

**S12/S14:** Preparation remains keyboard/status operable and works offline with
the public scheduling asset admitted before answers. Updates never replace a live
workspace. Local preparation never grants target acceptance/apply authority.

### Verified Defect And Unresolved Boundaries

Browser `ConfigurationEditor.razor.cs:210,236-267` snapshots, composes a temporary
bundle, disposes it and recomposes each admitted artifact through operations.
The separate preparation evidence audit confirmed per-artifact validation too.
These are source facts, not measured CPU attribution.

The following work is not bounded merely by an `Advance(workQuota)` API:

- `SetupDefinition.cs:139-157`: snapshot canonicalization, parse and clone before
  preparation starts.
- `DefinitionArtifactComposer.cs:20-21,38-54`: whole-buffer constructor clone/hash,
  defensive `Bytes` copy and synchronous zeroization.
- `SetupPresentationContracts.cs:37-48`: output adoption copy and synchronous
  clear. Browser export introduces another owned copy.
- Canonical ordinal map ordering, normalized duplicate detection, long strings,
  decimal normalization, encoding and digest finalization. Canonicalization is
  also used by admission/snapshots, not only output.

Minimum acceptable design: enumerate every owner and atomic CPU/allocation span
from click through final frame and cancellation. Either include a span within
bounded advancement or establish an explicit measured atomic ceiling under current
admitted limits. Never silently claim constant wall-clock work from structural
quotas. Cancellation latency includes synchronous erasure; do not yield halfway
through denying or clearing protected output to obtain a better timing.

### Retained Architecture And Alternatives

One preparation-specific Core `DefinitionPreparationSession` owns admitted
definition, immutable answers, bounded requested outputs and complete/draft mode.
Proposed methods: `Advance(workQuota)`, single terminal `TakeResult()`, synchronous
`Dispose()` clearing partial/unclaimed buffers. No JS, scheduler, trusted-
validation bypass or Browser service enters Core. Small evaluation/writer cursors
share the authoritative evaluator with genuine native synchronous entrypoints;
no fast/legacy duplicate engine.

Retained initial quota hypothesis: 128 structural steps and 4 KiB scalar/output
work per advance. Tune from actual traces, never treat an entire large string
or subtree as one cheap step. Keep JSON/YAML/dotenv/Markdown bytes and digests,
canonical identity, limits and complete/draft semantics. Draft bundles may carry
valid artifacts and withholding diagnostics while `Succeeded` is false; do not
reduce terminal state to a misleading success Boolean.

Owned prepared-output adoption into `DefinitionPresentationOperation` must be
disposable before execution, create the existing owned-copy outcome, clear
temporary buffers in `finally`, and preserve both workspace operation-generation
and Browser editor-generation fences. Metadata is value-free; buffers are not.
Never-started, cancelled, rejected, stale, accepted and disposed owners each have
one retirement path. No borrowed retained view or asynchronous erasure is admitted.
The unchanged copying seam cannot be called bounded without the proof above.

Browser owns scheduling and truthful idle/preparing/cancelled/failed/ready states.
Proposed `BrowserPreparationScheduler` / `browser-scheduling.js` carry no private
payload. A real browser task boundary (`scheduler.yield()` where admitted, otherwise
`MessageChannel`) and initial render opportunity are required; microtask-only
yielding and `Task.Run` do not prove main-thread responsiveness. Preload before
workspace custody, include the exact public asset in reviewed worker inventory,
dispose resources and check generation after every await. Block duplicate starts.

Alternatives: synchronous composition is smaller but may block one large artifact;
yielding between artifacts has the same gap. A worker isolates CPU but changes
forbidden answer custody and lifecycle. AOT may speed execution but removes
neither duplicate work nor missing event-loop opportunities. Do not use any of
these as a waiver of the measured end-to-end requirement.

### P Red And Acceptance

Before implementation: literal independent vectors for every format/mode,
withheld artifacts, completion paths, limits, long scalar/wide map, repeated
terminal access, dispose-before-start, cancellation/fault at each ownership
boundary, reentrant replacement and stale adoption. Real Core/workspaces, no
internal mocks; controlled scheduling gates, no sleeps. Keep already-passing
anchors honestly distinct from newly failing regressions.

At 1x, a near-limit single-output input/cancel action must be processed within
100 ms without partial publication; at 4x feedback/cancel <=200 ms. Preserve
ordinary control/search budgets in the active plan. Measure capture, evaluation,
adoption, encoding/hash/copy and erasure, not only advancement. These remain
provisional product budgets, not achieved results.

### P Paths, Sequence And Commit Outcomes

Re-baseline exact ownership before execution. Original bounded path inventory:

- Core Definitions: existing `DefinitionValidator.cs`, `DefinitionJson.cs`; new
  `DefinitionEvaluation.cs`.
- Core Artifacts: existing artifact/complete/draft bundle composers; new
  `DefinitionPreparationSession.cs`, `DefinitionArtifactWriter.cs`.
- Core tests: existing `DefinitionBundleTests.cs`, `DefinitionDraftArtifactTests.cs`;
  new `DefinitionPreparationSessionTests.cs` under `Definitions/`.
- Presentation: existing `DefinitionPresentationOperation.cs` and its tests;
  existing outcome/workspace owners only if new boundedness proof requires them.
- Browser: existing editor, export/lifecycle/offline fixtures/tests and published
  worker; proposed scheduler/bridge and `BrowserPreparationWorkflowTests.cs`.
- Docs: Core/foundation/presentation contracts, public browser/launch integration,
  technical launch; final preparation ADR and actual evidence.

Retained atomic outcomes, not approved executable packets:
`perf(artifacts): prepare bundles in bounded evaluation steps`;
`refactor(architecture): adopt prepared artifacts with explicit ownership`
(internal skip trailers required);
`fix(browser): keep preparation responsive to edits and cancellation`.
Integrate dependent callers within each buildable increment and delete obsolete
constructors after their last actual caller migrates; no final compatibility facade.
Each later implementation phase needs its own Release build and selected Core,
Presentation or Browser owner. Final actual Browser evidence is not optional.

## H: Public HTTPS Delivery

Existing `src/Configurator.Browser.Host/Program.cs` adds response compression and
WASM MIME but does not opt into HTTPS compression. Historical local response was
uncompressed; this does not explain no-request control latency.

S13: negotiate Brotli/gzip for public assets over HTTPS, correct `Vary`/MIME,
unencoded requests still work and missing assets remain real errors. Keep CSP,
no request logging, GET/HEAD restrictions and no answer processing. No new
certificate secret, publisher or production deployment authority.

Exact initial paths: host Program, `docs/public/packaging.md`,
`docs/internal/PACKAGING.md`. Retained commit outcome:
`perf(self-hosting): compress public assets over HTTPS` (public performance).
Remove the old artificial dependency on Browser preparation. Qualify actual host/
container responses and edge behavior separately from static package tests.
The user's existing Coolify installation remains the delivery target; no
replacement hosting architecture is introduced.

## Q: Qualification Tool And Durable Evidence

Original proposal: new native .NET file-based
`eng/configurator/MeasureBrowserInteractions.cs`, built-in `ClientWebSocket`/JSON,
loopback-only published URL and DevTools endpoint, isolated synthetic fixtures,
strict TLS by default. No Node toolchain, Browser product dependency or telemetry.
Proposed flags: `--self-test`, `--base-url`, `--devtools-url`, `--output`,
`--samples` (40), `--warmups` (5), `--cpu-rate` (1/4), optional
`--allow-self-signed-loopback` limited to isolated loopback and recorded explicitly.
These commands do not exist and were not run by the review.

Retain all samples and nearest-rank p50/p95/max; first-use separate; actual final
frame rather than handler return/spinner. Versioned value-free JSON contains
candidate/definition identity, environment, scenario IDs/counts/timings, request/
reload counts and pass/fail, never real inputs/private paths/artifact bytes.
Self-test percentile/schema vectors independently and reject incomplete evidence.
Use subscribed readiness/events, bounded timeouts, no polling luck.

Coordinate with `browser-production-v1-followups.md` before choosing a new
automation owner. Reuse its required candidate-identity and missing-check rejection
concerns; its historical dependency/version proposals are not admitted packages.
Primary interaction qualification must not wait for this tool.

Retained possible paths: native tool; actual `BrowserStorePackageTests` only
where inventory changes require it; Operations/Testing; final preparation/
scheduling ADR; existing presentation journal/major decisions; I-VSD report.
Retained outcome: `test(testing): qualify published browser interaction budgets`
with internal skip trailers. Exact packets must be authored in Q's later triad,
not improvised during execution.

## Traceability And Provider-Responsibility Gate

- IVSD-F001/M001: active S1/S2 repair plus P S5 and Q measurement.
- IVSD-F002/M002: active warmed-cache custody plus P S8/S9 ownership/scheduling.
- IVSD-F003/M003: P S5-S7 truthful progress and terminal readiness.
- IVSD-F004/M004: active remount/focus plus P accessible preparation.
- IVSD-F005/M005: active SPA/abandonment regression plus P/Q offline/update proof.

R2 I-VSD alignment was stale at the split. The
[R3 responsibility report](../../islamic-value-sensitive-design/workstreams/i-vsd-browser-interaction-performance.md)
now revalidates active and deferred mappings without closing the deferred work.
Each followup still needs current revalidation and approval before implementation.
Retain source-free official research references in the active plan's research
register. Canonical behavior, consent and erasure are current product invariants,
not backward-compatibility obligations. New dependencies/provenance or authority
changes need their own admission, not permission inferred from this backlog.
