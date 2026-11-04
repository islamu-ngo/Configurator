namespace Configurator.Browser.Components.Configuration;

using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;

public partial class ConfigurationEditor
{
    private long _contextChoiceKey;
    private ElementReference _contextControl;
    private string? _developerArtifactId;
    private long _developerDestinationKey;
    private ElementReference _developerDestinationControl;
    private async Task ChangeRecipeAsync(ChangeEventArgs args)
    {
        if (args.Value is string recipe && _form?.ConfigurationContext?.ScopeId is { } scope)
            await SelectContextAsync(recipe, scope);
    }

    private async Task ChangeScopeAsync(ChangeEventArgs args)
    {
        if (args.Value is string scope && _form?.ConfigurationContext?.RecipeId is { } recipe)
            await SelectContextAsync(recipe, scope);
    }

    private async Task SelectContextAsync(string recipe, string scope)
    {
        DefinitionFormModel? form = _form;
        if (_disposed || !_ready || form is null) return;
        bool resolved = await ResolvePendingAsync();
        ++_contextChoiceKey;
        if (_disposed || !_ready || !ReferenceEquals(form, _form)) return;
        if (!resolved) { RequestFocus(FocusTarget.Context); return; }
        if (!form.SelectConfigurationContext(recipe, scope))
        {
            SetStatus("ContextSelectionRejectedStatus");
            _statusCode = "deployment-context-invalid";
            RequestFocus(FocusTarget.Context);
            return;
        }
        RetireDeveloper();
        _selectedSection = null;
        SelectWorkflowStage(WorkflowStage.Configure);
        RequestFocus(FocusTarget.Context);
        SetStatus("ContextSelectionChangedStatus");
        _statusCode = "deployment-context-changed";
    }

    private string RecipeTitle(DefinitionDeploymentRecipe recipe)
    {
        int index = _form!.ConfigurationRecipes.ToList().FindIndex(item => item.Id == recipe.Id);
        return _form.LocalizedText(DefinitionPath.FromSegments(["deployment", "recipes", index.ToString(System.Globalization.CultureInfo.InvariantCulture), "title"]), recipe.Title)!;
    }

    private string ScopeTitle(DefinitionDeploymentRecipe recipe, DefinitionDeploymentScope scope)
    {
        int recipeIndex = _form!.ConfigurationRecipes.ToList().FindIndex(item => item.Id == recipe.Id);
        int scopeIndex = recipe.Scopes.ToList().FindIndex(item => item.Id == scope.Id);
        return _form.LocalizedText(DefinitionPath.FromSegments(["deployment", "recipes", recipeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), "scopes", scopeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), "title"]), scope.Title)!;
    }

    private void RefreshImportArtifacts()
    {
        ImportArtifacts = _form!.EligibleArtifacts.ToArray();
        AnswerSnapshot snapshot;
        try { snapshot = _form.Snapshot(); }
        catch (DefinitionException)
        {
            _importCapabilities = new Dictionary<string, DefinitionImportCapability>();
            return;
        }
        _importCapabilities = ImportArtifacts.ToDictionary(artifact => artifact.Id,
            artifact => DefinitionImport.GetCapability(Definition, artifact.Id, snapshot), StringComparer.Ordinal);
    }

    private ArtifactDefinition[] DeveloperArtifacts =>
        _form!.EligibleArtifacts.Where(artifact => artifact.Format == DefinitionArtifactFormat.Dotenv).ToArray();

    private string? DeveloperArtifactId
    {
        get
        {
            ArtifactDefinition[] artifacts = DeveloperArtifacts;
            return artifacts.FirstOrDefault(artifact => artifact.Id == _developerArtifactId)?.Id
                ?? artifacts.FirstOrDefault(artifact => artifact.Id == _form!.ConfigurationContext?.PrimaryArtifactId)?.Id
                ?? (artifacts.Length == 1 ? artifacts[0].Id : null);
        }
    }

    private async Task ChangeDeveloperDestinationAsync(ChangeEventArgs args)
    {
        DefinitionFormModel? form = _form;
        if (args.Value is not string id || _disposed || !_ready || form is null) return;
        bool resolved = await ResolvePendingAsync();
        ++_developerDestinationKey;
        if (_disposed || !_ready || !ReferenceEquals(form, _form)) return;
        RequestFocus(FocusTarget.DeveloperDestination);
        if (!resolved || !DeveloperArtifacts.Any(artifact => artifact.Id == id)) return;
        RetireDeveloper();
        _developerArtifactId = id;
    }
}
