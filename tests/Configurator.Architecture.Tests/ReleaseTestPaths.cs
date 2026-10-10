using System.Diagnostics;
using System.Security.Cryptography;

namespace Configurator.Architecture.Tests;

internal static class ReleaseTestPaths
{
    internal static string Root { get; } = FindRoot();
    internal static string PackageRoot => Environment.GetEnvironmentVariable("CONFIGURATOR_PACKAGE_ROOT")
        ?? Path.Combine(Root, "artifacts/configurator");
    internal static string StorePackageRoot => Environment.GetEnvironmentVariable("CONFIGURATOR_STORE_PACKAGE_ROOT")
        ?? Path.Combine(Root, "artifacts/configurator-with-store");
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static async Task<(int ExitCode, string Output)> Run(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        process.Start();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        return (process.ExitCode, await output + await error);
    }

    private static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Configurator.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Configurator root not found.");
    }
}
