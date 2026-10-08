namespace Configurator.Terminal.Tests;

using System.Text;
using Configurator.Core.Definitions;
using Configurator.Presentation;

internal static class TerminalDefinitionFixture
{
    internal const string Json = """
        {
          "schemaVersion":1,"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726",
          "revision":1,"applicationId":"neutral.service","applicationVersions":["1.0"],
          "publisher":"example","title":"Neutral service","entryPoints":["setup"],"locales":["en","ar"],
          "fields":[
            {"id":"name","title":"Name","type":"string","required":true,"sensitivity":"public","disclosure":"guided"},
            {"id":"token","title":"Token","type":"string","required":true,"sensitivity":"secret","disclosure":"advanced"},
            {"id":"enabled","title":"Enabled","type":"boolean","default":true,"sensitivity":"public","disclosure":"guided"},
            {"id":"count","title":"Count","type":"integer","default":1,"sensitivity":"public","disclosure":"guided"},
            {"id":"ratio","title":"Ratio","type":"number","default":1.5,"sensitivity":"public","disclosure":"guided"},
            {"id":"mode","title":"Mode","type":"enum","choices":["local","external"],"default":"local","sensitivity":"public","disclosure":"guided"},
            {"id":"endpoint","title":"Endpoint","type":"string","required":true,"activeWhen":{"op":"equals","field":"mode","value":"external"},"sensitivity":"public","disclosure":"guided"},
            {"id":"tags","title":"Tags","type":"list","itemType":"string","sensitivity":"public","disclosure":"guided"},
            {"id":"profile","title":"Profile","type":"object","sensitivity":"public","disclosure":"guided",
             "properties":[
               {"id":"display","title":"Display","type":"string","sensitivity":"public","disclosure":"guided"},
               {"id":"password","title":"Password","type":"string","sensitivity":"secret","disclosure":"advanced"}
             ]},
            {"id":"advanced","title":"Advanced","type":"string","sensitivity":"public","disclosure":"advanced"}
          ],
          "artifacts":[
            {"id":"config","format":"json","fileName":"config.json","applySemantics":"create","restart":"process",
             "projection":{"op":"object","members":{"name":{"op":"copy","field":"name"},"token":{"op":"copy","field":"token"}}}},
            {"id":"environment","format":"dotenv","fileName":"config.env","applySemantics":"create","restart":"process",
             "projection":{"op":"object","members":{"NAME":{"op":"copy","field":"name"},"TOKEN":{"op":"copy","field":"token"}}}},
            {"id":"yaml","format":"yaml","fileName":"config.yaml","applySemantics":"create","restart":"process",
             "projection":{"op":"object","members":{"name":{"op":"copy","field":"name"},"token":{"op":"copy","field":"token"}}}},
            {"id":"legal","format":"markdown","fileName":"legal.md","applySemantics":"create","restart":"none",
             "projection":{"op":"list","items":[{"op":"literal","value":"Operator: "},{"op":"copy","field":"token"}]}},
            {"id":"identity","format":"json","fileName":"identity.json","applySemantics":"create","restart":"none",
             "projection":{"op":"object","members":{"legalName":{"op":"copy","field":"token"}}}}
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
