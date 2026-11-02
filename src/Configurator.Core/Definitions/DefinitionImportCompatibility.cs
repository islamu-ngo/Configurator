namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

public enum DefinitionImportCompatibility { Compatible, ChangesRequired, Incomplete, Unsupported, InvalidInput }
