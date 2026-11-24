using System.Collections.ObjectModel;

namespace Configurator.Cli.Tests;

internal static class CliContractSpecification
{
    internal static readonly IReadOnlySet<string> InstallationStates = new HashSet<string>(
        ["not-applicable", "not-attempted", "not-established", "established", "unknown"], StringComparer.Ordinal);
    internal static readonly IReadOnlySet<string> VerificationStates = new HashSet<string>(
        ["not-applicable", "not-attempted", "succeeded", "failed", "unknown"], StringComparer.Ordinal);

    internal static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Operations =
        new ReadOnlyDictionary<string, IReadOnlySet<string>>(
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["info"] = new HashSet<string>(["info"], StringComparer.Ordinal),
                ["schema"] = new HashSet<string>(["list", "export"], StringComparer.Ordinal),
                ["catalogue"] = new HashSet<string>(["list", "describe"], StringComparer.Ordinal),
                ["artifact"] = new HashSet<string>(["create", "validate", "diff", "coverage", "import", "check-existing", "compare", "prefill", "bundle-check", "bundle-create"], StringComparer.Ordinal),
                ["doctor"] = new HashSet<string>(["doctor"], StringComparer.Ordinal),
                ["secret"] = new HashSet<string>(["generate", "derive"], StringComparer.Ordinal),
                ["profile"] = new HashSet<string>(["save", "list", "inspect", "unlock", "diff", "upgrade-plan", "adopt", "slots", "recovery-check"], StringComparer.Ordinal),
                ["definition"] = new HashSet<string>(["init", "check", "build", "revise", "lint", "inspect", "describe", "diff", "test", "answer-schema"], StringComparer.Ordinal)
            });

    internal static readonly IReadOnlyDictionary<string, string> SchemaIds =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["answers"] = "urn:islamu:configurator:answers:v0.0.1",
            ["authoring"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["command"] = "urn:islamu:configurator:cli:v0.0.1",
            ["fixtures"] = "urn:islamu:configurator:fixtures:v0.0.1",
            ["runtime"] = "urn:islamu:configurator:setup-definition:v0.0.1",
            ["store-curation"] = "urn:islamu:configurator:store-curation:v0.0.1"
        });

    internal static readonly IReadOnlySet<string> Capabilities = new HashSet<string>(
        ["predicate.item", "predicate.root", "presentation.next-actions", "projection.copy", "projection.index", "projection.list", "projection.literal", "projection.object",
            "projection.omit", "projection.repeat", "relationship.presence", "schema.array", "schema.boolean", "schema.bounds", "schema.const",
            "schema.enum", "schema.integer", "schema.notBlank", "schema.nullable", "schema.number", "schema.object",
            "schema.string", "schema.uniqueBy", "schema.map", "schema.variant", "schema.localRef",
            "schema.multipleOf", "schema.uniqueItems", "schema.secret", "values.initial", "values.suggested", "values.suggested-from",
            "deployment.scope", "environment.docker-run", "environment.compose"], StringComparer.Ordinal);

    internal static readonly IReadOnlySet<string> FindingClassifications = new HashSet<string>(
        ["missing-required", "invalid-supplied-value", "unrecognized-input", "structural-output-change",
            "unassessed", "context-derived-value"], StringComparer.Ordinal);

    internal static bool IsInvocation(string family, string operation) =>
        Operations.TryGetValue(family, out IReadOnlySet<string>? operations)
            && (operations.Contains(operation) || family is not ("doctor" or "info") && operation == "help")
        || family == "cli" && operation is "help" or "version";

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
            ["io"] = 74,
            ["cancelled"] = 130
        });
}
