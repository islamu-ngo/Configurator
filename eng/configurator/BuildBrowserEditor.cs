#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=$(BaseIntermediateOutputPath)browser-editor.packages.lock.json
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property AssemblyName=Configurator.BrowserEditorBuilder
#:property RootNamespace=Configurator.Engineering

using System.Formats.Tar;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

return await BrowserEditorBuilder.RunAsync(args);

internal static class BrowserEditorBuilder
{
    private const int MaximumArchiveBytes = 8 * 1024 * 1024;
    private const int MaximumLicenseBytes = 128 * 1024;
    private static readonly HashSet<string> LicenseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "LICENSE", "LICENSE.txt", "LICENSE.md", "COPYING", "COPYING.txt", "NOTICE", "NOTICE.txt"
    };

    internal static async Task<int> RunAsync(string[] arguments)
    {
        try
        {
            if (arguments is ["--self-test"])
            {
                await SelfTestAsync();
                return 0;
            }
            if (arguments is ["--review-closure", var directory, ..] && arguments.Length is 2 or 3)
            {
                await ReviewClosureAsync(Path.GetFullPath(directory),
                    Path.GetFullPath(arguments.Length == 3 ? arguments[2] : directory));
                return 0;
            }
            if (arguments is [var operation, var root] && operation is "--write" or "--check")
            {
                await BuildAsync(Path.GetFullPath(root), operation == "--check");
                return 0;
            }
            Console.Error.WriteLine("Usage: BuildBrowserEditor.cs (--self-test|--review-closure directory [output]|--write root|--check root)");
            return 64;
        }
        catch (EditorAssetException exception)
        {
            Console.Error.WriteLine(exception.Code);
            return 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or JsonException or HttpRequestException or InvalidDataException or FormatException
            or ArgumentException or InvalidOperationException or KeyNotFoundException or TaskCanceledException)
        {
            Console.Error.WriteLine("editor-asset-input-invalid");
            return 1;
        }
    }

    private static async Task SelfTestAsync()
    {
        // Synthetic verification authority exists only in this disposable fixture.
        // It admits no real dependency, installation or distributable editor.
        string root = Path.Combine(Path.GetTempPath(), "configurator-editor-self-test-" + Guid.NewGuid().ToString("N"));
        const string packagePath = "eng/configurator/editor/package.json";
        const string manifestPath = "src/Configurator.Browser/wwwroot/vendor/configuration-editor.manifest.json";
        const string bundlePath = "src/Configurator.Browser/wwwroot/vendor/configuration-editor.js";
        const string noticePath = "src/Configurator.Browser/wwwroot/vendor/configuration-editor.LICENSE.txt";
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["eng/configurator/editor/package-lock.json"] = """{"lockfileVersion":3,"packages":{}}"""u8.ToArray(),
            ["eng/configurator/editor/editor-entry.js"] = "// synthetic entry\n"u8.ToArray(),
            ["eng/configurator/BuildBrowserEditor.cs"] = "// synthetic builder\n"u8.ToArray(),
            ["docs/internal/legal/dependencies/codemirror.md"] = "Synthetic fixture authority, not dependency approval.\n"u8.ToArray(),
            [bundlePath] = "// synthetic output\n"u8.ToArray(),
            [noticePath] = "Synthetic fixture notice, not third-party terms.\n"u8.ToArray()
        };
        EditorComponent[] components =
        [
            new("configurator-self-test", "1.0.0", "direct-runtime", "MIT", "sha512-synthetic",
                "https://example.invalid/synthetic.tgz", [new("package/LICENSE", Hash(files[noticePath]))])
        ];
        string closure = Hash(Json(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            WriteComponents(writer, components);
            writer.WriteEndObject();
        }));
        files[packagePath] = Json(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartObject("configuratorEditor");
            writer.WriteString("approval", "approved");
            writer.WriteString("closureSha256", closure);
            writer.WriteString("recordSha256", Hash(files["docs/internal/legal/dependencies/codemirror.md"]));
            writer.WriteString("lockSha256", Hash(files["eng/configurator/editor/package-lock.json"]));
            writer.WriteString("noticesSha256", Hash(files[noticePath]));
            writer.WriteString("node", "synthetic-node");
            writer.WriteString("npm", "synthetic-npm");
            writer.WriteString("bun", "synthetic-bun");
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
        files[manifestPath] = Json(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("closureSha256", closure);
            writer.WriteStartObject("toolchain");
            writer.WriteString("node", "synthetic-node");
            writer.WriteString("npm", "synthetic-npm");
            writer.WriteString("bun", "synthetic-bun");
            writer.WriteEndObject();
            writer.WriteStartObject("inputs");
            foreach ((string path, byte[] bytes) in files.Where(item => item.Key != bundlePath && item.Key != noticePath)
                .OrderBy(item => item.Key, StringComparer.Ordinal))
                writer.WriteString(path, Hash(bytes));
            writer.WriteEndObject();
            writer.WriteStartObject("outputs");
            foreach (string path in new[] { bundlePath, noticePath })
            {
                writer.WriteStartObject(Path.GetFileName(path));
                writer.WriteString("sha256", Hash(files[path]));
                writer.WriteNumber("bytes", files[path].Length);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            WriteComponents(writer, components);
            writer.WriteEndObject();
        });
        try
        {
            foreach ((string path, byte[] bytes) in files)
            {
                string target = Path.Combine(root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllBytesAsync(target, bytes);
            }
            if (await RunAsync(["--check", root]) != 0) throw new EditorAssetException("editor-self-test-baseline");
            int rejected = 0;
            await RejectAsync(packagePath, "{}"u8.ToArray());
            await RejectAsync("docs/internal/legal/dependencies/codemirror.md", "changed authority"u8.ToArray());
            await RejectAsync("eng/configurator/editor/package-lock.json", "{}"u8.ToArray());
            await RejectAsync("eng/configurator/editor/editor-entry.js", "// changed entry"u8.ToArray());
            await RejectAsync(manifestPath, Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(files[manifestPath])
                .Replace(closure, new string('0', 64), StringComparison.Ordinal)));
            await RejectAsync(manifestPath, Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(files[manifestPath])
                .Replace("1.0.0", "2.0.0", StringComparison.Ordinal)));
            await RejectAsync(bundlePath, "// changed output"u8.ToArray());
            await RejectAsync(noticePath, null);
            await RejectAsync(manifestPath, Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(files[manifestPath])
                .Replace("synthetic-npm", "changed-npm", StringComparison.Ordinal)));
            Console.WriteLine($"EDITOR_SELF_TEST passed={rejected + 1} rejected={rejected} network=none");

            async Task RejectAsync(string path, byte[]? replacement)
            {
                string target = Path.Combine(root, path);
                try
                {
                    if (replacement is null) File.Delete(target);
                    else await File.WriteAllBytesAsync(target, replacement);
                    if (await RunAsync(["--check", root]) != 1)
                        throw new EditorAssetException("editor-self-test-accepted-tampering");
                    rejected++;
                }
                finally { await File.WriteAllBytesAsync(target, files[path]); }
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }

        static byte[] Json(Action<Utf8JsonWriter> write)
        {
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output)) write(writer);
            return output.ToArray();
        }
    }

    private static async Task BuildAsync(string root, bool check)
    {
        string input = Path.Combine(root, "eng/configurator/editor");
        string packagePath = Path.Combine(input, "package.json");
        if (!File.Exists(packagePath)) throw new EditorAssetException("editor-approval-required");
        using JsonDocument package = JsonDocument.Parse(await File.ReadAllBytesAsync(packagePath));
        if (!package.RootElement.TryGetProperty("configuratorEditor", out JsonElement recipe)
            || recipe.GetProperty("approval").GetString() != "approved")
            throw new EditorAssetException("editor-approval-required");
        string record = Path.Combine(root, "docs/internal/legal/dependencies/codemirror.md");
        string lockPath = Path.Combine(input, "package-lock.json");
        string closure = recipe.GetProperty("closureSha256").GetString()!;
        if (Hash(await File.ReadAllBytesAsync(record)) != recipe.GetProperty("recordSha256").GetString()
            || Hash(await File.ReadAllBytesAsync(lockPath)) != recipe.GetProperty("lockSha256").GetString())
            throw new EditorAssetException("editor-approval-binding");
        var inputs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in new[]
        {
            "eng/configurator/editor/package.json",
            "eng/configurator/editor/package-lock.json",
            "eng/configurator/editor/editor-entry.js",
            "eng/configurator/BuildBrowserEditor.cs",
            "docs/internal/legal/dependencies/codemirror.md"
        })
            inputs.Add(path, Hash(await File.ReadAllBytesAsync(Path.Combine(root, path))));
        string vendor = Path.Combine(root, "src/Configurator.Browser/wwwroot/vendor");
        string manifestPath = Path.Combine(vendor, "configuration-editor.manifest.json");
        if (check)
        {
            if (!File.Exists(manifestPath)) throw new EditorAssetException("editor-output-missing");
            using JsonDocument generated = JsonDocument.Parse(await File.ReadAllBytesAsync(manifestPath));
            JsonElement metadata = generated.RootElement;
            if (metadata.GetProperty("schemaVersion").GetInt32() != 1
                || metadata.GetProperty("closureSha256").GetString() != closure)
                throw new EditorAssetException("editor-output-closure");
            foreach (string name in new[] { "node", "npm", "bun" })
                if (metadata.GetProperty("toolchain").GetProperty(name).GetString() != recipe.GetProperty(name).GetString())
                    throw new EditorAssetException("editor-output-toolchain");
            EditorComponent[] recorded = metadata.GetProperty("components").EnumerateArray().Select(component =>
                new EditorComponent(component.GetProperty("name").GetString()!, component.GetProperty("version").GetString()!,
                    component.GetProperty("role").GetString()!, component.GetProperty("license").GetString()!,
                    component.GetProperty("integrity").GetString()!, component.GetProperty("source").GetString()!,
                    component.GetProperty("licenses").EnumerateArray().Select(license => new EditorLicense(
                        license.GetProperty("name").GetString()!, license.GetProperty("sha256").GetString()!)).ToArray())).ToArray();
            using var recordedInventory = new MemoryStream();
            using (var writer = new Utf8JsonWriter(recordedInventory))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", 1);
                WriteComponents(writer, recorded);
                writer.WriteEndObject();
            }
            if (recorded.Length == 0 || Hash(recordedInventory.ToArray()) != closure)
                throw new EditorAssetException("editor-output-closure");
            foreach ((string path, string hash) in inputs)
                if (!metadata.GetProperty("inputs").TryGetProperty(path, out JsonElement expected)
                    || expected.GetString() != hash)
                    throw new EditorAssetException("editor-input-stale");
            var outputNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty output in metadata.GetProperty("outputs").EnumerateObject())
            {
                if (output.Name is not ("configuration-editor.js" or "configuration-editor.LICENSE.txt")
                    || !outputNames.Add(output.Name))
                    throw new EditorAssetException("editor-output-invalid");
                byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(vendor, output.Name));
                if (Hash(bytes) != output.Value.GetProperty("sha256").GetString()
                    || bytes.LongLength != output.Value.GetProperty("bytes").GetInt64())
                    throw new EditorAssetException("editor-output-stale");
                if (output.Name == "configuration-editor.LICENSE.txt"
                    && Hash(bytes) != recipe.GetProperty("noticesSha256").GetString())
                    throw new EditorAssetException("editor-notices-unreviewed");
            }
            if (outputNames.Count != 2)
                throw new EditorAssetException("editor-output-incomplete");
            Console.WriteLine($"EDITOR_ASSET_CHECK closure={closure} outputs=2");
            return;
        }

        foreach ((string command, string expected) in new[]
        {
            ("node", "v" + recipe.GetProperty("node").GetString()),
            ("npm", recipe.GetProperty("npm").GetString()!),
            ("bun", recipe.GetProperty("bun").GetString()!)
        })
            if ((await RunProcessAsync(command, ["--version"], input)).Trim() != expected)
                throw new EditorAssetException("editor-toolchain-mismatch");
        await RunProcessAsync("npm", ["ci", "--ignore-scripts", "--no-audit", "--no-fund"], input);
        using JsonDocument locked = JsonDocument.Parse(await File.ReadAllBytesAsync(lockPath));
        if (locked.RootElement.GetProperty("lockfileVersion").GetInt32() != 3)
            throw new EditorAssetException("editor-lock-version");
        var components = new List<EditorComponent>();
        using var notices = new MemoryStream();
        JsonElement direct = package.RootElement.GetProperty("dependencies");
        foreach (JsonProperty item in locked.RootElement.GetProperty("packages").EnumerateObject()
            .Where(item => item.Name.Length != 0).OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            const string prefix = "node_modules/";
            if (!item.Name.StartsWith(prefix, StringComparison.Ordinal)
                || item.Name[prefix.Length..].Contains("/node_modules/", StringComparison.Ordinal))
                throw new EditorAssetException("editor-closure-duplicated");
            string name = item.Name[prefix.Length..];
            string version = item.Value.GetProperty("version").GetString()!;
            bool isDirect = direct.TryGetProperty(name, out JsonElement pin);
            if (isDirect && pin.GetString() != version
                || item.Value.GetProperty("license").GetString() != "MIT")
                throw new EditorAssetException("editor-closure-unreviewed");
            string directory = Path.Combine(input, item.Name);
            var licenses = new List<EditorLicense>();
            foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(path => LicenseNames.Contains(Path.GetFileName(path))).Order(StringComparer.Ordinal))
            {
                if (new FileInfo(path).Length > MaximumLicenseBytes)
                    throw new EditorAssetException("editor-license-invalid");
                byte[] bytes = await File.ReadAllBytesAsync(path);
                string licenseName = "package/" + Path.GetRelativePath(directory, path).Replace('\\', '/');
                licenses.Add(new(licenseName, Hash(bytes)));
                notices.Write(Encoding.UTF8.GetBytes($"\n--- {name} {version} / {licenseName} ---\n"));
                notices.Write(bytes);
                notices.WriteByte((byte)'\n');
            }
            if (licenses.Count == 0) throw new EditorAssetException("editor-license-missing");
            components.Add(new(name, version, isDirect ? "direct-runtime" : "transitive-runtime", "MIT",
                item.Value.GetProperty("integrity").GetString()!, item.Value.GetProperty("resolved").GetString()!,
                licenses.OrderBy(license => license.Name, StringComparer.Ordinal).ToArray()));
        }
        using var inventory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(inventory))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            WriteComponents(writer, components);
            writer.WriteEndObject();
        }
        if (Hash(inventory.ToArray()) != closure) throw new EditorAssetException("editor-closure-unreviewed");
        string temporary = Path.Combine(Path.GetTempPath(), "configurator-editor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        byte[] bundle;
        try
        {
            string output = Path.Combine(temporary, "configuration-editor.js");
            await RunProcessAsync("bun", ["build", "editor-entry.js", "--target", "browser", "--format", "esm",
                "--minify", "--outfile", output], input);
            bundle = Encoding.UTF8.GetBytes("/*! Component terms: configuration-editor.LICENSE.txt */\n")
                .Concat(await File.ReadAllBytesAsync(output)).ToArray();
        }
        finally { Directory.Delete(temporary, recursive: true); }
        byte[] terms = notices.ToArray();
        if (Hash(terms) != recipe.GetProperty("noticesSha256").GetString())
            throw new EditorAssetException("editor-notices-unreviewed");
        using var manifest = new MemoryStream();
        using (var writer = new Utf8JsonWriter(manifest, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("closureSha256", closure);
            writer.WriteStartObject("toolchain");
            foreach (string name in new[] { "node", "npm", "bun" })
                writer.WriteString(name, recipe.GetProperty(name).GetString());
            writer.WriteEndObject();
            writer.WriteStartObject("inputs");
            foreach ((string path, string hash) in inputs) writer.WriteString(path, hash);
            writer.WriteEndObject();
            writer.WriteStartObject("outputs");
            foreach ((string name, byte[] bytes) in new[]
            {
                ("configuration-editor.js", bundle), ("configuration-editor.LICENSE.txt", terms)
            })
            {
                writer.WriteStartObject(name);
                writer.WriteString("sha256", Hash(bytes));
                writer.WriteNumber("bytes", bytes.Length);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            WriteComponents(writer, components);
            writer.WriteEndObject();
        }
        Directory.CreateDirectory(vendor);
        await File.WriteAllBytesAsync(Path.Combine(vendor, "configuration-editor.js"), bundle);
        await File.WriteAllBytesAsync(Path.Combine(vendor, "configuration-editor.LICENSE.txt"), terms);
        await File.WriteAllBytesAsync(manifestPath, manifest.ToArray());
        Console.WriteLine($"EDITOR_ASSET_BUILT closure={closure} components={components.Count} bytes={bundle.Length}");
    }

    private static async Task<string> RunProcessAsync(string command, string[] arguments, string directory)
    {
        var start = new ProcessStartInfo(command)
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new EditorAssetException("editor-tool-unavailable");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(output, error);
        if (process.ExitCode != 0) throw new EditorAssetException("editor-tool-failed");
        return await output;
    }

    private static async Task ReviewClosureAsync(string directory, string output)
    {
        using JsonDocument manifest = JsonDocument.Parse(
            await File.ReadAllBytesAsync(Path.Combine(directory, "package.json")));
        using JsonDocument locked = JsonDocument.Parse(
            await File.ReadAllBytesAsync(Path.Combine(directory, "package-lock.json")));
        if (locked.RootElement.GetProperty("lockfileVersion").GetInt32() != 3)
            throw new EditorAssetException("editor-lock-version");
        JsonElement direct = manifest.RootElement.GetProperty("dependencies");
        var components = new List<EditorComponent>();
        using var notices = new MemoryStream();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        foreach (JsonProperty package in locked.RootElement.GetProperty("packages").EnumerateObject()
            .Where(package => package.Name.Length != 0).OrderBy(package => package.Name, StringComparer.Ordinal))
        {
            const string prefix = "node_modules/";
            if (!package.Name.StartsWith(prefix, StringComparison.Ordinal)
                || package.Name[prefix.Length..].Contains("/node_modules/", StringComparison.Ordinal))
                throw new EditorAssetException("editor-closure-duplicated");
            string name = package.Name[prefix.Length..];
            JsonElement metadata = package.Value;
            string version = metadata.GetProperty("version").GetString()!;
            string integrity = metadata.GetProperty("integrity").GetString()!;
            if (metadata.GetProperty("license").GetString() != "MIT"
                || !integrity.StartsWith("sha512-", StringComparison.Ordinal))
                throw new EditorAssetException("editor-license-unreviewed");
            bool isDirect = direct.TryGetProperty(name, out JsonElement pin);
            if (isDirect && pin.GetString() != version)
                throw new EditorAssetException("editor-root-not-pinned");
            Uri source = new(metadata.GetProperty("resolved").GetString()!, UriKind.Absolute);
            if (source.Scheme != Uri.UriSchemeHttps || source.Host != "registry.npmjs.org"
                || !source.IsDefaultPort || source.UserInfo.Length != 0 || source.Query.Length != 0
                || source.Fragment.Length != 0)
                throw new EditorAssetException("editor-package-source");
            using HttpResponseMessage response = await http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumArchiveBytes)
                throw new EditorAssetException("editor-package-limit");
            await using Stream incoming = await response.Content.ReadAsStreamAsync();
            byte[] archive = await ReadBoundedAsync(incoming, MaximumArchiveBytes);
            if (!CryptographicOperations.FixedTimeEquals(SHA512.HashData(archive),
                Convert.FromBase64String(integrity["sha512-".Length..])))
                throw new EditorAssetException("editor-package-integrity");
            var licenses = new List<EditorLicense>();
            using var compressed = new MemoryStream(archive, writable: false);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var reader = new TarReader(gzip);
            int entries = 0;
            while (reader.GetNextEntry() is { } entry)
            {
                if (++entries > 10_000) throw new EditorAssetException("editor-package-limit");
                string filename = Path.GetFileName(entry.Name);
                if (!LicenseNames.Contains(filename)) continue;
                if (entry.DataStream is null || entry.Length > MaximumLicenseBytes)
                    throw new EditorAssetException("editor-license-invalid");
                byte[] bytes = await ReadBoundedAsync(entry.DataStream, MaximumLicenseBytes);
                licenses.Add(new(entry.Name, Hash(bytes)));
                notices.Write(Encoding.UTF8.GetBytes($"\n--- {name} {version} / {entry.Name} ---\n"));
                notices.Write(bytes);
                notices.WriteByte((byte)'\n');
            }
            if (licenses.Count == 0) throw new EditorAssetException("editor-license-missing");
            components.Add(new(name, version, isDirect ? "direct-runtime" : "transitive-runtime",
                "MIT", integrity, source.AbsoluteUri,
                licenses.OrderBy(license => license.Name, StringComparer.Ordinal).ToArray()));
        }
        if (components.Count == 0 || direct.EnumerateObject().Count() != components.Count(component =>
            component.Role == "direct-runtime"))
            throw new EditorAssetException("editor-closure-incomplete");
        using var inventory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(inventory))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            WriteComponents(writer, components);
            writer.WriteEndObject();
        }
        string closureSha256 = Hash(inventory.ToArray());
        using var review = new MemoryStream();
        using (var writer = new Utf8JsonWriter(review, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("status", "proposed");
            writer.WriteString("closureSha256", closureSha256);
            writer.WriteString("lockSha256", Hash(await File.ReadAllBytesAsync(Path.Combine(directory, "package-lock.json"))));
            writer.WriteString("manifestSha256", Hash(await File.ReadAllBytesAsync(Path.Combine(directory, "package.json"))));
            writer.WriteString("noticesSha256", Hash(notices.ToArray()));
            WriteComponents(writer, components);
            writer.WriteEndObject();
        }
        Directory.CreateDirectory(output);
        await File.WriteAllBytesAsync(Path.Combine(output, "closure-review.json"), review.ToArray());
        await File.WriteAllBytesAsync(Path.Combine(output, "license-review.txt"), notices.ToArray());
        Console.WriteLine($"EDITOR_CLOSURE_REVIEW components={components.Count} direct={direct.EnumerateObject().Count()} sha256={closureSha256} status=proposed");
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream input, int maximum)
    {
        using var output = new MemoryStream();
        byte[] buffer = new byte[16 * 1024];
        int count;
        while ((count = await input.ReadAsync(buffer)) != 0)
        {
            if (output.Length + count > maximum) throw new EditorAssetException("editor-package-limit");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void WriteComponents(Utf8JsonWriter writer, IEnumerable<EditorComponent> components)
    {
        writer.WriteStartArray("components");
        foreach (EditorComponent component in components)
        {
            writer.WriteStartObject();
            writer.WriteString("name", component.Name);
            writer.WriteString("version", component.Version);
            writer.WriteString("role", component.Role);
            writer.WriteString("license", component.License);
            writer.WriteString("integrity", component.Integrity);
            writer.WriteString("source", component.Source);
            writer.WriteStartArray("licenses");
            foreach (EditorLicense license in component.Licenses)
            {
                writer.WriteStartObject();
                writer.WriteString("name", license.Name);
                writer.WriteString("sha256", license.Sha256);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private sealed record EditorLicense(string Name, string Sha256);
    private sealed record EditorComponent(string Name, string Version, string Role, string License,
        string Integrity, string Source, IReadOnlyList<EditorLicense> Licenses);
    private sealed class EditorAssetException(string code) : Exception(code)
    {
        internal string Code { get; } = code;
    }
}
