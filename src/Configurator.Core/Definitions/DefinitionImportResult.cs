namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;
using Configurator.Core.Dotenv;
using Configurator.Core.Environment;

public sealed record DefinitionImportResult(bool Succeeded, AnswerSnapshot? Answers, string Code)
{
    public IReadOnlyList<string> ProvidedFields { get; init; } = [];
    public DefinitionDiagnostic? Diagnostic { get; init; }
    public DefinitionImportSchemaLocation? Schema { get; init; }
    public override string ToString() => $"{nameof(DefinitionImportResult)}:{Code}";
}
