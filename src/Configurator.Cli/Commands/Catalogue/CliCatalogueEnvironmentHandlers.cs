using System.Text.Json;
using Configurator.Core;
using Configurator.Core.Definitions;

namespace Configurator.Cli;

internal static class CliCatalogueEnvironmentHandlers
{
    internal static async Task<CliCommandResult> CatalogueAsync(CliCommand command, CliInvocation invocation,
        CliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        Definition definition = await CliIoOperations.DefinitionAsync(invocation, command.Definition!, cancellationToken);
        FieldDefinition[] fields = command.Operation == "list"
            ? definition.Fields.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray()
            : definition.Fields.Where(item => item.Id == command.Key).ToArray();
        if (command.Operation != "list" && fields.Length == 0)
            return CliResults.Failure(CliExitCode.Validation, "catalogue-key-unknown", "/key");

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            if (command.Operation == "list") writer.WriteStartArray();
            foreach (FieldDefinition field in fields)
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteStartObject();
                writer.WriteString("id", field.Id);
                writer.WriteString("type", field.Type);
                writer.WriteString("sensitivity", CliResults.Lower(field.Sensitivity));
                writer.WriteBoolean("required", field.Required);
                writer.WriteString("disclosure", CliResults.Lower(field.Disclosure));
                if (command.Operation == "describe")
                {
                    using JsonDocument constraints = JsonDocument.Parse(field.Constraints.GetRawText());
                    writer.WriteBoolean("conditionalActivation", constraints.RootElement.TryGetProperty("x-configurator", out var extension)
                        && extension.TryGetProperty("applicableWhen", out _));
                    writer.WriteBoolean("conditionalRequirement", definition.Configuration.Keywords.TryGetProperty("if", out _)
                        || definition.Configuration.Keywords.TryGetProperty("dependentRequired", out _));
                    writer.WriteBoolean("hasDefault", field.Default.HasValue);
                    writer.WriteBoolean("hasRecommendation", field.SuggestedValue.HasValue);
                }
                // Free text, defaults and choices are not public output authority.
                writer.WriteEndObject();
            }
            if (command.Operation == "list") writer.WriteEndArray();
        }
        byte[] bytes = stream.ToArray();
        CliCommandResult result = CliResults.Success([CliResults.Artifact("catalogue", "application/json", bytes, "public",
            CliResults.PathIntent(command.Output), command.DryRun ? "planned" : "not-attempted")]);
        if (command.DryRun) return result;
        ledger.Plan(invocation, command, result);
        cancellationToken.ThrowIfCancellationRequested();
        ledger.BeginPublication();
        return ledger.Apply(await invocation.Io.Output.WriteArtifactAsync(command.Output!, ArtifactKind.PublicCatalogue,
            bytes, invocation.Io.MaximumBytes, cancellationToken), result);
    }
}
