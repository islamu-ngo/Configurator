using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Configurator.ReleaseEngineering;

/// <summary>
/// Stages authenticated runtime bytes before execution. Unsigned assembly is a separate
/// data operation and never creates a promotion receipt, signature, or signer root.
/// </summary>
public sealed class TrustedBundleRuntime : IDisposable
{
    internal const string Engine = "Configurator.ReleaseEngineering";
    internal const string PromotionSigners = Engine + ".promotion-allowed-signers";
    internal const string ProfilePrefix = "bin/package-verification/";
    private static readonly string[] RuntimeAssetGroups = ["runtime", "native", "resources", "runtimeTargets"];
    private static readonly string[] VerificationTools = ["provenance/Configurator.PackageProvenance",
        "capture/Configurator.BuildCapture", "support/Configurator.SupportQualification", "evidence/Configurator.TestEvidence"];
    private static readonly string[] AdmissionFiles = ["admission/native-release-capabilities.json",
        "admission/browser-release-capabilities.json", "licenses/Configurator.LICENSE"];
    private readonly string directory;

    private TrustedBundleRuntime(string directory) => this.directory = directory;

    internal static TrustedBundleRuntime Stage(VerifiedTrustedBundle bundle, string candidate, bool packageVerification)
    {
        string stage = Path.Combine(Path.GetTempPath(), $"islamu-promoted-{Guid.NewGuid():N}");
        Require(!Overlaps(stage, candidate) && !Overlaps(stage, bundle.Root) && !Overlaps(stage, AppContext.BaseDirectory));
        SafeParents(stage);
        byte[] manifest = ReadFile(Path.Combine(bundle.Root, "trusted-bundle.manifest.json"));
        Require(Hash(manifest) == bundle.ManifestDigest);
        Dictionary<string, byte[]> snapshot = ReadManifestFiles(bundle.Root, manifest);
        string prefix = packageVerification ? ProfilePrefix : "bin/";
        Dictionary<string, byte[]> runtime = snapshot.Where(item => item.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(item => item.Key[prefix.Length..], item => item.Value, StringComparer.Ordinal);
        if (packageVerification) ValidateProfile(runtime);
        else
        {
            Require(!runtime.ContainsKey(PromotionSigners));
            ValidateRuntime(runtime, Engine);
        }
        try
        {
            CreatePrivateDirectory(stage);
            foreach ((string path, byte[] bytes) in runtime)
                WriteNew(stage, path, bytes);
            if (!packageVerification)
            {
                string installed = Path.GetFullPath(AppContext.BaseDirectory);
                Require(!Overlaps(installed, candidate) && !Overlaps(installed, bundle.Root));
                WriteNew(stage, PromotionSigners, ReadFile(Path.Combine(installed, PromotionSigners)));
            }
            return new(stage);
        }
        catch
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
            throw;
        }
    }

    internal int RunEngine(string[] arguments, string candidate, TimeSpan timeout) =>
        Run(Engine + ".dll", arguments, candidate, timeout);

    internal VerifiedPackageEvidence? VerifyPackage(PackageEvidenceOperands operands, IReadOnlyList<string> repositoryNames)
    {
        PackageEvidencePolicy.ValidateRepositoryNames(repositoryNames);
        byte[] namespaceBytes = JsonSerializer.SerializeToUtf8Bytes(repositoryNames);
        Require(namespaceBytes.Length <= PackageEvidencePolicy.MaximumJsonBytes);
        const string namespaceFile = "candidate-repository-names.json";
        WriteNew(directory, namespaceFile, namespaceBytes);
        string namespacePath = Path.Combine(directory, namespaceFile);
        var snapshots = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        byte[] Read(string path)
        {
            byte[] bytes = ReadFile(path);
            snapshots.Add(path, bytes);
            return bytes;
        }
        byte[] manifest = Read(operands.Manifest);
        byte[] publication = Read(Path.Combine(operands.Capture, "capture.json"));
        using JsonDocument checks = JsonDocument.Parse(Read(operands.Checks));
        if (checks.RootElement.GetArrayLength() != 10) return null;
        var captures = checks.RootElement.EnumerateArray().OrderBy(check => check.GetProperty("owner").GetString(), StringComparer.Ordinal)
            .Select(check =>
            {
                string session = check.GetProperty("session").GetString()!;
                return new CheckCapture(check.GetProperty("owner").GetString()!, session, Read(Path.Combine(session, "capture.json")));
            }).ToList().AsReadOnly();
        var elapsed = Stopwatch.StartNew();
        TimeSpan remaining;
        foreach (string session in new[] { operands.Capture }.Concat(captures.Select(capture => capture.Session)))
        {
            remaining = TimeSpan.FromMinutes(3) - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero || Run("capture/Configurator.BuildCapture.dll",
                ["--verify", session, "--repository-names", namespacePath], directory, remaining) != 0) return null;
        }
        remaining = TimeSpan.FromMinutes(3) - elapsed.Elapsed;
        if (remaining <= TimeSpan.Zero) return null;
        if (Run("provenance/Configurator.PackageProvenance.dll", ["--verify", .. operands.NativeArguments], directory,
            remaining) != 0) return null;
        if (!ReadFile(namespacePath).AsSpan().SequenceEqual(namespaceBytes)
            || snapshots.Any(item => !ReadFile(item.Key).AsSpan().SequenceEqual(item.Value))) return null;
        return VerifiedPackageEvidence.Create(manifest, publication, captures);
    }

    internal sealed record CheckCapture(string Owner, string Session, byte[] Bytes);

    internal sealed class VerifiedPackageEvidence
    {
        private VerifiedPackageEvidence(byte[] manifest, byte[] publication, IReadOnlyList<CheckCapture> checks)
        {
            Manifest = manifest;
            PublicationCapture = publication;
            CheckCaptures = checks;
        }
        internal byte[] Manifest { get; }
        internal byte[] PublicationCapture { get; }
        internal IReadOnlyList<CheckCapture> CheckCaptures { get; }
        internal static VerifiedPackageEvidence Create(byte[] manifest, byte[] publication, IReadOnlyList<CheckCapture> checks) =>
            new(manifest, publication, checks);
    }

    private int Run(string entry, string[] arguments, string workingDirectory, TimeSpan budget)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(ReleaseToolPaths.Dotnet)
            {
                WorkingDirectory = workingDirectory, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true,
                RedirectStandardInput = true, CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add(Path.Combine(directory, entry));
        foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        foreach (string name in process.StartInfo.Environment.Keys.Where(name =>
            name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("COMPlus_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("CORECLR_", StringComparison.OrdinalIgnoreCase)).ToArray())
            process.StartInfo.Environment.Remove(name);
        process.Start();
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(budget);
        Task stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
        Task stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
        try
        {
            Task.WhenAll(process.WaitForExitAsync(timeout.Token), stdout, stderr).GetAwaiter().GetResult();
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw;
        }
    }

    /// <summary>Assembles create-only unsigned tooling from a base inventory and a fixed compiled profile.</summary>
    public static int Assemble(string[] arguments, TextWriter output)
    {
        if (arguments is not ["--base", var basePath, "--profile", var profilePath, "--output", var outputPath])
        {
            output.WriteLine("unsigned_bundle_arguments_invalid");
            return Program.UsageError;
        }
        string? created = null;
        try
        {
            string source = Path.GetFullPath(basePath), profile = Path.GetFullPath(profilePath),
                destination = Path.GetFullPath(outputPath);
            Require(!Overlaps(source, profile) && !Overlaps(destination, source) && !Overlaps(destination, profile)
                && !Directory.Exists(destination) && !File.Exists(destination));
            SafeParents(destination);
            byte[] original = ReadFile(Path.Combine(source, "trusted-bundle.manifest.json"));
            Dictionary<string, byte[]> files = ReadManifestFiles(source, original);
            Require(!files.Keys.Any(path => path.StartsWith(ProfilePrefix, StringComparison.Ordinal)));
            Dictionary<string, byte[]> verifier = ReadTree(profile);
            ValidateProfile(verifier);
            foreach ((string path, byte[] bytes) in verifier) files.Add(ProfilePrefix + path, bytes);
            CheckBounds(files);
            using JsonDocument document = JsonDocument.Parse(original);
            Dictionary<string, object?> fields = document.RootElement.EnumerateObject()
                .Where(property => property.Name != "files")
                .ToDictionary(property => property.Name, property => (object?)property.Value.Clone(), StringComparer.Ordinal);
            fields["files"] = files.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new { path = item.Key, sha256 = Hash(item.Value) }).ToArray();
            ArtifactPolicyResult canonical = ReleaseArtifactPolicy.NormalizeJson(JsonSerializer.Serialize(fields));
            Require(canonical.IsValid && canonical.Bytes is not null);
            created = Path.Combine(Path.GetDirectoryName(destination)!, ".unsigned-bundle-" + Guid.NewGuid().ToString("N"));
            CreatePrivateDirectory(created);
            foreach ((string path, byte[] bytes) in files) WriteNew(created, path, bytes);
            WriteNew(created, "trusted-bundle.manifest.json", canonical.Bytes!);
            Directory.Move(created, destination);
            output.WriteLine("unsigned_bundle_assembled: " + Hash(canonical.Bytes!));
            created = null;
            return Program.Success;
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException
            or JsonException or InvalidOperationException or KeyNotFoundException or OverflowException
            or OperationCanceledException or System.ComponentModel.Win32Exception or FormatException)
        {
            output.WriteLine("unsigned_bundle_rejected");
            return Program.ToolchainRejected;
        }
        finally { if (created is not null && Directory.Exists(created)) Directory.Delete(created, true); }
    }

    private static void ValidateProfile(Dictionary<string, byte[]> files)
    {
        Require(!files.Keys.Any(path => path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
            || path.Contains("allowed-signers", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".runtimeconfig.dev.json", StringComparison.OrdinalIgnoreCase)));
        var required = new HashSet<string>(AdmissionFiles, StringComparer.Ordinal);
        foreach (string tool in VerificationTools)
            required.UnionWith(ValidateRuntime(files, tool));
        Require(required.SetEquals(files.Keys));
        foreach (string path in AdmissionFiles) Require(files.TryGetValue(path, out byte[]? bytes) && bytes.Length > 0);
    }

    private static HashSet<string> ValidateRuntime(Dictionary<string, byte[]> files, string entry)
    {
        var required = new HashSet<string>(StringComparer.Ordinal)
        {
            entry + ".dll", entry + ".deps.json", entry + ".runtimeconfig.json"
        };
        Require(required.All(files.ContainsKey));
        using JsonDocument config = JsonDocument.Parse(files[entry + ".runtimeconfig.json"]);
        Require(!config.RootElement.GetProperty("runtimeOptions").TryGetProperty("additionalProbingPaths", out _));
        using JsonDocument deps = JsonDocument.Parse(files[entry + ".deps.json"]);
        string target = deps.RootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
        string prefix = entry.Contains('/') ? entry[..(entry.LastIndexOf('/') + 1)] : string.Empty;
        foreach (JsonProperty library in deps.RootElement.GetProperty("targets").GetProperty(target).EnumerateObject())
            foreach (string group in RuntimeAssetGroups.Where(group => library.Value.TryGetProperty(group, out _)))
                foreach (JsonProperty asset in library.Value.GetProperty(group).EnumerateObject())
                {
                    Require(Relative(asset.Name));
                    string name = Path.GetFileName(asset.Name);
                    string relative = prefix + (group == "resources"
                        ? asset.Value.GetProperty("locale").GetString() + "/" + name : name);
                    Require(Relative(relative) && files.ContainsKey(relative));
                    required.Add(relative);
                }
        return required;
    }

    private static Dictionary<string, byte[]> ReadManifestFiles(string root, byte[] manifest)
    {
        Require(manifest.Length <= TrustedBundlePolicy.MaximumManifestBytes);
        ArtifactPolicyResult canonical = ReleaseArtifactPolicy.NormalizeJson(Encoding.UTF8.GetString(manifest));
        Require(canonical.IsValid && canonical.Bytes is not null && manifest.AsSpan().SequenceEqual(canonical.Bytes));
        using JsonDocument document = JsonDocument.Parse(manifest);
        Require(document.RootElement.GetProperty("schemaVersion").GetString() == "trusted-bundle.v1");
        Dictionary<string, string> declared = document.RootElement.GetProperty("files").EnumerateArray()
            .ToDictionary(item => item.GetProperty("path").GetString()!, item => item.GetProperty("sha256").GetString()!,
                StringComparer.Ordinal);
        Dictionary<string, byte[]> files = ReadTree(root);
        Require(files.Remove("trusted-bundle.manifest.json") && declared.Count == files.Count
            && files.All(item => declared.TryGetValue(item.Key, out string? digest) && digest == Hash(item.Value)));
        return files;
    }

    private static Dictionary<string, byte[]> ReadTree(string root)
    {
        Require(Directory.Exists(root));
        SafeParents(root);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int visited = 0;
        long total = 0;
        void Walk(string directory, int depth)
        {
            Require(depth <= TrustedBundlePolicy.MaximumPathDepth);
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                Require(++visited <= TrustedBundlePolicy.MaximumEnumeratedEntries);
                Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0);
                if (Directory.Exists(path)) { Walk(path, depth + 1); continue; }
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                Require(Relative(relative) && names.Add(relative) && files.Count < TrustedBundlePolicy.MaximumBundleFiles + 1);
                long length = new FileInfo(path).Length;
                Require(length <= TrustedBundlePolicy.MaximumFileBytes && (total += length) <= TrustedBundlePolicy.MaximumTotalBytes);
                files.Add(relative, ReadFile(path));
            }
        }
        Walk(root, 0);
        return files;
    }

    private static void CheckBounds(Dictionary<string, byte[]> files) =>
        Require(files.Count <= TrustedBundlePolicy.MaximumBundleFiles
            && files.Values.Sum(bytes => (long)bytes.Length) <= TrustedBundlePolicy.MaximumTotalBytes);

    private static byte[] ReadFile(string path)
    {
        SafeParents(path);
        return ReleaseArtifactPolicy.ReadRegularFile(path, TrustedBundlePolicy.MaximumFileBytes);
    }

    private static void WriteNew(string root, string relative, byte[] bytes)
    {
        Require(Relative(relative));
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
    }

    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    private static bool Relative(string path) => path.Length is > 0 and <= TrustedBundlePolicy.MaximumPathUtf8Bytes
        && path.All(c => c is >= '!' and <= '~') && !path.Contains('\\') && !path.Contains(':')
        && path.Split('/').All(part => part.Length > 0 && part is not ("." or ".."))
        && path.Split('/').Length <= TrustedBundlePolicy.MaximumPathDepth;
    private static bool Overlaps(string left, string right)
    {
        left = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
        right = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return left.Equals(right, comparison) || left.StartsWith(right + Path.DirectorySeparatorChar, comparison)
            || right.StartsWith(left + Path.DirectorySeparatorChar, comparison);
    }
    private static void SafeParents(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if (File.Exists(current) || Directory.Exists(current))
                Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0);
    }
    private static void Require(bool condition) { if (!condition) throw new InvalidDataException("trusted_runtime_invalid"); }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
