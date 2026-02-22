# Aiden 本地集成版 TDD（技术设计）

## 1. 文档目的
定义 Aiden 本地集成版当前实现的技术方案、组件职责、数据口径、配置与运行策略，面向研发与运维。

## 2. 总体架构
链路：

`Gemini CLI / Codex CLI / Claude Code CLI -> OTel Collector -> VictoriaMetrics -> RuntimeAgent -> WPF Tray`

- 上报链路：CLI 通过 OTLP 到 Collector，再写入 VM。
- 守护链路：RuntimeAgent 探活并拉起 VM / Collector。
- 查询链路：Tray 通过 VM `/api/v1/query` 获取展示数据。

## 3. 组件设计
- `Aiden.RuntimeAgent`
  - `RuntimeSupervisor`：健康检查、指数退避重启、控制端点（`/healthz`、`/status`、`/restart`）。
  - `VmProcessService` / `CollectorProcessService`：拉起与健康探测。
- `Aiden.TrayMonitor`
  - `TrayPanelWindow`：主面板 UI。
  - `TrayPanelViewModel`：状态管理、分页状态、分页切换。
  - `TelemetryService`：轮询与手动刷新。
  - `VmClient`：MetricsQL 查询封装与业务口径实现。
  - `RuntimeAgentClient`：Agent 就绪检查、状态查询与重启请求。
  - `CliProvisioningService`：CLI 安装检测与配置写入。
  - `UserStateService`：首次引导完成标记。
  - `OnboardingProvisioningWindow` / `CliProvisioningWindow` / `StartupLoadingWindow` / `StartupErrorWindow`：启动与配置管理 UI。

## 4. 配置设计
### 4.1 文件拆分
- 共享：`runtime.shared.json`
  - `Vm.*`、`Collector.*`、`Agent.*`
- Tray 专属：`Aiden.TrayMonitor/appsettings.json`
  - `Vm.MaxHistoryDays`、`Vm.PollSeconds`、`Pricing.*`、`ModelCapability.*`
- Agent 专属：`Aiden.RuntimeAgent/agentsettings.json`
  - Agent 局部覆盖项

### 4.2 关键约束
- `Vm.BaseUrl`/`Collector.BaseUrl` 可不带端口，运行时按配置补齐。
- `Vm.ServiceNameFilter` 默认 `gemini-cli`。
- `Vm.MaxHistoryDays` 默认 `365`。
- `Agent.AutoStartOnLogin=true` 时，Tray 保证 HKCU Run 存在 `AidenRuntimeAgent`。

## 5. CLI 配置对齐策略
### 5.1 Gemini CLI
- 文件：`%USERPROFILE%\\.gemini\\settings.json`
- 路径：`telemetry.*`
- 开启：`enabled=true`、`target=local`、`useCollector=true`、`otlpProtocol=grpc`、`otlpEndpoint=http://127.0.0.1:4317`、`logPrompts=false`
- 关闭：`enabled=false`（其余字段保留）

### 5.2 Codex CLI
- 文件：`%USERPROFILE%\\.codex\\config.toml`
- 路径：`[otel]`
- 开启：`environment="dev"`、`log_user_prompt=false`、`exporter={ otlp-grpc = { endpoint = "http://127.0.0.1:4317" } }`、`trace_exporter={ otlp-grpc = { endpoint = "http://127.0.0.1:4317" } }`
- 关闭：`exporter="none"`、`trace_exporter="none"`

### 5.3 Claude Code CLI
- 文件：`%USERPROFILE%\\.claude\\settings.json`
- 路径：`env.*`
- 开启：`CLAUDE_CODE_ENABLE_TELEMETRY=1`、`OTEL_METRICS_EXPORTER=otlp`、`OTEL_LOGS_EXPORTER=otlp`、`OTEL_EXPORTER_OTLP_PROTOCOL=grpc`、`OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317`
- 关闭：`CLAUDE_CODE_ENABLE_TELEMETRY=0`、`OTEL_METRICS_EXPORTER=none`、`OTEL_LOGS_EXPORTER=none`

### 5.4 安装态判定
- `Installed = where.exe <cli>` 命令可用。
- CLI 可用状态：`IsInstalled && IsEnabled`。

## 6. 查询与计算口径
### 6.1 Input / Output
- 瞬时优先：
  - `sum(gen_ai.client.token.usage_sum{gen_ai.token.type="input",service.name="<filter>"})`
  - `sum(gen_ai.client.token.usage_sum{gen_ai.token.type="output",service.name="<filter>"})`
- 回退：`sum(last_over_time(...[<lookbackDays>d]))`
- `lookbackDays = min(ceil(now - activeAt) + 1, MaxHistoryDays)`；无最新用户时使用 `MaxHistoryDays`。
- Codex（`service.name=codex-cli`）由 Collector 将 `response.completed` 日志转换为同一指标：
  `gen_ai.client.token.usage_sum`，其中 `gen_ai.token.type` 为 `input` / `output`。
- Codex 转换链路在 metrics pipeline 上增加 `deltatocumulative` + `metricstarttime`，
  以保证写入 VictoriaMetrics 后可稳定查询。
- Codex 指标写入后到查询可见可能有约 20-30 秒延迟。

### 6.2 Current User
- 瞬时优先：
  - `topk(1, max by (user.email) (timestamp(gen_ai.client.token.usage_sum{service.name="<filter>",user.email!=""})))`
- 回退：
  - `topk(1, max by (user.email) (timestamp(last_over_time(...[<MaxHistoryDays>d]))))`

### 6.3 User Active
- 取 Current User 查询结果时间戳 `value[0]`。
- 计算：`floor(now - latestSampleTime)`（天）。

### 6.4 Context
1. 选当前用户活跃 `session.id`（瞬时优先，回退窗口）。
2. 取该 session 的 `input` token：
   `sum(gen_ai.client.token.usage_sum{service.name="<filter>",user.email="<currentUser>",session.id="<session>",gen_ai.token.type="input"})`。
3. 取该 session 最新模型 `gen_ai.request.model`。
4. 计算占比：`input_usage_sum / ModelContextWindowTokens[model] * 100`。
5. session 选择稳定规则：时间戳最大优先；并列取字典序最大 `session.id`。

### 6.5 Cost
- 按模型与 token.type 聚合后套用 `Pricing` 单价。
- 未知模型走默认单价。

## 7. 主面板分页技术策略
- 分页映射（运行时覆盖 `Vm.ServiceNameFilter`，不落盘）：
  - Gemini -> `gemini-cli`
  - Codex -> `codex-cli`
  - Claude -> `claude-code`
- 分页切换流程：
  1. 更新 `TelemetryService -> VmClient` 的 `service.name` 过滤。
  2. 立即触发 `RefreshOnceAsync`。
- 分页可用性来源：`CliProvisioningService.GetStatesAsync`。
- 禁用规则：不可用分页禁用且不触发切换。
- 回退规则：当前分页失效时，按 `Gemini -> Codex -> Claude` 回退到首个可用分页。
- Settings 关闭后触发状态重载，更新分页可用性。

## 8. 运行时流程
1. Tray 启动加载配置。
2. `OnboardingCompleted=false` 时按开通状态决定是否弹引导。
3. Continue 后进入首次启动状态页流程（Loading/Error）。
4. 非首次启动直接 `RuntimeAgentClient.EnsureReadyAsync()`。
5. Runtime 就绪后启动轮询并显示主 Dashboard。
6. Exit 仅关闭 Tray，Agent 持续运行。

## 9. 升级与卸载
### 9.1 升级（停机升级）
- 脚本：`Aiden.RuntimeAgent/scripts/upgrade-stop-install-start.ps1`
- 流程：停进程 -> 覆盖安装 -> 更新 HKCU Run -> 启动 Agent（可选 Tray）

### 9.2 卸载清理
- 脚本：`Aiden.RuntimeAgent/scripts/uninstall-clean-agent.ps1`
- 流程：停进程 -> 删除 HKCU Run -> 删除安装目录
- 不恢复 CLI telemetry 配置

## 10. 状态定义
- `ONLINE`：VM 健康检查可访问且关键查询成功。
- `OFFLINE`：健康检查或关键查询失败。
- Tray 菜单可查看 Runtime 状态。

## 11. 关联文档
- 功能规格见：`docs/Aiden本地集成版-FSD.md`
- 需求总览见：`docs/Aiden本地集成版-FRD.md`
