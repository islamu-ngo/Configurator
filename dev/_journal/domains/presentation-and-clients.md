# Presentation & Clients Findings

Last Updated: 2026-10-15 Europe/Brussels

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

[2026-10-15 Europe/Brussels] — Cancellation separates private readiness from physical completion

**Context**: Terminal hardening introduced asynchronous local import and truthful
protected-publication recovery using the existing shared workspace and writer.

**Symptom / Observation**: Faulting completion observers could leave accepted
private bytes live. Cancelling a workspace could finish its visible operation
while a writer had already created files. Cleanup failure could hide a Written
status, and an already-empty private clear could admit a pending secret import.

**Root Cause**: One visible operation state cannot represent accepted-outcome
ownership, cancellation callback retirement, owner-side import admission and
irreversible native publication. Cancellation also does not undo earlier effects.

**Resolution**: Match active and accepted outcome identities atomically; preserve
worker/callback ownership until retirement and deliver notifications outside
gates. Clear private roots in one revision even when empty to fence pending
imports. Record native status before observers, cancellation checks and cleanup.
Keep bounded value-free publication receipts independent of current private
readiness, and serialize physical imports/publications until their owned work
finishes. Deterministic barriers reproduced the faults; supported-host tests
verify every committed prefix against actual files and modes.

**Why This Matters for Future Work**: UI cancellation can release the visible
workspace without ending physical work. Never use a stale-result fence as proof
of rollback, buffer retirement or filesystem state. Fault cleanup must target
its accepted result without clearing a reentrant replacement. Recovery status
must retain known success even when subsequent observation or cleanup fails.

**References**:

- `src/Configurator.Presentation/Presentation/SetupPresentationWorkspace.cs`
- `src/Configurator.Terminal/SetupTerminalImportOperation.cs`
- `src/Configurator.Terminal/SetupTerminalArtifactOperation.cs`
- `src/Configurator.Artifacts/ProtectedArtifactBundlePublisher.cs`
- `tests/Configurator.Presentation.Tests/SetupPresentationModelTests.cs`
- `tests/Configurator.Terminal.Tests/SetupTerminalPublicationReceiptTests.cs`
- [Terminal lifetime ADR](../../../docs/internal/adr/ADR-terminal-editing-and-publication-lifetimes.md)

**Promotion Consideration**: The ADR and existing Presentation/Terminal/protected
output anchors contain the maintained contracts. Retain this entry as the
observed root-cause record. Official API facts and independent implementation
provenance are recorded in the ADR; native terminal qualification remains a
separate release-support gate.
