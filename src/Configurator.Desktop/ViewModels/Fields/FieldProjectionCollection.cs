namespace Configurator.Desktop.ViewModels.Fields;

using System.Collections.ObjectModel;

/// <summary>Updates a mounted slice without replacing surviving native editor containers.</summary>
internal static class FieldProjectionCollection
{
    internal static void Synchronize(ObservableCollection<FieldViewModel> current, FieldViewModel[] requested)
    {
        for (int index = current.Count - 1; index >= 0; index--)
            if (!requested.Contains(current[index])) current.RemoveAt(index);
        for (int index = 0; index < requested.Length; index++)
        {
            FieldViewModel field = requested[index];
            int previous = current.IndexOf(field);
            if (previous < 0) current.Insert(index, field);
            else if (previous != index) current.Move(previous, index);
        }
    }
}
