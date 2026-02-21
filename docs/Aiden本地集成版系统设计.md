# Aiden 本地集成版系统设计（当前实现）

## 1. 目标与范围
面向 Windows 托盘监控场景，展示已开通 CLI（Gemini/Codex/Claude Code）的 telemetry 指标，并在关闭 UI 后保持后台采集。

本期目标：
- 托盘常驻显示核心数据。
- 用户级 RuntimeAgent 守护 VictoriaMetrics 与 OTel Collector（无需管理员权限）。
- Tray 退出仅关闭 UI，不中断 Runtime 采集链路。
- 基于 VictoriaMetrics 查询接口周期刷新 UI。

本期不包含：
- Windows 服务托管。
- 无缝热升级。
- 终端日志展示。
- 趋势与性能分析页面。
- Web 界面。

## 2. 链路
`Gemini CLI / Codex CLI / Claude Code CLI -> OTel Collector -> VictoriaMetrics -> RuntimeAgent -> WPF Tray`

- 上报链路：已开通 CLI 通过 OTLP 发到 Collector，Collector 再转发到 VM。
- 守护链路：RuntimeAgent 周期健康检查并自动拉起 VM / Collector。
- 查询链路：Tray App 通过 VM 的 `/api/v1/query` 查询展示。

## 3. 组件
- `Aiden.RuntimeAgent`：用户级后台守护进程。
  - `RuntimeSupervisor`：健康检查、指数退避重启、控制端点（`/healthz`、`/status`、`/restart`）。
  - `VmProcessService` / `CollectorProcessService`：VM/Collector 拉起与健康探测。
- `TrayPanelWindow`：展示 Input/Output/User/User Active/Cost/Context/Status。
- `CliProvisioningWindow`：首次引导与 Settings 复用的 CLI 开通管理窗口。
- `TelemetryService`：按 `Vm.PollSeconds` 轮询，支持手动刷新。
- `VmClient`：封装 MetricsQL 查询与业务口径。
- `RuntimeAgentClient`：Tray 侧 Agent 探活、状态查询与重启请求。
- `CliProvisioningService`：检测 CLI 安装状态，读写 Gemini/Codex/Claude telemetry 配置。
- `UserStateService`：管理首次引导完成标记。

### 3.1 CLI 配置字段对齐（当前实现）
- Gemini CLI：`%USERPROFILE%\\.gemini\\settings.json`
  - 路径：`telemetry.*`
  - 开启：`enabled=true`、`target=local`、`useCollector=true`、`otlpProtocol=grpc`、`otlpEndpoint=http://127.0.0.1:4317`、`logPrompts=false`
  - 关闭：`enabled=false`（其余字段保留）
- Codex CLI：`%USERPROFILE%\\.codex\\config.toml`
  - 路径：`[otel]`
  - 开启：`environment="dev"`、`log_user_prompt=false`、`exporter={ otlp-grpc = { endpoint = "http://127.0.0.1:4317" } }`、`trace_exporter={ otlp-grpc = { endpoint = "http://127.0.0.1:4317" } }`
  - 关闭：`exporter="none"`、`trace_exporter="none"`
- Claude Code CLI：`%USERPROFILE%\\.claude\\settings.json`
  - 路径：`env.*`
  - 开启：`CLAUDE_CODE_ENABLE_TELEMETRY=1`、`OTEL_METRICS_EXPORTER=otlp`、`OTEL_LOGS_EXPORTER=otlp`、`OTEL_EXPORTER_OTLP_PROTOCOL=grpc`、`OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317`
  - 关闭：`CLAUDE_CODE_ENABLE_TELEMETRY=0`、`OTEL_METRICS_EXPORTER=none`、`OTEL_LOGS_EXPORTER=none`
- 安装态判定：优先 `where.exe <cli>`；若失败但配置文件存在，仍视为已安装。

## 4. 配置

### 4.1 配置文件拆分
- 共享配置：`runtime.shared.json`
  - `Vm.BaseUrl`、`Vm.Port`、`Vm.QueryEndpoint`、`Vm.HealthEndpoint`、`Vm.OtlpEndpoint`、`Vm.ServiceNameFilter`
  - `Collector.BaseUrl`、`Collector.GrpcPort`、`Collector.HttpPort`、`Collector.HealthPort`
  - `Agent.Enabled`、`Agent.AutoStartOnLogin`、`Agent.HealthCheckSeconds`、`Agent.BackoffMinSeconds`、`Agent.BackoffMaxSeconds`、`Agent.StatusPort`
- Tray 专属：`Aiden.TrayMonitor/appsettings.json`
  - `Vm.MaxHistoryDays`、`Vm.PollSeconds`
  - `Pricing.*`
  - `ModelCapability.*`
- Agent 专属：`Aiden.RuntimeAgent/agentsettings.json`
  - Agent 局部覆盖项（按需）

### 4.2 约束
- `Vm.BaseUrl`、`Collector.BaseUrl` 可不带端口，端口由对应配置补齐。
- `Vm.ServiceNameFilter` 默认 `gemini-cli`。
- `Vm.MaxHistoryDays` 默认 `365`。
- `Agent.AutoStartOnLogin=true` 时，Tray 启动会确保 HKCU Run：`AidenRuntimeAgent`。

## 5. 指标口径
### 5.1 Input / Output
- 瞬时优先：
  - `sum(gen_ai.client.token.usage_sum{gen_ai.token.type="input",service.name="<filter>"})`
  - `sum(gen_ai.client.token.usage_sum{gen_ai.token.type="output",service.name="<filter>"})`
- 无样本回退：`sum(last_over_time(...[<lookbackDays>d]))`
- `lookbackDays`：`min(ceil(now - activeAt) + 1, MaxHistoryDays)`；若无最新用户则 `MaxHistoryDays`。
- 当 `CurrentUserEmail=Unknown` 时，Input/Output 显示 `N/A`。

### 5.2 Current User
- 瞬时优先：
  - `topk(1, max by (user.email) (timestamp(gen_ai.client.token.usage_sum{service.name="<filter>",user.email!=""})))`
- 回退窗口：
  - `topk(1, max by (user.email) (timestamp(last_over_time(...[<MaxHistoryDays>d]))))`
- 无结果显示 `Unknown`。

### 5.3 User Active
- 从“当前用户”查询结果的 `value[0]`（Unix 时间戳）解析。
- 计算口径：`floor(now - latestSampleTime)`（单位：天）。
- 显示格式：`X days`。
- 当前用户为 `Unknown` 时显示 `N/A`。

### 5.4 Context（当前活跃会话）
1. 选当前用户最后活跃 `session.id`（瞬时优先，回退窗口）。
   - 瞬时候选查询：
     `max by (session.id) (timestamp(gen_ai.client.token.usage_sum{service.name="<filter>",user.email="<currentUser>",session.id!=""}))`
   - 回退候选查询：
     `max by (session.id) (timestamp(last_over_time(gen_ai.client.token.usage_sum{service.name="<filter>",user.email="<currentUser>",session.id!=""}[<lookbackDays>d])))`
   - 应用内稳定选择规则：先取时间戳最大；若时间戳并列，取字典序最大的 `session.id`。
2. 取该 session 的综合 `usage_sum`（不区分 token.type）。
3. 取该 session 最后一次模型 `gen_ai.request.model`。
4. 计算百分比：`usage_sum / ModelContextWindowTokens[model] * 100`。
5. 显示：`x.xxx M (yy.y%)`。

显示规则：
- 用户未知或模型能力缺失时，Context 显示 `N/A`。

### 5.5 Cost
- 按模型和 token.type 聚合 `usage_sum` 后，套用 `Pricing` 单价计算。
- 未知模型使用默认单价。

## 6. 运行时流程
1. Tray 启动：加载配置 -> `RuntimeAgentClient.EnsureReadyAsync()`。
2. Agent 探活失败：Tray 拉起 Agent，并等待健康。
3. 读取 `UserStateService`：
   - 若 `OnboardingCompleted=false` 且三项 CLI 未全部开通，则弹出引导页。
   - 若 `OnboardingCompleted=false` 且三项 CLI 已全部开通，则自动标记完成并跳过引导。
4. 引导页行为：
   - 点击 Continue：继续启动 Tray，并标记 `OnboardingCompleted=true`。
   - 直接关闭引导页：应用显式退出，不继续启动 Tray。
5. 引导通过后，Tray 开始轮询显示。
6. 用户点击 Exit：仅关闭 Tray，Agent 持续运行。

## 7. 升级与卸载
### 7.1 升级（停机升级）
- 使用脚本：`Aiden.RuntimeAgent/scripts/upgrade-stop-install-start.ps1`
- 流程：停进程 -> 覆盖安装 -> 更新 HKCU Run -> 启动 Agent（可选 Tray）。

### 7.2 卸载清理
- 使用脚本：`Aiden.RuntimeAgent/scripts/uninstall-clean-agent.ps1`
- 流程：停进程 -> 删除 HKCU Run -> 删除安装目录（按参数）。
- 不恢复 CLI telemetry 配置。

## 8. 状态
- `Online`：VM 健康检查可访问且关键查询成功。
- `Offline`：健康检查或关键查询失败。
- `Runtime Status` 菜单项可查看 Agent 报告状态。
