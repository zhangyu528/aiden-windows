# 自动化测试-ATD

## 1. 文档定位

本文档是自动化测试与 CI 的单一事实源，定义：
- 本地测试入口
- CI 流程与触发条件
- 报告产物路径
- 常见排障方法

## 2. 测试矩阵

| 类型 | 测试项目 | 本地入口 | 报告格式 | 报告路径 | PR | Nightly |
|---|---|---|---|---|---|---|
| .NET 单元 | `Aiden.RuntimeAgent.UnitTests` | `Invoke-TestGate.ps1 -Scope Staged` | TRX | `artifacts/test-results/dotnet/*.trx` | Yes | Yes |
| .NET 单元 | `Aiden.TrayMonitor.UnitTests` | `Invoke-TestGate.ps1 -Scope Staged` | TRX | `artifacts/test-results/dotnet/*.trx` | Yes | Yes |
| .NET 集成 | `Aiden.IntegrationTests` | `Invoke-TestGate.ps1 -Scope PR` | TRX | `artifacts/test-results/dotnet/*.trx` | Yes | Yes |
| UI | `Aiden.UI.Tests` | `Invoke-TestGate.ps1 -Scope Nightly` | TRX | `artifacts/test-results/ui/*.trx` | No | Yes |
| 脚本 | `automation/tests/Workflow` | `Invoke-TestGate.ps1 -Scope Staged` | NUnit XML | `artifacts/test-results/scripts/pester-workflow.xml` | Yes | Yes |

## 3. 本地执行

前置条件：
- Windows 10/11
- .NET 8 SDK
- PowerShell 7 (`pwsh`)

### 3.1 .NET 单元与集成

```pwsh
dotnet test tests/Aiden.RuntimeAgent.UnitTests/Aiden.RuntimeAgent.UnitTests.csproj -c Release --logger "trx;LogFileName=runtimeagent-unit.trx" --results-directory artifacts/test-results/dotnet
dotnet test tests/Aiden.TrayMonitor.UnitTests/Aiden.TrayMonitor.UnitTests.csproj -c Release --logger "trx;LogFileName=traymonitor-unit.trx" --results-directory artifacts/test-results/dotnet
dotnet test tests/Aiden.IntegrationTests/Aiden.IntegrationTests.csproj -c Release --logger "trx;LogFileName=integration.trx" --results-directory artifacts/test-results/dotnet
```

### 3.2 UI 测试

```pwsh
pwsh -ExecutionPolicy Bypass -File .\automation\tests\Invoke-AidenTests.ps1 -Type UI -PublishApp -Configuration Release -TrxLogFileName "ui-smoke.trx" -ResultsDirectory "artifacts/test-results/ui"
```

### 3.3 脚本测试（Pester）

```pwsh
pwsh -ExecutionPolicy Bypass -File .\automation\tests\Invoke-AidenTests.ps1 -Type Scripts -InstallPester
```

## 4. CI 流程

### 4.1 Build（手动编译校验）

Workflow：`.github/workflows/build.yml`

- 触发：`workflow_dispatch`
- 动作：`dotnet restore Aiden.sln` + `dotnet build Aiden.sln -c Release --no-restore`
- 目标：快速确认解决方案可编译
- 不上传构建产物

### 4.2 PR Tests

Workflow：`.github/workflows/tests-pr.yml`

- 触发：`pull_request`、`push main`
- Job：`dotnet-tests`、`scripts-tests`
- 报告：
  - .NET 通过 `dorny/test-reporter@v2` 解析 TRX
  - scripts 上传 XML 并写入 Summary

### 4.3 Nightly Tests

Workflow：`.github/workflows/tests-nightly.yml`

- 触发：定时 + 手动
- Job：`dotnet-tests`、`ui-tests`、`scripts-tests`
- 报告：
  - .NET/UI 通过 `dorny/test-reporter@v2`
  - scripts 上传 XML 并写入 Summary

### 4.4 Feishu Notification

Workflow：`.github/workflows/feishu-notification.yml`

- 触发：`push main`
- 卡片主体：main 最新提交的 commit message
- 依赖：`FEISHU_WEBHOOK`

## 5. 报告产物规范

目录统一：

```text
artifacts/
  test-results/
    dotnet/
      *.trx
    ui/
      *.trx
    scripts/
      pester-scripts.xml
```

## 6. 常见问题与排障

### 6.1 PR 上看到两个 checks

这是正常行为，`tests-pr.yml` 定义了两个 job：`dotnet-tests` 与 `scripts-tests`。

### 6.2 UI 用例有 skip

`Aiden.UI.Tests` 中部分 smoke 用例依赖交互式桌面，默认可能跳过。

### 6.3 为什么同时有 TRX 和 XML

- .NET 测试输出 TRX
- Pester 输出 NUnit XML

### 6.4 Feishu workflow 报脚本找不到

确保 workflow 包含 `actions/checkout@v4`，否则 runner 中没有仓库文件。

## 7. 维护约定

- workflow 或脚本路径变更时，必须同步更新本文件。
- 报告目录规范固定为 `artifacts/test-results/*`。
- README 仅保留摘要，细节统一以本文件为准。
