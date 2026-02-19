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
- Auto-start VictoriaMetrics from `Aiden.TrayMonitor/runtime/vm/<version>/victoria-metrics.exe` if not already running
- Auto-start OTel Collector from `Aiden.TrayMonitor/runtime/collector/<version>/otelcol*.exe` if available
- Poll VictoriaMetrics every 5 seconds
- Manual refresh support

## Runtime Binaries

Place binaries under:

- `Aiden.TrayMonitor/runtime/vm/<version>/victoria-metrics.exe`
- `Aiden.TrayMonitor/runtime/collector/<version>/otelcol*.exe`

The app will generate collector config at runtime:

- `Aiden.TrayMonitor/runtime/collector/<version>/config/otelcol-vm.yaml`

## Config
Edit `Aiden.TrayMonitor/appsettings.json`:
- `Vm.BaseUrl`
- `Vm.Port`
- `Vm.QueryEndpoint`
- `Vm.HealthEndpoint`
- `Vm.OtlpEndpoint`
- `Vm.PollSeconds`
- `Collector.BaseUrl`
- `Collector.GrpcPort`
- `Collector.HttpPort`
- `Collector.HealthPort`

`Vm.QueryEndpoint` / `Vm.HealthEndpoint` / `Vm.OtlpEndpoint` can be relative paths.
If relative, the app resolves them with `Vm.BaseUrl`.
`Vm.BaseUrl` can omit port. When omitted, the app uses `Vm.Port`.
`Collector.BaseUrl` can omit port. When omitted, the app uses each collector port item (`GrpcPort` / `HttpPort` / `HealthPort`).

Default telemetry target expected by this app:
- `telemetry.useCollector = true`
- `telemetry.otlpProtocol = "grpc"`
- `telemetry.otlpEndpoint = "http://127.0.0.1:4317"`
