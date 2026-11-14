namespace Configurator.Terminal;

using System.Drawing;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.Views;

/// <summary>Virtualized ITableSource table view over consent-safe CompactPropertyRow items.</summary>
internal sealed class TerminalCompactTableView : TableView
{
    private readonly DefinitionFormModel _form;
    private readonly List<CompactPropertyRow> _rows = [];
    private readonly CompactTableSource _source;
    private bool _includeInactive;
    private string? _sectionId;
    private string? _query;
    private DefinitionDiscoveryFilter _filter = DefinitionDiscoveryFilter.All;

    internal TerminalCompactTableView(DefinitionFormModel form)
    {
        _form = form;
        _source = new(this);
        FullRowSelect = true;
        MultiSelect = false;
        Style.ShowHeaders = true;
        Style.AlwaysShowHeaders = true;
        Table = _source;
        RefreshRows();
    }

    internal IReadOnlyList<CompactPropertyRow> Rows => _rows;

    internal int SelectedRowIndex
    {
        get => Value?.SelectedCell.Y ?? -1;
        set
        {
            if (_rows.Count == 0)
            {
                Value = null;
                return;
            }
            int clamped = Math.Clamp(value, 0, _rows.Count - 1);
            Value = new TableSelection(new Point(0, clamped));
            EnsureCursorIsVisible();
        }
    }

    internal CompactPropertyRow? SelectedRowItem =>
        SelectedRowIndex is int row && row >= 0 && row < _rows.Count ? _rows[row] : null;

    internal void RefreshRows(bool? includeInactive = null, string? sectionId = null, string? query = null,
        DefinitionDiscoveryFilter? filter = null)
    {
        DefinitionPath? priorPath = SelectedRowItem?.DataPath;
        if (includeInactive is { } inc) _includeInactive = inc;
        if (sectionId is not null || query is not null) _sectionId = sectionId;
        if (query is not null) _query = query;
        if (filter is { } f) _filter = f;

        _rows.Clear();
        _rows.AddRange(_form.GetCompactRows(_includeInactive, _sectionId, _query, _filter));
        Update();
        if (_rows.Count == 0)
        {
            Value = null;
            return;
        }
        int restored = priorPath is null ? -1 : _rows.FindIndex(row => row.DataPath.Equals(priorPath));
        SelectedRowIndex = restored >= 0 ? restored : Math.Clamp(Math.Max(0, SelectedRowIndex), 0, _rows.Count - 1);
    }

    internal bool SelectRowByPath(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        int index = _rows.FindIndex(row => row.DataPath.Equals(path));
        if (index < 0) return false;
        SelectedRowIndex = index;
        return true;
    }

    internal bool TryEditSelected(string? text)
    {
        if (SelectedRowItem is not { } row || row.IsReadOnly || row.IsSecretLocked || row.IsSensitiveMasked)
            return false;
        bool edited = _form.EditCompactValue(row.DataPath, text);
        RefreshRows();
        return edited;
    }

    internal bool TryOmitSelected()
    {
        if (SelectedRowItem is not { } row || row.IsReadOnly || row.IsSecretLocked)
            return false;
        bool omitted = _form.EditCompactValue(row.DataPath, null);
        RefreshRows();
        return omitted;
    }

    private sealed class CompactTableSource(TerminalCompactTableView owner) : ITableSource
    {
        private static readonly string[] FallbackHeaders = ["Field", "Status", "Origin", "Value"];

        public int Rows => owner._rows.Count;

        public int Columns => 4;

        public string[] ColumnNames
        {
            get
            {
                string[] parts = TerminalStrings.Get("CompactColumns", owner._form.Locale).Split('|');
                return parts.Length == 4 ? parts : FallbackHeaders;
            }
        }

        public object this[int row, int col]
        {
            get
            {
                if (row < 0 || row >= owner._rows.Count) return string.Empty;
                CompactPropertyRow item = owner._rows[row];
                string locale = owner._form.Locale;
                return col switch
                {
                    0 => Sanitize(item.PrimaryLabel + (item.IsRequired ? " *" : "")),
                    1 => item.HasError ? "!"
                        : item.IsDeferred ? TerminalStrings.Get("Omitted", locale)
                        : item.IsRequired && string.IsNullOrEmpty(item.ValuePreview) ? TerminalStrings.Get("Required", locale)
                        : "OK",
                    2 => item.Origin switch
                    {
                        DefinitionAnswerOrigin.Omitted => TerminalStrings.Get("Omitted", locale),
                        DefinitionAnswerOrigin.Generated => TerminalStrings.Get("GeneratedValue", locale),
                        _ => item.Origin.ToString()
                    },
                    3 => item.IsSecretLocked || item.IsSensitiveMasked
                        ? (item.ValuePreview.Length > 0 ? "***" : string.Empty)
                        : Sanitize(item.ValuePreview),
                    _ => string.Empty
                };
            }
        }

        private static string Sanitize(string text) =>
            new(text.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
    }
}
