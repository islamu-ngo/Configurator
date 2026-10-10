# Configurator Repository Foundation

## Target-first authority

The MIT utility prepares bounded configuration artifacts offline. It does not
host Event services, apply configuration remotely or authenticate operators.

Event's maintained integration now supplies a neutral `configurator.json` and one
compiled `definition.json`. The Event-owned profile is the sole family/revision
source; semantic field paths replace traversal numbering. Its public `release.json`
records the independently checked source/definition digests, compiler executable
pin and command/authoring/runtime contract versions. These are reproduction facts,
not transferred release authority. Configurator's checked snapshot and linked
browser/test resources require no Event checkout during a normal build.
`AGENTS.md`, `PROJECTS.md` and the adapted intent registry govern this checkout.
The transferred knowledge library preserves generic engineering material;
Event-specific examples remain reference-only and cannot introduce requirements
for absent backend projects, HAL affordances, database providers or AGPL service
covenants. Reusable policies retain their distinct target documents and source
anchors; this overview is not a replacement for governance, testing, operations,
release instructions or documentation architecture. Absent Event-only policies
are explicitly historical references, not disguised target requirements.

## Build and tooling

Maintainer authoring uses one closed YAML/JSON source and a pure Core compiler,
then existing canonical runtime admission. YAML's bounded dialect admits precise
JSON-shaped numbers and resolved string keys; unsupported/empty scalars and all
expansion authority reject. Native local loaders and Browser local selection share
this compiler before answers; remote Browser distribution remains canonical runtime
JSON. See [maintainer source ownership](../MAINTAINER_AUTHORING.md) and
[runtime contract](../CORE_DEFINITION_CONTRACT.md).

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

Local Arch Desktop development assembly lives in
`eng/configurator/package-desktop-arch.sh`, reuses standalone publication and
native makepkg, and has a focused isolated-pacman verification command. It
retains public notices/contracts, requires XWayland for Wayland sessions and
does not activate signing or release publication. See
[packaging ownership](../PACKAGING.md#native-arch-development-assembly).

## Knowledge library use

Load generic debugging, refactoring, research, accessibility, native tooling,
review and documentation skills only when they match the actual task. Preserved
backend-only skills are explicitly reference-only. Their examples explain the
source system; they do not describe the Configurator product.

Rules targeting Core, Artifacts, Presentation, browser and tests bind those
actual paths. Twin rules are byte-identical. Harness pointer files defer to the
root contract. No local MCP configuration, session state or credentials transfer.

## Browser preparation and custody

The browser is a complete configuration-preparation adapter over browser-safe
Presentation/Core. Standalone WebAssembly interactivity deliberately keeps
answer processing local and enables offline work; a larger initial download
and client resource use are accepted instead of Server/Auto answer custody.
Its public definition distribution boundary is distinct
from transient private answers. Secret input is disabled for each new workspace
until the operator explicitly consents. Revocation recursively clears secret
leaves and prepared output; edits and replacement invalidate old operations.
Complete composition remains strict. A secret-deferred draft instead identifies
completion locations and never claims complete readiness or target acceptance.

Bounded credential-free HTTPS or local JSON definition admission occurs before
answer collection and pins canonical identity. Remote origin and publisher
labels are not authentication. The PWA retains only public app assets and
explicitly selected admitted definitions; answers/output never enter caches,
requests or worker messages. Restricted copy/download deliberately transfers
custody to the operator and cannot establish native create-only/owner-only
protection. Native Artifacts and target authorization remain separate owners.
See [browser custody ADR](../adr/ADR-browser-session-trust-and-export.md),
[launch](../LAUNCH_CONTRACT.md), [presentation](../PRESENTATION_STATE.md) and
[packaging](../PACKAGING.md) for the corresponding source and evidence anchors.

## Verification and provenance

The browser's Event-style UI adaptation uses identified first-party originals
and separately licensed MudBlazor assets; exact source paths, revision/blob and
dispositions live in [Browser UI transfer](BROWSER_UI_TRANSFER.md). Optional
[definition-store content](../DEFINITION_STORE.md) is generated public data,
never an engine, native-client or independent-source dependency. Appearance
preferences contain only bounded display settings; browser route discard uses
editor-owned invalidation rather than hidden answer persistence.
The [browser interface anchor](../BROWSER_UI.md#interface-refinement) owns the
layered vendor import, shared control styling, responsive form composition and
independent-design research record. These presentation refinements do not move
configuration policy or answers into the shell.

Desktop verifies bundle composition before enabling protected save, disposes
the temporary restricted buffers, and recomposes the current revision at save.
Both single and bundle output enforce the host capability at the operation
boundary. See [Desktop custody](../DESKTOP_CLIENT.md).

Engineering changes run engineering tests only. Generic release tests use local
inert workflow/script fixtures; the verified fixture closure requires no Event
checkout and does not activate publication or import signer authority.
No historical Event trust, publication receipt or changelog is installed.

See `transfer-manifest.json` for exact dispositions and `PROVENANCE.md` for the
Steward MIT direction, excluded example corpus and dependency rebuild evidence.
Existing design/strategy files remain user-owned and unmodified.

## Faithful scaffold adaptation

The transfer preserves original ISLAMU-owned structure and reusable content,
changing only target names, actual owner/test paths, product/privacy boundaries
and licensing/publication facts. Thin summaries are not equivalent transfers.
The README retains the source project's community, maintainer, sponsorship,
acknowledgement and document-router structure; Event feature/service claims are
replaced by actual Configurator capabilities.

Original benchmark fields/context budgets, rule bodies, test discipline, generic
Markdown examples and contribution/finding workflows are preserved. Event-only
rules retain historical bodies with nonmatching activation paths. Target journal
assets contain their reusable templates/procedures, not Event finding history.

Contributor Covenant expression remains outside the first-party MIT grant.
The 154-file unlicensed Evolve corpus, Event production trust and credentials
remain excluded. Necessary differences are recorded separately from restored
reusable content in the scoped fidelity evidence.
