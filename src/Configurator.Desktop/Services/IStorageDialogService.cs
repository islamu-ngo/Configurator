namespace Configurator.Desktop.Services;

using Configurator.Artifacts;
using Configurator.Desktop.ViewModels;

public interface IStorageDialogService
{
    /// <summary>
    /// Returns an explicit local selection outcome. Cancellation discards late results.
    /// </summary>
    Task<StorageSelection<Uri>> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one provider-selected import within 4 MiB without requiring a local path.
    /// The caller owns a selected input; the provider item and stream settle before return.
    /// </summary>
    Task<StorageSelection<BoundedFileInput>> ReadImportAsync(CancellationToken cancellationToken = default);
}
