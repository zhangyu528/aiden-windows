# aiden-windows

![Platform](https://img.shields.io/badge/platform-Windows_10%20%7C%2011-blue)
![.NET Version](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
[![Build](https://github.com/zhangyu528/aiden-windows/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/zhangyu528/aiden-windows/actions/workflows/build.yml)
[![PR Tests](https://github.com/zhangyu528/aiden-windows/actions/workflows/tests-pr.yml/badge.svg?branch=main)](https://github.com/zhangyu528/aiden-windows/actions/workflows/tests-pr.yml)
[![Nightly Tests](https://github.com/zhangyu528/aiden-windows/actions/workflows/tests-nightly.yml/badge.svg)](https://github.com/zhangyu528/aiden-windows/actions/workflows/tests-nightly.yml)
[![GitHub Release](https://img.shields.io/github/v/release/zhangyu528/aiden-windows)](https://github.com/zhangyu528/aiden-windows/releases/latest)
![Code Signed](https://img.shields.io/badge/Code_Signed-SignPath-success?logo=checkmarx)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Windows desktop monitoring project for Gemini CLI telemetry.

## Project Structure

- `tests/`: Consolidated test directory
  - `drivers/`: .NET test runners
- `docs/`: product/spec/design/test docs

## Quick Start

Prerequisites:
- Windows 10/11
- .NET 8 SDK
- PowerShell 7 (`pwsh`)

Run Tray app:

```pwsh
cd Aiden.TrayMonitor
dotnet restore
dotnet run
```

Run RuntimeAgent:

```pwsh
cd Aiden.RuntimeAgent
dotnet restore
dotnet run
```

## Test & CI Quick Links

Local quick commands:

```pwsh
dotnet test tests/Aiden.RuntimeAgent.UnitTests/Aiden.RuntimeAgent.UnitTests.csproj
dotnet test tests/Aiden.TrayMonitor.UnitTests/Aiden.TrayMonitor.UnitTests.csproj
dotnet test tests/Aiden.IntegrationTests/Aiden.IntegrationTests.csproj
pwsh -ExecutionPolicy Bypass -File .\tests\Invoke-TestGate.ps1 -Scope Nightly
pwsh -ExecutionPolicy Bypass -File .\tests\Invoke-TestGate.ps1 -Scope PR
```

CI workflows:
- Build (manual): `.github/workflows/build.yml`
- PR tests: `.github/workflows/tests-pr.yml`
- Nightly tests: `.github/workflows/tests-nightly.yml`
- Feishu notify on `push main`: `.github/workflows/feishu-notification.yml`

Pre-commit gate:
- Enable repo-managed hooks:

```pwsh
pwsh -ExecutionPolicy Bypass -File .\.githooks\setup-githooks.ps1
```

- Hook entrypoint: `.githooks/pre-commit`
- Gate script: `.githooks/pre-commit-gate.ps1`
- Fast checks run on staged changes only:
  - impacted project build + unit tests

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

Use GitHub Actions workflow `.github/workflows/pre-release.yml` to create pre-releases.
Detailed test matrix/report paths/troubleshooting:
- [自动化测试-ATD](docs/自动化测试-ATD.md)

## Release

Pre-release workflow:
- `.github/workflows/pre-release.yml`

Code signing policy:
- [CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md)

For full policy details, see `CODE_SIGNING_POLICY.md`.
## Docs Index

- [产品需求-FRD](docs/产品需求-FRD.md)
- [功能规格-FSD](docs/功能规格-FSD.md)
- [技术设计-TDD](docs/技术设计-TDD.md)
- [自动化测试-ATD](docs/自动化测试-ATD.md)
- [Gemini Metrics Labels DeepDive](docs/Gemini-Metrics-Labels-DeepDive.md)
