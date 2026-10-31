# Command-Line Interface (CLI)

`islamu-configurator` is an offline command-line preparation tool. It reads
bounded declarative definitions and private answer workspaces to validate and
prepare declared configuration artifacts (`.env`, JSON, YAML).

The CLI is designed for headless servers, automation pipelines, and CI/CD workflows. It never contacts target servers or administers running systems.

> [!NOTE]
> **Getting Started:** If you are running Configurator for the first time with synthetic example files, see the [Prepare a configuration offline](getting-started.md) guide.

For portable bounded-v2 adoption, start with the
[integration workflow](integrating.md), then the
[definition reference](definition-reference.md) and
[cookbook](definition-cookbook.md). Public authoring/runtime contracts are v2,
private answers are v1, and machine receipts are v4; those versions are independent.
Machine v3 and its schema are retired without an adapter or alias.

For exact command, operation and option vocabulary and declared bounds, use the
[generated command reference](cli-command-reference.md). It projects the current
machine schema, not option meaning or host qualification. The procedures below
and the [technical contract](../internal/CLI_CONTRACT.md) own those explanations.

## Qualified pre-release scope

The repository records standalone Linux x64 CLI qualification for complete
preparation, incomplete answers and create-only existing-file conflicts. This
source-based documentation correction did not rerun those exercises. Consult
[current status](../internal/STATUS.md) and the
[operations record](../internal/OPERATIONS.md#definition-v2-qualification) for
the exact evidence and its contract versions. Qualification does not
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
- `--machine`: Healthy delivery emits one UTF-8 JSON envelope and one trailing
  newline, together at most 65,536 bytes. A broken stream may leave partial
  output; no replacement envelope or automatic replay follows.

All command blocks below use **Bash**, with a current packaged
`islamu-configurator` on `PATH`, from the checkout root. For source invocation,
complete [build prerequisites](../../PROJECTS.md), then replace that executable
with `dotnet run --project src/Configurator.Cli --configuration Release --`;
this may restore/build and is not an offline installation procedure.
Read-only operations can be rerun. Write examples require an existing trusted
parent directory and an absent destination filename; they never overwrite.
Choose fresh destinations on a rerun and inspect partial results first.

```bash
# Display human help
islamu-configurator --help

# Generate a configuration file with machine receipt
islamu-configurator artifact create \
  --definition examples/minimal-self-hosted/setup-definition.json \
  --artifact configuration \
  --input examples/minimal-self-hosted/answers/complete.json \
  --output ./prepared-service.json \
  --machine
```

This runnable example uses the maintained synthetic fixture and writes one
restricted file on a supported Linux host; it does not configure a live service.
For the remaining runnable examples, define these Bash variables once:

```bash
definition=examples/minimal-self-hosted/setup-definition.json
answers=examples/minimal-self-hosted/answers/complete.json
existing=examples/minimal-self-hosted/expected/service.json
```

### Private answer format and presence

The following is the maintained `answers/complete.json` fixture, formatted for
reading, not an unwrapped answer object:

```json
{
  "answerContractVersion": 1,
  "values": {
    "metrics.enabled": true,
    "service.credential": "synthetic-placeholder",
    "service.mode": "extended",
    "service.name": "sample"
  },
  "omitted": []
}
```

Values use exact schema property names, not generated presentation field IDs.
`service.name` is a literal root name, selected by `/service.name`, not
`/service/name`. Names are case-sensitive and are not Unicode-normalized.
Omissions are concrete RFC 6901 pointers (`~0` escapes tilde; `~1` escapes slash).
Supplied and omitted locations cannot conflict, and omissions cannot overlap.

Required means presence, not nonblank: false, zero, empty strings and empty
containers remain supplied values, subject to their constraints. Null counts as
present only where nullable. Standard `default` is descriptive and never
inserted. `x-configurator.initialValue` initializes eligible missing values;
explicit omission suppresses it. Reset removes the override and omission,
restoring initialization eligibility. Suggested values require deliberate
adoption. Children cannot initialize an absent optional container into existence.
The fixture's literal keys and private v1 envelope are independent of runtime v2
and command v4.

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
| `env render` | `--definition`, `--artifact`, `--input`, `--output` | `--input-revision`, `--format`, `--dry-run`, `--answers` | Render a declared dotenv artifact from answers |
| `env validate` | `--definition`, `--artifact`, `--input` | `--input-revision`, `--answers` | Validate answer compatibility for a dotenv artifact |
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
| `artifact preview` | `--definition`, `--artifact`, `--input`, `--output` | `--input-revision`, `--format`, `--dry-run`, `--answers` | Validate and write protected preview output |
| `doctor` | *(none)* | *(none)* | Check system writer availability and schema status |
| `definition init` | `--project-id`, `--name`, `--application-version` (unless forking) | `--root`, `--output`, `--format`, `--fork-from`, `--from-env-example`, `--from-schema`, `--accept-bounded-semantics` | Initialize a source, or fork to a new family with explicit output |
| `definition check` | *(none)* | `--source`, `--root`, `--against` | Verify maintainer source syntax and canonical hash |
| `definition lint` | *(none)* | `--source`, `--root`, `--against`, `--detail`, `--offset`, `--limit`, `--expected-detail-digest` | Admit source and report bounded authoring warnings without publication |
| `definition schema` | *(none)* | `--kind` | Discover built-in public schemas without reading files |
| `definition build` | *(none)* | `--source`, `--root`, `--against`, `--output`, `--id-map` | Compile maintainer source into distribution JSON |
| `definition revise` | `--output` *(when changed)* | `--source`, `--root`, `--against` | Increment revision if source contents changed |

`--machine` and `--text` are common mode options. Output is optional on
`--dry-run` operations; no destination preflight or publication occurs.
`--format` must match the selected artifact, not request conversion.
`--input-revision` is a nonnegative local snapshot revision, not the definition
revision or target revision hash.

Detail options (`--detail`, `--offset`, `--limit`,
`--expected-detail-digest`) are for read-only `definition check|lint` and
`artifact validate|check-existing|bundle-check|compare`; they require machine
mode. `findings` needs an assessment. Bundle selection takes up to 64 distinct,
repeated `--artifact` flags, not CSV `--artifacts`.

Normally `--input` supplies private answers. For `check-existing`, `compare`
and `prefill`, it supplies an existing projected file. On `check-existing` and
`prefill`, `--answers` optionally supplies the current workspace for conflict
checks. On other operations admitting `--answers`, it supplies answers while
`--input` becomes an artifact to verify against recomposition; `format` writes
canonical composed bytes, not arbitrary reformatted input. At most one private
input can use redirected stdin; definitions must be local files. Interactive
stdin is rejected without prompting.

---

## Command Examples

### Authoring diagnostics and schema discovery

```bash
islamu-configurator definition lint \
  --source examples/minimal-self-hosted/configurator.yaml --machine \
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
characters. For omitted diagnostics, request `--detail diagnostics`, use the
returned `nextOffset`, and send `--expected-detail-digest` on noninitial
pages. A digest mismatch means the public diagnostic view changed: restart at
offset zero. Values and native paths are never recovery identifiers.

### Catalogue Inspection

Inspect definition structure without supplying answers:

```bash
# List all declared fields to a JSON catalogue
islamu-configurator catalogue list \
  --definition "$definition" \
  --output catalogue.json \
  --machine

# Describe constraints and activations for a specific field
islamu-configurator catalogue describe \
  --definition "$definition" \
  --key service.name \
  --output field-details.json \
  --machine
```

### Environment Operations (dotenv)

Render or validate `.env` format artifacts declared in the definition:

```bash
# Validate answers for a declared environment artifact
islamu-configurator env validate \
  --definition "$definition" \
  --artifact environment \
  --input "$answers" \
  --machine

# Render the .env file to disk
islamu-configurator env render \
  --definition "$definition" \
  --artifact environment \
  --input "$answers" \
  --output ./prepared.env \
  --machine
```

### Artifact Operations

Prepare, validate, diff, and generate application artifacts:

```bash
# Assess an existing configuration before attempting autofill
islamu-configurator artifact compare \
  --definition "$definition" \
  --artifact configuration \
  --input "$existing" \
  --detail findings --limit 16 \
  --machine

# Create a single protected configuration artifact
islamu-configurator artifact create \
  --definition "$definition" \
  --artifact configuration \
  --input "$answers" \
  --output ./prepared-service-2.json \
  --machine

# Prefill answers by ingesting an existing service.json file
islamu-configurator artifact prefill \
  --definition "$definition" \
  --artifact configuration \
  --input "$existing" \
  --output prefilled-answers.json \
  --machine

# Compare workspaces; the same maintained fixture establishes equality
islamu-configurator artifact diff \
  --definition "$definition" \
  --artifact configuration \
  --input "$answers" \
  --baseline "$answers" \
  --machine
```

The following is an **invocation template**, not runnable with the neutral
fixture: `import-definition.json` must declare an import-semantic artifact and
an active required string revision field copied unconditionally to a root
output member. Supply the target's expected digest (or supported `absent`
marker), a compatible private v1 answer file, and a fresh destination:

```bash
islamu-configurator artifact import \
  --definition import-definition.json \
  --artifact import-spec \
  --input answers.json \
  --output import.json \
  --expected-revision 8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4 \
  --revision-field target_revision \
  --machine
```

This runnable bundle example composes first, preflights all destinations, then
publishes sequentially. It is **not a whole-bundle transaction**; a later failure
can leave a written prefix. `mktemp` gives each rerun a fresh private directory:

```bash
destination=$(mktemp -d "$HOME/configurator-bundle.XXXXXXXX")
islamu-configurator artifact bundle-create \
  --definition "$definition" \
  --artifact configuration \
  --artifact environment \
  --input "$answers" \
  --output "$destination" \
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

This is a **Bash authoring workflow template**. Use a fresh local project
directory with an existing `candidates/` subdirectory; do not run it over the
maintained fixtures. `init` allocates a fresh family and writes public source,
`check` is read-only, and `build` writes public runtime JSON. After an actual
source edit, `revise` writes a separate create-only candidate; it does not replace
the original. Inspect/adopt the candidate before building the new revision.
Rerun checks freely; use fresh output paths for changed candidates.

```bash
# Initialize a new maintainer definition
islamu-configurator definition init \
  --project-id example.service \
  --name "Example Service" \
  --application-version 1.0.0 \
  --format yaml

# Check local source admission
islamu-configurator definition check --machine

# Compile canonical runtime distribution JSON
islamu-configurator definition build --machine

# After editing source, create a separately reviewable next revision
islamu-configurator definition revise \
  --output candidates/configurator.next.yaml \
  --machine
```

### System Doctor

Check environment capabilities, native writer availability, and schema authority:

```bash
islamu-configurator doctor --machine
```

Doctor does not probe the selected output filesystem, qualify another host,
or establish that target software will accept an artifact.

---

## Machine Contract (`--machine`)

In machine mode, the CLI emits a single JSON object adhering to the `islamu-configurator-command/v4` schema. It provides deterministic, structured facts about operation results, file publication, validation diagnostics and existing-file assessment.

### 1. Successful Artifact Creation

The JSON examples in this section are **illustrative receipt shapes**, not
captured command results. Digests are stand-ins and counts describe the example;
do not use them as fixture expectations. The generated v4 schema, not a
documentation excerpt, defines the complete machine contract.

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
  "detailDigest": "0000000000000000000000000000000000000000000000000000000000000000",
  "diagnostics": [],
  "artifacts": [
    {
      "id": "configuration",
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
    "coveredCount": 4,
    "missingCount": 0,
    "detailDigest": "0000000000000000000000000000000000000000000000000000000000000000"
  },
  "readiness": {
    "state": "incomplete",
    "missingCount": 1,
    "blockedCount": 0
  }
}
```

`preparation: "ready"` means local composition succeeded. Root readiness remains
incomplete because host validation is required; `targetAcceptance` remains
unassessed. `written` records installation, not proof of successful verification
or cleanup. Check those independent facts and the process exit. Receipt delivery
is an attempt, not a self-attested acknowledgment by its consumer.

### 2. Incomplete Answers with Diagnostics

For a missing required credential, single-artifact validation returns incomplete
(exit 3). Answer diagnostics use value-free field metadata and typed corrections;
compiler diagnostics instead carry optional public source coordinates.
Prose `message` fields are not part of the diagnostic contract:

```json
{
  "schemaVersion": "islamu-configurator-command/v4",
  "invocation": {
    "commandFamily": "artifact",
    "operation": "validate",
    "mode": "machine"
  },
  "status": "incomplete",
  "exitCategory": "incomplete",
  "exitCode": 3,
  "dryRun": false,
  "completion": "failed",
  "preparation": "incomplete",
  "targetAcceptance": "unassessed",
  "cleanup": "not-required",
  "delivery": "attempt",
  "diagnosticsTotal": 1,
  "diagnosticsOmitted": 0,
  "detailDigest": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "diagnostics": [
    {
      "code": "value-required",
      "path": "/input",
      "severity": "error",
      "correction": {
        "inputRevision": 0,
        "fieldId": "service.credential",
        "segments": []
      }
    }
  ],
  "artifacts": [],
  "coverage": {
    "coveredCount": 3,
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

Query detailed coverage or diagnostics on a read-only operation. This
illustrative first page requests `--detail coverage --offset 0 --limit 2` and
returns two of four covered fields; continue at `nextOffset: 2` with the returned
digest, not the requested page size:

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
  "cleanup": "not-required",
  "delivery": "attempt",
  "diagnosticsTotal": 0,
  "diagnosticsOmitted": 0,
  "detailDigest": "0000000000000000000000000000000000000000000000000000000000000000",
  "detail": {
    "kind": "coverage",
    "offset": 0,
    "limit": 2,
    "total": 4,
    "retainedCount": 4,
    "returnedCount": 2,
    "nextOffset": 2,
    "terminal": false,
    "digest": "0000000000000000000000000000000000000000000000000000000000000000",
    "diagnostics": [],
    "findings": [],
    "coverage": [
      {
        "id": "metrics.enabled",
        "state": "covered"
      },
      {
        "id": "service.credential",
        "state": "covered"
      }
    ]
  },
  "diagnostics": [],
  "artifacts": [
    {
      "id": "configuration",
      "kind": "definition-artifact",
      "mediaType": "application/json",
      "digest": "44499c22ed7fcfeafe85f36a2351de1769ab7df9ac2456635fca46ef7f56d026",
      "sensitivity": "restricted",
      "pathIntent": "input",
      "writeStatus": "none",
      "installation": "not-applicable",
      "verification": "not-applicable"
    }
  ],
  "coverage": {
    "coveredCount": 4,
    "missingCount": 0,
    "detailDigest": "0000000000000000000000000000000000000000000000000000000000000000"
  },
  "readiness": {
    "state": "incomplete",
    "missingCount": 1,
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
