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
- Show latest reported user email (`user.email`, fallback `Unknown`)
- Show user active age in days since latest sample (fallback `N/A`)
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
- `Vm.ServiceNameFilter`
- `Vm.HistoryFallbackDays`
- `Vm.PollSeconds`
- `Collector.BaseUrl`
- `Collector.GrpcPort`
- `Collector.HttpPort`
- `Collector.HealthPort`
- `Pricing.DefaultInputPerMillionUsd`
- `Pricing.DefaultOutputPerMillionUsd`
- `Pricing.ModelRates.<model>.InputPerMillionUsd`
- `Pricing.ModelRates.<model>.OutputPerMillionUsd`
- `ModelCapability.ModelContextWindowTokens.<model>`

`Vm.QueryEndpoint` / `Vm.HealthEndpoint` / `Vm.OtlpEndpoint` can be relative paths.
If relative, the app resolves them with `Vm.BaseUrl`.
`Vm.BaseUrl` can omit port. When omitted, the app uses `Vm.Port`.
`Collector.BaseUrl` can omit port. When omitted, the app uses each collector port item (`GrpcPort` / `HttpPort` / `HealthPort`).

Default telemetry target expected by this app:
- `telemetry.useCollector = true`
- `telemetry.otlpProtocol = "grpc"`
- `telemetry.otlpEndpoint = "http://127.0.0.1:4317"`

Token query filter:
- The app queries only the configured service name via `service.name`.
- Default is `Vm.ServiceNameFilter = "gemini-cli"`.
- Input/Output uses instant query first; if instant has no sample, it falls back to
  `last_over_time(...[<HistoryFallbackDays>d])`.
- Default fallback window is `Vm.HistoryFallbackDays = 7`.
- When current user is `Unknown`, Input/Output display as `N/A`.

Latest user rule:
- The app resolves latest user with instant query first:
  `topk(1, max by (user.email) (timestamp(gen_ai.client.token.usage_sum{service.name="<filter>",user.email!=""})))`
- If instant has no sample, it falls back to:
  `topk(1, max by (user.email) (timestamp(last_over_time(gen_ai.client.token.usage_sum{service.name="<filter>",user.email!=""}[<HistoryFallbackDays>d]))))`
- If no user email exists, it shows `Unknown`.
- User active value is computed from the same timestamp as `floor(now - latestSampleTime)` in days.
- If current user is `Unknown`, user active time is shown as `N/A`.

Context rule:
- Context means token usage of the current user's last active session.
- Session selection uses instant query first:
  `topk(1, max by (session.id) (timestamp(gen_ai.client.token.usage_sum{service.name="<filter>",user.email="<currentUser>",session.id!=""})))`
- If instant has no sample, it falls back to a window query with `HistoryFallbackDays`.
- Context value uses total `usage_sum` of that session (all token types), then divides by
  model context window tokens and shows `M + %`.
- If current user is `Unknown`, context is shown as `N/A`.
- If the active model has no configured capability, context is shown as `N/A`.

Cost rule:
- Cost is computed from token totals by model and token type.
- Formula:
  `costUsd = sum(inputTokens/1_000_000 * inputRatePerMillion + outputTokens/1_000_000 * outputRatePerMillion)`
- Unknown model rates use `Pricing.DefaultInputPerMillionUsd` / `Pricing.DefaultOutputPerMillionUsd`.
