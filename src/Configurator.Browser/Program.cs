using Configurator.Browser;
using Configurator.Browser.Services;
using Configurator.Browser.Services.Accessibility;
using Configurator.Browser.Services.Docking;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

// Keep answers local and support offline work; hosting rationale: docs/internal/LAUNCH_CONTRACT.md.
WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(BrowserDefinitionRegistry.LoadBundled());
builder.Services.AddScoped<BrowserOfflinePolicy>();
builder.Services.AddScoped<AppearanceThemeService>();
builder.Services.AddScoped<AccessibilityFocusService>();
builder.Services.AddScoped<AccessibilityAnnouncerService>();
builder.Services.AddScoped<DockLayoutState>();
builder.Services.AddScoped<IDockPanelRegistry>(services => services.GetRequiredService<DockLayoutState>());
builder.Services.AddScoped<IDockLayoutPersistence, LocalStorageDockLayoutPersistence>();
builder.Services.AddScoped<BrowserNavigationState>();
builder.Services.AddScoped<WorkspaceSearchState>();
builder.Services.AddSingleton<Configurator.Presentation.Store.IDefinitionStoreProvider, Configurator.Presentation.Store.EmbeddedDefinitionStoreProvider>();
builder.Services.AddScoped<DefinitionStoreCatalog>();
builder.Services.AddMudServices(config =>
{
    config.ResizeOptions.NotifyOnBreakpointOnly = false;
    config.ResizeOptions.ReportRate = 100;
});
WebAssemblyHost host = builder.Build();
await host.Services.GetRequiredService<BrowserOfflinePolicy>().InitializeAsync();
await host.RunAsync();
