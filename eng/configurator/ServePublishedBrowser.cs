#:property RestorePackagesWithLockFile=false

using System.Globalization;
using System.Net;

if (args.Length != 2 || !Path.IsPathFullyQualified(args[0]) ||
    !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port is < 1024 or > 65535)
{
    Console.Error.WriteLine("Usage: ServePublishedBrowser.cs ABSOLUTE_PUBLIC_ROOT PORT");
    return 64;
}
string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[0])) + Path.DirectorySeparatorChar;
if (!File.Exists(Path.Combine(root, "index.html")))
{
    Console.Error.WriteLine("published-index-missing");
    return 1;
}
using var listener = new HttpListener();
listener.Prefixes.Add($"http://127.0.0.1:{port}/");
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, signal) =>
{
    signal.Cancel = true;
    cancellation.Cancel();
    listener.Stop();
};
listener.Start();
Console.WriteLine($"PUBLISHED_BROWSER_READY http://127.0.0.1:{port}/");
try
{
    while (!cancellation.IsCancellationRequested)
    {
        HttpListenerContext context = await listener.GetContextAsync();
        using HttpListenerResponse response = context.Response;
        if (context.Request.HttpMethod is not ("GET" or "HEAD"))
        {
            response.StatusCode = 405;
            continue;
        }
        string relative = Uri.UnescapeDataString(context.Request.Url!.AbsolutePath).TrimStart('/');
        string path = Path.GetFullPath(Path.Combine(root, relative.Length == 0 ? "index.html" : relative));
        if (path.StartsWith(root, StringComparison.Ordinal) && !File.Exists(path) &&
            Path.GetExtension(path).Length == 0 &&
            context.Request.AcceptTypes?.Any(type => type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)) == true)
            path = Path.Combine(root, "index.html");
        if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
        {
            response.StatusCode = 404;
            continue;
        }
        response.ContentType = Path.GetExtension(path) switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" => "application/json",
            ".wasm" => "application/wasm",
            ".webmanifest" => "application/manifest+json",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream"
        };
        response.Headers["Cache-Control"] = "no-cache";
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, useAsync: true);
        response.ContentLength64 = input.Length;
        if (context.Request.HttpMethod == "GET")
            await input.CopyToAsync(response.OutputStream, cancellation.Token);
    }
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
catch (HttpListenerException) when (cancellation.IsCancellationRequested) { }
return 0;
