namespace Configurator.Core.Profiles;

using System.Text.Json;
using Configurator.Core.Definitions;

/// <summary>Compares admitted schemas, using schema coordinates rather than answer paths for collections.</summary>
public static class DefinitionSchemaDiff
{
    public static IReadOnlyList<DefinitionSchemaDelta> Compare(Definition source, Definition target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (source.FamilyId != target.FamilyId) throw new DefinitionException("family-mismatch");
        _ = new DefinitionRegistry([source, target]);
        var changes = new List<DefinitionSchemaDelta>();
        Visit(source.Configuration, target.Configuration, "/configuration");
        return Array.AsReadOnly(changes.OrderBy(change => change.SchemaPath, StringComparer.Ordinal)
            .ThenBy(change => change.Kind).ToArray());

        void Visit(DefinitionNode before, DefinitionNode after, string path)
        {
            if (before.Type != after.Type || before.IsNullable != after.IsNullable) Add(DefinitionSchemaChange.Type);
            if (!before.Required.SequenceEqual(after.Required, StringComparer.Ordinal)) Add(DefinitionSchemaChange.Required);
            if (DefinitionSensitivityPolicy.SensitivityOf(before) != DefinitionSensitivityPolicy.SensitivityOf(after))
                Add(DefinitionSchemaChange.Sensitivity);
            Check(["minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "multipleOf", "minLength", "maxLength",
                "minItems", "maxItems", "minProperties", "maxProperties"], DefinitionSchemaChange.Range);
            Check(["enum", "const"], DefinitionSchemaChange.Choice);
            Check(["if", "then", "else"], DefinitionSchemaChange.Activation);
            Check(["format", "uniqueItems"], DefinitionSchemaChange.OtherConstraint);
            Check(["dependentRequired", "readOnly", "writeOnly"], DefinitionSchemaChange.OtherConstraint);
            foreach (string keyword in new[] { "notBlank", "readOnly", "acquisition", "initialValue" })
                if (!Same(Extension(before), Extension(after), keyword)) Add(DefinitionSchemaChange.OtherConstraint);
            foreach (string keyword in new[] { "applicableWhen", "requiredWhen" })
                if (!Same(Extension(before), Extension(after), keyword)) Add(DefinitionSchemaChange.Activation);
            if (!Same(Extension(before), Extension(after), "secret")) Add(DefinitionSchemaChange.SecretPolicy);
            if (!Same(Extension(before), Extension(after), "uniqueBy")) Add(DefinitionSchemaChange.Collection);
            foreach (string name in before.Properties.Keys.Union(after.Properties.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                string childPath = path + "/properties/" + Escape(name);
                if (!before.Properties.TryGetValue(name, out DefinitionNode? left))
                    changes.Add(new(childPath, after.Required.Contains(name, StringComparer.Ordinal)
                        ? DefinitionSchemaChange.AddedRequired : DefinitionSchemaChange.AddedOptional));
                else if (!after.Properties.TryGetValue(name, out DefinitionNode? right))
                    changes.Add(new(childPath, DefinitionSchemaChange.Removed));
                else Visit(left, right, childPath);
            }
            Child(before.Items, after.Items, "/items");
            Child(before.AdditionalProperties, after.AdditionalProperties, "/additionalProperties");
            if (before.Variants.Count != after.Variants.Count) Add(DefinitionSchemaChange.Variant);
            for (int index = 0; index < Math.Min(before.Variants.Count, after.Variants.Count); index++)
                Visit(before.Variants[index], after.Variants[index], path + "/oneOf/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));

            void Child(DefinitionNode? left, DefinitionNode? right, string suffix)
            {
                if (left is null || right is null)
                {
                    if (left != right) changes.Add(new(path + suffix, DefinitionSchemaChange.Collection));
                }
                else Visit(left, right, path + suffix);
            }
            void Add(DefinitionSchemaChange kind)
            {
                var delta = new DefinitionSchemaDelta(path, kind);
                if (!changes.Contains(delta)) changes.Add(delta);
            }
            void Check(string[] keywords, DefinitionSchemaChange kind)
            {
                if (keywords.Any(keyword => !Same(before.Keywords, after.Keywords, keyword))) Add(kind);
            }
        }
    }

    internal static string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal)
        .Replace("/", "~1", StringComparison.Ordinal);

    internal static JsonElement Extension(DefinitionNode node) =>
        node.Keywords.TryGetProperty("x-configurator", out JsonElement extension) ? extension : default;

    internal static bool Same(JsonElement left, JsonElement right, string keyword)
    {
        bool hasLeft = left.ValueKind == JsonValueKind.Object && left.TryGetProperty(keyword, out _);
        bool hasRight = right.ValueKind == JsonValueKind.Object && right.TryGetProperty(keyword, out _);
        return hasLeft == hasRight && (!hasLeft || JsonElement.DeepEquals(left.GetProperty(keyword), right.GetProperty(keyword)));
    }
}
