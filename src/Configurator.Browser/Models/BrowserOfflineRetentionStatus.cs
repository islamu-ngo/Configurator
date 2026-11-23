namespace Configurator.Browser.Models;

/// <summary>Outcome of explicitly retaining an admitted public definition for offline reopening.</summary>
public enum BrowserOfflineRetentionStatus
{
    Retained,
    CapacityReached,
    StorageUnavailable,
    BusyInWorkspace,
    InvalidDefinition
}
