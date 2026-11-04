namespace Configurator.Browser.Tests;

using System.Collections;
using System.IO.Compression;
using System.Text;
using Bunit;
using Configurator.Browser.Services;

public sealed class BrowserArtifactBundleExportTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationBetweenAssemblyEntriesRetiresPriorOutputAndAdoptsNoPartialDelivery(bool archive)
    {
        await using var context = BrowserFixture.Context();
        await using var export = new BrowserArtifactBundleExport(context.JSInterop.JSRuntime);
        await Assert.That(export.SetOutput([("old.env", "synthetic-prior"u8.ToArray())], archive)).IsTrue();
        using var cancellation = new CancellationTokenSource();
        // Clipboard preflights both source bytes and labelled size; ZIP preflights source bytes only.
        var entries = new CancellingEntries(cancellation, cancelOnPass: archive ? 2 : 3);
        await Assert.That(() => export.SetOutput(entries, archive, cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(export.HasOutput).IsFalse();
        await Assert.That(await export.CopyAsync()).IsFalse();
        await Assert.That(context.JSInterop.Invocations.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DeliveryPreservesFileBoundariesAndExactContents(bool archive)
    {
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        module.Setup<bool>(archive ? "download" : "copy", _ => true).SetResult(true);
        await using var export = new BrowserArtifactBundleExport(context.JSInterop.JSRuntime);
        await Assert.That(export.SetOutput([
            ("worker.env", "TOKEN=synthetic-worker\n"u8.ToArray()),
            ("dashboard.env", "TOKEN=synthetic-dashboard\n"u8.ToArray())], archive)).IsTrue();
        await Assert.That(archive ? await export.DownloadAsync() : await export.CopyAsync()).IsTrue();
        byte[] bytes = (byte[])module.Invocations.Single(item => item.Identifier == "open").Arguments[2]!;
        if (archive)
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            await Assert.That(string.Join('|', zip.Entries.Select(entry => entry.FullName))).IsEqualTo("worker.env|dashboard.env");
            await Assert.That(zip.Entries.All(entry => entry.LastWriteTime.DateTime == new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                && entry.ExternalAttributes == 0)).IsTrue();
            using var worker = new StreamReader(zip.GetEntry("worker.env")!.Open());
            using var dashboard = new StreamReader(zip.GetEntry("dashboard.env")!.Open());
            await Assert.That(await worker.ReadToEndAsync()).IsEqualTo("TOKEN=synthetic-worker\n");
            await Assert.That(await dashboard.ReadToEndAsync()).IsEqualTo("TOKEN=synthetic-dashboard\n");
        }
        else await Assert.That(Encoding.UTF8.GetString(bytes)).IsEqualTo(
            "--- worker.env ---\nTOKEN=synthetic-worker\n\n--- dashboard.env ---\nTOKEN=synthetic-dashboard\n\n");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RefusalRetiresOldOutputAndNeverAdoptsPartialBytes(bool archive)
    {
        await using var context = BrowserFixture.Context();
        await using var export = new BrowserArtifactBundleExport(context.JSInterop.JSRuntime);
        await Assert.That(export.SetOutput([("one.env", "synthetic-old"u8.ToArray())], archive)).IsTrue();
        await Assert.That(export.SetOutput([("one.env", new byte[4_194_305])], archive)).IsFalse();
        await Assert.That(export.HasOutput).IsFalse();
        await Assert.That(await export.CopyAsync()).IsFalse();
        await Assert.That(context.JSInterop.Invocations.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("../secret.env")]
    [Arguments("CON.env")]
    [Arguments("bad\\name.env")]
    [Arguments("bad:name.env")]
    [Arguments("bad.env.")]
    public async Task UnsafePortableNamesAndDuplicateNamesAreRejected(string name)
    {
        await using var context = BrowserFixture.Context();
        await using var export = new BrowserArtifactBundleExport(context.JSInterop.JSRuntime);
        await Assert.That(export.SetOutput([(name, "x"u8.ToArray())], true)).IsFalse();
        await Assert.That(export.SetOutput([("same.env", "x"u8.ToArray()), ("SAME.env", "y"u8.ToArray())], true)).IsFalse();
    }

    [Test]
    public async Task ClipboardCountsHeadersAndArchiveCountsEntries()
    {
        await using var context = BrowserFixture.Context();
        await using var export = new BrowserArtifactBundleExport(context.JSInterop.JSRuntime);
        await Assert.That(export.SetOutput([("one.env", new byte[4_194_304])], false)).IsTrue();
        await Assert.That(export.SetOutput([("one.env", new byte[4_194_304]), ("two.env", ReadOnlyMemory<byte>.Empty)], false)).IsFalse();
        await Assert.That(export.SetOutput([("one.env", new byte[4_194_268]), ("two.env", ReadOnlyMemory<byte>.Empty)], false)).IsTrue();
        await Assert.That(export.SetOutput([("one.env", new byte[4_194_269]), ("two.env", ReadOnlyMemory<byte>.Empty)], false)).IsFalse();
        var entries = Enumerable.Range(0, 64).Select(index => ($"file{index}.env", (ReadOnlyMemory<byte>)"x"u8.ToArray())).ToArray();
        await Assert.That(export.SetOutput(entries, true)).IsTrue();
        await Assert.That(export.SetOutput(entries.Append(("last.env", ReadOnlyMemory<byte>.Empty)).ToArray(), true)).IsFalse();
    }

    [Test]
    public async Task RevocationDuringModuleLoadingCannotOpenOldAggregate()
    {
        var javascript = new BrowserExportLifecycleTests.GatedJavascript();
        await using var export = new BrowserArtifactBundleExport(javascript);
        await Assert.That(export.SetOutput([("worker.env", "synthetic-api-admin"u8.ToArray())], false)).IsTrue();
        Task<bool> pending = export.CopyAsync();
        await javascript.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await export.InvalidateAsync();
        await using var module = new BrowserExportLifecycleTests.RecordingModule();
        javascript.Release.TrySetResult(module);
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(module.PrivateOpeningCount).IsEqualTo(0);
    }

    [Test]
    public async Task ZipOverheadCannotExceedDeliveryBudgetAndCancelledAssemblyHasNoOutput()
    {
        await using var context = BrowserFixture.Context();
        await using var export = new BrowserArtifactBundleExport(context.JSInterop.JSRuntime);
        byte[] incompressible = new byte[4_194_304];
        new Random(5821).NextBytes(incompressible);
        await Assert.That(export.SetOutput([("random.env", incompressible)], true)).IsFalse();
        await Assert.That(export.HasOutput).IsFalse();
        await Assert.That(export.SetOutput([("one.env", "synthetic-old"u8.ToArray())], true)).IsTrue();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(() => export.SetOutput([("one.env", "synthetic-new"u8.ToArray())], true, cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(export.HasOutput).IsFalse();
    }

    [Test]
    public async Task RepeatedExportCannotReleaseRetiredBytesOrRetireTheLaterValidOutput()
    {
        var javascript = new BrowserExportLifecycleTests.GatedJavascript();
        await using var export = new BrowserArtifactBundleExport(javascript);
        await Assert.That(export.SetOutput([("api.env", "synthetic-api-admin"u8.ToArray())], false)).IsTrue();
        Task<bool> oldCopy = export.CopyAsync();
        await javascript.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await export.InvalidateAsync();
        await Assert.That(export.SetOutput([("blazor.env", "synthetic-blazor-only"u8.ToArray())], false)).IsTrue();
        Task<bool> newCopy = export.CopyAsync();
        await using var module = new BrowserExportLifecycleTests.RecordingModule();
        javascript.Release.TrySetResult(module);
        await Assert.That(await oldCopy.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(await newCopy.WaitAsync(TimeSpan.FromSeconds(5))).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(module.OpeningBytes!)).IsEqualTo("synthetic-blazor-only");
        await Assert.That(export.HasOutput).IsTrue();
    }

    private sealed class CancellingEntries(CancellationTokenSource cancellation, int cancelOnPass)
        : IReadOnlyList<(string FileName, ReadOnlyMemory<byte> Bytes)>
    {
        private readonly (string FileName, ReadOnlyMemory<byte> Bytes)[] _entries =
            [("first.env", "synthetic-first"u8.ToArray()), ("second.env", "synthetic-second"u8.ToArray())];
        private int _passes;
        public int Count => _entries.Length;
        public (string FileName, ReadOnlyMemory<byte> Bytes) this[int index] => _entries[index];
        public IEnumerator<(string FileName, ReadOnlyMemory<byte> Bytes)> GetEnumerator()
        {
            int pass = ++_passes;
            yield return _entries[0];
            if (pass == cancelOnPass) cancellation.Cancel();
            yield return _entries[1];
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
