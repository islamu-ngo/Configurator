# Blazor Browser Production Readiness and First Public V1

Last Updated: 2026-10-19

## Review Metadata

- Mode: standalone
- Subject: ISLAMU Configurator Browser, publicly hosted anonymous static application
- Workstream: none
- Report kind: detailed-audit
- Report status: current
- Disposition: changes-required
- Evidence cutoff: 2026-10-19
- Reviewed input: clean product working tree at Git commit `37bfa9b492f54f0e746150edc9a624ad668774b3`; current source, tests, policies and local publication inspected in this session
- Supersedes: none; the October 18 feature-completeness audit has a different purpose and remains historical input
- Agreed scope: user selected **Public static app**; no backward-compatibility requirement
- Decision: **NO-GO for production v1 at the reviewed revision**

## Executive Summary

**Do not launch this revision as production v1. Keep the standalone WebAssembly architecture and finish a bounded reliability, privacy and release-operations increment. A backend rewrite is neither necessary nor justified.**

The browser already performs substantial configuration preparation. It admits public definitions, renders typed fields and collections, uses the shared validation/composition engine, distinguishes incomplete drafts from complete preparation, requires workspace-specific secret consent, and offers deliberate private exports. It is not merely a catalogue or a UI prototype. Its static hosting model is an appropriate way to preserve local answer processing.

However, green tests do not establish production readiness. This session ran **135 Browser tests successfully**, published a fresh static package, and passed **two package-test rows**. Actual browser use nevertheless demonstrated:

1. The Home page's filled **Open configuration** link has identical foreground and background colors: **1:1 text contrast**, invisible in settled light and dark appearance.
2. Importing `{"name":"synthetic-audit-name","optional":""}` reports success, but preparing the supported JSON artifact produces `{"name":"synthetic-audit-name"}`. The explicit empty string disappears.
3. Supplying an optional object with a required child makes preparation fail; the UI has no action to omit that object again while preserving unrelated answers.
4. The resulting validation message identifies the object but gives only a generic instruction to check its constraints.

A separate source-level review found a more consequential privacy problem: consent revocation waits for asynchronous export cleanup before denying secret entry. An intervening edit can advance the editor generation and make the revocation continuation abandon the denial. This race was **not reproduced under a controlled browser schedule**; the source/control-flow evidence is sufficient to require a deterministic regression and correction before release, not to claim observed secret exfiltration.

The public service is also not qualified. The repository builds unsigned validation packages; it does not demonstrate a production HTTPS origin, enforced host headers, promotion of exact validated bytes, installed-worker rollback, staffed incident response, or release-specific browser support. The app pins the WebAssembly runtime and Microsoft packages to `10.0.10`, while the official October 13, 2026 servicing release is `10.0.13`. The current NuGet audit returned no reported vulnerabilities; that does not establish that every shipped runtime component is current or unaffected.

The provider-responsibility conclusion is practical: users are entrusting deployment information and possibly secrets to software delivered by ISLAMU. Local processing reduces server custody, but it increases the importance of trustworthy delivery, effective revocation, truthful limitations, usable correction paths and maintainable updates. These are Trust, Non-Harm, Rights of People, Truthfulness and Excellence concerns, not reasons to add accounts or surveillance.

This report is current for its named evidence revision and requires changes. It is not release approval, an external penetration test, a WCAG conformance statement, a religious ruling, or operational certification. The minimum release gates below are intentionally narrower than complete cross-client capability parity.

## Scope

### The release being assessed

An ISLAMU-operated, anonymous HTTPS site serving the complete published `Configurator.Browser` static tree at an origin root. Editing, validation and composition remain in the browser. The optional definition store is public content; independent local-file and publisher-link entry remain first-class.

The operator journey is:

1. Choose or admit a definition and review its identity.
2. Start a transient local workspace.
3. Enter ordinary settings; optionally consent to masked secret entry.
4. Correct errors and prepare complete files or clearly incomplete secret-deferred drafts.
5. Deliberately reveal, copy or download artifacts.
6. Leave or clear the workspace without stale answers, output or operations returning.

Review covered architecture, data custody, browser lifecycle, form semantics, accessibility, recovery, dependency servicing, packaging, CI, hosting, deployment, support, governance and evaluation. It included three independent read-only source-audit tracks, with parent verification of the material mechanisms and bounded published-browser reproduction.

### Scope exclusions

- No implementation fixes, commits, release tags, remote publication or deployment were authorized or performed.
- No accounts, server-held answers, target authentication, target apply, databases, BFF, tenancy or Event backend are launch requirements.
- Native protected saves, native-platform qualification and full maintainer-tool parity are separate products or workstreams.
- No backward-compatibility adapters, legacy routes or obsolete schemas should be preserved merely for compatibility.
- No real credentials, operator answers or private deployment configurations were used.
- A live public origin, hosting account, branch-protection settings, support mailbox operation and organizational contracts were not inspected.

## Claim Boundary

**Observed** below means exercised in the current locally published application or physically executed command. **Source-supported** means a concrete current control/data flow was inspected but its adverse schedule was not executed. **Missing evidence** means readiness is unproven, not that an unseen deployed system is defective.

I-VSD supplies provider-responsibility design reasoning and implementation traceability. Stakeholder validation and production operational validation are missing. Qualified Sunni scholars retain religious-legal authority; no halal, haram, riba, Sharia-compliance or certification conclusion is made here.

There is no demonstrated P0 compromise or observed answer-bearing exfiltration in this audit. The release recommendation is still no-go: a production claim cannot reasonably coexist with the identified privacy, answer-fidelity, accessibility and operating gaps.

## Current Architecture and Readiness

### Control and data flow

`Program.cs` boots `WebAssemblyHostBuilder`, registers MudBlazor and browser-owned services, initializes `BrowserOfflinePolicy`, then starts the app. Browser references Core and browser-safe Presentation, not native Artifacts.

Bundled, local-file, public-link and optional-store entry converge on bounded admission and a pinned definition. Local JSON/YAML authoring sources compile through Core; remote distribution uses canonical runtime JSON. A canonical digest establishes content identity, not publisher authenticity. Registry history rejects conflicting already-admitted revisions.

`ConfigurationEditor` adapts `DefinitionFormModel`. Typed controls update the shared answer snapshot; Core owns activation, validation, draft decisions and artifact composition. Presentation owns answer revisions and operation generations. Browser owns navigation, consent interaction and current export capabilities.

`BrowserArtifactExport` holds private bytes and fences late JavaScript operations. Its module exposes current previews, clipboard operations and short-lived Blob downloads only after user action. Exported files and clipboard contents leave application custody and cannot be retracted by clearing a workspace.

The published worker caches manifest-hashed public application assets. Optional store assets and deliberately retained public definitions have separate admission/lifetime rules. Answers and generated artifacts do not belong in those caches. Worker activation deliberately avoids forced replacement of active work.

This is a useful separation of responsibilities. The defects below occur where the adapter loses a typed distinction, orders an asynchronous privacy transition incorrectly, fails to invalidate on edit intent, or lacks an effective recovery/control path.

### Readiness matrix

| Area | Current assessment | V1 implication |
| --- | --- | --- |
| Standalone local-processing architecture | Appropriate; source and actual package support the boundary | Retain |
| Definition admission and canonical pinning | Substantial bounded implementation and tests | Retain; qualify current transport behavior |
| Independent source entry | Bundled, local JSON/YAML and public HTTPS paths exist | Store must remain optional |
| Typed editing and collections | Real functionality; presence/empty semantics have defects | Fix answer fidelity |
| Complete and secret-deferred preparation | Both implemented; target acceptance stays separate | Retain truthful distinctions |
| Secret consent | Safe initial default, but revocation ordering has a race | Release blocker |
| Export invalidation | Extensive fencing, with an invalid-nested-edit gap | Release blocker |
| Import | Limited inverse mapping is legitimate; empty-string loss is not | Fix fidelity; explain supported imports |
| Validation/correction | Useful section and duplicate-row support; generic nested failures remain | Make supported workflows correctable |
| Navigation/discard | Explicit route-departure protection exists | Preserve; align Clear behavior |
| Visual accessibility | Current published primary-link contrast failure observed | Release blocker |
| Keyboard/assistive technology | Structural foundations and historical evidence; current full qualification missing | Qualify every claimed supported workflow |
| Offline/update behavior | Deliberate cache boundaries; startup recovery is sticky | Repair recovery and exercise release transitions |
| Dependency servicing | Central pins/locks and auditing exist; runtime pin trails servicing | Update and review exact delivered closure |
| Static package | Fresh publication and focused package assertions passed | Necessary, not sufficient |
| CI | Strong validation configuration; no checked-in browser platform gate found | Add published-app regression evidence |
| Production hosting and promotion | No production configuration or deployment evidence reviewed | Establish and exercise before public launch |
| Incident/support ownership | Reporting address exists; acknowledgement time is undefined | Replace with a sustainable operating contract |
| Performance and browser support | No current measured release budget or complete support matrix established | Measure and limit claims |

## Findings

P1 findings require resolution or a demonstrably safe, explicit scope reduction before public production v1. P2 findings are important improvements that may be deferred with ownership and honest public limitations. All findings are **open**; mitigation IDs identify proposed work, not work delivered by this audit.

### IVSD-F001: A competing edit can cancel consent revocation

**Priority/severity:** P1, high. **Claim type/validation:** source-supported privacy defect; adverse schedule not executed.

`ConfigurationEditor.ConsentAsync` captures a generation, awaits `InvalidateOutputAsync`, then checks whether the generation still matches before calling `form.SetSecretEntryEnabled(false)`. An ordinary field edit calls `InputChanged` and `Invalidate`, advancing that generation. Export disposal can yield to JavaScript cleanup when an export module has been opened. Consequently, an ordinary edit during that await can cause the revocation continuation to return while secret entry and secret answers remain enabled.

The provider controls this transition; users cannot repair an abandoned denial. Trust and Rights of People require consent withdrawal to take effect independently of unrelated edits. Losing output cleanup freshness must not cancel the privacy decision.

**IVSD-M001:** Apply denial and recursive secret clearing synchronously before awaiting export cleanup. Fence late cleanup separately. Preserve ordinary edits and fail closed for later preparation.

**Owner/acceptance:** Browser and Presentation maintainers. Gate export invalidation, revoke consent, edit an ordinary field, release cleanup, and assert secrets remain absent, consent remains disabled, ordinary edits survive and subsequent exports cannot include revoked values.

**Traceability:** E03; `ConfigurationEditor.razor.cs:98-100,121-128,341-352`; `BrowserArtifactExport.cs:55-73`. Principles: Trust, Rights, Non-Harm. Domains: technical/design. Stakeholders: secret-entering operators. Escalation: privacy-focused release review after the deterministic regression passes.

### IVSD-F002: Invalid nested edit intent need not invalidate existing exports

**Priority/severity:** P1, high. **Claim type/validation:** source-supported stale-output defect; adverse schedule not executed.

Invalid nested input is translated into `SetValue(path, null)`. When the relevant answer/ancestor is absent, the form can legitimately report no snapshot change. The editor's validity callback then records the invalid control but does not invalidate prepared artifacts or pending imports. A previously prepared artifact can therefore remain available after a user has made the current visible form invalid.

For example, an absent nested masked numeric field can accept invalid text as an unfinished edit while its typed answer remains absent. A new Prepare is blocked, but that is not the same as revoking the old Copy/Download capability. The distinction matters because the UI promises that edits discard old output.

**IVSD-M002:** Invalidate private output and pending import work on edit intent, including invalid/rejected input, independently of a successful typed-snapshot mutation.

**Owner/acceptance:** Browser maintainer. Prepare/reveal output, enter invalid text into an absent nested masked numeric field, and assert immediate capability revocation. Gate an import across the same edit and prove its late result cannot overwrite the unfinished input.

**Traceability:** E03/E04; `ConfigurationField.razor.cs:99-125`; `DefinitionFormModel.cs:89-130`; `ConfigurationEditor.razor.cs:210-214`. Principles: Trust, Truthfulness, Non-Harm. Domains: technical/design. Stakeholders: operators relying on current exports. Escalation: privacy regression review, not a claim of observed external disclosure.

### IVSD-F003: A successful import can silently drop an explicit empty string

**Priority/severity:** P1, high. **Claim type/validation:** reproduced answer-fidelity defect.

An independently authored local fixture declared an optional unconstrained string and a supported direct-copy JSON projection. Importing `{"name":"synthetic-audit-name","optional":""}` succeeded. Complete preparation and explicit Reveal produced `{"name":"synthetic-audit-name"}`: the optional property was absent.

`SetImported` reduces the typed string to display text; `TryValue` treats zero-length text as no answer; `Snapshot` omits it. The UI therefore reports a successful import while changing its meaning. A related list inconsistency exists in source: a newly added string item starts as `""`, whereas typing then erasing nested text writes null.

**IVSD-M003:** Preserve explicit typed presence separately from edit text. Define consistent absent, empty-string, null and collection-item semantics across import, editing, snapshot and composition. Fix the shared representation, not an exporter-specific special case; compatibility aliases are unnecessary.

**Owner/acceptance:** Presentation owner with Browser/Core coverage. Round-trip an optional empty string through import and actual output. Compare untouched empty string-list items with typed-then-erased items. Add a regression that fails on semantic loss even when import reports success.

**Traceability:** E04/E08; `DefinitionFormModel.cs:300-311,494-498,523-531`; `ConfigurationField.razor.cs:111-123`. Principles: Truthfulness, Trust, Non-Harm. Domains: technical/design. Stakeholders: operators and target maintainers. Escalation: no scholarly question; target conformance remains target-owned.

### IVSD-F004: Optional objects and lists cannot reliably return to absence

**Priority/severity:** P1, medium. **Claim type/validation:** reproduced correction dead end, supported by source.

The UI supplies optional objects/lists and removes list rows, but offers no omit-container action. Clearing children can retain `{}`; removing the last row retains `[]`. These are valid distinctions in Core, but users need a way to choose either side of them.

In the current published app, supplying an optional object with a required child made preparation fail. Its supply control disappeared, no container-removal control existed, and the unrelated Name answer remained populated. The user had to complete an unwanted object or clear the workspace.

**IVSD-M004:** Expose an explicit omission action for optional supplied containers, preserving unrelated answers and consent rules. Do not redefine all empty containers as absent: targets can legitimately distinguish them.

**Owner/acceptance:** Browser/Presentation maintainers. Supply/edit/omit an optional object and a minimum-sized optional list; verify omission, preserved unrelated values, invalidated exports and restored readiness. Retain tests for explicit `{}` and `[]`.

**Traceability:** E04/E08; `ConfigurationField.razor:11-45`; `DefinitionFormModel.cs:122-130,176-183`. Principles: Rights of People, Excellence, Non-Harm. Domains: design/technical. Stakeholders: operators editing conditional/optional configuration. Escalation: none beyond product acceptance.

### IVSD-F005: Filled primary navigation links have invisible labels

**Priority/severity:** P1, high. **Claim type/validation:** reproduced visual/accessibility defect.

The Home page's **Open configuration** anchor is a filled MudBlazor button. In settled light appearance its text and background are both `rgb(15, 118, 110)`; in settled dark appearance both are `rgb(94, 234, 212)`. Each measured pair has **1:1 contrast**. Desktop and mobile screenshots show the empty-looking primary action. The accessible text exists, so DOM text assertions do not catch the defect.

The global `a` rule is in the higher `base` layer, while the vendor stylesheet is imported into `reset`. That higher layer overrides the vendor's filled-link foreground. This is not a reason to abandon cascade layers; it is a missing component-level color contract. A screenshot captured during the dark transition briefly showed text, so a transitional image must not be treated as the settled result.

**IVSD-M005:** Establish the filled-link foreground through the owning theme/component layer, then verify actual rendered states across all consumers, not just one Home link. Preserve ordinary text-link styling and all appearance modes.

**Owner/acceptance:** UI maintainer. Measure final normal/hover/focus/disabled states for anchor and button variants in light, dark and high contrast; inspect phone and desktop captures. Use WCAG 2.2 AA text contrast as the acceptance target, not a source/CSS-string test.

**Traceability:** E05/E08/X05; `wwwroot/css/layers.css:1-7`; `wwwroot/css/base.css:3`; current Home captures. Principles: Justice, Excellence, Non-Harm. Domains: design/evaluation. Stakeholders: all users, particularly users with reduced vision. Escalation: accessibility acceptance before release.

### IVSD-F006: Some validation failures do not explain how to correct them

**Priority/severity:** P1, medium. **Claim type/validation:** reproduced generic-error behavior plus source-supported location loss.

For the optional-object scenario, Review displayed the object title and “Check the declared field constraints. (value-invalid)”, without identifying its required child. Core reduces many non-duplicate constraint failures to root-level `value-invalid`; the UI does not generally expose the declared numeric/string rules.

The provider cannot delegate all correction usability to publisher prose while advertising generic supported definitions. A keyboard user may reach the right section but still need to read JSON to discover the failed rule. Safe diagnostics should omit values, not omit actionable public paths and constraints.

**IVSD-M006:** Preserve structured diagnostic paths/rule codes where available; render relevant declared rules next to controls; focus the actual failing nested control. Keep diagnostics value-free.

**Owner/acceptance:** Core diagnostic and Browser owners. Correct nested required-child, numeric-bound, string-format and duplicate-row failures without opening definition source or revealing rejected values. Do not weaken generic validation to make the UI appear successful.

**Traceability:** E04/E05/E08; `DefinitionValidator.cs:126-130`; `ConfigurationDiagnosticText.cs:29-39`; `ConfigurationField.razor:78-87`. Principles: Excellence, Justice, Truthfulness. Domains: design/technical. Stakeholders: first-time and assistive-technology users. Escalation: usability/accessibility review for supported constraints.

### IVSD-F007: The visible workspace retry reuses failed initialization

**Priority/severity:** P1, medium. **Claim type/validation:** source-supported recovery defect; transient failure not injected in this session.

Both `BrowserOfflinePolicy` and `pwa-lifecycle.js` memoize initialization. A worker/install/status failure can leave app preparation false for the page lifetime. The visible **Start local workspace** action calls the same cached initialization rather than checking a newly healthy worker.

Refusing answer entry until custody preparation succeeds is correct. Offering an ineffective retry after a transient failure is not. This affects initial online usability as well as advertised offline recovery.

**IVSD-M007:** Provide a clean-state readiness retry, or explicitly label a reload recovery where that is the supported solution. Do not silently bypass the preparation boundary.

**Owner/acceptance:** Browser offline owner. Fail first initialization, restore worker/assets, invoke the stated recovery and prove controls unlock only after fresh readiness confirmation. Also cover reconnect and unsupported/blocked storage with value-safe explanations.

**Traceability:** E03/E05; `BrowserOfflinePolicy.cs:22-57`; `pwa-lifecycle.js:117-150`; `ConfigurationEditor.razor.cs:308-328`. Principles: Trust, Excellence, Non-Harm. Domains: technical/design/operational. Stakeholders: operators on unreliable networks or restricted browsers. Escalation: none.

### IVSD-F008: The delivered runtime pin trails current servicing

**Priority/severity:** P1, high release gate. **Claim type/validation:** verified version gap, not a finding that a specific CVE is exploitable here.

`Directory.Build.props:16` pins browser `RuntimeFrameworkVersion` to `10.0.10`; central Microsoft packages are also `10.0.10`. Official Microsoft sources identify `10.0.13`, released October 13, 2026, as the current .NET 10 servicing release at this review. SDK `10.0.302` is separately pinned; SDK, package and delivered Mono/WASM runtime identities must be assessed separately.

The fresh Browser NuGet audit returned no reported vulnerabilities. That supports the bounded audit result only: it does not prove the runtime pack is current, every advisory is applicable/inapplicable, or future advisories will be noticed.

**IVSD-M008:** Select a currently serviced stable .NET 10 toolchain and compatible packages/runtime; regenerate locks natively; review delivered runtime advisories and actual package closure/notices. Do not switch to a .NET 11 prerelease merely to be newer.

**Owner/acceptance:** Build/release owner. Record chosen SDK/runtime/package identities, current advisory disposition and regenerated locks; repeat affected Browser/shared/package checks after the upgrade.

**Traceability:** E01/E02/E07/X01/X02. Principles: Trust, Non-Harm, Excellence. Domains: technical/operational. Stakeholders: users receiving provider-delivered code. Escalation: security maintainer for applicable advisories; no unsupported exploit claim.

### IVSD-F009: Production HTTPS, routing and response-header behavior is unqualified

**Priority/severity:** P1, high. **Claim type/validation:** missing operational evidence.

The package supplies a useful meta CSP and hosting guidance, not evidence of a configured public edge. This session's Apache localhost preview served `/`, `/open` and `/help` with HTTP 200 and a missing framework asset with 404. It did not establish production TLS, certificate renewal, security headers, CDN behavior or compression.

A meta CSP cannot enforce `frame-ancestors`; the static host must supply that response header. Its broad `connect-src https:` supports independent public definitions and must not be described as a hard no-egress sandbox. Server-rendered Blazor security defaults do not automatically apply to this standalone static app.

**IVSD-M009:** Version-control the actual host configuration and deploy to a dedicated HTTPS origin. Verify TLS/redirects, a response-header CSP with framing denied, `nosniff`, referrer policy, appropriate HSTS, navigation fallback, asset 404s, MIME types and encoded transport. Keep WASM/worker/MudBlazor functioning without adding generic script `unsafe-eval`.

**Owner/acceptance:** Hosting owner. Capture actual responses and browser boot from the intended host configuration, including deep links and failure cases. Explain and review required CSP allowances rather than asserting the meta policy is sufficient.

**Traceability:** E01/E05/E06/E08/X03/X04. Principles: Trust, Non-Harm. Domains: technical/operational. Stakeholders: public users and operators. Escalation: release/security approval of the actual host posture, not this report.

### IVSD-F010: Validation packages are not an exercised deployment and recovery process

**Priority/severity:** P1, high. **Claim type/validation:** missing release-operating evidence.

CI builds packages, writes checksums and retains unsigned artifacts for seven days. The release runbook explicitly says publication authority is not activated. Neither a deterministic compiler setting nor a successful local publish proves that production receives the exact approved complete tree.

Incremental overwrite can mix boot files, worker manifests and assets. Reverting origin files also does not instantly replace installed/offline workers. The current worker intentionally waits for old clients and deletes older application caches on activation. These are legitimate behaviors that need an operating recovery plan.

**IVSD-M010:** Retain immutable identified bundles and previous known-good bytes; promote the tested bundle without rebuilding; use an atomic release switch and defined entrypoint cache policy. Rehearse normal update, failed deployment, rollback and emergency guidance across active tabs and an installed app.

**Owner/acceptance:** Release/hosting owners. Demonstrate A-to-B-to-A transitions without lost active answers or mixed assets, record worker/package identities, verify optional-store failure behavior, and state that disconnected clients cannot be remotely recalled.

**Traceability:** E02/E05/E06/X03. Principles: Trust, Promise-Keeping, Non-Harm. Domains: operational/governance/technical. Stakeholders: active and offline operators. Escalation: independently authorized Configurator promotion authority; never reuse Event signers or bypass fail-closed signed publication.

### IVSD-F011: Current production-browser qualification is not a repeatable release gate

**Priority/severity:** P1, high. **Claim type/validation:** missing assurance with a reproduced escaped visual defect.

The workflow executes owner/package tests but contains no current real-browser release job. Operations records distinguish older platform checks from later unqualified UI refinements. This session's limited Chrome check found a basic contrast defect despite all 135 current Browser tests passing.

The missing evidence includes current complete keyboard/assistive-technology journeys, actual saved-download bytes/names, clipboard denial, privacy sink inspection, offline reopening, and two-tab update/recovery on the exact release. bUnit roles and counts cannot substitute for these platform outcomes.

**IVSD-M011:** Add a small published-app browser regression lane and retain release-bound evidence. Combine deterministic owner tests for races with actual browser integration and human accessibility acceptance. Publish a bounded browser/OS support statement.

**Owner/acceptance:** Browser QA owner. Qualify every claimed supported engine and critical operator journey; use synthetic canaries for network, storage, cache and worker-message inspection. Include 320/390-pixel reflow, real zoom, keyboard, focus, themes and a screen-reader-supported journey. Constrain claims if a platform remains unqualified.

**Traceability:** E02/E06/E07/E08/X05. Principles: Excellence, Justice, Truthfulness. Domains: evaluation/design/operational. Stakeholders: all supported users. Escalation: accessibility and security practitioners for release qualification; no certification inferred.

### IVSD-F012: Public-service security response lacks a bounded operating commitment

**Priority/severity:** P1, high. **Claim type/validation:** current policy defect and missing operating evidence.

`SECURITY.md` provides a reporting address but promises acknowledgement within an undefined number of business days. The CI workflow runs on push, PR and manual dispatch, not a recurring advisory schedule. CODEOWNERS/repository ownership does not establish a person watching TLS expiry, host compromise, dependency alerts or the security mailbox.

A secret-capable hosted utility needs a sustainable response arrangement even when development is volunteer-led. The remedy is an honest limited commitment with a backup, not an invented 24/7 SLA.

**IVSD-M012:** Name primary/backup owners for domain, hosting, releases and vulnerability intake; adopt achievable acknowledgement/severity/escalation targets; schedule advisory review; rehearse disabling or repairing a compromised origin. Define public incident notices, safe diagnostics and hosted-access-log retention.

**Owner/acceptance:** Project steward and operating lead. A synthetic report reaches both responders; a tabletop produces an owned disposition and emergency release/notice path. Require relevant checks before promotion and inspect actual repository/host access settings. Availability monitoring must be answer-free.

**Traceability:** E02/E06; `SECURITY.md:7-8,76`; `build-test.yml:3-7,129-136`. Principles: Trust, Promise-Keeping, Non-Harm, Avoiding Spying. Domains: operational/governance. Stakeholders: users, reporters, volunteer maintainers. Escalation: legal/privacy advice if hosting jurisdiction or incident obligations require it.

### IVSD-F013: The in-app secret decision omits important hosted-code trust guidance

**Priority/severity:** P1, medium. **Claim type/validation:** source-supported communication gap; user comprehension not tested.

The README and public guide explain that a hosted web app adds trust in its origin, delivered code, browser and extensions, and recommend native/deferred-secret paths for high-value secrets. The editor's consent text mainly says values are masked and revocation clears them. Its collapsed privacy details discuss exports, not the compromised-origin threat.

A person arriving directly at a publisher launch link may never read the repository FAQ. Masking is a screen-display measure, not protection from code delivered by a compromised origin. Local processing is valuable but does not remove that trust decision.

**IVSD-M013:** Put concise origin/browser trust guidance and the native/deferred-secret alternative beside secret consent, with an accessible route to complete help. Make it clear that ordinary settings can also be confidential. Keep consent optional and off per workspace.

**Owner/acceptance:** Product/UI owner. First-time operators can explain where answers execute, who delivers the code, what masking does, how revocation works, and what exported data cannot be retracted. Do not label the product “secure”, “certified” or “Islamically compliant” on this evidence.

**Traceability:** E01/E03/E05; `ConfigurationEditor.razor:5-13,29-34`; `Pages/Help.razor:11-18`. Principles: Truthfulness, Rights, Trust, Avoiding Deception. Domains: design/strategic/evaluation. Stakeholders: directly linked first-time users. Escalation: stakeholder validation, not a religious ruling.

### IVSD-F014: Import eligibility and conflicts are explained too late

**Priority/severity:** P2, medium. **Claim type/validation:** source-supported usability limitation.

The import selector lists every artifact although Core legitimately rejects conditional, repeated or transformed projections without a supported inverse. Conflicts return `answer-conflict`, while the UI tells users to clear conflicting fields without naming them.

**IVSD-M014:** Explain unsupported inverses before file selection; identify conflicting public field paths without values. Keep rejection atomic and preserve existing answers. Universal inverse import and automatic merge are unnecessary for v1.

**Owner/acceptance:** Browser/Core owners. Test unsupported, supported, conflicting and secret-bearing imports through the real UI; an operator can identify the next action without trial-and-error clearing.

**Traceability:** E04/E05; `DefinitionImport.cs:25-41,63-65`; `ConfigurationEditor.razor:95-109`; `ConfigurationEditor.razor.cs:280-284`. Principles: Truthfulness, Excellence, Rights. Domains: design/technical. Stakeholders: returning operators. Escalation: none; defer only with an explicit supported-import limitation.

### IVSD-F015: Clear immediately destroys transient work

**Priority/severity:** P2, medium. **Claim type/validation:** observed immediate clearing and source-supported absence of confirmation.

Clear sits beside preparation actions and immediately disposes the form/output. This session observed the Name value become empty and the cleared status appear. Route departure, by contrast, has explicit discard protection. Because answers are deliberately not persisted, an accidental Clear has no recovery.

**IVSD-M015:** Require a deliberate confirmation when clearing populated work; preserve a simple immediate action for an already empty workspace if appropriate. Do not introduce secret persistence or a hidden undo buffer merely to solve this.

**Owner/acceptance:** Browser UI owner. Cancel preserves values/consent/output; confirm clears them and restores useful focus. Describe the action as destructive.

**Traceability:** E03/E05/E08; `ConfigurationEditor.razor:68-70`; `ConfigurationEditor.razor.cs:354-382`. Principles: Rights, Non-Harm, Excellence. Domains: design. Stakeholders: operators with lengthy workspaces. Escalation: none.

### IVSD-F016: Offline retention and connection state have limited recovery controls

**Priority/severity:** P2, medium. **Claim type/validation:** source-supported limitation; capacity/reconnect scenarios not executed.

Retention rejects a ninth distinct definition, but the UI only lists retained entries and reports a generic failure. There is no selective removal flow. The C# `IsOffline` property is populated during initialization; the reviewed owner exposes no live refresh API. JavaScript independently checks current connectivity, so this is a recoverability/state-consistency concern, not proof of unauthorized fetching.

**IVSD-M016:** Add bounded public-cache management, specific capacity/storage messages and an explicit readiness/connection refresh. Keep public definitions separate from answers and never silently persist work.

**Owner/acceptance:** Offline owner. Fill eight slots, reject the ninth clearly, remove one selected public revision, retain a replacement, and exercise online/offline/reconnect without inventing readiness.

**Traceability:** E03/E05; `pwa-lifecycle.js:173-185`; `Pages/Open.razor:98-104,309-315`; `BrowserOfflinePolicy.cs:15-20,27-41`. Principles: Promise-Keeping, Excellence, Rights. Domains: technical/design/operational. Stakeholders: offline and low-connectivity operators. Escalation: scope-specific release acceptance.

### IVSD-F017: Responsiveness and support claims lack current measured limits

**Priority/severity:** P2, medium; measurement of supported worst cases is a release gate. **Claim type/validation:** missing performance/stakeholder evidence, not a demonstrated freeze.

The app accepts bounded definitions up to 4 MiB and can render substantial nested configuration, including Event's tenant rows. Bounds prevent unbounded vocabulary but do not establish acceptable startup, validation, composition or memory behavior on a low-end supported device. RTL appearance is implemented; it is not a translated Arabic interface.

**IVSD-M017:** Measure cold/warm startup, transfer size, input responsiveness, large-definition rendering, preparation and memory on the slowest supported device class. Adopt budgets from that evidence; narrow limits or move proven expensive work only when measurement requires it. State the supported UI languages separately from RTL direction.

**Owner/acceptance:** Browser performance/product owners. Record device, browser, network profile, definition size and operation timings; first-time operators complete a representative journey without assistance. No default answer telemetry is needed.

**Traceability:** E01/E03/E06/E08; supported bounds and current interface contracts. Principles: Excellence, Justice, Truthfulness. Domains: evaluation/design/strategic. Stakeholders: low-resource, mobile, RTL and first-time users. Escalation: accessibility/local-language practitioners where those audiences are claimed.

## Recommendations

### Minimum v1 contract

Release an English-language anonymous static preparation app on one dedicated HTTPS origin, with an explicit tested browser matrix. Retain local files and public publisher links independently of Store availability. Support correct typed entry, clear correction, deliberate complete/draft exports and safe transient custody.

Keep secrets optional and disabled by default. A temporary no-secret-entry launch could reduce the exposure of the revocation issue, but only if the capability is genuinely removed/disabled and package, UI and documentation agree. It would not fix answer fidelity, stale ordinary exports, unreadable actions or hosting readiness. Do not use a warning as a substitute for fixing a faulty enabled privacy transition.

### Recommended implementation order

These are proposed coherent increments, not approved implementation tasks:

| Increment | Deliverable | Findings | Exit evidence |
| --- | --- | --- | --- |
| Correctness and custody | Synchronous denial; invalid-edit fencing; typed presence; optional omission | F001-F004 | Deterministic owner regressions plus actual import/edit/export vectors |
| Usable completion | Filled-link colors; exact correction; startup recovery; informed secret choice | F005-F007, F013 | Current rendered, keyboard and correction journeys |
| Serviced release inputs | Stable serviced toolchain/runtime; generated locks; advisory and notice disposition | F008 | Updated identity and affected-owner/package verification |
| Host and operations | Versioned host policy, immutable bundles, deployment/rollback, named responders | F009-F010, F012 | Staging rehearsal and actual response/access evidence |
| Release qualification | Published-app CI, browser matrix, accessibility and supported worst-case measurements | F011, F017 | Exact-candidate acceptance ledger |
| Follow-up usability | Import assessment, Clear protection, public cache management | F014-F016 | Scoped acceptance or recorded deferrals |

Do not implement an exporter workaround for a shared answer-model defect, or add a persistence layer to compensate for a missing confirmation. Breaking internal contracts is acceptable where it simplifies the correct representation.

### Public deployment blueprint

1. **Choose one dedicated origin.** Host at `/`; put previews on different origins. The worker currently deletes other `configurator-app-*` caches on activation, so do not assume sibling Configurator installations on the same origin are isolated. Supporting sibling scopes later requires scope-qualified cache ownership.
2. **Control delivery authority.** Name who owns DNS, TLS, the static host, deployment credentials, releases and emergency replacement. Restrict promotion separately from read-only PR checks. A controlled manual promotion is acceptable; an unreviewed upload from a developer working directory is not.
3. **Build once and retain the candidate.** Bind commit, SDK/runtime/package closure, Store variant, public definition identities, complete-tree checksums, notices and test evidence. Keep durable release storage beyond expiring CI artifacts and preserve a known-good predecessor. Validate reproducibility of the declared deployable content.
4. **Configure the host, not only the app.** HTTPS redirect, certificate renewal, appropriate HSTS, header CSP/framing protection, referrer policy, MIME types, selective SPA fallback and genuine missing-asset errors need deployed evidence. Verify original and compressed response bytes, encodings and `Vary` behavior.
5. **Apply a deliberate cache policy.** Revalidate mutable HTML/worker/manifest entrypoints. Use immutable caching only for genuinely content-addressed resources. Do not apply a blanket year-long cache policy to all same-origin CSS/JavaScript. Confirm headers and CDN transformations do not break hashes or worker imports.
6. **Promote to staging without rebuilding.** Exercise the exact complete tree under intended headers, origin-root paths and compression. Verify `/open`, bundled `/launch/<family>` routes, Store and a real independent CORS source, along with local-file fallback.
7. **Rehearse update and recovery.** Open two active workspaces, install the next worker, verify work is not replaced, close/clear deliberately, then prove clean activation. Test failed install, cache eviction, rollback, offline reopening and optional-store mismatch. Emergency guidance must explain when users should stop, finish/export locally, close all tabs or use a reviewed native/deferred-secret path.
8. **Promote identical bytes with authorized access.** An atomic switch avoids mixed releases; retain the old release and evidence. Probe actual production headers, boot and key routes afterward. This report grants no external-write authority.
9. **Operate without collecting answers.** Monitor public availability, certificate expiry, boot integrity and advisory feeds. Define minimal host-log fields, access and retention; IP/user-agent/request logs can exist at the provider even though application answers stay local. Never request secret screenshots/artifacts in support.
10. **Publish a bounded release statement.** Identify the version/commit, supported browsers, language, import limits, secret-custody choice, offline prerequisites, download filename caveats, target-owned acceptance and reporting route.

No cloud-provider selection was made. Provider pricing, account posture, data locations and deployed configuration were not reviewed. A conventional static edge/object host or a small managed static server can satisfy this design; Kubernetes, multi-region services and an application database are not prerequisites.

### Explicit v1 exit gates

| Gate | Required observable outcome | Present status |
| --- | --- | --- |
| G01 - Privacy transitions | Revocation cannot be cancelled by edits; invalid edits revoke old output/import authority | Open: F001-F002 |
| G02 - Answer fidelity | Empty/absent/null distinctions and optional omission survive supported journeys | Failed in current browser: F003-F004 |
| G03 - Usable correction | Visible actions, actionable safe errors, usable keyboard/focus and recovery | Failed/partly unverified: F005-F007 |
| G04 - Servicing | Current approved stable runtime/SDK/package closure, native locks and advisory disposition | Open: F008 |
| G05 - Host policy | Actual secure origin, headers, routes, MIME/encoding and asset-failure evidence | Not reviewed: F009 |
| G06 - Promotion and recovery | Exact retained bundle promoted without rebuild; installed-worker update/rollback demonstrated | Not reviewed: F010 |
| G07 - Platform qualification | Exact candidate passes supported-browser, export, custody, offline and accessibility journeys | Partial local evidence only: F011 |
| G08 - Operating ownership | Real responders, access controls, acknowledgement/escalation, answer-free monitoring and incident rehearsal | Not reviewed/incomplete policy: F012 |
| G09 - Honest user choice | Direct-launch users understand origin trust, optional secrets, draft limits and export custody | Open: F013 |
| G10 - Supported limits | Worst supported definition/device measured; language/browser claims match qualification | Open: F017 |

F014-F016 can be deferred only with a named owner and public limitation. No engineering fix or deferral was accepted by the user in this audit. The gate table is a proposed release decision framework, not evidence that work has been scheduled.

### Rejected alternatives and trade-offs

- **Server/Auto or BFF migration:** would change answer custody and add operating dependencies without fixing the adapter defects. Retain WASM.
- **Accounts, cloud drafts and analytics:** not needed for the anonymous preparation contract; they create new privacy, retention and security duties.
- **Mandatory central Store:** would weaken independent use and portability. Store failure must not block local/public-source work.
- **Browser download presented as protected native save:** the platform does not provide that guarantee. Explain the distinction.
- **Forced worker update over active work:** trades update speed for loss of transient configuration and authority confusion. Preserve deliberate lifecycle transitions and document emergency limits.
- **Full CLI/desktop/maintainer parity before v1:** not necessary. Universal inverse import, comparison, checkpoints, authoring workspaces, multi-workspace editing and bundle archives can be deferred.
- **Translated UI inferred from RTL:** direction support is useful but does not translate strings or prove Arabic usability. Scope language claims honestly.
- **Large bespoke publication PKI as the first hosting task:** unnecessary scope expansion. Use independently authorized Configurator promotion and preserved evidence; never claim or bypass unavailable signed-release authority.
- **Arbitrary performance percentages or a calendar promise:** unsupported without representative device measurements, staffing and remediation estimates.

## Stakeholders

| Stakeholder | Benefit or exposure | Provider-controlled responsibility |
| --- | --- | --- |
| First-time self-hosters | Less manual configuration; risk of misunderstood output or trust | Correct semantics, intelligible correction, honest completion claims |
| Secret-entering operators | Convenience with origin/browser trust exposure | Effective consent/revocation, safe delivery and incident response |
| Secret-deferring/offline operators | Lower live exposure; incomplete drafts and cache prerequisites | Truthful draft status, usable offline preparation/recovery |
| Keyboard, low-vision and assistive-technology users | Potential exclusion from critical actions | Rendered contrast, semantics, focus and complete supported journeys |
| Low-resource/mobile users | Large WASM download and local computation costs | Measured supported limits and responsive behavior |
| Definition publishers and target maintainers | Reusable integration; unsupported acceptance assumptions | Exact identity, clear provenance and target-owned semantic authority |
| ISLAMU and volunteer maintainers | Public trust and ongoing delivery duties | Sustainable ownership, bounded commitments and safe support processes |
| People represented by deployment metadata | Ordinary settings can reveal sensitive topology or identity | Minimize collection, avoid telemetry and protect diagnostics |

## I-VSD Principles And Domains

Trust/Amanah applies to delivered code, consent, private output and maintenance promises. Truthfulness/Sidq requires “import succeeded,” “complete,” “offline ready” and “local” to describe what actually happened. Non-Harm requires preventing foreseeable stale output and loss of important configuration. Rights of People support withdrawal, omission, correction and deliberate custody transfer. Justice/Adl and Excellence/Ihsan require usable access, not merely a non-crashing DOM. Avoiding Spying/Tajassus supports the no-answer-telemetry boundary and separately governed host logs.

| Domain | Assessment |
| --- | --- |
| Strategic | Anonymous local processing and independent entry align with portability; avoid turning the first launch into an account/store funnel |
| Design | Protective defaults exist; consent context, omission, correction, contrast and destructive-action protection need work |
| Technical | Strong layer separation and bounded definitions; identified lifecycle and representation defects require correction |
| Operational | Hosting, update recovery, response capacity and retained releases need actual operating evidence |
| Governance | Promotion/access/incident decision rights must be named; repository configuration alone does not establish enforcement |
| Evaluation | Current local tests/reproductions exist; supported-platform, stakeholder and longitudinal evidence remains missing |

No in-app finance, lending, billing, AI inference, ranking or moderation mechanism was identified in the reviewed browser flow. That is not an audit of ISLAMU's funding, sponsor, banking, contractual or organization-wide practices. Those matters require their own evidence and, for religious-legal questions, qualified Sunni scholarly review.

## Common Overlooked Failures And Outcomes

- A successful import can be semantically wrong even when every value is individually valid.
- Empty, absent, null and supplied-empty containers are different user decisions.
- A generation fence can correctly reject stale work while incorrectly cancelling a newer privacy decision.
- An invalid edit can change what the user sees without changing the typed snapshot.
- A label present in the accessibility tree can be invisible on screen.
- “Static hosting” does not mean noninteractive, and “local processing” does not mean no trust in the provider.
- A NuGet scan with no findings does not prove every embedded runtime advisory is covered.
- An intact origin rollback does not immediately replace an installed/offline worker.
- Public definitions may contain private explanatory text/defaults; retention consent remains necessary.
- Ordinary configuration may be confidential even if no secret field is enabled.
- Browser downloads may rename `.env`; the actual saved file, not the requested hint, is the acceptance evidence.
- Workspace clearing does not retract clipboard contents, downloads, screenshots or extension observations.
- Host access logs and provider control-plane data are separate from the app's answer-custody promise.
- Passing 135 tests is execution evidence, not measured coverage or proof that omitted race cases pass.

The intended positive outcome is an operator who can deliberately choose the trust level, prepare the exact intended configuration, correct errors without source inspection, export knowingly, and obtain meaningful help when delivery or updates fail. No user study or operational measurement establishes that outcome yet.

## Validation Gaps

### Verification Evidence Report: executed in this session

All .NET commands used `TMPDIR="$HOME/.cache/agent-tmp"` and SDK `10.0.302`. Product files were not changed. These commands were run to establish audit evidence, not because the Markdown deliverable requires product tests.

| Check | Executed command or action | Exact result |
| --- | --- | --- |
| Checkout identity | `git status --short`; `git branch --show-current`; `git rev-parse HEAD`; `dotnet --version` | Initially clean; `main`; commit named in metadata; SDK `10.0.302`; exit 0 |
| Browser owner | `dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1 --no-progress` | Exit 0; 135 succeeded, 0 failed, 0 skipped; test assembly 6.901 s; command wall time 110.472 s |
| Fresh Store-enabled static package | `dotnet msbuild eng/configurator/Configurator.Release.proj -target:PublishConfiguratorBrowser -property:Configuration=Release -property:ConfiguratorOutputRoot="$PWD/.tmp/v1-readiness-audit" -property:DefinitionStoreSourceRoot="$PWD/store" -verbosity:quiet` | Exit 0; 98.234 s wall time |
| Actual static package assertions | `dotnet test --project tests/Configurator.Architecture.Tests/Configurator.Architecture.Tests.csproj --configuration Release --no-restore --verbosity quiet -- --treenode-filter "/*/*/StandalonePackageTests/BrowserPackageIsCompleteStaticWasmWithoutNativeOrTestAssemblies*" --minimum-expected-tests 2 --no-progress` | Exit 0; 2 succeeded, 0 failed, 0 skipped; assembly 3.289 s; test summary 5.322 s; command wall time 51.366 s |
| Package-test scope qualification | Both `CONFIGURATOR_PACKAGE_ROOT` and `CONFIGURATOR_STORE_PACKAGE_ROOT` pointed to `.tmp/v1-readiness-audit` | Both parameter rows inspected the same fresh Store-enabled bundle; this is **not** fresh Store-free qualification |
| Browser NuGet audit | `dotnet list src/Configurator.Browser/Configurator.Browser.csproj package --vulnerable --include-transitive --format json --no-restore` | Exit 0; NuGet.org source; no reported vulnerabilities; list-command wall time 13.476 s |
| Repository advisory validator | `dotnet run .ci/scripts/validate-nuget-vulnerabilities.cs -- .tmp/v1-readiness-audit-nuget.json` | Exit 0; “No NuGet vulnerabilities detected.”; separate validator duration not recorded |
| Native preview | `httpd -t -f "$PWD/.tmp/v1-readiness-audit-httpd.conf"`; foreground Apache on `127.0.0.1:5099` | Syntax check exit 0; Apache 2.4.68; isolated local test host, not production |
| HTTP routing | `curl` checks of `/`, `/open`, `/help`, `/_framework/missing-audit.wasm`, and worker headers | 200, 200, 200, 404; worker 200 with `text/javascript`; command exit 0 |
| Browser reproduction | Chrome-backed `Bun.WebView`; installed Chrome `153.0.8010.52`; local package below | Local admission, import, complete prepare/reveal, optional-object failure and immediate Clear observed |
| Visual checks | Home at desktop and mobile; light and dark; final computed link colors | Light and settled dark primary-link text contrast 1:1; sampled Home layouts had no horizontal page overflow |

Fresh package identity:

- Worker asset-manifest version: `6qxw7BdH`.
- `service-worker-assets.js` SHA-256: `b699637260765f1167fb3e36ece1205846feb5db60fb921ff0e07427d01f5e4a`.
- This identifies the manifest used for local checks; it is not a complete signed release-bundle digest.
- Published root: `.tmp/v1-readiness-audit/browser/wwwroot/`.
- Synthetic definition family: `abc62026-1019-4d00-8000-000000000001`, revision 1; canonical digest `cb6a3bd631744d7b95f344bb7c300006cd66747c970ecc0a329af00a05eba4cf`.

Local evidence artifacts:

- `.tmp/v1-readiness-audit-publish.log`
- `.tmp/v1-readiness-audit-package-tests.log`
- `.tmp/v1-readiness-audit-nuget.json`
- `.tmp/v1-readiness-audit-definition.json` and `.tmp/v1-readiness-audit-import.json`
- `.tmp/v1-readiness-audit-home-desktop-light.png`
- `.tmp/v1-readiness-audit-home-mobile-light.png`
- `.tmp/v1-readiness-audit-home-desktop-dark.png`
- `.tmp/v1-readiness-audit-home-mobile-dark-transition.png` - transitional, not settled dark acceptance

These ignored local artifacts are session evidence, not committed distribution inputs. Desktop light capture was 1440 by 913 CSS pixels; mobile capture used a 390 by 844 touch-emulated viewport; desktop dark used 1440 by 1000. This was not a full-device or complete visual-conformance matrix.

### Failed tooling and recovery

- The first test wrapper used unavailable `/usr/bin/time` and exited 127 before running tests. It was replaced with Bash timing; the actual test run above then passed.
- Two task-owned Omowright attempts lost their CDP sessions after navigation. They do not establish an application defect. The materially different built-in Chrome-backed WebView completed the reported checks.
- A driver click initially required explicit scrolling; an ambiguous selector activated Copy rather than Reveal. The current JSON reproduction used the unique reveal control and observed the actual preview. These driver mistakes are not product findings.
- JSON LSP diagnostics were unavailable because the configured Biome executable was absent. The synthetic definition was admitted by the actual Core/browser boundary instead. No analyzer was installed and no product diagnostic was suppressed.
- The knowledge graph reported its last update as October 15, 2026. Current source inspection, not stale graph edges, supplied the final findings.

### Relevant checks not executed

- No complete Core, Presentation, native, engineering or full architecture suites; focused Browser tests use real Core/Presentation, but do not prove all owner suites pass.
- No separate solution build/format run; fresh browser publication exercised its own build path. No product implementation was changed.
- No fresh Store-free package or complete package matrix. Historical store independence is not promoted into current execution evidence.
- No controlled runtime reproduction of F001/F002; their proposed adversarial regressions remain required.
- No complete current screen-reader/keyboard/real-zoom/RTL/high-contrast/device matrix, Firefox/Safari/Edge qualification or saved-download byte comparison.
- No current two-tab A-to-B-to-A update/rollback, physical disconnection, storage eviction/capacity, or transient worker-failure experiment.
- No complete current network/storage/cache/worker-message canary sweep. Prior platform records retain their original revisions.
- No production host, TLS/CDN/header audit, repository-protection inspection, penetration test, responder drill or deployment.
- No measured line/branch coverage, low-end performance study or first-time-operator study.

## Escalation Needed

Before release, the project steward must identify hosting/release/security ownership and authorize the actual promotion path. Security and accessibility practitioners should review the repaired privacy transitions and supported critical journeys. Hosting privacy, log retention, terms and incident-notification obligations need qualified advice for the actual operator/jurisdiction where applicable.

No religious-legal question is necessary to fix these engineering defects. If future funding, sponsorship, financial contracts, religious claims or contested provider duties are introduced, route those questions to qualified Sunni scholarly authority. Neither CTO readiness nor I-VSD analysis supplies that authority.

## Evidence Reviewed

### Repository evidence register

| ID | Reviewed material and role |
| --- | --- |
| E01 | `AGENTS.md` supplied in session; `PROJECTS.md`; `.agents/contract/intents.yaml` documentation entry; `QUICK_REFERENCE.md`; extraction `FOUNDATION.md`; public Browser guide and README trust guidance |
| E02 | `global.json`; `Directory.Build.props`; `Directory.Packages.props`; Browser project; `.github/workflows/build-test.yml`; release-runbook current/future-authority sections |
| E03 | `Program.cs`; `BrowserOfflinePolicy`; `BrowserArtifactExport`; editor consent, invalidation, preparation, import and discard flows; launch/custody contract |
| E04 | `DefinitionFormModel` mutation/import/snapshot/typed-value flows; `DefinitionImport`; `DefinitionValidator`; Browser field editing and diagnostic projection |
| E05 | Current Browser Razor/CSS/JS: Open, Help, editor/fields, worker/lifecycle, manifest, CSS layer/base/override files; UI ownership guide |
| E06 | `SECURITY.md`; packaging and operations contracts, including explicitly historical platform evidence; user-owned strategy excerpts reviewed unmodified |
| E07 | Current executed Browser owner tests, focused actual-package assertions and NuGet audit; neighboring test source for consent, lifecycle, navigation, collections and container presence |
| E08 | Fresh package and actual local browser/HTTP reproduction described in Validation Gaps; synthetic inputs and screenshot artifacts |
| E09 | Three independent read-only audits: custody/security; packaging/deployment/operations; operator workflow/accessibility. Findings were synthesized and checked against parent-inspected source and actual evidence, not treated as execution proof |

Core source locators used above are repository-relative. Useful navigation anchors:

- [Browser product and custody guide](../../docs/public/browser.md)
- [Launch and browser authority contract](../../docs/internal/LAUNCH_CONTRACT.md)
- [Browser UI ownership](../../docs/internal/BROWSER_UI.md)
- [Packaging ownership](../../docs/internal/PACKAGING.md)
- [Operating evidence](../../docs/internal/OPERATIONS.md#browser-custody-and-offline-verification)
- [Security reporting policy](../../SECURITY.md)
- [Previous feature-completeness audit](i-vsd-browser-feature-completeness-detailed-audit.md)

### External source register

Accessed October 19, 2026. Research used the requested **Context7** and **Anysearch MCP**. Only official framework/standards facts inform recommendations; no external implementation was copied into product code.

| ID | Source, retrieval and bounded use |
| --- | --- |
| X01 | Microsoft .NET support policy, Anysearch search/extract: `https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core`; current servicing/support, not package-specific exploitability |
| X02 | Microsoft October servicing bulletin, Anysearch search/extract and provider web retrieval: `https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-october-2026-servicing-updates/`; identifies October 13 servicing and 10.0.13 |
| X03 | Microsoft Blazor PWA guidance, Context7 `/dotnet/aspnetcore.docs` plus Anysearch extraction: `https://learn.microsoft.com/en-us/aspnet/core/blazor/progressive-web-app/?view=aspnetcore-10.0`; published-worker/offline/update behavior |
| X04 | Microsoft Blazor CSP and deployment/integrity guidance, Context7 `/dotnet/aspnetcore.docs`: `https://learn.microsoft.com/en-us/aspnet/core/blazor/security/content-security-policy?view=aspnetcore-10.0`; `https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/bundle-caching-and-integrity-check-failures?view=aspnetcore-10.0`; `https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/blazor/host-and-deploy/webassembly/nginx.md`; meta/header distinction and static-host/integrity requirements |
| X05 | W3C WCAG 2.2, Anysearch discovery and provider web retrieval: `https://www.w3.org/TR/WCAG22/`; contrast, reflow, focus, keyboard and error-identification acceptance targets, not a conformance result |
| X06 | MudBlazor official installation/version guidance, Context7 `/mudblazor/mudblazor`: `https://mudblazor.com/getting-started/installation`; standalone WASM is interactive; do not confuse static file hosting with unsupported static component rendering |

Context7 supplied current main/dev documentation, not a formal compatibility certification for every pinned package. Its generic Nginx example and third-party documentation-site policies were not adopted verbatim. The report distinguishes .NET 10 production facts from examples for other hosting models or older framework versions.

## Missing Evidence

- Actual public origin, provider selection, deployed configuration, TLS/header/CDN/access posture and service ownership.
- Promotion approval/enforcement, durable full-bundle identity, clean reproducibility comparison and deployed-byte equality.
- Current installed-worker update/rollback/emergency and offline failure/capacity evidence.
- Complete supported-browser/device/accessibility/export/privacy acceptance for the candidate.
- Controlled adverse-schedule tests for revocation and invalid nested edit fencing.
- Current full shared-owner test/coverage reports and delivered-runtime advisory disposition.
- Stakeholder comprehension/usability, low-end measurements, support operation and incident exercises.
- Funding/sponsorship/contracts, jurisdiction-specific hosting/privacy obligations and any scholarly assessment of future religious-legal questions.

Missing records are not silently interpreted as nonexistent organizational practices. Conversely, an available policy, test source or historical screenshot is not treated as current operational proof.

## Context Inventory

- Repository/workspace evidence: product contracts, policies, source/config/tests, packaging/CI, historical reports, local publication and generated test artifacts.
- User context: extensive objective Senior CTO audit, development mode without compatibility constraints, and explicit public-static-v1 selection.
- Retrieval: repository-native reads/search/LSP outline, graph coverage check, three bounded read-only audit tracks, Context7 and Anysearch official-source research.
- Visible hosted-project capabilities included documentation/issue integrations, but no user-selected hosted project, support records, live host or operational exports were ingested. No external issue, governance or incident review is claimed.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
| --- | --- | --- | --- | --- |
| 2026-10-19 | none | current / changes-required | Agreed public-static-v1 scope; current source, research, executed checks and bounded browser reproduction | Commit and evidence identity above; IVSD-F001 through IVSD-F017 remain open |

Refresh the affected findings after changes to answer presence, consent/export ordering, source trust, offline lifecycle, UI styling, dependency/runtime pins, host topology, release promotion or operating promises. A different deployed bundle requires fresh release-bound platform/host evidence. Updating the date or rerunning only unit tests cannot turn this assessment into production approval.
