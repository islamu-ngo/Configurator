# Major Decisions

Last Updated: 2026-11-23 Europe/Brussels

## Offline Runtime And Target-Owned Authority

The accepted [runtime boundary ADR](../../docs/internal/adr/ADR-definition-runtime-boundary.md) is authoritative. Core, Artifacts, Presentation and four clients prepare bounded data offline; target validation and apply authority remain target-owned. This index links the decision rather than duplicating its technical contract.

## Static Browser Coolify Container Image (2026-11-23)

The accepted [static Browser Coolify image ADR](../../docs/internal/adr/ADR-browser-coolify-static-image.md) codifies the single unified embedded-Store container (`src/Configurator.Browser/Dockerfile`, non-root UID `1654`, curl-free loopback-fenced `--healthcheck`, allowlist-preserving HTTPS Brotli/Gzip, exact-digest Coolify rollback, and `StandaloneArchitectureTests` guards against `Dockerfile.dockerignore` drift or `SecretKit*`/`Azure.*`/`Microsoft.Playwright` product leakage). It cross-links both SecretKit/ConnectedHost ADRs to keep the static image permanently target-disconnected.

## Secret-Store Integration Through SecretKit (2026-11-14)

The accepted [secret-store integration ADR](../../docs/internal/adr/ADR-secret-store-integration-and-credential-sources.md) is authoritative. Future Infisical and Azure Key Vault operations run only through ISLAMU SecretKit, initially in native clients. Vendor CLIs never carry secret values; they may only be explicit, opt-in credential sources (Azure `AzureCliCredential`, chosen for enterprise tenants that have not approved a new application). Azure sign-in uses the operator's own client ID. Infisical starts with Universal Auth, with OIDC as the documented expansion.

The [self-hosted connected Browser ADR](../../docs/internal/adr/ADR-self-hosted-connected-browser-and-public-static-boundary.md) (2026-11-16) narrows the previous permanent universal Browser exclusion. ISLAMU's public instance and the current self-hosted static Browser remain permanently target-disconnected; no ISLAMU public proxy is permitted. After native pilots, a separately packaged operator-owned connected host may reuse the local WebAssembly editor and submit only explicitly reviewed target deltas through its authenticated connector, with SecretKit server-side. The static Host gains no integration switch. Nothing is implemented; target apply stays deferred in every package.

## Desktop UI Library Selection: SukiUI And Optional-Utility Pragmatism (2026-11-23)

The accepted [Desktop UI library ADR](../../docs/internal/adr/ADR-desktop-ui-library-and-sukiui.md) selects 100% MIT `SukiUI` (Avalonia 12 `7.x` line) as the planned UI component and theme library for `Configurator.Desktop`, rejecting open-core alternatives (`Semi.Avalonia` + `Irihi.Ursa` due to IRIHI's competing commercial flagship control library `Asura` and closed-source `Semi` adapters) and non-MIT/unlicensed options. ISLAMU Event is ISLAMU's main product and Configurator is an optional quality-of-life utility: safety, secret custody, data-only definitions, generation fencing and protected native output remain non-negotiable, while Desktop delegates UI/theming/controls heavily to SukiUI and treats Browser visual/density parity, Desktop RTL and strict automated WCAG/high-contrast clamping as optional bonuses rather than blockers. Desktop currently remains on `Avalonia.Themes.Fluent 12.1.6`; dependency admission and implementation remain planned.
