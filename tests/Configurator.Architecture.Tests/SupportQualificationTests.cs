using System.Text.Json.Nodes;

namespace Configurator.Architecture.Tests;

[NotInParallel]
public sealed class SupportQualificationTests
{
    private const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Fixture = """
        {"format":"configurator-support-qualification-0.0.1",
         "productSourceInputsDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
         "buildRecipeDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
         "payloadDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
         "commit":"1111111111111111111111111111111111111111",
         "dirtyInputsDigest":null,"toolVersion":"0.0.1",
         "records":[{"client":"cli","packageRid":"linux-x64","testRunnerRid":"linux-x64",
         "hostOs":"linux","hostDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
         "capability":"protected-save","locale":"en","scenario":"create-only-save",
         "recordedAt":"2026-11-04T12:00:00Z","status":"passed",
         "exitCode":0,"passedCount":1,"failedCount":0,"artifactCreated":true}]}
        """;

    [Test]
    public async Task ProjectionReturnsValidatedIdentityAndAdmissionWithoutQualifyingMissingRows()
    {
        var result = await Validate(Fixture, projection: true);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        var data = JsonNode.Parse(result.Output)!;
        await Assert.That(data["productSourceInputsDigest"]!.GetValue<string>()).IsEqualTo(Digest);
        await Assert.That(data["buildRecipeDigest"]!.GetValue<string>()).IsEqualTo(Digest);
        await Assert.That(data["payloadDigest"]!.GetValue<string>()).IsEqualTo(Digest);
        await Assert.That(data["admission"]!.AsArray().Count).IsEqualTo(180);
        await Assert.That(data["receipts"]!.AsArray().Count).IsEqualTo(1);
        await Assert.That(data["receipts"]![0]!["receipt"]!["records"]![0]!["status"]!.GetValue<string>()).IsEqualTo("passed");
    }

    [Test]
    public async Task InvalidReceiptsRejectWithoutEchoingInput()
    {
        var cases = new List<string> { "{}", Fixture.Replace("\"format\":", "\"format\":\"duplicate\",\"format\":", StringComparison.Ordinal) };
        foreach (string field in new[] { "payloadDigest", "toolVersion", "records" })
        {
            var node = JsonNode.Parse(Fixture)!.AsObject();
            node.Remove(field);
            cases.Add(node.ToJsonString());
        }
        foreach (var (field, value) in new (string, object?)[]
        {
            ("status", "unknown"), ("status", "unexecuted"), ("status", "failed"),
            ("status", "verified-unsupported"), ("exitCode", 3),
            ("failedCount", 1), ("passedCount", 0), ("packageRid", "linux-musl-x64"),
            ("testRunnerRid", "win-x64"), ("hostOs", "osx"),
            ("client", "desktop"), ("capability", "telemetry"),
            ("locale", "zz"), ("recordedAt", "yesterday"), ("artifactCreated", false),
            ("scenario", "private-answer-canary"), ("unknown", "private-answer-canary")
        })
        {
            var node = JsonNode.Parse(Fixture)!;
            node["records"]![0]![field] = JsonValue.Create(value);
            // Desktop linux-x64 is admitted; a CLI-only scenario must still reject.
            if (field == "client") node["records"]![0]!["scenario"] = "machine-envelope";
            cases.Add(node.ToJsonString());
        }
        var duplicate = JsonNode.Parse(Fixture)!;
        duplicate["records"]!.AsArray().Add(duplicate["records"]![0]!.DeepClone());
        cases.Add(duplicate.ToJsonString());
        cases.Add(Fixture.Replace(Digest, new string('b', 64), StringComparison.Ordinal));
        foreach (string field in new[] { "productSourceInputsDigest", "buildRecipeDigest", "payloadDigest" })
        {
            var stale = JsonNode.Parse(Fixture)!;
            stale[field] = new string('b', 64);
            cases.Add(stale.ToJsonString());
        }
        cases.Add(new string(' ', 1024 * 1024 + 1));
        for (int index = 0; index < cases.Count; index++)
        {
            foreach (bool projection in new[] { false, true })
            {
                var result = await Validate(cases[index], projection);
                await Assert.That(result.ExitCode).IsEqualTo(1).Because($"Negative fixture {index}, projection {projection}");
                await Assert.That(result.Output.Trim()).IsEqualTo("SUPPORT_QUALIFICATION_REJECTED");
            }
        }
    }

    [Test]
    public async Task OutcomesRemainDistinctAndUnsupportedSaveCannotCreateArtifact()
    {
        foreach (string status in new[] { "passed", "failed", "verified-unsupported", "unexecuted" })
        {
            var node = JsonNode.Parse(Fixture)!;
            var record = node["records"]![0]!;
            record["status"] = status;
            if (status == "failed")
            {
                record["exitCode"] = 1;
                record["passedCount"] = 0;
                record["failedCount"] = 1;
                record["artifactCreated"] = false;
            }
            if (status == "verified-unsupported")
            {
                record["packageRid"] = "win-x64";
                record["testRunnerRid"] = "win-x64";
                record["hostOs"] = "win";
                record["exitCode"] = 1;
                record["artifactCreated"] = false;
            }
            if (status == "unexecuted")
            {
                record["testRunnerRid"] = null;
                record["hostOs"] = null;
                record["hostDigest"] = null;
                record["exitCode"] = null;
                record["passedCount"] = 0;
                record["artifactCreated"] = false;
            }
            var result = await Validate(node.ToJsonString());
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(result.Output).Contains($"\"{status}\":1");
        }
    }

    [Test]
    public async Task GeneratedSchemaAndSelfTestsAreCurrent()
    {
        foreach (string mode in new[] { "--self-test", "--schema-check" })
        {
            var result = await ReleaseTestPaths.Run("dotnet", "run", "--file",
                "eng/configurator/ValidateSupportQualification.cs", "--", mode);
            await Assert.That(result.ExitCode).IsEqualTo(0);
        }
    }

    private static async Task<(int ExitCode, string Output)> Validate(string input, bool projection = false)
    {
        string path = Path.Combine(Path.GetTempPath(), $"support-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, input);
            string[] arguments = projection
                ? ["--projection", "--source", Digest, "--build", Digest, "--payload", Digest, "--receipt", path]
                : ["--receipt", path, "--source", Digest, "--build", Digest, "--payload", Digest];
            return await ReleaseTestPaths.Run("dotnet", projection
                ? ["run", "--file", "eng/configurator/ValidateSupportQualification.cs", "--", .. arguments]
                : [Path.Combine(ReleaseTestPaths.Root, "artifacts/release-verifier/support/Configurator.SupportQualification.dll"),
                    .. arguments]);
        }
        finally { File.Delete(path); }
    }
}
