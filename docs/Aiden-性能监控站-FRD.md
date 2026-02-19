# 业务需求文档 (FRD) - Project Aiden (Phase 1.3)

## 项目信息
- **项目名称**：Aiden 性能与能耗监控站 (本地集成版)
- **当前版本**：v0.2.0 (同步自 package.json)
- **状态**：已实现核心闭环 (Core Logic Implemented)
- **最后更新**：2026年2月19日

---

## 1. 项目目标 (Project Objectives)

### 1.1 全栈透明化 (Full-Stack Transparency)
实时监控 AI 的 Token 消耗、成本计算及性能表现，通过视觉反馈确保系统状态一目了然。

### 1.2 高性能查询 (High-Performance Monitoring)
通过并发请求（Parallel Fetching）和高效聚合算法（MetricsQL），实现毫秒级的数据刷新感，无操作迟滞。

### 1.3 极简交互 (Minimalist Interaction)
统一所有卡片的视觉语言，通过平滑的数字动画和高亮呼吸灯，让用户直观感受到“数据在流动”。

---

## 2. 核心功能需求 (Functional Requirements)

### 2.1 监控面板功能 (Dashboard Features)

#### A. 实时指标卡片 (Metric Cards)
- **Token 统计**：支持 Input/Output Token 的实时累计显示。
- **成本估算**：基于不同模型的 Token 权重（$0.075/M Input, $0.30/M Output）实时计算 Session 成本。
- **Context Window**：动态显示上下文占用，单位自动适配为 `M` (Millions)。
- **动态走马灯**：所有数值变动时采用 `requestAnimationFrame` 实现平滑滚动效果。

#### B. 元数据同步 (Metadata Sync)
- **项目信息**：标题与版本号直接驱动自 `package.json`，确保工程一致性。
- **激活统计**：基于 `tfirst_over_time` 高效定位首条遥测数据时间，动态计算服务激活天数 (Active D)。

### 2.2 交互与视觉规范 (Interaction & UI)

#### A. 高亮闪烁 (Border Flash)
- 当数据发生变动时，卡片边框需触发高强度青色闪烁（呼吸灯效果）。
- 包含 Outer Glow（外部发光）、Inset Glow（内部发光）及背景色脉冲，持续时长约 1s。

#### B. 手动刷新 (Manual Refresh)
- 右上角提供全局刷新按钮。
- 点击时图标执行旋转动画，并触发全量数据刷新。

#### C. 系统状态判定
- **Online**: VictoriaMetrics 查询可用且数据上报链路正常。
- **Offline**: 任一必需链路失败即切换为“System Offline”告警状态。

### 2.3 首次使用引导 (Onboarding Experience)

针对新用户提供全自动的引导流程，确保直连监控链路正确启动且数据接入配置完成。

#### A. 激活逻辑
- **首次检测**: 使用本地状态标记 (`aiden_onboarded`) 判定。若不存在，则在进入主界面前弹出全屏遮罩引导。
- **持久化**: 完成引导后记录状态，后续访问不再干扰。

#### B. 引导步骤 (Workflow)
1. **欢迎界面 (Welcome)**: 介绍 Aiden 的核心价值（隐私优先、全链路观测）。
2. **环境自检 (Health Check)**: 
   - 自动检测 VictoriaMetrics OTLP 接收配置是否可达。
   - **协议要求**: `otlpProtocol = http`。
   - **端点要求**: `otlpEndpoint = http://127.0.0.1:8428/opentelemetry`（客户端上报时使用 `/v1/metrics`）。
   - **校验结果**: 配置项完整且端点可访问判定为通过，否则标记为未通过。
3. **CLI 检测与自动配置 (CLI Detection & Auto-Config)**:
   - **自动检测**: 扫描系统路径及命令，确认 `gemini` CLI 是否安装。
   - **一键配置（默认）**: 程序自动修改 `~/.gemini/settings.json`，启用 Telemetry 并采用直连 VictoriaMetrics 模式。
   - **默认配置明细**:
     - `telemetry.enabled = true`
     - `telemetry.useCollector = false`
     - `telemetry.otlpProtocol = "http"`
     - `telemetry.otlpEndpoint = "http://127.0.0.1:8428/opentelemetry"`
4. **完成与预览 (Success)**: 确认配置成功并提供“进入仪表盘”的跳转入口。

#### C. 视觉与交互规范
- **沉浸式体验**: 采用半透明暗色背景 (Backdrop Blur) 和玻璃拟态窗口。
- **进度指示**: 顶部显示 5 阶段进度条（新增 CLI 检测阶段）。
- **动态加载**: 在自检步骤中使用 Loading 动画，并在成功时切换为绿色的 Check 图标。

---

## 3. 技术实现标准 (Technical Standards)

### 3.1 查询优化
- 禁止串行 await 查询，所有 Metrics 请求必须包裹在 `Promise.all` 中并发执行。
- 时间戳查询必须使用 VictoriaMetrics 的 `tfirst_over_time` 优化函数，严禁全量扫描。

### 3.2 界面性能
- 动画必须使用高精度计时能力，确保时间精度。
- 采用玻璃拟态 (Glassmorphism) 风格，确保在暗色模式下的视觉通透感。

---

## 4. 技术架构 (Direct-to-VM)

| 组件 | 选型 | 职责 |
|------|----------|----------|
| **指标库** | VictoriaMetrics | MetricsQL 高效时序存储 |
| **查询层** | vmClient | 统一多端查询逻辑 |
| **展示层** | Client UI | 响应式看板 |

---

## 5. 项目里程碑

### Phase 1.3 (已完成)
- [x] 并发查询性能优化
- [x] 数字滚动与边框闪烁效果
- [x] package.json 元数据联动
- [x] Context Window 单位适配 (M)
- [x] 手动刷新功能实现

### Phase 2 (进行中)
- [ ] Thought Stream (基于 Trace 的思维流可视化)
- [ ] 导出本地监控报告 (PDF)
- [ ] 异常流量告警推送
