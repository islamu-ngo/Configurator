namespace Configurator.Core.Definitions.Authoring;

using System.Globalization;
using System.Text;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

/// <summary>Patches only machine identity scalar tokens in an admitted source, preserving all other bytes.</summary>
public static class DefinitionAuthoringIdentityEditor
{
    public static byte[] UpdateRevision(ReadOnlyMemory<byte> bytes, bool yaml, long revision)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(revision, 1);
        return Edit(bytes, yaml, "revision", revision.ToString(CultureInfo.InvariantCulture));
    }

    public static byte[] Fork(ReadOnlyMemory<byte> bytes, bool yaml, Guid familyId)
    {
        if (familyId == Guid.Empty) throw new ArgumentException("A nonempty identity is required.", nameof(familyId));
        byte[] changed = Edit(bytes, yaml, "familyId", familyId.ToString("D"));
        return UpdateRevision(changed, yaml, 1);
    }

    private static byte[] Edit(ReadOnlyMemory<byte> bytes, bool yaml, string member, string replacement)
    {
        byte[] original = bytes.ToArray();
        DefinitionAuthoringDocument document = yaml ? DefinitionAuthoringCodec.ParseYaml(original) : DefinitionAuthoringCodec.ParseJson(original);
        _ = DefinitionAuthoringCompiler.Compile(document);
        byte[] changed;
        if (yaml)
        {
            string source = new UTF8Encoding(false, true).GetString(original);
            (int start, int end, ScalarStyle style) = YamlSpan(source, member);
            string token = member == "familyId"
                ? style == ScalarStyle.SingleQuoted ? "'" + replacement + "'" : JsonSerializer.Serialize(replacement)
                : replacement;
            changed = Encoding.UTF8.GetBytes(string.Concat(source.AsSpan(0, start), token, source.AsSpan(end)));
        }
        else
        {
            int bom = original.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
            (int start, int end) = JsonSpan(original.AsSpan(bom), member);
            byte[] token = Encoding.UTF8.GetBytes(member == "familyId" ? JsonSerializer.Serialize(replacement) : replacement);
            changed = new byte[original.Length - (end - start) + token.Length];
            original.AsSpan(0, start + bom).CopyTo(changed);
            token.CopyTo(changed.AsSpan(start + bom));
            original.AsSpan(end + bom).CopyTo(changed.AsSpan(start + bom + token.Length));
        }
        _ = DefinitionAuthoringCompiler.Compile(yaml ? DefinitionAuthoringCodec.ParseYaml(changed) : DefinitionAuthoringCodec.ParseJson(changed));
        return changed;
    }

    private static (int Start, int End) JsonSpan(ReadOnlySpan<byte> bytes, string member)
    {
        var reader = new Utf8JsonReader(bytes);
        bool identity = false;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1)
                identity = reader.ValueTextEquals("identity");
            if (identity && reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 2 && reader.ValueTextEquals(member))
            {
                if (!reader.Read()) break;
                return (checked((int)reader.TokenStartIndex), checked((int)reader.BytesConsumed));
            }
        }
        throw new SetupDefinitionException("authoring-identity-span");
    }

    private static (int Start, int End, ScalarStyle Style) YamlSpan(string source, string member)
    {
        int bom = source.Length > 0 && source[0] == '\uFEFF' ? 1 : 0;
        using var textReader = new StringReader(source[bom..]);
        var parser = new Parser(textReader);
        int depth = 0;
        bool identity = false;
        bool rootKey = true;
        bool identityKey = true;
        bool selected = false;
        while (parser.MoveNext())
        {
            switch (parser.Current)
            {
                case MappingStart:
                case SequenceStart:
                    depth++;
                    break;
                case MappingEnd:
                case SequenceEnd:
                    depth--;
                    if (depth == 1) rootKey = true;
                    break;
                case Scalar scalar when depth == 1:
                    if (rootKey) identity = scalar.Value == "identity";
                    rootKey = !rootKey;
                    break;
                case Scalar scalar when depth == 2 && identity:
                    if (identityKey) selected = scalar.Value == member;
                    else if (selected) return (checked((int)scalar.Start.Index + bom), checked((int)scalar.End.Index + bom), scalar.Style);
                    identityKey = !identityKey;
                    break;
            }
        }
        throw new SetupDefinitionException("authoring-identity-span");
    }
}
