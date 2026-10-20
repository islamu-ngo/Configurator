# Configuration definitions

A definition describes an application's questions, constraints and artifact
mappings. Configurator interprets bounded data; it does not execute scripts,
install services or grant application-administration authority.

## Choose the right definition file

| File | Purpose | Where to start |
| --- | --- | --- |
| Maintainer source (`configurator.yaml`, `.yml` or `.json`) | The compact public declaration maintained beside an application's code. | [Integrate your tool](integrate-your-tool.md) |
| Compiled runtime definition (canonical JSON) | The admitted, versioned definition distributed to clients and used for integrity pinning. | [Check and build a source](integrate-your-tool.md#check-build-and-revise) |
| Answer workspace (JSON object) | Local values keyed by definition field IDs; not part of either public definition. | [Prepare a configuration](getting-started.md) |

Start with the [minimal runtime definition](../../examples/minimal-self-hosted/setup-definition.json).
Its fields produce a dotenv file, JSON document or YAML document through the same
definition used by every interface. The sample's credential can be deferred in
an explicitly incomplete browser draft or entered voluntarily after per-workspace
consent. All answer-derived artifacts are restricted even when all fields are
declared public; browser downloads cannot establish native file protection.

## Identity and distribution

Each definition has a stable family GUID, a revision, declared application
versions and a digest computed from canonical definition bytes. GUID identity
is not publisher authentication. A session pins the admitted revision and
digest; it never silently upgrades when a catalog changes.

For public discovery, contributors can propose a project folder and versioned
JSON definitions in the optional [Definition Store](definition-store.md).
The native builder derives canonical identity and compatibility metadata from
admitted content. Store inclusion remains separate from publisher authenticity;
local files and independent public publisher links remain usable without it.

## Fields, validation and output mappings

Field IDs are independent of output keys. Activation and required conditions
refer to declared fields. Disclosure controls the view, never the validation:
required or protected decisions cannot disappear from guided review.

Lists may contain scalars or bounded objects. An object list declares
`itemType: "object"`, its `properties`, and `minItems`/`maxItems`; each row is
validated against those properties. Optional `uniqueBy: ["metadata", "name"]`
identifies a declared nested string key for ordinal uniqueness across rows.
Unknown members, malformed rows, missing required properties and duplicate
keys reject; diagnostics never echo the duplicate value. Core's depth, node,
scalar and 4,096 repeated-item budgets still apply. A target may impose stricter
limits or additional validation; admitting a definition does not prove that the
target will accept every prepared file.

Nested fields preserve their own sensitivity. A public parent label never
permits secret defaults or bypasses browser consent. Removed object-list rows
are absent from the next immutable answer snapshot and composed output.

Use typed dotenv, JSON, YAML or Markdown projections. Paths are sanitized
filename hints, not permission to write arbitrary files. Apply/restart metadata
describes consequences for the operator; the target still validates and applies
its own configuration.

## Authored journeys and field guidance

Optional `presentation` describes sections and experiences in the public definition.
It supplies no answers, presets, activation or validation overrides. Admission of
this metadata does not itself implement a client journey. Machine-only definitions
may omit it; absence is neutral, not an instruction to select a legacy display mode.

Each root field belongs to exactly one nonempty section. Every experience lists
every section exactly once, in its intended order, and `defaultExperience` names an
existing experience. Nested properties remain inside their root editor. Source
sections accept `fields` containing logical keys and/or `groups` selecting roots by
their existing `group`. Compilation expands group members in ordinal logical-key
order, after explicit fields, and rewrites all references to runtime root IDs.
Duplicate, overlapping, empty and unresolved selections reject. Runtime sections
contain only `fields`, never `groups`.

There are at most 64 sections and 16 experiences. Their unique IDs are lowercase
ASCII kebab-case, start with a letter and contain at most 64 characters. Titles
are at most 128 characters; optional descriptions at most 2,048. Section `disclosure`
and experience `initialDisclosure` accept `essential`, `guided` or `advanced` and
default to `guided`. Experience `navigation` accepts `guided` or `sections` and
defaults to `sections`. Authors choose these facts; names such as Recommended and
Custom have no reserved behavior.

Existing field `help` is the short explanation (at most 4,096 characters), and
`consequence` retains its existing meaning. Optional `guidance` adds:

| Member | Bound and meaning |
| --- | --- |
| `instructions` | Up to 4,096 characters of inert plain text explaining acquisition or use. |
| `links` | Up to four objects containing only `title` (128 characters) and `url` (2,048 characters). URLs must be absolute credential-free HTTPS with no query or fragment. |
| `searchTerms` | Up to sixteen distinct nonempty strings, each at most 64 characters. |

Text rejects controls and HTML delimiters. Instructions also reject Markdown
links/code and template substitution markers; no commands, images or substitution
are executed. Links are public instructions, not a way to send an answer or fetch
a credential. Clients must open them only deliberately, without automatic fetch,
prefetch, opener or referrer transfer. YAML syntax comments are not runtime help.
Never include real answers or secret examples in this public metadata.

Optional field `credential` has exactly `kind` and `acquisition`. It is admitted
only on string leaves in a secret context, including a public/sensitive leaf below
a secret object or object-list ancestor. It cannot lower sensitivity or permit
private defaults/recommendations.

| `kind` | Permitted `acquisition` |
| --- | --- |
| `password` | `existing`, `create`, `issuer` |
| `opaque-token` | `existing`, `issuer` |
| `symmetric-key` | `existing`, `create` |
| `private-key` | `existing`, `issuer` |
| `connection-string` | `existing`, `issuer` |

`create` describes the operator's task, not permission to run a generator.
Certificates use ordinary guidance, not a private-key label. Non-password kinds
must not be presented as current account passwords. No `generation`, `comment`
alias or password-manager-specific member is admitted. Omitted guidance and
credential metadata remain valid for generic machine-only definitions.

All admitted metadata participates in the canonical digest. Publish a new revision
when it changes; a family/revision pair cannot be reused with different guidance
or journey content. See [authoring examples](integrate-your-tool.md#describe-journeys-and-credential-acquisition)
and the [technical contract](../internal/CORE_DEFINITION_CONTRACT.md#presentation-guidance-and-credentials).

## Complete configuration versus a secret-deferred draft

Complete preparation requires all active required answers and valid values.
Secret-deferred preparation is a separate draft operation: missing secret
leaves produce blank dotenv values, empty JSON/YAML strings or null nonstring
leaves, together with completion locations. These are placeholders, never
fabricated valid credentials. Ordinary missing/invalid fields remain errors;
secret-dependent conditions cannot silently be treated as false. Complete the
listed locations and run target validation before use. A draft is neither a
ready configuration nor host acceptance.

## Schemas and YAML syntax

The runtime schema is
[`setup-definition-v1.schema.json`](../../schemas/setup-definition-v1.schema.json).
See the [definition contract](../internal/CORE_DEFINITION_CONTRACT.md) for the
closed grammar and exact Configurator v1 canonicalization profile. It is not an RFC 8785 compatibility
claim. Local YAML is normalized to the same definition model; distribution,
integrity and digest pinning use the canonical JSON bytes.

For compact maintainer sources, use the generated
[`configurator-authoring-v1.schema.json`](../../schemas/configurator-authoring-v1.schema.json)
and [integration guide](integrate-your-tool.md). Local YAML supports comments,
multiline strings and precise JSON-shaped decimal/exponent numbers. Quote intended
numeric, boolean or null keys and ambiguous strings. Empty plain values, typed keys,
duplicate/colliding keys, merge keys, anchors, aliases, tags, directives and multiple
documents reject. Use explicit `null` or `""` instead of a blank plain value.

## More examples and existing configuration

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
