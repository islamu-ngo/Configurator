using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using Configurator.Presentation;

namespace Configurator.Architecture.Tests;

public sealed class OutputGuidanceProviderTests
{
    private static readonly string[] Names =
    [
        "isOfficialInstance", "jurisdictionCountryCode", "legalName", "operatorId",
        "operatorKindCode", "publicName", "websiteUrl"
    ];

    [Test]
    [Arguments("examples/islamu-event/configurator.json")]
    [Arguments("store/islamu-event/configurator.json")]
    public async Task SevenSameEntityDraftLinksRetainCompatiblePublicSourceAndSensitiveDestination(string path)
    {
        JsonNode source = JsonNode.Parse(File.ReadAllBytes(Path.Combine(ReleaseTestPaths.Root, path)))!;
        JsonNode properties = source["configuration"]!["properties"]!;
        JsonNode bootstrap = properties["identity"]!["properties"]!["operatorIdentity"]!["properties"]!;
        JsonNode document = properties["operatorIdentity"]!["properties"]!["document"]!["properties"]!;
        foreach (JsonNode ancestor in new[]
        {
            source["configuration"]!, properties["identity"]!,
            properties["identity"]!["properties"]!["operatorIdentity"]!
        })
            await Assert.That(ancestor["x-configurator"]!["sensitivity"]!.GetValue<string>()).IsEqualTo("public");
        foreach (string name in Names)
        {
            await Assert.That(bootstrap[name]!["type"]!.GetValue<string>())
                .IsEqualTo(name == "isOfficialInstance" ? "boolean" : "string");
            await Assert.That(document[name]!["type"]!.GetValue<string>())
                .IsEqualTo(name == "isOfficialInstance" ? "boolean" : "string");
            await Assert.That(bootstrap[name]!["x-configurator"]!["sensitivity"]!.GetValue<string>()).IsEqualTo("public");
            await Assert.That(document[name]!["x-configurator"]!["sensitivity"]!.GetValue<string>()).IsEqualTo("sensitive");
            await Assert.That(document[name]!["x-configurator"]!["suggestedFrom"]!["pointer"]!.GetValue<string>())
                .IsEqualTo("/identity/operatorIdentity/" + name);
        }
        await Assert.That(bootstrap["operatorKindCode"]!["enum"]!.AsArray().Count).IsEqualTo(5);
        await Assert.That(bootstrap["websiteUrl"]!["format"]!.GetValue<string>()).IsEqualTo("uri");
        await Assert.That(document["websiteUrl"]!["format"]).IsNull();
        await Assert.That(document["operatorKindCode"]!["enum"]).IsNull();
        _ = Compile(path);
    }

    [Test]
    [Arguments("Bootstrap", "MultiTenant", true, 3)]
    [Arguments("ValidateOnly", "SingleTenant", true, 2)]
    [Arguments("Off", "MultiTenant", false, 1)]
    [Arguments("Off", "SingleTenant", false, 0)]
    [Arguments("Bootstrap", "SingleTenant", false, 1)]
    [Arguments("ValidateOnly", "MultiTenant", false, 2)]
    [Arguments("Off", "SingleTenant", true, 1)]
    public async Task EnvironmentGuidanceUsesActualHostingModesAndOperatorPresence(
        string mode, string tenancy, bool identity, int count)
    {
        using var form = new DefinitionFormModel(Compile("examples/islamu-event/configurator.json"));
        form.SetValue(["hosting"], JsonSerializer.SerializeToElement(new
        {
            deploymentMode = tenancy,
            configurationManifest = new { mode }
        }));
        if (identity) SupplyBootstrap(form);
        string[] eligible = form.EligibleArtifacts.Select(artifact => artifact.Id).ToArray();
        var context = form.CaptureOutputExportContext(form.InputRevision, eligible);
        await Assert.That(form.RecordOutputExport(context, eligible, DefinitionOutputDeliveryKind.DownloadRequested)).IsTrue();
        var targets = form.OutputWorkflow.ProposedNextActions.Select(action => action.TargetOption).ToArray();
        await Assert.That(targets.Length).IsEqualTo(count);
        await Assert.That(targets.Contains("configuration-manifest")).IsEqualTo(mode != "Off");
        await Assert.That(targets.Contains("tenant-package")).IsEqualTo(tenancy == "MultiTenant");
        await Assert.That(targets.Contains("operator-identity")).IsEqualTo(identity);
    }

    [Test]
    public async Task RetainedInactiveBootstrapSupportsSevenExplicitTransfersAndMissingIdSupportsNone()
    {
        using var form = new DefinitionFormModel(Compile("examples/islamu-event/configurator.json"));
        await Assert.That(form.ReviewOutputTransition("operator-identity").Candidates.Count).IsEqualTo(0);
        SupplyBootstrap(form);
        await Assert.That(form.ReviewOutputTransition("operator-identity").Candidates.Count).IsEqualTo(7);
        form.SelectOutput("operator-identity");
        await Assert.That(form.Validate().Fields.Single(field => field.Definition.Name == "identity").IsActive).IsFalse();
        var review = form.ReviewOutputTransition("operator-identity");
        await Assert.That(review.Candidates.Count).IsEqualTo(7);
        await Assert.That(form.ApplyTransfer(review, review.Candidates.Select(candidate => candidate.Id).ToArray()).Succeeded).IsTrue();
        string[] expected = ["false", "\"ZZ\"", "\"Synthetic Operator\"", "\"synthetic-operator\"",
            "\"individual\"", "\"Synthetic\"", "\"https://example.invalid\""];
        for (int index = 0; index < Names.Length; index++)
        {
            await Assert.That(form.Validate().TryGetEffectiveValue(
                DefinitionPath.Parse("/operatorIdentity/document/" + Names[index]), out var value)).IsTrue();
            await Assert.That(value.GetRawText()).IsEqualTo(expected[index]);
        }
        string[] eligible = form.EligibleArtifacts.Select(artifact => artifact.Id).ToArray();
        var context = form.CaptureOutputExportContext(form.InputRevision, eligible);
        form.RecordOutputExport(context, eligible, DefinitionOutputDeliveryKind.DownloadRequested);
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Single().TargetOption).IsEqualTo("legal-drafts");
    }

    [Test]
    public async Task OperatorTaskWithoutDocumentIdDoesNotProposeLegalDrafts()
    {
        using var form = new DefinitionFormModel(Compile("store/islamu-event/configurator.json"));
        form.SelectOutput("operator-identity");
        string[] eligible = form.EligibleArtifacts.Select(artifact => artifact.Id).ToArray();
        var context = form.CaptureOutputExportContext(form.InputRevision, eligible);
        form.RecordOutputExport(context, eligible, DefinitionOutputDeliveryKind.DownloadRequested);
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task HistoricalReleasesRemainImmutableAndNewRevisionPreservesAnchorIdentity()
    {
        string[] hashes =
        [
            "65443aa92be441ec42728acf0cf31fd1dcf49fa5914689e1a6c48cc488b3cbf6",
            "029d4e573dc18533ca86735cf308a7e83a275cb807dcd7c92485f7cfad29e228",
            "bba29249228105562bcaed8b7439a58c0700ebccd2abdc3fed99236c861ab939",
            "229a6b556f09fe925aa036629fa526db13fc9a2b5c7b3d36eacdf02734af6288"
        ];
        for (int revision = 1; revision <= 4; revision++)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(ReleaseTestPaths.Root,
                $"store/islamu-event/releases/{revision}.json"));
            await Assert.That(Convert.ToHexStringLower(SHA256.HashData(bytes))).IsEqualTo(hashes[revision - 1]);
            _ = DefinitionCodec.ParseJson(bytes);
        }
        Definition old = DefinitionCodec.ParseJson(File.ReadAllBytes(
            Path.Combine(ReleaseTestPaths.Root, "store/islamu-event/releases/4.json")));
        foreach (string path in new[] { "examples/islamu-event/configurator.json", "store/islamu-event/configurator.json" })
        {
            Definition current = Compile(path);
            await Assert.That(current.Revision).IsEqualTo(5);
            await Assert.That(current.FamilyId).IsEqualTo(old.FamilyId);
            await Assert.That(current.Digest == old.Digest).IsFalse();
        }
    }

    [Test]
    [Arguments("examples/minimal-self-hosted/configurator.yaml")]
    [Arguments("examples/grafana-13.2.2/configurator.yaml")]
    [Arguments("examples/secret-generation/configurator.yaml")]
    public async Task NonTaskExamplesRemainQuiet(string path)
    {
        Definition definition = DefinitionAuthoringCompiler.Compile(
            DefinitionAuthoringCodec.ParseYaml(File.ReadAllBytes(Path.Combine(ReleaseTestPaths.Root, path))));
        using var form = new DefinitionFormModel(definition);
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("examples/islamu-event/definition.json")]
    [Arguments("store/islamu-event/releases/5.json")]
    public async Task ManuallySuppliedSyntheticOperatorArtifactPreservesGoldenBytes(string current)
    {
        Definition before = DefinitionCodec.ParseJson(File.ReadAllBytes(
            Path.Combine(ReleaseTestPaths.Root, "store/islamu-event/releases/4.json")));
        Definition after = DefinitionCodec.ParseJson(File.ReadAllBytes(Path.Combine(ReleaseTestPaths.Root, current)));
        using var oldForm = new DefinitionFormModel(before);
        using var newForm = new DefinitionFormModel(after);
        foreach (var form in new[] { oldForm, newForm })
        {
            form.SelectOutput("operator-identity");
            form.SetValue(["operatorIdentity"], JsonSerializer.SerializeToElement(new
            {
                contentDigest = "synthetic-digest",
                revisionHash = "synthetic-revision",
                document = new
                {
                    isOfficialInstance = false,
                    jurisdictionCountryCode = "ZZ",
                    legalName = "Synthetic Operator",
                    operatorId = "synthetic-operator",
                    operatorKindCode = "individual",
                    publicName = "Synthetic",
                    revision = "1",
                    websiteUrl = "https://example.invalid"
                }
            }));
        }
        var oldArtifact = DefinitionArtifactComposer.Compose(before, "operator-identity", oldForm.Snapshot());
        var newArtifact = DefinitionArtifactComposer.Compose(after, "operator-identity", newForm.Snapshot());
        await Assert.That(oldArtifact.Succeeded).IsTrue()
            .Because(string.Join(",", oldArtifact.Diagnostics.Select(diagnostic => diagnostic.Code)));
        await Assert.That(newArtifact.Succeeded).IsTrue()
            .Because(string.Join(",", newArtifact.Diagnostics.Select(diagnostic => diagnostic.Code)));
        await Assert.That(newArtifact.Artifact!.Bytes.Span.SequenceEqual(oldArtifact.Artifact!.Bytes.Span)).IsTrue();
    }

    private static Definition Compile(string path) => DefinitionAuthoringCompiler.Compile(
        DefinitionAuthoringCodec.ParseJson(File.ReadAllBytes(Path.Combine(ReleaseTestPaths.Root, path))));

    private static void SupplyBootstrap(DefinitionFormModel form) =>
        form.SetValue(["identity"], JsonSerializer.SerializeToElement(new
        {
            bootstrapMode = "Interactive",
            operatorIdentity = new
            {
                isOfficialInstance = false,
                jurisdictionCountryCode = "ZZ",
                legalName = "Synthetic Operator",
                operatorId = "synthetic-operator",
                operatorKindCode = "individual",
                publicName = "Synthetic",
                websiteUrl = "https://example.invalid"
            }
        }));
}
