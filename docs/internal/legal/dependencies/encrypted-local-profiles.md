# Encrypted profile container dependencies

> **Owner:** Core / Artifacts / Browser
> **Reviewed:** 2026-11-13
> **Disposition:** Native and exact Browser asset additions approved for local integration; product qualification and publication remain separate

## Reviewed components and authority

The implementation owner selected these maintained implementations under the
authorized encrypted-local-profiles workstream. This records a technical
dependency decision, not user approval of a particular archive hash, an external
security audit or publication authority.

| Component | Version | Role | Terms | Disposition |
| --- | --- | --- | --- | --- |
| NuGet `jose-jwt` | 5.3.0 | Native Artifacts runtime container API | MIT | Approved addition; verify the restored graph before functional use |
| npm `jose` | 6.2.17 | Browser worker container API | MIT | Exact one-component closure approved by the implementation owner under the authorized workstream |

Existing `BouncyCastle.Cryptography` and `YamlDotNet` dependencies retain their
existing identities and reviews. The selected native `net6.0` jose-jwt asset
declares no package dependencies. Its addition therefore contributes one node
to Artifacts and the native client/test closures. Actual restored locks and
published inventories remain authoritative; no central declaration proves that
a dependency ships in every client.

## Exact identities and original terms

The native package is bound to repository commit
`20abc4ebf916e1e36586d67609c17d045af1d503` and archive SHA-512:

```text
2RAlrBmpOt+f0dC76BY7f+W56bj638c8EkG8pug91Y2ygQ84CpLw3+q4dZXJArjSBzzPGjUlr5SQavGD87ER9w==
```

Its original `LICENSE` retains Copyright (c) 2014-2024 dvsekhvalnov. SHA-256 is
`8b8d71198e5b98e2e54282781c53e19842d53d4f26fcfa4f85cb71c0ece7b4c1`.
The qualified native DLL SHA-256 is
`f568845a3ebe60bc07072cdd6a0eefec1ad5978b8e06d842c44c0d24833cc98f`.

Native restore reproduced that archive, license and DLL. Its generated
NuGet lock/content identity is
`Doidvhyg6TN77F6HPL6Ry0KkEz6tnOUlAWfxOMSSESLU6vQSDJerX5/mr8k4xv2kA05W2Y8Ji0HXLPJM+02W9w==`;
keep this distinct from the raw archive SHA-512 above. The restored Artifacts
runtime graph contains jose-jwt 5.3.0 plus the unchanged Core closure,
BouncyCastle.Cryptography 2.7.0 and YamlDotNet 18.1.0. Native caller locks are
regenerated through their actual project graphs, not hand-edited.

The Browser archive SHA-512 is:

```text
wOuK9V1yujQFH5S0ZWmLcDZN7+IKh9bUCC2ZPCf2HDlol3WWHy+PPyNjEI75FXRLMp3vDKQcupP59xhVU95hcg==
```

Its original `LICENSE.md` retains Copyright (c) 2018 Filip Skokan. SHA-256 is
`8078b0829d6c3e9ecde2f003e1966e4cad3efc6e7a640efbab0313cc166b1af1`.
The native-generated v3 lock at `eng/configurator/profile-worker/package-lock.json`
resolves exactly the root and `node_modules/jose`, with no transitive or build npm
dependencies. Its SHA-256 is
`e0cc75ea65cd7c27dbaeb048dc95a09ce01693f7eb4f175cc26a9f8faed507bb`.
The resolved archive is `https://registry.npmjs.org/jose/-/jose-6.2.17.tgz`.
The independent profile asset tool uses Node `26.13.0`, npm `12.2.2` and Bun
`1.4.2`; Bun supplies the existing repository ESM bundling mechanism without a
new bundler dependency. These operator tools are not conveyed in the static
Browser output.

Both original MIT texts permit the intended local use and redistribution while
requiring preservation of their copyright and permission notices. Keep their
complete warranty/liability disclaimers. Neither imposes a source offer,
hosting, seat, field-of-use or royalty requirement for this use; neither supplies
an express patent grant or trademark authority. No unclear compatibility term
was identified. This decision does not relicense third-party expression or
certify unspecified assembled distributions.

## Distribution and update ownership

The existing native `.ci/scripts/write-dependency-notices.cs` copies the package's
own nuspec and original license/notice files from the restored closure. Retain
those outputs with native distributions. Do not replace the original notice
with a generic license-expression label or an ISLAMU ownership claim.

`eng/configurator/BuildBrowserProfileWorker.cs` checks the exact archive SHA-512,
lock and original license SHA-256 before generation. Its manifest binds the
entry, generator, package, lock, this legal record and generated bundle/notice
hashes. `src/Configurator.Browser.Worker/wwwroot/vendor/profile-crypto.LICENSE.txt`
preserves the complete original MIT file without rewriting it. Keep that file
and `profile-crypto.manifest.json` beside `profile-crypto.js` in the isolated
published worker closure. The editor bundle is not its owner.

The parent agreed the exact dependency and channel packet on 2026-11-13.
This records implementation-owner review under existing clear-MIT workstream
authority, not user approval of an archive hash. SDK worker/runtime and inherited
Core NuGet components retain their existing original notices and actual resolved
graph; the one-node npm review does not certify that wider publication closure.

Artifacts and Browser maintainers own advisory review and updates for their
respective implementations. A version, algorithm, iteration policy, dependency
graph or notice change requires renewed review and independent interoperability
and rejection checks. No known-advisory scan or vendor reputation establishes
absence of defects.

## Independent design and qualification

The source-free handoff consists of RFC 7516/7518 format facts, public APIs,
registry metadata, original license material and synthetic observed behavior.
No selected container dependency implementation, decompiled code, tests or
expressive examples informed these adapters. They follow Configurator's existing Core/native/Browser
ownership and disposal contracts; standard identifiers and necessary API
signatures are interoperability constraints, not an imported implementation.

Temporary probes qualified one JWE profile: A256GCM with
PBES2-HS512+A256KW, PBKDF2-HMAC-SHA512 exactly 600000, no compression, bounded
salts/IV/tag, one selected recipient per unlock and strict framework JSON
admission. Native/Browser cross-decryption, malformed/tamper cases and published
AOT worker interop passed within recorded bounds. These results do not qualify
the product's workspace custody, native keyring, recovery workflows or packages.
See the [cryptography contract](../../SECRET_CRYPTOGRAPHY.md) and the workstream's
provider/hosting evidence.

### Quarantined macOS research exposure

On 2026-11-13, a gated research worker encountered implementation bodies from
`node-keytar`'s `src/keytar_mac.cc`, generated `objc2-core-foundation` bindings,
and usage examples in vendor/forum/search results. Their licenses were not
established; no reuse permission is asserted. That worker changed no repository
files and ran no product or native Keychain checks. Its session and expressive
material are excluded from implementation input.

A separate fresh worker receives only the approved product contract, existing
first-party ownership patterns and independently established public API facts.
Public declaration/ownership metadata must exclude implementation and example
blocks. This is a provenance control, not a claim that incidental exposure
licensed reuse or that the forthcoming native adapter is already qualified.
Excluded incident identities, recorded only for provenance and not to be
retrieved as implementation input:

- `github.com/MarshallOfSound/node-keytar/blob/master/src/keytar_mac.cc`.
- `docs.rs/objc2-core-foundation/latest/src/objc2_core_foundation/generated/CFData.rs.html`.
- Apple `SecItemAdd` and `SecItemCopyMatching` documentation examples, Developer
  Forums threads `685773`/`64184`, and archived Instruments `FindingLeakedMemory`.
- Stack Overflow questions `33598908`, `31183272`, `56802647`, `23730454`,
  `20640228`, `13201497`, `3642726`, `3407008`; CMake Discourse topic `16399`.

### Quarantined SDK research exposure

On 2026-11-13, a documentation-only Context7 query for `/dotnet/msbuild`
returned SDK implementation excerpts. The researcher stopped without repository
edits, builds or publication. No exposed implementation or source-derived
structure is implementation input. The response supplied no request identifier;
its excluded source identities are recorded without retaining their contents:

- `github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/Components/RequestBuilder/TaskHost.cs`
- `github.com/dotnet/msbuild/blob/main/src/Tasks/Microsoft.Common.CurrentVersion.targets`
- `github.com/dotnet/msbuild/blob/main/eng/BootStrapMsBuild.targets`

The parent had independently selected separate application publication before
that research. An unexposed implementation worker received only the functional
boundary and first-party project/generated-manifest observations: independent
SDK application bundles, ordinary public Worker assets below `profile-worker`,
and unchanged AOT, capture and PWA obligations. That worker used no external
research or sibling transcript. Its MSBuild naming, staging and content wiring
were independently authored in the repository. Product verification remains
separate from this provenance attestation.

### Excluded generated editor bundle search output

On 2026-11-13, an overbroad repository search for the failing profile import
also returned unrelated minified lines from the existing generated
`src/Configurator.Browser/wwwroot/vendor/configuration-editor.js`. No editor
implementation or structure informs this work. The offline asset-policy repair
was assigned to a fresh worker receiving only the observed failed request,
the first-party cache contract and permitted source paths. No snippets or
source-derived representations were handed off, and the editor bundle is
unchanged by that repair.

## Primary sources

Accessed 2026-11-13:

- [Native exact NuGet catalog](https://api.nuget.org/v3/catalog0/data/2026.03.27.14.45.38/jose-jwt.5.3.0.json).
- [Browser exact npm metadata](https://registry.npmjs.org/jose/6.2.17).
- Original license files from the corresponding verified archives.
- [RFC 7516](https://www.rfc-editor.org/rfc/rfc7516) and
  [RFC 7518](https://www.rfc-editor.org/rfc/rfc7518), standard format and algorithm contracts.
