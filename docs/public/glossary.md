# Glossary

This glossary explains key terms and concepts used across the ISLAMU Configurator documentation and software interfaces.

---

### Admitted Definition
A configuration definition that has been validated and accepted by `Configurator.Core`. The admission process checks that all fields, types, conditions, and artifact mappings conform strictly to the supported vocabulary. Once admitted, the definition is immutable and pinned by a cryptographic digest.

### Answer Snapshot (`SetupAnswerSnapshot`)
An immutable, point-in-time collection of user-supplied values keyed by definition field IDs. The snapshot isolates validation and file generation from ongoing UI edits, ensuring that generated files always reflect a coherent state.

### Answer Workspace
The interactive container holding the operator's current working answers in memory. In the browser, terminal, and desktop clients, the workspace manages real-time field validation, dirty state, and change notifications.

### Artifact
A specific target configuration file produced by Configurator. Common artifact types include dotenv files (`.env`), JSON documents (`config.json`), YAML manifests (`docker-compose.override.yml`), and Markdown tables. Each artifact is declared in the definition with an ID, output filename, format, and field bindings.

### Canonical Digest
A SHA-256 hash computed over the normalized, canonical JSON representation of a definition. The canonical digest uniquely identifies the exact definition content, guaranteeing that all clients validate against the identical ruleset regardless of whitespace or formatting differences.

### Create-Only Save
A filesystem protection mechanism used on Linux where Configurator creates new files using atomic, owner-only (`0600`) permissions, but strictly refuses to overwrite existing files. If a file with the target name already exists, the save operation aborts safely to prevent accidental data loss.

### Definition (`SetupDefinition`)
The core contract describing an application's configuration surface. A definition declares questions (fields), data types (string, boolean, integer, enum, object, list), default values, relevance conditions, sensitivity levels, and mappings to output artifacts. Definitions are purely declarative and contain no executable code.

### Family ID (Family GUID)
A stable, unique identifier (UUID/GUID) assigned to an application definition family across all of its revisions and versions. For example, a service might maintain the same Family ID across versions `1.0`, `2.0`, and `3.0`.

### Journeys & Experiences
Authored presentation flows within a definition that organize questions into logical sections and disclosure levels. A definition can define experiences such as **Recommended** (showing only essential settings) and **Custom** (revealing advanced operational parameters).

### Maintainer Source (`configurator.yaml`)
The compact, human-authored YAML or JSON file maintained in an application's repository. Maintainers author this file, which is then compiled into a canonical runtime definition using `islamu-configurator definition build`.

### Projection
The mapping rules declared in a definition that translate user answers into specific keys and structures inside an output artifact. For example, a projection might map the answer for `database.password` to the `.env` variable `DB_PASSWORD` or the JSON path `$.database.credentials.secret`.

### Revision
An integer incremented whenever a definition's content or schema changes within the same Family ID. Revisions allow clients to detect updates and ensure compatibility with specific releases of the target application.

### Secret Classification (`sensitivity: secret`)
A metadata flag marking a field as sensitive (e.g., passwords, private keys, API tokens). In all interfaces, secret fields are masked during display, excluded from non-secret drafts, protected by per-workspace consent in the browser, and saved to owner-only files on Linux.

### Secret-Deferred Draft
An incomplete configuration artifact generated intentionally without sensitive values. In a secret-deferred draft, secret fields are left as empty placeholders or comments, accompanied by an explicit list of locations where the operator must manually supply the secrets on the target server.

### Sensitivity Floor
An architectural rule guaranteeing that a rendering surface (such as the web browser) cannot demote or ignore a field's sensitivity level. If a field is declared `secret`, no client interface can treat it as `public` or log it in plaintext.

### Target Application
The external software being configured (such as an ISLAMU Event instance, Grafana server, Keycloak realm, or custom web app). Configurator generates the configuration files for the target application, but the target application remains entirely responsible for loading, validating, and applying those files.

### Target Authority
The administrative authority required to install, restart, or run the target application. Configurator explicitly possesses **no target authority**: it does not restart services, execute shell commands, or manage cloud instances.
