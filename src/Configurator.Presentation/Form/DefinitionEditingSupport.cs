namespace Configurator.Presentation;

using Configurator.Core.Definitions;

/// <summary>Placement admission for clients whose controls currently render ordinary root properties.</summary>
public static class DefinitionEditingSupport
{
    public static bool HasPropertyRoot(Definition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Configuration.AdditionalProperties is null && definition.Configuration.Variants.Count == 0;
    }

    /// <summary>Rejects unsupported root controls before a client can collect answers; nested shapes remain admitted.</summary>
    public static void RequirePropertyRoot(Definition definition)
    {
        if (!HasPropertyRoot(definition)) throw new ArgumentException("consumer-root-unsupported", nameof(definition));
    }
}
