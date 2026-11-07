using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.ReleaseEngineering;

namespace Configurator.ReleaseEngineering.Tests;

[NotInParallel("RuntimePromotionTrustRoot")]
public sealed class TrustedVerifierPackagingTests
{
    private static readonly string[] CaptureAssets = ["Configurator.BuildCapture.deps.json", "Configurator.BuildCapture.dll",
        "Configurator.BuildCapture.runtimeconfig.json"];

    [Test]
    public async Task CaptureVerificationRunsOutsideRepositoryWithoutSessionExecutablesOrSdkAssemblies()
    {
        string root = Path.Combine(Path.GetTempPath(), "verifier-closure-" + Guid.NewGuid().ToString("N"));
        try
        {
            string repository = Path.Combine(root, "candidate");
            PackageEvidenceFixture.WriteSources(repository);
            using var fixture = new PackageEvidenceFixture(Path.Combine(root, "records"), repository);
            string stage = Path.Combine(root, "stage");
            Directory.CreateDirectory(stage);
            string built = Path.Combine(PackageEvidenceFixture.Tools, "capture");
            foreach (string name in CaptureAssets)
                File.Copy(Path.Combine(built, name), Path.Combine(stage, name));
            foreach (string session in fixture.Sessions)
            {
                foreach (string relative in new[] { "private/tool/Configurator.BuildCapture.dll", "frozen/sdk/dotnet",
                    "frozen/sdk/sdk/10.0.302/Microsoft.Build.Framework.dll" })
                {
                    string poison = Path.Combine(session, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(poison)!);
                    File.WriteAllText(poison, "must never execute or load");
                }
            }
            File.WriteAllText(Path.Combine(repository, "dotnet"), "must never execute");
            string executable = Path.Combine(stage, "Configurator.BuildCapture.dll");
            var capture = await PackageEvidenceFixture.Run(executable, stage, "--verify", fixture.Session);
            await Assert.That(capture.ExitCode).IsEqualTo(0).Because(capture.Output);
            string owner = "Configurator.Architecture";
            var check = await PackageEvidenceFixture.Run(executable, stage, "--verify-check",
                fixture.CheckSession(owner), Path.Combine(fixture.Evidence, owner + ".execution.json"),
                Path.Combine(fixture.Evidence, owner + ".report.json"));
            await Assert.That(check.ExitCode).IsEqualTo(0).Because(check.Output);
            await Assert.That(check.Output).Contains("CAPTURE_CHECK_VALID");
            await Assert.That(Directory.GetFiles(stage).Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray())
                .IsEquivalentTo(CaptureAssets);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Test]
    public async Task AuthenticatedCompiledProfileIgnoresInjectedRuntimeAndUntrustedToolLocations()
    {
        using GovernedReleaseFixture fixture = GovernedReleaseFixture.CreateSha1();
        PackageEvidenceFixture evidence = fixture.EvidenceFor("1.1.0");
        foreach (string session in evidence.Sessions)
            foreach (string relative in new[] { "private/tool/Configurator.BuildCapture.dll", "frozen/sdk/dotnet" })
            {
                string path = Path.Combine(session, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "untrusted executable must not load");
            }
        string candidateTool = Path.Combine(fixture.RepositoryPath, "eng/configurator/VerifyPackageProvenance.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(candidateTool)!);
        File.WriteAllText(candidateTool, "throw new System.Exception(\"candidate code executed\");");
        var injection = new Dictionary<string, string>
        {
            ["DOTNET_STARTUP_HOOKS"] = Path.Combine(fixture.RepositoryPath, "untrusted.dll"),
            ["DOTNET_ADDITIONAL_DEPS"] = Path.Combine(fixture.RepositoryPath, "untrusted.deps.json"),
            ["DOTNET_SHARED_STORE"] = fixture.RepositoryPath,
            ["CORECLR_ENABLE_PROFILING"] = "1",
            ["COMPlus_ReadyToRun"] = "0"
        };
        Dictionary<string, string?> originals = injection.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach ((string key, string value) in injection) Environment.SetEnvironmentVariable(key, value);
            using TrustedBundleRuntime runtime = TrustedBundleRuntime.Stage(Admit(fixture), fixture.RepositoryPath, packageVerification: true);
            PackageEvidenceOperands operands = PackageEvidencePolicy.ParseArguments(evidence.Arguments)!;

            TrustedBundleRuntime.VerifiedPackageEvidence? verified = runtime.VerifyPackage(operands, evidence.RepositoryNames);

            await Assert.That(verified).IsNotNull();
            await Assert.That(PackageEvidencePolicy.Bind(verified!, operands).PayloadDigest).IsEqualTo(evidence.Payload);
        }
        finally { foreach ((string key, string? value) in originals) Environment.SetEnvironmentVariable(key, value); }
    }

    [Test]
    [Arguments("missing")]
    [Arguments("probing")]
    [Arguments("escaping-dependency")]
    [Arguments("pdb")]
    [Arguments("signers")]
    public async Task AuthenticatedProfileRejectsIncompleteOrUnadmittedRuntimeAssets(string attack)
    {
        using GovernedReleaseFixture fixture = GovernedReleaseFixture.CreateSha1();
        string profile = Path.Combine(fixture.Root, "bundle/bin/package-verification");
        if (attack == "missing") File.Delete(Path.Combine(profile, "support/Configurator.SupportQualification.dll"));
        if (attack == "pdb") File.WriteAllText(Path.Combine(profile, "capture/private.pdb"), "private debug data");
        if (attack == "signers") File.WriteAllText(Path.Combine(profile, "capture/allowed-signers"), "not an authority");
        if (attack == "probing")
        {
            string path = Path.Combine(profile, "provenance/Configurator.PackageProvenance.runtimeconfig.json");
            JsonNode document = JsonNode.Parse(File.ReadAllBytes(path))!;
            document["runtimeOptions"]!["additionalProbingPaths"] = new JsonArray(fixture.RepositoryPath);
            File.WriteAllText(path, document.ToJsonString());
        }
        if (attack == "escaping-dependency")
        {
            string path = Path.Combine(profile, "capture/Configurator.BuildCapture.deps.json");
            JsonNode document = JsonNode.Parse(File.ReadAllBytes(path))!;
            string target = document["runtimeTarget"]!["name"]!.GetValue<string>();
            JsonObject assets = document["targets"]![target]!["Configurator.BuildCapture/0.0.1"]!["runtime"]!.AsObject();
            assets["../../Configurator.BuildCapture.dll"] = new JsonObject();
            File.WriteAllText(path, document.ToJsonString());
        }
        fixture.ReadmitBundle();
        VerifiedTrustedBundle admitted = Admit(fixture);

        await Assert.That(() => TrustedBundleRuntime.Stage(admitted, fixture.RepositoryPath, packageVerification: true))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task UnsignedAssemblerIsCreateOnlyAndLeavesProductionAuthorityInactive()
    {
        using GovernedReleaseFixture fixture = GovernedReleaseFixture.CreateSha1();
        string source = Path.Combine(fixture.Root, "bundle");
        string basis = Path.Combine(fixture.Root, "unsigned-base");
        string destination = Path.Combine(fixture.Root, "assembled");
        foreach (string path in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, path).Replace('\\', '/');
            if (relative.StartsWith("bin/package-verification/", StringComparison.Ordinal)) continue;
            string copy = Path.Combine(basis, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(path, copy);
        }
        string manifestPath = Path.Combine(basis, "trusted-bundle.manifest.json");
        JsonNode manifest = JsonNode.Parse(File.ReadAllBytes(manifestPath))!;
        manifest["files"] = new JsonArray(manifest["files"]!.AsArray()
            .Where(item => !item!["path"]!.GetValue<string>().StartsWith("bin/package-verification/", StringComparison.Ordinal))
            .Select(item => item!.DeepClone()).ToArray());
        File.WriteAllBytes(manifestPath, ReleaseArtifactPolicy.NormalizeJson(manifest.ToJsonString()).Bytes!);
        string[] arguments = ["--base", basis, "--profile", PackageEvidenceFixture.Tools, "--output", destination];
        using var output = new StringWriter();

        int code = TrustedBundleRuntime.Assemble(arguments, output);

        await Assert.That(code).IsEqualTo(Program.Success).Because(output.ToString());
        byte[] first = File.ReadAllBytes(Path.Combine(destination, "trusted-bundle.manifest.json"));
        await Assert.That(TrustedBundleRuntime.Assemble(arguments, output)).IsEqualTo(Program.ToolchainRejected);
        await Assert.That(File.ReadAllBytes(Path.Combine(destination, "trusted-bundle.manifest.json"))).IsEquivalentTo(first);
        await Assert.That(Directory.GetFiles(destination, "*.sig", SearchOption.AllDirectories)).IsEmpty();
        await Assert.That(Directory.GetFiles(destination, "*promotion-allowed-signers", SearchOption.AllDirectories)).IsEmpty();
        using RuntimePromotionTrustRootScope inactive = RuntimePromotionTrustRootScope.UsePackagedDefault();
        TrustedBundleResult result = TrustedBundlePolicy.Verify(Request(fixture) with
        {
            BundleRoot = destination, ExpectedManifestDigest = PackageEvidenceFixture.Hash(first)
        });
        await Assert.That(result.Diagnostic).IsEqualTo("trusted_bundle_promotion_authority_not_configured");
    }

    private static VerifiedTrustedBundle Admit(GovernedReleaseFixture fixture)
    {
        using RuntimePromotionTrustRootScope trust = RuntimePromotionTrustRootScope.Use(Path.Combine(fixture.Root, "authority/allowed-promoters"));
        TrustedBundleResult result = TrustedBundlePolicy.Verify(Request(fixture));
        return result.Bundle ?? throw new InvalidOperationException(result.Diagnostic);
    }

    private static TrustedBundleVerificationRequest Request(GovernedReleaseFixture fixture)
    {
        string bundle = Path.Combine(fixture.Root, "bundle"), authority = Path.Combine(fixture.Root, "authority");
        return new(bundle, fixture.RepositoryPath, new PromotionAuthorityInput(
            Path.Combine(authority, "promotion-receipt.v1.json"), Path.Combine(authority, "promotion-receipt.v1.json.sig"),
            "fixture-tooling-promoter"), "islamu-release-engineering", "1.0.0", "policy-v1", "config-v1", "trust-v1")
        { ExpectedManifestDigest = PackageEvidenceFixture.Hash(File.ReadAllBytes(Path.Combine(bundle, "trusted-bundle.manifest.json"))) };
    }
}

/// <summary>
/// Synthetic private ledgers exercise native verification and candidate binding. They do not
/// represent executed owner tests, consumed build inputs, or a qualified product package.
/// </summary>
internal sealed class PackageEvidenceFixture : IDisposable
{
    internal static readonly string[] Owners = ["AgentWorkflow", "Architecture", "Artifacts", "Browser", "Cli",
        "Core", "Desktop", "Presentation", "ReleaseEngineering", "Terminal"];
    private static readonly string[] ProductOwners = ["Artifacts", "Browser", "Cli", "Core", "Desktop", "Presentation", "Terminal"];
    private static readonly string[] NamespaceRootFiles = ["Directory.Build.props"];
    private static readonly string[] NamespaceDirectories = ["src", "tests", "eng/release/policy"];
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };
    internal const string Prefix = "generated/repo/artifacts/payload/linux-x64/";
    internal const string TestSource = "tests/SharedAssertions.cs";
    internal const string UnconsumedSource = "src/Configurator.Core/Unconsumed.cs";
    internal string Root { get; }
    internal string Package => Path.Combine(Root, "payload");
    internal string Session => Path.Combine(Root, "capture");
    internal string Evidence => Path.Combine(Root, "evidence");
    internal string Manifest => Path.Combine(Root, "provenance.json");
    internal string Checks => Path.Combine(Root, "checks.json");
    internal string Source { get; }
    internal string Build { get; }
    internal string Payload { get; }
    internal string EvidenceDigest { get; private set; } = string.Empty;
    internal string ExpectedDigest { get; private set; } = string.Empty;
    internal IReadOnlyList<string> RepositoryNames { get; }
    internal IEnumerable<string> Sessions => new[] { Session }.Concat(Owners.Select(owner => CheckSession("Configurator." + owner)));
    internal string CheckSession(string owner) => Path.Combine(Root, owner);
    internal static string Tools => Path.Combine(RepositoryRoot.Find(), "artifacts/release-verifier");

    internal static void WriteSources(string repository)
    {
        foreach (string owner in ProductOwners)
        {
            string path = Path.Combine(repository, "src", "Configurator." + owner, "fixture.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "synthetic " + owner + "\n");
        }
        File.WriteAllText(Path.Combine(repository, "Directory.Build.props"), "<Project />\n");
        Directory.CreateDirectory(Path.Combine(repository, "tests"));
        File.WriteAllText(Path.Combine(repository, TestSource), "// Synthetic shared test input.\n");
        File.WriteAllText(Path.Combine(repository, UnconsumedSource), "// Eligible but unconsumed fixture source.\n");
    }

    internal PackageEvidenceFixture(string root, string repository, bool conflictingTestInput = false)
    {
        Root = root;
        Directory.CreateDirectory(Package);
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Path.Combine(Package, "app.txt"), "payload");
        // These are the explicitly modeled fixture roots, not a second production eligibility policy.
        RepositoryNames = NamespaceRootFiles
            .Concat(NamespaceDirectories
                .Where(relative => Directory.Exists(Path.Combine(repository, relative)))
                .SelectMany(relative => Directory.GetFiles(Path.Combine(repository, relative), "*", SearchOption.AllDirectories))
                .Select(path => Path.GetRelativePath(repository, path).Replace('\\', '/')))
            .Select(path => "repo/" + path).Order(StringComparer.Ordinal).ToList().AsReadOnly();
        var inputs = new[] { Input(repository, "Directory.Build.props", "recipe") }
            .Concat(ProductOwners.Select(owner => Input(repository, $"src/Configurator.{owner}/fixture.txt", "compiler")))
            .OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();
        var settings = new[] { new { project = "repo/synthetic.proj", name = "RuntimeIdentifier", value = "linux-x64" } };
        Source = HashJson(inputs.Where(item => item.Kind != "recipe").ToArray());
        Build = HashJson(new { sdkVersion = "10.0.302", workloadVersion = "10.0.301.1",
            inputs = inputs.Where(item => item.Kind == "recipe").ToArray(), settings,
            invocations = Array.Empty<object>(), targets = Array.Empty<object>(),
            movedInputs = Array.Empty<object>(), retiredInputs = Array.Empty<object>() });
        var payload = new[] { new { path = "app.txt", length = 7L, sha256 = Hash("payload"u8.ToArray()) } };
        Payload = HashJson(payload);
        var published = new Identity(Prefix + "app.txt", 7, Hash("payload"u8.ToArray()), "output");
        WriteCapture(Session, "synthetic.proj", [published]);
        var checks = new List<object>();
        var bindings = new List<object>();
        foreach (string name in Owners)
        {
            string owner = "Configurator." + name;
            string session = CheckSession(owner);
            string parent = name == "AgentWorkflow" ? "eng/agent-workflow/tests/" :
                name == "ReleaseEngineering" ? "eng/release/tests/" : "tests/";
            string project = parent + owner + ".Tests/" + owner + ".Tests.csproj";
            var executable = new Identity("generated/repo/" + parent + owner + ".Tests/bin/Release/net10.0/" + owner + ".Tests.dll",
                16, Hash("synthetic binary"u8.ToArray()), "output");
            WriteCapture(session, project, [published, executable], includeTestInput: true,
                conflict: conflictingTestInput && name == "Core");
            string reportName = owner + ".report.json", executionName = owner + ".execution.json";
            var report = new
            {
                SchemaVersion = 1, Owner = owner, TestRunnerRid = "linux-x64", PackageRid = (string?)null,
                Run = "synthetic", CommandExitCode = 0, EvidenceStatus = "validated", TestStatus = "passed",
                Tests = new { Assembly = owner + ".Tests", Passed = 1L, Failed = 0L, Skipped = 0L,
                    DurationMilliseconds = 1d, Cases = new[] { new { IdentitySha256 = Hash("case"u8.ToArray()),
                        Outcome = "Passed", DurationMilliseconds = 1d } } },
                CoverageStatus = "unmeasured", Coverage = (object?)null, Diagnostics = Array.Empty<string>()
            };
            byte[] reportBytes = JsonSerializer.SerializeToUtf8Bytes(report, ReportJson);
            File.WriteAllBytes(Path.Combine(Evidence, reportName), reportBytes);
            string captureHash = Hash(File.ReadAllBytes(Path.Combine(session, "capture.json")));
            byte[] execution = Canonical(new
            {
                schemaVersion = "0.0.1", safeReportHash = Hash(reportBytes), profile = owner + ".full-owner.v1",
                owner, scope = "full-owner", testCaptureDigest = captureHash, executable,
                dependencies = Array.Empty<object>(), commandExitCode = 0, passed = 1, failed = 0, skipped = 0,
                outcome = "passed", runnerRid = "linux-x64"
            });
            File.WriteAllBytes(Path.Combine(Evidence, executionName), execution);
            Directory.CreateDirectory(Path.Combine(session, "private/checks"));
            File.WriteAllBytes(Path.Combine(session, "private/checks", Hash(execution) + ".json"), execution);
            checks.Add(new { owner, session, execution = executionName, report = reportName });
            bindings.Add(new { owner, execution = executionName, report = reportName, testCaptureSha256 = captureHash });
        }
        File.WriteAllBytes(Checks, Canonical(checks));
        File.WriteAllBytes(Path.Combine(Evidence, "support.receipt.json"), Canonical(new
        {
            format = "configurator-support-qualification-0.0.1", productSourceInputsDigest = Source,
            buildRecipeDigest = Build, payloadDigest = Payload, commit = new string('1', 40),
            dirtyInputsDigest = (string?)null, toolVersion = "0.0.1",
            records = new[] { new { client = "cli", packageRid = "linux-x64", testRunnerRid = (string?)null,
                hostOs = (string?)null, hostDigest = (string?)null, capability = "prepare", locale = "en",
                scenario = "prepare-output", recordedAt = "2026-11-04T12:00:00Z", status = "unexecuted",
                exitCode = (int?)null, passedCount = 0, failedCount = 0, artifactCreated = false } }
        }));
        var evidence = Directory.GetFiles(Evidence).OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(path => new
        {
            path = Path.GetFileName(path), length = new FileInfo(path).Length, sha256 = Hash(File.ReadAllBytes(path)),
            kind = path.EndsWith(".execution.json", StringComparison.Ordinal) ? "execution" :
                path.EndsWith(".receipt.json", StringComparison.Ordinal) ? "support" : "report"
        }).ToArray();
        EvidenceDigest = HashJson(evidence);
        byte[] manifest = Canonical(new
        {
            schemaVersion = "0.0.1", client = "cli", packageRid = "linux-x64", productSourceInputsDigest = Source,
            buildRecipeDigest = Build, payloadDigest = Payload, evidenceSetDigest = EvidenceDigest,
            publicationCaptureSha256 = Hash(File.ReadAllBytes(Path.Combine(Session, "capture.json"))),
            payloadPrefix = Prefix, payload, evidence, requiredChecks = bindings
        });
        File.WriteAllBytes(Manifest, manifest);
        ExpectedDigest = Hash(manifest);

        void WriteCapture(string session, string project, Identity[] outputs, bool includeTestInput = false, bool conflict = false)
        {
            byte[] testBytes = File.ReadAllBytes(Path.Combine(repository, TestSource));
            if (conflict) testBytes = [.. testBytes, (byte)' '];
            Identity[] capturedInputs = includeTestInput
                ? inputs.Append(new Identity("repo/" + TestSource, testBytes.LongLength, Hash(testBytes), "compiler"))
                    .OrderBy(item => item.Path, StringComparer.Ordinal).ToArray()
                : inputs;
            Directory.CreateDirectory(Path.Combine(session, "private/events"));
            foreach (string name in RepositoryNames)
                foreach (string tree in new[] { "frozen", "work" })
                {
                    string destination = Path.Combine(session, tree, name);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    if (includeTestInput && name == "repo/" + TestSource) File.WriteAllBytes(destination, testBytes);
                    else File.Copy(Path.Combine(repository, name[5..]), destination);
                }
            foreach (Identity item in outputs)
                foreach (string tree in new[] { "frozen/generated/repo", "work/repo" })
                {
                    string destination = Path.Combine(session, tree, item.Path["generated/repo/".Length..]);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.WriteAllText(destination, item.Path.EndsWith("app.txt", StringComparison.Ordinal) ? "payload" : "synthetic binary");
                }
            File.WriteAllBytes(Path.Combine(session, "private/session.json"), Canonical(new
            {
                root = repository, project, target = "Build", sdk = "unused", properties = Array.Empty<string>(), hasPackages = false,
                repositoryNamespace = RepositoryNames
            }));
            byte[] bytes = Canonical(new
            {
                schemaVersion = "0.0.1", sdkVersion = "10.0.302", workloadVersion = "10.0.301.1",
                project = "repo/" + project, target = "Build", inputs = capturedInputs, settings,
                outputs = outputs.OrderBy(item => item.Path, StringComparer.Ordinal).ToArray(),
                productSourceProjection = inputs,
                productSourceInputsDigest = HashJson(capturedInputs.Where(item => item.Kind != "recipe").ToArray()),
                buildRecipeDigest = Build, invocations = Array.Empty<object>(), targets = Array.Empty<object>(),
                repositoryNamespace = RepositoryNames,
                movedInputs = Array.Empty<object>(), retiredInputs = Array.Empty<object>()
            });
            File.WriteAllBytes(Path.Combine(session, "capture.json"), bytes);
            File.WriteAllText(Path.Combine(session, "capture.sha256"), Hash(bytes));
            int index = 0;
            foreach (Identity input in capturedInputs)
                Event(new { operation = "input", path = input.Path, kind = input.Kind, hash = input.Sha256,
                    length = input.Length, project = (string?)null, name = (string?)null, value = (object?)null });
            Event(new { operation = "setting", path = "", kind = "property", hash = (string?)null,
                length = 0, project = "repo/synthetic.proj", name = "RuntimeIdentifier", value = "linux-x64" });
            foreach (string operation in new[] { "expected", "started", "finished" })
                Event(new { operation, path = "repo/" + project, kind = "graph", hash = (string?)null,
                    length = 0, project = (string?)null, name = (string?)null, value = (object?)null });
            void Event(object value) => File.WriteAllBytes(Path.Combine(session, "private/events", (++index) + ".json"), Canonical(value));
        }
    }

    internal void OmitUnconsumedNamespaceFromCheck(string owner)
    {
        string session = CheckSession(owner);
        foreach (string relative in new[] { "capture.json", "private/session.json" })
        {
            string path = Path.Combine(session, relative);
            JsonNode document = JsonNode.Parse(File.ReadAllBytes(path))!;
            document["repositoryNamespace"] = new JsonArray(document["repositoryNamespace"]!.AsArray()
                .Where(name => name!.GetValue<string>() != "repo/" + UnconsumedSource)
                .Select(name => name!.DeepClone()).ToArray());
            File.WriteAllBytes(path, Canonical(document));
        }
        foreach (string tree in new[] { "frozen/repo", "work/repo" }) File.Delete(Path.Combine(session, tree, UnconsumedSource));
        string captureHash = Hash(File.ReadAllBytes(Path.Combine(session, "capture.json")));
        File.WriteAllText(Path.Combine(session, "capture.sha256"), captureHash);
        string executionPath = Path.Combine(Evidence, owner + ".execution.json");
        JsonNode execution = JsonNode.Parse(File.ReadAllBytes(executionPath))!;
        execution["testCaptureDigest"] = captureHash;
        byte[] executionBytes = Canonical(execution);
        File.WriteAllBytes(executionPath, executionBytes);
        File.WriteAllBytes(Path.Combine(session, "private/checks", Hash(executionBytes) + ".json"), executionBytes);
        JsonNode manifest = JsonNode.Parse(File.ReadAllBytes(Manifest))!;
        manifest["requiredChecks"]!.AsArray().Single(check => check!["owner"]!.GetValue<string>() == owner)!["testCaptureSha256"] = captureHash;
        JsonNode index = manifest["evidence"]!.AsArray().Single(item => item!["path"]!.GetValue<string>() == owner + ".execution.json")!;
        index["length"] = executionBytes.LongLength;
        index["sha256"] = Hash(executionBytes);
        EvidenceDigest = Hash(Canonical(manifest["evidence"]));
        manifest["evidenceSetDigest"] = EvidenceDigest;
        byte[] manifestBytes = Canonical(manifest);
        File.WriteAllBytes(Manifest, manifestBytes);
        ExpectedDigest = Hash(manifestBytes);
    }

    internal string[] Arguments =>
    [
        "--package", Package, "--manifest", Manifest, "--capture", Session, "--source", Source, "--build", Build,
        "--payload", Payload, "--payload-prefix", Prefix, "--evidence", Evidence, "--evidence-digest", EvidenceDigest,
        "--checks", Checks, "--client", "cli", "--package-rid", "linux-x64", "--expected-digest", ExpectedDigest
    ];

    internal static void CopyTools(string bundle)
    {
        foreach (string path in Directory.GetFiles(Tools, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(bundle, "bin/package-verification", Path.GetRelativePath(Tools, path));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(path, destination, overwrite: true);
        }
    }

    internal static async Task<(int ExitCode, string Output)> Run(string assembly, string workingDirectory, params string[] arguments)
    {
        string dotnet = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "../../..", "dotnet"));
        var start = new ProcessStartInfo(dotnet) { WorkingDirectory = workingDirectory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["DOTNET_STARTUP_HOOKS"] = Path.Combine(workingDirectory, "must-not-load.dll");
        start.Environment["DOTNET_ADDITIONAL_DEPS"] = Path.Combine(workingDirectory, "must-not-resolve");
        start.Environment["CORECLR_ENABLE_PROFILING"] = "1";
        foreach (string key in start.Environment.Keys.Where(key => key.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("COMPlus_", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("CORECLR_", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(key);
        start.ArgumentList.Add(assembly);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return (process.ExitCode, await stdout + await stderr);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }

    private static Identity Input(string repository, string path, string kind)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(repository, path));
        return new("repo/" + path, bytes.LongLength, Hash(bytes), kind);
    }
    private static byte[] Canonical<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);
    private static string HashJson<T>(T value) => Hash(Canonical(value));
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed record Identity(string Path, long Length, string Sha256, string Kind);
    public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
}
