# Encrypted Local Configuration Profiles & Day-2 Schema Comparison — Provider-Responsibility Assessment

Last Updated: 2026-11-13

## Review Metadata
- Mode: planning
- Subject: Multi-mode local configuration persistence and Day-2 schema upgrade/diffing (`compare-only`, `import-upgrade-only`, `inspectable-encrypted`, alongside existing unencrypted exports), convenience-first multi-recipient DEK wrapping (OS keyring auto-unlock + Argon2id portable/recovery passphrase), immutable create-only `.cfgvault` storage, Browser WASM transient passphrase portability, and deferred remote Coolify/Secret-Manager apply boundaries
- Workstream: encrypted-local-profiles
- Report kind: implementation-planning assessment
- Report revision: encrypted-profiles-ivsd-r1
- Report status: current
- Disposition: plan-aligned
- Evidence cutoff: 2026-11-13
- Reviewed input: Completed `dev/active/encrypted-local-profiles/{encrypted-local-profiles-plan,encrypted-local-profiles-context,encrypted-local-profiles-tasks}.md` (`rev-1`), [`ADR-encrypted-local-profiles-and-target-apply.md`](../../docs/internal/adr/ADR-encrypted-local-profiles-and-target-apply.md), repository codebase inspection, `.NET 10 browser-wasm` cryptography API boundaries, OWASP Password Storage / RFC 8439 / RFC 9106 / FreeDesktop Secret Service specifications
- Supersedes: none (initial planning-aligned assessment)

## Executive Summary

Self-hosting administrators upgrading an application across breaking schema releases (`v1 -> v2`) often either re-type every secret and setting from scratch, keep plaintext `.env` backups scattered across developer disks, or inspect live production servers manually just to see what changed. Configurator can eliminate that friction by managing encrypted local configuration profiles (`.cfgvault`) alongside secret-free comparison snapshots, allowing Day-2 schema comparison (`compare-only`) and secret-blind upgrades (`import-upgrade-only`) as well as in-app encrypted editing (`inspectable-encrypted`).

That convenience introduces concentrated custodial obligations. Under **Amanah (trustworthy stewardship)**, **Sidq (truthfulness)**, **Bayan (clear disclosure)**, **La Darar (non-harm)**, and **Adl (equitable access)**, the provider controls the cryptographic envelope format, key-wrapping hierarchy, memory-clearing discipline, tamper resistance against mode downgrades, and whether the Browser client honors its zero-persistence promise. This assessment evaluates six ethical and architectural findings (`IVSD-F001` through `IVSD-F006`) and maps every mitigation (`IVSD-M001` through `IVSD-M006`) directly into the `encrypted-local-profiles` implementation plan and Red→Green task checklist.

Crucially, `compare-only` is the only profile mode that guarantees **cryptographic unreadability** of secrets because secret plaintext is stripped prior to serialization. By contrast, `import-upgrade-only` hides secrets from casual in-app reveal, copy, and export surfaces during an upgrade session while an authorized operator holds the unlocked workspace key in memory; pretending UI hiding equals cryptographic isolation against a local debugger would deceive users (**Sidq**). Likewise, OS keyring auto-unlock (`Slot A`) maximizes daily Desktop convenience, yet without an optional portable Argon2id passphrase (`Slot B`), switching laptops or reinstalling an OS would lock administrators out of their own production secrets (**La Darar**). Remote Coolify and external secret-manager apply (`ISLAMU SecretKit`, Infisical, Azure Key Vault) remain explicitly out of scope and deferred to a subsequent workstream.

With the `encrypted-local-profiles` triad complete (`Report status: current`, `Disposition: plan-aligned`), every ethical safeguard traces to concrete Red-before-Green TUnit specs across Phases 1–7. Plan alignment confirms architectural and ethical coverage of the design; runtime proof remains pending execution once approved.

## Scope

- **In Scope:**
  - `Configurator.Core`: `.cfgvault` binary/JSON AEAD envelope format (`ChaCha20-Poly1305` + `Argon2id` + `HKDF-SHA256` via `BouncyCastle.Cryptography` `2.7.0`), multi-recipient key slots, domain-separated keyed HMAC-SHA256 rotation commitments for `compare-only`, canonical `DefinitionSchemaDiff` and `ProfileUpgradePlanner` across definition revisions.
  - `Configurator.Artifacts`: Immutable per-revision create-only profile persistence (`IProtectedArtifactWriter` semantics under OS user config paths), OS Keyring wrapping provider abstractions (`libsecret` on Linux Desktop with graceful headless fallback; DPAPI / Keychain platform adapters scoped accurately per `STATUS.md`).
  - `Configurator.Presentation`: Immutable unlock/upgrade state fencing (`OperationGeneration`), `BlindSecretSlot` custody for `import-upgrade-only` forwarding without UI reveal/copy/export leakage, synchronous buffer zeroing on edit/cancel/revocation/disposal, and bilingual (`en`/`ar`) localized review models.
  - `Configurator.Cli`, `Configurator.Desktop`, `Configurator.Terminal`, `Configurator.Browser`: CLI `profile` command family + `schemas/configurator-cli.schema.json` generation, Desktop & Terminal convenience unlock/save/upgrade flows, and Browser explicit passphrase-only portable `.cfgvault` import/export under `BrowserSecretsEnabled` consent with zero browser persistence.
  - Documentation & release governance: paired public/internal documentation, `ADR` alignment, and future delta seams for Coolify / `ISLAMU SecretKit`.
- **Explicitly Out of Scope (Deferred):**
  - Network API calls, credential storage, or live apply against Coolify, `ISLAMU SecretKit`, Infisical, HashiCorp Vault, or Azure Key Vault.
  - Telemetry, cloud account sync, automated backup uploads, or Event database/HAL/BFF persistence.

## Claim Boundary

This assessment relies strictly on static repository inspection (committed workspace files, `docs/internal/PROTECTED_ARTIFACTS.md`, `docs/internal/SECRET_CRYPTOGRAPHY.md`, `docs/internal/LAUNCH_CONTRACT.md`, `docs/internal/ADR-encrypted-local-profiles-and-target-apply.md`, `eng/configurator/Configurator.Release.proj`, `tests/Configurator.Architecture.Tests/StandaloneArchitectureTests.cs`) and public specification analysis (RFC 8439, RFC 9106, OWASP Password Storage Cheat Sheet, FreeDesktop Secret Service API, .NET 10 `browser-wasm` WebCrypto limitations). **No encryption codec, keyring P/Invoke, or client flow has been implemented or executed yet.** Nothing in this document claims runtime test proof, cross-OS native certification on Windows/macOS, or any religious-legal (fiqh) ruling.

## Findings

### IVSD-F001: Four-mode persistence boundaries and tamper-resistant AEAD purpose binding (`Sidq` & `Bayan`)

An operator choosing to save a configuration for Day-2 maintenance needs distinct security guarantees: sharing a stripped schema-comparison baseline (`compare-only`), blindly carrying production secrets across a breaking upgrade (`import-upgrade-only`), opening an editable local working vault (`inspectable-encrypted`), or writing raw target deliverables (existing unencrypted export). If `compare-only` accidentally includes defaulted or inherited secret leaves, secrets leak permanently in plain text. Conversely, if a mutable unauthenticated JSON header field selects `purpose: "import-upgrade-only"`, anyone editing that plain string could relabel it `"inspectable-encrypted"` and bypass in-app reveal restrictions; and if UI copy claims `import-upgrade-only` is cryptographically unbreakable even while unlocked on an untrusted machine, it misleads operators about their threat model (**Sidq** and **Bayan**).

- **Mechanism & Mitigation (`IVSD-M001`)**: Model `ProfileProtectionMode` (`CompareOnly`, `ImportUpgradeOnly`, `InspectableEncrypted`) as a closed Core contract with strict behavioral separation:
  1. `CompareOnly` walks `DefinitionSensitivityPolicy` recursively across `AnswerSnapshot`, stripping every `Sensitive` and `Secret` leaf before JSON serialization (rejecting unencrypted export routes for raw secrets cleanly via diagnostic codes) and optionally recording non-reversible keyed HMAC-SHA256 secret commitments (`IVSD-F005`). It requires no AEAD secret payload or workspace secret consent in Browser unless HMAC key unlocking is explicitly requested.
  2. `ImportUpgradeOnly` and `InspectableEncrypted` seal the full serialized `AnswerSnapshot` inside a `.cfgvault` envelope encrypted with 256-bit `ChaCha20-Poly1305` (`BouncyCastle.Cryptography`), binding the canonical header JSON (including `vaultContractVersion`, `purpose`, `profileId`, `definition.familyId`, `definition.revision`, `definition.digest`, `environment`, and wrapped `recipients[]`) verbatim as ChaCha20-Poly1305 Associated Authenticated Data (`AAD`) so any header or purpose modification invalidates authentication (`vault-tampered`).
  3. Surface clear localized explanations (`en`/`ar`) stating what each mode guarantees: `compare-only` strips all secrets permanently; `import-upgrade-only` seals secrets from in-app reveal, copy, undo history, and plaintext inspection during upgrades while relying on local OS/session integrity during unlock; `inspectable-encrypted` permits full in-app edit when unlocked.
- **Lifecycle / severity / claim type**: proposed / critical / truthfulness and cryptographic mode-integrity constraint
- **Principles / domains**: Truthfulness (`Sidq`), Clear Disclosure (`Bayan`), Trust (`Amanah`) / Core cryptography, UI messaging
- **Stakeholders**: Self-hosting operators, security auditors, maintenance engineers
- **Controlled decision**: Closed `ProfileProtectionMode` semantics, header-as-AAD binding, and honest threat-model copy
- **Evidence / validation**: `E01` (`DefinitionSecretPolicy.cs`, `ADR-encrypted-local-profiles-and-target-apply.md`); `E02` (RFC 8439 AEAD AAD binding)
- **Linked mitigation**: `IVSD-M001`
- **Owner / next validation**: Core & Presentation maintainers; Red invariant tests attempting `purpose` header tampering, sensitivity leakage into `CompareOnly`, and downgrade conversions
- **Escalation**: Security review if non-AEAD or unauthenticated headers are ever proposed

### IVSD-F002: Convenience-first multi-recipient DEK wrapping (`Slot A` OS Keyring + `Slot B` Argon2id Recovery) (`Amanah` & `La Darar`)

Operators asked for convenience on Desktop (`Auto-unlock` via OS keyring) rather than re-typing master passphrases on every open, upgrade, or comparison. However, tying `.cfgvault` decryption exclusively to an ephemeral OS keyring secret creates two ethical hazards (**La Darar** and **Amanah**): (a) hardware failure, VM rebuilds, SSH terminal workflows, or migrating a config to another server would permanently lock operators out of their own secrets; and (b) deriving a fallback key silently from static machine identifiers (`/etc/machine-id`, username, hostname) creates a false sense of encryption easily broken offline. Meanwhile, calling `.NET 10`'s `AesGcm` or `ChaCha20Poly1305` BCL wrappers fails with `PlatformNotSupportedException` inside Browser WASM (`E02`).

- **Mechanism & Mitigation (`IVSD-M002`)**: Implement pure managed multi-recipient envelope encryption inside `Configurator.Core.Profiles` using already-pinned `BouncyCastle.Cryptography` `2.7.0`:
  1. Generate a fresh 256-bit random Content/Data Encryption Key (`DEK`) per `.cfgvault` revision via `RandomNumberGenerator`.
  2. Wrap the `DEK` independently for each configured recipient slot using `HKDF-SHA256` subkey derivation + `ChaCha20-Poly1305` bound to slot metadata + vault AAD:
     - **Slot A (`os-keyring-wrap`):** Wraps the `DEK` under a user-scoped 256-bit `KEK-local` stored inside `IOsKeyringProvider` (`Configurator.Artifacts` — Linux `libsecret-1.so.0` D-Bus Secret Service on Desktop; native Windows DPAPI `crypt32.dll` / macOS `Security.framework` adapters where host-qualified; immediate fail-closed `KeyringUnavailable` when running headless/SSH or when daemon is locked, NEVER falling back to static host-ID deterministic keys or plaintext).
     - **Slot B (`argon2id-passphrase`):** Wraps the same `DEK` using `Argon2BytesGenerator` (`Argon2id` v1.3, 128-bit random salt, minimum enforced floor `>= 19456 KiB`, `>= 2` iterations, `>= 1` parallelism per `SECRET_CRYPTOGRAPHY.md` and OWASP 2025, default `65536 KiB` / `3` iterations / `1` thread on Desktop/CLI and configurable within safe floor bounds for Browser responsive UX). Reject weak/empty passphrases when saving passphrase-protected profiles.
  3. Surface a non-blocking `"Add recovery passphrase (Slot B) for machine portability"` prompt whenever an operator saves a Slot-A-only profile on Desktop, and allow non-destructive addition/rotation of slots on new snapshots.
- **Lifecycle / severity / claim type**: proposed / critical / recoverability and cryptographic custody boundary
- **Principles / domains**: Trust (`Amanah`), Non-Harm (`La Darar`) / key management, cross-platform cryptography
- **Stakeholders**: Solo self-hosters, Desktop users, SSH Terminal admins, Browser air-gapped operators
- **Controlled decision**: Multi-recipient DEK envelope, zero deterministic machine-ID fallback, pure BouncyCastle managed primitives
- **Evidence / validation**: `E01` (`SECRET_CRYPTOGRAPHY.md`, `Directory.Packages.props`); `E02` (RFC 9106 Argon2id, .NET 10 WASM WebCrypto gap)
- **Linked mitigation**: `IVSD-M002`
- **Owner / next validation**: Core & Artifacts maintainers; Red tests verifying Slot A/Slot B cross-unlock, floor parameter rejection (`memory < 19456 KiB` / `iterations < 2`), fail-closed `KeyringUnavailable` without deterministic fallback, and WASM execution probe
- **Escalation**: None unless hardware YubiKey/FIDO2 PRF WebAuthn wrapping is added later

### IVSD-F003: Blind secret forwarding custody (`BlindSecretSlot`) and synchronous buffer zeroing (`Amanah` & `Tajassus` avoidance)

When upgrading via `import-upgrade-only`, Configurator holds sensitive strings (database passwords, API tokens, OIDC client secrets, signing keys) that the operator specifically chose NOT to read on screen. Any leaky abstraction—exposing a shared `FieldViewModel.Value` property, leaving reveal/copy buttons active, pushing secret values into `DefinitionFormUndoHistory`, echoing values in diagnostic error codes, or failing to clear arrays when a workspace changes or consent is revoked—breaks **Amanah** and **Tajassus avoidance** (preventing unnecessary exposure).

- **Mechanism & Mitigation (`IVSD-M003`)**: Introduce an explicit **`BlindSecretSlot`** abstraction inside `Configurator.Presentation`:
  1. When adopting an `import-upgrade-only` `.cfgvault`, compatible secret fields enter `DefinitionFormModel` as opaque `BlindSecretSlot` buffers rather than plaintext string properties on observable viewmodels.
  2. Presentation/UI projections expose `IsBlindProtectedSecret == true` and blocked affordances: zero Reveal toggle, zero Copy-to-Clipboard command, zero plain export (`export` / `compare-only` strips them cleanly or preserves keyed commitments only), and zero entry in `DefinitionFormUndoHistory` plaintext value logs.
  3. Editing or clicking `"Replace protected secret"` explicitly clears and zeroes the old `BlindSecretSlot`, requiring a deliberate user confirmation dialog before discarding a retained production secret (`La Darar`).
  4. `CryptographicOperations.ZeroMemory` clears all temporary UTF-8 plaintext arrays, `DEK`, `KEK`, and `BlindSecretSlot` buffers synchronously inside `finally` blocks and on cancellation, workspace edit/replacement, `DisableSecretEntry()` (Browser consent revocation), and `Dispose()`. All diagnostics emit stable value-free codes (`vault-invalid-passphrase`, `vault-tampered`, `vault-keyring-unavailable`, `vault-stale`).
- **Lifecycle / severity / claim type**: proposed / critical / runtime memory custody and anti-exfiltration boundary
- **Principles / domains**: Trust (`Amanah`), Privacy / avoiding unnecessary exposure (`Tajassus`), Non-Harm (`La Darar`) / Presentation state, Desktop/Terminal/Browser UI
- **Stakeholders**: Operators delegating upgrade prep, sysadmins sharing screens, privacy-sensitive environments
- **Controlled decision**: Separate `BlindSecretSlot` custody in `DefinitionFormModel`, synchronous `ZeroMemory`, blocked UI reveal/copy/undo surfaces
- **Evidence / validation**: `E01` (`DefinitionSessionPrivacy.cs`, `DefinitionFormModel.cs`, `privacy-and-pii.md`)
- **Linked mitigation**: `IVSD-M003`
- **Owner / next validation**: Presentation & Client maintainers; Red custody-breach tests proving zero plaintext bytes survive reveal/copy probes, diagnostics, undo history, or Browser `DisableSecretEntry()`
- **Escalation**: None

### IVSD-F004: Immutable per-revision create-only storage vs. Browser zero-persistence portability (`Amanah` & `Sidq`)

Overwriting a single mutable `.cfgvault` file in place risks silent data loss during a failed upgrade, symlink/TOCTOU overwrite bugs, and stale index corruption. Meanwhile, Browser WASM (`Configurator.Browser`) operates under `AGENTS.md` Rule 4 (`persistence: false`, `answerPersistence: false`): saving encrypted vaults into `localStorage`, `IndexedDB`, or PWA CacheStorage—even encrypted—would blur Browser's zero-persistence guarantee and surprise operators sharing a browser profile.

- **Mechanism & Mitigation (`IVSD-M004`)**:
  1. **Native Profile Store (`Configurator.Artifacts.Profiles`):** Persist native saved profiles as **immutable per-revision `.cfgvault` files** (`rev-<revision>-<timestamp>-<shortDigest>.cfgvault`) inside `<XDG_CONFIG_HOME or ~/.config>/islamu-configurator/profiles/<safe-app-id>/<safe-env>/`, created strictly through `IProtectedArtifactWriter` (`UnixProtectedArtifactWriter` create-only `0600` files inside `0700` user-owned directories; existing conflicts never overwrite). Unsupported protected-save hosts (`!writer.IsAvailable`, i.e., non-Linux hosts while Windows/macOS protected writers remain `Planned`) fail closed with `ProtectedArtifactStatus.UnsupportedPlatform` and zero insecure plain-file fallback (`AGENTS.md` Rule 5). Discover saved profiles by reading bounded **public header bytes** directly from immutable `.cfgvault` files in the directory—avoiding any mutable sidecar index overwrite race!
  2. **Browser Portable Vault (`Configurator.Browser`):** Offer `.cfgvault` solely as **in-memory file upload / deliberate file download** (`Passphrase Slot B` + `CompareOnly`). Opening a secret-bearing `.cfgvault` (`ImportUpgradeOnly` or `InspectableEncrypted`) requires explicit `BrowserSecretsEnabled` workspace consent first; revoking consent purges all unlocked secrets, blind slots, and loaded vault buffers immediately. Zero `localStorage`, `IndexedDB`, `CacheStorage`, `ServiceWorker` cache, or network requests ever touch `.cfgvault` bytes or passphrases. Keep `eng/configurator/generated/browser-release-capabilities.json` strictly `persistence: false` and `answerPersistence: false`.
- **Lifecycle / severity / claim type**: proposed / critical / native atomic write safety and Browser zero-persistence invariant
- **Principles / domains**: Trust (`Amanah`), Truthfulness (`Sidq`), Non-Harm (`La Darar`) / native I/O safety, WASM browser custody
- **Stakeholders**: Linux Desktop/Terminal/CLI users, Browser WASM users, multi-user workstation operators
- **Controlled decision**: Append-only immutable per-revision `.cfgvault` files via `IProtectedArtifactWriter`, header-scan discovery, Browser memory-only upload/download
- **Evidence / validation**: `E01` (`ProtectedArtifactWriter.cs`, `UnixProtectedArtifactWriter.cs`, `browser-release-capabilities.json`, `blazor-client.md`)
- **Linked mitigation**: `IVSD-M004`
- **Owner / next validation**: Artifacts, Browser, & Architecture maintainers; real Linux `Artifacts.Tests` symlink/conflict/mode-0600 races, unsupported-host rejection checks, and bUnit/Browser storage isolation tests
- **Escalation**: Revisit only when Windows/macOS `IProtectedArtifactWriter` implementations undergo native host qualification

### IVSD-F005: Honest Day-2 schema upgrade diffing, keyed HMAC rotation commitments, and Arabic (`ar`) RTL parity (`Sidq` & `Adl`)

When upgrading from definition `V_old` to `V_new`, a naive JSON diff either misses silent breaking changes (such as a secret field whose `minBytes` increased from `16` to `32`, a field whose `sensitivity` escalated from `public` to `secret`, or removed choices) or compares secrets in `compare-only` baselines using plain unsalted SHA-256 digests—enabling offline dictionary brute-forcing of weak shared secrets like `"changeme"` or common tokens. Additionally, English-only upgrade classifications and RTL-broken technical identifiers exclude Arabic-speaking self-hosters (**Adl**).

- **Mechanism & Mitigation (`IVSD-M005`)**:
  1. Build deterministic `DefinitionSchemaDiff` and `ProfileUpgradePlanner` in `Configurator.Core.Profiles` comparing canonical field schemas across `Definition V_old` and `Definition V_new`: classify `AddedRequired`, `AddedOptional`, `Removed`, `TypeChanged`, `FormatOrRangeTightened`, `ChoicesChanged`, `SensitivityEscalated`, `SecretPolicyTightened`, and `ApplicabilityChanged`. Enforce fail-safe upgrade rules: whenever `SecretPolicyTightened` or `SensitivityEscalated` occurs, reject blind carry-forward (`BlindCarryRejected`) and require explicit regeneration or new input so weak legacy credentials never sneak into `V_new`.
  2. For `compare-only` secret change detection, compute optional rotation commitments exclusively via **domain-separated keyed HMAC-SHA256** (`HMAC(commitmentKey, "islamu-cfg-commit-v1\0" || canonicalPath || "\0" || utf8Secret)`) using a per-profile random 256-bit `commitmentKey` stored inside the operator's protected key material (or OS keyring / local `.cfgvault` envelope)—never bare unsalted SHA-256 hashes! Where no `commitmentKey` is available during cross-host comparison, honestly report `SecretComparisonStatus.UnavailableWithoutKey` instead of pretending `"unchanged"` or `"changed"`.
  3. Provide complete English (`en`, LTR) and Arabic (`ar`, RTL) resource strings across `PresentationStrings`, `DesktopStrings`, `TerminalStrings`, and `BrowserStrings`, wrapping digests, JSON Pointers, field wire names, and environment slugs with explicit LTR bidi isolation (`dir="ltr"` / `FlowDirection.LeftToRight`).
- **Lifecycle / severity / claim type**: proposed / high / schema-upgrade correctness, offline dictionary resistance, and multilingual accessibility
- **Principles / domains**: Truthfulness (`Sidq`), Justice (`Adl`), Non-Harm (`La Darar`) / schema diffing, cryptography, localization
- **Stakeholders**: English and Arabic administrators managing version upgrades across revisions (`e.g. revision 3 -> 4+`), CLI automation engineers
- **Controlled decision**: Fail-safe `SecretPolicyTightened` rejection on blind carry-forward, keyed HMAC-SHA256 commitments (zero bare hashes), complete `en`/`ar` RTL-isolated diff UI
- **Evidence / validation**: `E01` (`DefinitionImport.cs`, `DefinitionSchema.cs`, `SecretGenerator.cs`); `E02` (NIST SP 800-107 / domain separation)
- **Linked mitigation**: `IVSD-M005`
- **Owner / next validation**: Core, Presentation, & all Client maintainers; Red dictionary-proof HMAC domain-separation tests, `SecretPolicyTightened` blind-block tests, and `en`/`ar` UI localization tests
- **Escalation**: None

### IVSD-F006: Clean-room MIT dependency hygiene, workstream boundary respect, and deferred Coolify/SecretKit boundary (`Haqq al-'Ibad` & `Amanah`)

Introducing third-party OS keyring NuGet libraries with mixed native binaries or incompatible licenses, mutating `store/islamu-event/releases/1..3.json`, colliding with active uncommitted work in `definition-schema-redesign` / queued `configuration-workspace-redesign`, or smuggling unapproved Coolify/SecretKit HTTP clients into offline Configurator would violate dependency provenance, IP stewardship (**Haqq al-'Ibad**), and explicit scope boundaries (`AGENTS.md` Rules 5, 10, 19).

- **Mechanism & Mitigation (`IVSD-M006`)**:
  1. **Zero New Runtime NuGet Packages:** Implement all `.cfgvault` cryptography cleanly via already-pinned `BouncyCastle.Cryptography` `2.7.0` (`MIT`) and standard `.NET 10` BCL primitives, and implement native keyring calls via bounded `LibraryImport` P/Invoke against standard OS libraries (`libsecret-1.so.0` / `libglib-2.0.so.0`, Win32 `crypt32.dll` / `advapi32.dll`, macOS `Security.framework`) in `Configurator.Artifacts` (`AllowUnsafeBlocks` only where required by marshaling, zero copy-pasted third-party code). Verify `.ci/scripts/validate-dependency-license-policy.cs` and `Configurator.Architecture.Tests` at exit.
  2. **Explicit Workstream Coordination:** Sequence `encrypted-local-profiles` cleanly alongside `definition-schema-redesign` (active in working tree) and `configuration-workspace-redesign` (`Workstream 2`): Core, Artifacts, and CLI profile phases touch zero active UI files, while Presentation/UI integration phases cleanly extend the finalized shell without breaking or duplicating workstream state.
  3. **Strict Offline Apply Boundary:** Emit structured, secret-free delta payloads (`ProfileDeltaSummary` JSON via CLI `--json`) that a future `Coolify` / `ISLAMU SecretKit` / `Infisical` / `AKV` provider workstream can consume later, while maintaining zero HTTP/target-credential/apply code inside Configurator today (`targetApply: false` retained in `frozen-contract-baseline.json`).
- **Lifecycle / severity / claim type**: proposed / high / licensing provenance, workstream coordination, and offline trust boundary
- **Principles / domains**: Rights of Others (`Haqq al-'Ibad`), Trust (`Amanah`) / dependency closure, release architecture, future extensibility
- **Stakeholders**: MIT downstreams, contributors on active workstreams, future target-apply integrators
- **Controlled decision**: Zero added NuGet dependencies, bounded P/Invoke OS keyring adapters, `targetApply: false` maintained strictly
- **Evidence / validation**: `E01` (`Directory.Packages.props`, `.ci/policy/dependency-licenses.json`, `StandaloneArchitectureTests.cs`, `frozen-contract-baseline.json`)
- **Linked mitigation**: `IVSD-M006`
- **Owner / next validation**: Architecture & Artifacts maintainers; `StandaloneArchitectureTests`, `PolicyGateTests`, `validate-dependency-license-policy.cs`, and `GenerateConfiguratorRatchets.cs --check`
- **Escalation**: Legal/IP escalation only if any new NuGet package or third-party native blob were ever proposed

## Common Overlooked Failures And Outcomes

1. **Silently Allowing `.cfgvault` Purpose Relabeling:** If `purpose` lives only in unauthenticated metadata outside AEAD `AAD`, an attacker with write access to disk can flip `import-upgrade-only` to `inspectable-encrypted` cleanly (`IVSD-M001` closes this by signing the canonical header as AEAD `AAD`).
2. **Storing `AES-GCM` via BCL and Failing on Browser WASM:** `.NET 10` throws `PlatformNotSupportedException` for `System.Security.Cryptography.AesGcm` / `ChaCha20Poly1305` on `browser-wasm` (`IVSD-M002` uses pure managed `BouncyCastle.Cryptography` `2.7.0` everywhere so Browser AOT and Native binaries share identical bit-level vectors).
3. **Blindly Forwarding Old Weak Secrets After Schema Tightening:** Carrying a legacy 8-char password or 128-bit secret silently into a `V_new` schema that now requires `32` bytes breaks production security (`IVSD-M005` rejects blind carry-forward on `SecretPolicyTightened` / `SensitivityEscalated`).
4. **D-Bus Headless Linux Crash on Keyring Call:** Invoking `libsecret` without runtime `NativeLibrary.TryLoad` + missing-D-Bus handling crashes SSH/Docker Desktop sessions (`IVSD-M002` degrades cleanly to `KeyringUnavailable` + Slot B passphrase prompt).
5. **Leaking Passphrases or Blind Secrets into `DefinitionFormUndoHistory` / Browser Storage:** Putting unlocked form states into Browser `IndexedDB` or naive undo snapshots subverts `DisableSecretEntry()` (`IVSD-M003`, `IVSD-M004` enforce synchronous zeroing and zero Browser persistence).

## Recommendations

1. **Adopt `ChaCha20-Poly1305` + `Argon2id` + `HKDF-SHA256` in `Configurator.Core` via `BouncyCastle.Cryptography` `2.7.0`:**
   - *Rationale:* Bit-for-bit identical cross-platform execution across Linux, macOS, Windows, and Browser WASM AOT with zero new NuGet packages.
   - *Rejected alternative:* BCL `AesGcm` (`PlatformNotSupportedException` on `browser-wasm`) or split crypto implementations per platform (maintenance risk and format drift).
2. **Adopt Multi-Recipient DEK Slots (`Slot A` OS Keyring + `Slot B` Argon2id Recovery) Over Single-Key Schemes:**
   - *Rationale:* Gives Desktop operators zero-prompt convenience (`Slot A`) while guaranteeing cross-machine portability, headless SSH/CLI usage, Browser upload/download, and disaster recovery (`Slot B`).
   - *Rejected alternative:* Keyring-only (catastrophic data loss on machine loss / headless environments) or passphrase-only (high daily UX friction triggering insecure plaintext exports).
3. **Use Immutable Per-Revision Create-Only `.cfgvault` Snapshots + Header Scanning Over Mutable Single-File Overwrites or Sidecar Indexes:**
   - *Rationale:* Preserves existing `IProtectedArtifactWriter` `O_TMPFILE` + `linkat` create-only guarantees on Linux, maintains Day-2 rollback history automatically, and prevents mutable index race corruption.
   - *Rejected alternative:* In-place overwrite (breaks `UnixProtectedArtifactWriter` contract and creates rollback risk) or separate JSON index file (sync drift with filesystem revisions).
4. **Sequence Core + Artifacts + CLI cleanly as independent foundations, then wire Presentation Blind Custody & Desktop/Terminal/Browser UI cleanly behind Workstream 2 (`configuration-workspace-redesign`) UI shell completion:**
   - *Rationale:* Prevents merge collisions in active working-tree files (`definition-schema-redesign`) and queued shell rewrites while allowing full test-driven progress.

## Stakeholders

- **Self-Hosting Operators (Desktop & Terminal):** Gain zero-friction local encrypted saves (`Slot A`), safe Day-2 schema upgrade diffs without revealing production secrets (`import-upgrade-only`), editable in-app vaults (`inspectable-encrypted`), and Day-2 secret-free comparison baselines (`compare-only`).
- **Headless / SSH / Automation Administrators (CLI):** Gain deterministic machine-readable `profile save|unlock|diff|upgrade-plan|slots` commands authenticated via explicit passphrase/file-descriptor input and secret-free delta JSON output ready for future PaaS pipelines.
- **Air-Gapped & Browser WASM Operators:** Gain zero-persistence in-memory `.cfgvault` upload, passphrase unlock, upgrade planning, and deliberate encrypted file export under strict per-workspace secret consent.
- **Arabic (`ar`) & Multilingual Administrators:** Gain full RTL-isolated, localized explanations of security modes, blind-secret status, recovery prompts, and schema breaking changes.

## I-VSD Principles And Domains

- **Amanah (Trustworthy Stewardship):** Multi-recipient key wrapping (`DEK` + `Slot A`/`Slot B`), `BlindSecretSlot` isolation, synchronous `ZeroMemory`, immutable create-only native writes (`IVSD-F001`–`IVSD-F004`, `IVSD-F006`).
- **Sidq (Truthfulness):** Honest distinction between cryptographic secret exclusion (`compare-only`) and runtime UI protection (`import-upgrade-only`), AEAD header-as-AAD tamper verification, keyed HMAC unavailability transparency, and `SecretPolicyTightened` blind rejection (`IVSD-F001`, `IVSD-F004`, `IVSD-F005`).
- **Bayan (Clear Disclosure):** Explicit security-mode labels, Slot-B recovery nudges when relying on OS keyring alone, and unambiguous Browser temporary custody notices (`IVSD-F001`, `IVSD-F002`, `IVSD-F004`).
- **La Darar (Non-Harm):** Mandatory portable recovery slot path (`Slot B`), replacement confirmation prompts before overwriting blind secrets, create-only no-clobber writes, and fail-closed unsupported-host handling (`IVSD-F002`–`IVSD-F004`).
- **Adl (Justice & Equity):** Complete `en`/`ar` RTL localization and equivalent Day-2 upgrade clarity across Desktop, Terminal, CLI, and Browser (`IVSD-F005`).
- **Haqq al-'Ibad (Rights of Others):** Clean-room first-party crypto/keyring code, zero unapproved third-party dependencies, preserved release history (`IVSD-F006`).
- **Non-Applicable Domains:** Monetization, riba/commerce, user ranking/feeds, AI inference/moderation, server multi-tenancy, and cloud authentication do not apply to offline Configurator.

## Validation Gaps

1. **Planning Phase Only (Zero Execution Yet):** All findings rest on static code review and standards analysis on `2026-11-13`. Cryptographic round-trips, AEAD tamper rejection, Argon2id memory/latency on Browser WASM AOT, Linux `libsecret` / create-only `0600` filesystem races, and blind-secret UI isolation remain unverified until Red/Green execution.
2. **Non-Linux Protected-Save Hosts (`Windows`, `macOS`):** `ProtectedArtifactWriter.IsAvailable` is currently `true` only on Linux (`Configurator.Release.proj` `protectedExportHosts: ["linux"]`). Windows DPAPI / macOS Keychain adapters can be designed behind clean platform interfaces (`[SupportedOSPlatform]`), but real host qualification stays `Planned` until native CI/runners validate protected writes there.

## Escalation Needed

- **Scholarly escalation:** None required.
- **Specialist security / release escalation:** Security review prior to release if (a) hardware FIDO2/WebAuthn PRF key slots are later added, (b) native Windows/macOS protected-artifact writers move from `Planned` to `Supported`, or (c) the future Coolify / `ISLAMU SecretKit` remote-apply workstream begins.

## Evidence Reviewed

- `E01` (Repository Contracts & Code): `AGENTS.md`, `PROJECTS.md`, `docs/internal/{QUICK_REFERENCE,PROTECTED_ARTIFACTS,SECRET_CRYPTOGRAPHY,LAUNCH_CONTRACT,STATUS,ARCHITECTURE,DESKTOP_CLIENT}.md`, `docs/internal/adr/ADR-encrypted-local-profiles-and-target-apply.md`, `src/Configurator.Core/{Definitions/AnswerSnapshot*,Definitions/Definition.cs,Secrets/Secret*}`, `src/Configurator.Artifacts/{ProtectedArtifactWriter,UnixProtectedArtifactWriter}.cs`, `src/Configurator.Presentation/{Secrets/DefinitionSessionPrivacy,Import/DefinitionImportState,Form/DefinitionFormModel}.cs`, `src/Configurator.Cli/Commands/{CliCommandSchemaMetadata,Artifacts/*,Secrets/*}.cs`, `eng/configurator/{Configurator.Release.proj,GenerateConfiguratorRatchets.cs}`, `tests/Configurator.Architecture.Tests/StandaloneArchitectureTests.cs`.
- `E02` (External Specifications & Standards): RFC 8439 (`ChaCha20-Poly1305`), RFC 9106 (`Argon2id`), RFC 5869 (`HKDF`), NIST SP 800-107 (HMAC domain separation), OWASP Password Storage Cheat Sheet (`>= 19 MiB` / `>= 2` iterations floor), FreeDesktop Secret Service D-Bus / `libsecret` API, Windows `CryptProtectData` / Credential Manager API, Apple `Security.framework` Keychain API, `.NET 10 browser-wasm` cryptography `PlatformNotSupportedException` profile.

## Missing Evidence

- Runtime TUnit pass logs for Core crypto/schema-diff tests, Linux `Artifacts.Tests` create-only & `libsecret` tests, `Presentation.Tests` blind-custody/undo-purge tests, CLI schema/ratchet freshness checks, Desktop headless/`NativeHost` checks, Terminal checks, and published Browser WASM AOT execution of Argon2id/ChaCha20-Poly1305.

## Context Inventory

- Active checkout holds uncommitted working-tree edits from Workstream 1 (`dev/active/definition-schema-redesign/`) and queued UI plan `dev/active/configuration-workspace-redesign/` (`Workstream 2`).
- This workstream (`encrypted-local-profiles`) coordinates cleanly with both: Core/Artifacts/CLI phases have zero overlap with Workstream 2 UI files, and Presentation/UI phases integrate into the completed Workstream 2 shell without reverting any Workstream 1 or Workstream 2 contracts.

## Planning Handoff
- Workstream: encrypted-local-profiles
- Status: current
- Reviewed input: `dev/active/encrypted-local-profiles/{encrypted-local-profiles-plan,encrypted-local-profiles-context,encrypted-local-profiles-tasks}.md` (`rev-1`, `2026-11-13`)
- Findings and mitigations: `IVSD-F001 -> IVSD-M001`, `IVSD-F002 -> IVSD-M002`, `IVSD-F003 -> IVSD-M003`, `IVSD-F004 -> IVSD-M004`, `IVSD-F005 -> IVSD-M005`, `IVSD-F006 -> IVSD-M006`
- Required plan mappings:
  - `IVSD-F001 / IVSD-M001` -> Scenarios `S03`, `S04`, `S12`; Phases `1`, `5`, `6`, `7` (`TASK-1.1`–`TASK-1.4`, `TASK-5.1`, `TASK-6.1`–`TASK-6.4`, `TASK-7.1`–`TASK-7.2`)
  - `IVSD-F002 / IVSD-M002` -> Scenarios `S01`, `S02`, `S08`; Phases `1`, `3`, `6`, `7` (`TASK-1.1`–`TASK-1.2`, `TASK-3.1`–`TASK-3.4`, `TASK-6.1`–`TASK-6.4`, `TASK-7.1`–`TASK-7.2`)
  - `IVSD-F003 / IVSD-M003` -> Scenarios `S05`, `S07`, `S11`, `S13`; Phases `5`, `6`, `7` (`TASK-5.1`–`TASK-5.3`, `TASK-6.1`–`TASK-6.4`, `TASK-7.1`–`TASK-7.2`)
  - `IVSD-F004 / IVSD-M004` -> Scenarios `S09`, `S10`, `S11`, `S12`; Phases `3`, `4`, `6`, `7` (`TASK-3.1`–`TASK-3.2`, `TASK-4.1`–`TASK-4.2`, `TASK-6.1`–`TASK-6.4`, `TASK-7.1`–`TASK-7.2`)
  - `IVSD-F005 / IVSD-M005` -> Scenarios `S04`, `S06`, `S14`, `S15`; Phases `1`, `2`, `4`, `5`, `6` (`TASK-1.3`–`TASK-1.4`, `TASK-2.1`–`TASK-2.4`, `TASK-4.1`–`TASK-4.2`, `TASK-5.3`, `TASK-6.1`–`TASK-6.4`)
  - `IVSD-F006 / IVSD-M006` -> Scenario `S16`; All Phases + Phase `4`, `7` (`TASK-4.3`, `TASK-7.3`, `TASK-7.4`, `VERIFY-1`–`VERIFY-7`)
- Escalations required before: release (native Windows/macOS protected-save qualification remains `Planned`; remote Coolify/SecretKit apply remains deferred).
- Refresh triggers: Any change that (1) weakens Argon2id floor parameters or omits header-as-AAD binding, (2) allows deterministic machine-ID fallback without explicit passphrase or OS keyring, (3) exposes `BlindSecretSlot` characters to UI reveal/copy/undo/diagnostics/Browser storage, (4) introduces non-create-only overwrites on native storage or persistence in Browser WASM, (5) allows blind secret carry-forward across tightened secret policies, or (6) adds network apply/telemetry or unapproved NuGet packages.

## Review Lifecycle

| Date | Previous status | New status | Trigger | Evidence/replacement |
| --- | --- | --- | --- | --- |
| 2026-11-13 | `none` | `draft` (`awaiting-plan`, `r1`) | Initial planning intake from Turn 1–4 decisions and ADR | `E01`–`E02` static code and specification review |
| 2026-11-13 | `draft` (`awaiting-plan`) | `current` (`plan-aligned`, `r1`) | Revalidated against completed `dev/active/encrypted-local-profiles/` triad (`rev-1`) | Scenarios `S01`–`S16` and `TASK-1.1`–`TASK-7.4` bound cleanly across Phases 1–7 |
