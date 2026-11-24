using System.Reflection;
using Configurator.Core;

namespace Configurator.Cli;

/// <summary>Reads compiled assembly metadata without invoking git or reading external files.</summary>
internal static class CliBuildInfo
{
    private const int MaximumVersionLength = 128;

    internal static string ApplicationName => CliExecutableMarker.ExecutableName;

    internal static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        Assembly assembly = typeof(CliBuildInfo).Assembly;
        string? raw = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return ConfiguratorVersion.Current;

        string trimmed = raw.Trim();
        if (trimmed.Length > MaximumVersionLength)
            trimmed = trimmed[..MaximumVersionLength];

        foreach (char character in trimmed)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
                return ConfiguratorVersion.Current;
        }

        return trimmed;
    }
}
