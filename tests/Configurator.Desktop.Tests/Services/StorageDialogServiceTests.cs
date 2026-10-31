namespace Configurator.Desktop.Tests.Services;

using System.Reflection;
using Avalonia.Platform.Storage;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;

public sealed class StorageDialogServiceTests
{
    [Test]
    public async Task AmbiguousProviderSelectionReleasesEveryItemDespiteOneDisposalFailure()
    {
        bool secondDisposed = false;
        IStorageFile first = StorageProxy.Create<IStorageFile>((method, _) => method == "Dispose"
            ? throw new IOException("synthetic-disposal-failure") : throw new InvalidOperationException("unexpected-provider-read"));
        IStorageFile second = StorageProxy.Create<IStorageFile>((method, _) =>
        {
            if (method != "Dispose") throw new InvalidOperationException("unexpected-provider-read");
            secondDisposed = true;
            return null;
        });
        IStorageProvider provider = StorageProxy.Create<IStorageProvider>((method, _) => method switch
        {
            "get_CanOpen" => true,
            "OpenFilePickerAsync" => Task.FromResult<IReadOnlyList<IStorageFile>>([first, second]),
            _ => throw new NotSupportedException()
        });
        IOException? failure = null;
        try { using var input = await new AvaloniaStorageDialogService(() => provider).ReadImportAsync(); }
        catch (IOException exception) { failure = exception; }
        await Assert.That(failure).IsNotNull();
        await Assert.That(secondDisposed).IsTrue();
    }

    [Test]
    public async Task ProviderItemDisposalFailureClearsInputBeforeReturningSafeFailureToCaller()
    {
        using var stream = new BorrowedInputStream();
        bool streamClosedBeforeItem = false;
        IStorageFile file = StorageProxy.Create<IStorageFile>((method, _) => method switch
        {
            "OpenReadAsync" => Task.FromResult<Stream>(stream),
            "Dispose" => DisposeItem(),
            _ => throw new InvalidOperationException("provider-path-must-not-be-read")
        });
        IStorageProvider provider = StorageProxy.Create<IStorageProvider>((method, _) => method switch
        {
            "get_CanOpen" => true,
            "OpenFilePickerAsync" => Task.FromResult<IReadOnlyList<IStorageFile>>([file]),
            _ => throw new NotSupportedException()
        });
        IOException? failure = null;
        try { using var input = await new AvaloniaStorageDialogService(() => provider).ReadImportAsync(); }
        catch (IOException exception) { failure = exception; }
        await Assert.That(failure).IsNotNull();
        await Assert.That(streamClosedBeforeItem).IsTrue();
        await Assert.That(stream.Borrowed.Length > 0).IsTrue();
        await Assert.That(stream.Borrowed.ToArray().All(value => value == 0)).IsTrue();

        object? DisposeItem()
        {
            streamClosedBeforeItem = !stream.CanRead;
            throw new IOException("synthetic-provider-failure");
        }
    }

    [Test]
    [Arguments(@"\\server\share\synthetic-private.json")]
    [Arguments("file://server/share/synthetic-private.json")]
    [Arguments("file://localhost/tmp/synthetic-private.json")]
    [Arguments("file://127.0.0.1/tmp/synthetic-private.json")]
    [Arguments("file://[::1]/tmp/synthetic-private.json")]
    [Arguments("https://example.invalid/synthetic-private.json")]
    [Arguments("content://provider/synthetic-private.json")]
    [Arguments("file:///tmp/synthetic-private.json?answer=sentinel")]
    [Arguments("file:///tmp/synthetic-private.json#sentinel")]
    [Arguments("https://user:sentinel@example.invalid/synthetic-private.json")]
    [Arguments("synthetic-private.json")]
    [Arguments("/tmp/synthetic-private.json")]
    [Arguments("file:synthetic-private.json")]
    [Arguments("file:////server/share/synthetic-private.json")]
    [Arguments("file:///%2Fserver/share/synthetic-private.json")]
    [Arguments("file:///%5C%5Cserver/share/synthetic-private.json")]
    public async Task UnsafeLocationsCannotCrossLocalSelectionBoundary(string value)
    {
        Uri location = value.StartsWith("/tmp/", StringComparison.Ordinal)
            ? new Uri(value, UriKind.Relative)
            : new Uri(value, UriKind.RelativeOrAbsolute);
        bool accepted = AvaloniaStorageDialogService.TryGetLocalPath(location, out string path);
        await Assert.That(accepted).IsFalse();
        await Assert.That(path).IsEqualTo(string.Empty);
        var item = new StorageItem(location);
        var provider = new StorageProvider { Items = [item] };
        var service = new AvaloniaStorageDialogService(() => provider.Proxy);
        InvalidOperationException? rejected = null;
        try { await service.SelectAsync(DesktopPathSelectionKind.Definition); }
        catch (InvalidOperationException exception) { rejected = exception; }
        await Assert.That(rejected).IsNotNull();
        await Assert.That(rejected!.Message).IsEqualTo("selection-nonlocal");
        await Assert.That(rejected.InnerException).IsNull();
        await Assert.That(item.Disposed).IsTrue();
    }

    [Test]
    [Arguments(DesktopPathSelectionKind.Definition)]
    [Arguments(DesktopPathSelectionKind.Import)]
    [Arguments(DesktopPathSelectionKind.OutputDirectory)]
    public async Task SingleLocalSelectionUsesCorrectPickerAndReleasesItem(DesktopPathSelectionKind kind)
    {
        var location = new Uri(Path.Combine(Path.GetTempPath(), "synthetic selection.json"));
        var item = new StorageItem(location);
        var provider = new StorageProvider { Items = [item] };
        var service = new AvaloniaStorageDialogService(() => provider.Proxy);
        await Assert.That(await service.SelectAsync(kind)).IsEqualTo(location);
        await Assert.That(AvaloniaStorageDialogService.TryGetLocalPath(location, out string path)).IsTrue();
        await Assert.That(path).IsEqualTo(Path.Combine(Path.GetTempPath(), "synthetic selection.json"));
        await Assert.That(item.Disposed).IsTrue();
        if (kind == DesktopPathSelectionKind.OutputDirectory)
        {
            await Assert.That(provider.FolderOptions).IsNotNull();
            await Assert.That(provider.FileOptions).IsNull();
            await Assert.That(provider.FolderOptions!.AllowMultiple).IsFalse();
        }
        else
        {
            await Assert.That(provider.FileOptions).IsNotNull();
            await Assert.That(provider.FolderOptions).IsNull();
            await Assert.That(provider.FileOptions!.AllowMultiple).IsFalse();
            await Assert.That(provider.FileOptions.FileTypeFilter).IsNotNull();
            string[] patterns = provider.FileOptions.FileTypeFilter!
                .SelectMany(filter => filter.Patterns ?? []).ToArray();
            if (kind == DesktopPathSelectionKind.Definition)
                await Assert.That(string.Join(",", patterns)).IsEqualTo("*.json,*.yaml,*.yml");
            else await Assert.That(patterns.Contains("*")).IsTrue();
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(2)]
    public async Task DismissedOrAmbiguousSelectionReturnsNothingAndReleasesItems(int count)
    {
        StorageItem[] items = Enumerable.Range(0, count)
            .Select(index => new StorageItem(new Uri(Path.Combine(Path.GetTempPath(), $"selection-{index}.json"))))
            .ToArray();
        var provider = new StorageProvider { Items = items };
        var service = new AvaloniaStorageDialogService(() => provider.Proxy);
        await Assert.That(await service.SelectAsync(DesktopPathSelectionKind.Import)).IsNull();
        await Assert.That(items.All(item => item.Disposed)).IsTrue();
    }

    [Test]
    [Arguments(DesktopPathSelectionKind.Definition)]
    [Arguments(DesktopPathSelectionKind.Import)]
    [Arguments(DesktopPathSelectionKind.OutputDirectory)]
    public async Task CancellationDiscardsLateResultsAndReleasesItems(DesktopPathSelectionKind kind)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<StorageItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new StorageProvider { Completion = completion };
        var service = new AvaloniaStorageDialogService(() => provider.Proxy);
        using var cancellation = new CancellationTokenSource();
        Task<Uri?> pending = service.SelectAsync(kind, cancellation.Token);
        await provider.Opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        var item = new StorageItem(new Uri(Path.Combine(Path.GetTempPath(), "late.json")));
        completion.SetResult([item]);
        OperationCanceledException? rejected = null;
        try { await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (OperationCanceledException exception) { rejected = exception; }
        await Assert.That(rejected).IsNotNull();
        await Assert.That(rejected!.CancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(item.Disposed).IsTrue();
    }

    [Test]
    public async Task PreCancelledSelectionNeverOpensPicker()
    {
        var provider = new StorageProvider();
        bool resolved = false;
        var service = new AvaloniaStorageDialogService(() => { resolved = true; return provider.Proxy; });
        await Assert.That(resolved).IsFalse();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        OperationCanceledException? rejected = null;
        try { await service.SelectAsync(DesktopPathSelectionKind.Import, cancellation.Token); }
        catch (OperationCanceledException exception) { rejected = exception; }
        await Assert.That(rejected).IsNotNull();
        await Assert.That(resolved).IsFalse();
        await Assert.That(provider.Opened.Task.IsCompleted).IsFalse();
    }

    [Test]
    [Arguments(DesktopPathSelectionKind.Definition)]
    [Arguments(DesktopPathSelectionKind.Import)]
    [Arguments(DesktopPathSelectionKind.OutputDirectory)]
    public async Task UnsupportedPickerFailsWithoutOpeningDialog(DesktopPathSelectionKind kind)
    {
        var provider = new StorageProvider { CanOpen = false, CanPickFolder = false };
        NotSupportedException? rejected = null;
        try { await new AvaloniaStorageDialogService(() => provider.Proxy).SelectAsync(kind); }
        catch (NotSupportedException exception) { rejected = exception; }
        await Assert.That(rejected).IsNotNull();
        await Assert.That(rejected!.Message).IsEqualTo("selection-unavailable");
        await Assert.That(provider.Opened.Task.IsCompleted).IsFalse();
    }

    private sealed class StorageProvider
    {
        public IStorageProvider Proxy => StorageProxy.Create<IStorageProvider>((method, arguments) => method switch
        {
            "get_CanOpen" => CanOpen,
            "get_CanPickFolder" => CanPickFolder,
            "OpenFilePickerAsync" => OpenFilePickerAsync((FilePickerOpenOptions)arguments![0]!),
            "OpenFolderPickerAsync" => OpenFolderPickerAsync((FolderPickerOpenOptions)arguments![0]!),
            _ => throw new NotSupportedException()
        });
        public bool CanOpen { get; init; } = true;
        public bool CanPickFolder { get; init; } = true;
        public bool CanSave => false;
        public IReadOnlyList<StorageItem> Items { get; init; } = [];
        public TaskCompletionSource<IReadOnlyList<StorageItem>>? Completion { get; init; }
        public TaskCompletionSource Opened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public FilePickerOpenOptions? FileOptions { get; private set; }
        public FolderPickerOpenOptions? FolderOptions { get; private set; }
        public async Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options)
        {
            FileOptions = options;
            Opened.TrySetResult();
            return (Completion is null ? Items : await Completion.Task).Select(item => item.File).ToArray();
        }
        public async Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options)
        {
            FolderOptions = options;
            Opened.TrySetResult();
            return (Completion is null ? Items : await Completion.Task).Select(item => item.Folder).ToArray();
        }
    }

    private sealed class StorageItem(Uri path)
    {
        public IStorageFile File => StorageProxy.Create<IStorageFile>(Invoke);
        public IStorageFolder Folder => StorageProxy.Create<IStorageFolder>(Invoke);
        public bool Disposed { get; private set; }
        private object? Invoke(string method, object?[]? arguments)
        {
            if (method == "get_Path") return path;
            if (method == "Dispose") { Disposed = true; return null; }
            throw new NotSupportedException();
        }
    }

    public class StorageProxy : DispatchProxy
    {
        private Func<string, object?[]?, object?> _invoke = null!;

        public static T Create<T>(Func<string, object?[]?, object?> invoke) where T : class
        {
            T proxy = Create<T, StorageProxy>();
            ((StorageProxy)(object)proxy)._invoke = invoke;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            _invoke(targetMethod!.Name, args);
    }

    private sealed class BorrowedInputStream() : MemoryStream("""{"name":"synthetic-private-input"}"""u8.ToArray())
    {
        internal Memory<byte> Borrowed { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position == 0) Borrowed = buffer;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
