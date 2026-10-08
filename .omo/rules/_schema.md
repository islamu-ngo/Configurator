<!-- ABOUTME: Schema for path-scoped rule files under .agents/rules/. -->
<!-- ABOUTME: Twin copy at .omo/rules/_schema.md. When modifying this file, update both paths. -->

# Rule File Schema

## Purpose

Path-scoped rules supplement the intent contract. `.agents/rules/` is explicitly
routed by the root/intent contract; OmO auto-loads the physical `.omo/rules/`
twins through matching YAML `paths:` globs. Claude Code native discovery uses
`.claude/rules/`, not this directory. See [README.md](README.md) for harness
discovery, ranking, budgets and the no-symlink contract.

## Required YAML Frontmatter

| Field | Type | Requirement |
|---|---|---|
| `name` | string | kebab-case; usually matches filename |
| `description` | string | one sentence saying when the rule applies |
| `paths` | string[] | valid target globs; inactive historical rules use only reserved `reference-only/<name>/**` with no matching files |
| `related_skills` | string[] | existing skill names only |
| `related_docs` | string[] | existing repo docs only |
| `minimum_tests` | string[] | existing test project names; empty for owner-selected/cross-cutting or inactive rules |
| `related_intents` | string[] | ids from `.agents/contract/intents.yaml` |

## Body Sections

Retain the original rule's structure rather than rewrite it to satisfy a
synthetic template. Constraint-list rules (`tests`, `blazor-client`) use
`## Applies To`, `## Path-Specific Constraints`, `## Must Read`,
`## Verification` and `## Related`. Matrix rules use the structure below.
Invariant-table rules (`privacy-and-pii`) retain their original `## Applies To`,
`## Critical Rules & Invariants`, `## Must Read`, `## Verification` and
`## Related` headings. Extra owned constraints do not replace that structure.

1. `# Title`
2. `> **Applies to:** ...`
3. `> **Authority:** ...`
4. `## Rules (Correct / Wrong)` with a table
5. `## Must-Reads for This Path`
6. `## Anti-Patterns (Forbidden on These Paths)`
7. `## Verification`
8. `## Related`

Inactive historical rules instead have a prominent reference-only scope,
reserved nonmatching glob, empty active skills/tests/intents, source revision
and the verbatim original body inside a fenced Markdown block. Historical
links and commands inside that block are not live target routes. Never activate
their absent Event layer to satisfy this schema.

## Rules Table Contract

- Use columns: `#`, `Rule`, `Correct`, `Wrong`.
- Keep 5-10 rows.
- Source rows from governing docs and skills.
- Cross-reference `docs/internal/QUICK_REFERENCE.md`; do not paste large invariant blocks into rule files.

## Authoring Limits

- Keep each path-scoped rule short and surgical.
- Prefer tables over long prose.
- No ASCII architecture diagrams.
- Keep examples minimal or omit them.
- All 17 pairs, including README/schema, remain byte-identical physical files.
- `related_docs`, skill names, intent IDs and named tests must resolve in the
  actual target; route generic policies to their target technical anchors,
  not `docs/internal/extraction/FOUNDATION.md`.
