using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests.Definitions;

public sealed class EventProviderContractTests
{
    // Target: Event b6ad1594b7f92ac3399a5a8bf8d241e4f8d69c66; canonical wire choices.
    [Test]
    [Arguments("/platform/authenticationProvider", "local,keycloak,atproto")]
    [Arguments("/platform/authorizationProvider", "local,cerbos")]
    [Arguments("/database/provider", "PostgreSql,Sqlite,SqlServer,MariaDb,MySql")]
    [Arguments("/identity/database/provider", "PostgreSql,Sqlite,SqlServer,MariaDb,MySql")]
    [Arguments("/database/tlsMode", "Prefer,Required,Disabled")]
    [Arguments("/identity/database/tlsMode", "Prefer,Required,Disabled")]
    [Arguments("/database/erasure/tlsMode", "Prefer,Required,Disabled")]
    [Arguments("/secrets/provider", "Environment,UserSecrets,Infisical")]
    [Arguments("/integrations/ai/provider", "NONE,FAKE,OPENAI_COMPATIBLE,ANTHROPIC_COMPATIBLE,OPENAI,AZURE_OPENAI,ANTHROPIC")]
    [Arguments("/integrations/geocoding/provider", "None,Photon")]
    [Arguments("/observability/reporting/mode", "Disabled,LocalOnly,Osprey,Coop,Composite")]
    [Arguments("/integrations/webhooks/provider", "Disabled,Local,Svix,Composite,DryRun")]
    [Arguments("/hosting/configurationManifest/mode", "Off,ValidateOnly,Bootstrap")]
    [Arguments("/identity/bootstrapMode", "Interactive,ConfiguredAdministrator")]
    [Arguments("/bootstrapAdministrator/provider", "local,keycloak,atproto")]
    [Arguments("/hosting/deploymentMode", "SingleTenant,MultiTenant")]
    [Arguments("/identity/database/topology", "colocated,external")]
    [Arguments("/platform/erasure/databaseTopology", "EmbeddedSqlite,ExternalDatabase,CoLocated")]
    [Arguments("/messaging/smtp/encryption", "None,StartTls,SslOnConnect,Auto")]
    [Arguments("/payments/stripe/mode", "Test,Live")]
    [Arguments("/platform/checkoutGovernance/activationStatus", "approved,suspended")]
    [Arguments("/platform/checkoutGovernance/chargeType", "direct-charge")]
    [Arguments("/identity/operatorIdentity/operatorKindCode", "registered_organization,sole_trader,individual,public_body,unincorporated_association")]
    [Arguments("/svix/environment", "self-hosted")]
    [Arguments("/svix/providerVersion", "1.96.1")]
    [Arguments("/svix/capabilityPolicyVersion", "svix-self-hosted-1.96.1-v1")]
    [Arguments("/localStorage/createRootIfMissing", "true,false")]
    [Arguments("/observability/reporting/evaluateSignals", "true,false")]
    [Arguments("/observability/reporting/executeDecisions", "true,false")]
    [Arguments("/observability/reporting/mirrorReviewQueue", "true,false")]
    [Arguments("/observability/reporting/syncReports", "true,false")]
    [Arguments("/observability/reporting/coop/allowLocalProviderEndpoints", "true,false")]
    [Arguments("/observability/reporting/osprey/allowLocalProviderEndpoints", "true,false")]
    [Arguments("/integrations/listmonk/preconfirmSubscriptions", "true,false")]
    [Arguments("/integrations/listmonk/syncOnRegistration", "true,false")]
    [Arguments("/platform/provisioning/trusted", "true,false")]
    [Arguments("/platform/mcp/stateless", "true,false")]
    [Arguments("/platform/mcp/enableLegacySse", "true,false")]
    [Arguments("/controlPlane/managedMode", "true,false")]
    [Arguments("/keycloak/requireHttpsMetadata", "true,false")]
    [Arguments("/storageReconciliation/deleteQuarantinedObjects", "true,false")]
    [Arguments("/storageReconciliation/quarantineMissingObjects", "true,false")]
    [Arguments("/storageReconciliation/quarantineOrphanLocalFiles", "true,false")]
    [Arguments("/security/ticketingRecovery/manifestSigningKeyReference", "ticketing.recovery_manifest_hmac_key")]
    public async Task CanonicalChoicesMatchReviewedTargetContract(string location, string expected)
    {
        foreach (Definition definition in new[] { Definition(), StoreDefinition() })
        {
            FieldDefinition field = definition.Field(location);
            await Assert.That(string.Join(',', field.Choices.Select(value => value.GetString()))).IsEqualTo(expected);
            await Assert.That(field.Choices.All(value => value.ValueKind == JsonValueKind.String)).IsTrue();
        }
    }

    [Test]
    [Arguments("/recipientAuthorities/infisical/combined/clientSecret", null)]
    [Arguments("/database/runtimePassword", DefinitionSecretKind.Password)]
    [Arguments("/messaging/vapid/privateKey", null)]
    [Arguments("/messaging/emailDispatch/connectionString", DefinitionSecretKind.ConnectionString)]
    [Arguments("/controlPlane/registrationToken", null)]
    public async Task TargetCredentialsRequireSecretCustodyWithoutInventingUnknownFormats(string location, DefinitionSecretKind? expectedKind)
    {
        foreach (Definition definition in new[] { Definition(), StoreDefinition() })
        {
            FieldDefinition field = definition.Field(location);
            await Assert.That(field.Sensitivity).IsEqualTo(FieldSensitivity.Secret);
            await Assert.That(field.Node.Secret?.Kind).IsEqualTo(expectedKind);
        }
    }

    [Test]
    public async Task SettingsAreNestedObjectsWithoutFlatKeysOrSyntheticToggles()
    {
        foreach (Definition definition in new[] { Definition(), StoreDefinition() })
            await Assert.That(definition.Configuration.Properties.Keys.Where(name => name.Contains('.', StringComparison.Ordinal)))
                .IsEmpty();
    }

    [Test]
    public async Task AuthenticationSelectionControlsConnectionRequirementsWithoutLocalDeployment()
    {
        Definition definition = Definition();
        foreach (string provider in new[] { "local", "keycloak", "atproto" })
        {
            DefinitionValidationResult result = DefinitionValidator.Validate(definition,
                DefinitionPathLookup.Answers(0, ("/platform/authenticationProvider", provider)));
            foreach (string pointer in new[] { "/keycloak/endpoint", "/keycloak/realm", "/keycloak/clientId", "/keycloak/blazorClientSecret" })
            {
                DefinitionNodeState node = result.Node(definition, pointer);
                await Assert.That(node.IsActive).IsEqualTo(provider == "keycloak");
                await Assert.That(node.IsRequired).IsEqualTo(provider == "keycloak");
            }
            await Assert.That(result.Node(definition, "/supportingServices/keycloak/adminPassword").IsActive).IsFalse();
            await Assert.That(result.Node(definition, "/supportingServices/keycloak/httpPort").IsActive).IsFalse();
        }
    }

    [Test]
    public async Task SqliteUsesDatabaseNameWithoutServerCredentials()
    {
        Definition definition = Definition();
        DefinitionValidationResult result = DefinitionValidator.Validate(definition, DefinitionPathLookup.Answers(0,
            ("/database/provider", "Sqlite"), ("/database/name", "event.db"),
            ("/database/runtimePassword", "synthetic-inactive-password")));
        await Assert.That(result.Node(definition, "/database/name").IsRequired).IsTrue();
        foreach (string pointer in new[] { "/database/host", "/database/port", "/database/runtimeUsername", "/database/runtimePassword",
            "/database/migratorUsername", "/database/migratorPassword", "/databaseServer/version", "/database/tlsMode",
            "/database/trustServerCertificate" })
        {
            await Assert.That(result.Node(definition, pointer).IsActive).IsFalse();
            await Assert.That(result.TryGetEffectiveValue(DefinitionPath.Parse(pointer), out _)).IsFalse();
        }
    }

    [Test]
    public async Task BootstrapModeControlsAdministratorFieldsAndNamePair()
    {
        Definition definition = Definition();
        foreach (string mode in new[] { "Interactive", "ConfiguredAdministrator" })
        {
            DefinitionValidationResult result = DefinitionValidator.Validate(definition, DefinitionPathLookup.Answers(0,
                ("/identity/bootstrapMode", mode), ("/bootstrapAdministrator/firstName", "Synthetic")));
            await Assert.That(result.Node(definition, "/bootstrapAdministrator/provider").IsRequired).IsEqualTo(mode == "ConfiguredAdministrator");
            await Assert.That(result.Node(definition, "/bootstrapAdministrator/lastName").IsRequired).IsEqualTo(mode == "ConfiguredAdministrator");
        }
    }

    [Test]
    [Arguments("env.AI_API_KEY")]
    [Arguments("/integrations/ai/apiKey")]
    public async Task RetiredCredentialAliasRejectsInsteadOfProducingIneffectiveOutput(string location)
    {
        AnswerSnapshot answers = location.StartsWith('/')
            ? DefinitionPathLookup.Answers(0, (location, "synthetic-retired-credential"))
            : new AnswerSnapshot(0, new Dictionary<string, JsonElement> { [location] = JsonSerializer.SerializeToElement("synthetic-retired-credential") });
        foreach (Definition definition in new[] { Definition(), StoreDefinition() })
            await Assert.That(DefinitionValidator.Validate(definition, answers).Diagnostics
                .Any(item => item.Code is "field-unknown" or "value-invalid")).IsTrue();
    }

    [Test]
    public async Task UnknownAuthenticationChoiceFailsSharedValidation()
    {
        Definition definition = Definition();
        DefinitionValidationResult result = DefinitionValidator.Validate(definition,
            DefinitionPathLookup.Answers(0, ("/platform/authenticationProvider", "unsupported-provider")));
        string platform = definition.Fields.Single(field => field.Name == "platform").Id;
        await Assert.That(result.Diagnostics.Any(item => item.FieldId == platform && item.Code == "value-invalid"
            && item.RelativePath.SequenceEqual(AuthenticationProviderPath))).IsTrue();
    }

    private static readonly string[] AuthenticationProviderPath = ["authenticationProvider"];

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
