namespace Configurator.Desktop.Services;

/// <summary>Explicit transfer of a secret to the native clipboard; callers retain their workspace on failure.</summary>
public interface ISecretClipboardService
{
    Task<bool> CopyAsync(string value);
}
