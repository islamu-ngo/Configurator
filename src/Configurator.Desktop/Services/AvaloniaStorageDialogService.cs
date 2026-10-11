namespace Configurator.Desktop.Services;

using Avalonia.Platform.Storage;
using Configurator.Desktop.ViewModels;

public sealed class AvaloniaStorageDialogService(Func<IStorageProvider> provider) : IStorageDialogService
{
    public async Task<Uri?> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IStorageProvider storageProvider = provider();
        IReadOnlyList<IStorageItem> items;
        switch (kind)
        {
            case DesktopPathSelectionKind.OutputDirectory:
                if (!storageProvider.CanPickFolder) throw new NotSupportedException("selection-unavailable");
                items = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Choose existing protected output directory",
                    AllowMultiple = false
                });
                break;
            case DesktopPathSelectionKind.Definition:
            case DesktopPathSelectionKind.Import:
                if (!storageProvider.CanOpen) throw new NotSupportedException("selection-unavailable");
                items = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = kind == DesktopPathSelectionKind.Definition
                        ? "Choose local definition" : "Choose existing configuration",
                    AllowMultiple = false,
                    FileTypeFilter = kind == DesktopPathSelectionKind.Definition
                        ? [new FilePickerFileType("Setup definitions") { Patterns = ["*.json", "*.yaml", "*.yml"] }]
                        : [new FilePickerFileType("All files") { Patterns = ["*"] }]
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
        try
        {
            // Avalonia pickers have no cancellation token. Never accept their late result.
            cancellationToken.ThrowIfCancellationRequested();
            if (items.Count != 1) return null;
            Uri location = items[0].Path;
            bool local = TryGetLocalPath(location, out _);
            cancellationToken.ThrowIfCancellationRequested();
            if (!local) throw new InvalidOperationException("selection-nonlocal");
            return location;
        }
        finally { foreach (IStorageItem item in items) item.Dispose(); }
    }

    /// <summary>
    /// Converts only host-free, fully qualified local file locations. This lexical
    /// boundary does not establish filesystem access or protected-write capability.
    /// </summary>
    public static bool TryGetLocalPath(Uri location, out string path)
    {
        path = string.Empty;
        if (!location.IsAbsoluteUri || !location.IsFile || location.IsUnc ||
            location.Host.Length > 0 || location.UserInfo.Length > 0 ||
            location.Query.Length > 0 || location.Fragment.Length > 0) return false;
        string localPath = location.LocalPath;
        if (!Path.IsPathFullyQualified(localPath) ||
            localPath.StartsWith("//", StringComparison.Ordinal) ||
            localPath.StartsWith(@"\\", StringComparison.Ordinal) ||
            localPath.StartsWith(@"/\", StringComparison.Ordinal) ||
            localPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
        path = localPath;
        return true;
    }
}
