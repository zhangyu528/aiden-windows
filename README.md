# aiden-windows

![Platform](https://img.shields.io/badge/platform-Windows_10%20%7C%2011-blue)
![.NET Version](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
[![PR Tests](https://github.com/zhangyu528/aiden-windows/actions/workflows/tests-pr.yml/badge.svg?branch=main)](https://github.com/zhangyu528/aiden-windows/actions/workflows/tests-pr.yml)
[![Create Pre-release](https://github.com/zhangyu528/aiden-windows/actions/workflows/prerelease.yml/badge.svg)](https://github.com/zhangyu528/aiden-windows/actions/workflows/prerelease.yml)
[![GitHub Release](https://img.shields.io/github/v/release/zhangyu528/aiden-windows)](https://github.com/zhangyu528/aiden-windows/releases/latest)
![Code Signed](https://img.shields.io/badge/Code_Signed-SignPath-success?logo=checkmarx)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Windows desktop monitoring project for Gemini CLI telemetry.

## Project Structure

- `Aiden.TrayMonitor/`: WPF tray app MVP (Gemini -> Collector -> VM)
- `Aiden.RuntimeAgent/`: user-level background daemon that supervises VM + Collector
- `docs/`: FRD and system design documents

## Prerequisites

- Windows 10/11
- .NET 8 SDK

## Run

```pwsh
cd Aiden.TrayMonitor
dotnet restore
dotnet run
```

Run runtime agent (optional manual run, normally auto-started by tray):

```pwsh
cd Aiden.RuntimeAgent
dotnet restore
dotnet run
```

Configuration layout:
- Shared runtime settings: `runtime.shared.json` (each project output)
- Tray UI settings: `Aiden.TrayMonitor/appsettings.json`
- Agent-only settings: `Aiden.RuntimeAgent/agentsettings.json`

## Current MVP Features

- Tray resident app
- Left click tray icon to toggle panel
- Right click menu: refresh / show-hide / runtime status / restart runtime / exit
- User-level background agent auto-start (HKCU Run) to keep VM + Collector running after tray exit
- Poll VictoriaMetrics periodically
- Manual refresh from panel

## Default Telemetry Target

- `telemetry.enabled = true`
- `telemetry.useCollector = true`
- `telemetry.otlpProtocol = "grpc"`
- `telemetry.otlpEndpoint = "http://127.0.0.1:4317"`

## VM Binary Management (Not in Git)

Runtime binaries are intentionally excluded from version control.

- Local install path: `Aiden.RuntimeAgent/runtime/vm/<version>/victoria-metrics.exe`
- Collector install path: `Aiden.RuntimeAgent/runtime/collector/<version>/otelcol-contrib.exe`
- Ignore rules are defined in `.gitignore` (`runtime/`, archives, etc.)

Download and install `victoria-metrics.exe` with hash verification:

```pwsh
pwsh -ExecutionPolicy Bypass -File .\Aiden.RuntimeAgent\scripts\download-vm.ps1 `
  -Version "v1.113.0" `
  -DownloadUrl "https://github.com/VictoriaMetrics/VictoriaMetrics/releases/download/v1.113.0/victoria-metrics-windows-amd64-v1.113.0.zip" `
  -Sha256 "ed8f660442a45b260a2c0a0976440ecec863bb75ccb7cec6aad9580364a92de6"
```

`-DownloadUrl` and `-Sha256` are optional in the script. If omitted, provide your own defaults or update script behavior as needed.

Download OTel Collector binary to project runtime path:

```pwsh
pwsh -ExecutionPolicy Bypass -File .\Aiden.RuntimeAgent\scripts\download-collector.ps1 `
  -Version "v0.146.1" `
  -DownloadUrl "https://github.com/open-telemetry/opentelemetry-collector-releases/releases/download/v0.146.1/otelcol-contrib_0.146.1_windows_amd64.tar.gz" `
  -Sha256 "0eaa1ff9d0f5d8009921667368981617641cebb1766fc7b38be95d5dc21a126a"
```

Notes:
- Default artifact is contrib collector (`otelcol-contrib_...`).
- `-DownloadUrl` and `-Sha256` are optional; script can auto-resolve SHA256 from release `checksums.txt`.

## Codex Conversion Notes

- Codex telemetry is converted from logs (`response.completed`) to
  `gen_ai.client.token.usage_sum` in collector.
- Converted Codex metrics are exported as cumulative series using
  `deltatocumulative` + `metricstarttime` processors.
- Query visibility in VictoriaMetrics may lag by about 20-30 seconds after ingest.

## Automated Tests

Test suites:
- `tests/Aiden.RuntimeAgent.UnitTests`
- `tests/Aiden.TrayMonitor.UnitTests`
- `tests/Aiden.IntegrationTests`
- `tests/Aiden.UI.Tests`
- `tests/Aiden.Scripts.Tests` (Pester)

Run locally:

```pwsh
dotnet test tests/Aiden.RuntimeAgent.UnitTests/Aiden.RuntimeAgent.UnitTests.csproj
dotnet test tests/Aiden.TrayMonitor.UnitTests/Aiden.TrayMonitor.UnitTests.csproj
dotnet test tests/Aiden.IntegrationTests/Aiden.IntegrationTests.csproj
dotnet test tests/Aiden.UI.Tests/Aiden.UI.Tests.csproj
pwsh -ExecutionPolicy Bypass -File .\scripts\run-script-tests.ps1
```

CI:
- PR quick gate: `.github/workflows/tests-pr.yml`
- Nightly full suite: `.github/workflows/tests-nightly.yml`

Pre-commit gate:
- Enable repo-managed hooks:

```pwsh
pwsh -ExecutionPolicy Bypass -File .\scripts\setup-githooks.ps1
```

- Hook entrypoint: `.githooks/pre-commit`
- Gate script: `scripts/precommit-gate.ps1`
- Fast checks run on staged changes only:
  - impacted project build + unit tests
  - script tests via Pester
- Pre-commit script tests require Pester v5.
  Install/update locally:

```pwsh
pwsh -ExecutionPolicy Bypass -File .\scripts\ensure-pester-v5.ps1 -Install
```

## Release Signature and Integrity Verification

Release artifacts are signed by the project release workflow via SignPath.

- `Aiden.TrayMonitor.exe`
- `Aiden.RuntimeAgent.exe`
- `Aiden-Setup-<version>-win-x64.exe`

Verify installer signature:

```pwsh
Get-AuthenticodeSignature .\Aiden-Setup-<version>-win-x64.exe | Format-List Status,SignerCertificate,TimeStamperCertificate
```

Expected `Status`: `Valid`.

Each release also includes `SHA256SUMS.txt`.
Verify file hash against published digest:

```pwsh
Get-FileHash -Algorithm SHA256 .\Aiden-Setup-<version>-win-x64.exe
```

Compare the output hash with the corresponding line in `SHA256SUMS.txt`.

## Pre-release Publishing

Use GitHub Actions workflow `.github/workflows/prerelease.yml` to create pre-releases.

Inputs:
- `base_version`: must be `vMAJOR.MINOR.PATCH` (example: `v0.1.0`)
- `channel`: choose `alpha`, `beta`, or `rc`

Behavior:
- Tag source is fixed to `main`.
- Workflow auto-increments channel sequence per base version:
  - `v0.1.0 + alpha` => `v0.1.0-alpha.1`, then `v0.1.0-alpha.2`, etc.
  - `v0.1.0 + beta` => `v0.1.0-beta.1`, then `v0.1.0-beta.2`, etc.
  - `v0.1.0 + rc` => `v0.1.0-rc.1`, then `v0.1.0-rc.2`, etc.
- After pre-release creation, installer build + SignPath signing + signature verification run automatically.
- Signed installer and `SHA256SUMS.txt` are uploaded to the created pre-release assets.
- Final release is produced by promoting the validated pre-release in GitHub UI (mark prerelease off), without rebuilding artifacts.

Do not include prerelease suffixes in `base_version` input.

For full policy details, see `CODE_SIGNING_POLICY.md`.

