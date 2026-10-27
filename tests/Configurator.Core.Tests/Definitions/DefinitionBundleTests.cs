using System.Reflection;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionBundleTests
{
    [Test]
    public async Task SelectedOutputsShareOneMeasuredValidationAndEachComposeOnce()
    {
        using var activity = new Activity(nameof(SelectedOutputsShareOneMeasuredValidationAndEachComposeOnce));
        activity.Start();
        using var measurement = new PreparationMeasurement(activity);
        using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(Definition(3),
            ["yaml2", "config", "dotenv1"], DefinitionFixture.Answers());

        await Assert.That(bundle.Succeeded).IsTrue();
        await Assert.That(string.Join(',', measurement.Composed)).IsEqualTo("config,dotenv1,yaml2");
        await Assert.That(measurement.Validations).IsEqualTo(1);
    }

    [Test]
    public async Task DisposalClearsCoreOwnedRestrictedBytesAndRejectsLaterReads()
    {
        using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(Definition(3),
            ["config", "dotenv1", "yaml2"], DefinitionFixture.Answers());
        byte[][] owned = bundle.Artifacts.Select(artifact =>
            (byte[])typeof(DefinitionArtifact).GetField("_bytes", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(artifact)!).ToArray();
        await Assert.That(owned.All(bytes => bytes.Any(value => value != 0))).IsTrue();
        await Assert.That(bundle.Artifacts[0].ByteLength).IsEqualTo(17);

        bundle.Dispose();
        bundle.Dispose();

        await Assert.That(owned.All(bytes => bytes.All(value => value == 0))).IsTrue();
        foreach (DefinitionArtifact artifact in bundle.Artifacts)
            await Assert.That(() => artifact.Bytes).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task DeclaredFormatsUseOneSnapshotAndDefinitionOrder()
    {
        SetupDefinition definition = Definition(3);
        using DefinitionBundleResult result = DefinitionBundleComposer.Compose(definition,
            ["yaml2", "config", "dotenv1"], DefinitionFixture.Answers());

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(string.Join(',', result.Artifacts.Select(item => item.ArtifactId)))
            .IsEqualTo("config,dotenv1,yaml2");
        await Assert.That(Encoding.UTF8.GetString(result.Artifacts[0].Bytes.Span)).IsEqualTo("""{"name":"sample"}""");
        await Assert.That(Encoding.UTF8.GetString(result.Artifacts[1].Bytes.Span)).IsEqualTo("NAME=sample\n");
        await Assert.That(Encoding.UTF8.GetString(result.Artifacts[2].Bytes.Span)).IsEqualTo("\"name\": \"sample\"\n");
        string[] digests =
        [
            "976cd2d13306ef7b411425368f511318aed70ea05278b4e7559099f7820eaf1b",
            "a871146e6c5914b69930037c8cf0893b33a209b85d244aa5149c4bccc752e128",
            "5299f07bd93b982a0900910440867f3915f4bb37a08f28096ca3d9d3e73062d0"
        ];
        for (int index = 0; index < result.Artifacts.Count; index++)
        {
            DefinitionArtifact artifact = result.Artifacts[index];
            await Assert.That(artifact.InputRevision).IsEqualTo(7L);
            await Assert.That(artifact.DefinitionDigest).IsEqualTo(definition.Digest);
            await Assert.That(artifact.Digest.Value).IsEqualTo(digests[index]);
            await Assert.That(artifact.Sensitivity).IsEqualTo(SetupArtifactSensitivity.Restricted);
            await Assert.That(artifact.ToString().Contains("sample", StringComparison.Ordinal)).IsFalse();
        }
    }

    [Test]
    public async Task OrderedByteOracleRejectsPermutedContents()
    {
        using DefinitionBundleResult result = DefinitionBundleComposer.Compose(Definition(3),
            ["yaml2", "config", "dotenv1"], DefinitionFixture.Answers());
        byte[] expected = """{"name":"sample"}"""u8.ToArray();
        await Assert.That(result.Artifacts[0].Bytes.Span.SequenceEqual(expected)).IsTrue();
        (expected[0], expected[1]) = (expected[1], expected[0]);
        await Assert.That(result.Artifacts[0].Bytes.Span.SequenceEqual(expected)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationAfterFirstComposedOutputAbandonsTheWholeBundle(bool draft)
    {
        using var cancellation = new CancellationTokenSource();
        using var activity = new Activity(nameof(CancellationAfterFirstComposedOutputAbandonsTheWholeBundle));
        activity.Start();
        using var measurement = new PreparationMeasurement(activity, _ => cancellation.Cancel());
        SetupDefinition definition = Definition(3);

        await Assert.That(() =>
        {
            if (draft)
            {
                using DefinitionDraftBundleResult result = DefinitionDraftArtifactComposer.ComposeBundle(
                    definition, ["config", "dotenv1", "yaml2"], DefinitionFixture.Answers(), cancellation.Token);
            }
            else
            {
                using DefinitionBundleResult result = DefinitionBundleComposer.Compose(
                    definition, ["config", "dotenv1", "yaml2"], DefinitionFixture.Answers(), cancellation.Token);
            }
        }).Throws<OperationCanceledException>();
        await Assert.That(string.Join(',', measurement.Composed)).IsEqualTo("config");
        await Assert.That(measurement.Validations).IsEqualTo(1);
    }

    [Test]
    public async Task InvalidLaterArtifactLeavesNoPublishablePrefix()
    {
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        root["artifacts"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"bad","format":"dotenv","fileName":"bad.env","applySemantics":"create",
             "restart":"none","projection":{"op":"object","members":{"nested":{"op":"object","members":{}}}}}
            """));
        DefinitionBundleResult result = DefinitionBundleComposer.Compose(DefinitionFixture.Parse(root.ToJsonString()),
            ["config", "bad"], DefinitionFixture.Answers());
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Artifacts).IsEmpty();
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("dotenv-shape");
        await Assert.That(result.ToString().Contains("sample", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task AggregateBytesAreBoundedAcrossIndividuallyValidArtifacts()
    {
        SetupDefinition definition = Definition(64);
        string value = new('x', SetupCompositionLimits.DefaultScalarCharacters);
        string answer = JsonSerializer.Serialize(new Dictionary<string, string> { ["service.name"] = value });
        DefinitionBundleResult result = DefinitionBundleComposer.Compose(definition,
            definition.Artifacts.Select(item => item.Id), DefinitionFixture.Answers(answer));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Artifacts).IsEmpty();
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("bundle-limit");
    }

    [Test]
    public async Task OversizedSelectionStopsAtDefinitionArtifactCount()
    {
        DefinitionBundleResult result = DefinitionBundleComposer.Compose(DefinitionFixture.Parse(),
            ["config", "extra"], DefinitionFixture.Answers());
        await Assert.That(result.Artifacts).IsEmpty();
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("artifact-limit");
    }

    [Test]
    public async Task ActiveBundleIncludesOnlyResolvedActiveArtifactsWithoutWeakeningExplicitSelection()
    {
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        root["artifacts"]![0]!["projection"]!["when"] = JsonNode.Parse(
            """{"op":"equals","field":"service.name","value":"first"}""");
        JsonNode second = root["artifacts"]![0]!.DeepClone();
        second["id"] = "second";
        second["fileName"] = "second.json";
        second["projection"]!["when"]!["value"] = "second";
        root["artifacts"]!.AsArray().Add(second);
        SetupDefinition definition = DefinitionFixture.Parse(root.ToJsonString());
        SetupAnswerSnapshot answers = DefinitionFixture.Answers("""{"service.name":"first"}""");

        using DefinitionBundleResult explicitSelection = DefinitionBundleComposer.Compose(definition, ["config", "second"], answers);
        await Assert.That(explicitSelection.Succeeded).IsFalse();
        await Assert.That(explicitSelection.Artifacts).IsEmpty();
        await Assert.That(explicitSelection.Diagnostics.Single().Code).IsEqualTo("artifact-omitted");
        using DefinitionBundleResult active = DefinitionBundleComposer.ComposeActive(definition, answers);
        await Assert.That(active.Succeeded).IsTrue();
        await Assert.That(active.Artifacts.Single().ArtifactId).IsEqualTo("config");
        await Assert.That(Encoding.UTF8.GetString(active.Artifacts.Single().Bytes.Span)).IsEqualTo("""{"name":"first"}""");

        using DefinitionBundleResult inactive = DefinitionBundleComposer.ComposeActive(definition,
            DefinitionFixture.Answers("""{"service.name":"neither"}"""));
        await Assert.That(inactive.Artifacts).IsEmpty();
        await Assert.That(inactive.Diagnostics.Single().Code).IsEqualTo("artifact-empty");
        using DefinitionBundleResult invalid = DefinitionBundleComposer.ComposeActive(definition, DefinitionFixture.Answers("{}"));
        await Assert.That(invalid.Artifacts).IsEmpty();
        await Assert.That(invalid.Diagnostics.Single().Code).IsEqualTo("value-required");
    }

    [Test]
    public async Task ActiveBundleLateFailureClearsEveryEarlierArtifact()
    {
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        root["artifacts"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"bad","format":"dotenv","fileName":"bad.env","applySemantics":"create","restart":"none",
             "projection":{"op":"literal","value":[]}}
            """));
        using DefinitionBundleResult result = DefinitionBundleComposer.ComposeActive(
            DefinitionFixture.Parse(root.ToJsonString()), DefinitionFixture.Answers());
        await Assert.That(result.Artifacts).IsEmpty();
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("dotenv-shape");
    }

    [Test]
    public async Task ActiveBundleRetainsTheAggregateOutputLimit()
    {
        using DefinitionBundleResult result = DefinitionBundleComposer.ComposeActive(Definition(64),
            DefinitionFixture.Answers("{\"service.name\":\"" + new string('x', SetupCompositionLimits.DefaultScalarCharacters) + "\"}"));
        await Assert.That(result.Artifacts).IsEmpty();
        await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("bundle-limit");
    }

    private static SetupDefinition Definition(int count)
    {
        JsonNode root = JsonNode.Parse(DefinitionFixture.Json)!;
        JsonArray artifacts = root["artifacts"]!.AsArray();
        for (int index = 1; index < count; index++)
        {
            JsonNode artifact = artifacts[0]!.DeepClone();
            artifact["id"] = (index % 3) switch { 1 => "dotenv" + index, 2 => "yaml" + index, _ => "config" + index };
            artifact["format"] = count == 64 ? "json" : (index % 3) switch { 1 => "dotenv", 2 => "yaml", _ => "json" };
            artifact["fileName"] = "output" + index + "." + ((index % 3) switch { 1 => "env", 2 => "yaml", _ => "json" });
            if (count != 64 && index % 3 == 1)
                artifact["projection"]!["members"]!["NAME"] = artifact["projection"]!["members"]!["name"]!.DeepClone();
            if (count != 64 && index % 3 == 1)
                artifact["projection"]!["members"]!.AsObject().Remove("name");
            artifacts.Add(artifact);
        }
        if (count == 64) root["fields"]![0]!["maxLength"] = SetupCompositionLimits.DefaultScalarCharacters;
        return DefinitionFixture.Parse(root.ToJsonString());
    }

    internal sealed class PreparationMeasurement : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly Activity _activity;
        private readonly Action<string>? _composed;
        private readonly IDisposable _listeners;
        private readonly List<IDisposable> _subscriptions = [];
        public PreparationMeasurement(Activity activity, Action<string>? composed = null)
        {
            _activity = activity;
            _composed = composed;
            _listeners = DiagnosticListener.AllListeners.Subscribe(this);
        }
        public int Validations { get; private set; }
        public List<string> Composed { get; } = [];
        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "Configurator.Core.Preparation") _subscriptions.Add(value.Subscribe(this));
        }
        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (!ReferenceEquals(Activity.Current, _activity)) return;
            if (value.Key == "ValidationCompleted") Validations++;
            if (value.Key == "ArtifactComposed")
            {
                string id = (string)value.Value!;
                Composed.Add(id);
                _composed?.Invoke(id);
            }
        }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void Dispose()
        {
            _listeners.Dispose();
            foreach (IDisposable subscription in _subscriptions) subscription.Dispose();
        }
    }
}
