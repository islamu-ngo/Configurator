using System.Text.Json;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using System.Text;

namespace Configurator.Core.Tests.Definitions;

public sealed class EventDeploymentContractTests
{
    [Test]
    public async Task FreshEnvironmentStartsWithStandaloneAndAllComponents()
    {
        DefinitionValidationResult validation = DefinitionValidator.Validate(Definition(), new AnswerSnapshot(0, new Dictionary<string, JsonElement>()));
        await Assert.That(validation.TryGetEffectiveValue(DefinitionPath.Parse("/deployment/recipe"), out JsonElement recipe)).IsTrue();
        await Assert.That(recipe.GetString()).IsEqualTo("standalone");
        await Assert.That(validation.TryGetEffectiveValue(DefinitionPath.Parse("/deployment/scope"), out JsonElement scope)).IsTrue();
        await Assert.That(scope.GetString()).IsEqualTo("all");
    }

    [Test]
    public async Task BlazorScopeExcludesApiAdministratorAndRequiresItsExternalApiConnection()
    {
        Definition definition = Definition();
        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, DefinitionPathLookup.Answers(0,
            ("/deployment/recipe", "separate-images"), ("/deployment/scope", "blazor"),
            ("/platform/setupSecret", "synthetic-api-administrator-sentinel"), ("/apiEndpoint", "https://api.example.test/")));
        await Assert.That(validation.Node(definition, "/platform/setupSecret").IsActive).IsFalse();
        await Assert.That(validation.Node(definition, "/apiEndpoint").IsRequired).IsTrue();
        await Assert.That(validation.TryGetEffectiveValue(DefinitionPath.Parse("/platform/setupSecret"), out _)).IsFalse();
    }

    [Test]
    public async Task SecretAuthoritySelectionIsRequiredWithoutAnOptionalSecurityToggle()
    {
        Definition definition = Definition();
        DefinitionValidationResult validation = DefinitionValidator.Validate(definition, new AnswerSnapshot(0, new Dictionary<string, JsonElement>()));
        DefinitionNodeState authority = validation.Node(definition, "/secrets/provider");
        await Assert.That(authority.IsActive).IsTrue();
        await Assert.That(authority.IsRequired).IsTrue();
    }

    [Test]
    public async Task CombinedAdminOriginReachesRoutingAndApiConsumersFromOneAnswer()
    {
        AnswerSnapshot source = AnswerSnapshotCodec.ParseJson(Resource("EventAnswers.environment-standalone.json"), 6);
        var values = source.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        values.Set("/instanceAdminPublicOrigin", "https://admin.example.test/");
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(Definition(), "environment-standalone", new AnswerSnapshot(7, values));
        await Assert.That(result.Succeeded).IsTrue();
        using DefinitionArtifact artifact = result.Artifact!;
        string text = Encoding.UTF8.GetString(artifact.Bytes.Span);
        await Assert.That(text).Contains("INSTANCE_ADMIN_PUBLIC_ORIGIN=https://admin.example.test/\n");
        await Assert.That(text).Contains("Bff__AdminHosts__0=https://admin.example.test/\n");
    }

    [Test]
    public async Task BlazorCompleteAndDraftBytesExcludeRetainedApiAndMigrationAuthority()
    {
        AnswerSnapshot source = AnswerSnapshotCodec.ParseJson(Resource("EventAnswers.environment-blazor.json"), 4);
        var values = source.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        // Values retained from an API-scope session stay in scope-excluded members of included groups.
        values.Set("/platform/setupSecret", "synthetic-api-administrator-sentinel");
        values.Set("/recipientAuthorities/infisical/migration/clientSecret", "synthetic-migration-sentinel");
        values.Set("/recipientAuthorities/infisical/api/clientSecret", "synthetic-api-vault-sentinel");
        values.Set("/hosting/replicaCount", 2);
        var answers = new AnswerSnapshot(5, values);
        Definition definition = Definition();
        DefinitionArtifactResult complete = DefinitionArtifactComposer.Compose(definition, "environment-blazor", answers);
        await Assert.That(complete.Succeeded).IsTrue();
        using DefinitionArtifact artifact = complete.Artifact!;
        string completeText = Encoding.UTF8.GetString(artifact.Bytes.Span);
        await Assert.That(completeText).DoesNotContain("synthetic-api-");
        await Assert.That(completeText).DoesNotContain("synthetic-migration-");
        DefinitionDraftArtifactResult draft = DefinitionDraftArtifactComposer.Compose(definition, "environment-blazor", answers);
        await Assert.That(draft.Succeeded).IsTrue();
        using DefinitionArtifact draftArtifact = draft.Artifact!;
        string draftText = Encoding.UTF8.GetString(draftArtifact.Bytes.Span);
        await Assert.That(draftText).DoesNotContain("synthetic-api-");
        await Assert.That(draftText).DoesNotContain("synthetic-migration-");
        await Assert.That(DefinitionArtifactComposer.Compose(definition, "environment-api", answers).Succeeded).IsFalse();
    }

    private static byte[] Resource(string name)
    {
        using Stream stream = typeof(EventDeploymentContractTests).Assembly.GetManifestResourceStream(name)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static Definition Definition()
    {
        return DefinitionCodec.ParseJson(Resource("EventDefinition"));
    }
}
