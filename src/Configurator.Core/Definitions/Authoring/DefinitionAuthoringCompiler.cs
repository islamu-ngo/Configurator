namespace Configurator.Core.Definitions.Authoring;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;

/// <summary>
/// Lowers authoring conveniences (named predicates, single-source choices, authored order, value-keyed
/// localization and concise outputs) into the runtime contract, then delegates every semantic check to the sole
/// runtime admission owner.
/// </summary>
public static class DefinitionAuthoringCompiler
{
    public static Definition Compile(DefinitionAuthoringDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        try
        {
            JsonObject root = JsonNode.Parse(document.CanonicalBytes.Span)!.AsObject();
            Only(root, "$schema", "identity", "project", "applicationVersions", "entryPoints", "configuration",
                "outputs", "presentation", "localization", "annotations", "deployment", "predicates");
            if (root["$schema"]!.GetValue<string>() != DefinitionAuthoringCodec.SchemaId)
                throw new DefinitionException("authoring-schema");
            DefinitionPredicateLowering.Lower(root);
            JsonObject identity = root["identity"]!.AsObject();
            Only(identity, "familyId", "revision");
            JsonObject project = root["project"]!.DeepClone().AsObject();
            if (!project.ContainsKey("publisher")) project["publisher"] = project["name"]!.DeepClone();
            var locales = new SortedSet<string>(StringComparer.Ordinal) { "en" };
            if (root["localization"] is JsonObject localization)
            {
                locales.Clear();
                locales.Add(localization["defaultLocale"]!.GetValue<string>());
                foreach ((string locale, _) in localization["translations"]!.AsObject()) locales.Add(locale);
            }
            var runtime = new JsonObject
            {
                ["schemaVersion"] = DefinitionProfile.RuntimeSchemaVersion,
                ["familyId"] = identity["familyId"]!.DeepClone(),
                ["revision"] = identity["revision"]!.DeepClone(),
                ["project"] = project,
                ["applicationVersions"] = root["applicationVersions"]!.DeepClone(),
                ["entryPoints"] = root.ContainsKey("entryPoints") ? root["entryPoints"]!.DeepClone() : new JsonArray("setup"),
                ["locales"] = new JsonArray(locales.Select(locale => (JsonNode?)JsonValue.Create(locale)).ToArray()),
                ["configuration"] = LowerConfiguration(root["configuration"]!, document.SourceMap)
            };
            foreach (string name in new[] { "presentation", "localization", "annotations", "deployment" })
                if (root.ContainsKey(name)) runtime[name] = root[name]!.DeepClone();
            DefinitionLocalizationLowering.Lower(runtime);
            runtime["artifacts"] = DefinitionOutputLowering.Lower(root["outputs"]!.AsObject(), runtime["configuration"]!.AsObject());
            Definition admitted = DefinitionCodec.AdmitCompiled(runtime);
            return new Definition(admitted.Document, admitted.CanonicalBytes.ToArray(),
                admitted.Configuration, document.SourceMap);
        }
        catch (DefinitionException exception)
        {
            throw exception.WithLocation(document.SourceMap, exception.SchemaPath ?? "");
        }
        catch (CompositionException)
        {
            throw new DefinitionException("document-limit").WithLocation(document.SourceMap, "");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException
            or ArgumentException or NullReferenceException or FormatException or OverflowException)
        {
            throw new DefinitionException("authoring-invalid").WithLocation(document.SourceMap, "");
        }
    }

    private static JsonObject LowerConfiguration(JsonNode source, DefinitionSourceMap sourceMap)
    {
        JsonObject configuration = source.DeepClone().AsObject();
        DefinitionSchemaLowering.Lower(configuration, "/configuration", sourceMap);
        return configuration;
    }

    /// <summary>The deterministic artifact identifier assigned to an output that declares no explicit id.</summary>
    public static string OutputId(string fileName) => DefinitionOutputLowering.OutputId(fileName);
    private static void Only(JsonObject node, params string[] allowed)
    {
        if (node.Any(pair => !allowed.Contains(pair.Key, StringComparer.Ordinal)))
            throw new DefinitionException("authoring-member");
    }
}
