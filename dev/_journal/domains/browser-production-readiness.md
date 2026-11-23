# Browser Production Readiness & Qualification Findings

Last Updated: 2026-11-23 Europe/Brussels

Use [journal guidance](../README.md), [the finding template](../FINDING_TEMPLATE.md), and [promotion rules](../PROMOTION_RULES.md) for recording and promotion.

## Durable Findings

[2026-11-23 Europe/Brussels] — ASP.NET Core 10 ResponseCompression disables HTTPS and webmanifest by default

**Context**: While hardening `src/Configurator.Browser.Host` for Coolify/HTTPS delivery in Phase 1 of `browser-launch-readiness`, we added unit and live TLS compression verification in `BrowserHostPolicyTests.cs`.

**Symptom / Observation**: Probing `IResponseCompressionProvider.ShouldCompressResponse(httpsContext)` against default `AddResponseCompression()` returned `false` on `https://` requests even when `Accept-Encoding: br, gzip` was sent, and `application/manifest+json` was omitted from `ResponseCompressionDefaults.MimeTypes`. Plain-HTTP (`http://127.0.0.1`) tests hid the bug completely.

**Root Cause**: ASP.NET Core defaults `ResponseCompressionOptions.EnableForHttps` to `false` as a general BREACH mitigation for dynamic/authenticated responses, and does not include `application/wasm` or `application/manifest+json` in default MIME types.

**Resolution**: Set `options.EnableForHttps = true` with explicit `BrotliCompressionProvider` and `GzipCompressionProvider` (`CompressionLevel.Fastest`) plus `application/wasm` and `application/manifest+json` in `src/Configurator.Browser.Host/BrowserHostApplication.cs`, and added genuine ephemeral-TLS loopback `https://` integration tests in `tests/Configurator.Architecture.Tests/BrowserHostPolicyTests.cs` (`e19a4e5`, `40/40` passed against the hardened container).

**Why This Matters for Future Work**: Static-only public hosts (zero auth/cookies/reflected user input) can safely compress over HTTPS, but testing compression over plain `http://` is a tautology. If a separate future `Configurator.Browser.ConnectedHost` introduces auth or dynamic responses, it must NOT copy `EnableForHttps = true` blindly onto dynamic endpoints.

**References**:
- `src/Configurator.Browser.Host/BrowserHostApplication.cs`
- `tests/Configurator.Architecture.Tests/BrowserHostPolicyTests.cs`
- `docs/internal/adr/ADR-browser-coolify-static-image.md`
- Commit: `e19a4e5`

**Promotion Consideration**:
- [x] Candidate for ADR / `MAJOR_DECISIONS.md` (`ADR-browser-coolify-static-image.md`)

---

[2026-11-23 Europe/Brussels] — Active Service Worker navigation caching intercepts in-browser 404 probes

**Context**: While executing live `Q09-settings-and-explore-reloads` in `eng/configurator/Configurator.BrowserQualification/QualificationChecks.cs` against the published static Browser container in Phase 6.

**Symptom / Observation**: After visiting `/` (allowing `service-worker.published.js` to install and `clients.claim()`), `page.GotoAsync("/settings/nonexistent.js")` returned HTTP `200` (cached `index.html`) in both Chromium and Firefox, even though `BrowserHostPolicyTests` proved `Configurator.Browser.Host` returns HTTP `404`.

**Root Cause**: `src/Configurator.Browser/wwwroot/service-worker.published.js:71` intercepts all same-origin `request.mode === 'navigate'` requests and serves cached `index.html` for offline SPA boot. Top-level browser navigations therefore bypass the origin server once the worker controls the tab.

**Resolution**: Kept `page.GotoAsync(...)` for verifying real SPA route rendering (`/settings`, `/settings/appearance`, `/settings/privacy`, `/help`, `/open`, `/explore/{id}`) and used Playwright's `APIRequestContext.GetAsync("/settings/nonexistent.js")` to verify the static Host's direct HTTP `404` policy without Service Worker interception (`395b890`).

**Why This Matters for Future Work**: Any end-to-end test validating server-side HTTP status codes (`404`/`405`) against a PWA-enabled origin must use a non-navigation HTTP client or fresh unregistered context; otherwise an active Service Worker masks origin status codes.

**References**:
- `src/Configurator.Browser/wwwroot/service-worker.published.js:67-79`
- `eng/configurator/Configurator.BrowserQualification/QualificationChecks.cs`
- Commit: `395b890`

**Promotion Consideration**:
- [x] Stays in journal only (reusable Playwright/PWA testing lesson)

---

[2026-11-23 Europe/Brussels] — Playwright FillAsync and SelectOptionAsync dispatch untrusted DOM events

**Context**: While running 56-cohort (`2,576`-sample) Chromium CDP interaction benchmarks in `eng/configurator/Configurator.BrowserQualification/CompoundRootBenchmarks.cs` against `eng/configurator/MeasureInteractiveWork.cs` in Phase 6.

**Symptom / Observation**: Capturing `event.isTrusted` inside our page-side timing listener caused `ValidateInteractionPayload` to reject cohorts driven by `SelectOptionAsync` and repeated `FillAsync` calls (`trusted: false`).

**Root Cause**: Playwright's `SelectOptionAsync` and `FillAsync` mutate form state and dispatch synthetic DOM `input`/`change` events in JavaScript (`event.isTrusted === false`), unlike OS-level CDP pointer clicks (`ClickAsync`) and keyboard dispatch (`Keyboard.TypeAsync`), which produce genuine `event.isTrusted === true` events.

**Resolution**: Replaced `SelectOptionAsync`/`FillAsync` in `CompoundRootBenchmarks.cs` with trusted pointer clicks (`ClickAsync`) and triple-click + `Keyboard.TypeAsync(...)` (`395b890`), achieving `100%` `isTrusted === true` compliance across all 56 cohorts (`2,576` samples) accepted structurally by `MeasureInteractiveWork.cs`.

**Why This Matters for Future Work**: Performance harnesses enforcing `MeasureInteractiveWork.cs`'s `trusted: true` invariant must use real pointer/keyboard input dispatch rather than convenience form setters; checking `event.isTrusted` in-page is an effective fail-closed guard against synthetic input illusions.

**References**:
- `eng/configurator/Configurator.BrowserQualification/CompoundRootBenchmarks.cs`
- `eng/configurator/MeasureInteractiveWork.cs:133`
- Commit: `395b890`

**Promotion Consideration**:
- [x] Stays in journal only (reusable CDP benchmark engineering lesson)

---

[2026-11-23 Europe/Brussels] — Published AOT WASM passes 1x near-limit prep but breaches 200ms at 4x on 512+ leaf trees

**Context**: While running the empirical `QF01` near-limit preparation gate and 56-cohort Chromium CDP suite on the published Docker AOT WASM image (`sha256:9eb64f06533d...`) in Phase 6 (`TASK-6.2`).

**Symptom / Observation**: All 36/36 mandatory core cohorts passed (`0` outliers, `0` warmed `1x` long tasks) and all 4/4 near-limit `QF01` fixtures passed at `1x` CPU (`63.3–81.5ms <= 100ms`). Under `4x` CPU throttling, `wide-nested-object` (512 nested fields) measured `p50=110.5ms / p95=214.1ms` and `large-map-list` (1,024 leaves) measured `p50=176.1ms / p95=224.4ms` (exceeding `200ms`), and `few-root-deep-tree` `4x` reverse (`Review -> Configure`) measured `p95=407.1ms` (vs `400ms`).

**Root Cause**: Full synchronous bundle preparation and deep recursive section remounting remain single-pass on the WASM UI thread without incremental yielding (`DefinitionPreparationSession`), which fits easily inside `100ms` at `1x` CPU but crosses `200ms` by `14–24ms` on 512–1,024 leaf trees at `4x` throttling.

**Resolution**: Preserved all exact measured traces and outliers without masking in `docs/internal/releases/browser-v1-qualification.md`, kept `QF01` incremental preparation explicitly open for a dedicated bounded performance slice rather than claiming `4x` closure.

**Why This Matters for Future Work**: Empirical `4x` AOT measurement pinpointed the exact two fixtures (`wide-nested-object`, `large-map-list`) and marginal excess (`14–24ms`) where future `DefinitionPreparationSession` yielding pays off, without risking premature complexity during launch hardening.

**References**:
- `docs/internal/releases/browser-v1-qualification.md`
- `eng/configurator/BuildInteractiveWorkFixtures.cs`
- `eng/configurator/MeasureInteractiveWork.cs`

**Promotion Consideration**:
- [x] Stays in journal + tracked in `docs/internal/STATUS.md` and `dev/backlog/README.md`
