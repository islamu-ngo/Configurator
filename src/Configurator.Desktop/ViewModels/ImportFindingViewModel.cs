namespace Configurator.Desktop.ViewModels;

using CommunityToolkit.Mvvm.Input;
using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;
using Configurator.Presentation;

/// <summary>One detached, value-free imported-file issue; it never holds a proposal or workspace location.</summary>
public sealed class ImportFindingViewModel(DefinitionImportFinding finding, string locale, IRelayCommand explain)
{
    private readonly DesktopStrings _strings = new(locale);
    public string Summary => _strings.Format("ImportFindingFormat",
        PresentationStrings.ImportFinding(finding.Kind, locale), PresentationStrings.Diagnostic(finding.Code, locale));
    public bool CanExplain => finding.Schema is not null;
    public bool IsBlocking => finding.Kind is DefinitionImportFindingKind.MissingRequired or
        DefinitionImportFindingKind.InvalidSuppliedValue or DefinitionImportFindingKind.UnrecognizedInput or
        DefinitionImportFindingKind.StructuralOutputChange;
    public string ExplainLabel => _strings.Read("ExplainImportLabel");
    public IRelayCommand ExplainCommand { get; } = explain;
}

