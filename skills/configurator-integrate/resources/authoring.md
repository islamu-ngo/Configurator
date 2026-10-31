# Author a compact integration

## Contracts and lifecycle

Use `schemas/configurator-authoring-v2.schema.json` for structural editor assistance and `definition schema --kind authoring --text` for built-in discovery. Semantic admission remains `definition check`; `definition lint` adds advisories. The installed command help owns options. Keep these contracts separate:

| Document | Contract |
|---|---|
| One handwritten YAML/JSON source | `$schema: urn:islamu:configurator:authoring:v2` |
| Compiled canonical public JSON | `schemaVersion: 2`, `profile: bounded-v2`, computed sorted `requiredCapabilities`; no `$schema` |
| Machine receipt | Command v4; authoring/runtime handshake 2/2 |
| Separate private answers | `answerContractVersion: 1`, object `values`, concrete pointer array `omitted` |

Declare `identity`, project ID/name, exact `applicationVersions`, recursive `configuration` and filename-keyed `outputs`. Repository and explicit definition license are needed for optional Store redistribution, not invented for local use. Application version and family revision are independent of contract versions. Native `definition init` allocates identity; `init --fork-from` creates a new family from admitted source without converting its format. Never copy fixture UUIDs. Within a family, retain the current canonical anchor, edit the source without manually advancing revision, use `definition revise`, review/adopt its next-revision candidate, then build. Unchanged canonical content does not advance revision; retain release history.

## Recursive schema and answer states

Use `configuration.properties`, recursive object `properties` and array `items`, not the retired `fields`/`itemType` vocabulary. Declare a base `type` (`string`, `boolean`, `integer`, `number`, `object`, `array`), optionally paired with `"null"`. Object `required` names local declared properties; presence does not mean nonblank text or create an absent container. Use typed scalar `enum`/`const`, numeric bounds/`multipleOf`, Unicode-scalar string length, container cardinality and structural `uniqueItems` deliberately. Integers are Int64; exact decimals are bounded, not rounded arbitrary JSON numbers.

Keys preserve empty names, punctuation, case and Unicode composition. References are `{scope: root|item, path: <RFC6901 pointer>}`: empty pointer selects the scope root, `/` the empty key; escape `~` as `~0` and `/` as `~1`. Dots and asterisks are literal. Metadata IDs are not answer keys. Item scope exists only in an admitted row/repeat context; never infer paths from labels or traversal order.

| State | Effective behavior |
|---|---|
| Supplied false, zero, empty string/container | Preserve and validate |
| Supplied null | Present only if nullable; not omission |
| Explicit `omitted` pointer | Missing; suppress initialization |
| Never supplied | Eligible `x-configurator.initialValue` may initialize |
| Reset | Remove override and omission; initialization becomes eligible |
| `x-configurator.suggestedValue` | Adopt only by explicit action |
| Standard `default` | Descriptive metadata; never inserted |

Child initialization never creates an absent parent. Review every nested leaf before declaring `x-configurator.sensitivity: public`. Missing sensitivity is sensitive; `writeOnly` and inherited secret floors cannot be lowered. All definition literals are public: keep private defaults, initial/suggested values, examples and constants out, and never lower classification to admit them.

## Supported bounded patterns

- Typed maps use a recursive `additionalProperties` schema, optionally beside fixed properties; ordinary closed objects use `false`. Untyped `true` rejects. Map keys remain exact and private diagnostic identifiers must not expose them.
- Variants use 2-16 closed object `oneOf` branches with exactly one eligible common required scalar discriminator and distinct `const` values. This is not general composition; nullable variants handle null outside branch selection.
- Local `$ref` begins `#/$defs/` under the original configuration root. Expansion is bounded and acyclic; no network/filesystem resolution, dynamic scope or executable plugin.
- Property `x-configurator.applicableWhen` uses typed `exists`, `equals`, `notEquals`, `in`, `all`, `any`, `not` predicates and explicit root/item references. Missing controllers make leaves false; invalid or secret-deferred controllers remain unknown. Applicability cycles reject. Local `dependentRequired` and restricted `if`/`then`/`else` required-name sets validate presence, not visibility. Include guard `required` when absence must select else.
- `format` is annotation-only unless a string opts into `x-configurator.assertFormat: true`. Asserted names are exactly `hostname`, `email-address`, `uri`, `http-url`; these are lexical checks, not network or credential validation.

Keep help inert and public. Static guidance links are credential-free HTTPS without query or answer interpolation, opened deliberately without automatic fetch. Layout, detail, choice labels and localization do not change typed values, validity or authority. Core support is not proof of every adapter's rendered behavior.

## Outputs and verification

Choose `bindings` or `projection`, never both. JSON/YAML without either copy the effective root. JSON/YAML binding keys are concrete target pointers; dotenv keys are literal variable names with compatible non-null scalar references. Projection operations are `literal`, `copy`, `omit`, `index`, `object`, `list`, `repeat`; item scope exists inside repeats. Markdown requires a bounded list of non-null strings. Explicit omission is not null. `targetUpdateMode` and `restartRequirement` default to `unspecified` and convey expectations, never apply or filesystem authority.

YAML accepts one bounded document with exact lowercase booleans/null and JSON-shaped exact numbers. Quote ambiguous strings and typed-looking keys. Empty plain scalars, aliases, anchors, tags, directives, merge keys and multiple documents reject. Comments affect neither canonical bytes nor digest.

Before releasing, check/lint/build and exercise small complete and incomplete synthetic answer workspaces against independent target keys, types, output bytes and readiness. A draft may defer genuinely missing secrets, not malformed supplied values or ordinary required work; secret-dependent outputs may be withheld. Inverse existing-file import is narrower than generation: inspect the current executable's capability/result contract rather than assuming every generated shape is reversible. Comparison can report unknown content without adoption; strict prefill rejects unsafe conflicts and unaccounted input atomically. Proved structural/list/repeat shapes do not authorize guessed hidden or filtered values. Prefill does not reset explicit omissions or prove completeness.

Keep all answer-derived artifacts restricted, even with public fields. Browser copy/download is not native protected save. Record source admission, synthetic behavior, adapter/package/host observations and target acceptance separately. Only the target validates semantics and authorizes apply/restart; preparation does not prove connectivity, host acceptance or administrator authority.
