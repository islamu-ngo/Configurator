<a name="readme-top"></a>

<div align="center">

<img src="https://raw.githubusercontent.com/islamu-ngo/Event/develop/assets/images/adopters/ISLAMU/islamu-logo-text-only-v2.png" alt="ISLAMU logo" width="200" />

# ISLAMU Configurator

Understand your application's settings. Prepare the configuration it needs.

Guided configuration, validation, secret generation and local file preparation through a web app, desktop app, terminal interface or CLI. Open source and built for local processing.

> 🚧 **Active Pre-Release Development:** There is **no official release or public hosted Configurator app yet**. You can build the clients or self-host the browser from this repository. Features and platform support are still being developed and verified; see [current status](docs/internal/STATUS.md).

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

Setting up an application often means working through long environment-variable lists, nested configuration files and unfamiliar security settings. ISLAMU Configurator turns those requirements into a guided workspace: understand each setting, choose the relevant options, correct errors and prepare the files your application expects.

A maintainer supplies a **configuration definition** in YAML or JSON. It describes the settings, explanations, validation rules, secret requirements and output files. Configurator uses that definition across its interfaces to prepare formats such as `.env`, JSON and YAML. Configuration logic stays with the application maintainer, so operators can use familiar deployment tools and hosting environments.

Browser-first development supports a simple adoption path: follow a **Configure** link in an application's README and open its configuration workspace without installing a native client. The native clients serve local and remote-shell workflows, while the CLI supports automation. The longer-term TUI direction is a full configuration workbench; interface capabilities and layout differ today.

Configurator prepares configuration locally. You review and use the resulting files with your application; it remains responsible for accepting them and authorizing changes. Start with the [overview](docs/public/overview.md), [worked quickstart](docs/public/getting-started.md) or [FAQ](#faq).

## ✨ Who It Serves

### Application Operators

For people setting up an application or reviewing a configuration they already run.

- **Understand what to enter:** Read field explanations and requirements alongside the setting, with related options shown according to the definition.
- **Find mistakes before deployment:** Review missing settings and invalid values, then follow correction guidance instead of discovering every problem at startup.
- **Handle credentials deliberately:** Generate supported passwords or key material, reveal values when needed, and keep sensitive entry masked by default. Available recipes come from the definition.
- **Reuse existing work:** Compare a supported local configuration file with the selected definition and deliberately fill eligible fields. See [import and comparison](docs/public/import-and-compare.md).
- **Choose how you work:** Use the [Browser](docs/public/browser.md), [Desktop](docs/public/desktop.md), [Terminal](docs/public/terminal.md) or [CLI](docs/public/cli.md), then prepare the application's files for review and use.

### Application Authors

For maintainers who want to offer a useful setup experience without building a separate configuration UI for every interface.

- **Describe configuration once:** Maintain one YAML or JSON source for fields, conditions, validation, guidance and file mappings. The clients share the same configuration semantics.
- **Let an AI agent write your definition:** Give your coding agent the [Configurator Definition skill](skills/configurator-integrate/SKILL.md) to create your application's `configurator.yaml` or `configurator.json` from its public configuration declarations and schemas. The skill guides the agent through authoring and CLI validation, leaving you a definition to review and ship with your application.
- **Put setup next to your documentation:** Publish a definition with your application and add a **Configure** link to a browser host. You can distribute definitions directly; the optional Definition Store is not required.
- **Explain the difficult parts:** Add sections, localized help, choice descriptions and credential requirements so operators understand the decisions they are making.
- **Keep your existing file formats:** Map answers to the files your application reads, including multiple outputs from one configuration workspace.
- **Evolve definitions with your application:** Check, compile and revise the definition with the authoring CLI, while keeping final application validation and deployment under your control.

Start with [the integration guide](docs/public/integrating.md), [definition cookbook](docs/public/definition-cookbook.md) or [examples](examples/README.md).

---

## 🚀 Deployment & Hosting Options

Choose the interface that fits the task. They share the configuration engine, while their interaction, file access and secret-handling boundaries differ:

| Topology Mode | Target Use Case | Step-by-Step Documentation |
|---|---|---|
| **Machine CLI** | Validate, compare and prepare files in scripts or automated workflows. | 📖 **[CLI Guide](docs/public/cli.md)** |
| **Terminal Client** | Configure with the keyboard in a local terminal or remote shell. | 📖 **[Terminal Guide](docs/public/terminal.md)** |
| **Desktop Client** | Work through native forms with local file access and protected saving. | 📖 **[Desktop Guide](docs/public/desktop.md)** |
| **Static Configuration Browser** | Open a guided workspace without a native installation; self-host it as static files. | 📖 **[Browser Hosting](docs/public/browser.md#self-hosting--deployment)** |

See [packaging](docs/public/packaging.md) for build instructions and platform limits. Protected native saving is currently verified on Linux; unsupported hosts refuse it. Browser downloads use the browser's ordinary download behavior. Offline browser use requires the app assets and a retained definition; answers are not restored between workspaces.

### Portable definition adoption

Keep the public YAML or JSON definition source with your application. Use
`definition init`, `check`, `build` and `revise` to create and maintain it, then
distribute the compiled definition with your release, through a browser host,
or on local media. GitHub and the optional Definition Store are distribution
choices, not requirements for local configuration.

One definition can serve the CLI, TUI, Desktop and Browser. It describes
configuration requirements without running scripts or plugins. Definition
revisions identify changes, and each open workspace retains the exact revision
it loaded. Public definitions must never contain your operators' private answers.

Start with the [adoption tutorial](docs/public/integrating.md) and
[cookbook](docs/public/definition-cookbook.md). The
[definition reference](docs/public/definition-reference.md) explains formats,
defaults, presence and versioning. Product and public contract versions remain
`0.0.1` during active development; breaking changes are expected.

---

## 🎨 Branding & Customization

Shape the configuration experience through your application's definition: its name, sections, entry points, field explanations, choices and translations. This lets maintainers present application-specific guidance without writing a separate form for every client.

- **Application Identity:** Give operators a clear application name and explicit version selection.
- **Guidance & Localization:** Explain settings and choices in the languages declared by your definition.
- **Launch Experience:** Link to a selected configuration entry and locale using the [launch integration](docs/public/launch-integration.md).

Available presentation features follow the supported definition format. A publisher label or application name is descriptive metadata, not proof of publisher identity.

---

## 🛡️ Enterprise Security & Compliance

Configuration can include passwords, private keys and operational details. Configurator's security model focuses on where that data is processed, what a definition may do, and how files leave the workspace:

- **Data-only definitions:** Definitions describe supported fields and output mappings. They cannot execute plugins, scripts or arbitrary network calls.
- **Stable sessions:** An open workspace keeps its admitted definition revision and content identity, so a later definition update cannot silently change its requirements.
- **Local browser processing:** Answers and generated files stay out of application network requests and persistent caches. Secret entry requires consent for each workspace; revoking it clears secret fields and prepared output.
- **Protected native saving:** On verified Linux hosts, native saves create restricted files and refuse to overwrite existing ones. Unsupported hosts refuse protected saving instead of silently using an ordinary file write.
- **Explicit application control:** Preparing configuration does not log into your application, change a running service or grant administrative access.

These are specific safeguards, not a security certification or a guarantee against a compromised device. See the [security policy](SECURITY.md), [browser trust guidance](docs/public/browser.md#data-custody--security-architecture) and [native output guide](docs/public/native-output.md).

---

## FAQ

### What does Configurator actually do?

It helps you turn application requirements into usable configuration files. Open a definition, work through its settings, review validation and prepare its declared outputs, such as `.env`, JSON or YAML. A definition can describe nested structures and multiple files, so the workspace can cover much more than a short installation wizard. See the [worked quickstart](docs/public/getting-started.md).

### Does it work with any application or any JSON Schema?

An application needs a definition describing the settings and file mappings Configurator should use. Maintainers can author one with the [integration tools](docs/public/integrating.md); operators can use an existing definition. Configurator supports a bounded definition format, rather than every JSON Schema feature or automatic understanding of arbitrary applications. Schema import can scaffold a starting point that still needs maintainer review. The optional [Definition Store](docs/public/definition-store.md) helps distribute definitions, but is not required.

### Which interface should I choose?

Choose the **Browser** for a guided workspace without native installation, **Desktop** for native forms and local file workflows, **Terminal** for keyboard-driven work in a local or remote shell, or **CLI** for scripts and agents. They share configuration semantics, but feature coverage and platform qualification differ. Browser-first development is the current adoption focus; a richer TUI workbench is part of the [documented direction](docs/internal/adr/ADR-terminal-framework-and-workbench.md).

### Where are my answers and secrets processed?

Locally. The static Blazor WebAssembly app runs forms, validation and file preparation in your browser, rather than sending answers to a Configurator server. It downloads public app assets and may retrieve a public definition before collecting answers. Answers and generated files are excluded from application requests, persistent storage and public caches. This local-processing choice costs a larger initial download; see [why WebAssembly](docs/public/browser.md#why-webassembly-interactivity).

You still need to trust the delivered code, browser, extensions and device. For high-value secrets, prefer a reviewed local native build on a trusted machine. Native software also requires trust in its build and operating system. Browser secret entry is disabled initially and requires consent for each workspace. Enabling it keeps processing local; revoking consent clears secret fields and prepared output. Masking protects against casual viewing, not compromised code. See [browser trust guidance](docs/public/browser.md#data-custody--security-architecture).

### Can I work offline or leave secrets out?

Yes. Native clients can work with local definitions. For offline browser use, first load the complete app assets and explicitly retain the definition; installing a PWA shortcut alone is not enough. Browser answers are transient and are not restored in a new workspace. Offline use still requires trust in the downloaded code and device. See the [offline guide](docs/public/browser.md#offline-work--pwa-support).

You can also leave browser secret entry disabled and prepare an incomplete **secret-deferred draft**, then complete the indicated secret locations locally. Ordinary required settings must still be valid, and unresolved secret-dependent conditions can block preparation. Ordinary settings may themselves be confidential; a draft is not ready for deployment until completed and checked.

### Can I reuse existing files or connect a secret manager?

Supported local `.json`, `.yaml` and `.env` files can be assessed against a selected definition. Comparison explains recognized values, missing requirements and incompatible content; filling eligible fields is a separate, deliberate action. Import reads the file locally, without uploading it. This is configuration assessment, not arbitrary schema comparison or an automatic migration tool. See [import and comparison](docs/public/import-and-compare.md).

Direct Infisical and Azure Key Vault workflows are longer-term native plans, not available integrations. The intended direction includes explicit secret inspection, comparison, updates and verification, with separately designed credentials, permissions and conflict handling. The current product prepares configuration locally.

### Does successful preparation mean my application will accept the files?

No. Configurator checks the requirements represented by the definition; it cannot prove every application-specific or deployment constraint. Review the files and use the target application's own validation and deployment process. Configurator does not apply configuration or restart services for you.

Saving and applying are also different steps. Native protected saving is verified on Linux and refuses existing-file conflicts. Browser downloads cannot enforce those protections or control clipboard/download history. Protect exported files yourself; clearing a workspace cannot retract them. See [native output](docs/public/native-output.md).

---

## Roadmap

We are prioritizing the browser so application maintainers can offer an easy Configure link, with guidance, complex configuration editing and comparison in one workspace. The TUI is intended to grow into a full operator/developer workbench, including document editing. Schema-to-schema comparison and direct native secret-manager workflows remain longer-term plans.

Read the [vision and approach](docs/internal/VISION_AND_APPROACH.md), [Terminal decision](docs/internal/adr/ADR-terminal-framework-and-workbench.md) and [current status](docs/internal/STATUS.md) for the distinctions. The wider ISLAMU roadmap is tracked in the [Roadmap Kanban View][roadmap-link]; discuss Configurator proposals in [GitHub Discussions][github-discussions-link].

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
- 🧪 Test configuration, privacy and file-saving behavior
- 📊 Measure and improve responsiveness with large definitions
- 🎨 Improve forms, keyboard navigation, translations and accessibility across clients

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

The CLI supports definition authoring, configuration validation and comparison,
local secret operations, and file preparation. Use `--help` for commands and
options, or `--machine` for structured JSON results. See the
[CLI guide](docs/public/cli.md) for workflows and the
[command reference](docs/public/cli-command-reference.md) for the full vocabulary.
Choose a definition explicitly; no application is selected for you.

| Client | Start from the checkout | Packaged entry point |
| --- | --- | --- |
| Machine CLI | `dotnet run --project src/Configurator.Cli --configuration Release -- doctor --machine` | `islamu-configurator` |
| Terminal | `dotnet run --project src/Configurator.Terminal --configuration Release -- --definition examples/minimal-self-hosted/setup-definition.json` | `islamu-configurator-terminal` |
| Desktop | `dotnet run --project src/Configurator.Desktop --configuration Release -- /absolute/path/setup.json /absolute/path/output-directory` | `islamu-configurator-desktop` |
| Browser | Publish `Configurator.Browser`, then serve its complete `wwwroot/` | `/` catalogue, `/launch/<familyId>` and pinned `/open#src=…` |

The Desktop row is an invocation template: substitute an admitted local
definition and an existing trusted output directory. Terminal/Desktop launch
opens an interactive workspace; saving remains a deliberate later action.
The `doctor` command reports writer availability; it does not test whether a
particular destination can save files.

The maintained neutral family can be opened on your own Browser host at
`/launch/4d8950ad-972c-4357-8605-7a7c89c05a17?appVersion=1.0&entry=setup&locale=en`.
This is a relative route for your own host, not a link to an official deployment.
Configure a SPA navigation fallback for deep links, not missing framework assets.
Native preparation requires no Configurator backend, database or Event server.

### Contributor Onboarding Path
1. Review our [Contribution Guidelines](CONTRIBUTING.md) and [Local Getting Started](#quick-start-for-developers--contributors).
2. Read the [Project Guide](PROJECTS.md) for project responsibilities and targeted build/test commands.
3. Check the authoritative [Quick Reference (Invariants)](docs/internal/QUICK_REFERENCE.md), [Core Contract](docs/internal/CORE_DEFINITION_CONTRACT.md), and [Governance Standards](docs/internal/GOVERNANCE.md).
4. AI-assisted coding agents must strictly follow [`AGENTS.md`](AGENTS.md) and [`.agents/contract/intents.yaml`](.agents/contract/intents.yaml).

### ✍️ Contributor Licensing & Community Protection

Contributors retain ownership of their contributions. Configurator's ISLAMU-owned source uses the [MIT license][license-link]; third-party material keeps its own terms. The Steward authorized the first-party Event transfer without changing Event's separate AGPL license or service covenant.

Keep source provenance, attribution and dependency notices intact. Do not assume Event's CLA automation or alternative-license terms govern this independent utility. See [CONTRIBUTING.md](CONTRIBUTING.md) and [IP Governance](docs/internal/legal/IP_GOVERNANCE.md).

---

## 📚 Documentation: Public Guides vs. Internal Specifications

Start with **Public Guides** to use Configurator or add support to your application. Use **Internal Specifications** when contributing to the engine, clients or security boundaries. Both live in this repository:

### 🌐 1. Public Operator & Adopter Documentation

> 📖 **Documentation Hub:** [docs/README.md](docs/README.md) routes you to the guide for your task.

* **[Overview & Mental Model](docs/public/overview.md)** — What Configurator is, how the 3-step model works, and interface comparisons.
* **[Quickstart](docs/public/getting-started.md)** — Step-by-step walkthrough preparing configuration offline.
* **[Glossary](docs/public/glossary.md)** — Plain-language definitions for all domain concepts.
* **[CLI Reference](docs/public/cli.md)** — Automate validation, comparison, secret generation and file preparation.
* **[Terminal](docs/public/terminal.md)** — Configure with the keyboard and save locally.
* **[Desktop](docs/public/desktop.md)** — Edit through native forms and use protected saving.
* **[Browser Configuration](docs/public/browser.md)** — Configure in your browser, choose how to handle secrets, export files or host the app yourself.
* **[Launch Integration](docs/public/launch-integration.md)** — Add a Configure link that selects a definition, version and starting task.
* **[Definitions](docs/public/definitions.md)** — Describe your application's fields, rules and output files.
* **[Native Output](docs/public/native-output.md)** — Save private files, understand conflicts and check platform limits.
* **[Packaging](docs/public/packaging.md)** — Build native executables or publish the static browser app.
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
| **Vision & Core Goals** | [VISION_AND_APPROACH.md](docs/internal/VISION_AND_APPROACH.md) | Browser-first adoption, the TUI workbench direction and the product's current boundaries. |
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
| Public browser | Static Blazor WebAssembly / MudBlazor |
| Definition data | Bounded JSON/YAML, canonical SHA-256 identity |
| Protected output | Linux create-only native writes; other hosts fail closed |
| Dependencies | Central package versions, generated locks and retained notices |
| Tests | TUnit, bUnit, headless native controls and architecture/package tests |

## Community

Join the ISLAMU community on [Discord][discord-link] and our [GitHub Discussions][github-discussions-link]. We follow a [Code of Conduct][code-of-conduct] in all our community channels.

Ask questions, share definitions, describe configuration problems, or show how you use Configurator. Reports from real application setups help us decide what to improve next.

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
- [ISLAMU Configurator][github-repo-link]: Guided application configuration and local file preparation.
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
- [ASP.NET Core][aspnet-core-link]: Blazor web application tooling and the optional static browser host.
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

The configuration engine is independent of Event and any application it configures. The four clients share its definition and validation rules. Interactive clients share workspace behavior; native clients also use the protected file writer. The Browser uses only browser-safe components and keeps file export within browser capabilities.

Terminal.Gui uses the official pinned package, with dependency locks and retained notices. The [framework decision](docs/internal/adr/ADR-terminal-framework-and-workbench.md) explains its selection for the TUI, and the [dependency history](docs/internal/TERMINAL_GUI_DEPENDENCY.md) records the earlier fork's retirement. Local validation packages are not official releases; publication and signing follow the separate [release process](docs/internal/RELEASE_RUNBOOK.md).

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
