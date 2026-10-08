namespace Configurator.ReleaseEngineering;

public static class SecurityChangeSelection
{
    private static readonly string[] Prefixes =
    [
        "src/Configurator.Core/",
        "src/Configurator.Artifacts/",
        "src/Configurator.Presentation/",
        "src/Configurator.Browser/",
        "src/Configurator.Cli/",
        "src/Configurator.Terminal/",
        "src/Configurator.Desktop/",
        "tests/Configurator.",
        "eng/release/",
        "eng/agent-workflow/"
    ];

    private static readonly string[] ExactPaths =
    [
        "Directory.Build.props",
        "Directory.Packages.props",
        "NuGet.Config",
        "global.json",
        ".github/workflows/security-tests.yml"
    ];

    public static bool RequiresExecution(IEnumerable<string> changedPaths)
    {
        bool anyPath = false;
        foreach (string path in changedPaths)
        {
            if (path.Length == 0)
            {
                continue;
            }

            anyPath = true;
            if (ExactPaths.Contains(path, StringComparer.Ordinal) ||
                Prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return !anyPath;
    }

    public static int Run(string[] args, TextWriter output)
    {
        if (args.Length != 2)
        {
            output.WriteLine("invalid_arguments: select-security-changes requires a changed-paths file");
            return Program.UsageError;
        }

        bool selected;
        try
        {
            selected = RequiresExecution(File.ReadLines(args[1]));
        }
        catch (IOException)
        {
            selected = true;
        }
        catch (UnauthorizedAccessException)
        {
            selected = true;
        }

        output.WriteLine(selected ? "run-tests=true" : "run-tests=false");
        return Program.Success;
    }
}
