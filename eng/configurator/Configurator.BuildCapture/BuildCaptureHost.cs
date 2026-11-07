using System.Runtime.CompilerServices;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace Configurator.BuildCapture;

/// <summary>
/// Hosts the pinned engine in one process. Synchronous dispatch and complete
/// declared task parameters are trusted engine contracts, guarded by a canary;
/// the public logging flag alone does not establish consumption ordering.
/// </summary>
internal static class BuildCaptureHost
{
    internal const string CanaryBefore = "configurator-calibration-before-execute\n";
    internal const string CanaryAfter = "configurator-calibration-after-execute\n";

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int Run(string sessionPath, string project, string target, string[] arguments)
    {
        var session = CaptureSession.Open(sessionPath);
        string jsonOverride = Path.Combine(session.Work, "artifacts/capture-overrides.targets");
        string toolsetRecipe = Path.Combine(session.Work, ReadWasmPropsProfile.ToolsetRecipe);
        CaptureSession.Require(File.Exists(jsonOverride)
            && File.ReadAllText(jsonOverride) == ReadWasmPropsProfile.OverrideProject(session.Tool).ToString(),
            "CAPTURE_JSON_OVERRIDE_CHANGED");
        CaptureSession.Require(File.Exists(toolsetRecipe)
            && File.ReadAllText(toolsetRecipe) == ReadWasmPropsProfile.ToolsetOverrideProject(session.Tool).ToString(),
            "CAPTURE_JSON_OVERRIDE_CHANGED");
        var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (string argument in arguments)
        {
            int equals = argument.IndexOf('=');
            CaptureSession.Require(equals > 0, "CAPTURE_HOST_PROPERTY_INVALID");
            properties[argument[..equals]] = argument[(equals + 1)..];
        }
        CaptureSession.Require(properties.GetValueOrDefault("ConfiguratorCaptureSession") == Path.GetFullPath(sessionPath)
            && properties.GetValueOrDefault("ConfiguratorCaptureTool") == session.Tool
            && properties.GetValueOrDefault("ConfiguratorCaptureOverrides") == jsonOverride
            && properties.GetValueOrDefault("CustomBeforeMicrosoftCommonTargets") == jsonOverride
            && properties.GetValueOrDefault("CustomAfterMicrosoftCommonProps")
                == Path.Combine(session.Work, "artifacts/capture-early.props"), "CAPTURE_HOST_PROPERTIES_UNSUPPORTED");
        properties["UseSharedCompilation"] = "false";
        properties["BuildInParallel"] = "false";
        session.ContractQueryOutcome(project, target, properties, null);
        string mode = properties.GetValueOrDefault("ConfiguratorCaptureCalibration") ?? "synchronous";
        CaptureSession.Require(mode is "synchronous" or "asynchronous" or "early-read", "CAPTURE_CALIBRATION_MODE_INVALID");
        Environment.SetEnvironmentVariable("CONFIGURATOR_CAPTURE_CALIBRATION", mode);
        string directory = Path.Combine(sessionPath, "private/tmp/calibration", CaptureSession.CurrentGraph);
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, "input.txt");
        string calibrationProject = Path.Combine(directory, "calibration.proj");
        File.WriteAllText(input, CanaryBefore);
        File.WriteAllText(calibrationProject, new XElement("Project",
            new XElement("UsingTask",
                new XAttribute("TaskName", typeof(BuildCaptureCalibrationTask).FullName!),
                new XAttribute("AssemblyFile", session.Tool)),
            new XElement("Target", new XAttribute("Name", "Calibrate"),
                new XElement(nameof(BuildCaptureCalibrationTask), new XAttribute("InputFile", input),
                    new XAttribute("FinalValue", "final-parameter"), new XAttribute("FinalNumber", "17"),
                    new XAttribute("FinalFlag", "true")))).ToString());
        using var collection = new ProjectCollection();
        ProjectRootElement[] defaultTaskDeclarations = [];
        var logger = new BuildCaptureLogger
        {
            Parameters = sessionPath,
            CalibrationProject = calibrationProject,
            CalibrationInput = input,
            DeferCalibration = mode == "asynchronous",
            ValidateJsonProject = (projectPath, globals) =>
            {
                var evaluated = collection.LoadProject(projectPath,
                    globals.ToDictionary(property => property.Key, property => (string?)property.Value,
                        StringComparer.OrdinalIgnoreCase), null);
                bool usesReader = evaluated.Imports.Select(import => import.ImportedProject).Append(evaluated.Xml)
                    .SelectMany(root => root.UsingTasks).Any(declaration =>
                        evaluated.ExpandString(declaration.TaskName).Split('.').Last() == ReadWasmPropsProfile.Name)
                    || evaluated.Targets.Values.Any(targetElement => targetElement.Tasks.Any(taskElement =>
                        taskElement.Name.Split('.').Last() == ReadWasmPropsProfile.Name));
                if (usesReader && (!evaluated.Imports.Any(import => import.ImportedProject.FullPath == jsonOverride)
                    || File.ReadAllText(jsonOverride) != ReadWasmPropsProfile.OverrideProject(session.Tool).ToString()))
                    throw new LoggerException("CAPTURE_JSON_OVERRIDE_REQUIRED");
            },
            ValidateTaskHost = (projectPath, taskName, targetName, globals, report) =>
            {
                var evaluated = collection.LoadProject(projectPath,
                    globals.ToDictionary(property => property.Key, property => (string?)property.Value,
                        StringComparer.OrdinalIgnoreCase), null);
                if (taskName == "Move" && targetName == "PublishConfigurator")
                    CaptureSession.Require(evaluated.Targets.TryGetValue(targetName, out var owner)
                        && owner.FullPath == Path.Combine(session.Work, "eng/configurator/Configurator.Release.proj"),
                        "CAPTURE_MOVE_RECIPE_UNSUPPORTED");
                var candidates = new List<Dictionary<string, object?>>();
                bool unsupported = false;
                bool failed = false;
                bool truncated = false;
                bool completed = false;
                int jsonOriginals = 0, jsonOverrides = 0, jsonToolsetOverrides = 0;
                try
                {
                    foreach (var declaration in evaluated.Imports.Select(import => import.ImportedProject).Append(evaluated.Xml)
                        .Concat(defaultTaskDeclarations).SelectMany(root => root.UsingTasks))
                    {
                        Dictionary<string, object?>? candidate = null;
                        try
                        {
                            if (evaluated.ExpandString(declaration.TaskName).Split('.').Last() != taskName)
                                continue;
                            // Order identifies candidates only, never the selected declaration.
                            if (candidates.Count < 128)
                            {
                                candidate = new Dictionary<string, object?>
                                {
                                    ["task"] = declaration.TaskName,
                                    ["recipe"] = session.Logical(declaration.ContainingProject.FullPath),
                                    ["line"] = declaration.Location.Line,
                                    ["condition"] = "unavailable",
                                    ["factory"] = null,
                                    ["runtime"] = null,
                                    ["architecture"] = null,
                                    ["selection"] = "unavailable"
                                };
                                candidates.Add(candidate);
                                if (declaration.TaskFactory.Length != 0)
                                    candidate["declarationMetadata"] = DeclarationMetadata(declaration, evaluated, session);
                                using var recipe = File.OpenRead(declaration.ContainingProject.FullPath);
                                candidate["recipeSha256"] = Convert.ToHexStringLower(
                                    System.Security.Cryptography.SHA256.HashData(recipe));
                            }
                            else truncated = true;
                            if (declaration.Condition.Length != 0)
                            {
                                // The condition probe has properties only, not the original item/metadata scope.
                                if (declaration.Condition.Contains("@(", StringComparison.Ordinal)
                                    || declaration.Condition.Contains("%(", StringComparison.Ordinal))
                                {
                                    failed = true;
                                    if (candidate is not null) candidate["evaluation"] = "unavailable";
                                    continue;
                                }
                                using var conditions = new ProjectCollection();
                                var condition = ProjectRootElement.Create(conditions);
                                condition.FullPath = Path.Combine(Path.GetDirectoryName(declaration.ContainingProject.FullPath)!,
                                    ".configurator-condition-" + Guid.NewGuid().ToString("N") + ".proj");
                                var admitted = condition.AddPropertyGroup().AddProperty("ConfiguratorCaptureUsingTask", "true");
                                admitted.Condition = evaluated.ExpandString(declaration.Condition);
                                if (new Project(condition, null, null, conditions).GetPropertyValue("ConfiguratorCaptureUsingTask") != "true")
                                {
                                    if (candidate is not null) candidate["condition"] = "inactive";
                                    continue;
                                }
                            }
                            if (candidate is not null) candidate["condition"] = "active";
                            string factory = evaluated.ExpandString(declaration.TaskFactory);
                            string runtime = evaluated.ExpandString(declaration.Runtime);
                            string architecture = evaluated.ExpandString(declaration.Architecture);
                            if (candidate is not null)
                            {
                                candidate["factory"] = factory;
                                candidate["runtime"] = runtime;
                                candidate["architecture"] = architecture;
                            }
                            if (taskName == ReadWasmPropsProfile.Name)
                            {
                                string recipePath = Path.GetFullPath(declaration.ContainingProject.FullPath);
                                string overridePath = Path.Combine(session.Work, "artifacts/capture-overrides.targets");
                                bool isOverride = recipePath == overridePath
                                    && declaration.TaskName == ReadWasmPropsProfile.Name
                                    && factory == ReadWasmPropsProfile.AdapterFactory
                                    && File.ReadAllText(overridePath) == ReadWasmPropsProfile.OverrideProject(session.Tool).ToString();
                                bool isToolsetOverride = recipePath == toolsetRecipe
                                    && declaration.TaskName == ReadWasmPropsProfile.Name
                                    && factory == ReadWasmPropsProfile.AdapterFactory
                                    && File.ReadAllText(toolsetRecipe) == ReadWasmPropsProfile.ToolsetOverrideProject(session.Tool).ToString();
                                bool isOriginal = session.Logical(recipePath) == ReadWasmPropsProfile.Recipe
                                    && CaptureSession.FileHash(recipePath) == ReadWasmPropsProfile.RecipeHash
                                    && declaration.TaskName == ReadWasmPropsProfile.Name
                                    && factory == ReadWasmPropsProfile.Factory
                                    && declaration.TaskBody is null
                                    && declaration.ParameterGroup is not null
                                    && CaptureSession.Hash(System.Text.Encoding.UTF8.GetBytes(
                                        declaration.ParameterGroup.OuterElement)) == ReadWasmPropsProfile.GroupHash
                                    && declaration.AssemblyName.Length == 0
                                    && session.Logical(Path.GetFullPath(evaluated.ExpandString(declaration.AssemblyFile)
                                        .Replace('\\', Path.DirectorySeparatorChar), Path.GetDirectoryName(recipePath)!))
                                        == ReadWasmPropsProfile.Assembly;
                                CaptureSession.Require(runtime.Length == 0 && architecture.Length == 0
                                    && (isOriginal || isOverride || isToolsetOverride), "CAPTURE_JSON_DECLARATION_UNSUPPORTED");
                                if (isOriginal) jsonOriginals++;
                                if (isOverride) jsonOverrides++;
                                if (isToolsetOverride) jsonToolsetOverrides++;
                                continue;
                            }
                            if (factory.Length != 0 || runtime is not ("" or "NET" or "CurrentRuntime" or "*")
                                || !(architecture is "" or "CurrentArchitecture" or "*"
                                    || architecture.Equals(System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                                        StringComparison.OrdinalIgnoreCase)))
                            {
                                if (!unsupported)
                                    File.AppendAllText(Path.Combine(sessionPath, "private/task-hosts.log"),
                                        System.Text.Json.JsonSerializer.Serialize(new
                                        {
                                            taskName,
                                            factory,
                                            runtime,
                                            architecture,
                                            declaration = session.Logical(declaration.ContainingProject.FullPath),
                                            conditional = declaration.Condition.Length != 0
                                        }) + "\n");
                                unsupported = true;
                            }
                        }
                        catch (Exception error) when (error is Microsoft.Build.Exceptions.InvalidProjectFileException
                            or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or CaptureException)
                        {
                            failed = true;
                            if (candidate is not null) candidate["evaluation"] = "unavailable";
                        }
                    }
                    completed = true;
                    if (taskName == ReadWasmPropsProfile.Name)
                        CaptureSession.Require(jsonOriginals == 1 && jsonOverrides == 1 && jsonToolsetOverrides == 1 && !truncated && !failed,
                            "CAPTURE_JSON_DECLARATION_UNSUPPORTED");
                }
                finally
                {
                    report(System.Text.Json.JsonSerializer.SerializeToElement(new
                    {
                        candidates,
                        completeness = !completed || failed ? "failed" : truncated ? "truncated" : "completed",
                        truncated
                    }, Program.Json));
                }
                CaptureSession.Require(!unsupported && !failed, "CAPTURE_TASK_HOST_UNSUPPORTED");
            }
        };
        collection.ProjectAdded += (_, args) =>
        {
            ProjectRootElement root = args.ProjectRootElement;
            try
            {
                foreach (ProjectUsingTaskElement declaration in root.UsingTasks)
                {
                    string name = declaration.TaskName.Split('.').Last();
                    bool fixedLeaf = name.Length > 0 && name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
                    bool maySelectReader = name.Equals(ReadWasmPropsProfile.Name, StringComparison.OrdinalIgnoreCase)
                        || declaration.TaskName.Contains("$(", StringComparison.Ordinal) && !fixedLeaf;
                    if (!maySelectReader)
                        continue;
                    bool original = root.FullPath == Path.Combine(session.Frozen, ReadWasmPropsProfile.Recipe)
                        && CaptureSession.FileHash(root.FullPath) == ReadWasmPropsProfile.RecipeHash
                        && declaration.TaskName == ReadWasmPropsProfile.Name
                        && declaration.TaskFactory == ReadWasmPropsProfile.Factory;
                    bool generated = root.FullPath == jsonOverride
                        && XElement.Parse(root.RawXml).ToString() == ReadWasmPropsProfile.OverrideProject(session.Tool).ToString();
                    bool toolset = root.FullPath == toolsetRecipe
                        && XElement.Parse(root.RawXml).ToString() == ReadWasmPropsProfile.ToolsetOverrideProject(session.Tool).ToString();
                    CaptureSession.Require(original || generated || toolset, "CAPTURE_JSON_DECLARATION_UNSUPPORTED");
                }
            }
            catch (CaptureException error)
            {
                logger.RejectJsonDeclaration();
                // A failed load can remain cached and be reused after ContinueOnError.
                // Retire that XML before refusal so every subsequent request is admitted again.
                collection.UnloadProject(root);
                throw new Microsoft.Build.Exceptions.InvalidProjectFileException(error.Code);
            }
        };
        Toolset originalToolset = collection.GetToolset(collection.DefaultToolsVersion)!;
        collection.AddToolset(new Toolset(originalToolset.ToolsVersion, originalToolset.ToolsPath,
            originalToolset.Properties.ToDictionary(property => property.Key, property => property.Value.EvaluatedValue,
                StringComparer.OrdinalIgnoreCase), collection, Path.GetDirectoryName(toolsetRecipe)!));
        defaultTaskDeclarations = CaptureSession.DefaultTaskRecipes
            .Select(name => ProjectRootElement.Open(Path.Combine(session.Frozen, "sdk/sdk", CaptureSession.SdkVersion, name), collection))
            .Append(ProjectRootElement.Open(toolsetRecipe, collection)).ToArray();
        var parameters = new BuildParameters(collection)
        {
            Loggers = [logger],
            MaxNodeCount = 1,
            EnableNodeReuse = false,
            DisableInProcNode = false,
            ShutdownInProcNodeOnBuildFinish = true,
            UseSynchronousLogging = mode != "asynchronous"
        };
        // Validate the entry before MSBuild can initialize its task factories;
        // ProjectStarted is too late for an omitted override.
        try
        {
            logger.ValidateJsonProject(project, properties.ToDictionary(property => property.Key,
                property => property.Value ?? "", StringComparer.OrdinalIgnoreCase));
        }
        catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
        {
            logger.RequireAccepted();
            throw;
        }
        using var manager = new BuildManager();
        manager.BeginBuild(parameters);
        try
        {
            session.Observe(toolsetRecipe, "recipe");
            var calibration = manager.PendBuildRequest(new BuildRequestData(
                calibrationProject, properties, null, ["Calibrate"], null)).Execute();
            CaptureSession.Require(!File.Exists(input + ".early"), "CAPTURE_EARLY_READ_UNSUPPORTED");
            logger.RequireAccepted();
            CaptureSession.Require(calibration.OverallResult == BuildResultCode.Success
                && logger.CalibrationObserved && mode == "synchronous", "CAPTURE_CALIBRATION_FAILED");
            session.Setting(project, "capture.eventBoundary", System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                engine = CaptureSession.SdkVersion,
                synchronousLogging = true,
                maxNodeCount = 1,
                nodeReuse = false,
                sharedCompilation = false,
                processorCount = 2,
                assumption = "pinned-engine-ordering-and-declared-parameter-completeness",
                calibration = "input-frozen-before-execute"
            }));
            // The command-line restore switch skips projects without a Restore
            // target. The Build API takes literal targets instead, so retain
            // that distinction for package-free orchestration projects.
            if (target == "Restore" && !collection.LoadProject(project, properties, null).Targets.ContainsKey("Restore"))
            {
                session.InvocationEvent("target-event", CaptureSession.CurrentGraph + "/host-restore",
                    System.Text.Json.JsonSerializer.SerializeToElement(new CapturedTarget(
                        CaptureSession.CurrentGraph + "/host-restore", session.Logical(project), "Restore", "skipped",
                        "HostRestoreTargetNotDefined"), Program.Json));
                return 0;
            }
            var result = manager.PendBuildRequest(new BuildRequestData(project, properties, null, [target], null)).Execute();
            logger.RequireAccepted();
            session.ContractQueryOutcome(project, target, properties, result.OverallResult == BuildResultCode.Success);
            return result.OverallResult == BuildResultCode.Success ? 0 : 1;
        }
        finally
        {
            manager.EndBuild();
            logger.RequireAccepted();
        }
    }
    private static Dictionary<string, object?> DeclarationMetadata(
        ProjectUsingTaskElement declaration, Project evaluated, CaptureSession session)
    {
        var unavailable = new { availability = "unavailable", sha256 = (string?)null };
        var result = new Dictionary<string, object?>
        {
            ["assemblyKind"] = "unavailable",
            ["assemblyReference"] = unavailable,
            ["assembly"] = new { availability = "unavailable", path = (string?)null, sha256 = (string?)null },
            ["factoryTypeDefinition"] = new { availability = "unavailable" },
            ["parameterDeclarations"] = new { availability = "unavailable", parameters = Array.Empty<object>() },
            ["initialization"] = unavailable,
            ["parameterGroup"] = unavailable
        };
        try
        {
            // Declaration fingerprints do not establish factory initialization semantics.
            result["initialization"] = DeclarationIdentity(declaration.TaskBody?.OuterElement);
            result["parameterGroup"] = DeclarationIdentity(declaration.ParameterGroup?.OuterElement);
            result["parameterDeclarations"] = DeclaredParameters(declaration.ParameterGroup);
            bool file = declaration.AssemblyFile.Length != 0;
            result["assemblyKind"] = file ? "AssemblyFile"
                : declaration.AssemblyName.Length != 0 ? "AssemblyName" : "unavailable";
            string reference = evaluated.ExpandString(file
                ? declaration.AssemblyFile : declaration.AssemblyName).Trim();
            result["assemblyReference"] = DeclarationIdentity(reference);
            // Never resolve AssemblyName by loading an assembly or a factory.
            if (file && reference.Length is > 0 and <= 16384)
            {
                string path = Path.GetFullPath(reference.Replace('\\', Path.DirectorySeparatorChar),
                    Path.GetDirectoryName(declaration.ContainingProject.FullPath)!);
                string alias = session.Logical(path);
                CaptureSession.Unlinked(path);
                if (alias.Length <= 16384)
                {
                    using var content = File.OpenRead(path);
                    long length = content.Length;
                    if (length > 67108864)
                        result["assembly"] = new { availability = "size-limit", path = alias, sha256 = (string?)null };
                    else
                    {
                        byte[] bytes = new byte[(int)length];
                        content.ReadExactly(bytes);
                        CaptureSession.Require(content.ReadByte() == -1, "CAPTURE_DECLARATION_CHANGED");
                        result["assembly"] = new { availability = "observed", path = alias, sha256 = CaptureSession.Hash(bytes) };
                        result["factoryTypeDefinition"] = new
                        {
                            availability = ContainsTypeDefinition(bytes, evaluated.ExpandString(declaration.TaskFactory)) ? "observed" : "unavailable"
                        };
                    }
                }
            }
        }
        catch (Exception error) when (error is Microsoft.Build.Exceptions.InvalidProjectFileException
            or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or CaptureException)
        {
            // Unacquired fields retain explicit unavailable states; admission is unchanged.
        }
        return result;
    }

    private static object DeclaredParameters(UsingTaskParameterGroupElement? group)
    {
        object[] empty = [];
        if (group is null) return new { availability = "absent", parameters = empty };
        if (group.Parameters.Count > 128) return new { availability = "count-limit", parameters = empty };
        var parameters = new List<object>();
        int characters = 0;
        foreach (ProjectUsingTaskParameterElement parameter in group.Parameters)
        {
            string name = parameter.Name;
            string type = parameter.ParameterType;
            bool required = false, output = false;
            if (name.Length is 0 or > 256 || !name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_') ||
                type.Length is 0 or > 4096 || !type.All(character => char.IsAsciiLetterOrDigit(character) || "._+[]`,= ".Contains(character)) ||
                parameter.Required.Length > 0 && !bool.TryParse(parameter.Required, out required) ||
                parameter.Output.Length > 0 && !bool.TryParse(parameter.Output, out output))
                return new { availability = "unavailable", parameters = empty };
            characters += name.Length + type.Length;
            if (characters > 16384) return new { availability = "size-limit", parameters = empty };
            parameters.Add(new { name, type, required, output });
        }
        return new { availability = "observed", parameters = parameters.ToArray() };
    }

    private static bool ContainsTypeDefinition(byte[] bytes, string factory)
    {
        try
        {
            using var image = new PEReader(new MemoryStream(bytes, writable: false));
            if (!image.HasMetadata) return false;
            MetadataReader metadata = image.GetMetadataReader();
            int matches = 0;
            foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
            {
                TypeDefinition type = metadata.GetTypeDefinition(handle);
                if (!type.GetDeclaringType().IsNil) continue;
                string space = metadata.GetString(type.Namespace);
                string name = metadata.GetString(type.Name);
                if ((space.Length == 0 ? name : space + "." + name) == factory) ++matches;
            }
            return matches == 1;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static object DeclarationIdentity(string? value)
    {
        string availability = string.IsNullOrEmpty(value) ? "absent"
            : value.Length > 16384 || System.Text.Encoding.UTF8.GetByteCount(value) > 16384
                ? "size-limit" : "observed";
        return new
        {
            availability,
            sha256 = availability == "observed" && value is not null
                ? CaptureSession.Hash(System.Text.Encoding.UTF8.GetBytes(value)) : null
        };
    }
}

/// <summary>Overwrites its own synthetic input only after checking the independently written receipt.</summary>
public sealed class BuildCaptureCalibrationTask : Microsoft.Build.Utilities.Task
{
    private string _inputFile = "";

    [Required]
    public string InputFile
    {
        get => _inputFile;
        set
        {
            _inputFile = value;
            if (Environment.GetEnvironmentVariable("CONFIGURATOR_CAPTURE_CALIBRATION") == "early-read")
            {
                _ = File.ReadAllBytes(value);
                File.WriteAllText(value + ".early", "setter-read");
            }
        }
    }

    [Required] public string FinalValue { get; set; } = "";
    [Required] public int FinalNumber { get; set; }
    public bool FinalFlag { get; set; }

    public override bool Execute()
    {
        bool frozen = File.Exists(InputFile + ".receipt") && File.Exists(InputFile + ".frozen")
            && File.ReadAllText(InputFile + ".frozen") == BuildCaptureHost.CanaryBefore;
        if (frozen)
        {
            using var receipt = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(InputFile + ".receipt"));
            frozen = receipt.RootElement.GetProperty("inputHash").GetString()
                    == CaptureSession.Hash(System.Text.Encoding.UTF8.GetBytes(BuildCaptureHost.CanaryBefore))
                && receipt.RootElement.GetProperty("finalValue").GetString() == FinalValue
                && receipt.RootElement.GetProperty("finalNumber").GetInt32() == FinalNumber
                && receipt.RootElement.GetProperty("finalFlag").GetBoolean() == FinalFlag;
        }
        File.WriteAllText(InputFile, BuildCaptureHost.CanaryAfter);
        File.WriteAllText(InputFile + ".entered", "execute");
        if (!frozen) Log.LogError("CAPTURE_CALIBRATION_ORDERING");
        return frozen;
    }
}
