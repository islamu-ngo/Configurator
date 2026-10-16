using System.Collections.ObjectModel;

namespace Configurator.Cli.Tests;

internal static class SetupCliContractSpecification
{
    internal static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Operations =
        new ReadOnlyDictionary<string, IReadOnlySet<string>>(
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["catalogue"] = new HashSet<string>(["list", "show", "describe"], StringComparer.Ordinal),
                ["artifact"] = new HashSet<string>(["create", "open", "validate", "format", "diff", "coverage", "export", "import", "check-existing", "prefill", "bundle-check", "bundle-create", "preview"], StringComparer.Ordinal),
                ["env"] = new HashSet<string>(["render", "validate"], StringComparer.Ordinal),
                ["doctor"] = new HashSet<string>(["doctor"], StringComparer.Ordinal),
                ["definition"] = new HashSet<string>(["init", "check", "build", "revise"], StringComparer.Ordinal)
            });

    internal static bool IsInvocation(string family, string operation) =>
        Operations.TryGetValue(family, out IReadOnlySet<string>? operations)
            && (operations.Contains(operation) || family != "doctor" && operation == "help")
        || family == "cli" && operation == "help";

    internal static readonly IReadOnlyDictionary<string, int> ExitCodes =
        new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["success"] = 0,
            ["validation"] = 2,
            ["incomplete"] = 3,
            ["blocked"] = 4,
            ["usage"] = 64,
            ["data"] = 65,
            ["internal"] = 70,
            ["io"] = 74,
            ["cancelled"] = 130
        });
}
