namespace Configurator.Core.Composition;

using Configurator.Core;

public sealed record CompositionScaleTelemetry(
    CompositionSourceKind SourceKind,
    CompositionScaleProfileId Profile,
    CompositionScaleOutcome Outcome,
    int AggregateBytes,
    int Nodes,
    int Files,
    long DurationMicroseconds);
