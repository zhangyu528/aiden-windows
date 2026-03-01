# 自动化测试-ATD

## 1. 目标与范围

本文档用于说明当前仓库的自动化测试体系，包括：

- 本地如何执行测试
- PR 与 Nightly 在 GitHub Actions 中如何触发和运行
- 测试报告产物如何生成与查看
- 常见问题与已知限制

适用对象：日常开发、提 PR、维护 CI 的团队成员。

---

## 2. 测试全景总览

| 测试类型 | 测试项目 | 本地执行入口 | 报告格式 | 报告路径 | CI 覆盖 |
|---|---|---|---|---|---|
| .NET 单元测试 | `Aiden.RuntimeAgent.UnitTests` | `dotnet test ...` | TRX | `artifacts/test-results/dotnet/*.trx` | PR + Nightly |
| .NET 单元测试 | `Aiden.TrayMonitor.UnitTests` | `dotnet test ...` | TRX | `artifacts/test-results/dotnet/*.trx` | PR + Nightly |
| .NET 集成测试 | `Aiden.IntegrationTests` | `dotnet test ...` | TRX | `artifacts/test-results/dotnet/*.trx` | PR + Nightly |
| UI 测试 | `Aiden.UI.Tests` | `scripts/run-ui-tests.ps1` | TRX | `artifacts/test-results/ui/*.trx` | Nightly |
| 脚本测试 | `tests/Aiden.Scripts.Tests` (Pester) | `scripts/run-script-tests.ps1` | NUnit XML | `artifacts/test-results/scripts/pester-scripts.xml` | PR + Nightly |

---

## 3. 本地执行指南

## 3.1 前置条件

- Windows 10/11
- .NET 8 SDK
- PowerShell 7 (`pwsh`)

## 3.2 .NET 单元与集成测试

```pwsh
dotnet test tests/Aiden.RuntimeAgent.UnitTests/Aiden.RuntimeAgent.UnitTests.csproj -c Release --logger "trx;LogFileName=runtimeagent-unit.trx" --results-directory artifacts/test-results/dotnet
dotnet test tests/Aiden.TrayMonitor.UnitTests/Aiden.TrayMonitor.UnitTests.csproj -c Release --logger "trx;LogFileName=traymonitor-unit.trx" --results-directory artifacts/test-results/dotnet
dotnet test tests/Aiden.IntegrationTests/Aiden.IntegrationTests.csproj -c Release --logger "trx;LogFileName=integration.trx" --results-directory artifacts/test-results/dotnet
```

## 3.3 UI 测试

推荐入口（会自动设置 `AIDEN_UI_APP_PATH`）：

```pwsh
pwsh -ExecutionPolicy Bypass -File .\scripts\run-ui-tests.ps1 -PublishApp -Configuration Release -TrxLogFileName "ui-smoke.trx" -ResultsDirectory "artifacts/test-results/ui"
```

如果已经有可用可执行文件，也可显式传入路径：

```pwsh
pwsh -ExecutionPolicy Bypass -File .\scripts\run-ui-tests.ps1 -Configuration Release -NoRestore -AppPath ".\artifacts\ui-test\tray\Aiden.TrayMonitor.exe" -TrxLogFileName "ui-smoke.trx" -ResultsDirectory "artifacts/test-results/ui"
```

## 3.4 脚本测试（Pester）

```pwsh
pwsh -ExecutionPolicy Bypass -File .\scripts\run-script-tests.ps1 -InstallPester
```

输出报告：

- `artifacts/test-results/scripts/pester-scripts.xml`

## 3.5 本地全量建议顺序

```pwsh
dotnet test tests/Aiden.RuntimeAgent.UnitTests/Aiden.RuntimeAgent.UnitTests.csproj -c Release --logger "trx;LogFileName=runtimeagent-unit.trx" --results-directory artifacts/test-results/dotnet
dotnet test tests/Aiden.TrayMonitor.UnitTests/Aiden.TrayMonitor.UnitTests.csproj -c Release --logger "trx;LogFileName=traymonitor-unit.trx" --results-directory artifacts/test-results/dotnet
dotnet test tests/Aiden.IntegrationTests/Aiden.IntegrationTests.csproj -c Release --logger "trx;LogFileName=integration.trx" --results-directory artifacts/test-results/dotnet
pwsh -ExecutionPolicy Bypass -File .\scripts\run-ui-tests.ps1 -PublishApp -Configuration Release -TrxLogFileName "ui-smoke.trx" -ResultsDirectory "artifacts/test-results/ui"
pwsh -ExecutionPolicy Bypass -File .\scripts\run-script-tests.ps1 -InstallPester
```

---

## 4. CI 流程说明

## 4.1 PR Tests (`.github/workflows/tests-pr.yml`)

触发：

- `pull_request`
- `push` 到 `main`

Job：

- `dotnet-tests`
- `scripts-tests`

报告机制：

- `.NET`：`dorny/test-reporter@v2` 解析 `artifacts/test-results/dotnet/**/*.trx`
- `scripts`：上传 `artifacts/test-results/scripts/pester-scripts.xml`，并写入 Workflow Summary

## 4.2 Nightly Tests (`.github/workflows/tests-nightly.yml`)

触发：

- 定时任务：`cron: "0 2 * * *"`
- 手动触发：`workflow_dispatch`

Job：

- `dotnet-tests`
- `ui-tests`
- `scripts-tests`

报告机制：

- `.NET`：`dorny/test-reporter@v2` 解析 `artifacts/test-results/dotnet/**/*.trx`
- `UI`：`dorny/test-reporter@v2` 解析 `artifacts/test-results/ui/**/*.trx`
- `scripts`：上传 `artifacts/test-results/scripts/pester-scripts.xml`，并写入 Workflow Summary

---

## 5. 报告与产物规范

统一目录：

- `artifacts/test-results/dotnet/`
- `artifacts/test-results/ui/`
- `artifacts/test-results/scripts/`

格式说明：

- `TRX`：`dotnet test`（VSTest）输出，适用于 .NET 单元/集成/UI 测试
- `NUnit XML`：Pester 输出，适用于脚本测试

---

## 6. 常见问题（FAQ）

## 6.1 为什么 PR 页面会看到两个 checks？

因为 `tests-pr.yml` 定义了两个 job：

- `dotnet-tests`
- `scripts-tests`

它们会分别显示为两个 check。

## 6.2 为什么 UI 测试里有 1 个 skip？

`Aiden.UI.Tests.TrayUiSmokeTests.LaunchAndAttach_Smoke` 当前被显式标记为 Skip，原因是该用例需要稳定的交互式桌面会话与 UI 自动化标识。

## 6.3 为什么报告文件既有 `.trx` 又有 `.xml`？

因为测试框架不同：

- .NET 测试走 VSTest，输出 TRX
- Pester 脚本测试走 NUnit XML

## 6.4 集成测试偶发 `HttpListener` 相关失败是什么原因？

在受限执行环境（例如部分沙箱/权限隔离环境）中，`HttpListener` 可能无法正常绑定。建议优先在本机正常用户会话或 CI Runner 环境复现与验证。

---

## 7. 维护约定

- 新增测试类型时，必须同步更新本文件的“总览表”和“报告路径”章节
- workflow 中 `--results-directory` 或报告解析路径变更时，必须同步更新本文件
- `run-script-tests.ps1` / `run-ui-tests.ps1` 参数变更时，必须同步更新本文件命令示例
