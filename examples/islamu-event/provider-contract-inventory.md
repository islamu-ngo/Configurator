# Event provider contract inventory

> **Audience:** Definition maintainers
> **Status:** Baseline classification audited; downstream image compatibility remains target-owned
> **Owner:** Event definition integration
> **Last Verified:** 2026-11-01
> **Source Anchors:** `configurator.json`, `README.md`

This inventory covers the 361 environment fields in the Configurator baseline
`a7b09f3cfba0d14a63e3c28402e4effea3299023`. Target facts were read from the
first-party Event checkout at `b6ad1594b7f92ac3399a5a8bf8d241e4f8d69c66`.
The definition declares `0.0.0-development`; these facts do not promise support
for another Event revision. Source paths below are relative to that Event checkout.
Its modified standalone appsettings and documentation were not used as authority.

## Closed choices and owning source

Values below are canonical string wire values, not translated display labels.
Case-insensitive target parsers may accept additional spellings; Configurator can
deliberately admit only canonical values. Localized labels must retain these values.
No choice acquires credentials or grants target acceptance.

| ID | Environment fields | Canonical values | Target source / rule |
| --- | --- | --- | --- |
| C01 | AUTHENTICATION_PROVIDER | local, keycloak, atproto | `src/Explore.Application/Configuration/AuthenticationProviderDeploymentOptions.cs`: trimmed, case insensitive; absent/blank unspecified; atproto rejects explicitly disabled AT Protocol login |
| C02 | AUTHORIZATION_PROVIDER | local, cerbos | `src/Explore.Infrastructure/Services/AuthorizationProviderDeploymentOptions.cs`: trimmed, case insensitive; absent/blank unspecified |
| C03 | DATABASE_PROVIDER, IDENTITY_DATABASE_PROVIDER | PostgreSql, Sqlite, SqlServer, MariaDb, MySql | `src/Explore.Persistence/Database/PrimaryDatabaseProvider.cs`; `PrimaryDatabaseConfiguration.cs`; `src/Explore.Persistence/Identity/IdentityDatabaseConfiguration.cs` |
| C04 | DATABASE_TLS_MODE, IDENTITY_DATABASE_TLS_MODE, ERASURE_DATABASE_TLS_MODE, PRIVACY_ERASURE_AUTHORITY_TLS_MODE | Prefer, Required, Disabled | `src/Explore.Persistence/Database/PrimaryDatabaseTlsMode.cs`; API `Extensions/ConfigurationExtensions.cs` maps the first three; `PrivacyErasureAuthorityDatabaseConfiguration.cs:125` maps the fourth |
| C05 | SECRET_PROVIDER | Environment, UserSecrets, Infisical | `src/Explore.ServiceDefaults/Secrets/SecretAuthoritySelection.cs`; explicit selection required; UserSecrets restricted to Development/Testing |
| C06 | AI_PROVIDER | NONE, FAKE, OPENAI_COMPATIBLE, ANTHROPIC_COMPATIBLE, OPENAI, AZURE_OPENAI, ANTHROPIC | `src/Explore.API/Extensions/ConfigurationExtensions.cs`, `MapProviderMasterCodeToId`; uppercase and hyphen normalization; `AiProviderSettingsValidator` restricts FAKE to Development/Testing |
| C07 | GEOCODING_PROVIDER | None, Photon | `src/Explore.Infrastructure/Geocoding/PhotonOptionsValidator.cs`; default None, case insensitive |
| C08 | REPORTING_MODE | Disabled, LocalOnly, Osprey, Coop, Composite | `src/Explore.Infrastructure/Configuration/ModerationProviderOptions.cs`; default LocalOnly; case insensitive |
| C09 | WEBHOOKS_PROVIDER | Disabled, Local, Svix, Composite, DryRun | `src/Explore.Infrastructure/Configuration/WebhookOptions.cs` and validator; default Local; case insensitive |
| C10 | CONFIGURATION_MANIFEST_MODE | Off, ValidateOnly, Bootstrap | `src/Explore.Infrastructure/ConfigurationManifest/ConfigurationManifestOptions.cs` and `ConfigurationManifestBootstrapDeployment.ParseMode`; default Off |
| C11 | INSTANCE_BOOTSTRAP_MODE | Interactive, ConfiguredAdministrator | `src/Explore.Infrastructure/Services/ConfiguredAdministratorBootstrapProvider.cs`, `ReadConfiguration`; exact case, explicit value required |
| C12 | INSTANCE_BOOTSTRAP_ADMIN_PROVIDER | local, keycloak, atproto | Same `ReadConfiguration`; exact case; required for ConfiguredAdministrator |
| C13 | DEPLOYMENT_MODE | SingleTenant, MultiTenant | `src/Explore.API/Extensions/ConfigurationExtensions.cs`, `NormalizeDeploymentMode`; also normalizes case, underscores, hyphens and spaces |
| C14 | MAIL_SMTP_ENCRYPTION | None, StartTls, SslOnConnect, Auto | `src/Explore.Persistence/Seed/DatabaseSeeder.cs`, `NormalizeSmtpSecurity`; `src/Explore.Application/Models/SmtpConfiguration.cs`; target aliases include off/false, start-tls, ssl/tls/ssl-on-connect |
| C15 | IDENTITY_DATABASE_TOPOLOGY | colocated, external | `src/Explore.Persistence/Identity/IdentityDatabaseConfiguration.cs`, `GetTopology`; case insensitive, absent/blank colocated |
| C16 | ERASURE_DATABASE_TOPOLOGY | EmbeddedSqlite, ExternalDatabase, CoLocated | `src/Explore.Application/Configuration/PrivacyErasureDurabilityOptions.cs`; API maps to PrivacyErasure:Authority:Topology; default EmbeddedSqlite |
| C17 | PAYMENTS_STRIPE_MODE | Test, Live | `src/Explore.Infrastructure/Payments/Stripe/StripePaymentOptions.cs`; exact case; default Test |
| C18 | PAYMENTS__CHECKOUTGOVERNANCE__ACTIVATIONSTATUS | approved, suspended | `src/Explore.Application/Contracts/Services/IPaidCheckoutGovernance.cs`, `IsComplete`; case insensitive; default suspended |
| C19 | PAYMENTS__CHECKOUTGOVERNANCE__CHARGETYPE | direct-charge | Same `IsComplete`; exact value |
| C20 | INSTANCE__OPERATORIDENTITY__OPERATORKINDCODE | registered_organization, sole_trader, individual, public_body, unincorporated_association | `src/Explore.Domain/ValueObjects/TenantDirectoryOperatorIdentity.cs`, `TenantDirectoryOperatorKinds.All`; exact case; used by `InstanceOperatorIdentityReadiness` |
| C21 | WEBHOOKS_SVIX_ENVIRONMENT | self-hosted | `src/Explore.Infrastructure/Webhooks/SvixConformanceProfileRegistry.cs`, verified supported profile; coupled to C22/C23 and an explicit base URL |
| C22 | WEBHOOKS_SVIX_PROVIDER_VERSION | 1.96.1 | Same verified profile |
| C23 | WEBHOOKS_SVIX_CAPABILITY_POLICY_VERSION | svix-self-hosted-1.96.1-v1 | Same verified profile |
| C24 | TICKETING__RECOVERY__MANIFESTSIGNINGKEYREFERENCE | ticketing.recovery_manifest_hmac_key | `src/Explore.Infrastructure/Recovery/TicketingRecoveryOperatorOptionsValidator.cs`; exact trimmed reference required when recovery enabled |

## Provider requirements and remaining limits

Keycloak's BFF configuration is one complete group: `KEYCLOAK_ENDPOINT`,
`KEYCLOAK_REALM`, `KEYCLOAK_CLIENT_ID`, `KEYCLOAK_BLAZOR_CLIENT_SECRET`.
`src/Explore.Blazor/Extensions/ConfigurationExtension.cs:19-44` declares the group
and rejects any blank member before installing it. Later client-ID defaults do
not remove this group requirement. `KEYCLOAK_BLAZOR_CLIENT_ID` is not the group's
client-ID key. Endpoint and realm construct the OIDC authority. Admin credentials,
database credentials and local HTTP port belong to deliberate local deployment,
not selection of Keycloak as the authentication provider. `KEYCLOAK_INTERNAL_URL`
is an optional API connection override, not a local-deployment credential.

Infisical requires URL, project ID, vault environment, client ID and client secret
in `src/Explore.ServiceDefaults/Secrets/SecretAuthorityBootstrap.cs:82-89`.
Database server providers require host, database, username and password;
MariaDb/MySql also need compatible server flavor/version, with target-owned
defaults 11.4/8.4 and flavor inferred from the provider. `DATABASE_SERVER_VERSION`
is therefore an optional override limited to those two providers. Sqlite instead
requires a persisted local database path through `DATABASE_NAME`, which the API
maps to both `Database:Database` and `Database:Name`. It forbids host, port, role
username/password, flavor/version and certificate-trust override; its TLS mode
must remain Prefer. PostgreSql/SqlServer forbid server flavor/version. See
`PrimaryDatabaseConfiguration.Bind` and `Validate`.
External Identity has separately declared roles and provider settings; authority
database uses PostgreSql and distinct runtime/migrator usernames. These rules must
not be replaced by a generic rule making every database credential always required.

Photon requires endpoint, bounded language tag, one to ten two-letter country
codes and nonempty dataset version. AI model identifiers are extensible; enabled
compatible providers need endpoint and model, while Azure requires HTTPS.
RabbitMQ queue/exchange/routing/consumer names remain nonempty deployment values;
its settings validator requires positive prefetch/timeouts/batch/retry values.
The connection factory accepts an explicit connection string, then a configured
connection-string name, then a derived environment URI (default `MESSAGING_URI`).
These are alternatives, not simultaneously required answers. S3 configuration
requires endpoint, bucket and both access-key parts, with optional public endpoint
and a default region; its region remains extensible across S3-compatible hosts.
Osprey/Coop endpoints depend on their enabled mode; API keys are not unconditionally
required by their options validators. Svix environment/version/capability selection
depends on an executed conformance profile and cannot be reduced to a guessed list.

The four checkout responsibility owner strings are deployment-specific names,
not provider enums. Realm, client IDs, hosts, model IDs, paths, secret references,
bucket names and provider-issued credentials must never become private lookup
lists. Locale/country identifiers need a separately versioned public catalogue if
a finite lookup is offered; none was established by this audit.

The current Event catalogue has evolved beyond the 361-field Configurator baseline.
This document does not establish coverage of newly added target fields. Third-party
compose settings are forwarded into operator-selected images by `docker-compose.yml`
and `src/Explore.AppHost/AppHost.cs`; their values are deployment-specific rather
than a closed Event runtime enum. An observed default is not the complete accepted
set of every possible image. Coop explicitly supports extension adapters; its
[official adapter documentation](https://docs.roost.tools/coop/1.0.0/development/data-warehouse.html)
describes registering additional warehouse/analytics providers. Consequently,
`COOP_*_ADAPTER` remains extensible. No third-party implementation is transferred.
Environment names and deployment image tags also remain extensible.

`SetupSecretProvider.HasTrustedManagedProvisioningConfiguration` reads
`MANAGED_CLIENT_EXTERNAL_PROVIDER` and `PHYSICAL_TENANCY_MODE` only for nonblank
presence. `PROVISIONING_MODE` recognizes managed/managedprovider/managedhosting
after case/separator normalization, treating other strings as unmanaged; this is
not a closed validator. These three fields therefore remain extensible input.
The Svix registry's additional production/managed-api-v1/svix-managed-api-v1 tuple
has zero executed tests and is rejected by the active provider validator; it must
not be advertised as an available verified choice.

## Scalar and open-input owners

These source owners establish domains for the remaining baseline fields. A
deployment field can contain private values; this classification neither lowers
sensitivity nor claims that a public annotation in old metadata was correct.
Credentials/connection strings need explicit secret handling even when an older
catalogue described their whole category as public.

| ID | Fields / ownership | Target evidence |
| --- | --- | --- |
| D01 | Admissions credential/recovery counters and versions | `src/Explore.Application/Configuration/AdmissionCredentialOptions.cs`, `AdmissionRecoveryOptions.cs`; numeric properties; `src/Explore.Infrastructure/Services/Registration/AdmissionRecoveryRateLimiter.cs` bounds rate-limit inputs |
| D02 | AI endpoint, model and credentials | `src/Explore.Infrastructure/Ai/AiProviderSettings.cs` and validator; model IDs remain extensible, credentials come from secret authorities |
| D03 | Compose-only service deployment values | `docker-compose.yml` and `src/Explore.AppHost/AppHost.cs`: explicit interpolation/forwarding into Cerbos, Coop, Formbricks, Keycloak, Osprey, Svix and Weblate; names, hosts, images and credentials are deployment-owned, not Event enums |
| D04 | Manifest location | `src/Explore.Infrastructure/ConfigurationManifest/ConfigurationManifestOptions.cs`; filesystem path, with host directory forwarded by compose |
| D05 | Control-plane identity, limits and credentials | `src/Explore.Application/Management/ManagedControlPlaneOptions.cs` and Infrastructure validator; Enabled Boolean, MaximumTenantCount integer, ManagedInstanceId GUID, URLs and registration credential strings |
| D06 | Database host/name/schema/role credentials/port/version | `src/Explore.Persistence/Database/PrimaryDatabaseConnectionOptions.cs`, `PrimaryDatabaseConfiguration.cs`; Identity and erasure configuration adapters reuse this contract |
| D07 | RabbitMQ names, transport and limits | `src/Explore.Infrastructure/EmailDispatchRabbitMqSettings.cs` and validator; names are nonblank strings, prefetch unsigned integers, timing/batch values positive integers; connection strings deployment-owned |
| D08 | Embedded erasure authority path and limits | `src/Explore.Persistence/Database/EmbeddedPrivacyErasureAuthorityOptions.cs`; local path, replica count exactly 1, timeout 1–300 seconds; API/compose map ERASURE and PRIVACY_ERASURE_AUTHORITY aliases |
| D09 | Geocoding locale/country/dataset and limits | `src/Explore.Infrastructure/Geocoding/PhotonOptionsValidator.cs`; bounded tags/country codes/dataset identifier and numeric limits; retry delays an integer list |
| D10 | Identity issuer references | `src/Explore.Application/Configuration/IdentityCorrelationOptions.cs`; issuer strings are deployment-specific |
| D11 | Secret-authority bootstrap values | `src/Explore.ServiceDefaults/Secrets/SecretAuthorityBootstrap.cs`; public identifiers and URL plus authority-issued client credential, never public choices |
| D12 | Bootstrap administrator identity | `src/Explore.Infrastructure/Services/ConfiguredAdministratorBootstrapProvider.cs`; provider-specific subject, bounded names/email, generation integer and local password |
| D13 | Operator identity | `src/Explore.Application/Contracts/Services/InstanceOperatorIdentityOptions.cs` and `src/Explore.Domain/ValueObjects/InstanceOperatorIdentityReadiness.cs`; identifiers, bounded names/URLs/contact, country code, Boolean official flag; kind is C20 |
| D14 | Keycloak runtime connection | `src/Explore.Blazor/Extensions/ConfigurationExtension.cs`, API `Extensions/ConfigurationExtensions.cs` and ServiceDefaults `SecretStartupConfigurationProjection.cs`; endpoint, realm, client IDs, secret, optional URL/HTTPS Boolean |
| D15 | Listmonk values | API `Extensions/ConfigurationExtensions.cs` maps API key/username, instance URL, list ID and normalized Boolean subscription flags into integration settings |
| D16 | Local storage and reconciliation | `src/Explore.Infrastructure/Storage/LocalFileStorageOptions.cs`, `src/Explore.Infrastructure/StorageReconciliationSettings.cs`; path, create-root Boolean and Boolean reconciliation actions |
| D17 | MCP | API `Extensions/ConfigurationExtensions.cs`; normalized endpoint path and normalized Boolean enabled/stateless/legacy flags |
| D18 | Organizer payment configuration | `src/Explore.Application/Contracts/Services/IOrganizerPaymentCommerceConfiguration.cs`; provider code and ConnectPlatformId are strings; `IPaidCheckoutGovernance.cs` owns responsibility strings, language tag and descriptor |
| D19 | Provisioning/setup | `src/Explore.Infrastructure/Services/SetupSecretProvider.cs`; Boolean trusted flag, extensible provisioning strings, generated-secret path and deployment-owned secret |
| D20 | Anonymous registration rate limits | `src/Explore.API/Extensions/AnonymousRegistrationRateLimiting.cs`; integer ranges IP 1–1000, subnet 1–10000, window 1–3600, concurrency 1–128, queue 0–64 |
| D21 | Reporting providers and health | Infrastructure `Configuration/ModerationProviderOptions.cs`, `CoopProviderOptions.cs`, `OspreyProviderOptions.cs`; Boolean actions/allow-local flags; `GetInstanceOperationsQueryHandler` reads health counters as integers |
| D22 | Ticketing recovery | `src/Explore.Infrastructure/Recovery/TicketingRecoveryOperatorOptions.cs` and validator; numeric versions/floors/counters/times, deployment revision strings and key reference |
| D23 | Web push | ServiceDefaults `SecretStartupConfigurationProjection.cs` maps VAPID keys/subject to WebPush; AppHost explicitly passes the private key as a secret parameter |
| D24 | Webhook credentials and profile | `src/Explore.Infrastructure/Configuration/WebhookOptions.cs` and validator; secret values/references and deployment URL, with C21–C23 verified profile tuple |
| D25 | Promotion lookup version | `src/Explore.Application/Configuration/PromotionCodeLookupOptions.cs`; integer ActiveKeyVersion |
| D26 | Infrastructure connection strings and bootstrap credentials | API mapping, AppHost pass-through and `SecretStartupConfigurationProjection.cs`; deployment-owned connection/credential values, never finite public lookups |

## Baseline field classification

This is an exhaustive baseline-name classification, not proof of runtime
conformance. `domain` means Boolean/numeric/structured scalar input;
`deployment` means operator-specific values rather than public lookup choices;
`extensible` means identifiers with no fixed target-owned finite catalogue.
Closed rows reference the independently inspected contracts above. Initial values
shown are baseline presence metadata; absence remains absence. They are not target
default assertions. Reset follows authored presence metadata, never a guessed default.

Further confirmed Boolean candidates still typed as strings in the baseline are
`LOCAL_STORAGE_CREATE_ROOT_IF_MISSING` (`LocalFileStorageOptions`),
`REPORTING_EVALUATE_SIGNALS`, `REPORTING_EXECUTE_DECISIONS`,
`REPORTING_MIRROR_REVIEW_QUEUE`, `REPORTING_SYNC_REPORTS`
(`ModerationProviderOptions`), the Coop/Osprey `ALLOW_LOCAL_PROVIDER_ENDPOINTS`
fields (their options), `LISTMONK_PRECONFIRM_SUBSCRIPTIONS` and
`LISTMONK_SYNC_ON_REGISTRATION` (API `ConfigurationExtensions` Boolean mapping),
`PROVISIONING_TRUSTED` (`SetupSecretProvider.ReadBoolean`), `MCP_STATELESS` and
`MCP_ENABLE_LEGACY_SSE` (API Boolean mapping). The table records the target domain
and the baseline string type separately; a classification does not implement or
verify a whole input/output type conversion. `CONTROL_PLANE_MANAGED_MODE` is also
a Boolean (`ManagedControlPlaneOptions.Enabled`), not an open provider mode.
The remaining four Boolean candidates complete the 17-field baseline string audit:
`KEYCLOAK_REQUIRE_HTTPS_METADATA` (compose maps to `Keycloak:RequireHttpsMetadata`),
`STORAGE_RECONCILIATION_DELETE_QUARANTINED_OBJECTS`,
`STORAGE_RECONCILIATION_QUARANTINE_MISSING_OBJECTS`, and
`STORAGE_RECONCILIATION_QUARANTINE_ORPHAN_LOCAL_FILES`
(`StorageReconciliationSettings`). Existing typed Booleans are not counted again.

Credential review identifies the following baseline non-secret fields for secret
sensitivity, based on credential consumers rather than a naming-only rule:

- Database binders: `DATABASE_RUNTIME_PASSWORD`, `DATABASE_MIGRATOR_PASSWORD`,
  `ERASURE_DATABASE_RUNTIME_PASSWORD`, `ERASURE_DATABASE_MIGRATOR_PASSWORD`,
  `PRIVACY_ERASURE_AUTHORITY_RUNTIME_PASSWORD`,
  `PRIVACY_ERASURE_AUTHORITY_MIGRATOR_PASSWORD`.
- Authority/client credentials: `INFISICAL_CLIENT_SECRET`,
  `CONTROL_PLANE_REGISTRATION_TOKEN`, `KEYCLOAK_ADMIN_PASSWORD`,
  `REPORTING_COOP_API_KEY`, `REPORTING_COOP_WEBHOOK_SECRET`,
  `REPORTING_OSPREY_API_KEY`, `VAPID_PRIVATE_KEY`.
- Credential-bearing transport values: `EMAIL_DISPATCH_RABBITMQ_CONNECTION_STRING`,
  `MESSAGING_URI`, `REDIS_CONNECTION_STRING`, `CERBOS_PG_URL`, `SVIX_DB_DSN`,
  `SVIX_REDIS_DSN`. A particular value may omit a password; the accepted transport
  representation can contain one and must not bypass secret consent.
- Compose credential inputs: `CERBOS_ADMIN_PASSWORD_HASH`,
  `CERBOS_POSTGRES_PASSWORD`, `COOP_DATABASE_PASSWORD`, `COOP_SCYLLA_PASSWORD`,
  `COOP_SESSION_SECRET`, `FORMBRICKS_CRON_SECRET`, `FORMBRICKS_CUBEJS_API_SECRET`,
  `FORMBRICKS_DATABASE_PASSWORD`, `FORMBRICKS_ENCRYPTION_KEY`,
  `FORMBRICKS_HUB_API_KEY`, `FORMBRICKS_NEXTAUTH_SECRET`, `SVIX_DB_PASSWORD`,
  `SVIX_JWT_SECRET`, `WEBLATE_ADMIN_PASSWORD`, `WEBLATE_POSTGRES_PASSWORD`.

`AI_API_KEY` was an authored legacy credential slot. It is removed from the current
source and output mapping because the reviewed Event API configuration wiring
consumes provider-specific AI credential keys, not this alias. Its baseline row
remains below as an explicit removal, keeping the 361-field audit accountable.
Secret-reference identifiers, connection-string names, `SECRET_PROVIDER`,
`SETUP_SECRET_FILE`, `SETUP_SECRET_REQUIRED`, public VAPID keys and key-version
identifiers are not credential contents. No automatic sensitivity downgrade follows.

ConfiguredAdministrator bootstrap requires provider, subject and positive binding
generation. Optional first/last names must be supplied together; Interactive mode
rejects configured administrator values. Inactive branches must therefore be
excluded from output, not only hidden. S3 credentials, Infisical client secrets,
database role passwords and VAPID private keys retain secret treatment regardless
of category metadata inherited from older catalogues.

On target refresh, inspect each referenced parser and validator at the new commit,
review unknown rows and new/removed environment names, update independent expected
sets in conformance tests, then author both families and regenerate through native
revision/adoption. Keep historical Store snapshots immutable. Do not derive expected
choices from the definition under test. Runtime tests must cover invalid values,
provider switching, inactive-secret drafts and revoked secret leaves before this
inventory can support a full coverage claim.

| Field | Class | Contract / baseline type | Initial value |
| --- | --- | --- | --- |
| `ADMISSIONS_CREDENTIAL_LOOKUP_HMAC_KEY` | deployment | string | absent |
| `ADMISSIONS_RECOVERY_CAPABILITY_HMAC_KEY` | deployment | string | absent |
| `ADMISSIONS_SCANNER_CAPABILITY_HMAC_KEY` | deployment | string | absent |
| `ADMISSIONS__CREDENTIALLOOKUP__ACTIVEKEYVERSION` | domain | D01 / baseline string | absent |
| `ADMISSIONS__RECOVERY__ACTIVEKEYVERSION` | domain | D01 / baseline string | absent |
| `ADMISSIONS__RECOVERY__CAPABILITYLIFETIMEMINUTES` | domain | D01 / baseline string | absent |
| `ADMISSIONS__RECOVERY__RATELIMITBUCKETCOUNT` | domain | D01 / baseline string | absent |
| `ADMISSIONS__RECOVERY__RATELIMITPERMITCOUNT` | domain | D01 / baseline string | absent |
| `ADMISSIONS__RECOVERY__RATELIMITWINDOWSECONDS` | domain | D01 / baseline string | absent |
| `AGENT_BROWSER_PERSONA_PASSWORD` | deployment | string | absent |
| `AGENT_BROWSER_REDIS_PASSWORD` | deployment | string | absent |
| `AGENT_BROWSER_SEED_ENABLED` | domain | boolean | false |
| `AI_ANTHROPIC_API_KEY` | deployment | string | absent |
| `AI_API_KEY` | removed | D02 / no current target consumer; use provider-specific fields | absent |
| `AI_ENDPOINT` | extensible | string | absent |
| `AI_MODEL_ID` | extensible | string | absent |
| `AI_OPENAI_API_KEY` | deployment | string | absent |
| `AI_PROVIDER` | closed | C06 | absent |
| `AI_TOOL_PROPOSALS_ENABLED` | domain | boolean | false |
| `ANALYTICS_PERSONAL_API_KEY` | deployment | string | absent |
| `ANALYTICS_POSTHOG_HOST` | deployment | string | absent |
| `ANALYTICS_POSTHOG_PUBLIC_KEY` | deployment | string | absent |
| `API_ENDPOINT` | extensible | string | absent |
| `API_HTTP_PORT` | domain | integer | absent |
| `ASPNETCORE_ENVIRONMENT` | extensible | string | absent |
| `ATPROTO_LOGIN_ENABLED` | domain | boolean | absent |
| `ATPROTO_OAUTH_CLIENT_PRIVATE_JWKS` | deployment | string | absent |
| `ATPROTO_SESSION_ENCRYPTION_KEYRING` | deployment | string | absent |
| `ATPROTO_SESSION_JWT_PRIVATE_JWKS` | deployment | string | absent |
| `AUTHENTICATION_LOCAL_JWT_KEY` | deployment | string | absent |
| `AUTHENTICATION_LOCAL_LOCKOUT_DURATION_MINUTES` | domain | integer | 15 |
| `AUTHENTICATION_LOCAL_LOCKOUT_THRESHOLD` | domain | integer | 5 |
| `AUTHENTICATION_PROVIDER` | closed | C01 | absent |
| `AUTHORIZATION_PROVIDER` | closed | C02 | absent |
| `CERBOS_ADMIN_PASSWORD` | deployment | string | absent |
| `CERBOS_ADMIN_PASSWORD_HASH` | deployment | D03 / baseline string | absent |
| `CERBOS_ADMIN_USERNAME` | deployment | string | absent |
| `CERBOS_GRPC_ENDPOINT` | deployment | string | absent |
| `CERBOS_GRPC_PORT` | domain | integer | absent |
| `CERBOS_HTTP_ENDPOINT` | extensible | string | absent |
| `CERBOS_HTTP_PORT` | domain | integer | absent |
| `CERBOS_PG_URL` | deployment | D03 / baseline string | absent |
| `CERBOS_PLAINTEXT_MODE` | domain | boolean | absent |
| `CERBOS_POSTGRES_DB` | deployment | D03 / baseline string | absent |
| `CERBOS_POSTGRES_PASSWORD` | deployment | D03 / baseline string | absent |
| `CERBOS_POSTGRES_USER` | deployment | D03 / baseline string | absent |
| `CERBOS_USE_TLS` | domain | boolean | absent |
| `CONFIGURATION_MANIFEST_HOST_DIRECTORY` | deployment | D04 / baseline string | absent |
| `CONFIGURATION_MANIFEST_MODE` | closed | C10 | "Off" |
| `CONFIGURATION_MANIFEST_PATH` | deployment | D04 / baseline string | absent |
| `CONTROL_PLANE_INSTANCE_ID` | deployment | D05 / baseline string | absent |
| `CONTROL_PLANE_MANAGED_MODE` | domain | D05 / baseline string | absent |
| `CONTROL_PLANE_MAXIMUM_TENANT_COUNT` | domain | D05 / baseline string | absent |
| `CONTROL_PLANE_REGISTRATION_CREDENTIALS` | deployment | string | absent |
| `CONTROL_PLANE_REGISTRATION_TOKEN` | deployment | D05 / baseline string | absent |
| `CONTROL_PLANE_TENANT_ADMINISTRATOR_SIGN_IN_URL` | deployment | D05 / baseline string | absent |
| `CONTROL_PLANE_URL` | extensible | string | absent |
| `COOP_ANALYTICS_ADAPTER` | extensible | D03 / baseline string | absent |
| `COOP_CLIENT_HTTP_PORT` | domain | integer | absent |
| `COOP_CLIENT_IMAGE` | extensible | D03 / baseline string | absent |
| `COOP_DATABASE_NAME` | deployment | D03 / baseline string | absent |
| `COOP_DATABASE_PASSWORD` | deployment | D03 / baseline string | absent |
| `COOP_DATABASE_USER` | deployment | D03 / baseline string | absent |
| `COOP_HTTP_PORT` | domain | integer | absent |
| `COOP_IMAGE` | extensible | D03 / baseline string | absent |
| `COOP_MIGRATIONS_IMAGE` | extensible | D03 / baseline string | absent |
| `COOP_NODE_ENV` | extensible | D03 / baseline string | absent |
| `COOP_OTEL_SERVICE_NAME` | deployment | D03 / baseline string | absent |
| `COOP_SCYLLA_HOSTS` | deployment | D03 / baseline string | absent |
| `COOP_SCYLLA_LOCAL_DATACENTER` | deployment | D03 / baseline string | absent |
| `COOP_SCYLLA_PASSWORD` | deployment | D03 / baseline string | absent |
| `COOP_SCYLLA_SSL` | domain | boolean | absent |
| `COOP_SCYLLA_USERNAME` | deployment | D03 / baseline string | absent |
| `COOP_SESSION_SECRET` | deployment | D03 / baseline string | absent |
| `COOP_UI_URL` | deployment | D03 / baseline string | absent |
| `COOP_WAREHOUSE_ADAPTER` | extensible | D03 / baseline string | absent |
| `DATABASE_HOST` | deployment | D06 / baseline string | absent |
| `DATABASE_MIGRATOR_PASSWORD` | deployment | D06 / baseline string | absent |
| `DATABASE_MIGRATOR_USERNAME` | deployment | D06 / baseline string | absent |
| `DATABASE_NAME` | deployment | D06 / baseline string | absent |
| `DATABASE_PORT` | domain | integer | absent |
| `DATABASE_PROVIDER` | closed | C03 | "PostgreSql" |
| `DATABASE_RUNTIME_PASSWORD` | deployment | D06 / baseline string | absent |
| `DATABASE_RUNTIME_USERNAME` | deployment | D06 / baseline string | absent |
| `DATABASE_SCHEMA` | deployment | D06 / baseline string | "islamu_event" |
| `DATABASE_SERVER_VERSION` | deployment | D06 / baseline string | absent |
| `DATABASE_TLS_MODE` | closed | C04 | "Prefer" |
| `DATABASE_TRUST_SERVER_CERTIFICATE` | domain | boolean | false |
| `DEPLOYMENT_MODE` | closed | C13 | absent |
| `DOTNET_ENVIRONMENT` | extensible | string | absent |
| `EMAIL_DISPATCH_RABBITMQ_CLIENT_PROVIDED_NAME` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_CONNECTION_STRING` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_CONNECTION_STRING_NAME` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_CONSUMER_ID` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_DEAD_LETTER_EXCHANGE_NAME` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_DEAD_LETTER_QUEUE_NAME` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_DEAD_LETTER_REPLAY_CONSUMER_ID` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_DEAD_LETTER_REPLAY_ENABLED` | domain | boolean | absent |
| `EMAIL_DISPATCH_RABBITMQ_DEAD_LETTER_REPLAY_PREFETCH_COUNT` | domain | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_DEAD_LETTER_ROUTING_KEY` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_DISPATCH_QUEUE_NAME` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_DISPATCH_ROUTING_KEY` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_ENABLED` | domain | boolean | false |
| `EMAIL_DISPATCH_RABBITMQ_EXCHANGE_NAME` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_PARKING_QUEUE_NAME` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_PARKING_ROUTING_KEY` | deployment | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_PREFETCH_COUNT` | domain | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_PUBLISHER_BATCH_SIZE` | domain | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_PUBLISHER_POLLING_INTERVAL_SECONDS` | domain | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_PUBLISHER_RETRY_DELAY_SECONDS` | domain | D07 / baseline string | absent |
| `EMAIL_DISPATCH_RABBITMQ_PUBLISH_TIMEOUT_SECONDS` | domain | D07 / baseline string | absent |
| `ERASURE_BUSY_TIMEOUT_SECONDS` | domain | D08 / baseline string | absent |
| `ERASURE_DATABASE_HOST` | deployment | D06 / baseline string | absent |
| `ERASURE_DATABASE_MIGRATOR_PASSWORD` | deployment | D06 / baseline string | absent |
| `ERASURE_DATABASE_MIGRATOR_USERNAME` | deployment | D06 / baseline string | absent |
| `ERASURE_DATABASE_NAME` | deployment | D06 / baseline string | absent |
| `ERASURE_DATABASE_PORT` | domain | integer | absent |
| `ERASURE_DATABASE_RUNTIME_PASSWORD` | deployment | D06 / baseline string | absent |
| `ERASURE_DATABASE_RUNTIME_USERNAME` | deployment | D06 / baseline string | absent |
| `ERASURE_DATABASE_TLS_MODE` | closed | C04 | absent |
| `ERASURE_DATABASE_TOPOLOGY` | closed | C16 | absent |
| `ERASURE_DATABASE_TRUST_SERVER_CERTIFICATE` | domain | boolean | absent |
| `ERASURE_EMBEDDED_PATH` | deployment | D08 / baseline string | absent |
| `ERASURE_WRITER_REPLICA_COUNT` | domain | D08 / baseline string | absent |
| `FORMBRICKS_CRON_SECRET` | deployment | D03 / baseline string | absent |
| `FORMBRICKS_CUBEJS_API_SECRET` | deployment | D03 / baseline string | absent |
| `FORMBRICKS_DATABASE_NAME` | deployment | D03 / baseline string | absent |
| `FORMBRICKS_DATABASE_PASSWORD` | deployment | D03 / baseline string | absent |
| `FORMBRICKS_DATABASE_USER` | deployment | D03 / baseline string | absent |
| `FORMBRICKS_ENCRYPTION_KEY` | deployment | D03 / baseline string | absent |
| `FORMBRICKS_HTTP_PORT` | domain | integer | absent |
| `FORMBRICKS_HUB_API_KEY` | deployment | D03 / baseline string | absent |
| `FORMBRICKS_NEXTAUTH_SECRET` | deployment | D03 / baseline string | absent |
| `FORMBRICKS_WEBAPP_URL` | deployment | D03 / baseline string | absent |
| `GEOCODING_COUNTRY_CODES` | extensible | D09 / baseline string | absent |
| `GEOCODING_DATASET_VERSION` | extensible | D09 / baseline string | absent |
| `GEOCODING_ENDPOINT` | extensible | string | absent |
| `GEOCODING_LANGUAGE` | extensible | D09 / baseline string | absent |
| `GEOCODING_MAXIMUM_RESPONSE_BYTES` | domain | D09 / baseline string | absent |
| `GEOCODING_MAXIMUM_RESULTS` | domain | D09 / baseline string | absent |
| `GEOCODING_MAXIMUM_RETRY_COUNT` | domain | D09 / baseline string | absent |
| `GEOCODING_PROVIDER` | closed | C07 | absent |
| `GEOCODING_READINESS_TIMEOUT_MILLISECONDS` | domain | D09 / baseline string | absent |
| `GEOCODING_RETRY_DELAYS_MILLISECONDS` | domain | D09 / baseline string | absent |
| `GEOCODING_SELECTION_LIFETIME_SECONDS` | domain | D09 / baseline string | absent |
| `GEOCODING_TOTAL_TIMEOUT_MILLISECONDS` | domain | D09 / baseline string | absent |
| `HOSTING_REPLICA_COUNT` | domain | integer | 1 |
| `IDENTITYCORRELATION__TRUSTEDISSUERS__0` | deployment | D10 / baseline string | absent |
| `IDENTITY_DATABASE_CONNECTION_STRING` | deployment | string | absent |
| `IDENTITY_DATABASE_HOST` | deployment | D06 / baseline string | absent |
| `IDENTITY_DATABASE_MIGRATOR_PASSWORD` | deployment | string | absent |
| `IDENTITY_DATABASE_MIGRATOR_USERNAME` | deployment | D06 / baseline string | absent |
| `IDENTITY_DATABASE_NAME` | deployment | D06 / baseline string | absent |
| `IDENTITY_DATABASE_PORT` | domain | integer | absent |
| `IDENTITY_DATABASE_PROVIDER` | closed | C03 | absent |
| `IDENTITY_DATABASE_RUNTIME_PASSWORD` | deployment | string | absent |
| `IDENTITY_DATABASE_RUNTIME_USERNAME` | deployment | D06 / baseline string | absent |
| `IDENTITY_DATABASE_SCHEMA` | deployment | D06 / baseline string | absent |
| `IDENTITY_DATABASE_TLS_MODE` | closed | C04 | absent |
| `IDENTITY_DATABASE_TOPOLOGY` | closed | C15 | "colocated" |
| `IDENTITY_DATABASE_TRUST_SERVER_CERTIFICATE` | domain | boolean | absent |
| `INFISICAL_CLIENT_ID` | deployment | D11 / baseline string | absent |
| `INFISICAL_CLIENT_SECRET` | deployment | D11 / baseline string | absent |
| `INFISICAL_ENV` | deployment | D11 / baseline string | absent |
| `INFISICAL_PROJECT_ID` | deployment | D11 / baseline string | absent |
| `INFISICAL_URL` | deployment | D11 / baseline string | absent |
| `INSTANCE_ADMIN_PUBLIC_ORIGIN` | extensible | string | absent |
| `INSTANCE_BOOTSTRAP_ADMIN_EMAIL` | extensible | string | absent |
| `INSTANCE_BOOTSTRAP_ADMIN_FIRST_NAME` | deployment | D12 / baseline string | absent |
| `INSTANCE_BOOTSTRAP_ADMIN_LAST_NAME` | deployment | D12 / baseline string | absent |
| `INSTANCE_BOOTSTRAP_ADMIN_PROVIDER` | closed | C12 | absent |
| `INSTANCE_BOOTSTRAP_ADMIN_SUBJECT` | deployment | D12 / baseline string | absent |
| `INSTANCE_BOOTSTRAP_BINDING_GENERATION` | domain | integer | absent |
| `INSTANCE_BOOTSTRAP_LOCAL_PASSWORD` | deployment | string | absent |
| `INSTANCE_BOOTSTRAP_MODE` | closed | C11 | absent |
| `INSTANCE__OPERATORIDENTITY__ISOFFICIALINSTANCE` | domain | boolean | absent |
| `INSTANCE__OPERATORIDENTITY__JURISDICTIONCOUNTRYCODE` | deployment | D13 / baseline string | absent |
| `INSTANCE__OPERATORIDENTITY__LEGALNAME` | deployment | D13 / baseline string | absent |
| `INSTANCE__OPERATORIDENTITY__LEGALNOTICEURL` | extensible | string | absent |
| `INSTANCE__OPERATORIDENTITY__OFFICIALORIGIN` | deployment | D13 / baseline string | absent |
| `INSTANCE__OPERATORIDENTITY__OPERATORID` | deployment | D13 / baseline string | absent |
| `INSTANCE__OPERATORIDENTITY__OPERATORKINDCODE` | closed | C20 / baseline string | absent |
| `INSTANCE__OPERATORIDENTITY__PRIVACYURL` | extensible | string | absent |
| `INSTANCE__OPERATORIDENTITY__PUBLICCONTACTEMAIL` | deployment | D13 / baseline string | absent |
| `INSTANCE__OPERATORIDENTITY__PUBLICNAME` | deployment | D13 / baseline string | absent |
| `INSTANCE__OPERATORIDENTITY__REGISTRATIONIDENTIFIER` | deployment | D13 / baseline string | absent |
| `INSTANCE__OPERATORIDENTITY__TERMSURL` | extensible | string | absent |
| `INSTANCE__OPERATORIDENTITY__WEBSITEURL` | extensible | string | absent |
| `KEYCLOAK_ADMIN` | deployment | D14 / baseline string | absent |
| `KEYCLOAK_ADMIN_PASSWORD` | deployment | D14 / baseline string | absent |
| `KEYCLOAK_BLAZOR_CLIENT_ID` | deployment | D14 / baseline string | absent |
| `KEYCLOAK_BLAZOR_CLIENT_SECRET` | deployment | string | absent |
| `KEYCLOAK_CLIENT_ID` | deployment | string | absent |
| `KEYCLOAK_DB_DATABASE` | deployment | D14 / baseline string | absent |
| `KEYCLOAK_DB_PASSWORD` | deployment | string | absent |
| `KEYCLOAK_DB_USERNAME` | deployment | D14 / baseline string | absent |
| `KEYCLOAK_ENDPOINT` | deployment | string | absent |
| `KEYCLOAK_HTTP_PORT` | domain | integer; local container port, gated by supporting-service deployment (`docker-compose.yml:495`) | absent |
| `KEYCLOAK_INTERNAL_URL` | deployment | D14 / baseline string | absent |
| `KEYCLOAK_REALM` | deployment | string | absent |
| `KEYCLOAK_REQUIRE_HTTPS_METADATA` | domain | D14 / baseline string | absent |
| `LISTMONK_API_KEY` | deployment | string | absent |
| `LISTMONK_API_USERNAME` | deployment | string | absent |
| `LISTMONK_DEFAULT_LIST_ID` | domain | D15 / baseline string | absent |
| `LISTMONK_ENABLED` | domain | boolean | false |
| `LISTMONK_INSTANCE_URL` | extensible | string | absent |
| `LISTMONK_PRECONFIRM_SUBSCRIPTIONS` | domain | D15 / baseline string | absent |
| `LISTMONK_SYNC_ON_REGISTRATION` | domain | D15 / baseline string | absent |
| `LOCALIZATION_TMS_API_KEY` | deployment | string | absent |
| `LOCAL_STORAGE_CREATE_ROOT_IF_MISSING` | domain | D16 / baseline string | absent |
| `LOCAL_STORAGE_ROOT_PATH` | deployment | D16 / baseline string | absent |
| `MAILPIT_UI_PORT` | domain | integer | 8025 |
| `MAIL_SMTP_ENCRYPTION` | closed | C14 | absent |
| `MAIL_SMTP_FROM_ADDRESS` | deployment | string | absent |
| `MAIL_SMTP_FROM_NAME` | deployment | string | absent |
| `MAIL_SMTP_HOST` | deployment | string | absent |
| `MAIL_SMTP_PASSWORD` | deployment | string | absent |
| `MAIL_SMTP_PORT` | domain | integer | absent |
| `MAIL_SMTP_USERNAME` | deployment | string | absent |
| `MANAGED_CLIENT_EXTERNAL_PROVIDER` | extensible | D19 / baseline string | absent |
| `MCP_ENABLED` | domain | boolean | false |
| `MCP_ENABLE_LEGACY_SSE` | domain | D17 / baseline string | absent |
| `MCP_ENDPOINT_PATH` | deployment | D17 / baseline string | absent |
| `MCP_STATELESS` | domain | D17 / baseline string | absent |
| `MESSAGING_URI` | deployment | D26 / baseline string | absent |
| `MINIO_API_PORT` | domain | integer | absent |
| `MINIO_CONSOLE_PORT` | domain | integer | absent |
| `OSPREY_BIDI_STREAM_PORT` | domain | D03 / baseline string | absent |
| `OSPREY_IMAGE` | extensible | D03 / baseline string | absent |
| `OSPREY_POD_IP` | deployment | D03 / baseline string | absent |
| `OSPREY_RUST_LOG` | extensible | D03 / baseline string | absent |
| `OSPREY_SYNC_ACTION_PORT` | domain | D03 / baseline string | absent |
| `PAYMENTS_ORGANIZER_DIRECT_CONNECT_PLATFORM_ID` | deployment | D18 / baseline string | absent |
| `PAYMENTS_ORGANIZER_DIRECT_PROVIDER_CODE` | extensible | D18 / baseline string | absent |
| `PAYMENTS_STRIPE_MODE` | closed | C17 | absent |
| `PAYMENTS__CHECKOUTGOVERNANCE__ACTIVATIONSTATUS` | closed | C18 | absent |
| `PAYMENTS__CHECKOUTGOVERNANCE__CHARGETYPE` | closed | C19 | absent |
| `PAYMENTS__CHECKOUTGOVERNANCE__COMPLAINTOWNER` | extensible | string | absent |
| `PAYMENTS__CHECKOUTGOVERNANCE__DISPUTEOWNER` | extensible | string | absent |
| `PAYMENTS__CHECKOUTGOVERNANCE__RECONCILIATIONOWNER` | extensible | string | absent |
| `PAYMENTS__CHECKOUTGOVERNANCE__REFUNDOWNER` | extensible | string | absent |
| `PAYMENTS__CHECKOUTGOVERNANCE__REFUNDPOLICYLANGUAGETAG` | deployment | D18 / baseline string | absent |
| `PAYMENTS__CHECKOUTGOVERNANCE__STATEMENTDESCRIPTOR` | deployment | D18 / baseline string | absent |
| `PHYSICAL_TENANCY_MODE` | extensible | D19 / baseline string | absent |
| `POSTGRESQL_DATABASE` | deployment | string | absent |
| `POSTGRESQL_HOST` | deployment | string | absent |
| `POSTGRESQL_PASSWORD` | deployment | string | absent |
| `POSTGRESQL_PORT` | domain | integer | absent |
| `POSTGRESQL_USERNAME` | deployment | string | absent |
| `PRIVACY_ERASURE_AUTHORITY_BUSY_TIMEOUT_SECONDS` | domain | D06 / baseline string | absent |
| `PRIVACY_ERASURE_AUTHORITY_DATABASE` | deployment | D06 / baseline string | absent |
| `PRIVACY_ERASURE_AUTHORITY_EMBEDDED_PATH` | deployment | D06 / baseline string | absent |
| `PRIVACY_ERASURE_AUTHORITY_HOST` | deployment | D06 / baseline string | absent |
| `PRIVACY_ERASURE_AUTHORITY_MIGRATOR_PASSWORD` | deployment | D06 / baseline string | absent |
| `PRIVACY_ERASURE_AUTHORITY_MIGRATOR_USERNAME` | deployment | D06 / baseline string | absent |
| `PRIVACY_ERASURE_AUTHORITY_PORT` | domain | D06 / baseline string | absent |
| `PRIVACY_ERASURE_AUTHORITY_RUNTIME_PASSWORD` | deployment | D06 / baseline string | absent |
| `PRIVACY_ERASURE_AUTHORITY_RUNTIME_USERNAME` | deployment | D06 / baseline string | absent |
| `PRIVACY_ERASURE_AUTHORITY_TLS_MODE` | closed | C04 | absent |
| `PRIVACY_ERASURE_AUTHORITY_TRUST_SERVER_CERTIFICATE` | domain | boolean | absent |
| `PRIVACY_ERASURE_AUTHORITY_WRITER_REPLICA_COUNT` | domain | D06 / baseline string | absent |
| `PRIVACY_ERASURE_IDENTITY_FENCE_KEY` | deployment | string | absent |
| `PRIVACY_ERASURE_IDENTITY_FENCE_KEY_ID` | deployment | D26 / baseline string | absent |
| `PROMOTIONS_CODE_LOOKUP_ACTIVE_KEY_VERSION` | domain | D25 / baseline string | absent |
| `PROMOTIONS_CODE_LOOKUP_HMAC_KEY` | deployment | string | absent |
| `PROVISIONING_MODE` | extensible | D19 / baseline string | absent |
| `PROVISIONING_TRUSTED` | domain | D19 / baseline string | absent |
| `PUBLIC_BASE_URL` | extensible | string | absent |
| `RATELIMITING__ANONYMOUSREGISTRATION__CONCURRENCYLIMIT` | domain | D20 / baseline string | "8" |
| `RATELIMITING__ANONYMOUSREGISTRATION__IPPERMITLIMIT` | domain | D20 / baseline string | "10" |
| `RATELIMITING__ANONYMOUSREGISTRATION__QUEUELIMIT` | domain | D20 / baseline string | "0" |
| `RATELIMITING__ANONYMOUSREGISTRATION__SUBNETPERMITLIMIT` | domain | D20 / baseline string | "40" |
| `RATELIMITING__ANONYMOUSREGISTRATION__WINDOWSECONDS` | domain | D20 / baseline string | "60" |
| `REDIS_CONNECTION_STRING` | deployment | D26 / baseline string | absent |
| `REGISTRATION_PROVIDER_API_TOKEN` | deployment | string | absent |
| `REGISTRATION_PROVIDER_WEBHOOK_SECRET` | deployment | string | absent |
| `REPORTING_COOP_ALLOW_LOCAL_PROVIDER_ENDPOINTS` | domain | D21 / baseline string | absent |
| `REPORTING_COOP_API_KEY` | deployment | D21 / baseline string | absent |
| `REPORTING_COOP_ENABLED` | domain | boolean | absent |
| `REPORTING_COOP_ENDPOINT_URL` | extensible | string | absent |
| `REPORTING_COOP_WEBHOOK_SECRET` | deployment | D21 / baseline string | absent |
| `REPORTING_ENABLED` | domain | boolean | false |
| `REPORTING_EVALUATE_SIGNALS` | domain | D21 / baseline string | absent |
| `REPORTING_EXECUTE_DECISIONS` | domain | D21 / baseline string | absent |
| `REPORTING_HEALTH_FAILED_PROVIDER_SYNC_WARNING_THRESHOLD` | domain | D21 / baseline string | absent |
| `REPORTING_HEALTH_STUCK_PROVIDER_SYNC_MINUTES` | domain | D21 / baseline string | absent |
| `REPORTING_MIRROR_REVIEW_QUEUE` | domain | D21 / baseline string | absent |
| `REPORTING_MODE` | closed | C08 | absent |
| `REPORTING_OSPREY_ALLOW_LOCAL_PROVIDER_ENDPOINTS` | domain | D21 / baseline string | absent |
| `REPORTING_OSPREY_API_KEY` | deployment | D21 / baseline string | absent |
| `REPORTING_OSPREY_ENABLED` | domain | boolean | absent |
| `REPORTING_OSPREY_ENDPOINT_URL` | extensible | string | absent |
| `REPORTING_SYNC_REPORTS` | domain | D21 / baseline string | absent |
| `SECRET_PROVIDER` | closed | C05 | absent |
| `SETUP_SECRET` | deployment | string | absent |
| `SETUP_SECRET_BINDING_COMMITMENT_HMAC_KEY` | deployment | string | absent |
| `SETUP_SECRET_FILE` | deployment | D19 / baseline string | absent |
| `SETUP_SECRET_REQUIRED` | domain | boolean | absent |
| `STORAGE_RECONCILIATION_DELETE_QUARANTINED_OBJECTS` | domain | D16 / baseline string | absent |
| `STORAGE_RECONCILIATION_DRY_RUN` | domain | boolean | false |
| `STORAGE_RECONCILIATION_ENABLED` | domain | boolean | false |
| `STORAGE_RECONCILIATION_QUARANTINE_MISSING_OBJECTS` | domain | D16 / baseline string | absent |
| `STORAGE_RECONCILIATION_QUARANTINE_ORPHAN_LOCAL_FILES` | domain | D16 / baseline string | absent |
| `STORAGE_S3_ACCESS_KEY_ID` | deployment | string | absent |
| `STORAGE_S3_BUCKET_NAME` | deployment | string | absent |
| `STORAGE_S3_ENDPOINT` | deployment | string | absent |
| `STORAGE_S3_PUBLIC_ENDPOINT` | deployment | string | absent |
| `STORAGE_S3_REGION` | deployment | string | absent |
| `STORAGE_S3_SECRET_ACCESS_KEY` | deployment | string | absent |
| `STRIPE_PLATFORM_SECRET_KEY` | deployment | string | absent |
| `STRIPE_WEBHOOK_SECRET` | deployment | string | absent |
| `SVIX_CACHE_TYPE` | deployment | D03 / baseline string | absent |
| `SVIX_DB_DATABASE` | deployment | D03 / baseline string | absent |
| `SVIX_DB_DSN` | deployment | D03 / baseline string | absent |
| `SVIX_DB_PASSWORD` | deployment | D03 / baseline string | absent |
| `SVIX_DB_USERNAME` | deployment | D03 / baseline string | absent |
| `SVIX_HTTP_PORT` | domain | integer | absent |
| `SVIX_JWT_SECRET` | deployment | D03 / baseline string | absent |
| `SVIX_QUEUE_TYPE` | deployment | D03 / baseline string | absent |
| `SVIX_REDIS_DSN` | deployment | D03 / baseline string | absent |
| `SVIX_TAG` | extensible | D03 / baseline string | absent |
| `TICKETING_RECOVERY_MANIFEST_HMAC_KEY` | deployment | string | absent |
| `TICKETING__RECOVERY__BACKLOGTHRESHOLD` | domain | D22 / baseline string | absent |
| `TICKETING__RECOVERY__DECLAREDRPOMINUTES` | domain | D22 / baseline string | absent |
| `TICKETING__RECOVERY__DECLAREDRTOMINUTES` | domain | D22 / baseline string | absent |
| `TICKETING__RECOVERY__ENABLED` | domain | boolean | absent |
| `TICKETING__RECOVERY__EXPECTEDRELEASEREVISION` | deployment | D22 / baseline string | absent |
| `TICKETING__RECOVERY__EXPECTEDSCHEMAREVISION` | deployment | D22 / baseline string | absent |
| `TICKETING__RECOVERY__MANIFESTSIGNINGKEYREFERENCE` | closed | C24 / baseline string | absent |
| `TICKETING__RECOVERY__MINIMUMAUTHORITYFLOOR` | domain | D22 / baseline string | absent |
| `TICKETING__RECOVERY__MINIMUMIDEMPOTENCYFLOOR` | domain | D22 / baseline string | absent |
| `TICKETING__RECOVERY__MINIMUMPROVIDERCURSOR` | domain | D22 / baseline string | absent |
| `TICKETING__RECOVERY__MINIMUMRETAINEDKEYVERSION` | domain | D22 / baseline string | absent |
| `TICKETING__RECOVERY__MINIMUMWORKERFENCE` | domain | D22 / baseline string | absent |
| `TICKETING__RECOVERY__RETAINEDKEYVERSIONS__0` | domain | D22 / baseline string | absent |
| `TICKETING__RECOVERY__UNHEALTHYOLDESTDUESECONDS` | domain | D22 / baseline string | absent |
| `TICKETING__RECOVERY__WARNINGOLDESTDUESECONDS` | domain | D22 / baseline string | absent |
| `UI_HTTP_PORT` | domain | integer | absent |
| `VAPID_PRIVATE_KEY` | deployment | D23 / baseline string | absent |
| `VAPID_PUBLIC_KEY` | deployment | D23 / baseline string | absent |
| `VAPID_SUBJECT` | deployment | D23 / baseline string | absent |
| `WEBHOOKS_ENABLED` | domain | boolean | false |
| `WEBHOOKS_PROVIDER` | closed | C09 | absent |
| `WEBHOOKS_SVIX_AUTH_TOKEN` | deployment | string | absent |
| `WEBHOOKS_SVIX_AUTH_TOKEN_SECRET_REF` | deployment | D24 / baseline string | absent |
| `WEBHOOKS_SVIX_BASE_URL` | extensible | string | absent |
| `WEBHOOKS_SVIX_CAPABILITY_POLICY_VERSION` | lookup | C23 / baseline string | absent |
| `WEBHOOKS_SVIX_ENVIRONMENT` | lookup | C21 / baseline string | absent |
| `WEBHOOKS_SVIX_OPERATIONAL_WEBHOOK_SECRET` | deployment | string | absent |
| `WEBHOOKS_SVIX_OPERATIONAL_WEBHOOK_SECRET_REF` | deployment | D24 / baseline string | absent |
| `WEBHOOKS_SVIX_PROVIDER_VERSION` | lookup | C22 / baseline string | absent |
| `WEBLATE_ADMIN_EMAIL` | deployment | D03 / baseline string | absent |
| `WEBLATE_ADMIN_NAME` | deployment | D03 / baseline string | absent |
| `WEBLATE_ADMIN_PASSWORD` | deployment | D03 / baseline string | absent |
| `WEBLATE_HTTP_PORT` | domain | integer | absent |
| `WEBLATE_IMAGE` | extensible | D03 / baseline string | absent |
| `WEBLATE_POSTGRES_DB` | deployment | D03 / baseline string | absent |
| `WEBLATE_POSTGRES_PASSWORD` | deployment | D03 / baseline string | absent |
| `WEBLATE_POSTGRES_USER` | deployment | D03 / baseline string | absent |
| `WEBLATE_SITE_DOMAIN` | deployment | D03 / baseline string | absent |
| `WEB_PUSH_ENABLED` | domain | boolean | false |
