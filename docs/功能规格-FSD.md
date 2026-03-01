# Aiden 本地集成版 FSD（功能规格）

## 1. 文档目的
定义 Aiden 本地集成版在用户侧可见的功能行为、边界和验收口径，面向产品、测试与交付。

## 2. 产品目标与范围
### 2.1 目标
- 在 Windows 提供低打扰托盘监控体验。
- 展示已开通 CLI（Gemini/Codex/Claude Code）核心 telemetry 数据。
- 关闭 UI 后保持后台采集连续性。

### 2.2 In Scope
- 托盘主面板核心指标展示。
- 主面板分页切换（Gemini/Codex/Claude）。
- 首次引导页与 Settings 页的 CLI 开通管理。
- 启动中/启动失败状态页。
- 自动刷新与手动刷新。

### 2.3 Out of Scope
- 趋势分析与长期报表。
- 终端日志展示。
- Web 控制台。
- 无缝热升级。

## 3. 功能规格
### 3.1 主面板
- 展示字段：
  - Input Tokens
  - Output Tokens
  - Current User Email
  - User Active
  - Cost USD
  - Context（值 + %）
  - Status（ONLINE/OFFLINE）
- 支持手动刷新。
- 自动刷新按配置周期执行。

### 3.2 主面板分页（Gemini / Codex / Claude）
- 页面提供三个分页入口。
- 分页可用条件：对应 CLI 必须满足“已安装且已开通”。
- 不可用分页表现为禁用，不允许切换。
- 切换分页后，主面板数据必须切换到对应 CLI 数据源。
- 若当前分页在运行中变为不可用（例如在 Settings 关闭开关），主面板自动回退到可用分页。

### 3.3 字段显示规则
- Current User Email：
  - 有可识别用户时显示邮箱。
  - 无可识别用户时显示 `Unknown`。
- Input/Output：
  - 当前用户已识别时显示数值。
  - 当前用户未知时显示 `N/A`。
- User Active：
  - 显示 `X days`。
  - 当前用户未知时显示 `N/A`。
- Context：
  - 有效时显示上下文值与百分比。
  - 用户未知、会话未知或模型能力缺失时显示 `N/A`。
- Cost：
  - 有数据时显示 USD 成本。
  - 无数据时显示 `0`（或 UI 约定占位）。
- Status：
  - 查询链路可用显示 `ONLINE`。
  - 查询链路不可用显示 `OFFLINE`。

### 3.4 首次引导
- 当 `OnboardingCompleted=false` 且未全部开通时显示引导页。
- 引导页展示三类 CLI 的安装状态与开关：
  - 已安装项可开关。
  - 未安装项仅提示安装命令，不可开关。
- 首次引导必须至少开通一个 CLI 才可 Continue。
- Continue 后进入启动流程；关闭引导页则退出应用。

### 3.5 Settings（CLI 管理）
- 可随时打开 Settings 管理三类 CLI 开关。
- 已安装项开关即时生效。
- 未安装项仅展示安装提示。
- 关闭 Settings 返回主面板后，分页可用状态应立即与最新配置对齐。

### 3.6 启动状态页
- 首次启动流程可展示：
  - Startup Loading（启动中）
  - Startup Error（启动失败）
- Startup Error 支持重试与退出。

### 3.7 后台连续采集
- 用户在主面板点击 Exit 时，仅关闭 Tray UI。
- RuntimeAgent 持续运行，数据采集不中断。

## 4. 非功能要求
- 可用性：后台异常应在 UI 体现为 OFFLINE。
- 时效性：刷新后更新时间需及时更新。
- 可理解性：Unknown/N/A/禁用态行为必须一致且可预期。

## 5. 验收标准
1. 主面板可展示完整核心字段。
2. 关闭 UI 后重新打开，期间数据连续。
3. 分页仅可切换到“已安装且已开通”CLI。
4. 分页切换后数据源正确切换。
5. Settings 变更后主面板分页可用状态及时更新。
6. 启动阶段可正确显示 Loading/Error 并支持重试。
7. 手动刷新与自动刷新均生效，更新时间更新。
8. 异常场景下状态正确显示为 OFFLINE。

## 6. 关联文档
- 技术实现细节见：`docs/技术设计-TDD.md`
- 需求总览见：`docs/产品需求-FRD.md`

## 7. 指标口径补充（实现对齐）
- Input/Output 口径保持不变：按 `gen_ai.client.token.usage_sum` + `gen_ai.token.type` 查询。
- Context 口径为当前活跃 session 的 `input` token 使用量，不再使用所有 token type 总和。
- Codex 页签数据来自 collector 日志转换：
  `response.completed` 日志 -> `gen_ai.client.token.usage_sum`（input/output）。
- Codex 转换指标导出 VM 前增加 `deltatocumulative` 与 `metricstarttime` 处理，
  以保证 VictoriaMetrics 可查询性。
- Codex 转换指标查询可见性存在延迟，通常约 20-30 秒。

