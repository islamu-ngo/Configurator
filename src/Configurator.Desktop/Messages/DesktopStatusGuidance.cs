namespace Configurator.Desktop.Messages;

/// <summary>User correction category, independent of diagnostic text and identifiers.</summary>
public enum DesktopStatusGuidance
{
    General, DefinitionRequired, DefinitionInvalid, ValidationInvalid, PreparationInvalid,
    SelectionUnavailable, SelectionNonlocal, ImportUnreadable, ImportUnsupported,
    AnswerConflict, InputInvalid, SaveFailed, SaveInstalledUnverified, SaveInstalledVerified,
    BundleSavePartial, Cancelled, InputsChanged
}
