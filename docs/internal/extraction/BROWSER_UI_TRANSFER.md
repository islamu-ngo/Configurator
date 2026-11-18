# Browser UI transfer provenance

Source: local first-party Event revision
`072f2b6dbd8e4b87e16b795bc02a9ea23f902046`. The Steward-authorized ISLAMU-owned
MIT transfer in [IP governance](../legal/IP_GOVERNANCE.md#legal-posture-and-authority-boundary)
applies. Event is neither a build input nor a runtime dependency.

| Event source path | Git blob | Disposition |
| --- | --- | --- |
| `src/Explore.Blazor.Client/Layout/MainLayout.razor` | `01d075d0ce5a96c269bd966eabf75a8b4a34dd34` | Adapt providers, skip/main structure and layout ownership to standalone WASM |
| `src/Explore.Blazor.Client/Layout/NavMenu.razor` | `741d9c1eaff0fb70df1d617a26e40ab119d08992` | Replace Event routes/accounts with local Home/Store/Open/Help navigation |
| `src/Explore.Blazor.Client/Layout/Footer.razor` | `414f29297cd9e47f0d623948122ac0c9d0416d33` | Local useful footer; no tenant/footer service |
| `src/Explore.Blazor.Client/Layout/ThemeQuickSwitcher.razor` | `c2410c2c480e87683846b49a64080d3ce220a46f` | Bounded local appearance/direction controls; no account/preset CRUD |
| `src/Explore.Blazor.Client/Services/AppearanceState.cs` | `6ec88634f5523e995abe9fc8cd4f1f6d71c0e9b5` | Immutable local presentation-only record; exclude server DTOs |
| `src/Explore.Blazor.Client/Services/AppearanceThemeService.cs` | `4d4929519f20d118c2235eb9395ec48a747580e7` | Adapt built-in palettes/typography; independently implement bounded local preferences |
| `src/Explore.Blazor.Client/Components/Common/AppButton.razor` | `cfcb64c3f2bc350b826736ae57d494816b1631e0` | Consumed thin control wrapper with actual needed parameters |
| `src/Explore.Blazor.Client/Components/Common/AppCard.razor` | `5ca1ae948f829c403d4e9b096943615eeb790545` | Consumed thin card wrapper |
| `src/Explore.Blazor.Client/Components/Common/AppButton.razor.css` | `70fccfc782b9b3e61ec902cee780fcbb3a07b87d` | Preserve transparent wrapper/scoped control styling; exclude unused variants |
| `src/Explore.Blazor.Client/Components/Common/AppCard.razor.css` | `34843081d7ff25ec0cdea2993c38a5af7ac9322a` | Preserve transparent wrapper/scoped token binding; no unused hover behavior |
| `src/Explore.Blazor.Client/Layout/MainLayout.razor.css` | `d2b70bf96ffcbb646880b0b33b2c9d6231fee3d0` | Consulted layout precedent; standalone logical sidebar composition |
| `src/Explore.Blazor.Client/Layout/NavMenu.razor.css` | `a7817cecf9475fa7612f403df671d62faf0320e8` | Adapt compact brand/header/sidebar styling with logical properties; exclude Event actions |
| `src/Explore.Blazor/wwwroot/css/layers.css` | `ad8cbe291b5e89b205935280bf074bf30cffcf95` | Preserve six-layer ordering; local browser consumers |
| `src/Explore.Blazor/wwwroot/css/tokens.css` | `bc08953e68ab07e64c9133a194a7047c2a713cfc` | Adapt consumed spacing/radius/semantic/typography tokens |
| `src/Explore.Application/Services/HslColor.cs` | `2ba2f32993bb82cf9baa87910b9216ba51c9bd30` | Port pure HSL color math and WCAG contrast helpers into `Configurator.Presentation.Theming.HslColor` (`b947b83f8eb049aabbc81393752d72b57e1fd09c`) |
| `src/Explore.Application/Services/AppearancePaletteGenerator.cs` | `bfa85b34a3cc51faedeb04cee509c850e7e21367` | Port deterministic Light/Dark/High-Contrast generator into `Configurator.Presentation.Theming.ThemePaletteGenerator` (`b947b83f8eb049aabbc81393752d72b57e1fd09c`) |
| `src/Explore.Persistence/Seed/LookupTableSeeder.cs` | `f4d1d35e73a53dcbc04bad4f388f3a1459def5b5` | Port the 8 system theme presets (`SeedUiThemePresetsAsync`) into `Configurator.Presentation.Theming.ThemePresetCatalog` (`b947b83f8eb049aabbc81393752d72b57e1fd09c`) |
| `src/Explore.Blazor.Client/Services/Docking/DockLayoutState.cs` | `08efd3c5f4aac007827cbed31e3b6571c9b665fb` | Port dock layout state machine into `Configurator.Browser.Services.Docking.DockLayoutState` with `420px` content floor (`b947b83f8eb049aabbc81393752d72b57e1fd09c`) |
| `src/Explore.Blazor.Client/Components/Docking/DockLayoutHost.razor` | `dd0b2762ad57f6b213f3e3aeaec947cc348ad650` | Port dock host grid with zero `overflow-x: hidden` (`b947b83f8eb049aabbc81393752d72b57e1fd09c`) |
| `src/Explore.Blazor.Client/Components/Docking/DockSideHost.razor` | `666818abad2d9fd63436a48736e60ea2893ad905` | Port side stack and tabbed dock container (`b947b83f8eb049aabbc81393752d72b57e1fd09c`) |
| `src/Explore.Blazor.Client/Components/Docking/DockPanelHost.razor` | `12777eeb15c1d738a4f387a71889b06bbfad93e9` | Port dock panel chrome and resize handle integration (`b947b83f8eb049aabbc81393752d72b57e1fd09c`) |
| `src/Explore.Blazor.Client/Components/Docking/DockResizeHandle.razor` | `6123733a5522f41d4897db2d7a5b48645684bbd8` | Port pointer + keyboard splitter with RTL delta inversion (`b947b83f8eb049aabbc81393752d72b57e1fd09c`) |
| `src/Explore.Blazor.Client/Components/Docking/DockTabStrip.razor` | `e66a78f853de8d9afc5c4679e7588a263a74b828` | Port accessible ARIA `tablist` with roving tabindex (`b947b83f8eb049aabbc81393752d72b57e1fd09c`) |
| `src/Explore.Blazor.Client/Components/Docking/DockOverlayHost.razor` | `7f90c144876f5e1c78464e725355c90557f75b90` | Port `760px` mobile overlay drawer and focus restoration (`b947b83f8eb049aabbc81393752d72b57e1fd09c`) |
| `src/Explore.Blazor.Client/Services/Interop/LocalStorageDockLayoutPersistence.cs` | `3c2674b307539f507a4d8058cb9547630f6d6eee` | Adapt bounded visual-only dock persistence without Event tenant services (`b947b83f8eb049aabbc81393752d72b57e1fd09c`) |

No Event assembly, BFF, API, authentication, telemetry, publisher authority, font
or third-party implementation source transfers.
The curated `ISLAMU Event` store entry separately embeds a downscaled `128×128`
PNG copy of `assets/images/adopters/ISLAMU/islamu-logo-text-only-v2-square.png`
(blob `5a6a0eaa80b44f30850a4dbccc15940b998c5d45`) under its own `CC BY 4.0`
terms (`assets/images/adopters/ISLAMU/LICENSE.md`, blob
`1c62e21b28876a616bdb65b8dbbb1feb65add278`).
MudBlazor remains a separately licensed dependency: [dependency decision](../legal/dependencies/configurator-ui.md).
External influence is restricted to official public API facts; implementation,
tests and browser preference admission are independently designed for the local
custody boundary. AFC/SSO review: authorized first-party UI precedents are
identified above; public API identifiers and platform semantics are functional
constraints; no third-party discretionary expression was copied.
