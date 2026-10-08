using Configurator.ReleaseEngineering;

namespace Configurator.ReleaseEngineering.Tests;

public sealed class SecurityChangeSelectionTests
{
    [Test]
    [Arguments("src/Configurator.Core/Definitions/DefinitionAdmission.cs")]
    [Arguments("src/Configurator.Artifacts/ProtectedArtifactWriter.cs")]
    [Arguments("src/Configurator.Presentation/WorkspaceState.cs")]
    [Arguments("src/Configurator.Browser/Pages/Launch.razor")]
    public async Task AuthorityOnlyChangeSelectsRealExecution(string path)
    {
        await Assert.That(SecurityChangeSelection.RequiresExecution([path])).IsTrue();
    }

    [Test]
    [Arguments("Directory.Build.props")]
    [Arguments("Directory.Packages.props")]
    [Arguments("NuGet.Config")]
    [Arguments("global.json")]
    [Arguments("src/Configurator.Cli/Program.cs")]
    [Arguments("eng/release/policy/release-policy.yaml")]
    [Arguments("tests/Configurator.Core.Tests/DefinitionAdmissionTests.cs")]
    [Arguments(".github/workflows/security-tests.yml")]
    public async Task ExistingSecurityPathRemainsSelected(string path)
    {
        await Assert.That(SecurityChangeSelection.RequiresExecution([path])).IsTrue();
    }

    [Test]
    [Arguments("README.md")]
    [Arguments("docs/public/changelog/README.md")]
    [Arguments("docs/internal/extraction/FOUNDATION.md")]
    public async Task UnrelatedChangeRemainsAnHonestNoOp(string path)
    {
        await Assert.That(SecurityChangeSelection.RequiresExecution([path])).IsFalse();
    }

    [Test]
    public async Task EmptyDetectionSelectsExecutionFailSafe()
    {
        await Assert.That(SecurityChangeSelection.RequiresExecution([])).IsTrue();
    }

    [Test]
    public async Task MissingChangedPathsFileSelectsExecutionFailSafe()
    {
        using var output = new StringWriter();
        string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "changed-paths.txt");

        int code = SecurityChangeSelection.Run(["select-security-changes", missingPath], output);

        await Assert.That(code).IsEqualTo(Program.Success);
        await Assert.That(output.ToString()).IsEqualTo("run-tests=true" + Environment.NewLine);
    }
}
