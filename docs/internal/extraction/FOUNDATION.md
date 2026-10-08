# Configurator Repository Foundation

## Target-first authority

The MIT utility prepares bounded configuration artifacts offline. It does not
host Event services, apply configuration remotely or authenticate operators.
`AGENTS.md`, `PROJECTS.md` and the adapted intent registry govern this checkout.
The transferred knowledge library preserves generic engineering material;
Event-specific examples remain reference-only and cannot introduce requirements
for absent backend projects, HAL affordances, database providers or AGPL service
covenants. Links formerly targeting nontransferred Event policies route here.

## Build and tooling

`global.json` pins SDK 10.0.302 and Microsoft.Testing.Platform.
`Directory.Build.props` supplies deterministic builds, generated dependency
locks, strict warnings, .NET analyzers and MIT metadata. There are no inherited
warning suppressions. `Directory.Packages.props` carries only the product and
engineering dependency closure. Restore remains locked in CI; version changes
must regenerate locks with native dotnet restore.

Root configuration applies before solution registration. AgentWorkflow validates
single-document intent YAML and literal commit paths. ReleaseEngineering owns
Conventional Commit syntax, scope/trailer validation, bounded release inputs,
Change-Id allocation, deterministic presentation and signed evidence validation.
Missing promotion trust rejects signed publication; the project does not embed
Event's signer list.

## Knowledge library use

Load generic debugging, refactoring, research, accessibility, native tooling,
review and documentation skills only when they match the actual task. Preserved
backend-only skills are explicitly reference-only. Their examples explain the
source system; they do not describe the Configurator product.

Rules targeting Core, Artifacts, Presentation, browser and tests bind those
actual paths. Twin rules are byte-identical. Harness pointer files defer to the
root contract. No local MCP configuration, session state or credentials transfer.

## Verification and provenance

Engineering changes run engineering tests only. Source-derived tests that
require Event CI scripts/workflows are retained as reference and must be rebased
by the workflow owner before claiming the complete release suite passes.
No historical Event trust, publication receipt or changelog is installed.

See `transfer-manifest.json` for exact dispositions and `PROVENANCE.md` for the
Steward MIT direction, excluded example corpus and dependency rebuild evidence.
Existing design/strategy files remain user-owned and unmodified.
