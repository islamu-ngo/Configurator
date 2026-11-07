using System.Text;

namespace Configurator.AgentWorkflow.Tests;

[NotInParallel("AgentWorkflowConsole")]
public sealed class GuardTests
{
    [Test]
    public async Task IntentsYamlMustBeOneValidDocument()
    {
        using var fixture = new IntentFixture();
        string path = fixture.Path;
        File.WriteAllText(path, IntentFixture.ValidYaml, new UTF8Encoding(false));
        await Assert.That(Invoke(["validate-intents", path])).IsEqualTo(0);

        File.WriteAllText(path, "intents:\n  - id: [\n", new UTF8Encoding(false));
        await Assert.That(Invoke(["validate-intents", path])).IsEqualTo(2);

        File.WriteAllText(path, IntentFixture.ValidYaml, Encoding.Unicode);
        await Assert.That(Invoke(["validate-intents", path])).IsEqualTo(2);

        File.WriteAllText(path, IntentFixture.ValidYaml + "---\n" + IntentFixture.ValidYaml);
        await Assert.That(Invoke(["validate-intents", path])).IsEqualTo(2);
        await Assert.That(Invoke(["validate-intents", string.Empty])).IsEqualTo(2);
    }

    [Test]
    [Arguments("version: 1\nintents: []\n")]
    [Arguments("version: 1\nintents: [{id: incomplete}]\n")]
    [Arguments("version: '1'\nintents: []\n")]
    [Arguments("version: 1\nversion: 2\nintents: []\n")]
    [Arguments("version: 1\nintents: &cycle [*cycle]\n")]
    public async Task InvalidIntentStructureRejectsWithSafeDiagnostic(string yaml)
    {
        using var fixture = new IntentFixture();
        File.WriteAllText(fixture.Path, yaml);
        await Assert.That(Invoke(["validate-intents", fixture.Path])).IsEqualTo(2);
    }

    [Test]
    [Arguments("category: domain", "category: imaginary")]
    [Arguments("title: Synthetic contribution", "title: short")]
    [Arguments("load_skills: []", "load_skills: missing")]
    [Arguments("paths_in_scope: [src/**/*.cs]", "paths_in_scope: [../outside/**]")]
    [Arguments("paths_in_scope: [src/**/*.cs]", "paths_in_scope: ['C:/outside/**']")]
    [Arguments("must_read_docs: [AGENTS.md]", "must_read_docs: [docs/missing.md]")]
    [Arguments("load_skills: []", "load_skills: [missing-skill]")]
    [Arguments("load_rules: []", "load_rules: [.agents/rules/missing.md]")]
    [Arguments("minimum_tests: []", "minimum_tests: [Configurator.Missing.Tests]")]
    [Arguments("docs_to_update: []", "docs_to_update: [docs/missing.md]")]
    [Arguments("tier: standard", "tier: invented")]
    [Arguments("review_protocol: peer_review", "review_protocol: 7")]
    [Arguments("review_protocol: peer_review", "review_protocol: 9999999999999999999999999999999999999999999999999999999")]
    [Arguments("  - id: synthetic", "  - unknown: value\n    id: synthetic")]
    public async Task InvalidIntentSemanticsRejectAtCommandBoundary(string before, string after)
    {
        using var fixture = new IntentFixture();
        File.WriteAllText(fixture.Path, IntentFixture.ValidYaml.Replace(before, after, StringComparison.Ordinal));
        await Assert.That(Invoke(["validate-intents", fixture.Path])).IsEqualTo(2);
    }

    [Test]
    public async Task DuplicateIntentIdentityRejects()
    {
        using var fixture = new IntentFixture();
        File.WriteAllText(fixture.Path, IntentFixture.ValidYaml +
            IntentFixture.ValidYaml[IntentFixture.ValidYaml.IndexOf("  - id:", StringComparison.Ordinal)..]);
        await Assert.That(Invoke(["validate-intents", fixture.Path])).IsEqualTo(2);
    }

    [Test]
    public async Task ValidIntentAllowsFutureEditGlobs()
    {
        using var fixture = new IntentFixture();
        File.WriteAllText(fixture.Path, IntentFixture.ValidYaml);
        await Assert.That(Invoke(["validate-intents", fixture.Path])).IsEqualTo(0);
    }

    [Test]
    public async Task RejectedReferenceProducesOnlyFixedCode()
    {
        using var fixture = new IntentFixture();
        File.WriteAllText(fixture.Path, IntentFixture.ValidYaml.Replace("AGENTS.md", "docs/private-sentinel.md", StringComparison.Ordinal));
        TextWriter originalOutput = Console.Out;
        TextWriter originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exit;
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            exit = Program.Main(["validate-intents", fixture.Path]);
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
        await Assert.That(exit).IsEqualTo(2);
        await Assert.That(output.ToString()).IsEqualTo(string.Empty);
        await Assert.That(error.ToString()).IsEqualTo("invalid_intents_reference" + Environment.NewLine);
    }

    [Test]
    public async Task CommitGuardAcceptsOnlyDistinctLiteralFiles()
    {
        await Assert.That(Invoke([
            "validate-commit", "--", "git", "commit", "--only", "-m", "safe", "--", "src/Event.cs", "docs/README.md",
        ])).IsEqualTo(0);

        string[][] unsafePaths = [["."], ["src/*.cs"], ["../escape.cs"], ["src/"], [":(glob)src/*.cs"], ["src/Event.cs", "src/Event.cs"]];
        foreach (string[] paths in unsafePaths)
        {
            await Assert.That(Invoke(["validate-commit", "--", "git", "commit", "--", .. paths])).IsEqualTo(2);
        }

        await Assert.That(Invoke(["validate-commit", "--", "git", "commit", "--", ".", "--", "src/Event.cs"])).IsEqualTo(2);
        await Assert.That(Invoke(["validate-commit", "--", "git", "commit", "--all", "--", "src/Event.cs"])).IsEqualTo(2);
    }

    private static int Invoke(string[] args)
    {
        TextWriter output = Console.Out;
        TextWriter error = Console.Error;
        try
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            return Program.Main(args);
        }
        finally
        {
            Console.SetOut(output);
            Console.SetError(error);
        }
    }

    private sealed class IntentFixture : IDisposable
    {
        internal const string ValidYaml = """
            version: 1
            intents:
              - id: synthetic
                title: Synthetic contribution
                category: domain
                triggers: [synthetic]
                must_read_docs: [AGENTS.md]
                load_skills: []
                load_rules: []
                paths_in_scope: [src/**/*.cs]
                minimum_tests: []
                docs_to_update: []
                unique_acceptance: [Independent acceptance]
                pr_checklist: [Verify the contract]
                forbidden_without_approval: []
                criticality:
                  tier: standard
                  required_model_tier: economical
                  verification_depth: standard
                  tenancy_scope: system_level
                  rollback_strategy: atomic_revert
                  intake_clarification_mode: autonomous_defaults
                  exploration_protocol: bounded_caller_callee
                  testing_strategy: behavioral_unit_integration
                  review_protocol: peer_review
                  safety_gates: [no_private_to_public_projection]

            """;

        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"intents-{Guid.NewGuid():N}");
        internal string Path => System.IO.Path.Combine(_root, ".agents", "contract", "intents.yaml");

        internal IntentFixture()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var repository = new DirectoryInfo(AppContext.BaseDirectory);
            while (repository is not null && !File.Exists(System.IO.Path.Combine(repository.FullName, ".agents", "contract", "schema.json")))
                repository = repository.Parent;
            File.Copy(System.IO.Path.Combine(repository!.FullName, ".agents", "contract", "schema.json"),
                System.IO.Path.Combine(_root, ".agents", "contract", "schema.json"));
            File.WriteAllText(System.IO.Path.Combine(_root, "AGENTS.md"), "Synthetic referenced document.");
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
