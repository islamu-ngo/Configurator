using System.Collections.ObjectModel;

namespace Configurator.Cli.Tests;

internal static class SetupCliContractSpecification
{
    internal static readonly IReadOnlySet<string> InstallationStates = new HashSet<string>(
        ["not-applicable", "not-attempted", "not-established", "established", "unknown"], StringComparer.Ordinal);
    internal static readonly IReadOnlySet<string> VerificationStates = new HashSet<string>(
        ["not-applicable", "not-attempted", "succeeded", "failed", "unknown"], StringComparer.Ordinal);

    internal static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Operations =
        new ReadOnlyDictionary<string, IReadOnlySet<string>>(
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["catalogue"] = new HashSet<string>(["list", "show", "describe"], StringComparer.Ordinal),
                ["artifact"] = new HashSet<string>(["create", "open", "validate", "format", "diff", "coverage", "export", "import", "check-existing", "compare", "prefill", "bundle-check", "bundle-create", "preview"], StringComparer.Ordinal),
                ["env"] = new HashSet<string>(["render", "validate"], StringComparer.Ordinal),
                ["doctor"] = new HashSet<string>(["doctor"], StringComparer.Ordinal),
                ["definition"] = new HashSet<string>(["init", "check", "build", "revise", "lint", "schema"], StringComparer.Ordinal)
            });

    internal static readonly IReadOnlyDictionary<string, string> SchemaIds =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["authoring"] = "urn:islamu:configurator:authoring:v2",
            ["runtime"] = "urn:islamu:configurator:setup-definition:v2",
            ["answers"] = "urn:islamu:configurator:answers:v1",
            ["command"] = "https://schemas.islamu.org/configurator/command/v4/schema.json"
        });

    internal static readonly IReadOnlySet<string> Capabilities = new HashSet<string>(
        ["predicate.item", "predicate.root", "projection.copy", "projection.index", "projection.list", "projection.literal", "projection.object",
            "projection.omit", "projection.repeat", "relationship.presence", "schema.array", "schema.boolean", "schema.bounds", "schema.const",
            "schema.enum", "schema.integer", "schema.notBlank", "schema.nullable", "schema.number", "schema.object",
            "schema.string", "schema.uniqueBy", "schema.map", "schema.variant", "schema.localRef",
            "schema.multipleOf", "schema.uniqueItems", "schema.assertFormat", "values.initial", "values.suggested"], StringComparer.Ordinal);

    internal static readonly IReadOnlySet<string> FindingClassifications = new HashSet<string>(
        ["missing-required", "invalid-supplied-value", "unrecognized-input", "structural-output-change",
            "unassessed", "context-derived-value"], StringComparer.Ordinal);

    internal static bool IsInvocation(string family, string operation) =>
        Operations.TryGetValue(family, out IReadOnlySet<string>? operations)
            && (operations.Contains(operation) || family != "doctor" && operation == "help")
        || family == "cli" && operation == "help";

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
