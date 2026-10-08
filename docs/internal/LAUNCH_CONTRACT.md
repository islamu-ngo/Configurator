# Browser launch and public-template contract

Status: implemented browser unit of `configurator-extraction-r2`.

## Ownership and admission

Browser references Core and Blazor WASM only. `BrowserDefinitionRegistry` loads
embedded `Definitions/**/*.json` through `SetupDefinitionCodec.ParseJson` and
constructs the real `SetupDefinitionRegistry`. There is no Event wire/catalogue
or native artifact dependency. Parent-owned examples and Event exporters are
separate from the neutral browser bundle.

The full admitted definition is retained. A browser render restriction is not
a new definition variant: `FamilyId`, `Revision`, `Digest` and `CanonicalBytes`
are unchanged. Bundled metadata must be safe for public distribution, including
metadata associated with native-only fields. Core enforces closed admission,
sensitivity inheritance and restricted answer-derived artifacts.

## Launch and session pin

`Pages/Launch.razor` routes `/launch/{FamilyId}`.
`BrowserLaunchSelection.Resolve` requires nonzero lowercase D-format identity
and a closed query (1,024 encoded characters maximum). Each of `appVersion`
(64), `entry` (128), `locale` (35) and `digest` (64) occurs at most once; names
are compared after percent decoding. Selector characters are ASCII letters,
digits, dot, hyphen and underscore; digest additionally requires SHA-256 hex.
Empty values, malformed escapes, fragments, extra/duplicate keys and unknown
metadata reject with value-free codes.

Selection delegates family/version/digest compatibility to Core's registry.
Version matching is exact; highest compatible revision is selected only when
a version is explicit. Digest selects exact content. Without version or digest
multiple revisions reject. Omitted entry/locale select first declared metadata.
They do not select another document or certify translation/trust.

The selection holds the immutable admitted definition itself. Repeated renders
of the same URI retain it, even after registry replacement. A different URI
invalidates the editor immediately and creates a new keyed workspace. Registry
replacement validates old/current revision identities against new content to
reject conflicting digests. There is no asynchronous definition fetch.

## Closed public transfer boundary

`BrowserPublicTemplate` is distinct from `DefinitionArtifactComposer`, whose
answer-derived outputs remain restricted. Its schema is
`configurator-public-template/v1`; root properties are exactly `schemaVersion`,
`familyId`, `revision`, `definitionDigest`, `selections`. Identity must equal
the pinned definition, with canonical lowercase digest/family encoding.
Duplicate root or selection properties, additional members, unknown/private
fields, free text, non-scalar values, incorrect enum/boolean values and inactive
selections reject. Input is capped at 64 KiB and JSON depth 4. Output uses
deterministic root order and ordinal selection order with a matching byte cap.

`IsSelectable` requires Core effective sensitivity `Public` and type `enum` or
`boolean`. Core validation determines activation and validates supplied choices;
missing answers are allowed in this deliberately partial template. Native-only
fields never gain answer controls or downloads. Public question metadata can
be displayed without exposing answer entry. No full-readiness claim is made
when native-only required answers remain absent.

`PublicTemplateEditor` snapshots the pinned definition for each upload, reads
at most limit+1 bytes, cancels prior reads and commits only its current generation.
Navigation, definition replacement, new uploads, edits, clear and disposal
invalidate previous output. Deactivated choices are removed. Upload buffers are
zeroed on invalidation and in `finally`, including readers ignoring cancellation.
File inputs are replaced after completion/rejection to release file selection;
focus returns to the new input. Rejected input is never echoed.

## Hosting and verification ownership

Static hosting serves published `wwwroot` and provides SPA fallback for
`/launch/*`, without turning missing `_framework` resources into HTML.
The root base path is `/`; subpath deployments must update it consistently.
Boot requires static assets, and offline relaunch is not promised. No arbitrary
URL fetching, auth tokens, return URLs, target API, persistence, telemetry,
service worker or native I/O is supported. GUID/publisher identity confers no
trust or apply authority.

Browser tests use real bundled definition admission and the real registry,
with bUnit route/render tests and event-gated stale-upload races. Targeted
Release verification belongs to `Configurator.Browser` and
`Configurator.Browser.Tests`; final desktop/mobile real-browser QA belongs to
the parent workstream, not this unit.

Public contracts: [browser](../public/browser.md) and
[launch integration](../public/launch-integration.md). Core authority:
[definition contract](CORE_DEFINITION_CONTRACT.md).
