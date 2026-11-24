namespace Configurator.Core.Tests.Definitions;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

public sealed class DefinitionPreparationSessionTests
{
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task CooperativePreparationCancellationHaltsMidWorkAndPublishesNothing(int suspendAt)
    {
        (Definition definition, AnswerSnapshot answers) = BuildWideNestedFixture(512);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int yields = 0;
        using var cancellation = new CancellationTokenSource();

        Task<DefinitionPreparationResult> pending = DefinitionPreparationSession.PrepareActiveAsync(
            definition, answers, Yield, cancellation.Token).AsTask();

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(pending.IsCompleted).IsFalse();

        cancellation.Cancel();
        release.SetResult();

        await Assert.That(async () =>
        {
            using DefinitionPreparationResult result = await pending;
        }).Throws<OperationCanceledException>();

        ValueTask Yield(CancellationToken _)
        {
            if (++yields != suspendAt) return ValueTask.CompletedTask;
            entered.TrySetResult();
            return new(release.Task);
        }
    }

    [Test]
    public async Task PreCancelledPreparationNeverInvokesCooperativeYield()
    {
        (Definition definition, AnswerSnapshot answers) = BuildWideNestedFixture(32);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        int yields = 0;

        await Assert.That(async () =>
        {
            using DefinitionPreparationResult result = await DefinitionPreparationSession.PrepareActiveAsync(
                definition, answers, Yield, cancellation.Token);
        }).Throws<OperationCanceledException>();

        await Assert.That(yields).IsEqualTo(0);

        ValueTask Yield(CancellationToken token)
        {
            yields++;
            token.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MultiArtifactPreparationValidatesExactlyOnce(bool draft)
    {
        (Definition definition, AnswerSnapshot answers) = BuildMultiArtifactFixture(includeSecret: !draft);
        using var root = new Activity("test-preparation-session");
        root.Start();
        using var measurement = new DefinitionBundleTests.PreparationMeasurement(root);

        int yields = 0;
        using DefinitionPreparationResult result = draft
            ? await DefinitionPreparationSession.PrepareActiveDraftAsync(definition, answers, Yield)
            : await DefinitionPreparationSession.PrepareActiveAsync(definition, answers, Yield);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Validation).IsNotNull();
        await Assert.That(result.Artifacts.Count).IsEqualTo(4);
        await Assert.That(measurement.Validations).IsEqualTo(1);
        await Assert.That(measurement.DraftModes).IsEquivalentTo([draft]);
        await Assert.That(measurement.Composed.Count).IsEqualTo(4);
        await Assert.That(yields).IsGreaterThan(0);

        ValueTask Yield(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            yields++;
            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task CooperativePreparationMatchesSynchronousBytesAndDigestsAcrossNearLimitShapes()
    {
        (string Name, Definition Definition, AnswerSnapshot Answers)[] fixtures =
        [
            ("wide-nested", BuildWideNestedFixture(512).Definition, BuildWideNestedFixture(512).Answers),
            ("large-map-list", BuildLargeMapListFixture(128, 256).Definition, BuildLargeMapListFixture(128, 256).Answers),
            ("deep-tree", BuildDeepTreeFixture(7).Definition, BuildDeepTreeFixture(7).Answers),
            ("multi-artifact", BuildMultiArtifactFixture(includeSecret: true).Definition, BuildMultiArtifactFixture(includeSecret: true).Answers)
        ];

        foreach ((string _, Definition definition, AnswerSnapshot answers) in fixtures)
        {
            using DefinitionBundleResult synchronous = DefinitionBundleComposer.ComposeActive(definition, answers);
            int yields = 0;
            using DefinitionPreparationResult cooperative = await DefinitionPreparationSession.PrepareActiveAsync(
                definition, answers, Yield);

            await Assert.That(synchronous.Succeeded).IsTrue();
            await Assert.That(cooperative.Succeeded).IsTrue();
            await Assert.That(cooperative.Validation).IsNotNull();
            await Assert.That(cooperative.Validation!.IsReady).IsTrue();
            await Assert.That(cooperative.Artifacts.Count).IsEqualTo(synchronous.Artifacts.Count);
            await Assert.That(yields).IsGreaterThan(0);

            for (int index = 0; index < synchronous.Artifacts.Count; index++)
            {
                DefinitionArtifact expected = synchronous.Artifacts[index];
                DefinitionArtifact actual = cooperative.Artifacts[index];
                await Assert.That(actual.ArtifactId).IsEqualTo(expected.ArtifactId);
                await Assert.That(actual.Digest).IsEqualTo(expected.Digest);
                await Assert.That(actual.Bytes.Span.SequenceEqual(expected.Bytes.Span)).IsTrue();
            }

            ValueTask Yield(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                yields++;
                return ValueTask.CompletedTask;
            }
        }
    }

    [Test]
    public async Task CooperativeDraftPreparationMatchesSynchronousBytesCompletionsAndDiagnostics()
    {
        (Definition definition, AnswerSnapshot answers) = BuildDraftWithNestedSecretFixture();
        using DefinitionDraftBundleResult synchronous = DefinitionDraftArtifactComposer.ComposeActiveBundle(definition, answers);
        using DefinitionPreparationResult cooperative = await DefinitionPreparationSession.PrepareActiveDraftAsync(
            definition, answers, token =>
            {
                token.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            });

        await Assert.That(cooperative.IsDraftMode).IsTrue();
        await Assert.That(cooperative.IsDraft).IsEqualTo(synchronous.IsDraft);
        await Assert.That(cooperative.Succeeded).IsEqualTo(synchronous.Succeeded);
        await Assert.That(cooperative.Artifacts.Count).IsEqualTo(synchronous.Artifacts.Count);
        await Assert.That(cooperative.Diagnostics.Count).IsEqualTo(synchronous.Diagnostics.Count);
        await Assert.That(cooperative.Completions.Count).IsEqualTo(synchronous.Completions.Count);

        for (int index = 0; index < synchronous.Artifacts.Count; index++)
        {
            await Assert.That(cooperative.Artifacts[index].ArtifactId).IsEqualTo(synchronous.Artifacts[index].ArtifactId);
            await Assert.That(cooperative.Artifacts[index].Digest).IsEqualTo(synchronous.Artifacts[index].Digest);
            await Assert.That(cooperative.Artifacts[index].Bytes.Span.SequenceEqual(synchronous.Artifacts[index].Bytes.Span)).IsTrue();
        }
        for (int index = 0; index < synchronous.Completions.Count; index++)
        {
            await Assert.That(cooperative.Completions[index]).IsEqualTo(synchronous.Completions[index]);
        }
        for (int index = 0; index < synchronous.Diagnostics.Count; index++)
        {
            await Assert.That(cooperative.Diagnostics[index].Code).IsEqualTo(synchronous.Diagnostics[index].Code);
            await Assert.That(cooperative.Diagnostics[index].FieldId).IsEqualTo(synchronous.Diagnostics[index].FieldId);
        }
    }

    [Test]
    public async Task DefinitionNodePrecomputesRequiredSetAndHasOptionalDescendant()
    {
        const string schemaJson = """
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1},
             "project":{"id":"example.optional","name":"Optional metadata","publisher":"example"},
             "applicationVersions":["1.0"],"entryPoints":["setup"],
             "configuration":{"type":"object","additionalProperties":false,"required":["allRequired","nestedOptional","basicOptional"],
              "x-configurator":{"sensitivity":"public"},
              "properties":{
                "allRequired":{"type":"object","additionalProperties":false,"required":["a"],
                  "properties":{"a":{"type":"string"}}},
                "nestedOptional":{"type":"object","additionalProperties":false,"required":["inner"],
                  "properties":{"inner":{"type":"object","additionalProperties":false,"required":["req"],
                    "properties":{"req":{"type":"string"},"opt":{"type":"string","x-configurator":{"detail":"advanced"}}}}}},
                "basicOptional":{"type":"object","additionalProperties":false,"required":["req"],
                  "properties":{"req":{"type":"string"},"optBasic":{"type":"string","x-configurator":{"detail":"basic"}}}}
              }},
             "outputs":{"out.json":{"id":"out","format":"json","targetUpdateMode":"create",
              "restartRequirement":"none","projection":{"op":"object","members":{
                "allRequired":{"op":"copy","reference":{"scope":"root","path":"/allRequired"}}}}}}}
            """;

        Definition definition = DefinitionFixture.Parse(schemaJson);
        DefinitionNode root = definition.Configuration;

        await Assert.That(root.RequiredSet.Contains("allRequired")).IsTrue();
        await Assert.That(root.RequiredSet.Contains("nestedOptional")).IsTrue();
        await Assert.That(root.RequiredSet.Contains("missing")).IsFalse();
        await Assert.That(root.Properties["allRequired"].HasOptionalDescendant).IsFalse();
        await Assert.That(root.Properties["nestedOptional"].HasOptionalDescendant).IsTrue();
        await Assert.That(root.Properties["nestedOptional"].Properties["inner"].HasOptionalDescendant).IsTrue();
        await Assert.That(root.Properties["basicOptional"].HasOptionalDescendant).IsFalse();
        await Assert.That(root.HasOptionalDescendant).IsTrue();
    }

    private static (Definition Definition, AnswerSnapshot Answers) BuildWideNestedFixture(int leafCount)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        var values = new JsonObject();
        for (int index = 0; index < leafCount; index++)
        {
            string name = "item" + index.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
            properties[name] = new JsonObject { ["type"] = "string", ["minLength"] = 1 };
            required.Add(name);
            values[name] = "value-" + index.ToString(CultureInfo.InvariantCulture);
        }

        var root = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject { ["familyId"] = "ea050002-4ef0-4c80-9f10-000000000002", ["revision"] = 1 },
            ["project"] = new JsonObject { ["id"] = "example.wide", ["name"] = "Wide", ["publisher"] = "example" },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["entryPoints"] = new JsonArray("setup"),
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("settings"),
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["properties"] = new JsonObject
                {
                    ["settings"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = false,
                        ["required"] = required,
                        ["properties"] = properties
                    }
                }
            },
            ["outputs"] = new JsonObject
            {
                ["settings.json"] = new JsonObject
                {
                    ["id"] = "settings",
                    ["format"] = "json",
                    ["targetUpdateMode"] = "create",
                    ["restartRequirement"] = "none",
                    ["projection"] = new JsonObject
                    {
                        ["op"] = "object",
                        ["members"] = new JsonObject
                        {
                            ["settings"] = new JsonObject
                            {
                                ["op"] = "copy",
                                ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/settings" }
                            }
                        }
                    }
                }
            }
        };

        var answerRoot = new JsonObject { ["settings"] = values };
        return (DefinitionFixture.Parse(root.ToJsonString()), DefinitionFixture.Answers(answerRoot.ToJsonString()));
    }

    private static (Definition Definition, AnswerSnapshot Answers) BuildLargeMapListFixture(int listCount, int mapCount)
    {
        var listAnswers = new JsonArray();
        for (int index = 0; index < listCount; index++)
        {
            listAnswers.Add(new JsonObject
            {
                ["name"] = "row-" + index.ToString(CultureInfo.InvariantCulture),
                ["enabled"] = index % 2 == 0,
                ["weight"] = index + 1
            });
        }
        var mapAnswers = new JsonObject();
        for (int index = 0; index < mapCount; index++)
        {
            mapAnswers["k" + index.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0')] =
                "v" + index.ToString(CultureInfo.InvariantCulture);
        }

        var root = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject { ["familyId"] = "ea050003-4ef0-4c80-9f10-000000000003", ["revision"] = 1 },
            ["project"] = new JsonObject { ["id"] = "example.maplist", ["name"] = "MapList", ["publisher"] = "example" },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["entryPoints"] = new JsonArray("setup"),
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("list", "map"),
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["properties"] = new JsonObject
                {
                    ["list"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["additionalProperties"] = false,
                            ["required"] = new JsonArray("name", "enabled", "weight"),
                            ["properties"] = new JsonObject
                            {
                                ["name"] = new JsonObject { ["type"] = "string", ["minLength"] = 1 },
                                ["enabled"] = new JsonObject { ["type"] = "boolean" },
                                ["weight"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 }
                            }
                        }
                    },
                    ["map"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = new JsonObject { ["type"] = "string", ["minLength"] = 1 }
                    }
                }
            },
            ["outputs"] = new JsonObject
            {
                ["bundle.json"] = new JsonObject
                {
                    ["id"] = "bundle",
                    ["format"] = "json",
                    ["targetUpdateMode"] = "create",
                    ["restartRequirement"] = "none",
                    ["projection"] = new JsonObject
                    {
                        ["op"] = "object",
                        ["members"] = new JsonObject
                        {
                            ["list"] = new JsonObject
                            {
                                ["op"] = "copy",
                                ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/list" }
                            },
                            ["map"] = new JsonObject
                            {
                                ["op"] = "copy",
                                ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/map" }
                            }
                        }
                    }
                }
            }
        };

        var answerRoot = new JsonObject { ["list"] = listAnswers, ["map"] = mapAnswers };
        return (DefinitionFixture.Parse(root.ToJsonString()), DefinitionFixture.Answers(answerRoot.ToJsonString()));
    }

    private static (Definition Definition, AnswerSnapshot Answers) BuildDeepTreeFixture(int depth)
    {
        (JsonObject schema, JsonNode value) = BuildLevel(depth, "n");
        var root = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject { ["familyId"] = "ea050001-4ef0-4c80-9f10-000000000001", ["revision"] = 1 },
            ["project"] = new JsonObject { ["id"] = "example.deep", ["name"] = "Deep", ["publisher"] = "example" },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["entryPoints"] = new JsonArray("setup"),
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("root"),
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["properties"] = new JsonObject { ["root"] = schema }
            },
            ["outputs"] = new JsonObject
            {
                ["deep.json"] = new JsonObject
                {
                    ["id"] = "deep",
                    ["format"] = "json",
                    ["targetUpdateMode"] = "create",
                    ["restartRequirement"] = "none",
                    ["projection"] = new JsonObject
                    {
                        ["op"] = "object",
                        ["members"] = new JsonObject
                        {
                            ["root"] = new JsonObject
                            {
                                ["op"] = "copy",
                                ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/root" }
                            }
                        }
                    }
                }
            }
        };
        return (DefinitionFixture.Parse(root.ToJsonString()),
            DefinitionFixture.Answers(new JsonObject { ["root"] = value }.ToJsonString()));

        static (JsonObject Schema, JsonNode Value) BuildLevel(int remaining, string prefix)
        {
            if (remaining == 0)
            {
                return (new JsonObject { ["type"] = "string", ["minLength"] = 1 }, JsonValue.Create("leaf-" + prefix)!);
            }
            (JsonObject leftSchema, JsonNode leftValue) = BuildLevel(remaining - 1, prefix + "0");
            (JsonObject rightSchema, JsonNode rightValue) = BuildLevel(remaining - 1, prefix + "1");
            return (
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("left", "right"),
                    ["properties"] = new JsonObject { ["left"] = leftSchema, ["right"] = rightSchema }
                },
                new JsonObject { ["left"] = leftValue, ["right"] = rightValue });
        }
    }

    private static (Definition Definition, AnswerSnapshot Answers) BuildMultiArtifactFixture(bool includeSecret)
    {
        const string json = """
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1},
             "project":{"id":"example.multi","name":"Multi","publisher":"example"},
             "applicationVersions":["1.0"],"entryPoints":["setup"],
             "configuration":{"type":"object","additionalProperties":false,"required":["service","apiKey"],
              "x-configurator":{"sensitivity":"public"},
              "properties":{
                "service":{"type":"string","minLength":1},
                "apiKey":{"type":"string","minLength":8,"x-configurator":{"sensitivity":"secret","secret":{"kind":"opaque-token","encoding":"base64url","acquisition":"existing"}}}
              }},
             "outputs":{
               "app.json":{"id":"app-json","format":"json","targetUpdateMode":"create","restartRequirement":"none",
                 "projection":{"op":"object","members":{
                   "service":{"op":"copy","reference":{"scope":"root","path":"/service"}},
                   "apiKey":{"op":"copy","reference":{"scope":"root","path":"/apiKey"}}}}},
               "app.yaml":{"id":"app-yaml","format":"yaml","targetUpdateMode":"create","restartRequirement":"none",
                 "projection":{"op":"object","members":{
                   "service":{"op":"copy","reference":{"scope":"root","path":"/service"}},
                   "apiKey":{"op":"copy","reference":{"scope":"root","path":"/apiKey"}}}}},
               "app.env":{"id":"app-env","format":"dotenv","environmentDialect":"compose","targetUpdateMode":"create","restartRequirement":"none",
                 "projection":{"op":"object","members":{
                   "SERVICE":{"op":"copy","reference":{"scope":"root","path":"/service"}},
                   "API_KEY":{"op":"copy","reference":{"scope":"root","path":"/apiKey"}}}}},
               "app.md":{"id":"app-md","format":"markdown","targetUpdateMode":"create","restartRequirement":"none",
                 "projection":{"op":"list","items":[
                   {"op":"literal","value":"# Service\n"},
                   {"op":"copy","reference":{"scope":"root","path":"/service"}}]}}
             }}
            """;
        string answerJson = includeSecret
            ? """{"service":"alpha","apiKey":"c3ludGhldGljLWtleS12YWx1ZS0xMjM0NTY3ODkwMTI"}"""
            : """{"service":"alpha"}""";
        return (DefinitionFixture.Parse(json), DefinitionFixture.Answers(answerJson));
    }

    private static (Definition Definition, AnswerSnapshot Answers) BuildDraftWithNestedSecretFixture()
    {
        const string json = """
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1},
             "project":{"id":"example.draft","name":"Draft","publisher":"example"},
             "applicationVersions":["1.0"],"entryPoints":["setup"],
             "configuration":{"type":"object","additionalProperties":false,"required":["service","db"],
              "x-configurator":{"sensitivity":"public"},
              "properties":{
                "service":{"type":"string","minLength":1},
                "db":{"type":"object","additionalProperties":false,"required":["host","password"],
                  "properties":{
                    "host":{"type":"string","minLength":1},
                    "password":{"type":"string","minLength":8,"x-configurator":{"sensitivity":"secret","secret":{"kind":"password","acquisition":"local"}}}
                  }}
              }},
             "outputs":{
               "app.json":{"id":"app-json","format":"json","targetUpdateMode":"create","restartRequirement":"none",
                 "projection":{"op":"object","members":{
                   "service":{"op":"copy","reference":{"scope":"root","path":"/service"}},
                   "db":{"op":"copy","reference":{"scope":"root","path":"/db"}}}}},
               "app.env":{"id":"app-env","format":"dotenv","environmentDialect":"compose","targetUpdateMode":"create","restartRequirement":"none",
                 "projection":{"op":"object","members":{
                   "SERVICE":{"op":"copy","reference":{"scope":"root","path":"/service"}},
                   "DB_PASSWORD":{"op":"copy","reference":{"scope":"root","path":"/db/password"}}}}},
               "gated.json":{"id":"gated","format":"json","targetUpdateMode":"create","restartRequirement":"none",
                 "projection":{"op":"object","when":{"op":"equals","reference":{"scope":"root","path":"/db/password"},"value":"exact"},
                   "members":{"service":{"op":"copy","reference":{"scope":"root","path":"/service"}}}}}
             }}
            """;
        return (DefinitionFixture.Parse(json), DefinitionFixture.Answers("""{"service":"alpha","db":{"host":"db.example.invalid"}}"""));
    }
}
