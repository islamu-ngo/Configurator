using Configurator.Browser;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddSingleton(BrowserDefinitionRegistry.LoadBundled());
builder.Services.AddScoped<BrowserOfflinePolicy>();
builder.Services.AddScoped<BrowserDefinitionSource>();
WebAssemblyHost host = builder.Build();
await host.Services.GetRequiredService<BrowserOfflinePolicy>().InitializeAsync();
await host.RunAsync();
