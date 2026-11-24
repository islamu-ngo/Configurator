# Static Browser v1 Launch Qualification Record (2026-11-23)

> **Audience:** Maintainers | Release reviewers | Self-hosting operators | AI agents
> **Status:** Local Linux container, dual-engine `Q01–Q16`, and Track A `4x` `QF01`/scale qualification passed; external human/hardware gates remain open
> **Owner:** Browser / Packaging
> **Last Verified:** 2026-11-24
> **Source Anchors:** `src/Configurator.Browser/Dockerfile`, `src/Configurator.Browser.Host/`, `eng/configurator/Configurator.BrowserQualification/`, `eng/configurator/ValidateSupportQualification.cs`, `eng/configurator/MeasureInteractiveWork.cs`

This qualification record binds exact container, payload, test, and benchmark identities executed on Linux x64 (`SDK 10.0.404`, Docker `29.9.3` BuildKit) after implementing Phases 1–5 of the `browser-launch-readiness` workstream. Automated passes do not substitute for physical assistive-technology sign-off, manual high-zoom evaluation, Apple WebKit/Safari hardware testing, or live Coolify edge TLS inspection.

---

## 1. Qualified Artifact & Container Identities

| Identity | Value |
| --- | --- |
| Product build commit (container source) | `96f9240081c491ec206f3bfe17d9ec08d227da94` |
| Qualification harness refinement commit | `395b89096777b38ed39c363de9323638012d9bc3` |
| Unified container image ID (`src/Configurator.Browser/Dockerfile`) | `sha256:9eb64f06533d3059fab2053b46e92be19ce9ddc42dc98fb7521171a6af3c07d9` |
| Base images (pinned digests) | `mcr.microsoft.com/dotnet/sdk:10.0.404-noble`, `mcr.microsoft.com/dotnet/aspnet:10.0.10-noble` |
| Container runtime UID | `1654` (non-root `$APP_UID`, zero `curl`/`wget`) |
| Container `HEALTHCHECK` command | `["CMD","dotnet","Configurator.Browser.Host.dll","--healthcheck"]` (`30s` interval, `3s` timeout, `5s` start-period, `3` retries) |
| Published `service-worker-assets.js` SHA-256 (`payloadDigest`) | `c4e582ccb06faa77602dee527adb06d8d6f841389c0f89971a4397e7caef0288` |
| Source inputs digest (`productSourceInputsDigest`) | `9c0f7c713452ba45ef89cc8e5466fc51214a5a71b0c0126b86bca711d622ec72` |
| Build recipe digest (`buildRecipeDigest`) | `83cd2a508d35e87377affe0aefc4abae4c3086dce5d400882e455b4be7c65baa` |
| Interactive fixture bundle digest (`fixtureDigest`) | `4c1490e03a986486252eebcbbb86de1abb377f8165e02645f40249a238a010dc` |
| Qualification host digest (`hostDigest`) | `b0f2ee4ac145bcd495f7f1093567e1934ce40a88831b68f5191787375a92ebbd` |

---

## 2. Hardened Container & Static Host Verification (`TASK-6.1`)

Executed with Docker BuildKit (`DOCKER_BUILDKIT=1`) using the repaired `src/Configurator.Browser/Dockerfile.dockerignore` (`9.41 MB` transferred build context) and run hardened (`--read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m --cap-drop ALL --security-opt no-new-privileges`):

- **Health supervision:** Container transitioned to `healthy`; in-container `dotnet /app/Configurator.Browser.Host.dll --healthcheck` exited `0`; stopping the container caused `--healthcheck` to fail closed (`exit 1`).
- **Zero-log privacy boundary:** Requests to `/?canary=SYNTH-LOG-CANARY-1234` (`200`) and `/SYNTH-404-CANARY-5678` (`404`) emitted zero canary strings in `docker logs`.
- **Containerized `BrowserHostPolicyTests`** (`CONFIGURATOR_HOST_URL=http://127.0.0.1:18781` with isolated read-only `host-attack.*` probes, plus ephemeral TLS loopback HTTPS compression and child `--healthcheck` SSRF suites): **`40/40` passed**, `0` failed, `0` skipped (`2.59s`).
- **Container-extracted package tests** (`CONFIGURATOR_PACKAGE_ROOT` copied from `cfg-browser-qual:/app/wwwroot`):
  - `BrowserStorePackageTests`: **`2/2` passed**, `0` failed, `0` skipped (`7.18s`).
  - `StandalonePackageTests/BrowserPackage*`: **`1/1` passed**, `0` failed, `0` skipped (`0.71s`).
  - `StandaloneArchitectureTests` (including zero-Docker `Dockerfile.dockerignore` completeness and zero `SecretKit*`/`Azure.*`/`Microsoft.Playwright` product guards): **`31/31` passed**, `0` failed, `0` skipped (`1.41s`).
  - `node --test tests/Configurator.Browser.Tests/Profiles/ProfileWorkerHostTests.mjs`: **`12/12` passed**, `0` failed, `0` skipped (`87.7ms`).

---

## 3. Multi-Engine Published-Browser Qualification (`Q01–Q16`)

Executed via `eng/configurator/Configurator.BrowserQualification` (`Microsoft.Playwright` `1.65.1`, MIT, outside `src/` closure) against the live hardened container at `http://localhost:18780`:

| Engine | Launch Status | Engine Version | `Q01–Q16` Result | Notes |
| --- | --- | --- | --- | --- |
| Chromium | Launched | `159.0.8154.6` | **`16/16` passed** | Zero failures, zero skips |
| Firefox | Launched | Playwright `1.65.1` Firefox | **`16/16` passed** | Zero failures, zero skips |
| WebKit | **`engine-launch-failed`** | Playwright `v2388` (`27.2`) | **`0/16` (unexecuted)** | Arch Linux workstation lacks Ubuntu-linked `libicu74`/`libxml2`; physical macOS/iOS Safari remains an explicit external release gate |

- **Detached receipt validation:** `dotnet run --file eng/configurator/ValidateSupportQualification.cs -- --projection --source ... --build ... --payload ... --receipt support-receipt.json` exited **`0`**.
- **Assistive-technology honesty boundary:** Both `en` and `ar` `("accessibility", "assistive-technology")` rows in the validated receipt remain explicitly **`status: "unexecuted"`**.
- **Coverage verified across `Q01–Q16`:** WASM boot & embedded Store cards (`Q01`), malformed rejection & valid definition admission (`Q02`), typed scalars/choices/nested objects/omission (`Q03`), `#secret-consent` unchecked default + `en`/`ar` hosted-code/extension custody disclosure + `/help#custody` + revocation (`Q04`), deferred-secret draft & complete output (`Q05`), unconsented import refusal + Compare/Autofill nested `answer-conflict` declared paths without dynamic map key leakage (`Q06`), hidden-by-default reveal/hide + real download + copy (`Q07`), invalid-edit fencing & actionable corrections (`Q08`), direct `/settings`, `/settings/appearance`, `/settings/privacy`, `/help`, `/open`, `/explore/{id}` reloads + HTTP `404` on `/settings/nonexistent.js` (`Q09`), offline retention (`1 / 8`), selective delete & APG focus restoration to `#definition-file` (`Q10`), zero answer/secret canary leaks across network, `localStorage`, `sessionStorage`, `CacheStorage`, and `IndexedDB` (`Q11`), two-tab SW update isolation (`Q12`), keyboard Tab, `390x844` zero horizontal overflow, and Arabic `dir="rtl"` (`Q13`), WCAG contrast & `:focus-visible` outlines (`Q14`), 120-field scale admission & layout/Review switching (`Q15`), and `/healthz`, `405`, `404`, CSP/nosniff/no-referrer headers (`Q16`).

---

## 4. Trusted-Input Chromium CDP Benchmarks (`MeasureInteractiveWork.cs`) & Empirical `QF01` Gate

Executed on Chromium `159.0.8154.6` via CDP `Emulation.setCPUThrottlingRate` (`1x` and `4x`) with OS-level trusted pointer/keyboard input (`isTrusted === true`), **46 samples per cohort** (`1 first + 5 warmups + 40 measured`), across **56 cohorts (`2,576` total samples)** validated by `eng/configurator/MeasureInteractiveWork.cs` (structurally valid; exit `2` retaining all outliers transparently):

### 4.1 Mandatory Core Journeys (`36/36` Passed — `100%`)

All 9 core journeys (`fresh-environment`, `switch-manifest`, `ten-scalars`, `ten-short-choices`, `obscure-correction`, `import-compare`, `deferred-draft`, `bulk-edit-malformed-recovery`, `consent-revoke-editor`) across both directions and both `1x`/`4x` CPU rates passed with **`0` outliers** and **`0` warmed `1x` long tasks**:

- Worst `1x` content `p95`: **`80ms`** (`bulk-edit-malformed-recovery`, budget `< 100ms`); worst `1x` feedback max: **`23ms`** (budget `< 50ms`).
- Worst `4x` content `p95`: **`202ms`** (`bulk-edit-malformed-recovery`, budget `< 400ms`); worst `4x` feedback max: **`87ms`** (budget `< 200ms`).

### 4.2 Extended Compound-Root & `compact-600` Cohorts (`17/20` Passed, `3/20` Tail Breaches Retained)

| Fixture Journey | `1x` Forward / Reverse `p95` (budget `100ms`) | `4x` Forward / Reverse `p95` (budget `400ms`) | Status & Retained Outliers |
| --- | --- | --- | --- |
| `wide-nested-object` (512 fields) | `70ms` / `78ms` | `147ms` / `269ms` | **4/4 Passed** |
| `large-map-list` (256 map + 256 list) | `49ms` / `64ms` | `93ms` / `111ms` | **4/4 Passed** |
| `multi-artifact` (8 JSON outputs) | `65ms` / `77ms` | `136ms` / `220ms` | **4/4 Passed** |
| `few-root-deep-tree` (7-level, 128 leaves) | `66ms` / `86ms` | `130ms` / **`407.1ms`** | **3/4 Passed** — `4x` reverse (`Review -> Configure`) exceeded `400ms` by `7.1ms` (`5` measured outliers `403.9–429.6ms`; `fbMax=87.9ms <= 200ms` passed) |
| `compact-600` (600 root fields) | `80.9ms` / `90ms` | `197.6ms` / `334ms` | **2/4 Passed** — `1x` forward measured samples passed (`p95=80.9ms`, `fbMax=36.6ms`, `taskMax=0`), but first-use ordinal `0` hit `fb=50.5ms` (`+0.5ms`); `4x` forward passed `p95=197.6ms`, but single GC/layout outlier ordinal `17` hit `fb=214.2ms` (`+14.2ms`) |

### 4.3 Empirical `QF01` Near-Limit Preparation Gate

Measured end-to-end `#prepare-complete` trusted click to `[data-prepared-file]` visible + 2 animation frames (`3` warmups + `10` measured samples per fixture/rate):

| Fixture | `1x` `p50` / `p95` (budget `<= 100ms`) | `1x` Gate | `4x` `p50` / `p95` (budget `<= 200ms`) | `4x` Gate |
| --- | --- | --- | --- | --- |
| `few-root-deep-tree` | `47.8ms` / `63.3ms` | **PASS** | `93.1ms` / `108.7ms` | **PASS** |
| `wide-nested-object` | `64.5ms` / `64.8ms` | **PASS** | `110.5ms` / **`214.1ms`** | **EXCEEDED (`+14.1ms`)** |
| `large-map-list` | `64.9ms` / `81.5ms` | **PASS** | `176.1ms` / **`224.4ms`** | **EXCEEDED (`+24.4ms`)** |
| `multi-artifact` (8 outputs) | `48.1ms` / `64.5ms` | **PASS** | `110.1ms` / `127.2ms` | **PASS** |

**`QF01` Decision Gate Outcome:** All 4 near-limit fixtures pass at `1x` CPU (`63.3–81.5ms <= 100ms`). Under `4x` CPU throttling, `wide-nested-object` (`214.1ms`) and `large-map-list` (`224.4ms`) exceed `200ms` by `14.1–24.4ms`. Per the empirical Decision Gate, `QF01` incremental `DefinitionPreparationSession` scheduling and the 3 `4x`/cold extended-scale tail optimizations remain an explicitly tracked bounded follow-up rather than being claimed closed.

---

## 5. Workstream Exit Ring Owner Verification

- `dotnet restore Configurator.slnx --locked-mode && dotnet build Configurator.slnx --configuration Release --no-restore --verbosity quiet`: **`0 Warning(s), 0 Error(s)`** across all 30 projects.
- `dotnet run .ci/scripts/validate-dependency-license-policy.cs -- .`: **97 package/version pairs passed** (`exit 0`).
- `dotnet run eng/configurator/GenerateConfiguratorRatchets.cs -- --check`: **`3/3` ratchets current** (`exit 0`).
- `dotnet run --project eng/configurator/Configurator.BrowserQualification --configuration Release --no-build -- --self-test`: **`13/13` passed** (`exit 0`).
- Scoped `dotnet format Configurator.slnx --verify-no-changes` across every C# file modified/added in Phases 1–5: **`exit 0`** (pre-existing unrelated `IMPORTS` ordering drift in untouched `Configurator.Desktop.Tests` and `Configurator.Presentation.Tests/Form/DefinitionCorrectionFactsTests.cs` remains quarantined without modification).
- `Configurator.Core.Tests`: **`969/969` passed** (`0` failed, `0` skipped, `16.2s`).
- `Configurator.Presentation.Tests`: **`496/496` passed** (`0` failed, `0` skipped, `2.1s`).
- `Configurator.Browser.Tests`: **`689/689` passed** (`0` failed, `0` skipped, `18.2s`).
- Quarantined unrelated baseline: `TrustedVerifierPackagingTests` and `ReleaseCandidateVerificationTests` (`GovernedReleaseFixture.CloseRelease` `candidate_input_invalid`, pre-existing per [`OPERATIONS.md`](../OPERATIONS.md)).

---

## 6. Remaining Open Human & External Release Gates

These gates cannot be closed by local Linux automation and remain explicit prerequisites before promoting a public Coolify production release:

1. **Physical screen-reader & braille evaluation (`en` and `ar`):** NVDA/JAWS on Windows, VoiceOver on macOS/iOS, and Orca on Linux with Steward sign-off.
2. **Manual `200%` and `400%` browser UI zoom & reflow verification:** Real browser chrome zoom (not CSS emulation) across desktop and mobile widths.
3. **Safari / WebKit on physical macOS & iOS hardware:** Real Apple WebKit WASM boot, service worker caching/eviction, clipboard/download custody, and RTL shaping (Linux Playwright WebKit failed launch on Arch due to Ubuntu `libicu74`/`libxml2`).
4. **Live Coolify production edge deployment & rollback drill:** Verified HTTPS TLS termination, reverse-proxy `Vary: Accept-Encoding` compression cooperation, query/referrer-stripped edge access logs, and exact-digest rollback verification.

---

## 7. Track A `4x` Performance Tail Closure Addendum (2026-11-24)

Executed on Linux x64 (`SDK 10.0.404`, Docker `29.9.2` BuildKit, Chromium `159.0.8154.6`) after implementing single-pass cooperative `DefinitionPreparationSession` (streaming canonical `Utf8JsonWriter` + `ZeroingByteBufferWriter`), precomputed `DefinitionNode.RequiredSet`/`HasOptionalDescendant`, O(1) `DefinitionFormProjection.FieldStatesById` & `OptionalMissing`, and fenced two-phase `ConfigurationReview` effects hydration.

### 7.1 Track A Rebuilt Container & Input Identities

| Identity | Value |
| --- | --- |
| Rebuilt unified container image ID (`src/Configurator.Browser/Dockerfile`) | `sha256:6f56bbed3df0006fb0183567e4669c29fb968b89112cd2f7ea1d9db6305a5a53` |
| Published `service-worker-assets.js` SHA-256 (`payloadDigest`) | `59eb19ae44c0f85a92bf5be3dc9f3596a218c0f2e5871e08e6908aa10272c91f` |
| Interactive fixture bundle digest (`fixtureDigest`) | `4c1490e03a986486252eebcbbb86de1abb377f8165e02645f40249a238a010dc` |
| Qualification host digest (`hostDigest`) | `b0f2ee4ac145bcd495f7f1093567e1934ce40a88831b68f5191787375a92ebbd` |

### 7.2 `MeasureInteractiveWork.cs` (`56/56` Passed — Exit `0`)

Ran `Configurator.BrowserQualification --measure-interactive` against the hardened container (`--read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m --cap-drop ALL --security-opt no-new-privileges`) and validated raw outputs via `dotnet run --file eng/configurator/MeasureInteractiveWork.cs` (**`exit 0`**, `passed: true`, **`56/56` cohorts passed**, `1` retained non-failing outlier across `2,576` total samples):

- **Mandatory core cohorts (`36/36` passed, `0` outliers):** worst `1x` content `p95` **`78.5ms`** (`bulk-edit-malformed-recovery` reverse, budget `100ms`), worst `1x` feedback max **`23.8ms`** (`deferred-draft` reverse, budget `50ms`); worst `4x` content `p95` **`198.6ms`** (`bulk-edit-malformed-recovery` reverse, budget `400ms`), worst `4x` feedback max **`94.1ms`** (`obscure-correction` reverse, budget `200ms`).
- **Extended compound-root & `compact-600` cohorts (`20/20` passed):**

| Fixture Journey | `1x` Forward / Reverse `p95` (budget `100ms`) | `4x` Forward / Reverse `p95` (budget `400ms`) | Worst `4x` Feedback Max (budget `200ms`) | Status |
| --- | --- | --- | --- | --- |
| `wide-nested-object` (512 fields) | `66.3ms` / `72.4ms` | `141.8ms` / `224.4ms` | `91.6ms` | **4/4 Passed** |
| `large-map-list` (256 map + 256 list) | `81.9ms` / `81.9ms` | `93.0ms` / `111.8ms` | `66.7ms` | **4/4 Passed** |
| `multi-artifact` (8 JSON outputs) | `68.1ms` / `73.6ms` | `193.6ms` / `267.0ms` | `142.0ms` | **4/4 Passed** (`1` retained `4x` forward outlier ordinal `32` at `695.7ms`, `fb=52.1ms`, `p95=193.6ms <= 400ms`) |
| `few-root-deep-tree` (7-level, 128 leaves) | `64.8ms` / `84.1ms` | `136.4ms` / **`377.0ms`** | `90.0ms` | **4/4 Passed** (`4x` reverse down from `407.1ms`, `max=391.0ms`, `0` outliers) |
| `compact-600` (600 root fields) | `64.9ms` / `88.9ms` | `182.6ms` / `327.7ms` | **`187.1ms`** | **4/4 Passed** (`1x` forward first-use `fb=35.5ms < 50ms`, `4x` forward `fbMax=187.1ms < 200ms`, `0` outliers) |

### 7.3 `QF01` Near-Limit Preparation Gate (`8/8` Passed — `passed: true`)

| Fixture | `1x` `p50` / `p95` (budget `<= 100ms`) | `1x` Gate | `4x` `p50` / `p95` (budget `<= 200ms`) | `4x` Gate |
| --- | --- | --- | --- | --- |
| `few-root-deep-tree` | `64.6ms` / `64.8ms` | **PASS** | `92.7ms` / `94.5ms` | **PASS** |
| `wide-nested-object` | `64.7ms` / `64.9ms` | **PASS** | `124.4ms` / **`140.5ms`** | **PASS** (down from `214.1ms`) |
| `large-map-list` | `81.2ms` / `81.6ms` | **PASS** | `156.1ms` / **`166.4ms`** | **PASS** (down from `224.4ms`) |
| `multi-artifact` (8 outputs) | `64.7ms` / `65.0ms` | **PASS** | `90.5ms` / `109.6ms` | **PASS** |

- **Owner unit verification for this payload:** `Configurator.Core.Tests` (`978/978`, `13.6s`), `CoreArchitectureTests` (`10/10`), `Configurator.Presentation.Tests` (`498/498`, `2.2s`), `Configurator.Browser.Tests` (`693/693`), Release `Configurator.Browser` build (`0W, 0E`).
- **Remaining release gates:** External human/hardware items `1–4` in Section 6 (physical screen-reader/braille sign-off, manual `200%`/`400%` zoom, physical macOS/iOS Safari/WebKit, and live Coolify edge TLS/rollback drill) remain open and are unaffected by this automation pass.
