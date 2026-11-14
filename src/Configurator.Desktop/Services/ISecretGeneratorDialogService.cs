namespace Configurator.Desktop.Services;

using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;
using Configurator.Presentation;

/// <summary>Opens the local modal secret-generator options dialog for one admitted secret field.</summary>
public interface ISecretGeneratorDialogService
{
    Task<DefinitionSecretOperationResult?> OpenAsync(
        DefinitionFormModel form,
        DefinitionPath path,
        DesktopStrings strings,
        ISecretClipboardService? clipboard = null,
        CancellationToken cancellationToken = default);
}
