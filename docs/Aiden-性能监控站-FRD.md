# Aiden 本地托盘监控 FRD（当前版本）

## 1. 目标
在 Windows 下提供 Gemini CLI telemetry 的托盘可视化，低打扰展示核心消耗数据。

## 2. 范围
### 2.1 In Scope
- 托盘面板展示：Input、Output、User、User Active、Cost、Context、Status。
- 链路：Gemini CLI -> OTel Collector -> VictoriaMetrics。
- 应用启动时自动拉起 VM/Collector（未运行时）。
- 自动刷新与手动刷新。

### 2.2 Out of Scope
- 终端日志展示。
- 趋势与性能分析。
- Web 页面。

## 3. 配置需求
### 3.1 Gemini CLI（`~/.gemini/settings.json`）
- `telemetry.enabled = true`
- `telemetry.target = "local"`
- `telemetry.useCollector = true`
- `telemetry.otlpProtocol = "grpc"`
- `telemetry.otlpEndpoint = "http://127.0.0.1:4317"`
- `telemetry.logPrompts = false`

### 3.2 应用（`Aiden.TrayMonitor/appsettings.json`）
- `Vm.*`：查询和健康检查配置、服务过滤、回退窗口、轮询周期。
- `Collector.*`：Collector 地址与端口。
- `Pricing.*`：成本单价。
- `ModelCapability.*`：模型上下文能力。

## 4. 功能需求
### 4.1 展示字段
- Input Tokens
- Output Tokens
- Current User Email
- User Active
- Cost USD
- Context（M + %）
- Status

### 4.2 刷新行为
- 自动轮询：按 `Vm.PollSeconds`。
- 手动刷新：点击 Refresh 按钮。

### 4.3 运行时行为
- 启动时检查并拉起 VM / Collector。
- 依赖不可用时降级为 Offline。

## 5. 指标口径
### 5.1 Input / Output
- 查询策略：瞬时优先，空值回退 `last_over_time` 窗口。
- 用户未知（`CurrentUserEmail=Unknown`）时显示 `N/A`。

### 5.2 Current User
- 取最近一次上报用户（瞬时优先，空值回退窗口）。
- 无数据显示 `Unknown`。

### 5.3 User Active
- 使用 Current User 同一查询结果中的时间戳。
- 展示为距今天数：`floor(now - latestSampleTime)`，格式 `X days`。
- 用户未知时显示 `N/A`。

### 5.4 Cost
- 按模型和 token 类型聚合后，使用 `Pricing` 计算。
- 未知模型走默认单价。

### 5.5 Context
- 基于当前用户的最后活跃 session。
- session 选择策略：
  - 瞬时优先，回退 `last_over_time` 窗口。
  - 先查询全部候选 session（按 `session.id` 聚合时间戳），再在应用内稳定选择：
    1) 时间戳最大优先；
    2) 时间戳并列时，取字典序最大的 `session.id`。
- 使用该 session 的综合 `usage_sum`。
- 按模型能力计算百分比并显示 `x.xxx M (yy.y%)`。
- 用户未知或模型能力缺失时显示 `N/A`。

## 6. 验收标准
1. 启动应用后，VM/Collector 未运行时可自动拉起。
2. 触发 Gemini CLI 请求后，面板字段可更新。
3. 停止上报后，回退窗口内仍可显示历史值。
4. 用户未知时，Input/Output/User Active/Context 显示 `N/A` 或 `Unknown`（按字段定义）。
5. 模型能力缺失时，Context 显示 `N/A`。
