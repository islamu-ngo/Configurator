using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;

namespace Configurator.Browser.Host;

internal static class Program
{
    private static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        // URLs may contain user-supplied text; this host emits no request logs.
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
        builder.Services.AddResponseCompression(options =>
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append("application/wasm"));

        WebApplication app = builder.Build();
        app.Use((context, next) =>
        {
            context.Response.Headers.ContentSecurityPolicy =
                "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self' https:; worker-src 'self'; object-src 'none'; base-uri 'self'; form-action 'none'";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers.CacheControl = "no-cache";

            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return Task.CompletedTask;
            }

            return next(context);
        });
        app.UseResponseCompression();

        FileExtensionContentTypeProvider contentTypes = new();
        contentTypes.Mappings[".wasm"] = "application/wasm";
        contentTypes.Mappings[".webmanifest"] = "application/manifest+json";
        contentTypes.Mappings[".js"] = "text/javascript";
        contentTypes.Mappings[".mjs"] = "text/javascript";
        StaticFileOptions publicFiles = new()
        {
            ContentTypeProvider = contentTypes,
            ServeUnknownFileTypes = true
        };
        app.UseStaticFiles(publicFiles);

        foreach (string route in new[]
        {
            "/", "/open", "/help", "/store",
            "/store/{slug:nonfile}", "/launch/{id:nonfile}"
        })
        {
            app.MapFallbackToFile(route, "index.html", publicFiles);
        }

        app.Run();
    }
}
