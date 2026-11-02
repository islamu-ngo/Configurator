namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionSecretValidationTests
{
    [Test]
    public async Task InactiveInvalidSecretRetainsItsAnswerWithoutActiveErrors()
    {
        JsonObject source = JsonNode.Parse(DefinitionSecretOperationTests.Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"]!["provider"] = JsonNode.Parse("""
          {"type":"string","x-configurator":{"sensitivity":"public"}}
          """);
        source["configuration"]!["properties"]!["password"]!["x-configurator"]!["applicableWhen"] = JsonNode.Parse("""
          {"op":"equals","reference":{"scope":"root","path":"/provider"},"value":"active"}
          """);
        source["requiredCapabilities"]!.AsArray().Insert(0, "predicate.root");
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        string[] path = [form.Fields.Single(field => field.Definition.Name == "password").Definition.Id];
        string[] provider = [form.Fields.Single(field => field.Definition.Name == "provider").Definition.Id];
        form.EditValue(provider, "active");
        form.EditValue(path, "short");
        await Assert.That(form.GetSecretValidation(path).Failures.Count).IsGreaterThan(0);
        form.EditValue(provider, "inactive");
        DefinitionSecretValidation facts = form.GetSecretValidation(path);
        await Assert.That(facts.IsActive).IsFalse();
        await Assert.That(facts.Failures).IsEmpty();
        await Assert.That(form.Snapshot().Values[path[0]].GetString()).IsEqualTo("short");
    }

    [Test]
    public async Task UntouchedDeferredAndCorrectedFieldsHaveDistinctFeedback()
    {
        using var form = new DefinitionFormModel(DefinitionSecretOperationTests.Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        string[] path = [form.Fields.Single().Definition.Id];
        DefinitionSecretValidation untouched = form.GetSecretValidation(path);
        await Assert.That(untouched.IsDeferred).IsTrue();
        await Assert.That(untouched.Failures).IsEmpty();
        await Assert.That(untouched.Requirements.Count).IsGreaterThan(0);
        form.SetSecretEntryEnabled(true);
        await Assert.That(form.GetSecretValidation(path).ShowErrors).IsFalse();
        form.EditValue(path, "short");
        DefinitionSecretValidation invalid = form.GetSecretValidation(path);
        await Assert.That(invalid.ShowErrors).IsTrue();
        await Assert.That(invalid.Failures.Count).IsGreaterThan(0);
        await Assert.That(invalid.Failures.Any(item => item.Message.Contains("short", StringComparison.Ordinal))).IsFalse();
        form.EditValue(path, "synthetic-valid-password");
        await Assert.That(form.GetSecretValidation(path).Failures).IsEmpty();
        long revision = form.InputRevision;
        form.SelectLocale("ar");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.ReadValue(path)!.Value.GetString()).IsEqualTo("synthetic-valid-password");
    }
}
