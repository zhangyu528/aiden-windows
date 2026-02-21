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
- Collector install path: `Aiden.RuntimeAgent/runtime/collector/<version>/otelcol.exe`
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
  -DownloadUrl "https://github.com/open-telemetry/opentelemetry-collector-releases/releases/download/v0.146.1/otelcol_0.146.1_windows_amd64.tar.gz" `
  -Sha256 "0eaa1ff9d0f5d8009921667368981617641cebb1766fc7b38be95d5dc21a126a"
```

Notes:
- Default artifact is core collector (`otelcol_...`) rather than contrib.
- `-DownloadUrl` and `-Sha256` are optional; script can auto-resolve SHA256 from release `checksums.txt`.
