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

No Event assembly, BFF, API, authentication, telemetry, docking, AI system,
publisher authority, font, logo or third-party implementation source transfers.
MudBlazor remains a separately licensed dependency: [dependency decision](../legal/dependencies/configurator-ui.md).
External influence is restricted to official public API facts; implementation,
tests and browser preference admission are independently designed for the local
custody boundary. AFC/SSO review: authorized first-party UI precedents are
identified above; public API identifiers and platform semantics are functional
constraints; no third-party discretionary expression was copied.
