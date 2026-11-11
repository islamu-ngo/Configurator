using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Configurator.BuildCapture;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private static int Main(string[] args)
    {
        try
        {
            if (args is ["--host", var hostSession, var hostProject, var hostTarget, ..])
            {
                var capturedHost = CaptureSession.Open(hostSession);
                string sdkDirectory = Path.Combine(capturedHost.Frozen, "sdk/sdk", CaptureSession.SdkVersion);
                Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", Path.Combine(sdkDirectory, "MSBuild.dll"));
                Environment.SetEnvironmentVariable("MSBuildSDKsPath", Path.Combine(sdkDirectory, "Sdks"));
                Environment.SetEnvironmentVariable("MSBuildExtensionsPath", sdkDirectory);
                Environment.SetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", Path.Combine(capturedHost.Frozen, "sdk"));
                System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, name) =>
                {
                    string path = Path.Combine(sdkDirectory, name.Name + ".dll");
                    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
                };
                return BuildCaptureHost.Run(hostSession, hostProject, hostTarget, args[4..]);
            }
            if (args is ["--schema-write" or "--schema-check", var root])
            {
                foreach ((string name, Type type) in new[]
                {
                    ("configurator-build-inputs", typeof(BuildInputs)),
                    ("configurator-check-execution", typeof(CheckExecution))
                })
                {
                    JsonNode schema = Json.GetJsonSchemaAsNode(type);
                    Constrain(schema);
                    schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
                    schema["title"] = name;
                    string text = schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
                    string path = Path.Combine(root, "schemas", name + ".schema.json");
                    if (args[0] == "--schema-write") File.WriteAllText(path, text);
                    else CaptureSession.Require(File.ReadAllText(path) == text, "CAPTURE_SCHEMA_STALE");
                }
                Console.WriteLine("CAPTURE_SCHEMAS_VALID");
                return 0;
            }
            if (args is ["--verify", var session, .. var verificationOptions]
                && (verificationOptions.Length == 0 || verificationOptions is ["--repository-names", _]))
            {
                CaptureSession.Open(session).Verify(verificationOptions.Length == 0 ? null : verificationOptions[1]);
                Console.WriteLine("CAPTURE_VALID");
                return 0;
            }
            if (args is ["--capture", var rootPath, var project, var target, var destination, ..])
            {
                return CaptureSession.Capture(rootPath, project, target, destination, args[5..]);
            }
            if (args is ["--run-check", var captured, var owner, var output, ..])
                return CaptureSession.Open(captured).RunCheck(owner, output, args[4..]);
            if (args is ["--verify-check", var capturedSession, var execution, var report])
            {
                CaptureSession.Open(capturedSession).VerifyCheck(execution, report);
                Console.WriteLine("CAPTURE_CHECK_VALID");
                return 0;
            }
            Console.Error.WriteLine("CAPTURE_ARGUMENTS_INVALID");
            return 64;
        }
        catch (CaptureException error)
        {
            Console.Error.WriteLine(error.Code);
            return 1;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception
            or System.Reflection.ReflectionTypeLoadException or TypeLoadException or BadImageFormatException)
        {
            Console.Error.WriteLine("CAPTURE_INPUT_INVALID");
            return 1;
        }
    }

    private static void Constrain(JsonNode node)
    {
        if (node is JsonObject value)
        {
            if (value["type"] is JsonArray types && types.Any(type => type?.GetValue<string>() == "object"))
                value["type"] = "object";
            if (value["properties"] is JsonObject properties)
                foreach ((string name, JsonNode? property) in properties)
                {
                    if (property is not JsonObject field) continue;
                    if (name == "schemaVersion") field["const"] = "0.0.1";
                    if (name == "sdkVersion") field["const"] = CaptureSession.SdkVersion;
                    if (name == "workloadVersion") field["const"] = CaptureSession.WorkloadVersion;
                    if (name is "sha256" or "safeReportHash" or "testCaptureDigest" or "productSourceInputsDigest" or "buildRecipeDigest" or "receipt")
                        field["pattern"] = "^[0-9a-f]{64}$";
                    if (name is "length" or "passed" or "failed" or "skipped" or "commandExitCode") field["minimum"] = 0;
                    if (name == "scope") field["enum"] = new JsonArray("full-owner", "focused");
                    if (name == "outcome") field["enum"] = properties.ContainsKey("target") && properties.ContainsKey("reason")
                        ? new JsonArray("started", "succeeded", "failed", "skipped")
                        : new JsonArray("passed", "failed");
                    if (name == "repositoryNamespace")
                    {
                        field["type"] = "array";
                        field["maxItems"] = CaptureSession.MaximumRepositoryNames;
                        field["uniqueItems"] = true;
                        field["items"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["pattern"] = "^repo/",
                            ["minLength"] = 6,
                            ["maxLength"] = CaptureSession.MaximumRepositoryNameLength
                        };
                    }
                    if (name is "movedInputs" or "retiredInputs")
                    {
                        field["type"] = "array";
                        field["maxItems"] = CaptureSession.MaximumRepositoryNames;
                    }
                    if (name is "sourcePath" or "destinationPath" or "outputPath")
                        field["maxLength"] = CaptureSession.MaximumRepositoryNameLength;
                    if (name is "invocation" or "generatorInvocation" or "queryGraph")
                        field["maxLength"] = 512;
                    if (name == "queryGraph") field["pattern"] = "^[0-9a-f]{32}$";
                    if (name == "surface") field["enum"] = new JsonArray("cli", "terminal", "desktop");
                }
            foreach (var child in value.Select(pair => pair.Value).OfType<JsonNode>().ToArray()) Constrain(child);
        }
        else if (node is JsonArray array)
            foreach (var child in array.OfType<JsonNode>()) Constrain(child);
    }
}

internal sealed class CaptureException(string code) : Exception
{
    internal string Code { get; } = code;
}

public sealed record FileIdentity(string Path, long Length, string Sha256, string Kind);
public sealed record BuildSetting(string Project, string Name, JsonElement Value);
public sealed record CapturedParameter(string Name, string Type, JsonElement Value, FileIdentity[] Inputs);
public sealed record CapturedTransformation(string Parameter, string Path, FileIdentity? Before, FileIdentity After);
public sealed record CapturedTarget(string Id, string Project, string Target, string Outcome, string? Reason);
public sealed record CapturedInvocation(string Id, string Project, string Target, string Task,
    FileIdentity Assembly, FileIdentity Recipe, CapturedParameter[] Parameters, string Receipt,
    CapturedTransformation[] Transformations, bool Succeeded);
public sealed record CapturedMove(string Invocation, FileIdentity Recipe, string SourcePath, string DestinationPath,
    FileIdentity Source, FileIdentity Destination);
public sealed record CapturedRetirement(string Id, string QueryGraph, string GeneratorInvocation, string Surface,
    string OutputPath, FileIdentity Source, FileIdentity Recipe);
internal sealed record GeneratorReceipt(string Id, string Invocation, FileIdentity Recipe, FileIdentity Executable, bool? Succeeded);
internal sealed record ContractQueryReceipt(string QueryGraph, string GeneratorId, string GeneratorInvocation,
    string Surface, string OutputPath, string PhysicalOutput, FileIdentity Recipe, bool Succeeded, FileIdentity? Source,
    [property: JsonRequired] string CaptureRoot);
internal sealed record TaskParameterContract(string Name, string Type, bool Output);
internal sealed record TaskContract(string Name, string Type, string Assembly, TaskParameterContract[] Parameters);
internal sealed record JsonFactoryValue(string Name, string Type, bool Bound, JsonElement Value);
internal sealed record JsonFactoryTool(string PhysicalPath, FileIdentity Identity);
internal sealed record JsonFactoryPermit(string InputPath, FileIdentity Input, JsonFactoryTool[] Tools);
internal sealed record JsonFactoryBarrier(string Id, string Token, string Context, int Thread,
    FileIdentity Input, FileIdentity[] Tools, CapturedParameter[] Parameters);
internal sealed record JsonFactoryOutcome(string Id, string Token, bool Succeeded, CapturedParameter[] Outputs);

/// <summary>The finite, source-free reader contract; also used by SDK-free receipt verification.</summary>
internal static class ReadWasmPropsProfile
{
    internal const string Name = "ReadWasmProps";
    internal const string Factory = "JsonToItemsTaskFactory.JsonToItemsTaskFactory";
    internal const string RuntimeType = Factory + "+JsonToItemsTask";
    internal const string AdapterFactory = "Configurator.BuildCapture.ReadWasmPropsCaptureFactory";
    internal const string AdapterTask = "Configurator.BuildCapture.ReadWasmPropsCaptureTask";
    internal const string Event = "Configurator.ReadWasmProps.Capture";
    internal const string Assembly = "sdk/packs/Microsoft.NET.Runtime.MonoTargets.Sdk/10.0.9/tasks/net10.0/MonoTargetsTasks.dll";
    internal const string AssemblyHash = "cb321810c0d58ee645113d3677a10635814dd02536086ab7815bd16822e8d8ce";
    internal const string Recipe = "sdk/packs/Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.9/Sdk/WasmApp.Common.targets";
    internal const string RecipeHash = "c92ba321b041e8cf155e7a58fc7875cc6b4d8c94147600c74db63c1335d32b97";
    internal const string GroupHash = "69a09ff309c1996612b7f4c7809415507b18b019a15473f51afd6ce04eed651a";
    internal const string Body = "pinned-json-reader-10.0.403";
    internal const string ToolsetRecipe = "artifacts/capture-task-overrides/ReadWasmProps.overridetasks";
    internal static readonly string[] Outputs = ["EmccDefaultExportedFunctions", "EmccDefaultExportedRuntimeMethods",
        "EmccProperties", "PropertiesThatTriggerRelinking", "WasmOptConfigurationFlags"];
    internal static readonly string[] HostProperties = ["ConfiguratorCaptureSession", "ConfiguratorCaptureTool",
        "ConfiguratorCaptureOverrides", "CustomAfterMicrosoftCommonProps", "CustomBeforeMicrosoftCommonTargets"];
    internal static TaskContract Contract(string assembly) => new(Name, AdapterTask, assembly,
        Outputs.Select(name => new TaskParameterContract(name, "Microsoft.Build.Framework.ITaskItem[]", true))
            .Append(new("JsonFilePath", "System.String", false)).OrderBy(value => value.Name, StringComparer.Ordinal).ToArray());
    internal static XElement OverrideProject(string tool) => new("Project",
        new XElement("PropertyGroup", new XElement("ConfiguratorCaptureBindingsLoaded", "true")),
        new XElement("UsingTask", new XAttribute("TaskName", Name), new XAttribute("TaskFactory", AdapterFactory),
            new XAttribute("AssemblyFile", tool), new XAttribute("Override", "true"), new XElement("Task", Body)));
    internal static XElement ToolsetOverrideProject(string tool) => new("Project",
        new XElement(OverrideProject(tool).Elements("UsingTask").Single()));
}
public sealed record BuildInputs(string SchemaVersion, string SdkVersion, string WorkloadVersion,
    string Project, string Target, FileIdentity[] Inputs, BuildSetting[] Settings, FileIdentity[] Outputs,
    FileIdentity[] ProductSourceProjection, string ProductSourceInputsDigest, string BuildRecipeDigest,
    CapturedInvocation[] Invocations, CapturedTarget[] Targets,
    [property: JsonRequired] string[] RepositoryNamespace,
    [property: JsonRequired] CapturedMove[] MovedInputs, [property: JsonRequired] CapturedRetirement[] RetiredInputs);
public sealed record CheckExecution(string SchemaVersion, string SafeReportHash, string Profile, string Owner,
    string Scope, string TestCaptureDigest, FileIdentity Executable, FileIdentity[] Dependencies,
    int CommandExitCode, long Passed, long Failed, long Skipped, string Outcome, string RunnerRid);
internal sealed record SessionConfiguration(string Root, string Project, string Target, string Sdk,
    string[] Properties, bool HasPackages, [property: JsonRequired] string[] RepositoryNamespace);
internal sealed record Observation(string Operation, string Path, string Kind, string? Hash, long Length,
    string? Project, string? Name, JsonElement? Value);

/// <summary>
/// Owns frozen admitted roots and the detached observations of actual consumers.
/// The known tools are trusted to declare their file semantics; this is not a filesystem sandbox.
/// </summary>
public sealed class CaptureSession
{
    internal const string SdkVersion = "10.0.403";
    internal const string WorkloadVersion = "10.0.301.1";
    internal const int MaximumRepositoryNames = 100_000;
    internal const int MaximumRepositoryNameLength = 4096;
    private const int MaximumRepositoryNamesBytes = 16 * 1024 * 1024;
    private static readonly System.Text.UTF8Encoding StrictUtf8 = new(false, true);
    internal static readonly string[] DefaultTaskRecipes = ["Microsoft.Common.tasks", "Microsoft.Common.overridetasks"];
    private static readonly string[] ExcludedDirectories =
        [".git", ".worktrees", ".agents", ".omo", ".claude", "dev", "artifacts", "bin", "obj", "node_modules", ".vs", "TestResults"];
    private static readonly string[] Owners =
        ["Core", "Presentation", "Artifacts", "Cli", "Terminal", "Desktop", "Browser", "Architecture", "AgentWorkflow", "ReleaseEngineering"];
    private static readonly string[] RepositoryPrefixes =
        ["src/", "tests/", "eng/", ".ci/", "schemas/", "examples/", "store/", "skills/", "docs/internal/legal/"];
    private readonly string _session;
    private readonly SessionConfiguration _configuration;
    private readonly Dictionary<string, FileIdentity> _observed = new(StringComparer.Ordinal);
    private HashSet<string>? _boundaries;
    private string _lastBuildOutputBase = "";

    private CaptureSession(string session, SessionConfiguration configuration)
    {
        _session = Path.GetFullPath(session);
        _configuration = configuration;
    }

    internal string Work => Path.Combine(_session, "work/repo");
    internal string Frozen => Path.Combine(_session, "frozen");
    internal string Dotnet => Path.Combine(Frozen, "sdk", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
    internal string Tool => Path.Combine(_session, "private/tool/Configurator.BuildCapture.dll");
    internal string LoggerArgument => "-logger:Configurator.BuildCapture.BuildCaptureLogger," + Tool + ";" + _session;

    internal static void Require(bool condition, string code)
    {
        if (!condition) throw new CaptureException(code);
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
    internal static byte[] Canonical<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Program.Json);

    internal static CaptureSession Open(string path)
    {
        string full = Path.GetFullPath(path);
        Unlinked(full);
        Unlinked(Path.Combine(full, "private/session.json"));
        var configuration = JsonSerializer.Deserialize<SessionConfiguration>(
            File.ReadAllBytes(Path.Combine(full, "private/session.json")), Program.Json)
            ?? throw new CaptureException("CAPTURE_RECORD_INVALID");
        ValidateRepositoryNamespace(configuration.RepositoryNamespace);
        return new(full, configuration);
    }

    internal static int Capture(string root, string project, string target, string destination, string[] options)
    {
        root = Path.GetFullPath(root);
        destination = Path.GetFullPath(destination);
        Require(!Directory.Exists(destination) && !File.Exists(destination), "CAPTURE_SESSION_EXISTS");
        Require(!Inside(destination, root) && !Inside(root, destination), "CAPTURE_ROOT_OVERLAP");
        Require(Relative(project) && target.Length > 0 && target.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'),
            "CAPTURE_ENTRY_INVALID");
        var properties = new List<string>();
        var additions = new HashSet<string>(StringComparer.Ordinal);
        string? store = null;
        string? fixtureAdmission = null;
        for (int i = 0; i < options.Length; i += 2)
        {
            Require(i + 1 < options.Length, "CAPTURE_ARGUMENTS_INVALID");
            if (options[i] == "--admit-new")
            {
                Require(Relative(options[i + 1]) && additions.Add(options[i + 1]), "CAPTURE_SOURCE_ADMISSION_INVALID");
            }
            else if (options[i] == "--fixture-admission")
            {
                Require(fixtureAdmission is null, "CAPTURE_SOURCE_ADMISSION_INVALID");
                fixtureAdmission = Path.GetFullPath(options[i + 1]);
            }
            else if (options[i] == "--store")
            {
                Require(store is null, "CAPTURE_ARGUMENTS_INVALID");
                store = Path.GetFullPath(options[i + 1]);
            }
            else
            {
                Require(options[i] == "--property", "CAPTURE_ARGUMENTS_INVALID");
                string value = options[i + 1];
                int equals = value.IndexOf('=');
                Require(equals > 0 && value[..equals] is "Configuration" or "RuntimeIdentifier" or "ConfiguratorRid"
                    or "ConfiguratorSurface" or "SelfContained" or "PublishSingleFile" or "PublishTrimmed"
                    or "RunAOTCompilation" or "UseAppHost" or "ConfiguratorCaptureCalibration", "CAPTURE_PROPERTY_UNSUPPORTED");
                Require(value[(equals + 1)..].All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.'),
                    "CAPTURE_PROPERTY_INVALID");
                Require(properties.All(existing => !existing.StartsWith(value[..(equals + 1)], StringComparison.OrdinalIgnoreCase)),
                    "CAPTURE_PROPERTY_DUPLICATE");
                properties.Add(value);
            }
        }
        Require(fixtureAdmission is not null || !properties.Any(value => value.StartsWith("ConfiguratorCaptureCalibration=", StringComparison.Ordinal)),
            "CAPTURE_CALIBRATION_TEST_ONLY");
        string sdk = Path.GetDirectoryName(Environment.ProcessPath)!;
        Require(File.Exists(Path.Combine(sdk, "sdk", SdkVersion, "MSBuild.dll")), "CAPTURE_SDK_UNSUPPORTED");
        Directory.CreateDirectory(Path.Combine(destination, "private"));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(destination, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string frozen = Path.Combine(destination, "frozen");
        FreezeRepository(root, Path.Combine(frozen, "repo"), additions, fixtureAdmission);
        string[] repositoryNamespace = SnapshotRepositoryNamespace(Path.Combine(frozen, "repo"));
        Require(File.Exists(Path.Combine(frozen, "repo", project)), "CAPTURE_ENTRY_MISSING");
        bool hasPackages = Directory.EnumerateFiles(Path.Combine(frozen, "repo"), "packages.lock.json", SearchOption.AllDirectories).Any();
        var session = new CaptureSession(destination, new(root, project, target, sdk, properties.ToArray(), hasPackages, repositoryNamespace));
        File.WriteAllBytes(Path.Combine(destination, "private/session.json"), Canonical(session._configuration));
        Clone(Path.Combine(frozen, "repo"), session.Work);
        string globalPath = Path.Combine(session.Work, "global.json");
        if (!File.Exists(globalPath))
            File.WriteAllBytes(globalPath, Canonical(new
            {
                sdk = new { version = SdkVersion, workloadVersion = WorkloadVersion, rollForward = "disable" }
            }));
        using (var global = JsonDocument.Parse(File.ReadAllBytes(globalPath)))
        {
            var selected = global.RootElement.GetProperty("sdk");
            Require(selected.GetProperty("version").GetString() == SdkVersion
                && selected.GetProperty("workloadVersion").GetString() == WorkloadVersion
                && selected.GetProperty("rollForward").GetString() == "disable", "CAPTURE_SDK_UNSUPPORTED");
        }
        // The SDK, workload packs and package cache are frozen before any evaluation or restore.
        // Reflinks are an optimization only; copy semantics remain independent file identities.
        Clone(sdk, Path.Combine(frozen, "sdk"));
        session.FreezePackages();
        Clone(Path.Combine(frozen, "package"), Path.Combine(destination, "work/package"));
        if (store is not null)
        {
            FreezeStore(store, Path.Combine(frozen, "external-store"));
            Clone(Path.Combine(frozen, "external-store"), Path.Combine(destination, "work/external-store"));
        }
        Directory.CreateDirectory(Path.Combine(destination, "private/tool"));
        foreach (string name in new[] { "Configurator.BuildCapture.dll", "Configurator.BuildCapture.deps.json", "Configurator.BuildCapture.runtimeconfig.json" })
            File.Copy(Path.Combine(AppContext.BaseDirectory, name), Path.Combine(destination, "private/tool", name));
        Directory.CreateDirectory(Path.Combine(destination, "private/events"));
        Directory.CreateDirectory(Path.Combine(destination, "private/tmp"));
        string framework = Path.Combine(frozen, "sdk/sdk", SdkVersion, "Microsoft.Build.Framework.dll");
        session.Observe(framework, "tool");
        _ = System.Reflection.Assembly.LoadFrom(framework);
        session.CreateTaskBindings();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(destination, "private/tool")))
            session.Observe(file, "tool");
        session.Observe(Path.Combine(session.Work, project), "recipe");
        session.Observe(Path.Combine(frozen, "sdk", "dotnet"), "tool");
        // Host/resolver inputs are part of the tool recipe. Installer bookkeeping
        // under metadata/workloads is writable SDK state, not a selected pack input.
        foreach (string directory in new[] { "host", "shared", "sdk-manifests", "sdk/" + SdkVersion + "/Roslyn/bincore" })
        {
            string path = Path.Combine(frozen, "sdk", directory);
            if (Directory.Exists(path))
                foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    session.Observe(file, "tool");
        }
        foreach (string name in new[] { "dotnet.deps.json", "dotnet.runtimeconfig.json", "MSBuild.deps.json", "MSBuild.runtimeconfig.json" })
            session.Observe(Path.Combine(frozen, "sdk/sdk", SdkVersion, name), "tool");
        foreach (string name in DefaultTaskRecipes)
            session.Observe(Path.Combine(frozen, "sdk/sdk", SdkVersion, name), "recipe");
        session.Observe(Path.Combine(frozen, "sdk/sdk", SdkVersion, "Roslyn/Microsoft.Build.Tasks.CodeAnalysis.dll"), "tool");
        session.Observe(Path.Combine(frozen, "sdk/sdk", SdkVersion, "Roslyn/Microsoft.Managed.Core.targets"), "recipe");
        if (File.Exists(Path.Combine(session.Work, "global.json"))) session.Observe(Path.Combine(session.Work, "global.json"), "recipe");
        int exit = session.Build(project, target, properties);
        if (exit == 0 && target == "Build" && project.EndsWith(".Tests.csproj", StringComparison.Ordinal))
            exit = session.Generator("evidence", [], execute: false);
        if (exit != 0)
        {
            Console.Error.WriteLine("CAPTURE_BUILD_FAILED");
            return exit;
        }
        var capture = session.Assemble(sealOutputs: true);
        byte[] bytes = Canonical(capture);
        File.WriteAllBytes(Path.Combine(destination, "capture.json"), bytes);
        File.WriteAllText(Path.Combine(destination, "capture.sha256"), Hash(bytes));
        Console.WriteLine("CAPTURE_SEALED");
        return 0;
    }

    internal int Build(string project, string target, IEnumerable<string>? properties = null, bool restore = true)
    {
        string intermediate = "obj/capture/" + Guid.NewGuid().ToString("N") + "/";
        _lastBuildOutputBase = "bin/capture/" + Guid.NewGuid().ToString("N") + "/";
        string overrides = Path.Combine(Work, "artifacts/capture-overrides.targets");
        string entry = Path.GetFullPath(project, Work);
        if (Path.GetExtension(entry) == ".proj")
        {
            string wrapper = Path.Combine(Path.GetDirectoryName(entry)!, ".capture-entry-" + Guid.NewGuid().ToString("N") + ".proj");
            File.WriteAllText(wrapper, new XElement("Project",
                new XElement("Import", new XAttribute("Project", overrides)),
                new XElement("Import", new XAttribute("Project", entry))).ToString());
            entry = wrapper;
        }
        var arguments = new List<string>
        {
            "Configuration=Release", "UseSharedCompilation=false",
            "EmitCompilerGeneratedFiles=true",
            "PathMap=" + Work + "=repo",
            "BaseIntermediateOutputPath=" + intermediate,
            "BaseOutputPath=" + _lastBuildOutputBase,
            "CustomAfterMicrosoftCommonProps=" + Path.Combine(Work, "artifacts/capture-early.props"),
            "ConfiguratorCaptureSession=" + _session,
            "ConfiguratorCaptureTool=" + Tool,
            "ConfiguratorCaptureOverrides=" + overrides,
            "CustomBeforeMicrosoftCommonTargets=" + overrides,
            "RestorePackagesPath=" + Path.Combine(_session, "work/package")
        };
        if (Directory.Exists(Path.Combine(Frozen, "external-store")))
            arguments.Add("DefinitionStoreSourceRoot=" + Path.Combine(_session, "work/external-store"));
        if (properties is not null) arguments.AddRange(properties);
        if (restore && Path.GetExtension(project) == ".csproj")
        {
            int restored = Execute(Dotnet, new[] { Tool, "--host", _session, entry, "Restore" }.Concat(arguments), Work, "build");
            if (restored != 0) return restored;
        }
        return Execute(Dotnet, new[] { Tool, "--host", _session, entry, target }.Concat(arguments), Work, "build");
    }

    private void CreateTaskBindings()
    {
        string sdk = Path.Combine(Frozen, "sdk/sdk", SdkVersion);
        string[] assemblies =
        [
            Tool,
            Path.Combine(sdk, "Microsoft.Build.dll"),
            Path.Combine(sdk, "Microsoft.Build.Tasks.Core.dll"),
            Path.Combine(sdk, "NuGet.Build.Tasks.dll"),
            Path.Combine(sdk, "Roslyn/Microsoft.Build.Tasks.CodeAnalysis.dll"),
            Path.Combine(sdk, "Sdks/Microsoft.NET.Sdk/tools/net10.0/Microsoft.NET.Build.Tasks.dll"),
            Path.Combine(sdk, "Sdks/Microsoft.NET.Sdk.StaticWebAssets/tasks/net10.0/Microsoft.NET.Sdk.StaticWebAssets.Tasks.dll"),
            Path.Combine(sdk, "Sdks/Microsoft.NET.Sdk.Razor/tasks/net10.0/Microsoft.NET.Sdk.Razor.Tasks.dll"),
            Path.Combine(sdk, "Sdks/Microsoft.NET.Sdk.BlazorWebAssembly/tools/net10.0/Microsoft.NET.Sdk.BlazorWebAssembly.Tasks.dll"),
            Path.Combine(sdk, "Sdks/Microsoft.SourceLink.Common/tools/net/Microsoft.SourceLink.Common.dll"),
            Path.Combine(sdk, "Sdks/Microsoft.Build.Tasks.Git/tools/net/Microsoft.Build.Tasks.Git.dll"),
            Path.Combine(sdk, "Sdks/Microsoft.SourceLink.GitHub/tools/net/Microsoft.SourceLink.GitHub.dll"),
            Path.Combine(sdk, "Sdks/Microsoft.SourceLink.GitLab/tools/net/Microsoft.SourceLink.GitLab.dll"),
            Path.Combine(sdk, "Sdks/Microsoft.SourceLink.Bitbucket.Git/tools/net/Microsoft.SourceLink.Bitbucket.Git.dll"),
            Path.Combine(sdk, "Sdks/Microsoft.SourceLink.AzureRepos.Git/tools/net/Microsoft.SourceLink.AzureRepos.Git.dll")
        ];
        string testingTasks = Path.Combine(Frozen,
            "package/microsoft.testing.platform.msbuild/2.3.3/_MSBuildTasks/netstandard2.0/Microsoft.Testing.Platform.MSBuild.dll");
        if (File.Exists(testingTasks)) assemblies = [.. assemblies, testingTasks];
        foreach (string path in new[]
        {
            Path.Combine(Frozen, "package/avalonia/12.1.3/tools/netstandard2.0/Avalonia.Build.Tasks.dll"),
            Path.Combine(Frozen, "package/microsoft.net.illink.tasks/10.0.10/tools/net/ILLink.Tasks.dll"),
            Path.Combine(Frozen, "sdk/packs/Microsoft.NET.Runtime.MonoAOTCompiler.Task/10.0.9/tasks/net10.0/MonoAOTCompiler.dll"),
            Path.Combine(Frozen, "sdk/packs/Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.9/tasks/WasmBuildTasks.dll")
        })
            if (File.Exists(path)) assemblies = [.. assemblies, path];
        var directories = assemblies.Select(Path.GetDirectoryName).OfType<string>().Prepend(sdk).ToArray();
        System.Reflection.Assembly? Resolve(System.Runtime.Loader.AssemblyLoadContext context, System.Reflection.AssemblyName name)
        {
            string? path = directories.Select(directory => Path.Combine(directory, name.Name + ".dll")).FirstOrDefault(File.Exists);
            if (path is null) return null;
            Observe(path, "tool");
            return context.LoadFromAssemblyPath(path);
        }
        System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += Resolve;
        try
        {
            var framework = System.Reflection.Assembly.LoadFrom(Path.Combine(sdk, "Microsoft.Build.Framework.dll"));
            Type taskInterface = framework.GetType("Microsoft.Build.Framework.ITask")!;
            var root = ReadWasmPropsProfile.OverrideProject(Tool);
            var boundaries = new HashSet<string>(StringComparer.Ordinal) { ReadWasmPropsProfile.Name };
            var contracts = new List<TaskContract> { ReadWasmPropsProfile.Contract(Tool) };
            foreach (string path in assemblies)
            {
                Observe(path, "tool");
                var assembly = System.Reflection.Assembly.LoadFrom(path);
                var taskTypes = path.EndsWith("/MonoTargetsTasks.dll", StringComparison.Ordinal)
                    || path.EndsWith("/WasmBuildTasks.dll", StringComparison.Ordinal)
                    ? assembly.GetTypes() : assembly.GetExportedTypes();
                foreach (Type type in taskTypes.Where(type => !type.IsAbstract
                    && taskInterface.IsAssignableFrom(type)))
                {
                    contracts.Add(new(type.Name, type.FullName!, path,
                        type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                            .Where(property => property.Name is not ("BuildEngine" or "HostObject")
                                && (property.SetMethod?.IsPublic == true
                                    || property.GetMethod?.IsPublic == true
                                        && property.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == "Microsoft.Build.Framework.OutputAttribute"))
                                && property.PropertyType.FullName != "Microsoft.Build.Framework.TaskEnvironment")
                            .Select(property => new TaskParameterContract(property.Name, property.PropertyType.FullName!,
                                property.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == "Microsoft.Build.Framework.OutputAttribute")))
                            .OrderBy(property => property.Name, StringComparer.Ordinal).ToArray()));
                    if (!BuildCaptureLogger.KnownTasks.Contains(type.Name)) continue;
                    if (type.Name == "MapSourceRoots")
                    {
                        Require(type.FullName == "Microsoft.CodeAnalysis.BuildTasks.MapSourceRoots"
                            && path == Path.Combine(sdk, "Roslyn/Microsoft.Build.Tasks.CodeAnalysis.dll"),
                            "CAPTURE_MAP_IDENTITY_UNSUPPORTED");
                        var shape = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.DeclaredOnly);
                        Require(shape.Length == 3 && shape.All(property => property.Name switch
                        {
                            "SourceRoots" => property.PropertyType.FullName == "Microsoft.Build.Framework.ITaskItem[]"
                                && property.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == "Microsoft.Build.Framework.RequiredAttribute"),
                            "Deterministic" => property.PropertyType == typeof(bool),
                            "MappedSourceRoots" => property.PropertyType.FullName == "Microsoft.Build.Framework.ITaskItem[]"
                                && property.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == "Microsoft.Build.Framework.OutputAttribute"),
                            _ => false
                        }), "CAPTURE_MAP_SHAPE_UNSUPPORTED");
                        continue;
                    }
                    // MSBuild and CallTarget route graphs; their native engine integration
                    // remains intact. Their child projects are independently instrumented.
                    if (type.Name is "MSBuild" or "CallTarget" or "BuildCaptureTask") continue;
                    boundaries.Add(type.Name);
                    // Keep the SDK's native task binding. The calibrated host
                    // freezes declared inputs inline and reconciles every
                    // invocation; replacing task instances breaks tasks that
                    // depend on native engine binding services.
                }
            }
            string enginePath = Path.Combine(sdk, "Microsoft.Build.dll");
            Observe(enginePath, "tool");
            var engine = System.Reflection.Assembly.LoadFrom(enginePath);
            var intrinsic = engine.GetTypes().Single(type => type.Name == "MSBuild" && taskInterface.IsAssignableFrom(type));
            var skipTargets = intrinsic.GetProperty("SkipNonexistentTargets",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                ?? throw new CaptureException("CAPTURE_INTRINSIC_SHAPE_UNSUPPORTED");
            Require(skipTargets.PropertyType == typeof(bool) && skipTargets.SetMethod?.IsPublic == true,
                "CAPTURE_INTRINSIC_SHAPE_UNSUPPORTED");
            int msbuild = contracts.FindIndex(contract => contract.Name == "MSBuild");
            Require(msbuild >= 0 && contracts[msbuild].Parameters.All(parameter => parameter.Name != skipTargets.Name),
                "CAPTURE_INTRINSIC_SHAPE_UNSUPPORTED");
            contracts[msbuild] = contracts[msbuild] with
            {
                Parameters = [.. contracts[msbuild].Parameters,
                    new(skipTargets.Name, skipTargets.PropertyType.FullName!, false)]
            };
            string destination = Path.Combine(Work, "artifacts/capture-overrides.targets");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, root.ToString());
            string toolsetRecipe = Path.Combine(Work, ReadWasmPropsProfile.ToolsetRecipe);
            Directory.CreateDirectory(Path.GetDirectoryName(toolsetRecipe)!);
            File.WriteAllText(toolsetRecipe, ReadWasmPropsProfile.ToolsetOverrideProject(Tool).ToString());
            File.WriteAllText(Path.Combine(Work, "artifacts/capture-early.props"),
                new XElement("Project", new XElement("PropertyGroup",
                    new XElement("DefaultItemExcludes", "$(DefaultItemExcludes);**/obj/**;**/bin/**"))).ToString());
            File.WriteAllBytes(Path.Combine(_session, "private/boundary-tasks.json"), Canonical(boundaries.Order(StringComparer.Ordinal).ToArray()));
            File.WriteAllBytes(Path.Combine(_session, "private/task-parameters.json"), Canonical(contracts));
        }
        finally { System.Runtime.Loader.AssemblyLoadContext.Default.Resolving -= Resolve; }
    }

    internal int Generator(string operation, string[] arguments, bool execute = true)
    {
        (string source, string assembly) = operation switch
        {
            "ratchets" => ("eng/configurator/GenerateConfiguratorRatchets.cs", "Configurator.RatchetGenerator"),
            "store" => ("eng/configurator/BuildDefinitionStore.cs", "Configurator.DefinitionStoreBuilder"),
            "editor" => ("eng/configurator/BuildBrowserEditor.cs", "Configurator.BrowserEditorBuilder"),
            "notices" => (".ci/scripts/write-dependency-notices.cs", "Configurator.DependencyNotices"),
            "evidence" => (".ci/scripts/prepare-test-evidence.cs", "Configurator.TestEvidence"),
            _ => throw new CaptureException("CAPTURE_GENERATOR_UNSUPPORTED")
        };
        Require(operation != "editor" || arguments is ["--check", _], "CAPTURE_EDITOR_OPERATION_UNSUPPORTED");
        Require(operation != "ratchets" || arguments is ["--check"], "CAPTURE_RATCHET_OPERATION_UNSUPPORTED");
        string directory = Path.Combine(Work, "artifacts/capture-tools", operation);
        Directory.CreateDirectory(directory);
        string original = Path.Combine(Work, source);
        Observe(original, "recipe");
        var properties = new XElement("PropertyGroup",
            new XElement("OutputType", "Exe"), new XElement("TargetFramework", "net10.0"),
            new XElement("ImplicitUsings", "enable"), new XElement("Nullable", "enable"),
            new XElement("EnableDefaultCompileItems", "false"), new XElement("IsPackable", "false"));
        var items = new XElement("ItemGroup", new XElement("Compile", new XAttribute("Include", "Program.cs")));
        var body = new List<string>();
        // These four owned recipes use only property/project file-program directives.
        // Materialize their native project explicitly so no hidden CLI build escapes logging.
        foreach (string line in File.ReadAllLines(original))
        {
            if (line.StartsWith("#:property ", StringComparison.Ordinal))
            {
                string setting = line["#:property ".Length..];
                int equals = setting.IndexOf('=');
                Require(equals > 0, "CAPTURE_GENERATOR_DIRECTIVE_INVALID");
                properties.Add(new XElement(setting[..equals], setting[(equals + 1)..]));
                body.Add("");
            }
            else if (line.StartsWith("#:project ", StringComparison.Ordinal))
            {
                string reference = Path.GetFullPath(line["#:project ".Length..], Path.GetDirectoryName(original)!);
                Require(Inside(reference, Work), "CAPTURE_GENERATOR_REFERENCE_UNADMITTED");
                if (reference == Path.Combine(Work, "eng/configurator/Configurator.BuildCapture/Configurator.BuildCapture.csproj"))
                    items.Add(new XElement("Reference", new XAttribute("Include", "Configurator.BuildCapture"),
                        new XElement("HintPath", Tool), new XElement("Private", "true")));
                else items.Add(new XElement("ProjectReference", new XAttribute("Include", reference)));
                body.Add("");
            }
            else
            {
                Require(!line.StartsWith("#:", StringComparison.Ordinal) && !line.StartsWith("#!", StringComparison.Ordinal),
                    "CAPTURE_GENERATOR_DIRECTIVE_UNSUPPORTED");
                body.Add(line);
            }
        }
        string project = Path.Combine(directory, assembly + ".csproj");
        File.WriteAllText(project, new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"), properties, items).ToString());
        File.WriteAllLines(Path.Combine(directory, "Program.cs"), body);
        int build = Build(project, "Build");
        if (build != 0) return build;
        string executable = Path.Combine(directory, _lastBuildOutputBase, "Release/net10.0", assembly + ".dll");
        Observe(executable, "generator");
        if (!execute) return 0;
        if (operation != "ratchets")
            return Execute(Dotnet, new[] { executable }.Concat(arguments), Work, "generator");
        string invocation = Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_PARENT_INVOCATION")
            ?? throw new CaptureException("CAPTURE_GENERATOR_UNBOUND");
        string id = Guid.NewGuid().ToString("N");
        var receipt = new GeneratorReceipt(id, invocation, Observe(original, "recipe"), Observe(executable, "generator"), null);
        WriteLifecycle("generators", id, receipt);
        int exit = Execute(Dotnet, new[] { executable }.Concat(arguments), Work, "generator", id);
        WriteLifecycle("generators", id, receipt with { Succeeded = exit == 0 });
        return exit;
    }

    internal int Execute(string executable, IEnumerable<string> arguments, string workingDirectory, string operation,
        string? generatorId = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        start.Environment.Clear();
        start.Environment["PATH"] = Path.GetDirectoryName(Dotnet) + Path.PathSeparator
            + (OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32")
                : "/usr/bin:/bin");
        if (OperatingSystem.IsWindows())
            start.Environment["SystemRoot"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        start.Environment["LANG"] = "C.UTF-8";
        start.Environment["LC_ALL"] = "C.UTF-8";
        if (operation == "check")
            foreach (string name in new[]
            {
                "CONFIGURATOR_HOST_URL", "CONFIGURATOR_PACKAGE_ROOT",
                "DISPLAY", "WAYLAND_DISPLAY", "XDG_RUNTIME_DIR", "DBUS_SESSION_BUS_ADDRESS"
            })
                if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        start.Environment["DOTNET_ROOT"] = Path.Combine(Frozen, "sdk");
        start.Environment["DOTNET_HOST_PATH"] = Dotnet;
        start.Environment["NUGET_PACKAGES"] = Path.Combine(_session, "work/package");
        start.Environment["DOTNET_CLI_HOME"] = Path.Combine(_session, "private/home");
        start.Environment["HOME"] = Path.Combine(_session, "private/home");
        start.Environment["USERPROFILE"] = Path.Combine(_session, "private/home");
        start.Environment["XDG_CONFIG_HOME"] = Path.Combine(_session, "private/home/.config");
        start.Environment["TMPDIR"] = Path.Combine(_session, "private/tmp");
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true";
        start.Environment["MSBUILDLOGTASKINPUTS"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["DOTNET_PROCESSOR_COUNT"] = "2";
        if (operation is "build" or "generator-build" or "generator")
            start.Environment["CONFIGURATOR_CAPTURE_SESSION"] = _session;
        if (generatorId is not null)
        {
            start.Environment["CONFIGURATOR_CAPTURE_GENERATOR_ID"] = generatorId;
            start.Environment["CONFIGURATOR_CAPTURE_GRAPH"] = CurrentGraph;
            start.Environment["CONFIGURATOR_CAPTURE_PARENT_INVOCATION"] =
                Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_PARENT_INVOCATION")
                    ?? throw new CaptureException("CAPTURE_GENERATOR_UNBOUND");
        }
        if (operation is "build" or "generator-build") ExpectGraph(start);
        // Host-specific MSBuild overrides must not route evaluation outside the admitted SDK.
        foreach (string name in new[] { "MSBuildSDKsPath", "MSBUILD_EXE_PATH", "MSBuildExtensionsPath", "MSBuildToolsPath" })
            start.Environment.Remove(name);
        using Process process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(1_200_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new CaptureException("CAPTURE_PROCESS_TIMEOUT");
        }
        string log = Path.Combine(_session, "private", operation + "-" + Guid.NewGuid().ToString("N") + ".log");
        string text = output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult();
        File.WriteAllText(log, text);
        if (process.ExitCode != 0)
        {
            foreach (string code in Regex.Matches(text, @"\b(?:CAPTURE_[A-Za-z0-9_]+|MSB\d{4}|NETSDK\d{4}|CS\d{4}|CA\d{4}|NU\d{4}|TUnit[A-Za-z0-9]+)\b")
                .Select(match => match.Value).Distinct(StringComparer.Ordinal))
                Console.Error.WriteLine(code);
        }
        return process.ExitCode;
    }

    internal FileIdentity Observe(string path, string kind)
    {
        path = Path.GetFullPath(path);
        Unlinked(path);
        Require(File.Exists(path), "CAPTURE_INPUT_MISSING");
        string logical = Logical(path);
        long length = new FileInfo(path).Length;
        string hash = FileHash(path);
        if (logical.StartsWith("repo/", StringComparison.Ordinal) || logical.StartsWith("external-store/", StringComparison.Ordinal)
            || logical.StartsWith("package/", StringComparison.Ordinal))
        {
            string original = Path.Combine(Frozen, logical);
            if (File.Exists(original))
                Require(FileHash(original) == hash, "CAPTURE_ORIGINAL_MUTATED");
            else
            {
                Require(!logical.StartsWith("package/", StringComparison.Ordinal), "CAPTURE_PACKAGE_UNADMITTED");
                logical = "generated/" + logical;
                kind = "generated";
            }
        }
        if (logical.StartsWith("generated/", StringComparison.Ordinal))
        {
            kind = "generated";
            Append(new("generation", logical, kind, hash, length, null, null, null));
            logical = "generated/versions/" + hash + "/" + logical["generated/".Length..];
        }
        if (_observed.TryGetValue(logical, out var previous))
            Require(previous.Sha256 == hash, "CAPTURE_CONSUMED_INPUT_MUTATED");
        else
        {
            if (logical.StartsWith("generated/", StringComparison.Ordinal))
            {
                string preserved = Path.Combine(Frozen, logical);
                Directory.CreateDirectory(Path.GetDirectoryName(preserved)!);
                if (!File.Exists(preserved)) File.Copy(path, preserved);
                Require(FileHash(preserved) == hash, "CAPTURE_GENERATED_INPUT_MUTATED");
            }
            _observed.Add(logical, new(logical, length, hash, kind));
            Append(new("input", logical, kind, hash, length, null, null, null));
        }
        return new(logical, length, hash, kind);
    }

    internal void Setting(string project, string name, JsonElement value) =>
        Append(new("setting", "", "", null, 0, Logical(project), name, value));

    internal void RecordAssets(string project, string path)
    {
        string directory = Path.Combine(_session, "private/assets");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, Hash(System.Text.Encoding.UTF8.GetBytes(Logical(project))) + ".txt"), path);
    }

    internal string Assets(string project)
    {
        string path = Path.Combine(_session, "private/assets", Hash(System.Text.Encoding.UTF8.GetBytes(Logical(Path.GetFullPath(project, Work)))) + ".txt");
        Require(File.Exists(path), "CAPTURE_ASSETS_BINDING_MISSING");
        string assets = File.ReadAllText(path);
        Require(Inside(assets, Work) && File.Exists(assets), "CAPTURE_ASSETS_BINDING_INVALID");
        return assets;
    }

    internal string NormalizeSetting(string value)
    {
        foreach ((string path, string alias) in new[]
        {
            (Work, "repo"), (Path.Combine(_session, "work/external-store"), "external-store"),
            (Path.Combine(_session, "work/package"), "package"),
            (Path.Combine(Frozen, "repo"), "repo"), (Path.Combine(Frozen, "package"), "package"),
            (Path.Combine(Frozen, "sdk"), "sdk"), (Path.Combine(_session, "private/tool"), "capture-tool"),
            (Path.Combine(_session, "private/tmp"), "generated/tmp"), (Path.Combine(_session, "private/home"), "generated/home")
        })
            value = value.Replace(path, alias, StringComparison.Ordinal);
        return value;
    }

    internal void PackageMetadata(string assetsPath)
    {
        using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
        string[] folders = assets.RootElement.GetProperty("packageFolders").EnumerateObject().Select(item => item.Name).ToArray();
        foreach (var library in assets.RootElement.GetProperty("libraries").EnumerateObject())
        {
            if (library.Value.GetProperty("type").GetString() != "package") continue;
            string relative = library.Value.GetProperty("path").GetString()!;
            Require(Relative(relative), "CAPTURE_PACKAGE_ALIAS_INVALID");
            string? directory = folders.Select(folder => Path.Combine(folder, relative)).FirstOrDefault(Directory.Exists);
            Require(directory is not null, "CAPTURE_PACKAGE_MISSING");
            foreach (string path in Directory.EnumerateFiles(directory!, "*", SearchOption.TopDirectoryOnly))
                if (Path.GetExtension(path) == ".nuspec" || path.EndsWith(".nupkg.sha512", StringComparison.Ordinal)
                    || Path.GetFileName(path) is ".nupkg.metadata" or "runtime.json")
                    Observe(path, "dependency");
        }
    }

    internal void Finished(bool success) =>
        Append(new(success ? "finished" : "failed", GraphId(), "", null, 0, null, null, null));

    internal void Started() => Append(new("started", GraphId(), "", null, 0, null, null, null));

    internal bool RequiresBoundary(string task) => (_boundaries ??= JsonSerializer.Deserialize<string[]>(
        File.ReadAllBytes(Path.Combine(_session, "private/boundary-tasks.json")), Program.Json)!.ToHashSet(StringComparer.Ordinal)).Contains(task);

    internal void Consumer(string project, string task, bool expected) =>
        Append(new(expected ? "task" : "consumer", Logical(project), task, null, 0, null, null, null));

    internal void InvocationEvent(string operation, string id, JsonElement value) =>
        Append(new(operation, id, "", null, 0, null, null, value));

    private static string GraphId() => Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_GRAPH")
        ?? throw new CaptureException("CAPTURE_GRAPH_UNBOUND");
    internal static string CurrentGraph => GraphId();

    private void ExpectGraph(ProcessStartInfo start)
    {
        string id = Guid.NewGuid().ToString("N");
        start.Environment["CONFIGURATOR_CAPTURE_GRAPH"] = id;
        string? parent = Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_PARENT_INVOCATION");
        string? graph = Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_GRAPH");
        Require(parent is null || graph is not null && parent.StartsWith(graph + "/", StringComparison.Ordinal),
            "CAPTURE_CHILD_HOST_UNBOUND");
        Append(new("expected", id, "", null, 0, graph, parent, null));
    }

    internal void Rejected(string code) => Append(new("refused", "", code, null, 0, null, null, null));

    private void Append(Observation observation)
    {
        string directory = Path.Combine(_session, "private/events");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json"), Canonical(observation));
    }

    internal string Logical(string path)
    {
        path = Path.GetFullPath(path);
        if (Inside(path, Work)) return "repo/" + Path.GetRelativePath(Work, path).Replace('\\', '/');
        if (Inside(path, Path.Combine(_session, "work/package")))
            return "package/" + Path.GetRelativePath(Path.Combine(_session, "work/package"), path).Replace('\\', '/');
        if (Inside(path, Path.Combine(_session, "work/external-store")))
            return "external-store/" + Path.GetRelativePath(Path.Combine(_session, "work/external-store"), path).Replace('\\', '/');
        foreach (string alias in new[] { "repo", "package", "sdk", "external-store" })
        {
            string root = Path.Combine(Frozen, alias);
            if (Inside(path, root)) return alias + "/" + Path.GetRelativePath(root, path).Replace('\\', '/');
        }
        if (Inside(path, Path.Combine(_session, "private/tool")))
            return "capture-tool/" + Path.GetRelativePath(Path.Combine(_session, "private/tool"), path).Replace('\\', '/');
        if (Inside(path, Path.Combine(_session, "private/tmp")))
            return "generated/tmp/" + Path.GetRelativePath(Path.Combine(_session, "private/tmp"), path).Replace('\\', '/');
        if (Inside(path, Path.Combine(_session, "private/home")))
            return "generated/home/" + Path.GetRelativePath(Path.Combine(_session, "private/home"), path).Replace('\\', '/');
        throw new CaptureException("CAPTURE_ROOT_UNADMITTED");
    }

    private string Resolve(string logical)
    {
        Require(Relative(logical), "CAPTURE_ALIAS_INVALID");
        if (logical.StartsWith("generated/versions/", StringComparison.Ordinal))
            return Path.Combine(Frozen, logical);
        if (logical.StartsWith("generated/repo/", StringComparison.Ordinal))
            return Path.Combine(Work, logical["generated/repo/".Length..]);
        if (logical.StartsWith("generated/tmp/", StringComparison.Ordinal))
            return Path.Combine(_session, "private/tmp", logical["generated/tmp/".Length..]);
        if (logical.StartsWith("generated/home/", StringComparison.Ordinal))
            return Path.Combine(_session, "private/home", logical["generated/home/".Length..]);
        if (logical.StartsWith("generated/external-store/", StringComparison.Ordinal))
            return Path.Combine(_session, "work/external-store", logical["generated/external-store/".Length..]);
        if (logical.StartsWith("capture-tool/", StringComparison.Ordinal))
            return Path.Combine(_session, "private/tool", logical["capture-tool/".Length..]);
        Require(logical.StartsWith("repo/", StringComparison.Ordinal) || logical.StartsWith("sdk/", StringComparison.Ordinal)
            || logical.StartsWith("package/", StringComparison.Ordinal) || logical.StartsWith("external-store/", StringComparison.Ordinal),
            "CAPTURE_ALIAS_INVALID");
        return Path.Combine(Frozen, logical);
    }

    private BuildInputs Assemble(bool sealOutputs)
    {
        Require(_configuration.RepositoryNamespace.SequenceEqual(
            SnapshotRepositoryNamespace(Path.Combine(Frozen, "repo")), StringComparer.Ordinal), "CAPTURE_NAMESPACE_MISMATCH");
        var entries = Directory.EnumerateFiles(Path.Combine(_session, "private/events"), "*.json")
            .Select(path => JsonSerializer.Deserialize<Observation>(File.ReadAllBytes(path), Program.Json)
                ?? throw new CaptureException("CAPTURE_RECORD_INVALID")).ToArray();
        Require(entries.Any(entry => entry.Operation == "finished")
            && entries.All(entry => entry.Operation is "input" or "setting" or "expected" or "started" or "finished" or "task" or "consumer"
                or "invocation-started" or "invocation-freeze" or "invocation-finished" or "generation" or "target-event"
                or "moved" or "retired" or "json-factory-barrier" or "json-factory-outcome"),
            "CAPTURE_GRAPH_INCOMPLETE");
        foreach (var graph in entries.Where(entry => entry.Operation is "expected" or "started" or "finished")
            .GroupBy(entry => entry.Path, StringComparer.Ordinal))
            Require(graph.Count(entry => entry.Operation == "expected") == 1
                && graph.Count(entry => entry.Operation == "started") == 1
                && graph.Count(entry => entry.Operation == "finished") == 1, "CAPTURE_SUBPROCESS_MISSING");
        foreach (var consumer in entries.Where(entry => entry.Operation is "task" or "consumer")
            .GroupBy(entry => (entry.Path, entry.Kind)))
            Require(consumer.Count(entry => entry.Operation == "task") == consumer.Count(entry => entry.Operation == "consumer"),
                "CAPTURE_CONSUMER_BOUNDARY_MISSING");
        var targets = entries.Where(entry => entry.Operation == "target-event")
            .Select(entry => entry.Value!.Value.Deserialize<CapturedTarget>(Program.Json)
                ?? throw new CaptureException("CAPTURE_TARGET_INVALID"))
            .OrderBy(target => target.Id, StringComparer.Ordinal).ThenBy(target => target.Outcome, StringComparer.Ordinal)
            .ThenBy(target => target.Reason, StringComparer.Ordinal).ToArray();
        foreach (var target in targets.Where(target => target.Outcome == "started"))
            Require(targets.Count(completed => completed.Id == target.Id && completed.Project == target.Project
                && completed.Target == target.Target && completed.Outcome == "succeeded") == 1,
                "CAPTURE_TARGET_INCOMPLETE");
        var invocations = entries.Where(entry => entry.Operation.StartsWith("invocation-", StringComparison.Ordinal))
            .GroupBy(entry => entry.Path, StringComparer.Ordinal).Select(group =>
            {
                var started = group.Where(entry => entry.Operation == "invocation-started").ToArray();
                var finished = group.Where(entry => entry.Operation == "invocation-finished").ToArray();
                Require(started.Length == 1 && finished.Length == 1, "CAPTURE_INVOCATION_INCOMPLETE");
                var invocation = finished[0].Value!.Value.Deserialize<CapturedInvocation>(Program.Json)
                    ?? throw new CaptureException("CAPTURE_INVOCATION_INVALID");
                var parameters = group.Where(entry => entry.Operation == "invocation-freeze")
                    .Select(entry => entry.Value!.Value.Deserialize<CapturedParameter>(Program.Json)
                        ?? throw new CaptureException("CAPTURE_PARAMETER_INVALID"))
                    .OrderBy(parameter => parameter.Name, StringComparer.Ordinal).ToArray();
                Require(invocation.Id == group.Key && invocation.Succeeded
                    && targets.Any(target => target.Id == invocation.Id[..invocation.Id.LastIndexOf('/')]
                        && target.Project == invocation.Project && target.Target == invocation.Target && target.Outcome == "started")
                    && parameters.Select(parameter => parameter.Name).Distinct(StringComparer.Ordinal).Count() == parameters.Length
                    && Hash(Canonical(parameters)) == invocation.Receipt
                    && Canonical(parameters).AsSpan().SequenceEqual(Canonical(invocation.Parameters))
                    && Canonical(new { invocation.Project, invocation.Target, invocation.Task, invocation.Assembly, invocation.Recipe })
                        .AsSpan().SequenceEqual(Canonical(started[0].Value!.Value)), "CAPTURE_INVOCATION_RECEIPT_INVALID");
                return invocation;
            }).OrderBy(invocation => invocation.Id, StringComparer.Ordinal).ToArray();
        var jsonSettings = new List<BuildSetting>();
        var jsonTokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in entries.Where(entry => entry.Operation is "json-factory-barrier" or "json-factory-outcome")
            .GroupBy(entry => entry.Path, StringComparer.Ordinal))
        {
            var barriers = group.Where(entry => entry.Operation == "json-factory-barrier").ToArray();
            var outcomes = group.Where(entry => entry.Operation == "json-factory-outcome").ToArray();
            var invocation = invocations.SingleOrDefault(value => value.Id == group.Key && value.Task == ReadWasmPropsProfile.Name);
            Require(invocation is not null && barriers.Length == 1 && outcomes.Length == 1, "CAPTURE_JSON_RECEIPT_INCOMPLETE");
            var barrier = barriers[0].Value!.Value.Deserialize<JsonFactoryBarrier>(Program.Json)!;
            var outcome = outcomes[0].Value!.Value.Deserialize<JsonFactoryOutcome>(Program.Json)!;
            Require(barrier.Id == group.Key && outcome.Id == group.Key && outcome.Token == barrier.Token
                && Guid.TryParseExact(barrier.Token, "N", out _) && jsonTokens.Add(barrier.Token)
                && barrier.Thread > 0 && barrier.Context != "none" && outcome.Succeeded
                && invocation!.Assembly.Path == "capture-tool/Configurator.BuildCapture.dll"
                && Canonical(barrier.Parameters).AsSpan().SequenceEqual(Canonical(invocation.Parameters))
                && barrier.Parameters.Select(value => (value.Name, value.Type)).SequenceEqual(
                    ReadWasmPropsProfile.Contract("").Parameters.Select(value => (value.Name, value.Type)))
                && barrier.Parameters.Single(value => value.Name == "JsonFilePath").Inputs.SequenceEqual([barrier.Input])
                && barrier.Parameters.Where(value => value.Name != "JsonFilePath").All(value => value.Inputs.Length == 0)
                && barrier.Tools.Select(tool => tool.Path).Distinct(StringComparer.Ordinal).Count() == barrier.Tools.Length
                && barrier.Tools.Any(tool => tool.Path == ReadWasmPropsProfile.Assembly && tool.Sha256 == ReadWasmPropsProfile.AssemblyHash)
                && barrier.Tools.Any(tool => tool.Path == ReadWasmPropsProfile.Recipe && tool.Sha256 == ReadWasmPropsProfile.RecipeHash)
                && barrier.Tools.Contains(invocation.Assembly) && barrier.Tools.Contains(invocation.Recipe)
                && outcome.Outputs.Select(value => value.Name).SequenceEqual(ReadWasmPropsProfile.Outputs, StringComparer.Ordinal)
                && outcome.Outputs.All(value => value.Type == "Microsoft.Build.Framework.ITaskItem[]" && value.Inputs.Length == 0),
                "CAPTURE_JSON_RECEIPT_INVALID");
            jsonSettings.Add(new(invocation!.Project, ReadWasmPropsProfile.Name + "." + group.Key,
                JsonSerializer.SerializeToElement(new
                {
                    factory = ReadWasmPropsProfile.Factory,
                    runtimeType = ReadWasmPropsProfile.RuntimeType,
                    parameterGroup = ReadWasmPropsProfile.GroupHash,
                    initialization = "absent",
                    assumption = "pinned-json-reader-no-output-path-consumption-or-independent-children",
                    barrier,
                    outcome
                }, Program.Json)));
        }
        Require(jsonSettings.Count == invocations.Count(value => value.Task == ReadWasmPropsProfile.Name),
            "CAPTURE_JSON_RECEIPT_INCOMPLETE");
        foreach (var child in entries.Where(entry => entry.Operation == "expected" && entry.Name is not null))
            Require(invocations.Any(invocation => invocation.Id == child.Name && invocation.Task == "BuildCaptureTask"
                && invocation.Id.StartsWith(child.Project + "/", StringComparison.Ordinal)), "CAPTURE_CHILD_HOST_UNBOUND");
        var movedInputs = entries.Where(entry => entry.Operation == "moved").Select(entry =>
            entry.Value!.Value.Deserialize<CapturedMove>(Program.Json) ?? throw new CaptureException("CAPTURE_MOVE_INVALID"))
            .OrderBy(move => move.Invocation, StringComparer.Ordinal).ThenBy(move => move.SourcePath, StringComparer.Ordinal).ToArray();
        var retiredInputs = entries.Where(entry => entry.Operation == "retired").Select(entry =>
            entry.Value!.Value.Deserialize<CapturedRetirement>(Program.Json) ?? throw new CaptureException("CAPTURE_RETIREMENT_INVALID"))
            .OrderBy(retirement => retirement.Id, StringComparer.Ordinal).ToArray();
        Require(movedInputs.Length + retiredInputs.Length <= MaximumRepositoryNames, "CAPTURE_LIFECYCLE_LIMIT");
        string[] disposed = movedInputs.Select(move => move.SourcePath).Concat(retiredInputs.Select(item => item.OutputPath)).ToArray();
        Require(disposed.Distinct(StringComparer.Ordinal).Count() == disposed.Length
            && movedInputs.Select(move => move.Invocation).Distinct(StringComparer.Ordinal).Count() == movedInputs.Length
            && retiredInputs.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == retiredInputs.Length,
            "CAPTURE_LIFECYCLE_DUPLICATE");
        foreach (var move in movedInputs) ValidateMove(move, invocations);
        foreach (var retirement in retiredInputs) ValidateRetirement(retirement, invocations, targets);
        foreach (string path in disposed)
            Require(entries.Any(entry => entry.Operation == "generation" && entry.Path == path), "CAPTURE_LIFECYCLE_ORPHAN");
        foreach (var generation in entries.Where(entry => entry.Operation == "generation")
            .GroupBy(entry => entry.Path, StringComparer.Ordinal))
        {
            var transformations = invocations.SelectMany(invocation => invocation.Transformations.Select(
                transformation => (Invocation: invocation, Transformation: transformation)))
                .Where(item => item.Transformation.Path == generation.Key).ToArray();
            foreach (var item in transformations)
                Require((item.Invocation.Task == "DefineStaticWebAssets" && item.Transformation.Parameter == "CacheManifestPath"
                        || item.Invocation.Task == "CompileAvaloniaXamlTask" && item.Transformation.Parameter is "AssemblyFile" or "RefAssemblyFile"
                        || item.Invocation.Task == "BuildCaptureTask" && item.Transformation.Parameter == "InventoryOutput"
                            && item.Invocation.Parameters.Any(parameter => parameter.Name == "Operation"
                                && parameter.Type == "System.String"
                                && parameter.Value.GetProperty("sha256").GetString() == Hash(Canonical<object>("frameworks"))))
                    && item.Transformation.After.Kind == "generated"
                    && item.Transformation.After.Path == "generated/versions/" + item.Transformation.After.Sha256
                        + "/" + generation.Key["generated/".Length..]
                    && (item.Transformation.Before is null
                        || item.Transformation.Before.Kind == "generated"
                            && item.Transformation.Before.Path == "generated/versions/" + item.Transformation.Before.Sha256
                                + "/" + generation.Key["generated/".Length..])
                    && item.Invocation.Parameters.Any(parameter => parameter.Name == item.Transformation.Parameter
                        && (item.Transformation.Before is null || parameter.Inputs.Contains(item.Transformation.Before))),
                    "CAPTURE_TRANSFORMATION_UNADMITTED");
            var transitions = transformations.Select(item => item.Transformation)
                .Where(item => item.Before?.Sha256 != item.After.Sha256)
                .DistinctBy(item => (item.Before?.Sha256, item.After.Sha256)).ToArray();
            var hashes = generation.Select(entry => entry.Hash!).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
            string terminal;
            if (transitions.Length == 0)
            {
                Require(hashes.Count == 1, "CAPTURE_CONSUMED_INPUT_MUTATED");
                terminal = hashes.Single();
            }
            else
            {
                var next = transitions.GroupBy(item => item.Before?.Sha256 ?? "", StringComparer.Ordinal).ToArray();
                Require(next.All(group => group.Count() == 1), "CAPTURE_GENERATION_BRANCH");
                var after = transitions.Select(item => item.After.Sha256).ToHashSet(StringComparer.Ordinal);
                string[] roots = next.Select(group => group.Key).Where(hash => !after.Contains(hash)).ToArray();
                Require(roots.Length == 1, "CAPTURE_GENERATION_CHAIN");
                terminal = roots[0];
                var visited = new HashSet<string>(StringComparer.Ordinal);
                if (terminal.Length != 0) visited.Add(terminal);
                while (next.SingleOrDefault(group => group.Key == terminal) is { } step)
                {
                    terminal = step.Single().After.Sha256;
                    Require(visited.Add(terminal), "CAPTURE_GENERATION_CYCLE");
                }
                Require(visited.SetEquals(hashes), "CAPTURE_GENERATION_CHAIN");
            }
            string live = Resolve(generation.Key);
            var disposition = movedInputs.Where(move => move.SourcePath == generation.Key).Select(move => move.Source)
                .Concat(retiredInputs.Where(item => item.OutputPath == generation.Key).Select(item => item.Source)).SingleOrDefault();
            if (disposition is null)
                Require(File.Exists(live) && FileHash(live) == terminal, "CAPTURE_GENERATED_INPUT_MUTATED");
            else
                Require(disposition.Sha256 == terminal && LifecycleAbsent(live),
                    "CAPTURE_LIFECYCLE_TERMINAL_INVALID");
        }
        var inputs = entries.Where(entry => entry.Operation == "input").GroupBy(entry => entry.Path, StringComparer.Ordinal)
            .Select(group =>
            {
                Require(group.Select(entry => entry.Hash).Distinct(StringComparer.Ordinal).Count() == 1,
                    "CAPTURE_CONSUMED_INPUT_MUTATED");
                var first = group.OrderBy(entry => entry.Kind switch
                {
                    "tool" => 0,
                    "recipe" => 1,
                    "legal" => 2,
                    "compiler" => 3,
                    "generator" => 4,
                    "generated" => 5,
                    "dependency" => 6,
                    "asset" => 7,
                    _ => throw new CaptureException("CAPTURE_CATEGORY_UNSUPPORTED")
                }).First();
                var identity = new FileIdentity(first.Path, first.Length, first.Hash!, first.Kind);
                string current = Resolve(identity.Path);
                if (identity.Path.StartsWith("generated/", StringComparison.Ordinal))
                {
                    CheckFile(Path.Combine(Frozen, identity.Path), identity);
                    if (File.Exists(current)) CheckFile(current, identity);
                }
                else CheckFile(current, identity);
                if (identity.Path.StartsWith("repo/", StringComparison.Ordinal))
                    CheckFile(Path.Combine(Work, identity.Path[5..]), identity);
                else if (identity.Path.StartsWith("package/", StringComparison.Ordinal))
                    CheckFile(Path.Combine(_session, "work", identity.Path), identity);
                else if (identity.Path.StartsWith("external-store/", StringComparison.Ordinal))
                    CheckFile(Path.Combine(_session, "work", identity.Path), identity);
                if (sealOutputs && identity.Path.StartsWith("generated/", StringComparison.Ordinal))
                {
                    string destination = Path.Combine(Frozen, identity.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    if (!File.Exists(destination)) File.Copy(Resolve(identity.Path), destination, overwrite: false);
                }
                return identity;
            }).OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray();
        foreach (var setting in jsonSettings)
        {
            var barrier = setting.Value.GetProperty("barrier").Deserialize<JsonFactoryBarrier>(Program.Json)!;
            Require(barrier.Tools.Append(barrier.Input).All(bound => inputs.Any(input => input.Path == bound.Path
                && input.Length == bound.Length && input.Sha256 == bound.Sha256)), "CAPTURE_JSON_TOOL_RECEIPT_MISSING");
        }
        var settings = entries.Where(entry => entry.Operation == "setting")
            .Select(entry => new BuildSetting(entry.Project!, entry.Name!, entry.Value!.Value))
            .Concat(jsonSettings)
            .DistinctBy(entry => Hash(Canonical(entry)))
            .OrderBy(entry => entry.Project, StringComparer.Ordinal).ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.Value.GetRawText(), StringComparer.Ordinal).ToArray();
        foreach (string project in settings.Where(setting => setting.Name == "MapSourceRoots.contract")
            .Select(setting => setting.Project).Distinct(StringComparer.Ordinal))
            Require(settings.Any(setting => setting.Project == project && setting.Name == "Csc.PathMap"),
                "CAPTURE_MAP_COMPILER_BINDING_MISSING");
        // Only actual build outputs are retained. Stale source obj/bin trees never entered Work.
        var outputs = Directory.EnumerateFiles(Work, "*", SearchOption.AllDirectories)
            .Where(path => path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "artifacts")
                && !File.Exists(Path.Combine(Frozen, "repo", Path.GetRelativePath(Work, path))))
            .Select(path => new FileIdentity("generated/repo/" + Path.GetRelativePath(Work, path).Replace('\\', '/'),
                new FileInfo(path).Length, FileHash(path), "output"))
            .OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray();
        foreach (var move in movedInputs)
            Require(outputs.Any(output => output.Path == move.DestinationPath && output.Sha256 == move.Destination.Sha256
                && output.Length == move.Destination.Length), "CAPTURE_MOVE_PAYLOAD_MISSING");
        if (sealOutputs)
            foreach (var output in outputs)
            {
                string destination = Path.Combine(Frozen, output.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (!File.Exists(destination)) File.Copy(Resolve(output.Path), destination);
            }
        foreach (var original in Directory.EnumerateFiles(Path.Combine(Frozen, "repo"), "*", SearchOption.AllDirectories))
        {
            string working = Path.Combine(Work, Path.GetRelativePath(Path.Combine(Frozen, "repo"), original));
            Require(File.Exists(working) && FileHash(original) == FileHash(working), "CAPTURE_ORIGINAL_MUTATED");
        }
        var projection = inputs.Where(input => input.Path.StartsWith("repo/", StringComparison.Ordinal)
            && !input.Path.StartsWith("repo/tests/", StringComparison.Ordinal)
            && !input.Path.Contains("/tests/", StringComparison.Ordinal)).ToArray();
        string sourceDigest = Hash(Canonical(inputs.Where(input => input.Kind is not ("recipe" or "tool")).ToArray()));
        string recipeDigest = Hash(Canonical(new
        {
            sdkVersion = SdkVersion,
            workloadVersion = WorkloadVersion,
            inputs = inputs.Where(input => input.Kind is "recipe" or "tool").ToArray(),
            settings,
            invocations,
            targets,
            movedInputs,
            retiredInputs
        }));
        return new("0.0.1", SdkVersion, WorkloadVersion, "repo/" + _configuration.Project, _configuration.Target,
            inputs, settings, outputs, projection, sourceDigest, recipeDigest, invocations, targets,
            _configuration.RepositoryNamespace, movedInputs, retiredInputs);
    }

    internal BuildInputs Verify(string? repositoryNames = null)
    {
        Unlinked(Path.Combine(_session, "capture.json"));
        Unlinked(Path.Combine(_session, "capture.sha256"));
        byte[] bytes = File.ReadAllBytes(Path.Combine(_session, "capture.json"));
        Require(File.ReadAllText(Path.Combine(_session, "capture.sha256")) == Hash(bytes), "CAPTURE_SEAL_MISMATCH");
        var capture = JsonSerializer.Deserialize<BuildInputs>(bytes, Program.Json)
            ?? throw new CaptureException("CAPTURE_RECORD_INVALID");
        ValidateRepositoryNamespace(capture.RepositoryNamespace);
        Require(capture.MovedInputs is not null && capture.RetiredInputs is not null
            && capture.MovedInputs.Length + capture.RetiredInputs.Length <= MaximumRepositoryNames, "CAPTURE_LIFECYCLE_LIMIT");
        Require(capture.RepositoryNamespace.SequenceEqual(_configuration.RepositoryNamespace, StringComparer.Ordinal),
            "CAPTURE_NAMESPACE_MISMATCH");
        if (repositoryNames is not null)
        {
            Unlinked(repositoryNames);
            using var namesStream = new FileStream(repositoryNames, FileMode.Open, FileAccess.Read, FileShare.Read);
            Require(namesStream.Length is > 0 and <= MaximumRepositoryNamesBytes,
                "CAPTURE_NAMESPACE_LIMIT");
            byte[] namesBytes = new byte[(int)namesStream.Length];
            namesStream.ReadExactly(namesBytes);
            Require(namesStream.ReadByte() == -1, "CAPTURE_NAMESPACE_LIMIT");
            using var document = JsonDocument.Parse(StrictUtf8.GetString(namesBytes));
            Require(document.RootElement.ValueKind == JsonValueKind.Array, "CAPTURE_NAMESPACE_INVALID");
            var names = document.RootElement.EnumerateArray().Take(MaximumRepositoryNames + 1)
                .Select(name => name.ValueKind == JsonValueKind.String ? name.GetString()!
                    : throw new CaptureException("CAPTURE_NAMESPACE_INVALID")).ToArray();
            ValidateRepositoryNamespace(names);
            Require(names.Where(name => RepositoryInput(name["repo/".Length..]))
                .SequenceEqual(capture.RepositoryNamespace, StringComparer.Ordinal), "CAPTURE_CANDIDATE_NAMESPACE_MISMATCH");
        }
        Require(Canonical(capture).AsSpan().SequenceEqual(bytes), "CAPTURE_RECORD_INVALID");
        Require(Canonical(Assemble(sealOutputs: false)).AsSpan().SequenceEqual(bytes), "CAPTURE_CLOSURE_MISMATCH");
        foreach (var input in capture.Inputs.Concat(capture.Outputs))
        {
            string path = input.Path.StartsWith("generated/", StringComparison.Ordinal)
                ? Path.Combine(Frozen, input.Path) : Resolve(input.Path);
            CheckFile(path, input);
        }
        return capture;
    }

    private static void CheckFile(string path, FileIdentity identity)
    {
        Unlinked(path);
        if (!File.Exists(path) || new FileInfo(path).Length != identity.Length || FileHash(path) != identity.Sha256)
        {
            Console.Error.WriteLine("CAPTURE_BYTES_CHANGED " + identity.Path);
            throw new CaptureException("CAPTURE_BYTES_CHANGED");
        }
    }

    internal static bool Inside(string path, string root) =>
        path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    internal static bool Relative(string path) => !Path.IsPathRooted(path) && path.Length > 0
        && !path.Any(char.IsControl)
        && path.Split('/', '\\').All(part => part.Length > 0 && part is not ("." or ".."))
        && !path.Contains(':', StringComparison.Ordinal);

    internal static void Unlinked(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "CAPTURE_UNSAFE_ALIAS");
    }

    private static void FreezeRepository(string source, string destination, HashSet<string> additions, string? fixtureAdmission)
    {
        Unlinked(source);
        string[] files;
        if (File.Exists(Path.Combine(source, "Configurator.slnx")))
        {
            Require(fixtureAdmission is null, "CAPTURE_SOURCE_ADMISSION_INVALID");
            string[] tracked = GitFiles(source, others: false)
                .Except(GitFiles(source, others: false, deleted: true), StringComparer.Ordinal).Where(RepositoryInput).ToArray();
            string[] untracked = GitFiles(source, others: true).Where(RepositoryInput).ToArray();
            Require(untracked.All(additions.Contains) && additions.All(path => untracked.Contains(path, StringComparer.Ordinal)
                || tracked.Contains(path, StringComparer.Ordinal)),
                "CAPTURE_UNTRACKED_SOURCE_UNADMITTED");
            files = tracked.Concat(untracked).ToArray();
        }
        else
        {
            // Standalone repros are explicitly declared synthetic input pools.
            // The supplied pool cannot omit an existing file: the live closure
            // is still discovered independently by the actual consuming graph.
            Require(fixtureAdmission is not null && additions.Count == 0, "CAPTURE_SOURCE_ADMISSION_MISSING");
            Unlinked(fixtureAdmission!);
            using var fixture = JsonDocument.Parse(File.ReadAllBytes(fixtureAdmission!));
            Require(fixture.RootElement.GetProperty("kind").GetString() == "synthetic-build", "CAPTURE_SOURCE_ADMISSION_INVALID");
            files = fixture.RootElement.GetProperty("files").EnumerateArray().Select(item => item.GetString()!).ToArray();
            Require(files.All(Relative) && files.Distinct(StringComparer.Ordinal).Count() == files.Length,
                "CAPTURE_SOURCE_ADMISSION_INVALID");
            string[] existing = EnumerateNames(source).Order(StringComparer.Ordinal).ToArray();
            Require(existing.SequenceEqual(files.Order(StringComparer.Ordinal)), "CAPTURE_SOURCE_ADMISSION_INCOMPLETE");
        }
        Directory.CreateDirectory(destination);
        foreach (string relative in files.Order(StringComparer.Ordinal))
        {
            Require(Relative(relative), "CAPTURE_SOURCE_ADMISSION_INVALID");
            Require(!relative.Split('/').Any(part => part is ".tmp" or "private" or ".private"
                || part.StartsWith(".env", StringComparison.Ordinal)), "CAPTURE_PRIVATE_SOURCE_UNADMITTED");
            if (relative.Split('/').Any(part => ExcludedDirectories.Contains(part, StringComparer.Ordinal))) continue;
            string path = Path.Combine(source, relative);
            Unlinked(path);
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(path, target);
            Require(FileHash(path) == FileHash(target), "CAPTURE_FREEZE_CHANGED");
        }
    }

    private static bool RepositoryInput(string path) => !path.Split('/').Any(part => part is ".tmp" or "private" or ".private"
        || part.StartsWith(".env", StringComparison.Ordinal))
        && !path.Split('/').Any(part => ExcludedDirectories.Contains(part, StringComparer.Ordinal))
        && (path is "global.json" or "Directory.Build.props" or "Directory.Build.targets"
            or "Directory.Packages.props" or "NuGet.Config" or "Configurator.slnx" or ".editorconfig"
            or "LICENSE" or "THIRD-PARTY-NOTICES.md"
            || RepositoryPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal)));

    private static string[] GitFiles(string root, bool others, bool deleted = false)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in new[] { "--no-pager", "ls-files", "-z", "--full-name" }) start.ArgumentList.Add(argument);
        if (others)
        {
            start.ArgumentList.Add("--others");
            start.ArgumentList.Add("--exclude-standard");
        }
        if (deleted) start.ArgumentList.Add("--deleted");
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new CaptureException("CAPTURE_SOURCE_ADMISSION_TIMEOUT");
        }
        _ = error.GetAwaiter().GetResult();
        Require(process.ExitCode == 0, "CAPTURE_SOURCE_ADMISSION_FAILED");
        return output.GetAwaiter().GetResult().Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static IEnumerable<string> EnumerateNames(string root)
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(root))
        {
            Unlinked(entry);
            if (!Directory.Exists(entry)) yield return Path.GetFileName(entry);
            else
                foreach (string child in EnumerateNames(entry)) yield return Path.GetFileName(entry) + "/" + child;
        }
    }

    private static string[] SnapshotRepositoryNamespace(string root)
    {
        Unlinked(root);
        var names = EnumerateNames(root).Take(MaximumRepositoryNames + 1)
            .Select(name => "repo/" + name).Order(StringComparer.Ordinal).ToArray();
        ValidateRepositoryNamespace(names);
        return names;
    }

    private static void ValidateRepositoryNamespace(string[]? names)
    {
        if (names is null) throw new CaptureException("CAPTURE_NAMESPACE_MISSING");
        Require(names.Length <= MaximumRepositoryNames, "CAPTURE_NAMESPACE_LIMIT");
        string? previous = null;
        foreach (string? name in names)
        {
            if (name is null || !name.StartsWith("repo/", StringComparison.Ordinal)
                || name.Length > MaximumRepositoryNameLength || name.Contains('\\')
                || !Relative(name["repo/".Length..]))
                throw new CaptureException("CAPTURE_NAMESPACE_INVALID");
            _ = StrictUtf8.GetByteCount(name);
            Require(previous is null || StringComparer.Ordinal.Compare(previous, name) < 0, "CAPTURE_NAMESPACE_INVALID");
            previous = name;
        }
    }

    private void FreezePackages()
    {
        string source = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages");
        string destination = Path.Combine(Frozen, "package");
        Directory.CreateDirectory(destination);
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(Path.Combine(Frozen, "repo"), "packages.lock.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            foreach (var framework in document.RootElement.GetProperty("dependencies").EnumerateObject())
                foreach (var package in framework.Value.EnumerateObject())
                    if (package.Value.TryGetProperty("resolved", out var version))
                        selected.Add(package.Name.ToLowerInvariant() + "/" + version.GetString()!.ToLowerInvariant());
        }
        // RID download packs are selected by the pinned net10 SDK rather than
        // framework-only development locks. Admit its installed 10.0.10 packs.
        if (_configuration.HasPackages)
        {
            // Workload 10.0.301.1 selects this reference pack during the observed
            // Browser graph, independently of the SDK's installed 10.0.10 pack.
            selected.Add("microsoft.netcore.app.ref/10.0.9");
            foreach (string directory in Directory.EnumerateDirectories(source))
            {
                string name = Path.GetFileName(directory);
                if (name.StartsWith("microsoft.netcore.app.runtime.", StringComparison.Ordinal)
                    || name.StartsWith("microsoft.netcore.app.host.", StringComparison.Ordinal)
                    || name.StartsWith("microsoft.aspnetcore.app.runtime.", StringComparison.Ordinal))
                    if (Directory.Exists(Path.Combine(directory, "10.0.10"))) selected.Add(name + "/10.0.10");
            }
        }
        foreach (string package in selected.Order(StringComparer.Ordinal))
        {
            Require(Relative(package), "CAPTURE_PACKAGE_ALIAS_INVALID");
            string directory = Path.Combine(source, package);
            Unlinked(directory);
            Clone(directory, Path.Combine(destination, package));
        }
    }

    private static void FreezeStore(string source, string destination)
    {
        Unlinked(source);
        var files = new List<string>();
        string[] projects = Directory.EnumerateFileSystemEntries(source).Take(1026).ToArray();
        Require(projects.Length <= 1025, "CAPTURE_STORE_LAYOUT_INVALID");
        foreach (string project in projects)
        {
            Unlinked(project);
            if (Path.GetFileName(project) == "README.md" && File.Exists(project)) continue;
            Require(Directory.Exists(project), "CAPTURE_STORE_LAYOUT_INVALID");
            string projectName = Path.GetFileName(project);
            Require(projectName.Length is > 0 and <= 64
                && projectName.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-'),
                "CAPTURE_STORE_LAYOUT_INVALID");
            string[] entries = Directory.EnumerateFileSystemEntries(project).Take(6).ToArray();
            Require(entries.Length <= 4 && entries.Count(path => Path.GetFileName(path)
                is "configurator.json" or "configurator.yaml" or "configurator.yml") == 1, "CAPTURE_STORE_LAYOUT_INVALID");
            foreach (string entry in entries)
            {
                Unlinked(entry);
                string name = Path.GetFileName(entry);
                if (name == "releases" && Directory.Exists(entry))
                {
                    string[] releases = Directory.EnumerateFileSystemEntries(entry).Take(65).ToArray();
                    Require(releases.Length <= 64, "CAPTURE_STORE_LAYOUT_INVALID");
                    foreach (string release in releases)
                    {
                        Unlinked(release);
                        Require(File.Exists(release) && Path.GetExtension(release) == ".json"
                            && long.TryParse(Path.GetFileNameWithoutExtension(release), out long revision) && revision > 0
                            && Path.GetFileName(release) == revision.ToString(CultureInfo.InvariantCulture) + ".json",
                            "CAPTURE_STORE_LAYOUT_INVALID");
                        files.Add(release);
                    }
                }
                else
                {
                    Require(File.Exists(entry) && name is "configurator.json" or "configurator.yaml" or "configurator.yml" or "curation.json",
                        "CAPTURE_STORE_LAYOUT_INVALID");
                    files.Add(entry);
                }
            }
        }
        Directory.CreateDirectory(destination);
        foreach (string file in files)
        {
            Require(new FileInfo(file).Length <= (Path.GetFileName(file) == "curation.json" ? 16 * 1024 : 4 * 1024 * 1024),
                "CAPTURE_STORE_LIMIT");
            string path = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(file, path);
            Require(FileHash(file) == FileHash(path), "CAPTURE_FREEZE_CHANGED");
        }
    }

    private static void Clone(string source, string destination)
    {
        Require(Directory.Exists(source), "CAPTURE_ROOT_MISSING");
        Directory.CreateDirectory(destination);
        if (OperatingSystem.IsLinux())
        {
            var start = new ProcessStartInfo("/usr/bin/cp") { UseShellExecute = false };
            foreach (string argument in new[] { "--recursive", "--preserve=mode,timestamps", "--reflink=auto", source + "/.", destination })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            process.WaitForExit();
            Require(process.ExitCode == 0, "CAPTURE_FREEZE_FAILED");
        }
        else
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(source))
            {
                Unlinked(entry);
                string target = Path.Combine(destination, Path.GetFileName(entry));
                if (Directory.Exists(entry)) Clone(entry, target);
                else File.Copy(entry, target);
            }
        }
    }

    private static bool LifecycleAlias(string path) => path is not null && path.Length <= MaximumRepositoryNameLength
        && !path.Contains('\\') && Relative(path);

    private void ValidateMove(CapturedMove move, CapturedInvocation[] invocations)
    {
        Require(LifecycleAlias(move.SourcePath) && LifecycleAlias(move.DestinationPath)
            && move.Invocation is { Length: > 0 and <= 512 }
            && move.Recipe == OriginalRecipe("eng/configurator/Configurator.Release.proj"), "CAPTURE_MOVE_INVALID");
        var invocation = invocations.SingleOrDefault(item => item.Id == move.Invocation)
            ?? throw new CaptureException("CAPTURE_MOVE_INVOCATION_INVALID");
        Require(invocation.Succeeded && invocation.Task == "Move" && invocation.Target == "PublishConfigurator"
            && invocation.Recipe == move.Recipe
            && invocation.Assembly.Path == "sdk/sdk/" + SdkVersion + "/Microsoft.Build.Tasks.Core.dll",
            "CAPTURE_MOVE_INVOCATION_INVALID");
        string? sourcePrefix = null;
        string? destinationPrefix = null;
        foreach (var pair in new[]
        {
            (Surface: "cli", Assembly: "Configurator.Cli", Executable: "islamu-configurator"),
            (Surface: "terminal", Assembly: "Configurator.Terminal", Executable: "islamu-configurator-terminal"),
            (Surface: "desktop", Assembly: "Configurator.Desktop", Executable: "islamu-configurator-desktop")
        })
        {
            string directory = "generated/repo/artifacts/configurator/" + pair.Surface + "/";
            if (!move.SourcePath.StartsWith(directory, StringComparison.Ordinal)) continue;
            string tail = move.SourcePath[directory.Length..];
            int separator = tail.IndexOf('/');
            Require(separator is > 0 and <= 128 && tail[..separator].All(character =>
                char.IsAsciiLetterOrDigit(character) || character == '-'), "CAPTURE_MOVE_PAIRING_INVALID");
            string extension = tail[..separator].StartsWith("win-", StringComparison.Ordinal) ? ".exe" : "";
            sourcePrefix = directory + tail[..separator] + "/" + pair.Assembly + extension;
            destinationPrefix = directory + tail[..separator] + "/" + pair.Executable + extension;
            break;
        }
        Require(move.SourcePath == sourcePrefix && move.DestinationPath == destinationPrefix
            && move.Source.Path == "generated/versions/" + move.Source.Sha256 + "/" + move.SourcePath["generated/".Length..]
            && move.Destination.Path == "generated/versions/" + move.Destination.Sha256 + "/" + move.DestinationPath["generated/".Length..]
            && move.Source.Sha256 == move.Destination.Sha256 && move.Source.Length == move.Destination.Length,
            "CAPTURE_MOVE_PAIRING_INVALID");
        foreach (var pair in new[] { (Name: "SourceFiles", Path: move.SourcePath), (Name: "DestinationFiles", Path: move.DestinationPath) })
        {
            var parameter = invocation.Parameters.SingleOrDefault(item => item.Name == pair.Name)
                ?? throw new CaptureException("CAPTURE_MOVE_PAIRING_INVALID");
            Require(parameter.Type == "Microsoft.Build.Framework.ITaskItem[]"
                && parameter.Value.TryGetProperty("paths", out var paths) && paths.ValueKind == JsonValueKind.Array
                && paths.GetArrayLength() == 1 && paths[0].GetString() == pair.Path, "CAPTURE_MOVE_PAIRING_INVALID");
            if (pair.Name == "SourceFiles")
                Require(parameter.Inputs.Contains(move.Source), "CAPTURE_MOVE_VERSION_INVALID");
        }
        CheckFile(Path.Combine(Frozen, move.Source.Path), move.Source);
        CheckFile(Path.Combine(Frozen, move.Destination.Path), move.Destination);
        Require(LifecycleAbsent(Resolve(move.SourcePath)), "CAPTURE_MOVE_SOURCE_RECREATED");
        CheckFile(Resolve(move.DestinationPath), move.Destination);
    }

    private void ValidateRetirement(CapturedRetirement retirement, CapturedInvocation[] invocations, CapturedTarget[] targets)
    {
        Require(Guid.TryParseExact(retirement.Id, "N", out _) && retirement.Id == retirement.QueryGraph
            && retirement.GeneratorInvocation is { Length: > 0 and <= 512 }
            && retirement.Surface is "cli" or "terminal" or "desktop"
            && LifecycleAlias(retirement.OutputPath) && retirement.OutputPath.StartsWith("generated/tmp/configurator-contract-", StringComparison.Ordinal)
            && retirement.Recipe == OriginalRecipe("eng/configurator/GenerateConfiguratorRatchets.cs"),
            "CAPTURE_RETIREMENT_INVALID");
        var query = ReadLifecycle<ContractQueryReceipt>("queries", retirement.QueryGraph);
        var generator = ReadLifecycle<GeneratorReceipt>("generators", query.GeneratorId);
        Require(query.Succeeded && query.QueryGraph == retirement.QueryGraph
            && query.GeneratorInvocation == retirement.GeneratorInvocation && query.Surface == retirement.Surface
            && query.OutputPath == retirement.OutputPath
            && query.CaptureRoot is { Length: > 0 and <= 4096 } && Path.IsPathFullyQualified(query.CaptureRoot)
            && Path.GetFullPath(query.CaptureRoot) == query.CaptureRoot
            && query.PhysicalOutput == Path.Combine(query.CaptureRoot, "private", retirement.OutputPath["generated/".Length..])
            && query.Source == retirement.Source && query.Recipe == retirement.Recipe
            && generator.Id == query.GeneratorId && generator.Invocation == retirement.GeneratorInvocation
            && generator.Succeeded == true && generator.Recipe == retirement.Recipe
            && ReadLifecycle<CapturedRetirement>("retired", retirement.Id) == retirement
            && retirement.Source.Path == "generated/versions/" + retirement.Source.Sha256 + "/" + retirement.OutputPath["generated/".Length..],
            "CAPTURE_RETIREMENT_INVALID");
        var invocation = invocations.SingleOrDefault(item => item.Id == retirement.GeneratorInvocation)
            ?? throw new CaptureException("CAPTURE_RETIREMENT_INVOCATION_INVALID");
        Require(invocation.Succeeded && invocation.Task == "BuildCaptureTask"
            && invocation.Parameters.Any(parameter => parameter.Name == "Operation" && parameter.Type == "System.String"
                && parameter.Value.TryGetProperty("sha256", out var hash)
                && hash.GetString() == Hash(Canonical<object>("ratchets")))
            && targets.Any(target => target.Id.StartsWith(retirement.QueryGraph + "/", StringComparison.Ordinal)
                && target.Project == "repo/eng/configurator/Configurator.Release.proj"
                && target.Target == "WriteConfiguratorReleaseContract" && target.Outcome == "succeeded"),
            "CAPTURE_RETIREMENT_INVOCATION_INVALID");
        CheckFile(Resolve(generator.Executable.Path), generator.Executable);
        CheckFile(Path.Combine(Frozen, retirement.Source.Path), retirement.Source);
        Require(LifecycleAbsent(Resolve(retirement.OutputPath)), "CAPTURE_RETIRED_INPUT_RECREATED");
    }

    internal static bool LifecycleAbsent(string path) =>
        !File.Exists(path) && !Directory.Exists(path) && new FileInfo(path).LinkTarget is null;

    private string LifecyclePath(string group, string id)
    {
        Require(Guid.TryParseExact(id, "N", out _), "CAPTURE_LIFECYCLE_ID_INVALID");
        return Path.Combine(_session, "private/lifecycles", group, id + ".json");
    }

    private void WriteLifecycle<T>(string group, string id, T value)
    {
        string path = LifecyclePath(group, id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Canonical(value));
    }

    private T ReadLifecycle<T>(string group, string id)
    {
        string path = LifecyclePath(group, id);
        Unlinked(path);
        Require(new FileInfo(path).Length is > 0 and <= 1_048_576, "CAPTURE_LIFECYCLE_RECEIPT_INVALID");
        return JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Program.Json)
            ?? throw new CaptureException("CAPTURE_LIFECYCLE_RECEIPT_INVALID");
    }

    private FileIdentity OriginalRecipe(string relative)
    {
        string path = Path.Combine(Frozen, "repo", relative);
        Unlinked(path);
        return new("repo/" + relative, new FileInfo(path).Length, FileHash(path), "recipe");
    }

    private ContractQueryReceipt QueryForOutput(string path)
    {
        string generatorId = Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_GENERATOR_ID")
            ?? throw new CaptureException("CAPTURE_QUERY_UNBOUND");
        string directory = Path.Combine(_session, "private/lifecycles/queries");
        Require(Directory.Exists(directory), "CAPTURE_QUERY_UNBOUND");
        var queries = Directory.EnumerateFiles(directory, "*.json").Take(MaximumRepositoryNames + 1)
            .Select(file => ReadLifecycle<ContractQueryReceipt>("queries", Path.GetFileNameWithoutExtension(file)))
            .Where(query => query.GeneratorId == generatorId && query.PhysicalOutput == Path.GetFullPath(path)).ToArray();
        Require(queries.Length == 1, "CAPTURE_QUERY_UNBOUND");
        return queries[0];
    }

    internal void BeginContractQuery(ProcessStartInfo start, string surface, string output)
    {
        string generatorId = Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_GENERATOR_ID")
            ?? throw new CaptureException("CAPTURE_GENERATOR_UNBOUND");
        var generator = ReadLifecycle<GeneratorReceipt>("generators", generatorId);
        Require(generator.Succeeded is null
            && generator.Invocation == Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_PARENT_INVOCATION")
            && generator.Recipe == OriginalRecipe("eng/configurator/GenerateConfiguratorRatchets.cs"),
            "CAPTURE_GENERATOR_UNBOUND");
        string name = Path.GetFileName(output);
        Require(name.StartsWith("configurator-contract-", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal)
            && Guid.TryParseExact(name["configurator-contract-".Length..^5], "N", out _),
            "CAPTURE_QUERY_OUTPUT_INVALID");
        string graph = start.Environment["CONFIGURATOR_CAPTURE_GRAPH"]!;
        string alias = Logical(output);
        Require(alias.StartsWith("generated/tmp/", StringComparison.Ordinal) && LifecycleAbsent(output),
            "CAPTURE_QUERY_OUTPUT_INVALID");
        WriteLifecycle("queries", graph, new ContractQueryReceipt(graph, generatorId, generator.Invocation, surface,
            alias, output, generator.Recipe, false, null, _session));
    }

    internal void ContractQueryOutcome(string project, string target, IReadOnlyDictionary<string, string?> properties, bool? succeeded)
    {
        string path = LifecyclePath("queries", CurrentGraph);
        if (!File.Exists(path)) return;
        var query = ReadLifecycle<ContractQueryReceipt>("queries", CurrentGraph);
        Require(project == Path.Combine(Work, "eng/configurator/Configurator.Release.proj")
            && target == "WriteConfiguratorReleaseContract"
            && properties.GetValueOrDefault("ConfiguratorSurface") == query.Surface
            && properties.GetValueOrDefault("ConfiguratorContractOutput") == query.PhysicalOutput,
            "CAPTURE_QUERY_INVALID");
        if (succeeded is not null)
            WriteLifecycle("queries", CurrentGraph, query with { Succeeded = succeeded.Value });
    }

    /// <summary>Checks and performs retirement only for the admitted successful ratchet query.</summary>
    public static void RetireContractOutput(string path)
    {
        string root = Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_SESSION")
            ?? throw new CaptureException("CAPTURE_QUERY_UNBOUND");
        var session = Open(root);
        path = Path.GetFullPath(path);
        var query = session.QueryForOutput(path);
        var generator = session.ReadLifecycle<GeneratorReceipt>("generators", query.GeneratorId);
        Require(query.Succeeded && query.Source is not null && query.CaptureRoot == session._session
            && query.Recipe == session.OriginalRecipe("eng/configurator/GenerateConfiguratorRatchets.cs")
            && query.GeneratorInvocation == generator.Invocation && query.Recipe == generator.Recipe
            && generator.Succeeded is null
            && query.GeneratorInvocation == Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_PARENT_INVOCATION")
            && query.OutputPath == session.Logical(path)
            && query.OutputPath.StartsWith("generated/tmp/", StringComparison.Ordinal)
            && query.Surface is "cli" or "terminal" or "desktop", "CAPTURE_RETIREMENT_INVALID");
        FileIdentity source = query.Source!;
        Require(source.Path == "generated/versions/" + source.Sha256 + "/" + query.OutputPath["generated/".Length..],
            "CAPTURE_RETIREMENT_INVALID");
        CheckFile(path, source);
        CheckFile(Path.Combine(session.Frozen, source.Path), source);
        string receiptPath = session.LifecyclePath("retired", query.QueryGraph);
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        using (new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        File.Delete(path);
        Require(LifecycleAbsent(path), "CAPTURE_RETIREMENT_FAILED");
        var retirement = new CapturedRetirement(query.QueryGraph, query.QueryGraph, query.GeneratorInvocation,
            query.Surface, query.OutputPath, source, query.Recipe);
        session.WriteLifecycle("retired", query.QueryGraph, retirement);
        session.InvocationEvent("retired", retirement.Id, JsonSerializer.SerializeToElement(retirement, Program.Json));
    }

    internal void RecordMove(CapturedInvocation invocation, IReadOnlyList<string> sources, IReadOnlyList<string> destinations)
    {
        Require(invocation.Succeeded && sources.Count == 1 && destinations.Count == 1, "CAPTURE_MOVE_INVALID");
        string sourcePath = "generated/" + Logical(sources[0]);
        string destinationPath = "generated/" + Logical(destinations[0]);
        var source = invocation.Parameters.Single(parameter => parameter.Name == "SourceFiles").Inputs.Single(
            identity => identity.Path.StartsWith("generated/versions/", StringComparison.Ordinal)
                && identity.Path.EndsWith("/" + sourcePath["generated/".Length..], StringComparison.Ordinal));
        CheckFile(Path.Combine(Frozen, source.Path), source);
        Require(LifecycleAbsent(sources[0]), "CAPTURE_MOVE_INVALID");
        var destination = Observe(destinations[0], "generated");
        Require(destination.Path.StartsWith("generated/versions/", StringComparison.Ordinal)
            && destination.Sha256 == source.Sha256 && destination.Length == source.Length, "CAPTURE_MOVE_INVALID");
        var move = new CapturedMove(invocation.Id, OriginalRecipe("eng/configurator/Configurator.Release.proj"),
            sourcePath, destinationPath, source, destination);
        InvocationEvent("moved", invocation.Id, JsonSerializer.SerializeToElement(move, Program.Json));
    }

    /// <summary>Called by the admitted first-party generators immediately before using a file.</summary>
    public static void Consume(string path, string kind = "generator")
    {
        string? session = Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_SESSION");
        if (session is not null)
        {
            var capture = Open(session);
            var source = capture.Observe(path, kind);
            if (Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_GENERATOR_ID") is not null
                && Path.GetFileName(path).StartsWith("configurator-contract-", StringComparison.Ordinal)
                && Inside(Path.GetFullPath(path), Path.Combine(session, "private/tmp")))
            {
                var query = capture.QueryForOutput(path);
                Require(query.Succeeded && query.Source is null, "CAPTURE_QUERY_CONSUMPTION_INVALID");
                capture.WriteLifecycle("queries", query.QueryGraph, query with { Source = source });
            }
        }
    }

    /// <summary>Attaches an admitted generator's native MSBuild query to its parent graph.</summary>
    public static void Instrument(ProcessStartInfo start)
    {
        string? root = Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_SESSION");
        if (root is null) return;
        Require(start.ArgumentList is ["msbuild", "eng/configurator/Configurator.Release.proj", "-nologo", "-verbosity:quiet",
            "-target:WriteConfiguratorReleaseContract", var surface, var output]
            && surface.StartsWith("-property:ConfiguratorSurface=", StringComparison.Ordinal)
            && surface["-property:ConfiguratorSurface=".Length..] is "cli" or "terminal" or "desktop"
            && output.StartsWith("-property:ConfiguratorContractOutput=", StringComparison.Ordinal),
            "CAPTURE_SUBPROCESS_UNADMITTED");
        var session = Open(root);
        string contractOutput = Path.GetFullPath(start.ArgumentList[6]["-property:ConfiguratorContractOutput=".Length..]);
        Require(Inside(contractOutput, Path.Combine(root, "private/tmp")), "CAPTURE_OUTPUT_ROOT_UNADMITTED");
        string surfaceProperty = start.ArgumentList[5]["-property:".Length..];
        start.ArgumentList.Clear();
        start.FileName = session.Dotnet;
        foreach (string argument in new[]
        {
            session.Tool, "--host", root, Path.Combine(session.Work, "eng/configurator/Configurator.Release.proj"),
            "WriteConfiguratorReleaseContract", surfaceProperty, "ConfiguratorContractOutput=" + contractOutput,
            "UseSharedCompilation=false", "ConfiguratorCaptureSession=" + root, "ConfiguratorCaptureTool=" + session.Tool,
            "ConfiguratorCaptureOverrides=" + Path.Combine(session.Work, "artifacts/capture-overrides.targets"),
            "CustomAfterMicrosoftCommonProps=" + Path.Combine(session.Work, "artifacts/capture-early.props"),
            "CustomBeforeMicrosoftCommonTargets=" + Path.Combine(session.Work, "artifacts/capture-overrides.targets"),
            "BaseIntermediateOutputPath=obj/capture/" + Guid.NewGuid().ToString("N") + "/",
            "BaseOutputPath=bin/capture/" + Guid.NewGuid().ToString("N") + "/"
        }) start.ArgumentList.Add(argument);
        session.ExpectGraph(start);
        session.BeginContractQuery(start, surfaceProperty["ConfiguratorSurface=".Length..], contractOutput);
    }

    /// <summary>Queries runtime-pack metadata through an instrumented native target rather than the CLI query shortcut.</summary>
    public static string? FrameworkInventory(string project, string runtime)
    {
        string? root = Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_SESSION");
        if (root is null) return null;
        var session = Open(root);
        string output = Path.Combine(root, "private/tmp/frameworks-" + Guid.NewGuid().ToString("N") + ".json");
        int exit = session.Build(project, "WriteConfiguratorCapturedFrameworks",
            ["RuntimeIdentifier=" + runtime, "SelfContained=true", "ConfiguratorFrameworkInventory=" + output], restore: false);
        Require(exit == 0, "CAPTURE_FRAMEWORK_QUERY_FAILED");
        session.Observe(output, "generator");
        return File.ReadAllText(output);
    }

    internal int RunCheck(string owner, string output, string[] options)
    {
        BuildInputs capture = Verify();
        Require(owner.StartsWith("Configurator.", StringComparison.Ordinal)
            && Owners.Contains(owner["Configurator.".Length..], StringComparer.Ordinal), "CAPTURE_CHECK_OWNER_INVALID");
        string suffix = "/" + owner + ".Tests.dll";
        var executables = capture.Outputs.Where(file => file.Path.EndsWith(suffix, StringComparison.Ordinal)
            && file.Path.Contains("/bin/", StringComparison.Ordinal) && !file.Path.Contains("/obj/", StringComparison.Ordinal)).ToArray();
        Require(executables.Length == 1, "CAPTURE_CHECK_EXECUTABLE_MISSING");
        string project = owner switch
        {
            "Configurator.AgentWorkflow" => "eng/agent-workflow/tests/" + owner + ".Tests/" + owner + ".Tests.csproj",
            "Configurator.ReleaseEngineering" => "eng/release/tests/" + owner + ".Tests/" + owner + ".Tests.csproj",
            _ => "tests/" + owner + ".Tests/" + owner + ".Tests.csproj"
        };
        Require(capture.Project == "repo/" + project && capture.Target == "Build", "CAPTURE_CHECK_PROFILE_MISMATCH");
        Require(options.Length == 0 || options is ["--filter", _], "CAPTURE_CHECK_ARGUMENTS_INVALID");
        string scope = options.Length == 0 ? "full-owner" : "focused";
        output = Path.GetFullPath(output);
        Require(!Directory.Exists(output), "CAPTURE_CHECK_OUTPUT_EXISTS");
        Directory.CreateDirectory(output);
        string raw = Path.Combine(_session, "private/check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raw);
        string executionRoot = Path.Combine(raw, "repo");
        Clone(Path.Combine(Frozen, "repo"), executionRoot);
        var staged = new Dictionary<string, FileIdentity>(StringComparer.Ordinal);
        foreach (var file in capture.Outputs.Where(file => file.Path.Contains("/bin/", StringComparison.Ordinal)
            && !file.Path.Contains("/obj/", StringComparison.Ordinal)))
        {
            string relative = ExecutionPath(file.Path);
            if (staged.TryGetValue(relative, out var previous))
                Require(previous.Sha256 == file.Sha256, "CAPTURE_CHECK_OUTPUT_AMBIGUOUS");
            else
            {
                string target = Path.Combine(executionRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(Frozen, file.Path), target);
                CheckFile(target, file);
                staged.Add(relative, file);
            }
        }
        foreach (var file in capture.Inputs.Where(file => file.Path.StartsWith("capture-tool/", StringComparison.Ordinal)))
        {
            string relative = "eng/configurator/Configurator.BuildCapture/bin/Release/net10.0/" + Path.GetFileName(file.Path);
            string target = Path.Combine(executionRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Resolve(file.Path), target);
            CheckFile(target, file);
            staged.Add(relative, file);
        }
        var executable = executables[0];
        string directory = executable.Path[..(executable.Path.LastIndexOf('/') + 1)];
        var dependencies = capture.Outputs.Where(file => file.Path.StartsWith(directory, StringComparison.Ordinal)
            && file.Path != executable.Path)
            .Concat(capture.Inputs.Where(file => file.Path.StartsWith("sdk/shared/", StringComparison.Ordinal)
                || file.Path.StartsWith("sdk/host/", StringComparison.Ordinal) || file.Path.StartsWith("capture-tool/", StringComparison.Ordinal)
                || file.Path == "sdk/dotnet"))
            .OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
        var arguments = new List<string> { Path.Combine(executionRoot, ExecutionPath(executable.Path)), "--minimum-expected-tests", "1",
            "--report-trx", "--report-trx-filename", "results.trx", "--results-directory", raw };
        arguments.AddRange(options.Length == 0 ? [] : new[] { "--treenode-filter", options[1] });
        int exit = Execute(Dotnet, arguments, executionRoot, "check");
        string preparer = Path.Combine(executionRoot, "artifacts/capture-tools/evidence/bin/Release/net10.0/Configurator.TestEvidence.dll");
        Require(staged.ContainsKey("artifacts/capture-tools/evidence/bin/Release/net10.0/Configurator.TestEvidence.dll"),
            "CAPTURE_CHECK_PREPARER_MISSING");
        int prepared = Execute(Dotnet, [preparer,
            "--owner", owner, "--test-runner-rid", RuntimeInformation.RuntimeIdentifier,
            "--run", "captured", "--input", raw, "--output", output,
            "--exit-code", exit.ToString(CultureInfo.InvariantCulture), "--coverage", "none"], executionRoot, "safe-report");
        string report = Path.Combine(output, owner + "-" + RuntimeInformation.RuntimeIdentifier + "-captured.json");
        Require(File.Exists(report), "CAPTURE_CHECK_REPORT_MISSING");
        using var document = JsonDocument.Parse(File.ReadAllBytes(report));
        var tests = document.RootElement.GetProperty("Tests");
        long passed = tests.ValueKind == JsonValueKind.Null ? 0 : tests.GetProperty("Passed").GetInt64();
        long failed = tests.ValueKind == JsonValueKind.Null ? 0 : tests.GetProperty("Failed").GetInt64();
        long skipped = tests.ValueKind == JsonValueKind.Null ? 0 : tests.GetProperty("Skipped").GetInt64();
        foreach ((string relative, FileIdentity identity) in staged)
            CheckFile(Path.Combine(executionRoot, relative), identity);
        _ = Verify();
        var execution = new CheckExecution("0.0.1", FileHash(report), owner + ".full-owner.v1", owner, scope,
            Hash(Canonical(capture)), executable, dependencies, exit, passed, failed, skipped,
            exit == 0 && prepared == 0 && passed > 0 && failed == 0 && skipped == 0 ? "passed" : "failed",
            RuntimeInformation.RuntimeIdentifier);
        File.WriteAllBytes(Path.Combine(output, "execution.json"), Canonical(execution));
        string binding = Path.Combine(_session, "private/checks");
        Directory.CreateDirectory(binding);
        File.WriteAllBytes(Path.Combine(binding, Hash(Canonical(execution)) + ".json"), Canonical(execution));
        return execution.Outcome == "passed" ? 0 : exit == 0 ? 1 : exit;
    }

    internal void VerifyCheck(string executionPath, string reportPath)
    {
        var capture = Verify();
        byte[] bytes = File.ReadAllBytes(executionPath);
        var execution = JsonSerializer.Deserialize<CheckExecution>(bytes, Program.Json)
            ?? throw new CaptureException("CAPTURE_RECORD_INVALID");
        string binding = Path.Combine(_session, "private/checks", Hash(bytes) + ".json");
        Require(File.Exists(binding) && File.ReadAllBytes(binding).AsSpan().SequenceEqual(bytes), "CAPTURE_CHECK_UNBOUND");
        Require(Canonical(execution).AsSpan().SequenceEqual(bytes) && execution.SchemaVersion == "0.0.1"
            && execution.TestCaptureDigest == Hash(Canonical(capture))
            && execution.SafeReportHash == FileHash(reportPath), "CAPTURE_CHECK_BINDING_INVALID");
        Require(execution.Scope == "full-owner" && execution.Profile == execution.Owner + ".full-owner.v1"
            && execution.Outcome == "passed" && execution.CommandExitCode == 0
            && execution.Passed > 0 && execution.Failed == 0 && execution.Skipped == 0, "CAPTURE_CHECK_INCOMPLETE");
        Require(capture.Outputs.Contains(execution.Executable), "CAPTURE_CHECK_EXECUTABLE_INVALID");
        string directory = execution.Executable.Path[..(execution.Executable.Path.LastIndexOf('/') + 1)];
        var dependencies = capture.Outputs.Where(file => file.Path.StartsWith(directory, StringComparison.Ordinal)
            && file.Path != execution.Executable.Path)
            .Concat(capture.Inputs.Where(file => file.Path.StartsWith("sdk/shared/", StringComparison.Ordinal)
                || file.Path.StartsWith("sdk/host/", StringComparison.Ordinal) || file.Path.StartsWith("capture-tool/", StringComparison.Ordinal)
                || file.Path == "sdk/dotnet"))
            .OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
        Require(Canonical(dependencies).AsSpan().SequenceEqual(Canonical(execution.Dependencies)), "CAPTURE_CHECK_DEPENDENCIES_INVALID");
        using var report = JsonDocument.Parse(File.ReadAllBytes(reportPath));
        var record = report.RootElement;
        var tests = record.GetProperty("Tests");
        Require(record.GetProperty("Owner").GetString() == execution.Owner
            && record.GetProperty("TestRunnerRid").GetString() == execution.RunnerRid
            && record.GetProperty("CommandExitCode").GetInt32() == execution.CommandExitCode
            && record.GetProperty("EvidenceStatus").GetString() == "validated"
            && record.GetProperty("TestStatus").GetString() == "passed"
            && tests.GetProperty("Passed").GetInt64() == execution.Passed
            && tests.GetProperty("Failed").GetInt64() == 0 && tests.GetProperty("Skipped").GetInt64() == 0,
            "CAPTURE_CHECK_REPORT_INVALID");
    }

    private static string ExecutionPath(string logical)
    {
        Require(logical.StartsWith("generated/repo/", StringComparison.Ordinal), "CAPTURE_CHECK_OUTPUT_INVALID");
        string relative = logical["generated/repo/".Length..];
        int marker = relative.IndexOf("/bin/capture/", StringComparison.Ordinal);
        if (marker < 0) return relative;
        int end = relative.IndexOf('/', marker + "/bin/capture/".Length);
        Require(end >= 0, "CAPTURE_CHECK_OUTPUT_INVALID");
        return relative[..marker] + "/bin" + relative[end..];
    }
}
