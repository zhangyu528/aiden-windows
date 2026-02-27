# Code Signing Policy

This project signs Windows release artifacts via SignPath to provide publisher identity and tamper evidence.

## Scope

The following files are signed for release builds:

- `Aiden.TrayMonitor.exe`
- `Aiden.RuntimeAgent.exe`
- `Aiden-Setup-<version>-win-x64.exe`

## Signing Trigger

Signing runs only in the GitHub Actions release workflow:

- Workflow: `.github/workflows/release-installer.yml`
- Trigger: `release` with type `published`
- Provider: SignPath signing request pipeline

No signing is performed for pull requests or arbitrary branch pushes.

## SignPath Onboarding Requirements

Set the following GitHub repository variables before enabling release signing (`SIGNPATH_READY=true`):

- `SIGNPATH_ORGANIZATION_ID`
- `SIGNPATH_PROJECT_SLUG`
- `SIGNPATH_SIGNING_POLICY_SLUG`
- `SIGNPATH_UNSIGNED_ARTIFACT_CFG`
- `SIGNPATH_INSTALLER_ARTIFACT_CFG`

The release workflow enforces these values in the readiness gate and fails fast when any required variable is missing.

## Verification Requirements

Release workflow must fail if any signature is not valid.

Local verification example:

```powershell
Get-AuthenticodeSignature .\Aiden-Setup-0.2.0-win-x64.exe | Format-List Status,SignerCertificate,TimeStamperCertificate
```

Expected status: `Valid`.

## Release Integrity Metadata

Each release publishes `SHA256SUMS.txt` that includes the installer SHA256 digest.

Consumers should verify both:

- Authenticode signature
- SHA256 digest from `SHA256SUMS.txt`

## Runtime Dependency Integrity

Installer runtime dependency downloads (VictoriaMetrics and OpenTelemetry Collector) require SHA256 verification.

If checksum resolution or verification fails, installation fails by default.
