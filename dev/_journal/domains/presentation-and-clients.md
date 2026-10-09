# Presentation & Clients Findings

Last Updated: 2026-10-09 Europe/Brussels

Use [journal guidance](../README.md), [the finding template](../FINDING_TEMPLATE.md)
and [promotion rules](../PROMOTION_RULES.md) for recording and promotion.
This is Configurator-owned evidence; Event findings are not copied as
Configurator history.

## Durable Findings

Append observed target findings chronologically using the template.

[2026-10-09 Europe/Brussels] — Browser caches are separate from answer persistence

**Context**: While implementing `browser-complete-configuration`, the existing
browser capability ratchet described all persistence as disabled. Published PWA
assets and explicitly retained public definitions need retention without
changing private answer custody.

**Symptom / Observation**: The original capability file had `persistence=false`
and no separate public-cache capability. The new generator emits explicit
public asset/definition cache flags while keeping `persistence=false`,
`answerPersistence=false` and `nativeProtectedSave=false`. Native regeneration
produced all three ratchets successfully; generation alone is not platform QA.

**Root Cause**: A single persistence flag conflates public distribution assets
with transient private workspace state. Browser answer/output custody and
native protected-save support are separate contracts from static asset caching.

**Resolution**: Keep generator-owned capabilities explicit, require consent for
secret entry and user action for restricted export, and check the actual browser
package's Presentation/Core closure, PWA files and capability values.
`TMPDIR="$HOME/.cache/agent-tmp" dotnet run eng/configurator/GenerateConfiguratorRatchets.cs -- --write`
returned exit 0 and `Generated Configurator ratchets (3/3).` Exact published
network/cache/update evidence remains separately owned by Operations.

**Why This Matters for Future Work**: Do not interpret public offline retention
as permission to persist answers, send private worker messages, or offer native
filesystem guarantees. Consumers of capability metadata must distinguish these
flags rather than infer private capabilities from a PWA package.

**References**:

- `eng/configurator/GenerateConfiguratorRatchets.cs`
- `eng/configurator/generated/browser-release-capabilities.json`
- `tests/Configurator.Architecture.Tests/StandalonePackageTests.cs`
- `docs/internal/adr/ADR-browser-session-trust-and-export.md`
- `docs/internal/OPERATIONS.md#browser-custody-and-offline-verification`

Source register and source-free functional handoff: the browser ADR records
official Microsoft/MDN sources accessed through Firecrawl on 2026-10-09.
For this documentation/capability slice, only platform interface/lifecycle facts
informed requirements; no external implementation, prose, artwork or dependency
was copied. Generator layout, compiled/package checks and documentation were
independently designed around existing Configurator owners. AFC/SSO review
separates platform-required worker/manifest identifiers and common privacy
constraints from discretionary repository structure. This attests engineering
provenance, not legal certification or unexecuted browser evidence.

**Promotion Consideration**: Already reflected in the browser ADR, packaging
contract and browser/privacy rule twins; retain this entry as the source and
capability-separation record.

[2026-10-09 Europe/Brussels] — Browser download hints do not guarantee artifact names

**Context**: The parent execution track tested the actual published Browser
against the neutral independent dotenv/JSON/YAML vectors.

**Symptom / Observation**: Chrome downloaded the `.env` artifact as `env.txt`
even though the download link carried the declared `.env` filename hint. The
artifact-content checks passed; the filename was normalized by the browser.

**Root Cause**: Browser/OS download custody includes filename handling outside
the adapter's authority. Setting the download attribute does not establish
native filename or filesystem-permission guarantees.

**Resolution**: Public browser guidance requires checking the actual saved name
and renaming to the target's declared filename before use. No new archive format,
dependency or native-save fallback was added to conceal the platform boundary.

**Why This Matters for Future Work**: A correct synthetic output vector and
download hint are insufficient proof that a target can discover its expected
file. Real-browser QA must inspect both content and saved filename.

**References**:

- `src/Configurator.Browser/wwwroot/browser-artifacts.js`
- `docs/public/browser.md#copy-and-download`
- `docs/internal/LAUNCH_CONTRACT.md`
- `docs/internal/OPERATIONS.md#browser-custody-and-offline-verification`

**Promotion Consideration**: Already recorded in public/operator and technical
browser guidance; retain as observed platform evidence, not a guarantee of
identical normalization in every browser.
