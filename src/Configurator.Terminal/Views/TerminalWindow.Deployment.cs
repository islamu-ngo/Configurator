namespace Configurator.Terminal;

using System.Globalization;
using System.Text;
using Configurator.Core.Definitions;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

internal sealed partial class TerminalWindow
{
    private readonly List<(DefinitionDeploymentRecipe Recipe, DefinitionDeploymentScope Scope,
        DefinitionPath RecipeTitle, DefinitionPath ScopeTitle, Button Button)> _configurationChoices = [];
    private View[] _configurationContent = [];
    private int _configurationRows;

    private View[] CreateConfigurationChoices()
    {
        var recipes = _operation.Form.ConfigurationRecipes;
        for (int recipeIndex = 0; recipeIndex < recipes.Count; recipeIndex++)
        {
            DefinitionDeploymentRecipe recipe = recipes[recipeIndex];
            for (int scopeIndex = 0; scopeIndex < recipe.Scopes.Count; scopeIndex++)
            {
                DefinitionDeploymentScope scope = recipe.Scopes[scopeIndex];
                Button choice = Button("Configuration", 1, 2 + _configurationChoices.Count,
                    () => SelectConfigurationContext(recipe.Id, scope.Id));
                choice.Width = Dim.Fill(2);
                choice.HotKeySpecifier = (Rune)0xffff;
                string recipePosition = recipeIndex.ToString(CultureInfo.InvariantCulture);
                _configurationChoices.Add((recipe, scope,
                    DefinitionPath.FromSegments(["deployment", "recipes", recipePosition, "title"]),
                    DefinitionPath.FromSegments(["deployment", "recipes", recipePosition, "scopes",
                        scopeIndex.ToString(CultureInfo.InvariantCulture), "title"]), choice));
            }
        }
        return _configurationChoices.Select(choice => (View)choice.Button).ToArray();
    }

    private void RefreshConfigurationChoices()
    {
        var form = _operation.Form;
        DefinitionConfigurationContext? context = form.ConfigurationContext;
        int rows = context is null ? 0 : _configurationChoices.Count;
        if (rows != _configurationRows)
        {
            foreach (View control in _configurationContent) control.Y += rows - _configurationRows;
            _configurationRows = rows;
            SetContentHeight(25 + rows);
        }
        foreach (var choice in _configurationChoices)
        {
            bool selected = context?.RecipeId == choice.Recipe.Id && context.ScopeId == choice.Scope.Id;
            choice.Button.Text = Safe(LocalizedText(selected ? "Selected" : "Configuration") + ": "
                + form.LocalizedText(choice.RecipeTitle, choice.Recipe.Title) + " / "
                + form.LocalizedText(choice.ScopeTitle, choice.Scope.Title));
            choice.Button.Visible = !_tooSmall && context is not null;
            choice.Button.Enabled = !selected && !_operation.IsPublishing && Volatile.Read(ref _closeRequested) == 0;
        }
    }

    private void SelectConfigurationContext(string recipeId, string scopeId)
    {
        if (_operation.IsPublishing || !CommitField()) return;
        if (!_operation.Form.SelectConfigurationContext(recipeId, scopeId)) { Status("DraftInvalid"); return; }
        CancelImport();
        _fieldIndex = 0;
        _itemIndex = 0;
        ShowField(preserveLocation: false);
        Status("InputChanged");
    }
}
