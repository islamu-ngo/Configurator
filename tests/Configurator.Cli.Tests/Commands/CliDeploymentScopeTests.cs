using System.Text;
using System.Text.Json;

namespace Configurator.Cli.Tests;

public sealed class CliDeploymentScopeTests
{
    private const string Definition = """
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1},
         "project":{"id":"example.deployment","name":"Example deployment","publisher":"example"},
         "applicationVersions":["1.0"],"entryPoints":["setup"],
         "deployment":{"recipePointer":"/recipe","scopePointer":"/scope",
           "components":[{"id":"worker","title":"Worker"},{"id":"dashboard","title":"Dashboard"}],
           "recipes":[{"id":"separate","title":"Separate processes","scopes":[
             {"id":"all","title":"All components","componentIds":["worker","dashboard"],"artifactIds":["worker","dashboard"]},
             {"id":"worker","title":"Worker only","componentIds":["worker"],"artifactIds":["worker"],"primaryArtifactId":"worker"},
             {"id":"dashboard","title":"Dashboard only","componentIds":["dashboard"],"artifactIds":["dashboard"],"primaryArtifactId":"dashboard"}]},
             {"id":"combined","title":"Combined process","scopes":[
               {"id":"all","title":"All components","componentIds":["worker","dashboard"],"artifactIds":["worker","dashboard"]}]}]},
         "configuration":{"type":"object","additionalProperties":false,
           "required":["recipe","scope","workerSecret","dashboardSecret"],
           "x-configurator":{"sensitivity":"public"},"properties":{
             "recipe":{"type":"string","enum":["separate","combined"],"x-configurator":{"sensitivity":"public","initialValue":"separate"}},
             "scope":{"type":"string","enum":["all","worker","dashboard"],"x-configurator":{"sensitivity":"public","initialValue":"all"}},
             "workerSecret":{"type":"string","x-configurator":{"sensitivity":"secret"}},
             "dashboardSecret":{"type":"string","x-configurator":{"sensitivity":"secret"}}
           }},
         "outputs":{
           "worker.env":{"id":"worker","format":"dotenv","targetUpdateMode":"create","restartRequirement":"process",
             "consumerIds":["worker"],"surface":"process-environment","environmentDialect":"docker-run",
             "projection":{"op":"object","members":{"WORKER_SECRET":{"op":"copy","reference":{"scope":"root","path":"/workerSecret"}}}}},
           "dashboard.env":{"id":"dashboard","format":"dotenv","targetUpdateMode":"create","restartRequirement":"process",
             "consumerIds":["dashboard"],"surface":"process-environment","environmentDialect":"docker-run",
             "projection":{"op":"object","members":{"DASHBOARD_SECRET":{"op":"copy","reference":{"scope":"root","path":"/dashboardSecret"}}}}}
         }}
        """;

    [Test]
    public async Task NarrowScopePreparesOnlyItsRecipientAndRejectsAnotherArtifact()
    {
        CliFixture io = Fixture("dashboard", worker: true, dashboard: true);
        await Assert.That(await io.ArtifactAsync("create", "dashboard", "--output", "dashboard.env"))
            .IsEqualTo(CliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.Files["dashboard.env"]))
            .IsEqualTo("DASHBOARD_SECRET=synthetic-dashboard-sentinel\n");
        using (JsonDocument receipt = JsonDocument.Parse(io.StandardOutput))
        {
            await Assert.That(receipt.RootElement.GetProperty("preparation").GetString()).IsEqualTo("ready");
            await Assert.That(receipt.RootElement.GetProperty("readiness").GetProperty("state").GetString())
                .IsEqualTo("incomplete");
            await Assert.That(receipt.RootElement.GetProperty("targetAcceptance").GetString()).IsEqualTo("unassessed");
        }
        await Assert.That(await io.ArtifactAsync("create", "worker", "--output", "worker.env"))
            .IsEqualTo(CliExitCode.Validation);
        await Assert.That(io.Files.ContainsKey("worker.env")).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("synthetic-worker-sentinel");
        await Assert.That(await io.RunAsync("artifact", "bundle-create", "--definition", "definition.json",
            "--artifact", "worker", "--artifact", "dashboard", "--input", "answers.json", "--output", "bundle", "--machine"))
            .IsEqualTo(CliExitCode.Validation);
        await Assert.That(io.Files.ContainsKey(Path.Combine("bundle", "worker.env"))).IsFalse();
        await Assert.That(io.Files.ContainsKey(Path.Combine("bundle", "dashboard.env"))).IsFalse();
    }

    [Test]
    public async Task NarrowScopeDoesNotRequireUnrelatedSecretButDefaultAllDoes()
    {
        CliFixture scoped = Fixture("dashboard", worker: false, dashboard: true);
        await Assert.That(await scoped.ArtifactAsync("create", "dashboard", "--output", "scoped.env"))
            .IsEqualTo(CliExitCode.Success);
        CliFixture defaults = Fixture(null, worker: false, dashboard: true);
        await Assert.That(await defaults.ArtifactAsync("create", "dashboard", "--output", "default.env"))
            .IsEqualTo(CliExitCode.Incomplete);
        await Assert.That(defaults.Files.ContainsKey("default.env")).IsFalse();
        CliFixture complete = Fixture(null, worker: true, dashboard: true);
        await Assert.That(await complete.ArtifactAsync("create", "dashboard", "--output", "all.env"))
            .IsEqualTo(CliExitCode.Success);
    }

    [Test]
    public async Task UnknownContextOrUnknownFieldCannotPublish()
    {
        CliFixture io = Fixture("unknown", worker: true, dashboard: true);
        await Assert.That(await io.ArtifactAsync("create", "dashboard", "--output", "invalid.env"))
            .IsEqualTo(CliExitCode.Validation);
        await Assert.That(io.Files.ContainsKey("invalid.env")).IsFalse();
        io = Fixture("dashboard", worker: true, dashboard: true);
        io.Files["answers.json"] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(io.Files["answers.json"])
            .Replace("\"scope\":", "\"unknown\":true,\"scope\":", StringComparison.Ordinal));
        await Assert.That(await io.ArtifactAsync("create", "dashboard", "--output", "invalid.env"))
            .IsEqualTo(CliExitCode.Validation);
        await Assert.That(io.Files.ContainsKey("invalid.env")).IsFalse();
    }

    [Test]
    public async Task IncompatibleRecipeScopeCannotPublish()
    {
        CliFixture io = Fixture("dashboard", worker: true, dashboard: true, recipe: "combined");
        await Assert.That(await io.ArtifactAsync("create", "dashboard", "--output", "invalid.env"))
            .IsEqualTo(CliExitCode.Validation);
        await Assert.That(io.Files.ContainsKey("invalid.env")).IsFalse();
    }

    [Test]
    public async Task CompareUsesExplicitContextAndRefusesAnotherRecipientsArtifact()
    {
        CliFixture io = Fixture("dashboard", worker: false, dashboard: true);
        io.Files["existing.env"] = "DASHBOARD_SECRET=synthetic-dashboard-sentinel\n"u8.ToArray();
        await Assert.That(await io.RunAsync("artifact", "compare", "--definition", "definition.json",
            "--artifact", "dashboard", "--input", "existing.env", "--answers", "answers.json", "--machine"))
            .IsEqualTo(CliExitCode.Success);
        await Assert.That(await io.RunAsync("artifact", "compare", "--definition", "definition.json",
            "--artifact", "worker", "--input", "existing.env", "--answers", "answers.json", "--machine"))
            .IsEqualTo(CliExitCode.Blocked);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("synthetic-dashboard-sentinel");
        await Assert.That(await io.RunAsync("artifact", "compare", "--definition", "definition.json",
            "--artifact", "dashboard", "--input", "existing.env", "--machine"))
            .IsEqualTo(CliExitCode.Blocked);
    }

    [Test]
    [Arguments("docker-run", "process-environment", "DASHBOARD_SECRET=  synthetic $value #literal  \n")]
    [Arguments("compose", "compose-input", "DASHBOARD_SECRET=\"  synthetic $$value #literal  \"\n")]
    public async Task PreparedEnvironmentProofUsesDeclaredDialect(string dialect, string surface, string existing)
    {
        CliFixture io = Fixture("dashboard", worker: false, dashboard: false);
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(Definition
            .Replace("docker-run", dialect, StringComparison.Ordinal)
            .Replace("process-environment", surface, StringComparison.Ordinal));
        io.Files["answers.json"] = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        {
            answerContractVersion = "0.0.1",
            values = new Dictionary<string, string> { ["scope"] = "dashboard", ["dashboardSecret"] = "  synthetic $value #literal  " },
            omitted = Array.Empty<string>()
        });
        io.Files["existing.env"] = Encoding.UTF8.GetBytes(existing);
        await Assert.That(await io.RunAsync("artifact", "validate", "--definition", "definition.json",
            "--artifact", "dashboard", "--input", "existing.env", "--answers", "answers.json", "--machine"))
            .IsEqualTo(CliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("synthetic $value");
    }

    private static CliFixture Fixture(string? scope, bool worker, bool dashboard, string? recipe = null)
    {
        var io = new CliFixture();
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(Definition);
        var values = new Dictionary<string, string>();
        if (recipe is not null) values["recipe"] = recipe;
        if (scope is not null) values["scope"] = scope;
        if (worker) values["workerSecret"] = "synthetic-worker-sentinel";
        if (dashboard) values["dashboardSecret"] = "synthetic-dashboard-sentinel";
        io.Files["answers.json"] = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            new { answerContractVersion = "0.0.1", values, omitted = Array.Empty<string>() });
        return io;
    }
}
