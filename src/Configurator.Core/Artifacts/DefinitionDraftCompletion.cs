namespace Configurator.Core.Artifacts;

using Configurator.Core.Composition;
using Configurator.Core.Definitions;

public sealed record DefinitionDraftCompletion(string ArtifactId, string FieldPath, string OutputPath);
