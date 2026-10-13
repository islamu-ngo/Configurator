# Quick Reference

> **Audience:** Contributors | AI agents
> **Status:** Implemented invariant router
> **Owner:** Contributor Experience
> **Last Verified:** 2026-10-08
> **Source Anchors:** `AGENTS.md`, `PROJECTS.md`, `Directory.Build.props`, `docs/internal/extraction/FOUNDATION.md`

Selected reusable rules from Event revision
`c65788c36aaa4d2a7c0e4bbd7ccb0e96e529ae38` are adapted to actual Configurator
owners. Event database, HTTP, HAL, BFF, tenancy and service-secret authorities
do not apply.

## Critical Rules

1. Keep file-scoped namespaces for new C# files.
2. Avoid deleting necessary `using` statements blindly.
3. **External behavior research is clean-room only:** use neutral requirements,
   independently designed structure/sequence/organization and recorded provenance.
   The explicit authorized first-party MIT transfer is separate; third-party
   expression is not covered. See [IP Governance](legal/IP_GOVERNANCE.md).
4. **Dependencies preserve distribution rights:** retain exact identity, terms,
   notices, approval and closure evidence. No third-party relicensing by assertion.
5. **No Python/JavaScript task scripts:** native editing tools and POSIX commands
   are the default. Persistent high-value C#/Bash tools belong in `eng/`;
   `.ci/scripts/` is for CI pipelines only.
6. **Separate offline privacy and authority:** Core admits bounded data-only
   definitions; Browser keeps answers transient and requires per-workspace
   consent for secret entry. Public caches exclude answers/output. Bounded
   credential-free HTTPS definitions are pinned before answer collection.
   Local preparation never grants host apply/admin authority. No executable
   plugins, answer-bearing requests, telemetry or insecure native save fallback.
7. **Self-contained interaction:** questions, status and decisions must explain
   feature names, current position, tradeoffs and recommendation inline. Never
   require the user to open a plan to decode bare task codes.
8. **Bounded verification:** the approved cadence is relevant security Red once,
   coherent implementation slices, then targeted phase-exit build/affected classes.
   Preserve passes; broader affected-owner checks run once at workstream exit.
   See [Testing](TESTING.md#the-3-ring-progressive-verification-model).
9. **Quarantine unrelated test rot:** establish baseline evidence, log proven
   unrelated failures and continue assigned scope. Do not absorb unrelated repairs.
10. **Self-documenting code:** naming/folders and XML comments for non-inferable
    behavior replace synthetic source headers. Never generate `ABOUTME:` source
    comments. See [metadata policy](GOVERNANCE.md#header--file-metadata-policy-natural-metadata--code-retirement).
11. **No warning suppressions to force green:** fix the root cause, never weaken
    tests or hide compiler/analyzer failures.
12. **Execution continuity:** proceed through approved in-scope work without routine
    pauses. Pause only for material ambiguity, irreversible action or user direction.
13. **Verification evidence:** exact commands/outcomes and omitted checks with
    reasons belong in every completion. Do not claim unexecuted checks passed.
14. **Publication fails closed:** generic release validation and synthetic trust
    fixtures do not activate signers, provider publication or hosted docs.
15. **Native host proof:** Linux x64 package checks do not prove Windows x64,
    macOS arm64 or Linux arm64 restricted save. Keep their matrix status Planned
    until actual-host tests pass; unsupported saves refuse without fallback.

## Build And Test Baseline

Use SDK 10.0.302, MTP `--project`, central package versions and generated locks.
The [project map](../../PROJECTS.md) owns exact affected-owner commands and package
preconditions. Documentation-only changes run link/anchor/whitespace checks,
not product builds/tests. Engineering changes run engineering suites.

## UI Quick Rules

CLI, Terminal.Gui and Avalonia adapt shared Presentation/Artifacts/Core
contracts; Browser is static Blazor WASM over browser-safe Presentation/Core.
Secret-deferred drafts remain incomplete; browser downloads cannot establish
native file protection. Test actual
keyboard/focus/rendering semantics and accessibility, not CSS/source text.
Do not import backend role inspection or HAL gating into this offline utility.
Use the existing surface contracts and [test-layer guide](TESTING.md#choosing-the-right-test-layer).

## Protected Paths & Sensitive Boundaries

- Generated schemas/locks are regenerated through native owners, never hand-edited.
- User strategy files `docs/internal/universal-configurator.md` and
  `docs/internal/go-to-market.md` remain user-owned.
- Use official pinned Terminal.Gui and retain reviewed embedded-asset notices;
  see [the dependency decision](TERMINAL_GUI_DEPENDENCY.md).
- No `.env`, credentials, machine MCP settings, Event signer list or publication
  history transfers.
- Definition sensitivity, protected create-only output and generation fencing
  must be tested through observable seams.
- Do not modify other contributors' edits or switch the shared checkout without
  explicit authorization. The current task's narrower allowed paths win.

## Common Failure Patterns

| Failure | Correction |
|---|---|
| Importing backend operations into an offline utility | Follow actual Core/Artifacts/Presentation owners |
| Treating preparation as apply/admin authority | Keep launch/runtime authority separate |
| Claiming passing tests establish coverage | Inspect measured report and commit identity |
| Treating synthetic signer fixtures as activation | Require independent Configurator authority |
| Upgrading dependencies without embedded-asset review | Review the exact package, regenerate locks and retain component notices |
| Redirecting every named policy to FOUNDATION | Link the actual governance/testing/release anchor |

## Related Documentation

- [Governance](GOVERNANCE.md)
- [Documentation Architecture](DOCUMENTATION_ARCHITECTURE.md)
- [Operations and evidence](OPERATIONS.md)
- [Release Runbook](RELEASE_RUNBOOK.md)
- [IP Governance](legal/IP_GOVERNANCE.md)
