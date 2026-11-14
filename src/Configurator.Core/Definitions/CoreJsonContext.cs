namespace Configurator.Core.Definitions;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(string))]
internal partial class CoreJsonContext : JsonSerializerContext
{
}
