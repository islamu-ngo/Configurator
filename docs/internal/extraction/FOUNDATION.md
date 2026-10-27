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

The CLI's [command v2 contract](../CLI_CONTRACT.md) replaces command v1 only;
authoring/runtime v1 remain unchanged. Repeated bundle `--artifact` selection and
count/digest summaries keep the full output ledger within a 65,536-byte UTF-8
receipt. The CLI plans receipt capacity before publication and preserves written,
not-attempted and unknown outcomes through later failures. One application emitter
attempts delivery without Program fallback; cleanup and target acceptance remain
independent. See the [runtime decision](../adr/ADR-cli-command-runtime.md).
Public authoring custody, discovery/repair, portable skill lifecycle, actual-host
qualification and installed adoption remain open, not enterprise certification.

`global.json` pins SDK 10.0.302, the tested WebAssembly workload set and
Microsoft.Testing.Platform. Native-only and public-data CI jobs initialize the
pinned manifests with `dotnet workload update` before restoring projects; SDK
installation alone does not supply the selected workload set.
Browser Release publication uses AOT compilation;
Debug remains interpreted. CI and the Browser container build install the pinned
`wasm-tools` workload explicitly, with Python available on Linux builders.
The Browser project owns the Release setting; no new package reference or
alternative dependency graph is introduced. The release owner still emits
component notices and public package metadata. The larger download/cache cost
and actual runtime evidence belong in Operations.
`Directory.Build.props` supplies deterministic builds, generated dependency
locks, strict warnings, .NET analyzers and MIT metadata. There are no inherited
warning suppressions. `Directory.Packages.props` carries only the product and
engineering dependency closure. Restore remains locked in CI; version changes
must regenerate locks with native dotnet restore.

Terminal.Gui now uses the official pinned NuGet package with reviewed embedded-
asset notices. The temporary downstream archive/feed, patch, probe and rebuild
lane transferred during extraction were retired on 2026-10-13. See the
[dependency decision and history](../TERMINAL_GUI_DEPENDENCY.md); historical
transfer manifests and verification entries describe the earlier state.

Root configuration applies before solution registration. AgentWorkflow validates
single-document intent YAML and literal commit paths. ReleaseEngineering owns
Conventional Commit syntax, scope/trailer validation, bounded release inputs,
Change-Id allocation, deterministic presentation and signed evidence validation.
Digest-bound, append-only group corrections repair incompatible membership
without mutating original fragments, identities, impacts or release entries.
Candidate and authorized-inventory verification include the applicable committed
correction documents in source provenance. Reserved classification markers
remain prohibited independently of ordinary descriptions of restricted output.
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
Typed presence preserves explicitly supplied empty strings, admitted empty enum
choices and empty containers independently of draft text. Explicit omission is
distinct from those values. Deliberate invalid or same-value edits fence pending
work; programmatic refresh and untouched native navigation do not supply values.
Shared experience/disclosure/discovery state has its own notifications and cannot
grant consent, change answers or retire current preparation. Invalid nested drafts,
literal correction paths, provenance and workspace row identity belong to
Presentation/Core, rather than independent Browser or native visibility policies.
Preparation now composes a bounded Core bundle once and adopts its emitted
artifacts through disposable Presentation operations. Browser stages all owners
before publishing current output; it does not discard and recompose a validated
bundle. Never-started, stale and rejected adoption storage has an explicit cleanup
owner. Canonical bytes, aggregate rejection and incomplete draft/withholding
semantics remain Core contracts. This removes duplicate work without qualifying
the separate responsive-preparation budget.
An explicit shared disclosure override separates optional detail from authored
experience selection; null follows the authored level. Visibility and expansion
use that same effective level. Authored experience/section descriptions remain
public projection metadata. Correction may explicitly preserve the one current
discovery query/facets without advancing input revision or granting consent.
The [workspace presentation decision](../adr/ADR-browser-workspace-presentation.md)
keeps thin text/choice controls over that shared state, fences retired rendered
callbacks by mounted-control generation, and separates session density from
stored appearance. Owner-qualified application-bar search and contextual public
metadata inspectors hold no answer snapshots. Their actual runtime limits are
recorded in Operations; component tests alone do not qualify the surface.
Large Browser sections retain connected controls and progressively mount newly
editable roots through `ConfigurationSectionFields`. Form identity, operation
generation and cancellation fence every continuation. Busy state remains until
all current roots mount; correction may prioritize its destination. This is a
Browser rendering mechanism, not another evaluator or answer store. List rows
reuse admitted item metadata while preserving enclosing masking and required
state; new input revisions still refresh every mounted callback fence.
Admitted `SetupFieldDefinition` instances materialize immutable public field
identity, display/required/disclosure/sensitivity metadata, defaults, choices,
condition locations and ordered dependencies once. `DefinitionValidator` still
evaluates current answer-dependent
conditions and validates every revision; this metadata does not retain answers
or introduce a second evaluation authority.
The shared form owns a metadata-only definition index and one current input
revision's private derived facts. Disclosure/search reuse those facts; selected
experience order remains a separate dependency of ranked discovery. Read-only
lookup traverses already-owned immutable JSON, never an additional answer tree
or a reparsed whole-root copy, and invalid roots deny stale collection reads.
Correction promotion settles after nested edits and before post-lock observers,
not as a side effect of visibility reads. Shared relevant Review entries retain
active obligations, attention/deferred/supplied/defaulted work and literal-path
ancestors; deliberate metadata inspection does not activate answers.
Revision changes and disposal retire facts and current selection references.
Managed reference retirement does not promise physical JSON/string erasure;
workspace-owned prepared-output bytes are separately cleared on invalidation.
Browser denial takes effect synchronously, retiring every old artifact owner
before asynchronous cleanup can yield; cleanup cannot restore denied secrets
or clear replacement output.
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

The optional Browser Dockerfile wraps that same static publication owner and
serves its complete output through a pinned non-root host. It adds no
server-side answer processing, target transport or private persistence.
[Container packaging](../PACKAGING.md#browser-container) owns build-context
admission, optional public Store inclusion and image verification; production
hosting remains an operator action.

## Verification and provenance

The [documentation hub](../../README.md) routes readers by task into public
procedures and technical contracts. The [documentation architecture](../DOCUMENTATION_ARCHITECTURE.md)
owns their parity matrix and documentation-only review checks. Guides distinguish
public authoring/runtime definitions from private answer workspaces, and package
production from native host/protected-save qualification; historical transfer and
review reports remain evidence, not current product guarantees.

The [Terminal CTO review](../TERMINAL_CTO_REVIEW.md) records the point-in-time
2026-10-14 assessment of terminal interaction, security, maintainability and
qualification, with executed checks and reproduced failures. It is an
assessment, not a replacement for the client or shared-state contracts.

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
boundary. The desktop adapter uses injected loader, form and publisher viewmodels
with compiled declarative field templates; the root coordinates operation leases
and cancellation without moving answer or publication policy into the view.
See [Desktop custody](../DESKTOP_CLIENT.md).

Native publication results distinguish historical installation, immediate
verification and cleanup. Linux records successful namespace publication before
post-inspection; CLI and native UI receipts retain that fact after verification
failure without promising continued destination integrity or rollback. The
[protected-artifact contract](../PROTECTED_ARTIFACTS.md) owns these invariants and
the direct result/machine-contract replacement; the change does not qualify new
hosts or resolve the separately tracked Desktop operation-buffer settlement risk.

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
