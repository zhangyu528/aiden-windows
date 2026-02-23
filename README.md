# aiden-windows

Windows desktop monitoring project for Gemini CLI telemetry.

## Project Structure

- `Aiden.TrayMonitor/`: WPF tray app MVP (Gemini -> Collector -> VM)
- `Aiden.RuntimeAgent/`: user-level background daemon that supervises VM + Collector
- `docs/`: FRD and system design documents

## Prerequisites

- Windows 10/11
- .NET 8 SDK

## Run

```powershell
cd Aiden.TrayMonitor
dotnet restore
dotnet run
```

Run runtime agent (optional manual run, normally auto-started by tray):

```powershell
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

```powershell
powershell -ExecutionPolicy Bypass -File .\Aiden.RuntimeAgent\scripts\download-vm.ps1 `
  -Version "v1.113.0" `
  -DownloadUrl "https://github.com/VictoriaMetrics/VictoriaMetrics/releases/download/v1.113.0/victoria-metrics-windows-amd64-v1.113.0.zip" `
  -Sha256 "ed8f660442a45b260a2c0a0976440ecec863bb75ccb7cec6aad9580364a92de6"
```

`-DownloadUrl` and `-Sha256` are optional in the script. If omitted, provide your own defaults or update script behavior as needed.

Download OTel Collector binary to project runtime path:

```powershell
powershell -ExecutionPolicy Bypass -File .\Aiden.RuntimeAgent\scripts\download-collector.ps1 `
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

## Release Signature and Integrity Verification

Release artifacts are signed by the project release workflow.

- `Aiden.TrayMonitor.exe`
- `Aiden.RuntimeAgent.exe`
- `Aiden-Setup-<version>-win-x64.exe`

Verify installer signature:

```powershell
Get-AuthenticodeSignature .\Aiden-Setup-<version>-win-x64.exe | Format-List Status,SignerCertificate,TimeStamperCertificate
```

Expected `Status`: `Valid`.

Each release also includes `SHA256SUMS.txt`.
Verify file hash against published digest:

```powershell
Get-FileHash -Algorithm SHA256 .\Aiden-Setup-<version>-win-x64.exe
```

Compare the output hash with the corresponding line in `SHA256SUMS.txt`.
