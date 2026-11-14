# Major Decisions

Last Updated: 2026-11-14 Europe/Brussels

## Offline Runtime And Target-Owned Authority

The accepted [runtime boundary ADR](../../docs/internal/adr/ADR-definition-runtime-boundary.md) is authoritative. Core, Artifacts, Presentation and four clients prepare bounded data offline; target validation and apply authority remain target-owned. This index links the decision rather than duplicating its technical contract.

## Secret-Store Integration Through SecretKit (2026-11-14)

The accepted [secret-store integration ADR](../../docs/internal/adr/ADR-secret-store-integration-and-credential-sources.md) is authoritative. Future Infisical and Azure Key Vault operations run only in native clients and only through ISLAMU SecretKit. Vendor CLIs never carry secret values; they may only be explicit, opt-in credential sources (Azure `AzureCliCredential`, chosen for enterprise tenants that have not approved a new application). Azure sign-in uses the operator's own client ID. Infisical starts with Universal Auth, with OIDC as the documented expansion. The Browser's exclusion from secret managers is permanent. Nothing is implemented; target apply stays deferred.
