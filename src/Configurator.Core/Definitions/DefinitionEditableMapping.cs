namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Dotenv;
using Configurator.Core.Environment;

public sealed record DefinitionEditableMapping(string Key, DefinitionPath Path);
