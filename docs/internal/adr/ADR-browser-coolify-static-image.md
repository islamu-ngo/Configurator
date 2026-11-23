# ADR: Static Browser Coolify Container Image & Permanent SecretKit Boundary

> **Audience:** Maintainers | Self-hosting operators | Security reviewers | AI agents
> **Kind:** Accepted decision
> **Status:** Accepted and implemented (`2026-11-23`)
> **Owner:** Browser / Packaging
> **Last Verified:** 2026-11-23
> **Source Anchors:** `src/Configurator.Browser/Dockerfile`, `src/Configurator.Browser/Dockerfile.dockerignore`, `src/Configurator.Browser.Host/`, `tests/Configurator.Architecture.Tests/StandaloneArchitectureTests.cs`, `tests/Configurator.Architecture.Tests/BrowserHostPolicyTests.cs`

Date: 2026-11-23
Reviewers: Product Steward, Browser, Security/Privacy, and Packaging maintainers.
Related Decisions:
- [Self-hosted connected Browser and the public static boundary](ADR-self-hosted-connected-browser-and-public-static-boundary.md)
- [Secret-store integration and credential sources](ADR-secret-store-integration-and-credential-sources.md)
- [Browser session trust and export](ADR-browser-session-trust-and-export.md)

---

## Context

The Browser surface runs as standalone Blazor WebAssembly with the curated public definition Store embedded inside `Configurator.Presentation` resources. Self-hosting operators deploying on Coolify (and ISLAMU's public static origin) require one minimal, hardened, deployment-neutral container image that:

1. Serves every SPA route (`/`, `/open`, `/help`, `/settings`, `/settings/{section}`, `/store/{slug}`, `/launch/{id}`, `/explore/{id}`) while failing closed (`404`) on unknown paths and uninventoried files.
2. Compresses admitted static assets (including `application/wasm` and `application/manifest+json`) over both HTTP and HTTPS without weakening strict file allowlisting.
3. Provides Coolify-compatible container health supervision without adding `curl`, `wget`, or SSRF attack surface to the non-root ASP.NET Core runtime image.
4. Enforces the permanent separation between static Browser hosting and future server-side secret-store/target connectors (`ISLAMU SecretKit` / `Configurator.Browser.ConnectedHost`).

---

## Decision

### 1. One Unified Static Image Embedding the Curated Store

`src/Configurator.Browser/Dockerfile` produces a single unified static container image on pinned `mcr.microsoft.com/dotnet/aspnet:10.0.10-noble` running as non-root UID `1654` (`$APP_UID`). Because `Configurator.Presentation` compiles and embeds `store/` snapshots at build time, dual `Store=true`/`Store=false` container builds and runtime `wwwroot/definition-store/` HTTP trees are permanently retired.

`src/Configurator.Browser/Dockerfile.dockerignore` remains a strict allowlist admitting only the Browser/Host build closure and public Store inputs while excluding `.git`, `dev`, `tests`, `bin`, `obj`, `artifacts`, `.env`, and key/certificate files. `StandaloneArchitectureTests` mechanically verifies every source file and ancestor directory in the Browser build graph against `Dockerfile.dockerignore` without requiring a Docker daemon in unit CI.

### 2. Allowlist-Preserving Host With Genuine HTTPS Compression

`Configurator.Browser.Host` (`BrowserHostApplication.cs`) retains strict `UseStaticFiles(PublicationContentTypes)` driven solely by the SDK-generated `service-worker-assets.js` inventory (parsed as data, never executed) plus fixed packaging artifacts (`LICENSE`, `THIRD-PARTY-NOTICES.md`, schemas, capability contracts). It does **not** adopt broad `MapStaticAssets()` discovery so uninventoried files dropped into `wwwroot/` continue returning `404`.

Response compression enables `EnableForHttps = true` with `BrotliCompressionProvider` and `GzipCompressionProvider` (`CompressionLevel.Fastest`), explicitly admitting `application/wasm` and `application/manifest+json`, and emitting `Vary: Accept-Encoding` so Coolify/Traefik edge proxies cooperate cleanly. HTTPS compression is safe against BREACH on this host solely because every served byte is identical public static content with zero authentication, cookies, sessions, or user-reflected input.

### 3. Curl-Free, Loopback-Fenced Self-Executing `--healthcheck`

`Program.cs` short-circuits `args is ["--healthcheck"]` before `WebApplication.CreateBuilder` and delegates to `HealthCheckProbe`:

- Serves `GET` and `HEAD /healthz` (`200 OK`, zero-reflection `{"status":"ok"}`, `Cache-Control: no-store`); rejects `POST /healthz` with `405`.
- Resolves `CONFIGURATOR_HEALTHCHECK_URL` fail-closed (requiring `http://`, `IsLoopback`, exact `/healthz` path, valid port `1..65535`, and empty userinfo/query/fragment — exiting `1` without opening a socket on any violation) or derives the first valid port from `ASPNETCORE_HTTP_PORTS` (default `8080`).
- Disables HTTP redirects, proxies, and cookies, caps response reads to `256` bytes with a `2`-second timeout, and wires `HEALTHCHECK ... CMD ["dotnet", "Configurator.Browser.Host.dll", "--healthcheck"]` in `src/Configurator.Browser/Dockerfile` with zero extra OS packages.

### 4. Permanent Exclusion of `ISLAMU SecretKit` and Connected APIs

Consistent with [ADR-self-hosted-connected-browser-and-public-static-boundary.md](ADR-self-hosted-connected-browser-and-public-static-boundary.md) and [ADR-secret-store-integration-and-credential-sources.md](ADR-secret-store-integration-and-credential-sources.md), `Configurator.Browser` and `Configurator.Browser.Host` permanently exclude `ISLAMU SecretKit`, vendor CLIs, vault SDKs, credential intake endpoints, and remote target apply proxies. `StandaloneArchitectureTests` mechanically asserts that neither project references `SecretKit*`, `Azure.*`, or `Microsoft.Playwright`.

Future operator-owned connected operation must ship in a separate `Configurator.Browser.ConnectedHost` project, image, and security qualification boundary without adding mode switches, POST endpoints, or vault dependencies to this static Host.

### 5. Exact-Digest Coolify Rollback & Offline PWA Boundary

Production Coolify deployments must pin immutable container image digests (`@sha256:...`) so rollback is a deterministic redeploy of the prior verified digest. Operators must recognize that rolling back or stopping the static origin affects only subsequent network fetches and service-worker revalidation; it cannot recall already-exported files, clipboard copies, or active/disconnected offline PWA tabs until active workspaces close and reload cleanly.

---

## Consequences

- **Positive:** Minimal non-root attack surface (`UID 1654`, read-only root, dropped capabilities, zero `curl`, zero request-access logs), deterministic Coolify health supervision and exact-digest rollbacks, genuine HTTPS Brotli/Gzip delivery for WASM payloads, and compile-time/CI guardrails preventing `.dockerignore` drift or SecretKit contamination.
- **Trade-offs & Limits:** If a future separate host ever adds cookies, authentication, or user-reflected content, `EnableForHttps = true` compression must be re-evaluated for BREACH. Local Linux container qualification (`docs/internal/releases/browser-v1-qualification.md`) does not replace live Coolify edge TLS inspection or physical Safari/assistive-technology sign-off.
