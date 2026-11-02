namespace Configurator.Core.Definitions;

using System.Collections.ObjectModel;
using System.Text.Json;

public sealed class Definition
{
    private readonly byte[] _canonicalBytes;

    internal Definition(JsonElement document, byte[] canonicalBytes, DefinitionNode configuration,
        DefinitionSourceMap? sourceMap = null)
    {
        Document = document.Clone();
        _canonicalBytes = (byte[])canonicalBytes.Clone();
        Digest = ArtifactDigest.Compute(canonicalBytes);
        FamilyId = document.GetProperty("familyId").GetGuid();
        Revision = document.GetProperty("revision").GetInt64();
        Configuration = configuration;
        SourceMap = sourceMap ?? DefinitionSourceMap.Empty;
        FieldSensitivity inherited = FieldDefinition.SensitivityOf(configuration);
        Fields = Array.AsReadOnly(configuration.Properties.Select(item => new FieldDefinition(
            item.Key, item.Value, configuration.Required.Contains(item.Key, StringComparer.Ordinal), inherited)).ToArray());
        Artifacts = Array.AsReadOnly(document.GetProperty("artifacts").EnumerateArray()
            .Select(item => new ArtifactDefinition(item)).ToArray());
        ApplicationVersions = Strings("applicationVersions");
        EntryPoints = Strings("entryPoints");
        Locales = Strings("locales");
        RequiredCapabilities = Strings("requiredCapabilities");
        Presentation = document.TryGetProperty("presentation", out var presentation)
            ? new DefinitionPresentation(presentation) : null;
    }

    internal JsonElement Document { get; }
    public Guid FamilyId { get; }
    public long Revision { get; }
    public ArtifactDigest Digest { get; }
    public ReadOnlyMemory<byte> CanonicalBytes => new((byte[])_canonicalBytes.Clone());
    public string ApplicationId => Document.GetProperty("project").GetProperty("id").GetString()!;
    public string Publisher => Document.GetProperty("project").GetProperty("publisher").GetString()!;
    public string Title => Document.GetProperty("project").GetProperty("name").GetString()!;
    public DefinitionNode Configuration { get; }
    public DefinitionSourceMap SourceMap { get; }
    public IReadOnlyList<string> ApplicationVersions { get; }
    public IReadOnlyList<string> EntryPoints { get; }
    public IReadOnlyList<string> Locales { get; }
    public IReadOnlyList<string> RequiredCapabilities { get; }
    public IReadOnlyList<FieldDefinition> Fields { get; }
    public IReadOnlyList<ArtifactDefinition> Artifacts { get; }
    public DefinitionPresentation? Presentation { get; }
    public override string ToString() => $"{nameof(Definition)}:{FamilyId:D}:{Revision}:{Digest}";

    private ReadOnlyCollection<string> Strings(string name) => Array.AsReadOnly(
        Document.GetProperty(name).EnumerateArray().Select(item => item.GetString()!).ToArray());
}
