namespace Configurator.Engineering;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Measurement-candidate-only weak observations of actual import owners.
/// One fresh runtime observes exactly twenty sequential compare/clear cycles.
/// No private object is stored strongly or returned to the caller.
/// </summary>
public static class ImportComparisonOwnershipProbe
{
    private static readonly object Unobserved = new();
    private static readonly Cycle[] Cycles = CreateCycles();
    private static readonly Action<Task, object?> RecordTaskSettlement = static (_, state) =>
    {
        if (state is not ValueTuple<Cycle, bool> observation)
            throw new InvalidOperationException("ownership-probe-task-state");
        var (observed, isHandler) = observation;
        if (isHandler)
        {
            observed.HandlerSettledAt = Stopwatch.GetTimestamp();
            observed.HandlerSettled = 1;
        }
        else
        {
            observed.ComparisonSettledAt = Stopwatch.GetTimestamp();
            observed.ComparisonSettled = 1;
        }
    };
    private static int _started;
    private static int _errors;

    public static int BeginCycle(bool cancellation = false)
    {
        if (_started == Cycles.Length || _started > 0 &&
            (Cycles[_started - 1] is not { InputCompleted: 1, HandlerSettled: 1 } previous ||
             previous.ProposalDisposed != previous.ProposalObserved))
            throw new InvalidOperationException("ownership-probe-cycle-order");
        Cycles[_started++].Cancellation = cancellation ? 1 : 0;
        return _started;
    }

    public static void ObserveInput(byte[] input, object editor, object form, long generation,
        Func<object, long[]> readEditor, Func<object, long[]> readForm)
    {
        if (_started == 0) return;
        Cycle cycle = Cycles[_started - 1]!;
        if (cycle.InputObserved != 0) { ++_errors; return; }
        cycle.InputObserved = 1;
        cycle.Input.SetTarget(input);
        cycle.Editor.SetTarget(editor);
        cycle.Form.SetTarget(form);
        cycle.ReadEditor = readEditor;
        cycle.ReadForm = readForm;
        cycle.OperationGeneration = generation;
        cycle.OperationFormGeneration = readForm(form)[0];
        cycle.InputStartedAt = Stopwatch.GetTimestamp();
    }

    /// <summary>Observes the actual returned task, not a render-frame surrogate.</summary>
    public static void ObserveTask(Task task, bool handler)
    {
        if (_started == 0) return;
        Cycle cycle = Cycles[_started - 1]!;
        if (handler ? cycle.HandlerObserved != 0 : cycle.ComparisonObserved != 0)
        {
            ++_errors;
            return;
        }
        if (handler) { cycle.HandlerObserved = 1; cycle.HandlerTask.SetTarget(task); }
        else { cycle.ComparisonObserved = 1; cycle.ComparisonTask.SetTarget(task); }
        // The continuation retains only numeric/weak diagnostic state, never the task
        // or an editor, form, buffer or proposal. Its timestamp is an upper bound on
        // settlement, not a browser event-loop or rendering timestamp.
        _ = task.ContinueWith(RecordTaskSettlement, (cycle, handler), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>Called after synchronous cancellation, buffer release and proposal retirement.</summary>
    public static void ObserveRetirement(object editor)
    {
        if (_started == 0) return;
        Cycle cycle = Cycles[_started - 1]!;
        if (cycle.InputObserved == 0 || cycle.RetiredAt != 0 ||
            !cycle.Editor.TryGetTarget(out object? observed) || !ReferenceEquals(editor, observed)) return;
        cycle.ReadEditor!(editor).CopyTo(cycle.RetirementEditor, 0);
        if (cycle.Form.TryGetTarget(out object? form))
            cycle.ReadForm!(form).CopyTo(cycle.RetirementForm, 0);
        else ++_errors;
        cycle.RetiredAt = Stopwatch.GetTimestamp();
    }

    public static Task WaitForTask(int cycleNumber, bool handler)
    {
        // Cycle zero warms only the JS Task binding before any private observation.
        // It cannot stand in for an actual cycle or reset an observed runtime.
        if (cycleNumber == 0 && _started == 0) return Task.CompletedTask;
        Cycle cycle = GetCycle(cycleNumber);
        WeakReference<Task>? reference = handler ? cycle.HandlerTask : cycle.ComparisonTask;
        if ((handler ? cycle.HandlerObserved : cycle.ComparisonObserved) == 0 ||
            reference is null || !reference.TryGetTarget(out Task? task))
            throw new InvalidOperationException("ownership-probe-task-unavailable");
        return task;
    }

    public static void CompleteInput(byte[] input)
    {
        if (_started == 0) return;
        Cycle cycle = Cycles[_started - 1]!;
        if (cycle.InputCompleted != 0 || cycle.InputObserved == 0 ||
            !cycle.Input.TryGetTarget(out object? observed) || !ReferenceEquals(observed, input))
        {
            ++_errors;
            return;
        }
        cycle.InputCompleted = 1;
        cycle.InputCompletedAt = Stopwatch.GetTimestamp();
        cycle.InputCleared = 1;
        foreach (byte value in input)
            if (value != 0) { cycle.InputCleared = 0; break; }
    }

    public static void ObserveProposal(object owner, object? evidence, object? answers, object? assignments)
    {
        if (AppContext.GetData("Configurator.NativeProbe.Proposal") is Action<object, object?, object?, object?> observe)
            observe(owner, evidence, answers, assignments);
        if (_started == 0) return;
        Cycle cycle = Cycles[_started - 1]!;
        if (cycle.ProposalObserved != 0 || evidence is null || answers is null || assignments is null)
        {
            ++_errors;
            return;
        }
        cycle.ProposalObserved = 1;
        cycle.Proposal.SetTarget(owner);
        cycle.Evidence.SetTarget(evidence);
        cycle.Answers.SetTarget(answers);
        cycle.Assignments.SetTarget(assignments);
    }

    public static void DisposeProposal(object owner)
    {
        if (AppContext.GetData("Configurator.NativeProbe.DisposeProposal") is Action<object> observe)
            observe(owner);
        if (_started == 0) return;
        Cycle cycle = Cycles[_started - 1]!;
        if (cycle.ProposalDisposed != 0 || cycle.ProposalObserved == 0 ||
            !cycle.Proposal.TryGetTarget(out object? observed) || !ReferenceEquals(observed, owner))
        {
            ++_errors;
            return;
        }
        cycle.ProposalDisposed = 1;
    }

    /// <summary>
    /// Numeric JSON array: started, errors, input observed, input completed,
    /// input cleared, proposal observed, proposal disposed, live input,
    /// live proposal, live evidence, live answers, live assignments.
    /// Inspect every earlier cycle again after the twentieth cleanup.
    /// Missing instrumentation cannot masquerade as a zero-owner pass.
    /// </summary>
    public static string Snapshot(int cycleNumber)
    {
        Cycle cycle = GetCycle(cycleNumber);
        return "[" + string.Join(",", new[]
        {
            _started, _errors, cycle.InputObserved,
            cycle.InputCompleted, cycle.InputCleared, cycle.ProposalObserved,
            cycle.ProposalDisposed, cycle.InputObserved * Alive(cycle.Input), cycle.ProposalObserved * Alive(cycle.Proposal),
            cycle.ProposalObserved * Alive(cycle.Evidence), cycle.ProposalObserved * Alive(cycle.Answers),
            cycle.ProposalObserved * Alive(cycle.Assignments)
        }) + "]";
    }

    /// <summary>
    /// Numeric JSON: cancellation mode, operation editor/form generations,
    /// current editor [generation, input owner, pending flag, cancellation owner, disposed],
    /// current operation form [generation, proposal owner, disposed],
    /// those same two states at retirement, task observed/settled pairs,
    /// clock frequency, input start, logical retirement, physical finally completion,
    /// comparison settlement callback and handler settlement callback timestamps.
    /// Missing weak owners or an uncaptured retirement use -1, not a false zero.
    /// </summary>
    public static string BoundarySnapshot(int cycleNumber)
    {
        Cycle cycle = GetCycle(cycleNumber);
        long[] editor = cycle.InputObserved != 0 && cycle.Editor.TryGetTarget(out object? owner)
            ? cycle.ReadEditor!(owner) : new long[] { -1, -1, -1, -1, -1 };
        long[] form = cycle.InputObserved != 0 && cycle.Form.TryGetTarget(out object? model)
            ? cycle.ReadForm!(model) : new long[] { -1, -1, -1 };
        return "[" + string.Join(",", new long[]
        {
            cycle.Cancellation, cycle.OperationGeneration, cycle.OperationFormGeneration,
            editor[0], editor[1], editor[2], editor[3], editor[4],
            form[0], form[1], form[2],
            cycle.RetiredAt == 0 ? -1 : cycle.RetirementEditor[0], cycle.RetiredAt == 0 ? -1 : cycle.RetirementEditor[1],
            cycle.RetiredAt == 0 ? -1 : cycle.RetirementEditor[2], cycle.RetiredAt == 0 ? -1 : cycle.RetirementEditor[3],
            cycle.RetiredAt == 0 ? -1 : cycle.RetirementEditor[4],
            cycle.RetiredAt == 0 ? -1 : cycle.RetirementForm[0], cycle.RetiredAt == 0 ? -1 : cycle.RetirementForm[1],
            cycle.RetiredAt == 0 ? -1 : cycle.RetirementForm[2],
            cycle.ComparisonObserved, cycle.ComparisonSettled,
            cycle.HandlerObserved, cycle.HandlerSettled,
            Stopwatch.Frequency, cycle.InputStartedAt, cycle.RetiredAt,
            cycle.InputCompletedAt, cycle.ComparisonSettledAt, cycle.HandlerSettledAt
        }) + "]";
    }

    private static Cycle GetCycle(int cycleNumber)
    {
        if (cycleNumber < 1 || cycleNumber > _started)
            throw new ArgumentOutOfRangeException(nameof(cycleNumber));
        return Cycles[cycleNumber - 1]!;
    }

    private static int Alive(WeakReference<object>? reference) =>
        reference is not null && reference.TryGetTarget(out _) ? 1 : 0;

    private static Cycle[] CreateCycles()
    {
        // Retention uses twenty cycles; timing retains first use, five warmups and forty samples.
        var cycles = new Cycle[46];
        for (int index = 0; index < cycles.Length; index++) cycles[index] = new();
        return cycles;
    }

    private sealed class Cycle
    {
        public readonly WeakReference<object> Input = new(Unobserved);
        public readonly WeakReference<object> Proposal = new(Unobserved);
        public readonly WeakReference<object> Evidence = new(Unobserved);
        public readonly WeakReference<object> Answers = new(Unobserved);
        public readonly WeakReference<object> Assignments = new(Unobserved);
        public int InputObserved;
        public int ProposalObserved;
        public int InputCompleted;
        public int InputCleared;
        public int ProposalDisposed;
        public int Cancellation;
        public readonly WeakReference<object> Editor = new(Unobserved);
        public readonly WeakReference<object> Form = new(Unobserved);
        public Func<object, long[]>? ReadEditor;
        public Func<object, long[]>? ReadForm;
        public readonly WeakReference<Task> ComparisonTask = new(Task.CompletedTask);
        public readonly WeakReference<Task> HandlerTask = new(Task.CompletedTask);
        public int ComparisonObserved;
        public int HandlerObserved;
        public int ComparisonSettled;
        public int HandlerSettled;
        public long OperationGeneration;
        public long OperationFormGeneration;
        public readonly long[] RetirementEditor = new long[5];
        public readonly long[] RetirementForm = new long[3];
        public long InputStartedAt;
        public long RetiredAt;
        public long InputCompletedAt;
        public long ComparisonSettledAt;
        public long HandlerSettledAt;
    }
}
