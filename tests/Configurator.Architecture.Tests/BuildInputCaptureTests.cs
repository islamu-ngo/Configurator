using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Configurator.Architecture.Tests;

[NotInParallel]
public sealed class BuildInputCaptureTests
{
    private static string Tool => Environment.GetEnvironmentVariable("CONFIGURATOR_BUILD_CAPTURE_TEST_TOOL") ?? Path.Combine(ReleaseTestPaths.Root,
        "eng/configurator/Configurator.BuildCapture/bin/Release/net10.0/Configurator.BuildCapture.dll");
    private static readonly string[] NamespaceNames =
        ["repo/src/probe/input.txt", "repo/src/probe/main.proj", "repo/src/probe/unused.txt"];

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PinnedJsonFactoryCapturesRuntimeInputAndUnsetOutputs(bool populated)
    {
        using var fixture = new Fixture();
        fixture.Write("input.json", populated ? """
            {"items":{"EmccProperties":[{"Identity":"synthetic-property","Value":"synthetic-value"}],
            "EmccDefaultExportedFunctions":[{"Identity":"synthetic-function"}],
            "EmccDefaultExportedRuntimeMethods":[{"Identity":"synthetic-method"}],
            "PropertiesThatTriggerRelinking":[{"Identity":"synthetic-trigger"}],
            "WasmOptConfigurationFlags":[{"Identity":"synthetic-flag"}]}}
            """ : """{"items":{}}""");
        fixture.Write("main.proj", """
            <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <Target Name="CaptureJson">
                <ReadWasmProps JsonFilePath="input.json">
                  <Output TaskParameter="EmccProperties" ItemName="Observed" />
                </ReadWasmProps>
                <WriteLinesToFile File="result.txt" Lines="@(Observed)" Overwrite="true" />
              </Target>
            </Project>
            """);
        var result = await fixture.Capture("main.proj", "CaptureJson");
        if (result.ExitCode != 0)
        {
            string rejected = Path.Combine(fixture.Session, "private/rejections");
            if (Directory.Exists(rejected))
                foreach (string path in Directory.EnumerateFiles(rejected, "*.json"))
                {
                    var diagnostic = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
                    if (diagnostic["kind"]!.GetValue<string>() == "started")
                        Console.WriteLine("JSON_FACTORY_DISPATCH " + diagnostic.ToJsonString());
                }
        }
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        var capture = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!;
        var invocation = capture["invocations"]!.AsArray().Single(value => value!["task"]!.GetValue<string>() == "ReadWasmProps")!;
        var parameters = invocation["parameters"]!.AsArray();
        await Assert.That(parameters.Count).IsEqualTo(6);
        var input = parameters.Single(value => value!["name"]!.GetValue<string>() == "JsonFilePath")!;
        await Assert.That(input["type"]!.GetValue<string>()).IsEqualTo("System.String");
        await Assert.That(input["inputs"]!.AsArray().Single()!["path"]!.GetValue<string>()).IsEqualTo("repo/input.json");
        await Assert.That(invocation["assembly"]!["path"]!.GetValue<string>()).IsEqualTo("capture-tool/Configurator.BuildCapture.dll");
        await Assert.That(parameters.Where(value => value!["name"]!.GetValue<string>() != "JsonFilePath")
            .All(value => value!["value"] is null && value["inputs"]!.AsArray().Count == 0)).IsTrue();
        var setting = capture["settings"]!.AsArray().Single(value =>
            value!["name"]!.GetValue<string>() == "ReadWasmProps." + invocation["id"]!.GetValue<string>())!["value"]!;
        await Assert.That(setting["outcome"]!["outputs"]!.AsArray().Count).IsEqualTo(5);
        await Assert.That(setting["barrier"]!["tools"]!.AsArray().Any(tool =>
            tool!["path"]!.GetValue<string>() == "sdk/packs/Microsoft.NET.Runtime.MonoTargets.Sdk/10.0.9/tasks/net10.0/MonoTargetsTasks.dll"
            && tool["sha256"]!.GetValue<string>() == "cb321810c0d58ee645113d3677a10635814dd02536086ab7815bd16822e8d8ce")).IsTrue();
        await Assert.That(setting["barrier"]!["tools"]!.AsArray().Any(tool =>
            tool!["path"]!.GetValue<string>().StartsWith("capture-tool/host-runtime/", StringComparison.Ordinal)
            && tool["path"]!.GetValue<string>().Contains("libicu", StringComparison.Ordinal))).IsTrue();
        if (populated)
            await Assert.That((await File.ReadAllTextAsync(Path.Combine(fixture.Session, "work/repo/result.txt"))).Trim())
                .IsEqualTo("synthetic-property");
        var verified = await fixture.VerifyWithoutSdk("--verify", fixture.Session);
        await Assert.That(verified.ExitCode).IsEqualTo(0).Because(verified.Output);
        if (populated)
        {
            string events = Path.Combine(fixture.Session, "private/events");
            string barrierPath = Directory.EnumerateFiles(events, "*.json").Single(path =>
                JsonNode.Parse(File.ReadAllText(path))!["operation"]!.GetValue<string>() == "json-factory-barrier");
            string outcomePath = Directory.EnumerateFiles(events, "*.json").Single(path =>
                JsonNode.Parse(File.ReadAllText(path))!["operation"]!.GetValue<string>() == "json-factory-outcome");
            string barrierText = await File.ReadAllTextAsync(barrierPath);
            string outcomeText = await File.ReadAllTextAsync(outcomePath);
            foreach (string attack in new[] { "missing-barrier", "missing-outcome", "duplicate-outcome", "token", "input", "shape", "tool" })
            {
                var barrier = JsonNode.Parse(barrierText)!;
                var outcome = JsonNode.Parse(outcomeText)!;
                string extra = Path.Combine(events, "extra.json");
                switch (attack)
                {
                    case "missing-barrier": File.Delete(barrierPath); break;
                    case "missing-outcome": File.Delete(outcomePath); break;
                    case "duplicate-outcome": await File.WriteAllTextAsync(extra, outcomeText); break;
                    case "token": outcome["value"]!["token"] = Guid.NewGuid().ToString("N"); break;
                    case "input": barrier["value"]!["input"]!["sha256"] = new string('0', 64); break;
                    case "shape": outcome["value"]!["outputs"]![0]!["type"] = "System.String"; break;
                    case "tool":
                        barrier["value"]!["tools"]!.AsArray().Single(tool =>
                        tool!["path"]!.GetValue<string>().EndsWith("/MonoTargetsTasks.dll", StringComparison.Ordinal))!["sha256"] = new string('0', 64); break;
                }
                if (attack != "missing-barrier") await File.WriteAllTextAsync(barrierPath, barrier.ToJsonString());
                if (attack != "missing-outcome") await File.WriteAllTextAsync(outcomePath, outcome.ToJsonString());
                var rejected = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
                await Assert.That(rejected.ExitCode).IsNotEqualTo(0).Because(attack);
                await Assert.That(rejected.Output).Contains("CAPTURE_JSON_RECEIPT_");
                await File.WriteAllTextAsync(barrierPath, barrierText);
                await File.WriteAllTextAsync(outcomePath, outcomeText);
                if (File.Exists(extra)) File.Delete(extra);
            }
        }
    }

    [Test]
    [Arguments("constructor")]
    [Arguments("setter")]
    [Arguments("input-mutation")]
    public async Task PinnedJsonFactoryRefusesCompetingDeclarationBeforeConstructorOrSetter(string attack)
    {
        using var fixture = new Fixture();
        fixture.Write("input.json", """{"items":{}}""");
        fixture.Write("main.proj", $$"""
            <Project>
              <UsingTask TaskName="ReadWasmProps" TaskFactory="RoslynCodeTaskFactory"
                AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">
                <Task><Code Type="Class" Language="cs"><![CDATA[
                  public sealed class ReadWasmProps : Microsoft.Build.Utilities.Task
                  {
                    public ReadWasmProps() { {{(attack == "constructor" ? """System.IO.File.AppendAllText("activation.txt", "constructor\n");""" : "")}} }
                    public string JsonFilePath
                    {
                      get { return ""; }
                      set { {{(attack == "setter" ? """System.IO.File.AppendAllText("activation.txt", "setter\n");"""
                          : attack == "input-mutation" ? """System.IO.File.WriteAllText(value, "changed");""" : "")}} }
                    }
                    public override bool Execute() { return true; }
                  }
                ]]></Code></Task>
              </UsingTask>
              <Target Name="Build"><ReadWasmProps JsonFilePath="input.json" /></Target>
            </Project>
            """);
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
        await Assert.That(File.Exists(Path.Combine(fixture.Session, "work/repo/activation.txt"))).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "work/repo/input.json")))
            .IsEqualTo("""{"items":{}}""");
    }

    [Test]
    [Arguments("absent")]
    [Arguments("token")]
    [Arguments("context")]
    [Arguments("duplicate")]
    [Arguments("replay")]
    [Arguments("thread")]
    [Arguments("asynchronous")]
    [Arguments("cancel")]
    public async Task PinnedJsonFactoryRequiresNativeReceiptBeforeSdkActivation(string attack)
    {
        using var fixture = new Fixture();
        string probe = await fixture.PrepareNativeJsonProbe();
        var probed = await ReleaseTestPaths.Run(Path.Combine(fixture.Session, "frozen/sdk/dotnet"), probe, fixture.Session, attack);
        await Assert.That(probed.ExitCode).IsEqualTo(0).Because(probed.Output);
        using var observed = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "probe-result.json")));
        Console.WriteLine("JSON_FACTORY_NATIVE_BOUNDARY " + observed.RootElement.GetRawText());
        await Assert.That(observed.RootElement.GetProperty("requests").GetInt32()).IsEqualTo(attack == "cancel" ? 0 : 1);
        await Assert.That(observed.RootElement.GetProperty("nativeContext").GetBoolean()).IsEqualTo(attack != "cancel");
        await Assert.That(observed.RootElement.GetProperty("sdkLoaded").GetBoolean()).IsFalse();
        await Assert.That(observed.RootElement.GetProperty("succeeded").GetBoolean()).IsFalse();
    }

    [Test]
    [Arguments("valid")]
    [Arguments("assembly")]
    [Arguments("recipe")]
    [Arguments("group")]
    [Arguments("body")]
    [Arguments("factory")]
    [Arguments("override")]
    [Arguments("project-mutation")]
    [Arguments("child-bypass")]
    [Arguments("host-injection")]
    [Arguments("input")]
    public async Task PinnedJsonFactoryRejectsChangedBindingsBeforeNativeActivation(string attack)
    {
        using var fixture = new Fixture();
        string probe = await fixture.PrepareNativeJsonProbe();
        string recipe = Path.Combine(fixture.Session,
            "frozen/sdk/packs/Microsoft.NET.Runtime.WebAssembly.Sdk/10.0.9/Sdk/WasmApp.Common.targets");
        if (attack == "assembly")
            await File.AppendAllTextAsync(Path.Combine(fixture.Session,
                "frozen/sdk/packs/Microsoft.NET.Runtime.MonoTargets.Sdk/10.0.9/tasks/net10.0/MonoTargetsTasks.dll"), "changed");
        else if (attack == "recipe") await File.AppendAllTextAsync(recipe, "\n");
        else if (attack is "group" or "body" or "factory")
        {
            var document = System.Xml.Linq.XDocument.Load(recipe, System.Xml.Linq.LoadOptions.PreserveWhitespace);
            var declaration = document.Descendants().Single(element => element.Name.LocalName == "UsingTask"
                && element.Attribute("TaskName")?.Value == "ReadWasmProps");
            if (attack == "group")
                declaration.Descendants().Single(element => element.Name.LocalName == "EmccProperties")
                    .SetAttributeValue("ParameterType", "System.String");
            else if (attack == "body") declaration.Add(new System.Xml.Linq.XElement("Task", "unadmitted initialization"));
            else declaration.SetAttributeValue("TaskFactory", "Unadmitted.Factory");
            document.Save(recipe);
        }
        else if (attack == "override")
            await File.WriteAllTextAsync(Path.Combine(fixture.Session, "work/repo/artifacts/capture-overrides.targets"), "<Project />");
        else if (attack == "project-mutation")
        {
            string project = Path.Combine(fixture.Session, "work/repo/main.proj");
            var document = System.Xml.Linq.XDocument.Load(project);
            document.Descendants("Import").Remove();
            document.Save(project);
        }
        else if (attack == "input")
            await File.WriteAllTextAsync(Path.Combine(fixture.Session, "work/repo/input.json"), """{"items":{"changed":[]}}""");
        var result = await ReleaseTestPaths.Run(Path.Combine(fixture.Session, "frozen/sdk/dotnet"), probe, fixture.Session,
            attack == "child-bypass" ? "host-child" : attack == "host-injection" ? "host-injection" : "host");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        using var observed = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "probe-result.json")));
        Console.WriteLine("JSON_FACTORY_NATIVE_IDENTITY " + observed.RootElement.GetRawText());
        await Assert.That(observed.RootElement.GetProperty("succeeded").GetBoolean()).IsEqualTo(attack == "valid").Because(result.Output);
        await Assert.That(observed.RootElement.GetProperty("sdkLoaded").GetBoolean()).IsEqualTo(attack == "valid");
        if (attack != "valid")
            await Assert.That(result.Output).Contains(attack switch
            {
                "assembly" => "CAPTURE_JSON_IDENTITY_UNSUPPORTED",
                "override" => "CAPTURE_JSON_OVERRIDE_CHANGED",
                "host-injection" => "CAPTURE_HOST_PROPERTIES_UNSUPPORTED",
                "child-bypass" => "CAPTURE_CHILD_CAPTURE_PROPERTIES_UNSUPPORTED",
                "input" or "project-mutation" => "CAPTURE_ORIGINAL_MUTATED",
                _ => "CAPTURE_JSON_DECLARATION_UNSUPPORTED"
            });
        else
        {
            var outcomes = Directory.EnumerateFiles(Path.Combine(fixture.Session, "private/events"), "*.json")
                .Select(path => JsonNode.Parse(File.ReadAllText(path))!)
                .Where(value => value["operation"]!.GetValue<string>() == "json-factory-outcome").Select(value => value["value"]!).ToArray();
            await Assert.That(outcomes.Length).IsEqualTo(2);
            await Assert.That(outcomes.Select(value => value["id"]!.GetValue<string>()).Distinct().Count()).IsEqualTo(2);
            await Assert.That(outcomes.Select(value => value["token"]!.GetValue<string>()).Distinct().Count()).IsEqualTo(2);
        }
    }

    [Test]
    [Arguments("properties", false)]
    [Arguments("metadata-removal", true)]
    [Arguments("metadata-additional", false)]
    [Arguments("native-flags", true)]
    public async Task PinnedJsonFactoryPreservesCapturePropertiesAtNativeChildDispatch(string mode, bool succeeds)
    {
        using var fixture = new Fixture();
        string probe = await fixture.PrepareNativeJsonProbe();
        var result = await ReleaseTestPaths.Run(Path.Combine(fixture.Session, "frozen/sdk/dotnet"), probe,
            fixture.Session, "host-" + mode);
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        using var observed = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "probe-result.json")));
        await Assert.That(observed.RootElement.GetProperty("succeeded").GetBoolean()).IsEqualTo(succeeds).Because(result.Output);
        await Assert.That(observed.RootElement.GetProperty("sdkLoaded").GetBoolean()).IsEqualTo(succeeds);
        if (!succeeds)
            await Assert.That(result.Output).Contains("CAPTURE_CHILD_CAPTURE_PROPERTIES_UNSUPPORTED");
        else
        {
            var outcomes = Directory.EnumerateFiles(Path.Combine(fixture.Session, "private/events"), "*.json")
                .Select(path => JsonNode.Parse(File.ReadAllText(path))!)
                .Where(value => value["operation"]!.GetValue<string>() == "json-factory-outcome").ToArray();
            await Assert.That(outcomes.Length).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments("override")]
    [Arguments("imported")]
    [Arguments("repeat")]
    public async Task PinnedJsonFactoryRejectsChildDeclarationsBeforeSdkLoading(string mode)
    {
        using var fixture = new Fixture();
        string probe = await fixture.PrepareNativeJsonProbe();
        var result = await ReleaseTestPaths.Run(Path.Combine(fixture.Session, "frozen/sdk/dotnet"), probe,
            fixture.Session, "host-child-" + mode);
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        using var observed = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "probe-result.json")));
        await Assert.That(observed.RootElement.GetProperty("sdkLoaded").GetBoolean()).IsFalse().Because(result.Output);
        await Assert.That(observed.RootElement.GetProperty("succeeded").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task PinnedJsonFactoryRetainsFailedOutcomeAfterDelegatedCleanup()
    {
        using var fixture = new Fixture();
        fixture.Write("input.json", "{invalid-json");
        fixture.Write("main.proj", """
            <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <Target Name="CaptureJson"><ReadWasmProps JsonFilePath="input.json" /></Target>
            </Project>
            """);
        var result = await fixture.Capture("main.proj", "CaptureJson");
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
        var events = Directory.EnumerateFiles(Path.Combine(fixture.Session, "private/events"), "*.json")
            .Select(path => JsonNode.Parse(File.ReadAllText(path))!).ToArray();
        var barrier = events.Single(value => value["operation"]!.GetValue<string>() == "json-factory-barrier")["value"]!;
        var outcome = events.Single(value => value["operation"]!.GetValue<string>() == "json-factory-outcome")["value"]!;
        await Assert.That(outcome["id"]!.GetValue<string>()).IsEqualTo(barrier["id"]!.GetValue<string>());
        await Assert.That(outcome["succeeded"]!.GetValue<bool>()).IsFalse();
        var invocation = events.Single(value => value["operation"]!.GetValue<string>() == "invocation-finished"
            && value["value"]!["task"]!.GetValue<string>() == "ReadWasmProps")["value"]!;
        await Assert.That(invocation["succeeded"]!.GetValue<bool>()).IsFalse();
    }

    private const string NativeJsonReceiptProbe = """
        using System.Runtime.CompilerServices;
        using System.Runtime.Loader;
        using System.Text.Json;
        using Microsoft.Build.Evaluation;
        using Microsoft.Build.Execution;
        using Microsoft.Build.Framework;
        internal static class Program
        {
            public static int Main(string[] args)
            {
                Environment.SetEnvironmentVariable("MSBUILDLOGTASKINPUTS", "1");
                Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");
                string sdk = Path.Combine(args[0], "frozen/sdk/sdk/10.0.404");
                Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", Path.Combine(sdk, "MSBuild.dll"));
                Environment.SetEnvironmentVariable("MSBuildSDKsPath", Path.Combine(sdk, "Sdks"));
                Environment.SetEnvironmentVariable("MSBuildExtensionsPath", sdk);
                Environment.SetEnvironmentVariable("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", Path.Combine(args[0], "frozen/sdk"));
                AssemblyLoadContext.Default.Resolving += (context, name) =>
                {
                    string path = Path.Combine(sdk, name.Name + ".dll");
                    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
                };
                return Run(args);
            }
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static int Run(string[] args)
            {
                bool sdkLoaded = false;
                AppDomain.CurrentDomain.AssemblyLoad += (_, value) =>
                {
                    if (value.LoadedAssembly.GetName().Name == "MonoTargetsTasks") sdkLoaded = true;
                };
                if (args[1].StartsWith("host", StringComparison.Ordinal))
                {
                    Environment.SetEnvironmentVariable("CONFIGURATOR_CAPTURE_GRAPH", Guid.NewGuid().ToString("N"));
                    var tool = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(args[0], "private/tool/Configurator.BuildCapture.dll"));
                    string[] hostArguments = ["--host", args[0], Path.Combine(args[0], "work/repo/main.proj"),
                        args[1] switch
                        {
                            "host-child" => "ProbeChild",
                            "host-properties" => "ProbeProperties",
                            "host-metadata-removal" => "ProbeMetadataRemoval",
                            "host-metadata-additional" => "ProbeMetadataAdditional",
                            "host-native-flags" => "ProbeNativeFlags",
                            "host-child-override" => "ProbeOverride",
                            "host-child-imported" => "ProbeImported",
                            "host-child-repeat" => "ProbeRepeat",
                            _ => "Probe"
                        },
                        "ConfiguratorCaptureSession=" + args[0],
                        "ConfiguratorCaptureTool=" + Path.Combine(args[0], "private/tool/Configurator.BuildCapture.dll"),
                        "ConfiguratorCaptureOverrides=" + Path.Combine(args[0], "work/repo/artifacts/capture-overrides.targets"),
                        "CustomAfterMicrosoftCommonProps=" + Path.Combine(args[0], "work/repo/artifacts/capture-early.props"),
                        "CustomBeforeMicrosoftCommonTargets=" + Path.Combine(args[0], "work/repo/artifacts/capture-overrides.targets")];
                    if (args[1] == "host-injection")
                        hostArguments = hostArguments.Where(value => !value.StartsWith("CustomBeforeMicrosoftCommonTargets=", StringComparison.Ordinal)).ToArray();
                    int exit;
                    try { exit = (int)tool.EntryPoint!.Invoke(null, [hostArguments])!; }
                    catch (System.Reflection.TargetInvocationException error)
                    {
                        for (Exception? current = error; current is not null; current = current.InnerException)
                            foreach (System.Text.RegularExpressions.Match code in
                                System.Text.RegularExpressions.Regex.Matches(current.Message, @"CAPTURE_[A-Z0-9_]+"))
                                Console.WriteLine(code.Value);
                        exit = 1;
                    }
                    File.WriteAllText(Path.Combine(args[0], "probe-result.json"),
                        JsonSerializer.Serialize(new { sdkLoaded, succeeded = exit == 0 }));
                    return 0;
                }
                using var collection = new ProjectCollection();
                using var manager = new BuildManager();
                var logger = new ProbeLogger(args[1]);
                var parameters = new BuildParameters(collection)
                {
                    Loggers = [logger], UseSynchronousLogging = args[1] != "asynchronous", MaxNodeCount = 1,
                    EnableNodeReuse = false, DisableInProcNode = false, ShutdownInProcNodeOnBuildFinish = true
                };
                var globals = new Dictionary<string, string?>
                {
                    ["ConfiguratorCaptureOverrides"] = Path.Combine(args[0], "work/repo/artifacts/capture-overrides.targets"),
                    ["ProbeAssembly"] = typeof(Program).Assembly.Location,
                    ["CaptureTool"] = Path.Combine(args[0], "private/tool/Configurator.BuildCapture.dll"),
                    ["UseSharedCompilation"] = "false", ["BuildInParallel"] = "false"
                };
                var result = manager.Build(parameters, new BuildRequestData(Path.Combine(args[0], "work/repo/main.proj"),
                    globals, null, [args[1] == "cancel" ? "CancelProbe" : "Probe"], null));
                File.WriteAllText(Path.Combine(args[0], "probe-result.json"), JsonSerializer.Serialize(new
                {
                    requests = logger.Requests, nativeContext = logger.NativeContext, sdkLoaded,
                    succeeded = result.OverallResult == BuildResultCode.Success
                }));
                return 0;
            }
        }
        public sealed class CancelledReader : ITask
        {
            public IBuildEngine BuildEngine { get; set; } = null!;
            public ITaskHost HostObject { get; set; } = null!;
            [Required] public string CaptureTool { get; set; } = "";
            public bool Execute()
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(CaptureTool);
                var factory = (ITaskFactory)Activator.CreateInstance(
                    assembly.GetType("Configurator.BuildCapture.ReadWasmPropsCaptureFactory", true)!)!;
                if (!factory.Initialize("ReadWasmProps", new Dictionary<string, TaskPropertyInfo>(),
                    "pinned-json-reader-10.0.404", BuildEngine)) throw new InvalidOperationException();
                var task = factory.CreateTask(BuildEngine);
                task.BuildEngine = BuildEngine;
                task.HostObject = HostObject;
                ((ICancelableTask)task).Cancel();
                bool result = task.Execute();
                factory.CleanupTask(task);
                factory.CleanupTask(task);
                return result;
            }
        }
        internal sealed class ProbeLogger(string attack) : ILogger
        {
            private BuildEventContext? _current;
            public int Requests { get; private set; }
            public bool NativeContext { get; private set; }
            public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;
            public string? Parameters { get; set; }
            public void Initialize(IEventSource source)
            {
                source.TaskStarted += (_, args) => _current = args.BuildEventContext;
                source.TaskFinished += (_, _) => _current = null;
                source.CustomEventRaised += (_, args) =>
                {
                    if (args is not IExtendedBuildEventArgs extended
                        || extended.ExtendedType != "Configurator.ReadWasmProps.Capture") return;
                    Requests++;
                    NativeContext = _current is not null && _current.Equals(args.BuildEventContext);
                    var fields = extended.ExtendedMetadata!;
                    if (attack == "absent") return;
                    fields["ack-token"] = attack is "token" or "replay" ? "previous-token" : fields["token"];
                    fields["ack-context"] = attack is "context" or "replay" ? "previous-context" : Context(args.BuildEventContext);
                    fields["ack-thread"] = attack == "thread" ? "-1" : Environment.CurrentManagedThreadId.ToString();
                    fields["ack-count"] = attack == "duplicate" ? "2" : "1";
                };
            }
            private static string Context(BuildEventContext? c) => c is null ? "none" :
                $"{c.SubmissionId}/{c.NodeId}/{c.EvaluationId}/{c.ProjectInstanceId}/{c.ProjectContextId}/{c.TargetId}/{c.TaskId}/{c.BuildRequestId}";
            public void Shutdown() { }
        }
        """;

    [Test]
    [Arguments("valid")]
    [Arguments("destination")]
    [Arguments("recreated")]
    [Arguments("duplicate")]
    [Arguments("missing-array")]
    [Arguments("invocation")]
    [Arguments("recipe")]
    [Arguments("snapshot")]
    [Arguments("pairing")]
    public async Task LifecycleMoveBindsConsumedVersionToFinalPayload(string attack)
    {
        using var fixture = new Fixture();
        fixture.Write("eng/configurator/Configurator.Release.proj", """
            <Project><Import Project="$(ConfiguratorCaptureOverrides)" />
              <Target Name="PublishConfigurator">
                <MakeDir Directories="../../artifacts/configurator/cli/linux-x64" />
                <WriteLinesToFile File="../../artifacts/configurator/cli/linux-x64/Configurator.Cli" Lines="synthetic apphost" Overwrite="true" />
                <Move SourceFiles="../../artifacts/configurator/cli/linux-x64/Configurator.Cli"
                  DestinationFiles="../../artifacts/configurator/cli/linux-x64/islamu-configurator" />
              </Target>
            </Project>
            """);
        var captured = await fixture.Capture("eng/configurator/Configurator.Release.proj", "PublishConfigurator");
        await Assert.That(captured.ExitCode).IsEqualTo(0).Because(captured.Output);
        var record = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!.AsObject();
        await Assert.That(record["movedInputs"]!.AsArray().Count).IsEqualTo(1);
        var move = record["movedInputs"]![0]!;
        string directory = Path.Combine(fixture.Session, "work/repo/artifacts/configurator/cli/linux-x64");
        switch (attack)
        {
            case "valid":
                var valid = await fixture.VerifyWithoutSdk("--verify", fixture.Session);
                await Assert.That(valid.ExitCode).IsEqualTo(0).Because(valid.Output);
                return;
            case "destination": await File.AppendAllTextAsync(Path.Combine(directory, "islamu-configurator"), "wrong"); break;
            case "recreated": await File.WriteAllTextAsync(Path.Combine(directory, "Configurator.Cli"), "synthetic apphost"); break;
            case "duplicate": record["movedInputs"]!.AsArray().Add(move.DeepClone()); break;
            case "missing-array": record.Remove("movedInputs"); break;
            case "invocation": move["invocation"] = "orphan"; break;
            case "recipe": move["recipe"]!["sha256"] = new string('0', 64); break;
            case "snapshot":
                await File.AppendAllTextAsync(Path.Combine(fixture.Session, "frozen", move["source"]!["path"]!.GetValue<string>()), "wrong");
                break;
            case "pairing":
                record["invocations"]!.AsArray().Single(item => item!["id"]!.GetValue<string>() == move["invocation"]!.GetValue<string>())!
                    ["parameters"]!.AsArray().Single(item => item!["name"]!.GetValue<string>() == "DestinationFiles")!
                    ["value"]!["paths"]![0] = "generated/repo/artifacts/configurator/cli/linux-x64/wrong-payload";
                break;
        }
        await File.WriteAllTextAsync(fixture.Record, record.ToJsonString());
        await File.WriteAllTextAsync(Path.Combine(fixture.Session, "capture.sha256"),
            ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(fixture.Record)));
        var rejected = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
        await Assert.That(rejected.ExitCode).IsNotEqualTo(0);
    }

    [Test]
    [Arguments("delete")]
    [Arguments("skipped")]
    [Arguments("original")]
    public async Task LifecycleUnadmittedDisappearanceCannotAcquireMoveAuthority(string scenario)
    {
        using var fixture = new Fixture();
        fixture.Write("original.txt", "immutable synthetic original");
        fixture.Write("eng/configurator/Configurator.Release.proj", """
            <Project><Import Project="$(ConfiguratorCaptureOverrides)" />
              <Target Name="PublishConfigurator">
                <MakeDir Directories="../../artifacts/configurator/cli/linux-x64" />
                <WriteLinesToFile File="../../artifacts/configurator/cli/linux-x64/Configurator.Cli" Lines="synthetic apphost" Overwrite="true" />
                <ReadLinesFromFile File="../../artifacts/configurator/cli/linux-x64/Configurator.Cli" />
                OPERATION
              </Target></Project>
            """.Replace("OPERATION", scenario switch
        {
            "original" => """
                    <Move SourceFiles="../../original.txt" DestinationFiles="../../artifacts/configurator/cli/linux-x64/islamu-configurator" />
                    """,
            "skipped" => """
                    <Move Condition="false" SourceFiles="../../artifacts/configurator/cli/linux-x64/Configurator.Cli"
                      DestinationFiles="../../artifacts/configurator/cli/linux-x64/islamu-configurator" />
                    <Delete Files="../../artifacts/configurator/cli/linux-x64/Configurator.Cli" />
                    """,
            _ => """<Delete Files="../../artifacts/configurator/cli/linux-x64/Configurator.Cli" />"""
        }, StringComparison.Ordinal));
        var captured = await fixture.Capture("eng/configurator/Configurator.Release.proj", "PublishConfigurator");
        await Assert.That(captured.ExitCode).IsNotEqualTo(0);
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
    }

    [Test]
    [Arguments("valid")]
    [Arguments("mutation")]
    [Arguments("replay")]
    [Arguments("failed-query")]
    [Arguments("recreated")]
    [Arguments("original")]
    [Arguments("wrong-query")]
    [Arguments("wrong-surface")]
    [Arguments("wrong-output")]
    [Arguments("missing-consume")]
    [Arguments("wrong-generator")]
    [Arguments("wrong-parent")]
    [Arguments("recreated-link")]
    public async Task LifecycleRetirementRequiresSuccessfulQueryAndNativeDeletion(string scenario)
    {
        using var fixture = new Fixture();
        fixture.Write("main.proj", """
            <Project><Import Project="$(ConfiguratorCaptureOverrides)" />
              <UsingTask TaskName="Configurator.BuildCapture.BuildCaptureTask" AssemblyFile="$(ConfiguratorCaptureTool)" />
              <Target Name="Build"><BuildCaptureTask Session="$(ConfiguratorCaptureSession)"
                Project="$(MSBuildProjectFullPath)" Operation="ratchets" Arguments="--check" /></Target>
            </Project>
            """);
        fixture.Write("eng/configurator/Configurator.Release.proj", """
            <Project><Import Project="$(ConfiguratorCaptureOverrides)" />
              <Target Name="WriteConfiguratorReleaseContract">
                <WriteLinesToFile File="$(ConfiguratorContractOutput)" Lines="synthetic contract" Overwrite="true" />
                FAILURE
              </Target></Project>
            """.Replace("FAILURE", scenario == "failed-query" ? """<Error Text="synthetic query failure" />""" : "", StringComparison.Ordinal));
        fixture.Write("eng/configurator/GenerateConfiguratorRatchets.cs", """
            #:project Configurator.BuildCapture/Configurator.BuildCapture.csproj
            using System.Diagnostics;
            using Configurator.BuildCapture;
            string scenario = "SCENARIO";
            string output = Path.Combine(Path.GetTempPath(), "configurator-contract-" + Guid.NewGuid().ToString("N") + ".json");
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
            foreach (string argument in new[] { "msbuild", "eng/configurator/Configurator.Release.proj", "-nologo",
                "-verbosity:quiet", "-target:WriteConfiguratorReleaseContract", "-property:ConfiguratorSurface=cli",
                "-property:ConfiguratorContractOutput=" + output }) start.ArgumentList.Add(argument);
            try
            {
                CaptureSession.Instrument(start);
                if (scenario == "wrong-query") start.ArgumentList[4] = "UnadmittedQuery";
                if (scenario == "wrong-surface") start.ArgumentList[5] = "ConfiguratorSurface=terminal";
                if (scenario == "wrong-output") start.ArgumentList[6] += ".other";
                using var process = Process.Start(start)!;
                process.WaitForExit();
                if (process.ExitCode != 0) return process.ExitCode;
                if (scenario != "missing-consume") CaptureSession.Consume(output);
                _ = File.ReadAllText(output);
                if (scenario == "mutation") File.AppendAllText(output, "changed");
                if (scenario == "wrong-generator")
                    Environment.SetEnvironmentVariable("CONFIGURATOR_CAPTURE_GENERATOR_ID", Guid.NewGuid().ToString("N"));
                if (scenario == "wrong-parent")
                    Environment.SetEnvironmentVariable("CONFIGURATOR_CAPTURE_PARENT_INVOCATION", "orphan");
                var retire = typeof(CaptureSession).GetMethod("RetireContractOutput")
                    ?? throw new InvalidOperationException("RETIREMENT_API_MISSING");
                retire.Invoke(null, new object[] { scenario == "original" ? "main.proj" : output });
                if (scenario == "replay") retire.Invoke(null, new object[] { output });
                if (scenario == "recreated") File.WriteAllText(output, "recreated");
                if (scenario == "recreated-link") File.CreateSymbolicLink(output, output + ".missing");
                return 0;
            }
            finally { if (scenario is not ("recreated" or "recreated-link") && File.Exists(output)) File.Delete(output); }
            """.Replace("SCENARIO", scenario, StringComparison.Ordinal));
        var captured = await fixture.Capture("main.proj", "Build");
        await Assert.That(captured.ExitCode == 0).IsEqualTo(scenario == "valid").Because(captured.Output);
        if (scenario == "valid")
        {
            var record = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!;
            await Assert.That(record["retiredInputs"]!.AsArray().Count).IsEqualTo(1);
            var verified = await fixture.VerifyWithoutSdk("--verify", fixture.Session);
            await Assert.That(verified.ExitCode).IsEqualTo(0).Because(verified.Output);
            foreach (string attack in new[] { "query", "surface", "recipe", "invocation", "duplicate", "missing-array" })
            {
                var altered = record.DeepClone();
                var retirement = altered["retiredInputs"]![0]!;
                switch (attack)
                {
                    case "query": retirement["queryGraph"] = Guid.NewGuid().ToString("N"); break;
                    case "surface": retirement["surface"] = "terminal"; break;
                    case "recipe": retirement["recipe"]!["sha256"] = new string('0', 64); break;
                    case "invocation": retirement["generatorInvocation"] = "orphan"; break;
                    case "duplicate": altered["retiredInputs"]!.AsArray().Add(retirement.DeepClone()); break;
                    case "missing-array": altered.AsObject().Remove("retiredInputs"); break;
                }
                await File.WriteAllTextAsync(fixture.Record, altered.ToJsonString());
                await File.WriteAllTextAsync(Path.Combine(fixture.Session, "capture.sha256"),
                    ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(fixture.Record)));
                var rejected = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
                await Assert.That(rejected.ExitCode).IsNotEqualTo(0).Because(attack);
            }
            await File.WriteAllTextAsync(fixture.Record, record.ToJsonString());
            await File.WriteAllTextAsync(Path.Combine(fixture.Session, "capture.sha256"),
                ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(fixture.Record)));
            string relocated = fixture.Session + "-relocated";
            Directory.Move(fixture.Session, relocated);
            Directory.Move(fixture.Root, fixture.Root + "-unavailable");
            await Assert.That(Directory.Exists(fixture.Session)).IsFalse();
            await Assert.That(Directory.Exists(fixture.Root)).IsFalse();
            string runtime = Path.Combine(Path.GetDirectoryName(fixture.Root)!, "runtime-only");
            string muxer = Path.Combine(runtime, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            var relocatedVerification = await ReleaseTestPaths.Run(muxer,
                Path.Combine(runtime, "capture/Configurator.BuildCapture.dll"), "--verify", relocated);
            await Assert.That(relocatedVerification.ExitCode).IsEqualTo(0).Because(relocatedVerification.Output);
        }
    }

    [Test]
    public async Task RepositorySchemaRequiresNamespaceAndAdmitsTargetOutcomes()
    {
        var schema = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(ReleaseTestPaths.Root,
            "schemas/configurator-build-inputs.schema.json")))!;
        await Assert.That(schema.AsObject().ContainsKey("required")).IsTrue();
        await Assert.That(schema["required"]!.AsArray().Select(name => name!.GetValue<string>()))
            .Contains("repositoryNamespace");
        await Assert.That(schema["required"]!.AsArray().Select(name => name!.GetValue<string>())).Contains("movedInputs");
        await Assert.That(schema["required"]!.AsArray().Select(name => name!.GetValue<string>())).Contains("retiredInputs");
        var names = Resolve(schema["properties"]!["repositoryNamespace"]!);
        await Assert.That(names["type"]!.GetValue<string>()).IsEqualTo("array");
        await Assert.That(names["uniqueItems"]!.GetValue<bool>()).IsTrue();
        var target = Resolve(Resolve(schema["properties"]!["targets"]!)["items"]!);
        await Assert.That(target["properties"]!["outcome"]!["enum"]!.AsArray().Select(value => value!.GetValue<string>()))
            .IsEquivalentTo(["started", "succeeded", "failed", "skipped"]);

        JsonNode Resolve(JsonNode value)
        {
            while (value["$ref"] is { } reference)
            {
                string pointer = reference.GetValue<string>();
                if (!pointer.StartsWith("#/", StringComparison.Ordinal)) throw new InvalidOperationException();
                value = schema;
                foreach (string segment in pointer[2..].Split('/'))
                    value = value[segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)]!;
            }
            return value;
        }
    }

    [Test]
    [Arguments("none")]
    [Arguments("generated-work")]
    [Arguments("missing-public")]
    [Arguments("missing-private")]
    [Arguments("null-public")]
    [Arguments("duplicate-private")]
    [Arguments("unsorted-public")]
    [Arguments("malformed-private")]
    [Arguments("extra-frozen")]
    [Arguments("missing-frozen")]
    [Arguments("link-frozen")]
    [Arguments("link-private")]
    [Arguments("link-public")]
    public async Task RepositoryNamespaceRequiresFrozenPrivatePublicAgreement(string attack)
    {
        using var fixture = new Fixture();
        fixture.WriteNamespaceProject(attack == "generated-work");
        var captured = await fixture.Capture("src/probe/main.proj", "Build");
        await Assert.That(captured.ExitCode).IsEqualTo(0).Because(captured.Output);
        var record = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!.AsObject();
        string configurationPath = Path.Combine(fixture.Session, "private/session.json");
        var configuration = JsonNode.Parse(await File.ReadAllTextAsync(configurationPath))!.AsObject();
        if (attack is "none" or "generated-work")
        {
            await Assert.That(record.ContainsKey("repositoryNamespace")).IsTrue();
            await Assert.That(configuration.ContainsKey("repositoryNamespace")).IsTrue();
            await Assert.That(record["repositoryNamespace"]!.AsArray().Select(name => name!.GetValue<string>())
                .SequenceEqual(NamespaceNames, StringComparer.Ordinal)).IsTrue();
            await Assert.That(configuration["repositoryNamespace"]!.AsArray().Select(name => name!.GetValue<string>())
                .SequenceEqual(NamespaceNames, StringComparer.Ordinal)).IsTrue();
            var verified = await fixture.VerifyWithoutSdk("--verify", fixture.Session);
            await Assert.That(verified.ExitCode).IsEqualTo(0).Because(verified.Output);
            return;
        }
        switch (attack)
        {
            case "missing-public": record.Remove("repositoryNamespace"); break;
            case "missing-private": configuration.Remove("repositoryNamespace"); break;
            case "null-public": record["repositoryNamespace"] = null; break;
            case "duplicate-private":
                configuration["repositoryNamespace"] = JsonSerializer.SerializeToNode(
                    NamespaceNames.Append(NamespaceNames[0]).Order(StringComparer.Ordinal).ToArray());
                break;
            case "unsorted-public":
                record["repositoryNamespace"] = JsonSerializer.SerializeToNode(NamespaceNames.Reverse().ToArray());
                break;
            case "malformed-private":
                configuration["repositoryNamespace"] = JsonSerializer.SerializeToNode(
                    NamespaceNames.Select(name => name == NamespaceNames[0] ? "repo/src/../probe/input.txt" : name).ToArray());
                break;
            case "extra-frozen":
                await File.WriteAllTextAsync(Path.Combine(fixture.Session, "frozen/repo/src/probe/late.cs"), "late source");
                await File.WriteAllTextAsync(Path.Combine(fixture.Session, "work/repo/src/probe/late.cs"), "late source");
                break;
            case "missing-frozen":
                File.Delete(Path.Combine(fixture.Session, "frozen/repo/src/probe/unused.txt"));
                File.Delete(Path.Combine(fixture.Session, "work/repo/src/probe/unused.txt"));
                break;
            case "link-frozen":
                foreach (string root in new[] { "frozen/repo", "work/repo" })
                {
                    string unused = Path.Combine(fixture.Session, root, "src/probe/unused.txt");
                    File.Delete(unused);
                    File.CreateSymbolicLink(unused, Path.Combine(fixture.Session, root, "src/probe/input.txt"));
                }
                break;
            case "link-private":
                {
                    string retained = Path.Combine(fixture.Session, "private/retained-session.json");
                    File.Copy(configurationPath, retained);
                    File.Delete(configurationPath);
                    File.CreateSymbolicLink(configurationPath, retained);
                    break;
                }
            case "link-public":
                {
                    string retained = Path.Combine(fixture.Session, "private/retained-capture.json");
                    File.Copy(fixture.Record, retained);
                    File.Delete(fixture.Record);
                    File.CreateSymbolicLink(fixture.Record, retained);
                    break;
                }
            default: throw new InvalidOperationException();
        }
        await File.WriteAllTextAsync(fixture.Record, record.ToJsonString());
        await File.WriteAllTextAsync(Path.Combine(fixture.Session, "capture.sha256"),
            ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(fixture.Record)));
        await File.WriteAllTextAsync(configurationPath, configuration.ToJsonString());
        var rejected = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
        await Assert.That(rejected.ExitCode).IsNotEqualTo(0);
    }

    [Test]
    [Arguments("matching")]
    [Arguments("excluded-metadata")]
    [Arguments("late-source")]
    [Arguments("missing-source")]
    [Arguments("renamed-source")]
    [Arguments("late-test")]
    [Arguments("duplicate")]
    [Arguments("unsorted")]
    [Arguments("raw-name")]
    [Arguments("traversal")]
    [Arguments("invalid-utf8")]
    [Arguments("non-array")]
    [Arguments("non-string")]
    [Arguments("oversized")]
    public async Task RepositoryNamesRequireTheExactEligibleCandidateNamespace(string scenario)
    {
        using var fixture = new Fixture();
        fixture.WriteNamespaceProject();
        var captured = await fixture.Capture("src/probe/main.proj", "Build");
        await Assert.That(captured.ExitCode).IsEqualTo(0).Because(captured.Output);
        var names = NamespaceNames.ToList();
        switch (scenario)
        {
            case "excluded-metadata":
                names.Add("repo/docs/internal/detached-note.md");
                names.Add("repo/src/probe/obj/generated.cs");
                break;
            case "late-source": names.Add("repo/src/probe/LateAdded.cs"); break;
            case "missing-source": names.Remove("repo/src/probe/unused.txt"); break;
            case "renamed-source": names[2] = "repo/src/probe/renamed.txt"; break;
            case "late-test": names.Add("repo/tests/Probe/LateTests.cs"); break;
            case "duplicate":
                names.Add("repo/docs/detached.md");
                names.Add("repo/docs/detached.md");
                break;
            case "raw-name": names[0] = "src/probe/input.txt"; break;
            case "traversal": names.Add("repo/docs/../src/probe/forged.cs"); break;
        }
        names.Sort(StringComparer.Ordinal);
        if (scenario == "unsorted") names.Reverse();
        string namesPath = Path.Combine(Path.GetDirectoryName(fixture.Root)!, "candidate-names.json");
        if (scenario == "invalid-utf8")
        {
            byte[] invalid = [0xff];
            await File.WriteAllBytesAsync(namesPath, invalid);
        }
        else if (scenario == "oversized")
        {
            byte[] oversized = new byte[16 * 1024 * 1024 + 1];
            Array.Fill(oversized, (byte)' ');
            await File.WriteAllBytesAsync(namesPath, oversized);
        }
        else
            await File.WriteAllTextAsync(namesPath, scenario switch
            {
                "non-array" => "{}",
                "non-string" => "[17]",
                _ => JsonSerializer.Serialize(names)
            });
        var verified = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session, "--repository-names", namesPath);
        await Assert.That(verified.ExitCode == 0).IsEqualTo(scenario is "matching" or "excluded-metadata").Because(verified.Output);
        if (scenario == "oversized") await Assert.That(verified.Output).Contains("CAPTURE_NAMESPACE_LIMIT");
        if (scenario == "matching")
        {
            var staged = await fixture.VerifyWithoutSdk("--verify", fixture.Session, "--repository-names", namesPath);
            await Assert.That(staged.ExitCode).IsEqualTo(0).Because(staged.Output);
        }
    }

    [Test]
    [Arguments("asynchronous", "CAPTURE_CALIBRATION_ORDERING")]
    [Arguments("early-read", "CAPTURE_EARLY_READ_UNSUPPORTED")]
    public async Task UnprovenEventOrderingOrEarlyReadsCannotSeal(string mode, string diagnostic)
    {
        using var fixture = new Fixture();
        fixture.Write("main.proj", """<Project><Target Name="Build" /></Project>""");
        var result = await fixture.Capture("main.proj", "Build", "--property", "ConfiguratorCaptureCalibration=" + mode);
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.Output).Contains(diagnostic);
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
    }

    [Test]
    public async Task StandardSdkResourceConsumptionBindsOriginalAndGeneratedBytes()
    {
        using var fixture = new Fixture();
        fixture.Write("main.csproj", """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
            """);
        fixture.Write("Example.cs", "public sealed class Example;");
        fixture.Write("Strings.resx", """
            <root><data name="Greeting"><value>synthetic resource string</value></data></root>
            """);
        var result = await fixture.Capture("main.csproj", "Build");
        string hostMetadata = Path.Combine(fixture.Session, "private/task-hosts.log");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output
            + (File.Exists(hostMetadata) ? await File.ReadAllTextAsync(hostMetadata) : ""));
        var capture = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!;
        await Assert.That(capture["inputs"]!.AsArray().Any(input =>
            input!["path"]!.GetValue<string>() == "repo/Strings.resx")).IsTrue();
        await Assert.That(capture["inputs"]!.AsArray().Any(input =>
            input!["path"]!.GetValue<string>().EndsWith("/main.Strings.resources", StringComparison.Ordinal)
            && input["kind"]!.GetValue<string>() == "generated")).IsTrue();
        await Assert.That(capture["invocations"]!.AsArray().Any(invocation =>
            invocation!["task"]!.GetValue<string>() == "GenerateResource"
            && invocation["succeeded"]!.GetValue<bool>())).IsTrue();
    }

    [Test]
    [Arguments("none")]
    [Arguments("before")]
    [Arguments("after")]
    public async Task DeclaredGeneratedTransformationsBindBothVersionsAndDownstreamReads(string attack)
    {
        using var fixture = new Fixture();
        fixture.Write("main.proj", """
            <Project>
              <UsingTask TaskName="Configurator.BuildCapture.BuildCaptureTask" AssemblyFile="$(ConfiguratorCaptureTool)" />
              <Target Name="Build">
                <BuildCaptureTask Session="$(ConfiguratorCaptureSession)" Project="$(MSBuildProjectFullPath)" Operation="frameworks"
                  RuntimePacks="First.Pack" InventoryOutput="$(ConfiguratorCaptureSession)/private/tmp/frameworks.json" />
                <Copy SourceFiles="$(ConfiguratorCaptureSession)/private/tmp/frameworks.json" DestinationFiles="out/before.json" />
                <BuildCaptureTask Session="$(ConfiguratorCaptureSession)" Project="$(MSBuildProjectFullPath)" Operation="frameworks"
                  RuntimePacks="Second.Pack" InventoryOutput="$(ConfiguratorCaptureSession)/private/tmp/frameworks.json" />
                <Copy SourceFiles="$(ConfiguratorCaptureSession)/private/tmp/frameworks.json" DestinationFiles="out/after.json" />
              </Target>
            </Project>
            """);
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        var before = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "work/repo/out/before.json")))!;
        var after = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "work/repo/out/after.json")))!;
        await Assert.That(before["Items"]!["RuntimePack"]![0]!["Identity"]!.GetValue<string>()).IsEqualTo("First.Pack");
        await Assert.That(after["Items"]!["RuntimePack"]![0]!["Identity"]!.GetValue<string>()).IsEqualTo("Second.Pack");
        var capture = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!;
        var transformations = capture["invocations"]!.AsArray().SelectMany(invocation =>
            invocation!["transformations"]!.AsArray()).ToArray();
        await Assert.That(transformations.Length).IsEqualTo(2);
        var replacement = transformations.Single(transformation => transformation!["before"] is not null)!;
        string originalHash = replacement["before"]!["sha256"]!.GetValue<string>();
        string resultingHash = replacement["after"]!["sha256"]!.GetValue<string>();
        await Assert.That(originalHash).IsNotEqualTo(resultingHash);
        var consumed = capture["invocations"]!.AsArray()
            .Where(invocation => invocation!["task"]!.GetValue<string>() == "Copy")
            .SelectMany(invocation => invocation!["parameters"]!.AsArray())
            .Where(parameter => parameter!["name"]!.GetValue<string>() == "SourceFiles")
            .SelectMany(parameter => parameter!["inputs"]!.AsArray())
            .Select(input => input!["sha256"]!.GetValue<string>()).ToArray();
        await Assert.That(consumed).IsEquivalentTo([originalHash, resultingHash]);
        var valid = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
        await Assert.That(valid.ExitCode).IsEqualTo(0).Because(valid.Output);
        if (attack == "none") return;
        foreach (string path in Directory.EnumerateFiles(Path.Combine(fixture.Session, "private/events"), "*.json"))
        {
            var entry = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            if (entry["operation"]!.GetValue<string>() != "invocation-finished") continue;
            var versions = entry["value"]!["transformations"]!.AsArray();
            if (versions.Count != 1 || versions[0]!["before"] is null) continue;
            versions[0]![attack]!["sha256"] = new string('0', 64);
            await File.WriteAllTextAsync(path, entry.ToJsonString());
            break;
        }
        var rejected = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
        await Assert.That(rejected.ExitCode).IsNotEqualTo(0);
        await Assert.That(rejected.Output).Contains("CAPTURE_TRANSFORMATION_UNADMITTED");
    }

    [Test]
    [Arguments("CurrentArchitecture", true)]
    [Arguments("x86", true)]
    [Arguments("CurrentArchitecture", false)]
    public async Task TaskHostAdmissionHonorsConditionsAndRejectsExplicitHosts(string architecture, bool active)
    {
        using var fixture = new Fixture();
        fixture.Write("input.txt", "synthetic task-host input");
        fixture.Write("main.proj", $"""
            <Project>
              <UsingTask TaskName="Copy" AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll"
                TaskFactory="TaskHostFactory" Runtime="NET" Architecture="{architecture}" Override="true"
                Condition="'{active}' == 'True'" />
              <Target Name="Build"><Copy SourceFiles="input.txt" DestinationFiles="out/result.txt" /></Target>
            </Project>
            """);
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode == 0).IsEqualTo(!active).Because(result.Output);
        if (active)
        {
            await Assert.That(result.Output).Contains("CAPTURE_TASK_HOST_UNSUPPORTED");
            await Assert.That(File.Exists(fixture.Record)).IsFalse();
        }
        else
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "work/repo/out/result.txt")))
                .IsEqualTo("synthetic task-host input");
    }

    [Test]
    [Arguments(true, 0, false)]
    [Arguments(false, 0, false)]
    [Arguments(true, 16384, false)]
    [Arguments(true, 16385, false)]
    [Arguments(true, 0, true)]
    public async Task RejectedFirstPartyFactoryRetainsCorrelatedObservedEvidence(bool succeeds, int payloadLength, bool abort)
    {
        using var fixture = new Fixture();
        fixture.Write("main.proj", $$"""
            <Project>
              <UsingTask TaskName="SyntheticEvidenceTask" AssemblyFile="inactive.dll"
                TaskFactory="InactiveFactory" Condition="'false' == 'true'" />
              <UsingTask TaskName="SyntheticEvidenceTask" TaskFactory="RoslynCodeTaskFactory"
                AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">
                <ParameterGroup>
                  <Label ParameterType="System.String" Required="true" />
                  <Count ParameterType="System.Int32" Required="true" />
                  <Enabled ParameterType="System.Boolean" Required="true" />
                  <Effect ParameterType="System.String" Required="true" />
                  <Optional ParameterType="System.String" />
                  <Payload ParameterType="System.String" />
                  <Result ParameterType="System.String" Output="true" />
                  <Succeeds ParameterType="System.Boolean" />
                  <Abort ParameterType="System.Boolean" />
                </ParameterGroup>
                <Task><Code Type="Fragment" Language="cs"><![CDATA[
                  System.IO.File.WriteAllText(Effect, Label + ":" + Count + ":" + Enabled);
                  Result = Label + "-result";
                  if (Abort) System.Environment.Exit(23);
                  return Succeeds;
                ]]></Code></Task>
              </UsingTask>
              <Target Name="First">
                <SyntheticEvidenceTask Label="alpha" Count="7" Enabled="true" Effect="first.txt" Succeeds="true">
                  <Output TaskParameter="Result" PropertyName="FirstResult" />
                </SyntheticEvidenceTask>
              </Target>
              <Target Name="Build" DependsOnTargets="First">
                <SyntheticEvidenceTask Label="beta" Count="9" Enabled="false" Effect="second.txt" Succeeds="{{succeeds}}" Abort="{{abort}}" Payload="{{new string('x', payloadLength)}}">
                  <Output TaskParameter="Result" PropertyName="SecondResult" />
                </SyntheticEvidenceTask>
              </Target>
            </Project>
            """);
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.Output).Contains(abort ? "CAPTURE_BUILD_FAILED" : "CAPTURE_TASK_HOST_UNSUPPORTED");
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "work/repo/first.txt")))
            .IsEqualTo("alpha:7:True");
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "work/repo/second.txt")))
            .IsEqualTo("beta:9:False");
        string directory = Path.Combine(fixture.Session, "private/rejections");
        await Assert.That(Directory.Exists(directory)).IsTrue();
        var events = Directory.EnumerateFiles(directory, "*.json")
            .Select(path => JsonNode.Parse(File.ReadAllText(path))!).ToArray();
        var starts = events.Where(entry => entry["kind"]!.GetValue<string>() == "started"
            && entry["task"]!.GetValue<string>() == "SyntheticEvidenceTask").ToArray();
        var targets = Directory.EnumerateFiles(Path.Combine(fixture.Session, "private/events"), "*.json")
            .Select(path => JsonNode.Parse(File.ReadAllText(path))!)
            .Where(entry => entry["operation"]!.GetValue<string>() == "target-event")
            .Select(entry => entry["value"]!).ToArray();
        await Assert.That(starts.Length).IsEqualTo(2);
        await Assert.That(starts.Select(entry => entry["id"]!.GetValue<string>()).Distinct().Count()).IsEqualTo(2);
        await Assert.That(starts.Select(entry => entry["target"]!.GetValue<string>())).IsEquivalentTo(["First", "Build"]);
        foreach (var start in starts)
        {
            string id = start["id"]!.GetValue<string>();
            string targetId = $"{start["graph"]!.GetValue<string>()}/{start["node"]!.GetValue<int>()}/{start["projectContext"]!.GetValue<int>()}/{start["targetContext"]!.GetValue<int>()}";
            var target = targets.First(entry => entry["id"]!.GetValue<string>() == targetId);
            await Assert.That(start["project"]!.GetValue<string>()).IsEqualTo(target["project"]!.GetValue<string>());
            await Assert.That(start["recipe"]!.GetValue<string>()).IsEqualTo("repo/main.proj");
            await Assert.That(start["selection"]!.GetValue<string>()).IsEqualTo("unavailable");
            await Assert.That(start["outcome"]!.GetValue<string>()).IsEqualTo("unavailable");
            var correlated = events.Where(entry => entry["id"]!.GetValue<string>() == id).ToArray();
            await Assert.That(correlated.Any(entry => entry["kind"]!.GetValue<string>() == "rejected")).IsTrue();
            var finished = correlated.SingleOrDefault(entry => entry["kind"]!.GetValue<string>() == "finished");
            if (abort && start["target"]!.GetValue<string>() == "Build")
                await Assert.That(finished is null).IsTrue();
            else
            {
                await Assert.That(finished is not null).IsTrue();
                await Assert.That(finished!["succeeded"]!.GetValue<bool>()).IsEqualTo(
                    start["target"]!.GetValue<string>() == "First" || succeeds);
                await Assert.That(finished["unobservedParameters"]!.GetValue<string>()).IsEqualTo("unavailable");
            }
            var parameters = correlated.Where(entry => entry["kind"]!.GetValue<string>() == "parameter").ToArray();
            await Assert.That(parameters.Any(entry => entry["name"]!.GetValue<string>() == "Label")).IsTrue();
            await Assert.That(parameters.Any(entry => entry["name"]!.GetValue<string>() == "Optional")).IsFalse();
            if (start["target"]!.GetValue<string>() == "Build" && payloadLength > 0)
            {
                var payload = parameters.Single(entry => entry["name"]!.GetValue<string>() == "Payload")["items"]![0]!;
                await Assert.That(payload["type"]!.GetValue<string>()).IsEqualTo("System.String");
                await Assert.That(payload["availability"]!.GetValue<string>()).IsEqualTo(payloadLength > 16384 ? "size-limit" : "observed");
                if (payloadLength > 16384)
                    await Assert.That(payload["value"] is null).IsTrue();
                else
                    await Assert.That(payload["value"]!.GetValue<string>().Length).IsEqualTo(16384);
            }
            var declarations = correlated.Single(entry => entry["kind"]!.GetValue<string>() == "declarations")["candidates"]!.AsArray();
            await Assert.That(declarations.Count).IsEqualTo(2);
            await Assert.That(declarations.Any(candidate => candidate!["condition"]!.GetValue<string>() == "inactive")).IsTrue();
            await Assert.That(declarations.Any(candidate => candidate!["factory"]?.GetValue<string>() == "RoslynCodeTaskFactory")).IsTrue();
        }
    }

    [Test]
    public async Task RejectedFactoryDistinguishesDeclarationBindingsWithoutExposingReferencesOrSource()
    {
        using var fixture = new Fixture();
        fixture.Write("first.dll", "first synthetic assembly");
        fixture.Write("second.dll", "second synthetic assembly");
        const string parameters = """<ParameterGroup><Label ParameterType="System.String" /></ParameterGroup>""";
        const string body = """
            <Task><Code Type="Fragment" Language="cs"><![CDATA[
              System.IO.File.WriteAllText("effect.txt", "synthetic-body-one");
            ]]></Code></Task>
            """;
        static string Declaration(string name, string binding, string initialization, string group) => $$"""
            <UsingTask TaskName="{{name}}.SyntheticBindingTask" TaskFactory="RoslynCodeTaskFactory"
              {{binding}} Condition="'false' == 'true'">{{group}}{{initialization}}</UsingTask>
            """;
        fixture.Write("main.proj", $$"""
            <Project>
              <UsingTask TaskName="SyntheticBindingTask" TaskFactory="RoslynCodeTaskFactory"
                AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">{{parameters}}{{body}}</UsingTask>
            """ + Declaration("First", """AssemblyFile="first.dll" """, body, parameters)
                + Declaration("Second", """AssemblyFile="second.dll" """, body, parameters)
                + Declaration("Body", """AssemblyFile="first.dll" """,
                    body.Replace("synthetic-body-one", "synthetic-body-two", StringComparison.Ordinal), parameters)
                + Declaration("Parameters", """AssemblyFile="first.dll" """, body,
                    """<ParameterGroup><Label ParameterType="System.Int32" /></ParameterGroup>""")
                + Declaration("Name", """AssemblyName="Synthetic.Private.Assembly, Version=1.0.0.0" """, body, parameters)
                + """
                  <UsingTask TaskName="ExactFactory.SyntheticBindingTask"
                    TaskFactory="Microsoft.Build.Tasks.RoslynCodeTaskFactory"
                    AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll"
                    Condition="'false' == 'true'" />
                  """
                + Declaration("Missing", """AssemblyFile="/synthetic-private/missing.dll" """, body, parameters)
                + Declaration("Absent", """AssemblyFile="first.dll" """, "", "")
                + Declaration("Large", """AssemblyFile="first.dll" """,
                    "<Task>" + new string('x', 16385) + "</Task>", parameters)
                + Declaration("ManyParameters", """AssemblyFile="first.dll" """, body,
                    "<ParameterGroup>" + string.Concat(Enumerable.Range(0, 129)
                        .Select(index => $"<P{index} ParameterType=\"System.String\" />")) + "</ParameterGroup>") + """
              <Target Name="Build"><SyntheticBindingTask Label="synthetic" /></Target>
            </Project>
            """);
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.Output).Contains("CAPTURE_TASK_HOST_UNSUPPORTED");
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "work/repo/effect.txt")))
            .IsEqualTo("synthetic-body-one");
        var records = Directory.EnumerateFiles(Path.Combine(fixture.Session, "private/rejections"), "*.json")
            .Select(path => JsonNode.Parse(File.ReadAllText(path))!).ToArray();
        var declarations = records.Single(record => record["kind"]!.GetValue<string>() == "declarations");
        await Assert.That(declarations["completeness"]!.GetValue<string>()).IsEqualTo("completed");
        await Assert.That(declarations["selection"]!.GetValue<string>()).IsEqualTo("unavailable");
        var candidates = declarations["candidates"]!.AsArray();
        await Assert.That(candidates.Count).IsEqualTo(11);
        foreach (var candidate in candidates)
        {
            await Assert.That(candidate!["selection"]!.GetValue<string>()).IsEqualTo("unavailable");
            await Assert.That(candidate["declarationMetadata"] is JsonObject)
                .IsTrue().Because("Rejected declarations must retain assembly, initialization and parameter identities.");
        }
        JsonNode Metadata(string name) => candidates.Single(candidate =>
            candidate!["task"]!.GetValue<string>() == name + ".SyntheticBindingTask")!["declarationMetadata"]!;
        var first = Metadata("First");
        await Assert.That(first["assemblyKind"]!.GetValue<string>()).IsEqualTo("AssemblyFile");
        await Assert.That(first["assembly"]!["availability"]!.GetValue<string>()).IsEqualTo("observed");
        await Assert.That(first["assembly"]!["path"]!.GetValue<string>()).IsEqualTo("repo/first.dll");
        await Assert.That(first["assembly"]!["sha256"]!.GetValue<string>())
            .IsNotEqualTo(Metadata("Second")["assembly"]!["sha256"]!.GetValue<string>());
        await Assert.That(first["factoryTypeDefinition"] is JsonObject).IsTrue()
            .Because("Assembly bytes alone do not establish the declared factory type definition.");
        await Assert.That(first["factoryTypeDefinition"]!["availability"]!.GetValue<string>()).IsEqualTo("unavailable");
        await Assert.That(Metadata("ExactFactory")["factoryTypeDefinition"]!["availability"]!.GetValue<string>()).IsEqualTo("observed");
        await Assert.That(first["parameterDeclarations"] is JsonObject).IsTrue()
            .Because("A parameter-group hash does not identify its declared parameter types.");
        await Assert.That(first["parameterDeclarations"]!["availability"]!.GetValue<string>()).IsEqualTo("observed");
        var declared = first["parameterDeclarations"]!["parameters"]!.AsArray().Single()!;
        await Assert.That(declared["name"]!.GetValue<string>()).IsEqualTo("Label");
        await Assert.That(declared["type"]!.GetValue<string>()).IsEqualTo("System.String");
        await Assert.That(declared["required"]!.GetValue<bool>()).IsFalse();
        await Assert.That(declared["output"]!.GetValue<bool>()).IsFalse();
        await Assert.That(Metadata("Parameters")["parameterDeclarations"]!["parameters"]![0]!["type"]!.GetValue<string>())
            .IsEqualTo("System.Int32");
        await Assert.That(Metadata("Absent")["parameterDeclarations"]!["availability"]!.GetValue<string>()).IsEqualTo("absent");
        await Assert.That(Metadata("ManyParameters")["parameterDeclarations"]!["availability"]!.GetValue<string>()).IsEqualTo("count-limit");
        await Assert.That(Metadata("ManyParameters")["parameterDeclarations"]!["parameters"]!.AsArray()).IsEmpty();
        await Assert.That(first["initialization"]!["availability"]!.GetValue<string>()).IsEqualTo("observed");
        await Assert.That(first["initialization"]!["sha256"]!.GetValue<string>())
            .IsNotEqualTo(Metadata("Body")["initialization"]!["sha256"]!.GetValue<string>());
        await Assert.That(first["parameterGroup"]!["sha256"]!.GetValue<string>())
            .IsNotEqualTo(Metadata("Parameters")["parameterGroup"]!["sha256"]!.GetValue<string>());
        await Assert.That(first["initialization"]!["sha256"]!.GetValue<string>())
            .IsEqualTo(Metadata("Second")["initialization"]!["sha256"]!.GetValue<string>());
        await Assert.That(first["parameterGroup"]!["sha256"]!.GetValue<string>())
            .IsEqualTo(Metadata("Body")["parameterGroup"]!["sha256"]!.GetValue<string>());
        await Assert.That(Metadata("Name")["assemblyKind"]!.GetValue<string>()).IsEqualTo("AssemblyName");
        await Assert.That(Metadata("Name")["assemblyReference"]!["availability"]!.GetValue<string>()).IsEqualTo("observed");
        foreach (string name in new[] { "Name", "Missing" })
        {
            await Assert.That(Metadata(name)["assembly"]!["availability"]!.GetValue<string>()).IsEqualTo("unavailable");
            await Assert.That(Metadata(name)["assembly"]!["sha256"] is null).IsTrue();
        }
        await Assert.That(Metadata("Absent")["initialization"]!["availability"]!.GetValue<string>()).IsEqualTo("absent");
        await Assert.That(Metadata("Absent")["parameterGroup"]!["availability"]!.GetValue<string>()).IsEqualTo("absent");
        await Assert.That(Metadata("Large")["initialization"]!["availability"]!.GetValue<string>()).IsEqualTo("size-limit");
        await Assert.That(Metadata("Large")["initialization"]!["sha256"] is null).IsTrue();
        string safe = declarations.ToJsonString();
        foreach (string excluded in new[] { fixture.Root, fixture.Session, "/synthetic-private",
            "Synthetic.Private.Assembly", "synthetic-body-one", "synthetic-body-two", "ParameterType", "MSBuildToolsPath" })
            await Assert.That(safe.Contains(excluded, StringComparison.Ordinal)).IsFalse();
        var verification = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
        await Assert.That(verification.ExitCode).IsNotEqualTo(0);
    }

    [Test]
    [Arguments("completed")]
    [Arguments("truncated")]
    [Arguments("failed")]
    public async Task RejectedFactoryRetainsLaterDeclarationEvidence(string scenario)
    {
        using var fixture = new Fixture();
        string later = """
            <UsingTask TaskName="Later.SyntheticEnumerationTask" TaskFactory="LaterFactory"
              AssemblyFile="never-selected.dll" Runtime="DifferentRuntime" Architecture="x86" />
            """;
        string unavailable = """
            <UsingTask TaskName="Unavailable.SyntheticEnumerationTask" TaskFactory="UnavailableFactory"
              AssemblyFile="never-selected.dll" Condition="'@(MissingItems)' == ''" />
            """;
        fixture.Write("main.proj", """
            <Project>
              <UsingTask TaskName="SyntheticEnumerationTask" TaskFactory="RoslynCodeTaskFactory"
                AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">
                <Task><Code Type="Fragment" Language="cs"><![CDATA[
                  System.IO.File.WriteAllText("effect.txt", "first-party factory executed");
                ]]></Code></Task>
              </UsingTask>
            """ + (scenario == "failed" ? unavailable : "")
                + string.Concat(Enumerable.Repeat(later, scenario == "truncated" ? 129 : 1)) + """
              <Target Name="Build"><SyntheticEnumerationTask /></Target>
            </Project>
            """);
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
        var records = Directory.EnumerateFiles(Path.Combine(fixture.Session, "private/rejections"), "*.json")
            .Select(path => JsonNode.Parse(File.ReadAllText(path))!).ToArray();
        var declarations = records.Single(record => record["kind"]!.GetValue<string>() == "declarations");
        await Assert.That(declarations["completeness"]!.GetValue<string>()).IsEqualTo(scenario);
        await Assert.That(declarations["selection"]!.GetValue<string>()).IsEqualTo("unavailable");
        var candidates = declarations["candidates"]!.AsArray();
        await Assert.That(candidates.Count).IsEqualTo(scenario == "truncated" ? 128 : scenario == "failed" ? 3 : 2);
        await Assert.That(candidates.Any(candidate => candidate!["task"]!.GetValue<string>() == "Later.SyntheticEnumerationTask"
            && candidate["factory"]!.GetValue<string>() == "LaterFactory"
            && candidate["runtime"]!.GetValue<string>() == "DifferentRuntime"
            && candidate["architecture"]!.GetValue<string>() == "x86")).IsTrue();
        if (scenario == "failed")
            await Assert.That(candidates.Any(candidate => candidate!["task"]!.GetValue<string>() == "Unavailable.SyntheticEnumerationTask"
                && candidate["condition"]!.GetValue<string>() == "unavailable")).IsTrue();
        else
        {
            await Assert.That(result.Output).Contains("CAPTURE_TASK_HOST_UNSUPPORTED");
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "work/repo/effect.txt")))
                .IsEqualTo("first-party factory executed");
        }
    }

    [Test]
    public async Task RejectionCandidateLimitDoesNotBypassLaterHostValidation()
    {
        using var fixture = new Fixture();
        fixture.Write("input.txt", "synthetic bounded candidate input");
        string inactive = """
            <UsingTask TaskName="Copy" AssemblyFile="inactive.dll" Condition="'false' == 'true'" />
            """;
        fixture.Write("main.proj", "<Project>" + string.Concat(Enumerable.Repeat(inactive, 128)) + """
            <UsingTask TaskName="Copy" AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll"
              TaskFactory="TaskHostFactory" Runtime="NET" Architecture="CurrentArchitecture" Override="true" />
            <Target Name="Build"><Copy SourceFiles="input.txt" DestinationFiles="out/result.txt" /></Target>
            </Project>
            """);
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.Output).Contains("CAPTURE_TASK_HOST_UNSUPPORTED");
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
        var records = Directory.EnumerateFiles(Path.Combine(fixture.Session, "private/rejections"), "*.json")
            .Select(path => JsonNode.Parse(File.ReadAllText(path))!).ToArray();
        var declarations = records.Single(record => record["kind"]!.GetValue<string>() == "declarations");
        await Assert.That(declarations["candidates"]!.AsArray().Count).IsEqualTo(128);
        await Assert.That(declarations["completeness"]!.GetValue<string>()).IsEqualTo("truncated");
    }

    [Test]
    [Arguments("GenerateBlazorBootExtensionJson",
        "sdk/sdk/10.0.404/Sdks/Microsoft.NET.Sdk.BlazorWebAssembly/tools/net10.0/Microsoft.NET.Sdk.BlazorWebAssembly.Tasks.dll", true)]
    [Arguments("MonoAOTCompiler",
        "sdk/packs/Microsoft.NET.Runtime.MonoAOTCompiler.Task/10.0.9/tasks/net10.0/MonoAOTCompiler.dll", false)]
    [Arguments("ComputeManagedAssemblies",
        "package/microsoft.net.illink.tasks/10.0.10/tools/net/ILLink.Tasks.dll", true)]
    public async Task GeneratedInventoryIncludesDeclaredShapesFromFrozenSdk(string task, string assembly, bool required)
    {
        using var fixture = new Fixture();
        fixture.Write("main.proj", """<Project><Target Name="Build" /></Project>""");
        if (assembly.StartsWith("package/", StringComparison.Ordinal))
            fixture.Write("packages.lock.json", await File.ReadAllTextAsync(
                Path.Combine(ReleaseTestPaths.Root, "src/Configurator.Browser/packages.lock.json")));
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        string frozenAssembly = Path.Combine(fixture.Session, "frozen", assembly);
        bool present = File.Exists(frozenAssembly);
        if (required) await Assert.That(present).IsTrue();
        var inventory = JsonNode.Parse(await File.ReadAllTextAsync(
            Path.Combine(fixture.Session, "private/task-parameters.json")))!.AsArray();
        var shapes = inventory.Where(shape => shape!["name"]!.GetValue<string>() == task
            && shape["assembly"]!.GetValue<string>() == frozenAssembly).ToArray();
        Console.WriteLine($"SDK_TASK_SHAPES {task} present={present} count={shapes.Length} inventory={inventory.Count}");
        await Assert.That(shapes.Length).IsEqualTo(present ? 1 : 0);
        if (present)
        {
            await Assert.That(shapes[0]!["type"]!.GetValue<string>()).EndsWith(task);
            await Assert.That(shapes[0]!["parameters"]!.AsArray().Count).IsGreaterThan(0);
            var capture = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!;
            var input = capture["inputs"]!.AsArray().SingleOrDefault(input =>
                input!["path"]!.GetValue<string>() == assembly && input["kind"]!.GetValue<string>() == "tool");
            await Assert.That(input is not null).IsTrue()
                .Because("Inspecting public task shapes consumes assembly bytes that must remain bound in the sealed input closure.");
            await Assert.That(input!["sha256"]!.GetValue<string>())
                .IsEqualTo(ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(frozenAssembly)));
        }
    }

    [Test]
    public async Task PublicTaskMetadataDoesNotAdmitAnUnknownConsumer()
    {
        using var fixture = new Fixture();
        fixture.Write("main.proj", """
            <Project>
              <UsingTask TaskName="CreateItem" AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll" />
              <ItemGroup>
                <Probe Include="synthetic-item">
                  <AssetKind>synthetic-kind-value</AssetKind>
                  <SourceId>synthetic-source-value</SourceId>
                </Probe>
              </ItemGroup>
              <Target Name="Build"><CreateItem Include="@(Probe)" /></Target>
            </Project>
            """);
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.Output).Contains("CAPTURE_TASK_UNSUPPORTED_CreateItem");
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
        var records = Directory.EnumerateFiles(Path.Combine(fixture.Session, "private/rejections"), "*.json")
            .Select(path => JsonNode.Parse(File.ReadAllText(path))!).ToArray();
        var parameter = records.Single(record => record["kind"]!.GetValue<string>() == "parameter"
            && record["name"]!.GetValue<string>() == "Include"
            && record["direction"]!.GetValue<string>() == "TaskInput");
        var item = parameter["items"]!.AsArray().Single()!;
        await Assert.That(item["metadataNames"] is not null).IsTrue()
            .Because("A refused consumer's observed item shape must expose metadata names without exposing their values.");
        var metadata = item["metadataNames"]!;
        await Assert.That(metadata["availability"]!.GetValue<string>()).IsEqualTo("observed");
        var names = metadata["names"]!.AsArray().Select(name => name!.GetValue<string>()).ToArray();
        await Assert.That(names).Contains("AssetKind");
        await Assert.That(names).Contains("SourceId");
        await Assert.That(metadata.ToJsonString()).DoesNotContain("synthetic-kind-value");
        await Assert.That(metadata.ToJsonString()).DoesNotContain("synthetic-source-value");
    }

    [Test]
    [Arguments(129, false, "count-limit")]
    [Arguments(1, true, "size-limit")]
    public async Task RefusedItemMetadataNamesRemainBounded(int count, bool longName, string availability)
    {
        using var fixture = new Fixture();
        fixture.Write("main.proj", new XElement("Project",
            new XElement("UsingTask", new XAttribute("TaskName", "CreateItem"),
                new XAttribute("AssemblyFile", "$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll")),
            new XElement("ItemGroup", new XElement("Probe", new XAttribute("Include", "synthetic-item"),
                Enumerable.Range(0, count).Select(index => new XElement(longName ? new string('m', 257) : "M" + index,
                    "synthetic-private-metadata-value")))),
            new XElement("Target", new XAttribute("Name", "Build"),
                new XElement("CreateItem", new XAttribute("Include", "@(Probe)")))).ToString());
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.Output).Contains("CAPTURE_TASK_UNSUPPORTED_CreateItem");
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
        var records = Directory.EnumerateFiles(Path.Combine(fixture.Session, "private/rejections"), "*.json")
            .Select(path => JsonNode.Parse(File.ReadAllText(path))!).ToArray();
        var parameter = records.Single(record => record["kind"]!.GetValue<string>() == "parameter"
            && record["name"]!.GetValue<string>() == "Include"
            && record["direction"]!.GetValue<string>() == "TaskInput");
        var metadata = parameter["items"]!.AsArray().Single()!["metadataNames"]!;
        await Assert.That(metadata["availability"]!.GetValue<string>()).IsEqualTo(availability);
        var names = metadata["names"]!.AsArray().Select(name => name!.GetValue<string>()).ToArray();
        await Assert.That(names.All(name => name.Length <= 256)).IsTrue();
        if (longName) await Assert.That(names).DoesNotContain(new string('m', 257));
        else await Assert.That(names.Length).IsEqualTo(128);
        await Assert.That(metadata.ToJsonString()).DoesNotContain("synthetic-private-metadata-value");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ChildTargetSkippingIsTypedAndDoesNotFabricateExecution(bool skip)
    {
        using var fixture = new Fixture();
        fixture.Write("child.proj", """
            <Project><Target Name="Build"><WriteLinesToFile File="required.bin" Lines="required output" /></Target></Project>
            """);
        fixture.Write("main.proj", $"""
            <Project><Target Name="Build">
              <MSBuild Projects="child.proj" Targets="Missing" SkipNonexistentTargets="{skip}" />
            </Target></Project>
            """);
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode == 0).IsEqualTo(skip).Because(result.Output);
        await Assert.That(File.Exists(Path.Combine(fixture.Session, "work/repo/required.bin"))).IsFalse();
        if (!skip)
        {
            await Assert.That(File.Exists(fixture.Record)).IsFalse();
            return;
        }
        var capture = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!;
        var invocation = capture["invocations"]!.AsArray()
            .Single(invocation => invocation!["task"]!.GetValue<string>() == "MSBuild")!;
        var setting = invocation["parameters"]!.AsArray()
            .Single(parameter => parameter!["name"]!.GetValue<string>() == "SkipNonexistentTargets")!;
        await Assert.That(setting["type"]!.GetValue<string>()).IsEqualTo("System.Boolean");
        await Assert.That(setting["value"]!.GetValue<bool>()).IsTrue();
        var targets = invocation["parameters"]!.AsArray()
            .Single(parameter => parameter!["name"]!.GetValue<string>() == "Targets")!["value"]!.AsArray();
        await Assert.That(targets.Select(target => target!.GetValue<string>())).IsEquivalentTo(["Missing"]);
        await Assert.That(capture["targets"]!.AsArray().Any(target => target!["target"]!.GetValue<string>() == "Missing"
            && target["outcome"]!.GetValue<string>() == "succeeded")).IsFalse();
    }

    [Test]
    public async Task RepeatedGeneratedReadsBindOneFrozenByteVersion()
    {
        using var fixture = new Fixture();
        fixture.Write("main.proj", """
            <Project><Target Name="Build">
              <MakeDir Directories="obj" />
              <WriteLinesToFile File="obj/consumed.txt" Lines="first" Overwrite="true" />
              <Copy SourceFiles="obj/consumed.txt" DestinationFiles="out/first.txt" />
              <Copy SourceFiles="obj/consumed.txt" DestinationFiles="out/second.txt" />
            </Target></Project>
            """);
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        var record = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!;
        var generated = record["inputs"]!.AsArray().Where(input =>
            input!["kind"]!.GetValue<string>() == "generated"
            && input["path"]!.GetValue<string>().EndsWith("/obj/consumed.txt", StringComparison.Ordinal)).ToArray();
        await Assert.That(generated.Length).IsEqualTo(1);
        string path = Path.Combine(fixture.Session, "frozen", generated[0]!["path"]!.GetValue<string>());
        await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("first" + Environment.NewLine);
        await File.WriteAllTextAsync(path, "substituted frozen generated input");
        var rejected = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
        await Assert.That(rejected.ExitCode).IsNotEqualTo(0);
    }

    [Test]
    public async Task VerificationUsesOnlyTheRuntimeAndStagedCaptureTool()
    {
        using var fixture = new Fixture();
        fixture.Write("input.txt", "synthetic runtime-only verification");
        fixture.Write("main.proj", """
            <Project><Target Name="Build"><Copy SourceFiles="input.txt" DestinationFiles="out/result.txt" /></Target></Project>
            """);
        var captured = await fixture.Capture("main.proj", "Build");
        await Assert.That(captured.ExitCode).IsEqualTo(0).Because(captured.Output);
        var verified = await fixture.VerifyWithoutSdk("--verify", fixture.Session);
        await Assert.That(verified.ExitCode).IsEqualTo(0).Because(verified.Output);
        await Assert.That(verified.Output).Contains("CAPTURE_VALID");
    }

    [Test]
    [Arguments("freeze")]
    [Arguments("outcome")]
    [Arguments("identity")]
    public async Task EachInvocationRequiresItsOwnFrozenInputsIdentityAndOutcome(string attack)
    {
        using var fixture = new Fixture();
        fixture.Write("first.txt", "first synthetic input");
        fixture.Write("second.txt", "second synthetic input");
        fixture.Write("main.proj", """
            <Project><Target Name="Build">
              <Copy SourceFiles="first.txt" DestinationFiles="out/first.txt" />
              <Copy SourceFiles="second.txt" DestinationFiles="out/second.txt" />
            </Target></Project>
            """);
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        var capture = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!;
        var copies = capture["invocations"]!.AsArray()
            .Where(invocation => invocation!["task"]!.GetValue<string>() == "Copy").ToArray();
        await Assert.That(copies.Length).IsEqualTo(2);
        await Assert.That(copies.Select(invocation => invocation!["id"]!.GetValue<string>()).Distinct().Count()).IsEqualTo(2);
        var sourceParameters = copies.Select(invocation => invocation!["parameters"]!.AsArray()
            .Single(parameter => parameter!["name"]!.GetValue<string>() == "SourceFiles")!).ToArray();
        await Assert.That(sourceParameters.All(parameter => parameter["type"]!.GetValue<string>()
            == "Microsoft.Build.Framework.ITaskItem[]")).IsTrue();
        await Assert.That(sourceParameters.SelectMany(parameter => parameter["inputs"]!.AsArray())
            .Select(input => input!["path"]!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray())
            .IsEquivalentTo(["repo/first.txt", "repo/second.txt"]);

        string id = copies[0]!["id"]!.GetValue<string>();
        foreach (string path in Directory.EnumerateFiles(Path.Combine(fixture.Session, "private/events"), "*.json"))
        {
            var entry = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            if (entry["path"]!.GetValue<string>() != id) continue;
            string operation = entry["operation"]!.GetValue<string>();
            if (attack == "freeze" && operation == "invocation-freeze")
            {
                File.Delete(path);
                break;
            }
            if (attack != "freeze" && operation == "invocation-finished")
            {
                if (attack == "outcome") entry["value"]!["succeeded"] = false;
                else entry["value"]!["project"] = "repo/substituted.proj";
                await File.WriteAllTextAsync(path, entry.ToJsonString());
                break;
            }
        }
        var rejected = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
        await Assert.That(rejected.ExitCode).IsNotEqualTo(0);
        await Assert.That(rejected.Output).Contains("CAPTURE_INVOCATION_RECEIPT_INVALID");
    }

    [Test]
    public async Task EvaluatedGraphIncludesLateSourcesImportsResourcesAndTransitiveProjects()
    {
        using var fixture = new Fixture();
        fixture.Write("main.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
              <Import Project="late.targets" />
              <ItemGroup><ProjectReference Include="child/child.csproj" /><EmbeddedResource Include="resource.txt" /></ItemGroup>
            </Project>
            """);
        fixture.Write("late.targets", """
            <Project><Target Name="LateSource" BeforeTargets="CoreCompile">
              <ItemGroup><Compile Include="late.cs" /></ItemGroup>
            </Target></Project>
            """);
        fixture.Write("late.cs", "public sealed class Parent { public Child Value { get; } = new(); }");
        fixture.Write("resource.txt", "synthetic resource");
        fixture.Write("child/child.csproj", """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
            """);
        fixture.Write("child/Child.cs", "public sealed class Child;");
        var result = await fixture.Capture("main.csproj", "Build");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        await Assert.That(File.Exists(fixture.Record)).IsTrue()
            .Because("A successful build without an evaluated closure cannot qualify its inputs.");
        var capture = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!;
        var inputs = capture["inputs"]!.AsArray().Select(item => item!["path"]!.GetValue<string>()).ToArray();
        foreach (string path in new[] { "repo/main.csproj", "repo/late.targets", "repo/late.cs",
            "repo/resource.txt", "repo/child/child.csproj", "repo/child/Child.cs" })
            await Assert.That(inputs).Contains(path);
        await Assert.That(inputs.Any(path => path.Contains("Microsoft.CodeAnalysis.NetAnalyzers.dll", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    [Arguments("subprocess")]
    [Arguments("mutation")]
    [Arguments("unsafe-alias")]
    public async Task UnadmittedExecutionCannotProduceASealedCapture(string attack)
    {
        using var fixture = new Fixture();
        fixture.Write("input.txt", "original");
        string operation = attack switch
        {
            "subprocess" => """<Exec Command="dotnet --version" />""",
            "mutation" => """<WriteLinesToFile File="input.txt" Lines="changed" Overwrite="true" />""",
            "unsafe-alias" => """<Copy SourceFiles="escape.txt" DestinationFiles="out/result.txt" />""",
            _ => throw new InvalidOperationException()
        };
        fixture.Write("main.proj", $"""
            <Project><Target Name="Build">{operation}</Target></Project>
            """);
        if (attack == "unsafe-alias")
            File.CreateSymbolicLink(Path.Combine(fixture.Root, "escape.txt"), Path.Combine(fixture.Root, "input.txt"));
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsNotEqualTo(0)
            .Because("Capture admission must reject the executed attack, rather than endorse its successful build.");
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
    }

    [Test]
    [Arguments("omission")]
    [Arguments("substitution")]
    [Arguments("package-byte")]
    public async Task SealedCaptureRejectsOmittedSubstitutedAndChangedDependencyBytes(string attack)
    {
        using var fixture = new Fixture();
        fixture.Write("main.proj", """
            <Project><Target Name="Build"><Copy SourceFiles="input.txt" DestinationFiles="out/result.txt" /></Target></Project>
            """);
        fixture.Write("input.txt", "first input");
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        await Assert.That(File.Exists(fixture.Record)).IsTrue();
        var valid = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
        await Assert.That(valid.ExitCode).IsEqualTo(0).Because(valid.Output);
        var capture = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!;
        if (attack == "omission") capture["inputs"]!.AsArray().RemoveAt(0);
        if (attack == "substitution") capture["inputs"]![0]!["sha256"] = new string('0', 64);
        if (attack == "package-byte")
            await File.WriteAllTextAsync(Path.Combine(fixture.Session, "frozen/repo/input.txt"), "changed dependency");
        await File.WriteAllTextAsync(fixture.Record, capture.ToJsonString());
        await File.WriteAllTextAsync(Path.Combine(fixture.Session, "capture.sha256"),
            ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(fixture.Record)));
        var check = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
        await Assert.That(check.ExitCode).IsNotEqualTo(0).Because(attack);
    }

    [Test]
    public async Task ControlledCheckRequiresCapturedExecutableInsteadOfAcceptingASuppliedReport()
    {
        using var fixture = new Fixture();
        fixture.Write("main.proj", """<Project><Target Name="Build" /></Project>""");
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        await Assert.That(File.Exists(fixture.Record)).IsTrue()
            .Because("A controlled check must bind to an actual evaluated capture before selecting an executable.");
        var check = await ReleaseTestPaths.Run("dotnet", Tool, "--run-check", fixture.Session,
            "Configurator.Core", Path.Combine(fixture.Session, "checks"));
        await Assert.That(check.ExitCode).IsNotEqualTo(0);
        await Assert.That(check.Output).Contains("CAPTURE_CHECK_EXECUTABLE_MISSING");
    }

    [Test]
    public async Task StaleIntermediatesCannotEnterANewCapture()
    {
        using var fixture = new Fixture();
        fixture.Write("main.proj", """
            <Project><Target Name="Build"><Copy SourceFiles="obj/stale.txt" DestinationFiles="out/result.txt" /></Target></Project>
            """);
        fixture.Write("obj/stale.txt", "stale previous build");
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
    }

    [Test]
    public async Task GeneratedInputMutationRejectsTheEarlierConsumedGeneration()
    {
        using var fixture = new Fixture();
        fixture.Write("main.proj", """
            <Project><Target Name="Build">
              <MakeDir Directories="obj" />
              <WriteLinesToFile File="obj/consumed.txt" Lines="first" Overwrite="true" />
              <Copy SourceFiles="obj/consumed.txt" DestinationFiles="out/consumed.txt" />
              <WriteLinesToFile File="obj/consumed.txt" Lines="second" Overwrite="true" />
            </Target></Project>
            """);
        var result = await fixture.Capture("main.proj", "Build");
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "work/repo/out/consumed.txt")))
            .IsEqualTo("first" + Environment.NewLine);
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(fixture.Session, "work/repo/obj/consumed.txt")))
            .IsEqualTo("second" + Environment.NewLine);
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(File.Exists(fixture.Record)).IsFalse();
    }

    [Test]
    public async Task ChangedConsumedPackageBytesInvalidateTheSealedGraph()
    {
        using var fixture = new Fixture();
        fixture.Write("main.csproj", await File.ReadAllTextAsync(Path.Combine(ReleaseTestPaths.Root,
            "src/Configurator.Core/Configurator.Core.csproj")));
        foreach (string name in new[] { "Directory.Build.props", "Directory.Packages.props", "NuGet.Config" })
            fixture.Write(name, await File.ReadAllTextAsync(Path.Combine(ReleaseTestPaths.Root, name)));
        fixture.Write("packages.lock.json", await File.ReadAllTextAsync(Path.Combine(ReleaseTestPaths.Root,
            "src/Configurator.Core/packages.lock.json")));
        fixture.Write("Example.cs", "namespace Configurator.CaptureFixture; public sealed class Example;");
        var result = await fixture.Capture("main.csproj", "Build");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        var valid = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
        await Assert.That(valid.ExitCode).IsEqualTo(0).Because(valid.Output);
        var record = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Record))!;
        string package = record["inputs"]!.AsArray().Select(input => input!["path"]!.GetValue<string>())
            .Single(path => path.StartsWith("package/yamldotnet/", StringComparison.Ordinal)
                && path.EndsWith("/YamlDotNet.dll", StringComparison.Ordinal));
        string frozen = Path.Combine(fixture.Session, "frozen", package);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(frozen, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        await File.AppendAllTextAsync(frozen, "synthetic package mutation");
        var changed = await ReleaseTestPaths.Run("dotnet", Tool, "--verify", fixture.Session);
        await Assert.That(changed.ExitCode).IsNotEqualTo(0);
        await Assert.That(changed.Output).Contains("CAPTURE_BYTES_CHANGED");
    }

    [Test]
    [Arguments("focused")]
    [Arguments("missing")]
    [Arguments("failed")]
    [Arguments("skipped")]
    [Arguments("unbound")]
    [Arguments("report-substitution")]
    public async Task ControlledExecutionCannotPromoteIncompleteOrSubstitutedEvidence(string scenario)
    {
        using var fixture = new Fixture();
        const string project = "tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj";
        fixture.Write(project, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
                <IsTestProject>true</IsTestProject><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup><PackageReference Include="TUnit" /></ItemGroup>
            </Project>
            """);
        foreach (string name in new[] { "Directory.Build.props", "Directory.Packages.props", "NuGet.Config", "global.json", ".editorconfig" })
            fixture.Write(name, await File.ReadAllTextAsync(Path.Combine(ReleaseTestPaths.Root, name)));
        fixture.Write(".ci/scripts/prepare-test-evidence.cs",
            await File.ReadAllTextAsync(Path.Combine(ReleaseTestPaths.Root, ".ci/scripts/prepare-test-evidence.cs")));
        string attribute = scenario == "skipped" ? """[Skip("Synthetic skipped-execution counterexample")]""" : "";
        string body = scenario == "failed"
            ? """await Task.Yield(); throw new InvalidOperationException("Synthetic failed-execution counterexample");"""
            : """await Assert.That(typeof(SyntheticChecks).Assembly.GetName().Name).IsEqualTo("Configurator.Core.Tests");""";
        fixture.Write("tests/Configurator.Core.Tests/SyntheticChecks.cs", $$"""
            namespace Configurator.Core.Tests;
            public sealed class SyntheticChecks
            {
                [Test] public async Task Control()
                {
                    await Assert.That(typeof(SyntheticChecks).Assembly.GetName().Name).IsEqualTo("Configurator.Core.Tests");
                }
                [Test] {{attribute}}
                public async Task Scenario() { {{body}} }
            }
            """);
        var restored = await ReleaseTestPaths.RunInDirectory(fixture.Root, "dotnet", "restore", project, "--verbosity", "quiet");
        await Assert.That(restored.ExitCode).IsEqualTo(0).Because(restored.Output + restored.StandardError);
        var captured = await fixture.Capture(project, "Build");
        await Assert.That(captured.ExitCode).IsEqualTo(0).Because(captured.Output);
        string output = Path.Combine(fixture.Session, "checks");
        var arguments = new List<string> { Tool, "--run-check", fixture.Session, "Configurator.Core", output };
        if (scenario is "focused" or "missing")
            arguments.AddRange(["--filter", scenario == "focused" ? "/*/*/SyntheticChecks/Control" : "/*/*/AbsentChecks/*"]);
        var ran = await ReleaseTestPaths.Run("dotnet", arguments.ToArray());
        await Assert.That(ran.ExitCode == 0).IsEqualTo(scenario is "focused" or "unbound" or "report-substitution").Because(ran.Output);
        string executionPath = Path.Combine(output, "execution.json");
        string reportPath = Directory.EnumerateFiles(output, "Configurator.Core-*-captured.json").Single();
        var execution = JsonNode.Parse(await File.ReadAllTextAsync(executionPath))!;
        if (scenario == "focused") await Assert.That(execution["scope"]!.GetValue<string>()).IsEqualTo("focused");
        if (scenario is "failed" or "missing" or "skipped")
            await Assert.That(execution["outcome"]!.GetValue<string>()).IsEqualTo("failed");
        if (scenario == "skipped") await Assert.That(execution["skipped"]!.GetValue<long>()).IsEqualTo(1);
        if (scenario is "unbound" or "report-substitution")
        {
            var valid = await ReleaseTestPaths.Run("dotnet", Tool, "--verify-check", fixture.Session, executionPath, reportPath);
            await Assert.That(valid.ExitCode).IsEqualTo(0).Because(valid.Output);
            var staged = await fixture.VerifyWithoutSdk("--verify-check", fixture.Session, executionPath, reportPath);
            await Assert.That(staged.ExitCode).IsEqualTo(0).Because(staged.Output);
            if (scenario == "unbound")
            {
                execution["safeReportHash"] = new string('0', 64);
                await File.WriteAllTextAsync(executionPath, execution.ToJsonString());
            }
            else await File.AppendAllTextAsync(reportPath, " ");
        }
        var rejected = await ReleaseTestPaths.Run("dotnet", Tool, "--verify-check", fixture.Session, executionPath, reportPath);
        await Assert.That(rejected.ExitCode).IsNotEqualTo(0);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "configurator-capture-test-" + Guid.NewGuid().ToString("N"));
        internal string Root => Path.Combine(_directory, "source");
        internal string Session => Path.Combine(_directory, "session");
        internal string Record => Path.Combine(Session, "capture.json");

        internal async Task<string> PrepareNativeJsonProbe()
        {
            Write("input.json", """{"items":{}}""");
            Write("main.proj", """
                <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <Import Project="$(ConfiguratorCaptureOverrides)" />
                  <UsingTask TaskName="CancelledReader" AssemblyFile="$(ProbeAssembly)" Condition="'$(ProbeAssembly)' != ''" />
                  <Target Name="Prepare" />
                  <Target Name="Probe"><ReadWasmProps JsonFilePath="input.json" /><ReadWasmProps JsonFilePath="input.json" /></Target>
                  <Target Name="ProbeChild"><MSBuild Projects="child.proj" Targets="Probe"
                    RemoveProperties="CustomBeforeMicrosoftCommonTargets;CustomAfterMicrosoftCommonProps" /></Target>
                  <Target Name="ProbeProperties"><MSBuild Projects="child.proj" Targets="Probe"
                    Properties="CustomBeforeMicrosoftCommonTargets=;CustomAfterMicrosoftCommonProps=" /></Target>
                  <Target Name="ProbeMetadataRemoval">
                    <ItemGroup><RemovalProject Include="child.proj">
                      <GlobalPropertiesToRemove>CustomBeforeMicrosoftCommonTargets;CustomAfterMicrosoftCommonProps</GlobalPropertiesToRemove>
                    </RemovalProject></ItemGroup>
                    <MSBuild Projects="@(RemovalProject)" Targets="Probe" />
                  </Target>
                  <Target Name="ProbeMetadataAdditional">
                    <ItemGroup><AdditionalProject Include="child.proj">
                      <AdditionalProperties>CustomBeforeMicrosoftCommonTargets=;CustomAfterMicrosoftCommonProps=</AdditionalProperties>
                    </AdditionalProject></ItemGroup>
                    <MSBuild Projects="@(AdditionalProject)" Targets="Probe" />
                  </Target>
                  <Target Name="ProbeNativeFlags"><MSBuild Projects="child.proj" Targets="Probe"
                    RemoveProperties="RuntimeIdentifier;SelfContained;PublishAot" /></Target>
                  <Target Name="ProbeOverride"><MSBuild Projects="child-override.proj" Targets="Probe" /></Target>
                  <Target Name="ProbeImported"><MSBuild Projects="child-import.proj" Targets="Probe" /></Target>
                  <Target Name="ProbeRepeat">
                    <MSBuild Projects="child-override.proj" Targets="Probe" Properties="Attempt=first"
                      ContinueOnError="WarnAndContinue" />
                    <MSBuild Projects="child-override.proj" Targets="Probe" Properties="Attempt=second" />
                  </Target>
                  <Target Name="CancelProbe"><CancelledReader CaptureTool="$(CaptureTool)" /></Target>
                </Project>
                """);
            Write("child.proj", """
                <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <Target Name="Probe"><ReadWasmProps JsonFilePath="input.json" /></Target>
                </Project>
                """);
            const string readerDeclaration = """
                <Project>
                  <UsingTask TaskName="ReadWasmProps" TaskFactory="JsonToItemsTaskFactory.JsonToItemsTaskFactory"
                    AssemblyFile="$(MSBuildToolsPath)/../../packs/Microsoft.NET.Runtime.MonoTargets.Sdk/10.0.9/tasks/net10.0/MonoTargetsTasks.dll"
                    Override="true">
                    <ParameterGroup>
                      <EmccProperties ParameterType="Microsoft.Build.Framework.ITaskItem[]" Output="true" />
                      <WasmOptConfigurationFlags ParameterType="Microsoft.Build.Framework.ITaskItem[]" Output="true" />
                      <EmccDefaultExportedFunctions ParameterType="Microsoft.Build.Framework.ITaskItem[]" Output="true" />
                      <EmccDefaultExportedRuntimeMethods ParameterType="Microsoft.Build.Framework.ITaskItem[]" Output="true" />
                      <PropertiesThatTriggerRelinking ParameterType="Microsoft.Build.Framework.ITaskItem[]" Output="true" />
                    </ParameterGroup>
                  </UsingTask>
                  <Target Name="Probe"><ReadWasmProps JsonFilePath="input.json" /></Target>
                </Project>
                """;
            Write("child-override.proj", readerDeclaration);
            Write("reader-override.targets", readerDeclaration);
            Write("child-import.proj", """<Project><Import Project="reader-override.targets" /></Project>""");
            var prepared = await Capture("main.proj", "Prepare");
            await Assert.That(prepared.ExitCode).IsEqualTo(0).Because(prepared.Output);
            Write("probe/NuGet.Config", """<configuration><packageSources><clear /></packageSources></configuration>""");
            Write("probe/Probe.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
                    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup>
                  <ItemGroup>
                    <Reference Include="Microsoft.Build" HintPath="$(MSBuildBinPath)/Microsoft.Build.dll" Private="false" />
                    <Reference Include="Microsoft.Build.Framework" HintPath="$(MSBuildBinPath)/Microsoft.Build.Framework.dll" Private="false" />
                  </ItemGroup>
                </Project>
                """);
            Write("probe/Program.cs", NativeJsonReceiptProbe);
            var built = await ReleaseTestPaths.RunInDirectory(Path.Combine(Root, "probe"), "dotnet",
                "build", "Probe.csproj", "-c", "Release", "-v", "quiet", "-p:UseSharedCompilation=false");
            await Assert.That(built.ExitCode).IsEqualTo(0).Because(built.Output + built.StandardError);
            string staged = Path.Combine(Session, "private/tool/probe");
            Directory.CreateDirectory(staged);
            foreach (string name in new[] { "Probe.dll", "Probe.deps.json", "Probe.runtimeconfig.json" })
                File.Copy(Path.Combine(Root, "probe/bin/Release/net10.0", name), Path.Combine(staged, name));
            return Path.Combine(staged, "Probe.dll");
        }

        internal void WriteNamespaceProject(bool generatedWork = false)
        {
            Write("src/probe/input.txt", "original synthetic input");
            Write("src/probe/unused.txt", "unconsumed original source");
            Write("src/probe/main.proj", generatedWork
                ? """
                  <Project><Target Name="Build">
                    <WriteLinesToFile File="generated.cs" Lines="generated input" Overwrite="true" />
                    <Copy SourceFiles="generated.cs" DestinationFiles="bin/generated.txt" />
                    <Copy SourceFiles="input.txt" DestinationFiles="bin/output.txt" />
                  </Target></Project>
                  """
                : """
                  <Project><Target Name="Build"><Copy SourceFiles="input.txt" DestinationFiles="bin/output.txt" /></Target></Project>
                  """);
        }

        internal async Task<(int ExitCode, string Output)> VerifyWithoutSdk(params string[] arguments)
        {
            string runtime = Path.Combine(_directory, "runtime-only");
            string stage = Path.Combine(runtime, "capture");
            Directory.CreateDirectory(stage);
            string sdk = Path.Combine(Session, "frozen/sdk");
            string muxer = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
            File.Copy(Path.Combine(sdk, muxer), Path.Combine(runtime, muxer));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(Path.Combine(runtime, muxer), File.GetUnixFileMode(Path.Combine(sdk, muxer)));
            foreach (string directory in new[] { "host", "shared" })
                foreach (string source in Directory.EnumerateFiles(Path.Combine(sdk, directory), "*", SearchOption.AllDirectories))
                {
                    string destination = Path.Combine(runtime, Path.GetRelativePath(sdk, source));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(source, destination);
                    if (!OperatingSystem.IsWindows())
                        File.SetUnixFileMode(destination, File.GetUnixFileMode(source));
                }
            foreach (string name in new[] { "Configurator.BuildCapture.dll", "Configurator.BuildCapture.deps.json",
                "Configurator.BuildCapture.runtimeconfig.json" })
                File.Copy(Path.Combine(Path.GetDirectoryName(Tool)!, name), Path.Combine(stage, name));
            await Assert.That(Directory.Exists(Path.Combine(runtime, "sdk"))).IsFalse();
            return await ReleaseTestPaths.Run(Path.Combine(runtime, muxer),
                [Path.Combine(stage, "Configurator.BuildCapture.dll"), .. arguments]);
        }

        internal void Write(string relative, string text)
        {
            string path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }

        internal async Task<(int ExitCode, string Output)> Capture(string project, string target, params string[] options)
        {
            // The pre-owner baseline executes the real former build path. It cannot
            // manufacture a capture; every positive completeness assertion stays Red.
            if (!File.Exists(Tool))
            {
                var old = await ReleaseTestPaths.RunInDirectory(Root, "dotnet", "msbuild", project,
                    "-restore", "-target:" + target, "-nologo", "-verbosity:quiet");
                return (old.ExitCode, old.Output + old.StandardError);
            }
            string admission = Path.Combine(_directory, "admission.json");
            await File.WriteAllTextAsync(admission, System.Text.Json.JsonSerializer.Serialize(new
            {
                kind = "synthetic-build",
                files = Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/')).Order(StringComparer.Ordinal).ToArray()
            }));
            return await ReleaseTestPaths.Run("dotnet",
                [Tool, "--capture", Root, project, target, Session, "--fixture-admission", admission, .. options]);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
    }
}
