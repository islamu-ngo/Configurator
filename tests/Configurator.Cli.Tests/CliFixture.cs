using System.Security.Cryptography;
using System.Text;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;

namespace Configurator.Cli.Tests;

internal sealed class CliFixture : ICliInput, ICliWriter
{
    internal const string Definition = """
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
        "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1},
        "project":{"id":"example.service","name":"Example service","publisher":"example"},
        "applicationVersions":["1.0"],"entryPoints":["setup"],
        "configuration":{"type":"object","additionalProperties":false,"required":["service.name"],
          "x-configurator":{"sensitivity":"public"},"properties":{
            "service.name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public","detail":"standard"}},
            "enabled":{"type":"boolean","title":"Enabled","x-configurator":{"sensitivity":"public","detail":"standard","initialValue":true}},
            "expectedRevision":{"type":"string","title":"Revision","x-configurator":{"sensitivity":"public","detail":"advanced"}}
          }},
        "outputs":{
          "service.json":{"id":"config","format":"json","targetUpdateMode":"create","restartRequirement":"process",
           "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/service.name"}},"enabled":{"op":"copy","reference":{"scope":"root","path":"/enabled"}}}}},
          "service.yaml":{"id":"yaml","format":"yaml","targetUpdateMode":"create","restartRequirement":"process",
           "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/service.name"}},"enabled":{"op":"copy","reference":{"scope":"root","path":"/enabled"}}}}},
          "service.env":{"id":"environment","format":"dotenv","targetUpdateMode":"create","restartRequirement":"process",
           "projection":{"op":"object","members":{"SERVICE_NAME":{"op":"copy","reference":{"scope":"root","path":"/service.name"}}}}},
          "request.json":{"id":"request","format":"json","targetUpdateMode":"import","restartRequirement":"none",
           "projection":{"op":"object","members":{"expectedRevision":{"op":"copy","reference":{"scope":"root","path":"/expectedRevision"}},"name":{"op":"copy","reference":{"scope":"root","path":"/service.name"}}}}},
          "draft.md":{"id":"markdown","format":"markdown","targetUpdateMode":"generationOnly","restartRequirement":"none",
           "projection":{"op":"list","items":[{"op":"literal","value":"Draft"},{"op":"copy","reference":{"scope":"root","path":"/service.name"}}]}}
        }}
        """;
    internal const string Answers = """{"answerContractVersion":"0.0.1","values":{"service.name":"private-answer-marker"},"omitted":[]}""";

    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal)
    {
        ["definition.json"] = Encoding.UTF8.GetBytes(Definition),
        ["answers.json"] = Encoding.UTF8.GetBytes(Answers),
        ["baseline.json"] = Encoding.UTF8.GetBytes(Answers)
    };
    internal byte[] StandardOutput { get; private set; } = [];
    internal byte[] StandardError { get; private set; } = [];
    internal int MaximumRead { get; private set; }
    internal bool BundleBlocked { get; set; }
    internal int? BundleFailureAfter { get; set; }
    internal static ICliAuthoringFiles RejectingAuthoringFiles { get; } = new RejectingFiles();
    internal ICliAuthoringFiles AuthoringFiles { get; set; } = RejectingAuthoringFiles;
    internal int MaximumBytes { get; set; } = 4 * 1024 * 1024;
    internal string? ArtifactFailurePath { get; set; }
    internal Action<string>? ArtifactPublished { get; set; }
    internal bool ArtifactDestinationsBlocked { get; set; }

    public ValueTask<ProtectedArtifactStatus?> CheckArtifactDestinationAsync(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ProtectedArtifactStatus?>(ArtifactDestinationsBlocked ? ProtectedArtifactStatus.UnsupportedPlatform
            : Files.ContainsKey(path) ? ProtectedArtifactStatus.TargetExists : null);

    internal async Task<CliExitCode> RunAsync(params string[] arguments)
    {
        StandardOutput = [];
        StandardError = [];
        return await new CliApplication().RunAsync(new(arguments, CliMode.Text,
            new(this, this, new ErrorWriter(this), AuthoringFiles, 65_536, MaximumBytes)));
    }

    internal Task<CliExitCode> ArtifactAsync(string operation, string artifact = "config", params string[] options) =>
        RunAsync(["artifact", operation, "--definition", "definition.json", "--artifact", artifact,
            "--input", "answers.json", "--machine", .. options]);

    public async ValueTask<CliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken)
    {
        MaximumRead = maximumBytes;
        if (!Files.TryGetValue(path, out byte[]? bytes)) throw new IOException();
        byte[] ownedBytes = await ValueTask.FromResult(bytes.ToArray());
        return new(ownedBytes);
    }

    public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken)
    {
        if (path != "-" || bytes.Length > maximumBytes) throw new IOException();
        StandardOutput = bytes.ToArray();
        return ValueTask.CompletedTask;
    }

    public async ValueTask<CliPublicationResult> WriteArtifactAsync(string path, ArtifactKind kind,
        ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken)
    {
        if (bytes.Length > maximumBytes) throw new IOException();
        if (path == ArtifactFailurePath)
            return new([new(ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.IoFailure))]);
        if (path == "-")
        {
            if (!ArtifactPolicy.TryProjectPublic(kind, bytes, out _)) throw new IOException();
            await WriteAsync(path, bytes, maximumBytes, cancellationToken);
            return new([new(null, DeliveredToStandardOutput: true)]);
        }
        if (!Files.TryAdd(path, bytes.ToArray())) return new([new(ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.TargetExists))]);
        ArtifactPublished?.Invoke(path);
        return new([new(ProtectedArtifactCommitResult.Installed(true))]);
    }

    public ValueTask<CliPublicationResult> WriteBundleAsync(
        IReadOnlyList<DefinitionArtifact> artifacts, string directory, int maximumBytes,
        Action<string, CliPublicationOutcome> outcomeObserver, CancellationToken cancellationToken)
    {
        var results = new ProtectedArtifactCommitResult?[artifacts.Count];
        if (BundleBlocked)
        {
            for (int index = 0; index < results.Length; index++) Record(index, ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.UnsupportedPlatform));
            return Result();
        }
        if (artifacts.Any(item => item.ByteLength > maximumBytes))
        {
            for (int index = 0; index < results.Length; index++) Record(index, ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest));
            return Result();
        }
        for (int index = 0; index < artifacts.Count; index++)
        {
            if (!Files.ContainsKey(Path.Combine(directory, artifacts[index].FileName))) continue;
            Record(index, ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.TargetExists));
            return Result();
        }
        for (int index = 0; index < artifacts.Count; index++)
        {
            if (BundleFailureAfter == index)
            {
                Record(index, ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.IoFailure));
                break;
            }
            string path = Path.Combine(directory, artifacts[index].FileName);
            ReadOnlyMemory<byte> payload = artifacts[index].Bytes;
            if (!Files.TryAdd(path, payload.ToArray()))
            {
                Record(index, ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.TargetExists));
                if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(payload, out ArraySegment<byte> rejected))
                    CryptographicOperations.ZeroMemory(rejected.AsSpan());
                break;
            }
            if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(payload, out ArraySegment<byte> written))
                CryptographicOperations.ZeroMemory(written.AsSpan());
            Record(index, ProtectedArtifactCommitResult.Installed(true));
        }
        return Result();
        void Record(int index, ProtectedArtifactCommitResult result)
        {
            results[index] = result;
            outcomeObserver(artifacts[index].ArtifactId, new(result));
        }
        ValueTask<CliPublicationResult> Result() => ValueTask.FromResult(
            new CliPublicationResult(results.Select(result => new CliPublicationOutcome(result)).ToArray()));
    }

    private sealed class ErrorWriter(CliFixture owner) : ICliWriter
    {
        public ValueTask<ProtectedArtifactStatus?> CheckArtifactDestinationAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ProtectedArtifactStatus?>(ProtectedArtifactStatus.UnsupportedPlatform);
        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken)
        {
            owner.StandardError = bytes.ToArray();
            return ValueTask.CompletedTask;
        }
        public ValueTask<CliPublicationResult> WriteArtifactAsync(string path, ArtifactKind kind,
            ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken) =>
            throw new IOException();
        public ValueTask<CliPublicationResult> WriteBundleAsync(
            IReadOnlyList<DefinitionArtifact> artifacts, string directory, int maximumBytes,
            Action<string, CliPublicationOutcome> outcomeObserver, CancellationToken cancellationToken) =>
            throw new IOException();
    }

    private sealed class RejectingFiles : ICliAuthoringFiles
    {
        public ValueTask<string> DiscoverSourceAsync(string root, string? source, CancellationToken cancellationToken) =>
            throw new UnauthorizedAccessException();
        public ValueTask<CliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken) =>
            throw new UnauthorizedAccessException();
        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken) =>
            throw new UnauthorizedAccessException();
        public ICliPublicFilePreparation PrepareCreate(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, bool acceptIdentical) =>
            throw new UnauthorizedAccessException();
    }
}
