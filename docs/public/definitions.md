# Configuration definitions

A definition describes an application's questions, constraints and artifact
mappings. Configurator interprets bounded data; it does not execute scripts,
install services or grant application-administration authority.

Start with `examples/minimal-self-hosted/setup-definition.json`. Its fields
produce a dotenv file, JSON document or YAML document through the same
definition used by every interface. The sample's credential can be deferred in
an explicitly incomplete browser draft or entered voluntarily after per-workspace
consent. All answer-derived artifacts are restricted even when all fields are
declared public; browser downloads cannot establish native file protection.

Each definition has a stable family GUID, a revision, declared application
versions and a digest computed from canonical definition bytes. GUID identity
is not publisher authentication. A session pins the admitted revision and
digest; it never silently upgrades when a catalog changes.

For public discovery, contributors can propose a project folder and versioned
JSON definitions in the optional [Definition Store](definition-store.md).
The native builder derives canonical identity and compatibility metadata from
admitted content. Store inclusion remains separate from publisher authenticity;
local files and independent public publisher links remain usable without it.

Field IDs are independent of output keys. Activation and required conditions
refer to declared fields. Disclosure controls the view, never the validation:
required or protected decisions cannot disappear from guided review.

Lists may contain scalars or bounded objects. An object list declares
`itemType: "object"`, its `properties`, and `minItems`/`maxItems`; each row is
validated against those properties. Optional `uniqueBy: ["metadata", "name"]`
identifies a declared nested string key for ordinal uniqueness across rows.
Unknown members, malformed rows, missing required properties and duplicate
keys reject; diagnostics never echo the duplicate value. Core's depth, node,
scalar and 4,096 repeated-item budgets still apply. Event's tenant definition
uses its target-owned maximum of 256 and unique tenant names; no exporter must
be rerun merely to edit an operator's tenant rows.

Nested fields preserve their own sensitivity. A public parent label never
permits secret defaults or bypasses browser consent. Removed object-list rows
are absent from the next immutable answer snapshot and composed output.

Use typed dotenv, JSON, YAML or Markdown projections. Paths are sanitized
filename hints, not permission to write arbitrary files. Apply/restart metadata
describes consequences for the operator; the target still validates and applies
its own configuration.

Complete preparation requires all active required answers and valid values.
Secret-deferred preparation is a separate draft operation: missing secret
leaves produce blank dotenv values, empty JSON/YAML strings or null nonstring
leaves, together with completion locations. These are placeholders, never
fabricated valid credentials. Ordinary missing/invalid fields remain errors;
secret-dependent conditions cannot silently be treated as false. Complete the
listed locations and run target validation before use. A draft is neither a
ready configuration nor host acceptance.

The generated schema is `schemas/setup-definition-v1.schema.json`. See
`docs/internal/CORE_DEFINITION_CONTRACT.md` for the closed grammar and exact
Configurator v1 canonicalization profile. It is not an RFC 8785 compatibility
claim. Local YAML is normalized to the same definition model; distribution,
integrity and digest pinning use the canonical JSON bytes.

For compact maintainer sources, use the generated
[`configurator-authoring-v1.schema.json`](../../schemas/configurator-authoring-v1.schema.json)
and [integration guide](integrate-your-tool.md). Local YAML supports comments,
multiline strings and precise JSON-shaped decimal/exponent numbers. Quote intended
numeric, boolean or null keys and ambiguous strings. Empty plain values, typed keys,
duplicate/colliding keys, merge keys, anchors, aliases, tags, directives and multiple
documents reject. Use explicit `null` or `""` instead of a blank plain value.

For a second independently authored format example, see
[`examples/grafana-13.2.2`](../../examples/grafana-13.2.2/README.md). It shows
synthetic Docker environment and provisioning YAML preparation for pinned
Grafana OSS v13.2.2; the example has not been tested against a running Grafana
host and does not prove Grafana will accept those files.

Existing dotenv, JSON and YAML files can prefill answers only where an artifact
projection directly copies an active field into an unconditional top-level key.
Configurator rejects ambiguous, transformed or conflicting input without
changing the current workspace. Browser secret-bearing import requires consent.
Missing fields still need operator answers or explicit secret deferral.
