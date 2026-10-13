# Third-Party Notices

The root MIT license covers ISLAMU-owned Configurator material under the Project
Steward's explicit transfer direction. It does not replace component licenses,
copyright notices or disclaimers. Preserve the original license material from
each conveyed NuGet/native artifact with distributions.

## Official Terminal.Gui and embedded assets

Terminal packages use official `Terminal.Gui` `2.5.0` under MIT, retaining
Copyright 2007-2011 Novell Inc and Copyright 2017 Microsoft Corp.
Its transitive `TextMateSharp` and `TextMateSharp.Grammars` `2.0.4` retain
Daniel Peñalba's MIT terms. Embedded grammars, snippets and themes also retain
their original MIT, Apache, BSD and permissive TextMate Bundle terms; the NuGet
MIT label does not replace those component licenses.

The published `dependency-notices/` tree retains upstream package metadata,
Terminal.Gui and TextMateSharp licenses, and the reviewed grammar/theme notice
supplement. `Onigwrap` `1.0.11` already supplies its own
`THIRD-PARTY-NOTICES.TXT`, including the native Oniguruma BSD notice; that file
is retained unchanged. Keep the complete notice tree with distributions.

Reviewed supplemental text lives in `eng/configurator/dependency-notices/` and
is matched to exact restored package versions and canonical NuGet content
hashes. It contains license material only, not vendored implementation or asset
source. See the [decision, source register and history](docs/internal/TERMINAL_GUI_DEPENDENCY.md).

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

The static Browser additionally distributes MudBlazor `9.7.0` under its MIT
terms, preserving `Copyright (c) 2021 MudBlazor` from the package license.
Its identity, resolved closure and local asset usage are documented in the
[UI dependency record](docs/internal/legal/dependencies/configurator-ui.md).
Published dependency notices retain the package's own terms; no external fonts
or logos are imported by the browser UI.

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
