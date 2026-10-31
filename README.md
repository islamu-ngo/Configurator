<a name="readme-top"></a>

<div align="center">

<img src="https://raw.githubusercontent.com/islamu-ngo/Event/develop/assets/images/adopters/ISLAMU/islamu-logo-text-only-v2.png" alt="ISLAMU logo" width="200" />

# ISLAMU Configurator

Open-source, offline configuration preparation software for application authors, operators, and self-hosters.

ISLAMU Configurator prepares application-owned configuration artifacts, while the target application retains validation, authorization and apply authority.

> 🚧 **Active Pre-Release Development:** ISLAMU Configurator has **no official release yet**. Native clients, the static configuration browser and local validation packages are developed here. No public release, signed publication or hosted deployment is claimed.

![GitHub Workflow Status][github-workflow-status-shield]
[![GitHub License][github-license-shield]][github-license-link]
[![GitHub Repo Stars][github-stars-shield]][github-stars-link]
[![GitHub Last Commit][github-last-commit-shield]][github-last-commit-link]
[![Contributors][github-contributors-shield]][github-contributors-link]
[![Discussions][github-discussions-shield]][github-discussions-link]
[![Discord][discord-shield]][discord-link]
[![Documentation][docs-shield]][official-docs-link]
[![Donate via Stripe][stripe-shield]][stripe-donation-link]

[**Documentation**](docs/README.md) · [**Roadmap**][roadmap-link] · [**Quickstart**](#quick-start-for-developers--contributors) · [**Packaging**](docs/public/packaging.md) · [**Developer Guide**](docs/README.md) · [**Sponsor**][stripe-donation-link]

</div>

## About ISLAMU Configurator

ISLAMU Configurator is an **offline configuration preparation utility** for self-hosted applications. A bounded data-only definition describes questions, validation, activation and output. The target application remains responsible for accepting and applying the resulting artifact.

The software is **purpose-agnostic**: Event is one external definition target, not a compiled dependency of the engine. A family GUID identifies a definition family; it is not authentication, publisher verification or host authority.

Choosing a client for sensitive configuration? Start with the [safety and trust FAQ](#faq).

## ✨ Who It Serves

### Application Operators

- **🔍 Definition-Guided Configuration:** Inspect a local definition's questions, constraints and artifact choices before entering answers.
- **⌨️ Keyboard-Driven Preparation:** Use the retained Terminal.Gui workflow with transient private input and protected native exports — see [Terminal](docs/public/terminal.md).
- **🖥️ Native Desktop Forms:** Prepare configuration in the Avalonia desktop client using the same admitted definition and shared workspace — see [Desktop](docs/public/desktop.md).
- **📜 Deterministic Machine Output:** Use generic catalogue, artifact, environment and doctor commands with a closed JSON result envelope — see [CLI](docs/public/cli.md).
- **🔒 Restricted Native Output:** Answer-derived artifacts remain restricted; verified Linux writes are create-only and unsupported protected-save hosts reject — see [Native Output](docs/public/native-output.md).

### Application Authors

- **One maintainer source:** Author YAML or JSON once, then check and compile it with native `definition init/check/build/revise`; use `init --fork-from` for a new family. Start with [portable adoption](docs/public/integrating.md), the [definition cookbook](docs/public/definition-cookbook.md), [maintained examples](examples/README.md) and the [portable agent workflow](skills/configurator-integrate/SKILL.md).
- **🧩 Declarative Definitions:** Describe fields, activation, validation and output using bounded data rather than executable plugins — see [Definitions](docs/public/definitions.md).
- **🔗 Pinned Public Launch:** Link to a registered family with bounded version, entry, locale and digest selectors; the session retains its admitted revision and digest — see [Launch Integration](docs/public/launch-integration.md).
- **🌐 Complete Browser Preparation:** The static Blazor app collects ordinary settings and bounded tenant rows, with voluntary workspace-local secret entry or explicit secret-deferred drafts. Copy/download transfers restricted files deliberately — see [Browser Configuration](docs/public/browser.md).
- **🛡️ Target-Owned Authority:** Keep authentication, semantic acceptance and apply authorization in your application. Local preparation does not grant administration.

### Platform Owners & Self-Hosters

- **🆓 Free & Open Source:** ISLAMU-owned material is MIT-licensed; third-party components retain their terms and notices.
- **🛡️ Local Preparation:** Preparation has no Event API, domain, wire or database dependency. The browser has no target authentication, telemetry or answer persistence; bounded public HTTPS definitions are admitted and pinned before answers.
- **🧰 Independent Clients:** Choose the machine CLI, Terminal.Gui TUI, Avalonia Desktop or static Blazor WebAssembly browser without installing Event services.
- **📚 Dual Documentation Tracks:** Public guides explain operator workflows; internal contracts explain architecture, privacy and verification.
- **📜 Real Reference Target:** Event supplies a neutral source and retains its host validators. The maintained v2 source compiles into canonical runtime; five independent target payload families exercise generic projections. Its historical v1 release receipt is not a current v2 compiler or target-acceptance receipt. See [Event example](examples/islamu-event/README.md).

---

## 🚀 Deployment & Hosting Options

ISLAMU Configurator separates native preparation from public static hosting:

| Topology Mode | Target Use Case | Step-by-Step Documentation |
|---|---|---|
| **Machine CLI** | Reproducible local automation with explicit definitions and answer workspaces. | 📖 **[CLI Guide](docs/public/cli.md)** |
| **Terminal Client** | Keyboard-driven native preparation with protected output. | 📖 **[Terminal Guide](docs/public/terminal.md)** |
| **Desktop Client** | Avalonia native forms over the shared workspace. | 📖 **[Desktop Guide](docs/public/desktop.md)** |
| **Static Configuration Browser** | Transient local answers, deliberate file export and a public-asset PWA cache. | 📖 **[Browser Hosting](docs/public/browser.md#self-hosting--deployment)** |

See [Standalone Packaging](docs/public/packaging.md) for supported RIDs, executable names and package boundaries. Protected native writes are verified on Linux only. Browser offline reopening requires complete public app assets and an explicitly retained admitted definition; answers are never retained. Browser downloads cannot establish native owner-only/create-only protection. No public Configurator deployment is claimed.

### Portable definition adoption

Keep a public source and canonical runtime beside your application, including
on local media; the Definition Store and GitHub are optional distribution
channels, not runtime dependencies. Authoring v2, runtime v2 (`bounded-v2`),
command envelope v4 and private answers v1 are separate contracts. Native
tooling allocates family identity; changed canonical content needs a new
revision, while a fork starts a new family at revision 1.

The CLI, Terminal.Gui TUI, Avalonia Desktop and Blazor WebAssembly Browser use
the same admitted definition and Core value semantics. Native clients publish
restricted files through protected save; Browser transfers files by deliberate
copy/download and needs workspace-local consent for secret entry. Answers use
exact configuration property names, not generated field IDs or dot-inferred
nesting. Standard `default` is descriptive; `x-configurator.initialValue`
initializes eligible missing values, while `suggestedValue` needs deliberate
adoption. Omission suppresses initialization; reset restores eligibility.
Typed null is a supplied value only where nullable. A secret-deferred
draft remains incomplete and restricted. Neither successful compilation nor
generated files prove target acceptance or authorize apply/restart.

See [adoption steps and boundaries](docs/public/integrating.md), the
[cookbook](docs/public/definition-cookbook.md), [schema generation](schemas/README.md)
and the [Core mechanism map](docs/internal/CORE_DEFINITIONS.md).
Actual package/host qualification and target acceptance require separate
evidence; these guides do not claim final physical completion.

---

## 🎨 Branding & Customization

Application authors own definition family identity, publisher labels, application versions, entry points and locale metadata. Clients preserve the admitted definition's canonical identity rather than silently changing its revision or content.

- **Application Metadata:** Describe your target without hardcoded Event assumptions.
- **Public Integration:** Bundle safe public definition metadata and link to its pinned launch contract.
- **Target Governance:** Keep operator permissions, secret authority and configuration acceptance in the application that owns them.

---

## 🛡️ Enterprise Security & Compliance

The implementation enforces specific admission, custody and output boundaries;
these are not a security certification:

- **🔐 Definition Admission:** Closed schemas and bounded values reject unsupported definitions before use.
- **🛡️ Session Integrity:** Family, revision and digest remain pinned; removed revisions cannot return with conflicting content.
- **🔒 Private Artifacts:** Native answer-derived output is restricted and cannot overwrite an existing protected artifact.
- **🌐 Browser Custody:** Secret entry requires per-workspace consent; revocation clears secret leaves and output. Drafts visibly defer secrets, public caches exclude answers/artifacts, and neither preparation mode grants host authority.
- **🗝️ Target Authority:** The target performs semantic validation and authorization; Configurator does not authenticate or apply remotely.

See [SECURITY.md](SECURITY.md), [Core Contract](docs/internal/CORE_DEFINITION_CONTRACT.md) and [Protected Artifacts](docs/internal/PROTECTED_ARTIFACTS.md).

---

## FAQ

### Is it safe to enter my configuration and secrets?

Choose the interface according to your security needs. **For sensitive configuration, treat the hosted web app as the least safe option among Configurator's interfaces.** You must trust its origin, the code it delivers, your browser and extensions; browser downloads also lack native protected-save guarantees. Prefer a reviewed local [Desktop](docs/public/desktop.md), [TUI](docs/public/terminal.md) (terminal interface) or [CLI](docs/public/cli.md) build on a trusted machine when handling high-value secrets. Native clients still require trust in the software and operating system. Protected native saving is currently verified on Linux only; unsupported hosts refuse it.

The web app offers a lower-exposure path: leave secret entry disabled, prepare an incomplete **secret-deferred draft**, and fill in the documented secret locations locally. Ordinary settings can also be confidential, so review what you enter even when no passwords are involved. There is no official release or public hosted deployment yet; this is a design choice, not a security certification. See [Browser Configuration](docs/public/browser.md#data-custody--security-architecture).

### Why did ISLAMU Configurator choose WebAssembly interactivity?

**To keep configuration processing in your browser and support offline work.** The static Blazor WebAssembly app runs its interactive forms, validation and file preparation locally. Answers and generated files are excluded from application network requests, persistent storage and public caches. Static hosting describes how the app is delivered; the UI is still interactive.

Interactive Server could provide a smaller initial download and move computation off your device, but bound form values would reach the server and editing would depend on a live connection. Interactive Auto can use the server on initial visits and WebAssembly on later visits, making the processing location less predictable. Configurator chooses a consistent local boundary. WebAssembly costs a larger initial download and uses your device's resources, then avoids server round trips for local interactions. MudBlazor supports WebAssembly; it does not require server interactivity. Read the [hosting rationale and trade-offs](docs/public/browser.md#why-webassembly-interactivity).

### Can I use the web app without entering secrets?

Yes. Secret entry starts disabled in every new workspace. Complete the ordinary required settings and prepare a secret-deferred draft, then finish the indicated secret locations locally. Missing ordinary required values, invalid answers and unresolved secret-dependent conditions still block preparation. A draft is incomplete and may be rejected by the target until completed.

### What changes if I enable secret entry?

You explicitly trust the hosted code and your browser environment with secrets for that workspace. **This enables local processing; it does not switch to server processing.** Revoking consent clears secret fields, including nested values, and invalidates prepared output while retaining ordinary settings. Consent is not remembered for another workspace. Masked controls hide values on screen; they do not protect against compromised code or extensions.

### Does offline browser use remove the need for trust?

No. Prepare the public app assets and retain the public definition before entering answers, then physically disconnect for stronger protection against live network transmission. You still need to trust the downloaded code, browser and device. An installed PWA shortcut alone does not prove offline readiness. Answers are not saved between workspaces, and browser-managed update checks can generate public-asset traffic while connected. See [offline work](docs/public/browser.md#offline-work--pwa-support).

### Are downloaded files and copied secrets protected?

Browser copy/download is an explicit action, but Configurator cannot enforce owner-only permissions, prevent overwriting or control clipboard and download history. Check the saved filename and protect the destination yourself. Clearing the workspace cannot retract files or clipboard content already exported. Native clients offer the [protected save contract](docs/public/native-output.md) on verified Linux hosts; clearing application state is not a forensic-erasure guarantee in either environment.

### Does successful preparation mean my application will accept the files?

No. Configurator validates the admitted definition and prepares its declared files. The target application still owns semantic validation, authentication, authorization and applying the configuration. Definition identifiers, publisher labels and digests are not publisher authentication or administrative permission. Review the target's requirements before using the output.

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

For local development and code contribution, install .NET SDK **10.0.302**
and the pinned workloads described in [PROJECTS.md](PROJECTS.md). The following
Bash commands create a checkout, restore dependencies (network/cache required),
build it, and inspect native writer availability. They do not prepare target
configuration. If already in this checkout, skip clone and `cd`; rerunning restore
and build updates local build outputs.

```bash
git clone https://github.com/islamu-ngo/Configurator.git
cd Configurator
export TMPDIR="$HOME/.cache/agent-tmp"
mkdir -p "$TMPDIR"
dotnet restore Configurator.slnx --locked-mode
dotnet build Configurator.slnx --configuration Release --verbosity quiet
dotnet run --project src/Configurator.Cli --configuration Release -- doctor --machine
```

The CLI registers `catalogue list|show|describe`,
`artifact create|open|validate|format|diff|coverage|export|import|check-existing|compare|prefill|bundle-check|bundle-create|preview`,
`env render|validate`, `definition init|check|lint|schema|build|revise`, and
`doctor`. Use `--help` for operation-specific options and `--machine` for the
closed JSON envelope. Select a local definition explicitly; there is no
implicit Event target or remote apply command.

| Client | Start from the checkout | Packaged entry point |
| --- | --- | --- |
| Machine CLI | `dotnet run --project src/Configurator.Cli --configuration Release -- doctor --machine` | `islamu-configurator` |
| Terminal | `dotnet run --project src/Configurator.Terminal --configuration Release -- --definition examples/minimal-self-hosted/setup-definition.json` | `islamu-configurator-terminal` |
| Desktop | `dotnet run --project src/Configurator.Desktop --configuration Release -- /absolute/path/setup.json /absolute/path/output-directory` | `islamu-configurator-desktop` |
| Browser | Publish `Configurator.Browser`, then serve its complete `wwwroot/` | `/` catalogue, `/launch/<familyId>` and pinned `/open#src=…` |

The Desktop row is an invocation template: substitute an admitted local
definition and an existing trusted output directory. Terminal/Desktop launch
opens an interactive workspace; saving remains a deliberate later action.
Doctor only reports writer availability, not whether a chosen mount can save.

The maintained neutral v2 family can be opened on your own Browser host at
`/launch/9b6a7b3e-45bf-4485-8fa1-d7888938c09b?appVersion=1.0&entry=setup&locale=en`.
This is a relative route, not an official deployment. Configure a SPA navigation
fallback for deep links, not missing framework assets. Native preparation
requires no Aspire, database, Keycloak or Event server.

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

* **[Overview & Mental Model](docs/public/overview.md)** — What Configurator is, how the 3-step model works, and interface comparisons.
* **[Quickstart](docs/public/getting-started.md)** — Step-by-step walkthrough preparing configuration offline.
* **[Glossary](docs/public/glossary.md)** — Plain-language definitions for all domain concepts.
* **[CLI Reference](docs/public/cli.md)** — Machine operations, answer workspaces and deterministic envelopes.
* **[Terminal](docs/public/terminal.md)** — Keyboard-driven native preparation with Terminal.Gui.
* **[Desktop](docs/public/desktop.md)** — Avalonia forms and protected saves.
* **[Browser Configuration](docs/public/browser.md)** — Transient complete preparation, secret-deferred drafts, deliberate exports and static PWA hosting.
* **[Launch Integration](docs/public/launch-integration.md)** — Family/version/entry/locale/digest selection.
* **[Definitions](docs/public/definitions.md)** — Data-only target definition authoring.
* **[Native Output](docs/public/native-output.md)** — Restricted artifacts and host limits.
* **[Packaging](docs/public/packaging.md)** — Independent native packages and static WASM output.
* **[Troubleshooting](docs/public/troubleshooting.md)** — Diagnostics, recovery steps, and platform limitations.
* **[Comparison](docs/public/comparison.md)** — Architectural comparison with RunTipi, Backstage, Umbrel, Portainer, and Helm.

---

### 🛠️ 2. Internal Engineering Specifications (For Developers & Contributors)

For developers contributing code, architecture reviewers, and AI agents, use the authoritative in-repository specifications in [docs/README.md](docs/README.md):

Use the [current status](docs/internal/STATUS.md) to separate implemented behavior
from qualification and future work, and the
[knowledge map](docs/internal/KNOWLEDGE_MAP.md) to find the owning reference.

| Engineering Concern | Authoritative In-Repository Markdown File | What You Will Learn |
|---|---|---|
| **Vision & Core Goals** | [VISION_AND_APPROACH.md](docs/internal/VISION_AND_APPROACH.md) | The configuration experience problem, 7 core goals, and product boundaries. |
| **Design Alternatives** | [DESIGN_ALTERNATIVES.md](docs/internal/DESIGN_ALTERNATIVES.md) | Structured record of adopted, considered, and rejected architectural attempts. |
| **Architecture Overview** | [ARCHITECTURE.md](docs/internal/ARCHITECTURE.md) | Mermaid component diagrams, data flow pipeline, and layer dependency rules. |
| **Developer Mental Model** | [PROJECTS.md](PROJECTS.md) | Actual project ownership and targeted commands. |
| **Global Project Invariants** | [QUICK_REFERENCE.md](docs/internal/QUICK_REFERENCE.md) | Offline authority, privacy and engineering constraints. |
| **Clean Architecture** | [Runtime ADR](docs/internal/adr/ADR-definition-runtime-boundary.md) | Core → Artifacts/Presentation → adapters, without Event assemblies. |
| **Core Definition Contract** | [CORE_DEFINITION_CONTRACT.md](docs/internal/CORE_DEFINITION_CONTRACT.md) | Bounded data admission, validation and canonical identity. |
| **Coding Patterns & Conventions** | [GOVERNANCE.md](docs/internal/GOVERNANCE.md) | Naming, metadata, source documentation and scope discipline. |
| **Testing Strategy** | [TESTING.md](docs/internal/TESTING.md) | Deterministic assertions, focused owners and measured evidence. |
| **Operations & Build Verification** | [OPERATIONS.md](docs/internal/OPERATIONS.md) | Native tooling, verification and execution evidence. |
| **Machine CLI Contracts** | [CLI_CONTRACT.md](docs/internal/CLI_CONTRACT.md) | Closed operation vocabulary and output envelopes. |
| **Browser Launch** | [LAUNCH_CONTRACT.md](docs/internal/LAUNCH_CONTRACT.md) | Pinned bounded sources, transient custody and immutable sessions. |
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
| Terminal | Official Terminal.Gui, centrally pinned with retained component notices |
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

* **Monthly Support :** [👉 Support Monthly via Stripe][stripe-monthly-link]

* **One-Time Contribution:** [👉 Donate Once via Stripe][stripe-oneoff-link]

> 💼 To discuss corporate sponsorship, custom invoicing, or enterprise partnerships, email us at [contact@openislamu.org][contact-email].

## 🤝 In-Kind Supporters

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
- [SecretKit][secretkit-repo-link]: Unified secret management SDK for .NET

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
- [Terminal.Gui][terminal-gui-link]: Official pinned terminal UI.
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

Terminal.Gui uses its official NuGet package with an exact central pin, generated locks and retained notices for embedded third-party assets. The [dependency decision and history](docs/internal/TERMINAL_GUI_DEPENDENCY.md) explain retirement of the temporary downstream build. Signed publication remains fail-closed until Configurator establishes its own trust authority; unsigned local/CI validation packages do not activate publication.

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
[infisical-link]: https://infisical.com/
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
[secretkit-repo-link]: https://github.com/islamu-ngo/SecretKit

[avalonia-link]: https://avaloniaui.net/
[terminal-gui-link]: https://github.com/gui-cs/Terminal.Gui
[communitytoolkit-link]: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/
[spectre-link]: https://spectreconsole.net/
[yamldotnet-link]: https://github.com/aaubry/YamlDotNet
