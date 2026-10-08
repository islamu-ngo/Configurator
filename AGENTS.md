# Configurator Agent Contribution Contract

Configurator is an MIT-licensed offline configuration preparation utility.
Read this contract first. `PROJECTS.md` defines project ownership and commands.
The approved extraction ledger is only in `dev/active/configurator-extraction/`.

## Scope and authority

Explicit user instructions override repository guidance. Work directly in this
checkout when instructed; never create a branch, worktree or commit without
authorization. Preserve existing user changes and the design/strategy documents
`docs/internal/universal-configurator.md` and `docs/internal/go-to-market.md`.

Resolve the matching intent from `.agents/contract/intents.yaml`. Read the
smallest relevant skill/resource and path rule, not the whole reference library.
Copied Event backend guidance is reference-only: no Event database, API, HAL,
BFF, tenancy, Aspire, AGPL covenant or production deployment policy governs this
offline utility. Target instructions and actual Configurator projects win.
See `docs/internal/extraction/FOUNDATION.md` for the adapted authority map.

## Product boundaries

All authored projects, assemblies and namespaces use `Configurator.*`.
Core admits bounded data-only definitions and has no Event assembly dependency.
Artifacts owns protected native output. Presentation owns operation generations
and immutable workspace state. CLI, Terminal.Gui, Avalonia Desktop and static
Blazor WebAssembly are adapters over those boundaries. Browser accepts public
metadata only. Local preparation never grants a host's apply or admin authority.
No executable definition plugins, arbitrary remote launch URLs, browser secret
entry, telemetry or insecure save fallback.

## Engineering

Use native read and apply_patch for files, and POSIX shell/native .NET tools.
No ad-hoc Python, JavaScript or Node task scripts. Use SDK 10.0.302 and MTP,
central package versions, generated dependency locks, deterministic builds and
compiler/analyzer diagnostics. Never add warning suppressions or weaken tests.
Keep library identity and notices intact; do not rename ISLAMU.Terminal.Gui.
Set `TMPDIR="$HOME/.cache/agent-tmp"` on the current workstation.

Verify the affected project only. Tooling-only changes run engineering tests,
not product suites or Event database tests. Documentation-only changes use link,
schema and whitespace checks. Do not register an unready project in the solution.
Record exact commands and outcomes, including checks not run and why.

## Provenance and publication

The Project Steward authorized MIT transfer of ISLAMU-owned source. This does
not relicense third-party expression. Retain component approval, patch, license,
notices, hashes and dependency ratchets. The unlicensed site-derived Evolve Demo
example corpus is excluded; its GitBook tokens/assets are not transfer inputs.
See `THIRD-PARTY-NOTICES.md` and the transfer inventory.

Never copy `.env`, credentials, machine MCP settings, Event production signers,
trust activation or publication history. Generic release validation is retained;
signed publication fails closed until Configurator establishes its own promoted
trust authority. No unsigned publication bypass.

## Harness parity and reporting

Keep `.agents/rules` and `.omo/rules` byte-identical twins. GitHub instructions
must route to the same contract. Harness pointer files contain no second policy.
Skills about absent Event-only layers are retained as historical reference, not
active requirements. Update public/internal documentation when operator behavior
changes. Explain concrete implementation mechanisms and exact verification in
the final response; do not require the user to open a plan to understand status.
