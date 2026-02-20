# Aiden 本地托盘监控 FRD（当前版本）

## 1. 目标
在 Windows 下提供 Gemini CLI telemetry 的托盘可视化，低打扰展示核心消耗数据，并支持关闭 UI 后继续后台采集。

## 2. 范围
### 2.1 In Scope
- 托盘面板展示：Input、Output、User、User Active、Cost、Context、Status。
- 链路：Gemini CLI -> OTel Collector -> VictoriaMetrics。
- 用户级后台守护进程（RuntimeAgent）托管 VM / Collector。
- 自动刷新与手动刷新。
- 停机升级（先停后装）与卸载清理（含 Agent 与 HKCU Run）。

### 2.2 Out of Scope
- Windows 服务（管理员权限）托管。
- 无缝热升级。
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

### 3.2 应用配置拆分
- `runtime.shared.json`（共享）：
  - `Vm.*`（BaseUrl/Port/QueryEndpoint/HealthEndpoint/OtlpEndpoint/ServiceNameFilter）
  - `Collector.*`（BaseUrl/GrpcPort/HttpPort/HealthPort）
  - `Agent.*`（Enabled/AutoStartOnLogin/HealthCheckSeconds/BackoffMinSeconds/BackoffMaxSeconds/StatusPort）
- `Aiden.TrayMonitor/appsettings.json`（Tray 专属）：
  - `Vm.MaxHistoryDays`
  - `Vm.PollSeconds`
  - `Pricing.*`
  - `ModelCapability.*`
- `Aiden.RuntimeAgent/agentsettings.json`（Agent 专属覆盖项）

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
- Tray 启动时确保 RuntimeAgent 运行并写入 HKCU Run（可配置）。
- RuntimeAgent 守护 VM / Collector。
- Tray Exit 仅关闭 UI，不停止 RuntimeAgent。
- Runtime 不健康时自动重启（指数退避）。

## 5. 指标口径
### 5.1 Input / Output
- 查询策略：瞬时优先，空值回退 `last_over_time`。
- `lookbackDays = min(ceil(now - activeAt) + 1, MaxHistoryDays)`；
  若无最新用户，`lookbackDays = MaxHistoryDays`。
- 用户未知（`CurrentUserEmail=Unknown`）时显示 `N/A`。

### 5.2 Current User
- 取最近一次上报用户（瞬时优先，回退 `MaxHistoryDays` 窗口）。
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
  - 先查询候选 session（按 `session.id` 聚合时间戳）。
  - 应用内稳定选择：
    1) 时间戳最大优先；
    2) 时间戳并列时取字典序最大 `session.id`。
- 使用该 session 的综合 `usage_sum`。
- 按模型能力计算百分比并显示 `x.xxx M (yy.y%)`。
- 用户未知或模型能力缺失时显示 `N/A`。

## 6. 升级与卸载
### 6.1 升级（停机升级）
1. 停 RuntimeAgent + VM + Collector + Tray。
2. 安装新包。
3. 重写 HKCU Run（`AidenRuntimeAgent`）。
4. 启动 RuntimeAgent（可选同时启动 Tray）。

### 6.2 卸载
- 停 RuntimeAgent/Tray/VM/Collector。
- 删除 HKCU Run：`AidenRuntimeAgent`。
- 删除安装目录（按卸载参数）。
- 不恢复 CLI telemetry 配置。

## 7. 验收标准
1. 关闭 Tray UI 后，VM / Collector 仍持续运行并可采集。
2. Runtime 任一进程异常退出后可自动恢复。
3. 升级后 Runtime 可恢复运行，允许短时中断。
4. 卸载后无 Agent 进程与 HKCU 自启动残留。
5. 指标口径与显示规则符合第 5 节。
