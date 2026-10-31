# Contributing to ISLAMU Configurator

We’re glad you are interested in contributing to ISLAMU Configurator!

There are many ways to help:
- Answer questions and share ideas in [GitHub Discussions](https://github.com/islamu-ngo/Configurator/discussions) or [Discord](https://discord.gg/wrkY824Yv5)
- Report reproducible bugs with clear diagnostic context
- Submit pull requests to fix issues, add features, or improve modules
- Help improve and translate documentation, or contribute translations in the repository resource files
- Refine UI accessibility (WCAG 2.2 AA) and localization
- Advocate for ethical, community-owned configuration tooling

> [!NOTE]
> **Not a coder or short on time? You can still support the project:**
> - ⭐ **Star the project on GitHub** to help increase its reach.
> - 📢 **Spread the word**: Recommend ISLAMU Configurator to non-profits, student unions, NGOs, and community organizers looking for an ethical, offline configuration preparation utility.
> - 🌍 **Contribute translations**: Help translate UI strings into your language via Weblate.
> - 💬 **Share feedback**: Join our [Discord](https://discord.gg/wrkY824Yv5) and tell us about your configuration preparation experience or feature needs.

ISLAMU Configurator is a purpose-agnostic, offline configuration preparation utility. It is currently built and maintained by **Amir Akrari** on personal free time and funds, with **ISLAMU (ASBL en formation)** (a Belgian non-profit association in formation) being established as the operational and legal steward. Contributions are welcome — but **alignment matters more than quantity**.

This guide explains **what kind of contributions are likely to be accepted** and how to submit them properly. Following it saves time for both you and the maintainers.

For the practical development path, start with
[your first contribution](docs/internal/FIRST_CONTRIBUTION.md), then choose a
[worked contributor recipe](docs/internal/CONTRIBUTOR_RECIPES.md). The
[developer guide](docs/internal/DEVELOPER_GUIDE.md) explains ownership and control
flow; the [documentation style guide](docs/internal/DOCUMENTATION_STYLE_GUIDE.md)
helps you write and verify source-grounded guidance. Contribution policy below
still governs scope and submission.

> [!IMPORTANT]
> These guidelines may feel stricter than in many open-source projects. That is intentional.
> Clear structure, architectural boundaries, and legal hygiene prevent maintainer burnout and keep the project sustainable long-term.

---

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Security Vulnerabilities & Disclosure](#security-vulnerabilities--disclosure)
- [High-Level Expectations](#high-level-expectations)
- [State of the Project](#state-of-the-project)
- [What Makes a Strong Contribution](#what-makes-a-strong-contribution)
  - [Code Quality and Architecture Compliance](#code-quality-and-architecture-compliance)
  - [Atomic Changes](#atomic-changes)
- [Discussion Is Required for Larger Changes](#discussion-is-required-for-larger-changes)
  - [How to Propose an Enhancement](#how-to-propose-an-enhancement)
- [Maintainer Bandwidth & Expectations](#maintainer-bandwidth--expectations)
- [AI Contribution Policy](#ai-contribution-policy)
- [Contributor Legal Status & Licensing](#contributor-legal-status--licensing)
  - [Clean-Room & IP Protection](#clean-room--ip-protection)
- [Ways to Contribute](#ways-to-contribute)
  - [1. Support Contributions](#1-support-contributions)
  - [2. Bug Report Contributions](#2-bug-report-contributions)
  - [3. Documentation & Translation Contributions](#3-documentation--translation-contributions)
  - [4. Code Contributions](#4-code-contributions)
- [Required Testing Before Submitting](#required-testing-before-submitting)
- [Public Definition Store Contributions](#public-definition-store-contributions)
- [Submitting a Pull Request](#submitting-a-pull-request)
- [FAQ](#faq)
- [Development Guides & Resources](#development-guides--resources)

---

## Code of Conduct

This project and everyone participating in it is governed by the [ISLAMU Configurator Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. The Event source's third-party Contributor Covenant is not MIT-transferred into this policy.

Please report unacceptable behavior to [contact@openislamu.org](mailto:contact@openislamu.org). All reports are investigated promptly, fairly, and with strict confidentiality.

---

## Security Vulnerabilities & Disclosure

> [!CAUTION]
> **Never report security vulnerabilities, authorization flaws, or sensitive data leaks in public GitHub issues, discussions, or PRs.**

If you discover a potential security vulnerability (e.g. definition-admission bypass, public/private boundary failure, unsafe artifact writes, or private-answer leaks), please follow our [Security Policy](SECURITY.md):

1. Email your findings privately to **[contact@openislamu.org](mailto:contact@openislamu.org)**.
2. Include complete steps to reproduce, affected clients and versions, and sanitized logs.
3. Allow the maintainer time to investigate and coordinate a patch before public disclosure.

---

## High-Level Expectations

- **Clear Product Direction:** ISLAMU Configurator keeps Core independent, Artifacts responsible for protected native output, Presentation responsible for immutable workspace generations, and CLI/Terminal/Desktop/Browser as adapters.
- **Centralized Ownership:** Decisions and architectural governance are centralized.
- **Limited Review Capacity:** Review bandwidth is constrained (single maintainer).
- **Quality Over Volume:** Not every contribution will be accepted — even if technically functional — if it increases maintenance burden or diverges from the project's architectural roadmap.

---

## State of the Project

ISLAMU Configurator is currently in **pre-1.0** active development. Native clients, public browser, generic definitions and local validation packages are implemented; no public release or signed publication is claimed.
- Breaking changes may occur between pre-1.0 releases (we actively avoid data-loss bugs, but configuration or schema adjustments may be necessary).
- Small bug fixes and documentation improvements are accepted directly on the active development branch (`main`).
- New features, module integrations, and larger changes require prior discussion and alignment.

---

## What Makes a Strong Contribution

The following types of contributions are most likely to be accepted:

### Code Quality and Architecture Compliance
All contributions must adhere to the project's architectural invariants:
- **Clean Architecture:** Core has no Event backend, domain, wire or database dependency. Native Artifacts and shared Presentation remain independent of target application services.
- **Definition Boundaries:** Definitions are bounded declarative data. Family/revision/digest identity pins admitted content; a GUID or publisher label does not confer trust.
- **Browser Privacy:** Browser answers remain transient and local. Secret entry
  requires explicit per-workspace consent; revocation clears secret leaves and
  retires prepared output. Public assets and caches contain no answers or
  artifacts. Deliberate exports are not protected native saves.
- **Protected Output:** Native artifacts retain sensitivity floors and create-only writes. Unsupported hosts fail closed; adapters cannot add an ordinary-file fallback.
- **Shared Generations:** Immutable workspace snapshots prevent stale preparation, import or save completion from replacing current state.
- **Test-Driven:** Logic changes require focused behavioral invariants through actual public seams, with independent expected values.

### Atomic Changes
If your change is small and obvious (typo fix, small bug, minor docs update), you may open a pull request directly.

If you are fixing a bug in an endpoint or component, do not:
- Reformat unrelated files
- Refactor unrelated code
- Fix style issues elsewhere
- Combine multiple unrelated changes

Even well-intentioned "improvements" increase review complexity exponentially.

**One pull request = one logical change.**

If you want to refactor or clean up code, discuss it first and submit it separately.

---

## Discussion Is Required for Larger Changes

For anything beyond a small fix, you must discuss it before opening a pull request.

This includes:
- New features or modules (definition vocabulary, output formats, client workflows, target integrations)
- Blazor, Avalonia or terminal UI/UX changes or shared theme updates
- Changes to default behavior or configuration cascades
- Refactors or structural decomposition
- Performance rewrites
- Definition, answer-workspace or public-template schema changes
- Changes touching multiple layers or many files

Discussion happens in:
- **GitHub Discussions:** https://github.com/islamu-ngo/Configurator/discussions
- **Discord:** https://discord.gg/wrkY824Yv5

Pull requests introducing major changes without prior discussion may be closed without review. This ensures alignment before significant work is done.

### How to Propose an Enhancement

When proposing an enhancement or new feature in [GitHub Discussions (Feature Requests)](https://github.com/islamu-ngo/Configurator/discussions/categories/feature-requests), structure your proposal using this format:

1. **Problem Statement:** What specific real-world problem or workflow limitation are you trying to solve? Who is affected (application authors, operators, self-hosters)?
2. **Current vs. Expected Behavior:** Describe the user journey today, and what you envision instead.
3. **Architectural & Invariant Alignment:**
   - Does this belong in the generic configuration engine, an adapter, or an external target definition?
   - Is it white-label and purpose-agnostic (no hardcoded organization/domain assumptions)?
   - Does it uphold bounded admission, public/private separation, protected output and target-owned authority?
4. **Mockups or User Flow (if UI-related):** Provide wireframes, screenshots, or step-by-step navigation flows.
5. **Alternatives Considered:** Why can't this be solved with existing definition data, artifact commands, or target-side integration?

---

## Maintainer Bandwidth & Expectations

To set clear, realistic expectations:
- ISLAMU Configurator is currently in **pre-1.0 greenfield development** with a single primary maintainer. Review capacity is strictly finite.
- We cannot provide beginner-level mentorship or hand-holding on foundational C# / Clean Architecture concepts.
- Unsolicited major architectural refactors, stylistic formatting rewrites, or PRs touching broad surfaces without prior discussion are unlikely to be accepted.
- We do not accept AI-generated spam PRs, unverified code dumps, or unsolicited third-party package additions.

Contributions that are focused, well-tested, architecturally compliant, and aligned with our roadmap are warmly welcomed and deeply appreciated.

AI usage is permitted as an assistant. However, contributors must fully understand what their changes do and why.

---

## AI Contribution Policy

AI-assisted contributions (using GitHub Copilot, ChatGPT, Claude, Gemini, Cursor, etc.) are **permitted and welcomed**, provided they follow strict engineering standards.

### The Rules for AI-Assisted Submissions:
1. **Mandatory Disclosure:** You must explicitly disclose if AI tools were used in the pull request template, naming the tool(s) and describing how they were used.
2. **Human Understanding & Ownership:** You are responsible for every line of code submitted. You must understand the logic, architecture compliance, edge cases, and test results.
3. **Human-Authored Summary:** The "Changes" section and PR discussion must be written by a human in your own words, not blindly copied from an LLM prompt.
4. **Automated Submissions & Labeling:** Any pull request or issue that appears entirely automated, generated without contextual understanding, or disconnected from project patterns will be labeled as automated and **closed after *x* days** unless a human genuinely responds and demonstrates comprehension.
   > [!NOTE]
   > The exact duration of *x* days has not been formalized yet. Because the project is in its early stages and until **ISLAMU (ASBL en formation)** is officially registered with legal personality, maintainer capacity is limited and review windows may take an **indefinite amount of time**.

---

## Contributor Legal Status & Licensing

ISLAMU-owned Configurator material is distributed under the **MIT license**. Contributors retain copyright in their contributions and must have the right to submit their work under the project's terms.

The Steward authorized the first-party Event transfer into this independent utility. Event's AGPL license, alternative-license service covenant and CLA automation are not automatically Configurator terms. Do not claim a signing bot or licensing workflow exists here when it does not.

Third-party libraries, code, assets and documentation retain their own terms. A contributor signature or first-party transfer instruction does not relicense third-party expression. Preserve attribution, notices and dependency approval evidence; unclear rights block the affected transfer.

### Clean-Room & IP Protection
To protect the codebase and its outbound licensing options, you must **never** copy third-party proprietary, copyleft-incompatible, or unverified source code, snippets, ASTs, SQL, migrations, tests, comments, or assets into this repository. See [`docs/internal/legal/IP_GOVERNANCE.md`](docs/internal/legal/IP_GOVERNANCE.md) for details.

---

# Ways to Contribute

## 1. Support Contributions
We use Discord for real-time discussion and GitHub Discussions for structured help.

### Requesting Support
- Before opening a thread, search existing [GitHub Discussions](https://github.com/islamu-ngo/Configurator/discussions) and [Documentation Hub](docs/README.md) to see if your question is already covered.
- Provide complete and reproducible details (operating system, client/package version, sanitized failure codes, reproduction steps, screenshots).
- Be respectful — support is provided voluntarily by community members and the maintainer.
- Avoid tagging maintainers directly unless requested.

### Providing Support
- Verify information before sharing.
- Be constructive and patient with community members.

---

## 2. Bug Report Contributions
Create a GitHub issue using the [Bug Report issue form](.github/ISSUE_TEMPLATE/01_BUG_REPORT.yaml) **only** if:
- The bug is reproducible from a clean checkout on the `main` branch.
- You have verified that no existing issue or discussion covers it.
- The bug does **not** involve security, authentication bypass, or sensitive data leaks (see [Security Vulnerabilities & Disclosure](#security-vulnerabilities--disclosure) above).

Bug reports must include:
- Clear, step-by-step reproduction instructions from a fresh state.
- Expected behavior vs. actual behavior.
- Affected client (CLI, Terminal, Desktop or Browser), target definition identity/revision and supported host.
- Relevant logs, environment details, and error output with **all secrets, tokens, and PII strictly redacted**.

### Bug Triage Lifecycle
Issues progress through defined labels:
1. `🔍 Triage` — Newly reported issue awaiting maintainer review.
2. `needs-repro` — Steps are missing or the issue cannot be reproduced on a clean checkout. Issues in this state may be closed if unconfirmed.
3. `needs-fix` — Reproducible bug confirmed; open for an aligned pull request.

Incomplete bug reports or reports generated purely with generic AI output may be closed.

---

## 3. Documentation & Translation Contributions
- **Docs:** Repository documentation lives in `docs/`, with [public operator guides](docs/README.md) alongside internal source anchors. When editing or creating repository docs, follow [`docs/internal/DOCUMENTATION_ARCHITECTURE.md`](docs/internal/DOCUMENTATION_ARCHITECTURE.md) and include source anchors.
- **Translations:** Contributions to native resource files and documentation language packs are warmly welcomed. No Configurator Weblate deployment is assumed.

Start with the [task navigation](docs/README.md), then read only the affected
contract and public counterpart. Distinguish current requirements from proposals
and historical evidence using the [knowledge map](docs/internal/KNOWLEDGE_MAP.md).
When they disagree, follow the
[reconciliation procedure](docs/internal/DOCUMENTATION_ARCHITECTURE.md#resolve-disagreements);
do not silently turn an implementation bug into the intended contract.

For prose and navigation, run the [native knowledge checks](eng/knowledge/README.md)
on the affected current documents. Validate runnable examples against maintained
synthetic fixtures and actual command owners. Keep partial snippets labeled,
preserve historical evidence and record unexecuted checks honestly. Product
builds and suites are unnecessary for pure prose.

---

## 4. Code Contributions

### Issue / Discussion Requirement
Every non-trivial pull request must reference and close an existing Issue or Discussion. If none exists, start a discussion first.

### Commit Message Format
All commits must follow the **Conventional Commits** specification:

```text
type(scope): description
```

**Types:**
- `feat` — New features or functional capabilities
- `fix` — Bug fixes and error resolutions
- `refactor` — Code changes that neither fix a bug nor add a feature
- `docs` — Documentation additions or updates
- `test` — Adding or correcting tests
- `chore` — Maintenance, tooling, and release preparation
- `ci` — CI/CD workflow and script updates

**Approved Scopes:**
- *Public product scopes:* `definitions`, `self-hosting`, `artifacts`, `launch`, `cli`, `terminal`, `desktop`, `browser`, `output`, `privacy`, `localization`, `accessibility`
- *Engineering scopes:* `ci`, `dependencies`, `architecture`, `observability`, `documentation`, `release`, `testing`, `build`

The machine authority is [scope-registry.yaml](eng/release/policy/scope-registry.yaml).

**Examples:**
- `fix(definitions): reject conflicting family revision content`
- `feat(artifacts): validate restricted output before native export`
- `fix(browser): clear stale preview after rejected public import`
- `docs(self-hosting): explain Linux protected-save limits`

Keep the commit subject concise. Do not paste walls of change logs into the commit subject.

### Pull Request Title Format
PR titles follow the identical conventional format:
- `fix(definitions): reject conflicting family revision content`
- `feat(artifacts): validate restricted output before native export`

---

## Public Definition Store Contributions

Anyone can propose an ordinary project folder in the optional
[public definition store](store/README.md) through a pull request. No account,
rating or install hook is required. Include bounded `project.json` metadata,
the applicable definition license and versioned public JSON definitions. Keep
the project slug equal to its folder name; the native builder derives family,
revision, digest and application-version metadata from Core admission.

Submit only public data you have permission to distribute. Do not include saved
answers, real credentials, private defaults or private explanatory text. Review
checks public suitability and distribution rights; store inclusion does not
authenticate a publisher, certify application compatibility or confer host
apply authority. Local files and independent public HTTPS definitions continue
to work without store membership or availability.

Before submitting, run the builder self-test, generate into `.tmp/definition-store`
and check freshness using the commands in the [store guide](docs/public/definition-store.md).
The schema is generated; never hand-edit it or generated index/snapshot output.
If you change admitted content, update its owning source and definition revision
rather than relabeling existing family/revision content. Maintainers review
contributions under the same [licensing guidance](#contributor-legal-status--licensing)
as other repository material.

## Required Testing Before Submitting

Before submitting a pull request, verify your changes locally.

> [!TIP]
> **Inner Loop Testing (The 3-Ring Model):**
> You do not need to run the entire test suite during active coding.
> - **Ring 1:** Fast in-memory Core/Presentation sliced invariants; no timing sleeps, Docker or network lag.
> - **Ring 2:** Build/test the actual affected owner at phase exit, preserving unchanged passes.
> - **Ring 3:** Verify relevant package/architecture/schema boundaries once at workstream exit. No Event database or service matrix belongs here.

### 1. Solution Build
```bash
dotnet build Configurator.slnx --configuration Release --verbosity quiet
```

### 2. Run Test Projects Individually
Do not run solution-wide `dotnet test`. Run the individual test projects relevant to your changes:

```bash
# Core invariants
dotnet test --project tests/Configurator.Core.Tests/Configurator.Core.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1

# Shared workspace generations
dotnet test --project tests/Configurator.Presentation.Tests/Configurator.Presentation.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1

# Public browser contracts
dotnet test --project tests/Configurator.Browser.Tests/Configurator.Browser.Tests.csproj --configuration Release --verbosity quiet -- --minimum-expected-tests 1
```

Other actual owners and package prerequisites are listed in [PROJECTS.md](PROJECTS.md). Pure prose needs link/anchor/whitespace verification, not product builds. Tooling-only changes use native engineering suites.

---

## Submitting a Pull Request

1. **Branching Strategy:**
   - **Target Branch:** All feature and bug fix pull requests must branch from and target **`main`**.
   - Configurator currently develops on `main`; do not invent Event’s `develop` release topology.
2. **Complete the PR Template:**
   - Fill out every required section in `.github/PULL_REQUEST_TEMPLATE.md`.
   - Provide human-written change rationale.
   - Disclose AI tool usage.
   - Record Release Impact and Documentation Impact.
   - Record MIT/third-party provenance and any release-impact evidence.

---

## FAQ

**Q: Should I ask before fixing a typo or small bug?**
A: No. Narrow, obvious fixes for typos or isolated bug fixes with clear tests can be submitted directly as PRs against `main`.

**Q: I have an idea for a new feature or integration.**
A: Start a conversation in [GitHub Discussions](https://github.com/islamu-ngo/Configurator/discussions) or Discord first. Do not invest time writing code before reaching alignment with the maintainer.

**Q: My PR was closed without extensive feedback.**
A: This usually means the PR did not align with the product roadmap, introduced unnecessary architectural complexity, lacked prior discussion for a large change, or exceeded maintainer review bandwidth.

**Q: Can I submit refactoring or cleanup PRs?**
A: Please discuss structural refactorings beforehand. Unsolicited whitespace, formatting, or stylistic PRs will be closed to protect review bandwidth.

**Q: Can I use AI to help with my PR?**
A: Yes, AI-assisted development is welcome. However, you must disclose AI usage in the PR template, understand every change thoroughly, and be able to explain and defend your implementation.

---

## Development Guides & Resources

- **Code of Conduct:** [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- **Security Policy & Vulnerability Reporting:** [SECURITY.md](SECURITY.md)
- **MIT License:** [LICENSE](LICENSE)
- **IP & Clean-Room Governance:** [IP_GOVERNANCE.md](docs/internal/legal/IP_GOVERNANCE.md)
- **Documentation Hub & Router:** [docs/README.md](docs/README.md)
- **Getting Started & Actual Project Owners:** [PROJECTS.md](PROJECTS.md)
- **Testing Guide & Invariants:** [TESTING.md](docs/internal/TESTING.md)
- **Quick Reference & Invariants:** [QUICK_REFERENCE.md](docs/internal/QUICK_REFERENCE.md)
- **Architecture Boundary:** [Runtime ADR](docs/internal/adr/ADR-definition-runtime-boundary.md)
- **Documentation Architecture & Dual-Track Parity:** [DOCUMENTATION_ARCHITECTURE.md](docs/internal/DOCUMENTATION_ARCHITECTURE.md)
- **Commit/Change Workflow:** [eng/release/README.md](eng/release/README.md)
