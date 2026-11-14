namespace Configurator.Browser.Components.Configuration;

using Configurator.Browser.Resources;
using Microsoft.AspNetCore.Components;

public enum WorkspaceStartupStage
{
    BeforeEntry,
    Preparing,
    PreparationFailed,
    Ready
}

public partial class WorkspaceStartupGate
{
    [Parameter, EditorRequired] public string Locale { get; set; } = "en";
    [Parameter, EditorRequired] public WorkspaceStartupStage Stage { get; set; }
    [Parameter] public string? Title { get; set; }
    [Parameter] public string? StatusMessage { get; set; }
    [Parameter] public string? StatusCode { get; set; }
    [Parameter] public EventCallback StartRequested { get; set; }

    private ElementReference _startButton;

    public ElementReference StartButton => _startButton;

    private string StateToken => Stage switch
    {
        WorkspaceStartupStage.Preparing => "preparing",
        WorkspaceStartupStage.PreparationFailed => "failed",
        WorkspaceStartupStage.Ready => "ready",
        _ => "before-entry"
    };

    private string T(string key) => BrowserStrings.Get(key, Locale);
}
