using System.Collections;
using System.Text.Json;
using Microsoft.Build.Framework;

namespace Configurator.BuildCapture;

/// <summary>Observes the executed graph, including final parameters of consuming tasks.</summary>
public sealed class BuildCaptureLogger : ILogger
{
    private CaptureSession _session = null!;
    private readonly Dictionary<(int Node, int Project, int Target, int Task), (string Name, string Directory)> _tasks = [];
    private readonly HashSet<string> _failures = new(StringComparer.Ordinal);
    private readonly Dictionary<(int Node, int Project, int Target, int Task), string> _generated = [];
    private readonly Dictionary<(int Node, int Project, int Target, int Task), string> _metadataProjects = [];
    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;
    public string? Parameters { get; set; }
    internal string CalibrationProject { get; init; } = "";
    internal string CalibrationInput { get; init; } = "";
    internal bool DeferCalibration { get; init; }
    internal Action<string, string, string, IDictionary<string, string>, Action<JsonElement>>? ValidateTaskHost { get; init; }
    internal Action<string, IDictionary<string, string>>? ValidateJsonProject { get; init; }
    internal bool CalibrationObserved { get; private set; }
    private readonly Dictionary<string, string> _calibrationParameters = new(StringComparer.Ordinal);
    private TaskContract[] _contracts = [];
    private readonly Dictionary<(int, int, int, int), Invocation> _invocations = [];
    private readonly Dictionary<(int, int, int), string> _targets = [];
    private readonly Dictionary<(int, int), IDictionary<string, string>> _projectGlobals = [];
    private readonly Dictionary<(int, int, int, int), RejectionContext> _diagnostics = [];
    private readonly HashSet<string> _jsonTokens = new(StringComparer.Ordinal);
    private int _diagnosticCount;
    private sealed class RejectionContext
    {
        internal required string Id { get; init; }
        internal required object Start { get; init; }
        internal JsonElement? Declarations { get; set; }
        internal bool Rejected { get; set; }
        internal int Parameters { get; set; }
    }
    private sealed record Invocation(string Id, string Project, string Target, string Task, TaskContract Contract,
        FileIdentity Assembly, FileIdentity Recipe)
    {
        internal Dictionary<string, CapturedParameter> Parameters { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, (string Path, FileIdentity? Before)> Transformations { get; } = new(StringComparer.Ordinal);
        internal string Operation { get; set; } = "";
        internal List<string> MoveSources { get; } = [];
        internal List<string> MoveDestinations { get; } = [];
        internal JsonFactoryState? JsonFactory { get; init; }
    }
    private sealed class JsonFactoryState(BuildEventContext context, string recipePath)
    {
        internal BuildEventContext Context { get; } = context;
        internal string RecipePath { get; } = recipePath;
        internal string InputPath { get; set; } = "";
        internal string? Token { get; set; }
        internal JsonFactoryPermit? Permit { get; set; }
        internal bool? Succeeded { get; set; }
    }

    internal static readonly HashSet<string> KnownTasks = new(StringComparer.Ordinal)
    {
        "MSBuild", "CallTarget", "Message", "Warning", "Error", "Copy", "Move", "Delete", "RemoveDir", "MakeDir",
        "WriteLinesToFile", "ReadLinesFromFile", "Touch", "GetFileHash", "GetAssemblyIdentity",
        "FindUnderPath", "FindAppConfigFile", "RemoveDuplicates", "ConvertToAbsolutePath", "CombinePath",
        "AssignTargetPath", "AssignCulture", "CreateCSharpManifestResourceName", "GenerateResource", "AL",
        "Csc", "ResolveAssemblyReference", "ResolveProjectReferences", "ResolveNonMSBuildProjectOutput",
        "GetReferenceAssemblyPaths", "GetFrameworkPath", "GetSDKReferenceFiles", "ResolveSDKReference",
        "ResolveCodeAnalysisRuleSet", "GenerateBindingRedirects", "GenerateBindingRedirectsUpdateAppConfig",
        "WriteCodeFragment", "GenerateMSBuildEditorConfig", "GenerateGlobalUsings", "CheckForDuplicateItems",
        "GetPackageDirectory", "ResolvePackageAssets", "ResolvePackageFileConflicts", "CheckForUnsupportedWinMDReferences",
        "ResolveFrameworkReferences", "ProcessFrameworkReferences", "ResolveAppHosts", "ResolveRuntimePackAssets",
        "ResolveTargetingPackAssets", "ResolveOffByDefaultAnalyzers", "AllowEmptyTelemetry",
        "GetRestoreSettingsTask", "GetRestoreProjectStyleTask", "GetRestorePackageReferencesTask",
        "GetRestoreProjectReferencesTask", "GetRestoreDotnetCliToolsTask", "GetRestoreFrameworkReferencesTask",
        "GetRestorePackageDownloadsTask", "GetRestoreNuGetAuditSuppressionsTask", "GetRestoreTargetFrameworksTask",
        "GetRestoreProjectJsonPathTask", "GetRestoreSolutionProjectsTask", "WarnForInvalidProjectsTask",
        "RestoreTask", "CheckForImplicitPackageReferenceOverrides", "ApplyImplicitVersions",
        "NETSdkWarning", "NETSdkError", "CheckIfPackageReferenceShouldBeFrameworkReference",
        "GenerateDepsFile", "GenerateRuntimeConfigurationFiles", "CreateAppHost", "GenerateBundle",
        "GenerateSatelliteAssemblies", "GetCopyToOutputDirectoryItems", "Hash", "CheckForDuplicateFrameworkReferences",
        "GetAssemblyVersion", "GetPublishItemsOutputGroupOutputs", "ReadItemsFromFile", "WriteItemsToFile",
        "CheckForTargetInAssetsFile", "ReportAssetsLogMessages", "ResolvePackageDependenciesForBuild",
        "GetAppHostPaths", "ValidateExecutableReferences", "GetToolsetVersion", "SdkInfo",
        "CheckForMissingWorkload", "ShowMissingWorkloads", "GetInstalledWorkloadManifests",
        "ComputeNETCoreBuildOutputFiles", "ResolveReadyToRunCompilers", "GetResolvedSDKReferenceFiles",
        "DiscoverDefaultGeneratedFiles", "GetRestoreSolutionProjectsTask", "BuildCaptureTask",
        "GetCentralPackageVersionsTask", "AssignProjectConfiguration", "CheckForDuplicateNuGetItemsTask", "CombineTargetFrameworkInfoProperties",
        "CombineXmlElements", "CopyRefAssembly", "GenerateSourceLinkFile", "GetPackagesToPrune",
        "GetProjectTargetFrameworksTask", "GetReferenceNearestTargetFrameworkTask", "GetRestorePrunePackageReferencesTask",
        "GetSourceLinkUrl", "JoinItems", "LocateRepository", "MapSourceRoots", "NuGetMessageTask",
        "SetRidAgnosticValueForProjects", "SourceLinkHasSingleProvider", "TranslateRepositoryUrls",
        "TestingPlatformEntryPointTask", "TestingPlatformSelfRegisteredExtensions", "GetAssemblyAttributes",
        "CheckForDuplicateItemMetadata", "ResolveOverlappingItemGroupConflicts",
        "ApplyCssScopes", "ComputeCssScope", "DefineStaticWebAssetEndpoints", "DefineStaticWebAssets",
        "DiscoverDefaultScopedCssItems", "EncodeRazorInputItem", "FindAssembliesWithReferencesTo", "UpdatePackageStaticWebAssets",
        "CreateProperty", "ApplyCompressionNegotiation", "ApplyJsModules", "CollectStaticWebAssetsToCopy",
        "ComputeStaticWebAssetsForCurrentProject", "ComputeStaticWebAssetsTargetPaths", "ConcatenateCssFiles",
        "DiscoverPrecompressedAssets", "FilterStaticWebAssetEndpoints", "GZipCompress",
        "GenerateServiceWorkerAssetsManifest", "GenerateStaticWebAssetEndpointsManifest",
        "GenerateStaticWebAssetsDevelopmentManifest", "GenerateStaticWebAssetsManifest", "MergeConfigurationProperties",
        "ReadStaticWebAssetsManifestFile", "ResolveAllScopedCssAssets", "ResolveCompressedAssets",
        "ResolveFingerprintedStaticWebAssetEndpointsForAssets", "RewriteCss",
        "UpdateServiceWorkerFileWithVersion", "UpdateStaticWebAssetEndpoints",
        "GenerateAvaloniaResourcesTask", "CompileAvaloniaXamlTask"
    };
    private static readonly HashSet<string> RequiredFiles = new(StringComparer.Ordinal)
    {
        "Sources", "References", "Analyzers", "AdditionalFiles", "AnalyzerConfigFiles", "Resources", "SourceFiles",
        "FilesToBundle", "ResolvedFiles", "ReferenceAssemblies", "Assemblies", "InputFiles", "ApplicationManifest",
        "Win32Resource", "Win32Icon", "AppConfigFile", "KeyFile", "Ruleset", "CodeAnalysisRuleSet"
    };
    private static readonly HashSet<string> SafeProperties = new(StringComparer.Ordinal)
    {
        "TargetFramework", "TargetFrameworks", "RuntimeIdentifier", "RuntimeIdentifiers", "Configuration",
        "SelfContained", "PublishSingleFile", "PublishTrimmed", "RunAOTCompilation", "UseAppHost",
        "EnableCompressionInSingleFile", "IncludeNativeLibrariesForSelfExtract", "DebugSymbols", "DebugType",
        "Optimize", "Deterministic", "LangVersion", "Nullable", "AllowUnsafeBlocks", "OutputType",
        "TreatWarningsAsErrors", "AnalysisLevel", "AnalysisMode", "DefineConstants", "WarningLevel",
        "InvariantGlobalization", "BlazorWebAssemblyLoadAllGlobalizationData", "RuntimeFrameworkVersion", "NETCoreSdkVersion"
    };

    public void Initialize(IEventSource eventSource)
    {
        _session = CaptureSession.Open(Parameters ?? throw new LoggerException("CAPTURE_SESSION_MISSING"));
        CaptureSession.Require(ValidateTaskHost is not null, "CAPTURE_HOST_REQUIRED");
        _contracts = JsonSerializer.Deserialize<TaskContract[]>(
            File.ReadAllBytes(Path.Combine(Parameters!, "private/task-parameters.json")), Program.Json)!;
        _session.Started();
        if (eventSource is IEventSource3 inputs) inputs.IncludeTaskInputs();
        else throw new LoggerException("CAPTURE_EVENTS_UNSUPPORTED");
        if (eventSource is IEventSource4 evaluation) evaluation.IncludeEvaluationPropertiesAndItems();
        eventSource.AnyEventRaised += (_, args) =>
        {
            try { Observe(args); }
            catch (LoggerException error) when (error.Message is "CAPTURE_JSON_OVERRIDE_REQUIRED" or "CAPTURE_JSON_GLOBALS_UNAVAILABLE"
                or "CAPTURE_CHILD_CAPTURE_PROPERTIES_UNSUPPORTED")
            {
                _failures.Add(error.Message);
                throw;
            }
            catch (InvalidCastException)
            {
                _failures.Add("CAPTURE_EVENT_SHAPE_UNSUPPORTED");
                throw;
            }
            catch (CaptureException error)
            {
                _failures.Add(error.Code);
                if (_diagnostics.TryGetValue(Key(args.BuildEventContext), out var diagnostic))
                {
                    if (!diagnostic.Rejected)
                    {
                        diagnostic.Rejected = true;
                        Diagnostic(diagnostic.Start, diagnostic.Id);
                        Diagnostic(new
                        {
                            kind = "declarations",
                            id = diagnostic.Id,
                            candidates = diagnostic.Declarations?.GetProperty("candidates"),
                            selection = "unavailable",
                            completeness = diagnostic.Declarations?.GetProperty("completeness").GetString() ?? "unavailable",
                            truncated = diagnostic.Declarations?.GetProperty("truncated").GetBoolean()
                        }, diagnostic.Id);
                    }
                    Diagnostic(new { kind = "rejected", id = diagnostic.Id, code = error.Code }, diagnostic.Id);
                }
                File.AppendAllText(Path.Combine(Parameters!, "private/rejected-events.log"),
                    error.Code + " " + args.GetType().Name + " " + args.Message + "\n");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            { _failures.Add("CAPTURE_EVENT_INVALID"); }
        };
    }

    private void Observe(BuildEventArgs args)
    {
        if (args is CustomBuildEventArgs custom && args is IExtendedBuildEventArgs extended
            && extended.ExtendedType == ReadWasmPropsProfile.Event)
        {
            try { ObserveJsonFactory(custom, extended); }
            catch
            {
                // A replay can carry an earlier acknowledgement on the same
                // SDK-owned metadata object. Failure must retire that receipt
                // before the native task resumes, not merely poison sealing.
                if (extended.ExtendedMetadata is { } metadata)
                {
                    metadata["ack-count"] = "0";
                    metadata.Remove("permit");
                }
                throw;
            }
            return;
        }
        if (args is TaskParameterEventArgs observed
            && _diagnostics.TryGetValue(Key(observed.BuildEventContext), out var rejected) && rejected.Rejected)
        {
            if (++rejected.Parameters <= 128)
                Diagnostic(new
                {
                    kind = "parameter",
                    id = rejected.Id,
                    name = observed.ParameterName ?? observed.ItemType,
                    direction = observed.Kind.ToString(),
                    semanticType = "unavailable",
                    availability = observed.Items.Count <= 128 ? "observed" : "count-limit",
                    items = observed.Items.Cast<object?>().Take(128).Select(value =>
                    {
                        string[]? names = value is ITaskItem metadataItem
                            ? metadataItem.MetadataNames.Cast<string>().Take(129).ToArray() : null;
                        return new
                        {
                            type = value?.GetType().FullName,
                            availability = value switch
                            {
                                string text when text.Length > 16384 => "size-limit",
                                ITaskItem item when item.ItemSpec.Length > 16384 => "size-limit",
                                null or string or bool or int or ITaskItem => "observed",
                                _ => "unavailable"
                            },
                            value = value switch
                            {
                                string text when text.Length <= 16384 => (object)text,
                                ITaskItem item when item.ItemSpec.Length <= 16384 => item.ItemSpec,
                                bool boolean => boolean,
                                int number => number,
                                _ => null
                            },
                            metadataNames = names is null ? null : new
                            {
                                availability = names.Length > 128 ? "count-limit"
                                    : names.Any(name => name.Length > 256) ? "size-limit" : "observed",
                                names = names.Take(128).Where(name => name.Length <= 256)
                                    .Order(StringComparer.Ordinal).ToArray()
                            }
                        };
                    }).ToArray()
                }, rejected.Id);
            else if (rejected.Parameters == 129)
                Diagnostic(new { kind = "parameter-limit", id = rejected.Id, availability = "count-limit" }, rejected.Id);
        }
        if (args is TaskFinishedEventArgs outcome
            && _diagnostics.Remove(Key(outcome.BuildEventContext), out var finishedDiagnostic) && finishedDiagnostic.Rejected)
            Diagnostic(new
            {
                kind = "finished",
                id = finishedDiagnostic.Id,
                succeeded = outcome.Succeeded,
                unobservedParameters = "unavailable"
            }, finishedDiagnostic.Id);
        if (args is TaskParameterEventArgs input && input.Kind.ToString() == "TaskInput"
            && _invocations.TryGetValue(Key(input.BuildEventContext), out var invocation))
            CaptureParameter(invocation, input);
        if (args is TaskFinishedEventArgs completed
            && _invocations.Remove(Key(completed.BuildEventContext), out var captured))
        {
            if (captured.JsonFactory is { } json)
                CaptureSession.Require(json.Token is not null && json.Permit is not null
                    && json.Succeeded == completed.Succeeded, "CAPTURE_JSON_RECEIPT_INCOMPLETE");
            var parameters = captured.Parameters.Values.OrderBy(parameter => parameter.Name, StringComparer.Ordinal).ToArray();
            var transformations = new List<CapturedTransformation>();
            foreach (var (parameter, transformation) in captured.Transformations)
            {
                CaptureSession.Require(captured.Task != "BuildCaptureTask" || captured.Operation == "frameworks",
                    "CAPTURE_TRANSFORMATION_UNADMITTED");
                if (!File.Exists(transformation.Path))
                {
                    CaptureSession.Require(transformation.Before is null, "CAPTURE_TRANSFORMATION_OUTPUT_MISSING");
                    continue;
                }
                var after = _session.Observe(transformation.Path, "generated");
                CaptureSession.Require(after.Path.StartsWith("generated/versions/", StringComparison.Ordinal),
                    "CAPTURE_ORIGINAL_WRITE");
                string logical = _session.Logical(transformation.Path);
                transformations.Add(new(parameter, logical.StartsWith("generated/", StringComparison.Ordinal)
                    ? logical : "generated/" + logical, transformation.Before, after));
            }
            var finished = new CapturedInvocation(captured.Id, captured.Project, captured.Target, captured.Task,
                    captured.Assembly, captured.Recipe, parameters, CaptureSession.Hash(CaptureSession.Canonical(parameters)),
                    transformations.OrderBy(transformation => transformation.Parameter, StringComparer.Ordinal).ToArray(),
                    completed.Succeeded);
            _session.InvocationEvent("invocation-finished", captured.Id, JsonSerializer.SerializeToElement(finished, Program.Json));
            if (captured.Task == "Move" && captured.Target == "PublishConfigurator" && completed.Succeeded)
                _session.RecordMove(finished, captured.MoveSources, captured.MoveDestinations);
            if (captured.Task == "BuildCaptureTask")
                Environment.SetEnvironmentVariable("CONFIGURATOR_CAPTURE_PARENT_INVOCATION", null);
        }
        switch (args)
        {
            case TargetStartedEventArgs target:
                var targetContext = target.BuildEventContext ?? throw new CaptureException("CAPTURE_TARGET_CONTEXT_MISSING");
                _targets[(targetContext.NodeId, targetContext.ProjectContextId,
                    targetContext.TargetId)] = target.TargetName;
                TargetEvent(targetContext, target.ProjectFile, target.TargetName, "started", null);
                break;
            case TargetFinishedEventArgs target:
                TargetEvent(target.BuildEventContext, target.ProjectFile, target.TargetName,
                    target.Succeeded ? "succeeded" : "failed", null);
                break;
            case TargetSkippedEventArgs target:
                TargetEvent(target.BuildEventContext, target.ProjectFile, target.TargetName,
                    "skipped", target.SkipReason.ToString());
                break;
            case BuildErrorEventArgs error:
                Console.Error.WriteLine(error.Code + " " + error.Message);
                break;
            case ProjectImportedEventArgs import when !import.ImportIgnored && File.Exists(import.ImportedProjectFile):
                _session.Observe(import.ImportedProjectFile, "recipe");
                break;
            case ProjectEvaluationFinishedEventArgs evaluation when evaluation.ProjectFile is not null:
                if (evaluation.ProjectFile != CalibrationProject)
                {
                    var evaluationGlobals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (Microsoft.Build.Execution.ProjectPropertyInstance global in evaluation.GlobalProperties
                        ?? throw new LoggerException("CAPTURE_JSON_GLOBALS_UNAVAILABLE"))
                        evaluationGlobals.Add(global.Name, global.EvaluatedValue);
                    (ValidateJsonProject ?? throw new CaptureException("CAPTURE_HOST_REQUIRED"))(evaluation.ProjectFile, evaluationGlobals);
                }
                _session.Observe(evaluation.ProjectFile, "recipe");
                Properties(evaluation.ProjectFile, evaluation.Properties);
                break;
            case ProjectStartedEventArgs project when project.ProjectFile is not null:
                var projectContext = project.BuildEventContext ?? throw new CaptureException("CAPTURE_PROJECT_CONTEXT_MISSING");
                _projectGlobals[(projectContext.NodeId, projectContext.ProjectContextId)] = project.GlobalProperties
                    ?? throw new CaptureException("CAPTURE_GLOBAL_PROPERTIES_MISSING");
                if (project.ProjectFile != CalibrationProject)
                    (ValidateJsonProject ?? throw new CaptureException("CAPTURE_HOST_REQUIRED"))(
                        project.ProjectFile, _projectGlobals[(projectContext.NodeId, projectContext.ProjectContextId)]);
                _session.Observe(project.ProjectFile, "recipe");
                Properties(project.ProjectFile, project.Properties);
                break;
            case TaskStartedEventArgs task:
                string name = task.TaskName.Split('.').Last();
                _tasks[Key(task.BuildEventContext)] = (name, Path.GetDirectoryName(task.ProjectFile)!);
                if (name == nameof(BuildCaptureCalibrationTask))
                {
                    CaptureSession.Require(task.ProjectFile == CalibrationProject
                        && Path.GetFullPath(task.TaskAssemblyLocation) == Path.GetFullPath(_session.Tool),
                        "CAPTURE_CALIBRATION_IDENTITY");
                    break;
                }
                var diagnosticContext = task.BuildEventContext;
                string diagnosticId = $"rejected/{CaptureSession.CurrentGraph}/{diagnosticContext?.NodeId}/{diagnosticContext?.ProjectContextId}/{diagnosticContext?.TargetId}/{diagnosticContext?.TaskId}";
                _diagnostics[Key(diagnosticContext)] = new RejectionContext
                {
                    Id = diagnosticId,
                    Start = new
                    {
                        kind = "started",
                        id = diagnosticId,
                        graph = CaptureSession.CurrentGraph,
                        node = diagnosticContext?.NodeId,
                        projectContext = diagnosticContext?.ProjectContextId,
                        targetContext = diagnosticContext?.TargetId,
                        taskContext = diagnosticContext?.TaskId,
                        project = _session.Logical(task.ProjectFile),
                        task = task.TaskName,
                        target = diagnosticContext is null ? null : _targets.GetValueOrDefault(
                            (diagnosticContext.NodeId, diagnosticContext.ProjectContextId, diagnosticContext.TargetId)),
                        assembly = string.IsNullOrEmpty(task.TaskAssemblyLocation) ? null : _session.Logical(task.TaskAssemblyLocation),
                        recipe = string.IsNullOrEmpty(task.TaskFile) ? null : _session.Logical(task.TaskFile),
                        selection = "unavailable",
                        parameters = "unavailable",
                        outcome = "unavailable"
                    }
                };
                var taskContext = task.BuildEventContext ?? throw new CaptureException("CAPTURE_TASK_CONTEXT_MISSING");
                var globals = _projectGlobals.GetValueOrDefault((taskContext.NodeId, taskContext.ProjectContextId))
                    ?? throw new CaptureException("CAPTURE_PROJECT_CONTEXT_MISSING");
                string ownerTarget = _targets.GetValueOrDefault((taskContext.NodeId, taskContext.ProjectContextId, taskContext.TargetId))
                    ?? throw new CaptureException("CAPTURE_TARGET_CONTEXT_MISSING");
                (ValidateTaskHost ?? throw new CaptureException("CAPTURE_HOST_REQUIRED"))(task.ProjectFile, name, ownerTarget, globals,
                    declarations => _diagnostics[Key(taskContext)].Declarations = declarations);
                if (!KnownTasks.Contains(name) && name != ReadWasmPropsProfile.Name)
                {
                    File.AppendAllText(Path.Combine(Parameters!, "private/task-contracts.log"),
                        JsonSerializer.Serialize(new
                        {
                            task = task.TaskName,
                            assembly = string.IsNullOrEmpty(task.TaskAssemblyLocation) ? null : _session.Logical(task.TaskAssemblyLocation),
                            declaration = _session.Logical(task.TaskFile)
                        }) + "\n");
                    throw new CaptureException("CAPTURE_TASK_UNSUPPORTED_" + name);
                }
                if (name == "MapSourceRoots")
                {
                    CaptureSession.Require(task.TaskName == "Microsoft.CodeAnalysis.BuildTasks.MapSourceRoots"
                        && _session.Logical(task.TaskAssemblyLocation) == "sdk/sdk/10.0.302/Roslyn/Microsoft.Build.Tasks.CodeAnalysis.dll"
                        && _session.Logical(task.TaskFile) == "sdk/sdk/10.0.302/Roslyn/Microsoft.Managed.Core.targets",
                        "CAPTURE_MAP_IDENTITY_UNSUPPORTED");
                    _metadataProjects[Key(task.BuildEventContext)] = task.ProjectFile;
                    _session.Setting(task.ProjectFile, "MapSourceRoots.contract", JsonSerializer.SerializeToElement(new
                    {
                        sdkVersion = CaptureSession.SdkVersion,
                        assumption = "no-additional-file-content-input",
                        downstream = "Csc.PathMap"
                    }));
                }
                if (name is not ("MSBuild" or "CallTarget" or "MapSourceRoots" or "BuildCaptureTask"))
                    CaptureSession.Require(_session.RequiresBoundary(name), "CAPTURE_TASK_BOUNDARY_UNREGISTERED_" + name);
                _session.Observe(task.TaskFile, "recipe");
                if (!string.IsNullOrEmpty(task.TaskAssemblyLocation) && File.Exists(task.TaskAssemblyLocation))
                {
                    string alias = _session.Logical(task.TaskAssemblyLocation);
                    CaptureSession.Require(alias.StartsWith("sdk/", StringComparison.Ordinal)
                        || alias.StartsWith("package/", StringComparison.Ordinal)
                        || alias.StartsWith("capture-tool/", StringComparison.Ordinal),
                        "CAPTURE_TASK_ASSEMBLY_UNADMITTED");
                    _session.Observe(task.TaskAssemblyLocation, "tool");
                }
                if (name == "Exec") throw new CaptureException("CAPTURE_SUBPROCESS_UNADMITTED");
                {
                    string taskAssembly = Path.GetFullPath(task.TaskAssemblyLocation);
                    string assemblyAlias = _session.Logical(taskAssembly);
                    var contract = _contracts.SingleOrDefault(contract => contract.Name == name
                            && _session.Logical(contract.Assembly) == assemblyAlias)
                        ?? _contracts.SingleOrDefault(contract => contract.Name == name
                            && contract.Assembly == Path.Combine(_session.Frozen, "sdk/sdk", CaptureSession.SdkVersion, "Microsoft.Build.Tasks.Core.dll"))
                        ?? throw new CaptureException("CAPTURE_TASK_SHAPE_UNREGISTERED_" + name);
                    bool engineRouting = name is "MSBuild" or "CallTarget"
                        && taskAssembly == Path.Combine(_session.Frozen, "sdk/sdk", CaptureSession.SdkVersion, "Microsoft.Build.dll");
                    if (!engineRouting && assemblyAlias != _session.Logical(contract.Assembly))
                        File.AppendAllText(Path.Combine(Parameters!, "private/task-bindings.log"),
                            JsonSerializer.Serialize(new { task.TaskName, task.TaskAssemblyLocation, expected = contract.Assembly, task.TaskFile }) + "\n");
                    CaptureSession.Require(engineRouting || assemblyAlias == _session.Logical(contract.Assembly), "CAPTURE_TASK_IDENTITY_UNADMITTED");
                    var context = task.BuildEventContext ?? throw new CaptureException("CAPTURE_TASK_CONTEXT_MISSING");
                    string id = $"{CaptureSession.CurrentGraph}/{context.NodeId}/{context.ProjectContextId}/{context.TargetId}/{context.TaskId}";
                    CaptureSession.Require(_targets.TryGetValue((context.NodeId, context.ProjectContextId, context.TargetId), out string? targetName),
                        "CAPTURE_TARGET_CONTEXT_MISSING");
                    var started = new Invocation(id, _session.Logical(task.ProjectFile), targetName!, name, contract,
                        _session.Observe(taskAssembly, "tool"), _session.Observe(task.TaskFile, "recipe"))
                    {
                        JsonFactory = name == ReadWasmPropsProfile.Name ? new(context, task.TaskFile) : null
                    };
                    if (started.JsonFactory is not null)
                        CaptureSession.Require(task.TaskName == ReadWasmPropsProfile.Name
                            && taskAssembly == Path.GetFullPath(_session.Tool),
                            "CAPTURE_JSON_DISPATCH_UNSUPPORTED");
                    CaptureSession.Require(_invocations.TryAdd(Key(context), started), "CAPTURE_TASK_CONTEXT_DUPLICATE");
                    _session.InvocationEvent("invocation-started", id, JsonSerializer.SerializeToElement(new
                    {
                        started.Project,
                        started.Target,
                        started.Task,
                        started.Assembly,
                        started.Recipe
                    }, Program.Json));
                    if (name == "BuildCaptureTask")
                        Environment.SetEnvironmentVariable("CONFIGURATOR_CAPTURE_PARENT_INVOCATION", id);
                }
                break;
            case TaskParameterEventArgs parameter when _tasks.TryGetValue(Key(parameter.BuildEventContext), out var calibration)
                && calibration.Name == nameof(BuildCaptureCalibrationTask):
                if (parameter.Kind.ToString() == "TaskInput")
                {
                    string calibrationName = parameter.ParameterName ?? parameter.ItemType;
                    CaptureSession.Require(calibrationName is "InputFile" or "FinalValue" or "FinalNumber" or "FinalFlag"
                        && parameter.Items.Count == 1
                        && _calibrationParameters.TryAdd(calibrationName, parameter.Items[0]?.ToString() ?? ""),
                        "CAPTURE_CALIBRATION_INPUT");
                    if (_calibrationParameters.Count != 4) break;
                    CaptureSession.Require(_calibrationParameters["InputFile"] == CalibrationInput
                        && int.TryParse(_calibrationParameters["FinalNumber"], out _)
                        && bool.TryParse(_calibrationParameters["FinalFlag"], out _), "CAPTURE_CALIBRATION_INPUT");
                    if (DeferCalibration)
                    {
                        using var entered = new ManualResetEventSlim();
                        using var watcher = new FileSystemWatcher(Path.GetDirectoryName(CalibrationInput)!, Path.GetFileName(CalibrationInput) + ".entered");
                        watcher.Created += (_, _) => entered.Set();
                        watcher.EnableRaisingEvents = true;
                        if (File.Exists(CalibrationInput + ".entered")) entered.Set();
                        CaptureSession.Require(entered.Wait(TimeSpan.FromSeconds(10)), "CAPTURE_CALIBRATION_TIMEOUT");
                    }
                    CaptureSession.Require(!File.Exists(CalibrationInput + ".early"), "CAPTURE_EARLY_READ_UNSUPPORTED");
                    byte[] original = File.ReadAllBytes(CalibrationInput);
                    CaptureSession.Require(System.Text.Encoding.UTF8.GetString(original) == BuildCaptureHost.CanaryBefore,
                        "CAPTURE_CALIBRATION_ORDERING");
                    File.WriteAllBytes(CalibrationInput + ".frozen", original);
                    File.WriteAllBytes(CalibrationInput + ".receipt", CaptureSession.Canonical(new
                    {
                        inputHash = CaptureSession.Hash(original),
                        finalValue = _calibrationParameters["FinalValue"],
                        finalNumber = int.Parse(_calibrationParameters["FinalNumber"], System.Globalization.CultureInfo.InvariantCulture),
                        finalFlag = bool.Parse(_calibrationParameters["FinalFlag"])
                    }));
                    CalibrationObserved = true;
                }
                break;
            case TaskParameterEventArgs parameter when _metadataProjects.TryGetValue(Key(parameter.BuildEventContext), out string? metadataProject):
                string parameterName = parameter.ParameterName ?? parameter.ItemType;
                string direction = parameter.Kind.ToString();
                CaptureSession.Require(direction == "TaskInput" && parameterName is "SourceRoots" or "Deterministic"
                    || direction == "TaskOutput" && parameterName == "MappedSourceRoots", "CAPTURE_MAP_SHAPE_UNSUPPORTED");
                if (parameterName == "Deterministic")
                {
                    CaptureSession.Require(parameter.Items.Count == 1
                        && bool.TryParse(parameter.Items[0]?.ToString(), out _), "CAPTURE_MAP_SHAPE_UNSUPPORTED");
                    _session.Setting(metadataProject, "MapSourceRoots.Deterministic",
                        JsonSerializer.SerializeToElement(bool.Parse(parameter.Items[0]!.ToString()!)));
                }
                else
                {
                    CaptureSession.Require(parameter.Items.Cast<object>().All(value => value is ITaskItem),
                        "CAPTURE_MAP_SHAPE_UNSUPPORTED");
                    var metadata = parameter.Items.Cast<ITaskItem>().Select(item => new
                    {
                        item = _session.NormalizeSetting(item.ItemSpec),
                        metadata = item.CloneCustomMetadata().Keys.Cast<string>().Order(StringComparer.Ordinal)
                            .Select(key => new { name = key, value = _session.NormalizeSetting(item.GetMetadata(key)) }).ToArray()
                    }).ToArray();
                    _session.Setting(metadataProject, "MapSourceRoots." + parameterName, JsonSerializer.SerializeToElement(new
                    {
                        type = "source-root-metadata",
                        sha256 = CaptureSession.Hash(CaptureSession.Canonical(metadata))
                    }));
                }
                break;
            case TaskParameterEventArgs parameter when parameter.Kind.ToString() == "TaskInput":
                if (!_tasks.TryGetValue(Key(parameter.BuildEventContext), out var consumer))
                    throw new CaptureException("CAPTURE_TASK_CONTEXT_MISSING");
                foreach (object? value in parameter.Items)
                {
                    string text = value is ITaskItem item ? item.ItemSpec : value?.ToString() ?? "";
                    if (consumer.Name == "Exec" && (parameter.ParameterName ?? parameter.ItemType) == "Command")
                        File.AppendAllText(Path.Combine(Parameters!, "private/native-commands.log"),
                            JsonSerializer.Serialize(new { directory = _session.Logical(consumer.Directory), command = _session.NormalizeSetting(text) }) + "\n");
                    if (consumer.Name == "Csc" && (parameter.ParameterName ?? parameter.ItemType) == "GeneratedFilesOutputPath" && text.Length > 0)
                        _generated[Key(parameter.BuildEventContext)] = Path.GetFullPath(text, consumer.Directory);
                }
                break;
            case TaskFinishedEventArgs task when task.Succeeded && _generated.Remove(Key(task.BuildEventContext), out string? generated):
                if (Directory.Exists(generated))
                    foreach (string path in Directory.EnumerateFiles(generated, "*.cs", SearchOption.AllDirectories))
                        _session.Observe(path, "generated");
                break;
            case BuildFinishedEventArgs finished:
                CaptureSession.Require(_invocations.Count == 0, "CAPTURE_INVOCATION_INCOMPLETE");
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic))
                    if (!string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
                        _session.Observe(assembly.Location, "tool");
                _session.Finished(finished.Succeeded && _failures.Count == 0);
                break;
        }
    }

    private void CaptureParameter(Invocation invocation, TaskParameterEventArgs parameter)
    {
        string name = parameter.ParameterName ?? parameter.ItemType;
        if (invocation.Task == "MSBuild")
            foreach (object? entry in parameter.Items)
            {
                if (name is "Properties" or "RemoveProperties" && entry is string properties)
                    RequirePreservedCaptureProperties(properties);
                if (name == "Projects" && entry is ITaskItem project)
                    RequirePreservedCaptureProperties(project.GetMetadata("AdditionalProperties"));
            }
        var contract = invocation.Contract.Parameters.SingleOrDefault(property => property.Name == name)
            ?? throw new CaptureException("CAPTURE_PARAMETER_SHAPE_UNSUPPORTED");
        object? value = contract.Type switch
        {
            "System.Boolean" when parameter.Items.Count == 1
                && bool.TryParse(parameter.Items[0]?.ToString(), out bool boolean) => boolean,
            "System.Int32" when parameter.Items.Count == 1
                && int.TryParse(parameter.Items[0]?.ToString(), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int number) => number,
            "System.String" when parameter.Items.Count == 1 => CapturedBuildTask.Describe(parameter.Items[0], _session),
            "System.String[]" or "Microsoft.Build.Framework.ITaskItem[]" =>
                parameter.Items.Cast<object?>().Select(item => CapturedBuildTask.Describe(item, _session)).ToArray(),
            "Microsoft.Build.Framework.ITaskItem" when parameter.Items.Count == 1 =>
                CapturedBuildTask.Describe(parameter.Items[0], _session),
            _ => throw new CaptureException("CAPTURE_PARAMETER_CATEGORY_UNSUPPORTED")
        };
        var inputs = new List<FileIdentity>();
        if (invocation.Task == "BuildCaptureTask" && name == "Operation")
        {
            CaptureSession.Require(parameter.Items.Count == 1 && parameter.Items[0] is string,
                "CAPTURE_PARAMETER_CATEGORY_UNSUPPORTED");
            invocation.Operation = (string)parameter.Items[0]!;
        }
        if (invocation.Task == ReadWasmPropsProfile.Name && name == "JsonFilePath")
        {
            CaptureSession.Require(parameter.Items.Count == 1 && parameter.Items[0] is string path && path.Length > 0,
                "CAPTURE_JSON_INPUT_MISMATCH");
            invocation.JsonFactory!.InputPath = Path.GetFullPath((string)parameter.Items[0]!,
                _tasks[Key(parameter.BuildEventContext)].Directory);
            inputs.Add(_session.Observe(invocation.JsonFactory.InputPath, "asset"));
        }
        if (invocation.Task is not ("MapSourceRoots" or ReadWasmPropsProfile.Name))
            foreach (object? entry in parameter.Items)
            {
                string text = entry is ITaskItem item ? item.ItemSpec : entry?.ToString() ?? "";
                var observed = Input(invocation.Task, name, text, Path.GetDirectoryName(
                    Path.Combine(_session.Work, invocation.Project["repo/".Length..]))!);
                if (observed is not null) inputs.Add(observed);
                if ((invocation.Task == "DefineStaticWebAssets" && name == "CacheManifestPath"
                    || invocation.Task == "CompileAvaloniaXamlTask" && name is "AssemblyFile" or "RefAssemblyFile"
                    || invocation.Task == "BuildCaptureTask" && name == "InventoryOutput") && text.Length != 0)
                {
                    CaptureSession.Require(observed is null
                        || observed.Path.StartsWith("generated/versions/", StringComparison.Ordinal), "CAPTURE_ORIGINAL_WRITE");
                    string path = Path.GetFullPath(text, Path.GetDirectoryName(Path.Combine(_session.Work, invocation.Project["repo/".Length..]))!);
                    CaptureSession.Require(invocation.Task == "BuildCaptureTask"
                        ? CaptureSession.Inside(path, Path.Combine(Parameters!, "private/tmp"))
                        : CaptureSession.Inside(path, _session.Work), "CAPTURE_OUTPUT_ROOT_UNADMITTED");
                    invocation.Transformations.Add(name, (path, observed));
                }
                if (invocation.Task == "ResolvePackageAssets" && name == "ProjectAssetsFile")
                    _session.RecordAssets(Path.Combine(_session.Work, invocation.Project["repo/".Length..]),
                        Path.GetFullPath(text, Path.GetDirectoryName(Path.Combine(_session.Work, invocation.Project["repo/".Length..]))!));
                if (entry is ITaskItem metadata && !CapturedBuildTask.IsDestination(invocation.Task, name))
                    foreach (DictionaryEntry attribute in metadata.CloneCustomMetadata())
                    {
                        var file = Input(invocation.Task, attribute.Key.ToString()!, attribute.Value?.ToString() ?? "",
                            Path.GetDirectoryName(Path.Combine(_session.Work, invocation.Project["repo/".Length..]))!, required: false);
                        if (file is not null) inputs.Add(file);
                    }
            }
        bool childTargets = invocation.Task is "MSBuild" or "CallTarget" && name == "Targets";
        if (childTargets)
            CaptureSession.Require(contract.Type == "System.String[]" && parameter.Items.Cast<object>().All(
                item => item is string target && target.Length is > 0 and <= 256
                    && target.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.')),
                "CAPTURE_CHILD_TARGET_UNSUPPORTED");
        JsonElement typed = childTargets || value is bool or int ? JsonSerializer.SerializeToElement(value)
            : JsonSerializer.SerializeToElement(new { sha256 = CaptureSession.Hash(CaptureSession.Canonical(value)) });
        if (invocation.Task == "Move" && invocation.Target == "PublishConfigurator" && name is "SourceFiles" or "DestinationFiles")
        {
            var paths = parameter.Items.Cast<object>().Select(item => Path.GetFullPath(
                item is ITaskItem taskItem ? taskItem.ItemSpec : item.ToString()!,
                Path.GetDirectoryName(Path.Combine(_session.Work, invocation.Project["repo/".Length..]))!)).ToArray();
            CaptureSession.Require(paths.Length == 1 && paths.All(path => CaptureSession.Inside(path, _session.Work)),
                "CAPTURE_MOVE_PAIRING_INVALID");
            if (name == "SourceFiles")
            {
                CaptureSession.Require(inputs.Count > 0 && inputs.All(input =>
                    input.Path.StartsWith("generated/versions/", StringComparison.Ordinal)), "CAPTURE_ORIGINAL_WRITE");
                invocation.MoveSources.AddRange(paths);
            }
            else
            {
                CaptureSession.Require(paths.All(CaptureSession.LifecycleAbsent), "CAPTURE_MOVE_DESTINATION_EXISTS");
                invocation.MoveDestinations.AddRange(paths);
            }
            typed = JsonSerializer.SerializeToElement(new
            {
                sha256 = CaptureSession.Hash(CaptureSession.Canonical(value)),
                paths = paths.Select(path => "generated/" + _session.Logical(path)).ToArray()
            });
        }
        var receipt = new CapturedParameter(name, contract.Type, typed,
            inputs.DistinctBy(input => (input.Path, input.Sha256)).OrderBy(input => input.Path, StringComparer.Ordinal).ToArray());
        CaptureSession.Require(invocation.Parameters.TryAdd(name, receipt), "CAPTURE_PARAMETER_DUPLICATE");
        _session.InvocationEvent("invocation-freeze", invocation.Id, JsonSerializer.SerializeToElement(receipt, Program.Json));
        if (invocation.Task == "Csc" && name == "UseSharedCompilation")
            CaptureSession.Require(value is false, "CAPTURE_COMPILER_SERVER_UNSUPPORTED");
        if (name is "MSBuildRuntime" or "MSBuildArchitecture")
            CaptureSession.Require(parameter.Items.Count == 1
                && parameter.Items[0]?.ToString() is "" or "CurrentRuntime" or "CurrentArchitecture" or "*",
                "CAPTURE_TASK_HOST_UNSUPPORTED");
        if (invocation.Task == "Csc" && name == "PathMap")
            _session.Setting(Path.Combine(_session.Work, invocation.Project["repo/".Length..]), "Csc.PathMap", typed);
    }

    private void ObserveJsonFactory(CustomBuildEventArgs args, IExtendedBuildEventArgs extended)
    {
        RequireAccepted();
        var request = extended.ExtendedMetadata ?? throw new CaptureException("CAPTURE_JSON_REQUEST_INVALID");
        var fields = request.ToDictionary(pair => pair.Key,
            pair => pair.Value ?? throw new CaptureException("CAPTURE_JSON_REQUEST_INVALID"), StringComparer.Ordinal);
        CaptureSession.Require(fields.Count == 7
            && _invocations.TryGetValue(Key(args.BuildEventContext), out _), "CAPTURE_JSON_REQUEST_INVALID");
        var invocation = _invocations[Key(args.BuildEventContext)];
        var state = invocation.JsonFactory ?? throw new CaptureException("CAPTURE_JSON_DISPATCH_UNSUPPORTED");
        CaptureSession.Require(state.Context.Equals(args.BuildEventContext)
            && fields["type"] == ReadWasmPropsProfile.AdapterTask
            && Path.GetFullPath(fields["assembly"]) == Path.GetFullPath(_session.Tool)
            && fields["thread"] == Environment.CurrentManagedThreadId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            && fields["values"].Length <= 1048576 && Guid.TryParseExact(fields["token"], "N", out _),
            "CAPTURE_JSON_REQUEST_INVALID");
        var values = JsonSerializer.Deserialize<JsonFactoryValue[]>(fields["values"], Program.Json)
            ?? throw new CaptureException("CAPTURE_JSON_REQUEST_INVALID");
        var contract = invocation.Contract.Parameters.OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();
        CaptureSession.Require(values.Length == contract.Length && values.Length == 6
            && values.Select(value => (value.Name, value.Type)).SequenceEqual(contract.Select(value => (value.Name, value.Type)))
            && values.Single(value => value.Name == "JsonFilePath") is { Bound: true, Value.ValueKind: JsonValueKind.String },
            "CAPTURE_JSON_SHAPE_UNSUPPORTED");
        var normalized = values.Select(value => new CapturedParameter(value.Name, value.Type,
            value.Value.ValueKind == JsonValueKind.Null ? JsonSerializer.SerializeToElement<object?>(null)
                : JsonSerializer.SerializeToElement(new { sha256 = CaptureSession.Hash(CaptureSession.Canonical(NormalizeJson(value.Value))) }),
            [])).ToArray();
        if (fields["phase"] == "freeze")
        {
            CaptureSession.Require(state.Token is null && _jsonTokens.Add(fields["token"])
                && state.InputPath.Length > 0 && bool.TryParse(fields["succeeded"], out bool succeeded) && !succeeded,
                "CAPTURE_JSON_REQUEST_REPLAY");
            for (int index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (value.Bound)
                    CaptureSession.Require(invocation.Parameters.TryGetValue(value.Name, out var native)
                        && CaptureSession.Canonical(native.Value).AsSpan().SequenceEqual(CaptureSession.Canonical(normalized[index].Value)),
                        "CAPTURE_JSON_INPUT_MISMATCH");
                else
                {
                    CaptureSession.Require(value.Name != "JsonFilePath" && value.Value.ValueKind == JsonValueKind.Null
                        && invocation.Parameters.TryAdd(value.Name, normalized[index]), "CAPTURE_JSON_INPUT_MISMATCH");
                    _session.InvocationEvent("invocation-freeze", invocation.Id, JsonSerializer.SerializeToElement(normalized[index], Program.Json));
                }
            }
            FileIdentity input = _session.Observe(state.InputPath, "asset");
            CaptureSession.Require(invocation.Parameters["JsonFilePath"].Inputs.SequenceEqual([input]), "CAPTURE_JSON_INPUT_MISMATCH");
            var tools = FreezeJsonTools(invocation);
            state.Token = fields["token"];
            state.Permit = new(state.InputPath, input, tools);
            var barrier = new JsonFactoryBarrier(invocation.Id, state.Token, ReadWasmPropsCaptureTask.Context(args.BuildEventContext),
                Environment.CurrentManagedThreadId, input, tools.Select(tool => tool.Identity).Distinct().OrderBy(tool => tool.Path, StringComparer.Ordinal).ToArray(),
                invocation.Parameters.Values.OrderBy(value => value.Name, StringComparer.Ordinal).ToArray());
            _session.InvocationEvent("json-factory-barrier", invocation.Id, JsonSerializer.SerializeToElement(barrier, Program.Json));
            fields.Add("permit", JsonSerializer.Serialize(state.Permit, Program.Json));
        }
        else
        {
            CaptureSession.Require(fields["phase"] == "outcome" && state.Token == fields["token"]
                && state.Permit is not null && state.Succeeded is null, "CAPTURE_JSON_REQUEST_REPLAY");
            CaptureSession.Require(CaptureSession.Canonical(invocation.Parameters["JsonFilePath"].Value).AsSpan()
                .SequenceEqual(CaptureSession.Canonical(normalized.Single(value => value.Name == "JsonFilePath").Value)),
                "CAPTURE_JSON_INPUT_MISMATCH");
            foreach (var tool in state.Permit!.Tools.Append(new(state.InputPath, state.Permit.Input)))
                CaptureSession.Require(File.Exists(tool.PhysicalPath) && new FileInfo(tool.PhysicalPath).Length == tool.Identity.Length
                    && CaptureSession.FileHash(tool.PhysicalPath) == tool.Identity.Sha256, "CAPTURE_JSON_INPUT_MUTATED");
            var admitted = state.Permit.Tools.Select(tool => tool.PhysicalPath).ToHashSet(StringComparer.Ordinal);
            CaptureSession.Require(JsonRuntimePaths().All(admitted.Contains), "CAPTURE_JSON_RUNTIME_CHANGED");
            CaptureSession.Require(bool.TryParse(fields["succeeded"], out bool succeeded), "CAPTURE_JSON_REQUEST_INVALID");
            state.Succeeded = succeeded;
            _session.InvocationEvent("json-factory-outcome", invocation.Id, JsonSerializer.SerializeToElement(
                new JsonFactoryOutcome(invocation.Id, state.Token!, succeeded,
                    normalized.Where(value => value.Name != "JsonFilePath").ToArray()), Program.Json));
        }
        fields.Add("ack-token", fields["token"]);
        fields.Add("ack-context", ReadWasmPropsCaptureTask.Context(args.BuildEventContext));
        fields.Add("ack-thread", fields["thread"]);
        fields.Add("ack-count", "1");
        foreach (var field in fields.Where(field => field.Key.StartsWith("ack-", StringComparison.Ordinal) || field.Key == "permit"))
            request.Add(field.Key, field.Value);
    }

    private static void RequirePreservedCaptureProperties(string properties)
    {
        foreach (string property in Microsoft.Build.Evaluation.ProjectCollection.Unescape(properties)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int equals = property.IndexOf('=');
            string name = equals < 0 ? property : property[..equals].Trim();
            if (ReadWasmPropsProfile.HostProperties.Contains(name, StringComparer.OrdinalIgnoreCase))
                throw new LoggerException("CAPTURE_CHILD_CAPTURE_PROPERTIES_UNSUPPORTED");
        }
    }

    private JsonElement NormalizeJson(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => JsonSerializer.SerializeToElement(_session.NormalizeSetting(value.GetString()!)),
        JsonValueKind.Array => JsonSerializer.SerializeToElement(value.EnumerateArray().Select(NormalizeJson).ToArray(), Program.Json),
        JsonValueKind.Object => JsonSerializer.SerializeToElement(value.EnumerateObject()
            .ToDictionary(property => property.Name, property => NormalizeJson(property.Value), StringComparer.Ordinal), Program.Json),
        _ => value
    };

    private JsonFactoryTool[] FreezeJsonTools(Invocation invocation)
    {
        string reader = Path.Combine(_session.Frozen, ReadWasmPropsProfile.Assembly);
        string recipe = Path.Combine(_session.Frozen, ReadWasmPropsProfile.Recipe);
        CaptureSession.Require(CaptureSession.FileHash(reader) == ReadWasmPropsProfile.AssemblyHash
            && CaptureSession.FileHash(recipe) == ReadWasmPropsProfile.RecipeHash, "CAPTURE_JSON_IDENTITY_UNSUPPORTED");
        // The finite reader assumption includes SDKUtilities and the native host,
        // including ICU. These are byte-bound dependencies, not a purity claim.
        _ = System.Globalization.CultureInfo.GetCultureInfo("en-US").CompareInfo.Compare("a", "b",
            System.Globalization.CompareOptions.IgnoreCase);
        var paths = JsonRuntimePaths()
            .Concat(Directory.EnumerateFiles(Path.GetDirectoryName(reader)!, "*", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(Path.Combine(_session.Frozen, "sdk/shared"), "*", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(Path.Combine(_session.Frozen, "sdk/host"), "*", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(Path.Combine(_session.Frozen, "sdk/sdk", CaptureSession.SdkVersion), "*.dll"))
            .Concat(Directory.EnumerateFiles(Path.GetDirectoryName(_session.Tool)!))
            .Append(recipe).Append(invocation.JsonFactory!.RecipePath)
            .Append(Path.Combine(_session.Work, "artifacts/capture-overrides.targets"))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var tools = new List<JsonFactoryTool>();
        foreach (string path in paths)
        {
            string frozen = path;
            if (!CaptureSession.Inside(path, _session.Frozen) && !CaptureSession.Inside(path, Path.GetDirectoryName(_session.Tool)!)
                && !CaptureSession.Inside(path, _session.Work))
            {
                CaptureSession.Unlinked(path);
                string hash = CaptureSession.FileHash(path);
                frozen = Path.Combine(Path.GetDirectoryName(_session.Tool)!, "host-runtime", hash, Path.GetFileName(path));
                Directory.CreateDirectory(Path.GetDirectoryName(frozen)!);
                if (!File.Exists(frozen)) File.Copy(path, frozen);
                CaptureSession.Require(CaptureSession.FileHash(frozen) == hash, "CAPTURE_JSON_RUNTIME_CHANGED");
            }
            tools.Add(new(path, _session.Observe(frozen, path == recipe || path == invocation.JsonFactory!.RecipePath
                || path.EndsWith("capture-overrides.targets", StringComparison.Ordinal) ? "recipe" : "tool")));
        }
        CaptureSession.Require(tools.Any(tool => tool.Identity == invocation.Assembly)
            && tools.Any(tool => tool.Identity == invocation.Recipe), "CAPTURE_JSON_DISPATCH_UNSUPPORTED");
        return tools.ToArray();
    }

    private static string[] JsonRuntimePaths()
    {
        CaptureSession.Require(OperatingSystem.IsLinux(), "CAPTURE_JSON_HOST_UNSUPPORTED");
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return process.Modules.Cast<System.Diagnostics.ProcessModule>().Select(module => module.FileName)
            .Concat(AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic).Select(assembly => assembly.Location))
            .Where(path => !string.IsNullOrEmpty(path) && File.Exists(path)).Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TargetEvent(BuildEventContext? context, string project, string target, string outcome, string? reason)
    {
        var required = context ?? throw new CaptureException("CAPTURE_TARGET_CONTEXT_MISSING");
        string id = $"{CaptureSession.CurrentGraph}/{required.NodeId}/{required.ProjectContextId}/{required.TargetId}";
        _session.InvocationEvent("target-event", id, JsonSerializer.SerializeToElement(
            new CapturedTarget(id, _session.Logical(project), target, outcome, reason), Program.Json));
    }

    private FileIdentity? Input(string task, string parameter, string value, string directory, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.IndexOfAny(['\n', '\r', '\0']) >= 0) return null;
        string path;
        try { path = Path.GetFullPath(value.Replace('\\', Path.DirectorySeparatorChar), directory); }
        catch (ArgumentException) { return null; }
        bool consumes = !CapturedBuildTask.IsDestination(task, parameter);
        FileIdentity? result = null;
        if (parameter == "ToolPath")
            CaptureSession.Require(_session.Logical(path).StartsWith("sdk/", StringComparison.Ordinal), "CAPTURE_TOOL_UNADMITTED");
        if (task == "Csc" && parameter == "ToolExe")
            CaptureSession.Require(value is "csc.dll" or "csc.exe", "CAPTURE_TOOL_UNADMITTED");
        if (!consumes)
        {
            CaptureSession.Require(path == _session.Work || path == Path.Combine(Parameters!, "private/tmp")
                || path == Path.Combine(Parameters!, "work/external-store")
                || CaptureSession.Inside(path, _session.Work)
                || CaptureSession.Inside(path, Path.Combine(Parameters!, "private/tmp"))
                || CaptureSession.Inside(path, Path.Combine(Parameters!, "work/external-store")), "CAPTURE_OUTPUT_ROOT_UNADMITTED");
        }
        if (File.Exists(path) && consumes)
        {
            result = _session.Observe(path, task is "Csc" or "GenerateResource" ? "compiler" : "asset");
            if (task == "ResolvePackageAssets" && parameter == "ProjectAssetsFile")
            {
                _session.PackageMetadata(path);
            }
        }
        else if (required && RequiredFiles.Contains(parameter) && !Directory.Exists(path)
            && (task is "Csc" or "GenerateResource" or "Copy" or "Move"
                || parameter is "SourceFiles" or "FilesToBundle" or "InputFiles"))
            throw new CaptureException("CAPTURE_CONSUMER_INPUT_MISSING");
        if (!consumes && File.Exists(path) && CaptureSession.Inside(path, _session.Work))
        {
            string logical = _session.Logical(path);
            if (File.Exists(Path.Combine(_session.Frozen, logical)))
                throw new CaptureException("CAPTURE_ORIGINAL_WRITE");
        }
        return result;
    }

    private void Properties(string project, IEnumerable? values)
    {
        if (values is null) return;
        foreach (object value in values)
        {
            if (value is not DictionaryEntry entry) continue;
            string name = entry.Key.ToString()!;
            string text = entry.Value?.ToString() ?? "";
            if (name == "MSBuildAllProjects")
                foreach (string path in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    if (File.Exists(path)) _session.Observe(path, "recipe");
            if (!SafeProperties.Contains(name)) continue;
            CaptureSession.Require(!text.Contains('/') && !text.Contains('\\') && text.Length <= 4096, "CAPTURE_SETTING_UNSAFE");
            JsonElement typed = bool.TryParse(text, out bool boolean) ? JsonSerializer.SerializeToElement(boolean)
                : JsonSerializer.SerializeToElement(text);
            _session.Setting(project, name, typed);
        }
    }

    private static (int, int, int, int) Key(BuildEventContext? context) =>
        context is null ? (-1, -1, -1, -1) : (context.NodeId, context.ProjectContextId, context.TargetId, context.TaskId);

    internal void RequireAccepted()
    {
        if (_failures.Count != 0)
            throw new CaptureException(_failures.Order(StringComparer.Ordinal).First());
    }

    internal void RejectJsonDeclaration() => _failures.Add("CAPTURE_JSON_DECLARATION_UNSUPPORTED");

    private void Diagnostic(object record, string? id)
    {
        try
        {
            if (++_diagnosticCount > 20000)
            {
                _failures.Add("CAPTURE_DIAGNOSTIC_LIMIT");
                if (_diagnosticCount != 20001) return;
                record = new { kind = "record-limit", id, availability = "count-limit" };
            }
            byte[] bytes = CaptureSession.Canonical(record);
            if (bytes.Length > 1048576)
            {
                _failures.Add("CAPTURE_DIAGNOSTIC_LIMIT");
                bytes = CaptureSession.Canonical(new { kind = "record-limit", id, availability = "size-limit" });
            }
            string directory = Path.Combine(Parameters!, "private/rejections");
            Directory.CreateDirectory(directory);
            if (new DirectoryInfo(directory).LinkTarget is not null)
            {
                _failures.Add("CAPTURE_DIAGNOSTIC_FAILED");
                return;
            }
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using var stream = new FileStream(Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json"), options);
            stream.Write(bytes);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _failures.Add("CAPTURE_DIAGNOSTIC_FAILED");
        }
    }

    public void Shutdown()
    {
        foreach (var diagnostic in _diagnostics.Values.Where(value => value.Rejected))
            Diagnostic(new { kind = "unfinished", id = diagnostic.Id, outcome = "unavailable", unobservedParameters = "unavailable" }, diagnostic.Id);
        if (_failures.Count == 0) return;
        foreach (string error in _failures.Order(StringComparer.Ordinal))
            Console.Error.WriteLine(error);
        _session.Rejected("CAPTURE_GRAPH_UNSUPPORTED");
    }
}
