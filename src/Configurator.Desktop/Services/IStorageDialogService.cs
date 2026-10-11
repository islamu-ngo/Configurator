namespace Configurator.Desktop.Services;

using Configurator.Desktop.ViewModels;

public interface IStorageDialogService
{
    /// <summary>
    /// Returns one local selection, or null when dismissed. Unsafe selections throw
    /// InvalidOperationException with selection-nonlocal; unavailable pickers throw
    /// NotSupportedException with selection-unavailable. Cancellation discards late results.
    /// </summary>
    Task<Uri?> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default);
}
