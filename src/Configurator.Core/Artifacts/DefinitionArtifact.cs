namespace Configurator.Core.Artifacts;

using System.Security.Cryptography;
using Configurator.Core.Definitions;

public sealed class DefinitionArtifact : IDisposable
{
    private readonly byte[] _bytes;
    private readonly object _sync = new();
    private bool _disposed;
    internal DefinitionArtifact(Definition definition, ArtifactDefinition artifact,
        long inputRevision, byte[] bytes)
    {
        _bytes = (byte[])bytes.Clone();
        Digest = ArtifactDigest.Compute(bytes);
        DefinitionDigest = definition.Digest;
        DefinitionRevision = definition.Revision;
        InputRevision = inputRevision;
        ArtifactId = artifact.Id;
        FileName = artifact.FileName;
        ApplySemantics = artifact.ApplySemantics;
        Restart = artifact.Restart;
        MediaType = artifact.Format switch
        {
            DefinitionArtifactFormat.Json => "application/json",
            DefinitionArtifactFormat.Yaml => "application/yaml",
            DefinitionArtifactFormat.Markdown => "text/markdown",
            DefinitionArtifactFormat.Dotenv => "text/plain",
            _ => throw new DefinitionException("format-invalid")
        };
    }
    /// <summary>Returns a caller-owned copy. Disposal clears the artifact's retained buffer, not copies already returned.</summary>
    public ReadOnlyMemory<byte> Bytes => new(CopyBytes());

    internal byte[] CopyBytes()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return (byte[])_bytes.Clone();
        }
    }
    public int ByteLength => _bytes.Length;
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            CryptographicOperations.ZeroMemory(_bytes);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
    public ArtifactDigest Digest { get; }
    public ArtifactDigest DefinitionDigest { get; }
    public long DefinitionRevision { get; }
    public long InputRevision { get; }
    public string ArtifactId { get; }
    public string FileName { get; }
    public string MediaType { get; }
    public DefinitionTargetUpdateMode ApplySemantics { get; }
    public DefinitionRestartRequirement Restart { get; }
    public ArtifactSensitivity Sensitivity => ArtifactSensitivity.Restricted;
    public override string ToString() => $"{nameof(DefinitionArtifact)}:Restricted:Revision={InputRevision}";
}

