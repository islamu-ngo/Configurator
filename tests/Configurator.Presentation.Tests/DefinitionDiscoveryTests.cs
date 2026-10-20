namespace Configurator.Presentation.Tests;

using Configurator.Core.Definitions;

public sealed class DefinitionDiscoveryTests
{
    [Test]
    public async Task QueryRequiresEveryTokenAcrossPublicMetadata()
    {
        DefinitionDiscoveryEntry[] entries =
        [
            Entry("connection", title: "Mail relay", help: "Require TLS"),
            Entry("plain", title: "Mail relay"),
            Entry("transport", help: "Require TLS")
        ];

        DefinitionDiscoverySnapshot result = DefinitionDiscovery.Search(entries, new("  MAIL\tTls  "));

        await Assert.That(result.TotalCount).IsEqualTo(1);
        await Assert.That(result.Matches.Single().Entry.FieldId).IsEqualTo("connection");
        await Assert.That(DefinitionDiscovery.Search(entries, new("mail missing")).TotalCount).IsEqualTo(0);
    }

    [Test]
    public async Task FacetsUseOrWithinAndAcrossCategoriesAndQuery()
    {
        DefinitionDiscoveryEntry[] entries =
        [
            Entry("first", title: "Mail", section: "one"),
            Entry("second", title: "Mail", section: "two", sensitivity: SetupFieldSensitivity.Secret),
            Entry("third", title: "Mail", section: "three"),
            Entry("fourth", title: "Mail", section: "one", required: false),
            Entry("fifth", title: "Storage", section: "one")
        ];
        var selection = new DefinitionDiscoverySelection("mail", facets:
            new Dictionary<DefinitionDiscoveryFacet, IReadOnlyList<string>>
            {
                [DefinitionDiscoveryFacet.Section] = ["one", "two"],
                [DefinitionDiscoveryFacet.Requirement] = ["required"],
                [DefinitionDiscoveryFacet.Sensitivity] = ["PUBLIC", "secret"]
            });

        DefinitionDiscoverySnapshot result = DefinitionDiscovery.Search(entries, selection);

        await Assert.That(string.Join(",", result.Matches.Select(match => match.Entry.FieldId)))
            .IsEqualTo("first,second");
        await Assert.That(Count(result, DefinitionDiscoveryFacet.Sensitivity, "public")).IsEqualTo(1);
        await Assert.That(Count(result, DefinitionDiscoveryFacet.Sensitivity, "secret")).IsEqualTo(1);
        await Assert.That(result.Facets[DefinitionDiscoveryFacet.Sensitivity].Count).IsEqualTo(2);
    }

    [Test]
    public async Task EachFacetCountRemovesOnlyItsOwnSelection()
    {
        DefinitionDiscoveryEntry[] entries =
        [
            Entry("base"),
            Entry("section", section: "other"),
            Entry("requirement", required: false),
            Entry("applicability", active: false),
            Entry("state", ready: false, attention: true),
            Entry("sensitivity", sensitivity: SetupFieldSensitivity.Secret),
            Entry("artifact", artifacts: ["manifest"]),
            Entry("query-excluded", title: "Unrelated", section: "other")
        ];
        var selection = new DefinitionDiscoverySelection("common", facets:
            new Dictionary<DefinitionDiscoveryFacet, IReadOnlyList<string>>
            {
                [DefinitionDiscoveryFacet.Section] = ["main"],
                [DefinitionDiscoveryFacet.Requirement] = ["required"],
                [DefinitionDiscoveryFacet.Applicability] = ["active"],
                [DefinitionDiscoveryFacet.State] = ["ready"],
                [DefinitionDiscoveryFacet.Sensitivity] = ["public"],
                [DefinitionDiscoveryFacet.Artifact] = ["env"]
            });

        DefinitionDiscoverySnapshot result = DefinitionDiscovery.Search(entries, selection);

        await Assert.That(result.TotalCount).IsEqualTo(1);
        await Assert.That(Count(result, DefinitionDiscoveryFacet.Section, "other")).IsEqualTo(1);
        await Assert.That(Count(result, DefinitionDiscoveryFacet.Requirement, "optional")).IsEqualTo(1);
        await Assert.That(Count(result, DefinitionDiscoveryFacet.Applicability, "inactive")).IsEqualTo(1);
        await Assert.That(Count(result, DefinitionDiscoveryFacet.State, "needs-attention")).IsEqualTo(1);
        await Assert.That(Count(result, DefinitionDiscoveryFacet.Sensitivity, "secret")).IsEqualTo(1);
        await Assert.That(Count(result, DefinitionDiscoveryFacet.Artifact, "manifest")).IsEqualTo(1);
        // A field with repeated artifact declarations still contributes once to the option.
        await Assert.That(Count(result, DefinitionDiscoveryFacet.Artifact, "env")).IsEqualTo(1);
    }

    [Test]
    public async Task InactiveInclusionIsDeliberateAndDoesNotAlterEntryFacts()
    {
        DefinitionDiscoveryEntry active = Entry("active");
        DefinitionDiscoveryEntry inactive = Entry("inactive", active: false, ready: false, attention: true);
        DefinitionDiscoveryEntry[] entries = [inactive, active];

        DefinitionDiscoverySnapshot initial = DefinitionDiscovery.Search(entries, new());
        await Assert.That(initial.Matches.Single().Entry.FieldId).IsEqualTo("active");
        await Assert.That(Count(initial, DefinitionDiscoveryFacet.Applicability, "inactive")).IsEqualTo(1);
        await Assert.That(DefinitionDiscovery.Search(entries, new(includeInactive: true)).TotalCount).IsEqualTo(2);

        DefinitionDiscoverySnapshot result = DefinitionDiscovery.Search(entries,
            Select(DefinitionDiscoveryFacet.Applicability, "inactive"));
        await Assert.That(ReferenceEquals(result.Matches.Single().Entry, inactive)).IsTrue();
        await Assert.That(inactive.IsActive).IsFalse();
        await Assert.That(inactive.IsReady).IsFalse();
        await Assert.That(inactive.NeedsAttention).IsTrue();

        var both = new DefinitionDiscoverySelection(facets:
            new Dictionary<DefinitionDiscoveryFacet, IReadOnlyList<string>>
            {
                [DefinitionDiscoveryFacet.Applicability] = ["active", "inactive"]
            });
        await Assert.That(DefinitionDiscovery.Search(entries, both).TotalCount).IsEqualTo(2);
        await Assert.That(DefinitionDiscovery.Search(entries, Select(DefinitionDiscoveryFacet.State,
            "needs-attention")).TotalCount).IsEqualTo(0);
    }

    [Test]
    public async Task StateFacetsConsumeIndependentProjectionFactsAndRecordedOrigin()
    {
        DefinitionDiscoveryEntry[] entries =
        [
            Entry("default", origin: DefinitionAnswerOrigin.Defaulted),
            Entry("explicit", provided: true, origin: DefinitionAnswerOrigin.Explicit),
            Entry("imported", provided: true, origin: DefinitionAnswerOrigin.Imported),
            Entry("omitted", ready: false, attention: true),
            Entry("deferred", ready: false, attention: true, deferred: true,
                sensitivity: SetupFieldSensitivity.Secret)
        ];

        await Assert.That(DefinitionDiscovery.Search(entries,
            Select(DefinitionDiscoveryFacet.State, "defaulted")).Matches.Single().Entry.FieldId)
            .IsEqualTo("default");
        await Assert.That(DefinitionDiscovery.Search(entries,
            Select(DefinitionDiscoveryFacet.State, "supplied")).TotalCount).IsEqualTo(2);
        await Assert.That(DefinitionDiscovery.Search(entries,
            Select(DefinitionDiscoveryFacet.State, "omitted")).TotalCount).IsEqualTo(2);
        await Assert.That(DefinitionDiscovery.Search(entries,
            Select(DefinitionDiscoveryFacet.State, "needs-attention")).TotalCount).IsEqualTo(2);
        await Assert.That(DefinitionDiscovery.Search(entries,
            Select(DefinitionDiscoveryFacet.State, "ready")).TotalCount).IsEqualTo(3);
        await Assert.That(DefinitionDiscovery.Search(entries,
            Select(DefinitionDiscoveryFacet.State, "secret-deferred")).Matches.Single().Entry.FieldId)
            .IsEqualTo("deferred");
        await Assert.That(DefinitionDiscovery.Search(entries,
            Select(DefinitionDiscoveryFacet.Sensitivity, "secret")).TotalCount).IsEqualTo(1);
        await Assert.That(DefinitionDiscovery.Search([Entry("sensitive", sensitivity: SetupFieldSensitivity.Sensitive)],
            Select(DefinitionDiscoveryFacet.Sensitivity, "sensitive")).TotalCount).IsEqualTo(1);
    }

    [Test]
    public async Task RankingUsesExactKeysThenTitleThenTermsThenHelp()
    {
        DefinitionDiscoveryEntry[] entries =
        [
            Entry("instructions", instructions: "Mail delivery"),
            Entry("help", help: "Mail delivery"),
            Entry("terms", terms: ["mail"]),
            Entry("tokens", title: "Send mail now"),
            Entry("prefix", title: "Mail relay"),
            Entry("title", title: "Mail"),
            Entry("mail"),
            Entry("a-key", keys: ["MAIL"])
        ];

        DefinitionDiscoverySnapshot result = DefinitionDiscovery.Search(entries, new("mail"));

        await Assert.That(string.Join(",", result.Matches.Select(match => match.Entry.FieldId)))
            .IsEqualTo("a-key,mail,title,prefix,tokens,terms,help,instructions");
        await Assert.That(string.Join(",", result.Matches.Select(match => match.Rank)))
            .IsEqualTo("0,0,1,2,3,4,5,5");
    }

    [Test]
    public async Task EqualRanksUseSectionOrderAndLiteralPathSegmentsRegardlessOfInputOrder()
    {
        DefinitionDiscoveryEntry[] entries =
        [
            Entry("root", path: ["a.b"], order: 1),
            Entry("root", path: ["a", "b"], order: 1),
            Entry("root", path: ["a"], order: 1),
            Entry("z", order: 0)
        ];

        DefinitionDiscoverySnapshot forward = DefinitionDiscovery.Search(entries, new());
        DefinitionDiscoverySnapshot reverse = DefinitionDiscovery.Search(entries.Reverse(), new());

        await Assert.That(forward.Matches[0].Entry.FieldId).IsEqualTo("z");
        await Assert.That(forward.Matches[1].Entry.RelativePath.Count).IsEqualTo(1);
        await Assert.That(forward.Matches[1].Entry.RelativePath[0]).IsEqualTo("a");
        await Assert.That(forward.Matches[2].Entry.RelativePath.Count).IsEqualTo(2);
        await Assert.That(forward.Matches[3].Entry.RelativePath.Single()).IsEqualTo("a.b");
        await Assert.That(forward.Matches.Select(match => match.Entry)
            .SequenceEqual(reverse.Matches.Select(match => match.Entry))).IsTrue();

        DefinitionDiscoverySnapshot literal = DefinitionDiscovery.Search(entries, new("a.b"));
        await Assert.That(literal.Matches.Single().Entry.RelativePath.Single()).IsEqualTo("a.b");
    }

    [Test]
    public async Task EmptySelectionsAreUnrestrictedAndZeroResultsRetainRecoveryCounts()
    {
        DefinitionDiscoveryEntry[] entries = [Entry("first"), Entry("second", required: false)];
        var empty = Select(DefinitionDiscoveryFacet.Requirement);
        await Assert.That(DefinitionDiscovery.Search(entries, empty).TotalCount).IsEqualTo(2);

        var unknown = Select(DefinitionDiscoveryFacet.Section, "absent");
        DefinitionDiscoverySnapshot result = DefinitionDiscovery.Search(entries, unknown);
        await Assert.That(result.TotalCount).IsEqualTo(0);
        await Assert.That(Count(result, DefinitionDiscoveryFacet.Section, "main")).IsEqualTo(2);
        await Assert.That(Count(result, DefinitionDiscoveryFacet.Section, "absent")).IsEqualTo(0);
        await Assert.That(Count(result, DefinitionDiscoveryFacet.Requirement, "required")).IsEqualTo(0);
        await Assert.That(unknown.Facets[DefinitionDiscoveryFacet.Section].Single()).IsEqualTo("absent");

        var query = new DefinitionDiscoverySelection("no-match");
        await Assert.That(DefinitionDiscovery.Search(entries, query).TotalCount).IsEqualTo(0);
        await Assert.That(query.Query).IsEqualTo("no-match");
        await Assert.That(DefinitionDiscovery.Search(Array.Empty<DefinitionDiscoveryEntry>(), new()).TotalCount)
            .IsEqualTo(0);
    }

    [Test]
    public async Task QueriesAcceptExactBoundsAndRejectOversizedInputWithoutTruncation()
    {
        await Assert.That(new DefinitionDiscoverySelection(new string('x', 256)).Query.Length).IsEqualTo(256);
        await Assert.That(new DefinitionDiscoverySelection(string.Join(" ", Enumerable.Repeat("x", 16)))
            .QueryTerms.Count).IsEqualTo(16);
        var trimmed = new DefinitionDiscoverySelection(" \tMail\nTLS ");
        await Assert.That(trimmed.Query).IsEqualTo("Mail\nTLS");
        await Assert.That(trimmed.QueryTerms.Count).IsEqualTo(2);
        await Assert.That(DefinitionDiscovery.Search([Entry("field")], new(" \t\n ")).TotalCount).IsEqualTo(1);

        int rejected = 0;
        foreach (string query in new[] { new string('x', 257), string.Join(" ", Enumerable.Repeat("x", 17)) })
        {
            try
            {
                _ = new DefinitionDiscoverySelection(query);
            }
            catch (ArgumentException)
            {
                rejected++;
            }
        }
        await Assert.That(rejected).IsEqualTo(2);
    }

    [Test]
    public async Task CallerMutationCannotChangePublishedEntriesSelectionsOrResults()
    {
        string[] path = ["literal.key"];
        string[] terms = ["smtp"];
        string[] keys = ["MAIL_HOST"];
        string[] artifacts = ["env"];
        string[] sections = ["main"];
        var facets = new Dictionary<DefinitionDiscoveryFacet, IReadOnlyList<string>>
        {
            [DefinitionDiscoveryFacet.Section] = sections
        };
        DefinitionDiscoveryEntry entry = Entry("field", path: path, terms: terms, keys: keys, artifacts: artifacts);
        var entries = new List<DefinitionDiscoveryEntry> { entry };
        var selection = new DefinitionDiscoverySelection("smtp", facets: facets);
        DefinitionDiscoverySnapshot result = DefinitionDiscovery.Search(entries, selection);

        path[0] = "changed";
        terms[0] = "changed";
        keys[0] = "changed";
        artifacts[0] = "changed";
        sections[0] = "changed";
        facets.Clear();
        entries.Clear();

        await Assert.That(result.TotalCount).IsEqualTo(1);
        await Assert.That(entry.RelativePath.Single()).IsEqualTo("literal.key");
        await Assert.That(entry.SearchTerms.Single()).IsEqualTo("smtp");
        await Assert.That(entry.BindingKeys.Single()).IsEqualTo("MAIL_HOST");
        await Assert.That(entry.OutputArtifactIds.Single()).IsEqualTo("env");
        await Assert.That(selection.Facets[DefinitionDiscoveryFacet.Section].Single()).IsEqualTo("main");
        await Assert.That(DefinitionDiscovery.Search([entry], selection).TotalCount).IsEqualTo(1);
        await Assert.That(Count(result, DefinitionDiscoveryFacet.Artifact, "env")).IsEqualTo(1);
        await Assert.That(((ICollection<DefinitionDiscoveryMatch>)result.Matches).IsReadOnly).IsTrue();
        await Assert.That(((ICollection<DefinitionDiscoveryFacetOption>)
            result.Facets[DefinitionDiscoveryFacet.Section]).IsReadOnly).IsTrue();
    }

    private static int Count(DefinitionDiscoverySnapshot result, DefinitionDiscoveryFacet facet, string value) =>
        result.Facets[facet].Single(option => option.Value == value).Count;

    private static DefinitionDiscoverySelection Select(DefinitionDiscoveryFacet facet, params string[] values) =>
        new(facets: new Dictionary<DefinitionDiscoveryFacet, IReadOnlyList<string>> { [facet] = values });

    private static DefinitionDiscoveryEntry Entry(string id, string title = "Common", string section = "main",
        bool active = true, bool required = true, bool provided = false, bool ready = true,
        bool attention = false, bool deferred = false, DefinitionAnswerOrigin origin = DefinitionAnswerOrigin.Omitted,
        SetupFieldSensitivity sensitivity = SetupFieldSensitivity.Public, string[]? path = null,
        string[]? terms = null, string? help = null, string? instructions = null,
        string[]? artifacts = null, string[]? keys = null, int order = 0) =>
        new(id, path ?? [], title, section, section, "string", sensitivity, active, required, provided, ready,
            attention, deferred, origin, terms, help, instructions, artifacts ?? ["env", "env"], keys, order);
}
