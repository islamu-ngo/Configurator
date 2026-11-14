#:property RestorePackagesWithLockFile=false
#:property PublishAot=false
#:property NuGetLockFilePath=$(BaseIntermediateOutputPath)browser-profile-worker.packages.lock.json
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property AssemblyName=Configurator.BrowserProfileWorkerBuilder
#:property RootNamespace=Configurator.Engineering
#:project Configurator.BuildCapture/Configurator.BuildCapture.csproj

using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.BuildCapture;

return await ProfileWorkerBuilder.RunAsync(args);

internal static class ProfileWorkerBuilder
{
    private const string PackageDirectory = "eng/configurator/profile-worker";
    private const string VendorDirectory = "src/Configurator.Browser.Worker/wwwroot/vendor";
    private const string LegalRecord = "docs/internal/legal/dependencies/encrypted-local-profiles.md";
    private const string Archive = "https://registry.npmjs.org/jose/-/jose-6.2.17.tgz";
    private const string Integrity = "sha512-wOuK9V1yujQFH5S0ZWmLcDZN7+IKh9bUCC2ZPCf2HDlol3WWHy+PPyNjEI75FXRLMp3vDKQcupP59xhVU95hcg==";
    private const string LicenseHash = "8078b0829d6c3e9ecde2f003e1966e4cad3efc6e7a640efbab0313cc166b1af1";
    private const string LockHash = "e0cc75ea65cd7c27dbaeb048dc95a09ce01693f7eb4f175cc26a9f8faed507bb";
    private static readonly string[] Inputs =
    [
        PackageDirectory + "/package.json", PackageDirectory + "/package-lock.json",
        PackageDirectory + "/profile-crypto-entry.js", "eng/configurator/BuildBrowserProfileWorker.cs", LegalRecord
    ];
    private static readonly Dictionary<string, string> Tools = new(StringComparer.Ordinal)
    {
        ["node"] = "v26.13.0", ["npm"] = "12.2.2", ["bun"] = "1.4.2"
    };

    internal static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args is ["--self-test"])
            {
                SelfTest();
                return 0;
            }
            if (args is ["--review-closure", var directory, var output])
            {
                ValidateLock(await ReadAsync(Path.Combine(directory, "package-lock.json")));
                byte[] license = await ReadLicenseAsync();
                Directory.CreateDirectory(output);
                await File.WriteAllBytesAsync(Path.Combine(output, "license-review.txt"), license);
                await File.WriteAllBytesAsync(Path.Combine(output, "closure-review.json"), Describe(writer =>
                {
                    writer.WriteString("status", "implementation-owner-reviewed");
                    WriteIdentity(writer);
                }));
                Console.WriteLine("PROFILE_WORKER_CLOSURE components=1 transitive=0 license=MIT");
                return 0;
            }
            if (args is [var operation, var root] && operation is "--write" or "--check")
            {
                await BuildAsync(Path.GetFullPath(root), operation == "--check");
                return 0;
            }
            Console.Error.WriteLine("Usage: BuildBrowserProfileWorker.cs --self-test | --review-closure directory output | (--write|--check) root");
            return 64;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
            or HttpRequestException or InvalidDataException or InvalidOperationException or FormatException
            or KeyNotFoundException or ArgumentException or TaskCanceledException)
        {
            Console.Error.WriteLine("profile-worker-asset-invalid");
            return 1;
        }
    }

    private static async Task BuildAsync(string root, bool check)
    {
        string input = Path.Combine(root, PackageDirectory);
        var inputs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Inputs) inputs.Add(path, Hash(await ReadAsync(Path.Combine(root, path))));
        ValidateLock(await ReadAsync(Path.Combine(input, "package-lock.json")));
        using (JsonDocument package = JsonDocument.Parse(await ReadAsync(Path.Combine(input, "package.json"))))
        {
            JsonElement dependencies = package.RootElement.GetProperty("dependencies");
            Require(dependencies.EnumerateObject().Count() == 1 &&
                dependencies.GetProperty("jose").GetString() == "6.2.17");
        }
        string vendor = Path.Combine(root, VendorDirectory);
        string manifestPath = Path.Combine(vendor, "profile-crypto.manifest.json");
        if (check)
        {
            using JsonDocument manifest = JsonDocument.Parse(await ReadAsync(manifestPath));
            JsonElement metadata = manifest.RootElement;
            ValidateIdentity(metadata);
            Require(metadata.GetProperty("inputs").EnumerateObject().Count() == inputs.Count);
            foreach ((string path, string hash) in inputs)
                Require(metadata.GetProperty("inputs").GetProperty(path).GetString() == hash);
            foreach ((string name, string version) in Tools)
                Require(metadata.GetProperty("toolchain").GetProperty(name).GetString() == version);
            JsonElement outputs = metadata.GetProperty("outputs");
            Require(outputs.EnumerateObject().Count() == 2);
            foreach (string name in new[] { "profile-crypto.js", "profile-crypto.LICENSE.txt" })
            {
                byte[] bytes = await ReadAsync(Path.Combine(vendor, name));
                ValidateOutput(outputs.GetProperty(name), bytes);
                if (name.EndsWith(".txt", StringComparison.Ordinal)) Require(Hash(bytes) == LicenseHash);
            }
            Console.WriteLine("PROFILE_WORKER_ASSET_CHECK components=1 outputs=2");
            return;
        }

        foreach ((string name, string version) in Tools)
            Require((await RunProcessAsync(name, ["--version"], input)).Trim() == version);
        byte[] license = await ReadLicenseAsync();
        await RunProcessAsync("npm", ["ci", "--ignore-scripts", "--no-audit", "--no-fund"], input);
        Require(Hash(await ReadAsync(Path.Combine(input, "node_modules/jose/LICENSE.md"))) == LicenseHash);
        string temporary = Path.Combine(Path.GetTempPath(), "configurator-profile-worker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        byte[] bundle;
        try
        {
            string target = Path.Combine(temporary, "profile-crypto.js");
            await RunProcessAsync("bun", ["build", "profile-crypto-entry.js", "--target", "browser",
                "--format", "esm", "--minify", "--outfile", target], input);
            bundle = Encoding.UTF8.GetBytes("/*! Original component terms: profile-crypto.LICENSE.txt */\n")
                .Concat(await File.ReadAllBytesAsync(target)).ToArray();
        }
        finally { Directory.Delete(temporary, true); }
        byte[] generated = Describe(writer =>
        {
            WriteIdentity(writer);
            writer.WriteStartObject("toolchain");
            foreach ((string name, string version) in Tools) writer.WriteString(name, version);
            writer.WriteEndObject();
            writer.WriteStartObject("inputs");
            foreach ((string path, string hash) in inputs) writer.WriteString(path, hash);
            writer.WriteEndObject();
            writer.WriteStartObject("outputs");
            foreach ((string name, byte[] bytes) in new[]
            {
                ("profile-crypto.js", bundle), ("profile-crypto.LICENSE.txt", license)
            })
            {
                writer.WriteStartObject(name);
                writer.WriteString("sha256", Hash(bytes));
                writer.WriteNumber("bytes", bytes.Length);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        });
        Directory.CreateDirectory(vendor);
        await File.WriteAllBytesAsync(Path.Combine(vendor, "profile-crypto.js"), bundle);
        await File.WriteAllBytesAsync(Path.Combine(vendor, "profile-crypto.LICENSE.txt"), license);
        await File.WriteAllBytesAsync(manifestPath, generated);
        Console.WriteLine($"PROFILE_WORKER_ASSET_BUILT components=1 bytes={bundle.Length}");
    }

    private static void ValidateLock(byte[] bytes)
    {
        Require(Hash(bytes) == LockHash);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Require(root.GetProperty("lockfileVersion").GetInt32() == 3);
        JsonElement packages = root.GetProperty("packages");
        Require(packages.EnumerateObject().Count() == 2);
        JsonElement jose = packages.GetProperty("node_modules/jose");
        Require(jose.GetProperty("version").GetString() == "6.2.17" &&
            jose.GetProperty("resolved").GetString() == Archive &&
            jose.GetProperty("integrity").GetString() == Integrity &&
            jose.GetProperty("license").GetString() == "MIT");
    }

    private static async Task<byte[]> ReadLicenseAsync()
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        using HttpResponseMessage response = await http.GetAsync(Archive, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        byte[] archive = await ReadBoundedAsync(stream, 8 * 1024 * 1024);
        Require(CryptographicOperations.FixedTimeEquals(SHA512.HashData(archive),
            Convert.FromBase64String(Integrity["sha512-".Length..])));
        using var memory = new MemoryStream(archive, false);
        using var gzip = new GZipStream(memory, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        byte[]? license = null;
        int count = 0;
        while (tar.GetNextEntry() is { } entry)
        {
            Require(++count <= 10_000);
            if (entry.Name != "package/LICENSE.md") continue;
            Require(license is null && entry.DataStream is not null && entry.Length <= 128 * 1024);
            license = await ReadBoundedAsync(entry.DataStream!, 128 * 1024);
        }
        Require(license is not null && Hash(license) == LicenseHash);
        return license!;
    }

    private static void WriteIdentity(Utf8JsonWriter writer)
    {
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteString("name", "jose");
        writer.WriteString("version", "6.2.17");
        writer.WriteString("source", Archive);
        writer.WriteString("integrity", Integrity);
        writer.WriteString("license", "MIT");
        writer.WriteString("licenseSha256", LicenseHash);
        writer.WriteString("lockSha256", LockHash);
    }

    private static void ValidateIdentity(JsonElement value)
    {
        using JsonDocument expected = JsonDocument.Parse(Describe(WriteIdentity));
        foreach (JsonProperty property in expected.RootElement.EnumerateObject())
            Require(value.TryGetProperty(property.Name, out JsonElement actual) &&
                JsonElement.DeepEquals(property.Value, actual));
    }

    private static void ValidateOutput(JsonElement expected, byte[] bytes) =>
        Require(expected.GetProperty("sha256").GetString() == Hash(bytes) &&
            expected.GetProperty("bytes").GetInt32() == bytes.Length);

    private static void SelfTest()
    {
        byte[] identity = Describe(WriteIdentity);
        using JsonDocument valid = JsonDocument.Parse(identity);
        ValidateIdentity(valid.RootElement);
        int rejected = 0;
        foreach (string name in new[] { "schemaVersion", "name", "version", "source", "integrity",
            "license", "licenseSha256", "lockSha256" })
        {
            byte[] changed = Describe(writer =>
            {
                foreach (JsonProperty property in valid.RootElement.EnumerateObject())
                {
                    if (property.Name == name) writer.WriteString(name, "synthetic-tamper");
                    else property.WriteTo(writer);
                }
            });
            using JsonDocument mutation = JsonDocument.Parse(changed);
            Reject(() => ValidateIdentity(mutation.RootElement));
        }
        byte[] content = "synthetic-bundle"u8.ToArray();
        using JsonDocument output = JsonDocument.Parse(Describe(writer =>
        {
            writer.WriteString("sha256", Hash(content));
            writer.WriteNumber("bytes", content.Length);
        }));
        ValidateOutput(output.RootElement, content);
        Reject(() => ValidateOutput(output.RootElement, "synthetic-tamper"u8.ToArray()));
        Reject(() => ValidateLock("{}"u8.ToArray()));
        Console.WriteLine($"PROFILE_WORKER_SELF_TEST passed={rejected + 2} rejected={rejected} network=none");
        void Reject(Action action)
        {
            bool failed = false;
            try { action(); }
            catch (InvalidDataException) { failed = true; }
            Require(failed);
            rejected++;
        }
    }

    private static byte[] Describe(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream input, int maximum)
    {
        using var output = new MemoryStream();
        byte[] buffer = new byte[16 * 1024];
        int count;
        while ((count = await input.ReadAsync(buffer)) != 0)
        {
            Require(output.Length + count <= maximum);
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private static Task<byte[]> ReadAsync(string path)
    {
        CaptureSession.Consume(path);
        return File.ReadAllBytesAsync(path);
    }

    private static async Task<string> RunProcessAsync(string command, string[] arguments, string directory)
    {
        var start = new ProcessStartInfo(command)
        {
            WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidDataException();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(output, error);
        Require(process.ExitCode == 0);
        return await output;
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException();
    }
}
