using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace Configurator.Cli;

internal sealed class CliHelpProvider : IHelpProvider
{
    internal CliHelpView? View { get; private set; }

    public IEnumerable<IRenderable> Write(ICommandModel model, ICommandInfo? command)
    {
        var path = new Stack<string>();
        var chain = new List<ICommandInfo>();
        for (ICommandInfo? current = command; current is not null; current = current.Parent)
        {
            path.Push(current.Name);
            chain.Add(current);
        }
        string[] commandPath = path.ToArray();
        string[] commands = (command is null ? model.Commands : command.Commands)
            .Where(item => !item.IsHidden)
            .Where(item => command is not null || CliCommandRegistry.Families.Any(family => family.Name == item.Name))
            .Select(item => item.Name).Order(StringComparer.Ordinal).ToArray();
        CliHelpOption[] options = [];
        if (command is { IsBranch: false })
        {
            CliCommandRegistry.TryResolve(commandPath[0], command.Name, out CliOperationDescriptor? descriptor);
            options = chain.SelectMany(item => item.Parameters).OfType<ICommandOption>()
                .Where(option => !option.IsHidden)
                .SelectMany(option => option.LongNames.Select(name => new CliHelpOption("--" + name,
                    option.ValueName is not null && !option.ValueIsOptional,
                    descriptor?.Options.Any(item => item.Name == "--" + name && item.Repeatable) == true)))
                .Append(new("--help", false, false))
                .DistinctBy(option => option.Name, StringComparer.Ordinal)
                .OrderBy(option => option.Name, StringComparer.Ordinal).ToArray();
        }
        View = new(command is null ? "root" : command.IsBranch ? "branch" : "leaf",
            Array.AsReadOnly(commandPath), Array.AsReadOnly(commands), Array.AsReadOnly(options));
        return [];
    }
}
