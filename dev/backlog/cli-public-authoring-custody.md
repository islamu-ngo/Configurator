# CLI Public Authoring Custody

- Status: Required follow-up; not implemented or execution-authorized.
- Owner: Configurator CLI and Artifacts maintainers.
- Origin: CTO review `CLI-CTO-2026-10-15-r1`, 2026-10-15.
- Dependency: truthful command v2/runtime slice in
  `dev/active/cli-native-adoption/`; no full adoption claim before this gate.
- Contract: R04-R05 / S07-S11 in the retained native-adoption plan.
- I-VSD: IVSD-F003/M003 and custody portion of IVSD-F007/M007 remain open.

## Problem And Outcome

Current `DefinitionSourceFiles.cs` uses pathname check/use, an existence lock and
overwrite-capable revision. `DefinitionSchemaSourceFiles.cs` applies lexical
containment rather than binding opened reference identities. These are verified
source concerns, not executed exploit evidence. A runtime receipt fix cannot
prevent an editor save from being replaced or a public read from being redirected.

Deliver create-only revision candidates and captured native public-file authority.
This is public authoring, NOT relaxation of restricted artifact protection.
The user selected candidate-only revision on 2026-10-15; no in-place compatibility
path or automatic candidate promotion is allowed.

## Two Independently Reviewable Increments

1. **Candidate revision:** Red editor-save/alias/existing-output/overflow tests;
   require explicit non-source, non-discovery `--output`; compile captured source
   at revision n+1 with the same family; preserve supported BOM/comments/newlines;
   publish create-only and report captured-source/candidate digests. The contributor
   reviews and merges through their normal editor. This increment alone MUST NOT
   claim captured-identity race safety.
2. **Captured public authority and root exclusion:** Red native source/reference/
   staging/ancestor replacement tests before implementing narrow adapters. Bind
   source handles and destination parent handles, reject symlink/reparse traversal,
   no-replace publish, verify identity and exact bytes for idempotent public builds.
   Hold kernel-owned root initialization exclusion across YAML/JSON default-source
   selection; process exit releases ownership, not stale-lock deletion. Keep the
   stable lock entry rather than unlink/recreate it. Noncooperating writers may
   create ambiguity; subsequent discovery MUST refuse it.

Plan each increment as a bounded triad when activated; if candidate publication
cannot be safe independently, combine only the inseparable native primitive, not
all discovery or packaging work. Update I-VSD bindings before execution.

## Owners And Native Constraints

- CLI source/schema callers remain thin. Reuse Core identity editing and existing
  command result ledger; do not add a second definition compiler or transaction
  framework. Proposed owner: `src/Configurator.Artifacts/PublicDefinitions/` with
  `PublicDefinitionFiles`, `PosixPublicDefinitionFiles`, `WindowsPublicDefinitionFiles`.
  Those names are planned, not claims of existing symbols.
- Linux descriptor-relative/no-follow/no-replace semantics; Windows opened-handle/
  reparse-aware relative publication. macOS descriptor-source cloning such as
  `fclonefileat` was a design candidate, not proven portability. Verify the actual
  supported filesystem/API or refuse; do not copy Linux flags or fall back to
  unchecked paths. Small owned native bindings, no new dependency without need.
- Schema resolution remains under its captured root, bounded to 64 documents and
  the existing 4 MiB input budget. No remote references or executable definitions.
- Existing-file/concurrent conflicts never overwrite. Hard-link source/output
  aliases reject. A renamed captured root grants no authority over its replacement.
- Reuse publisher outcomes and cancellation semantics; canonical definition plus
  ID-map publication has truthful prefix facts, not multi-file rollback.

## Acceptance And Evidence

- S07-S11 establish Red before native code and pass through public operations.
  Real native tests live in `Configurator.Artifacts.Tests`; CLI candidate flow
  lives in `DefinitionSourceFileTests` / `SetupCliDefinitionTests`.
- Deterministic barriers test source/ref/staging/ancestor swaps, competing target
  creation, YAML-versus-JSON initialization, editor saves and process-holder death.
  Verify filesystem bytes and identities, never internal mock call counts.
- Actual Linux x64/arm64, macOS arm64 and Windows x64 host evidence qualifies the
  public-authoring adapter only. Restricted save remains Linux-only; unsupported
  protected-save variants reject without ordinary-file fallback. See
  [separate protected-host evidence](configurator-native-host-evidence.md).
- Update public CLI/integration guidance and technical authoring/output contracts
  in each behavior increment; native freshness, real package smoke and supported-
  host claims must agree. Graduate a runtime/custody ADR and reusable race lessons.

## Deferred Commit Provenance

Original allocations are unreserved and MUST be rechecked natively on activation:

| Original increment | Provisional allocation | Intended treatment |
| --- | --- | --- |
| C2 candidate-only revision | CHG-01M4ZEDZQ57FW5WM65QFVXKKRT | `feat(cli)!`, required-output breaking footer and fragment |
| C3 captured public authority | CHG-01M4ZEDZWC7VSH1NDN4G4T77BX | `fix(output)`, public fragment |

No old phase/commit packet authorizes execution. Write complete current literal-
path commit packets with each activated triad; never stage ignored active plans.

## Exclusions

No target authentication/apply, host acceptance, release signing, Event transport,
new protected-save platform, automatic editor merge or compatibility shim. Native
API feasibility remains a proof obligation, not permission to weaken the invariant.
