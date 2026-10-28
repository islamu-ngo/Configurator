---
description: "Author, review and distribute a bounded-v2 public configuration definition beside your application."
---

# Integrate Your Tool

Maintain one public `configurator.yaml` or `configurator.json` beside your
application. Core compiles it into immutable canonical runtime JSON; clients
collect private answers separately and prepare declared files offline.
Preparation never installs configuration or grants target administration.

The current base uses authoring v2 and runtime schemaVersion 2 with profile
`bounded-v2`. The [current reference](definition-reference.md) defines executable
grammar. The [public standard](definition-standard.md) remains Draft because
broader features and complete client/scenario qualification are not delivered.

## The Maintainer Lifecycle

1. **Initialize:** Allocate a fresh family and revision 1 through native tooling.
2. **Author:** Describe the configuration schema, guidance, journeys and outputs.
3. **Check:** Admit bounded syntax, references, privacy and constraints.
4. **Build:** Produce canonical `dist/configurator/revisions/1.json`.
5. **Revise:** Create a reviewed candidate for changed canonical content without
   overwriting the source or reusing an admitted family/revision.

Revision 1/2 are family revisions, not authoring or runtime contract versions.
Machine commands retain envelope v3; successful check/build reports the
independent authoring/runtime handshake as 2/2.

Run `definition lint --source configurator.yaml --machine` for advisory authoring
feedback after admission. Use `definition schema --kind authoring --text` for the
built-in editor schema or `--kind command --machine` for the current receipt
contract. Discovery never reads private answers or arbitrary schema locations.

## Complete Minimal `configurator.yaml` Example

The maintained [minimal source](../../examples/minimal-self-hosted/configurator.yaml)
and its [compiled runtime](../../examples/minimal-self-hosted/setup-definition.json)
are the complete neutral example; use them rather than a second full raw copy
that can drift. They include required secret entry, optional nested settings,
root applicability, journeys, guidance and dotenv/JSON/YAML mappings.

Current checked example identities are fresh v2 families, each at revision 1:

| Source | Family |
|---|---|
| `examples/minimal-self-hosted/configurator.yaml` | `9b6a7b3e-45bf-4485-8fa1-d7888938c09b` |
| `examples/grafana-13.2.2/configurator.yaml` | `ffc20a25-7ea5-4138-be3b-88f678cac36b` |
| `examples/islamu-event/configurator.json` | `c0d02619-0134-411b-b495-ef302f592669` |

The native `--check-examples` check confirmed source/runtime agreement **3/3**
on 2026-10-28. Agreement proves reproducible compilation of these
sources, not target startup, Grafana provisioning acceptance, Event acceptance
or qualification of every interactive scenario. Never copy these family IDs
into your own integration.

### Author configuration, not metadata IDs

This fragment illustrates schema structure, not a complete source envelope:

```yaml
configuration:
  type: object
  additionalProperties: false
  required: [service.port]
  x-configurator:
    sensitivity: public
  properties:
    service.port:
      type: integer
      minimum: 1
      maximum: 65535
      default: 8080
      x-configurator:
        sensitivity: public
        initialValue: 8080
        suggestedValue: 8443
    label:
      type: [string, "null"]
      enum: [internal, external]
      x-configurator:
        sensitivity: public
    peers:
      type: array
      items:
        type: object
        additionalProperties: false
        required: [name]
        x-configurator:
          sensitivity: public
        properties:
          name:
            type: string
            x-configurator:
              sensitivity: public
```

`service.port` is one exact data key. Its reference is
`{scope: root, path: /service.port}`, not `/service/port`.
`SetupFieldDefinition.Name` preserves that key; bounded `Id` is metadata only.
The generated `f-` hash namespace is reserved, including for literal names
already beginning `f-`. Do not use IDs as answer keys.

Every object explicitly declares `additionalProperties: false`; every array
declares recursive `items`. Enums retain the scalar type instead of `type: enum`.
Nullable form is one base type plus `"null"`. Names are ordinal, case-sensitive,
not normalized; RFC6901 escaping preserves slashes, tildes and empty names.

`default` describes the target; it does not fill answers. `initialValue` can fill
an eligible never-supplied location; `suggestedValue` requires deliberate adoption.
Omission suppresses initialization; reset removes both override and suppression.
No child initialization creates an absent container. Required means presence,
not nonblank text; declare a separate length/notBlank constraint if needed.
Keep private values out of all public default/initial/suggested/example metadata.

### Bind outputs explicitly

```yaml
outputs:
  .env:
    format: dotenv
    targetUpdateMode: create
    restartRequirement: process
    bindings:
      SERVICE_PORT: {scope: root, path: /service.port}
  config.json:
    format: json
    bindings:
      /server/port: {scope: root, path: /service.port}
```

Dotenv keys are literal variable names. JSON/YAML keys are concrete target
pointers; `/server/port` builds nested objects, not a dotted-key convention.
Source references have explicit root/item scope; item scope exists only in
admitted repeat contexts. Choose bindings or a projection, never both.
JSON/YAML without either copy the effective configuration tree. Omitted
`targetUpdateMode` and `restartRequirement` mean `unspecified`, not create or
hot reload. Filename hints never authorize writing a path.

## Step-by-Step Maintainer Guide

### Step 1: Initialize Your Project

```bash
islamu-configurator definition init \
  --project-id example.service \
  --name "Example Service" \
  --application-version 1.0.0 \
  --machine
```

To scaffold from a public environment template, add
`--from-env-example .env.example`. Only variable names are retained, not example
values. Review scalar types, required sets, sensitivity and output bindings.
Do not use real private environment files as public definition inputs.

### Step 2: Validate Your Definition

```bash
islamu-configurator definition check --machine
```

Check validates source admission, references, closed shapes, privacy, supported
constraints and budgets. It does not establish target-validator equivalence.
Root applicability is delivered, as are root presence-only
`if.required`/`then.required` and `dependentRequired`. Broader or nested schema
conditions and source maps are later phases. Recursive maps, bounded provider
branches, local references and explicit format assertions are implemented.
Unknown semantic vocabulary rejects rather than silently
degrading. `format` currently remains descriptive.

### Step 3: Build Canonical JSON

```bash
islamu-configurator definition build --machine
```

Build normally writes `dist/configurator/revisions/1.json`. The runtime includes
`schemaVersion: 2`, `profile: bounded-v2` and computed sorted
`requiredCapabilities`. Its SHA-256 binds the entire canonical definition.
Consumers must refuse unsupported mandatory semantics before answer collection;
generated metadata alone does not prove a client's editing support.

### Step 4: Revise for Subsequent Releases

```bash
islamu-configurator definition revise \
  --output candidates/configurator.next.yaml \
  --machine
```

Review the create-only candidate, then adopt it as your working source and build
the next revision. Native tooling preserves canonical family/revision history
and rejects conflicting admitted content; unchanged canonical content does not
advance the revision. Forking allocates a new family at revision 1.

## Scaffolding from JSON Schema (`--from-schema`)

An existing public application schema is a separate input:

```bash
islamu-configurator definition init \
  --project-id example.service \
  --name "Example Service" \
  --application-version 1.0.0 \
  --from-schema schema.json \
  --accept-bounded-semantics \
  --machine
```

The importer produces a v2 `configuration` schema, not the old `fields` map.
It retains selected title/description and supported closed-object/scalar/array
constraints. Defaults are discarded, ordinary nodes are sensitive, and
`writeOnly` raises the secret floor. Format remains a separate advisory annotation,
not asserted validation. Review classification before adding any public initial
or suggested value; no importer infers that a hostname or port is public.

### Why `--accept-bounded-semantics` is Required

The flag acknowledges encountered bounded numeric-domain differences (Int64 and
exact bounded decimals), and nullable provider semantics that admit null outside
branch selection. It does not acknowledge arbitrary losses. Native v2 requiredness
is presence and string length is Unicode-scalar based; the old whitespace and
UTF-16 differences are not the current native contract.

Scaffolding supports constrained recursive items, typed enum/null values, maps
and closed provider branches. General unions/composition, regex and unsupported
assertions reject. Its structured transformation ledger distinguishes retained,
converted, advisory, discarded and unsupported concepts by public schema location
and document ordinal. Defaults/examples never become initialization. Structural
containers/public constants receive explicit classification records; collected
leaves remain sensitive and writeOnly ancestry remains secret. Supplied local
references are bounded; no remote fetch or executable validator is admitted.
The flag cannot bypass unsupported assertions or privacy. See the
[import support boundary](definition-reference.md#json-schema-import).

## Private answers and qualification

Private answer files use `answerContractVersion: 1`, object `values` and array
`omitted`. Values preserve exact names and nesting; omissions are concrete
RFC6901 pointers. This envelope is not public source, store metadata or a browser
cache payload. A prepared secret-deferred draft remains incomplete.

Before distributing an integration:

- Generate your own family and enumerate exact target application versions.
- Review inherited sensitivity, all public literals and required secret guidance.
- Keep applicable required/error work reachable across your selected clients.
- Exercise omission, reset, nullable values and nested-container behavior.
- Validate prepared files through the target application's own startup/import
  path; record that separately from source checks and synthetic projections.
- Run definition checks in your own CI and qualify actual packages/hosts.

## Distributing Your Definition

Keep source and canonical revisions beside application code, host canonical
runtime JSON on an admitted public HTTPS origin for
[launch integration](launch-integration.md), or contribute through the
[Definition Store](definition-store.md). Browser remote selection accepts
compiled runtime JSON, not arbitrary remote authoring or answers.

No distribution channel grants target authentication, apply authority or native
protected-save guarantees. See the portable
[integration skill](../../skills/configurator-integrate/SKILL.md) for agent
workflow guidance; the current contract reference and admitted source remain
authoritative when older guidance differs.
