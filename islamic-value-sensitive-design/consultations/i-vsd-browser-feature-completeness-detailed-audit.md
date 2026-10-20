# Browser Feature Completeness and Cross-Client Capability Parity

Last Updated: 2026-10-20 Europe/Brussels

## Review Metadata

- Mode: standalone
- Subject: Browser feature completeness and capability parity across Browser, CLI, Terminal and Desktop
- Workstream: none; advisory input to future bounded implementation plans
- Report kind: detailed-audit
- Report status: current
- Disposition: advisory
- Evidence cutoff: 2026-10-20 for repository implementation and recorded execution; 2026-10-18 for external research
- Reviewed input: repository commit `65dde2668878db0e60e12d0051086643c8759520`, the completed guided-configuration-experiences execution record, and the original first-party strategy, historical reviews and dated official sources registered below
- Supersedes: none; earlier workstream reports retain their original scope and evidence dates

**Reading paths:** start with [Executive Summary](#executive-summary) and [Recommendations](#recommendations) for decisions; use the [capability matrix](#current-architecture-and-end-to-end-inventory) for cross-client scope, [Findings](#findings) for rationale and ownership, and [Evidence Reviewed](#evidence-reviewed) for source traceability. [Validation Gaps](#validation-gaps) defines what this audit does not establish.

## Executive Summary

**Browser should become a complete human-facing configuration workbench, including advanced validation and an optional maintainer workspace. The four clients should share configuration semantics and important user outcomes. They should not pretend to have identical interfaces, execution authority or storage guarantees.**

The user's central observation is correct: an agent-oriented CLI can serve a person, and a human-oriented Browser can offer rigorous validation, comparison, inspection and definition authoring. These are roles and interaction styles, not reasons to reserve useful domain capabilities for one client. A person should not need to install the CLI merely to understand a validation failure that the same shared engine already knows how to explain.

There is already a substantial Browser product. It is not merely a public-choice template generator. The completed guided-configuration work adds authored experiences and sections, shared unfinished-input retention and row identity, explicit omission, masked positive review, mutation-derived answer provenance, truthful required-answer progress, early eligible import with contextual recovery, consented credential controls and metadata-only search/facets. These build on typed configuration, deterministic preparation, deliberate exports, optional definition discovery and bounded offline use. Older documents describing an enum/boolean-only Browser, absent field search or missing omission controls are historical. Rebuilding those completed features would waste effort and could reintroduce addressed privacy and accessibility problems.

The next step is to finish the remaining operator gaps rather than rebuild the guided workspace: more specific rule explanations and automatic constraint guidance, explicit output-subset selection, offline reconnect/retry and retained-definition management, followed by local comparison and existing-file assessment. Persistent recovery and public maintainer authoring remain separately designed extensions. Neither workspace should acquire target administration or a server-side copy of private answers simply to imitate CLI commands.

This report recommends four separately measurable completion levels:

1. **Operator-complete:** a person can finish the supported configuration workflow without another Configurator client, subject to Browser's declared export limitations.
2. **Advanced-operator-complete:** comparison, import fidelity, provenance, recovery and multi-artifact decisions are understandable and controllable.
3. **Maintainer-complete:** a person can inspect, create, validate, revise and test supported public definitions without a terminal; repository submission and target testing remain explicit external activities.
4. **Assurance-qualified:** the above claims have current browser-engine, assistive-technology, offline/update, privacy and stakeholder evidence. A feature list alone cannot establish this level.

The most consequential provider responsibilities are truthful readiness claims, private-answer custody, intelligible validation, accessibility, supply-chain and update trust, neutral discovery, and evidence-backed support promises. Making every button available is insufficient if people cannot tell whether their input was retained, their download succeeded, a definition is trusted, or a target application will accept the result.

**Confidence and decision limits:** this is a current advisory assessment of the named repository revision, not release approval. The October 20 refresh inspects current source/tests and incorporates the completed implementation's recorded Core 145/145, Presentation 142/142 and Browser 176/176 results, Release build and bounded real-surface observations. It does not rerun those product checks. The original presence and empty-value defects are resolved within that verified implementation scope; seventeen other findings remain open, several with substantial delivered mitigations. Browser/extension/assistive-technology matrices, stakeholder studies, performance and production-host qualification remain incomplete. Private checkpoint contents and encryption remain unresolved custody decisions. This report authorizes no implementation or widening of private-data boundaries.

### Direct answers to the product questions

| Question | Recommendation |
| --- | --- |
| Should every client have exactly the same features? | Share the domain capabilities and semantic contracts; require an explicit justification for client-specific omissions. Do not require identical controls, transport or authority. |
| Should Browser gain useful capabilities currently exposed through CLI? | Yes: definition inspection and validation, detailed diagnostics, supported existing-configuration assessment, comparison, bundle review and an optional public-definition authoring workflow are appropriate candidates. Reuse shared owners rather than embed or simulate the CLI. |
| Should CLI also remain useful to people? | Yes: readable help, safe text diagnostics, explicit inputs, predictable exit states and repeatable commands are human features as well as automation features. An interactive TUI is an additional interaction, not a substitute for a good CLI. |
| Should every configuration surface support the same admitted field types and deterministic output? | Yes, within a declared engine/definition version. A hidden or unsupported field must not silently disappear or change meaning. |
| Should Browser promise the same protected-save guarantees as native applications? | No. Browser export remains a user-directed transfer with different observable outcomes and filesystem authority. This distinction must remain visible. |
| Does full feature parity mean target apply, secret-manager integration, accounts or cloud synchronization everywhere? | No. Those are new authority and custody decisions, not prerequisites for a complete offline preparation product. |
| Is the current Browser already feature-complete? | The approved guided-configuration implementation is complete, but that is narrower than this audit's operator, advanced-operator, maintainer and assurance profiles. The remaining gaps below prevent an unqualified Browser-completeness claim. |

### October 20 implementation reassessment

The completed work changes the disposition of several October 18 observations. This table separates delivered behavior from the work still needed; it is not a new implementation plan. Finding IDs belong to this consultation, not the similarly numbered six findings in the guided-configuration workstream report.

| Concern | Current result | Finding disposition / remaining obligation |
| --- | --- | --- |
| Presence and empty-value fidelity | Optional answers can be explicitly omitted; empty strings, false, zero and empty containers remain distinct from absence. Accepted empty-string imports survive shared snapshots and composition. | **IVSD-F002 resolved** for these defects, with shared and rendered regressions; full native-host qualification is not implied. |
| Guided disclosure and progress | Authored sections/experiences, local optional expansion, retained invalid drafts/row identities and active required-obligation counts are implemented. View changes do not edit answers or grant consent. | IVSD-F001/F011 partially addressed; comprehension and large-form benefit remain unmeasured. |
| Review and provenance | Positive review masks values and distinguishes authored defaults, imports, explicit answers and omission; Change/Return reaches the exact setting. Declared filenames and apply/restart metadata are visible. | IVSD-F005/F006 partially addressed; baseline comparison, projection proof and output-subset selection are still absent. |
| Import eligibility and correction | Only supported inverse mappings offer early import. Rejection is atomic, replaces no manual answer, and can route to a declared field, active controller or consent without enabling it. | IVSD-F004 partially addressed; a separate read-only existing-file assessment is still absent. |
| Credentials and custody | Denied secret leaves have no entry controls. Consented passwords have stable identities and honest new/current-password hints; stale rendered callbacks cannot survive invalidation. | IVSD-F012/F014/F016 retain qualification obligations. No vault integration, generator or extension-compatibility promise was added. |
| Large-definition navigation | Local public-metadata search, output-key matching, facets, own-category counts, independent resets and inactive-controller recovery are implemented. | IVSD-F011 partially addressed; explicit recommendation actions, automatic constraint/format explanations, independent findability and performance evidence remain. |
| Runtime evidence | Recorded owner suites, real Chromium-backed desktop/mobile-width light/dark observations, actual correction focus and limited native smoke checks now exist for the implementation. | IVSD-F012/F014/F016 remain open: bounded observations are not complete keyboard, AT, browser-engine, physical-device, package or deployed-origin qualification. |
| Offline and maintainer work | Reconnect/retry/cache management, comparison, checkpoints and the public maintainer workspace were not delivered by this workstream. | Existing IVSD-F007–F010/F015/F017 obligations remain; do not count excluded extensions as failed guided-configuration delivery. |

## Scope

The audit covers the complete user journey, not only components visible on the configuration page:

- finding, opening, admitting and identifying public definitions;
- entering scalar, enum, object and list answers, including explicit secret handling;
- explaining defaults, activation, requiredness, validation and readiness;
- importing, checking and comparing supported configurations;
- preparing, reviewing and exporting single artifacts and bundles;
- clearing, replacing, recovering and moving between workspaces;
- offline installation, retained public definitions, storage eviction and application updates;
- public-definition authoring, schema scaffolding, revision comparison and synthetic conformance fixtures;
- accessibility, internationalization, mobile and low-resource usability;
- packaging, hosting, origin trust, privacy, governance, provenance and operational support;
- parity with CLI, Terminal/TUI and Desktop, including deliberate differences;
- validation evidence and what would justify future completion claims.

This is a report and recommendation, not an implementation plan, code change, release approval or permission to widen trust boundaries. Proposed features are explicitly distinguished from existing implementation. No new account service, telemetry pipeline, target authentication, deployment system or remote answer processing is assumed.

## Claim Boundary

This is Islamic value-sensitive provider-responsibility reasoning. It is **not** a fatwa, Sharia certification, legal opinion, penetration-test attestation, WCAG conformance certificate or proof of stakeholder benefit. Religious-legal determinations belong to qualified Sunni scholarly authority where a concrete issue requires them. Licensing and jurisdiction-specific legal determinations belong to qualified legal authority.

Repository inspection establishes what code and tests express. A test present in source is not automatically executed evidence. The original October 18 audit ran no product checks; this refresh separately identifies current inspected source, recorded October 20 implementation execution, older operational evidence and still-proposed validation. The completed work's results are incorporated with their actual scope, not represented as rerun for this document edit. Official platform documentation establishes interface constraints, not that this application uses every interface correctly. No stakeholder study or new production-host qualification is established.

“Missing” is used in four different, explicitly distinguished senses:

1. **Not exposed:** a capability exists in a shared owner or another client but is absent from the reviewed Browser interaction.
2. **Workflow gap:** individual operations exist, but the person cannot complete or safely understand the end-to-end task.
3. **Assurance gap:** implementation may be correct, but the evidence needed for a support or protection claim is absent or insufficiently broad.
4. **Proposed extension:** useful future functionality that the current contract does not promise. It is not an existing defect merely because it is desirable.

Deliberate authority exclusions are not defects. Unsupported schema vocabulary is not automatically a defect either: expanding a bounded data language requires demonstrated user need, deterministic semantics, privacy review and conformance tests.

## Capability Parity Policy

### What should be identical

For the same admitted canonical definition, answer state, explicit options and engine version, clients should agree on:

- family/revision/digest identity and rejection of conflicting canonical content;
- supported field types, activation, defaults, validation and answer-revision rules;
- handling of inactive, missing, invalid, blocked and secret-deferred values;
- import eligibility and declared fidelity, rather than guessed inverse transformations;
- deterministic artifact content, sensitivity classification and coverage;
- the meaning of local preparation, incomplete readiness and unassessed target acceptance;
- stale-operation invalidation and cancellation effects on already established outcomes;
- public versus restricted data projections and the prohibition on accidental private-data disclosure.

Shared conformance vectors should establish this parity. Comparing labels or screenshots is not an adequate substitute. The CLI must not become a second independent validator whose results Browser approximates, and Browser must not independently reinterpret the definition language in UI code.

### What should be equivalent but differently presented

A command, a keyboard action and a graphical flow can accomplish the same task. Detailed validation can be paginated machine data in CLI and a linked diagnostic panel in Browser. A bundle can be an explicit repeated argument in CLI and a reviewed artifact selection in a graphical client. A maintainer operation can be discoverable under an advanced workspace without cluttering ordinary configuration.

The acceptance question is: **can the relevant person achieve the same supported outcome, with the same semantic safeguards, using this client?** It is not whether each client contains a control named after every command.

### What must remain different

| Dimension | Browser | Native clients | Consequence |
| --- | --- | --- | --- |
| Input authority | Explicitly selected files, admitted public sources and user-entered answers | Explicit local paths and supported native input mechanisms | Browser should not pretend to scan or administer the host. |
| Output authority | Deliberate copy/download; optional future permissioned APIs require their own design | Protected writer where host/filesystem support is qualified | Equal artifact content does not imply equal custody guarantees. |
| Process interaction | UI events, document lifecycle, workers where justified | Process signals, exit codes, standard streams and native windows | Lifecycle contracts must preserve the same user facts through different mechanisms. |
| Offline availability | Previously acquired assets and public definitions; browser storage can be evicted | Installed package and available local resources | An install icon is not a guarantee that all definitions or updates are offline. |
| Automation | Local user interaction; optional bounded machine-readable exports | CLI is the principal automation interface | Browser need not run arbitrary command lines or become a remote agent endpoint. |
| Trust surface | Hosting origin, delivered application code, browser and extensions | Installed package, OS, local filesystem and application provenance | Both need truthful trust explanations; neither is risk-free merely because it is local. |

### Exception register, not permanent second-class clients

Every intentional capability difference should name the affected persona, the platform constraint, the alternative workflow, the residual risk and the evidence needed to reconsider it. “Browser is for humans” and “CLI is for agents” are insufficient justifications for withholding validation, inspection or comparison. Conversely, forcing a native-only operation into Browser through a cloud proxy would add custody and operational obligations solely to satisfy a superficial parity score.

### Proposed dispositions for the important differences

The following are recommended product decisions for future planning, not features already promised or new implementation authorization. **Required semantic parity** means identical domain meaning wherever the operation runs. **Required persona outcome** means a client claiming the named completion profile must expose the outcome through its own usable interaction. **Justified client-specific capability** has an adequate alternative without pretending the interfaces are identical. **Conditional extension** needs a separate value, custody or authority decision.

| Difference | Proposed disposition | Practical consequence and completion gate |
| --- | --- | --- |
| Field types, presence, activation, defaults, validation, deterministic output and identity | Required semantic parity and required operator outcome | Every operator-complete client must reach the admitted states and produce equivalent results; a Core API without a client interaction is insufficient. |
| Safe explanations and exact diagnostic locations | Required persona outcome in every client | CLI supplies readable/machine diagnostics; graphical and terminal forms make the same correction reachable. No client may require another client merely to explain its own validation result. |
| Secret-deferred drafts | Required semantic parity and recommended required operator outcome across all four clients | Browser's existing draft workflow should become an explicit native-client capability, rather than be removed from Browser. Each adapter must distinguish drafts from readiness and preserve its own restricted-output handling; this remains native-client work to plan and verify. |
| Existing-file assessment, comparison and projection proof | Required advanced-operator outcome | Every client advertised as advanced-operator-complete should expose these supported local operations. A CLI result need not become an identical graphical diff, but comparison meaning and privacy must agree. |
| Import conflicts | Required safe rejection and explanation; merge is a conditional extension | Advanced completion can use atomic rejection with the conflicting field/rule and an explicit edit/re-import next step. It does not require automatic merging or a merge editor. Any later resolver needs separately specified conflict semantics. |
| Selected multi-output preparation | Required operator outcome for multi-output definitions | Offer deliberate selection and accurate aggregate/per-artifact state in each client; transport remains platform-specific. Download-all/archive is optional convenience, not required save equivalence. |
| Public maintainer authoring | Required persona outcome in clients claiming maintainer completeness | Recommend that Browser and CLI expose the full bounded authoring workflow. TUI/Desktop may deliberately remain operator-focused, with explicit handoff to Browser/CLI; they must not advertise maintainer completeness until they expose it. Add native maintainer interfaces when that persona has demonstrated demand, rather than duplicating an editor merely for symmetry. |
| Store browsing and pinned web links | Justified client-specific discovery interaction | Browser can remain the discovery front door. Native clients must reliably open the resulting public definition and show its identity without requiring Store availability; native integrated search or URL retrieval is optional, separately admitted network behavior. A client claiming a complete discovery workflow must provide its own discoverable access or clearly disclose the handoff. |
| Interactive CLI prompts | Justified interaction difference | A human-usable CLI needs comprehensible help, inputs and diagnostics, not a second TUI. Structured local input plus a documented TUI/Browser handoff is acceptable; neither human readability nor configuration correctness is optional. |
| Artifact body preview and local secret generation | Conditional extensions | Preserve Browser's explicit preview; do not automatically print private bodies in native interfaces. Generation/reveal may be added through explicit actions with the same sensitivity and lifecycle rules. |
| Portable checkpoints, undo and multiple workspaces | Conditional extensions with cross-client semantics if adopted | First decide contents, identity, consent, lifetime and recovery. A later shared checkpoint contract must be interoperable where supported; no client may silently create it to improve its parity score. |
| Browser export versus native protected save | Justified authority difference | Match artifact bytes and truthful outcome reporting, not filesystem guarantees. Browser must retain its export limitations; unsupported native writers must continue failing closed. |

This policy deliberately requires more than “the results agree when a feature happens to exist,” while avoiding a mandate that every surface serve every persona. The operator profile is shared; advanced and maintainer claims carry additional explicit obligations. Exceptions need a useful alternative and honest advertised scope, not a permanent label of “less capable because it is graphical.”

## Stakeholders

| Stakeholder | What they need | How an incomplete design can burden them |
| --- | --- | --- |
| First-time self-hoster | Understandable choices, safe defaults, actionable errors and a clear next step | A syntactically generated file is mistaken for a deployable configuration; unexplained technical fields encourage unsafe guessing. |
| Experienced operator | Import, comparison, exact provenance, repeatability and control over output | A form-only interface forces a second tool for every advanced check or hides meaningful changes. |
| Definition maintainer | Bounded authoring, precise repair feedback, synthetic fixtures and reviewable revisions | They hand-edit fragile identifiers or publish an apparently valid definition without understanding sensitivity and compatibility. |
| People represented in configuration | Confidentiality, data minimization and avoidance of accidental exposure | Personal addresses, tenant data or credentials escape through diagnostics, downloads, browser history or support requests. |
| Keyboard, screen-reader and low-vision users | Equivalent completion and error-recovery paths | A formally present feature becomes unusable because focus, announcements, reflow or conditional controls fail. |
| Arabic/RTL and other language users | Correct text direction, meaningful translation and unambiguous numeric input | Identifier humanization or visual order gives misleading meaning; technical tokens become hard to distinguish. |
| Low-bandwidth, offline and low-resource users | Bounded loading, truthful offline state and responsive large forms | Eviction, long main-thread work or hidden network requirements make completion unreliable. |
| Shared-device and privacy-sensitive users | Explicit custody choices, clear/reset behavior and realistic limits | “Local” is mistaken for guaranteed erasure or a clipboard/download is forgotten outside the workspace. |
| Self-hosted Browser operators | Reproducible deployment, secure hosting guidance and update/rollback procedures | Application integrity depends on an inadequately configured origin or stale caches with no operational explanation. |
| Contributors and publishers | Neutral discovery, visible provenance and fair correction/appeal processes | Curation is confused with endorsement, commercial placement or authenticated identity. |
| Target application maintainers | Honest separation between generic preparation and target-owned acceptance | Configurator claims imply target compatibility, authorization or successful deployment without target evidence. |

## I-VSD Principles and Domains

The selected principles guide provider-controlled decisions rather than decorate a feature list:

- **Amanah — trust and entrusted responsibility:** private answers and user expectations require explicit custody, bounded processing and reliable state transitions.
- **Sidq — truthfulness:** labels must distinguish admitted, locally validated, prepared, dispatched, downloaded, protected and target-accepted outcomes.
- **Adl — justice:** accessibility, language, offline and client differences must not impose avoidable barriers on particular users. Neutral discovery must not conceal favoritism.
- **Non-harm:** avoid foreseeable credential disclosure, configuration loss, unsafe retries and mistaken deployment confidence.
- **Rights of people:** respect privacy, contributor attribution, third-party licenses, correction rights and the interests of people whose data appears in configuration.
- **Promise-keeping:** advertised browser support, offline behavior and parity commitments need scope-specific evidence and maintenance ownership.
- **Ihsan — excellence:** choose maintainable shared semantics, useful tests and clear interactions rather than superficial feature counts.
- **Haya and restrained exposure:** do not expose private values unnecessarily in previews, shared screens or diagnostics; this is a design consideration, not a claim that masking alone protects data.
- **Avoiding spying:** no default telemetry, answer-bearing network traffic or hidden collection; any future measurement must be separately justified and voluntary.

These principles apply across all six review domains:

| Domain | Review question |
| --- | --- |
| Strategic | Which users and workflows does “feature-complete” actually promise to serve? |
| Design | Can people understand choices, errors, custody and consequences without specialist assistance? |
| Technical | Are semantics shared, operations bounded, private projections isolated and stale work invalidated? |
| Operational | Can hosts deploy, update, recover and support the product without exposing answers or overstating evidence? |
| Governance | Who owns definitions, curation, trust claims, exceptions, licenses and corrections? |
| Evaluation | Which reproducible tests and stakeholder outcomes establish that the promises hold? |

There is no evidence in this scope of a payment, financing or interest-bearing product requiring a specific financial ruling. Such questions should not be invented to make the audit appear more comprehensive. If monetization later changes ranking, access to privacy protections or contributor terms, it requires a new provider-responsibility review and any appropriate specialist escalation.

## Browser Platform Boundaries Established by Research

The official sources in the external register were searched and inspected on 2026-10-18. Their constraints explain which parity goals are appropriate and which require deliberately different contracts. They do not replace application testing.

### File access and export

Opening a user-selected file is compatible with local-only processing. It is not a browser-enforced promise that application code cannot upload the bytes: fetch can transmit file/blob data. Size, structural admission, cancellation, private metadata handling and the application's actual network behavior therefore matter [W03, W12].

The picker-based File System Access capability is not uniformly available across browser engines. `showSaveFilePicker()` requires a secure context and transient user activation. The broader availability of `FileSystemFileHandle.createWritable()` does not prove that a browser offers the user-visible picker. A writable stream may replace existing content; exclusive stream locking does not mean create-new-only publication. These APIs do not provide the native writer's POSIX-mode or Windows-ACL guarantees [W01, W02, W18].

Accordingly, a future optional save-picker enhancement can improve convenience and API-level completion feedback. It must feature-detect the exact operation, preserve a standard download path, distinguish cancellation/permission/failure, and avoid calling itself a protected save. Preparation should finish before a fresh explicit save action if that is necessary to preserve user activation. Browser does not become equivalent to the Linux protected writer by using a more convenient picker.

Ordinary download links provide no application-level proof that the user retained a file at the intended location. The browser can block, rename, cancel or fail a download. Automation completion hooks are not ordinary page APIs. The truthful state is typically **prepared** followed by **download requested**, not **saved successfully**. A single archive could reduce multiple-download ambiguity, but still would not prove delivery, extraction, file permissions or target acceptance [W17].

### Clipboard custody

Clipboard permission and activation behavior differ across Chromium, Firefox and Safari. Handle the actual write result rather than assuming a single portable permissions-query workflow. Do not read back clipboard content simply to test that a copy succeeded [W04].

Clipboard history, synchronized devices and pasted copies are outside the workspace. Windows explicitly supports history and cross-device synchronization, with pinned entries surviving ordinary clearing in some circumstances [W05]. Clearing answers or revoking consent cannot erase those copies. Delayed automatic clipboard overwrite is a poor default: it may be denied after user activation expires and could destroy newer content owned by the user.

### Offline storage and updates

Browser storage is normally best-effort. Caches, IndexedDB and origin-private files are subject to quotas, eviction, user deletion and private-mode lifecycle. A persistence request can be denied and does not prevent deliberate deletion. Storage estimates are approximate. WebKit policies vary by browser/application context, so a universal quota or simplistic seven-day survival promise would be misleading [W06, W07].

The product should distinguish four things: a complete cached application version, a retained admitted definition, an update installed but waiting, and the version actually controlling the workspace. A new service worker normally waits for old clients; forcing activation can mix old pages and new worker behavior. The .NET 10 Blazor PWA documentation describes coherent asset installation and retention of the old working version when a new cache fails, but each published package still requires verification [W08, W09].

An installed PWA is not permanent availability. Explicitly retained public definitions are reconstructible public resources, not a backup for private answers. Appearance preferences are a separate allowed persistence category. “No answer persistence” must not be simplified into “this browser stores nothing.”

In the current application, prepared assets and service-worker custody acknowledgement are also prerequisites for **online editing**, not only offline use. Missing service-worker support, failed registration or unavailable required cache preparation leaves workspace entry blocked; there is no online-only editing fallback. A visible page therefore does not prove that its configuration controls are usable. Explain this support prerequisite and test denied/unavailable environments explicitly. Whether a particular private-browsing, managed or embedded context triggers it needs execution evidence rather than an assumption about that context [B15, B20].

### Origin, CSP and remote-definition trust

WebAssembly is a runtime choice, not a confidentiality barrier against malicious application code. A dedicated origin without injected third-party scripts reduces shared-origin exposure; it does not prove the origin or delivered code is trustworthy. Same-origin storage is not isolated merely because applications have different URL paths [S06, S07].

CSP is useful containment. `'wasm-unsafe-eval'` permits WASM compilation without the broader JavaScript-evaluation permission of `'unsafe-eval'`. However, connection controls alone do not constrain every possible exfiltration channel, allowed destinations still receive allowed requests, and workers have relevant policy-delivery rules of their own. The current meta policy cannot supply `frame-ancestors`; effective deployment headers and worker responses need review [W10, W16, S09].

CORS controls whether script can read a cross-origin response, not whether the request contacted a host or whether the content is trustworthy. Opaque `no-cors` responses are unsuitable for inspectable definition admission. Credential omission, referrer suppression, redirect handling, bounded reads and canonical pinning are separate controls. A raw resource integrity hash and a canonical definition digest answer different questions [W11, W12].

A public-looking hostname is not portable proof of a public-network destination. Browser fetch does not expose a general DNS/IP-pinning contract. Chrome's local-network permission defenses are browser-specific, not a substitute for application source-admission rules. Even credential-free retrieval reveals connection metadata to network participants. Definition retrieval must remain before private-answer collection, and the interface should explain that local processing does not make the initial public download anonymous [W14].

Cache Storage does not automatically enforce ordinary HTTP cache expiration or `Cache-Control` rules. Public-cache exclusion of answers must be an application invariant, not an assumption about server response headers [W13].

### Recovery, encryption and responsiveness

Encrypted browser drafts would change the current transient-custody contract. A non-extractable Web Crypto key cannot be exported through the API, but hostile code able to use the key can still invoke cryptographic operations. Keeping ciphertext beside an automatically usable key does not establish an independent unlock boundary. Web Crypto does not promise hardware-backed storage, disk encryption or forensic zeroization [W15].

Therefore the first recovery proposal should be an explicit, user-controlled portable workspace file with a reviewed privacy and identity contract, not silent IndexedDB autosave. Whether such a file may include secrets, and whether encryption is required, is a future product/security decision. Revocation and clear/reset must remain truthful about what copies they can and cannot remove.

Workers can improve responsiveness and support operation termination, but they are not a sandbox for executable definitions or a guaranteed memory/CPU quota. Message copying, transfer semantics, networking and storage access require review. Generic Worker support does not establish that a particular .NET/WASM threading configuration is usable on every supported device [W16]. First measure large-form and bounded-input latency. Public-definition computation can be considered for a bounded, cancellable, generation-fenced worker after compatibility review. Answer-bearing validation/preparation must remain within the existing private execution boundary unless an explicit custody decision authorizes a new design: current contracts exclude answers and prepared output from worker messages. Measurements alone do not authorize that change; see IVSD-M014/M015.

## Accessibility and Internationalization Completion Contract

Accessibility is an operator-completion requirement, not a final visual polish phase. The current code contains useful semantics and tests. October 20 observations add desktop/mobile-width light/dark rendering, measured facet targets, actual review/correction focus and a limited narrow/doubled-text probe. The probe requested 320 CSS pixels but produced a 325-pixel layout viewport; it is not exact 320-pixel, native 400% zoom or complete WCAG evidence. Screen-reader, full keyboard-process, forced-color, RTL, contrast and physical-device qualification remain open. Older operational caveats retain their dates rather than negating these newer bounded observations [O03–O06, O17].

### Required observable outcomes

| Area | Acceptance criterion | Evidence needed |
| --- | --- | --- |
| Keyboard | Open a definition, edit all supported types, insert/remove rows, correct errors, consent/revoke, prepare, export and leave without a pointer; no trap or unexpected focus loss | Actual published package on declared browser/OS pairings; WCAG 2.1.1, 2.1.2, 2.4.3 |
| Focus | Focus remains visible and is not entirely obscured by authored content; removing a trigger/row or completing asynchronous work leaves a logical next location | Native keyboard observation, including sticky areas and error panels; 2.4.7, 2.4.11 |
| Error recovery | Each error identifies the relevant control and actionable correction without echoing private values; hidden invalid fields remain reachable | Programmatic associations plus screen-reader task completion; 3.3.1–3.3.3 |
| Status changes | Preparation, import, clipboard failure, offline and update states are announced without unnecessary focus movement or repeated noise | Declared screen-reader/browser pairings; 4.1.3 |
| Reflow and text | Ordinary forms remain usable at 320 CSS pixels and with 200% text resizing; text-spacing adjustments preserve content and operation | Actual native zoom as well as viewport/text tests; 1.4.4, 1.4.10, 1.4.12 |
| Pointer targets | Targets meet 24×24 CSS pixels or a documented WCAG 2.5.8 exception, including spacing alternatives | Measurement of row controls, disclosure actions, icons and error links; 44×44 is not the AA minimum |
| Contrast and themes | Text and meaningful non-text states remain distinguishable across themes, errors and forced colors; also review whether disabled controls remain understandable | Measurements and actual rendering, not palette names alone; 1.4.3 and 1.4.11 exempt inactive controls from their contrast minima, so the additional disabled-control review is a product-quality goal |
| Motion | Nonessential motion respects user preferences without hiding status or completion information | Actual reduced-motion behavior and equivalent static cues |
| Redundant input | Valid transient answers survive correction and navigation within the same process; avoid unnecessary repeated entry | Workflow testing; 3.3.7 does not require persistent secret storage |
| Language | Supported languages are declared separately from supported text direction; language metadata and terminology are meaningful | Native-language review; 3.1.1, 3.1.2 with their technical-term exceptions |
| Mixed direction | Arabic/Latin text, digests, URLs, keys, filenames, digits and punctuation remain visually understandable and copy exactly | Native-language keyboard/screen-reader tests and byte-exact output assertions |
| Authentication | Reassess accessible authentication if a hosting gateway introduces login | WCAG 3.3.8 is not automatically applicable merely because configuration contains a password field |

The normative reference is WCAG 2.2 [S01], supplemented by W3C internationalization and WAI cognitive/user-evaluation guidance [S02–S05]. Automated checks, component assertions and manual inspection each cover different failure modes. No one method or small participant sample can establish universal accessibility.

### Language work that is genuinely missing from a direction toggle

RTL layout does not translate the interface, identify the language of definition prose, choose culturally appropriate terminology or establish input semantics. A complete localization design should distinguish application UI strings, publisher-authored labels/help, technical identifiers, locale-independent serialized values and user-entered text. Missing translations need an explicit fallback rather than invented translations of canonical keys.

Definition authors also have responsibilities. A client can humanize `databaseHost`, but cannot infer why a setting matters, which value is safe, or what deployment consequence it has. Bounded authored guidance, concepts and deliberate public help links are now supported; useful labels and explanations still depend on the author. Do not silently rewrite the meaning of a pinned definition at render time. The existing human-label follow-up is complementary to Browser work, not a substitute for it [B24, B26].

## Current Architecture and End-to-End Inventory

### The existing control and data flow

1. **Choose a public source.** Catalogue, local-file admission, bounded public HTTPS retrieval or optional Store selection resolves a definition before answer custody. Local files can contain compact authoring or expanded runtime definitions; remote retrieval currently admits runtime JSON only [B01–B04].
2. **Admit and pin.** Core parses/compiles the bounded vocabulary; the registry binds family/revision to canonical content. A workspace retains its admitted revision and digest. Publisher labels and Store metadata do not authenticate a publisher [B01–B04, C03–C05].
3. **Establish custody.** Offline/application readiness and workspace generations gate entry. The presentation form owns accepted typed answers separately from invalid draft text, row identities and provenance; secret entry starts disabled for each workspace. Consent revocation clears secret leaves and invalidates output. Each emitted scalar callback additionally checks its captured form, literal path and input revision before editing, preventing delayed fill from reviving cleared values [B12, B14–B15, B25, B28].
4. **Edit, discover and review.** Browser mounts the selected authored section over the shared form; unmounting a control does not discard unfinished input. Core owns activation/validation and literal diagnostic paths. Presentation projects required obligations, provenance and a value-free metadata index. Search, facets, experiences and disclosure are view changes, not answer edits. Masked review and correction links route to the exact active field, collection row, controller or consent location [B24–B29].
5. **Import or prepare.** Eligible inverse imports are offered before manual questions. They replace only pristine authored defaults, record actually provided roots and preserve unrelated origins/drafts/rows; rejection changes no answers. Explicit empty strings survive the interactive snapshot and composition. Preparation captures a snapshot and preflights aggregate output before publishing prepared artifacts. Draft preparation never guesses unresolved secret-dependent projections: affected artifacts are withheld, while unrelated usable draft artifacts may remain available. Ordinary validation errors and fatal aggregate/budget failures clear the bundle rather than expose an earlier partial result [B10–B11, B25, B27].
6. **Export deliberately.** Prepared bodies stay outside preview DOM until reveal. Copy/download is per artifact and explicit; edit, clear, navigation, replacement and disposal invalidate generations, clear owned buffers/previews and revoke export URLs [B13–B14].
7. **Close and reopen.** Private answers are transient. Retained public definitions can reopen into an empty workspace; appearance preferences are a separate bounded persistence category. Reopening is not private-session restoration [B14–B16, O08].

This is a sound separation to preserve. The main architectural improvement is to make shared capabilities consistently reachable and understandable, not introduce a Browser backend or route domain operations through a shell invocation.

### Matrix legend and limits

**Yes** means a reviewed user-reachable capability, not qualification on every host. **Partial** names a concrete restriction. **No UI** means the capability was not found in the reviewed client interaction. **Excluded** means a deliberate authority boundary. “Next” is a recommendation, not a promise already made by current code. Each row is grounded by the evidence IDs shown; broader browser/platform qualification is discussed separately.

### A. Source admission, discovery and public identity

| Capability | Browser | CLI | TUI | Desktop | Next / evidence |
| --- | --- | --- | --- | --- | --- |
| Open local compact definition | Yes, JSON/YAML | Yes | Yes | Yes | Preserve shared compilation/admission [B02, C03] |
| Open expanded runtime definition | Yes | Yes | Yes | Yes | Shared identity and vocabulary [B01–B03, C03] |
| Public HTTPS definition retrieval | Yes, bounded runtime JSON, pre-answer | No route | No UI | No UI | Deliberate source policy; do not silently broaden formats/authority [B03, C04] |
| Pinned launch/share link | Yes, public selectors/digest only | Explicit local invocation | Local selection | Local selection/reload | Useful Browser-specific sharing; never encode answers [B01, B03] |
| Optional catalogue/Store | Yes, names/tags, explicit revision review | No store route | No store UI | No store UI | Browser already leads this capability [B04, C04] |
| Registry compatibility selection | Launch selectors | Explicit source operations | Local registry/version selection | Load/reload selected source | Preserve version/digest meaning; UI transport may differ [B01, C04–C05] |
| Canonical digest and revision pinning | Yes | Yes | Yes | Yes | Shared invariant, not publisher authentication [B01–B04, C05] |
| Source metadata before editing | Yes, catalogue/store/review | Catalogue/definition results | Definition selection | Loaded identity | Add a consolidated inspectable overview where needed [B01–B04, C05] |
| Retain public definitions offline | Yes, explicit confirmation | Local files/package | Local files/package | Local files/package | Different storage contract [B15, C03] |
| Delete/replace retained definitions | No management UI | Filesystem workflow | Filesystem workflow | Filesystem workflow | Browser needs bounded inventory/removal/recovery [B15, B20] |
| Authenticated publisher trust | Not established | Not established | Not established | Not established | Do not turn labels/digests into trust badges [O12, R03] |

### B. Editing, understanding and validation

| Capability | Browser | CLI | TUI | Desktop | Next / evidence |
| --- | --- | --- | --- | --- | --- |
| All seven admitted field types | Yes | Typed JSON input | Typed editor | Typed controls | Retain semantic parity [B05, C08–C09] |
| Nested objects/object-list rows | Yes | JSON workspace | Yes | Yes | Cross-client conformance fixtures [B05, C09] |
| Distinguish absence, false and empty containers | Yes, explicit omission and typed presence | Explicit JSON | Supply/Clear controls | Shared typed presence; native controls differ | Original Browser gap resolved; broader native interaction qualification separate [B25–B26, C09, O16] |
| Preserve explicitly supplied top-level empty string | Yes, including accepted imports and prepared output | Direct JSON input preserves it | Corrected shared model | Corrected shared model | Original shared fidelity defect resolved, not three client patches [B25, O16, O18] |
| Preserve valid empty string-list item | Yes, including edit-back-to-empty | Explicit JSON | Separate typed path | Separate typed path | Shared and rendered regression now covers historical Browser defect [B25–B26, O16] |
| Conditional activation | Yes, admitted vocabulary | Yes | Yes | Yes | Nested field conditions are rejected by language admission, not a Browser-only omission [B06, C10] |
| Guided/advanced disclosure | Authored experiences/sections and local optional expansion | Catalogue/explicit inputs | Authored experience/optional controls | Authored experience/optional controls | View state is separate from answers/validation; direct native caller migration [B24–B26, C10, O18] |
| Defaults and answer provenance | Defaulted/imported/explicit/omitted review states | Shared semantics; not identical review UI | Shared semantics | Shared semantics | Provenance tracks mutations, not equality to a default [B25, B27] |
| Explicit recommended-value action | No UI | Metadata, no interactive action | No action | Yes for permitted public values | Browser should reuse admitted public recommendations [B09, C10] |
| Automatic declared-format guidance | Not consumed by field renderer | Structural metadata | Authored guidance | Constraints/help | Expose format/constraint meaning without changing values [B09, B21] |
| Human labels/help/consequences | Yes, bounded authored guidance and deliberate public links | Structural metadata/help | Yes | Yes | Author quality and language review remain necessary [B24, B26, C10, R07] |
| Search fields/keys/help | Yes, metadata-only search/facets including output bindings | External machine inspection | No reviewed search UI | No reviewed search UI | Preserve focus, independent resets and no answer indexing; measure benefit [B29, O17] |
| Section navigation/correction routing | Yes, exact paths, row/controller recovery and Change/Return | Not a form UI | Field navigation | Native form/focus | Selected section alone mounts; shared drafts survive navigation [B25–B27, C06, O17] |
| Required-answer progress | Active required obligations, missing/invalid/deferred states; no percentage for zero required work | Shared facts; different interface | Shared facts | Shared facts | Answer readiness is not prepared/exported/target-accepted state [B25–B26] |
| Constraint-specific diagnostics | Literal nested paths/rows improved; rule text remains partly generic | Bounded codes/pages; original `/input` limits not requalified | Shared diagnostics; client wording differs | Shared diagnostics; client wording differs | Finish rule-specific bounds/format explanations, not another path-model rewrite [B25–B27, C07] |
| Structural source locations/repair | Partial admission messages, no repair workbench | Slots/pages; not comprehensive mapping | No authoring UI | No authoring UI | Shared source mapping plus Browser repair interaction [B02, B17, C07] |
| Undo/reversible editing history | No UI | Caller-managed inputs/version control | No general undo | No general undo | Optional bounded in-memory design; account for secret retention [B17, C18] |
| Explicit operation progress/Cancel | Lifecycle cancellation; no dedicated UI | Process cancellation | Responsive operations/close handling | Generation-fenced operations | Measure expensive paths and expose meaningful cancellation [B14, C17] |

### C. Import, comparison and recovery

| Capability | Browser | CLI | TUI | Desktop | Next / evidence |
| --- | --- | --- | --- | --- | --- |
| Import supported existing artifacts | Yes, bounded inverse | Yes | Yes | Yes | Preserve atomic validation and consent [B11, C11] |
| Explain unsupported inverse before selection | Only eligible mappings offer import; supported limit explained | Failure through explicit command | Limited | Limited | Do not confuse absent inverse support with invalid file content [B27, C11] |
| Assess existing file without mutation | No distinct assessment UI | `check-existing` | Import-oriented | Import-oriented | Add Browser inspect/assessment mode [B11, C11] |
| Export prefilled answer values | No answer-workspace export | Restricted `prefill` file | No UI | No UI | Distinguish values from full session format [B17, C11, C18] |
| Prove projection against supplied answers | No UI | `--answers` recomposition | No UI | No UI | Good advanced Browser capability [B17, C12] |
| Compare current artifact with baseline | No UI | Equal/different result, not rich textual diff | No UI | No UI | Shared comparison plus private local review [B13, C12] |
| Review/resolve import conflicts | Atomic rejection plus value-free field/controller/consent recovery; no merge | Rejects conflicts | Rejects conflicts | Rejects conflicts | Correction/re-import outcome delivered; assessment and optional merge remain distinct [B27, C11, O17] |
| Revision-bound import-request artifact | No equivalent revision-injection workflow | Explicit `artifact import` | Generic declared-output preparation | Generic declared-output preparation | Expose only with shared semantics and no target transport [C13] |
| Portable full workspace checkpoint | Absent by transient design | Caller files, not full session contract | Absent | Absent | New reviewed contract, not ordinary cache/autosave [B14, B17, C18] |
| Automatic cloud synchronization | Excluded | Excluded | Excluded | Excluded | Not necessary for completeness; new custody decision [R01, R05] |
| Multi-workspace/history dashboard | No reviewed workflow | Separate invocations/files | One session | One loaded workspace | Optional after single-workspace correctness; prevent cross-workspace leaks [B14, C18] |

### D. Preparation, output and custody

| Capability | Browser | CLI | TUI | Desktop | Next / evidence |
| --- | --- | --- | --- | --- | --- |
| JSON/YAML/dotenv/Markdown output | Yes, declared projections | Yes | Yes | Yes | Format assertion is not arbitrary format conversion [B10, C14] |
| Complete preparation | Yes | Yes | Yes | Yes | Shared readiness/coverage semantics [B10, C06] |
| Secret-deferred draft preparation | Yes | No reviewed route | No UI | No UI | Browser leads; shared draft API is not native UI completion [B10, C21] |
| Select outputs before preparation | No UI; prepares active outputs | Yes | Yes | Yes | Add explicit subset semantics and clear bundle status [B10, B13, C15] |
| Aggregate preparation preflight | Yes; documented draft omissions allowed | Yes | Yes | Yes | Fatal validation/budget failure clears results; withheld unresolved draft artifacts do not invalidate unrelated usable drafts [B10, C15] |
| Reveal private artifact body | Deliberate reveal | Restricted file, not private terminal stdout | Metadata only | Metadata only | Do not add automatic reveal to satisfy parity [B13, C14] |
| Copy/download per artifact | Yes | Not Browser transport | Not Browser transport | Native save | Existing Browser export contract [B13, C16] |
| Download all/archive | No UI | Native selected bundle | Native selected bundle | Native selected bundle | Optional single archive after all preparation; no save-completion claim [B13, W17] |
| Restart/apply-consequence display per output | Declared filenames and apply/restart metadata in review | Contract/declared operation information | Preparation metadata | Preparation metadata | Delivered metadata is not target acceptance or output-subset selection [B27, R02] |
| Protected create-only native save | Deliberately unavailable | Qualified Linux only | Qualified Linux only | Qualified Linux only | Not a Browser parity target; other native hosts still fail closed [C16, W01–W02] |
| Secret consent/revocation | Explicit per workspace | Explicit local input custody | Native input custody | Native input custody | Different consent interaction, same disclosure discipline [B12, C08] |
| Local secret generation | No UI | Shared API only | Shared API only | Shared API only | Optional reviewed capability; no implicit generation/copy/persistence [B17, C21] |
| Password-manager-friendly entry | Consented stable identities and new/current-password hints; issuer tokens stay off | Native input workflow | Native input workflow | Native input workflow | No vault API or generator; actual extension/browser compatibility unqualified [B28, O17] |
| Target login/apply/administration | Excluded | Excluded | Excluded | Excluded | Preparation does not grant target authority [R01–R02] |

### E. Offline, accessibility and assurance

| Capability | Browser | Other-client comparison | Next / evidence |
| --- | --- | --- | --- |
| Offline application and retained-definition reopening | Implemented with bounded public caches | Native packages use local resources | Preserve existing flow; qualify eviction and recovery [B15, W06–W09] |
| Service-worker/cache readiness before editing | Required even online; no online-only editor fallback | Native process prerequisites differ | Qualify registration/cache denial and unavailable service workers as blocked editing, not merely lost offline support [B15, B20] |
| Reconnect after offline initialization | Captured state can remain stale | Different native connectivity model | Source-derived recovery defect [B20] |
| Retry failed readiness initialization | Failure can remain memoized | Different process lifecycle | Explicit safe retry before custody [B20] |
| Visible cache/version/update dashboard | No reviewed management UI | Native package/version mechanisms differ | Explain cached/waiting/active and retained inventory [B15] |
| Keyboard and screen-reader semantics | Implemented foundations plus limited current actual focus checks | TUI/Desktop also have foundations and gaps | Complete-process keyboard/AT qualification remains open [B26–B29, O17–O18, C20] |
| Theme/contrast/direction controls | Yes | Native presentation differs | Do not confuse themes/RTL with verified accessibility [B16, O05–O06] |
| Translated Browser shell/diagnostics | Absent; English UI | TUI has English/Arabic resources; Desktop labels remain English | Declare and qualify languages separately [B16, C20] |
| Mobile/low-resource workflow qualification | Current desktop/mobile-width light/dark and narrow-text observations | Not an identical native-client target | Emulated width is not physical-device or large-form performance proof [O17, W16] |
| Current browser-engine support matrix | Not established | Native RID admission also differs from qualification | Publish scoped release evidence, not framework inference [O01–O02, O13, C22] |
| Production-host headers/update/incident qualification | Not established by local package QA | Native publication has separate trust lifecycle | Dedicated-origin deployment and operational ownership [O09–O14] |

### F. Maintainer capabilities

| Capability | Browser | CLI | TUI/Desktop | Recommendation / evidence |
| --- | --- | --- | --- | --- |
| Compile a local public authoring document | Yes, admission only | Yes | Yes, load path | Do not confuse compilation with an editor [B02, C01, C03] |
| Create initial public definition | No editor | `definition init` | No authoring UI | Optional Browser maintainer workspace [B17, C01] |
| Scaffold from environment example | No UI | Keys-only scaffold | No UI | Preserve public-only keys and human semantic review [C02] |
| Scaffold from bounded JSON Schema | No UI | Supported subset/explicit differences | No UI | Shared compiler/importer; unsupported semantics must stay explicit [C02] |
| Check/build canonical public definition | No dedicated authoring workflow | `check` / `build` | No authoring UI | Inspect/edit/validate/export workflow, not arbitrary execution [B02, B17, C01] |
| Revise/fork with stable logical identities | No UI | `revise` / fork workflow | No authoring UI | Review candidate identity and changes before export [C01] |
| Semantic definition diff | No UI | No complete installed semantic-diff workflow established | No UI | Shared future capability, not simply copy a current CLI feature [R04, C01, C12] |
| Synthetic fixture/conformance runner | No UI | Strategy/partial existing test tooling, not complete installed lifecycle | No UI | Bounded local fixtures; no arbitrary scripts or real secrets [R04, R05] |
| Launch-link/embedding helper | Pinned source sharing exists | No equivalent complete authoring assistant | No UI | Extend existing safe link sharing; never answer-bearing URLs [B03, R04] |
| Publish to repository/Store | External contribution workflow | External tooling/workflow | External workflow | Prepare reviewable artifacts; authentication/submission is a separate decision [R03] |

This matrix deliberately shows capabilities where Browser is ahead—Store discovery, pinned web sharing, explicit secret-deferred drafts and deliberate body preview—as well as gaps where CLI or native clients are ahead. “Parity” should raise all clients against shared outcomes, not treat CLI as a perfect specification or reduce Browser to the lowest common denominator.

## Findings

Finding and mitigation IDs are stable within this report. They do not reuse or overwrite the meaning of similarly numbered IDs in earlier workstream reports. Severity reflects potential user impact and claim risk, not an assertion that an exploit occurred. Suggested owners are responsibilities to assign, not commitments already accepted by named people.

### IVSD-F001 — Define completeness by supported outcomes, not a universal feature count

An operator choosing Browser needs to know whether they can finish their task there. Today, useful capabilities are distributed unevenly: Browser has discovery and draft preparation, CLI has authoring and existing-file assessment, and native clients have different editing and publication controls. Calling all clients equivalent would obscure these differences; making every client imitate every transport would increase complexity without helping the person.

The provider controls advertised scope, shared engine contracts and which capabilities are exposed. A capability inventory and exception register should become the basis of support promises. Operator, advanced-operator, maintainer and assurance completion should be assessed separately. In particular, an optional advanced workspace can make Browser powerful without making a first-time configuration form resemble a developer IDE.

- **Lifecycle / severity / claim type:** open / high / strategic and communication gap.
- **Principles / domains:** Sidq, Promise-keeping, Adl, Ihsan / strategic, design, governance, evaluation.
- **Stakeholders / controlled decision:** all client users and maintainers / completeness labels, parity policy and workflow scope.
- **Evidence / validation level:** B01–B17, B24–B29, C01–C22, O16–O18, R01–R05 / current implementation, bounded recorded execution and design reasoning; no stakeholder validation or full cross-client qualification.
- **IVSD-M001:** adopt the parity policy and matrix above; publish justified exceptions and completion gates. Preserve useful Browser-specific capabilities rather than reducing them to CLI's current surface.
- **Owner / next validation:** product and shared-engine owners / run the same synthetic domain vectors through every claimed client and verify each persona's complete workflow.
- **Escalation boundary:** a client-specific omission affecting essential correctness, privacy or accessibility needs an explicit product decision; transport differences alone need not be eliminated.

### IVSD-F002 — Presence and empty-value defects are corrected in the shared form

The October 18 audit identified three related fidelity/recovery defects: Browser lacked a way to omit a supplied optional container; an edited-back-to-empty string-list item could become null; and the shared interactive representation collapsed an explicitly supplied top-level empty string into absence, including after an accepted import. These distinctions matter because an absent optional object can be valid when a supplied empty object with required children is not, while an explicit `""` can legitimately require a present empty output member.

The completed implementation separates accepted typed values from invalid draft text and explicit omission. Browser exposes an Omit action for optional answers, including containers; it clears the affected subtree without clearing siblings. Shared presence/provenance no longer derives from display-text emptiness. Imported `""` survives `Snapshot` and exact artifact composition, and string-list edit-back-to-empty remains `""` rather than null. Native callers were migrated to the shared model rather than given independent conversion patches [B25–B26].

The provider responsibility was to preserve the person's intended supplied/absent state and offer proportionate correction, not weaken Core validation. Shared literal-output assertions and rendered omission/empty-row regressions now cover those outcomes, within the recorded passing owner suites. This resolves the defects, not every native control or accessibility combination; those broader obligations remain in IVSD-F012/F016 [O16, O18].

- **Lifecycle / severity / claim type:** resolved / medium at discovery / corrected correctness and recoverability defects.
- **Principles / domains:** Non-harm, Amanah, Ihsan, Adl / design, technical, evaluation.
- **Stakeholders / controlled decision:** operators editing nested/optional configuration / presence controls and typed edit conversion.
- **Evidence / validation level:** original B18–B19/B23 at the October 18 revision; current B25–B26/O16/O18, especially `DefinitionAnswerPresenceTests.ImportedEmptyStringSurvivesSnapshotAndPreparation`, `ExplicitEmptyScalarAndContainersCanBeOmittedWithoutLosingSiblings`, `PristineEmptyListItemMatchesTypedThenErasedItem`, `DefinitionContainerPresenceTests` and `BrowserCollectionWorkflowTests.EmptyStringRowAndContainerOmissionPreserveSiblingAnswers` / inspected behavioral regressions and recorded passing shared/Browser suites, not a fresh full native-host matrix.
- **IVSD-M002:** delivered explicit omission and shared typed presence/draft representation. Retain independent snapshot/output and rendered regressions for empty strings, false/zero, containers, unrelated-answer preservation, stable rows and secret consent.
- **Owner / next validation:** Browser, Presentation and native-client owners / preserve these regressions when editing the shared contract; qualify complete native-host/accessibility journeys under IVSD-M012/M016 rather than reopening the corrected shared defect without contrary evidence.
- **Escalation boundary:** do not weaken Core validation or automatically prune intentionally supplied empty containers to hide the UI problem.

### IVSD-F003 — Validation must tell people how to correct the exact problem

Browser validates data and now preserves literal nested diagnostic segments, collection-row identity and exact correction routing. That addresses the location portion of the original concern, including dotted property names and inactive-controller recovery. Many rule explanations still collapse to `value-invalid` or `value-constraint-invalid` and generic advice to check declared constraints. A person entering an out-of-range number can reach the correct control yet still need raw definition JSON to discover the precise rule. Remaining work is rule explanation, not another diagnostic-path redesign.

CLI's bounded pages and optional source-location slots do not solve this automatically: current CLI field-validation diagnostics also have coarse `/input` mapping. Improve the shared diagnostic model so every client can present exact, value-safe constraint and path information. Browser then needs actionable correction links, declared limits and carefully grouped summaries, rather than another independent validator.

- **Lifecycle / severity / claim type:** open / high / workflow and diagnostic precision gap.
- **Principles / domains:** Amanah, Sidq, Adl, Ihsan / design, technical, evaluation.
- **Stakeholders / controlled decision:** novice, keyboard and screen-reader users; definition authors / diagnostic granularity, safe parameters, source/field mapping and correction UX.
- **Evidence / validation level:** B08, B21, B25–B27, C07, O16–O17; `DefinitionDiagnosticPathTests`, `ConfigurationDiagnosticText.Describe` and rendered collection/recovery tests / current literal-path implementation and bounded actual focus evidence; comprehensive rule-specific comprehension and AT correction unverified.
- **IVSD-M003:** retain delivered literal paths and reachable row/controller/consent corrections; complete violated-rule identity and public bounds/choices/formats guidance without interpolating private values. Keep source-document positions distinct from answer-field paths.
- **Owner / next validation:** Core diagnostics, Presentation and Browser owners / independently specified nested numeric, required-child, duplicate-row, pattern and conditional cases; complete corrections with keyboard and a screen reader without consulting source JSON.
- **Escalation boundary:** diagnostics must not expose rejected raw input, secret-dependent values, private paths or arbitrary parser exception text.

### IVSD-F004 — Import needs an understandable assessment and conflict workflow

The inverse importer intentionally accepts a narrow unambiguous projection. It rejects transformed, conditional, repeated or ambiguous mappings and conflicting current answers atomically. The interface now uses the same admitted mapping scanner to offer import only for eligible artifacts, before manual questions, and explains the 1 MiB/direct-mapping limitation. Only pristine authored defaults may be replaced; explicit equal-to-default values and invalid drafts are not overwrite permission. Known-field rejection offers an exact field, active-controller or consent correction without applying the file or enabling consent.

The separate read-only “inspect this existing configuration” workflow remains absent. It should show supported format, inverse eligibility, coverage, missing required answers and non-value-bearing conflict counts before application. The delivered atomic rejection plus explicit correction/re-import path satisfies this report's conflict-handling outcome; a merge editor is not required to close that subconcern. Silent overwrite or guessed inverse transformation would undermine the existing safety boundary. A successful prefill is not proof of complete configuration.

The shared empty-string fidelity issue identified in IVSD-F002 is corrected. Regression evidence checks the committed snapshot and eventual output, not only the importer's success flag. Actual provided roots also retain imported provenance even when their value equals a default; unmentioned answers, origins, drafts and row identities survive application [B25, B27].

- **Lifecycle / severity / claim type:** open / medium / advanced-operator workflow gap.
- **Principles / domains:** Sidq, Amanah, Non-harm / design, technical, evaluation.
- **Stakeholders / controlled decision:** operators migrating existing files / assessment, import eligibility, fidelity claims and conflict handling.
- **Evidence / validation level:** B11, B25, B27, C11–C13, O16–O17, R04 / current implementation, recorded rejection regressions and actual file/consent recovery observations; separate read-only assessment not delivered.
- **IVSD-M004:** retain delivered eligibility gating, default/provenance fidelity and atomic value-free correction/re-import; add existing-file assessment without mutation and coverage/refusal explanations. If merge is added, require an explicit per-conflict decision and invalidate all old prepared output.
- **Owner / next validation:** shared import and client owners / unsupported inverse, duplicate keys, unknown/inactive fields, wrong types, consent refusal, conflicting values and partial required coverage; assert the original form is unchanged on every rejected path.
- **Escalation boundary:** broader inverse mappings or automatic merges require a separately specified semantic contract, not a UI-only patch.

### IVSD-F005 — Add local comparison and provenance without leaking a second copy

Operators often need to answer “what will change?” rather than merely generate a file. Browser now provides masked positive review with mutation-derived defaulted/imported/explicit/omitted provenance, readiness and declared bindings, plus exact Change/Return navigation. This helps explain the current configuration without exposing answer values. It is not output-baseline comparison, projection proof or definition-version migration review; those remain absent. CLI's existing diff is narrower than a complete review UI: it reports equality/difference rather than a rich semantic explanation. The strategy's semantic definition diff is also a future capability, not something Browser can simply enable today.

A useful Browser comparison should separate answer changes, prepared-output changes and definition changes. A changed default, sensitivity rule, condition or output mapping may matter even when two files look similar. Private values should remain masked until a deliberate local reveal. Diff state must be invalidated when the definition, answers, baseline or selected outputs change; otherwise an apparently current comparison can be misleading.

- **Lifecycle / severity / claim type:** open / medium / missing advanced capability and future shared design.
- **Principles / domains:** Sidq, Amanah, Non-harm / design, technical, governance.
- **Stakeholders / controlled decision:** experienced operators and maintainers / comparison semantics, provenance and reveal policy.
- **Evidence / validation level:** B13, B17, B25, B27, C12–C13, O16–O17, R04 / current masked review/provenance and bounded recorded interaction; proposed comparison/projection-proof interaction not implemented.
- **IVSD-M005:** preserve delivered masked provenance review; add bounded local baseline comparison and projection proof, then separately design semantic definition/revision review. Identify source digest, answer revision, engine/format and comparison mode. Explain when equality is structural, byte-exact or merely extracted-field coverage.
- **Owner / next validation:** shared composition/comparison and Browser owners / independent expected changes for JSON/YAML/dotenv and byte-sensitive Markdown; stale-result rejection; no private diff text in logs, URLs, caches or support exports.
- **Escalation boundary:** migration must never silently rewrite private answers under a new definition or imply target acceptance.

### IVSD-F006 — Make multi-output preparation and delivery decisions explicit

Browser preflights aggregate preparation and produces multiple declared outputs. Declared filenames are now visible before collection; review shows declared apply/restart metadata, answer provenance/readiness and output bindings without exposing values or promising conditional inclusion. It still prepares active outputs without a selection UI and exports each separately. Remaining work concerns deliberate subsets, relationships between files and delivery state, not adding the already delivered review.

Preserve the intentional distinction between complete and draft bundles. Draft composition may withhold artifacts diagnosed as `draft-dependency-unresolved` or `artifact-omitted` while retaining unrelated usable draft artifacts. That is not a fatal partial-publication failure and must not be “fixed” by guessing dependencies or discarding valid unrelated drafts. Other validation failures and aggregate budget failures clear the prepared bundle. Review must separately show available drafts, withheld artifacts and fatal failure, without implying complete readiness [B10].

Add explicit artifact selection where the definition contract permits it and explain aggregate/per-artifact coverage and required relationships. Retain the delivered metadata/consequence review. Consider a single archive for convenience only after measuring need. Do not call a sequence of download clicks a successfully saved transaction. Browser cannot observe ordinary download completion or enforce native create-only/owner-only permissions. A partially completed export journey must remain retryable without falsely reporting that no external copies exist.

- **Lifecycle / severity / claim type:** open / high / workflow gap and delivery-truth preservation obligation.
- **Principles / domains:** Sidq, Promise-keeping, Amanah / design, technical, operational.
- **Stakeholders / controlled decision:** operators producing several files / selection, bundle review, export states and retry guidance.
- **Evidence / validation level:** B10, B13, B27, C14–C17, O16–O17, W01–W02, W17 / current review and recorded draft-preservation interaction plus dated official interface constraints; no download-completion or new native-save experiment.
- **IVSD-M006:** retain delivered filenames, masked review and declared consequences; add deliberate subset/relationship review and separate prepared, revealed, copied, download-requested and any genuinely confirmed file-stream completion states. Preserve fatal-failure bundle clearing and documented artifact-level draft withholding; show both the usable subset and withheld reasons. Keep exports deliberate and never label a partial draft subset a complete configuration.
- **Owner / next validation:** Browser export and shared composition owners / multiple outputs, subset selection, aggregate failure, clipboard denial, download blocking, repeated actions and invalidation during delayed JS interop.
- **Escalation boundary:** no Browser wording may imply native filesystem protection, whole-bundle rollback, guaranteed download completion or target deployment.

### IVSD-F007 — Recovery is needed, but persistence must be an explicit new contract

Transient answers reduce retained exposure, but a refresh, crash or deliberate departure can also destroy substantial work. Shared drafts now survive section unmounting, experience changes and search/filter/navigation; that is in-session retention, not crash recovery. Current navigation guards and generation fences do not provide resumable-session recovery. CLI input files and reversible artifact imports are not a shared session format either. It would be misleading to describe this as a Browser-only defect or solve it by silently persisting private state.

The first candidate is explicit export/import of a portable workspace under a separate privacy design. It should bind definition identity, answer revision, format version and provenance, while reopening with consent decisions re-evaluated rather than blindly restored. An optional nonsecret checkpoint may help some users; it must not mislabel sensitive nonsecret answers as public. In-memory undo also retains earlier secrets and therefore needs bounded lifetime and clear/revoke semantics.

- **Lifecycle / severity / claim type:** open / high / recovery opportunity with new custody risk.
- **Principles / domains:** Amanah, Non-harm, Rights of people, Avoiding spying / design, technical, governance.
- **Stakeholders / controlled decision:** long-form, shared-device and privacy-sensitive users / checkpoint contents, persistence defaults, key custody and recovery claims.
- **Evidence / validation level:** B14, B17, C18, W06–W07, W15 / current-contract traceability and design reasoning.
- **IVSD-M007:** design explicit local checkpoint portability before considering browser autosave. Define sensitivity, optional encryption, version binding, secret consent, unsupported fields, stale revisions and clear/revocation behavior. Explain that deleting a workspace cannot erase exported copies.
- **Owner / next validation:** product, security/privacy and shared workspace owners / round-trip all typed values, conflicting definitions, tampering, wrong keys where applicable, lost-key behavior, stale imports and no automatic network/persistence.
- **Escalation boundary:** automatic browser persistence, cloud sync or encrypted-draft claims require renewed authorization and threat-model review; this report does not authorize them.

### IVSD-F008 — Offline use needs reconnect, retry and cache-management recovery

The existing offline design is substantive: complete app readiness is checked before custody, public definitions are explicitly retained and re-admitted, and updates wait rather than replacing an active workspace. Two source paths nevertheless make recovery fragile. `BrowserOfflinePolicy` stores its initialization task and captures online state once; `CanFetchDefinition` continues using that state. Booting offline and later reconnecting can leave a new-source request rejected. Both C# and JavaScript also memoize a failed initialization result, so retrying after asset installation recovers may still require a reload.

This readiness gate also blocks online editing when service-worker registration, required preparation or custody acknowledgement is unavailable. There is no current online-only editor fallback. The user needs to hear “configuration workspace unavailable because readiness failed,” rather than merely “offline support unavailable.” Do not weaken the custody gate silently to make a blocked environment appear supported.

The retained-definition list has no management action to delete/replace entries, despite an eight-entry bound. Users need an inventory explaining what is cached, which app version is active, whether an update is waiting, and how to recover safely. This should not allow network access during answer custody or silently evict a definition the person expected to keep.

- **Lifecycle / severity / claim type:** open / high / source-derived recovery defects and management gap.
- **Principles / domains:** Promise-keeping, Amanah, Adl, Ihsan / design, technical, operational, evaluation.
- **Stakeholders / controlled decision:** offline, intermittent-network and low-bandwidth users / readiness state, retry, public retention and update UI.
- **Evidence / validation level:** B15, B20; parent-reviewed `BrowserOfflinePolicy:20–41`, `BrowserDefinitionSource.FetchAsync`, `pwa-lifecycle.js:148–157`; W06–W09 / implementation traceability, not a fresh browser reproduction.
- **IVSD-M008:** refresh connectivity/readiness through a generation-safe pre-custody operation, allow explicit retry after failed initialization, and add bounded retained-definition management. Preserve active-workspace network exclusion and coherent waiting updates.
- **Owner / next validation:** Browser lifecycle/offline owners / offline boot→reconnect→new public source; failed installation→repair→retry; unavailable service workers and denied registration/cache preparation while online; eight retained definitions→remove one→retain another; eviction, corruption, two tabs and delayed old callbacks. Verify blocked editing is explained accurately and no unsafe fallback opens private custody.
- **Escalation boundary:** recovery must not use forced activation during live answers, cache private artifacts, or promise permanent offline availability.

### IVSD-F009 — Bring public definition authoring into an optional maintainer workspace

Browser can compile a local authoring document before answer collection, but it has no authoring editor, schema-scaffolding interface, structural repair workflow or canonical build/revision workspace. These are credible Browser capabilities: they process bounded public data using the same engine. Requiring a terminal for all definition creation unnecessarily excludes people who can understand the domain but prefer a graphical workflow.

The design should start with open/inspect/validate/export, then add structured editing and supported scaffolds. Keep public authoring separate from private answer collection. An environment example must not be treated as safe merely because of its filename; existing CLI scaffolding copies keys, not secret values. A JSON Schema scaffold must state which semantics were preserved, approximated or rejected. Browser must not imply arbitrary JSON Schema support or execute definition-supplied code.

- **Lifecycle / severity / claim type:** open / medium / missing maintainer-facing capability.
- **Principles / domains:** Adl, Ihsan, Sidq, Rights of people / strategic, design, technical, governance.
- **Stakeholders / controlled decision:** definition contributors and application maintainers / authoring workspace, supported import vocabulary and public/private separation.
- **Evidence / validation level:** B02, B17, C01–C02, R03–R05 / capability inventory and design reasoning.
- **IVSD-M009:** expose shared authoring compilation/check/build through a separate advanced workspace; add precise safe diagnostics and reviewable public exports. Reuse bounded schema/environment scaffolding with explicit semantic-difference acknowledgments and public-content review.
- **Owner / next validation:** authoring/Core and Browser owners / independent authoring-to-runtime vectors, unsupported schema constructs, local-reference confinement, secret defaults, malicious text, stable identities and no answer-bearing publication.
- **Escalation boundary:** remote schema references, executable plugins, repository credentials or automatic contribution submission are separate trust changes.

### IVSD-F010 — Revision and fixture tooling must distinguish local validity from target conformance

A maintainer can publish a definition that passes a schema yet generates a configuration the target rejects. Revision changes can also alter defaults, sensitivity, activation, requiredness or output mappings without an obvious textual signal. The strategy correctly calls for semantic definition review and separate conformance labels, but those complete installed workflows are not currently available merely because CLI has `revise` and the repository has tests.

Browser should eventually offer a bounded synthetic fixture runner and candidate revision review. Results should identify what was checked: definition admission, shared semantic validation, deterministic rendering, inverse import where supported, and any separately supplied target-test evidence. No generic green badge should collapse these into authenticated publisher trust or deployed success. Public-source filesystem custody improvements remain a separate native authoring obligation; a graphical editor does not solve them.

- **Lifecycle / severity / claim type:** open / high / maintainer assurance and trust-label gap.
- **Principles / domains:** Sidq, Amanah, Promise-keeping, Rights of people / technical, governance, evaluation.
- **Stakeholders / controlled decision:** maintainers, operators and target applications / revision review, fixture format and conformance claims.
- **Evidence / validation level:** C01, C12, R03–R06 / current partial tooling and strategy; target acceptance not established.
- **IVSD-M010:** add semantic change summaries and synthetic expected-output fixtures with explicit coverage; retain identity/history invariants. Separate schema-valid, render-tested, publisher-authenticated, curated and target-tested claims, each with evidence and date.
- **Owner / next validation:** shared authoring/test tooling and definition governance owners / fixtures with independently specified outputs, deliberate sensitivity/requiredness/default changes, stale revision references and rejected arbitrary commands/network actions.
- **Escalation boundary:** executing target applications or arbitrary user test scripts in Browser is not part of this proposal; promoted signing/curation requires independent authority.

### IVSD-F011 — Complete guidance and qualify the delivered large-definition navigation

Browser now supports authored experiences/sections, bounded instructions and deliberate public help links, exact correction navigation, masked review and local metadata search. Search includes titles, keys, concepts, guidance and declared output bindings, never answer values or artifact bodies. All query words must match; selections combine OR within a facet and AND across facets, counts ignore only their own category, and query/filter resets are independent. Inactive results route to an active controlling question rather than activating a feature or granting consent. These are delivered capabilities, not future search proposals.

Required-answer progress counts active obligations, including container/cardinality work separately from descendants, and distinguishes missing, invalid and secret-deferred states. No-required-answer definitions do not invent a completion percentage; invalid optional work remains a blocker. Search, filters and disclosure preserve answers, unfinished input, consent and prepared output.

The field renderer still lacks an explicit permitted-public-recommendation action and automatic declared format/constraint explanations. Authored guidance helps but cannot substitute for precise shared rule information. Independent findability tasks, comprehension studies and thousand-field performance measurements are also absent; actual search/focus observations do not prove the intended reduction in user effort. Mechanical humanization of keys cannot supply missing domain meaning.

- **Lifecycle / severity / claim type:** open / medium / usability and metadata-consumption gap.
- **Principles / domains:** Ihsan, Adl, Amanah / design, technical, evaluation.
- **Stakeholders / controlled decision:** novices and operators of large definitions / guidance, search, recommendation application and completion explanations.
- **Evidence / validation level:** B24–B29, C10, O16–O17, R04, R07 / current search/disclosure/progress implementation, recorded behavioral regressions and actual focus/draft-preservation observations; task-completion benefit and maximum-workload performance unmeasured.
- **IVSD-M011:** retain delivered metadata search, honest obligation progress, guided/free navigation and inert authored help; finish explicit recommendations and automatic format/constraint explanations. Improve maintainer guidance and evaluate the delivered interaction before adding further navigation modes.
- **Owner / next validation:** Browser UX and definition-authoring owners / independent twenty-query findability tasks, changing conditions/hidden errors, measured large forms and recommendation application without overwriting unrelated edits or private fields.
- **Escalation boundary:** do not infer unsafe recommendations from external services, translate canonical identifiers, or silently change pinned definition semantics.

### IVSD-F012 — Qualify accessibility on the actual current interface

The repository contains meaningful landmarks, labels, correction associations, focus requests, themes and responsive styles. October 20 real Chromium-backed observations cover desktop/mobile-width light/dark rendering, actual Change/Return and exact correction focus, 44 CSS-pixel facet-label targets and a limited narrow/doubled-text probe. Enter on the denied-consent import path submitted nothing, enabled no consent and prepared no output. These improve the evidence beyond mocked focus calls, but do not establish a complete physical keyboard journey, screen-reader announcements, forced colors, RTL, measured contrast, exact 320-pixel reflow or native zoom conformance.

The provider should make the accessibility contract above a release gate for claimed support. Disabled participants should be able to complete real synthetic tasks, not merely identify controls in isolation. Include asynchronous failure, secret-consent recovery, dynamic row changes, hidden errors and export alternatives. Preserve valid transient answers during correction so accessibility improvements do not introduce repeated work or private persistence.

- **Lifecycle / severity / claim type:** open / high / assurance gap with inclusion consequences.
- **Principles / domains:** Adl, Rights of people, Ihsan, Promise-keeping / design, operational, evaluation.
- **Stakeholders / controlled decision:** keyboard, screen-reader, low-vision and cognitive-accessibility users / interaction design and support claims.
- **Evidence / validation level:** B16, B22, B26–B29, O03–O07, O16–O18, S01–S05 / current semantic regressions and bounded recorded rendering/focus observations; comprehensive current AT and platform evidence absent. Native Desktop was AX-inspected only because Wayland pixel capture was unavailable.
- **IVSD-M012:** execute the declared WCAG/task acceptance matrix against exact package identities; combine automated checks, native keyboard/zoom review, assistive technology and participant feedback. Record exceptions and remediation owners.
- **Owner / next validation:** accessibility and Browser release owners / current-package end-to-end tasks on declared pairings, including focus obscuration, target sizes, announcements and reflow.
- **Escalation boundary:** do not claim WCAG conformance from a component suite, a theme name or this report; legal accessibility obligations require qualified interpretation where applicable.

### IVSD-F013 — Treat language support as more than RTL styling

Browser has direction preferences and logical styling, but the shell and diagnostics remain English and the document language is fixed to English. Launch locale metadata is not a translation system. Arabic users may benefit from RTL layout while still facing untranslated guidance and mixed-direction technical strings. Incorrect visual grouping of a URL, key or digest can also cause practical mistakes even when bytes are unchanged.

Define supported UI languages independently of definition locales and text direction. Separate translated application strings from publisher-authored prose and immutable identifiers. Use native-language reviewers for terminology, numerical input, reading order and screen-reader behavior. A partial translation needs an honest fallback rather than a claim that all definition content is localized.

- **Lifecycle / severity / claim type:** open / medium / missing localization and assurance capability.
- **Principles / domains:** Adl, Ihsan, Sidq / design, governance, evaluation.
- **Stakeholders / controlled decision:** Arabic/RTL and other language communities / language resources, metadata, fallback and exact technical-token handling.
- **Evidence / validation level:** B16, C20, O06, S02–S03 / implementation and authored-test inspection; multilingual stakeholder evidence absent.
- **IVSD-M013:** introduce reviewed shell/diagnostic resources and explicit language selection/fallback; qualify bidi isolation and exact copied/serialized bytes. Give definition authors a clear locale-content contract rather than automatically translating keys.
- **Owner / next validation:** localization, Browser and definition-schema owners / mixed Arabic/Latin identifiers, numbers, filenames and URLs; native-language task completion and assistive technology.
- **Escalation boundary:** expanding the definition locale schema is a shared contract change; machine values and canonical identities must remain stable.

### IVSD-F014 — Preserve privacy while communicating the limits of hosted execution

Existing controls exclude answers from application network requests, persistent caches and worker messages, require secret consent, fence stale exports and clear owned buffers. These are valuable implemented protections. They cannot establish that a compromised origin, extension or device cannot observe private data, nor that exported clipboard/download copies can be revoked. Sensitive nonsecret configuration can also be confidential even when secret entry remains disabled.

Consented credential entry adds a specific boundary worth preserving: denied secret leaves emit no answer-bearing input, while consented password kinds use stable family/path/row identities and honest new/current-password hints. Issuer tokens are not presented as generated passwords. A callback captures the rendered form/path/input revision and rejects invalidation before shared editing, including revocation followed by reconsent. Held-production-callback regressions and actual synthetic fill/revocation observations cover this failure mode. Browser hints do not authorize a vault API, establish extension compatibility, or erase a password manager's independent copies [B28, O16–O17].

The provider controls shipped assets, hosting guidance, permissions, disclosure language and support practices. Use a dedicated origin, avoid injected analytics/widgets, keep the export decision explicit, and explain the safer local-client option for high-value secrets without pretending native clients are universally safe or fully qualified on every OS. Do not turn “local processing” into “no information ever leaves the device”: public retrieval and infrastructure logs have their own metadata exposure.

- **Lifecycle / severity / claim type:** open / high / preservation and hosting-assurance obligation; no newly demonstrated answer leak.
- **Principles / domains:** Amanah, Rights of people, Non-harm, Haya, Avoiding spying / technical, design, operational, governance.
- **Stakeholders / controlled decision:** operators, represented individuals and shared-device users / origin trust, sink exclusions, export/reveal and support collection.
- **Evidence / validation level:** B12–B15, B25, B27–B29, O08–O12, O16–O17, W03–W05, W10–W15, S06–S09, `SECURITY.md` public Browser invariants / implementation, held-callback regressions, bounded current canary observations, historical synthetic checks and dated platform constraints. The newer probe is not a complete request-body/worker/production-origin audit.
- **IVSD-M014:** retain the current transient/private boundary; inventory every allowed storage/network sink; validate the actual deployed headers/assets; keep value-safe diagnostics and explicit export warnings. Review any new diff/history/worker/checkpoint feature against the same custody model.
- **Owner / next validation:** security/privacy, hosting and Browser owners / synthetic canaries across DOM reveal, requests, caches, preferences, worker messages, late callbacks, revocation, clipboard denial and navigation.
- **Escalation boundary:** accounts, telemetry, cloud answers, persistent drafts, target authentication or native handoff carrying private data require a new trust/custody review.

### IVSD-F015 — Make heavy work responsive without replacing bounded data with execution

Input and output budgets already protect important boundaries, and lifecycle cancellation invalidates stale operations. Selected-section mounting, cached shared projections and metadata-only discovery avoid some unnecessary work, but no measured latency or memory evidence establishes maximum-workload responsiveness. Required-answer progress is not execution progress: Browser still lacks a dedicated long-operation progress/Cancel workflow. An apparently frozen page can lead users to repeat actions, reload and lose work.

Measure first. Public-definition parsing/compilation and answer-bearing validation/preparation have different custody implications. A supported worker may be appropriate for demonstrated public-data computation, but private validation/preparation must first be improved within its existing execution boundary. Moving answers or restricted artifacts into worker messages—or another newly introduced private execution surface—requires an explicit boundary decision under IVSD-M014. The current launch contract and Browser trust ADR exclude private worker messages. A worker is neither permission to execute definition-supplied code nor a complete resource quota. Progress should describe meaningful phases, not invented percentages, and cancellation must distinguish work stopped from exports already requested.

- **Lifecycle / severity / claim type:** open / medium / performance and interaction assurance gap.
- **Principles / domains:** Ihsan, Non-harm, Adl / technical, design, evaluation.
- **Stakeholders / controlled decision:** mobile and low-resource users / work scheduling, budgets, cancellation and progress.
- **Evidence / validation level:** B10, B14, B22, W16 / existing bounds/lifecycle traceability; no benchmark or physical-device measurement performed here.
- **IVSD-M015:** define representative maximum-size workloads and responsiveness budgets; add accessible phase/cancel feedback. Consider supported workers only for measured public-data paths within current boundaries. Keep answer-bearing work inside the existing custody boundary; any private worker transfer or new private execution surface requires an explicit security/product decision, revised sink/lifetime contract and adversarial verification before implementation, coordinated with IVSD-M014.
- **Owner / next validation:** performance and Browser owners / long lists, nested edits, maximal bounded documents, bundle limits, repeated cancellation and replacement; assert responsiveness and stale-result rejection without timing-luck tests.
- **Escalation boundary:** no private worker messages without the explicit custody decision above; no arbitrary plugins, remote execution, unbounded background work or performance telemetry carrying answer content.

### IVSD-F016 — Browser support needs its own exact-release evidence matrix

Recorded Chromium/Linux package checks are valuable, including local authoring, exports, cache inspection and waiting-worker behavior. The October 20 local Chromium-backed rendering/focus/credential/import observations add current implementation evidence, but are not an exact-release published-PWA matrix. Neither set qualifies Firefox, Safari/WebKit, password-manager extensions, physical mobile devices, assistive technologies or current native zoom behavior. The configured cross-platform CI matrix is for CLI; it does not launch Browser or establish production hosting behavior. Package admission, package production and actual-host qualification are different evidence levels.

Publish a Browser matrix with exact package digest, browser/version, OS/device, AT where applicable, date, scenarios and limitations. Keep component tests, published-package browser automation, participant sessions and production-header checks separate. Avoid test-count-as-coverage claims. If a feature relies on a nonportable API, its fallback path is part of the matrix rather than a footnote.

Qualify prerequisites as well as conveniences: service-worker/cache readiness is required for online workspace entry too. Registration denial or missing support currently blocks editing instead of selecting a reduced online-only mode. Test that actual behavior in intended private/managed/embedded contexts and report unsupported or blocked states explicitly; do not infer them solely from an engine name [B15, B20].

- **Lifecycle / severity / claim type:** open / high / support-claim and release-evidence gap.
- **Principles / domains:** Sidq, Promise-keeping, Ihsan / operational, governance, evaluation.
- **Stakeholders / controlled decision:** all Browser users and downstream hosts / supported combinations, evidence retention and release gates.
- **Evidence / validation level:** B22, B28, O01–O02, O13, O16–O18, C22 / historical published-package records, current recorded local surface/owner checks and configured CI; no complete exact-release engine/extension/AT matrix.
- **IVSD-M016:** create scoped published-build lanes for supported engines and actual-platform checks where needed; bind results to exact assets and known limitations. Keep unsupported/unverified states visible.
- **Owner / next validation:** release/QA owners / Chromium and Firefox on declared desktop hosts, Safari/WebKit on actual Apple platforms before support claims, physical mobile tasks, AT pairings and denied/unavailable API fallbacks.
- **Escalation boundary:** an absent host or AT environment remains an evidence gap; emulation, cross-compilation or another client's green suite cannot be substituted as proof.

### IVSD-F017 — Hosting, update recovery and security support need operational ownership

The public hosting guide covers useful deployment mechanics, while the security policy currently promises acknowledgement within an “undefined amount of business days.” That is not an actionable response commitment. A blanket exclusion for missing CSP headers should also not discourage a report that demonstrates concrete exploitable impact in an actual deployment. These are governance and communication defects, not proof that the product is compromised.

Assign responsibility for domain/TLS renewal, atomic deployment, dependency updates, waiting-worker recovery, rollback and incident communication. A static application still has an operational supply chain. Checksums and hashed asset manifests identify bytes relative to a trusted reference; they do not authenticate a publisher if an attacker can replace both content and reference. Independent signing authority remains deliberately inactive and must not be implied by UI badges.

- **Lifecycle / severity / claim type:** open / high / operational readiness and promise gap.
- **Principles / domains:** Amanah, Sidq, Promise-keeping, Rights of people / operational, governance, evaluation.
- **Stakeholders / controlled decision:** hosted users, self-hosters and vulnerability reporters / deployment procedures, response commitments, logs and publication claims.
- **Evidence / validation level:** O09–O14, W08–W10, S09; parent-reviewed `SECURITY.md:17–24,71–80` / policy inspection and platform facts; no production deployment inspection.
- **IVSD-M017:** replace undefined response wording with a realistic owned target; distinguish missing-hardening advice from demonstrated impact; document safe incident reporting, log minimization, update rollback and compromised-worker retirement/cache recovery.
- **Owner / next validation:** project security contact, hosting and release owners / tabletop and controlled synthetic deployment/recovery exercise; verify effective headers and no real-answer collection in support.
- **Escalation boundary:** legal commitments, incident notifications and publication authority need the appropriate accountable human owner; this audit cannot grant them.

### IVSD-F018 — Keep discovery neutral, provenance honest and old evidence visibly historical

Store search, explicit revision review and local-source independence already support a useful neutral discovery model. Curation metadata, a digest, a publisher string and a successful render test answer different questions. Combining them into one “verified” label would transfer unjustified trust to users. Contributor rights, license attribution, correction procedures and any future sponsored ranking therefore belong to feature completeness as provider responsibilities.

There is also concrete documentation drift: older backlogs and the universal-MVP review describe a public-choice-only Browser and older native target matrices. Those statements were meaningful at their own revisions but are no longer current capability policy. Future work must not reintroduce those limitations or treat historical test counts as current qualification. The new report should coexist with those records through explicit scope/date relationships, not erase them.

The current documentation also contains narrower stale descriptions: `BROWSER_UI.md` says inactive panels remain mounted, whereas the current editor mounts only the selected section over retained shared drafts. The Browser guide's opening Use paragraph places import below preparation, whereas its newer detailed section and the editor place eligible import before manual questions. This consultation records those discrepancies without silently changing other owners' documents. Reconcile their current descriptions; retain historical reports as dated evidence [B26–B27, R02].

- **Lifecycle / severity / claim type:** open / medium / provenance, fairness and knowledge-governance gap.
- **Principles / domains:** Sidq, Adl, Rights of people, Promise-keeping / strategic, governance, operational, evaluation.
- **Stakeholders / controlled decision:** publishers, contributors, operators and rightsholders / ranking, trust labels, attribution, correction and evidence freshness.
- **Evidence / validation level:** B04, B26–B27, O12, O16–O18, R02–R08 / implemented Store behavior, current source/document comparison, recorded execution and historical strategy/reviews; no live moderation history or contributor study.
- **IVSD-M018:** keep local admission independent; separate identity, curation, authentication and target-test evidence; define correction/appeal ownership and preserve license provenance. Mark obsolete capability guidance as historical or update its current successor without rewriting old review conclusions.
- **Owner / next validation:** definition governance, documentation and Store owners / misleading labels, stale revisions, malformed metadata, unavailable Store, disputed entries and unbiased ordering; review contributor-facing explanations with actual maintainers.
- **Escalation boundary:** sponsored ranking, paid access to safety features, authenticated publisher promotion or disputed redistribution rights require separate governance and any relevant legal/scholarly review.

## Recommendations

### Priority order

Do not begin with an all-purpose Browser IDE or a cloud account system. Preserve the delivered guided workspace and fidelity fixes; address the remaining source-traced offline recovery and operator gaps while qualifying accessibility and platform behavior. These priorities are advisory; private checkpoints and private worker transfers remain separate custody decisions, not implementation permission conveyed by this table.

| Priority | Outcome | Included work | Dependency / completion gate |
| --- | --- | --- | --- |
| **P0 — correctness and honest claims** | Existing workflows recover predictably | Reproduce/fix offline reconnect and failed-readiness retry; preserve the delivered presence/empty-value regressions; correct unsupported completeness/translation/host claims and stale layout descriptions | IVSD-M008/M018; IVSD-M002 is delivered maintenance, not new feature work; behavioral regressions before remaining fixes |
| **P0 — assurance ownership** | Current users are not promised unverified guarantees | Assign accessibility/browser support, security-response and hosting owners; keep download/native-save and target-acceptance distinctions explicit | IVSD-M012/M014/M016/M017; no new feature needed to make claims accurate |
| **P1 — operator completion** | A person can finish configuration and understand errors | Rule-specific explanations and declared constraints/formats, explicit recommendations, read-only existing-file assessment, output-subset/relationship review and cache inventory/removal | IVSD-M003/M004/M006/M008/M011; retain delivered search, exact corrections, import eligibility/recovery and masked review rather than rebuilding them |
| **P2 — advanced operator workflows** | A person can compare changes and deliberately recover work | Local comparison/projection proof, explicit checkpoint design, bounded undo where justified, meaningful execution progress/cancel and optional bundled export | IVSD-M005/M007/M015; atomic conflict correction/re-import is delivered; new custody and stale-result contracts before extensions |
| **P3 — maintainer completion** | Public definitions can be created and tested in Browser | Separate inspect/edit/check/build workspace, bounded schema/environment scaffolds, revision review, synthetic fixtures and safe launch-link preparation | IVSD-M009/M010; shared authoring semantics and source-safe diagnostics; no publication credentials or arbitrary execution |
| **Continuous — trust and evidence** | Completeness remains true after changes | Cross-client conformance vectors, current browser/AT matrix, exact package evidence, hosting/update recovery, neutral Store governance and documentation freshness | IVSD-M001/M012–M018; claims cannot wait until an imagined final hardening phase |

These priorities are impact/dependency judgments, not delivery estimates. Staffing, actual supported browsers/languages and stakeholder results are missing inputs for scheduling. A future plan should split remaining independently releasable outcomes, map the seventeen open findings and preserve IVSD-M002's regressions, rather than treating all eighteen mitigations as untouched work.

### Minimum operator-complete Browser acceptance

A release may be called operator-complete only for its explicitly supported definition vocabulary and browser matrix when a person can:

1. Open a bundled, selected local or admitted public definition and identify its canonical revision/digest, source and unauthenticated publisher claim.
2. Understand whether network retrieval has finished and whether the application/definition is usable offline before supplying answers.
3. Enter every supported type, distinguish absent/empty/false, omit optional containers, edit lists and recover from mistakes without clearing unrelated values.
4. Understand requiredness, activation, declared constraints and recommendations; find a field and reach every error even when its section is hidden.
5. Import a supported configuration or receive an accurate explanation of why this projection cannot be inverted, with no partial mutation on failure.
6. Choose complete versus secret-deferred preparation, understand missing ordinary input versus deferred secrets, distinguish usable drafts from withheld unresolved artifacts, and avoid treating a draft subset as ready.
7. Review selected output metadata, sensitivity, coverage and declared consequences; clear prepared output on fatal validation or aggregate failure while preserving the separately documented nonfatal draft-withholding semantics.
8. Reveal/copy/download deliberately, understand which outcomes are observable, and recover from denied clipboard/download behavior.
9. Revoke consent, clear, replace or leave with stale work invalidated and no automatic private persistence or network transmission.
10. Complete the same journey with the claimed keyboard, assistive-technology, language and viewport support.

This is an acceptance contract, not a list of ten absent features. Presence, guided navigation/search, masked review, supported atomic import/recovery and bounded current focus checks now have implementation evidence. Rule explanations, selected outputs, offline recovery and the complete declared support matrix still prevent an unqualified completion label.

Checkpoint files and a maintainer editor are not prerequisites for this narrower operator-complete label, provided the absence of resumability is explicit and the supported workflow remains usable. They are important candidates for the broader workbench, not permission to postpone the basic correctness and accessibility fixes.

### Advanced operator and maintainer completion

**Advanced-operator-complete** adds explicit existing-file assessment, local comparison, projection proof, supported revision-bound request preparation, understandable conflict handling, deliberate recovery and useful large-form navigation. Atomic conflict rejection with exact value-safe reasons and an explicit correction/re-import path satisfies this conflict-handling gate; automatic merging or a merge editor is not required. It must explain fidelity and identity rather than merely offer more buttons.

Current search/facets, provenance review and conflict correction/re-import satisfy important parts of that profile. They do not substitute for the still-absent read-only assessment, comparison or projection-proof interactions. In-session draft retention is not a portable checkpoint; persistence remains a separate custody decision.

**Maintainer-complete** adds an end-to-end public-data workflow: create/open → inspect/edit → validate/repair → review semantic changes → run synthetic fixtures → export canonical candidate and supporting metadata → follow an explicit external contribution/target-validation process. A browser editor that only emits syntactically valid JSON is not sufficient. Conversely, authenticated repository submission, target deployment and arbitrary test execution are not required to complete this bounded maintainer workflow.

**Assurance-qualified** attaches current evidence to those claims: exact package identity, supported browser/OS/device combinations, accessibility tasks, privacy sink tests, offline/update recovery, and documented unresolved limits. Qualification is per claim and release, not a permanent label inherited from an earlier report.

### Secondary enhancements: useful, but not all mandatory

| Candidate | Value | Conditions before prioritizing |
| --- | --- | --- |
| Local secret generation | Avoid weak manually invented values where a definition legitimately requests generation | Reuse/review the existing Core generator; explicit action; cryptographic source and policy tests; no automatic reveal/copy/persistence; never imply compatibility with an unstated target policy |
| Evaluate the delivered guided flow | Authored Previous/Next section actions already exist alongside free navigation | Measure whether they improve novice completion; preserve exact validation and view/answer separation rather than adding a second wizard |
| In-memory undo | Recover accidental edits | Bound retained revisions; explain secret retention; clear/revoke must remove affected historical state; no silent persistence |
| Single-archive export | Reduce multi-download friction | Complete all selected preparation first; review filenames/collisions/path traversal, memory bounds and metadata leakage; archive delivery remains unconfirmed |
| Optional file-system picker | Better naming/location control and API-level completion | Exact feature detection, explicit permission/activation, standard download fallback, no protected-save claim [W01–W02, W18] |
| Multiple workspaces | Compare alternatives or configure several targets | Strong per-workspace identity/consent/generation isolation; avoid hidden background network and accidental cross-workspace export |
| Recent public definitions | Faster repeat use | Public-only bounded history with clear removal and source/digest identity; do not retain private filenames or answers by accident |
| Read-only conformance report export | Help maintainers review evidence | Synthetic/public projection, explicit tested scope and no restricted artifact/private-path dump |
| Safe native-client handoff | Move high-value secret work to a reviewed local client | Public definition identity/pointer only; native client independently admits and prompts; no answers in URLs or automatic apply |

Features such as arbitrary remote schemas, universal lossless import, live target testing, secret-manager connections, remote administration and cloud synchronization remain separate extensions. Their absence is not a reason to weaken bounded admission or target authority.

### Shared architecture and maintainability

Keep **Core** responsible for admitted definitions, validation, projections, import eligibility and deterministic domain results. Keep **Presentation** responsible for immutable form/workspace snapshots, typed edits, privacy policy and operation generations. Let **Browser** own rendering, focus, user gestures, file selection, public-cache controls and browser export outcomes. Native protected publication remains in **Artifacts** and its qualified host adapters. Authoring/compiler services should remain usable independently of any UI.

New workflows should consume typed shared results rather than parse CLI output, duplicate validator rules or invent a second definition AST in JavaScript. Diagnostic IDs/paths, import fidelity, selected-output semantics and comparison results are likely shared contracts; their Browser views are adapters. If a capability is only an API today, add the smallest complete user workflow and test it at its public seam before counting it as delivered.

Avoid a universal client abstraction that hides platform truth. A Browser export result and a native protected-publication receipt should share domain identity and artifact facts while retaining their different authority and observable outcomes. Explicit differences are more maintainable than a falsely uniform “save succeeded” interface.

### Rejected Alternatives and Trade-offs

1. **Exact interface parity everywhere:** rejected because standard streams, graphical controls, process signals and browser gestures are not interchangeable. Shared semantics and equivalent outcomes are the stronger requirement.
2. **CLI as the only complete client:** rejected because it unnecessarily withholds human-useful validation and authoring from Browser and overlooks capabilities Browser already leads.
3. **One large Browser IDE immediately:** rejected as the initial step because existing recovery and explanation problems affect ordinary users first. A separated maintainer workspace remains recommended later.
4. **Silent localStorage/IndexedDB autosave:** rejected under the current transient-answer contract. Recovery is valuable, but persistence changes custody and needs an explicit design decision.
5. **A backend proxy for validation or CORS workarounds:** rejected for the current product because it would acquire new server/network responsibilities and could compromise local-only expectations. Local-file fallback and admitted public-source rules already provide bounded alternatives.
6. **Universal inverse import:** rejected because transformed/conditional projections cannot be safely guessed. Explicit fidelity and supported mappings are preferable to confident corruption.
7. **Force-update every open tab:** rejected because coherency and private workspace continuity matter more than instantaneous version convergence.
8. **Add file-system picker APIs as native-save equivalence:** rejected; they can improve convenience, not establish owner-only/create-only protection.
9. **Telemetry as the default way to prove usability:** rejected; voluntary synthetic-task studies can provide valuable evidence without collecting configuration behavior by default.
10. **Claim completeness after more unit tests:** rejected because browser-engine behavior, assistive technology, hosting, stakeholder comprehension and target acceptance need different evidence.

The main trade-off is deliberate scope: a richer Browser becomes more useful but also gains more states, private-data copies and support obligations. Shared typed owners, progressive disclosure and small verified workflow increments control that cost better than either a permanently limited Browser or an all-at-once rewrite.

## Meaningful Verification Strategy

### Regression scenarios for the source-derived defects

These independent expected outcomes remain the regression/acceptance specification. Presence and empty-value fixes now have recorded shared/rendered regression evidence under IVSD-F002/O16; they are not unresolved defect proposals. The offline/retry/retention scenarios remain open. This table alone is not an execution log or a claim that every exact host/scenario combination was exercised.

| Scenario | Independent expected result | Appropriate seam |
| --- | --- | --- |
| Optional object with required child: supply, edit, then omit | Parent absent; unrelated answers preserved; preparation valid when the parent is optional | Browser control interaction plus shared snapshot/output assertion |
| Optional list with minimum one item: remove contents, then explicitly omit | `[]` remains distinct from absence; omission restores optional validity | Browser collection workflow |
| String-list item `"blue"` edited to `""` | Valid empty string where allowed; never a null array element; surviving row identity unchanged | Browser typed input and exact prepared projection |
| Import `{"setting":""}` for an optional unconstrained string with unconditional JSON copy output | Interactive snapshot retains the explicitly supplied empty string; output contains `"setting":""`, not an omitted member; matches direct CLI JSON input | Shared form/import seam plus Browser/TUI/Desktop preparation adapters |
| Boot offline, reconnect, open a new allowed public source before answers | Fresh connectivity/readiness assessment permits the bounded request; no answer-bearing request | Browser lifecycle with controlled connectivity transition |
| App readiness initially fails, resources recover, user retries | New attempt can succeed without losing public source selection; stale failed attempt cannot overwrite new readiness | C#/JS lifecycle and published-worker integration |
| Service workers unavailable or required registration/cache preparation denied while online | Editing remains blocked with an accurate actionable readiness explanation; no private workspace or silent online-only fallback | Actual browser readiness/custody entry and disabled-control behavior |
| Retention reaches eight definitions, user removes one and retains another | Only selected public entry removed; new entry admitted/digest checked; active workspace unaffected | Public-cache management and worker/storage integration |

### Shared conformance vectors

Use independently specified synthetic definitions and expected outputs across clients for all admitted types, nested containers, defaults, activation, sensitivity inheritance, missing values, duplicate rows, supported/unsupported inverse mappings, secret-deferred drafts and output formats. Include maximum permitted budgets, hostile text and stale-operation races. Expected values should come from the declared contract or an independent fixture specification, not the same helper that computes the implementation result.

Include explicit top-level empty strings before and after import, not only nonempty scalar examples. For draft bundles, independently specify one unresolved secret-dependent artifact alongside a usable unrelated artifact, then a fatal ordinary-input or budget failure: the first case retains only the usable draft with withholding diagnostics, while the fatal case exposes no prepared bundle.

For parity, verify both the shared result and reachability through the client. A Core unit test does not prove that a Browser button exists or that a TUI user can supply an empty container. Conversely, a screenshot or source-text assertion does not prove correct output bytes. Keep domain invariants in fast tests; exercise critical interaction and browser-boundary behavior through the actual public seams.

### Browser integration and published-package tests

- Exercise real input events, validation correction, focus and accessible status behavior, not only mocked JS invocation counts.
- Use exact asynchronous barriers and bounded deadlines for delayed imports, prepare/export invalidation, replacement and disposal. Do not depend on sleeps or repeated polling luck.
- Scan synthetic canaries across requests, URL/referrer handling, storage, public caches, worker messages, diagnostics and preview lifecycle. Test both positive intended export and negative unintended exposure.
- Test unavailable/denied clipboard, download dispatch, ordinary filename changes where observable, and optional picker cancellation separately. Do not strip unexpected terminal/markup/output noise merely to make assertions pass.
- Test actual published PWA installation, complete offline startup, retained-definition reopening, quota/eviction, interrupted update, old/new tabs, waiting activation and recovery. Development-server behavior is insufficient [W09].
- Qualify actual native zoom and physical mobile/AT combinations where claimed. Viewport/CSS emulation remains useful but narrower evidence.

### What not to add as “quality tests”

Do not pad the suite with assertions that merely pin prose, CSS source strings, framework internals, mock call counts or the implementation's own computed expected values. Do not equate more test cases with measured coverage. Preserve existing valuable privacy/concurrency tests and add a regression for each newly demonstrated defect. A test must defend a user outcome, domain invariant or actual platform boundary.

### Evaluation without default telemetry

Run consent-based synthetic-task studies comparing the current workflow with the improved workflow and, where useful, a manual documentation baseline. Measure task completion, correctness, time to first valid preparation, number of unresolved corrections, unnecessary re-entry, understanding of draft/export/target states, and accessibility obstacles. Include less-experienced operators, maintainers, keyboard/screen-reader users and native-language RTL participants [S05, O15, R04].

Record participant/task scope and minimized findings. Do not collect real answers, restricted outputs, session replay or background consumer analytics. Downloads and page views do not establish successful configuration or hours saved. Small qualitative studies can reveal important failures but should not be generalized into universal usability claims.

## Common Overlooked Failures and Outcomes

| Overlooked failure | Practical harm | Required response |
| --- | --- | --- |
| Empty container and absent container treated as interchangeable | Valid optional configuration becomes blocked, or intentionally supplied empty data disappears | Explicit presence controls and independent output assertions |
| A recommendation silently replaces a user edit | Configuration changes without informed intent | Deliberate action and visible provenance |
| A hidden advanced field is invalid | User cannot discover why preparation is blocked | Correction navigation must reveal and focus the relevant control |
| Import “success” omits required answers | Operator mistakes prefill for complete readiness | Separate extraction coverage and full validation |
| A diff stays visible after answers or baseline change | User approves stale evidence | Generation-bound comparison invalidation |
| A download click is reported as a saved file | User proceeds with missing or wrong files | Truthful dispatch state and retry guidance |
| Clear/revoke is described as erasing all copies | Clipboard history, downloads or external copies remain | State exactly which application-owned references/buffers were cleared |
| Encrypted autosave stores an automatically usable key beside ciphertext | Users infer an unlock boundary that does not exist | Explicit threat model and separate persistence authorization |
| Public retention accepts a document containing real private literals | Cache becomes a sensitive-data store | Public-content confirmation, authoring checks and review; labels alone are insufficient |
| Reconnection does not refresh cached offline state | Application remains unusable until reload | Explicit safe readiness retry before custody |
| Update activates into an existing private workspace | Old page/new runtime assumptions collide or data is lost | Coherent waiting updates and generation/version checks |
| A worker is treated as a sandbox for plugins | Network/storage access and resource pressure remain | Keep definitions data-only and work bounded |
| A digest badge is interpreted as authenticated publisher trust | User relies on unearned endorsement | Separate content identity, curation and authentication |
| Store failure blocks local configuration | Optional discovery becomes an unintended central dependency | Preserve independently usable local admission |
| RTL layout is advertised as Arabic support | Users receive untranslated or misordered technical content | Separate language/direction claims and native-language review |
| Old accessibility screenshots qualify a new interface | Current controls may be inaccessible | Evidence tied to exact package and scenario |
| Native CI is treated as Browser coverage | Browser API and assistive-technology failures escape review | Separate browser-engine and published-PWA evidence |
| Support requests include real configuration files | Incident handling itself causes disclosure | Synthetic reproductions and value-safe support projections |
| “Offline” is inferred from stopping one local server | Other network paths remain possible | Describe the actual isolation method and its limits |
| Target acceptance is inferred from deterministic rendering | Invalid or unauthorized deployment follows | Preserve target-owned validation and authorization |

## Validation Gaps

- This document refresh runs documentation checks only. It incorporates, without rerunning, the completed implementation's recorded Core 145/145, Presentation 142/142, Browser 176/176, targeted native checks and Release build. Test counts establish those executed suites, not exhaustive feature coverage.
- Current recorded desktop/mobile-width light/dark, actual focus, credential/import and narrow-text observations are bounded local runtime evidence. Screen-reader, complete keyboard-process, native zoom, exact 320 CSS-pixel/400% reflow, forced-color, full contrast, physical mobile and multilingual task qualification remain incomplete.
- Neither the newer Chromium-backed observations nor historical Chromium/Linux package QA establish Firefox, Safari/WebKit, embedded-webview, managed-profile or password-manager extension compatibility. Exact browser/version/package identities for the new observations are not a complete release matrix.
- Source-traced offline reconnect, failed-readiness retry and retained-definition management remain unreproduced/unqualified in this refresh. The corrected presence/empty-value family should not be grouped with those unresolved defects.
- No production origin, effective response headers, infrastructure logs, domain/TLS operations or real incident-response exercise was inspected.
- No real operator/maintainer interviews, accessibility participant sessions, support-ticket corpus or measured usability/latency study was available.
- No universal target-application acceptance or deployment outcome was established. Generic rendering and imported-field checks remain narrower.
- No authenticated publisher promotion or signed release authority was established; existing integrity controls must be described within that limit.
- Native client comparisons now include recorded targeted Desktop/Terminal tests and actual Linux smoke observations, not full host qualification. Desktop observation was AX-only because Wayland pixel capture was unavailable. No native output was saved/applied; macOS/Windows protected writers remain unavailable, and CLI RID admission does not resolve that.

## Escalation Needed

No new user decision is required to refresh this diagnosis. The user requested broad Browser capability and the original parity assessment, then explicitly requested this update after the guided implementation. That work's completion does not authorize the remaining extensions or certify the broader completeness profiles.

Future implementation decisions that need a self-contained product/security brief include private checkpoint contents and encryption, private worker transfers or other new private execution surfaces, automatic persistence, cloud synchronization, target/secret-manager authentication, answer-bearing native handoff, remote schema references, executable extensions, authenticated publisher promotion and sponsored discovery. Name the person affected, data/authority gained, safer alternative, evidence required and residual risk before deciding.

Qualified legal review is appropriate for concrete licensing, privacy-law, accessibility-law or incident-notification questions. Qualified Sunni scholarly review is appropriate if future monetization, content, financial or other religious-legal questions arise. This audit makes no independent halal/haram or compliance determination.

## Evidence Reviewed

### Evidence classification

- **Implementation traceability:** inspected source, call relationships and authored tests at the reviewed commit. This is stronger than a strategy statement but weaker than a fresh executed scenario.
- **Recorded execution:** the completed October 20 implementation record and older reports/operations records, each retaining its own revision, host, date and limitations. The newer counts/observations are current implementation evidence, not rerun results for this document edit or a universal package-support matrix.
- **Official platform/standards evidence:** documented browser/runtime behavior and normative requirements, accessed on the stated date. Living pages may change.
- **Design reasoning:** recommendations derived from those facts and I-VSD principles; not implemented behavior or stakeholder validation.
- **Missing evidence:** explicitly unreviewed or unavailable operational/stakeholder/host facts. Absence of evidence is not automatically a discovered vulnerability.

### Repository evidence register

All relative links below resolve from this consultation. Current source symbols refer to `65dde2668878db0e60e12d0051086643c8759520`; future edits can move them. B18–B19/B23's original defect observations and numeric ranges belong to `29092c7bc0f5c00beda76bc99c3a206f1ff66aee` and are historical where replaced by B25–B26.

| IDs | Sources and inspected scope |
| --- | --- |
| **R01** | [AGENTS.md](../../AGENTS.md), [quick reference](../../docs/internal/QUICK_REFERENCE.md), [project map](../../PROJECTS.md), [security policy](../../SECURITY.md): independent runtime, data-only definitions, transient Browser custody, target authority, protected output and evidence rules |
| **R02** | [Browser guide](../../docs/public/browser.md), [launch contract](../../docs/internal/LAUNCH_CONTRACT.md), [Browser trust/export ADR](../../docs/internal/adr/ADR-browser-session-trust-and-export.md), [Core definition contract](../../docs/internal/CORE_DEFINITION_CONTRACT.md): current supported workflow, worker custody and semantic boundaries |
| **R03** | [Definition Store technical contract](../../docs/internal/DEFINITION_STORE.md), [public Store guide](../../docs/public/definition-store.md): optional discovery, canonical metadata, history, licensing and contribution boundaries |
| **R04** | User-owned strategy, now located under `docs/internal/`: [universal configurator](../../docs/internal/universal-configurator.md), especially separate contracts, import fidelity, authoring, semantic diff and conformance sections; [go-to-market](../../docs/internal/go-to-market.md), especially forms, Browser/native roles, public-only handoff, link generation and friction studies. These are design intent, not current feature evidence; older command/framework references were not treated as authoritative implementation. |
| **R05** | [Universal follow-ups](../../dev/backlog/universal-configurator-followups.md), [maintainer authoring extensions](../../dev/backlog/maintainer-authoring-extensions.md): separately bounded future capabilities |
| **R06** | [CLI public-authoring custody](../../dev/backlog/cli-public-authoring-custody.md), [CLI discovery/adoption](../../dev/backlog/cli-discovery-adoption.md), [native host evidence](../../dev/backlog/configurator-native-host-evidence.md): unresolved custody, lifecycle and host requirements |
| **R07** | [Event definition human labels](../../dev/backlog/event-definition-human-labels.md), [separate Store repository](../../dev/backlog/separate-definition-store-repository.md): publisher metadata quality and conditional governance evolution |
| **R08** | Historical [Browser complete-configuration review](../workstreams/i-vsd-browser-complete-configuration.md), [Browser UI/Store review](../workstreams/i-vsd-browser-ui-and-definition-store.md), [universal MVP review](../workstreams/i-vsd-universal-mvp.md), [publisher distribution backlog](../../dev/backlog/publisher-definition-distribution.md), [remaining MVP work](../../dev/backlog/universal-mvp-remaining-work.md). Earlier public-choice-only Browser and old host-matrix statements are historical, not current capability policy. |
| **R09** | Completed guided-configuration-experiences [context](../../dev/zarchive/guided-configuration-experiences/guided-configuration-experiences-context.md), [plan](../../dev/zarchive/guided-configuration-experiences/guided-configuration-experiences-plan.md) and [tasks](../../dev/zarchive/guided-configuration-experiences/guided-configuration-experiences-tasks.md): local archived execution/authority record, revision-2 active scope S01–S20/S25–S34, delivered mechanisms and explicit qualification limits. Not a replacement for committed source/tests or published release evidence. |
| **B01** | [Catalogue](../../src/Configurator.Browser/Pages/Catalogue.razor), [launch selection](../../src/Configurator.Browser/BrowserLaunchSelection.cs), [registry](../../src/Configurator.Browser/BrowserDefinitionRegistry.cs): pinned identity and admission history; authored `LaunchRouteTests` |
| **B02–B03** | [Open page](../../src/Configurator.Browser/Pages/Open.razor), [definition source](../../src/Configurator.Browser/BrowserDefinitionSource.cs), [source pointer](../../src/Configurator.Browser/BrowserDefinitionPointer.cs), [fetch interop](../../src/Configurator.Browser/wwwroot/browser-definition-source.js), [source codec](../../src/Configurator.Core/Definitions/SetupDefinitionSourceCodec.cs): local/remote admission, bounds, sharing and pre-answer retrieval |
| **B04** | [Store](../../src/Configurator.Browser/Pages/Store.razor), [Store project](../../src/Configurator.Browser/Pages/StoreProject.razor), [Store service](../../src/Configurator.Browser/Services/DefinitionStoreCatalog.cs): search, revision review, metadata/digest consistency and independence |
| **B05–B09, B18–B19, B21** | [Field component](../../src/Configurator.Browser/Components/Configuration/ConfigurationField.razor), [field behavior](../../src/Configurator.Browser/Components/Configuration/ConfigurationField.razor.cs), [editor](../../src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor), [review](../../src/Configurator.Browser/Components/Configuration/ConfigurationReview.razor), [diagnostic text](../../src/Configurator.Browser/Components/Configuration/ConfigurationDiagnosticText.cs), [form model](../../src/Configurator.Presentation/Presentation/DefinitionFormModel.cs), [validator](../../src/Configurator.Core/Definitions/DefinitionValidator.cs): typed editing, presence, disclosure, correction and diagnostic granularity |
| **B10–B14** | [Editor behavior](../../src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs), [draft composition](../../src/Configurator.Core/Artifacts/DefinitionDraftArtifactComposer.cs), [inverse import](../../src/Configurator.Core/Definitions/DefinitionImport.cs), [session privacy](../../src/Configurator.Presentation/Presentation/DefinitionSessionPrivacy.cs), [artifact export](../../src/Configurator.Browser/BrowserArtifactExport.cs), [artifact interop](../../src/Configurator.Browser/wwwroot/browser-artifacts.js), [navigation state](../../src/Configurator.Browser/Services/BrowserNavigationState.cs): preparation, artifact-level draft withholding versus fatal bundle failure, import, consent, export and invalidation |
| **B15, B20** | [Offline policy](../../src/Configurator.Browser/BrowserOfflinePolicy.cs), [PWA lifecycle](../../src/Configurator.Browser/wwwroot/pwa-lifecycle.js), [published worker](../../src/Configurator.Browser/wwwroot/service-worker.published.js): readiness, retention, update and reconnect/retry behavior |
| **B16–B17** | [Layout](../../src/Configurator.Browser/Layout/MainLayout.razor), [navigation](../../src/Configurator.Browser/Layout/NavMenu.razor), [Browser UI contract](../../docs/internal/BROWSER_UI.md), [HTML entry](../../src/Configurator.Browser/wwwroot/index.html), reviewed Browser page/component inventory: appearance, direction, English shell and bounded absence inventory |
| **B23** | Historical October 18 observation at `29092c7bc0f5c00beda76bc99c3a206f1ff66aee`: shared `Snapshot:300–311`, `ImportAsync:335–349`, `TrySetValue/SetImported:480–498`, `TryValue:523–531` and artifact copy projection `161–185` identified top-level empty-string loss across interactive clients. Current [form model](../../src/Configurator.Presentation/Presentation/DefinitionFormModel.cs) and [composer](../../src/Configurator.Core/Artifacts/DefinitionArtifactComposer.cs) no longer establish that old defect; see B25/O16 for its correction. |
| **B24** | [Definition metadata](../../src/Configurator.Core/Definitions/SetupDefinition.cs), [source codec](../../src/Configurator.Core/Definitions/SetupDefinitionSourceCodec.cs), [Core contract](../../docs/internal/CORE_DEFINITION_CONTRACT.md) and [neutral example](../../examples/minimal-self-hosted/README.md): bounded data-only experiences, sections, guidance and credential-purpose metadata; canonical revision-3 example and unchanged expected artifact bytes |
| **B25** | [Shared form](../../src/Configurator.Presentation/Presentation/DefinitionFormModel.cs), [editor state](../../src/Configurator.Presentation/Presentation/DefinitionEditorState.cs), [projection](../../src/Configurator.Presentation/Presentation/DefinitionFormProjection.cs), [Presentation contract](../../docs/internal/PRESENTATION_STATE.md), [presence regressions](../../tests/Configurator.Presentation.Tests/DefinitionAnswerPresenceTests.cs) and [container regressions](../../tests/Configurator.Presentation.Tests/DefinitionContainerPresenceTests.cs): accepted values versus invalid drafts, omission, row identity, origins, literal paths and obligation progress |
| **B26** | [Editor](../../src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor), [field](../../src/Configurator.Browser/Components/Configuration/ConfigurationField.razor), [experience tests](../../tests/Configurator.Browser.Tests/BrowserExperienceWorkflowTests.cs) and [collection tests](../../tests/Configurator.Browser.Tests/BrowserCollectionWorkflowTests.cs): selected-section mounting, explicit omission, retained drafts and exact row correction |
| **B27** | [Review](../../src/Configurator.Browser/Components/Configuration/ConfigurationReview.razor), [editor behavior](../../src/Configurator.Browser/Components/Configuration/ConfigurationEditor.razor.cs), [inverse import](../../src/Configurator.Core/Definitions/DefinitionImport.cs), [shared review tests](../../tests/Configurator.Presentation.Tests/DefinitionReviewStateTests.cs) and [Browser review tests](../../tests/Configurator.Browser.Tests/BrowserConfigurationReviewTests.cs): masked origins/readiness, declared consequences, early eligibility, pristine-default replacement and atomic field/controller/consent recovery |
| **B28** | [Field behavior](../../src/Configurator.Browser/Components/Configuration/ConfigurationField.razor.cs), [credential regressions](../../tests/Configurator.Browser.Tests/BrowserCredentialEntryTests.cs) and [rendered-input lease finding](../../dev/_journal/journal.md): denied-control absence, stable consented IDs/names, honest password hints and actual emitted-callback form/path/revision invalidation; no vault integration or local generation |
| **B29** | [Discovery owner](../../src/Configurator.Presentation/Presentation/DefinitionDiscovery.cs), [Browser discovery](../../src/Configurator.Browser/Components/Configuration/ConfigurationDiscovery.razor), [shared tests](../../tests/Configurator.Presentation.Tests/DefinitionDiscoveryTests.cs) and [Browser tests](../../tests/Configurator.Browser.Tests/BrowserConfigurationDiscoveryTests.cs): metadata-only deterministic matching/ranking, facet counts/resets, inactive-controller routing and answer/output preservation |
| **B22, O01, O05, O13** | [Operations](../../docs/internal/OPERATIONS.md), especially lines 156–164, 210–258 and 445–472: recorded published-package work and explicit freshness/platform limitations |
| **C01–C05** | [CLI definition handlers](../../src/Configurator.Cli/SetupCliDefinitionHandlers.cs), [Terminal selection](../../src/Configurator.Terminal/SetupTerminalDefinitionSelection.cs), [Desktop loader](../../src/Configurator.Desktop/ViewModels/DefinitionLoaderViewModel.cs), [CLI guide](../../docs/public/cli.md), [authoring guide](../../docs/public/integrate-your-tool.md): native source/authoring/discovery inventory |
| **C06–C15, C18–C19** | [CLI artifact handlers](../../src/Configurator.Cli/SetupCliArtifactHandlers.cs), [command registry](../../src/Configurator.Cli/SetupCliCommandRegistry.cs), [Terminal window](../../src/Configurator.Terminal/SetupTerminalWindow.cs), [Desktop artifact publisher](../../src/Configurator.Desktop/ViewModels/ArtifactPublisherViewModel.cs), [CLI contract](../../docs/internal/CLI_CONTRACT.md): validation, import, comparison, output and machine/help contracts |
| **C08–C10, C20** | [Terminal field editor](../../src/Configurator.Terminal/SetupTerminalFieldEditor.cs), [Desktop field templates](../../src/Configurator.Desktop/Views/FieldTemplates.axaml), [Desktop list model](../../src/Configurator.Desktop/ViewModels/Fields/ListFieldViewModel.cs), [Desktop window](../../src/Configurator.Desktop/Views/MainWindow.axaml), [Terminal guide](../../docs/public/terminal.md), [Desktop guide](../../docs/public/desktop.md): native editing, presence, disclosure, keyboard and language differences |
| **C16–C17, C21–C22** | [Terminal artifact operation](../../src/Configurator.Terminal/SetupTerminalArtifactOperation.cs), [CLI application](../../src/Configurator.Cli/SetupCliApplication.cs), [shared preparation operation](../../src/Configurator.Presentation/Presentation/DefinitionPresentationOperation.cs), [secret generator](../../src/Configurator.Core/Secrets/LocalSecretGenerator.cs), [packaging contract](../../docs/internal/PACKAGING.md), [Desktop NativeHost](../../tests/Configurator.Desktop.NativeHost/Program.cs): API-versus-UI distinction, publication facts and host evidence boundaries |
| **O02, O12** | [Build/test workflow](../../.github/workflows/build-test.yml), [Store workflow](../../.github/workflows/validate-definition-store.yml), [testing guide](../../docs/internal/TESTING.md), [release runbook](../../docs/internal/RELEASE_RUNBOOK.md): configured controls, unsigned provenance and evidence scope |
| **O03–O08, O11** | [Browser tests](../../tests/Configurator.Browser.Tests/): `BrowserShellTests`, `BrowserConfigurationNavigationTests`, `BrowserSourceEntryTests`, `BrowserCollectionWorkflowTests`, `BrowserAppearanceTests`, `BrowserDefinitionSourceTests`, `BrowserTypedPrivacyTests`, `BrowserSecretBoundaryTests`, `BrowserExportLifecycleTests`, `BrowserTemplateReadLifecycleTests`, `BrowserAnswerBudgetTests`, `BrowserEventManifestWorkflowTests`, `BrowserStoreIndependenceTests`, `BrowserDefinitionStoreTests`. Original October 18 audit inspected authored coverage only; current recorded owner execution is separately O16. |
| **O09–O10, O14–O15** | [Security policy](../../SECURITY.md), [launch contract](../../docs/internal/LAUNCH_CONTRACT.md), [Browser hosting guide](../../docs/public/browser.md), [Browser UI contract](../../docs/internal/BROWSER_UI.md), historical Browser UI/Store review: hosting, response policy, origin limits and absent stakeholder evidence |
| **O16** | R09's recorded October 20 execution: Core 145/145, Presentation 142/142, Browser 176/176; targeted literal diagnostic, semantic and neutral-output vectors; native schema freshness/canonical parity and solution Release build. Exact principal commands/results appear below. Inspected committed regressions support specific claims; counts alone do not establish complete coverage. |
| **O17** | R09's October 20 local Chromium-backed observations: 1440 desktop and 390 touch/mobile widths, light/dark, review/correction focus, preserved three drafts through zero-match search, masked credential fill/revocation, actual import field/consent recovery, bounded canary sinks and narrow/doubled-text limitations. Not a published-PWA, extension, AT, physical-device or production-host matrix. |
| **O18** | R09's native checks: Desktop `FieldViewModelTests` 20/20, Terminal `SetupTerminalGuiAdapterTests` 26/26, actual Linux Desktop launch inspected through granted AX only, and Terminal 120×40 rendering/field navigation. No native save/apply; Wayland pixel capture unavailable. Other OS/RID and complete native accessibility qualification remain open. |

### October 20 recorded execution and observation limits

The completed implementation used SDK 10.0.302, Release/MTP and `TMPDIR="$HOME/.cache/agent-tmp"`. Its principal full-owner commands and recorded results are:

```bash
dotnet test --project tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 145 --no-progress --no-ansi
dotnet test --project tests/Configurator.Presentation.Tests/Configurator.Presentation.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 142 --no-progress --no-ansi
dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 176 --no-progress --no-ansi
dotnet build --configuration Release --verbosity quiet
```

The record reports 145/145, 142/142 and 176/176 respectively and build exit 0, with targeted Desktop/Terminal results separately recorded in O18. The final runtime correction is `3dceffb`; `65dde26` adds the durable rendered-input lease finding. No product source was changed or product suite rerun for this audit refresh.

The positive real-surface observations are deliberately narrower than qualification:

- Actual review/correction actions reached headings and exact controls; search retained focus and three prepared drafts through a zero-result query. This does not establish every physical-keyboard or assistive-technology journey.
- Consent produced masked credential controls with stable IDs/names and appropriate hints. Revocation removed controls and synthetic canary markup; reconsent restored empty controls only. A real extension/vault was not qualified.
- Actual conflicting-file recovery retained the manual service name and reached its control. Secret-bearing rejection focused the unchecked consent control without exposing the imported canary, submitting, enabling consent or preparing output.
- Query canaries were absent from URL, local/session storage and resource URLs. The finite probe found no cache entries or IndexedDB stores and one public service-worker registration/controller. This is not a complete worker-message, request-body, PWA-cache or deployed-origin audit.
- Desktop/mobile-width light/dark observations showed no horizontal overflow; facet-label targets measured 44 CSS pixels. The requested 320-pixel/doubled-text probe rendered a 325-pixel layout viewport. No exact 320-pixel, native zoom, forced-color, full contrast, screen-reader or physical-mobile pass is inferred.
- Native Desktop's granted accessibility tree exposed the loaded definition and controls; pixel capture was unavailable. Terminal actually rendered the example and navigated between fields. Neither smoke observation establishes protected-save behavior or other native hosts.

### External source register

Actual web searches and targeted official-document retrieval were used for the original audit. All sources below were accessed **2026-10-18**; they were not re-fetched or represented as newly verified on October 20. This refresh updates first-party implementation/evidence dispositions, not external API claims. These are source handles and paraphrased functional facts; no external implementation source, snippets, assets or expressive UI design were imported into the report's recommendations. Standards and interface constraints informed an independently organized, repository-grounded analysis. No dependencies were added.

| ID | Official source | Relevance |
| --- | --- | --- |
| **W01** | [MDN: showSaveFilePicker](https://developer.mozilla.org/en-US/docs/Web/API/Window/showSaveFilePicker) | Picker availability, activation, permission and cancellation |
| **W02** | [MDN: createWritable](https://developer.mozilla.org/en-US/docs/Web/API/FileSystemFileHandle/createWritable) | Write/close semantics, locking and failures; not native permission guarantees |
| **W03** | [W3C File API](https://www.w3.org/TR/FileAPI/) | User-selected files, metadata and reading failures |
| **W04** | [MDN Clipboard API](https://developer.mozilla.org/en-US/docs/Web/API/Clipboard_API#security_considerations) | Secure-context, permission and activation differences |
| **W05** | [Microsoft: Using the clipboard](https://support.microsoft.com/en-us/windows/apps/using-the-clipboard) | History, synchronization and pinned copies |
| **W06** | [MDN storage quotas and eviction](https://developer.mozilla.org/en-US/docs/Web/API/Storage_API/Storage_quotas_and_eviction_criteria) | Best-effort persistence, quota, private mode and deletion |
| **W07** | [WebKit storage policy](https://webkit.org/blog/14403/updates-to-storage-policy/) | Context-dependent persistence and eviction |
| **W08** | [web.dev service-worker lifecycle](https://web.dev/articles/service-worker-lifecycle) | Waiting workers, activation and client coherency |
| **W09** | [Microsoft .NET 10 Blazor PWA guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/progressive-web-app?view=aspnetcore-10.0) | Published-build offline/update behavior and limitations |
| **W10** | [MDN Content-Security-Policy](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy) | WASM allowance, destination directives and worker policy |
| **W11** | [MDN CORS](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/CORS) | Response-read permissions versus contact/content trust |
| **W12** | [MDN RequestInit](https://developer.mozilla.org/en-US/docs/Web/API/RequestInit) | Credentials, redirects, request bodies, referrer and integrity controls |
| **W13** | [MDN Cache](https://developer.mozilla.org/en-US/docs/Web/API/Cache) | Explicit Cache Storage lifetime and header independence |
| **W14** | [Chrome 142 release notes](https://developer.chrome.com/release-notes/142#local_network_access_restrictions) | Browser-specific local-network permission defense |
| **W15** | [W3C Web Cryptography Recommendation](https://www.w3.org/TR/2017/REC-WebCryptoAPI-20170126/) | Key storage/use and script/device threat limitations |
| **W16** | [MDN Web Workers](https://developer.mozilla.org/en-US/docs/Web/API/Web_Workers_API/Using_web_workers) | Worker lifecycle, messaging, transfers and policy |
| **W17** | [WHATWG HTML: downloading resources](https://html.spec.whatwg.org/multipage/links.html#downloading-resources) | Download cancellation, naming and page-versus-automation observability |
| **W18** | [Chrome: File System Access API](https://developer.chrome.com/docs/capabilities/web-apis/file-system-access) | Picker-based API support and permission model |
| **S01** | [W3C WCAG 2.2](https://www.w3.org/TR/WCAG22/) | Normative accessibility criteria; no application conformance inferred |
| **S02** | [W3C inline bidirectional text](https://www.w3.org/International/articles/inline-bidi-markup/) | Mixed-direction technical tokens and text isolation |
| **S03** | [W3C structural text direction](https://www.w3.org/International/questions/qa-html-dir.en.html) | Language/direction distinctions and RTL structure |
| **S04** | [WAI supportive forms](https://www.w3.org/WAI/WCAG2/supplemental/patterns/o4p04-supportive-forms/) | Cognitive accessibility and reduced memory/correction burden |
| **S05** | [WAI involving users in evaluation](https://www.w3.org/WAI/test-evaluate/involving-users/) | Participant involvement and limits of generalization |
| **S06** | [OWASP HTML5 security](https://cheatsheetseries.owasp.org/cheatsheets/HTML5_Security_Cheat_Sheet.html) | Same-origin storage and local-browser security limits |
| **S07** | [OWASP third-party JavaScript](https://cheatsheetseries.owasp.org/cheatsheets/Third_Party_Javascript_Management_Cheat_Sheet.html) | Privileges and risk of injected/third-party scripts |
| **S08** | [OWASP file upload guidance](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html) | Transferable bounded-content-validation principles; server upload controls not assumed applicable to local-only reading |
| **S09** | [W3C CSP Level 3](https://www.w3.org/TR/CSP3/) | Meta-policy limitations, effective policy and containment boundaries |

## Missing Evidence

The original October 18 audit recorded no external MCP resources or templates in its inventory. This refresh uses repository source and recorded execution rather than treating tool availability as external evidence. No support-ticket system, private analytics, incident records, participant recruitment data, production deployment access or publisher-verification records were reviewed. The report does not invent evidence from those sources.

The original audit found the user-owned strategy files under `dev/active/`; they now resolve at `docs/internal/universal-configurator.md` and `docs/internal/go-to-market.md`. This refresh corrects the links and confirms their identities without editing either document. Strategy material remains design intent, not stakeholder validation or current implementation proof inferred from its external citations.

Presence and empty-string fidelity now have recorded regression evidence. The main missing technical evidence is offline lifecycle recovery, the broader exact-release browser/extension/AT/mobile matrix, performance budgets, complete update/eviction recovery and effective production-host policy. The main missing product evidence is whether the delivered guided/search/review workflows and proposed advanced features improve completion and comprehension for the intended users. The main missing governance evidence is operational ownership and practice for support response, curation correction and independently authenticated publication.

## Context Inventory

- **Request and agreement:** the user requested an exhaustive, extensively researched I-VSD Browser audit and asked whether Browser, CLI, TUI and Desktop should expose the same features, then requested this update after completion of guided-configuration work. The agreed consultation scope includes advanced validation and public-definition authoring, not only form cosmetics.
- **Repository authority:** current contribution/security/runtime contracts and actual source take precedence over historical source-system examples and outdated strategy implementation details.
- **Current implementation:** reviewed at the exact Git object in Review Metadata. This consultation changes only this report; it does not implement recommendations or alter existing workstream reports.
- **Completed implementation:** the archived guided-configuration context/tasks and eight local commits establish the delivered scope. This consultation re-evaluates affected findings; completion of that workstream does not close unrelated maintainer, offline or release-qualification obligations.
- **Historical moral reviews:** the Browser complete-configuration and UI/Store reports remain evidence for their own scoped decisions. Older counts/caveats are not promoted into current results; newer execution is separately identified as O16–O18.
- **Platform research:** official browser/runtime, W3C/WAI and OWASP sources were consulted through actual October 18 web research and retain that access date; this documentation refresh performs no new external research.
- **Known exclusions:** executable definitions, answer-bearing fetch, default telemetry, target authority, automatic persistence and unsafe native-save fallback remain outside the current product contract.
- **Remaining product choices:** exact language/browser support priorities, checkpoint custody and optional advanced features need planning decisions informed by this report; no such choice was silently implemented.

## Review Lifecycle

This is a **current advisory audit of the named evidence revision**, not release approval or a claim that Browser meets every completion profile. **IVSD-F002 is resolved; seventeen findings remain open**, including partially delivered mitigations and ongoing assurance obligations. Existing protections described inside an open finding remain implemented; the open state concerns the named remaining gap, not a claim that delivered controls are absent.

| Date | Lifecycle transition | Trigger and evidence |
| --- | --- | --- |
| 2026-10-18 | Initial consultation → current advisory audit | User-agreed exhaustive scope; source review at `29092c7bc0f5c00beda76bc99c3a206f1ff66aee`, official-source research and independent report review. No product execution, implementation approval or resolved mitigation is implied. |
| 2026-10-20 | Current at October 18 revision → current at `65dde2668878db0e60e12d0051086643c8759520` | User-requested reassessment after eight guided-configuration commits; inspected current source/regressions and archived recorded execution. IVSD-F002 resolved; location diagnostics, eligibility/recovery, provenance/review, navigation/progress and privacy mitigations partially delivered within still-open findings. External sources retain October 18 access dates. No complete Browser/support or target-acceptance approval is implied. |

Material changes to Browser custody, source admission, shared definition semantics, authoring, hosting, export, persistence, supported platforms or the parity policy require revalidation. A later implementation plan should map each accepted mitigation to bounded behavior, an owner, meaningful verification and explicit deferred scope. It must not mark a finding resolved solely because a checkbox, API or UI control exists.

Earlier reports are neither overwritten nor silently superseded. If this report is updated, retain stable finding/mitigation IDs, record the new evidence revision and explain changed dispositions. New executed evidence should state exact commands or browser procedures, package identities, outcomes and limitations. Stakeholder and operational evidence should retain their own scope rather than being flattened into a single “passed” status.

**Overall recommendation:** preserve the completed guided/search/review foundation and corrected answer fidelity; finish offline recovery, precise explanations, output selection and current support evidence before making a broader operator-completeness claim. Add remaining advanced and maintainer workflows through bounded shared owners, with separate authorization for new custody. Require equal semantic honesty across clients while preserving their real authority differences.
