namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionSuggestionCustodyTests
{
    [Test]
    public async Task ReviewCannotCrossFormsOrSurviveDisposal()
    {
        using var form = new DefinitionFormModel(DefinitionTransitionTransferTests.TransferDefinition());
        using var replacement = new DefinitionFormModel(DefinitionTransitionTransferTests.TransferDefinition());
        form.EditValue(["source"], "Retained");
        var review = form.ReviewOutputTransition("identity");
        int candidate = review.Candidates.Single().Id;
        await Assert.That(replacement.ApplyTransfer(review, [candidate]).Succeeded).IsFalse();
        await Assert.That(replacement.ReadTransferPreview(review, candidate)).IsNull();
        form.Dispose();
        await Assert.That(form.ApplyTransfer(review, [candidate]).Succeeded).IsFalse();
        await Assert.That(form.ReadTransferPreview(review, candidate)).IsNull();
    }

    [Test]
    public async Task AncestorSensitivityMasksPreviewsAndDescriptorsContainNoAnswerValues()
    {
        var runtime = JsonNode.Parse(DefinitionTransitionTransferTests.TransferDefinition().CanonicalBytes.Span)!.AsObject();
        var properties = runtime["configuration"]!["properties"]!.AsObject();
        JsonNode source = properties["source"]!.DeepClone();
        properties.Remove("source");
        properties["group"] = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["x-configurator"] = new JsonObject { ["sensitivity"] = "sensitive" },
            ["properties"] = new JsonObject { ["source"] = source }
        };
        properties["target"]!["x-configurator"]!["sensitivity"] = "sensitive";
        properties["target"]!["x-configurator"]!["suggestedFrom"]!["pointer"] = "/group/source";
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString())));
        form.SetValue(["group", "source"], JsonSerializer.SerializeToElement("synthetic-inherited-sentinel"));
        var review = form.ReviewOutputTransition("identity");
        await Assert.That(review.Candidates.Single().RequiresMasking).IsTrue();
        await Assert.That(form.ReadTransferPreview(review, review.Candidates[0].Id)).IsNull();
        await Assert.That(JsonSerializer.Serialize(review).Contains("synthetic-inherited-sentinel", StringComparison.Ordinal)).IsFalse();
        await Assert.That(form.ApplyTransfer(review, [review.Candidates[0].Id]).Succeeded).IsTrue();
        await Assert.That(form.ReadValue(["target"])?.GetString()).IsEqualTo("synthetic-inherited-sentinel");
    }

    [Test]
    public async Task SecretReviewStaysMaskedAndRevocationRetiresHeldAuthority()
    {
        using var form = new DefinitionFormModel(DefinitionTransitionTransferTests.TransferDefinition(secret: true),
            DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.EditValue(["source"], "synthetic-transfer-sentinel");
        var review = form.ReviewOutputTransition("identity");
        var candidate = review.Candidates.Single();
        await Assert.That(candidate.RequiresMasking).IsTrue();
        await Assert.That(form.ReadTransferPreview(review, candidate.Id)).IsNull();
        form.SetSecretEntryEnabled(false);
        await Assert.That(form.ReadTransferPreview(review, candidate.Id)).IsNull();
        await Assert.That(form.ApplyTransfer(review, [candidate.Id]).Succeeded).IsFalse();
        await Assert.That(form.ReadValue(["source"])).IsNull();
        await Assert.That(form.ReadValue(["target"])).IsNull();
        while (form.Undo())
            await Assert.That(form.ReadValue(["source"])).IsNull();
    }

    [Test]
    public async Task PrivateClearRetiresReviewsEvenWithoutAnyProtectedAnswer()
    {
        using var form = new DefinitionFormModel(DefinitionTransitionTransferTests.TransferDefinition());
        form.EditValue(["source"], "Public");
        var review = form.ReviewOutputTransition("identity");
        long revision = form.InputRevision;
        form.ClearPrivateState();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.ApplyTransfer(review, [review.Candidates.Single().Id]).Succeeded).IsFalse();
        await Assert.That(form.ReadTransferPreview(review, review.Candidates.Single().Id)).IsNull();
    }

    [Test]
    public async Task BrowserWithoutSecretConsentNeverOffersSecretCandidates()
    {
        using var form = new DefinitionFormModel(DefinitionTransitionTransferTests.TransferDefinition(secret: true),
            DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.ReviewOutputTransition("identity").Candidates.Count).IsEqualTo(0);
    }
}
