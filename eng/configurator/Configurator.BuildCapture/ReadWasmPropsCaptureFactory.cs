using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Build.Framework;

namespace Configurator.BuildCapture;

/// <summary>Describes only the inert first-party contract. SDK activation belongs after the native receipt.</summary>
public sealed class ReadWasmPropsCaptureFactory : ITaskFactory
{
    private IBuildEngine? _initializationHost;
    public string FactoryName => nameof(ReadWasmPropsCaptureFactory);
    public Type TaskType => typeof(ReadWasmPropsCaptureTask);

    public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup,
        string taskBody, IBuildEngine taskFactoryLoggingHost)
    {
        bool valid = taskName == ReadWasmPropsProfile.Name && parameterGroup.Count == 0
            && taskBody.Trim() == ReadWasmPropsProfile.Body;
        _initializationHost = valid ? taskFactoryLoggingHost : null;
        return valid;
    }

    public TaskPropertyInfo[] GetTaskParameters() =>
        ReadWasmPropsProfile.Outputs.Select(name => new TaskPropertyInfo(name, typeof(ITaskItem[]), true, false))
            .Append(new("JsonFilePath", typeof(string), false, true)).ToArray();

    public ITask CreateTask(IBuildEngine taskFactoryLoggingHost) => new ReadWasmPropsCaptureTask(
        _initializationHost ?? throw new InvalidOperationException("CAPTURE_JSON_INITIALIZATION_FAILED"), taskFactoryLoggingHost);
    public void CleanupTask(ITask task) => ((ReadWasmPropsCaptureTask)task).Dispose();
}

/// <summary>
/// Retains native parameters without touching the SDK reader. The synchronous
/// native-context acknowledgement is required before loading any delegated code.
/// </summary>
public sealed class ReadWasmPropsCaptureTask : ICancelableTask, IDisposable
{
    private readonly object _gate = new();
    private readonly IBuildEngine _initializationHost;
    private readonly IBuildEngine _creationHost;
    private readonly ITaskItem[]?[] _outputs = new ITaskItem[]?[5];
    private readonly bool[] _bound = new bool[5];
    private ITaskFactory? _factory;
    private ITask? _task;
    private Dictionary<string, TaskPropertyInfo>? _parameters;
    private ReaderContext? _context;
    private bool _cancelled;
    private bool _disposed;
    private bool _entered;

    internal ReadWasmPropsCaptureTask(IBuildEngine initializationHost, IBuildEngine creationHost)
    {
        _initializationHost = initializationHost;
        _creationHost = creationHost;
    }

    public IBuildEngine BuildEngine { get; set; } = null!;
    public ITaskHost HostObject { get; set; } = null!;
    [Required] public string JsonFilePath { get; set; } = "";
    [Output] public ITaskItem[]? EmccDefaultExportedFunctions { get => _outputs[0]; set { _outputs[0] = value; _bound[0] = true; } }
    [Output] public ITaskItem[]? EmccDefaultExportedRuntimeMethods { get => _outputs[1]; set { _outputs[1] = value; _bound[1] = true; } }
    [Output] public ITaskItem[]? EmccProperties { get => _outputs[2]; set { _outputs[2] = value; _bound[2] = true; } }
    [Output] public ITaskItem[]? PropertiesThatTriggerRelinking { get => _outputs[3]; set { _outputs[3] = value; _bound[3] = true; } }
    [Output] public ITaskItem[]? WasmOptConfigurationFlags { get => _outputs[4]; set { _outputs[4] = value; _bound[4] = true; } }

    public bool Execute()
    {
        string token = Guid.NewGuid().ToString("N");
        JsonFactoryPermit? permit = null;
        bool succeeded = false;
        try
        {
            lock (_gate)
            {
                CaptureSession.Require(!_entered && !_disposed && !_cancelled, "CAPTURE_JSON_LIFETIME_INVALID");
                _entered = true;
            }
            var receipt = Exchange("freeze", token, Values(), false);
            permit = JsonSerializer.Deserialize<JsonFactoryPermit>(receipt["permit"]
                ?? throw new CaptureException("CAPTURE_JSON_ACK_INVALID"), Program.Json)
                ?? throw new CaptureException("CAPTURE_JSON_ACK_INVALID");
            CaptureSession.Require(Path.GetFullPath(JsonFilePath) == permit.InputPath, "CAPTURE_JSON_INPUT_MISMATCH");
            CheckInputs(permit);
            lock (_gate)
            {
                CaptureSession.Require(!_cancelled, "CAPTURE_JSON_CANCELLED");
                string assemblyPath = permit.Tools.Single(tool => tool.Identity.Path == ReadWasmPropsProfile.Assembly
                    && tool.Identity.Sha256 == ReadWasmPropsProfile.AssemblyHash).PhysicalPath;
                _context = new ReaderContext(Path.GetDirectoryName(assemblyPath)!);
                var assembly = _context.LoadFromAssemblyPath(assemblyPath);
                var factoryType = assembly.GetType(ReadWasmPropsProfile.Factory, throwOnError: true)!;
                _factory = (ITaskFactory)Activator.CreateInstance(factoryType)!;
                var group = ReadWasmPropsProfile.Outputs.ToDictionary(name => name,
                    name => new TaskPropertyInfo(name, typeof(ITaskItem[]), true, false), StringComparer.Ordinal);
                CaptureSession.Require(_factory.Initialize(ReadWasmPropsProfile.Name, group, "", _initializationHost),
                    "CAPTURE_JSON_INITIALIZATION_FAILED");
                _parameters = ValidateShape(_factory);
                _task = _factory.CreateTask(_creationHost);
                CaptureSession.Require(_task.GetType() == _factory.TaskType && _task is IGeneratedTask,
                    "CAPTURE_JSON_SHAPE_UNSUPPORTED");
                _task.BuildEngine = BuildEngine;
                _task.HostObject = HostObject;
                var generated = (IGeneratedTask)_task;
                generated.SetPropertyValue(_parameters["JsonFilePath"], JsonFilePath);
                for (int index = 0; index < _outputs.Length; index++)
                    if (_bound[index])
                        generated.SetPropertyValue(_parameters[ReadWasmPropsProfile.Outputs[index]], _outputs[index]!);
            }
            bool executed = _task.Execute();
            lock (_gate)
            {
                for (int index = 0; index < _outputs.Length; index++)
                {
                    object? output = ((IGeneratedTask)_task).GetPropertyValue(_parameters![ReadWasmPropsProfile.Outputs[index]]);
                    CaptureSession.Require(output is null or ITaskItem[], "CAPTURE_JSON_SHAPE_UNSUPPORTED");
                    _outputs[index] = (ITaskItem[]?)output;
                }
                succeeded = executed && !_cancelled;
            }
        }
        catch (Exception error) when (error is CaptureException or IOException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or TargetInvocationException or TypeLoadException
            or BadImageFormatException or JsonException)
        {
            Error(error is CaptureException capture ? capture.Code : "CAPTURE_JSON_EXECUTION_FAILED");
            succeeded = false;
        }
        finally
        {
            try
            {
                Dispose();
                if (permit is not null)
                {
                    CheckInputs(permit);
                    _ = Exchange("outcome", token, Values(), succeeded);
                }
            }
            catch (Exception error) when (error is CaptureException or IOException or UnauthorizedAccessException
                or ArgumentException or InvalidOperationException or TargetInvocationException)
            {
                Error(error is CaptureException capture ? capture.Code : "CAPTURE_JSON_CLEANUP_FAILED");
                succeeded = false;
            }
        }
        return succeeded;
    }

    private Dictionary<string, string?> Exchange(string phase, string token, JsonFactoryValue[] values, bool succeeded)
    {
        string thread = Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
        var fields = new Dictionary<string, string?>
        {
            ["phase"] = phase,
            ["token"] = token,
            ["thread"] = thread,
            ["assembly"] = GetType().Assembly.Location,
            ["type"] = GetType().FullName!,
            ["values"] = JsonSerializer.Serialize(values, Program.Json),
            ["succeeded"] = succeeded.ToString()
        };
        var request = new ExtendedCustomBuildEventArgs(ReadWasmPropsProfile.Event) { ExtendedMetadata = fields };
        BuildEngine.LogCustomEvent(request);
        CaptureSession.Require(request.BuildEventContext is not null
            && fields.GetValueOrDefault("ack-token") == token
            && fields.GetValueOrDefault("ack-context") == Context(request.BuildEventContext)
            && fields.GetValueOrDefault("ack-thread") == thread && fields.GetValueOrDefault("ack-count") == "1",
            "CAPTURE_JSON_ACK_INVALID");
        CaptureSession.Require(phase == "outcome" || fields.ContainsKey("permit"), "CAPTURE_JSON_ACK_INVALID");
        return fields;
    }

    internal static string Context(BuildEventContext? context) => context is null ? "none" :
        FormattableString.Invariant($"{context.SubmissionId}/{context.NodeId}/{context.EvaluationId}/{context.ProjectInstanceId}/{context.ProjectContextId}/{context.TargetId}/{context.TaskId}/{context.BuildRequestId}");

    private JsonFactoryValue[] Values() => ReadWasmPropsProfile.Outputs.Select((name, index) =>
        new JsonFactoryValue(name, "Microsoft.Build.Framework.ITaskItem[]", _bound[index],
            JsonSerializer.SerializeToElement(_outputs[index]?.Select(item => new
            {
                item = item.ItemSpec,
                metadata = item.CloneCustomMetadata().Keys.Cast<string>().Order(StringComparer.Ordinal)
                    .Select(name => new { name, value = item.GetMetadata(name) }).ToArray()
            }).ToArray(), Program.Json)))
        .Append(new("JsonFilePath", "System.String", true, JsonSerializer.SerializeToElement(JsonFilePath)))
        .OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();

    private static Dictionary<string, TaskPropertyInfo> ValidateShape(ITaskFactory factory)
    {
        var parameters = factory.GetTaskParameters().OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();
        var expected = ReadWasmPropsProfile.Contract("").Parameters;
        CaptureSession.Require(factory.TaskType.FullName == ReadWasmPropsProfile.RuntimeType
            && typeof(IGeneratedTask).IsAssignableFrom(factory.TaskType)
            && parameters.Length == expected.Length, "CAPTURE_JSON_SHAPE_UNSUPPORTED");
        for (int index = 0; index < expected.Length; index++)
        {
            var parameter = parameters[index];
            var contract = expected[index];
            CaptureSession.Require(parameter.Name == contract.Name && parameter.PropertyType.FullName == contract.Type
                && parameter.Output == contract.Output && parameter.Required == !contract.Output,
                "CAPTURE_JSON_SHAPE_UNSUPPORTED");
        }
        return parameters.ToDictionary(parameter => parameter.Name, StringComparer.Ordinal);
    }

    private static void CheckInputs(JsonFactoryPermit permit)
    {
        foreach (var input in permit.Tools.Append(new(permit.InputPath, permit.Input)))
        {
            CaptureSession.Unlinked(input.PhysicalPath);
            CaptureSession.Require(new FileInfo(input.PhysicalPath).Length == input.Identity.Length
                && CaptureSession.FileHash(input.PhysicalPath) == input.Identity.Sha256, "CAPTURE_JSON_INPUT_MUTATED");
        }
    }

    private void Error(string code) =>
        BuildEngine.LogErrorEvent(new BuildErrorEventArgs("", code, "", 0, 0, 0, 0, code, "", ReadWasmPropsProfile.Name));

    public void Cancel()
    {
        lock (_gate)
        {
            _cancelled = true;
            if (_task is ICancelableTask cancellable) cancellable.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_task is not null) _factory!.CleanupTask(_task);
            }
            finally
            {
                _task = null;
                _factory = null;
                _parameters = null;
                _context?.Unload();
                _context = null;
            }
        }
        GC.SuppressFinalize(this);
    }

    private sealed class ReaderContext(string directory) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            if (name.Name == typeof(ITask).Assembly.GetName().Name) return typeof(ITask).Assembly;
            if (name.Name == typeof(Microsoft.Build.Utilities.Task).Assembly.GetName().Name)
                return typeof(Microsoft.Build.Utilities.Task).Assembly;
            string path = Path.Combine(directory, name.Name + ".dll");
            return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
        }
    }
}
