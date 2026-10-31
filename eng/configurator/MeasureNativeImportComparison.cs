namespace Configurator.Engineering;

using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Configurator.Presentation;
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
using Avalonia.Controls;
using Avalonia.Threading;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;
#else
using global::Terminal.Gui.App;
using global::Terminal.Gui.ViewBase;
#endif

/// <summary>Explicit diagnostic-candidate driver inside the real native application process.</summary>
public static class MeasureNativeImportComparison
{
    private static object? _root;
    private static string? _input;
    private static readonly List<object> Samples = [];
    private static readonly List<OwnerCycle> Owners = [];
    private static readonly MemoryResult[] MemoryResults = new MemoryResult[20];
    private static readonly long[] WarmHeaps = new long[6];
    private static readonly int[] WarmCheckpoints = new int[6];
    private static readonly object Unobserved = new();
    private static readonly OwnerCycle WarmOwner = new();
    private static int _memoryCycle = -1;
    private static string _stage = "starting";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static long _liveAtCheckpoint, _allocatedAtCheckpoint, _peakUpperBound;
    private static int _checkpoints;
    private static bool _memoryEnabled;

    public static void Attach(object root)
    {
        if (Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_PROBE_INPUT") is not { Length: > 0 } input)
            return;
        _root = root;
        _input = input;
        _ = RunAsync();
    }

    private static async Task RunAsync()
    {
        string output = Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_PROBE_OUTPUT") ??
            throw new InvalidOperationException("native-probe-output-required");
        try
        {
            _stage = "ready";
            await ReadyAsync();
            await UiAsync(() =>
            {
                if (Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_PROBE_ARTIFACT") is not { Length: > 0 } artifact)
                    throw new InvalidOperationException("native-probe-artifact-required");
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
                Shell.Publisher.ArtifactId = artifact;
                Shell.Workspace.FollowLocaleDirection = false;
                Shell.Workspace.IsRightToLeft =
                    Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_PROBE_DIRECTION") == "rtl";
#else
                object operation = Field(_root!, "_operation");
                operation.GetType().GetProperty("ArtifactId", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(operation, artifact);
#endif
            });
            await PaintAsync();
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
            await UiAsync(() => ((MainWindow)_root!).FindControl<Control>("Import")!.BringIntoView());
            await PaintAsync();
#endif
            bool memory = Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_PROBE_MODE") == "memory";
            if (memory)
            {
                InstallMemoryObservers();
                // Warmup and observation share the complete acquisition, UI, owner and collection recipe.
                for (int sample = 0; sample < 26; sample++)
                {
                    bool warming = sample < WarmHeaps.Length;
                    _stage = "memory";
                    long baseline = GC.GetTotalMemory(true);
                    _liveAtCheckpoint = baseline;
                    _allocatedAtCheckpoint = GC.GetTotalAllocatedBytes(true);
                    _peakUpperBound = baseline;
                    _checkpoints = 0;
                    _memoryCycle = warming ? -1 : sample - WarmHeaps.Length;
                    await CompareAsync(sample, "memory");
                    MemoryCheckpoint();
                    _memoryCycle = -1;
                    long retiredHeap = GC.GetTotalMemory(true);
                    if (warming)
                    {
                        WarmHeaps[sample] = retiredHeap;
                        WarmCheckpoints[sample] = _checkpoints;
                    }
                    else MemoryResults[sample - WarmHeaps.Length] =
                        new(_peakUpperBound, retiredHeap, _checkpoints, baseline);
                }
            }
            else
            {
                for (int sample = 0; sample < 46; sample++)
                {
                    _stage = "comparison";
                    await CompareAsync(sample, sample == 0 ? "first" : sample <= 5 ? "warmup" : "warm");
                }
                for (int sample = 0; sample < 46; sample++)
                {
                    _stage = "cancellation";
                    await CancelAsync(sample);
                }
            }
            object result = new
            {
                process = Environment.ProcessId, framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                culture = System.Globalization.CultureInfo.CurrentUICulture.Name,
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
                direction = Shell.Workspace.FlowDirection.ToString(),
#else
                direction = "left-to-right terminal grid; resource culture recorded separately",
#endif
                mode = memory ? "memory" : "timing",
                measurement = "Actual application entrypoint, real client commands, physical input and cooperative scheduler. Diagnostic compiler inputs; not byte-identical normal release.",
                samples = Samples,
                warmHeaps = memory ? WarmHeaps : null,
                warmCheckpoints = memory ? WarmCheckpoints : null,
                warmOwners = memory ? WarmOwner.Snapshot() : null,
                warmPositive = memory && WarmOwner.Positive,
                diagnosticHistoryMode = DiagnosticHistoryMode(),
                memoryCycles = memory ? MemoryResults.Select((result, index) => new
                {
                    cycle = index + 1, conservativeWholeHeapPeakBytes = result.Peak,
                    baselineHeapBytes = result.Baseline,
                    retiredHeapBytes = result.Retired, checkpoints = result.Checkpoints,
                    owners = Owners[index].Snapshot(), positive = Owners[index].Positive
                }).ToArray() : null,
                finalOwners = memory ? Owners.Select(owner => owner.Snapshot()).ToArray() : null
            };
            await WriteReceiptAsync(output, result);
            // This process exists only for the explicitly opted-in measurement.
            // Teardown is not evidence of the ordinary application's close flow.
            Environment.Exit(0);
        }
        catch (Exception exception)
        {
            // Diagnostic evidence reports only the type, never private values or paths.
            await WriteReceiptAsync(output, new
            {
                error = exception.GetType().Name, stage = _stage, samples = Samples, warmHeaps = WarmHeaps,
                completedMemoryCycles = MemoryResults.Count(result => result.Checkpoints > 0)
            });
            Environment.Exit(1);
        }
    }

    private static async Task WriteReceiptAsync(string output, object receipt)
    {
        string pending = output + ".pending";
        await File.WriteAllTextAsync(pending, JsonSerializer.Serialize(receipt, JsonOptions));
        File.Move(pending, output);
    }

    private static void MemoryCheckpoint()
    {
        if (!_memoryEnabled) return;
        // Full live heap plus every allocation until the next checkpoint is an
        // inclusive peak upper bound. This intrusive path never runs in timing mode.
        long allocated = GC.GetTotalAllocatedBytes(true);
        _peakUpperBound = Math.Max(_peakUpperBound, _liveAtCheckpoint + allocated - _allocatedAtCheckpoint);
        _liveAtCheckpoint = GC.GetTotalMemory(true);
        _allocatedAtCheckpoint = GC.GetTotalAllocatedBytes(true);
        _checkpoints++;
    }

    private static void InstallMemoryObservers()
    {
        _memoryEnabled = true;
        // Allocate the meter's own retained state before warming the no-proposal baseline.
        for (int index = 0; index < 20; index++) Owners.Add(new());
        AppContext.SetData("Configurator.NativeProbe.Checkpoint", new Action(MemoryCheckpoint));
        AppContext.SetData("Configurator.NativeProbe.Input", new Action<object, byte[]>(static (owner, bytes) =>
        {
            OwnerCycle owned = _memoryCycle < 0 ? WarmOwner : Owners[_memoryCycle];
            owned.InputOwner.SetTarget(owner);
            owned.Input.SetTarget(bytes);
        }));
        AppContext.SetData("Configurator.NativeProbe.DisposeInput", new Action<byte[]>(static bytes =>
        {
            OwnerCycle owned = _memoryCycle < 0 ? WarmOwner : Owners[_memoryCycle];
            owned.InputDisposed++;
            owned.InputCleared = bytes.All(value => value == 0);
        }));
        AppContext.SetData("Configurator.NativeProbe.Proposal", new Action<object, object?, object?, object?>(static (owner, evidence, answers, assignments) =>
        {
            OwnerCycle owned = _memoryCycle < 0 ? WarmOwner : Owners[_memoryCycle];
            owned.Proposals++;
            owned.Proposal.SetTarget(owner);
            owned.Evidence.SetTarget(evidence ?? throw new InvalidOperationException("native-probe-evidence-absent"));
            owned.Answers.SetTarget(answers ?? throw new InvalidOperationException("native-probe-answers-absent"));
            owned.Assignments.SetTarget(assignments ?? throw new InvalidOperationException("native-probe-assignments-absent"));
        }));
        AppContext.SetData("Configurator.NativeProbe.DisposeProposal", new Action<object>(static _ =>
        {
            (_memoryCycle < 0 ? WarmOwner : Owners[_memoryCycle]).ProposalDisposed++;
        }));
    }

    private static async Task CompareAsync(int sample, string phase)
    {
        long start = Stopwatch.GetTimestamp();
        Task physical = Task.CompletedTask;
        double acknowledgment = 0;
        await UiAsync(() =>
        {
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
            DesktopShellViewModel shell = Shell;
            shell.Publisher.ExistingFilePath = _input!;
            shell.Publisher.ImportPurposeIndex = 0;
            physical = shell.Publisher.ImportFileAsync();
#else
            object window = _root!;
            object path = Field(window, "_importPath");
            path.GetType().GetProperty("Text")!.SetValue(path, _input);
            Invoke(window, "StartImport", true);
            physical = (Task)Field(window, "_importOperation").GetType().GetProperty("Completion", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Field(window, "_importOperation"))!;
#endif
            acknowledgment = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        });
        await physical.WaitAsync(TimeSpan.FromSeconds(30));
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
        await UiAsync(() => ((MainWindow)_root!).FindControl<Control>("ImportComparison")!.BringIntoView());
#endif
        await PaintAsync();
        double reportMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        await UiAsync(() =>
        {
            if (Form.ImportComparison is null) throw new InvalidOperationException("native-probe-report-absent");
            if (_memoryEnabled)
            {
                OwnerCycle owned = _memoryCycle < 0 ? WarmOwner : Owners[_memoryCycle];
                owned.Positive = owned.Proposal.TryGetTarget(out _) && owned.Evidence.TryGetTarget(out _) &&
                    owned.Answers.TryGetTarget(out _) && owned.Assignments.TryGetTarget(out _);
            }
        });
        long navigation = Stopwatch.GetTimestamp();
        bool navigated = false;
        await UiAsync(() =>
        {
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
            if (Shell.Publisher.CanNextImportPage)
            {
                Shell.Publisher.NextImportPageCommand.Execute(null);
                navigated = true;
            }
#else
            if (Form.ImportComparison!.RetainedFindingCount > 0)
            {
                Invoke(_root!, "NextComparisonFinding");
                navigated = true;
            }
#endif
        });
        await PaintAsync();
        double navigationMs = Stopwatch.GetElapsedTime(navigation).TotalMilliseconds;
        long schema = Stopwatch.GetTimestamp();
        bool explained = false;
        await UiAsync(() =>
        {
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
            ImportFindingViewModel? finding = Shell.Publisher.ImportFindings.FirstOrDefault(item => item.CanExplain);
            if (finding is not null) { finding.ExplainCommand.Execute(null); explained = true; }
#else
            // The native Next finding control publishes schema explanation together with the finding.
            explained = navigated;
#endif
        });
        await PaintAsync();
        double schemaMs = Stopwatch.GetElapsedTime(schema).TotalMilliseconds;
        await UiAsync(() =>
        {
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
            Shell.Publisher.ClearImportComparisonCommand.Execute(null);
#else
            Invoke(_root!, "ClearPrivateState");
#endif
        });
        await PaintAsync();
        await UiAsync(() =>
        {
            if (Form.ImportComparison is not null) throw new InvalidOperationException("native-probe-retirement-failed");
            ClearDiagnosticHistory();
        });
        if (phase != "memory")
            Samples.Add(new { phase, sample, acknowledgmentMs = acknowledgment, reportMs, navigated, navigationMs, explained, schemaMs });
    }

    private static string DiagnosticHistoryMode()
    {
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
        return "not-applicable";
#else
        string mode = Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_PROBE_CLEAR_HISTORY") ?? "none";
        return mode is "none" or "importPath" or "value" or "status" or "all"
            ? mode : throw new InvalidOperationException("native-probe-history-mode-invalid");
#endif
    }

    private static void ClearDiagnosticHistory()
    {
#if !CONFIGURATOR_DESKTOP_NATIVE_PROBE
        string mode = DiagnosticHistoryMode();
        if (mode is "importPath" or "all")
            ((global::Terminal.Gui.Views.TextField)Field(_root!, "_importPath")).ClearHistoryChanges();
        if (mode is "value" or "all")
            ((global::Terminal.Gui.Views.TextField)Field(_root!, "_value")).ClearHistoryChanges();
        if (mode is "status" or "all")
            ((global::Terminal.Gui.Views.TextField)Field(_root!, "_status")).ClearHistoryChanges();
#endif
    }

    private static async Task CancelAsync(int sample)
    {
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        AppContext.SetData("Configurator.NativeProbe.ReadComplete", new Action(() => read.TrySetResult()));
        Task physical = Task.CompletedTask;
        await UiAsync(() =>
        {
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
            Shell.Publisher.ExistingFilePath = _input!;
            physical = Shell.Publisher.ImportFileAsync();
#else
            object path = Field(_root!, "_importPath");
            path.GetType().GetProperty("Text")!.SetValue(path, _input);
            Invoke(_root!, "StartImport", true);
            object operation = Field(_root!, "_importOperation");
            physical = (Task)operation.GetType().GetProperty("Completion", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(operation)!;
#endif
        });
        await read.Task.WaitAsync(TimeSpan.FromSeconds(30));
        long started = Stopwatch.GetTimestamp();
        double logicalMs = 0;
        bool cancelledWhilePending = false;
        long[] before = [];
        long[] after = [];
        await UiAsync(() =>
        {
            cancelledWhilePending = !physical.IsCompleted;
            before = Form.ReadImportOwnershipProbeState();
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
            Shell.CancelCommand.Execute(null);
#else
            Invoke(_root!, "CancelImport");
#endif
            after = Form.ReadImportOwnershipProbeState();
            logicalMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        });
        try { await physical.WaitAsync(TimeSpan.FromSeconds(30)); }
        catch (OperationCanceledException) { }
        double physicalMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        await PaintAsync();
        double feedbackPaintMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        await UiAsync(() =>
        {
            if (Form.ImportComparison is not null) throw new InvalidOperationException("native-probe-late-publication");
        });
        AppContext.SetData("Configurator.NativeProbe.ReadComplete", null);
        Samples.Add(new
        {
            phase = "cancellation", sample, cancelledWhilePending, before, after,
            logicalMs, physicalMs, feedbackPaintMs
        });
    }

    private static Task ReadyAsync()
    {
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return ObserveAsync();
        async Task ObserveAsync()
        {
            await UiAsync(() =>
            {
                if (Shell.Publisher.CanImport) { ready.TrySetResult(); return; }
                PropertyChangedEventHandler? changed = null;
                changed = (_, _) =>
                {
                    if (!Shell.Publisher.CanImport) return;
                    Shell.Publisher.PropertyChanged -= changed;
                    ready.TrySetResult();
                };
                Shell.Publisher.PropertyChanged += changed;
            });
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
#else
        return Task.CompletedTask;
#endif
    }

    private static Task UiAsync(Action action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Run()
        {
            try { action(); done.TrySetResult(); }
            catch (Exception exception) { done.TrySetException(exception); }
        }
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
        Dispatcher.UIThread.Post(Run);
#else
        ((IApplication)Field(_root!, "_application")).Invoke(Run);
#endif
        return done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private static async Task PaintAsync()
    {
#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
        // A second frame is a conservative endpoint beyond the updated scene.
        for (int index = 0; index < 2; index++)
        {
            var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await UiAsync(() => ((MainWindow)_root!).RequestAnimationFrame(_ => rendered.TrySetResult()));
            await rendered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
#else
        await UiAsync(() =>
        {
            ((View)_root!).SetNeedsDraw();
            ((IApplication)Field(_root!, "_application")).LayoutAndDraw(true);
        });
#endif
    }

#if CONFIGURATOR_DESKTOP_NATIVE_PROBE
    private static DesktopShellViewModel Shell => (DesktopShellViewModel)((MainWindow)_root!).DataContext!;
    private static DefinitionFormModel Form => Shell.Workspace.Form!;
#else
    private static DefinitionFormModel Form =>
        (DefinitionFormModel)Field(_root!, "_operation").GetType().GetProperty("Form", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Field(_root!, "_operation"))!;

    private static object Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void Invoke(object target, string name, params object[] arguments) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);
#endif

    private sealed class OwnerCycle
    {
        internal readonly WeakReference<object> InputOwner = new(Unobserved);
        internal readonly WeakReference<object> Input = new(Unobserved);
        internal readonly WeakReference<object> Proposal = new(Unobserved);
        internal readonly WeakReference<object> Evidence = new(Unobserved);
        internal readonly WeakReference<object> Answers = new(Unobserved);
        internal readonly WeakReference<object> Assignments = new(Unobserved);
        internal int Proposals, InputDisposed, ProposalDisposed;
        internal bool InputCleared, Positive;
        internal int[] Snapshot() =>
        [
            Proposals, InputDisposed, InputCleared ? 1 : 0, ProposalDisposed,
            Alive(InputOwner), Alive(Input), Alive(Proposal), Alive(Evidence), Alive(Answers), Alive(Assignments)
        ];
        private static int Alive(WeakReference<object>? reference) =>
            reference is not null && reference.TryGetTarget(out _) ? 1 : 0;
    }

    private readonly record struct MemoryResult(long Peak, long Retired, int Checkpoints, long Baseline);
}
