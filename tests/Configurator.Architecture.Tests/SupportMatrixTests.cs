using System.Text.Json.Nodes;

namespace Configurator.Architecture.Tests;

[NotInParallel]
public sealed class SupportMatrixTests
{
    private const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Fixture = """
        {"format":"configurator-support-qualification-0.0.1",
        "productSourceInputsDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "buildRecipeDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "payloadDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "commit":"1111111111111111111111111111111111111111","dirtyInputsDigest":null,"toolVersion":"0.0.1",
        "records":[{"client":"cli","packageRid":"linux-x64","testRunnerRid":"linux-x64",
        "hostOs":"linux","hostDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "capability":"protected-save","locale":"en","scenario":"create-only-save","recordedAt":"2026-11-04T12:00:00Z",
        "status":"passed","exitCode":0,"passedCount":1,"failedCount":0,"artifactCreated":true}]}
        """;

    [Test]
    public async Task NoReceiptsLeavesEveryCurrentAdmittedScenarioMissing()
    {
        var result = await Render();
        await Assert.That(result.ExitCode).IsEqualTo(0);
        var data = JsonNode.Parse(result.Output)!;
        var rows = data["rows"]!.AsArray();
        await Assert.That(rows.Count).IsEqualTo(180);
        await Assert.That(rows.All(row => row!["status"]!.GetValue<string>() == "missing")).IsTrue();
        await Assert.That(rows.All(row => row!["observation"] is null && row["receiptDigest"] is null)).IsTrue();
        await Assert.That(data["payloadDigest"]!.GetValue<string>()).IsEqualTo(Digest);
        // Compare actual generated admission, not a second test-owned RID inventory.
        var native = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(ReleaseTestPaths.Root,
            "eng/configurator/generated/native-release-capabilities.json")))!;
        foreach (var surface in native["surfaces"]!.AsObject())
        {
            string[] actual = rows.Where(row => row!["client"]!.GetValue<string>() == surface.Key)
                .Select(row => row!["packageRid"]!.GetValue<string>()).Distinct().Order(StringComparer.Ordinal).ToArray();
            string[] expected = surface.Value!["runtimeIdentifiers"]!.AsArray()
                .Select(rid => rid!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
            await Assert.That(string.Join(",", actual)).IsEqualTo(string.Join(",", expected));
        }
        await Assert.That(rows.Any(row => row!["client"]!.GetValue<string>() == "browser"
            && row["packageRid"]!.GetValue<string>() == "browser-wasm"
            && row["scenario"]!.GetValue<string>() == "consent-revoke")).IsTrue();
    }

    [Test]
    public async Task PartialEvidenceRetainsOutcomesAndNeverQualifiesSiblingRows()
    {
        var node = JsonNode.Parse(Fixture)!;
        var records = node["records"]!.AsArray();
        var failed = records[0]!.DeepClone();
        failed["locale"] = "ar";
        failed["status"] = "failed";
        failed["exitCode"] = 1;
        failed["passedCount"] = 0;
        failed["failedCount"] = 1;
        failed["artifactCreated"] = false;
        records.Add(failed);
        var refusal = records[0]!.DeepClone();
        refusal["packageRid"] = "win-x64";
        refusal["testRunnerRid"] = "win-x64";
        refusal["hostOs"] = "win";
        refusal["status"] = "verified-unsupported";
        refusal["exitCode"] = 1;
        refusal["artifactCreated"] = false;
        records.Add(refusal);
        var macRefusal = refusal.DeepClone();
        macRefusal["packageRid"] = "osx-x64";
        macRefusal["testRunnerRid"] = "osx-x64";
        macRefusal["hostOs"] = "osx";
        records.Add(macRefusal);
        var unexecuted = records[0]!.DeepClone();
        unexecuted["packageRid"] = "osx-arm64";
        foreach (string key in new[] { "testRunnerRid", "hostOs", "hostDigest", "exitCode" }) unexecuted[key] = null;
        unexecuted["status"] = "unexecuted";
        unexecuted["passedCount"] = 0;
        unexecuted["artifactCreated"] = false;
        records.Add(unexecuted);
        var result = await WithReceipts([node.ToJsonString()]);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        var rows = JsonNode.Parse(result.Output)!["rows"]!.AsArray();
        foreach (string status in new[] { "passed", "failed", "verified-unsupported", "unexecuted" })
            await Assert.That(rows.Count(row => row!["status"]!.GetValue<string>() == status))
                .IsEqualTo(status == "verified-unsupported" ? 2 : 1);
        await Assert.That(rows.Count(row => row!["status"]!.GetValue<string>() == "missing")).IsEqualTo(175);
        var save = rows.Single(row => row!["status"]!.GetValue<string>() == "passed")!;
        await Assert.That(save["observation"]!["artifactCreated"]!.GetValue<bool>()).IsTrue();
        await Assert.That(save["receiptDigest"]!.GetValue<string>().Length).IsEqualTo(64);
        // Record order never changes row ordering or status.
        var reversed = records.Reverse().Select(record => record!.DeepClone()).ToArray();
        node["records"] = new JsonArray(reversed);
        var reordered = await WithReceipts([node.ToJsonString()]);
        // Receipt hashes bind original bytes, so only row order/status is invariant under receipt byte changes.
        var reorderedRows = JsonNode.Parse(reordered.Output)!["rows"]!.AsArray();
        await Assert.That(string.Join(",", rows.Select(row => row!["status"]!.GetValue<string>())))
            .IsEqualTo(string.Join(",", reorderedRows.Select(row => row!["status"]!.GetValue<string>())));
    }

    [Test]
    public async Task InvalidDuplicateStaleAndOwnerReportsRejectWithoutProjection()
    {
        var irrelevant = JsonNode.Parse(Fixture)!;
        irrelevant["records"]![0]!["scenario"] = "machine-envelope";
        string stale = Fixture.Replace(Digest, new string('b', 64), StringComparison.Ordinal);
        foreach (string[] inputs in new[]
        {
            new[] { "{}" }, new[] { "{" }, new[] { Fixture, Fixture },
            new[] { irrelevant.ToJsonString() }, new[] { stale },
            new[] { Fixture, stale }, new[] { """{"format":"configurator-test-evidence-0.0.1"}""" }
        })
        {
            var result = await WithReceipts(inputs);
            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(result.Output.Trim()).IsEqualTo("SUPPORT_MATRIX_REJECTED");
        }
        var missing = await Render("--receipt", Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json"));
        await Assert.That(missing.ExitCode).IsEqualTo(1);
        await Assert.That(missing.Output.Trim()).IsEqualTo("SUPPORT_MATRIX_REJECTED");
    }

    [Test]
    public async Task SelfTestAndRepeatedProjectionAreDeterministic()
    {
        var self = await ReleaseTestPaths.Run("dotnet", "run", "--file",
            "eng/configurator/RenderSupportMatrix.cs", "--", "--self-test");
        await Assert.That(self.ExitCode).IsEqualTo(0);
        var arabic = JsonNode.Parse(Fixture)!;
        arabic["records"]![0]!["locale"] = "ar";
        var first = await WithReceipts([Fixture, arabic.ToJsonString()]);
        var second = await WithReceipts([arabic.ToJsonString(), Fixture]);
        await Assert.That(first.ExitCode).IsEqualTo(0);
        await Assert.That(second.Output).IsEqualTo(first.Output);
    }

    private static Task<(int ExitCode, string Output)> Render(params string[] arguments) =>
        ReleaseTestPaths.Run("dotnet", ["run", "--file", "eng/configurator/RenderSupportMatrix.cs", "--",
            "--source", Digest, "--build", Digest, "--payload", Digest, .. arguments]);

    private static async Task<(int ExitCode, string Output)> WithReceipts(string[] inputs)
    {
        var paths = new List<string>();
        try
        {
            foreach (string input in inputs)
            {
                string path = Path.Combine(Path.GetTempPath(), $"matrix-{Guid.NewGuid():N}.json");
                paths.Add(path);
                await File.WriteAllTextAsync(path, input);
            }
            return await Render(paths.SelectMany(path => new[] { "--receipt", path }).ToArray());
        }
        finally { foreach (string path in paths) File.Delete(path); }
    }
}
