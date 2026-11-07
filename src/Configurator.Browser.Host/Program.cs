using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using System.Text.Json;

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
        PublicationContentTypes contentTypes = new(app.Environment.WebRootPath);
        app.Use((context, next) =>
        {
            context.Response.Headers.ContentSecurityPolicy =
                "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self' https:; worker-src 'self'; object-src 'none'; base-uri 'self'; form-action 'none'; frame-ancestors 'none'";
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

        StaticFileOptions publicFiles = new()
        {
            ContentTypeProvider = contentTypes
        };
        app.UseStaticFiles(publicFiles);

        foreach (string route in new[]
        {
            "/", "/open", "/help",
            "/store/{slug:nonfile}", "/launch/{id:nonfile}"
        })
        {
            app.MapFallbackToFile(route, "index.html", publicFiles);
        }

        app.Run();
    }

    /// <summary>
    /// The SDK PWA manifest identifies application assets, including optional
    /// Store content. Release.proj adds a fixed set of public contracts afterward.
    /// Merely placing another file in webroot never grants HTTP publication.
    /// </summary>
    private sealed class PublicationContentTypes : IContentTypeProvider
    {
        private readonly HashSet<string> _paths = new(StringComparer.Ordinal);
        private readonly FileExtensionContentTypeProvider _types = new();

        public PublicationContentTypes(string webRoot)
        {
            _types.Mappings[".wasm"] = "application/wasm";
            _types.Mappings[".dat"] = "application/octet-stream";
            _types.Mappings[".webmanifest"] = "application/manifest+json";
            _types.Mappings[".js"] = "text/javascript";
            _types.Mappings[".mjs"] = "text/javascript";
            _types.Mappings[".map"] = "application/json";
            _types.Mappings[".md"] = "text/plain";
            string manifestPath = Path.Combine(webRoot, "service-worker-assets.js");
            const string prefix = "self.assetsManifest = ";
            if (new FileInfo(manifestPath).Length > 4 * 1024 * 1024)
                throw new InvalidOperationException("Public asset inventory exceeds its limit.");
            string manifest = File.ReadAllText(manifestPath).Trim();
            if (!manifest.StartsWith(prefix, StringComparison.Ordinal) || !manifest.EndsWith(';'))
                throw new InvalidOperationException("Invalid public asset inventory.");
            using JsonDocument document = JsonDocument.Parse(manifest[prefix.Length..^1]);
            JsonElement assets = document.RootElement.GetProperty("assets");
            if (assets.GetArrayLength() > 10000)
                throw new InvalidOperationException("Public asset inventory exceeds its limit.");
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                string path = asset.GetProperty("url").GetString()!;
                if (string.IsNullOrEmpty(path) || path.StartsWith('/') || path.Contains('\\')
                    || path.Contains('?') || path.Contains('#') || path.Contains('%')
                    || path.Split('/').Any(segment => segment is "" or "." or ".."))
                    throw new InvalidOperationException("Invalid public asset inventory path.");
                _paths.Add("/" + path);
            }
            foreach (string path in new[]
            {
                "service-worker.js", "service-worker-assets.js", "LICENSE", "THIRD-PARTY-NOTICES.md",
                "contracts/browser-release-capabilities.json", "contracts/frozen-contract-baseline.json",
                "contracts/native-release-capabilities.json",
                "schemas/setup-definition.schema.json", "schemas/configurator-authoring.schema.json",
                "schemas/configurator-cli.schema.json", "schemas/configurator-answers.schema.json",
                "schemas/definition-store-curation.schema.json"
            })
                _paths.Add("/" + path);
        }

        public bool TryGetContentType(string subpath, out string contentType)
        {
            if (!_paths.Contains(subpath))
            {
                contentType = string.Empty;
                return false;
            }
            if (subpath == "/LICENSE")
            {
                contentType = "text/plain";
                return true;
            }
            return _types.TryGetContentType(subpath, out contentType!);
        }
    }
}
