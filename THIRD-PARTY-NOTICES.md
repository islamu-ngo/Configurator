# Third-Party Notices

The root MIT license covers ISLAMU-owned Configurator material under the Project
Steward's explicit transfer direction. It does not replace component licenses,
copyright notices or disclaimers. Preserve the original license material from
each conveyed NuGet/native artifact with distributions.

## Audited ISLAMU.Terminal.Gui downstream package

- Identity: `ISLAMU.Terminal.Gui` `2.4.17-islamu.1`; upstream Terminal.Gui
  contributors retain their identity and MIT terms.
- Upstream commit: `d0a0ed9b150d3fc8aacf4ab07b7f7d91264fe6d6`.
- The archive retains upstream LICENSE, authorship, README, repository identity
  and the ISLAMU downstream modification notice.
- Authorized modification removes TextMateSharp and grammar/theme dependencies,
  keeps the syntax-highlighter extension interface and selects plain built-in
  rendering. No upstream source is vendored here.
- Exact approved archive SHA-256:
  `d61e31f90f58875bc8e8632a96b1882eb718b265f66da0309c816ca9510049ec`.
- Patch, assembly and transitive closure are separately frozen in
  `eng/release/dependencies/terminal-gui/approval.json`. That approval is not
  broadened by regenerating evidence.
- Complete 21-component dependency evidence is retained in
  `eng/release/dependencies/terminal-gui/generated/terminal-gui.cdx.json` and
  `package-evidence.json`. Retain each component's own terms.

## Native and client framework closure

The transferred dependency review records Spectre.Console `0.57.2`,
Spectre.Console.Cli `0.56.1`, Spectre.Console.Ansi `0.57.2`, and Avalonia
`12.1.3` framework/Desktop/Fluent/Headless packages under MIT package terms.
Avalonia ANGLE Windows native assets `2.1.27548.20260419` retain BSD-3-Clause
redistribution, disclaimer and non-endorsement obligations.

Desktop's resolved SkiaSharp `3.119.4` and HarfBuzzSharp `8.3.1.3` native families
must be reviewed from the actual published RID graph. Test-only Avalonia.Headless
and its font dependency do not become approved application assets. Package
metadata for a font-bearing package does not replace the font's own terms.
See `docs/internal/legal/dependencies/configurator-ui.md`.

Other resolved framework, YAML, MVVM and testing packages retain their original
NuGet license/copyright files. Central version declarations are not a claim that
all declared packages ship in every client.

## Release renderer

git-cliff `2.13.1` is an operator-supplied build-time tool with the MIT option
selected from `MIT OR Apache-2.0`. No renderer binary or Event production trust
is transferred. If conveyed later, retain its copyright/permission notice and
complete statically linked component notices; the retained review explicitly
does not establish that complete binary component inventory.
See `docs/internal/legal/dependencies/git-cliff.md`.

## Excluded expressive corpus

The Evolve Demo site corpus and tokenized GitBook assets under the source
`.agents/skills/gitbook/resources/example-site` have no verified transfer
license. All 154 tracked entries are excluded, not relicensed. The GitBook skill
and independently authored project-native example guidance remain available.
