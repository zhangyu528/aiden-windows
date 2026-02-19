# Aiden Tray Monitor (MVP)

## Prerequisites
- Windows 10/11
- .NET 8 SDK

## Run
```powershell
cd Aiden.TrayMonitor
dotnet restore
dotnet run
```

## Current Features
- Tray resident app (left click to toggle panel)
- Right-click menu: Refresh / Show-Hide / Exit
- Auto-hide panel on focus loss
- Poll VictoriaMetrics every 5 seconds
- Manual refresh support

## Config
Edit `Aiden.TrayMonitor/appsettings.json`:
- `Vm.BaseUrl`
- `Vm.OtlpEndpoint`
- `Vm.PollSeconds`

Default telemetry target expected by this app:
- `telemetry.useCollector = false`
- `telemetry.otlpProtocol = "http"`
- `telemetry.otlpEndpoint = "http://127.0.0.1:8428/opentelemetry"`
