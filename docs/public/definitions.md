# Configuration definitions

A definition describes an application's questions, constraints and artifact
mappings. Configurator interprets bounded data; it does not execute scripts,
install services or grant application-administration authority.

Start with `examples/minimal-self-hosted/setup-definition.json`. Its fields
produce a dotenv file, JSON document or YAML document through the same
definition used by the native clients. The sample's private credential must be
answered in a native client. Native answer-derived artifacts are restricted even
when all fields are declared public.

Each definition has a stable family GUID, a revision, declared application
versions and a digest computed from canonical definition bytes. GUID identity
is not publisher authentication. A session pins the admitted revision and
digest; it never silently upgrades when a catalog changes.

Field IDs are independent of output keys. Activation and required conditions
refer to declared fields. Disclosure controls the view, never the validation:
required or protected decisions cannot disappear from guided review.

Use typed dotenv, JSON, YAML or Markdown projections. Paths are sanitized
filename hints, not permission to write arbitrary files. Apply/restart metadata
describes consequences for the operator; the target still validates and applies
its own configuration.

The generated schema is `schemas/setup-definition-v1.schema.json`. See
`docs/internal/CORE_DEFINITION_CONTRACT.md` for the closed grammar and exact
Configurator v1 canonicalization profile. It is not an RFC 8785 compatibility
claim. Local YAML is normalized to the same definition model; distribution,
integrity and digest pinning use the canonical JSON bytes.

For a second independently authored format example, see
[`examples/grafana-13.2.2`](../../examples/grafana-13.2.2/README.md). It shows
synthetic Docker environment and provisioning YAML preparation for pinned
Grafana OSS v13.2.2; the example has not been tested against a running Grafana
host and does not prove Grafana will accept those files.

Existing dotenv, JSON and YAML files can prefill answers only where an artifact
projection directly copies an active field into an unconditional top-level key.
Configurator rejects ambiguous, transformed or conflicting input without
changing a native workspace. Missing fields still need operator answers.
