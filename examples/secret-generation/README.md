# Synthetic local secret policies

> **Audience:** Integrators | Contributors | AI agents
> **Kind:** Guide
> **Status:** Current synthetic example; execution and host qualification are separate
> **Owner:** Core
> **Last Verified:** 2026-11-02
> **Source Anchors:** `configurator.yaml`, `src/Configurator.Core/Definitions/DefinitionSecretSpecification.cs`, `src/Configurator.Cli/Commands/SecretCommands.cs`

[configurator.yaml](configurator.yaml) describes a synthetic application accepting
a constrained password, a 32-byte Base64url token and an optional SHA-256 digest.
The checked answer fixtures contain deliberately public synthetic values, never
usable credentials. Their all-zero token demonstrates encoding/length validity,
not entropy or security. Do not deploy these values. No real target application
or credential issuer is involved.

The source's identity was allocated by native `definition init`. Its generated
[runtime](setup-definition.json) is maintained by the definition schema generator;
do not hand-edit it. Use native init for your own family rather than copying this
identity. Authoring/runtime, machine receipts and private answers use the fixed development version 0.0.1.

## Check the policy and answer states

From the repository root, use the freshly built CLI or the packaged executable.
The commands below use the built Release CLI and only public fixture paths:

```bash
dotnet src/Configurator.Cli/bin/Release/net10.0/Configurator.Cli.dll secret generate --help --machine
dotnet src/Configurator.Cli/bin/Release/net10.0/Configurator.Cli.dll definition check --source examples/secret-generation/configurator.yaml --machine
dotnet src/Configurator.Cli/bin/Release/net10.0/Configurator.Cli.dll artifact validate --definition examples/secret-generation/setup-definition.json --artifact config --input examples/secret-generation/answers-valid.json --machine
dotnet src/Configurator.Cli/bin/Release/net10.0/Configurator.Cli.dll artifact validate --definition examples/secret-generation/setup-definition.json --artifact config --input examples/secret-generation/answers-invalid.json --machine
dotnet src/Configurator.Cli/bin/Release/net10.0/Configurator.Cli.dll artifact validate --definition examples/secret-generation/setup-definition.json --artifact config --input examples/secret-generation/answers-deferred.json --machine
```

Expect the valid fixture to report local `preparation: ready`, the short password
to produce safe validation diagnostics, and the absent required secrets to be
incomplete. The separate overall readiness can remain incomplete because target
host validation is still required; `targetAcceptance` remains `unassessed`.
Missing secrets may be deferred by draft preparation; supplied malformed values
must remain invalid. These are different states, not three equivalent empty
inputs. To test policy rejection, create a disposable copy of the source and
add `sensitivity: public` beside a `secret` declaration; admission must reject
the contradiction. Never weaken the maintained policy to make invalid input pass.

## Generate into protected local answers

On a supported protected-save host, create a private temporary directory, then
generate one field at a time. No generated value goes to arguments or stdout:

```bash
umask 077
secret_example_dir=$(mktemp -d)
dotnet src/Configurator.Cli/bin/Release/net10.0/Configurator.Cli.dll secret generate --definition examples/secret-generation/setup-definition.json --pointer /password --input examples/secret-generation/answers-deferred.json --output "$secret_example_dir/password-answers.json" --machine
dotnet src/Configurator.Cli/bin/Release/net10.0/Configurator.Cli.dll secret generate --definition examples/secret-generation/setup-definition.json --pointer /token --input "$secret_example_dir/password-answers.json" --output "$secret_example_dir/complete-answers.json" --machine
dotnet src/Configurator.Cli/bin/Release/net10.0/Configurator.Cli.dll artifact validate --definition examples/secret-generation/setup-definition.json --artifact config --input "$secret_example_dir/complete-answers.json" --machine
```

Read safe receipt statuses and validate locally; do not print answer contents.
Reusing a destination must fail create-only publication without changing the
existing file. A local pass proves the declared policy, not a provider-issued
credential or target acceptance. Remove private test outputs deliberately after
review, without claiming secure erasure.

For the optional digest, `secret derive` accepts exactly one bounded
`--secret-input-file` or `--secret-input-stdin` transport. Never use a digest in
place of password hashing. Generated-input hash/digest recipes additionally need
a distinct protected `--generated-password-output` sidecar. See the
[CLI guide](../../docs/public/cli.md), [publication recovery](../../docs/public/native-output.md#original-passwords-and-hash-answers)
and [cryptographic format contract](../../docs/internal/SECRET_CRYPTOGRAPHY.md).
