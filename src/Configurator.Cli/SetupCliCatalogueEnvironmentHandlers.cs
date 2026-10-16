using System.Text.Json;
using Configurator.Core;
using Configurator.Core.Definitions;

namespace Configurator.Cli;

internal static class SetupCliCatalogueEnvironmentHandlers
{
    internal static async Task<SetupCliCommandResult> CatalogueAsync(SetupCliCommand command, SetupCliInvocation invocation,
        SetupCliOutcomeLedger ledger, CancellationToken cancellationToken)
    {
        SetupDefinition definition = await SetupCliIoOperations.DefinitionAsync(invocation, command.Definition!, cancellationToken);
        SetupFieldDefinition[] fields = command.Operation == "list"
            ? definition.Fields.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray()
            : definition.Fields.Where(item => item.Id == command.Key).ToArray();
        if (command.Operation != "list" && fields.Length == 0)
            return SetupCliResults.Failure(SetupCliExitCode.Validation, "catalogue-key-unknown", "$.key");

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            if (command.Operation == "list") writer.WriteStartArray();
            foreach (SetupFieldDefinition field in fields)
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteStartObject();
                writer.WriteString("id", field.Id);
                writer.WriteString("type", field.Type);
                writer.WriteString("sensitivity", SetupCliResults.Lower(field.Sensitivity));
                writer.WriteBoolean("required", field.Required);
                writer.WriteString("disclosure", SetupCliResults.Lower(field.Disclosure));
                if (command.Operation == "describe")
                {
                    using JsonDocument constraints = JsonDocument.Parse(field.Constraints.GetRawText());
                    writer.WriteBoolean("conditionalActivation", constraints.RootElement.TryGetProperty("activeWhen", out _));
                    writer.WriteBoolean("conditionalRequirement", constraints.RootElement.TryGetProperty("requiredWhen", out _));
                    writer.WriteBoolean("hasDefault", field.Default.HasValue);
                    writer.WriteBoolean("hasRecommendation", field.Recommended.HasValue);
                }
                // Free text, defaults and choices are not public output authority.
                writer.WriteEndObject();
            }
            if (command.Operation == "list") writer.WriteEndArray();
        }
        byte[] bytes = stream.ToArray();
        SetupCliCommandResult result = SetupCliResults.Success([SetupCliResults.Artifact("catalogue", "application/json", bytes, "public",
            SetupCliResults.PathIntent(command.Output), command.DryRun ? "planned" : "not-attempted")]);
        if (command.DryRun) return result;
        ledger.Plan(invocation, command, result);
        cancellationToken.ThrowIfCancellationRequested();
        ledger.BeginPublication();
        return ledger.Apply(await invocation.Io.Output.WriteArtifactAsync(command.Output!, SetupArtifactKind.PublicCatalogue,
            bytes, invocation.Io.MaximumBytes, cancellationToken), result);
    }
}
