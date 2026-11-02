using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests.Definitions;

public sealed class EventProviderContractTests
{
    // Target: Event b6ad1594b7f92ac3399a5a8bf8d241e4f8d69c66; canonical wire choices.
    [Test]
    [Arguments("AUTHENTICATION_PROVIDER", "local,keycloak,atproto")]
    [Arguments("AUTHORIZATION_PROVIDER", "local,cerbos")]
    [Arguments("DATABASE_PROVIDER", "PostgreSql,Sqlite,SqlServer,MariaDb,MySql")]
    [Arguments("IDENTITY_DATABASE_PROVIDER", "PostgreSql,Sqlite,SqlServer,MariaDb,MySql")]
    [Arguments("DATABASE_TLS_MODE", "Prefer,Required,Disabled")]
    [Arguments("IDENTITY_DATABASE_TLS_MODE", "Prefer,Required,Disabled")]
    [Arguments("ERASURE_DATABASE_TLS_MODE", "Prefer,Required,Disabled")]
    [Arguments("PRIVACY_ERASURE_AUTHORITY_TLS_MODE", "Prefer,Required,Disabled")]
    [Arguments("SECRET_PROVIDER", "Environment,UserSecrets,Infisical")]
    [Arguments("AI_PROVIDER", "NONE,FAKE,OPENAI_COMPATIBLE,ANTHROPIC_COMPATIBLE,OPENAI,AZURE_OPENAI,ANTHROPIC")]
    [Arguments("GEOCODING_PROVIDER", "None,Photon")]
    [Arguments("REPORTING_MODE", "Disabled,LocalOnly,Osprey,Coop,Composite")]
    [Arguments("WEBHOOKS_PROVIDER", "Disabled,Local,Svix,Composite,DryRun")]
    [Arguments("CONFIGURATION_MANIFEST_MODE", "Off,ValidateOnly,Bootstrap")]
    [Arguments("INSTANCE_BOOTSTRAP_MODE", "Interactive,ConfiguredAdministrator")]
    [Arguments("INSTANCE_BOOTSTRAP_ADMIN_PROVIDER", "local,keycloak,atproto")]
    [Arguments("DEPLOYMENT_MODE", "SingleTenant,MultiTenant")]
    [Arguments("IDENTITY_DATABASE_TOPOLOGY", "colocated,external")]
    [Arguments("ERASURE_DATABASE_TOPOLOGY", "EmbeddedSqlite,ExternalDatabase,CoLocated")]
    [Arguments("MAIL_SMTP_ENCRYPTION", "None,StartTls,SslOnConnect,Auto")]
    [Arguments("PAYMENTS_STRIPE_MODE", "Test,Live")]
    [Arguments("PAYMENTS__CHECKOUTGOVERNANCE__ACTIVATIONSTATUS", "approved,suspended")]
    [Arguments("PAYMENTS__CHECKOUTGOVERNANCE__CHARGETYPE", "direct-charge")]
    [Arguments("INSTANCE__OPERATORIDENTITY__OPERATORKINDCODE", "registered_organization,sole_trader,individual,public_body,unincorporated_association")]
    [Arguments("WEBHOOKS_SVIX_ENVIRONMENT", "self-hosted")]
    [Arguments("WEBHOOKS_SVIX_PROVIDER_VERSION", "1.96.1")]
    [Arguments("WEBHOOKS_SVIX_CAPABILITY_POLICY_VERSION", "svix-self-hosted-1.96.1-v1")]
    [Arguments("LOCAL_STORAGE_CREATE_ROOT_IF_MISSING", "true,false")]
    [Arguments("REPORTING_EVALUATE_SIGNALS", "true,false")]
    [Arguments("REPORTING_EXECUTE_DECISIONS", "true,false")]
    [Arguments("REPORTING_MIRROR_REVIEW_QUEUE", "true,false")]
    [Arguments("REPORTING_SYNC_REPORTS", "true,false")]
    [Arguments("REPORTING_COOP_ALLOW_LOCAL_PROVIDER_ENDPOINTS", "true,false")]
    [Arguments("REPORTING_OSPREY_ALLOW_LOCAL_PROVIDER_ENDPOINTS", "true,false")]
    [Arguments("LISTMONK_PRECONFIRM_SUBSCRIPTIONS", "true,false")]
    [Arguments("LISTMONK_SYNC_ON_REGISTRATION", "true,false")]
    [Arguments("PROVISIONING_TRUSTED", "true,false")]
    [Arguments("MCP_STATELESS", "true,false")]
    [Arguments("MCP_ENABLE_LEGACY_SSE", "true,false")]
    [Arguments("CONTROL_PLANE_MANAGED_MODE", "true,false")]
    [Arguments("KEYCLOAK_REQUIRE_HTTPS_METADATA", "true,false")]
    [Arguments("STORAGE_RECONCILIATION_DELETE_QUARANTINED_OBJECTS", "true,false")]
    [Arguments("STORAGE_RECONCILIATION_QUARANTINE_MISSING_OBJECTS", "true,false")]
    [Arguments("STORAGE_RECONCILIATION_QUARANTINE_ORPHAN_LOCAL_FILES", "true,false")]
    [Arguments("TICKETING__RECOVERY__MANIFESTSIGNINGKEYREFERENCE", "ticketing.recovery_manifest_hmac_key")]
    public async Task CanonicalChoicesMatchReviewedTargetContract(string name, string expected)
    {
        foreach (Definition definition in new[] { Definition(), StoreDefinition() })
        {
            FieldDefinition field = definition.Fields.Single(field => field.Id == "env." + name);
            await Assert.That(string.Join(',', field.Choices.Select(value => value.GetString()))).IsEqualTo(expected);
            await Assert.That(field.Choices.All(value => value.ValueKind == JsonValueKind.String)).IsTrue();
        }
    }

    [Test]
    [Arguments("INFISICAL_CLIENT_SECRET", null)]
    [Arguments("DATABASE_RUNTIME_PASSWORD", DefinitionSecretKind.Password)]
    [Arguments("VAPID_PRIVATE_KEY", null)]
    [Arguments("EMAIL_DISPATCH_RABBITMQ_CONNECTION_STRING", DefinitionSecretKind.ConnectionString)]
    [Arguments("CONTROL_PLANE_REGISTRATION_TOKEN", null)]
    public async Task TargetCredentialsRequireSecretCustodyWithoutInventingUnknownFormats(string name, DefinitionSecretKind? expectedKind)
    {
        foreach (Definition definition in new[] { Definition(), StoreDefinition() })
        {
            FieldDefinition field = definition.Fields.Single(field => field.Id == "env." + name);
            await Assert.That(field.Sensitivity).IsEqualTo(FieldSensitivity.Secret);
            await Assert.That(field.Node.Secret?.Kind).IsEqualTo(expectedKind);
        }
    }

    [Test]
    public async Task AuthenticationSelectionControlsConnectionRequirementsWithoutLocalDeployment()
    {
        Definition definition = Definition();
        foreach (string provider in new[] { "local", "keycloak", "atproto" })
        {
            var answers = new AnswerSnapshot(0, new Dictionary<string, JsonElement>
            {
                ["env.AUTHENTICATION_PROVIDER"] = JsonSerializer.SerializeToElement(provider)
            });
            DefinitionValidationResult result = DefinitionValidator.Validate(definition, answers);
            foreach (string name in new[] { "KEYCLOAK_ENDPOINT", "KEYCLOAK_REALM", "KEYCLOAK_CLIENT_ID", "KEYCLOAK_BLAZOR_CLIENT_SECRET" })
            {
                FieldState field = result.Fields.Single(field => field.Id == "env." + name);
                await Assert.That(field.IsActive).IsEqualTo(provider == "keycloak");
                await Assert.That(field.IsRequired).IsEqualTo(provider == "keycloak");
            }
            await Assert.That(result.Fields.Single(field => field.Id == "env.KEYCLOAK_ADMIN_PASSWORD").IsActive).IsFalse();
            await Assert.That(result.Fields.Single(field => field.Id == "env.KEYCLOAK_HTTP_PORT").IsActive).IsFalse();
        }
        await Assert.That(definition.Configuration.Properties.ContainsKey("select.feature.keycloak-config")).IsFalse();
    }

    [Test]
    public async Task SqliteUsesDatabaseNameWithoutServerCredentials()
    {
        var answers = new AnswerSnapshot(0, new Dictionary<string, JsonElement>
        {
            ["select.feature.database-config"] = JsonSerializer.SerializeToElement(true),
            ["env.DATABASE_PROVIDER"] = JsonSerializer.SerializeToElement("Sqlite"),
            ["env.DATABASE_NAME"] = JsonSerializer.SerializeToElement("event.db"),
            ["env.DATABASE_RUNTIME_PASSWORD"] = JsonSerializer.SerializeToElement("synthetic-inactive-password")
        });
        DefinitionValidationResult result = DefinitionValidator.Validate(Definition(), answers);
        await Assert.That(result.Fields.Single(field => field.Id == "env.DATABASE_NAME").IsRequired).IsTrue();
        foreach (string name in new[] { "DATABASE_HOST", "DATABASE_PORT", "DATABASE_RUNTIME_USERNAME", "DATABASE_RUNTIME_PASSWORD", "DATABASE_MIGRATOR_USERNAME", "DATABASE_MIGRATOR_PASSWORD", "DATABASE_SERVER_VERSION", "DATABASE_TLS_MODE", "DATABASE_TRUST_SERVER_CERTIFICATE" })
        {
            await Assert.That(result.Fields.Single(field => field.Id == "env." + name).IsActive).IsFalse();
            await Assert.That(result.TryGetEffectiveValue(DefinitionPath.FromSegments(["env." + name]), out _)).IsFalse();
        }
    }

    [Test]
    public async Task BootstrapModeControlsAdministratorFieldsAndNamePair()
    {
        Definition definition = Definition();
        foreach (string mode in new[] { "Interactive", "ConfiguredAdministrator" })
        {
            var answers = new AnswerSnapshot(0, new Dictionary<string, JsonElement>
            {
                ["select.feature.identity-config"] = JsonSerializer.SerializeToElement(true),
                ["env.INSTANCE_BOOTSTRAP_MODE"] = JsonSerializer.SerializeToElement(mode),
                ["env.INSTANCE_BOOTSTRAP_ADMIN_FIRST_NAME"] = JsonSerializer.SerializeToElement("Synthetic")
            });
            DefinitionValidationResult result = DefinitionValidator.Validate(definition, answers);
            await Assert.That(result.Fields.Single(field => field.Id == "env.INSTANCE_BOOTSTRAP_ADMIN_PROVIDER").IsRequired).IsEqualTo(mode == "ConfiguredAdministrator");
            await Assert.That(result.Fields.Single(field => field.Id == "env.INSTANCE_BOOTSTRAP_ADMIN_LAST_NAME").IsRequired).IsEqualTo(mode == "ConfiguredAdministrator");
        }
    }

    [Test]
    public async Task RetiredCredentialAliasRejectsInsteadOfProducingIneffectiveOutput()
    {
        var answers = new AnswerSnapshot(0, new Dictionary<string, JsonElement>
        {
            ["env.AI_API_KEY"] = JsonSerializer.SerializeToElement("synthetic-retired-credential")
        });
        foreach (Definition definition in new[] { Definition(), StoreDefinition() })
            await Assert.That(DefinitionValidator.Validate(definition, answers).Diagnostics.Any(item => item.Code == "field-unknown")).IsTrue();
    }

    [Test]
    public async Task UnknownAuthenticationChoiceFailsSharedValidation()
    {
        var answers = new AnswerSnapshot(0, new Dictionary<string, JsonElement>
        {
            ["env.AUTHENTICATION_PROVIDER"] = JsonSerializer.SerializeToElement("unsupported-provider")
        });
        DefinitionValidationResult result = DefinitionValidator.Validate(Definition(), answers);
        await Assert.That(result.Diagnostics.Any(item => item.FieldId == "env.AUTHENTICATION_PROVIDER" && item.Code == "value-invalid")).IsTrue();
    }

    private static Definition Definition()
    {
        using Stream stream = typeof(EventProviderContractTests).Assembly.GetManifestResourceStream("EventDefinition")!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return DefinitionCodec.ParseJson(buffer.ToArray());
    }

    private static Definition StoreDefinition()
    {
        using Stream stream = typeof(EventProviderContractTests).Assembly.GetManifestResourceStream("EventStoreSource")!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(buffer.ToArray()));
    }
}
