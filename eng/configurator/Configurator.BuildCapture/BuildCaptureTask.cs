using Microsoft.Build.Framework;
using System.Reflection;
using System.Xml.Linq;

namespace Configurator.BuildCapture;

/// <summary>Runs an explicitly admitted child graph with the same capture owner.</summary>
public sealed class BuildCaptureTask : Microsoft.Build.Utilities.Task
{
    [Required] public string Session { get; set; } = "";
    [Required] public string Project { get; set; } = "";
    public string Target { get; set; } = "Build";
    public ITaskItem[] Properties { get; set; } = [];
    public string Operation { get; set; } = "build";
    public ITaskItem[] Arguments { get; set; } = [];
    [Output] public string ProjectAssetsFile { get; set; } = "";
    public ITaskItem[] RuntimePacks { get; set; } = [];
    public ITaskItem[] FrameworkReferences { get; set; } = [];
    public string InventoryOutput { get; set; } = "";

    public override bool Execute()
    {
        try
        {
            var session = CaptureSession.Open(Session);
            if (Operation == "frameworks")
            {
                CaptureSession.Require(CaptureSession.Inside(Path.GetFullPath(InventoryOutput), Path.Combine(Session, "private/tmp")),
                    "CAPTURE_OUTPUT_ROOT_UNADMITTED");
                File.WriteAllBytes(InventoryOutput, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
                {
                    Items = new
                    {
                        RuntimePack = RuntimePacks.Select(item => new
                        {
                            Identity = item.ItemSpec, FrameworkName = item.GetMetadata("FrameworkName"),
                            NuGetPackageId = item.GetMetadata("NuGetPackageId"), NuGetPackageVersion = item.GetMetadata("NuGetPackageVersion")
                        }).ToArray(),
                        FrameworkReference = FrameworkReferences.Select(item => new { Identity = item.ItemSpec }).ToArray()
                    }
                }));
                return true;
            }
            if (Operation == "assets")
            {
                ProjectAssetsFile = session.Assets(Project);
                return true;
            }
            int exit = Operation == "build"
                ? session.Build(Project, Target, Properties.Select(item => item.ItemSpec))
                : session.Generator(Operation, Arguments.Select(item => item.ItemSpec).ToArray());
            if (exit != 0) Log.LogError("CAPTURE_CHILD_FAILED");
            return exit == 0;
        }
        catch (CaptureException error)
        {
            Log.LogError(error.Code);
            return false;
        }
    }
}

/// <summary>
/// Binds admitted SDK tasks through their public parameter contract, keeping byte
/// observation at Execute rather than relying on asynchronous logger delivery.
/// </summary>
public sealed class BuildCaptureTaskFactory : ITaskFactory2
{
    private Type _implementation = null!;
    private string _session = "";
    private string _project = "";
    private string _directory = "";
    private TaskPropertyInfo[] _parameters = [];
    public string FactoryName => nameof(BuildCaptureTaskFactory);
    public Type TaskType => typeof(CapturedBuildTask);

    public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup,
        string taskBody, IBuildEngine taskFactoryLoggingHost)
    {
        var binding = XElement.Parse(taskBody);
        string assembly = binding.Attribute("Assembly")!.Value;
        string type = binding.Attribute("Type")!.Value;
        _session = binding.Attribute("Session")!.Value;
        _project = binding.Attribute("Project")!.Value;
        _directory = binding.Attribute("Directory")!.Value;
        CaptureSession.Require(Path.IsPathRooted(_project) && Path.IsPathRooted(_directory), "CAPTURE_TASK_CONTEXT_UNBOUND");
        var session = CaptureSession.Open(_session);
        session.Observe(assembly, "tool");
        _implementation = Assembly.LoadFrom(assembly).GetType(type, throwOnError: true)!;
        CaptureSession.Require(typeof(ITask).IsAssignableFrom(_implementation)
            && BuildCaptureLogger.KnownTasks.Contains(_implementation.Name), "CAPTURE_TASK_FACTORY_UNADMITTED");
        _parameters = _implementation.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod?.IsPublic == true
                && (property.SetMethod?.IsPublic == true || property.IsDefined(typeof(OutputAttribute)))
                && property.Name is not ("BuildEngine" or "HostObject")
                // This SDK service obtains the current directory/environment from
                // BuildEngine9. It is not an XML task parameter or an environment dump.
                && property.PropertyType != typeof(TaskEnvironment))
            .Select(property => new TaskPropertyInfo(property.Name, property.PropertyType,
                property.IsDefined(typeof(OutputAttribute)), property.IsDefined(typeof(RequiredAttribute)))).ToArray();
        return true;
    }

    public TaskPropertyInfo[] GetTaskParameters() => _parameters;
    public bool Initialize(string taskName, IDictionary<string, string> factoryIdentityParameters,
        IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost)
    {
        ValidateIdentity(factoryIdentityParameters);
        return Initialize(taskName, parameterGroup, taskBody, taskFactoryLoggingHost);
    }

    public ITask CreateTask(IBuildEngine taskFactoryLoggingHost) =>
        new CapturedBuildTask((ITask)Activator.CreateInstance(_implementation)!, _session, _project, _directory, _parameters);
    public ITask CreateTask(IBuildEngine taskFactoryLoggingHost, IDictionary<string, string> taskIdentityParameters)
    {
        ValidateIdentity(taskIdentityParameters);
        return CreateTask(taskFactoryLoggingHost);
    }

    private static void ValidateIdentity(IDictionary<string, string> values)
    {
        foreach ((string name, string value) in values)
            CaptureSession.Require(name == "Runtime" && (string.IsNullOrEmpty(value) || value is "NET" or "CurrentRuntime" or "*")
                || name == "Architecture" && (string.IsNullOrEmpty(value) || value is "CurrentArchitecture" or "*"
                    || value.Equals(System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), StringComparison.OrdinalIgnoreCase))
                || name == "TaskHostFactoryExplicitlyRequested" && string.IsNullOrEmpty(value), "CAPTURE_TASK_HOST_UNSUPPORTED");
    }
    public void CleanupTask(ITask task)
    {
        if (task is CapturedBuildTask captured) captured.Dispose();
    }
}

public sealed class CapturedBuildTask(ITask implementation, string sessionPath, string project, string directory, TaskPropertyInfo[] parameters)
    : IGeneratedTask, ICancelableTask, IDisposable
{
    private readonly HashSet<string> _boundParameters = new(StringComparer.OrdinalIgnoreCase);
    public IBuildEngine BuildEngine { get => implementation.BuildEngine; set => implementation.BuildEngine = value; }
    public ITaskHost HostObject { get => implementation.HostObject; set => implementation.HostObject = value; }
    public void SetPropertyValue(TaskPropertyInfo property, object value)
    {
        _boundParameters.Add(property.Name);
        implementation.GetType().GetProperty(property.Name)!.SetValue(implementation, value);
    }
    public object GetPropertyValue(TaskPropertyInfo property) =>
        implementation.GetType().GetProperty(property.Name)!.GetValue(implementation)!;

    public bool Execute()
    {
        var session = CaptureSession.Open(sessionPath);
        var inputs = new List<(string Path, string Hash)>();
        try
        {
            session.Consumer(project, implementation.GetType().Name, expected: false);
            if (implementation.GetType().Name == "Csc")
            {
                CaptureSession.Require(implementation.GetType().GetProperty("UseSharedCompilation")!.GetValue(implementation) is false,
                    "CAPTURE_COMPILER_SERVER_UNSUPPORTED");
                object? pathMap = implementation.GetType().GetProperty("PathMap")!.GetValue(implementation);
                session.Setting(project, "Csc.PathMap", System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    type = typeof(string).FullName,
                    sha256 = CaptureSession.Hash(CaptureSession.Canonical(Describe(pathMap, session)))
                }));
            }
            foreach (var parameter in parameters.Where(parameter => _boundParameters.Contains(parameter.Name)))
            {
                object? parameterValue = GetPropertyValue(parameter);
                if (parameterValue is bool boolean)
                    session.Setting(project, implementation.GetType().Name + "." + parameter.Name,
                        System.Text.Json.JsonSerializer.SerializeToElement(boolean));
                else if (parameterValue is int number)
                    session.Setting(project, implementation.GetType().Name + "." + parameter.Name,
                        System.Text.Json.JsonSerializer.SerializeToElement(number));
                else
                    session.Setting(project, implementation.GetType().Name + "." + parameter.Name,
                        System.Text.Json.JsonSerializer.SerializeToElement(new
                        {
                            type = parameter.PropertyType.FullName,
                            sha256 = CaptureSession.Hash(CaptureSession.Canonical(Describe(parameterValue, session)))
                        }));
                bool destination = IsDestination(implementation.GetType().Name, parameter.Name);
                foreach (string value in Values(GetPropertyValue(parameter), includeMetadata: !destination))
                {
                    if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['\n', '\r', '\0']) >= 0) continue;
                    if (implementation.GetType().Name == "Csc" && parameter.Name == "ToolExe")
                        CaptureSession.Require(value is "csc.dll" or "csc.exe", "CAPTURE_TOOL_UNADMITTED");
                    string path = Path.GetFullPath(value.Replace('\\', Path.DirectorySeparatorChar), directory);
                    if (parameter.Name == "ToolPath")
                        CaptureSession.Require(session.Logical(path).StartsWith("sdk/", StringComparison.Ordinal), "CAPTURE_TOOL_UNADMITTED");
                    if (destination)
                    {
                        bool admitted = path == session.Work || path == Path.Combine(sessionPath, "private/tmp")
                            || path == Path.Combine(sessionPath, "work/external-store")
                            || CaptureSession.Inside(path, session.Work)
                            || CaptureSession.Inside(path, Path.Combine(sessionPath, "private/tmp"))
                            || CaptureSession.Inside(path, Path.Combine(sessionPath, "work/external-store"));
                        if (!admitted)
                            File.AppendAllText(Path.Combine(sessionPath, "private/rejected-consumers.log"),
                                implementation.GetType().FullName + " " + parameter.Name + " " + path + "\n");
                        CaptureSession.Require(admitted, "CAPTURE_OUTPUT_ROOT_UNADMITTED");
                        continue;
                    }
                    if (!File.Exists(path)) continue;
                    session.Observe(path, "asset");
                    if (implementation.GetType().Name == "ResolvePackageAssets" && parameter.Name == "ProjectAssetsFile")
                        session.RecordAssets(project, path);
                    inputs.Add((path, CaptureSession.FileHash(path)));
                }
            }
            bool result = implementation.Execute();
            if (!result) session.Rejected("CAPTURE_TASK_FAILED");
            foreach ((string path, string hash) in inputs)
            {
                bool unchanged = File.Exists(path) ? CaptureSession.FileHash(path) == hash
                    : implementation.GetType().Name == "Move"
                        && !File.Exists(Path.Combine(session.Frozen, session.Logical(path)));
                if (!unchanged)
                    File.AppendAllText(Path.Combine(sessionPath, "private/mutated-consumers.log"),
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            task = implementation.GetType().FullName, input = session.Logical(path), before = hash,
                            after = File.Exists(path) ? CaptureSession.FileHash(path) : null
                        }) + "\n");
                CaptureSession.Require(unchanged, "CAPTURE_CONSUMER_MUTATION");
            }
            return result;
        }
        catch (CaptureException error)
        {
            session.Rejected(error.Code);
            BuildEngine.LogErrorEvent(new BuildErrorEventArgs("", error.Code, "", 0, 0, 0, 0, error.Code, "", nameof(CapturedBuildTask)));
            return false;
        }
    }

    internal static bool IsDestination(string task, string name) =>
        name is "DestinationFiles" or "DestinationFolder" or "OutputFile" or "OutputAssembly" or "Directories"
            or "FileWrites" or "OutputPath" or "CacheFile" or "FilesWritten" or "DestinationFile"
            or "OutputDirectory" or "AppHostDestinationPath" or "OutputResources" or "OutputRefAssembly"
            or "GeneratedFilesOutputPath" or "DocumentationFile" or "PdbFile"
        || task is "Delete" or "RemoveDir" or "MakeDir" or "Touch"
        || task == "GenerateAvaloniaResourcesTask" && name == "Output"
        || task == "WriteLinesToFile" && name == "File";

    private static IEnumerable<string> Values(object? value, bool includeMetadata)
    {
        if (value is string text) yield return text;
        else if (value is ITaskItem item)
        {
            yield return item.ItemSpec;
            if (includeMetadata)
                foreach (System.Collections.DictionaryEntry metadata in item.CloneCustomMetadata())
                    if (metadata.Value is string textValue) yield return textValue;
        }
        else if (value is System.Collections.IEnumerable items)
            foreach (object? entry in items)
                foreach (string textValue in Values(entry, includeMetadata)) yield return textValue;
    }

    internal static object? Describe(object? value, CaptureSession session) => value switch
    {
        null => null,
        string text => session.NormalizeSetting(text),
        bool or int or long or double or decimal => value,
        Enum enumeration => enumeration.ToString(),
        ITaskItem item => new
        {
            item = session.NormalizeSetting(item.ItemSpec),
            metadata = item.CloneCustomMetadata().Keys.Cast<string>()
                .Order(StringComparer.Ordinal)
                .Select(name => new { name, value = session.NormalizeSetting(item.GetMetadata(name)) }).ToArray()
        },
        System.Collections.IEnumerable items => items.Cast<object?>().Select(entry => Describe(entry, session)).ToArray(),
        _ => throw new CaptureException("CAPTURE_PARAMETER_CATEGORY_UNSUPPORTED_" + value.GetType().Name)
    };

    public void Cancel()
    {
        if (implementation is ICancelableTask cancellable) cancellable.Cancel();
    }

    public void Dispose()
    {
        if (implementation is IDisposable disposable) disposable.Dispose();
        GC.SuppressFinalize(this);
    }
}
