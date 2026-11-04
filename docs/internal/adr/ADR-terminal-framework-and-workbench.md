# ADR: Terminal framework and configuration workbench

> **Audience:** Contributors | Maintainers | AI agents
> **Kind:** Accepted decision
> **Status:** Official Terminal.Gui retained; workbench direction and qualification remain planned
> **Owner:** Terminal
> **Last Verified:** 2026-11-03
> **Source Anchors:** `Directory.Packages.props`, `src/Configurator.Terminal/`, `src/Configurator.Presentation/DocumentEditing/`, `src/Configurator.Browser/Components/Configuration/`, `docs/internal/TERMINAL_GUI_DEPENDENCY.md`
> **Review basis:** Product-owner decision and source-free capability handoff; Configurator source review at `97c5a1789545b73a9536a4b9d4fa934976afb16c`. No new runtime qualification.

## Decision and authority

Keep official Terminal.Gui v2, exactly pinned centrally, currently **2.5.0**.
The product owner explicitly accepted this recommendation on 2026-11-03 after
considering SharpConsoleUI. Do not switch toolkits, revive a fork or introduce
a generic abstraction over both toolkits.

The [dependency decision](../TERMINAL_GUI_DEPENDENCY.md) remains authoritative
for the official-package migration, component terms, notices, hashes and upgrade
process. This record explains the renewed framework choice and product direction;
it changes no dependency, distribution permission or release authority. Normal
compatibility, dependency-closure and host checks remain necessary. Terminal.Gui
2.5.0 has breaking changes; a stable label does not establish compatibility.

Acceptance applies to framework selection. It does not approve an exact layout,
authorize an implementation workstream, establish delivered feature parity,
select provider SDKs or grant secret-store read/write authority.

## Product direction

The adoption priority is a project's GitHub README **Configure** link opening
static Blazor WebAssembly. Browser-first development supports this low-friction
entry point. The TUI direction remains a full, long-lived configuration workbench
for operators and developers working locally or through remote shells.

That workbench should make large definitions understandable: nested typed forms,
field-specific controls, compact/comfortable presentation, descriptions and
consequences, validation/correction, deliberate secret generation/reveal/copy,
local import, configuration comparison and protected save. Developer text editing
and richer schema comparison need further design. Shared meaning matters more
than reproducing the Browser's physical layout.

A possible workspace has navigation, a central form/editor, contextual help or
comparison and persistent status. This is a direction to evaluate, not an
approved screen specification. Adaptive collapse must keep focused fields and
actions reachable on small terminals. Keyboard access and useful focus behavior
remain requirements throughout.

## Comparison and tradeoffs

The released baselines are Terminal.Gui **2.5.0** and SharpConsoleUI **2.10.5**,
observed on 2026-11-03. Both support large applications and provide control trees,
forms, tables, trees, tabs, editors, file pickers, UI dispatch/background work
and headless test facilities. Neither supplies Configurator's typed recursive
renderer, semantic comparison, secret custody or protected writer automatically.

| Concern | Terminal.Gui for Configurator | SharpConsoleUI alternative |
|---|---|---|
| Forms | Released controls are adequate building blocks around shared typed paths. | Higher-level `FormControl` hints, sections and validation reduce presentation assembly; named-string values still need Configurator's typed tree. |
| Tables/trees | Controls support inventories and navigation, with application-specific composition. | Integrated `TableControl`/`TreeTableControl` sorting, filtering, inline editing and virtual sources are attractive for larger inventories. |
| Binding | Existing adapter explicitly refreshes from shared state. | `INotifyPropertyChanged` can reduce refresh plumbing; it does not replace immutable answer authority. |
| Text editing | Toolkit editor building blocks exist; no terminal document editor is implemented. | `MultilineEditControl` syntax, gutters, find/replace and undo/redo reduce editor assembly. |
| Async work | Released `IApplication.Invoke` already works with application lifetime checks. | Dispatch/background-work facilities exist; application cancellation and stale-result rejection remain necessary. |
| Maintenance | Substantial adapter, tests and safeguards remain; broader contributor history lowers dependency concentration. | Smaller contributor base concentrates maintenance risk; migration requires rebuilding and qualifying application safeguards. |

SharpConsoleUI's higher-level controls are meaningful benefits, especially for
editing and tabular review. Terminal.Gui already covers the required workflow
building blocks, and no demonstrated requirement forces a switch. Preserving
the adapter is useful engineering value rather than the sole rationale. A
rewrite must earn its cost by resolving a real blocker.

Contributor history is a maintenance observation, not a guarantee of response
time or correctness. Terminal.Gui v1 history is not proof of v2 maturity. This
assessment establishes no performance winner, superior screen-reader support
or more reliable threading. Those claims need comparable measurements or
actual assistive-technology evidence.

### Upstream observations and release boundaries

SharpConsoleUI's released roadmap records remaining work on oversized centered
windows opening blank, nested editor scroll-to-cursor behavior, portal key
routing, layout invalidation and a dedicated diff composite. These are upstream
statements, not failures reproduced in Configurator or proof of affected flows.

Terminal.Gui also has relevant reports. Issue 5743 concerns an async continuation
after UI disposal. Issue 5666 reports a Windows shutdown hang on **2.4.17**;
it does not establish the same failure on 2.5.0. Neither was reproduced in this
assessment. Real application lifetimes need qualification with either toolkit.

Research also inspected development snapshots: Terminal.Gui develop
`925cca7b47596af47fd8bf7ae52b7fabf171c4d4` and ConsoleEx master
`090f1477c375458a1cb7d160ef9d6bce7eea08f5`. Their capabilities are not the
released baseline. In particular, richer `InvokeAsync` session cancellation on
Terminal.Gui develop must not be assumed available in pinned 2.5.0. Feature
documentation is capability evidence, not a tested Configurator workflow.

## Current behavior and planned depth

| Area | Current source-observed behavior | Direction or remaining boundary |
|---|---|---|
| Forms | `TerminalWindow` presents one selected field; `TerminalFieldEditor` handles nested objects, lists, maps and variants through shared operations. | Evaluate richer navigation, field-specific controls and density without another answer tree. |
| Guidance | Selected-field help, validation and correction navigation exist. | Improve sustained work on the largest real definitions; qualify usability. |
| Secrets | Masked entry, generation/derivation, guarded reveal/copy and clearing exist. | Preserve custody in every panel/editor; no universal string/clipboard erasure promise. |
| Local files | Bounded import, read-only comparison, deliberate adoption, preparation and create-only protected save exist. | A file picker only selects a path. Import is local I/O, not upload. |
| Developer text | No terminal whole-document editor. Browser has richer rendering/density and developer-editor integration source. | Reuse shared public-scope document ownership where applicable; source inspection does not complete Browser qualification. |
| Schema comparison | Imported configuration/artifact is assessed against the selected admitted definition/current workspace. | Arbitrary schema-to-schema diff, rename inference and migration planning are not current capabilities. |
| Providers | No native secret-store connector. | Opt-in provider operations require separate design and qualification. |

`DefinitionDocumentSession` edits public-scope **answer projections**: a canonical
answer envelope or bounded Environment representation. It does not edit arbitrary
admitted definition/schema bytes. Parse produces a detached candidate; Apply
rechecks session identity, digest, revision, privacy generation and cancellation
before one accepted-state commit. Future terminal editing should reuse this
contract where applicable. Schema authoring needs separate admission and must
not mutate the frozen definition identity underneath active answers.

## Ownership the framework must preserve

Native interactive clients depend on Presentation and Artifacts, which depend
on Core. Browser depends only on browser-safe Presentation and Core.
Core owns bounded admission, validation, local secret algorithms and composition.
`DefinitionFormModel` and `PresentationWorkspace` own typed paths, immutable
snapshots, accepted answers, revisions, operation generations and cancellation.
Terminal owns controls, focus, rendering, dispatch and transient input.

UI dispatch does not cancel work or prove results current. The adapter checks
lifetime and generation again inside callbacks and rejects late results after
edits, replacement or close. These safeguards must survive UI expansion.

`SecretTextField` uses a separate `TerminalSecretBuffer`, masks by default and
blocks ordinary copy/cut/paste, undo/redo, context and history paths. Reveal and
copy are separate guarded actions. Owned buffers can be cleared; immutable
strings, system clipboard history and forced termination cannot receive a
universal zeroization guarantee.

Artifacts owns protected writing/publication. On qualified Linux hosts, commits
are create-only; conflicts do not overwrite. Unsupported protected-save hosts
reject without ordinary-file fallback. Installed files and partial outcomes
remain real after cancellation. Targets retain semantic acceptance and apply
authorization. A framework file picker cannot supply any of these guarantees.

## Native provider direction

The product-owner direction includes eventual opt-in native REST operations for
**Infisical** and **Azure Key Vault**: configure, compare, update and verify
secrets. These names record intent, not selected SDKs, implemented support or
provider write authority. The offline/local engine remains the baseline.

Follow the existing [secret-store backlog](../../../dev/backlog/universal-configurator-followups.md#secret-store-inspection-and-completion).
One concrete provider pilot precedes abstractions. Intake must separate value-free
inventory from content validation and establish credential custody, authentication,
permissions, deliberate conflict-safe writes, partial outcomes, cancellation,
lifetimes, redaction and recovery. No HTTP client or native/provider authority
enters Core or Browser. Provider operations do not imply target administration
or application apply permission.

## Alternatives and reconsideration

Switching now could reduce presentation assembly but would replace a working
adapter without an established blocker. Keeping both behind a generic wrapper
adds another UI contract without resolving that uncertainty. A fork restores
downstream maintenance retired by the official-package decision. None is selected.

Reconsider when a representative released-version workflow demonstrates a
material Terminal.Gui blocker that cannot reasonably be fixed in the adapter
or through an upstream upgrade. Run the same vertical slice on a released
alternative and prove improvement. Include migration cost and preserve typed
semantics, custody, protected save and lifetime safeguards. Feature counts or
untested development APIs do not suffice.

## Qualification for the next implementation increment

Use the largest representative real definition for one complete vertical slice:

- Nested objects, lists, maps and variants; help, validation and correction.
- Secret mask/reveal/generation/copy/clearing, with no unintended disclosure.
- Local import, comparison, deliberate adoption, protected save and conflicts.
- Public-scope editor parse/cancel/Apply through the shared document owner.
- Edits, replacement and close during pending work; late results are dropped.
- 80×24 and larger terminals, resizing and keyboard focus reachability.
- Local Linux and SSH, plus actual Windows/macOS for any claimed hosts.
- Mixed Arabic/English, combining/wide characters, focus and assistive tools.
- Measured latency, memory and terminal output before performance claims.

Use a fake provider for future lifetime checks, without real credentials.
No interactive demos, benchmarks or product tests ran for this assessment.
Existing Linux PTY observations and limits remain in [Terminal](../TERMINAL_CLIENT.md)
and [Operations](../OPERATIONS.md); this decision does not renew them.

## Provenance and independent authoring

This documentation was authored in a fresh source-free context from functional
observations, interface facts and first-party material. No third-party code,
implementation, prose or assets were copied. Abstraction covers operator goals
and workflows; filtration separates commonplace controls/platform interfaces
from discretionary design. Organization and workflow ownership derive from
ISLAMU's bounded model, shared state and native protection contracts. Suggested
panes are a product evaluation direction, not an upstream application recreation.

No dependency or license changed. Existing dependency reviews remain in force.
Independent documentation review on 2026-11-03 passed after correcting the
native/Browser dependency distinction. Review checked first-party ownership,
current/planned boundaries and independent authoring; the native Markdown checker
accepted all seven changed documents and 160 local links with zero errors.
This is documentation evidence, not a licensing opinion or runtime receipt.

## Source register

Research access date: **2026-11-03**, supplied through a sanitized functional
handoff. Links identify release/interface evidence; this authoring context did
not reopen third-party source or copy upstream documentation.

- Terminal.Gui: [2.5.0 release](https://github.com/tui-cs/Terminal.Gui/releases/tag/v2.5.0), [NuGet package](https://www.nuget.org/packages/Terminal.Gui/2.5.0), [released views](https://github.com/tui-cs/Terminal.Gui/blob/v2.5.0/docfx/docs/views.md), [multitasking](https://github.com/tui-cs/Terminal.Gui/blob/v2.5.0/docfx/docs/multitasking.md).
- Reports: [disposed-UI continuation, 5743](https://github.com/tui-cs/Terminal.Gui/issues/5743), [Windows shutdown on 2.4.17, 5666](https://github.com/tui-cs/Terminal.Gui/issues/5666).
- SharpConsoleUI: [2.10.5 release](https://github.com/nickprotop/ConsoleEx/releases/tag/v2.10.5), [NuGet package](https://www.nuget.org/packages/SharpConsoleUI/2.10.5).
- Form/table interfaces: [FormControl](https://github.com/nickprotop/ConsoleEx/blob/v2.10.5/docs/controls/FormControl.md), [TableControl](https://github.com/nickprotop/ConsoleEx/blob/v2.10.5/docs/controls/TableControl.md), [TreeTableControl](https://github.com/nickprotop/ConsoleEx/blob/v2.10.5/docs/controls/TreeTableControl.md).
- Editor/host interfaces: [MultilineEditControl](https://github.com/nickprotop/ConsoleEx/blob/v2.10.5/docs/controls/MultilineEditControl.md), [syntax highlighting](https://github.com/nickprotop/ConsoleEx/blob/v2.10.5/docs/SYNTAX_HIGHLIGHTING.md), [threading/async](https://github.com/nickprotop/ConsoleEx/blob/v2.10.5/docs/THREADING_AND_ASYNC.md), [drivers](https://github.com/nickprotop/ConsoleEx/blob/v2.10.5/docs/DRIVERS.md).
- Remaining work: [released roadmap](https://github.com/nickprotop/ConsoleEx/blob/v2.10.5/ROADMAP.md).
- Maintenance observations: [Terminal.Gui contributors](https://api.github.com/repos/tui-cs/Terminal.Gui/contributors?per_page=100), [ConsoleEx contributors](https://api.github.com/repos/nickprotop/ConsoleEx/contributors?per_page=100); date-scoped, not service guarantees.
