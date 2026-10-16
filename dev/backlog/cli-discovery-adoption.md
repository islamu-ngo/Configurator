# CLI Discovery, Repair And Installed Adoption

- Status: Required roadmap, split into the bounded increments below; not executed.
- Owner: Configurator CLI/Core/package maintainers.
- Origin: CTO review `CLI-CTO-2026-10-15-r1`, 2026-10-15.
- Dependencies: reliable command v2/runtime and
  [public authoring custody](cli-public-authoring-custody.md) before full adoption.
- Retained specification: native-adoption plan R07-R13 / S14-S27.
- I-VSD: discovery part of F004/M004 and F005/M005, F006/M006, F008/M008 remain open.

## Deliver In Separate PR-Sized Triads

### 1. Installed Discovery And Canonical Grammar

Expose actual application build/version (not Spectre library version), public
contract versions, capability conditions, exact grammar/repeatability/requirements
and declared output identities. No runtime Git invocation, environment scan,
network discovery or private/default/free-form field dump. Absent source identity
is explicitly unknown, never a fabricated revision.

Canonical tree, breaking directly without aliases:

```text
definition init | check | build | revise | describe
catalogue list | describe
artifact create | validate | diff | coverage | check-existing | prefill | import
artifact bundle-check | bundle-create
info
schema list | export
doctor
--version | --help | --machine | --text
```

Remove `catalogue show`, `artifact open/format/export/preview` and `env` routes;
delete `EnvironmentCommands.cs`. Retain dotenv through canonical artifact commands
and retain inverse/import semantics. Root version is explicit application metadata,
not hidden `cli version`. No compatibility aliases or automatic old-route dispatch.
Existing modules/settings/registry remain the structural owner.

Proposed CLI owners: `Commands/DiscoveryCommands.cs`, `SetupCliDiscoveryHandlers.cs`,
`SetupCliBuildInfo.cs`; update generated schema metadata and independent verifier.
Schema export returns compiled/shipped public resource bytes/digest, no installed
engineering generator dependency. Public bytes may deliberately use text stdout;
machine export uses a public file receipt, never two competing JSON documents.
Declare authoring `settings` versus runtime `out-settings` identities explicitly.
Pages default/max 16, fit a byte budget even with escaping, always make progress,
and require expected public-definition/view digest for noninitial pages. No raw
answer hash or persistent private snapshot. Doctor reports capabilities; explicit
`--require protected-write` can reject unsupported hosts without blocking unrelated
discovery. Capability support is not a filesystem/target-acceptance certification.

Acceptance: S14-S18 and applicable S20 through installed binaries outside checkout,
schema byte equality, each retained route, removed-route safe refusal, descriptive
human help and bounded machine metadata. No prose snapshots as assurance.

### 2. Safe Structural Authoring Repair

Own a bounded `DefinitionAuthoringDiagnostic` in Core authoring, not CLI exception-
message scraping. Preserve safe code, known RFC 6901 structural pointer and numeric
syntax location through the actual YAML parser/JSON normalizer/authoring codec/
compiler catch boundaries. `SetupCompositionException` is owned with the normalizer.
Units: one-based line/Unicode-scalar column and zero-based original UTF-8 offset
where available. BOM, CRLF and non-ASCII mappings need independent expected values.
Unknown submitted member names, values, source snippets, private paths and native
exception messages never become diagnostics. Private-answer locations remain stricter.

Acceptance: S19-S20 fail first in Core `DefinitionAuthoringDiagnosticTests` and CLI
repair cases, then pass on actual malformed/unsupported examples and synthetic
private sentinels. No authoring/runtime schema version bump unless their vocabulary
actually changes. Update public authoring guide and technical source anchor together;
correct outdated codec documentation to `SetupDefinitionSourceCodec`.

### 3. Portable Skill And Linux Installed Adoption

One maintained public integration file; no target-specific exporter, compiler
wrapper, copied runtime or host apply authority. Keep portable public
`skills/configurator-integrate/` distinct from the repository `.agents` router.
Teach an installed native CLI, not checkout or SDK assumptions.

Acceptance S21-S24: in a temporary directory outside checkout, discover contracts,
init/edit/check/build/describe, use candidate revise and explicit contributor merge,
observe incomplete versus complete synthetic inputs, prepare outputs, independently
compare expected bytes and invoke a test-owned target semantic validator. The
validator is not shipped as an integration wrapper. Target acceptance/auth remains
with the target. Local pinned archive/checksum/sidecars support offline execution
without SDK/source/network/credentials; checksum integrity is not publisher
authentication. Public and technical guides, install/CI recipes and skill ship
together. No prose pinning; real binary workflow is the oracle.

### 4. Actual-Host Qualification And Durable Handoff

The user's 2026-10-16 request expands modern CLI targets to glibc and musl Linux
x64/Arm64, macOS Intel/Arm64 and Windows 10/11 x64; Windows 7 is excluded. Run native
packages on matching hosts, not merely cross-publish them. The configured CLI CI
lanes use Ubuntu 24 x64/Arm64, macOS 15 Intel/Arm64 and Windows Server 2025 x64;
configured jobs are not executed evidence. Musl hosts and Windows 10/11 client
editions need separate qualification. Keep reviewed Actions pinned by SHA.
Qualify public-authoring/discovery/candidate flow on each host and restricted
publication only where supported. Windows/macOS must positively prove restricted-
save refusal, never ordinary-file fallback. See S25-S26 and
[protected host expansion](configurator-native-host-evidence.md) for the newly
required native-writer work. Missing hosts/principals are pending evidence, not a
green support claim. This does not authorize unsafe ordinary-file fallbacks.

S27: graduate ADRs, reusable journal lessons and any remaining concrete backlog;
keep public/technical/skill/package resources synchronized. Relevant native owner,
architecture/package, generator/ratchet and rights/dependency checks run once at
workstream exit. Full Architecture needs all native Linux x64 surfaces plus Browser;
do not treat a CLI-only package as complete suite evidence. No Event provider matrix.

## Deferred Commit Provenance

Original allocations are unreserved. Recheck collisions/classification and author
complete literal-path packets on activation, not during this review:

| Original increment | Provisional allocation | Intended treatment |
| --- | --- | --- |
| C6 discovery/grammar | CHG-01M4ZEE0D8QYJ05JKNB2Q6Q5HD | `feat(cli)!` plus breaking footer/fragment |
| C7 authoring repair | CHG-01M4ZEE0Q4WCAA2NCWPJTBHNS5 | `feat(definitions)` plus fragment |
| C8 adoption guidance | CHG-01M4ZEE0ZPD0Z10A58X5FQC1V8 | `docs(cli)` public fragment |
| C9 installed tests | CHG-01M4ZEE13A4T2Y5Z6VE08SEEVV | Intentionally unused; test-only skip needs no public ID |
| C10 actual hosts | CHG-01M4ZEE16SRD5P02MCKRW30F3G | Reclassify on real outcome; original proposal `feat(release)` plus fragment |
| C11 graduation | None | Graduate per slice, not a disconnected omnibus docs commit |

Any changed public contract travels with its actual callers, tests, generated
schemas/ratchets, public/technical guidance and governed change fragment. Backlog
is not implementation authorization and the old eleven-commit umbrella is retired.

## Preserved Longer-Term Exclusions

Promoted distribution/signing/authority and representative multi-project adoption
research remain future work. Trigger the former only with separately established
Configurator authority and immutable promotion evidence; trigger the latter after
one real installed workflow passes, comparing bounded target examples without
private data. Neither may borrow Event credentials, receipts or release history.
No telemetry, arbitrary remote execution, plugins, auto-updater or target apply.
