# Glossary

This glossary explains key terms and concepts used across the ISLAMU Configurator documentation and software interfaces.

---

### Admitted Definition
A configuration definition that has been validated and accepted by `Configurator.Core`. The admission process checks that all fields, types, conditions, and artifact mappings conform strictly to the supported vocabulary. Once admitted, the definition is immutable and pinned by a cryptographic digest.

### Answer Snapshot (`SetupAnswerSnapshot`)
An immutable, point-in-time configuration value tree plus explicit omissions,
bound to an input revision. Values use exact configuration property names, not
presentation field IDs. For example, `service.name` is one literal root key;
`/service.name` selects it, whereas `/service/name` selects nested properties.
The snapshot isolates validation and file generation from ongoing UI edits.
Its serialized private v1 envelope contains `answerContractVersion`, `values`
and `omitted`; answer files remain restricted.

### Answer Workspace
The interactive container holding the operator's current working answers in memory. In the browser, terminal, and desktop clients, the workspace manages real-time field validation, dirty state, and change notifications.

### Artifact
A specific target configuration file produced by Configurator. Common artifact types include dotenv files (`.env`), JSON documents (`config.json`), YAML manifests (`docker-compose.override.yml`), and Markdown tables. Each artifact is declared in the definition with an ID, output filename, format, and field bindings.

### Canonical Digest
A SHA-256 content identity computed over canonical definition JSON. Equivalent
formatting yields the same canonical content. A digest pins admitted bytes; it
does not authenticate the publisher, certify client behavior or prove target
acceptance.

### Create-Only Save
A filesystem protection mechanism used on supported Linux hosts where each
prepared file is installed atomically with owner-only (`0600`) permissions,
without overwriting an existing destination. Bundle publication is sequential,
not a whole-bundle transaction; a later failure can leave a written prefix.
Installation, immediate verification and cleanup are separate receipt facts.

### Definition (`SetupDefinition`)
The core contract describing an application's configuration surface. Runtime v2
(`bounded-v2`) declares a recursive configuration schema, applicability and
presence relationships, sensitivity, and output projections. Base types are
string, boolean, integer, number, object and array; enum constrains typed scalar
choices and nullable schemas also admit null. Definitions are data, not
executable plugins. Standard `default` is descriptive, not an answer.

### Default, Initialization & Suggestion
Standard `default` describes a value without inserting it.
`x-configurator.initialValue` supplies an eligible never-supplied value;
it cannot create an absent optional container merely because a child has an
initial value. `x-configurator.suggestedValue` remains inert until deliberately
adopted. Reset removes both a supplied override and explicit omission, making
initialization eligible again.

### Presence, Omission & Null
Required means present, not nonblank. Supplied `false`, zero, empty string,
empty object and empty array remain values, subject to their schema constraints.
Null counts as present only under a nullable schema. Explicit omission records
intent to leave a location absent and suppresses initialization; it is not reset.
Private `omitted` entries are concrete RFC 6901 pointers: escape `~` as `~0`
and `/` as `~1`, without interpreting dots as nesting.

### Family ID (Family GUID)
A stable, unique identifier (UUID/GUID) assigned to an application definition family across all of its revisions and versions. For example, a service might maintain the same Family ID across versions `1.0`, `2.0`, and `3.0`.

### Journeys & Experiences
Authored presentation flows that arrange declared sections. **Recommended** and
**Custom** are example journey labels, not built-in answer presets or guarantees
about requiredness. Detail/disclosure is separate view state; hiding a section
does not make its active required values optional.

### Maintainer Source (`configurator.yaml`)
The compact, human-authored YAML or JSON file maintained in an application's repository. Maintainers author this file, which is then compiled into a canonical runtime definition using `islamu-configurator definition build`.

### Projection
The bounded output expression declared for an artifact. The maintained neutral
example copies `{scope: root, path: /service.credential}` into dotenv member
`SERVICE_CREDENTIAL` and JSON member `credential`. The reference is a scoped
RFC 6901 pointer, not a JSONPath expression or a dotted-name lookup.

### Revision
An integer incremented when canonical definition content changes within the same
family. Different content cannot reuse an admitted family/revision. Application
version selectors match declared versions exactly; neither version labels nor
revision increments prove target compatibility.

### Secret Classification (`sensitivity: secret`)
A classification declared as `x-configurator.sensitivity: secret` (also raised
by `writeOnly: true`). It marks credentials such as passwords and tokens;
inherited and descendant floors cannot be lowered by a client. Browser entry
requires per-workspace consent. Masked editors reduce screen exposure, but
prepared files can contain plaintext secrets and remain restricted. Missing
sensitivity defaults to sensitive, not public.

### Secret-Deferred Draft
An intentionally incomplete preparation that defers missing secret leaves and
reports completion locations. It does not remove all confidential data or turn
invalid answers into valid ones. Missing ordinary required values and unresolved
secret-dependent conditions still block affected preparation. Drafts remain
restricted even without secrets; never publish them as public examples.
Complete preparation and target acceptance remain separate.

### Sensitivity Floor
An architectural rule guaranteeing that a rendering surface (such as the web browser) cannot demote or ignore a field's sensitivity level. If a field is declared `secret`, no client interface can treat it as `public` or log it in plaintext.

### Target Application
The external software being configured (such as an ISLAMU Event instance, Grafana server, Keycloak realm, or custom web app). Configurator generates the configuration files for the target application, but the target application remains entirely responsible for loading, validating, and applying those files.

### Target Authority
The administrative authority required to install, restart, or run the target application. Configurator explicitly possesses **no target authority**: it does not restart services, execute shell commands, or manage cloud instances.
