# Repository knowledge inventory and context

> **Audience:** Contributors | Maintainers | AI agents
> **Status:** Implemented tooling; see the migration report for executed validation
> **Owner:** Contributor Experience
> **Source Anchors:** `eng/knowledge/Knowledge.cs`, `eng/knowledge/knowledge.config.json`, `docs/internal/DOCUMENTATION_ARCHITECTURE.md`, `eng/configurator/ValidateDefinitionDocumentation.cs`

Generate a complete, reviewable inventory before choosing a smaller context.
These commands are local engineering tools, not a second documentation authority
or a replacement for the repository entry points. They need the repository's
.NET SDK 10.0.302 and Git, with no added packages, npm, Repomix installation,
network retrieval or product build.

Run from this checkout (a subdirectory also works when the source operand is
adjusted):

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet run --file eng/knowledge/Knowledge.cs -- inventory
dotnet run --file eng/knowledge/Knowledge.cs -- context current
dotnet run --file eng/knowledge/Knowledge.cs -- context definitions
dotnet run --file eng/knowledge/Knowledge.cs -- context clients
dotnet run --file eng/knowledge/Knowledge.cs -- context agents
dotnet run --file eng/knowledge/Knowledge.cs -- context audit
```

`dotnet run --file` compiles the engineering command itself. It does not build
the solution. Each command writes only its named generated outputs under
`.tmp/knowledge/`; repeat it after source changes to refresh its snapshot.

## Outputs and source identity

All generated files go under `.tmp/knowledge/`, never into the source knowledge
directories. Repeating a command replaces only its named generated outputs.
Symlinked inputs and output path components are refused or excluded.

| Command | Generated files |
| --- | --- |
| `inventory` | `inventory.tsv`, `inventory.md` |
| `context PROFILE` | `context-PROFILE.md`, `context-PROFILE.tsv` |
| `links PROFILE` | `links-PROFILE.tsv`, `links-PROFILE.log`, `links-PROFILE-result.tsv` |

The TSV is the complete per-path machine view, including excluded candidates.
It records tracked/untracked origin, purpose, owner, kind, authority, effective and declared
status, declared owner, last verification metadata, preservation, disposition,
selection, selection reason, SHA-256 and classification evidence. Tabs, newlines
and backslashes in cells use `\t`, `\n`, `\r` and `\\` escapes. Comment lines
beginning with `#` precede the tab-separated header.

Every manifest records `HEAD`, the selection-configuration digest and the fact
that contents come from the **working tree**. Per-document SHA-256 binds the
bytes actually read. A revision alone does not assert a clean checkout or that
all files were captured atomically. Untracked explicit inputs are labeled
`tracked=false`; removed tracked inputs remain visible as excluded `missing`.

Context files contain the **original bytes**, not summaries, rewritten policies,
compressed representations or generated replacements. Begin/end comments add
path, status and digest boundaries without changing the enclosed source bytes.
The added boundary newline is outside the original contents. Source Markdown
may have nested headings, frontmatter and fences: this is a retrieval bundle,
not a promise that the concatenation renders like a single authored page.

## Inventory boundary

The candidate set is:

1. Every tracked path whose extension is `.md`, case-insensitively, from
   `git ls-files --cached -z`.
2. Literal `explicitPaths` in `knowledge.config.json`, including the new tool,
   its README and selection configuration, the existing Repomix configuration,
   the actual intent registry/schema and the non-Markdown harness pointer.

The explicit list admits known new knowledge/configuration inputs without
discovering arbitrary untracked files. It is a selection configuration, not a
per-document classification registry. Newly tracked Markdown is discovered
automatically; a new untracked input needs an explicit literal path.
Profiles use literal case-insensitive path substrings, not shell globs.

Before any candidate content read, the tool excludes:

- Git-ignored candidates, including tracked candidates now matched by ignore
  rules. Git's repository and local exclusion configuration applies.
- `dev/active/`, `.worktrees`, `.tmp`, local `.claude/`, `.codex/`, `.copilot/`
  state, `.env*`, `AGENTS.local.md`, and machine MCP configuration.
- `private`, `secrets`, `bin`, `obj`, `assets`, `node_modules`, `.git`,
  `artifacts`, `.artifacts` and `TestResults` path components.
- The generated `.repomix/repomix-docs.md`, missing inputs and symlinked paths.

Excluded tracked Markdown still gets an inventory row with `not-inspected`
metadata and its exclusion reason; its content and digest are not read.
Untracked non-explicit paths are outside the universe, not silently classified.
There is no private/active-work override, recursive home-directory scan,
arbitrary operand import or history checkout.

This boundary is path-based. It cannot detect a secret committed under an
ordinary documentation filename or certify source licensing from prose.
Only repository knowledge and reviewed explicit public inputs belong here.
The tool does not follow links while constructing inventories or contexts.

## Classification policy

Authority stays in [AGENTS.md](../../AGENTS.md),
[PROJECTS.md](../../PROJECTS.md),
[Documentation Architecture](../../docs/internal/DOCUMENTATION_ARCHITECTURE.md)
and each source document. This tool neither promotes an old report to policy
nor authorizes edits, deletion, third-party relicensing or publication.

Purpose uses the existing Diataxis categories: `Tutorial`, `How-to`,
`Reference`, `Explanation`. Owner uses the documentation policy's accountable
categories: `Packaging/Release`, `Core`, `Artifacts/Security`, `Presentation`,
`CLI`, `Browser`, `Terminal`, `Desktop`, `Project Steward`,
`Contributor Experience`, `Agent Context`.

`authority` identifies existing contribution, project, documentation-policy and
task-routing owners, or labels subordinate guidance, proposals, strategy,
decisions and reference evidence. It is a generated interpretation of the
existing authority order, not a new precedence registry.

The parser reads flat frontmatter and initial title metadata, including natural
blockquote fields, plain ADR fields and bullet metadata. It does not scan body
prose for arbitrary status words or implement a YAML engine. Explicit `Intent`
and recognized `Owner` metadata win. Otherwise path/category inference is
labeled in `evidence`; unrecognized original owner labels remain separately
available in `declared_owner`. Absent status stays `unspecified`, never a claim
of implementation or verification. `Last Verified` is copied, not renewed.

| Source category | Effective treatment | Preservation/disposition |
| --- | --- | --- |
| Current public guides, technical anchors, entry docs | Declared status retained; unspecified status remains visible | Preserve source; review absent metadata |
| Reports and I-VSD assessments | Historical even when an old header says Implemented | Immutable point-in-time record; audit context |
| Extraction records except current `FOUNDATION.md` | Historical transfer/verification evidence | Immutable point-in-time record; audit context |
| ADRs | Decision records, not live interpreter contracts | Retain rationale; audit context |
| Reference-only skill/rule metadata | Inactive reference | Retain audit context, never activate backend requirements |
| Resources beneath an explicitly reference-only skill | Inherit the skill's reference status | Retain with inherited evidence path |
| `vendor`, `third-party`, `third_party` paths | Reference-only | Preserve third-party terms; audit context |
| Backlog, declared Draft/Planned | Planning, not implemented support | Retain planning; audit context |
| User strategy documents | Source status retained; excluded from current/focused bundles | User-owned; preserve in audit without rewriting |

Kinds identify existing repository roles (guide, technical anchor, rule, skill,
harness, journal, report, decision, example, configuration, index). They are
navigation labels, not newly binding owner assignments. Generic engineering
skills are not all historical merely because they were transferred; explicit
reference-only metadata governs that distinction. Files without such metadata
are not automatically treated as third-party/vendor material.

## Context profiles

All profiles have deterministic ordering: current entry documents in configured
order, remaining current/unspecified documents by ordinal path, planning,
historical/reference records, then excluded inventory rows. Historical groups
also sort by path; this is authority-first ordering, not a modification-time
or inferred recency ranking.

| Profile | Selection |
| --- | --- |
| `current` | Current entrypoints, guides and technical contracts; decision indexes remain reachable, while strategy, history and the deep skill library stay on demand |
| `definitions` | Current definition, Core, authoring, schema, integration, launch, example and store paths |
| `clients` | Current browser, desktop, terminal, CLI, Presentation, output, packaging, comparison, import, accessibility and UI paths |
| `agents` | Current agent/skill entrypoints, rules, harnesses, journal and engineering paths; linked `resources/` and `references/` remain on demand or in audit |
| `audit` | Every eligible candidate, including planning, ADRs, historical and reference material |

Every focused profile also selects current entry documents, actual intent
registry/schema, active rules and harness pointers. It does not substitute a
summary for their original contents. If a required entry is missing, ignored or
symlinked, generation fails rather than silently dropping the contract.
Historical inactive rules remain in the full audit profile.

“Full-history audit” means the complete retained knowledge corpus **at the
current checkout**, including reports, extraction evidence, assessments, journals
and decisions. It does not traverse every Git commit or recover deleted files.
All profiles' TSV manifests show which candidate paths were excluded and why,
including profile-only exclusions.

## Bounded link checking and honest failures

Reuse the existing native checker; do not add a competing Markdown parser:

```bash
dotnet run --file eng/knowledge/Knowledge.cs -- links current
dotnet run --file eng/knowledge/Knowledge.cs -- links definitions
dotnet run --file eng/knowledge/Knowledge.cs -- links clients
dotnet run --file eng/knowledge/Knowledge.cs -- links agents
dotnet run --file eng/knowledge/Knowledge.cs -- links audit
```

`links current` checks **all current Markdown**, including deep skill resources
omitted from the compact current context. Focused link commands check every
Markdown file selected by their profile. Primary guides, contracts and indexes also receive natural-metadata
checks. Agent frontmatter and preserved dependency records use `--links-only`:
they have different metadata contracts, and adding current review dates to
approval evidence would misstate its history. Each batch records whether
metadata was checked. This is not an exemption from link, anchor or fence errors;
agent metadata is checked separately against its own schema and registry.

`links audit` checks every eligible Markdown candidate, including historical,
reference and planning documents. It deliberately applies the existing
checker's behavior unchanged. Historical metadata and broken-link errors remain
errors. No file-specific exemption, waived error, baseline subtraction, warning
suppression or rewritten historical record makes an audit pass.

Both modes invoke
[ValidateDefinitionDocumentation.cs](../configurator/ValidateDefinitionDocumentation.cs)
with explicit operands in batches of at most 32 files, grouped by metadata
contract. They retain stdout and
stderr in a generated log, record each batch exit and a final result TSV, continue
through all failing batches, and return nonzero if **any** batch fails.
The result reports file/batch counts, not an invented deduplicated issue total.
An interrupted or failed launch may leave a partial log; only a newly completed
result TSV is a completed run, and its revision must match the intended inputs.
The previous result TSV is removed before starting the check, so interruption
cannot leave an old success result beside a new partial log.

The checker's limits:

- Inline Markdown links and images; ATX heading anchors outside fenced/inline
  code. It is not a full Markdown renderer, reference-link parser or HTML anchor
  engine. Unsupported syntax can itself produce an error.
- Local target existence, supported heading fragments, root containment and
  symlink rejection. External URIs are not requested or verified.
- Flat frontmatter, synthetic-header detection, and current natural metadata
  requirements on non-ADR `docs/internal/` operands. That last rule also reports
  historical internal records without current metadata; this is not waived.
- Referenced anchor targets can be loaded by the checker. The inventory's
  no-follow-links guarantee does not extend to this separate explicit check.
  Its existing root/symlink bounds apply, not a new privacy scanner.

Keep current-check and historical-audit outcomes separate. Fix current navigation
at its owner; preserve historical limitations in the
[report index](../../docs/internal/reports/README.md#classified-historical-validation-limitations).
A green current check does not imply that historical links were repaired.

## Verification commands

### Documentation CI

[Documentation quality](../../.github/workflows/docs-quality.yml) runs knowledge,
Markdown and reference self-tests, schema/reference freshness and `links current`
for relevant pull requests, main/develop
pushes and manual dispatch. It uses the pinned SDK, read-only repository
permissions and no product test matrix. Input triggers cover knowledge, agent
routing, schema/tool sources and shared build/ignore prerequisites. Local
equivalent commands below do not establish hosted execution.

Manual dispatch can additionally select `audit`. Its separate retained-history
job runs `links audit` and preserves the actual nonzero exit; it does not subtract
known findings or turn a current pass into a full-history claim. Neither job
uploads original-content bundles, requests external links or uses secrets.

Run the synthetic machine-contract checks, then inspect real outputs:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet run --file eng/knowledge/Knowledge.cs -- --self-test
dotnet run --file eng/configurator/ValidateDefinitionDocumentation.cs -- --self-test
dotnet run --file eng/knowledge/Knowledge.cs -- inventory
for profile in current definitions clients agents audit; do
  dotnet run --file eng/knowledge/Knowledge.cs -- context "$profile" || break
done
dotnet run --file eng/knowledge/Knowledge.cs -- links current
dotnet run --file eng/knowledge/Knowledge.cs -- links audit
```

The knowledge self-test uses only synthetic machine metadata, manifest fields,
candidate paths, digests and profile selections. It does not pin repository
prose, execute Git writes or read private fixtures. It covers exclusion,
deduplication, current/history/reference selection, deterministic ordering,
metadata precedence, TSV escaping and invalid configuration rejection.
Real Git ignore behavior, filesystem symlink behavior and actual repository
output require executed verification in addition to these synthetic cases.

## Generated command reference

`RenderCommandReference.cs` reads only the fixed public
`schemas/islamu-configurator-command-v4.schema.json` and projects its metadata to
`docs/public/cli-command-reference.md`. It accepts no answer files or arbitrary
input/output operands. The generated page names its source identity, vocabulary,
bounds and semantic limits; the maintained CLI guide and contract own meaning.
Unsupported metadata fails before output is written. Do not edit generated
Markdown to make a freshness check pass.

Run from the repository root, in this order:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
dotnet build eng/configurator/Configurator.CommandSchemaGenerator/Configurator.CommandSchemaGenerator.csproj --configuration Release --verbosity quiet
dotnet run --project eng/configurator/Configurator.CommandSchemaGenerator/Configurator.CommandSchemaGenerator.csproj -- --check
dotnet run --file eng/knowledge/RenderCommandReference.cs -- --self-test
dotnet run --file eng/knowledge/RenderCommandReference.cs -- --write
dotnet run --file eng/knowledge/RenderCommandReference.cs -- --check
```

When compiled CLI metadata changes, first regenerate the schema through its
owning generator's `--write`, then run these checks and regenerate the reference.
CI checks schema freshness before reference freshness and never rewrites output.
Synthetic renderer checks prove machine projection, invalid-shape rejection,
escaping and drift behavior, not the semantic accuracy of user instructions.

## Existing Repomix full export

The user-owned
[`.repomix/repomix.docs.config.json`](../../.repomix/repomix.docs.config.json)
is preserved unchanged. It still describes its broader full export to
`.repomix/repomix-docs.md`, including schemas, docs, assessments and configured
development paths with Git ignore handling. The new native commands do not
invoke Repomix, install it, replace this configuration, alter its existing
selection or read/rewrite the generated export.

That existing export and the native `audit` bundle are distinct: neither claims
to recover all Git revisions. The native tooling additionally inventories actual
agent entry points, intents/rules and tracked knowledge outside the older
Repomix selection, while enforcing its own active/private-work exclusions.
