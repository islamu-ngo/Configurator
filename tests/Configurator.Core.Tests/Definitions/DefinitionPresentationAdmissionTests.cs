using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionPresentationAdmissionTests
{
    private const string Source = """
        {"$schema":"urn:islamu:configurator:authoring:v1",
         "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1},
         "project":{"id":"example.service","name":"Example Service"},"applicationVersions":["1.0"],
         "fields":{
           "Service port":{"type":"integer","sensitivity":"public","default":8080,"group":"network"},
           "TOKEN":{"type":"string","sensitivity":"secret","group":"access",
             "help":"Credential for the service.","consequence":"Allows service access.",
             "guidance":{"instructions":"Obtain the credential from your administrator.",
               "links":[{"title":"Public instructions","url":"https://example.org/docs/credentials"}],
               "searchTerms":["access","credential"]},
             "credential":{"kind":"opaque-token","acquisition":"issuer"}}},
         "presentation":{"defaultExperience":"recommended",
           "sections":[{"id":"access","title":"Access","disclosure":"essential","fields":["TOKEN"]},
             {"id":"network","title":"Network","groups":["network"]}],
           "experiences":[{"id":"recommended","title":"Recommended","initialDisclosure":"essential","navigation":"guided","sections":["network","access"]},
             {"id":"custom","title":"Custom","description":"Review every setting.","initialDisclosure":"advanced","navigation":"sections","sections":["access","network"]}]},
         "outputs":{"service.json":{"format":"json"}}}
        """;

    private const string Yaml = """
        $schema: urn:islamu:configurator:authoring:v1
        identity:
          familyId: 69cdb122-41d2-4fab-9b82-7929a889c726
          revision: 1
        project:
          id: example.service
          name: Example Service
        applicationVersions: ["1.0"]
        fields:
          Service port:
            type: integer
            sensitivity: public
            default: 8080
            group: network
          TOKEN:
            type: string
            sensitivity: secret
            group: access
            # This syntax comment is not runtime help.
            help: Credential for the service.
            consequence: Allows service access.
            guidance:
              instructions: Obtain the credential from your administrator.
              links:
                - title: Public instructions
                  url: https://example.org/docs/credentials
              searchTerms: [access, credential]
            credential:
              kind: opaque-token
              acquisition: issuer
        presentation:
          defaultExperience: recommended
          sections:
            - id: access
              title: Access
              disclosure: essential
              fields: [TOKEN]
            - id: network
              title: Network
              groups: [network]
          experiences:
            - id: recommended
              title: Recommended
              initialDisclosure: essential
              navigation: guided
              sections: [network, access]
            - id: custom
              title: Custom
              description: Review every setting.
              initialDisclosure: advanced
              navigation: sections
              sections: [access, network]
        outputs:
          service.json:
            format: json
        """;

    private static SetupDefinition Compile(JsonObject source) =>
        DefinitionAuthoringTests.Compile(source.ToJsonString());

    private static JsonObject Authoring() => JsonNode.Parse(Source)!.AsObject();
    private static JsonObject Runtime() => JsonNode.Parse(Compile(Authoring()).CanonicalBytes.Span)!.AsObject();
    private static SetupDefinition Parse(JsonObject runtime) => DefinitionFixture.Parse(runtime.ToJsonString());

    [Test]
    public async Task EquivalentYamlJsonCompilesGroupsAndLogicalReferencesInAuthoredOrder()
    {
        SetupDefinition json = Compile(Authoring());
        SetupDefinition yaml = DefinitionAuthoringCompiler.Compile(
            DefinitionAuthoringCodec.ParseYaml(Encoding.UTF8.GetBytes(Yaml)));
        await Assert.That(json.Digest).IsEqualTo(yaml.Digest);
        JsonNode runtime = JsonNode.Parse(json.CanonicalBytes.Span)!;
        await Assert.That(runtime["presentation"]!["sections"]![0]!["fields"]![0]!.GetValue<string>()).IsEqualTo("TOKEN");
        await Assert.That(runtime["presentation"]!["sections"]![1]!["fields"]![0]!.GetValue<string>())
            .IsEqualTo(DefinitionAuthoringCompiler.FieldId("Service port"));
        await Assert.That(runtime["presentation"]!["sections"]![1]!["groups"] is null).IsTrue();
        await Assert.That(runtime["presentation"]!["experiences"]![0]!["sections"]!.ToJsonString())
            .IsEqualTo("""["network","access"]""");
        await Assert.That(SetupDefinitionCodec.ParseYaml(json.CanonicalBytes).Digest).IsEqualTo(json.Digest);
        await Assert.That(json.Fields.Single(field => field.Id == "TOKEN").Help).IsEqualTo("Credential for the service.");
    }

    [Test]
    public async Task OptionalMachineOnlyMetadataAbsenceRemainsNeutral()
    {
        SetupDefinition definition = DefinitionAuthoringTests.Compile();
        JsonObject runtime = JsonNode.Parse(definition.CanonicalBytes.Span)!.AsObject();
        await Assert.That(runtime.ContainsKey("presentation")).IsFalse();
        await Assert.That(runtime["fields"]![1]!["guidance"] is null).IsTrue();
        await Assert.That(runtime["fields"]![1]!["credential"] is null).IsTrue();
        await Assert.That(definition.Presentation is null).IsTrue();
    }

    [Test]
    public async Task PublicSnapshotsPreserveAuthoredSemanticsAndNeutralDefaults()
    {
        JsonObject source = Authoring();
        SetupDefinition definition = Compile(source);
        DefinitionPresentation presentation = definition.Presentation!;
        source["presentation"]!["sections"]![0]!["fields"]!.AsArray().Clear();
        byte[] bytes = definition.CanonicalBytes.ToArray();
        bytes.AsSpan().Clear();
        await Assert.That(presentation.DefaultExperience).IsEqualTo("recommended");
        await Assert.That(presentation.Sections[0].Disclosure).IsEqualTo(SetupFieldDisclosure.Essential);
        await Assert.That(presentation.Sections[1].Disclosure).IsEqualTo(SetupFieldDisclosure.Guided);
        await Assert.That(presentation.Experiences[0].InitialDisclosure).IsEqualTo(SetupFieldDisclosure.Essential);
        await Assert.That(presentation.Experiences[0].Navigation).IsEqualTo("guided");
        await Assert.That(presentation.Experiences[1].InitialDisclosure).IsEqualTo(SetupFieldDisclosure.Advanced);
        await Assert.That(presentation.Experiences[1].Navigation).IsEqualTo("sections");
        await Assert.That(() => ((IList<string>)presentation.Sections[0].Fields).Clear()).Throws<NotSupportedException>();
        await Assert.That(() => ((IList<DefinitionSection>)presentation.Sections).Clear()).Throws<NotSupportedException>();
        await Assert.That(() => ((IList<DefinitionExperience>)presentation.Experiences).Clear()).Throws<NotSupportedException>();
        SetupFieldDefinition field = definition.Fields.Single(field => field.Id == "TOKEN");
        await Assert.That(field.InputPurpose!.Kind).IsEqualTo("opaque-token");
        await Assert.That(field.InputPurpose.Acquisition).IsEqualTo("issuer");
        await Assert.That(field.Guidance!.Links[0].Url).IsEqualTo("https://example.org/docs/credentials");
        await Assert.That(() => ((IList<DefinitionGuidanceLink>)field.Guidance.Links).Clear()).Throws<NotSupportedException>();
        await Assert.That(() => ((IList<string>)field.Guidance.SearchTerms).Clear()).Throws<NotSupportedException>();
        source = Authoring();
        source["presentation"]!["experiences"]![0]!.AsObject().Remove("initialDisclosure");
        source["presentation"]!["experiences"]![0]!.AsObject().Remove("navigation");
        DefinitionExperience defaults = Compile(source).Presentation!.Experiences[0];
        await Assert.That(defaults.InitialDisclosure).IsEqualTo(SetupFieldDisclosure.Guided);
        await Assert.That(defaults.Navigation).IsEqualTo("sections");
    }

    [Test]
    public async Task SectionsRequireExactRootPartitionAndExperiencesRequireEverySectionOnce()
    {
        _ = Runtime();
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            root => root["presentation"]!["sections"]!.AsArray().RemoveAt(1),
            root => root["presentation"]!["sections"]![0]!["fields"] = new JsonArray(),
            root => root["presentation"]!["sections"]![0]!["fields"] = new JsonArray("missing"),
            root => root["presentation"]!["sections"]![0]!["fields"]!.AsArray().Add("TOKEN"),
            root => root["presentation"]!["sections"]![1]!["fields"]!.AsArray().Add("TOKEN"),
            root => root["presentation"]!["experiences"]![0]!["sections"] = new JsonArray("access"),
            root => root["presentation"]!["experiences"]![0]!["sections"] = new JsonArray("access", "access"),
            root => root["presentation"]!["experiences"]![0]!["sections"] = new JsonArray("access", "missing"),
            root => root["presentation"]!["defaultExperience"] = "missing",
            root => root["presentation"]!.AsObject().Remove("defaultExperience"),
            root => root["presentation"]!["sections"]![1]!["id"] = "access",
            root => root["presentation"]!["experiences"]![1]!["id"] = "recommended",
            root => root["presentation"]!["sections"]![0]!["id"] = "Invalid_Id",
            root => root["presentation"]!["sections"]![0]!["id"] = "two--hyphens",
            root => root["presentation"]!["experiences"]![0]!["id"] = "ends-",
            root => root["presentation"]!["experiences"]![0]!["initialDisclosure"] = "all",
            root => root["presentation"]!["experiences"]![0]!["navigation"] = "wizard",
            root => root["presentation"]!["sections"]![0]!["disclosure"] = "hidden",
            root => root["presentation"]!["experiences"]![0]!["answers"] = new JsonObject(),
            root => root["presentation"]!["sections"]![0]!["groups"] = new JsonArray("access"),
            root => root["presentation"]!["command"] = "synthetic-private-marker"
        })
        {
            JsonObject runtime = Runtime();
            mutate(runtime);
            await Reject(() => Parse(runtime));
        }
    }

    [Test]
    public async Task AuthoringRejectsMissingEmptyAndOverlappingGroupSelectors()
    {
        _ = Compile(Authoring());
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            root => root["presentation"]!["sections"]![1]!["groups"] = new JsonArray("missing"),
            root => root["presentation"]!["sections"]![1]!["groups"] = new JsonArray(),
            root => root["presentation"]!["sections"]![1]!["groups"] = new JsonArray("network", "network"),
            root => root["presentation"]!["sections"]![1]!["groups"] = new JsonArray("access"),
            root => root["presentation"]!["sections"]![1]!["fields"] = new JsonArray("Service port"),
            root => root["presentation"]!["sections"]![0]!["fields"] = new JsonArray("TOKEN/child"),
            root => root["presentation"]!["sections"]![1]!["fields"] = new JsonArray()
        })
        {
            JsonObject source = Authoring();
            mutate(source);
            await Reject(() => Compile(source));
        }
    }

    [Test]
    public async Task GroupExpansionUsesStableRootOrderAndKeepsNestedPropertiesInsideRoot()
    {
        JsonObject source = Authoring();
        source["fields"]!["ANOTHER"] = JsonNode.Parse("""{"type":"object","group":"network","properties":{"child":{"type":"string"}}}""");
        JsonNode runtime = JsonNode.Parse(Compile(source).CanonicalBytes.Span)!;
        JsonArray roots = runtime["presentation"]!["sections"]![1]!["fields"]!.AsArray();
        await Assert.That(roots.Count).IsEqualTo(2);
        await Assert.That(roots[0]!.GetValue<string>()).IsEqualTo("ANOTHER");
        await Assert.That(roots[1]!.GetValue<string>()).IsEqualTo(DefinitionAuthoringCompiler.FieldId("Service port"));
    }

    [Test]
    public async Task PresentationBoundsAcceptTheirEdgesAndRejectOverflow()
    {
        _ = Runtime();
        foreach ((string member, int maximum) in new[] { ("id", 64), ("title", 128), ("description", 2048) })
        {
            JsonObject runtime = Runtime();
            runtime["presentation"]!["experiences"]![1]![member] = new string('x', maximum);
            _ = Parse(runtime);
            runtime["presentation"]!["experiences"]![1]![member] = new string('x', maximum + 1);
            await Reject(() => Parse(runtime));
            runtime = Runtime();
            runtime["presentation"]!["sections"]![0]![member] = new string('x', maximum + 1);
            await Reject(() => Parse(runtime));
        }
        foreach (string collection in new[] { "experiences", "sections" })
        {
            JsonObject runtime = Runtime();
            runtime["presentation"]![collection] = new JsonArray();
            await Reject(() => Parse(runtime));
            int maximum = collection == "experiences" ? 16 : 64;
            for (int index = 0; index <= maximum; index++)
                runtime["presentation"]![collection]!.AsArray().Add(JsonNode.Parse(
                    $$"""{"id":"entry-{{index}}","title":"Entry","sections":["access","network"],"fields":["TOKEN"]}"""));
            await Reject(() => Parse(runtime));
        }
    }

    [Test]
    public async Task MaximumSectionAndExperienceCountsAdmitACompletePartition()
    {
        JsonObject runtime = Runtime();
        runtime["fields"] = new JsonArray();
        var sections = new JsonArray();
        var sectionIds = new JsonArray();
        for (int index = 0; index < 64; index++)
        {
            string suffix = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            runtime["fields"]!.AsArray().Add(new JsonObject
            {
                ["id"] = "root-" + suffix, ["type"] = "string", ["title"] = "Root",
                ["sensitivity"] = "public", ["disclosure"] = "guided"
            });
            sections.Add(new JsonObject
            {
                ["id"] = "section-" + suffix, ["title"] = "Section", ["fields"] = new JsonArray("root-" + suffix)
            });
            sectionIds.Add("section-" + suffix);
        }
        var experiences = new JsonArray();
        for (int index = 0; index < 16; index++)
            experiences.Add(new JsonObject
            {
                ["id"] = "experience-" + index, ["title"] = "Experience", ["sections"] = sectionIds.DeepClone()
            });
        runtime["presentation"] = new JsonObject
        {
            ["defaultExperience"] = "experience-0", ["sections"] = sections, ["experiences"] = experiences
        };
        runtime["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        SetupDefinition admitted = Parse(runtime);
        await Assert.That(admitted.Presentation!.Sections.Count).IsEqualTo(64);
        await Assert.That(admitted.Presentation.Experiences.Count).IsEqualTo(16);
        experiences.Add(new JsonObject
        {
            ["id"] = "overflow", ["title"] = "Overflow", ["sections"] = sectionIds.DeepClone()
        });
        await Reject(() => Parse(runtime));
        experiences.RemoveAt(16);
        sections.Add(new JsonObject { ["id"] = "overflow", ["title"] = "Overflow", ["fields"] = new JsonArray("root-0") });
        await Reject(() => Parse(runtime));
    }

    [Test]
    public async Task GuidanceBoundsAndClosedLinksRejectActiveOrUnboundedMetadata()
    {
        _ = Runtime();
        int mutationIndex = 0;
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            field => field["guidance"]!["instructions"] = new string('x', 4097),
            field => field["guidance"]!["instructions"] = "",
            field => field["guidance"]!["instructions"] = "<script>synthetic-private-marker</script>",
            field => field["guidance"]!["instructions"] = "line\nbreak",
            field => field["guidance"]!["instructions"] = "${answer}",
            field => field["guidance"]!["instructions"] = "[Run](https://example.org)",
            field => field["guidance"]!["instructions"] = "`execute synthetic-command`",
            field => field["guidance"]!["exec"] = "synthetic-private-marker",
            field => field["guidance"]!["searchTerms"] = new JsonArray("duplicate", "duplicate"),
            field => field["guidance"]!["searchTerms"] = new JsonArray(new string('x', 65)),
            field => field["guidance"]!["searchTerms"] = new JsonArray(""),
            field => field["guidance"]!["searchTerms"] = new JsonArray(Enumerable.Range(0, 17).Select(index => (JsonNode?)JsonValue.Create("term" + index)).ToArray()),
            field => field["guidance"]!["links"]![0]!["title"] = new string('x', 129),
            field => field["guidance"]!["links"]![0]!["url"] = "https://example.org/" + new string('x', 2048),
            field => field["guidance"]!["links"]![0]!["onclick"] = "synthetic-private-marker",
            field => field["guidance"]!["links"] = new JsonArray(Enumerable.Range(0, 5).Select(_ => JsonNode.Parse("""{"title":"Docs","url":"https://example.org"}""")).ToArray())
        })
        {
            JsonObject runtime = Runtime();
            mutate(runtime["fields"]!.AsArray().Single(field => field!["id"]!.GetValue<string>() == "TOKEN")!.AsObject());
            await Reject(() => Parse(runtime), $"guidance-case-{mutationIndex++}");
        }
        foreach (string url in new[] { "http://example.org", "/docs", "javascript:alert(1)", "https://user:pass@example.org",
            "https://example.org?q=value", "https://example.org#fragment", "https://example.org/?", "https://example.org/#",
            "https://example.org/${answer}", "https://example.org/{{answer}}" })
        {
            JsonObject runtime = Runtime();
            runtime["fields"]![1]!["guidance"]!["links"]![0]!["url"] = url;
            await Reject(() => Parse(runtime), $"link-case-{mutationIndex++}");
        }
    }

    [Test]
    public async Task GuidanceMaximumInstructionsLinksAndSearchTermsRemainUsable()
    {
        JsonObject source = Authoring();
        JsonNode guidance = source["fields"]!["TOKEN"]!["guidance"]!;
        guidance["instructions"] = new string('x', 4096);
        guidance["searchTerms"] = new JsonArray(Enumerable.Range(0, 16)
            .Select(index => (JsonNode?)JsonValue.Create(index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + new string('x', 62))).ToArray());
        guidance["links"] = new JsonArray(Enumerable.Range(0, 4)
            .Select(_ => JsonNode.Parse("""{"title":"Docs","url":"https://example.org/docs"}""")).ToArray());
        _ = Compile(source);
        source["fields"]!["TOKEN"]!["guidance"] = new JsonObject();
        _ = Compile(source);
        await Assert.That(Parse(Runtime()).Fields.Count).IsEqualTo(2);
    }

    [Test]
    public async Task MetadataNullsAndUnknownMembersRejectThroughTheRuntimeBoundary()
    {
        _ = Runtime();
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            root => root["presentation"] = null,
            root => root["presentation"]!["sections"]![0]!["fields"] = null,
            root => root["presentation"]!["experiences"]![0]!["initialDisclosure"] = null,
            root => root["presentation"]!["experiences"]![0]!["navigation"] = null,
            root => root["fields"]![1]!["guidance"] = null,
            root => root["fields"]![1]!["credential"] = null,
            root => root["fields"]![1]!["guidance"]!["links"] = null,
            root => root["fields"]![1]!["guidance"]!["searchTerms"] = null,
            root => root["fields"]![1]!["credential"]!["kind"] = null,
            root => root["fields"]![1]!["credential"]!["generation"] = new JsonObject(),
            root => root["fields"]![1]!["comment"] = "synthetic-private-marker"
        })
        {
            JsonObject runtime = Runtime();
            mutate(runtime);
            await Reject(() => Parse(runtime));
        }
    }

    [Test]
    public async Task CredentialVocabularyAndAcquisitionAreClosedAndNeverImplyGeneration()
    {
        _ = Runtime();
        foreach (string kind in new[] { "password", "opaque-token", "symmetric-key", "private-key", "connection-string" })
            foreach (string acquisition in new[] { "existing", "create", "issuer" })
            {
                JsonObject source = Authoring();
                source["fields"]!["TOKEN"]!["credential"] = new JsonObject { ["kind"] = kind, ["acquisition"] = acquisition };
                bool allowed = acquisition == "existing" || acquisition == "create" && kind is "password" or "symmetric-key"
                    || acquisition == "issuer" && kind != "symmetric-key";
                if (allowed) _ = Compile(source);
                else await Reject(() => Compile(source));
            }
        foreach (string credential in new[] { """{"kind":"certificate","acquisition":"existing"}""",
            """{"kind":"password","acquisition":"generate"}""", """{"kind":"password"}""",
            """{"acquisition":"existing"}""", """{"kind":"password","acquisition":"create","generation":{}}""",
            """{"kind":"password","acquisition":"existing","autocomplete":"current-password"}""" })
        {
            JsonObject source = Authoring();
            source["fields"]!["TOKEN"]!["credential"] = JsonNode.Parse(credential);
            await Reject(() => Compile(source));
        }
    }

    [Test]
    public async Task CredentialRequiresAnEffectivelySecretStringLeafIncludingInheritedContext()
    {
        _ = Runtime();
        foreach (string sensitivity in new[] { "public", "sensitive" })
        {
            JsonObject source = Authoring();
            source["fields"]!["TOKEN"]!["sensitivity"] = sensitivity;
            await Reject(() => Compile(source));
        }
        foreach (string type in new[] { "integer", "boolean", "enum", "object", "list" })
        {
            JsonObject source = Authoring();
            source["fields"]!["TOKEN"]!["type"] = type;
            if (type == "enum") source["fields"]!["TOKEN"]!["choices"] = new JsonArray("one");
            if (type is "object" or "list") source["fields"]!["TOKEN"]!["properties"] = new JsonObject { ["child"] = new JsonObject { ["type"] = "string" } };
            if (type == "list") source["fields"]!["TOKEN"]!["itemType"] = "object";
            await Reject(() => Compile(source));
        }
        foreach (string type in new[] { "object", "list" })
        {
            JsonObject source = Authoring();
            source["fields"]!["TOKEN"] = JsonNode.Parse("""
                {"type":"object","sensitivity":"secret","properties":{
                  "child":{"type":"string","sensitivity":"public",
                    "credential":{"kind":"password","acquisition":"existing"}}}}
                """);
            source["fields"]!["TOKEN"]!["type"] = type;
            if (type == "list") source["fields"]!["TOKEN"]!["itemType"] = "object";
            SetupDefinition definition = Compile(source);
            await Assert.That(definition.Fields.Single(field => field.Id == "TOKEN").Sensitivity).IsEqualTo(SetupFieldSensitivity.Secret);
            source["fields"]!["TOKEN"]!["sensitivity"] = "public";
            await Reject(() => Compile(source));
        }
    }

    [Test]
    public async Task DescriptorsCannotDemoteSensitivityOrAdmitPrivateDefaults()
    {
        _ = Runtime();
        foreach (string member in new[] { "default", "recommended" })
        {
            JsonObject source = Authoring();
            source["fields"]!["TOKEN"]![member] = "synthetic-private-marker";
            await Reject(() => Compile(source));
        }
        JsonObject inherited = Authoring();
        inherited["fields"]!["TOKEN"] = JsonNode.Parse("""
            {"type":"object","sensitivity":"secret","properties":{"child":{"type":"string","sensitivity":"public",
             "default":"synthetic-private-marker","credential":{"kind":"password","acquisition":"existing"}}}}
            """);
        await Reject(() => Compile(inherited));
    }

    [Test]
    public async Task AnyAdmittedMetadataChangeBindsDigestAndConflictsAtTheSameRevision()
    {
        SetupDefinition original = Compile(Authoring());
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            source => source["presentation"]!["experiences"]![0]!["title"] = "A different title",
            source => source["fields"]!["TOKEN"]!["guidance"]!["instructions"] = "Different public guidance.",
            source => source["fields"]!["TOKEN"]!["credential"]!["acquisition"] = "existing"
        })
        {
            JsonObject source = Authoring();
            mutate(source);
            SetupDefinition changed = Compile(source);
            await Assert.That(changed.Digest == original.Digest).IsFalse();
            await Reject(() => _ = new SetupDefinitionRegistry([original, changed]));
            source["identity"]!["revision"] = 2;
            SetupDefinition next = Compile(source);
            var registry = new SetupDefinitionRegistry([original, next]);
            await Assert.That(registry.Resolve(original.FamilyId, "1.0", original.Digest).Digest).IsEqualTo(original.Digest);
        }
    }

    private static async Task Reject(Action action, string? scenario = null)
    {
        try
        {
            action();
        }
        catch (SetupDefinitionException exception)
        {
            await Assert.That(exception.Code.Length <= 64).IsTrue();
            await Assert.That(exception.ToString().Contains("synthetic-private-marker", StringComparison.Ordinal)).IsFalse();
            return;
        }
        throw new InvalidOperationException($"Expected bounded admission rejection: {scenario ?? "metadata"}.");
    }
}
