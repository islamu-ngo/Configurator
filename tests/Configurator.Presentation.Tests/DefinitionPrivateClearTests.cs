namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

public sealed class DefinitionPrivateClearTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);

    [Test]
    public async Task ClearPreservesPublicRootsAndClearsProtectedRootsInOneRevision()
    {
        using var form = new DefinitionFormModel(Definition());
        form.Fields[0].Text = "public-title";
        form.Fields[1].Text = "synthetic-token";
        form.Fields[2].Text = "synthetic-password";
        form.Fields[3].Text = """{"name":"public-sibling","password":"nested-secret"}""";
        form.Fields[2].Text = new string('x', 65_537);
        long before = form.InputRevision;
        int revisions = 0;
        form.InputChanged += (_, _) => revisions++;
        bool changed = form.ClearPrivateState();
        SetupAnswerSnapshot snapshot = form.Snapshot();
        await Assert.That(changed).IsTrue();
        await Assert.That(snapshot.InputRevision).IsEqualTo(before + 1);
        await Assert.That(revisions).IsEqualTo(1);
        await Assert.That(snapshot.Values.Count).IsEqualTo(1);
        await Assert.That(snapshot.Values["title"].GetString()).IsEqualTo("public-title");
        await Assert.That(form.Fields.All(field => field.InputDiagnosticCode is null)).IsTrue();
    }

    [Test]
    public async Task NestedMutationNotifiesAfterRevisionAndOutsideFormGate()
    {
        using var form = new DefinitionFormModel(Definition());
        long observedRevision = -1;
        System.ComponentModel.PropertyChangedEventHandler subscriber = (_, args) =>
        {
            if (args.PropertyName != nameof(DefinitionFieldInput.Text)) return;
            observedRevision = Task.Run(form.Snapshot).WaitAsync(SignalTimeout).GetAwaiter().GetResult().InputRevision;
        };
        form.Fields[3].PropertyChanged += subscriber;
        try { form.SetValue(["settings", "name"], JsonSerializer.SerializeToElement("public-sibling")); }
        finally { form.Fields[3].PropertyChanged -= subscriber; }
        await Assert.That(observedRevision).IsEqualTo(1);
    }

    [Test]
    public async Task ClearEmptyProtectedRootsStillFencesPendingImport()
    {
        using var form = new DefinitionFormModel(Definition());
        var read = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportResult> import = form.ImportAsync("config", _ => read.Task);
        form.ClearPrivateState();
        read.SetResult("""{"password":"late-secret"}"""u8.ToArray());
        await Assert.That((await import.WaitAsync(SignalTimeout)).Succeeded).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ImportNotificationCannotRestoreSecretsAfterReentrantClear()
    {
        using var form = new DefinitionFormModel(Definition());
        bool cleared = false;
        form.Fields[1].PropertyChanged += (_, args) =>
        {
            if (cleared || args.PropertyName != nameof(DefinitionFieldInput.Text)) return;
            cleared = true;
            form.ClearPrivateState();
        };
        byte[] source = """{"title":"public-title","token":"synthetic-token","password":"synthetic-password","settings":{"password":"nested-secret"}}"""u8.ToArray();
        await form.ImportAsync("config", _ => Task.FromResult<ReadOnlyMemory<byte>>(source));
        await Assert.That(cleared).IsTrue();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values.ContainsKey("title")).IsTrue();
        await Assert.That(source.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task ClearFencesPendingImportAndStillCompletesWhenSubscriberThrows()
    {
        using var form = new DefinitionFormModel(Definition());
        form.Fields[1].Text = "synthetic-token";
        form.Fields[2].Text = "synthetic-password";
        var read = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportResult> import = form.ImportAsync("config", _ => read.Task);
        int invalidations = 0;
        form.InputChanged += (_, _) => invalidations++;
        System.ComponentModel.PropertyChangedEventHandler subscriber = (_, _) => throw new InvalidOperationException("synthetic-subscriber");
        form.Fields[1].PropertyChanged += subscriber;
        Exception? observed = null;
        try { form.ClearPrivateState(); }
        catch (Exception exception) { observed = exception; }
        finally { form.Fields[1].PropertyChanged -= subscriber; }
        byte[] source = """{"password":"late-secret"}"""u8.ToArray();
        read.SetResult(source);
        DefinitionImportResult result = await import.WaitAsync(SignalTimeout);
        await Assert.That(observed).IsNotNull();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(invalidations).IsEqualTo(1);
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(source.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task FailingInputSubscriberCannotSkipRemainingInvalidationObservers()
    {
        using var form = new DefinitionFormModel(Definition());
        form.Fields[1].Text = "synthetic-token";
        EventHandler failing = (_, _) => throw new InvalidOperationException("synthetic-subscriber");
        int invalidations = 0;
        form.InputChanged += failing;
        form.InputChanged += (_, _) => invalidations++;
        AggregateException? observed = null;
        try { form.ClearPrivateState(); }
        catch (AggregateException exception) { observed = exception; }
        finally { form.InputChanged -= failing; }
        await Assert.That(observed).IsNotNull();
        await Assert.That(invalidations).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    private static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion":1,"familyId":"eba722b7-f132-444b-8c44-21cece008ca7","revision":1,
      "applicationId":"example","applicationVersions":["1"],"publisher":"example","title":"Example",
      "entryPoints":["setup"],"locales":["en"],
      "fields":[
        {"id":"title","type":"string","title":"Title","sensitivity":"public","disclosure":"guided"},
        {"id":"token","type":"string","title":"Token","sensitivity":"sensitive","disclosure":"guided"},
        {"id":"password","type":"string","title":"Password","sensitivity":"secret","disclosure":"guided"},
        {"id":"settings","type":"object","title":"Settings","sensitivity":"public","disclosure":"guided",
          "properties":[
            {"id":"name","type":"string","title":"Name","sensitivity":"public","disclosure":"guided"},
            {"id":"password","type":"string","title":"Password","sensitivity":"secret","disclosure":"guided"}]}],
      "artifacts":[{"id":"config","format":"json","fileName":"config.json","applySemantics":"create","restart":"process",
        "projection":{"op":"object","members":{
          "title":{"op":"copy","field":"title"},"token":{"op":"copy","field":"token"},
          "password":{"op":"copy","field":"password"},"settings":{"op":"copy","field":"settings"}}}}]
    }
    """));
}
