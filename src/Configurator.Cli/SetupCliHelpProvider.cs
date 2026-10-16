using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace Configurator.Cli;

public sealed record SetupCliHelpView(string Scope, IReadOnlyList<string> CommandPath,
    IReadOnlyList<string> Commands, IReadOnlyList<SetupCliHelpOption> Options);

public sealed record SetupCliHelpOption(string Name, bool RequiresValue, bool Repeatable);

internal sealed class SetupCliHelpProvider : IHelpProvider
{
    internal SetupCliHelpView? View { get; private set; }

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
            .Where(item => command is not null || SetupCliCommandRegistry.Families.Any(family => family.Name == item.Name))
            .Select(item => item.Name).Order(StringComparer.Ordinal).ToArray();
        SetupCliHelpOption[] options = [];
        if (command is { IsBranch: false })
        {
            SetupCliCommandRegistry.TryResolve(commandPath[0], command.Name, out SetupCliOperationDescriptor? descriptor);
            options = chain.SelectMany(item => item.Parameters).OfType<ICommandOption>()
                .Where(option => !option.IsHidden)
                .SelectMany(option => option.LongNames.Select(name => new SetupCliHelpOption("--" + name,
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
