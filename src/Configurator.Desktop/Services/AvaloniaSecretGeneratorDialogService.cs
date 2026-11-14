namespace Configurator.Desktop.Services;

using Avalonia.Controls;
using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;
using Configurator.Presentation;

/// <summary>Hosts <see cref="SecretGeneratorDialog"/> over the Desktop window for bounded local secret configuration.</summary>
public sealed class AvaloniaSecretGeneratorDialogService(
    Func<Window?> ownerWindow,
    ISecretClipboardService? defaultClipboard = null) : ISecretGeneratorDialogService
{
    public async Task<DefinitionSecretOperationResult?> OpenAsync(
        DefinitionFormModel form,
        DefinitionPath path,
        DesktopStrings strings,
        ISecretClipboardService? clipboard = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(strings);

        using DefinitionSecretGeneratorSession? session = form.CreateSecretGeneratorSession(path);
        if (session is null)
            return null;

        Window? owner = ownerWindow();
        if (owner is null)
            return null;

        using var viewModel = new SecretGeneratorDialogViewModel(
            form,
            session,
            strings,
            clipboard ?? defaultClipboard);
        var dialog = new SecretGeneratorDialog { DataContext = viewModel };
        using CancellationTokenRegistration cancelRegistration =
            cancellationToken.Register(() => viewModel.CancelCommand.Execute(null));

        await dialog.ShowDialog<bool>(owner);
        return viewModel.LastResult;
    }
}
