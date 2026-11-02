namespace Configurator.Core;

public enum ArtifactKind
{
    Unknown,
    PublicCatalogue,
    PublicTemplate,
    Environment,
    Configuration,
    OperatorIdentity
}

public enum ArtifactSensitivity
{
    Restricted,
    Public
}

public static class ArtifactPolicy
{
    public static ArtifactSensitivity Classify(ArtifactKind kind) =>
        kind is ArtifactKind.PublicCatalogue or ArtifactKind.PublicTemplate
            ? ArtifactSensitivity.Public
            : ArtifactSensitivity.Restricted;

    public static bool TryProjectPublic(
        ArtifactKind kind,
        ReadOnlyMemory<byte> bytes,
        out ReadOnlyMemory<byte> publicBytes)
    {
        publicBytes = Classify(kind) == ArtifactSensitivity.Public
            ? bytes : ReadOnlyMemory<byte>.Empty;
        return Classify(kind) == ArtifactSensitivity.Public;
    }
}
