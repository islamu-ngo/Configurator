using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Architecture.Tests;

[NotInParallel]
public sealed class PackageProvenanceTests
{
    [Test]
    [Arguments("malformed")]
    [Arguments("duplicate")]
    [Arguments("escaping")]
    [Arguments("cycle")]
    [Arguments("extra")]
    [Arguments("missing")]
    [Arguments("link")]
    [Arguments("tamper")]
    [Arguments("source")]
    [Arguments("build")]
    [Arguments("receipt")]
    [Arguments("expected")]
    [Arguments("scope")]
    [Arguments("binary")]
    [Arguments("report")]
    [Arguments("payload-binding")]
    [Arguments("special")]
    [Arguments("hardlink")]
    [Arguments("rid")]
    [Arguments("rehashed-payload")]
    [Arguments("required")]
    [Arguments("projection")]
    public async Task DetachedVerificationRejectsAlteredOrIncompleteInputs(string attack)
    {
        using var fixture = new Fixture();
        if (attack is "scope" or "binary" or "report" or "projection") fixture.BindSyntheticCheck();
        var written = await fixture.Run(write: true);
        await Assert.That(written.ExitCode).IsEqualTo(0).Because(written.Output);
        string digest = ReleaseTestPaths.Hash(File.ReadAllBytes(fixture.Manifest));
        switch (attack)
        {
            case "malformed": File.WriteAllText(fixture.Manifest, "{}"); break;
            case "duplicate":
                var duplicate = JsonNode.Parse(File.ReadAllText(fixture.Manifest))!;
                duplicate["payload"]!.AsArray().Add(duplicate["payload"]![0]!.DeepClone());
                File.WriteAllText(fixture.Manifest, duplicate.ToJsonString()); break;
            case "escaping":
                var escaping = JsonNode.Parse(File.ReadAllText(fixture.Manifest))!;
                escaping["payload"]![0]!["path"] = "../app.txt";
                File.WriteAllText(fixture.Manifest, escaping.ToJsonString()); break;
            case "cycle": File.Copy(fixture.Manifest, Path.Combine(fixture.Evidence, "provenance.json")); break;
            case "extra": File.WriteAllText(Path.Combine(fixture.Package, "extra"), "extra"); break;
            case "missing": File.Delete(Path.Combine(fixture.Package, "app.txt")); break;
            case "link": File.CreateSymbolicLink(Path.Combine(fixture.Package, "alias"), "app.txt"); break;
            case "tamper": File.WriteAllText(Path.Combine(fixture.Package, "app.txt"), "changed"); break;
            case "source": File.WriteAllText(Path.Combine(fixture.Session, "frozen/repo/source.cs"), "changed"); break;
            case "build": File.WriteAllText(Path.Combine(fixture.Session, "frozen/repo/build.props"), "changed"); break;
            case "receipt": File.WriteAllText(Path.Combine(fixture.Evidence, "receipt.json"), "{}"); break;
            case "expected": digest = new string('0', 64); break;
            case "scope": fixture.AlterCheck("scope"); break;
            case "binary": fixture.AlterCheck("binary"); break;
            case "report": fixture.AlterCheck("report"); break;
            case "payload-binding": fixture.WrongReceiptPayload(); break;
            case "special":
                var fifo = await ReleaseTestPaths.Run("mkfifo", Path.Combine(fixture.Package, "pipe"));
                await Assert.That(fifo.ExitCode).IsEqualTo(0); break;
            case "hardlink":
                var link = await ReleaseTestPaths.Run("ln", Path.Combine(fixture.Package, "app.txt"), Path.Combine(fixture.Package, "hardlink"));
                await Assert.That(link.ExitCode).IsEqualTo(0); break;
            case "rid": fixture.Rid = "win-x64"; break;
            case "rehashed-payload": fixture.RehashPackage(); break;
            case "required": fixture.RequireMissingCheck(); break;
            case "projection": fixture.AlterCheck("projection"); break;
        }
        var result = await fixture.Run(write: false, digest);
        await Assert.That(result.ExitCode).IsEqualTo(1).Because(attack + ": " + result.Output);
        await Assert.That(result.Output.Trim()).IsEqualTo("PACKAGE_PROVENANCE_REJECTED");
        if (attack is "scope" or "binary" or "report" or "payload-binding" or "cycle" or "source" or "build"
            or "projection" or "rehashed-payload" or "required")
        {
            File.Delete(fixture.Manifest);
            var regenerated = await fixture.Run(write: true);
            await Assert.That(regenerated.ExitCode).IsEqualTo(1)
                .Because("Rehashing changed evidence must not bypass semantic or native capture verification: " + attack);
        }
    }

    [Test]
    public async Task CopiedDetachedRecordsVerifyAndOrdinaryDocumentationDoesNotChangeInputs()
    {
        using var fixture = new Fixture();
        var written = await fixture.Run(write: true);
        await Assert.That(written.ExitCode).IsEqualTo(0).Because(written.Output);
        byte[] bytes = File.ReadAllBytes(fixture.Manifest);
        string digest = ReleaseTestPaths.Hash(bytes);
        string copied = Path.Combine(fixture.Root, "retrieved.json");
        File.WriteAllBytes(copied, bytes);
        fixture.Manifest = copied;
        string retrievedEvidence = Path.Combine(fixture.Root, "retrieved-evidence");
        Directory.CreateDirectory(retrievedEvidence);
        foreach (string path in Directory.GetFiles(fixture.Evidence))
            File.Copy(path, Path.Combine(retrievedEvidence, Path.GetFileName(path)));
        fixture.Evidence = retrievedEvidence;
        File.WriteAllText(Path.Combine(fixture.Root, "operator-notes.md"), "detached documentation");
        var verified = await fixture.Run(write: false, digest);
        await Assert.That(verified.ExitCode).IsEqualTo(0).Because(verified.Output);
        await Assert.That(verified.Output).Contains("PACKAGE_PROVENANCE_VALID");
        await Assert.That(verified.Output).Contains("\"unexecuted\":1");
        var overwrite = await fixture.Run(write: true);
        await Assert.That(overwrite.ExitCode).IsEqualTo(1);
    }

    [Test]
    public async Task NativeSchemaAndCanonicalizationSelfChecksPass()
    {
        foreach (string mode in new[] { "--self-test", "--schema-check" })
        {
            var result = await ReleaseTestPaths.Run("dotnet", "run", "--file",
                "eng/configurator/VerifyPackageProvenance.cs", "--", mode);
            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        }
    }

    [Test]
    public async Task ControlledCheckBindingUsesIndependentTestAndPublicationGraphs()
    {
        using var fixture = new Fixture();
        fixture.BindSyntheticCheck();
        var result = await fixture.Run(write: true);
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        var verified = await fixture.Run(write: false, ReleaseTestPaths.Hash(File.ReadAllBytes(fixture.Manifest)));
        await Assert.That(verified.ExitCode).IsEqualTo(0).Because(verified.Output);
    }

    [Test]
    public async Task FailedEvidenceRemainsRetainedWithoutSatisfyingRequiredChecks()
    {
        using var fixture = new Fixture();
        fixture.RetainFailedEvidence();
        var result = await fixture.Run(write: true);
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        await Assert.That(result.Output).Contains("\"failed\":1");
        var verified = await fixture.Run(write: false, ReleaseTestPaths.Hash(File.ReadAllBytes(fixture.Manifest)));
        await Assert.That(verified.ExitCode).IsEqualTo(0).Because(verified.Output);
        fixture.RequireMissingCheck();
        File.Delete(fixture.Manifest);
        var required = await fixture.Run(write: true);
        await Assert.That(required.ExitCode).IsEqualTo(1);
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "provenance-" + Guid.NewGuid().ToString("N"));
        internal string Package => Path.Combine(Root, "payload");
        internal string Session => Path.Combine(Root, "capture");
        internal string Evidence { get; set; }
        internal string Manifest { get; set; }
        private string Checks => Path.Combine(Root, "checks.json");
        private string Required = "none";
        private readonly string Source;
        private readonly string Build;
        private string Payload;
        internal string Rid { get; set; } = "linux-x64";

        internal Fixture()
        {
            Evidence = Path.Combine(Root, "evidence");
            Manifest = Path.Combine(Root, "provenance.json");
            Directory.CreateDirectory(Package);
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Path.Combine(Package, "app.txt"), "payload");
            Payload = HashJson(new[] { new { path = "app.txt", length = 7L, sha256 = HashText("payload") } });
            Directory.CreateDirectory(Path.Combine(Session, "private/events"));
            foreach (string tree in new[] { "frozen/repo", "work/repo" })
            {
                Directory.CreateDirectory(Path.Combine(Session, tree));
                File.WriteAllText(Path.Combine(Session, tree, "source.cs"), "source");
                File.WriteAllText(Path.Combine(Session, tree, "build.props"), "recipe");
            }
            foreach (string tree in new[] { "frozen/generated/repo", "work/repo" })
            {
                Directory.CreateDirectory(Path.Combine(Session, tree, "artifacts/payload/linux-x64"));
                File.WriteAllText(Path.Combine(Session, tree, "artifacts/payload/linux-x64/app.txt"), "payload");
            }
            // Synthetic sealed observations exercise the consumer contract. They do not
            // certify BuildCapture's actual task-boundary consumption or real qualification.
            var inputs = new[]
            {
                new { path = "repo/build.props", length = 6L, sha256 = HashText("recipe"), kind = "recipe" },
                new { path = "repo/source.cs", length = 6L, sha256 = HashText("source"), kind = "compiler" }
            };
            Source = HashJson(inputs.Where(item => item.kind != "recipe").ToArray());
            var settings = new[] { new { project = "repo/synthetic.proj", name = "RuntimeIdentifier", value = "linux-x64" } };
            string[] repositoryNamespace = ["repo/build.props", "repo/source.cs"];
            Build = HashJson(new { sdkVersion = "10.0.302", workloadVersion = "10.0.301.1",
                inputs = inputs.Where(item => item.kind == "recipe").ToArray(), settings,
                invocations = Array.Empty<object>(), targets = Array.Empty<object>(),
                movedInputs = Array.Empty<object>(), retiredInputs = Array.Empty<object>() });
            var capture = new { schemaVersion = "0.0.1", sdkVersion = "10.0.302", workloadVersion = "10.0.301.1",
                project = "repo/synthetic.proj", target = "Build", inputs, settings,
                outputs = new[] { new { path = "generated/repo/artifacts/payload/linux-x64/app.txt", length = 7L,
                    sha256 = HashText("payload"), kind = "output" } }, productSourceProjection = inputs,
                productSourceInputsDigest = Source, buildRecipeDigest = Build,
                invocations = Array.Empty<object>(), targets = Array.Empty<object>(), repositoryNamespace,
                movedInputs = Array.Empty<object>(), retiredInputs = Array.Empty<object>() };
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(capture, Json);
            File.WriteAllBytes(Path.Combine(Session, "capture.json"), bytes);
            File.WriteAllText(Path.Combine(Session, "capture.sha256"), ReleaseTestPaths.Hash(bytes));
            File.WriteAllText(Path.Combine(Session, "private/session.json"), JsonSerializer.Serialize(new
            {
                root = Path.Combine(Root, "unused"), project = "synthetic.proj", target = "Build", sdk = "unused",
                properties = Array.Empty<string>(), hasPackages = false, repositoryNamespace
            }, Json));
            foreach (var input in inputs)
                File.WriteAllText(Path.Combine(Session, "private/events", input.kind + ".json"),
                    JsonSerializer.Serialize(new { operation = "input", input.path, input.kind, hash = input.sha256,
                        input.length, project = (string?)null, name = (string?)null, value = (object?)null }, Json));
            File.WriteAllText(Path.Combine(Session, "private/events/setting.json"), JsonSerializer.Serialize(new
            {
                operation = "setting", path = "", kind = "property", hash = (string?)null, length = 0,
                project = "repo/synthetic.proj", name = "RuntimeIdentifier", value = "linux-x64"
            }, Json));
            foreach (string operation in new[] { "expected", "started", "finished" })
                File.WriteAllText(Path.Combine(Session, "private/events", operation + ".json"),
                    JsonSerializer.Serialize(new { operation, path = "repo/synthetic.proj", kind = "graph",
                        hash = (string?)null, length = 0, project = (string?)null, name = (string?)null, value = (object?)null }, Json));
            File.WriteAllText(Checks, "[]");
            File.WriteAllText(Path.Combine(Evidence, "receipt.json"), JsonSerializer.Serialize(new
            {
                format = "configurator-support-qualification-0.0.1",
                productSourceInputsDigest = Source, buildRecipeDigest = Build, payloadDigest = Payload,
                commit = new string('1', 40), dirtyInputsDigest = (string?)null, toolVersion = "0.0.1",
                records = new[] { new { client = "cli", packageRid = "linux-x64", testRunnerRid = (string?)null,
                    hostOs = (string?)null, hostDigest = (string?)null, capability = "prepare", locale = "en",
                    scenario = "prepare-output", recordedAt = "2026-11-04T12:00:00Z", status = "unexecuted",
                    exitCode = (int?)null, passedCount = 0, failedCount = 0, artifactCreated = false } }
            }, Json));
        }

        internal void WrongReceiptPayload()
        {
            string path = Path.Combine(Evidence, "receipt.json");
            var receipt = JsonNode.Parse(File.ReadAllText(path))!;
            receipt["payloadDigest"] = new string('0', 64);
            File.WriteAllText(path, receipt.ToJsonString());
        }

        internal void RehashPackage()
        {
            File.WriteAllText(Path.Combine(Package, "app.txt"), "replacement");
            Payload = HashJson(new[] { new { path = "app.txt", length = 11L, sha256 = HashText("replacement") } });
            string path = Path.Combine(Evidence, "receipt.json");
            var receipt = JsonNode.Parse(File.ReadAllText(path))!;
            receipt["payloadDigest"] = Payload;
            File.WriteAllText(path, receipt.ToJsonString());
        }

        internal void RequireMissingCheck() => Required = "Configurator.Core";

        internal void RetainFailedEvidence()
        {
            BindSyntheticCheck();
            Required = "none";
            File.WriteAllText(Checks, "[]");
            File.Delete(Path.Combine(Evidence, "execution.json"));
            string path = Path.Combine(Evidence, "receipt.json");
            var receipt = JsonNode.Parse(File.ReadAllText(path))!;
            var row = receipt["records"]![0]!;
            row["status"] = "failed";
            row["testRunnerRid"] = "linux-x64";
            row["hostOs"] = "linux";
            row["hostDigest"] = HashText("synthetic host");
            row["exitCode"] = 1;
            row["failedCount"] = 1;
            File.WriteAllText(path, receipt.ToJsonString(Json));
            path = Path.Combine(Evidence, "report.json");
            var report = JsonNode.Parse(File.ReadAllText(path))!;
            report["CommandExitCode"] = 1;
            report["TestStatus"] = "failed";
            report["Tests"]!["Passed"] = 0;
            report["Tests"]!["Failed"] = 1;
            report["Tests"]!["Cases"]![0]!["Outcome"] = "Failed";
            File.WriteAllText(path, report.ToJsonString(ReportJson));
        }

        private string TestSession => Path.Combine(Root, "test-capture");
        private string TestBinary => "tests/Configurator.Architecture.Tests/bin/Release/net10.0/Configurator.Architecture.Tests.dll";

        internal void BindSyntheticCheck()
        {
            Required = "Configurator.Architecture";
            foreach (string file in Directory.GetFiles(Session, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(TestSession, Path.GetRelativePath(Session, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            string project = "tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj";
            var configuration = JsonNode.Parse(File.ReadAllText(Path.Combine(TestSession, "private/session.json")))!;
            configuration["project"] = project;
            File.WriteAllText(Path.Combine(TestSession, "private/session.json"), configuration.ToJsonString(Json));
            var capture = JsonNode.Parse(File.ReadAllText(Path.Combine(TestSession, "capture.json")))!;
            capture["project"] = "repo/" + project;
            foreach (string tree in new[] { "work/repo", "frozen/generated/repo" })
            {
                string binary = Path.Combine(TestSession, tree, TestBinary);
                Directory.CreateDirectory(Path.GetDirectoryName(binary)!);
                File.WriteAllText(binary, "synthetic binary");
            }
            var executable = new { path = "generated/repo/" + TestBinary, length = 16L,
                sha256 = HashText("synthetic binary"), kind = "output" };
            capture["outputs"]!.AsArray().Add(JsonSerializer.SerializeToNode(executable, Json));
            byte[] captureBytes = System.Text.Encoding.UTF8.GetBytes(capture.ToJsonString(Json));
            File.WriteAllBytes(Path.Combine(TestSession, "capture.json"), captureBytes);
            File.WriteAllText(Path.Combine(TestSession, "capture.sha256"), ReleaseTestPaths.Hash(captureBytes));
            File.WriteAllText(Checks, JsonSerializer.Serialize(new[] { new { owner = Required, session = TestSession,
                execution = "execution.json", report = "report.json" } }, Json));
            // Synthetic native ledger fixtures test only P11C's consumer. The public
            // record and private binding are not presented as actual test execution.
            var report = new
            {
                SchemaVersion = 1, Owner = Required, TestRunnerRid = "linux-x64", PackageRid = (string?)null,
                Run = "synthetic", CommandExitCode = 0, EvidenceStatus = "validated", TestStatus = "passed",
                Tests = new { Assembly = Required + ".Tests", Passed = 1L, Failed = 0L, Skipped = 0L,
                    DurationMilliseconds = 1d, Cases = new[] { new { IdentitySha256 = HashText("case"),
                        Outcome = "Passed", DurationMilliseconds = 1d } } },
                CoverageStatus = "unmeasured", Coverage = (object?)null, Diagnostics = Array.Empty<string>()
            };
            File.WriteAllBytes(Path.Combine(Evidence, "report.json"),
                JsonSerializer.SerializeToUtf8Bytes(report, ReportJson));
            File.WriteAllText(Path.Combine(Evidence, "execution.json"), JsonSerializer.Serialize(new
            {
                schemaVersion = "0.0.1", safeReportHash = ReleaseTestPaths.Hash(File.ReadAllBytes(Path.Combine(Evidence, "report.json"))),
                profile = Required + ".full-owner.v1", owner = Required, scope = "full-owner",
                testCaptureDigest = ReleaseTestPaths.Hash(captureBytes), executable,
                dependencies = Array.Empty<object>(), commandExitCode = 0, passed = 1, failed = 0, skipped = 0,
                outcome = "passed", runnerRid = "linux-x64"
            }, Json));
            BindExecution();
        }

        private void BindExecution()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(Evidence, "execution.json"));
            string bindings = Path.Combine(TestSession, "private/checks");
            Directory.CreateDirectory(bindings);
            File.WriteAllBytes(Path.Combine(bindings, ReleaseTestPaths.Hash(bytes) + ".json"), bytes);
        }

        internal void AlterCheck(string attack)
        {
            if (attack == "binary")
                File.WriteAllText(Path.Combine(TestSession, "frozen/generated/repo", TestBinary), "altered binary");
            if (attack == "scope")
            {
                string path = Path.Combine(Evidence, "execution.json");
                var execution = JsonNode.Parse(File.ReadAllText(path))!;
                execution["scope"] = "focused";
                File.WriteAllText(path, execution.ToJsonString(Json));
                BindExecution();
            }
            if (attack == "report")
            {
                string path = Path.Combine(Evidence, "report.json");
                var report = JsonNode.Parse(File.ReadAllText(path))!;
                report["Tests"]!["Passed"] = 2;
                File.WriteAllText(path, report.ToJsonString());
            }
            if (attack == "projection")
            {
                foreach (string tree in new[] { "frozen/repo", "work/repo" })
                    File.WriteAllText(Path.Combine(TestSession, tree, "source.cs"), "changed");
                string eventPath = Path.Combine(TestSession, "private/events/compiler.json");
                var observation = JsonNode.Parse(File.ReadAllText(eventPath))!;
                observation["hash"] = HashText("changed");
                observation["length"] = 7;
                File.WriteAllText(eventPath, observation.ToJsonString(Json));
                string path = Path.Combine(TestSession, "capture.json");
                var capture = JsonNode.Parse(File.ReadAllText(path))!;
                foreach (string array in new[] { "inputs", "productSourceProjection" })
                {
                    var input = capture[array]!.AsArray().Single(item => item!["path"]!.GetValue<string>() == "repo/source.cs")!;
                    input["sha256"] = HashText("changed");
                    input["length"] = 7;
                }
                var sourceInputs = new JsonArray(capture["inputs"]!.AsArray()
                    .Where(item => item!["kind"]!.GetValue<string>() != "recipe").Select(item => item!.DeepClone()).ToArray());
                capture["productSourceInputsDigest"] = HashText(sourceInputs.ToJsonString(Json));
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(capture.ToJsonString(Json));
                File.WriteAllBytes(path, bytes);
                File.WriteAllText(Path.Combine(TestSession, "capture.sha256"), ReleaseTestPaths.Hash(bytes));
                string executionPath = Path.Combine(Evidence, "execution.json");
                var execution = JsonNode.Parse(File.ReadAllText(executionPath))!;
                execution["testCaptureDigest"] = ReleaseTestPaths.Hash(bytes);
                File.WriteAllText(executionPath, execution.ToJsonString(Json));
                BindExecution();
            }
        }

        internal Task<(int ExitCode, string Output)> Run(bool write, string? expected = null)
        {
            // Independent specification: evidence records use the sorted, typed index;
            // each record's hash covers its exact bytes, not a self aggregate.
            var index = Directory.GetFiles(Evidence).OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(path => new
            {
                path = Path.GetFileName(path), length = new FileInfo(path).Length,
                sha256 = ReleaseTestPaths.Hash(File.ReadAllBytes(path)),
                kind = Path.GetFileName(path) == "receipt.json" ? "support" : Path.GetFileName(path) == "execution.json" ? "execution" : "report"
            }).ToArray();
            var args = new List<string> { Path.Combine(ReleaseTestPaths.Root,
                "artifacts/release-verifier/provenance/Configurator.PackageProvenance.dll"),
                write ? "--write" : "--verify", "--package", Package, "--manifest", Manifest, "--capture", Session,
                "--source", Source, "--build", Build, "--payload", Payload, "--evidence", Evidence,
                "--payload-prefix", "generated/repo/artifacts/payload/linux-x64/",
                "--evidence-digest", HashJson(index), "--checks", Checks, "--required-checks", Required,
                "--client", "cli", "--package-rid", Rid };
            if (!write) args.AddRange(["--expected-digest", expected!]);
            return ReleaseTestPaths.Run("dotnet", args.ToArray());
        }

        private static string HashText(string text) => ReleaseTestPaths.Hash(System.Text.Encoding.UTF8.GetBytes(text));
        private static string HashJson<T>(T value) => ReleaseTestPaths.Hash(JsonSerializer.SerializeToUtf8Bytes(value, Json));
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
