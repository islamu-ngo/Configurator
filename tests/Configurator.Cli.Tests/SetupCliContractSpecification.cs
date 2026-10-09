using System.Collections.ObjectModel;

namespace Configurator.Cli.Tests;

internal static class SetupCliContractSpecification
{
    internal static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Operations =
        new ReadOnlyDictionary<string, IReadOnlySet<string>>(
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["catalogue"] = new HashSet<string>(["list", "show", "describe"], StringComparer.Ordinal),
                ["artifact"] = new HashSet<string>(["create", "open", "validate", "format", "diff", "coverage", "export", "import", "check-existing", "prefill", "preview"], StringComparer.Ordinal),
                ["env"] = new HashSet<string>(["render", "validate"], StringComparer.Ordinal),
                ["doctor"] = new HashSet<string>(["doctor"], StringComparer.Ordinal)
            });

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
            ["io"] = 74
        });
}
