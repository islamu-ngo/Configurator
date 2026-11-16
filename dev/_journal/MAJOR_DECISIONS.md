# Major Decisions

Last Updated: 2026-11-16 Europe/Brussels

## Offline Runtime And Target-Owned Authority

The accepted [runtime boundary ADR](../../docs/internal/adr/ADR-definition-runtime-boundary.md) is authoritative. Core, Artifacts, Presentation and four clients prepare bounded data offline; target validation and apply authority remain target-owned. This index links the decision rather than duplicating its technical contract.

## Secret-Store Integration Through SecretKit (2026-11-14)

The accepted [secret-store integration ADR](../../docs/internal/adr/ADR-secret-store-integration-and-credential-sources.md) is authoritative. Future Infisical and Azure Key Vault operations run only through ISLAMU SecretKit, initially in native clients. Vendor CLIs never carry secret values; they may only be explicit, opt-in credential sources (Azure `AzureCliCredential`, chosen for enterprise tenants that have not approved a new application). Azure sign-in uses the operator's own client ID. Infisical starts with Universal Auth, with OIDC as the documented expansion.

The [self-hosted connected Browser ADR](../../docs/internal/adr/ADR-self-hosted-connected-browser-and-public-static-boundary.md) (2026-11-16) narrows the previous permanent universal Browser exclusion. ISLAMU's public instance and the current self-hosted static Browser remain permanently target-disconnected; no ISLAMU public proxy is permitted. After native pilots, a separately packaged operator-owned connected host may reuse the local WebAssembly editor and submit only explicitly reviewed target deltas through its authenticated connector, with SecretKit server-side. The static Host gains no integration switch. Nothing is implemented; target apply stays deferred in every package.
