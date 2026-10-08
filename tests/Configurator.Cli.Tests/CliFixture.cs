using System.Text;
using Configurator.Core;

namespace Configurator.Cli.Tests;

internal sealed class CliFixture : ISetupCliInput, ISetupCliWriter
{
    internal const string Definition = """
        {"schemaVersion":1,"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726",
        "revision":1,"applicationId":"example.service","applicationVersions":["1.0"],
        "publisher":"example","title":"Example service","entryPoints":["setup"],"locales":["en"],
        "fields":[
          {"id":"service.name","type":"string","required":true,"sensitivity":"public","disclosure":"guided","title":"Name"},
          {"id":"enabled","type":"boolean","required":false,"sensitivity":"public","disclosure":"guided","title":"Enabled","default":true},
          {"id":"expectedRevision","type":"string","required":false,"sensitivity":"public","disclosure":"advanced","title":"Revision"}
        ],
        "artifacts":[
          {"id":"config","format":"json","fileName":"service.json","applySemantics":"create","restart":"process",
           "projection":{"op":"object","members":{"name":{"op":"copy","field":"service.name"},"enabled":{"op":"copy","field":"enabled"}}}},
          {"id":"yaml","format":"yaml","fileName":"service.yaml","applySemantics":"create","restart":"process",
           "projection":{"op":"object","members":{"name":{"op":"copy","field":"service.name"},"enabled":{"op":"copy","field":"enabled"}}}},
          {"id":"environment","format":"dotenv","fileName":"service.env","applySemantics":"create","restart":"process",
           "projection":{"op":"object","members":{"SERVICE_NAME":{"op":"copy","field":"service.name"}}}},
          {"id":"request","format":"json","fileName":"request.json","applySemantics":"import","restart":"none",
           "projection":{"op":"object","members":{"expectedRevision":{"op":"copy","field":"expectedRevision"},"name":{"op":"copy","field":"service.name"}}}},
          {"id":"markdown","format":"markdown","fileName":"draft.md","applySemantics":"generationOnly","restart":"none",
           "projection":{"op":"list","items":[{"op":"literal","value":"Draft"},{"op":"copy","field":"service.name"}]}}
        ]}
        """;
    internal const string Answers = """{"service.name":"private-answer-marker"}""";

    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal)
    {
        ["definition.json"] = Encoding.UTF8.GetBytes(Definition),
        ["answers.json"] = Encoding.UTF8.GetBytes(Answers),
        ["baseline.json"] = Encoding.UTF8.GetBytes(Answers)
    };
    internal byte[] StandardOutput { get; private set; } = [];
    internal byte[] StandardError { get; private set; } = [];
    internal int MaximumRead { get; private set; }

    internal SetupCliExitCode Run(params string[] arguments)
    {
        StandardOutput = [];
        StandardError = [];
        return new SetupCliApplication().Run(new(arguments, SetupCliMode.Text,
            new(this, this, new ErrorWriter(this), 65_536, 4 * 1024 * 1024), new([])));
    }

    internal SetupCliExitCode Artifact(string operation, string artifact = "config", params string[] options) =>
        Run(["artifact", operation, "--definition", "definition.json", "--artifact", artifact,
            "--input", "answers.json", "--machine", .. options]);

    public ReadOnlyMemory<byte> Read(string path, int maximumBytes)
    {
        MaximumRead = maximumBytes;
        return Files.TryGetValue(path, out byte[]? bytes) ? bytes : throw new IOException();
    }

    public void Write(string path, ReadOnlyMemory<byte> bytes, int maximumBytes)
    {
        if (path != "-" || bytes.Length > maximumBytes) throw new IOException();
        StandardOutput = bytes.ToArray();
    }

    public void WriteArtifact(string path, SetupArtifactKind kind, ReadOnlyMemory<byte> bytes, int maximumBytes)
    {
        if (bytes.Length > maximumBytes) throw new IOException();
        if (path == "-")
        {
            if (!SetupArtifactPolicy.TryProjectPublic(kind, bytes, out _)) throw new IOException();
            Write(path, bytes, maximumBytes);
        }
        else if (!Files.TryAdd(path, bytes.ToArray())) throw new IOException();
    }

    private sealed class ErrorWriter(CliFixture owner) : ISetupCliWriter
    {
        public void Write(string path, ReadOnlyMemory<byte> bytes, int maximumBytes) =>
            owner.StandardError = bytes.ToArray();
        public void WriteArtifact(string path, SetupArtifactKind kind, ReadOnlyMemory<byte> bytes, int maximumBytes) =>
            throw new IOException();
    }
}
