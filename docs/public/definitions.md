# Configuration definitions

A definition describes an application's questions, constraints and artifact
mappings. Configurator interprets bounded data; it does not execute scripts,
install services or grant application-administration authority.

Start with `examples/minimal-self-hosted/setup-definition.json`. Its fields
produce a dotenv file or JSON document through the same definition used by the
native clients. Native answer-derived artifacts are restricted even when all
fields are declared public.

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
