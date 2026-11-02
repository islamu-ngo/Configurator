namespace Configurator.Browser.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

internal sealed class DiscardPrompt : IDisposable
{
    private readonly IDialogService _dialogs;
    private readonly IRenderedComponent<MudDialogProvider> _provider;
    internal TaskCompletionSource Opened { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int Count { get; private set; }
    internal bool IsOpen => _provider.FindAll("[role='dialog']").Count != 0;

    internal DiscardPrompt(BunitContext context)
    {
        _provider = context.Render<MudDialogProvider>();
        _dialogs = context.Services.GetRequiredService<IDialogService>();
        _dialogs.DialogInstanceAddedAsync += AddedAsync;
    }

    private Task AddedAsync(IDialogReference dialog)
    {
        Count++;
        Opened.TrySetResult();
        return Task.CompletedTask;
    }

    internal async Task DecideAsync(bool discard)
    {
        await Opened.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _provider.InvokeAsync(() => { });
        string action = discard ? "confirm-discard" : "keep-workspace";
        await _provider.Find($"[data-action='{action}']").ClickAsync(new MouseEventArgs());
        Opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Dispose()
    {
        _dialogs.DialogInstanceAddedAsync -= AddedAsync;
        _provider.Dispose();
    }
}
