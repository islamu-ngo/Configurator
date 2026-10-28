using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests.Definitions;

public sealed class DefinitionPresentationAdmissionTests
{
    private const string Source = """
        {"$schema":"urn:islamu:configurator:authoring:v2",
         "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1},
         "project":{"id":"example.service","name":"Example Service"},"applicationVersions":["1.0"],
         "configuration":{"type":"object","additionalProperties":false,"required":["TOKEN"],
          "x-configurator":{"sensitivity":"public"},"properties":{
           "Service port":{"type":"integer","x-configurator":{"sensitivity":"public","initialValue":8080}},
           "TOKEN":{"type":"string","description":"Credential for the service.",
             "x-configurator":{"sensitivity":"secret","detail":"basic","notBlank":true,
               "guidance":{"instructions":"Obtain the credential from your administrator.",
                 "links":[{"title":"Public instructions","url":"https://example.org/docs/credentials"}],
                 "searchTerms":["access","credential"]},
               "credential":{"kind":"opaque-token","acquisition":"issuer"}}}}},
         "presentation":{"defaultJourney":"recommended","navigation":"wizard",
           "sections":[{"id":"access","title":"Access","pointers":["/TOKEN"]},
             {"id":"network","title":"Network","pointers":["/Service port"]}],
           "journeys":[{"id":"recommended","title":"Recommended","sections":["network","access"]},
             {"id":"custom","title":"Custom","description":"Review every setting.","sections":["access","network"]}]},
         "outputs":{"service.json":{"format":"json"}}}
        """;

    private const string Yaml = """
        $schema: urn:islamu:configurator:authoring:v2
        identity:
          familyId: 69cdb122-41d2-4fab-9b82-7929a889c726
          revision: 1
        project:
          id: example.service
          name: Example Service
        applicationVersions: ["1.0"]
        configuration:
          type: object
          additionalProperties: false
          required: [TOKEN]
          x-configurator: {sensitivity: public}
          properties:
            Service port:
              type: integer
              x-configurator: {sensitivity: public, initialValue: 8080}
            TOKEN:
              type: string
              # This syntax comment is not runtime help.
              description: Credential for the service.
              x-configurator:
                sensitivity: secret
                detail: basic
                notBlank: true
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
          defaultJourney: recommended
          navigation: wizard
          sections:
            - id: access
              title: Access
              pointers: [/TOKEN]
            - id: network
              title: Network
              pointers: ["/Service port"]
          journeys:
            - id: recommended
              title: Recommended
              sections: [network, access]
            - id: custom
              title: Custom
              description: Review every setting.
              sections: [access, network]
        outputs:
          service.json:
            format: json
        """;

    private static SetupDefinition Compile(JsonObject source) =>
        DefinitionAuthoringTests.Compile(source.ToJsonString());

    private static JsonObject Authoring() => JsonNode.Parse(Source)!.AsObject();
    private static JsonObject Runtime() => JsonNode.Parse(Compile(Authoring()).CanonicalBytes.Span)!.AsObject();
    private static SetupDefinition Parse(JsonObject runtime) =>
        SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString()));
    private static JsonObject Token(JsonObject document) =>
        document["configuration"]!["properties"]!["TOKEN"]!.AsObject();
    private static JsonObject Metadata(JsonObject document) => Token(document)["x-configurator"]!.AsObject();

    [Test]
    public async Task EquivalentYamlJsonCompilesPointersAndJourneysInAuthoredOrder()
    {
        SetupDefinition json = Compile(Authoring());
        SetupDefinition yaml = DefinitionAuthoringCompiler.Compile(
            DefinitionAuthoringCodec.ParseYaml(Encoding.UTF8.GetBytes(Yaml)));
        await Assert.That(json.Digest).IsEqualTo(yaml.Digest);
        JsonNode runtime = JsonNode.Parse(json.CanonicalBytes.Span)!;
        await Assert.That(runtime["presentation"]!["sections"]![0]!["pointers"]![0]!.GetValue<string>()).IsEqualTo("/TOKEN");
        await Assert.That(runtime["presentation"]!["sections"]![1]!["pointers"]![0]!.GetValue<string>()).IsEqualTo("/Service port");
        await Assert.That(runtime["presentation"]!["journeys"]![0]!["sections"]!.ToJsonString())
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
        JsonNode metadata = runtime["configuration"]!["properties"]!["SERVICE_TOKEN"]!["x-configurator"]!;
        await Assert.That(metadata["guidance"] is null).IsTrue();
        await Assert.That(metadata["credential"] is null).IsTrue();
        await Assert.That(definition.Presentation is null).IsTrue();
    }

    [Test]
    public async Task PublicSnapshotsPreserveAuthoredSemanticsAndNeutralDefaults()
    {
        JsonObject source = Authoring();
        SetupDefinition definition = Compile(source);
        DefinitionPresentation presentation = definition.Presentation!;
        source["presentation"]!["sections"]![0]!["pointers"]!.AsArray().Clear();
        byte[] bytes = definition.CanonicalBytes.ToArray();
        bytes.AsSpan().Clear();
        await Assert.That(presentation.DefaultExperience).IsEqualTo("recommended");
        await Assert.That(presentation.Sections[0].Pointers.SequenceEqual(["/TOKEN"])).IsTrue();
        await Assert.That(presentation.Sections[1].Pointers.SequenceEqual(["/Service port"])).IsTrue();
        await Assert.That(presentation.Experiences[0].Navigation).IsEqualTo("wizard");
        await Assert.That(presentation.Experiences[1].Navigation).IsEqualTo("wizard");
        await Assert.That(() => ((IList<string>)presentation.Sections[0].Fields).Clear()).Throws<NotSupportedException>();
        await Assert.That(() => ((IList<DefinitionSection>)presentation.Sections).Clear()).Throws<NotSupportedException>();
        await Assert.That(() => ((IList<DefinitionExperience>)presentation.Experiences).Clear()).Throws<NotSupportedException>();
        SetupFieldDefinition field = definition.Fields.Single(field => field.Id == "TOKEN");
        await Assert.That(field.Disclosure).IsEqualTo(SetupFieldDisclosure.Essential);
        await Assert.That(field.InputPurpose!.Kind).IsEqualTo("opaque-token");
        await Assert.That(field.InputPurpose.Acquisition).IsEqualTo("issuer");
        await Assert.That(field.Guidance!.Links[0].Url).IsEqualTo("https://example.org/docs/credentials");
        await Assert.That(() => ((IList<DefinitionGuidanceLink>)field.Guidance.Links).Clear()).Throws<NotSupportedException>();
        await Assert.That(() => ((IList<string>)field.Guidance.SearchTerms).Clear()).Throws<NotSupportedException>();
        source = Authoring();
        source["presentation"]!.AsObject().Remove("navigation");
        Metadata(source).Remove("detail");
        SetupDefinition defaults = Compile(source);
        await Assert.That(defaults.Presentation!.Experiences[0].Navigation).IsEqualTo("sections");
        await Assert.That(defaults.Fields.Single(value => value.Id == "TOKEN").Disclosure).IsEqualTo(SetupFieldDisclosure.Guided);
    }

    [Test]
    public async Task SectionsAndJourneysRejectInvalidReferencesAndClosedMetadata()
    {
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            root => root["presentation"]!["sections"]![0]!["pointers"] = new JsonArray("/missing"),
            root => root["presentation"]!["sections"]![0]!["pointers"]!.AsArray().Add("/TOKEN"),
            root => root["presentation"]!["journeys"]![0]!["sections"] = new JsonArray("access", "access"),
            root => root["presentation"]!["journeys"]![0]!["sections"] = new JsonArray("access", "missing"),
            root => root["presentation"]!["defaultJourney"] = "missing",
            root => root["presentation"]!.AsObject().Remove("defaultJourney"),
            root => root["presentation"]!["sections"]![1]!["id"] = "access",
            root => root["presentation"]!["journeys"]![1]!["id"] = "recommended",
            root => root["presentation"]!["navigation"] = "guided",
            root => root["presentation"]!["journeys"]![0]!["initialDisclosure"] = "essential",
            root => root["presentation"]!["journeys"]![0]!["answers"] = new JsonObject(),
            root => root["presentation"]!["sections"]![0]!["disclosure"] = "hidden",
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
    public async Task AuthoringRejectsLegacyGroupAndLogicalFieldSelectors()
    {
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            root => root["presentation"]!["sections"]![1]!["groups"] = new JsonArray("network"),
            root => root["presentation"]!["sections"]![1]!["fields"] = new JsonArray("Service port"),
            root => root["presentation"]!["sections"]![0]!["pointers"] = new JsonArray("TOKEN/child"),
            root => root["presentation"]!["sections"]![0]!["pointers"] = new JsonArray("/TOKEN/child"),
            root => root["presentation"]!["defaultExperience"] = "recommended",
            root => Metadata(root)["group"] = "access",
            root => Metadata(root)["consequence"] = "synthetic-private-marker"
        })
        {
            JsonObject source = Authoring();
            mutate(source);
            await Reject(() => Compile(source));
        }
    }

    [Test]
    public async Task ExplicitPointersKeepNestedPropertiesInsideTheirDeclaredRoot()
    {
        JsonObject source = Authoring();
        source["configuration"]!["properties"]!["ANOTHER"] = JsonNode.Parse(
            """{"type":"object","additionalProperties":false,"properties":{"child":{"type":"string"}}}""");
        source["presentation"]!["sections"]![1]!["pointers"] = new JsonArray("/ANOTHER", "/Service port");
        JsonNode runtime = JsonNode.Parse(Compile(source).CanonicalBytes.Span)!;
        JsonArray roots = runtime["presentation"]!["sections"]![1]!["pointers"]!.AsArray();
        await Assert.That(roots.Count).IsEqualTo(2);
        await Assert.That(roots[0]!.GetValue<string>()).IsEqualTo("/ANOTHER");
        await Assert.That(roots[1]!.GetValue<string>()).IsEqualTo("/Service port");
        source["presentation"]!["sections"]![0]!["pointers"] = new JsonArray();
        source["presentation"]!["journeys"]![0]!["sections"] = new JsonArray("network");
        DefinitionValidationResult validation = DefinitionValidator.Validate(Compile(source), DefinitionFixture.Answers("{}"));
        await Assert.That(validation.IsReady).IsFalse();
        await Assert.That(validation.Fields.Single(field => field.Id == "TOKEN").IsRequired).IsTrue();
        await Assert.That(validation.Fields.Single(field => field.Id == "TOKEN").HasError).IsTrue();
    }

    [Test]
    public async Task PresentationBoundsAcceptTheirEdgesAndRejectOverflow()
    {
        foreach ((string member, int maximum) in new[] { ("id", 64), ("title", 128), ("description", 2048) })
        {
            JsonObject runtime = Runtime();
            runtime["presentation"]!["journeys"]![1]![member] = new string('x', maximum);
            _ = Parse(runtime);
            runtime["presentation"]!["journeys"]![1]![member] = new string('x', maximum + 1);
            await Reject(() => Parse(runtime));
            runtime = Runtime();
            runtime["presentation"]!["sections"]![0]![member] = new string('x', maximum + 1);
            await Reject(() => Parse(runtime));
        }
        foreach (string collection in new[] { "journeys", "sections" })
        {
            JsonObject runtime = Runtime();
            runtime["presentation"]![collection] = new JsonArray();
            await Reject(() => Parse(runtime));
            int maximum = collection == "journeys" ? 16 : 64;
            for (int index = 0; index <= maximum; index++)
                runtime["presentation"]![collection]!.AsArray().Add(collection == "journeys"
                    ? JsonNode.Parse($$"""{"id":"entry-{{index}}","title":"Entry","sections":["access","network"]}""")
                    : JsonNode.Parse($$"""{"id":"entry-{{index}}","title":"Entry","pointers":["/TOKEN"]}"""));
            await Reject(() => Parse(runtime));
        }
    }

    [Test]
    public async Task MaximumSectionAndJourneyCountsAdmitACompletePartition()
    {
        JsonObject runtime = Authoring();
        runtime["configuration"]!["properties"] = new JsonObject();
        runtime["configuration"]!["required"] = new JsonArray();
        var sections = new JsonArray();
        var sectionIds = new JsonArray();
        for (int index = 0; index < 64; index++)
        {
            string suffix = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            runtime["configuration"]!["properties"]!["root-" + suffix] = JsonNode.Parse(
                """{"type":"string","title":"Root","x-configurator":{"sensitivity":"public"}}""");
            sections.Add(new JsonObject
            {
                ["id"] = "section-" + suffix,
                ["title"] = "Section",
                ["pointers"] = new JsonArray("/root-" + suffix)
            });
            sectionIds.Add("section-" + suffix);
        }
        var journeys = new JsonArray();
        for (int index = 0; index < 16; index++)
            journeys.Add(new JsonObject
            {
                ["id"] = "journey-" + index,
                ["title"] = "Journey",
                ["sections"] = sectionIds.DeepClone()
            });
        runtime["presentation"] = new JsonObject
        {
            ["defaultJourney"] = "journey-0",
            ["sections"] = sections,
            ["journeys"] = journeys
        };
        SetupDefinition admitted = Compile(runtime);
        await Assert.That(admitted.Presentation!.Sections.Count).IsEqualTo(64);
        await Assert.That(admitted.Presentation.Experiences.Count).IsEqualTo(16);
        journeys.Add(new JsonObject
        {
            ["id"] = "overflow",
            ["title"] = "Overflow",
            ["sections"] = sectionIds.DeepClone()
        });
        await Reject(() => Compile(runtime));
        journeys.RemoveAt(16);
        sections.Add(new JsonObject { ["id"] = "overflow", ["title"] = "Overflow", ["pointers"] = new JsonArray("/root-0") });
        await Reject(() => Compile(runtime));
    }

    [Test]
    public async Task GuidanceBoundsAndClosedLinksRejectActiveOrUnboundedMetadata()
    {
        int mutationIndex = 0;
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            field => field["guidance"]!["instructions"] = new string('x', 4097),
            field => field["guidance"]!["instructions"] = "",
            field => field["guidance"]!["instructions"] = "${answer}",
            field => field["guidance"]!["instructions"] = "{{answer}}",
            field => field["guidance"]!["instructions"] = "\u0001",
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
            mutate(Metadata(runtime));
            await Reject(() => Parse(runtime), $"guidance-case-{mutationIndex++}");
        }
        foreach (string url in new[] { "http://example.org", "/docs", "javascript:alert(1)", "https://user:pass@example.org",
            "https://example.org?q=value", "https://example.org/?",
            "https://example.org/${answer}", "https://example.org/{{answer}}" })
        {
            JsonObject runtime = Runtime();
            Metadata(runtime)["guidance"]!["links"]![0]!["url"] = url;
            await Reject(() => Parse(runtime), $"link-case-{mutationIndex++}");
        }
        foreach (string inert in new[] { "<script>synthetic-private-marker</script>", "line\nbreak",
            "[Run](https://example.org)", "`execute synthetic-command`" })
        {
            JsonObject source = Authoring();
            Metadata(source)["guidance"]!["instructions"] = inert;
            await Assert.That(Compile(source).Fields.Single(field => field.Id == "TOKEN").Guidance!.Instructions).IsEqualTo(inert);
        }
        JsonObject fragment = Authoring();
        Metadata(fragment)["guidance"]!["links"]![0]!["url"] = "https://example.org/docs#credential";
        await Assert.That(Compile(fragment).Fields.Single(field => field.Id == "TOKEN").Guidance!.Links[0].Url)
            .IsEqualTo("https://example.org/docs#credential");
    }

    [Test]
    public async Task GuidanceMaximumInstructionsLinksAndSearchTermsRemainUsable()
    {
        JsonObject source = Authoring();
        JsonNode guidance = Metadata(source)["guidance"]!;
        guidance["instructions"] = new string('x', 4096);
        guidance["searchTerms"] = new JsonArray(Enumerable.Range(0, 16)
            .Select(index => (JsonNode?)JsonValue.Create(index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + new string('x', 62))).ToArray());
        guidance["links"] = new JsonArray(Enumerable.Range(0, 4)
            .Select(_ => JsonNode.Parse("""{"title":"Docs","url":"https://example.org/docs"}""")).ToArray());
        _ = Compile(source);
        Metadata(source)["guidance"] = new JsonObject();
        _ = Compile(source);
        await Assert.That(Parse(Runtime()).Fields.Count).IsEqualTo(2);
    }

    [Test]
    public async Task MetadataNullsAndUnknownMembersRejectThroughTheRuntimeBoundary()
    {
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            root => root["presentation"] = null,
            root => root["presentation"]!["sections"]![0]!["pointers"] = null,
            root => root["presentation"]!["navigation"] = null,
            root => Metadata(root)["detail"] = null,
            root => Metadata(root)["guidance"] = null,
            root => Metadata(root)["credential"] = null,
            root => Metadata(root)["guidance"]!["links"] = null,
            root => Metadata(root)["guidance"]!["searchTerms"] = null,
            root => Metadata(root)["credential"]!["kind"] = null,
            root => Metadata(root)["credential"]!["generation"] = new JsonObject(),
            root => Token(root)["comment"] = "synthetic-private-marker"
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
        foreach (string kind in new[] { "password", "opaque-token", "symmetric-key", "private-key", "connection-string" })
            foreach (string acquisition in new[] { "existing", "create", "issuer" })
            {
                JsonObject source = Authoring();
                Metadata(source)["credential"] = new JsonObject { ["kind"] = kind, ["acquisition"] = acquisition };
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
            Metadata(source)["credential"] = JsonNode.Parse(credential);
            await Reject(() => Compile(source));
        }
    }

    [Test]
    public async Task CredentialRequiresAnEffectivelySecretStringLeafIncludingInheritedContext()
    {
        foreach (string sensitivity in new[] { "public", "sensitive" })
        {
            JsonObject source = Authoring();
            Metadata(source)["sensitivity"] = sensitivity;
            await Reject(() => Compile(source));
        }
        foreach (string type in new[] { "integer", "boolean", "object", "array" })
        {
            JsonObject source = Authoring();
            Token(source)["type"] = type;
            Metadata(source).Remove("notBlank");
            if (type == "object")
            {
                Token(source)["additionalProperties"] = false;
                Token(source)["properties"] = new JsonObject { ["child"] = new JsonObject { ["type"] = "string" } };
            }
            if (type == "array") Token(source)["items"] = new JsonObject { ["type"] = "string" };
            await Reject(() => Compile(source));
        }
        foreach (bool array in new[] { false, true })
        {
            JsonObject source = Authoring();
            JsonNode container = JsonNode.Parse("""
                {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"secret"},
                 "properties":{"child":{"type":"string","x-configurator":{"sensitivity":"public",
                   "credential":{"kind":"password","acquisition":"existing"}}}}}
                """)!;
            source["configuration"]!["properties"]!["TOKEN"] = array
                ? new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = container,
                    ["x-configurator"] = new JsonObject { ["sensitivity"] = "secret" }
                }
                : container;
            SetupDefinition definition = Compile(source);
            await Assert.That(definition.Fields.Single(field => field.Id == "TOKEN").Sensitivity).IsEqualTo(SetupFieldSensitivity.Secret);
            Metadata(source)["sensitivity"] = "public";
            if (array) Token(source)["items"]!["x-configurator"]!["sensitivity"] = "public";
            await Reject(() => Compile(source));
        }
        JsonObject enumeration = Authoring();
        Token(enumeration)["enum"] = new JsonArray("one");
        await Assert.That(Compile(enumeration).Fields.Single(field => field.Id == "TOKEN").InputPurpose!.Kind).IsEqualTo("opaque-token");
    }

    [Test]
    public async Task DescriptorsCannotDemoteSensitivityOrAdmitPrivateDefaults()
    {
        foreach (string member in new[] { "default", "initialValue", "suggestedValue" })
        {
            JsonObject source = Authoring();
            if (member == "default") Token(source)[member] = "synthetic-private-marker";
            else Metadata(source)[member] = "synthetic-private-marker";
            await Reject(() => Compile(source));
        }
        JsonObject inherited = Authoring();
        inherited["configuration"]!["properties"]!["TOKEN"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"secret"},
             "properties":{"child":{"type":"string","default":"synthetic-private-marker","x-configurator":{"sensitivity":"public",
               "credential":{"kind":"password","acquisition":"existing"}}}}}
            """);
        await Reject(() => Compile(inherited));
    }

    [Test]
    public async Task AnyAdmittedMetadataChangeBindsDigestAndConflictsAtTheSameRevision()
    {
        SetupDefinition original = Compile(Authoring());
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            source => source["presentation"]!["journeys"]![0]!["title"] = "A different title",
            source => Metadata(source)["guidance"]!["instructions"] = "Different public guidance.",
            source => Metadata(source)["credential"]!["acquisition"] = "existing"
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
