namespace Configurator.Core.Artifacts;

using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Core.Dotenv;
using Configurator.Core.Environment;

internal static class DefinitionArtifactSerializer
{
    internal static byte[] WriteDotenv(JsonElement value, EnvironmentDestinationDialect dialect, bool draft = false)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new DefinitionException("dotenv-shape");
        var entries = new List<DotenvEntry>();
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null)
                throw new DefinitionException("dotenv-shape");
            string text = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()! : property.Value.GetRawText();
            bool placeholder = draft && text.Length == 0;
            entries.Add(new(property.Name, placeholder ? null : text,
                placeholder ? DotenvEntryKind.EmptyPlaceholder : DotenvEntryKind.LocalHumanValue, true, DotenvProvenance.UserInput));
        }
        DotenvRenderResult rendered = DotenvCodec.Render(new DotenvDocument(entries), true, dialect,
            draft ? DotenvPurpose.TemplatePlaceholders : DotenvPurpose.EditableValues);
        if (!rendered.Succeeded) throw new DefinitionException("dotenv-value");
        return rendered.Bytes.ToArray();
    }

    internal static byte[] WriteYaml(JsonElement value)
    {
        var result = new StringBuilder();
        Write(value, 0);
        return Encoding.UTF8.GetBytes(result.ToString());
        void Write(JsonElement node, int indent)
        {
            if (node.ValueKind == JsonValueKind.Object && node.EnumerateObject().Any())
            {
                foreach (JsonProperty property in node.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    result.Append(' ', indent).Append(JsonSerializer.Serialize(property.Name)).Append(':');
                    Child(property.Value, indent);
                }
            }
            else if (node.ValueKind == JsonValueKind.Array && node.GetArrayLength() > 0)
            {
                foreach (JsonElement child in node.EnumerateArray())
                {
                    result.Append(' ', indent).Append('-');
                    Child(child, indent);
                }
            }
            else result.Append(' ', indent).Append(node.GetRawText()).Append('\n');
        }
        void Child(JsonElement child, int indent)
        {
            if (child.ValueKind == JsonValueKind.Object && child.EnumerateObject().Any()
                || child.ValueKind == JsonValueKind.Array && child.GetArrayLength() > 0)
            {
                result.Append('\n');
                Write(child, indent + 2);
            }
            else result.Append(' ').Append(child.GetRawText()).Append('\n');
        }
    }

    internal static byte[] WriteMarkdown(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array
            || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
            throw new DefinitionException("markdown-shape");
        var output = new StringBuilder();
        foreach (JsonElement item in value.EnumerateArray())
        {
            foreach (char character in item.GetString()!)
            {
                if ("\\`*_{}[]()#+-.!|><&".Contains(character, StringComparison.Ordinal)) output.Append('\\');
                if (character != '\r') output.Append(character);
            }
        }
        return Encoding.UTF8.GetBytes(output.ToString());
    }
}
