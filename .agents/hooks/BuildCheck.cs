using System.Diagnostics;

if (args is not [string project] || !project.EndsWith(".csproj", StringComparison.Ordinal) || !File.Exists(project))
{
    Console.Error.WriteLine("usage: dotnet run .agents/hooks/BuildCheck.cs -- <affected-project.csproj>");
    return 64;
}

using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
using var process = new Process
{
    StartInfo = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    },
};
foreach (string argument in new[] { "build", project, "--configuration", "Release", "--verbosity", "quiet", "--nologo" })
{
    process.StartInfo.ArgumentList.Add(argument);
}
process.Start();
Task<string> stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
Task<string> stderr = process.StandardError.ReadToEndAsync(deadline.Token);
try
{
    await Task.WhenAll(process.WaitForExitAsync(deadline.Token), stdout, stderr);
    Console.Out.Write(await stdout);
    Console.Error.Write(await stderr);
    return process.ExitCode;
}
catch (OperationCanceledException)
{
    if (!process.HasExited) process.Kill(entireProcessTree: true);
    Console.Error.WriteLine("build_check_timeout");
    return 1;
}
