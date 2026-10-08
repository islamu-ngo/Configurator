# Security Policy

This document outlines the security protocols and vulnerability reporting guidelines for the ISLAMU Configurator project. Ensuring the security of our systems is a top priority, and while we work diligently to maintain robust protection, vulnerabilities may still occur. We highly value the community’s role in identifying and reporting security concerns to uphold the integrity of our systems and safeguard our users.

## Reporting a Vulnerability

If you have identified a security vulnerability, submit your findings to [contact@openislamu.org](mailto:contact@openislamu.org).
Ensure your report includes all relevant information needed for us to reproduce and assess the issue. Include the affected client, package version, operating system and reproduction command; include the URL when the affected browser client is hosted. Do not include private answers, restricted artifacts, credentials or personal data.

To ensure a responsible and effective disclosure process, please adhere to the following:

- Maintain confidentiality and refrain from publicly disclosing the vulnerability until we have had the opportunity to investigate and address the issue.
- Refrain from running automated vulnerability scans on our infrastructure or dashboard without prior consent. Contact us to set up a sandbox environment if necessary.
- Do not exploit any discovered vulnerabilities for malicious purposes, such as accessing or altering user data.
- Do not engage in physical security attacks, social engineering, distributed denial of service (DDoS) attacks, spam campaigns, or attacks on third-party applications as part of your vulnerability testing.

## Out of Scope

While we appreciate all efforts to assist in improving our security, please note that the following types of vulnerabilities are considered out of scope:

- Vulnerabilities requiring man-in-the-middle (MITM) attacks or physical access to a user’s device.
- Content spoofing or text injection issues without a clear attack vector or the ability to modify HTML/CSS.
- Issues related to email spoofing.
- Missing DNSSEC, CAA, or CSP headers.
- Absence of secure or HTTP-only flags on non-sensitive cookies.

## Definition Admission Invariants

- Definitions are bounded, declarative data, not executable plugins. Core validates the closed field, validation and output vocabulary before a client uses it.
- Family GUIDs and publisher labels are identity claims, not authentication or host authorization. Exact revision and canonical SHA-256 digest bind admitted content.
- A previously admitted family/revision cannot be replaced with different content, including after removal from the current registry. Active sessions retain their admitted definition rather than silently upgrading.

## Public Browser Privacy Invariants

- Browser answer controls accept active public enum and boolean choices only. Private answers, secrets, arbitrary free text, tokens and target administration do not belong in browser input or launch URLs.
- Public templates are partial choices, not full native configuration or a claim of readiness. Import validates the exact family, revision and digest and rejects unknown, duplicate, inactive or private members.
- Navigation, edits, rejected imports, replacement and disposal invalidate old output. Uploaded byte buffers are cleared, but managed strings and user-controlled downloads do not provide a forensic-erasure guarantee.

## Protected Native Artifact Invariants

- Answer-derived native artifacts remain restricted. Supported Linux writes use create-only protected output; concurrent or existing-file conflicts do not overwrite earlier output.
- Unsupported protected-save hosts fail closed. Ordinary file output is not an acceptable fallback.
- Shared operation generations fence stale preparation and save results. Public diagnostics must not reveal raw answers, credentials, restricted content or private paths.

## Target Authority And Publication Invariants

- Local preparation grants no authentication, administration or apply authority. The target application owns semantic validation, authorization and acceptance of the artifact.
- Event's live adapter and backend authority remain Event-owned. Configurator has no Event runtime, domain, wire or database dependency.
- Signed publication remains unavailable until Configurator establishes its own trust authority. Transferred generic validation tooling is not Event trust activation.

## Our Commitment

At ISLAMU Configurator, we are committed to maintaining transparent and collaborative communication throughout the vulnerability resolution process. Here's what you can expect from us:

- **Response Time** <br/>
We will acknowledge receipt of your vulnerability report within an undefined amount of business days as we are Still Students! and provide an estimated timeline for resolution.
- **Legal Protection** <br/>
We will not initiate legal action against you for reporting vulnerabilities, provided you adhere to the reporting guidelines.
- **Confidentiality** <br/>
Your report will be treated with confidentiality. We will not disclose your personal information to third parties without your consent.
- **Recognition** <br/>
With your permission, we are happy to publicly acknowledge your contribution to improving our security once the issue is resolved.
- **Timely Resolution** <br/>
We are committed to working closely with you throughout the resolution process, providing timely updates as necessary. Our goal is to address all reported vulnerabilities swiftly, and we will actively engage with you to coordinate a responsible disclosure once the issue is fully resolved.

We appreciate your help in ensuring the security of our platform. Your contributions are crucial to protecting our users and maintaining a secure environment. Thank you for working with us to keep ISLAMU Configurator safe.
