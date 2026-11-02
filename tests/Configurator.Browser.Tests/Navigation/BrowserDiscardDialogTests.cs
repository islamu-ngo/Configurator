namespace Configurator.Browser.Tests;

using Bunit;
using Configurator.Browser.Components.Common;
using Configurator.Browser.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

public sealed class BrowserDiscardDialogTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RetiringSourceDismissesPendingDiscardDecision(bool dispose)
    {
        await using var context = BrowserFixture.Context();
        using var prompt = new DiscardPrompt(context);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/open#src=https%3A%2F%2Fpublisher.example%2Fdefinition.json");
        var transport = context.JSInterop.SetupModule("./js/browser-definition-source.js");
        transport.Setup<byte[]>("fetchDefinition", _ => true).SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
        var host = context.Render<CascadingValue<bool>>(parameters => parameters
            .Add(item => item.Value, true)
            .Add(item => item.ChildContent, builder =>
            {
                builder.OpenComponent<Open>(0);
                builder.CloseComponent();
            }));
        var page = host.FindComponent<Open>();
        await page.Find("[data-action='open-workspace']").ClickAsync(new MouseEventArgs());
        Task discarding = page.Find("[data-action='discard-workspace']").ClickAsync(new MouseEventArgs());
        await prompt.Opened.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            if (dispose) host.Render(parameters => parameters.Add(item => item.Value, true).Add(item => item.ChildContent, (RenderFragment?)null));
            else await page.InvokeAsync(() => navigation.NavigateTo("/open"));
            await discarding.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(prompt.IsOpen).IsFalse();
        }
        finally
        {
            if (prompt.IsOpen) await prompt.DecideAsync(false);
            await discarding.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    [Arguments("en", "ltr")]
    [Arguments("ar", "rtl")]
    public async Task DialogNamesItsDecisionAndCancellationKeepsWorkspace(string locale, string direction)
    {
        await using var context = BrowserFixture.Context();
        var provider = context.Render<MudDialogProvider>();
        IDialogService dialogs = context.Services.GetRequiredService<IDialogService>();
        Task<bool>? decision = null;
        await provider.InvokeAsync(() => { decision = AppDiscardDialog.ConfirmAsync(dialogs, locale); });

        var dialog = provider.Find("[role='dialog']");
        string label = dialog.GetAttribute("aria-labelledby")!;
        await Assert.That(provider.Find("#" + label).TextContent.Length).IsGreaterThan(0);
        await Assert.That(dialog.QuerySelector("p")!.GetAttribute("dir")).IsEqualTo(direction);
        await Assert.That(dialog.QuerySelector("button")!.GetAttribute("data-action")).IsEqualTo("keep-workspace");
        await provider.Find("[data-action='keep-workspace']").ClickAsync(new MouseEventArgs());

        await Assert.That(await decision!.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(provider.FindAll("[role='dialog']")).IsEmpty();
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Identifier == "confirm")).IsFalse();
    }

    [Test]
    public async Task SupersededNavigationDismissesItsPendingDialog()
    {
        await using var context = BrowserFixture.Context();
        var provider = context.Render<MudDialogProvider>();
        using var cancellation = new CancellationTokenSource();
        IDialogService dialogs = context.Services.GetRequiredService<IDialogService>();
        Task<bool>? decision = null;
        await provider.InvokeAsync(() => { decision = AppDiscardDialog.ConfirmAsync(dialogs, "en", cancellation.Token); });
        await provider.InvokeAsync(cancellation.Cancel);

        await Assert.That(await decision!.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(provider.FindAll("[role='dialog']")).IsEmpty();
    }
}
