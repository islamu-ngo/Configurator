namespace Configurator.Desktop.Services;

using Avalonia.Input.Platform;

public sealed class AvaloniaSecretClipboardService(Func<IClipboard?> clipboard) : ISecretClipboardService
{
    public async Task<bool> CopyAsync(string value)
    {
        try
        {
            if (clipboard() is not { } target) return false;
            await target.SetTextAsync(value);
            return true;
        }
        catch (Exception) { return false; }
    }
}
