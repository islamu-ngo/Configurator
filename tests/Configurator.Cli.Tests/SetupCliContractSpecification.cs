using System.Collections.ObjectModel;
using Configurator.Cli;

namespace Configurator.Cli.Tests;

internal static class SetupCliContractSpecification
{
    internal static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Operations =
        new ReadOnlyDictionary<string, IReadOnlySet<string>>(
            SetupCliCommandSchemaMetadata.Commands.ToDictionary(
                command => command.Name,
                command => (IReadOnlySet<string>)command.Operations.Select(operation => operation.Name)
                    .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal));

    internal static readonly IReadOnlyDictionary<string, int> ExitCodes =
        new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["success"] = 0, ["validation"] = 2, ["incomplete"] = 3, ["blocked"] = 4,
            ["usage"] = 64, ["data"] = 65, ["internal"] = 70, ["io"] = 74
        });
}
