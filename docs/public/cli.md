# Command-Line Interface (CLI)

`islamu-configurator` is a high-performance, offline command-line tool. It reads declarative setup definitions and answer workspaces to deterministically validate and prepare configuration artifacts (`.env`, JSON, YAML).

The CLI is designed for headless servers, automation pipelines, and CI/CD workflows. It never contacts target servers or administers running systems.

> [!NOTE]
> **Getting Started:** If you are running Configurator for the first time with synthetic example files, see the [Prepare a configuration offline](getting-started.md) guide.

For portable bounded-v2 adoption, start with the
[integration workflow](integrating.md), then the
[definition reference](definition-reference.md) and
[cookbook](definition-cookbook.md). Public authoring/runtime contracts are v2,
private answers are v1, and machine receipts are v4; those versions are independent.
Machine v3 and its schema are retired without an adapter or alias.

## Qualified pre-release scope

Parent-reported standalone Linux x64 CLI qualification exercised complete
preparation, incomplete answers and create-only existing-file conflicts. This
documentation update did not rerun those exercises. Qualification does not
establish target acceptance, another host's protected-save support, installed
adoption, assistive-technology compatibility or stakeholder/scholarly proof.
The target still owns semantic validation and permission to apply the files.
See [packaging](packaging.md) for host boundaries and the
[technical conformance profile](../internal/DEFINITION_CONFORMANCE_PROFILE.md)
for the bounded definition mechanisms and limits.

---

## Invocation Basics & Confidentiality

The CLI operates in two mutually exclusive output modes:
- `--text`: Human-readable terminal output formatted using Spectre.Console.
- `--machine`: Emits exactly one machine-readable UTF-8 JSON envelope to stdout (max 64 KiB), followed by a single newline.

```bash
# Display human help
islamu-configurator --help

# Generate a configuration file with machine receipt
islamu-configurator artifact create \
  --definition setup.json \
  --artifact config \
  --input answers.json \
  --output service.json \
  --machine
```

### Confidentiality & Secret Handling

All answer-derived configuration output is **restricted**, even if individual fields are marked public. The CLI guarantees:
- **No Answers on stdout:** Generated configuration files are never written to standard output. Only public metadata (such as `catalogue list`) may use `--output -` in text mode.
- **No Answers in Arguments:** Answers cannot be passed as command-line arguments or ambient environment variables. They must be supplied via a local file or redirected standard input (`--input -`).
- **Value-Free Diagnostics:** Error messages contain structural RFC 6901 JSON pointers and diagnostic codes, never private user input or credential plaintext.

> [!IMPORTANT]
> **Linux Protected Save Contract (`0600`):**
> On Linux x64, the CLI uses a native create-only writer to publish files with owner-only (`0600`) permissions. Existing files are never overwritten. On unsupported hosts or unsafe targets, file writes fail closed.

---

## Command Reference

The CLI uses strict, case-sensitive option parsing:

| Command | Required Options | Optional Options | Description |
| --- | --- | --- | --- |
| `catalogue list` | `--definition`, `--output` | `--dry-run` | List all declared field keys and structural metadata |
| `catalogue show` | `--definition`, `--key`, `--output` | `--dry-run` | Inspect metadata for a single declared field |
| `catalogue describe` | `--definition`, `--key`, `--output` | `--dry-run` | Inspect detailed activation and constraint metadata |
| `env render` | `--definition`, `--artifact`, `--input`, `--output` | `--input-revision`, `--format`, `--dry-run` | Render a declared dotenv artifact from answers |
| `env validate` | `--definition`, `--artifact`, `--input` | `--input-revision` | Validate answer compatibility for a dotenv artifact |
| `artifact create` | `--definition`, `--artifact`, `--input`, `--output` | `--input-revision`, `--format`, `--dry-run`, `--answers` | Generate a declared configuration artifact |
| `artifact open` | `--definition`, `--artifact`, `--input` | `--input-revision`, `--answers` | Validate answers and stage output without writing |
| `artifact validate` | `--definition`, `--artifact`, `--input` | `--input-revision`, `--answers` | Verify answer constraints against schema rules |
| `artifact format` | `--definition`, `--artifact`, `--input`, `--output` | `--input-revision`, `--format`, `--dry-run`, `--answers` | Publish canonical formatting for an existing artifact |
| `artifact diff` | `--definition`, `--artifact`, `--input`, `--baseline` | `--input-revision`, `--answers` | Compare candidate answers against a baseline |
| `artifact coverage` | `--definition`, `--artifact`, `--input` | `--input-revision`, `--answers` | Report provided vs missing required fields |
| `artifact export` | `--definition`, `--artifact`, `--input`, `--output` | `--input-revision`, `--format`, `--dry-run`, `--answers` | Publish artifact using canonical output writer |
| `artifact import` | `--definition`, `--artifact`, `--input`, `--output`, `--expected-revision`, `--revision-field` | `--input-revision`, `--format`, `--dry-run`, `--answers` | Prepare an import artifact with explicit revision binding |
| `artifact check-existing` | `--definition`, `--artifact`, `--input` | `--answers`, `--input-revision` | Verify if a local file can be reverse-mapped to answers |
| `artifact compare` | `--definition`, `--artifact`, `--input` | `--detail`, `--offset`, `--limit`, `--expected-detail-digest` | Assess an existing file against the selected definition without changing answers or files |
| `artifact prefill` | `--definition`, `--artifact`, `--input`, `--output` | `--answers`, `--input-revision`, `--format`, `--dry-run` | Ingest local configuration into a JSON answer workspace |
| `artifact bundle-check` | `--definition`, repeated `--artifact`, `--input` | `--input-revision` | Validate multi-artifact bundle readiness |
| `artifact bundle-create` | `--definition`, repeated `--artifact`, `--input`, `--output` | `--input-revision`, `--dry-run` | Create all bundle artifacts in the target directory |
| `artifact preview` | `--definition`, `--artifact`, `--input`, `--output` | `--input-revision`, `--format`, `--dry-run` | Validate and write protected preview output |
| `doctor` | *(none)* | *(none)* | Check system writer availability and schema status |
| `definition init` | `--project-id`, `--name`, `--application-version` | `--root`, `--output`, `--format`, `--from-env-example`, `--from-schema` | Initialize a new maintainer definition |
| `definition check` | *(none)* | `--source`, `--root`, `--against` | Verify maintainer source syntax and canonical hash |
| `definition lint` | *(none)* | `--source`, `--root`, `--against`, `--detail`, `--offset`, `--limit`, `--expected-detail-digest` | Admit source and report bounded authoring warnings without publication |
| `definition schema` | *(none)* | `--kind` | Discover built-in public schemas without reading files |
| `definition build` | *(none)* | `--source`, `--root`, `--against`, `--output`, `--id-map` | Compile maintainer source into distribution JSON |
| `definition revise` | `--output` *(when changed)* | `--source`, `--root`, `--against` | Increment revision if source contents changed |

---

## Command Examples

### Authoring diagnostics and schema discovery

```bash
islamu-configurator definition lint --source configurator.yaml --machine \
  --detail diagnostics --limit 16
islamu-configurator definition schema --kind authoring --machine
islamu-configurator definition schema --kind command --text
```

Schema kinds are exactly `authoring` (default), `runtime`, `answers`, and
`command`. Machine discovery returns a `schema` object containing its kind, ID,
canonical digest, `bounded-v2` profile, capabilities, document, and independent
authoring/runtime/answer versions 2/2/1. Text mode prints only the public schema
document. Discovery takes no source, input, output or root authority.

Lint runs the same admission as check, then reports descriptive defaults,
advisory formats and missing output update/restart advice as warnings. Warnings
alone succeed; unsupported source still fails admission. Lint never revises or
publishes source. A bounded report overflow fails explicitly rather than hiding
unreported diagnostics.

Compiler diagnostics preserve their code and optional public `source` location:
schema pointer, zero-based document index and one-based start/end line/column.
Answer diagnostics can instead carry a revision-bound `correction` with a field
ID and typed `property`, `arrayRow`, or `mapEntry` segments. Map entries use
ordinals, never private keys. A numeric declared property remains a property.
Do not apply a correction after changing the input revision.

Locations may stop at the nearest representable ancestor: pointers are limited
to 256 characters; corrections to 32 segments and 256 aggregate declared-name
characters. For omitted diagnostics, request `--detail diagnostics`, advance by
the actual returned count, and send `--expected-detail-digest` on noninitial
pages. A digest mismatch means the public diagnostic view changed: restart at
offset zero. Values and native paths are never recovery identifiers.

### Catalogue Inspection

Inspect definition structure without supplying answers:

```bash
# List all declared fields to a JSON catalogue
islamu-configurator catalogue list \
  --definition setup.json \
  --output catalogue.json \
  --machine

# Describe constraints and activations for a specific field
islamu-configurator catalogue describe \
  --definition setup.json \
  --key service.port \
  --output field-details.json \
  --machine
```

### Environment Operations (dotenv)

Render or validate `.env` format artifacts declared in the definition:

```bash
# Validate answers for a declared environment artifact
islamu-configurator env validate \
  --definition setup.json \
  --artifact environment \
  --input answers.json \
  --machine

# Render the .env file to disk
islamu-configurator env render \
  --definition setup.json \
  --artifact environment \
  --input answers.json \
  --output .env \
  --machine
```

### Artifact Operations

Prepare, validate, diff, and generate application artifacts:

```bash
# Assess an existing configuration before attempting autofill
islamu-configurator artifact compare \
  --definition setup.json \
  --artifact config \
  --input existing-service.json \
  --detail findings --limit 16 \
  --machine

# Create a single protected configuration artifact
islamu-configurator artifact create \
  --definition setup.json \
  --artifact config \
  --input answers.json \
  --output service.json \
  --machine

# Prefill answers by ingesting an existing service.json file
islamu-configurator artifact prefill \
  --definition setup.json \
  --artifact config \
  --input existing-service.json \
  --output prefilled-answers.json \
  --machine

# Compare candidate answers against a baseline
islamu-configurator artifact diff \
  --definition setup.json \
  --artifact config \
  --input candidate-answers.json \
  --baseline baseline-answers.json \
  --machine

# Prepare an import artifact with explicit revision binding
islamu-configurator artifact import \
  --definition setup.json \
  --artifact import-spec \
  --input answers.json \
  --output import.json \
  --expected-revision 8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4 \
  --revision-field target_revision \
  --machine

# Atomically generate a multi-artifact bundle
islamu-configurator artifact bundle-create \
  --definition setup.json \
  --artifact config \
  --artifact environment \
  --input answers.json \
  --output ./prepared \
  --machine
```

### Existing-file assessment and recovery

`artifact compare` reports what the supplied file establishes, without inserting
initial values or preparing an answer workspace. Its `assessment` object separates
`compatibility`, `coverage`, `inventory` and `autofill`. Root `completion` describes
execution: a completed comparison can return exit 2 (`changes-required`) or exit 3
(`incomplete`). Standalone comparison always has `preparation: "not-evaluated"`,
workspace readiness `unassessed` and `targetAcceptance: "unassessed"`.

Compatibility is `compatible`, `changes-required`, `incomplete`, `unsupported` or
`invalid-input`. Coverage is `complete` or `incomplete`; inventory is `complete`
or `stopped`; autofill is `eligible` or `ineligible`. `retainedCount` is the number
of available findings, up to 4,096. A complete inventory also supplies exact
`total`; a stopped inventory omits `total` because its retained count is only a
lower bound. A short display page does not make the assessment incomplete.

Use `--detail findings` to retrieve stable classifications and codes with safe
source ordinals and declared schema locations. These are not workspace correction
tokens. Unknown member names and private values are never recovery coordinates.
Declared schema paths may stop at a bounded enclosing ancestor.

For every detail kind (`findings`, `diagnostics`, `coverage`), use the returned
`nextOffset`, not `offset + limit`, and pass `detail.digest` as
`--expected-detail-digest`. Legal offsets run from 0 through retained count,
including a terminal empty page, at most 4,096. Limits are 1–32, default 16.
`returnedCount` can be smaller than requested to preserve the shared 8,192-byte
detail/diagnostic budget within the 65,536-byte receipt. `terminal: true` has
`nextOffset: null`; a nonterminal page always advances. A finding that cannot
fit fails with `detail-item-unencodable`, never an empty repeating continuation.
The digest binds the sanitized assessment, not exact private bytes or authenticity:
two files differing only in private values can have the same digest.

Strict `prefill` remains a separate deliberate operation that creates a restricted
answer snapshot; `check-existing` checks that same adoption path. If comparison
completes but autofill refuses, repair a separate copy and re-run comparison before
prefilling. Keep the original unchanged. `diff` compares byte/projection results,
and `import` prepares revision-bound target output; neither is an alias for compare.
See [Import and compare](import-and-compare.md) for the complete workflow.

### Authoring Maintainer Definitions

Create, validate, compile, and increment setup definition sources:

```bash
# Initialize a new maintainer definition
islamu-configurator definition init \
  --project-id example.service \
  --name "Example Service" \
  --application-version 1.0.0 \
  --format yaml

# Check local source validity against canonical build
islamu-configurator definition check --machine

# Compile canonical runtime distribution JSON
islamu-configurator definition build --machine

# Increment revision after source edits
islamu-configurator definition revise \
  --output candidates/configurator.next.yaml \
  --machine
```

### System Doctor

Check environment capabilities, native writer availability, and schema authority:

```bash
islamu-configurator doctor --machine
```

---

## Machine Contract (`--machine`)

In machine mode, the CLI emits a single JSON object adhering to the `islamu-configurator-command/v4` schema. It provides deterministic, structured facts about operation results, file publication, validation diagnostics and existing-file assessment.

### 1. Successful Artifact Creation

```json
{
  "schemaVersion": "islamu-configurator-command/v4",
  "invocation": {
    "commandFamily": "artifact",
    "operation": "create",
    "mode": "machine"
  },
  "status": "success",
  "exitCategory": "success",
  "exitCode": 0,
  "dryRun": false,
  "completion": "completed",
  "preparation": "ready",
  "targetAcceptance": "unassessed",
  "cleanup": "completed",
  "delivery": "attempt",
  "diagnosticsTotal": 0,
  "diagnosticsOmitted": 0,
  "detailDigest": "a1b2c3d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef0",
  "diagnostics": [],
  "artifacts": [
    {
      "id": "config",
      "kind": "definition-artifact",
      "mediaType": "application/json",
      "digest": "8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4",
      "sensitivity": "restricted",
      "pathIntent": "output",
      "writeStatus": "written",
      "installation": "established",
      "verification": "succeeded"
    }
  ],
  "coverage": {
    "coveredCount": 14,
    "missingCount": 0,
    "detailDigest": "a1b2c3d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef0"
  },
  "readiness": {
    "state": "ready",
    "missingCount": 0,
    "blockedCount": 0
  }
}
```

### 2. Validation Failure with Diagnostics

When validation fails, diagnostics include fixed error codes, RFC 6901 structural paths, and source locations:

```json
{
  "schemaVersion": "islamu-configurator-command/v4",
  "invocation": {
    "commandFamily": "artifact",
    "operation": "validate",
    "mode": "machine"
  },
  "status": "validation",
  "exitCategory": "validation",
  "exitCode": 2,
  "dryRun": false,
  "completion": "failed",
  "preparation": "incomplete",
  "targetAcceptance": "unassessed",
  "cleanup": "completed",
  "delivery": "attempt",
  "diagnosticsTotal": 1,
  "diagnosticsOmitted": 0,
  "detailDigest": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "diagnostics": [
    {
      "code": "field-required-missing",
      "path": "/database/password",
      "severity": "error",
      "line": 4,
      "column": 5,
      "byteOffset": 82
    }
  ],
  "artifacts": [
    {
      "id": "config",
      "kind": "definition-artifact",
      "mediaType": "application/json",
      "digest": "0000000000000000000000000000000000000000000000000000000000000000",
      "sensitivity": "restricted",
      "pathIntent": "output",
      "writeStatus": "not-attempted",
      "installation": "not-attempted",
      "verification": "not-attempted"
    }
  ],
  "coverage": {
    "coveredCount": 8,
    "missingCount": 1,
    "detailDigest": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
  },
  "readiness": {
    "state": "incomplete",
    "missingCount": 1,
    "blockedCount": 0
  }
}
```

### 3. Paginated Detail Query

Query detailed coverage or diagnostics using pagination options (`--detail coverage --offset 0 --limit 16`):

```json
{
  "schemaVersion": "islamu-configurator-command/v4",
  "invocation": {
    "commandFamily": "artifact",
    "operation": "validate",
    "mode": "machine"
  },
  "status": "success",
  "exitCategory": "success",
  "exitCode": 0,
  "dryRun": false,
  "completion": "completed",
  "preparation": "ready",
  "targetAcceptance": "unassessed",
  "cleanup": "completed",
  "delivery": "attempt",
  "diagnosticsTotal": 0,
  "diagnosticsOmitted": 0,
  "detailDigest": "9b12c8a34ef018293a4b5c6d7e8f90123456789abcdef0123456789abcdef01",
  "detail": {
    "kind": "coverage",
    "offset": 0,
    "limit": 2,
    "total": 2,
    "retainedCount": 2,
    "returnedCount": 2,
    "nextOffset": null,
    "terminal": true,
    "digest": "9b12c8a34ef018293a4b5c6d7e8f90123456789abcdef0123456789abcdef01",
    "diagnostics": [],
    "findings": [],
    "coverage": [
      {
        "id": "service.name",
        "state": "covered"
      },
      {
        "id": "database.port",
        "state": "covered"
      }
    ]
  },
  "diagnostics": [],
  "artifacts": [],
  "coverage": {
    "coveredCount": 2,
    "missingCount": 0,
    "detailDigest": "9b12c8a34ef018293a4b5c6d7e8f90123456789abcdef0123456789abcdef01"
  },
  "readiness": {
    "state": "ready",
    "missingCount": 0,
    "blockedCount": 0
  }
}
```

---

## Exit Codes & Status Categories

The CLI uses standard, predictable POSIX-compatible exit codes:

| Exit Code | Category | Meaning |
| --- | --- | --- |
| `0` | `success` | Operation completed successfully |
| `2` | `validation` | Field/projection constraints failed, or comparison established required changes even with incomplete coverage |
| `3` | `incomplete` | Required answers are missing, or comparison coverage/evaluation is incomplete without known blockers |
| `4` | `blocked` | Privacy/host boundary refused, or the selected inverse is unsupported |
| `64` | `usage` | Invalid command-line arguments or syntax error |
| `65` | `data` | Malformed input/admission refusal, stale detail digest or explicit page-reporting failure |
| `70` | `internal` | Unexpected internal engine failure |
| `74` | `io` | I/O failure (file bounds exceeded, protected write blocked, or stdout broken) |
| `130` | `cancelled` | Operation cancelled cooperatively via `SIGINT` (`Ctrl+C`) or `SIGTERM` |

For advanced authoring and schema guidelines, see the
[Authoring Integration Guide](integrate-your-tool.md),
[bounded patterns](definition-cookbook.md), and [Packaging Guide](packaging.md).
