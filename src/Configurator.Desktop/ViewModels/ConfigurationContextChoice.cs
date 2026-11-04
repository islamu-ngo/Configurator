namespace Configurator.Desktop.ViewModels;

/// <summary>A declared compatible pair; selection is accepted atomically by Presentation.</summary>
public sealed record ConfigurationContextChoice(string RecipeId, string ScopeId, string Title);
