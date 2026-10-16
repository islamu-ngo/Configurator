namespace Configurator.Cli;

internal sealed record SetupCliPreflight(
    SetupCliCommand Command,
    string[] Arguments,
    bool RunSpectre,
    string? Error);

internal static class SetupCliArgumentPreflight
{
    private static readonly Dictionary<string, bool> ValueOptions = SetupCliCommandRegistry.Families
        .SelectMany(family => family.Operations).SelectMany(operation => operation.Options)
        .GroupBy(option => option.Name, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First().RequiresValue, StringComparer.Ordinal);

    internal static SetupCliMode Mode(IReadOnlyList<string> arguments) =>
        OptionTokens(arguments).Contains("--machine", StringComparer.Ordinal) ? SetupCliMode.Machine : SetupCliMode.Text;

    internal static SetupCliPreflight Inspect(SetupCliInvocation invocation)
    {
        string[] arguments = invocation.Arguments.ToArray();
        string[] tokens = OptionTokens(arguments).ToArray();
        bool machine = invocation.Mode == SetupCliMode.Machine || tokens.Contains("--machine", StringComparer.Ordinal);
        bool text = tokens.Contains("--text", StringComparer.Ordinal);
        if (arguments.Length > 256 || arguments.Any(argument => argument.Length > 4096)
            || arguments.Sum(argument => System.Text.Encoding.UTF8.GetByteCount(argument) + 1) > 32_768)
            return Failure(machine, "argument-shape-invalid");
        if (arguments.Any(argument => argument.Any(char.IsControl)))
            return Failure(machine, "argument-shape-invalid");

        string family = "cli";
        string operation = "help";
        bool help = arguments.Length == 0 || tokens.Any(token => token is "--help" or "-h");
        IReadOnlyList<SetupCliOptionDescriptor>? options = null;
        int optionStart = 0;
        if (arguments.Length > 0 && !arguments[0].StartsWith('-'))
        {
            family = arguments[0];
            SetupCliFamilyDescriptor? branch = SetupCliCommandRegistry.Families.FirstOrDefault(item => item.Name == family);
            if (branch is null)
                return Failure(machine, "command-unknown");

            optionStart = 1;
            if (family == "doctor")
            {
                operation = "doctor";
                options = branch.Operations[0].Options;
            }
            else if (arguments.Length > 1 && !arguments[1].StartsWith('-'))
            {
                operation = arguments[1];
                if (!SetupCliCommandRegistry.TryResolve(family, operation, out SetupCliOperationDescriptor? descriptor))
                    return Failure(machine, "operation-unknown", family);
                options = descriptor!.Options;
                optionStart = 2;
            }
            else
                help = true;
        }

        if (options is null)
        {
            // Root and branch admission must not expose framework-owned diagnostic/export routes.
            if (arguments.Skip(optionStart).Any(argument => argument is not ("--help" or "-h" or "--machine" or "--text")))
                return Failure(machine, "command-unknown", family);
            help = true;
        }

        if (machine && text)
            return Failure(machine, "mode-conflict", family, operation);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string token in tokens)
        {
            string name = token.Split('=', 2)[0];
            if (name == "-h") name = "--help";
            bool singleton = name is "--machine" or "--text" or "--help"
                || options?.Any(option => option.Name == name && !option.Repeatable) == true;
            if (singleton && !seen.Add(name))
                return Failure(machine, "option-duplicate", family, operation);
        }

        var command = new SetupCliCommand(family, operation, machine, false, help, null, null, null, null, null, null);
        return new(command, NormalizeStandardIo(arguments, options), true, null);
    }

    private static IEnumerable<string> OptionTokens(IReadOnlyList<string> arguments)
    {
        for (int index = 0; index < arguments.Count; index++)
        {
            string token = arguments[index];
            if (token == "--") yield break;
            if (!token.StartsWith('-')) continue;
            yield return token;
            if (!token.Contains('=') && ValueOptions.TryGetValue(token, out bool requiresValue) && requiresValue
                && index + 1 < arguments.Count && !arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
                index++;
        }
    }

    private static string[] NormalizeStandardIo(string[] arguments, IReadOnlyList<SetupCliOptionDescriptor>? options)
    {
        var normalized = new List<string>(arguments.Length);
        for (int index = 0; index < arguments.Length; index++)
        {
            string argument = arguments[index];
            if (argument == "--")
            {
                normalized.AddRange(arguments.Skip(index));
                break;
            }
            // Spectre strict parsing accepts a lone dash as an equals value, not a separate token.
            if (index + 1 < arguments.Length && arguments[index + 1] == "-"
                && options?.Any(option => option.Name == argument && option.RequiresValue) == true)
            {
                normalized.Add(argument + "=-");
                index++;
            }
            else normalized.Add(argument);
        }
        return normalized.ToArray();
    }

    private static SetupCliPreflight Failure(bool machine, string error, string family = "cli", string operation = "help") =>
        new(new SetupCliCommand(family, operation, machine, false, false, null, null, null, null, null, null), [], false, error);
}
