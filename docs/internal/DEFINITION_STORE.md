# Definition store engineering contract

> **Audience:** Maintainers and contributors
> **Owner:** Native definition-store builder; Browser discovery adapter
> **Source Anchors:** `eng/configurator/BuildDefinitionStore.cs`, `store/`, `schemas/definition-store-curation-v1.schema.json`
> **Status:** Optional static discovery; no publisher authentication or host authority
> **Last Verified:** 2026-10-28

## Ownership and flow

`BuildDefinitionStore.cs` is a native file-based .NET tool referencing Core only.
It parses one root-confined JSON or YAML source through
`DefinitionAuthoringCodec` and `DefinitionAuthoringCompiler`, admits historical
v2 JSON releases through `SetupDefinitionCodec`, verifies project application/family agreement and uses
`SetupDefinitionRegistry` for global same-revision conflicts. It then derives a
deterministic index and canonical public snapshots. Core does not depend on
store curation, Browser or UI packages. Discovery does not evaluate definitions
or replace canonical admission.

The input root may be another checkout. Each immediate project folder contains
exactly one `configurator.json`, `configurator.yaml` or `configurator.yml`,
optional `curation.json`, and generated `releases/<revision>.json`. A root
`README.md` is allowed. Root and descendant symlinks,
escaping/absolute paths, undeclared files, duplicate members, unknown members and
duplicate IDs reject. Files are read with a bounded stream rather than trusted
file-length allocation. Every source is admitted before output is created.
Output cannot overlap the source and existing symlinks reject before writes.
The tool assumes a stable checkout during generation; it is not a protected
native artifact writer and does not claim resistance to a concurrently hostile
filesystem. Answer-derived artifacts continue to belong to native Artifacts.

## Authoring, curation and bounds

The source's project declares `id`, `name`, `description`, `repository` and
`definitionLicense`; its shared compiler owns the field/output vocabulary and
runtime admission. Store redistribution requires the last three declarations
even when an independently used local source does not need them. Slugs derive
from the lowercase project ID by splitting dot/underscore/hyphen separators
and joining with hyphens. Collisions reject rather than allocate hidden IDs.

Optional store-owned curation has `schemaVersion: 1`, optional `tags`, and an
optional paired `slug`/`slugReason` override. An exception matches the folder
and requires a trimmed, control-free reason of 16–512 characters. Curation
cannot provide project/license/history metadata. Its closed schema is generated
by the builder. Runtime validation additionally enforces filesystem confinement,
uniqueness, source admission, identities and public-data limits.

| Member or collection | Bound |
| --- | --- |
| Curation document | 16 KiB; JSON depth 4; duplicate/unknown members reject |
| Project slug | 1–64 ASCII lowercase letters/digits/hyphens; no leading/trailing hyphen |
| Name / description / license | 128 / 2048 / 128 characters; trimmed, nonempty, control-free |
| Source repository | 2048 characters; absolute public HTTPS shape; no credentials, query or fragment |
| Tags | 0–16 unique lowercase slugs, 1–32 characters each |
| Historical files | Direct `releases/<positive-canonical-integer>.json` filenames only |
| Projects / revision entries | 1024 / 4096 total; at most 64 per project |
| Source / definition bytes | 4 MiB; Core's own nesting/vocabulary/field limits also apply |
| Generated index | 4 MiB; explicit rejection, never truncation |

License is public attribution text, not a machine-proven license approval.
Maintainers must verify distribution authority and terms. Repository links are
not fetched by the builder. Core rejects protected public literals and
initialization/suggestions covering protected descendants. The builder also
rejects choices on effective secret nodes, traversing declared properties, list
items, typed map values and every provider branch. It uses Core's
`SetupFieldDefinition.SensitivityOf` for declared sensitivity and retains inherited
floors; sensitive enum vocabulary can remain public metadata.
Human review must still inspect prose, public
literals and purportedly public values: admission cannot classify every secret.
No private answers, executable hooks, imported image/font assets or install
commands enter this format.

## Generated contract

`--source <root> --output <directory> --write` generates `index.json` and
`definitions/{projectId}/{revision}.json` from the union of current compiled
source and admitted history. It creates missing current releases with create-only
writes and leaves existing release bytes unchanged. The published snapshots
are Core canonical bytes, with no appended newline. `--check` regenerates in
memory and compares every expected byte, current-release presence and the
complete output inventory. Unexpected output fails. Schema output
defaults to `schemas/definition-store-curation-v1.schema.json`; `--schema <path>`
selects another schema destination. The schema is separate from published data.
No timestamps, local paths, answers or publication receipts appear in output.

Historical files must match the source's application/family identity. Reusing
an admitted revision with changed canonical content rejects, as does assigning
one family to several store projects. Revision advancement belongs to the
shared maintainer source workflow, never an inferred store-specific identity.

`--baseline <inventory>` reads a separately supplied generated `index.json`
and its public canonical snapshots. It admits bounded, unlinked files, checks
the closed index member vocabulary, positive revisions, exact snapshot paths,
digests, canonical bytes, complete file inventory and family/application/project bindings, then requires
every historical snapshot to survive unchanged in the candidate output. The
baseline cannot overlap input, output or schema destinations. Baseline metadata
does not replace current source metadata. `--require-baseline` rejects omission;
local packaging without it establishes admission/freshness, not trusted history.

### Pre-release v2 generation boundary

The approved standard cutover retires v1 active inputs without reassigning their
canonical identities. Their exact historical content remains in Git.
`Configurator.DefinitionSchemaGenerator --renew-store-families` derives the two
checked store sources from the admitted v2 examples, carries public distribution
metadata, and invokes `DefinitionAuthoringIdentityEditor.Fork` for fresh families.
It validates the four named v1 snapshots before writes, admits all candidates,
creates fresh revision 1 releases and then retires only the named v1 files.
This is a one-time source-generation operation, not a runtime compatibility
interpreter or a baseline bypass.

The ordinary baseline validator is unchanged: it refuses missing, rewritten,
rebound or unsupported history. An old v1 inventory is not a promoted v2 history
authority. Publication trust activation and remote release remain outside this
local cutover.

The native file app explicitly enables its JSON reflection runtime setting to
match the existing Core/JIT serialization contract. SDK file-app defaults
otherwise caused `authoring-invalid` before the first synthetic fixture could
be admitted. Core serialization was not replaced to work around that host
setting. The executed v2 self-test has 48 passing cases, including recursive
protected-choice rejection and immutable-history guards. Actual store generation
and freshness checks admit two v2 revisions; these are local conformance results,
not publisher authentication, legal review or target acceptance.

The index root has `schemaVersion: 1` and `projects`. Projects use ordinal
`projectId` order; revisions use ascending numeric order. Each project has:

```text
projectId, name, description, sourceRepository, license, tags,
applicationId, familyId, revisions
```

Each revision has:

```text
revision, digest, publisher, title, applicationVersions,
entryPoints, locales, outputNames, snapshotPath
```

Family IDs use GUID `D` format; digest is lowercase 64-character SHA-256 from
Core canonical bytes. Application/version/entry/locale/output metadata comes
from the admitted definition. `snapshotPath` is relative to the generated
index folder. Browser publishing copies the generated root beneath
`definition-store/`; same-origin reads remain distinct from external HTTPS
source admission. Browser checks snapshot identity/digest against this metadata
before opening and retains registry conflict history. It loads discovery only
when requested before live answer work. The public projection is
[definition-store.md](../public/definition-store.md).

The published worker includes optional store URLs in its exact SDK-manifest
allowlist, but only core UI/runtime assets determine offline readiness. A
deliberate store read before answer collection fetches credential-free public
bytes with the manifest integrity hash and caches them for later offline reads.
Unavailable store content therefore cannot disable local configuration. Offline
discovery is limited to store assets already read; retaining an admitted external
definition remains a separate deliberate action.

## Validation and contribution lane

Run from the repository root with SDK 10.0.302 and workstation
`TMPDIR="$HOME/.cache/agent-tmp"`:

```bash
dotnet run eng/configurator/BuildDefinitionStore.cs -- --self-test
dotnet run eng/configurator/BuildDefinitionStore.cs -- --source store --output .tmp/definition-store --write
dotnet run eng/configurator/BuildDefinitionStore.cs -- --source store --output .tmp/definition-store --check
```

The self-test uses actual temporary files and real Core admission, including
source ambiguity, file/directory symlinks, duplicate/unknown/oversized curation,
unsafe links, duplicate slugs, mismatched identities, secret defaults, revision
conflicts, source/compiler equality and historical deletion/rewrite/rebinding.
The read-only fork workflow checks out the explicit pull-request target commit
separately, generates the baseline using that commit's builder, and passes its
inventory with `--require-baseline`. Push validation uses the pre-push commit;
manual validation uses its selected commit. A target without a store can provide
an empty bootstrap inventory; an existing store without its owner fails closed.
Pinned Actions, no secrets or publication preserve fork validation's scope.
Value-free codes and aggregate
counts are the only diagnostics; source content and private filesystem paths
are never printed.

## Initial source provenance

The retained `store/minimal-self-hosted/releases/2.json` and
`store/islamu-event/releases/5.json` are byte-identical native copies of
first-party public resources at their original paths:
`examples/minimal-self-hosted/setup-definition.json` and
`src/Configurator.Browser/Definitions/islamu-event.public.json`.
Those historical paths are not current source owners. Both originals come from Configurator commit
`7a244ee1af60129d30f95e11522088851ccb9493`. The neutral source blob is
`ba3a88b9d5d49fc87b9d7cf27c7c99f56b34c65e`; the Event source blob is
`fd2e868f4bd7cbd2358ddd5d8a5ec6c3a10436ed`. Native `cmp` verifies the copies.
Generated canonical SHA-256 digests are
`f984d9652fd6df55a7842fcbf1a27bec4c3cf7bc6c48ca50f4b1554822bce157`
and `6cd8341015d119501b6ffdca358bef6e6cc74363a5cd85ab6dadc7110a48155f`,
respectively; native `sha256sum` independently verifies generated bytes.
The neutral historical formatting remains untouched and canonicalization only
changes its generated representation. Current compact sources compile into
new revisions when canonical content changes; the current Event conformance
source and canonical definition live under `examples/islamu-event/` and are
produced by Event's pinned compiler handoff. The Event snapshot retains the existing
Configurator MIT transfer authority documented in
[Foundation](extraction/FOUNDATION.md) and
[IP Governance](legal/IP_GOVERNANCE.md). Its metadata license describes this
distributed definition, not the Event application or third-party components.
No new external implementation, dependency or asset was imported.
