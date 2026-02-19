# aiden-windows

Windows desktop monitoring project for Gemini CLI telemetry.

## Project Structure

- `Aiden.TrayMonitor/`: WPF tray app MVP (Direct-to-VM)
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

## Current MVP Features

- Tray resident app
- Left click tray icon to toggle panel
- Right click menu: refresh / show-hide / exit
- Poll VictoriaMetrics periodically
- Manual refresh from panel

## Default Telemetry Target

- `telemetry.enabled = true`
- `telemetry.useCollector = false`
- `telemetry.otlpProtocol = "http"`
- `telemetry.otlpEndpoint = "http://127.0.0.1:8428/opentelemetry"`

