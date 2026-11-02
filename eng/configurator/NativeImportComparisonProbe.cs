namespace Configurator.Engineering;

/// <summary>Typed callbacks shared only by explicitly instrumented native assemblies.</summary>
internal static class NativeImportComparisonProbe
{
    internal static Action? Checkpoint { get; set; }
    internal static Action<object, byte[]>? Input { get; set; }
    internal static Action? ReadComplete { get; set; }
    internal static Action<byte[]>? DisposeInput { get; set; }
    internal static Action<object, object?, object?, object?>? Proposal { get; set; }
    internal static Action<object>? DisposeProposal { get; set; }
}
