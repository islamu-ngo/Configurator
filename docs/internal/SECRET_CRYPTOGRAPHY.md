# Secret cryptography

> **Audience:** Contributors | Integrators
> **Kind:** Living contract
> **Status:** Core profiles verified on Linux x64 and the published browser AOT probe; product qualification is recorded separately
> **Owner:** Core
> **Last Verified:** 2026-11-02
> **Source Anchors:** `src/Configurator.Core/Secrets/`, `src/Configurator.Core/Profiles/`, `src/Configurator.Artifacts/Profiles/`, `Directory.Packages.props`

The typed definition policy is the authority for generation and validation.
Core performs local operations only; valid syntax and locally generated material
do not establish target acceptance, issuer trust or administrative authority.

## Algorithms and formats

Passwords use a distinct ASCII alphabet filtered by explicit exclusions,
required category draws and a cryptographic Fisher–Yates shuffle. The conservative
entropy bound counts only unconstrained alphabet draws; it deliberately credits
neither category draws nor shuffling. Recipes require at least 128 bits under
that bound, at most 1024 characters and no unbounded retry loop. Entered passwords
receive structural checks, never an entropy claim or implicit normalization.

Opaque tokens and HMAC/AES keys use cryptographic random bytes, at most 4096.
Encoding is canonical padded Base64, unpadded Base64url or lowercase hexadecimal.
HMAC minima are 32/48/64 bytes for HS256/HS384/HS512; AES128/192/256 require
exactly 16/24/32 bytes. An opaque token has at least 16 random bytes.

Argon2id uses version 19 and a PHC envelope with unpadded standard Base64 salt
and hash. Memory is 19456–65536 KiB, passes 2–6, parallelism 1–4, with defaults
19456/2/1. Generic PBKDF2 uses `configurator-pbkdf2-v1`: a JSON string containing
exactly version 1, algorithm, iterations, padded Base64 salt and derivedKey.
SHA256 requires at least 600000 iterations; SHA512 at least 210000; both cap at
2000000. Configurable salts are 16–64 bytes and subkeys 16–64 bytes.

ASP.NET Identity V3 uses a 61-byte envelope: marker 1, big-endian PRF 2,
iterations 210000, salt length 16, fresh 16-byte salt and 32-byte subkey.
Bcrypt emits `$2b$`, cost 10–14, with a fresh 16-byte salt; input exceeding
72 UTF-8 bytes or containing U+0000 rejects. The restored 2.7.0 API accepts
character arrays, so the adapter strictly decodes the already bounded UTF-8
input and zeroes that temporary array after the call. SHA256/384/512 digest output has
the algorithm's complete fixed length. Digests are not password hashes.

Private keys use RSA2048/3072/4096 or P-256/P-384/P-521. The wire format is
unencrypted PKCS#8 PEM or private JWK. EC x/y/d coordinates have exactly
32/48/66 unsigned big-endian bytes, including leading zeros; the public point
must equal d·G. RSA CRT values are minimal unsigned Base64url integers, with
odd e ≥ 65537 and n = p·q. Public-only and encrypted containers reject.
The bounded PKCS#8 profile accepts version-zero containers without attributes,
two-prime RSA and named-curve EC. EC scalar width is fixed; an optional embedded
public point must be uncompressed and match the private scalar. Explicit curve
parameters and compressed embedded points are outside this profile.

## Resource and custody boundaries

Validation inspects bounded syntax and policy; it never runs an imported hash's
KDF or verifies a password. Hash envelopes cap at 4096 characters and key inputs
at 64 KiB. Expensive generation is an explicit operation, fenced by Presentation
against changes, cancellation and consent revocation. Bounded synchronous crypto
may finish after cancellation; its stale result must be discarded.

`SecretGenerationResult` owns separate disposable final output and optional
original generated password. Original plaintext is never part of the answer
artifact by default. Mutable character/byte buffers are zeroed with
`CryptographicOperations.ZeroMemory`; immutable strings and library internals
cannot be promised complete erasure. Clipboard custody remains separate.
JWK serialization reserves 8192 bytes up front for the bounded RSA/EC formats,
avoiding abandoned secret-bearing buffers during buffer growth; the written
buffer is zeroed after encoding.

## Encrypted local profile container

The local-profile workstream is being integrated. The Core/native container
boundary is separate from existing secret generation; client workflows, keyring,
restore and package qualification remain their own gates.

`IProfileContainerCodec` is one host-independent facade. Core admits authenticated
application payloads and owns retained plaintext through `UnlockedProfile`.
Artifacts implements the native facade with `jose-jwt` 5.3.0. The selected Browser
implementation is a separate dedicated-worker integration, not a Core dependency.

The single format is RFC 7516 JSON JWE, admitting its general and flattened
serializations with exact separate root shapes and identical recipient policy.
Mixed shapes and compact serialization reject before derivation. It uses A256GCM content encryption and
PBES2-HS512+A256KW recipients with PBKDF2-HMAC-SHA512 exactly 600000 iterations.
There is no algorithm menu or fallback. The maintained library creates content
keys, nonces, salts and wrapped recipients. The CPU-hard derivation choice is not
equivalent to memory-hard Argon2id and cannot make a weak human passphrase strong.

Admission limits encrypted input/output to 4 MiB and plaintext to 2 MiB, with at
most eight recipients. Strict framework JSON rejects duplicate/colliding headers,
depth above eight, unknown fields, external AAD, compression and alternate
algorithms. Require canonical base64url, library-created salts of 12-64 bytes,
12-byte IV and 16-byte tag. Exactly one selected recipient is attempted on open;
an unrelated working recipient never causes an automatic retry. Native instances
share one nonqueued execution permit, held until physical work finishes.

Native device material is a random 32-byte value represented as Base64 text;
portable passphrases use exact valid UTF-8 without normalization. Creation
requires at least 15 Unicode scalars and at most 1024 UTF-8 bytes. Length is not
an entropy claim. Invalid UTF-8 is rejected rather than replaced.

Authenticated payloads require product contract 0.0.1, the configuration-profile
purpose, an exact admitted encrypted mode, nonempty profile/snapshot identities,
actual admitted definition content, private source/environment context and the
bounded answer envelope. Compare-only and ordinary unencrypted target output are
separate modes. `InspectAnswers` rejects import-upgrade-only payloads; ordinary
serialization of an unlocked owner contains no retained answers. Clients must
also establish workspace consent and preserve operation-generation fencing.
The authenticated `comparisonKey` member is mandatory and nullable. A present
key contains only its nonempty ID and canonical Base64 encoding of 32 independent
bytes. `CompareBaselineCodec.Create(UnlockedProfile)` can use that key inside
scoped Core processing without publishing it or the retained answers. It
rechecks owner liveness before returning the stripped result. Transient immutable
validator/snapshot allocations remain subject to the stated runtime erasure
limit; they never become the retained blind owner.

Routing labels and the whole recipient set are not authenticated authority.
Native unlock does not verify unused recovery recipients. Recovery verification
must independently exercise the exact copied file with the native material
unavailable; its reference digest must itself be trusted. Resealing cannot revoke
old copies or rotate target credentials.

Cancellation rejects adoption after native work returns, clears owned copies
and keeps the execution permit until then. Native library string inputs and
framework/library allocations do not have a complete-erasure guarantee. This
boundary does not itself establish workspace fencing, protected save, a working
keyring or restore.

The [dependency record](legal/dependencies/encrypted-local-profiles.md) binds
original MIT terms and exact package identities. Provider and temporary AOT
worker receipts are not product integration or an external security audit.

## Stripped profile comparisons

`CompareBaselineCodec` produces bounded `CompareBaselineSnapshot` data from an
admitted definition and supplied answers. It uses `DefinitionSensitivityPolicy`
and the admitted variant selector; public descendants cannot lower an ancestor's
sensitivity. It retains fixed public properties and a structural type projection,
without schema defaults, examples or other raw keyword data. Dynamic map names
are omitted. Collections containing protected descendants are omitted as a whole;
there are no ordinal secret commitments that could attach to a different row.
Missing values are never replaced with effective defaults.

Optional `ProfileSecretCommitment` owns a separately supplied 32-byte key.
Bouncy Castle HMAC-SHA256 receives a JSON-array frame containing a versioned
purpose, key ID, profile ID, definition family, exact environment context, fixed
schema path and canonical value. A separate context frame prevents cross-context
equality claims. Dynamic maps and protected collections can have one commitment
at their fixed container path; private keys and row paths never become labels.
The baseline contains only key ID, context tag and commitments. Neither its key
nor raw private environment context is exported.

Equality requires the actual held key, matching key/profile/family/context and
an available fixed-path commitment. Otherwise it is unavailable. Key rotation
does not preserve comparison authority. These tags disclose equality/linkability;
public answers can still reveal operational information. The stripped structure
cannot reconstruct the old definition or restore omitted values. Full schema
comparison requires independently admitted matching definition content.
Owned key/framing buffers are cleared; managed/library copies retain the
documented runtime erasure limits.

## Dependency and independent-design record

The existing secret-generation dependency is `BouncyCastle.Cryptography` 2.7.0, NuGet repository commit
`4007498b13582d90ee1eda5d9920c324428b98b3`, with no transitive packages. Package
metadata declares MIT. Original terms are retained under
`eng/configurator/dependency-notices/bouncycastle.cryptography/2.7.0/`.
NuGet content SHA512 is
`U+12df8UEWHgBi04YVf/Lgi2dy3SItlIYvHjjEVa/BngCQIzDCDRBk50DDByCfDvSbe5pRNFr3b7UrVK2kMcLw==`.
Nuspec SHA256 is `89c51e6b51c6d804b0ca2bfc55e00e797ef5e5d79656680d769616995832efc1`;
license SHA256 is `be0592a6c76f398356fbf252a4fb43476fb3ca3c45331428c2f4f22c499e8311`.

The source-free handoff is the workstream's typed policy and observable wire
formats. External facts constrain algorithm identifiers, encodings and public
library APIs. Repository-owned structure separates definition admission,
validation, generation and workspace custody; no third-party implementation,
tests, prose or expressive organization was copied. AFC review filters standard
formats and API signatures as interoperability constraints; discretionary
decomposition follows existing Core/Presentation ownership. The copied license
is retained legal material under its own terms, not MIT relicensing.

## Source register and qualification

Primary sources checked 2026-11-02:

- [Bouncy Castle 2.7.0 release](https://www.bouncycastle.org/resources/new-release-bouncy-castle-c-2-7-0/): approved release identity and security hardening announcement.
- [NuGet package](https://www.nuget.org/packages/BouncyCastle.Cryptography/2.7.0): exact package, terms and dependency closure, cross-checked against restored metadata.
- [.NET PBKDF2 API](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes.pbkdf2?view=net-10.0): static span-based derivation API.
- [RFC 7518](https://www.rfc-editor.org/rfc/rfc7518): RSA and EC JWK integer/coordinate contracts.
- [RFC 9106](https://www.rfc-editor.org/rfc/rfc9106): Argon2id parameters and test-vector specification.

`dotnet list src/Configurator.Core/Configurator.Core.csproj package --vulnerable
--include-transitive` completed on 2026-11-02 with no vulnerable packages reported
by the configured NuGet source. This is an advisory snapshot, not proof of
absence of vulnerabilities. Browser AOT probe and product publication are
separate qualification gates; no device performance claim follows from source
tests or a successful restore.

Native Core verification on 2026-11-02: Release rebuild completed with zero
warnings/errors; generation 5/5, derivation 18/18 and asymmetric-key 15/15
behavioral cases passed with zero skips. Private keys were independently imported
by the native BCL and exercised through signing/verification. This evidence does
not qualify a target application's semantic acceptance.

The separately published AOT probe executed all 37 profiles in Chrome with
.NET 10.0.10 WebAssembly on the same Linux x64 workstation. Individual observed
operations took 0–4339 ms; observed managed heap sizes were 4.32–26.82 MB.
These are observations, not peak-memory measurements or guarantees for other
devices. Network inspection showed only public local asset GET requests.
Product UI, package and host evidence belongs to the
[schema-driven secrets execution record](OPERATIONS.md#schema-driven-secrets-qualification-2026-11-02).

Independent synthetic derivation vectors were produced with the installed
Argon2 CLI (`20190702-7.1`) and OpenSSL (`3.6.4-1.1`). Tests retain the resulting
fixed public fixture expectations; no real credential is used. The Argon2
vector uses 19456 KiB, two passes, one lane and 32 output bytes. The PBKDF2
vector uses SHA256, 600000 iterations and 32 output bytes. These exercise the
whole adapter and serialization, separately from self-validation.
