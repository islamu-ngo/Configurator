namespace Configurator.Desktop.Services;

using Configurator.Artifacts;
using Configurator.Desktop.ViewModels;

public interface IStorageDialogService
{
    /// <summary>
    /// Returns one local selection, or null when dismissed. Unsafe selections throw
    /// InvalidOperationException with selection-nonlocal; unavailable pickers throw
    /// NotSupportedException with selection-unavailable. Cancellation discards late results.
    /// </summary>
    Task<Uri?> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one provider-selected import within 4 MiB without requiring a local path.
    /// The caller owns the returned input; the provider item and stream settle before return.
    /// </summary>
    Task<BoundedFileInput?> ReadImportAsync(CancellationToken cancellationToken = default);
}
