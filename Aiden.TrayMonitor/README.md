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
- Auto-start VictoriaMetrics from `Aiden.RuntimeAgent/runtime/vm/<version>/victoria-metrics.exe` if not already running
- Auto-start OTel Collector from `Aiden.RuntimeAgent/runtime/collector/<version>/otelcol-contrib.exe` if available
- Poll VictoriaMetrics every 5 seconds
- Manual refresh support

## Runtime Binaries

Place binaries under:

- `Aiden.RuntimeAgent/runtime/vm/<version>/victoria-metrics.exe`
- `Aiden.RuntimeAgent/runtime/collector/<version>/otelcol-contrib.exe`

The app will generate collector config at runtime:

- `Aiden.RuntimeAgent/runtime/collector/<version>/config/otelcol-vm.yaml`

## Config
Shared runtime settings are loaded from `runtime.shared.json`:
- `Vm.BaseUrl`
- `Vm.Port`
- `Vm.QueryEndpoint`
- `Vm.HealthEndpoint`
- `Vm.OtlpEndpoint`
- `Vm.ServiceNameFilter`
- `Collector.BaseUrl`
- `Collector.GrpcPort`
- `Collector.HttpPort`
- `Collector.HealthPort`
- `Agent.Enabled`
- `Agent.AutoStartOnLogin`
- `Agent.HealthCheckSeconds`
- `Agent.BackoffMinSeconds`
- `Agent.BackoffMaxSeconds`
- `Agent.StatusPort`

Tray-specific settings are loaded from `Aiden.TrayMonitor/appsettings.json`:
- `Vm.MaxHistoryDays`
- `Vm.PollSeconds`
- `Pricing.DefaultInputPerMillionUsd`
- `Pricing.DefaultOutputPerMillionUsd`
- `Pricing.ModelRates.<model>.InputPerMillionUsd`
- `Pricing.ModelRates.<model>.OutputPerMillionUsd`
- `ModelCapability.ModelContextWindowTokens.<model>`

`Vm.QueryEndpoint` / `Vm.HealthEndpoint` / `Vm.OtlpEndpoint` can be relative paths.
If relative, the app resolves them with `Vm.BaseUrl`.
`Vm.BaseUrl` can omit port. When omitted, the app uses `Vm.Port`.
`Collector.BaseUrl` can omit port. When omitted, the app uses each collector port item (`GrpcPort` / `HttpPort` / `HealthPort`).

Runtime agent behavior:
- Tray app ensures `Aiden.RuntimeAgent` is running at startup.
- If `Agent.AutoStartOnLogin = true`, tray writes HKCU auto-start key (`AidenRuntimeAgent`).
- Exiting tray UI does not stop runtime agent.

Default telemetry target expected by this app:
- `telemetry.useCollector = true`
- `telemetry.otlpProtocol = "grpc"`
- `telemetry.otlpEndpoint = "http://127.0.0.1:4317"`

Token query filter:
- The app queries only the configured service name via `service.name`.
- Default is `Vm.ServiceNameFilter = "gemini-cli"`.
- Input/Output uses instant query first; if instant has no sample, it falls back to
  `last_over_time(...[<lookbackDays>d])`.
- `lookbackDays` is dynamic:
  - when latest user active time exists: `ceil(now - activeAt) + 1`, capped by `Vm.MaxHistoryDays`;
  - when latest user is unknown: use `Vm.MaxHistoryDays`.
- When current user is `Unknown`, Input/Output display as `N/A`.

Codex log-to-metrics conversion:
- For `service.name=codex_cli_rs`, collector converts `response.completed` logs to
  `gen_ai.client.token.usage_sum`.
- Converted series use:
  - data point attribute: `gen_ai.token.type` (`input` / `output`)
  - resource attributes: `service.name=codex-cli`, `user.email`, `session.id`, `gen_ai.request.model`
- Codex metrics pipeline includes `deltatocumulative` + `metricstarttime` before export to VM.
  This is required so converted sum metrics are queryable reliably in VictoriaMetrics.
- Expected query visibility delay is about 20-30 seconds for Codex converted metrics.

Latest user rule:
- The app resolves latest user with instant query first:
  `topk(1, max by (user.email) (timestamp(gen_ai.client.token.usage_sum{service.name="<filter>",user.email!=""})))`
- If instant has no sample, it falls back to:
  `topk(1, max by (user.email) (timestamp(last_over_time(gen_ai.client.token.usage_sum{service.name="<filter>",user.email!=""}[<MaxHistoryDays>d]))))`
- If no user email exists, it shows `Unknown`.
- User active value is computed from the same timestamp as `floor(now - latestSampleTime)` in days.
- If current user is `Unknown`, user active time is shown as `N/A`.

Context rule:
- Context means token usage of the current user's last active session.
- Session selection uses instant query first, then stable pick in app:
  `max by (session.id) (timestamp(gen_ai.client.token.usage_sum{service.name="<filter>",user.email="<currentUser>",session.id!=""}))`
- If instant has no sample, it falls back to a window query with dynamic `lookbackDays`:
  `max by (session.id) (timestamp(last_over_time(gen_ai.client.token.usage_sum{service.name="<filter>",user.email="<currentUser>",session.id!=""}[<lookbackDays>d])))`
- Stable pick rule in app: highest timestamp first; when timestamps tie, choose lexical max `session.id`.
- Context value uses session `input` token usage only:
  `sum(gen_ai.client.token.usage_sum{...,gen_ai.token.type="input"})`,
  then divides by model context window tokens and shows `M + %`.
- If current user is `Unknown`, context is shown as `N/A`.
- If the active model has no configured capability, context is shown as `N/A`.

Cost rule:
- Cost is computed from token totals by model and token type.
- Formula:
  `costUsd = sum(inputTokens/1_000_000 * inputRatePerMillion + outputTokens/1_000_000 * outputRatePerMillion)`
- Unknown model rates use `Pricing.DefaultInputPerMillionUsd` / `Pricing.DefaultOutputPerMillionUsd`.
