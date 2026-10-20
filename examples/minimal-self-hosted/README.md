# Minimal self-hosted service example

> **Audience:** Adopters | Contributors
> **Status:** Implemented
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-20
> **Source Anchors:** `examples/minimal-self-hosted/configurator.yaml`, `examples/minimal-self-hosted/setup-definition.json`, `examples/minimal-self-hosted/answers/complete.json`, `examples/minimal-self-hosted/expected/`

The [offline quickstart](../../docs/public/getting-started.md) runs this
definition through the CLI and saves a `.env` file plus JSON and YAML manifests
on a protected Linux path. No target service is included or started.

[`configurator.yaml`](configurator.yaml) is the compact maintainer source;
[`setup-definition.json`](setup-definition.json) is its generated runtime
definition. Both now describe revision **3** of family
`69cdb122-41d2-4fab-9b82-7929a889c726`, with canonical SHA-256 digest
`7153b8cf94be4f242d2d3b675ad2cbdb254969fbbd80c0f8e0c6afaf9c10477c`.
Revision 2 remains in Git history. See the
[integration guide](../../docs/public/integrate-your-tool.md).

## Explore the authored experiences

**Recommended** starts with essential decisions in guided order: service identity
and deployment, local service access, extended operations, then optional issuer
integration. Choose a deployment name and minimal or extended mode, then create
the required local service password. **Custom** starts at advanced disclosure,
uses section navigation, and orders operations before access. These are authored
labels and navigation choices, not answer presets or target policies.

Help, instructions, public examples and discovery terms travel inside the
admitted definition. For example, search for `deployment`, `password`,
`monitoring`, `workers` or `issuer` to find the associated settings. The public
name `local-service` and worker-count example `2` explain input; help text does
not submit answers. Only the existing public defaults (name, mode and disabled
metrics) are declared defaults. There are no secret defaults or recommendations.
Discovery and expanded disclosure should reveal settings without changing
answers, granting consent or making inactive settings active.

Progress distinguishes required decisions from optional settings. A missing
required password remains incomplete even if its section is hidden; leaving an
optional token or operations object absent is not a missing required answer.
Extended mode activates the existing advanced metrics toggle and optional
`service.extended` object. Its guided `workers` integer and advanced `logLevel`
choice demonstrate nested disclosure. Minimal mode deactivates that object and
metrics. Neither mode starts a service or changes the host network.

The required `service.credential` is a **password / create** credential: create
and retain it yourself, preferably in a password manager. The optional
`service.issuerToken` is an **opaque-token / issuer** credential. Leave it absent
unless adapting the example to an actual issuer-backed service; obtain a scoped
token through that issuer's documented administrator workflow and confirm
permissions and expiry there. This neutral example supplies no issuer, provider,
acquisition endpoint or invented documentation link. Configurator generates
neither credential and performs no authentication.

## Outputs and authority

All original fields, output keys, types, formats and create/restart declarations
are preserved. The only additional output keys are optional:
`SERVICE_ISSUER_TOKEN` in dotenv, plus `issuerToken` and nested `extended` in JSON
and YAML. They are omitted when unanswered; nested settings have no defaults.
The new settings are an illustrative first-party configuration contract, not a
claim that an included server implements workers, logging or issuer integration.
An adopter must map them to its actual target's supported configuration.

| Input or expected output | Path |
| --- | --- |
| Definition | [`setup-definition.json`](setup-definition.json) |
| Synthetic answers | [`answers/complete.json`](answers/complete.json) |
| Expected dotenv | [`expected/environment.env`](expected/environment.env) |
| Expected JSON | [`expected/service.json`](expected/service.json) |
| Expected YAML | [`expected/service.yaml`](expected/service.yaml) |

The sample credential is a synthetic placeholder. Replace it in a locally
protected answer file for real use. The [browser](../../docs/public/browser.md)
can prepare these files with transient answers: explicitly consent to enter
the required credential, or defer it in an incomplete draft and finish it
locally. Browser downloads cannot establish protected native-save permissions.
Target validation and application remain with the target software.

## Executed native evidence

The following commands ran from the repository root on 2026-10-20 using the
existing CLI binary, without compiling any project or running product tests:

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
cli=src/Configurator.Cli/bin/Release/net10.0/Configurator.Cli.dll
source=examples/minimal-self-hosted/configurator.yaml
runtime=examples/minimal-self-hosted/setup-definition.json
dotnet "$cli" --help
dotnet "$cli" definition --help
dotnet "$cli" doctor --machine
dotnet "$cli" definition check --help
dotnet "$cli" definition revise --help
dotnet "$cli" definition build --help
dotnet "$cli" definition check --source "$source" --machine
dotnet "$cli" definition revise --source "$source" --against "$runtime" --machine
work=$(mktemp -d "$TMPDIR/p4-neutral-example.XXXXXXXX")
dotnet "$cli" definition check --source "$source" --machine
dotnet "$cli" definition build --source "$source" \
  --output "$work/setup-definition.json" --machine
# Inspect the generated file, then apply its content to the tracked runtime.
dotnet "$cli" definition check --source "$source" --against "$runtime" --machine
dotnet "$cli" artifact bundle-check --definition "$runtime" \
  --artifact environment --artifact configuration --artifact configuration-yaml \
  --input examples/minimal-self-hosted/answers/complete.json --machine
sha256sum examples/minimal-self-hosted/expected/environment.env \
  examples/minimal-self-hosted/expected/service.json \
  examples/minimal-self-hosted/expected/service.yaml
git diff --check -- examples/minimal-self-hosted/configurator.yaml \
  examples/minimal-self-hosted/setup-definition.json \
  examples/minimal-self-hosted/README.md
```

Doctor confirmed `islamu-configurator-command/v2`; admission reported authoring
contract v1 and runtime schema v1. All commands in the block exited **0**.
The starting compact source declared revision 3 while the prior runtime declared
2. Native `revise` requires equal starting family/revision, so the curated source
was first aligned to the existing revision 2, then native revision editing
advanced it to 3 against the still-unmodified prior runtime. No identity was
allocated or invented. Build wrote the canonical public runtime to the unique
directory `p4-neutral-example.zdWJZYsq` under `TMPDIR`.

`check --source ... --against ...` is the native **canonical equality** check,
not a comparison of successive releases. It passed with the digest above after
the generated content was applied. Raw `cmp` found only the final newline added
by `apply_patch`: the generated file is 6273 bytes without that newline, while
the tracked file has the same JSON content followed by a newline. Canonical
content and identity are equal; literal byte equality is not claimed.

The read-only bundle check returned three restricted artifact digests with
`writeStatus: none`, zero diagnostics and `targetAcceptance: unassessed`.
Each digest exactly matched SHA-256 of the unchanged expected fixture:

| Artifact | SHA-256 |
| --- | --- |
| environment | `551f0116ffa430302fbf70ffec04e7d7b44d51690eae6f7ba8ccef51aef88f59` |
| configuration | `44499c22ed7fcfeafe85f36a2351de1769ab7df9ac2456635fca46ef7f56d026` |
| configuration-yaml | `3fea90d8ccc25505507bac67a9a222e4666914d72739c6345704b0d9b0f68bfe` |

Preparation was ready; the separate readiness report remained incomplete
pending host validation. No answer or expected-output file needed modification.
Preliminary command probes returned expected contract errors: treating runtime
JSON as `definition check --source` returned 65 (`definition-source-invalid`);
checking the original mismatched source/runtime returned 65
(`definition-revision-conflict`). `artifact validate` and `artifact preview`
probes with `--answers` but without required artifact/input arguments returned
64 (`artifact-required`, then `input-required`); the documented bundle-check
command above resolved that usage. Those probes did not write output.

YAML and JSON language servers were unavailable; native admission and equality
were used instead of installing dependencies. Browser rendering, discovery
interaction and progress UI are not verified by these native checks. No target
was run, and no protected output was saved.
