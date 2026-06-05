# Security Policy

## Supported Versions

WopiHost ships from a single `master` line, and only the **latest published release** receives
security fixes. There are no maintained back-port branches — older majors are superseded as soon as
a new one ships on NuGet.

| Version | Supported |
| ------- | --------- |
| Latest release (currently `9.x`) | :white_check_mark: |
| Any earlier version | :x: |

If you're on an older version, the fix is to upgrade to the latest release. Because the project is
in its API-stabilization phase, a security fix may land in a new major — see the release notes and
[migration guide](https://github.com/petrsvihlik/WopiHost/releases) for upgrade steps.

## Reporting a Vulnerability

Please **do not** post exploit details, proof-of-concept code, or anything that could be weaponized
in a public channel before a fix is available.

There are two ways to report, depending on sensitivity:

1. **Sensitive / exploitable issues — report privately (recommended).**
   Use GitHub's private vulnerability reporting:
   **[Report a vulnerability](https://github.com/petrsvihlik/WopiHost/security/advisories/new)**
   (Security → Advisories → *Report a vulnerability*). This keeps the details between you and the
   maintainers until a fix and advisory are published, and lets us credit you in the advisory.

2. **Low-sensitivity issues — open a security issue.**
   Use the **🔒 Security Vulnerability** template at
   [New issue](https://github.com/petrsvihlik/WopiHost/issues/new/choose). Keep the public
   description high-level; if a detailed proof-of-concept is needed, prefer the private channel above.

When in doubt, choose the private channel.

### What to expect

WopiHost is a volunteer-maintained open-source project, so timelines are best-effort rather than a
contractual SLA:

- **Acknowledgement** within ~7 days that the report was received.
- **Initial assessment** (accepted / needs-more-info / declined, with reasoning) once the report
  has been triaged.
- **If accepted** — the maintainers work on a fix, cut a new release, and publish a
  [GitHub Security Advisory](https://github.com/petrsvihlik/WopiHost/security/advisories) crediting
  the reporter (unless you ask to stay anonymous). Coordinated disclosure is preferred: please hold
  public details until the fixed release is out.
- **If declined** — you'll get an explanation (e.g. out of scope, by-design, not reproducible, or
  belongs to an upstream dependency).

There is no bug-bounty program. Reports about third-party dependencies are forwarded upstream where
possible, but the upstream project owns the fix.
