# Aiden 本地集成版系统设计（当前实现）

## 1. 目标与范围
面向 Windows 托盘监控场景，展示 Gemini CLI 的 telemetry 指标。

本期目标：
- 托盘常驻显示核心数据。
- 自动拉起 VictoriaMetrics 与 OTel Collector（未运行时）。
- 基于 VictoriaMetrics 查询接口周期刷新 UI。

本期不包含：
- 终端日志展示。
- 趋势与性能分析页面。
- Web 界面。

## 2. 链路
`Gemini CLI -> OTel Collector -> VictoriaMetrics -> WPF Tray`

- 上报链路：Gemini CLI 通过 OTLP gRPC/protobuf 发到 Collector，Collector 再转发到 VM。
- 查询链路：Tray App 通过 VM 的 `/api/v1/query` 查询展示。

## 3. 组件
- `TrayPanelWindow`：展示 Input/Output/User/User Active/Cost/Context/Status。
- `TelemetryService`：按 `Vm.PollSeconds` 轮询，支持手动刷新。
- `VmProcessService`：启动并托管 `runtime/vm/<version>/victoria-metrics.exe`。
- `CollectorProcessService`：启动并托管 `runtime/collector/<version>/otelcol*.exe`，运行时生成 collector 配置。
- `VmClient`：封装 MetricsQL 查询与业务口径。

## 4. 配置
配置文件：`Aiden.TrayMonitor/appsettings.json`

- `Vm`：
  - `BaseUrl`、`Port`、`QueryEndpoint`、`HealthEndpoint`、`OtlpEndpoint`
  - `ServiceNameFilter`
  - `HistoryFallbackDays`
  - `PollSeconds`
- `Collector`：
  - `BaseUrl`、`GrpcPort`、`HttpPort`、`HealthPort`
- `Pricing`：默认单价与模型单价
- `ModelCapability`：模型上下文窗口 token 能力

约束：
- `Vm.BaseUrl`、`Collector.BaseUrl` 可不带端口，端口由对应配置补齐。
- `Vm.ServiceNameFilter` 默认 `gemini-cli`。
- `Vm.HistoryFallbackDays` 默认 `7`。

## 5. 指标口径
### 5.1 Input / Output
- 瞬时优先：
  - `sum(gen_ai.client.token.usage_sum{gen_ai.token.type="input",service.name="<filter>"})`
  - `sum(gen_ai.client.token.usage_sum{gen_ai.token.type="output",service.name="<filter>"})`
- 无样本回退：`sum(last_over_time(...[<HistoryFallbackDays>d]))`
- 当 `CurrentUserEmail=Unknown` 时，Input/Output 显示 `N/A`。

### 5.2 Current User
- 瞬时优先：
  - `topk(1, max by (user.email) (timestamp(gen_ai.client.token.usage_sum{service.name="<filter>",user.email!=""})))`
- 回退窗口：
  - `topk(1, max by (user.email) (timestamp(last_over_time(...[<HistoryFallbackDays>d]))))`
- 无结果显示 `Unknown`。

### 5.3 User Active
- 从“当前用户”查询结果的 `value[0]`（Unix 时间戳）解析。
- 计算口径：`floor(now - latestSampleTime)`（单位：天）。
- 显示格式：`X days`。
- 当前用户为 `Unknown` 时显示 `N/A`。

### 5.4 Context（当前活跃会话）
1. 选当前用户最后活跃 `session.id`（瞬时优先，回退窗口）。
2. 取该 session 的综合 `usage_sum`（不区分 token.type）。
3. 取该 session 最后一次模型 `gen_ai.request.model`。
4. 计算百分比：`usage_sum / ModelContextWindowTokens[model] * 100`。
5. 显示：`x.xxx M (yy.y%)`。

显示规则：
- 用户未知或模型能力缺失时，Context 显示 `N/A`。

### 5.5 Cost
- 按模型和 token.type 聚合 `usage_sum` 后，套用 `Pricing` 单价计算。
- 未知模型使用默认单价。

## 6. 状态
- `Online`：VM 健康检查可访问且关键查询成功。
- `Offline`：健康检查或关键查询失败。
