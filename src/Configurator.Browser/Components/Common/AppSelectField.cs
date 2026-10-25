namespace Configurator.Browser.Components.Common;

using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;

/// <summary>
/// Refreshes choice callbacks on every parameter update while avoiding unchanged
/// field output. Internal popup and keyboard events retain their own renders.
/// </summary>
public sealed class AppSelectField : MudSelect<string>
{
    [Parameter] public JsonElement ChoiceSchema { get; set; }
    private bool _suppressParameterRender;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        bool Changed<T>(string name, T current) =>
            parameters.TryGetValue<T>(name, out var supplied) && !EqualityComparer<T>.Default.Equals(current, supplied);

        bool attributesChanged = parameters.TryGetValue(nameof(UserAttributes), out Dictionary<string, object>? attributes)
            && ((UserAttributes?.Count ?? 0) != (attributes?.Count ?? 0)
                || attributes?.Any(item => UserAttributes is null
                    || !UserAttributes.TryGetValue(item.Key, out object? current) || !Equals(current, item.Value)) == true);
        _suppressParameterRender = !(attributesChanged
            || Changed(nameof(ChoiceSchema), ChoiceSchema)
            || Changed(nameof(Value), Value)
            || Changed(nameof(InputId), InputId)
            || Changed(nameof(Disabled), Disabled)
            || Changed(nameof(Error), Error)
            || Changed(nameof(ErrorId), ErrorId)
            || Changed(nameof(HelperId), HelperId)
            || Changed(nameof(ErrorText), ErrorText)
            || Changed(nameof(Variant), Variant)
            || Changed(nameof(Margin), Margin));
        try
        {
            return base.SetParametersAsync(parameters);
        }
        finally
        {
            _suppressParameterRender = false;
        }
    }

    protected override bool ShouldRender() => !_suppressParameterRender && base.ShouldRender();
}
