# 技术设计-TDD

## 1. 文档定位

本文档定义当前实现的技术架构、组件职责、配置与运行策略，面向研发与运维。

## 2. 总体架构

链路：

`Gemini/Codex/Claude CLI -> OTel Collector -> VictoriaMetrics -> RuntimeAgent -> WPF Tray`

职责拆分：
- 上报：CLI -> Collector -> VM
- 守护：RuntimeAgent 负责 VM/Collector 进程健康
- 展示：Tray 查询 VM 并渲染 UI

## 3. 组件职责

### 3.1 Aiden.RuntimeAgent
- `RuntimeSupervisor`：健康检查、重启请求、控制端点（`/healthz`、`/status`、`/restart`）
- `VmProcessService`：VM 进程拉起/探活
- `CollectorProcessService`：Collector 进程拉起/探活

### 3.2 Aiden.TrayMonitor
- `TrayPanelWindow` / `TrayPanelViewModel`：主面板 UI 与状态管理
- `TelemetryService`：轮询与手动刷新
- `VmClient`：指标查询与口径封装
- `RuntimeAgentClient`：Agent 状态检查与重启调用
- `CliProvisioningService`：CLI 安装检测与配置写入

### 3.3 脚本与流程
- `automation/tests/Aiden/`：产品逻辑测试目录 (Unit, Integration, UI)
- `automation/tests/Workflow/`：CI 脚本 Pester 用例目录
- `.github/workflows/build.yml`：手动编译校验
- `.github/workflows/tests-pr.yml`：PR 测试
- `.github/workflows/tests-nightly.yml`：Nightly 测试
- `.github/workflows/feishu-notification.yml`：main 提交通知

## 4. 配置设计

### 4.1 文件分层
- 共享配置：`runtime.shared.json`
- Tray 专属：`Aiden.TrayMonitor/appsettings.json`
- Agent 专属：`Aiden.RuntimeAgent/agentsettings.json`

### 4.2 关键默认约束
- `Agent.StatusPort` 默认 `18731`
- Agent 健康检查与退避由 `Agent.*` 参数控制
- Runtime 二进制放置在 `Aiden.RuntimeAgent/runtime/*`

## 5. 运行与恢复策略

- Agent 持续巡检 VM 与 Collector。
- 依赖异常时执行退避重试。
- `/restart` 控制端点可触发重启流程。

## 6. 测试与构建映射

- 构建：`build.yml`（手动，`dotnet build Aiden.sln -c Release`）
- 单元/集成：`tests-pr.yml` + `tests-nightly.yml`
- `automation/tests/Invoke-TestGate.ps1 -Scope Staged/PR/Nightly`：统一测试入口 + CI Summary

测试报告路径统一：
- `artifacts/test-results/dotnet/*.trx`
- `artifacts/test-results/ui/*.trx`
- `artifacts/test-results/scripts/pester-workflow.xml`

## 7. 限制与兼容性说明

- UI smoke 中部分用例依赖交互式桌面，默认可能存在 skip。
- 某些本地受限环境可能影响 `HttpListener` 行为，建议以正常用户会话或 CI 结果为准。
- Git hook 在部分环境中可能受 shell 权限策略影响，当前项目以可执行稳定性优先。

## 8. 相关文档

- 需求文档：`docs/产品需求-FRD.md`
- 功能规格：`docs/功能规格-FSD.md`
- 自动化测试：`docs/自动化测试-ATD.md`
