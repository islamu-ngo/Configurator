# Configurator Release Engineering

The standalone `net10.0` engine has no product project references. It validates
Conventional Commit syntax, scope, skip/breaking trailers, sortable Change-Id
ownership, immutable fragment inputs and bounded deterministic release evidence.
The repository's integration target is `main`.

```bash
dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release -- preflight-staged --target main
dotnet run --project eng/release/src/Configurator.ReleaseEngineering/Configurator.ReleaseEngineering.csproj --configuration Release -- preflight-range --target main --head HEAD
```

These commands inspect provenance; they do not commit, push, deploy or publish.
Activate tracked hooks with `git config core.hooksPath .githooks` only when
authorized and preserve an existing hook chain.

## Publication trust

Configurator has no copied Event signer list, trust activation or publication
history. Trust-sensitive inputs use `CONFIGURATOR_RELEASE_*`, not Event's
environment variables. Promoted publication requires independently installed
Configurator promotion authority and authenticated bundle/evidence inputs.
Missing authority returns rejection, never unsigned success.

`toolchain.lock.json` freezes the generic git-cliff binary identity and source,
not a production signing authority. `cliff.toml` renders validated records
offline without executing commands. No renderer binary is assumed installed.

## Audited Terminal.Gui dependency

The dependency feed retains the exact approved `ISLAMU.Terminal.Gui`
2.4.17-islamu.1 archive, patch, assembly/closure hashes, SBOM and notices.
Its native rebuild script now references Configurator Terminal and its renamed
probe. NuGet pack is not byte reproducible; do not overwrite the authoritative
approved archive or relax its ratchets to accept a new ZIP.
