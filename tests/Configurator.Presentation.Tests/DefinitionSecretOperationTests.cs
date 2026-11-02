namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionSecretOperationTests
{
    private static readonly string[] OriginalRows = ["synthetic-removed-password", "synthetic-survivor-password"];
    private static readonly string[] ReorderedRows = ["synthetic-survivor-password", "synthetic-removed-password"];
    [Test]
    public async Task RenewedConsentAllowsExplicitGenerationWithoutRestoringClearedSecrets()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        string[] path = [form.Fields.Single().Definition.Id];
        await Assert.That((await form.GenerateSecretAsync(path)).Succeeded).IsTrue();
        form.SetSecretEntryEnabled(false);
        await Assert.That(form.ReadValue(path)).IsNull();
        await Assert.That(form.CanGenerateSecret(path)).IsFalse();
        form.SetSecretEntryEnabled(true);
        await Assert.That(form.ReadValue(path)).IsNull();
        await Assert.That(form.CanGenerateSecret(path)).IsTrue();
        await Assert.That((await form.GenerateSecretAsync(path)).Succeeded).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ObserverFailureCannotStrandWorkOrMisreportACommittedAnswer(bool afterCommit)
    {
        using var form = new DefinitionFormModel(Definition());
        string[] path = [form.Fields.Single().Definition.Id];
        form.ViewChanged += (_, _) =>
        {
            if (afterCommit ? form.SecretOperation.Code == "secret-generated" : form.SecretOperation.IsRunning)
                throw new InvalidOperationException("synthetic-observer-failure");
        };
        DefinitionSecretOperationResult result = await form.GenerateSecretAsync(path);
        await Assert.That(form.SecretOperation.IsRunning).IsFalse();
        await Assert.That(result.Succeeded).IsEqualTo(afterCommit);
        await Assert.That(form.ReadValue(path) is not null).IsEqualTo(afterCommit);
        await Assert.That(form.CanGenerateSecret(path)).IsTrue();
    }

    [Test]
    public async Task GeneratedOutputMustMeetTheWholeNodeBeforeReplacingAnExistingAnswer()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        JsonNode field = source["configuration"]!["properties"]!["password"]!;
        field["format"] = "email-address";
        field["x-configurator"]!["assertFormat"] = true;
        field["x-configurator"]!["secret"]!["constraints"]!["allowedSymbols"] = "";
        source["requiredCapabilities"]!.AsArray().Insert(2, "schema.assertFormat");
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        string[] path = [form.Fields.Single().Definition.Id];
        form.EditValue(path, "synthetic@example.invalid");
        DefinitionSecretOperationResult result = await form.GenerateSecretAsync(path);
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Code).IsEqualTo("secret-generated-value-invalid");
        await Assert.That(form.ReadValue(path)!.Value.GetString()).IsEqualTo("synthetic@example.invalid");
    }

    [Test]
    public async Task ReadOnlyAncestorCannotGrantGenerationAuthority()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["readOnly"] = true;
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        string[] path = [form.Fields.Single().Definition.Id];
        await Assert.That(form.CanGenerateSecret(path)).IsFalse();
        await Assert.That((await form.GenerateSecretAsync(path)).Code).IsEqualTo("secret-operation-unavailable");
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SecondRequestIsRejectedAndCancellationPreservesThePreviousValue()
    {
        using var form = new DefinitionFormModel(Definition());
        string[] path = [form.Fields.Single().Definition.Id];
        form.EditValue(path, "synthetic-previous-password");
        using var cancellation = new CancellationTokenSource();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionSecretOperationResult> pending = form.GenerateSecretAsync(path, cancellationToken: cancellation.Token,
            cooperativeYield: async _ =>
            {
                reached.SetResult();
                await release.Task;
            });
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That((await form.GenerateSecretAsync(path)).Code).IsEqualTo("secret-busy");
        cancellation.Cancel();
        release.SetResult();
        await Assert.That((await pending.WaitAsync(TimeSpan.FromSeconds(10))).Code).IsEqualTo("secret-cancelled");
        await Assert.That(form.ReadValue(path)!.Value.GetString()).IsEqualTo("synthetic-previous-password");
        await Assert.That(form.SecretOperation.Code).IsEqualTo("secret-cancelled");
        await Assert.That(form.SecretOperation.IsRunning).IsFalse();
    }

    [Test]
    public async Task OriginalGeneratedPasswordIsSeparateAndRetiredOnEditAndDismissal()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"]!["password"] = JsonNode.Parse("""
          {"type":"string","x-configurator":{"secret":{"kind":"digest","acquisition":"local","algorithm":"SHA256",
            "encoding":"hex","generation":{"input":"generated","password":{"kind":"password","acquisition":"local",
              "generation":{"length":24}}}}}}
          """);
        source["requiredCapabilities"]!.AsArray().RemoveAt(2);
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        string[] path = [form.Fields.Single().Definition.Id];
        DefinitionSecretOperationResult result = await form.GenerateSecretAsync(path);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.HasGeneratedPassword).IsTrue();
        string original = form.CopyGeneratedPassword()!;
        await Assert.That(original.Length).IsEqualTo(24);
        await Assert.That(form.ReadValue(path)!.Value.GetString()).IsNotEqualTo(original);
        await Assert.That(JsonSerializer.Serialize(form.SecretOperation)).DoesNotContain(original);
        form.DismissSecretResult();
        await Assert.That(form.CopyGeneratedPassword()).IsNull();
        await form.GenerateSecretAsync(path);
        form.EditValue(path, "synthetic-edit");
        await Assert.That(form.CopyGeneratedPassword()).IsNull();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task StructuralListChangesDoNotOverwriteTheShiftedSurvivor(bool reorder)
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        JsonNode item = source["configuration"]!["properties"]!["password"]!.DeepClone();
        source["configuration"]!["properties"]!["password"] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = item,
            ["maxItems"] = 4
        };
        source["requiredCapabilities"]!.AsArray().Insert(2, "schema.array");
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        string[] root = [form.Fields.Single().Definition.Id];
        form.SetValue(root, JsonSerializer.SerializeToElement(OriginalRows));
        string[] path = [root[0], "0"];
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionSecretOperationResult> pending = form.GenerateSecretAsync(path, cooperativeYield: async _ =>
        {
            reached.SetResult();
            await release.Task;
        });
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (reorder)
            form.SetValue(root, JsonSerializer.SerializeToElement(ReorderedRows));
        else await Assert.That(form.RemoveListItem(root, 0)).IsTrue();
        release.SetResult();
        await Assert.That((await pending.WaitAsync(TimeSpan.FromSeconds(10))).Code).IsEqualTo("secret-stale");
        await Assert.That(form.ReadValue(path)!.Value.GetString()).IsEqualTo("synthetic-survivor-password");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MapRenameAndVariantSwitchRetireThePendingEditor(bool variant)
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        JsonNode secret = source["configuration"]!["properties"]!["password"]!.DeepClone();
        JsonObject Branch(string kind) => new()
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("kind", "value"),
            ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
            ["properties"] = new JsonObject
            {
                ["kind"] = new JsonObject
                {
                    ["type"] = "string",
                    ["const"] = kind,
                    ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
                },
                ["value"] = secret.DeepClone()
            }
        };
        source["configuration"]!["properties"]!["password"] = variant
            ? new JsonObject
            {
                ["type"] = "object",
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["oneOf"] = new JsonArray(Branch("first"), Branch("second"))
            }
            : new JsonObject { ["type"] = "object", ["additionalProperties"] = secret };
        source["requiredCapabilities"]!.AsArray().Insert(variant ? 6 : 3, variant ? "schema.variant" : "schema.map");
        if (variant) source["requiredCapabilities"]!.AsArray().Insert(3, "schema.const");
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        string[] root = [form.Fields.Single().Definition.Id];
        form.SetValue(root, JsonSerializer.SerializeToElement(variant
            ? new Dictionary<string, string> { ["kind"] = "first", ["value"] = "synthetic-old-password" }
            : new Dictionary<string, string> { ["synthetic-private-key"] = "synthetic-old-password" }));
        string[] path = [root[0], variant ? "value" : "synthetic-private-key"];
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionSecretOperationResult> pending = form.GenerateSecretAsync(path, cooperativeYield: async _ =>
        {
            reached.SetResult();
            await release.Task;
        });
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(form.SecretOperation.ToString()).DoesNotContain("synthetic-private-key");
        await Assert.That(JsonSerializer.Serialize(form.SecretOperation)).DoesNotContain("synthetic-private-key");
        await Assert.That(JsonSerializer.Serialize(form.SecretOperation)).DoesNotContain(form.SecretOperation.EditorIdentity!);
        if (variant) form.SelectVariant(root, 1);
        else form.SetValue(root, JsonSerializer.SerializeToElement(new Dictionary<string, string>
        { ["synthetic-renamed-key"] = "synthetic-old-password" }));
        release.SetResult();
        await Assert.That((await pending.WaitAsync(TimeSpan.FromSeconds(10))).Code).IsEqualTo("secret-stale");
        await Assert.That(form.ReadValue(path)).IsNull();
    }

    [Test]
    public async Task ConsentRevocationRejectsPendingGeneration()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        string[] path = [form.Fields.Single().Definition.Id];
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionSecretOperationResult> pending = form.GenerateSecretAsync(path, cooperativeYield: async _ =>
        {
            reached.SetResult();
            await release.Task;
        });
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(form.CanGenerateSecret(path)).IsFalse();
        form.SetSecretEntryEnabled(false);
        release.SetResult();
        DefinitionSecretOperationResult result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Code).IsEqualTo("secret-stale");
        await Assert.That(form.ReadValue(path)).IsNull();
        await Assert.That(form.CopyGeneratedPassword()).IsNull();
    }

    [Test]
    public async Task EditingOrDisposalRejectsPendingGeneration()
    {
        foreach (bool dispose in new[] { false, true })
        {
            using var form = new DefinitionFormModel(Definition());
            string[] path = [form.Fields.Single().Definition.Id];
            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<DefinitionSecretOperationResult> pending = form.GenerateSecretAsync(path, cooperativeYield: async _ =>
            {
                reached.SetResult();
                await release.Task;
            });
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (dispose) form.Dispose();
            else form.EditValue(path, "synthetic-explicit-value");
            using var replacement = new DefinitionFormModel(Definition());
            release.SetResult();
            await Assert.That((await pending.WaitAsync(TimeSpan.FromSeconds(10))).Code).IsEqualTo("secret-stale");
            if (!dispose) await Assert.That(form.ReadValue(path)!.Value.GetString()).IsEqualTo("synthetic-explicit-value");
            await Assert.That(form.CopyGeneratedPassword()).IsNull();
            await Assert.That(replacement.Snapshot().Values.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task GenerationAppliesValidatedValueAndRecordsGeneratedOrigin()
    {
        using var form = new DefinitionFormModel(Definition());
        string[] path = [form.Fields.Single().Definition.Id];
        DefinitionSecretOperationResult result = await form.GenerateSecretAsync(path);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(form.Validate().IsReady).IsTrue();
        await Assert.That(form.AnswerOrigin(path)).IsEqualTo(DefinitionAnswerOrigin.Generated);
        await Assert.That(form.ReadValue(path)!.Value.GetString()!.Length).IsEqualTo(24);
        await Assert.That(result.ToString()).DoesNotContain(form.ReadValue(path)!.Value.GetString()!);
    }

    internal static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion":"0.0.1","profile":"bounded-v0.0.1","familyId":"eba722b7-f132-444b-8c44-21cece008ca7","revision":1,
      "project":{"id":"example","name":"Example","publisher":"example"},"applicationVersions":["1"],
      "entryPoints":["setup"],"locales":["ar","en"],
      "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}},
      "requiredCapabilities":["projection.copy","projection.object","schema.bounds","schema.object","schema.secret","schema.string"],
      "configuration":{"type":"object","additionalProperties":false,"required":["password"],"x-configurator":{"sensitivity":"public"},"properties":{
        "password":{"type":"string","minLength":16,"maxLength":32,"x-configurator":{"secret":{
          "kind":"password","acquisition":"local","constraints":{"characterSet":"ascii-printable","allowWhitespace":false},
          "generation":{"length":24}}}}}},
      "artifacts":[{"id":"config","format":"json","fileName":"config.json","targetUpdateMode":"create","restartRequirement":"process",
        "projection":{"op":"object","members":{"password":{"op":"copy","reference":{"scope":"root","path":"/password"}}}}}]
    }
    """));
}

internal static class DefinitionSecretTestValues
{
    internal static JsonElement? ReadValue(this DefinitionFormModel form, IReadOnlyList<string> path)
    {
        if (!form.Snapshot().Values.TryGetValue(path[0], out JsonElement value)) return null;
        foreach (string segment in path.Skip(1))
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (!value.TryGetProperty(segment, out value)) return null;
            }
            else if (value.ValueKind == JsonValueKind.Array) value = value[int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture)];
            else return null;
        }
        return value;
    }
}
