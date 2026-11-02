namespace Configurator.Core.Definitions;

using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Configurator.Core.Composition;

public enum DefinitionNodeType { Text, Boolean, WholeNumber, Number, Mapping, Sequence }
