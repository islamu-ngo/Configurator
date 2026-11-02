using System.Reflection;
using Configurator.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Configurator.Cli;

internal sealed record CliOperationDescriptor(
    string Name,
    Type SettingsType,
    IReadOnlyList<CliOptionDescriptor> Options,
    Action<IConfigurator<CliCommandSettings>, string, CliCommandRuntime> RegisterBranch,
    Action<IConfigurator, string, CliCommandRuntime> RegisterRoot);

