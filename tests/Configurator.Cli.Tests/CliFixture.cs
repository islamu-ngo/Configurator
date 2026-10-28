using System.Security.Cryptography;
using System.Text;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;

namespace Configurator.Cli.Tests;

internal sealed class CliFixture : ISetupCliInput, ISetupCliWriter
{
    internal const string Definition = """
        {"$schema":"urn:islamu:configurator:authoring:v2",
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
    internal const string Answers = """{"answerContractVersion":1,"values":{"service.name":"private-answer-marker"},"omitted":[]}""";

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
    internal static ISetupCliAuthoringFiles RejectingAuthoringFiles { get; } = new RejectingFiles();
    internal ISetupCliAuthoringFiles AuthoringFiles { get; set; } = RejectingAuthoringFiles;
    internal int MaximumBytes { get; set; } = 4 * 1024 * 1024;

    internal async Task<SetupCliExitCode> RunAsync(params string[] arguments)
    {
        StandardOutput = [];
        StandardError = [];
        return await new SetupCliApplication().RunAsync(new(arguments, SetupCliMode.Text,
            new(this, this, new ErrorWriter(this), AuthoringFiles, 65_536, MaximumBytes)));
    }

    internal Task<SetupCliExitCode> ArtifactAsync(string operation, string artifact = "config", params string[] options) =>
        RunAsync(["artifact", operation, "--definition", "definition.json", "--artifact", artifact,
            "--input", "answers.json", "--machine", .. options]);

    public async ValueTask<SetupCliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken)
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

    public async ValueTask<SetupCliPublicationResult> WriteArtifactAsync(string path, SetupArtifactKind kind,
        ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken)
    {
        if (bytes.Length > maximumBytes) throw new IOException();
        if (path == "-")
        {
            if (!SetupArtifactPolicy.TryProjectPublic(kind, bytes, out _)) throw new IOException();
            await WriteAsync(path, bytes, maximumBytes, cancellationToken);
            return new([new(null, DeliveredToStandardOutput: true)]);
        }
        if (!Files.TryAdd(path, bytes.ToArray())) return new([new(ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.TargetExists))]);
        return new([new(ProtectedArtifactCommitResult.Installed(true))]);
    }

    public ValueTask<SetupCliPublicationResult> WriteBundleAsync(
        IReadOnlyList<DefinitionArtifact> artifacts, string directory, int maximumBytes,
        Action<string, SetupCliPublicationOutcome> outcomeObserver, CancellationToken cancellationToken)
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
        ValueTask<SetupCliPublicationResult> Result() => ValueTask.FromResult(
            new SetupCliPublicationResult(results.Select(result => new SetupCliPublicationOutcome(result)).ToArray()));
    }

    private sealed class ErrorWriter(CliFixture owner) : ISetupCliWriter
    {
        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken)
        {
            owner.StandardError = bytes.ToArray();
            return ValueTask.CompletedTask;
        }
        public ValueTask<SetupCliPublicationResult> WriteArtifactAsync(string path, SetupArtifactKind kind,
            ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken) =>
            throw new IOException();
        public ValueTask<SetupCliPublicationResult> WriteBundleAsync(
            IReadOnlyList<DefinitionArtifact> artifacts, string directory, int maximumBytes,
            Action<string, SetupCliPublicationOutcome> outcomeObserver, CancellationToken cancellationToken) =>
            throw new IOException();
    }

    private sealed class RejectingFiles : ISetupCliAuthoringFiles
    {
        public ValueTask<string> DiscoverSourceAsync(string root, string? source, CancellationToken cancellationToken) =>
            throw new UnauthorizedAccessException();
        public ValueTask<SetupCliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken) =>
            throw new UnauthorizedAccessException();
        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken) =>
            throw new UnauthorizedAccessException();
        public ISetupCliPublicFilePreparation PrepareCreate(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, bool acceptIdentical) =>
            throw new UnauthorizedAccessException();
    }
}
