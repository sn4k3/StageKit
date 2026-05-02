# Security Policy

The StageKit maintainers take security seriously. This document describes how to report vulnerabilities and what you can expect in response.

## Supported Versions

Only the latest released version on [NuGet](https://www.nuget.org/packages/StageKit) receives security fixes. Older versions will not be patched — upgrade to the latest release to receive fixes.

| Version | Supported          |
| ------- | ------------------ |
| Latest  | :white_check_mark: |
| Older   | :x:                |

While StageKit is pre-1.0 (0.x), any release may contain breaking changes. Security patches will ship as new minor or patch versions of the current 0.x line.

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues, discussions, or pull requests.**

Instead, report them privately through one of the following channels:

1. **GitHub Private Vulnerability Reporting** (preferred):
   Use the [Report a vulnerability](https://github.com/sn4k3/StageKit/security/advisories/new) button on the repository's Security tab. This creates a private advisory visible only to maintainers.

2. **Email**:
   Send a report to the maintainer at <tiago_caza@hotmail.com>. Use a clear subject like `[StageKit security] <short description>`.

### What to include

To help us triage quickly, please include as much of the following as you can:

- A description of the vulnerability and its impact
- The affected version(s) of StageKit
- Steps to reproduce (minimal code sample, input data, or proof-of-concept)
- Any relevant stack traces, logs, or crash dumps
- Your assessment of severity (e.g. remote code execution, denial of service, information disclosure)
- Whether the issue has been disclosed elsewhere

If the issue relates to a dependency, please say so — we may need to forward the report to the upstream maintainer.

## Response Process

You can expect the following timeline for a reported vulnerability:

| Stage                          | Target                         |
| ------------------------------ | ------------------------------ |
| Initial acknowledgement        | Within 7 days                  |
| Triage and severity assessment | Within 14 days                 |
| Fix or mitigation plan         | Within 30 days where feasible  |
| Coordinated disclosure         | Negotiated with the reporter   |

This is a volunteer-maintained open-source project, so timelines are best-effort. Complex issues, upstream dependencies, or reports received during periods of reduced availability may take longer.

We will:

- Acknowledge your report and work with you to confirm the issue.
- Keep you informed of progress toward a fix.
- Credit you in the release notes and security advisory if you wish (anonymous reports are also welcome).
- Coordinate public disclosure with you once a fix is available.

## Scope

In scope:

- Vulnerabilities in the `StageKit` library source code (this repository), including settings persistence, crash report handling, exception logging, file path handling, and process relaunch behavior.
- Security issues in the NuGet package metadata, signing, or build pipeline (GitHub Actions workflows in this repository).

Out of scope:

- Vulnerabilities in .NET runtime packages or third-party dependencies — please report those to their respective maintainers.
- Issues that require an attacker to already have arbitrary code execution on the host.
- Build-time or development-time issues that do not affect consumers of the published package.

If you are unsure whether something is in scope, report it anyway and we will route it appropriately.

## Security Considerations for Consumers

A few notes for applications using StageKit:

- **Settings files.** `RootSettingsFile<T>` writes JSON files under `ApplicationKit.ConfigPath`. Applications should set this path to a trusted per-user location and avoid sharing it with untrusted users.
- **Crash reports.** Crash reports can include exception messages, stack traces, process information, and custom text appended by `CrashReport.FormatMessageFunc`. Treat generated crash report files as potentially sensitive.
- **Process relaunch.** `UnhandledExceptions` can launch a new process instance for crash report handling. Applications should validate custom `ApplicationKit.CrashReportArg` values and avoid passing untrusted command-line content.
- **Thread-safety guarantees.** StageKit settings and crash report types follow standard .NET instance-member-not-thread-safe conventions unless explicitly documented otherwise.
- **NativeAOT / trimming.** This library is not currently validated for NativeAOT or aggressive trimming. Do not assume trim safety.

## Acknowledgements

We are grateful to the security community for responsible disclosure. Reporters who follow this policy will be credited in the affected release's notes unless they request otherwise.
