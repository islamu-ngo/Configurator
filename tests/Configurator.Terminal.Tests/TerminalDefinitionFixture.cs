namespace Configurator.Terminal.Tests;

using System.Text;
using Configurator.Core.Definitions;
using Configurator.Presentation;

internal static class TerminalDefinitionFixture
{
    internal const string Json = """
        {
          "schemaVersion":2,"profile":"bounded-v2","familyId":"69cdb122-41d2-4fab-9b82-7929a889c726",
          "revision":1,"project":{"id":"neutral.service","name":"Neutral service","publisher":"example"},
          "applicationVersions":["1.0"],"entryPoints":["setup"],"locales":["ar","en"],
          "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl"}}},
          "requiredCapabilities":["predicate.root","projection.copy","projection.list","projection.literal","projection.object",
            "relationship.presence","schema.array","schema.boolean","schema.enum","schema.integer","schema.number",
            "schema.object","schema.string","values.initial"],
          "configuration":{"type":"object","additionalProperties":false,"required":["name","token"],
            "x-configurator":{"sensitivity":"public"},
            "if":{"required":["mode"],"properties":{"mode":{"const":"external"}}},"then":{"required":["endpoint"]},
            "properties":{
              "name":{"title":"Name","type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}},
              "token":{"title":"Token","type":"string","x-configurator":{"sensitivity":"secret","detail":"advanced"}},
              "enabled":{"title":"Enabled","type":"boolean","x-configurator":{"initialValue":true,"sensitivity":"public","detail":"basic"}},
              "count":{"title":"Count","type":"integer","x-configurator":{"initialValue":1,"sensitivity":"public","detail":"basic"}},
              "ratio":{"title":"Ratio","type":"number","x-configurator":{"initialValue":1.5,"sensitivity":"public","detail":"basic"}},
              "mode":{"title":"Mode","type":"string","enum":["local","external"],
                "x-configurator":{"initialValue":"local","sensitivity":"public","detail":"basic"}},
              "endpoint":{"title":"Endpoint","type":"string","x-configurator":{"sensitivity":"public","detail":"basic",
                "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/mode"},"value":"external"}}},
              "tags":{"title":"Tags","type":"array","items":{"type":"string","x-configurator":{"sensitivity":"public"}},"x-configurator":{"sensitivity":"public","detail":"basic"}},
              "profile":{"title":"Profile","type":"object","additionalProperties":false,
                "x-configurator":{"sensitivity":"public","detail":"basic"},"properties":{
                  "display":{"title":"Display","type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}},
                  "password":{"title":"Password","type":"string","x-configurator":{"sensitivity":"secret","detail":"advanced"}}}},
              "advanced":{"title":"Advanced","type":"string","x-configurator":{"sensitivity":"public","detail":"advanced"}}
            }},
          "presentation":{"defaultJourney":"guided","navigation":"sections",
            "sections":[{"id":"settings","title":"Settings",
              "pointers":["/name","/token","/enabled","/count","/ratio","/mode","/endpoint","/tags","/profile","/advanced"]}],
            "journeys":[{"id":"guided","title":"Guided","sections":["settings"]}]},
          "artifacts":[
            {"id":"config","format":"json","fileName":"config.json","targetUpdateMode":"create","restartRequirement":"process",
             "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/name"}},"token":{"op":"copy","reference":{"scope":"root","path":"/token"}}}}},
            {"id":"environment","format":"dotenv","fileName":"config.env","targetUpdateMode":"create","restartRequirement":"process",
             "projection":{"op":"object","members":{"NAME":{"op":"copy","reference":{"scope":"root","path":"/name"}},"TOKEN":{"op":"copy","reference":{"scope":"root","path":"/token"}}}}},
            {"id":"yaml","format":"yaml","fileName":"config.yaml","targetUpdateMode":"create","restartRequirement":"process",
             "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/name"}},"token":{"op":"copy","reference":{"scope":"root","path":"/token"}}}}},
            {"id":"legal","format":"markdown","fileName":"legal.md","targetUpdateMode":"create","restartRequirement":"none",
             "projection":{"op":"list","items":[{"op":"literal","value":"Operator: "},{"op":"copy","reference":{"scope":"root","path":"/token"}}]}},
            {"id":"identity","format":"json","fileName":"identity.json","targetUpdateMode":"create","restartRequirement":"none",
             "projection":{"op":"object","members":{"legalName":{"op":"copy","reference":{"scope":"root","path":"/token"}}}}}
          ]
        }
        """;

    internal static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(Json));
    internal static DefinitionFormModel Form(string token = "privateValue")
    {
        var form = new DefinitionFormModel(Definition());
        form.Fields.Single(input => input.Definition.Id == "name").Text = "sample";
        form.Fields.Single(input => input.Definition.Id == "token").Text = token;
        return form;
    }
}
