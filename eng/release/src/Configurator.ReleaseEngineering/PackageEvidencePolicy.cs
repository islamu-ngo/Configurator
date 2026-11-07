using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Configurator.ReleaseEngineering;

internal sealed record PackageEvidenceOperands(string Package, string Manifest, string Capture,
    string Source, string Build, string Payload, string PayloadPrefix, string Evidence, string EvidenceDigest,
    string Checks, string Client, string PackageRid, string ExpectedDigest)
{
    internal string[] NativeArguments =>
    [
        "--package", Package, "--manifest", Manifest, "--capture", Capture, "--source", Source, "--build", Build,
        "--payload", Payload, "--payload-prefix", PayloadPrefix, "--evidence", Evidence, "--evidence-digest", EvidenceDigest,
        "--checks", Checks, "--required-checks", PackageEvidencePolicy.RequiredOwners,
        "--client", Client, "--package-rid", PackageRid, "--expected-digest", ExpectedDigest
    ];
}

internal sealed record CapturedRepositoryInput(string Path, long Length, string Sha256, string Kind);
internal sealed record PackageEvidenceBinding(string ProvenanceDigest, string ProductSourceInputsDigest,
    string BuildRecipeDigest, string PayloadDigest, string EvidenceSetDigest, string Client, string PackageRid,
    IReadOnlyList<CapturedRepositoryInput> RepositoryInputs, IReadOnlyList<string> RepositoryNamespace);

/// <summary>
/// Binds a successful native verification to independently supplied identities. This pure policy
/// does not interpret claimed reports as execution proof or replace native private-session verification.
/// </summary>
internal static class PackageEvidencePolicy
{
    internal const int MaximumJsonBytes = 16 * 1024 * 1024;
    internal const int MaximumInputs = 10000;
    internal const int MaximumRepositoryNames = 100_000;
    internal const int MaximumRepositoryNameLength = 4096;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal const string RequiredOwners = "Configurator.AgentWorkflow,Configurator.Architecture,Configurator.Artifacts," +
        "Configurator.Browser,Configurator.Cli,Configurator.Core,Configurator.Desktop,Configurator.Presentation," +
        "Configurator.ReleaseEngineering,Configurator.Terminal";
    private static readonly string[] Owners = RequiredOwners.Split(',');
    private static readonly string[] OptionNames = ["--package", "--manifest", "--capture", "--source", "--build",
        "--payload", "--payload-prefix", "--evidence", "--evidence-digest", "--checks", "--client", "--package-rid", "--expected-digest"];
    private static readonly string[] DigestOptions = ["--source", "--build", "--payload", "--evidence-digest", "--expected-digest"];

    internal static PackageEvidenceOperands? ParseArguments(string[] arguments)
    {
        if (arguments.Length != OptionNames.Length * 2) return null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Length; index += 2)
            if (!OptionNames.Contains(arguments[index], StringComparer.Ordinal) ||
                string.IsNullOrWhiteSpace(arguments[index + 1]) ||
                !values.TryAdd(arguments[index], arguments[index + 1])) return null;
        if (!DigestOptions.All(key => Digest(values[key]))) return null;
        return new(values["--package"], values["--manifest"], values["--capture"], values["--source"],
            values["--build"], values["--payload"], values["--payload-prefix"], values["--evidence"],
            values["--evidence-digest"], values["--checks"], values["--client"], values["--package-rid"],
            values["--expected-digest"]);
    }

    internal static PackageEvidenceBinding Bind(TrustedBundleRuntime.VerifiedPackageEvidence verified,
        PackageEvidenceOperands expected)
    {
        using JsonDocument manifest = Document(verified.Manifest);
        JsonElement value = manifest.RootElement;
        Require(Hash(verified.Manifest) == expected.ExpectedDigest && Text(value, "schemaVersion") == "0.0.1"
            && Text(value, "productSourceInputsDigest") == expected.Source
            && Text(value, "buildRecipeDigest") == expected.Build
            && Text(value, "payloadDigest") == expected.Payload
            && Text(value, "evidenceSetDigest") == expected.EvidenceDigest
            && Text(value, "client") == expected.Client && Text(value, "packageRid") == expected.PackageRid
            && Text(value, "payloadPrefix") == expected.PayloadPrefix
            && Text(value, "publicationCaptureSha256") == Hash(verified.PublicationCapture));
        JsonElement[] checks = value.GetProperty("requiredChecks").EnumerateArray().ToArray();
        Require(checks.Length == Owners.Length && verified.CheckCaptures.Count == Owners.Length);
        var inputs = new Dictionary<string, CapturedRepositoryInput>(StringComparer.Ordinal);
        string[]? repositoryNamespace = null;
        HashSet<string>? namespaceSet = null;
        ReadRepositoryInputs(verified.PublicationCapture, expected.Source, expected.Build);
        for (int index = 0; index < Owners.Length; index++)
        {
            Require(Text(checks[index], "owner") == Owners[index]
                && verified.CheckCaptures[index].Owner == Owners[index]
                && Text(checks[index], "testCaptureSha256") == Hash(verified.CheckCaptures[index].Bytes));
            ReadRepositoryInputs(verified.CheckCaptures[index].Bytes, null, null);
        }
        Require(inputs.Count > 0);
        return new(expected.ExpectedDigest, expected.Source, expected.Build, expected.Payload, expected.EvidenceDigest,
            expected.Client, expected.PackageRid, inputs.Values.OrderBy(input => input.Path, StringComparer.Ordinal).ToList().AsReadOnly(),
            Array.AsReadOnly(repositoryNamespace!));

        void ReadRepositoryInputs(byte[] bytes, string? source, string? build)
        {
            using JsonDocument capture = Document(bytes);
            JsonElement root = capture.RootElement;
            if (source is not null) Require(Text(root, "productSourceInputsDigest") == source && Text(root, "buildRecipeDigest") == build);
            JsonElement declaredNames = root.GetProperty("repositoryNamespace");
            Require(declaredNames.GetArrayLength() <= MaximumRepositoryNames);
            string[] names = declaredNames.EnumerateArray().Select(name => name.GetString()!).ToArray();
            ValidateRepositoryNames(names);
            if (repositoryNamespace is null)
            {
                repositoryNamespace = names;
                namespaceSet = names.ToHashSet(StringComparer.Ordinal);
            }
            else Require(names.SequenceEqual(repositoryNamespace, StringComparer.Ordinal));
            // Product projections compare graphs; exact candidate binding also covers test inputs.
            JsonElement capturedInputs = root.GetProperty("inputs");
            var local = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement item in capturedInputs.EnumerateArray())
            {
                string path = Text(item, "path");
                if (!path.StartsWith("repo/", StringComparison.Ordinal)) continue;
                Require(RelativeRepositoryPath(path[5..]) && namespaceSet!.Contains(path)
                    && local.Count < MaximumInputs && local.Add(path));
                long length = item.GetProperty("length").GetInt64();
                string digest = Text(item, "sha256");
                Require(length is >= 0 and <= 512L * 1024 * 1024 && Digest(digest));
                var input = new CapturedRepositoryInput(path[5..], length, digest, Text(item, "kind"));
                if (inputs.TryGetValue(input.Path, out CapturedRepositoryInput? existing)) Require(existing == input);
                else { Require(inputs.Count < MaximumInputs); inputs.Add(input.Path, input); }
            }
            Require(local.Count > 0);
        }
    }

    internal static void ValidateRepositoryNames(IReadOnlyList<string>? names)
    {
        if (names is null) throw new InvalidDataException("candidate_repository_namespace_missing");
        Require(names.Count is > 0 and <= MaximumRepositoryNames);
        string? previous = null;
        foreach (string name in names)
        {
            Require(IsRepositoryAlias(name) && (previous is null || StringComparer.Ordinal.Compare(previous, name) < 0));
            _ = StrictUtf8.GetByteCount(name);
            previous = name;
        }
    }

    internal static bool IsRepositoryAlias(string? name) => name is not null
        && name.StartsWith("repo/", StringComparison.Ordinal) && name.Length <= MaximumRepositoryNameLength
        && !name.Contains('\\') && !name.Contains(':') && !name.Contains('\0')
        && !Path.IsPathRooted(name[5..])
        && name[5..].Split('/').All(part => part.Length > 0 && part is not ("." or ".."));

    private static JsonDocument Document(byte[] bytes)
    {
        Require(bytes.Length is > 0 and <= MaximumJsonBytes);
        return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
    }
    internal static bool RelativeRepositoryPath(string path) => path.Length is > 0 and <= 1024
        && !path.Contains('\\') && !path.Contains(':') && !path.Any(char.IsControl)
        && path.Split('/').All(part => part.Length > 0 && part is not ("." or ".." or ".git"));
    private static string Text(JsonElement element, string property) => element.GetProperty(property).GetString() ?? throw new InvalidDataException();
    private static bool Digest(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition) { if (!condition) throw new InvalidDataException("package_evidence_binding_invalid"); }
}
