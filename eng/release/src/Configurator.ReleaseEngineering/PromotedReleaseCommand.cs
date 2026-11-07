using System.Text.Json;

namespace Configurator.ReleaseEngineering;

/// <summary>
/// Launches only publication commands from an authenticated promotion, using a private runtime
/// stage and the independently installed launcher's promotion authority, never candidate code.
/// Child output is discarded because it may contain operator inputs or secrets.
/// </summary>
public static class PromotedReleaseCommand
{
    private static readonly TimeSpan ProcessTimeout = PublicationInventoryVerificationBudget.PromotedProcessTimeout;

    public static int Run(string[] args, TextWriter output, string repositoryRoot)
    {
        if (args.Length < 2 || args[0] != "run-promoted" ||
            args[1] is not ("sync-public-changelog" or "prepare-publication-inventory"))
        {
            output.WriteLine("promoted_command_invalid_arguments");
            return Program.UsageError;
        }

        try
        {
            string Required(string name) => Environment.GetEnvironmentVariable(name) ?? string.Empty;
            string candidate = Path.GetFullPath(repositoryRoot);
            var request = new TrustedBundleVerificationRequest(
                Required("CONFIGURATOR_RELEASE_TRUSTED_BUNDLE"), candidate,
                new PromotionAuthorityInput(Required("CONFIGURATOR_RELEASE_PROMOTION_RECEIPT"),
                    Required("CONFIGURATOR_RELEASE_PROMOTION_SIGNATURE"), Required("CONFIGURATOR_RELEASE_PROMOTION_PRINCIPAL")),
                Required("CONFIGURATOR_RELEASE_BUNDLE_ID"), Required("CONFIGURATOR_RELEASE_BUNDLE_VERSION"),
                Required("CONFIGURATOR_RELEASE_POLICY_VERSION"), Required("CONFIGURATOR_RELEASE_CONFIG_VERSION"),
                Required("CONFIGURATOR_RELEASE_TRUST_VERSION"))
            { ExpectedManifestDigest = Required("CONFIGURATOR_RELEASE_MANIFEST_SHA256") };
            TrustedBundleResult trusted = TrustedBundlePolicy.Verify(request);
            if (!trusted.IsValid || trusted.Bundle is null)
                return Reject(output, "promoted_command_trusted_bundle_invalid");

            string bundle = trusted.Bundle.Root;
            string runtime = Path.GetFullPath(AppContext.BaseDirectory);
            if (Overlaps(runtime, candidate) || Overlaps(runtime, bundle))
                return Reject(output, "promoted_command_runtime_overlap");

            using TrustedBundleRuntime staged = TrustedBundleRuntime.Stage(trusted.Bundle, candidate, packageVerification: false);
            if (!TrustedBundlePolicy.Verify(request).IsValid)
                return Reject(output, "promoted_command_trusted_bundle_invalid");
            int code = staged.RunEngine(args[1..], candidate, ProcessTimeout);
            output.WriteLine("promoted_command_completed");
            return code;
        }
        catch (OperationCanceledException) { return Reject(output, "promoted_command_timeout"); }
        catch (InvalidDataException) { return Reject(output, "promoted_command_runtime_incomplete"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            ArgumentException or JsonException or InvalidOperationException or System.ComponentModel.Win32Exception or KeyNotFoundException)
        {
            return Reject(output, "promoted_command_failed");
        }
    }

    private static bool Overlaps(string left, string right)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        left = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
        right = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
        return left.Equals(right, comparison) ||
            left.StartsWith(right + Path.DirectorySeparatorChar, comparison) ||
            right.StartsWith(left + Path.DirectorySeparatorChar, comparison);
    }

    private static int Reject(TextWriter output, string diagnostic)
    {
        output.WriteLine(diagnostic);
        return Program.ToolchainRejected;
    }
}
