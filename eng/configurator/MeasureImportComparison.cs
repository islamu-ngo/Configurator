#:property RestorePackagesWithLockFile=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property AssemblyName=Configurator.ImportComparisonMeasurement
#:property RootNamespace=Configurator.Engineering
#:project ../../src/Configurator.Core/Configurator.Core.csproj

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Core.Definitions;

// Run from the checkout root. Output contains only cohort names, public identities and counters.
if (args.Length != 0)
{
    Console.Error.WriteLine("Usage: dotnet run eng/configurator/MeasureImportComparison.cs --configuration Release");
    return 64;
}

try
{
    return Qualification.Run();
}
catch (Exception exception)
{
    // Exception messages and stack traces can contain imported values or local paths.
    Console.Error.WriteLine($"QUALIFICATION_ERROR type={exception.GetType().Name}");
    return 1;
}

internal static class Qualification
{
    private const long NativeCeiling = 128L * 1024 * 1024;
    private const string FixtureDirectory = ".omo/evidence/20261030-artifact-import-comparison/browser-fixtures";
    private static readonly SetupAnswerSnapshot Empty = new(0, new Dictionary<string, JsonElement>());

    internal static int Run()
    {
        Console.WriteLine(FormattableString.Invariant(
            $"ENVIRONMENT framework={RuntimeInformation.FrameworkDescription.Replace(' ', '_')} os={RuntimeInformation.OSDescription.Replace(' ', '_')} architecture={RuntimeInformation.ProcessArchitecture} processors={System.Environment.ProcessorCount} availableMemoryBytes={GC.GetGCMemoryInfo().TotalAvailableMemoryBytes} serverGc={System.Runtime.GCSettings.IsServerGC} stopwatchFrequency={Stopwatch.Frequency}"));
        Identity("tool", "eng/configurator/MeasureImportComparison.cs");
        Console.WriteLine($"CORE_IDENTITY mvid={typeof(DefinitionImport).Assembly.ManifestModule.ModuleVersionId:D}");
        foreach (string name in new[] { "DefinitionImport", "DefinitionImportAssessment", "DefinitionImportProjection",
            "DefinitionValidator", "DefinitionJson", "SetupDefinition" })
            Identity("core-source-" + name, "src/Configurator.Core/Definitions/" + name + ".cs");
        Console.WriteLine("METHOD timing=first_then_five_warmups_then_all_forty samples=unforcedGc checkpoint=completed_callback_without_scheduler memory=separate_forcedGc_checkpoint_probe allocation=process_total_precise live=GcGetTotalMemoryFalse upper=previous_fullGc_heap_plus_interval_allocations ceilingBytes=134217728");
        Console.WriteLine("LIMITATIONS native_Core_only_not_Browser_not_AOT_not_client_UI; managed_heap_only_not_native_RSS; sampled_heap_is_not_exact_owned_live; conservative_upper_includes_runtime_and_admitted_definition; subtraction_estimate_is_not_the_ceiling_certificate; traversal_counters_are_inspection_only; weak_probes_cover_assessment_input_buffer_and_autofill_result_not_every_internal_object; no_untouched_baseline_invented; forcedGc_probe_latency_is_not_product_latency; no_physical_managed_string_erasure_claim");

        bool passed = true;
        passed &= Measure("event", "examples/islamu-event/definition.json",
            "examples/islamu-event/expected/configuration-manifest-1-tenants.json", "configuration-manifest", 2_000);
        passed &= Measure("maximum", FixtureDirectory + "/maximum-definition.json",
            FixtureDirectory + "/maximum-input.json", "config", 5_000);
        passed &= Measure("findings", FixtureDirectory + "/findings-definition.json",
            FixtureDirectory + "/findings-input.json", "config", 5_000);
        Console.WriteLine($"QUALIFICATION_COMPLETE passed={passed}");
        return passed ? 0 : 2;
    }

    private static bool Measure(string cohort, string definitionPath, string inputPath, string artifactId, double ceilingMs)
    {
        Identity(cohort + "-definition", definitionPath);
        Identity(cohort + "-input", inputPath);
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(File.ReadAllBytes(definitionPath));
        Console.WriteLine($"DEFINITION cohort={cohort} family={definition.FamilyId:D} revision={definition.Revision} digest={definition.Digest} artifact={artifactId}");
        var warm = new double[40];
        bool countersValid = true;
        Cycle first = CompleteCycle(definition, artifactId, inputPath, false);
        PrintCycle(cohort, "first", 1, first);
        countersValid &= Valid(cohort, first);
        for (int index = 0; index < 5; index++)
        {
            Cycle sample = CompleteCycle(definition, artifactId, inputPath, false);
            PrintCycle(cohort, "warmup", index + 1, sample);
            countersValid &= Valid(cohort, sample);
        }
        for (int index = 0; index < warm.Length; index++)
        {
            Cycle sample = CompleteCycle(definition, artifactId, inputPath, false);
            warm[index] = sample.ReportMilliseconds;
            PrintCycle(cohort, "warm", index + 1, sample);
            countersValid &= Valid(cohort, sample);
        }
        Array.Sort(warm);
        double p95 = warm[(int)Math.Ceiling(warm.Length * .95) - 1];
        Console.WriteLine(FormattableString.Invariant(
            $"TIMING cohort={cohort} samples={warm.Length} firstReportMs={first.ReportMilliseconds:F3} p95ReportMs={p95:F3} maximumReportMs={warm[^1]:F3} ceilingMs={ceilingMs:F0} passed={first.ReportMilliseconds <= ceilingMs && p95 <= ceilingMs}"));

        Cycle memory = CompleteCycle(definition, artifactId, inputPath, true);
        PrintCycle(cohort, "memory", 1, memory);
        bool memoryPassed = memory.UpperBytes <= NativeCeiling;
        Console.WriteLine($"MEMORY cohort={cohort} sampledHeapDeltaBytes={memory.PeakHeapDeltaBytes} forcedGcLiveDeltaBytes={memory.PeakLiveDeltaBytes} conservativeInclusiveUpperBytes={memory.UpperBytes} allocatedBytes={memory.AllocatedBytes} ceilingBytes={NativeCeiling} passed={memoryPassed}");

        // Establish the post-warmup no-proposal range after real completed disposal,
        // with the same assessment and weak-reference machinery as measured cycles.
        WeakReference[] owners = [];
        long low = long.MaxValue;
        long high = 0;
        for (int index = 0; index < 20; index++)
        {
            owners = CompleteAndRetireOwners(definition, artifactId, inputPath);
            long heap = CollectCompleted();
            int alive = CountAlive(owners);
            if (alive != 0)
                throw new InvalidOperationException("warmup-owner-retained");
            low = Math.Min(low, heap);
            high = Math.Max(high, heap);
            PrintDisposal(cohort, "warmup", index + 1, alive, heap, high);
        }
        Console.WriteLine($"NO_PROPOSAL cohort={cohort} samples=20 minimumHeapBytes={low} maximumHeapBytes={high}");
        bool retired = true;
        long lastHeap = 0;
        for (int index = 0; index < 20; index++)
        {
            // The previous owner is disposed and its operation completed before replacement.
            owners = CompleteAndRetireOwners(definition, artifactId, inputPath);
            lastHeap = CollectCompleted();
            int alive = CountAlive(owners);
            retired &= alive == 0;
            PrintDisposal(cohort, "measured", index + 1, alive, lastHeap, high);
        }
        // Falling below the observed range is also a successful return (not a retained-state leak).
        bool rangePassed = lastHeap <= high;
        Console.WriteLine($"RETENTION cohort={cohort} cycles=20 zeroRetainedOwnersOrBuffers={retired} returnedToNoProposalRange={rangePassed} finalHeapBytes={lastHeap} noProposalMaximumBytes={high}");
        GC.KeepAlive(definition);
        GC.KeepAlive(owners);
        return countersValid && Valid(cohort, memory) && memoryPassed && retired && rangePassed
            && first.ReportMilliseconds <= ceilingMs && p95 <= ceilingMs;
    }

    private static bool Valid(string cohort, Cycle cycle) =>
        cycle.WorkUnits <= 131_072 && cycle.InverseNodes <= 65_536 && cycle.ValidationNodes <= 65_536
        && cycle.Findings <= 4_096
        && (cycle.InventoryComplete && cycle.CoverageComplete || cycle.Compatibility != DefinitionImportCompatibility.Compatible)
        && (cohort != "findings" || cycle.InventoryComplete && cycle.Findings == 3_000)
        && (cohort != "event" || cycle.ExecutionCompleted && cycle.AutofillSucceeded);

    private static int CountAlive(WeakReference[] owners)
    {
        int count = 0;
        foreach (WeakReference owner in owners)
            if (owner.IsAlive) count++;
        return count;
    }

    private static void PrintDisposal(string cohort, string phase, int cycle, int alive, long heap, long high) =>
        Console.WriteLine($"DISPOSE cohort={cohort} phase={phase} cycle={cycle} retainedOwnersOrBuffers={alive} completedFullGcHeapBytes={heap} aboveNoProposalMaximumBytes={Math.Max(0, heap - high)}");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Cycle CompleteCycle(SetupDefinition definition, string artifactId, string inputPath, bool forceGc) =>
        CycleAsync(definition, artifactId, inputPath, forceGc).GetAwaiter().GetResult();

    // Keep completed Task/Cycle temporaries out of the caller's live heap probe.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CompleteAndRetireOwners(SetupDefinition definition, string artifactId, string inputPath) =>
        CompleteCycle(definition, artifactId, inputPath, false).Owners;

    private static async Task<Cycle> CycleAsync(SetupDefinition definition, string artifactId, string inputPath, bool forceGc)
    {
        var meter = new Meter(forceGc);
        long started = Stopwatch.GetTimestamp();
        byte[] bytes = File.ReadAllBytes(inputPath);
        meter.Sample();
        long inspectStarted = Stopwatch.GetTimestamp();
        using DefinitionImportAssessment assessment = await DefinitionImport.InspectAsync(
            definition, artifactId, bytes, meter.Checkpoint);
        double reportMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        double inspectMs = Stopwatch.GetElapsedTime(inspectStarted).TotalMilliseconds;
        meter.Sample();
        long inspectAllocated = meter.Allocated;
        long inspectHeap = meter.LastHeap;
        long autofillStarted = Stopwatch.GetTimestamp();
        DefinitionImportResult result = await assessment.AssessAutofillAsync(Empty, null, meter.Checkpoint);
        double autofillMs = Stopwatch.GetElapsedTime(autofillStarted).TotalMilliseconds;
        meter.Sample();
        long autofillAllocated = meter.Allocated - inspectAllocated;
        long autofillHeap = meter.LastHeap;
        WeakReference[] owners = [new(assessment), new(bytes), new(result.Answers ?? (object)result)];
        DefinitionImportReport report = assessment.Report;
        long disposeStarted = Stopwatch.GetTimestamp();
        assessment.Dispose();
        CryptographicOperations.ZeroMemory(bytes);
        double disposeMs = Stopwatch.GetElapsedTime(disposeStarted).TotalMilliseconds;
        meter.Sample();
        return new(reportMs, inspectMs, autofillMs, disposeMs, meter.Allocated, inspectAllocated, autofillAllocated,
            meter.PeakHeapDelta, meter.PeakLiveDelta, meter.Upper, inspectHeap, autofillHeap, meter.LastHeap,
            meter.Checkpoints, report.WorkUnits, report.InverseNodes, report.ValidationNodes,
            report.RetainedFindingCount, report.ExecutionCompleted, report.InventoryComplete, report.CoverageComplete,
            result.Succeeded, report.Compatibility, owners);
    }

    private static void PrintCycle(string cohort, string phase, int index, Cycle cycle) =>
        Console.WriteLine(FormattableString.Invariant(
            $"SAMPLE cohort={cohort} phase={phase} sample={index} reportMs={cycle.ReportMilliseconds:F3} inspectMs={cycle.InspectMilliseconds:F3} autofillMs={cycle.AutofillMilliseconds:F3} disposeMs={cycle.DisposeMilliseconds:F3} allocatedBytes={cycle.AllocatedBytes} inspectAllocatedBytes={cycle.InspectAllocatedBytes} autofillAllocatedBytes={cycle.AutofillAllocatedBytes} inspectHeapBytes={cycle.InspectHeapBytes} autofillHeapBytes={cycle.AutofillHeapBytes} disposeHeapBytes={cycle.DisposeHeapBytes} sampledHeapDeltaBytes={cycle.PeakHeapDeltaBytes} checkpoints={cycle.Checkpoints} work={cycle.WorkUnits} inverse={cycle.InverseNodes} validation={cycle.ValidationNodes} findings={cycle.Findings} executionComplete={cycle.ExecutionCompleted} inventoryComplete={cycle.InventoryComplete} coverageComplete={cycle.CoverageComplete} autofillSucceeded={cycle.AutofillSucceeded} compatibility={cycle.Compatibility}"));

    private static long CollectCompleted()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        return GC.GetTotalMemory(false);
    }

    private static void Identity(string name, string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Console.WriteLine($"IDENTITY name={name} bytes={bytes.Length} sha256={Convert.ToHexStringLower(SHA256.HashData(bytes))}");
    }

    /// <summary>
    /// Full-GC live heap plus all later allocations bounds managed live heap until the next
    /// checkpoint, including unobserved transients. The certificate uses the inclusive bound;
    /// baseline subtraction is only an estimate, since unrelated baseline objects may die.
    /// </summary>
    private sealed class Meter
    {
        private readonly bool _forceGc;
        private readonly long _baseline;
        private readonly long _allocatedStart;
        private long _lastAllocated;
        private long _lastLive;

        internal Meter(bool forceGc)
        {
            _forceGc = forceGc;
            _baseline = forceGc ? CollectCompleted() : GC.GetTotalMemory(false);
            _lastLive = _baseline;
            _allocatedStart = _lastAllocated = GC.GetTotalAllocatedBytes(precise: true);
        }

        internal long Allocated => GC.GetTotalAllocatedBytes(precise: true) - _allocatedStart;
        internal long LastHeap { get; private set; }
        internal long PeakHeapDelta { get; private set; }
        internal long PeakLiveDelta { get; private set; }
        internal long Upper { get; private set; }
        internal int Checkpoints { get; private set; }

        internal ValueTask Checkpoint(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sample();
            Checkpoints++;
            return ValueTask.CompletedTask;
        }

        internal void Sample()
        {
            LastHeap = GC.GetTotalMemory(false);
            PeakHeapDelta = Math.Max(PeakHeapDelta, Math.Max(0, LastHeap - _baseline));
            if (!_forceGc) return;
            // Read allocation after heap sampling so sampling overhead is conservatively charged.
            long allocated = GC.GetTotalAllocatedBytes(precise: true);
            Upper = Math.Max(Upper, _lastLive + allocated - _lastAllocated);
            _lastAllocated = allocated;
            _lastLive = CollectCompleted();
            PeakLiveDelta = Math.Max(PeakLiveDelta, Math.Max(0, _lastLive - _baseline));
        }
    }

    private sealed record Cycle(double ReportMilliseconds, double InspectMilliseconds, double AutofillMilliseconds,
        double DisposeMilliseconds, long AllocatedBytes, long InspectAllocatedBytes, long AutofillAllocatedBytes,
        long PeakHeapDeltaBytes, long PeakLiveDeltaBytes, long UpperBytes, long InspectHeapBytes, long AutofillHeapBytes,
        long DisposeHeapBytes, int Checkpoints, int WorkUnits, int InverseNodes, int ValidationNodes, int Findings,
        bool ExecutionCompleted, bool InventoryComplete, bool CoverageComplete, bool AutofillSucceeded,
        DefinitionImportCompatibility Compatibility, WeakReference[] Owners);
}
