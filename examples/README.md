# Maintained definition examples

These public sources demonstrate portable authoring v2 and canonical runtime v2.
They can be checked and compiled locally without a Store, GitHub access, target
service, sibling Event checkout or remote credentials. Use
[portable adoption](../docs/public/integrating.md) to allocate your own identity
and the [cookbook](../docs/public/definition-cookbook.md) for partial patterns.
Do not copy example family UUIDs into a new integration.

## Source and canonical runtime pairs

| Example | Handwritten source | Generated runtime | What to learn |
|---|---|---|---|
| Minimal self-hosted | [configurator.yaml](minimal-self-hosted/configurator.yaml) | [setup-definition.json](minimal-self-hosted/setup-definition.json) | Exact dotted keys, root applicability, optional nested settings, journeys, public help, required secret and dotenv/JSON/YAML bindings |
| Grafana OSS 13.2.2 | [configurator.yaml](grafana-13.2.2/configurator.yaml) | [setup-definition.json](grafana-13.2.2/setup-definition.json) | Independent synthetic environment/provisioning projections; see [scope and provenance](grafana-13.2.2/README.md) |
| ISLAMU Event | [configurator.json](islamu-event/configurator.json) | [definition.json](islamu-event/definition.json) | Larger neutral configuration, ordered tenant rows and five target payload families; see [Event boundary](islamu-event/README.md) |

Sources use `$schema: urn:islamu:configurator:authoring:v2`; runtimes use
`schemaVersion: 2`, `profile: bounded-v2` and computed sorted
`requiredCapabilities`. Command receipts use v3 and synthetic private answer
fixtures use `answerContractVersion: 1`. These version axes are independent.
Answers/expected output fixtures are synthetic reproduction inputs, not Browser
assets, public defaults or authority to inspect real private configuration.
All answer-derived output remains restricted even when a fixture uses only
public synthetic values.

## Generate from the source, not the runtime

From the repository root, the native generator owns all three pairs:

```bash
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator -- --write-examples
dotnet run --project eng/configurator/Configurator.DefinitionSchemaGenerator -- --check-examples
```

The first command compiles sources into canonical runtime files. The second
compares those bytes without writing and reports source/runtime agreement.
These are reproduction instructions. All three source/runtime pairs passed the
native byte-freshness check on 2026-10-28.

Do not hand-edit runtime JSON or advance example identity to repair freshness.
`--renew-example-families` deliberately forks all three through the native
identity editor and writes new source/runtime identities; it is not an ordinary
check. In your own project, use native `definition init`, `init --fork-from`,
check/build and revise as described in the adoption guide. Retain prior canonical
revision anchors and never reuse an admitted family/revision for changed content.
The [schema catalog](../schemas/README.md) separates structural schema generation
from source/runtime checking.

## Maintained behavioral pattern anchors

Complete runnable fixtures belong in tests rather than duplicated raw documents
in prose. The cookbook links these sources by mechanism:

| Pattern | Maintained source anchor |
|---|---|
| Typed null, omission, reset, suggestions and localized choice identity | [DefinitionTypedEditingTests.cs](../tests/Configurator.Presentation.Tests/DefinitionTypedEditingTests.cs) |
| Row-local applicability and conditional presence, typed maps, discriminator branches, local references, opt-in formats | [DefinitionProfileConformanceTests.cs](../tests/Configurator.Core.Tests/Definitions/DefinitionProfileConformanceTests.cs) |
| Ordered object rows and uniqueness | [DefinitionObjectListTests.cs](../tests/Configurator.Core.Tests/Definitions/DefinitionObjectListTests.cs) |
| Complete versus secret-deferred composition | [DefinitionDraftArtifactTests.cs](../tests/Configurator.Core.Tests/Definitions/DefinitionDraftArtifactTests.cs) |
| Narrow direct-copy inverse import and atomic refusal | [DefinitionImportTests.cs](../tests/Configurator.Core.Tests/Definitions/DefinitionImportTests.cs) |
| Identity allocation/revision mechanics | [DefinitionAuthoringIdentityTests.cs](../tests/Configurator.Core.Tests/Definitions/DefinitionAuthoringIdentityTests.cs) |

This index identifies source coverage, not a claim that every fixture, adapter
or physical package has passed. Admission and deterministic compilation do not
establish target startup, provisioning, connectivity, authorization or apply.
Grafana acceptance remains unproved by its projections. Event's historical v1
release receipt is not a current v2 compiler pin, family/digest or host-acceptance
receipt. Preserve it as history rather than relabeling it as new evidence.

Qualify your chosen CLI, Terminal, Desktop or Browser workflow separately.
Native protected saving and Browser deliberate copy/download are different
custody guarantees. Complete synthetic preparation is not a target certificate;
a secret-deferred draft remains incomplete. See
[Operations](../docs/internal/OPERATIONS.md) for supported-host evidence and limits.
