namespace Configurator.Browser.Tests;

using Bunit;
using Configurator.Browser.Services;

public sealed class BrowserAppearanceTests
{
    [Test]
    [Arguments("{\"themeMode\":\"dark\",\"direction\":\"rtl\",\"answers\":\"synthetic-private-canary\"}")]
    [Arguments("{\"themeMode\":\"<script>synthetic</script>\",\"direction\":\"rtl\"}")]
    [Arguments("{\"themeMode\":\"dark\",\"direction\":\"rtl\",\"direction\":\"ltr\"}")]
    [Arguments("{\"themeMode\":\"dark\",\"direction\":\"unknown\"}")]
    [Arguments("{\"themeMode\":1,\"direction\":\"auto\"}")]
    [Arguments("{broken")]
    [Arguments("[]")]
    public async Task CorruptOrAnswerBearingPreferencesFallBackToSystem(string preference)
    {
        await Assert.That(AppearanceState.Parse(preference)).IsEqualTo(new AppearanceState());
    }

    [Test]
    public async Task OversizedPreferencesCannotBecomePersistentConfiguration()
    {
        await Assert.That(AppearanceState.Parse(new string('x', 257))).IsEqualTo(new AppearanceState());
    }

    [Test]
    [Arguments("system")]
    [Arguments("light")]
    [Arguments("dark")]
    [Arguments("lighthighcontrast")]
    [Arguments("darkhighcontrast")]
    public async Task SupportedAppearanceAndDirectionRoundTrip(string mode)
    {
        await Assert.That(AppearanceState.Parse($$"""{"themeMode":"{{mode}}","direction":"rtl"}""")).IsEqualTo(new AppearanceState(mode, "rtl"));
    }

    [Test]
    public async Task OnlyBoundedPreferencesCrossStorageInteropAndSystemChangesStayLocal()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./browser-appearance.js");
        module.Setup<string?>("read", _ => true).SetResult("""{"themeMode":"dark","direction":"rtl","answers":"synthetic-private-canary"}""");
        module.SetupVoid("watch", _ => true).SetVoidResult();
        module.SetupVoid("apply", _ => true).SetVoidResult();
        module.SetupVoid("write", _ => true).SetVoidResult();
        module.SetupVoid("unwatch", _ => true).SetVoidResult();
        await using var service = new AppearanceThemeService(context.JSInterop.JSRuntime);
        await service.InitializeAsync();
        await Assert.That(service.Current).IsEqualTo(new AppearanceState());
        service.SystemPreferencesChanged(true, true);
        await Assert.That(service.IsDark).IsTrue();
        await Assert.That(service.IsRtl).IsTrue();
        await service.SetAsync("lighthighcontrast", "ltr");
        await Assert.That(service.IsDark).IsFalse();
        await Assert.That(service.IsRtl).IsFalse();
        await service.SetAsync("synthetic-private-canary", "ltr");
        await Assert.That(service.Current).IsEqualTo(new AppearanceState("lighthighcontrast", "ltr"));
        var writes = module.Invocations.Where(call => call.Identifier == "write").ToArray();
        await Assert.That(writes.Length).IsEqualTo(1);
        await Assert.That(writes[0].Arguments.Cast<string>().ToArray()).IsEquivalentTo(["lighthighcontrast", "ltr"]);
    }

    [Test]
    public async Task LatePreferenceReadCannotReplaceTheUsersCurrentChoice()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./browser-appearance.js");
        var read = module.Setup<string?>("read", _ => true);
        module.SetupVoid("watch", _ => true).SetVoidResult();
        module.SetupVoid("apply", _ => true).SetVoidResult();
        module.SetupVoid("write", _ => true).SetVoidResult();
        module.SetupVoid("unwatch", _ => true).SetVoidResult();
        await using var service = new AppearanceThemeService(context.JSInterop.JSRuntime);
        ValueTask initialization = service.InitializeAsync();
        await service.SetAsync("dark", "rtl");
        read.SetResult("""{"themeMode":"light","direction":"ltr"}""");
        await initialization;
        await Assert.That(service.Current).IsEqualTo(new AppearanceState("dark", "rtl"));
    }
}
