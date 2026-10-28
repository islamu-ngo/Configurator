namespace Configurator.Browser;

using System.Collections.Frozen;
using Configurator.Core;
using Configurator.Core.Definitions;

public sealed class BrowserLaunchException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

/// <summary>Captures the exact admitted document, not a mutable registry lookup.</summary>
public sealed record BrowserLaunchSelection(SetupDefinition Definition, string Entry, string Locale)
{
    internal static IReadOnlySet<string> EditingCapabilities { get; } = new[]
    {
        "predicate.item", "predicate.root", "projection.copy", "projection.index", "projection.list",
        "projection.literal", "projection.object", "projection.omit", "projection.repeat", "relationship.presence",
        "schema.array", "schema.boolean", "schema.bounds", "schema.const", "schema.enum", "schema.integer",
        "schema.notBlank", "schema.nullable", "schema.number", "schema.object", "schema.string",
        "schema.uniqueBy", "schema.map", "schema.variant", "schema.localRef", "schema.multipleOf",
        "schema.uniqueItems", "schema.assertFormat", "values.initial", "values.suggested"
    }.ToFrozenSet(StringComparer.Ordinal);
    public static BrowserLaunchSelection Resolve(
        SetupDefinitionRegistry registry, string familyId, string query)
    {
        if (!Guid.TryParseExact(familyId, "D", out Guid family)
            || family == Guid.Empty || family.ToString("D") != familyId)
            throw new BrowserLaunchException("family-invalid");
        if (query.Length > 1024 || query.Contains('#', StringComparison.Ordinal))
            throw new BrowserLaunchException("query-invalid");
        var selectors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (query.StartsWith('?')) query = query[1..];
        if (query.Length > 0)
        {
            foreach (string part in query.Split('&'))
            {
                int separator = part.IndexOf('=', StringComparison.Ordinal);
                if (separator <= 0 || !ValidEscapes(part))
                    throw new BrowserLaunchException("query-invalid");
                string name = Uri.UnescapeDataString(part[..separator]);
                string value = Uri.UnescapeDataString(part[(separator + 1)..]);
                int maximum = name switch
                {
                    "appVersion" => 64,
                    "entry" => 128,
                    "locale" => 35,
                    "digest" => 64,
                    _ => 0
                };
                if (value.Length == 0 || value.Length > maximum
                    || !value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
                    || !selectors.TryAdd(name, value))
                    throw new BrowserLaunchException("query-invalid");
            }
        }

        ArtifactDigest? digest = null;
        if (selectors.TryGetValue("digest", out string? suppliedDigest))
        {
            if (!ArtifactDigest.TryParse(suppliedDigest, out var parsed))
                throw new BrowserLaunchException("digest-invalid");
            digest = parsed;
        }
        SetupDefinition definition;
        try
        {
            definition = registry.Resolve(family, selectors.GetValueOrDefault("appVersion"), digest);
        }
        catch (SetupDefinitionException exception)
        {
            throw new BrowserLaunchException(exception.Code);
        }
        if (definition.RequiredCapabilities.Any(capability => !EditingCapabilities.Contains(capability)))
            throw new BrowserLaunchException("capability-unsupported");
        string entry = selectors.GetValueOrDefault("entry") ?? definition.EntryPoints[0];
        string locale = selectors.GetValueOrDefault("locale") ?? new DefinitionLocalization(definition).DefaultLocale;
        if (!definition.EntryPoints.Contains(entry, StringComparer.Ordinal))
            throw new BrowserLaunchException("entry-unknown");
        if (!definition.Locales.Contains(locale, StringComparer.Ordinal))
            throw new BrowserLaunchException("locale-unsupported");
        return new BrowserLaunchSelection(definition, entry, locale);
    }

    private static bool ValidEscapes(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '%') continue;
            if (i + 2 >= text.Length || !Uri.IsHexDigit(text[i + 1]) || !Uri.IsHexDigit(text[i + 2]))
                return false;
            i += 2;
        }
        return true;
    }
}
