#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=$(MSBuildProjectExtensionsPath)RenderSupportMatrix.packages.lock.json
#:property AssemblyName=Configurator.SupportMatrix
#:property PublishAot=false

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

return await SupportMatrix.Run(args);

/// <summary>
/// Renders detached evidence from the native receipt owner's projection. It makes no aggregate
/// client qualification claim and never writes payloads, evidence indexes or its own digest.
/// </summary>
internal static class SupportMatrix
{
    private static readonly string[] OwnerArguments =
        ["run", "--file", "eng/configurator/ValidateSupportQualification.cs", "--", "--projection"];

    internal static async Task<int> Run(string[] args)
    {
        try
        {
            if (args is ["--self-test"])
            {
                SelfTest();
                Console.WriteLine("SUPPORT_MATRIX_SELF_TEST_PASSED");
                return 0;
            }
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = FindRoot(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string argument in OwnerArguments.Concat(args)) start.ArgumentList.Add(argument);
            using var process = new Process { StartInfo = start };
            process.Start();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw new InvalidDataException();
            }
            string json = await output;
            _ = await error; // Owner diagnostics are deliberately not relayed.
            Require(process.ExitCode == 0);
            var projection = JsonNode.Parse(json)!.AsObject();
            Require(projection["format"]!.GetValue<string>() == "configurator-support-projection-0.0.1");
            Console.Write(Render(projection));
            return 0;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException
            or JsonException or InvalidOperationException or ArgumentException or NotSupportedException
            or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine("SUPPORT_MATRIX_REJECTED");
            return 1;
        }
    }

    private static string Render(JsonObject projection)
    {
        var observations = new Dictionary<Coverage, (string Digest, JsonNode Record)>();
        foreach (JsonNode? evidence in projection["receipts"]!.AsArray())
            foreach (JsonNode? record in evidence!["receipt"]!["records"]!.AsArray())
                observations.Add(Key(record!), (evidence["receiptDigest"]!.GetValue<string>(), record!));
        var rows = new JsonArray();
        foreach (JsonNode? admitted in projection["admission"]!.AsArray()
            .OrderBy(row => row!["client"]!.GetValue<string>(), StringComparer.Ordinal)
            .ThenBy(row => row!["packageRid"]!.GetValue<string>(), StringComparer.Ordinal)
            .ThenBy(row => row!["capability"]!.GetValue<string>(), StringComparer.Ordinal)
            .ThenBy(row => row!["locale"]!.GetValue<string>(), StringComparer.Ordinal)
            .ThenBy(row => row!["scenario"]!.GetValue<string>(), StringComparer.Ordinal))
        {
            var row = admitted!.DeepClone().AsObject();
            bool observed = observations.TryGetValue(Key(admitted), out var evidence);
            row["status"] = observed ? evidence.Record["status"]!.GetValue<string>() : "missing";
            row["receiptDigest"] = observed ? evidence.Digest : null;
            row["observation"] = observed ? evidence.Record.DeepClone() : null;
            rows.Add(row);
        }
        var result = new JsonObject
        {
            ["format"] = "configurator-support-matrix-0.0.1",
            ["productSourceInputsDigest"] = projection["productSourceInputsDigest"]!.DeepClone(),
            ["buildRecipeDigest"] = projection["buildRecipeDigest"]!.DeepClone(),
            ["payloadDigest"] = projection["payloadDigest"]!.DeepClone(),
            ["receipts"] = new JsonArray(projection["receipts"]!.AsArray()
                .OrderBy(receipt => receipt!["receiptDigest"]!.GetValue<string>(), StringComparer.Ordinal)
                .Select(receipt => (JsonNode?)new JsonObject
                {
                    ["receiptDigest"] = receipt!["receiptDigest"]!.DeepClone(),
                    ["commit"] = receipt["receipt"]!["commit"]!.DeepClone(),
                    ["dirtyInputsDigest"] = receipt["receipt"]!["dirtyInputsDigest"]?.DeepClone(),
                    ["toolVersion"] = receipt["receipt"]!["toolVersion"]!.DeepClone()
                }).ToArray()),
            ["rows"] = rows
        };
        return result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private static Coverage Key(JsonNode row) => new(row["client"]!.GetValue<string>(),
        row["packageRid"]!.GetValue<string>(), row["capability"]!.GetValue<string>(),
        row["locale"]!.GetValue<string>(), row["scenario"]!.GetValue<string>());

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(Environment.CurrentDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Configurator.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidDataException();
    }

    private static void Require(bool condition) { if (!condition) throw new InvalidDataException(); }

    private static void SelfTest()
    {
        // Synthetic projection tests rendering only; native admission/receipt policy remains in its owner.
        var projection = JsonNode.Parse("""
            {"productSourceInputsDigest":"source","buildRecipeDigest":"build","payloadDigest":"payload","receipts":[],
            "admission":[{"client":"fixture","packageRid":"fixture","capability":"fixture","locale":"en","scenario":"one"},
            {"client":"fixture","packageRid":"fixture","capability":"fixture","locale":"en","scenario":"two"}]}
            """)!.AsObject();
        var missing = JsonNode.Parse(Render(projection))!["rows"]!.AsArray();
        Require(missing.Count == 2 && missing.All(row => row!["status"]!.GetValue<string>() == "missing"));
        foreach (string status in new[] { "passed", "failed", "verified-unsupported", "unexecuted" })
        {
            var record = projection["admission"]![0]!.DeepClone();
            record["status"] = status;
            projection["receipts"] = new JsonArray(new JsonObject
            {
                ["receiptDigest"] = "fixture",
                ["receipt"] = new JsonObject
                {
                    ["commit"] = "fixture", ["dirtyInputsDigest"] = null, ["toolVersion"] = "0.0.1",
                    ["records"] = new JsonArray(record)
                }
            });
            string rendered = Render(projection);
            var rows = JsonNode.Parse(rendered)!["rows"]!.AsArray();
            Require(rows[0]!["status"]!.GetValue<string>() == status
                && rows[1]!["status"]!.GetValue<string>() == "missing"
                && rendered == Render(projection));
        }
    }

    private sealed record Coverage(string Client, string PackageRid, string Capability, string Locale, string Scenario);
}
