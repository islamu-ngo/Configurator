# ADR: Encrypted Local Profiles, Upgrade Modes, and Target Delta Apply

Status: Proposed; future design plan for encrypted local profiles, Day-2 upgrade diff modes, and secret-manager/PaaS (Coolify) delta apply — not implemented.
Date: 2026-11-12
Owners: Core, Artifacts/Security, Presentation, and native client maintainers.
Technical anchors: [Core contract](../CORE_DEFINITION_CONTRACT.md), [Secret cryptography](../SECRET_CRYPTOGRAPHY.md), [Protected artifacts](../PROTECTED_ARTIFACTS.md), [Import/comparison](../IMPORT_COMPARISON.md), [Launch/custody](../LAUNCH_CONTRACT.md), [Desktop client](../DESKTOP_CLIENT.md).
Backlog intake: [Configuration workflows beyond the offline MVP](../../../dev/backlog/universal-configurator-followups.md).

## Context

Operators currently prepare candidate configuration artifacts via `DefinitionArtifactComposer` / `DefinitionDraftArtifactComposer`, write native outputs via [`IProtectedArtifactWriter`](../PROTECTED_ARTIFACTS.md), or run read-only comparisons against ordinary exported `.env`, JSON, and YAML files via [`DefinitionImport`](../IMPORT_COMPARISON.md) ([evidence-aware import/comparison ADR](ADR-artifact-import-comparison.md)).

When upgrading self-hosted software across breaking schema releases—or managing targets that treat secrets as write-only (such as Coolify `is_shown_once` environment variables or API tokens scoped to `read` + `write` without `read:sensitive`, or external secret managers like Infisical and Azure Key Vault)—plain exported files fall short in two ways:

1. **Unencrypted persistence risk:** Storing plaintext `answers.json` or `.env` snapshots on disk (`~/.config/islamu-configurator/profiles/`) exposes database credentials and API keys to casual disk exposure or accidental repository commits.
2. **Write-only target blind spot:** Once secrets live inside a PaaS or vault that intentionally redacts read-back, Configurator cannot retrieve old secret values from the live target during a Day-2 breaking-change upgrade unless it maintains a local schema-bound snapshot or commitment manifest.
3. **Different security tolerances:** Operators need distinct trust postures depending on workflow:
   - True cryptographic non-readability when only schema comparison and target delta patching are needed (`compare-only`).
   - Encrypted offline secret carry-forward across schema upgrades without casual UI plaintext exposure (`import-upgrade-only`).
   - Encrypted-at-rest local convenience where an unlocked native app session can inspect and tweak saved settings (`inspectable-encrypted`).
   - Standard unencrypted export when intentionally materializing target files (already implemented).

At the same time, Browser WebAssembly (`Configurator.Browser`) must preserve its strict zero-persistence custody guarantee ([`LAUNCH_CONTRACT.md`](../LAUNCH_CONTRACT.md#transient-answer-and-output-boundary))—never caching answers or encrypted snapshots in `localStorage`, `IndexedDB`, `OPFS`, or service workers.

## Decision (Future Plan)

### 1. Support Four Explicit Persistence and Export Tiers

Configurator will model four explicit, mutually distinct persistence/export modes rather than a single generic "encrypted save":

| Mode | Stored Secret Material | Readability / UI Posture | Primary Workflow & Truth Guarantee |
| --- | --- | --- | --- |
| **`compare-only`** | **Zero secret plaintext.** Secrets are stripped before disk serialization. | **Cryptographically impossible** to read old secrets from disk. | Retains `familyId`, `revision`, `digest`, `applicationVersion`, scope/omissions, public values, schema secret policy-compliance summaries, and optional non-reversible keyed HMAC-SHA256 rotation commitments. Powers Day-2 schema diffs, secret-deferred drafts (`DefinitionDraftArtifactComposer`), and delta-only PaaS/vault updates. |
| **`import-upgrade-only`** | Full `AnswerSnapshot` encrypted via AEAD inside a split-plane envelope (`.cfgvault`). | **Blind-forwarding UI:** app carries unchanged secrets forward into upgraded outputs/targets without reveal/copy/raw-dump actions. | Honest boundary (*Sidq*): blocks casual UI inspection/exfiltration and authenticates `purpose` in AEAD AAD, though the local host/user holding the unlocking key controls runtime process memory during upgrade. |
| **`inspectable-encrypted`** | Full `AnswerSnapshot` encrypted via AEAD inside the same `.cfgvault` envelope (`purpose: "inspectable-encrypted"`). | **Unlocked-in-app inspection:** encrypted on disk, viewable and editable inside the authenticated Desktop/Terminal session while open. | Convenience tier for solo operators who want disk encryption without losing visual profile editing/inspection inside Configurator. |
| **Unencrypted Export** | Plaintext rendered artifacts or answers (`DefinitionArtifactComposer`). | Already implemented. | Linux create-only native output via `IProtectedArtifactWriter` or deliberate Browser copy/download. |

*(Future extension note: an asymmetric **`sealed-target`** tier using a remote deployment agent or cluster X25519 public key can later provide true cryptographic local unreadability even while bundling secrets for automated CI/PaaS push.)*

### 2. Split-Plane Authenticated Envelope and Tamper-Proof Purpose Binding

All encrypted local snapshots use one canonical envelope (conceptual `.cfgvault`, `vaultContractVersion: "0.0.1"`) built strictly on Configurator's existing cryptographic stack (`BouncyCastle.Cryptography` `2.7.0` + .NET 10 `System.Security.Cryptography`, adding zero new crypto packages):

1. **Authenticated Header (AEAD Associated Data / `AAD`):**
   - Contains `vaultContractVersion`, `purpose` (`compare-only`, `import-upgrade-only`, `inspectable-encrypted`), `definition` (`familyId`, `revision`, `digest`, `applicationVersion`, `environmentLabel`), `createdUtc`, KDF metadata, and wrapped key-recipient slots.
   - In `compare-only`, secret presence/policy summaries and keyed rotation commitments reside in metadata; no secret ciphertext block is emitted.
   - Do **not** place user-supplied dynamic map keys or arbitrary unknown keys into unencrypted metadata (preserving `IMPORT_COMPARISON.md`'s private-key non-disclosure rule).
2. **Encrypted Payload (`import-upgrade-only` and `inspectable-encrypted`):**
   - Fresh random 256-bit Data Encryption Key (DEK) and fresh nonce per saved snapshot, encrypted with `ChaCha20-Poly1305` or `AES-256-GCM`.
   - Canonical header bytes (including `purpose` and `definition.digest`) are bound as AEAD `AAD` so modifying `purpose: "import-upgrade-only"` to `"inspectable-encrypted"` on disk fails authentication.
   - Canonical `AnswerSnapshotCodec` JSON bytes are decrypted only into scoped buffers and wiped in `finally` via `CryptographicOperations.ZeroMemory`.

### 3. Convenience-First Multi-Recipient Key Wrapping

To satisfy zero-friction Desktop workflows (`configurator://` `/diff` auto-matching and instant environment switching) without trapping profiles on a single OS installation, wrap each snapshot DEK across explicit recipient slots (analogous to `age` multi-recipient wrapping):

- **Slot A — Primary Convenience (OS Keyring KEK, Native Desktop):**
  - Windows: DPAPI (`System.Security.Cryptography.ProtectedData`, `DataProtectionScope.CurrentUser`).
  - macOS: Keychain Services (`Security.framework`).
  - Linux Desktop: freedesktop Secret Service API (`libsecret` over D-Bus).
  - Unlocks managed profiles automatically within the authenticated OS user session.
- **Slot B — Portable / Headless Fallback (Argon2id Passphrase KEK):**
  - Reuses Configurator's verified `Argon2id` profile (`>= 19456 KiB`, `passes >= 2`, `parallelism >= 1`) from [`SECRET_CRYPTOGRAPHY.md`](../SECRET_CRYPTOGRAPHY.md#algorithms-and-formats).
  - Supports cross-machine backup/restore, headless SSH sessions (`Configurator.Terminal` / `Configurator.Cli` when D-Bus is absent, alongside optional Linux `systemd-creds --user`), and explicit Browser `.cfgvault` portability.

### 4. Recommended Storage Lifecycle: Immutable Per-Revision Create-Only Snapshots

Managed native profiles (`~/.config/islamu-configurator/profiles/<app-id>/<env>/`, or OS-idiomatic config directory) **must preserve `IProtectedArtifactWriter`'s create-only invariant** by storing **immutable per-revision `.cfgvault` files** (e.g., `<revision>-<digest8>.cfgvault` or monotonic generation filenames) paired with a non-secret metadata index rather than overwriting a single mutable file in place.

**Rationale:**

1. Keeps Configurator's existing Linux create-only, no-replace publication (`O_TMPFILE` + `linkat` + `statx` owner/mode verification) intact without weakening `Configurator.Artifacts` boundaries or risking partial-write corruption of the sole working snapshot.
2. Preserves automatic **Day-2 rollback history** (`v1.9.0` remains untouched on disk if a `v2.0.0` migration encounters target rejection).
3. Matches `Definition`'s immutable `(familyId, revision, digest)` identity model and keeps snapshot retention/pruning an explicit user action.

### 5. Browser Custody Boundary

`Configurator.Browser` retains **zero persistent answer storage**:

- No encrypted or unencrypted snapshots are persisted to `localStorage`, `IndexedDB`, `OPFS`, or service worker caches (avoiding both `LAUNCH_CONTRACT.md` violations and the same-origin XSS decryption-oracle risk of stored WebCrypto keys).
- Browser users participate in the workflow only via **explicit file download/upload** of passphrase-wrapped `.cfgvault` files (using `BouncyCastle` Argon2id + AEAD ephemerally in WASM memory) and `/diff` drop-zone comparison.

### 6. Deferred Target Integration: Coolify and Secret Managers (Future Workstream)

Direct target integration is **deferred** to a dedicated secret-management and PaaS-apply workstream after local profile storage and Day-2 schema-evolution diffing mature. That workstream will target two primary patterns strictly confined to native clients (`Desktop`, `Terminal`, `Cli`), never `Core` or `Browser`:

1. **Coolify REST API (`PATCH /api/v1/applications/{uuid}/envs/bulk`):**
   - **3-Way Reconciliation:** Compare local snapshot (`v1.9.0`) against target definition (`v2.0.0`) offline in `Core`, then query live Coolify variable names/flags (`GET /api/v1/applications/{uuid}/envs`) using a `read` + `write` API token (without requiring `read:sensitive`).
   - **Delta Apply:** Push only added/changed variables (`is_shown_once: true` for secrets, `is_literal`, `is_multiline`, `is_buildtime`/`is_runtime` flags), leave existing write-only secrets untouched when upgrading from a `compare-only` snapshot, and require explicit operator confirmation before deleting deprecated keys.
2. **Infisical and Azure Key Vault (`ISLAMU SecretKit`):**
   - Native sync via official provider CLIs (`infisical-cli`, `az`) or `SecretKit.Management` APIs, strictly operator-triggered and defaulting to zero network activity.

## Consequences and Alternatives

- **Single encrypted format without `compare-only` rejected:** Storing encrypted secrets everywhere prevents cryptographic non-readability when operators already keep secrets in Coolify (`is_shown_once`) or Infisical and only need local schema diffing.
- **In-place mutable vault file rejected:** Overwriting one `answers.vault` file violates `IProtectedArtifactWriter`'s create-only safety posture and destroys rollback history on interrupted upgrades.
- **Browser `IndexedDB` / WebAuthn PRF local vault rejected for now:** Same-origin XSS can invoke stored non-extractable keys as decryption oracles, and WebAuthn PRF roaming-key behavior still varies across mobile/browser combinations. Explicit passphrase `.cfgvault` download/upload preserves strict air-gapped Browser silence.
- **Honest security communication (*Bayan* / *Sidq*):** UI copy and docs must clearly state that `import-upgrade-only` hides plaintext in UI/exports and tamper-locks file `purpose`, whereas `compare-only` is the only local mode with **zero stored secret bytes**.
- **Cross-platform keyring qualification:** Windows DPAPI, macOS Keychain, Linux `libsecret`, and `systemd-creds` adapters will require separate per-host native verification per [`STATUS.md`](../STATUS.md#protected-save) before claiming host support.

## Verification Before Acceptance

Before promoting this record from `Proposed` in its future implementation workstream:

1. Require failing invariant tests proving `compare-only` envelopes contain zero secret plaintext bytes, even for dynamically authored maps or failed secret policies.
2. Prove AEAD `AAD` verification rejects `.cfgvault` files whose `purpose` or `definition.digest` header is modified on disk.
3. Prove `OperationGeneration` fencing, consent revocation, and error cleanup zero unwrapped DEKs and secret buffers and block late adoption.
4. Prove Linux create-only `IProtectedArtifactWriter` semantics preserve prior revision snapshots and reject concurrent target conflicts.
5. Verify Browser WASM `.cfgvault` upload/download retains zero bytes in `localStorage`, `IndexedDB`, `OPFS`, and network telemetry.
6. Qualify Coolify (`read` + `write`, `is_shown_once`, bulk delta) and Infisical/`SecretKit` target apply against live test fixtures with explicit operator-confirmation gates before marking target integration supported.
