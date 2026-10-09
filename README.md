<a name="readme-top"></a>

<div align="center">

<img src="https://raw.githubusercontent.com/islamu-ngo/Event/develop/assets/images/adopters/ISLAMU/islamu-logo-text-only-v2.png" alt="ISLAMU logo" width="200" />

# ISLAMU Configurator

Open-source, offline configuration preparation software for application authors, operators, and self-hosters.

ISLAMU Configurator prepares application-owned configuration artifacts, while the target application retains validation, authorization and apply authority.

> 🚧 **Active Pre-Release Development:** ISLAMU Configurator has **no official release yet**. Native clients, the public browser and local validation packages are implemented. No public release, signed publication or hosted deployment is claimed.

![GitHub Workflow Status][github-workflow-status-shield]
[![GitHub License][github-license-shield]][github-license-link]
[![GitHub Repo Stars][github-stars-shield]][github-stars-link]
[![GitHub Last Commit][github-last-commit-shield]][github-last-commit-link]
[![Contributors][github-contributors-shield]][github-contributors-link]
[![Discussions][github-discussions-shield]][github-discussions-link]
[![Discord][discord-shield]][discord-link]
[![Documentation][docs-shield]][official-docs-link]
[![Donate via Stripe][stripe-shield]][stripe-donation-link]

[**Documentation**](docs/README.md) · [**Roadmap**][roadmap-link] · [**Quickstart**](#quick-start-for-developers--contributors) · [**Packaging**](docs/public/packaging.md) · [**Developer Guide**](#-documentation-for-developers--contributors) · [**Sponsor**](#-sponsors)

</div>

## About ISLAMU Configurator

ISLAMU Configurator is an **offline configuration preparation utility** for self-hosted applications. A bounded data-only definition describes questions, validation, activation and output. The target application remains responsible for accepting and applying the resulting artifact.

The software is **purpose-agnostic**: Event is one external definition target, not a compiled dependency of the engine. A family GUID identifies a definition family; it is not authentication, publisher verification or host authority.

## ✨ Who It Serves

### Application Operators

- **🔍 Definition-Guided Configuration:** Inspect a local definition's questions, constraints and artifact choices before entering answers.
- **⌨️ Keyboard-Driven Preparation:** Use the retained Terminal.Gui workflow with transient private input and protected native exports — see [Terminal](docs/public/terminal.md).
- **🖥️ Native Desktop Forms:** Prepare configuration in the Avalonia desktop client using the same admitted definition and shared workspace — see [Desktop](docs/public/desktop.md).
- **📜 Deterministic Machine Output:** Use generic catalogue, artifact, environment and doctor commands with a closed JSON result envelope — see [CLI](docs/public/cli.md).
- **🔒 Restricted Native Output:** Answer-derived artifacts remain restricted; verified Linux writes are create-only and unsupported protected-save hosts reject — see [Native Output](docs/public/native-output.md).

### Application Authors

- **🧩 Declarative Definitions:** Describe fields, activation, validation and output using bounded data rather than executable plugins — see [Definitions](docs/public/definitions.md).
- **🔗 Pinned Public Launch:** Link to a registered family with bounded version, entry, locale and digest selectors; the session retains its admitted revision and digest — see [Launch Integration](docs/public/launch-integration.md).
- **🌐 Public Choice Templates:** The static Blazor browser collects active public enum/boolean choices, not private answers, arbitrary text or credentials — see [Public Browser](docs/public/browser.md).
- **🛡️ Target-Owned Authority:** Keep authentication, semantic acceptance and apply authorization in your application. Local preparation does not grant administration.

### Platform Owners & Self-Hosters

- **🆓 Free & Open Source:** ISLAMU-owned material is MIT-licensed; third-party components retain their terms and notices.
- **🛡️ Local Preparation:** Native preparation has no Event API, domain, wire or database dependency. The browser has no target authentication, telemetry, persistence or arbitrary remote-definition fetch.
- **🧰 Independent Clients:** Choose the machine CLI, Terminal.Gui TUI, Avalonia Desktop or static Blazor WebAssembly browser without installing Event services.
- **📚 Dual Documentation Tracks:** Public guides explain operator workflows; internal contracts explain architecture, privacy and verification.
- **📜 Real Reference Target:** Event owns its exporter and host validators. All five current reference artifacts are prepared through the generic runtime — see [Event example](examples/islamu-event/README.md).

---

## 🚀 Deployment & Hosting Options

ISLAMU Configurator separates native preparation from public static hosting:

| Topology Mode | Target Use Case | Step-by-Step Documentation |
|---|---|---|
| **Machine CLI** | Reproducible local automation with explicit definitions and answer workspaces. | 📖 **[CLI Guide](docs/public/cli.md)** |
| **Terminal Client** | Keyboard-driven native preparation with protected output. | 📖 **[Terminal Guide](docs/public/terminal.md)** |
| **Desktop Client** | Avalonia native forms over the shared workspace. | 📖 **[Desktop Guide](docs/public/desktop.md)** |
| **Public Static Browser** | Public choice templates served from an ordinary static host. | 📖 **[Browser Hosting](docs/public/browser.md#hosting)** |

See [Standalone Packaging](docs/public/packaging.md) for supported RIDs, executable names and package boundaries. Protected writes are verified on Linux only. Browser boot requires its static assets; disconnected relaunch is not promised because there is no service worker. No public Configurator deployment is claimed.

---

## 🎨 Branding & Customization

Application authors own definition family identity, publisher labels, application versions, entry points and locale metadata. Clients preserve the admitted definition's canonical identity rather than silently changing its revision or content.

- **Application Metadata:** Describe your target without hardcoded Event assumptions.
- **Public Integration:** Bundle safe public definition metadata and link to its pinned launch contract.
- **Target Governance:** Keep operator permissions, secret authority and configuration acceptance in the application that owns them.

---

## 🛡️ Enterprise Security & Compliance

Security and privacy fail closed at every layer:

- **🔐 Definition Admission:** Closed schemas and bounded values reject unsupported definitions before use.
- **🛡️ Session Integrity:** Family, revision and digest remain pinned; removed revisions cannot return with conflicting content.
- **🔒 Private Artifacts:** Native answer-derived output is restricted and cannot overwrite an existing protected artifact.
- **🌐 Browser Boundary:** Public templates are partial choices, not full configuration, credentials or host readiness.
- **🗝️ Target Authority:** The target performs semantic validation and authorization; Configurator does not authenticate or apply remotely.

See [SECURITY.md](SECURITY.md), [Core Contract](docs/internal/CORE_DEFINITION_CONTRACT.md) and [Protected Artifacts](docs/internal/PROTECTED_ARTIFACTS.md).

---

## Roadmap

The wider ISLAMU roadmap is tracked publicly in the [Roadmap Kanban View][roadmap-link]. Use it to follow planned work, vote, and comment. Configurator-specific proposals belong in this repository’s [GitHub Discussions][github-discussions-link].

---

## 🤝 Contributing

There are many ways you can contribute to ISLAMU Configurator:

**Non-Technical:**
- 🐛 [Report bugs][github-issues-link]
- 💡 [Suggest features][github-issues-link]
- 📖 [Improve Public Documentation](docs/README.md)
- 🌐 Translate UI resource files and documentation
- 📣 Spread the word in your community

**Technical:**
- 💻 Fix bugs & implement features
- 🧪 Author invariant-breaker test suites
- 📊 Profile & optimize bounded definition processing
- 🎨 Enhance Blazor, Avalonia and Terminal.Gui accessibility

---

## Quick Start (For Developers & Contributors)

For local development and code contribution, install .NET SDK **10.0.302** and use the independent Configurator checkout:

```bash
git clone https://github.com/islamu-ngo/Configurator.git
cd Configurator
dotnet restore Configurator.slnx --locked-mode
dotnet build Configurator.slnx --configuration Release --verbosity quiet
dotnet run --project src/Configurator.Cli --configuration Release -- doctor --machine
```

The CLI registers `catalogue list|show|describe`, `artifact create|open|validate|format|diff|coverage|export|import|preview`, `env render|validate`, and `doctor`. Use `--help` for operation-specific options and `--machine` for the closed JSON envelope. Select a local definition explicitly; there is no implicit Event target or remote apply command.

| Client | Start from the checkout | Packaged entry point |
| --- | --- | --- |
| Machine CLI | `dotnet run --project src/Configurator.Cli --configuration Release -- doctor --machine` | `islamu-configurator` |
| Terminal | `dotnet run --project src/Configurator.Terminal --configuration Release -- --definition examples/minimal-self-hosted/setup-definition.json` | `islamu-configurator-terminal` |
| Desktop | `dotnet run --project src/Configurator.Desktop --configuration Release -- /absolute/path/setup.json /absolute/path/output-directory` | `islamu-configurator-desktop` |
| Public browser | Publish `Configurator.Browser`, then serve its complete `wwwroot/` | `/` catalogue and `/launch/<familyId>` |

The neutral public family can be opened at `/launch/69cdb122-41d2-4fab-9b82-7929a889c726?appVersion=1.0&entry=setup&locale=en`. Configure a SPA navigation fallback for deep links, not missing framework assets. Native preparation requires no Aspire, database, Keycloak or Event server.

### Contributor Onboarding Path
1. Review our [Contribution Guidelines](CONTRIBUTING.md) and [Local Getting Started](#quick-start-for-developers--contributors).
2. Read the [Project Guide](PROJECTS.md) for the 5-minute mental model and coding patterns.
3. Check the authoritative [Quick Reference (Invariants)](docs/internal/QUICK_REFERENCE.md), [Core Contract](docs/internal/CORE_DEFINITION_CONTRACT.md), and [Governance Standards](docs/internal/GOVERNANCE.md).
4. AI-assisted coding agents must strictly follow [`AGENTS.md`](AGENTS.md) and [`.agents/contract/intents.yaml`](.agents/contract/intents.yaml).

### ✍️ Contributor Licensing & Community Protection

Contributors retain ownership of their contributions. Configurator's ISLAMU-owned source uses the [MIT license][license-link]; third-party material keeps its own terms. The Steward authorized the first-party Event transfer without changing Event's separate AGPL license or service covenant.

Keep source provenance, attribution and dependency notices intact. Do not assume Event's CLA automation or alternative-license terms govern this independent utility. See [CONTRIBUTING.md](CONTRIBUTING.md) and [IP Governance](docs/internal/legal/IP_GOVERNANCE.md).

---

## 📚 Documentation: Public Guides vs. Internal Specifications

ISLAMU Configurator maintains a deliberate separation between **Public Adopter Documentation** (operator/adopter perspective) and **Internal Engineering Specifications** (developer/contributor perspective in GitHub Markdown):

### 🌐 1. Public Operator & Adopter Documentation

> 📖 **Documentation Hub:** Visit [docs/README.md](docs/README.md) for the repository guides. No separate hosted Configurator documentation portal is claimed.

* **[CLI](docs/public/cli.md)** — Machine operations, answer workspaces and deterministic envelopes.
* **[Terminal](docs/public/terminal.md)** — Keyboard-driven native preparation.
* **[Desktop](docs/public/desktop.md)** — Avalonia forms and protected saves.
* **[Public Browser](docs/public/browser.md)** — Partial public choices and static hosting.
* **[Launch Integration](docs/public/launch-integration.md)** — Family/version/entry/locale/digest selection.
* **[Definitions](docs/public/definitions.md)** — Data-only target definition authoring.
* **[Native Output](docs/public/native-output.md)** — Restricted artifacts and host limits.
* **[Packaging](docs/public/packaging.md)** — Independent native packages and static WASM output.

---

### 🛠️ 2. Internal Engineering Specifications (For Developers & Contributors)

For developers contributing code, architecture reviewers, and AI agents, use the authoritative in-repository specifications in [docs/README.md](docs/README.md):

| Engineering Concern | Authoritative In-Repository Markdown File | What You Will Learn |
|---|---|---|
| **Developer Mental Model** | [PROJECTS.md](PROJECTS.md) | Actual project ownership and targeted commands. |
| **Global Project Invariants** | [QUICK_REFERENCE.md](docs/internal/QUICK_REFERENCE.md) | Offline authority, privacy and engineering constraints. |
| **Clean Architecture** | [Runtime ADR](docs/internal/adr/ADR-definition-runtime-boundary.md) | Core → Artifacts/Presentation → adapters, without Event assemblies. |
| **Core Definition Contract** | [CORE_DEFINITION_CONTRACT.md](docs/internal/CORE_DEFINITION_CONTRACT.md) | Bounded data admission, validation and canonical identity. |
| **Coding Patterns & Conventions** | [GOVERNANCE.md](docs/internal/GOVERNANCE.md) | Naming, metadata, source documentation and scope discipline. |
| **Testing Strategy** | [TESTING.md](docs/internal/TESTING.md) | Deterministic assertions, focused owners and measured evidence. |
| **Operations & Build Verification** | [OPERATIONS.md](docs/internal/OPERATIONS.md) | Native tooling, verification and execution evidence. |
| **Machine CLI Contracts** | [CLI_CONTRACT.md](docs/internal/CLI_CONTRACT.md) | Closed operation vocabulary and output envelopes. |
| **Browser Launch** | [LAUNCH_CONTRACT.md](docs/internal/LAUNCH_CONTRACT.md) | Exact selection, public-template privacy and immutable sessions. |
| **Protected Native Output** | [PROTECTED_ARTIFACTS.md](docs/internal/PROTECTED_ARTIFACTS.md) | Create-only writes and unsupported-host refusal. |
| **Documentation Architecture** | [DOCUMENTATION_ARCHITECTURE.md](docs/internal/DOCUMENTATION_ARCHITECTURE.md) | Separate public guidance and technical source anchors. |
| **AI Agent Contribution Contract** | [AGENTS.md](AGENTS.md) / [intents.yaml](.agents/contract/intents.yaml) | Shared contribution contract and task intake. |

---

## 🏗️ Technology Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10, SDK 10.0.302 |
| Architecture | Independent Core, Artifacts and Presentation with four adapters |
| Machine CLI | Spectre.Console / Spectre.Console.Cli |
| Terminal | Audited ISLAMU.Terminal.Gui downstream package |
| Desktop | Avalonia 12.1.3, CommunityToolkit.Mvvm |
| Public browser | Static Blazor WebAssembly |
| Definition data | Bounded JSON/YAML, canonical SHA-256 identity |
| Protected output | Linux create-only native writes; other hosts fail closed |
| Dependencies | Central package versions, generated locks and retained notices |
| Tests | TUnit, bUnit, headless native controls and architecture/package tests |

## Community

Join the ISLAMU community on [Discord][discord-link] and our [GitHub Discussions][github-discussions-link]. We follow a [Code of Conduct][code-of-conduct] in all our community channels.

Feel free to ask questions, report bugs, participate in discussions, share ideas, request features, or showcase. We would love to hear from you.

> Give us a Star ⭐️

## 🛡️ Security Disclosure

If you discover a security vulnerability in ISLAMU Configurator, please report it responsibly instead of opening a public issue. We take all legitimate reports seriously and will investigate them promptly. See the [Security Policy][security-policy] for more info.

To disclose any security issues, please email us at [contact@openislamu.org][contact-email].

### Security fixes from forks

If you maintain a fork of ISLAMU Configurator and discover or fix a security vulnerability, please report it privately first using the process in our Security Policy instead of opening a public issue or public pull request before coordination.

We are grateful for security fixes from forks. Contributions must preserve Configurator's MIT and third-party licensing boundaries and include provenance and focused verification evidence.

If you cannot submit a patch, you can still help by privately sharing the vulnerability details, affected versions, reproduction steps, and the general remediation approach. The ISLAMU maintainers may then implement an independent fix.

## Contributors

### Core Maintainer

|                                                                                                                                                                            Amir Akrari                                                                                                                                                                             |
| :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------: |
|                                                                                                                                         <img src="https://github.com/amirakrari.png" width="200px" alt="Amir Akrari" />                                                                                                                                          |
| <a href="https://github.com/amirakrari"><img src="https://api.iconify.design/devicon:github.svg" width="25px"></a> <a href="https://bsky.app/profile/amirakrari.bsky.social"><img src="https://api.iconify.design/simple-icons:bluesky.svg" width="25px"></a> |

I am deeply grateful to all our amazing contributors.

[![Contributors Image][contributors-image]][contributors-link]

## 💖 Sponsors

Support ISLAMU Configurator's ongoing development, infrastructure, and open-source mission by becoming a financial sponsor.

> ℹ️ **Recognition Policy:** Active tiers display ongoing monthly recurring sponsors. One-time contributions and sponsors who have stopped their monthly support are recognized under **Old Sponsors**.

### 🥇 Gold Sponsors

*No active Gold sponsors yet.*

* **Monthly Support (€250+/mo):** [👉 Support Monthly via Stripe][stripe-monthly-link]

#### Old Sponsors

* **One-Time Contribution:** [👉 Donate Once via Stripe][stripe-oneoff-link]

### 🥈 Silver Sponsors

*No active Silver sponsors yet.*

* **Monthly Support (€100+/mo):** [👉 Support Monthly via Stripe][stripe-monthly-link]

#### Old Sponsors

* **One-Time Contribution:** [👉 Donate Once via Stripe][stripe-oneoff-link]

### 🥉 Bronze Sponsors

*No active Bronze sponsors yet.*

* **Monthly Support (€25+/mo):** [👉 Support Monthly via Stripe][stripe-monthly-link]

#### Old Sponsors

* **One-Time Contribution:** [👉 Donate Once via Stripe][stripe-oneoff-link]

### ☕ Community Sponsors

*No active Community sponsors yet.*

* **Monthly Support (€5+/mo):** [👉 Support Monthly via Stripe][stripe-monthly-link]

#### Old Sponsors

* **One-Time Contribution:** [👉 Donate Once via Stripe][stripe-oneoff-link]

> 💼 To discuss corporate sponsorship, custom invoicing, or enterprise partnerships, email us at [contact@openislamu.org][contact-email].

## 🤝 In-Kind Supporters

Special thanks to the organizations providing free tooling, infrastructure, and licenses to support ISLAMU’s open-source development. These source-project acknowledgements do not imply a Configurator-specific service or sponsorship is activated:

| Organization | Contribution & Usage |
|:---:|---|
| [![GitBook logo](https://api.iconify.design/simple-icons:gitbook.svg?color=%233884FF)](https://www.gitbook.com) [**GitBook**](https://www.gitbook.com) | Generously provides their **Community Plan** to host and power our [Official Public Documentation](https://islamu.gitbook.io/islamu-event). |
| [![Gitar logo](https://assets-eu-01.kc-usercontent.com/ef593040-b591-0198-9506-ed88b30bc023/22779193-4b6d-4bdf-a860-37007a6d2315/gitar-wordmark.svg?w=128&h=32&fit=clip&q=80&auto=format)](https://gitar.ai) [**Sonar Gitar**](https://gitar.ai) | Generously provides their **Gitar Open Source Program** to empower our development with AI code review |

> If your organization would like to support ISLAMU Configurator with in-kind tooling, infrastructure, or software licenses, please reach out to us at [contact@openislamu.org][contact-email].

## 🧰 Public-Repository Tools

We appreciate these generally available public-repository tools. They are not project-specific sponsorships.

- [CodeRabbit][coderabbit-link]: AI code reviews for pull requests.
- [Codecov][codecov-link]: Test coverage reporting.
- [Socket.dev][socket-link]: Dependency security analysis.
- [Imgbot][imgbot-link]: Automated image optimization.
- [Coverity][coverity-scan-link]: Static analysis code scanning you can trust.
- [SonarQube Cloud][sonarqube-cloud-link]: Code quality and security analysis.

## ISLAMU Solutions

- [ISLAMU Event][event-repo-link]: Event Platform & Management System.
- [ISLAMU Configurator][github-repo-link]: Offline Configuration Preparation Utility.
- [I-VSD][ivsd-github-repo-link]: Islamic Value Sensitive Design: A Framework for Provider-Mediated Software Solutions

## 🙏 Acknowledgement

These projects support the wider ISLAMU ecosystem. Configurator’s direct library dependencies are listed separately below.

- [Infisical][infisical-link]: An Open Source Secret Management Platform.
- [MudBlazor][mudblazor-link]: An Open Source Blazor UI library that simplifies the creation of beautiful websites and webapps.
- [Penpot][penpot-link]: An Open Source Design Tool.
- [Plane][plane-link]: An Open Source Project Management Platform that unifies projects, knowledge and agents with all-in-one workspace: projects, wiki, and AI.
- [Coolify][coolify-link]: An Open Source Platform as a Service, alternative to Vercel, Heroku, Netlify, and Railway for easy deploying to your own servers.
- [Weblate][weblate-link]: An Open Source Translation Management Platform.
- [Kener][kener-link]: An Open Source Status Page.
- [Formbricks][formbricks-link]: Optional surveys and feedback collection.

### Open-Source Libraries & Dependencies

Our codebase is enriched by dozens of community-crafted .NET libraries. For the complete, centrally managed dependency list and version pins, see [`Directory.Packages.props`](Directory.Packages.props).

- [.NET][dotnet-link]: The application runtime.
- [ASP.NET Core][aspnet-core-link]: The web and API framework.
- [Avalonia][avalonia-link]: Native desktop UI.
- [Terminal.Gui][terminal-gui-link]: Audited downstream terminal UI.
- [CommunityToolkit.Mvvm][communitytoolkit-link]: Desktop model/viewmodel support.
- [Spectre.Console][spectre-link]: CLI command and presentation support.
- [YamlDotNet][yamldotnet-link]: Bounded YAML data processing.
- [TUnit][tunit-link]: Automated .NET tests.
- [bUnit][bunit-link]: Blazor component tests.

*A heartfelt thank you to every open-source author and maintainer whose work (direct or transitive) helps make this project possible.*

## 🌱 Sustainability

ISLAMU is built by one person — **Amir Akrari** — entirely on personal free time and personal funds.

**ISLAMU (ASBL en formation)** (Association Sans But Lucratif — a non-profit association in formation in Belgium) is being established as the operational and legal steward of all ISLAMU open-source projects and charitable activities. Once formally registered with legal personality, ISLAMU ASBL will assume full legal stewardship.

### How ISLAMU sustains itself

ISLAMU will not offer its open-source software as a hosted SaaS. Instead, sustainability is built on:

- **Fundraising & Grants:** Crowdfunding campaigns, foundation grants, and public-interest funding for open-source digital infrastructure
- **Sponsorships:** Ranked sponsorship tiers — sponsors receive recognition and visibility; every euro goes back into the non-profit
- **Official Partnerships & Fleet Tooling:** Organizations that want to offer ISLAMU software as a hosted service can become **Official ISLAMU Partners**. Partners are vetted, listed on the ISLAMU website, and actively recommended and marketed by ISLAMU. Official Partners receive access to **ISLAMU Event Control Plane** (our standalone fleet orchestrator that bridges multiple ISLAMU Event instances with PaaS engines like Coolify for automated provisioning, SLA management, and backups). ISLAMU does not compete with its partners by running a commercial SaaS; we empower our partners with tooling and referrals
- **Consultation & Support** — professional consulting around ISLAMU software deployment, customization, and integration

If you are a company or institution that wants to support this work, reach out at [contact@openislamu.org][contact-email] or start a conversation in our [Discord][discord-link].

## 📞 Contact

For any question or problem reporting, please consider opening a [new issue][github-issues-link] or send an email to [contact@openislamu.org][contact-email] or create a new post in our [Discord Server][discord-link].

## Privacy Policy

You can find details [here][privacy-policy].

## 📄 License & Ecosystem Architecture

This project is licensed under the terms of the [MIT license][license-link].

### 🌐 The ISLAMU Ecosystem

Configurator is a standalone offline utility. Event remains a separately governed application with its own AGPL community/enterprise/partner model. Configurator's MIT license does not impose Event's AGPL service covenant or create a shared runtime dependency.

The Steward's transfer authority covers ISLAMU-owned material only. Third-party libraries, datasets, fonts and assets retain their respective licenses, disclaimers and other terms. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), [IP Governance](docs/internal/legal/IP_GOVERNANCE.md) and [Transfer Provenance](docs/internal/extraction/PROVENANCE.md).

### Standalone Core And Optional Clients

Core admits bounded definitions without Event backend/domain/wire assemblies. Artifacts owns protected native writes, Presentation owns shared immutable workspace generations, and CLI, Terminal.Gui, Avalonia Desktop and Blazor WebAssembly adapt those boundaries independently.

The audited `ISLAMU.Terminal.Gui` package identity, approval, patch, hashes, SBOM and notices are preserved. Do not substitute upstream Terminal.Gui. Signed publication remains fail-closed until Configurator establishes its own trust authority; unsigned local/CI validation packages do not activate publication.

<div align="right">

[![][back-to-top]][back-to-top-link]

</div>

<!-- LINK GROUP -->

[back-to-top]: https://img.shields.io/badge/-BACK_TO_TOP-151515?style=flat-square
[back-to-top-link]: #readme-top
[islamu-platform]: https://event.openislamu.org
[official-docs-link]: docs/README.md
[roadmap-link]: https://sites.plane.so/views/b8b7d9fced694f5a9d9a546e9d40d988
[code-of-conduct]: CODE_OF_CONDUCT.md
[security-policy]: SECURITY.md
[privacy-policy]: https://openislamu.org/privacy
[license-link]: LICENSE
[contact-email]: mailto:contact@openislamu.org

[github-repo-link]: https://github.com/islamu-ngo/Configurator
[github-issues-link]: https://github.com/islamu-ngo/Configurator/issues/new
[github-discussions-link]: https://github.com/islamu-ngo/Configurator/discussions
[github-contributors-link]: https://github.com/islamu-ngo/Configurator/graphs/contributors

[github-workflow-status-shield]: https://img.shields.io/github/actions/workflow/status/islamu-ngo/Configurator/build-test.yml?branch=main&logo=github&style=flat-square
[coverity-scan-shield]: https://img.shields.io/coverity/scan/33311.svg
[coverity-scan-link]: https://scan.coverity.com/projects/islamu-ngo-event
[github-stars-shield]: https://img.shields.io/github/stars/islamu-ngo/Configurator?color=594ae2&style=flat-square&logo=github
[github-stars-link]: https://github.com/islamu-ngo/Configurator/stargazers
[github-license-shield]: https://img.shields.io/github/license/islamu-ngo/Configurator?color=594ae2&logo=github&style=flat-square
[github-license-link]: https://github.com/islamu-ngo/Configurator/blob/main/LICENSE
[github-last-commit-shield]: https://img.shields.io/github/last-commit/islamu-ngo/Configurator?color=594ae2&style=flat-square&logo=github
[github-last-commit-link]: https://github.com/islamu-ngo/Configurator
[github-contributors-shield]: https://img.shields.io/github/contributors/islamu-ngo/Configurator?color=594ae2&style=flat-square&logo=github
[github-discussions-shield]: https://img.shields.io/github/discussions/islamu-ngo/Configurator?color=594ae2&logo=github&style=flat-square
[discord-shield]: https://img.shields.io/discord/1357505436479131668?color=%237289da&label=Discord&logo=discord&logoColor=%237289da&style=flat-square
[discord-link]: https://discord.gg/wrkY824Yv5
[docs-shield]: https://img.shields.io/badge/Docs-Markdown-3884FF?style=flat-square&logo=markdown&logoColor=white
[stripe-shield]: https://img.shields.io/badge/Donate-Stripe-635BFF?style=flat-square&logo=stripe&logoColor=white

[repobeats-image]: https://repobeats.axiom.co/api/embed/a0f11a3d9b80342b5f5965127c2c45871c9d3397.svg
[contributors-image]: https://contrib.rocks/image?repo=islamu-ngo/Configurator
[contributors-link]: https://github.com/islamu-ngo/Configurator/graphs/contributors

[mudblazor-link]: https://www.mudblazor.com/
[weblate-link]: https://weblate.org/
[penpot-link]: https://penpot.app/
[plane-link]: https://plane.so/
[coolify-link]: https://coolify.io/
[kener-link]: https://kener.ing/
[gitbook-link]: https://www.gitbook.com/
[sonar-gitar-link]: https://gitar.ai/
[coderabbit-link]: https://www.coderabbit.ai/
[codecov-link]: https://about.codecov.io/
[socket-link]: https://socket.dev/
[imgbot-link]: https://imgbot.net/
[sonarqube-cloud-link]: https://www.sonarsource.com/products/sonarqube/cloud/
[dotnet-link]: https://dotnet.microsoft.com/
[aspnet-core-link]: https://dotnet.microsoft.com/apps/aspnet
[ef-core-link]: https://learn.microsoft.com/ef/core/
[carpanet-link]: https://github.com/drasticactions/CarpaNet
[quartz-link]: https://www.quartz-scheduler.net/
[yarp-link]: https://microsoft.github.io/reverse-proxy/
[ical-net-link]: https://github.com/ical-org/ical.net
[mailkit-link]: https://github.com/jstedfast/MailKit
[opentelemetry-link]: https://opentelemetry.io/
[serilog-link]: https://serilog.net/
[tunit-link]: https://tunit.dev/
[bunit-link]: https://bunit.dev/
[stripe-monthly-link]: https://donate.stripe.com/bJeeVeesc9kIdpCbMPaR201
[stripe-oneoff-link]: https://donate.stripe.com/14A6oIesc0Oc2KYg35aR200
[stripe-donation-link]: https://donate.stripe.com/bJeeVeesc9kIdpCbMPaR201

[ivsd-github-repo-link]: https://github.com/islamu-ngo/Islamic-Value-Sensitive-Design

[event-repo-link]: https://github.com/islamu-ngo/Event
[avalonia-link]: https://avaloniaui.net/
[terminal-gui-link]: https://github.com/gui-cs/Terminal.Gui
[communitytoolkit-link]: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/
[spectre-link]: https://spectreconsole.net/
[yamldotnet-link]: https://github.com/aaubry/YamlDotNet
