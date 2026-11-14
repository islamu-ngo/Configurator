namespace Configurator.Browser.Components.Configuration;

using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Configurator.Browser.Resources;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Virtualized compact setting grid backed directly by <see cref="CompactPropertyRow"/> projections
/// with consent-enforced secret locking, public-only filtering/sorting, and debounced search.
/// </summary>
public partial class ConfigurationCompactTable : IDisposable
{
    [Inject] private TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    [Parameter, EditorRequired] public DefinitionFormModel Form { get; set; } = null!;
    [Parameter] public EventCallback RequestSecretConsent { get; set; }
    [Parameter] public EventCallback<Configurator.Core.Definitions.DefinitionPath> OpenFieldInGuided { get; set; }

    private static readonly DefinitionDiscoveryFilter[] Filters =
    [
        DefinitionDiscoveryFilter.All,
        DefinitionDiscoveryFilter.Invalid,
        DefinitionDiscoveryFilter.MissingRequired,
        DefinitionDiscoveryFilter.Modified,
        DefinitionDiscoveryFilter.DeferredSecret
    ];

    private sealed record IndexedCompactRow(int Index, CompactPropertyRow Row);

    private DefinitionFormModel? _observedForm;
    private ITimer? _searchTimer;
    private string _pendingSearch = string.Empty;
    private string _debouncedQuery = string.Empty;
    private DefinitionDiscoveryFilter _filter = DefinitionDiscoveryFilter.All;
    private bool _includeInactive;
    private string? _sortColumn;
    private bool _sortAscending = true;
    private string? _activeIdentity;
    private IndexedCompactRow[] _indexedRows = [];
    private bool _disposed;

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_observedForm, Form))
        {
            DetachForm();
            _observedForm = Form;
            Form.PropertyChanged += OnFormChanged;
            Form.InputChanged += OnFormInputChanged;
            _activeIdentity = null;
        }
        RefreshRows();
    }

    private void OnFormChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed) return;
        _ = InvokeAsync(() =>
        {
            if (_disposed) return;
            RefreshRows();
            StateHasChanged();
        });
    }

    private void OnFormInputChanged(object? sender, EventArgs args)
    {
        if (_disposed) return;
        _ = InvokeAsync(() =>
        {
            if (_disposed) return;
            RefreshRows();
            StateHasChanged();
        });
    }

    private void OnSearchInput(ChangeEventArgs args)
    {
        if (_disposed) return;
        _pendingSearch = args.Value?.ToString() ?? string.Empty;
        _searchTimer?.Dispose();
        _searchTimer = TimeProvider.CreateTimer(_ =>
        {
            if (_disposed) return;
            _ = InvokeAsync(() =>
            {
                if (_disposed) return;
                _debouncedQuery = _pendingSearch;
                RefreshRows();
                StateHasChanged();
            });
        }, null, TimeSpan.FromMilliseconds(150), Timeout.InfiniteTimeSpan);
    }

    private void SetFilter(DefinitionDiscoveryFilter filter)
    {
        if (_filter == filter) return;
        _filter = filter;
        RefreshRows();
    }

    private void ToggleIncludeInactive(ChangeEventArgs args)
    {
        _includeInactive = args.Value is true;
        RefreshRows();
    }

    private void ToggleSort(string column)
    {
        if (string.Equals(_sortColumn, column, StringComparison.Ordinal))
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }
        RefreshRows();
    }

    private string SortAria(string column)
    {
        if (!string.Equals(_sortColumn, column, StringComparison.Ordinal)) return "none";
        return _sortAscending ? "ascending" : "descending";
    }

    private void RefreshRows()
    {
        IReadOnlyList<CompactPropertyRow> raw = Form.GetCompactRows(
            includeInactive: _includeInactive,
            sectionId: null,
            query: _debouncedQuery,
            filter: _filter);

        IEnumerable<CompactPropertyRow> ordered = _sortColumn switch
        {
            "section" => _sortAscending
                ? raw.OrderBy(item => item.SectionTitle, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.SortKey, StringComparer.OrdinalIgnoreCase)
                : raw.OrderByDescending(item => item.SectionTitle, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.SortKey, StringComparer.OrdinalIgnoreCase),
            "setting" => _sortAscending
                ? raw.OrderBy(item => item.SortKey, StringComparer.OrdinalIgnoreCase)
                : raw.OrderByDescending(item => item.SortKey, StringComparer.OrdinalIgnoreCase),
            "origin" => _sortAscending
                ? raw.OrderBy(item => OriginSortRank(item.Origin)).ThenBy(item => item.SortKey, StringComparer.OrdinalIgnoreCase)
                : raw.OrderByDescending(item => OriginSortRank(item.Origin)).ThenBy(item => item.SortKey, StringComparer.OrdinalIgnoreCase),
            "status" => _sortAscending
                ? raw.OrderBy(StatusSortRank).ThenBy(item => item.SortKey, StringComparer.OrdinalIgnoreCase)
                : raw.OrderByDescending(StatusSortRank).ThenBy(item => item.SortKey, StringComparer.OrdinalIgnoreCase),
            _ => raw
        };

        _indexedRows = ordered.Select((row, index) => new IndexedCompactRow(index, row)).ToArray();
    }

    private static int OriginSortRank(DefinitionAnswerOrigin origin) => origin switch
    {
        DefinitionAnswerOrigin.Explicit => 0,
        DefinitionAnswerOrigin.Generated => 1,
        DefinitionAnswerOrigin.Imported => 2,
        DefinitionAnswerOrigin.Defaulted => 3,
        _ => 4
    };

    private static int StatusSortRank(CompactPropertyRow row) =>
        row.HasError ? 0 :
        row.IsDeferred ? 1 :
        row.IsRequired && row.Origin == DefinitionAnswerOrigin.Omitted ? 2 :
        !row.IsActive ? 4 : 3;

    private void SetActiveRow(string identity) => _activeIdentity = identity;

    private string ScalarValue(CompactPropertyRow row) =>
        row.IsSensitiveMasked ? string.Empty : row.ValuePreview;

    private static string BooleanValueToken(CompactPropertyRow row)
    {
        if (row.IsSensitiveMasked || row.Origin == DefinitionAnswerOrigin.Omitted) return string.Empty;
        return row.ValuePreview switch
        {
            "true" => "true",
            "false" => "false",
            _ => string.Empty
        };
    }

    private static string ChoiceIndexToken(CompactPropertyRow row)
    {
        if (row.IsSensitiveMasked || row.Origin == DefinitionAnswerOrigin.Omitted) return string.Empty;
        for (int index = 0; index < row.Choices.Count; index++)
        {
            JsonElement choice = row.Choices[index].Value;
            string token = choice.ValueKind == JsonValueKind.String ? choice.GetString() ?? string.Empty : choice.GetRawText();
            if (string.Equals(token, row.ValuePreview, StringComparison.Ordinal))
                return index.ToString(CultureInfo.InvariantCulture);
        }
        return string.Empty;
    }

    private void OnScalarInput(CompactPropertyRow row, ChangeEventArgs args)
    {
        if (_disposed || row.IsSecretLocked || row.IsSensitiveMasked || !row.CanEdit || row.IsReadOnly) return;
        _activeIdentity = row.EditorIdentity;
        string? raw = args.Value?.ToString();
        Form.EditCompactValue(row.DataPath, string.IsNullOrEmpty(raw) ? null : raw);
        RefreshRows();
    }

    private void OnBooleanChanged(CompactPropertyRow row, ChangeEventArgs args)
    {
        if (_disposed || row.IsSecretLocked || row.IsSensitiveMasked || !row.CanEdit || row.IsReadOnly) return;
        _activeIdentity = row.EditorIdentity;
        string? value = args.Value?.ToString();
        Form.EditCompactValue(row.DataPath, string.IsNullOrEmpty(value) ? null : value);
        RefreshRows();
    }

    private void OnChoiceChanged(CompactPropertyRow row, ChangeEventArgs args)
    {
        if (_disposed || row.IsSecretLocked || row.IsSensitiveMasked || !row.CanEdit || row.IsReadOnly) return;
        _activeIdentity = row.EditorIdentity;
        string? raw = args.Value?.ToString();
        if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
            || index < 0 || index >= row.Choices.Count)
        {
            Form.EditCompactValue(row.DataPath, null);
        }
        else
        {
            JsonElement choice = row.Choices[index].Value;
            string serialized = choice.ValueKind == JsonValueKind.String
                ? choice.GetString() ?? string.Empty
                : choice.GetRawText();
            Form.EditCompactValue(row.DataPath, serialized);
        }
        RefreshRows();
    }

    private string OriginText(DefinitionAnswerOrigin origin) => Ui(origin switch
    {
        DefinitionAnswerOrigin.Defaulted => "ReviewOriginDefault",
        DefinitionAnswerOrigin.Imported => "ReviewOriginImported",
        DefinitionAnswerOrigin.Generated => "SecretGeneratedOrigin",
        DefinitionAnswerOrigin.Explicit => "ReviewOriginExplicit",
        _ => "ReviewOriginOmitted"
    });

    private string StatusText(CompactPropertyRow row) => Ui(!row.IsActive ? "ReviewStateInactive"
        : row.IsDeferred ? "ReviewStateDeferred"
        : row.HasError ? "ReviewStateCorrection"
        : row.Origin != DefinitionAnswerOrigin.Omitted ? "ReviewStateReady"
        : row.IsRequired ? "ReviewStateRequiredMissing" : "ReviewStateOptionalMissing");

    private static string FilterToken(DefinitionDiscoveryFilter filter) => filter switch
    {
        DefinitionDiscoveryFilter.Invalid => "invalid",
        DefinitionDiscoveryFilter.MissingRequired => "missing-required",
        DefinitionDiscoveryFilter.Modified => "modified",
        DefinitionDiscoveryFilter.DeferredSecret => "deferred-secret",
        _ => "all"
    };

    private static string FilterResourceKey(DefinitionDiscoveryFilter filter) => filter switch
    {
        DefinitionDiscoveryFilter.Invalid => "FilterInvalid",
        DefinitionDiscoveryFilter.MissingRequired => "FilterMissingRequired",
        DefinitionDiscoveryFilter.Modified => "FilterModified",
        DefinitionDiscoveryFilter.DeferredSecret => "FilterDeferredSecret",
        _ => "FilterAll"
    };

    private string Ui(string key) => BrowserStrings.Get(key, Form.Locale);

    private string UiFormat(string key, params object?[] args) =>
        string.Format(CultureInfo.GetCultureInfo(Form.Locale), Ui(key), args);

    private void DetachForm()
    {
        if (_observedForm is null) return;
        _observedForm.PropertyChanged -= OnFormChanged;
        _observedForm.InputChanged -= OnFormInputChanged;
        _observedForm = null;
    }

    public void Dispose()
    {
        _disposed = true;
        _searchTimer?.Dispose();
        _searchTimer = null;
        DetachForm();
        GC.SuppressFinalize(this);
    }
}
