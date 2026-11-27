namespace Configurator.Browser.Services;

/// <summary>
/// Browser-layer quick action surfaced inside <c>Ctrl+K</c> discovery without polluting
/// shared <c>Configurator.Presentation.WorkspaceCommandId</c> contracts.
/// </summary>
public sealed record BrowserWorkspaceCommand(
    string Id,
    string Title,
    string? ShortcutHint,
    bool IsEnabled,
    Func<Task> ExecuteAsync);
