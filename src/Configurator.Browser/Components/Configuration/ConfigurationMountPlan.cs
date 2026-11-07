namespace Configurator.Browser.Components.Configuration;

using System.Globalization;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Presentation;

/// <summary>
/// Private, immutable renderer coordinates. Reference identity lets unchanged subtrees
/// retain their render parameters; neither coordinates nor identities are diagnostic data.
/// </summary>
public sealed class ConfigurationMountNode
{
    internal ConfigurationMountNode(DefinitionPath location, string identity,
        IReadOnlyList<ConfigurationMountNode> children, bool admitted)
    {
        Location = location;
        Identity = identity;
        Children = Array.AsReadOnly(children.ToArray());
        Admitted = admitted;
    }

    public DefinitionPath Location { get; }
    public string Identity { get; }
    public IReadOnlyList<ConfigurationMountNode> Children { get; }
    public bool Admitted { get; }
}

/// <summary>
/// One section's descendant admission state. Construction borrows current form values
/// synchronously, retaining only coordinates, logical identities and immutable admission.
/// </summary>
internal sealed class ConfigurationMountPlan
{
    internal static ConfigurationMountPlan Empty { get; } = new([]);

    private ConfigurationMountPlan(IReadOnlyList<ConfigurationMountNode> roots, int? pendingCount = null)
    {
        Roots = Array.AsReadOnly(roots.ToArray());
        PendingCount = pendingCount ?? Descendants(Roots).Count(node => !node.Admitted);
    }

    internal IReadOnlyList<ConfigurationMountNode> Roots { get; }
    internal int PendingCount { get; }

    internal static IEnumerable<ConfigurationMountNode> Descendants(IEnumerable<ConfigurationMountNode> nodes)
    {
        foreach (ConfigurationMountNode node in nodes)
        {
            yield return node;
            foreach (ConfigurationMountNode child in Descendants(node.Children)) yield return child;
        }
    }

    internal static ConfigurationMountPlan Reconcile(DefinitionFormModel form,
        IReadOnlyList<FieldState> fields, bool literalRoot, ConfigurationMountPlan previous)
    {
        var existing = Descendants(previous.Roots).Where(node => node.Identity.Length > 0)
            .ToDictionary(node => node.Identity, StringComparer.Ordinal);
        var roots = new List<ConfigurationMountNode>();
        if (literalRoot)
        {
            DefinitionPath location = DefinitionPath.FromSegments([]);
            JsonElement? value = form.Validate().TryGetEffectiveValue(location, out JsonElement supplied) ? supplied : null;
            roots.Add(Visit(form.Definition.Configuration, location, value));
        }
        else
        {
            foreach (FieldState state in fields.Where(state => state.IsActive))
            {
                var editor = form.GetFieldEditor(state.Id);
                editor.Input.TryValue(out JsonElement? value);
                roots.Add(Visit(editor.Schema, DefinitionPath.FromSegments([editor.Input.Definition.Name]), value));
            }
        }
        return roots.SequenceEqual(previous.Roots) ? previous : new(roots);

        ConfigurationMountNode Visit(DefinitionNode schema, DefinitionPath location, JsonElement? value)
        {
            string identity = form.GetEditorIdentity(location);
            existing.TryGetValue(identity, out ConfigurationMountNode? old);
            var children = new List<ConfigurationMountNode>();
            if (schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence &&
                value is not { ValueKind: JsonValueKind.Null } && form.IsEditorVisible(location))
            {
                if (schema.Type == DefinitionNodeType.Mapping)
                {
                    DefinitionNode selected = value is { } current
                        ? DefinitionValidator.SelectVariant(schema, current) ?? schema : schema;
                    foreach (var child in selected.Properties)
                    {
                        JsonElement? supplied = value is { ValueKind: JsonValueKind.Object } mapping &&
                            mapping.TryGetProperty(child.Key, out JsonElement entry) ? entry : null;
                        children.Add(Visit(child.Value, Child(child.Key), supplied));
                    }
                    if (selected.AdditionalProperties is { } additional && value is { ValueKind: JsonValueKind.Object } entries)
                        foreach (JsonProperty entry in entries.EnumerateObject())
                            if (!selected.Properties.ContainsKey(entry.Name))
                                children.Add(Visit(additional, Child(entry.Name), entry.Value));
                }
                else if (schema.Type == DefinitionNodeType.Sequence && value is { ValueKind: JsonValueKind.Array } rows)
                    for (int index = 0; index < rows.GetArrayLength(); index++)
                        children.Add(Visit(form.GetListItemMetadata(location), Child(index.ToString(CultureInfo.InvariantCulture)), rows[index]));
            }
            return old is not null && old.Location.Equals(location) && children.SequenceEqual(old.Children)
                ? old : new(location, identity, children, old?.Admitted ?? false);

            DefinitionPath Child(string name) => DefinitionPath.FromSegments([.. location.Segments, name]);
        }
    }

    /// <summary>
    /// Initial traversal retains only ordinal routes into immutable coordinate topology.
    /// Child coordinates are captured in a synchronous borrow; width-dependent capture
    /// and immutable array copies are not bounded by the identity/admission allowance.
    /// </summary>
    internal sealed class InitialDiscovery
    {
        private readonly Stack<int[]> _remaining = new();

        internal ConfigurationMountPlan Start(DefinitionFormModel form, IReadOnlyList<FieldState> fields, bool literalRoot)
        {
            ConfigurationMountNode[] roots = literalRoot
                ? [Undiscovered(DefinitionPath.FromSegments([]))]
                : fields.Select(state => Undiscovered(DefinitionPath.FromSegments(
                    [form.GetFieldEditor(state.Id).Input.Definition.Name]))).ToArray();
            for (int index = roots.Length - 1; index >= 0; index--) _remaining.Push([index]);
            return new(roots, roots.Length);
        }

        internal bool Complete => _remaining.Count == 0;

        internal void Clear() => _remaining.Clear();

        internal ConfigurationMountPlan Advance(DefinitionFormModel form, IReadOnlyList<FieldState> fields,
            bool literalRoot, ConfigurationMountPlan plan, int maximum, DefinitionPath? priority)
        {
            while (maximum > 0)
            {
                int[]? route = PriorityRoute(plan, priority);
                while (route is null && _remaining.TryPop(out int[]? candidate))
                    if (!At(plan.Roots, candidate).Admitted) route = candidate;
                if (route is null) break;

                ConfigurationMountNode old = At(plan.Roots, route);
                string identity = form.GetEditorIdentity(old.Location);
                ConfigurationMountNode[] children = Children(form, fields, literalRoot, route, old.Location);
                var discovered = new ConfigurationMountNode(old.Location, identity, children, admitted: true);
                plan = new(Replace(plan.Roots, route, 0, discovered), plan.PendingCount - 1 + children.Length);
                for (int index = children.Length - 1; index >= 0; index--)
                    _remaining.Push([.. route, index]);
                --maximum;
            }
            // A prioritized node may still have its normal traversal entry. Retire it
            // without charging or resolving its identity a second time.
            while (_remaining.TryPeek(out int[]? route) && At(plan.Roots, route).Admitted) _remaining.Pop();
            return plan;
        }

        private static ConfigurationMountNode Undiscovered(DefinitionPath location) => new(location, "", [], admitted: false);

        private static ConfigurationMountNode At(IReadOnlyList<ConfigurationMountNode> nodes, int[] route)
        {
            ConfigurationMountNode node = nodes[route[0]];
            for (int depth = 1; depth < route.Length; depth++) node = node.Children[route[depth]];
            return node;
        }

        private static ConfigurationMountNode[] Replace(IReadOnlyList<ConfigurationMountNode> nodes,
            int[] route, int depth, ConfigurationMountNode replacement)
        {
            ConfigurationMountNode[] changed = nodes.ToArray();
            int index = route[depth];
            ConfigurationMountNode old = nodes[index];
            changed[index] = depth == route.Length - 1 ? replacement
                : new(old.Location, old.Identity, Replace(old.Children, route, depth + 1, replacement), old.Admitted);
            return changed;
        }

        private static int[]? PriorityRoute(ConfigurationMountPlan plan, DefinitionPath? priority)
        {
            if (priority is null) return null;
            IReadOnlyList<ConfigurationMountNode> nodes = plan.Roots;
            int[] route = [];
            while (nodes.Count > 0)
            {
                int index = 0;
                for (; index < nodes.Count; index++)
                    if (priority.Segments.Take(nodes[index].Location.Segments.Count).SequenceEqual(nodes[index].Location.Segments))
                        break;
                if (index == nodes.Count) return null;
                route = [.. route, index];
                if (!nodes[index].Admitted) return route;
                nodes = nodes[index].Children;
            }
            return null;
        }

        private static ConfigurationMountNode[] Children(DefinitionFormModel form, IReadOnlyList<FieldState> fields,
            bool literalRoot, int[] route, DefinitionPath location)
        {
            DefinitionNode schema = form.GetEditorSchema(location)!;
            if (schema.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence)) return [];
            JsonElement? value;
            if (literalRoot)
                value = form.Validate().TryGetEffectiveValue(location, out JsonElement supplied) ? supplied : null;
            else
            {
                form.GetFieldEditor(fields[route[0]].Id).Input.TryValue(out value);
                foreach (string segment in location.Segments.Skip(1))
                    value = value switch
                    {
                        { ValueKind: JsonValueKind.Object } mapping when mapping.TryGetProperty(segment, out JsonElement entry) => entry,
                        { ValueKind: JsonValueKind.Array } rows => rows[int.Parse(segment, CultureInfo.InvariantCulture)],
                        _ => null
                    };
            }
            if (value is { ValueKind: JsonValueKind.Null } || !form.IsEditorVisible(location)) return [];
            var children = new List<ConfigurationMountNode>();
            if (schema.Type == DefinitionNodeType.Mapping)
            {
                DefinitionNode selected = value is { } current
                    ? DefinitionValidator.SelectVariant(schema, current) ?? schema : schema;
                foreach (var child in selected.Properties) children.Add(Child(child.Key));
                if (selected.AdditionalProperties is not null && value is { ValueKind: JsonValueKind.Object } entries)
                    foreach (JsonProperty entry in entries.EnumerateObject())
                        if (!selected.Properties.ContainsKey(entry.Name)) children.Add(Child(entry.Name));
            }
            else if (value is { ValueKind: JsonValueKind.Array } rows)
                for (int index = 0; index < rows.GetArrayLength(); index++) children.Add(Child(index.ToString(CultureInfo.InvariantCulture)));
            return children.ToArray();

            ConfigurationMountNode Child(string name) => Undiscovered(DefinitionPath.FromSegments([.. location.Segments, name]));
        }
    }

    internal ConfigurationMountPlan Admit(int maximum, DefinitionPath? priority)
    {
        var selected = new HashSet<string>(StringComparer.Ordinal);
        if (priority is not null)
            foreach (ConfigurationMountNode node in Descendants(Roots))
                if (priority.Segments.Take(node.Location.Segments.Count).SequenceEqual(node.Location.Segments))
                    Select(node);
        foreach (ConfigurationMountNode node in Descendants(Roots)) Select(node);
        return selected.Count == 0 ? this : new(Roots.Select(Apply).ToArray());

        void Select(ConfigurationMountNode node)
        {
            if (maximum > 0 && !node.Admitted && selected.Add(node.Identity)) --maximum;
        }

        ConfigurationMountNode Apply(ConfigurationMountNode node)
        {
            ConfigurationMountNode[] children = node.Children.Select(Apply).ToArray();
            bool admitted = node.Admitted || selected.Contains(node.Identity);
            return admitted == node.Admitted && children.SequenceEqual(node.Children) ? node
                : new(node.Location, node.Identity, children, admitted);
        }
    }
}
