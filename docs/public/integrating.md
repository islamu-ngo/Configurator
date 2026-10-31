---
description: "Adopt portable bounded-v2 definitions beside your application, without a Store or GitHub dependency."
---

# Adopt Configurator beside your application

> **Audience:** Adopters | Integrators
> **Status:** Qualified pre-release workflow
> **Owner:** Core
> **Last Verified:** 2026-10-28
> **Source Anchors:** `src/Configurator.Cli/SetupCliDefinitionHandlers.cs`, `docs/internal/CORE_DEFINITIONS.md`

Maintain one public `configurator.yaml` or `configurator.json`, compile it to
canonical runtime JSON, and keep private answers separate. You can distribute
both public documents with your application on local media. Neither GitHub,
the Definition Store, a Configurator account nor an Event service is required.
Configurator prepares files offline; your application remains responsible for
accepting and applying them.

Use the [definition reference](definition-reference.md) for exact grammar,
the [cookbook](definition-cookbook.md) for supported patterns, and the
[maintainer guide](integrate-your-tool.md) for detailed authoring and import
background. The [standard](definition-standard.md) and internal
[conformance profile](../internal/DEFINITION_CONFORMANCE_PROFILE.md) distinguish
contract support from scenario, package and physical-host evidence.

## Keep the four versions separate

Maintain the public authoring source, compile the public runtime, collect private
answers, and inspect machine receipts as four different documents. Their
[current versions and discriminators](definition-reference.md#which-document-are-you-editing)
are independent; machine receipts contain operation facts rather than private
artifact bytes.

The current check/build handshake is authoring/runtime **2/2**. Family revision
and exact target application version are separate again. Revision 2 does not
mean a different runtime format. No v1 definition fallback or translation alias
is supplied. Never distribute private answer files as public source or assets.

## Start with a fresh native identity

Run these commands in your own project with the standalone
`islamu-configurator` executable:

```bash
islamu-configurator definition init \
  --project-id example.service \
  --name "Example Service" \
  --application-version 1.0.0 \
  --machine
islamu-configurator definition schema --kind authoring --text
islamu-configurator definition check --source configurator.yaml --machine
islamu-configurator definition lint --source configurator.yaml --machine
islamu-configurator definition build --source configurator.yaml --machine
```

`init` allocates a fresh family UUID and revision 1. Edit the generated public
source before check/build: declare exact names, recursive types, required sets,
inherited sensitivity, public guidance and target output mappings. Lint adds
advisories after admission; it does not replace check or target validation.
Schema discovery returns built-in schemas (`authoring`, `runtime`, `answers`,
`command`), not arbitrary schema locations. Build normally creates
`dist/configurator/revisions/1.json`; existing conflicting content is not
overwritten.

To adapt a maintained source instead of starting empty, fork it:

```bash
islamu-configurator definition init \
  --fork-from upstream/configurator.yaml \
  --output configurator.yaml \
  --machine
```

Fork takes an admitted authoring source and creates a new family at revision 1.
Keep its source format; it is not a format converter. Do not copy a fixture's
family UUID, edit generated runtime identity by hand, or treat publisher text
as authentication.

For a change within your existing family, retain the previous canonical runtime
anchor and edit the source without manually advancing its revision. Then:

```bash
islamu-configurator definition revise \
  --source configurator.yaml \
  --output candidates/configurator.next.yaml \
  --machine
```

Revise compares the source's canonical content to its current revision anchor;
`--against` can select an explicit prior runtime. Family and revision must agree.
Changed content produces a create-only next-revision candidate, preserving the
source; unchanged content does not advance revision. Review and adopt that
candidate as your working source, then build the new revision. An admitted
family/revision cannot later identify different canonical content, even after
registry removal. Workspaces remain pinned to their admitted revision and digest.

## Use the supported data model

Start with the [maintained source/runtime pairs](../../examples/README.md).
The cookbook covers exact literal names, nullable typed values, omission/reset,
arrays, typed maps, closed discriminator branches, local references, explicit
format assertions, and localized public help. These use the same Core admission
and effective-value resolver, not client-specific schema dialects.

Public `default` is descriptive, `initialValue` initializes eligible missing
locations, and `suggestedValue` requires an explicit user action. Requiredness
does not create absent containers. Public metadata must not contain private
defaults, examples, tokens or credentials; missing sensitivity is sensitive,
and inherited secret floors cannot be lowered by a child label.

JSON/YAML bindings map concrete target pointers to explicit root/item scoped
source references. Dotenv bindings use literal variable names. Choose bindings
or a projection, not both; JSON/YAML with neither copy the effective root.
Markdown uses bounded string-list projections, not executable templates.
`targetUpdateMode` and `restartRequirement` describe target expectations; both
default to `unspecified`. They never authorize filesystem paths or applying data.

## Scaffold a public schema deliberately

`definition init --from-env-example .env.example` retains variable names, not
example values. Review the inferred types and sensitivity before distribution.
Do not point it at real private environment files.

For a separate public application JSON Schema, use `--from-schema schema.json`.
Add `--accept-bounded-semantics` when acknowledging the importer's encountered
numeric-domain/nullable-branch conversions. This is not a bypass for unsupported
assertions. Review the structured transformation ledger: retained, converted,
advisory, discarded and unsupported concepts have bounded public locations and
document ordinals. Defaults/examples do not become initialization; collected
leaves remain sensitive and `writeOnly` ancestry remains secret.

Supplied local schema references are bounded and offline. There is no arbitrary
remote reference fetch, general JSON Schema evaluator or executable plugin.
See [schema import limits](definition-reference.md#json-schema-import).

## Choose an adapter and qualify your workflow

| Adapter | Definition and answer custody | Output boundary |
|---|---|---|
| [Machine CLI](cli.md) | Explicit local source/runtime and private answer file or redirected input; bounded machine receipts | Restricted artifact output through native save, never answers on stdout |
| [Terminal.Gui TUI](terminal.md) | Local source/runtime admitted before transient typed editing | Deliberate native protected export |
| [Avalonia Desktop](desktop.md) | Local source/runtime and shared typed workspace; explicit reload admits a replacement | Deliberate native protected save |
| [Blazor WebAssembly Browser](browser.md) | Local selection can compile source; remote selection admits and pins canonical runtime before answers | Deliberate restricted copy/download, not native protected save |

The interactive adapters use shared Presentation editing over Core's immutable
schema and snapshots. Typed choices, maps and provider branches are declared
editors, not an opaque arbitrary JSON editor. Unsupported mandatory capabilities
must refuse before answers; optional layout can fall back to reachable controls.
Shared implementation is not proof of every renderer, keyboard path or package.

Native protected writes are create-only on supported verified Linux hosts;
unsupported protection refuses without ordinary-file fallback. Browser answers
and artifacts remain transient, excluded from public caches and application
requests. Secret entry needs per-workspace consent; revocation clears secret
leaves and prepared output. A secret-deferred draft is incomplete, not readiness.

## Keep distribution and acceptance independent

Bundle canonical runtime locally, serve it from an admitted credential-free
public HTTPS origin for [browser launch](launch-integration.md), or optionally
contribute it to the [Store](definition-store.md). The Store is discovery and
curation, not a prerequisite for local adoption. Remote Browser loading does
not accept arbitrary authoring, private answers or launch-time executable logic.

Before shipping your integration, check and compile the public source, exercise
synthetic complete and secret-deferred flows, and have the target validate the
prepared files through its real startup/import path. Record separately:

- Source admission and canonical source/runtime agreement.
- Answer validation, output bytes and supported inverse-import behavior.
- Actual adapter/package/host observations.
- Target semantic acceptance, operator authorization and apply/restart outcome.

Only the target owns the last item. File generation does not authenticate an
operator, prove connectivity, install services, approve legal drafts or apply
configuration. The commands above are workflow instructions. Supported Linux
x64 and published Browser qualification is recorded in
[Operations](../internal/OPERATIONS.md); it does not close target, stakeholder,
translation or other-host evidence gaps.
