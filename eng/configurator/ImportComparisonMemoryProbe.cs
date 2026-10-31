namespace Configurator.Engineering;

using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

/// <summary>Numeric-only instrumentation linked exclusively into an explicit local measurement candidate.</summary>
[SupportedOSPlatform("browser")]
public static partial class ImportComparisonMemoryProbe
{
    [JSExport]
    public static double AllocatedBytes() => GC.GetTotalAllocatedBytes(precise: true);

    [JSExport]
    public static double ThreadAllocatedBytes() => GC.GetAllocatedBytesForCurrentThread();

    [JSExport]
    public static double HeapBytes() => GC.GetTotalMemory(forceFullCollection: false);

    [JSExport]
    public static double CollectHeapBytes() => GC.GetTotalMemory(forceFullCollection: true);

#if CONFIGURATOR_IMPORT_OWNERSHIP_PROBE
    [JSExport]
    public static int BeginOwnershipCycle() => ImportComparisonOwnershipProbe.BeginCycle();

    [JSExport]
    public static int BeginCancellationCycle() => ImportComparisonOwnershipProbe.BeginCycle(cancellation: true);

    [JSExport]
    public static string OwnershipSnapshot(int cycleNumber) => ImportComparisonOwnershipProbe.Snapshot(cycleNumber);

    [JSExport]
    public static string CancellationBoundarySnapshot(int cycleNumber) => ImportComparisonOwnershipProbe.BoundarySnapshot(cycleNumber);

    [JSExport]
    public static Task WaitForComparisonTask(int cycleNumber) => ImportComparisonOwnershipProbe.WaitForTask(cycleNumber, handler: false);

    [JSExport]
    public static Task WaitForInputHandlerTask(int cycleNumber) => ImportComparisonOwnershipProbe.WaitForTask(cycleNumber, handler: true);
#endif
}
