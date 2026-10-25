# Configurator Release Engineering

`Configurator.ReleaseEngineering` is a standalone `net10.0` console project for
governed release tooling. It has no references to product projects. These
operating instructions restore the reusable original sections from Event
revision `c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38`. Configurator's integration
target is `main`.

## Collision-proof change workflow

New public changes use sortable ULID-style identifiers such as
`CHG-01K3Q8Y7M6N5P4R3T2V1W0X9ZA`. Sequential identifiers are historical
provenance only; no command allocates them.

Create the fragment and exact commit footer in one operation:

```bash
dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release -- \
  create-change \
  --target main \
  --type feat \
  --scope definitions \
  --title "Definition validation" \
  --summary "Report invalid definition inputs before preparation." \
  --group definition-validation
```

The command atomically creates
`docs/internal/releases/changes/<Change-Id>.yaml`, initializes every required
impact section for review, and prints `commit_footer: Change-Id: <Change-Id>`.
Use `allocate-change-id --target main` only when another tool owns fragment
creation.

## Categorized release notes

`GitCliffRenderer` groups validated presentation records without changing the
normalized release context, version policy, visibility, backport identity, or
complete commit range. Breaking changes precede Features, Bug Fixes, Performance,
and Other Improvements; breaking fixes belong only to the first category.
Context order is preserved within each category, and empty categories are
omitted. Fixed headings appear once per group, while each primary entry retains
its scope, title, and display ID.

`ReleasePreparation` still composes the maintainer summary, rendered details,
applicable impact evidence, and complete range. An ID repeated in an impact
reference or complete range is not a duplicate primary entry. Applicable
configuration, security, breaking and operator evidence remains required by
the descriptor/fragments. Presentation does not authorize disclosure.

The engine and packaged template must be promoted together. Never modify an
already promoted bundle or regenerate signed historical notes with a newer
formatter. Candidate verification at exact `B` recomposes notes and rejects
different committed bytes with `candidate_release_notes_mismatch`. Before `B`,
recover generated files using the
[runbook](../../docs/internal/RELEASE_RUNBOOK.md#generated-note-mismatch-recovery);
after signing, correct forward.

Install local commit checks once, preserving the active hook chain:

```bash
dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release -- \
  install-change-hooks --target main
```

The installer resolves Git's active hook directory, including `core.hooksPath`,
and creates `pre-commit` and `commit-msg` checks there. Existing hooks are
preserved as `<hook>.before-islamu-release` (the retained native suffix) and called
first. An ambiguous overwrite fails closed.

The tracked `.githooks/commit-msg` invokes native `preflight-commit` before
recording a proposed message. Confirm `git config --get core.hooksPath`; enable
`.githooks` only when no other chain is configured. Preserve existing checks.
Whitespace between terminal trailer paragraphs is accepted; duplicate/conflicting
skip declarations, missing reasons, invalid breaking metadata and Change-Id
provenance remain rejected. The installer must not silently write unused
`.git/hooks` files when Git executes another directory.

Before starting a merge, validate the complete feature range:

```bash
dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release -- \
  preflight-staged --target main
dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release -- \
  preflight-range --target main --head HEAD
```

Range validation compares effective Change-Ids in `main..HEAD` with every ID
reachable from `main`, rejects duplicates inside the feature range, and requires
an exactly named fragment for every linked ID. Fragments and correction records
must already be committed at `HEAD`; staged, modified or untracked release
sources fail closed. These validators do not commit, push, deploy or publish.

### Correct immutable group membership

An existing change fragment remains immutable, including when its original
`Group` combines different scopes. Correct only that membership with an
append-only `docs/internal/releases/group-corrections/<Change-Id>.yaml` record.
The record contains exactly four scalar fields:

- `Schema-Version`: `fragment-group-correction.v1`.
- `Change-Id`: the existing fragment's identifier, matching the filename.
- `Fragment-Sha256`: lowercase SHA-256 of the UTF-8 original
  `NormalizedSnapshot`, using the release input owner's existing newline and
  trailing-whitespace normalization rather than reserializing YAML.
- `Group`: a different, nonempty group with the existing bounded group syntax.

The original fragment must already have a group. A correction changes only the
membership used to check that each effective group has one scope. It does not
replace fragment content, identifiers, commit linkage, impact declarations or
release entries. It cannot override another diagnostic, remove a group, form a
chain or patch another field. Duplicate targets, absent fragments, digest
mismatches, unknown fields and incompatible corrected groups reject.

Stage new records with their source/test changes and run `preflight-staged`.
Commit the records before `preflight-range` or release preparation. Committed
records are immutable; mutation, deletion and renaming reject. Candidate and
authorized-inventory verification use applicable records from the exact selected
Git object, include them in deterministic source-document hashes as
`fragment-group-correction`, and reject worktree-only substitutions.

### Public descriptions and reserved markers

Public descriptions may name restricted output, writes, bundles or byte buffers.
Those product terms do not classify a fragment as restricted input. The marker
gate instead rejects the reserved `Restricted-Details` key, standalone or
bracketed `restricted`, `restricted:` / `restricted=` classification labels, and
token-delimited restricted detail/credential markers. Case, separator and
supported confusable normalization apply to markers. Independent embargo,
secret-material, provider-identity, unknown-key and ambiguous-Unicode checks
remain enforced; a group correction cannot suppress them.

### Correct a colliding Change-Id

If an immutable commit already has a colliding footer, do not amend or rebase it.
Bind only that exact full commit object to a generated replacement ID:

```bash
dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release -- \
  rename-change \
  --commit <full-commit-oid> \
  --from <colliding-Change-Id> \
  --reason "Target branch already owns the original identifier."
```

The command creates/reuses the replacement fragment and writes
`docs/internal/releases/change-id-renames/<full-commit-oid>.yaml`. Preparation and
candidate verification apply the replacement only when the immutable commit
still has the recorded old footer. The old footer remains unchanged; loose
aliases, branch-bound mappings or reused replacement IDs are rejected. Commit
fragment and correction record before rerunning `preflight-range`.

```bash
export CONFIGURATOR_RELEASE_TOOL_BUNDLE=/absolute/path/to/verified/tool-directory
dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release -- verify-tools
```

The directory must contain the platform executable at its lock-file name:
`git-cliff` on Linux x64 or `git-cliff.exe` on Windows x64. `verify-tools` selects
only the current approved platform from `toolchain.lock.json`, checks SHA-256,
and requires the exact `git-cliff 2.13.1` version response. Missing, malformed,
mismatched, unsupported, noisy, failed or hung tools fail closed. It never
downloads a tool; acquisition and promotion are separate responsibilities.

## Publication trust

Configurator has no copied Event signer list, trust activation or publication
history. The following native validation mechanisms are retained, but signed
publication is unavailable until Configurator establishes its own promotion
authority and approved signer roots. Missing authority rejects; there is no
unsigned bypass. Local synthetic fixtures are not production activation.

Final attestation must use a separately promoted trusted bundle, not the candidate
checkout. `TrustedBundlePolicy` verifies the caller-supplied manifest SHA-256,
exact-bundle-bound promotion receipt, complete manifest file set, release-engine
binary, policy/schema/context versions, packaged `cliff.toml`,
`toolchain.lock.json`, and trust roots before trusting candidate data. A receipt
may verify repeatedly for the same immutable bundle, but cannot promote different
bytes. Root aliases, symlink roots/entries, hardlinked files, traversal and
candidate-local overrides fail closed.

`TrustedBundlePolicy.Verify` returns a `VerifiedTrustedBundle` capability only
after manifest, receipt, config/toolchain digest, paths and link safety pass.
`GitCliffRenderer` accepts that capability, not raw bundle, lock, config or
caller-supplied digest values. At render time it rereads bundle-owned config,
lock and executable; rejects symlink/reparse and hardlink aliases; and rechecks
bytes against verified capability/lock digests before copying anything into
an isolated run directory.

The packaged `cliff.toml` grammar is intentionally strict: comments/blank lines,
one exact `[changelog]` table, one multiline `body`, `trim = true`, and
`render_always = true`. The body may reference only `version`, loop over
`commits`, and print `commit.group`, `commit.scope`, `commit.message`, and
`commit.id`. Dotted, quoted, spaced, remote/provider/parser/bump/tag/range/
processor/exec/URL variants fail closed. The verified binary runs with `--config`,
`--from-context`, `--offline`, and `--no-exec` from a temporary non-Git directory
with a cleared deterministic environment. Candidate-local config/environment
is never consulted; there is no fallback renderer.

After `prepare` output is committed as final preparation commit `B`,
`verify-candidate` records pre-tag evidence:

```bash
dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release -- \
  verify-candidate docs/internal/releases/<version> <full-B-oid>
```

It never commits, tags, pushes, fetches or executes candidate code. It recomputes
the descriptor-selected range through exact `B`, requires `B`'s terminal
changelog skip reason, rerenders notes with the verified promoted bundle, and
writes/verifies deterministic `release-candidate.v1.json`. The manifest contains
authoritative object IDs/hashes; tag IDs and provider, clock, identity, raw-body,
token and secret data are excluded.

After candidate verification, generate the deterministic annotated-tag message
and sign outside the verifier. These are conditional procedure examples, not
instructions to activate publication during extraction:

```bash
dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release -- \
  tag-message docs/internal/releases/<version> > /tmp/configurator-release-tag-message.txt
git -c gpg.format=ssh -c user.signingKey=/path/to/release-signing-key \
  tag -s v<version> <full-B-oid> -F /tmp/configurator-release-tag-message.txt
dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release -- \
  verify-tag docs/internal/releases/<version> v<version>
```

The actual local `verify-tag` CLI accepts the tag name, not a caller-supplied
candidate OID or trust override. It resolves the full tag object locally and
rejects lightweight, unsigned, wrong-target/name/line, recreated, moved,
unauthorized, revoked, expired, wrong-algorithm, candidate/note/policy drift and
stale-output states before writing/accepting `release-evidence.v1.json`.
Final evidence chains to the candidate manifest by SHA-256 and records tag object
ID, target `B`, signer principal/role/algorithm/fingerprint, prior-tag relation,
and trusted bundle/tool/policy/config hashes. It excludes clocks, provider
metadata, emails, raw bodies, tokens and secrets.

Before a protected-ref adapter advances stable `main`, verify local topology
and compare-and-swap inputs:

```bash
old_main_oid=$(git rev-parse --verify refs/remotes/origin/main^{commit})
tag_object_id=$(git rev-parse --verify refs/tags/v<version>^{object})
dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release -- \
  verify-main docs/internal/releases/<version> "$old_main_oid" "$tag_object_id"
```

`verify-main` is read-only. It checks final evidence target, tag object and local
`origin/main` compare-and-swap OID before every stable action. Newest stable moves
require normal fast-forward topology before printing exact old/new OIDs and
action. Older-line patches emit `no-main-move`; prereleases, stale evidence/tags,
races, missing/short objects and non-descendant targets fail closed.

Event's provider evidence-bundle scripts and publication workflows are not live
Configurator commands. Retained local fixtures exercise candidate/tag/main
chaining. A future adapter must consume the final manifest rather than inventing
a second identity, retaining provider/workflow metadata separately and checking
descriptor, summary, context, notes, candidate/final evidence and bundle hashes.
No such transport is activated by documentation restoration.

## Tests

The locked-renderer checks are TUnit `[Explicit]` methods. The ordinary suite
does not run them, even when `CONFIGURATOR_RELEASE_TOOL_BUNDLE` is set. Provision
the Linux executable using archive URL/hash, executable name/hash in
`toolchain.lock.json`, retain license notices, and select each method explicitly.
Run sequentially: independent processes share the output directory's synthetic
promotion-root file, so TUnit's in-process grouping cannot protect concurrent
invocations.

```bash
export TMPDIR="$HOME/.cache/agent-tmp"
export CONFIGURATOR_RELEASE_TOOL_BUNDLE=/absolute/path/to/verified-test-tool-directory
dotnet build eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj --configuration Release --verbosity quiet
dotnet test --project eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj \
  --configuration Release --no-build \
  --treenode-filter "/*/*/GitCliffRendererTests/PromotedBinaryRendersTwiceByteIdenticallyOutsideGit" \
  --minimum-expected-tests 1
dotnet test --project eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj \
  --configuration Release --no-build \
  --treenode-filter "/*/*/FirstGovernedReleaseTests/CategorizedReleaseVerifiesAfterBranchDeletion" \
  --minimum-expected-tests 1
dotnet test --project eng/release/tests/Configurator.ReleaseEngineering.Tests/Configurator.ReleaseEngineering.Tests.csproj \
  --configuration Release --no-build \
  --treenode-filter "/*/*/ReleaseCandidateVerificationTests/VerifyCandidateRejectsCleanUnsignedBreakingNotesTamper" \
  --minimum-expected-tests 1
```

Missing/mismatched executable bytes fail selected checks. Stub executables cover
ordinary policy rejection, not real-renderer evidence. Explicit fixtures use the
real pinned renderer and shipped template under synthetic roots, receipts, SSH
signatures and dummy engine bundle bytes. They prove rendering and local Git
verification, not production bundle promotion, signer approval, provider
activation or hosted documentation delivery.

Source-derived tests that require absent Event CI/workflow files remain reference
until rebased by their owner; an ordinary suite must not be claimed green without
execution. See [TESTING.md](../../docs/internal/TESTING.md).

## Official Terminal.Gui dependency

Terminal.Gui is restored from NuGet.org with an exact central version and
generated locks. Normal package publication retains reviewed supplemental
notices for embedded third-party assets; no patched archive or upstream rebuild
is maintained. See [the dependency decision and history](../../docs/internal/TERMINAL_GUI_DEPENDENCY.md).
