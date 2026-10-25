using Configurator.Browser;
using Configurator.Browser.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

// Keep answers local and support offline work; hosting rationale: docs/internal/LAUNCH_CONTRACT.md.
WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddSingleton(BrowserDefinitionRegistry.LoadBundled());
builder.Services.AddScoped<BrowserOfflinePolicy>();
builder.Services.AddScoped<BrowserDefinitionSource>();
builder.Services.AddScoped<AppearanceThemeService>();
builder.Services.AddScoped<BrowserNavigationState>();
builder.Services.AddScoped<WorkspaceSearchState>();
builder.Services.AddScoped<DefinitionStoreCatalog>();
builder.Services.AddMudServices();
WebAssemblyHost host = builder.Build();
await host.Services.GetRequiredService<BrowserOfflinePolicy>().InitializeAsync();
await host.RunAsync();
