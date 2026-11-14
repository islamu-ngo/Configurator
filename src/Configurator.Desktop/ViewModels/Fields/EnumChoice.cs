namespace Configurator.Desktop.ViewModels.Fields;

using CommunityToolkit.Mvvm.ComponentModel;

public sealed class EnumChoice : ObservableObject
{
    private readonly Action<int>? _select;
    private string _label;
    private string? _explanation;
    private bool _isDeprecated;
    private string? _deprecationGuidance;
    private bool _isSelected;

    internal EnumChoice(
        int index,
        string label,
        string? explanation = null,
        bool isDeprecated = false,
        string? deprecationGuidance = null,
        Action<int>? select = null)
    {
        Index = index;
        _label = label;
        _explanation = explanation;
        _isDeprecated = isDeprecated;
        _deprecationGuidance = deprecationGuidance;
        _select = select;
    }

    public int Index { get; }
    public string Label => _label;
    public string? Explanation => _explanation;
    public bool IsDeprecated => _isDeprecated;
    public string? DeprecationGuidance => _deprecationGuidance;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value && !_isSelected) _select?.Invoke(Index);
        }
    }

    internal void UpdateLabel(string label) => SetProperty(ref _label, label, nameof(Label));

    internal void UpdateMetadata(string label, string? explanation, bool isDeprecated, string? deprecationGuidance)
    {
        SetProperty(ref _label, label, nameof(Label));
        SetProperty(ref _explanation, explanation, nameof(Explanation));
        SetProperty(ref _isDeprecated, isDeprecated, nameof(IsDeprecated));
        SetProperty(ref _deprecationGuidance, deprecationGuidance, nameof(DeprecationGuidance));
    }

    internal void SyncSelected(bool selected) => SetProperty(ref _isSelected, selected, nameof(IsSelected));
}
