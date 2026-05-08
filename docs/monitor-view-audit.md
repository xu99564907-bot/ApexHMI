# MonitorView 审计表（第 1 批）

> 用法：通览全表，**只在"预期"列写差异**——对的留空、不对的写一两句。完事一次性发回给我，我批量改。

监控页有 4 个子页（按可见性切换显示）：
- **生产数据**（`IsMonitorProductionPageVisible`）
- **IO 监控**（`IsMonitorIoPageVisible`）
- **OPC UA 通讯调试**（`IsMonitorCommunicationPageVisible`）
- **程序监控 / Trace**（`IsMonitorProgramPageVisible`）

---

## 子页 1：生产数据

### 1.1 顶部筛选条（[MonitorView.xaml:11-24](Views/Pages/MonitorView.xaml:11)）

| # | 控件 | 当前绑定 | 当前行为 | 你的预期 |
|---|---|---|---|---|
| 1 | ComboBox "监视分类" | `MonitorCategoryOptions` / `SelectedMonitorCategory` | 切换分类时 `RefreshMonitorView()` | |
| 2 | ComboBox "流程" | `FlowFilterOptions` / `SelectedFlowFilter` | 切换流程过滤 | |
| 3 | ComboBox "步号" | `FlowStepFilterOptions` / `SelectedFlowStepFilter` | 切换步号过滤 | |
| 4 | ComboBox "时间" | `FlowTimeRangeOptions` / `SelectedFlowTimeRange` | 切换时间范围 | |
| 5 | CheckBox "仅异常" | `ShowOnlyAbnormalFlow` | 只显示异常流程 | |
| 6 | Button **"刷新视图"** | `RefreshTagsCommand` | 调 `RefreshTagsAsync` → 全表读 PLC | |
| 7 | Button **"加载历史趋势"** | `LoadTrendHistoryCommand` | 读 `config/trend-history.csv` 加载到内存 | |
| 8 | Button **"导入流程CSV分析"** | `ImportFlowCsvCommand` | 弹文件选择对话框，导入 CSV 解析流程异常 | |

### 1.2 KPI 卡片（[xaml:25-30](Views/Pages/MonitorView.xaml:25)）

| # | 卡片 | 当前显示绑定 | 当前数据来源 | 你的预期 |
|---|---|---|---|---|
| 9 | "工单号" | `CurrentOrderText` | 读 tag `WorkOrder_No`，**找不到时回退假数据 "WO-20260404-01"** | |
| 10 | "配方" | `CurrentRecipeText` | 读 tag `Recipe_Name`，**找不到时回退假数据 "产品A"**；`SelectedRecipeName` 优先 | |
| 11 | "班次状态" | `ShiftStatusText` | 计算：`ShiftProductionCount >= TargetCount` ? "班次达成" : "班次生产中" | |
| 12 | "目标产量" | `TargetCount` | 读 tag `Production_TargetCount`，**找不到时回退假数据 1500** | |

### 1.3 KPI 详情卡（[xaml:35-36](Views/Pages/MonitorView.xaml:35)） ⚠ 这块有 **WPF 绑定语法 bug**

XAML 这样写：
```xml
<TextBlock Text="班次：{Binding ShiftProductionCount}" .../>
```
WPF **不会**把它当作绑定，而是直接显示成文本字面量"班次：{Binding ShiftProductionCount}"。正确写法：`Text="{Binding ShiftProductionCount, StringFormat=班次：{0}}"`。

| # | 显示内容 | 当前绑定 | 当前数据来源 | bug | 你的预期 |
|---|---|---|---|---|---|
| 13 | 班次产量 | `班次：{Binding ShiftProductionCount}` | tag `Shift_ProductionCount`，回退 460 | ⚠ 字面量 bug | |
| 14 | 良品 | `良品：{Binding GoodCount}` | tag `Production_GoodCount`，回退 1246 | ⚠ 字面量 bug | |
| 15 | 不良 | `不良：{Binding NgCount}` | tag `Production_NgCount`，回退 34 | ⚠ 字面量 bug | |
| 16 | UPH | `UPH：{Binding Tags[26].CurrentValue}` | **索引硬编码** Tags 列表第 26 项 | ⚠ 字面量 bug + 硬编码下标 | |
| 17 | 节拍 | `节拍：{Binding Tags[25].CurrentValue}s` | 同上 Tags[25] | ⚠ 字面量 bug + 硬编码下标 | |
| 18 | 运行时间 | `Tags[29].CurrentValue, StringFormat=运行时间：{0} min` | Tags[29]（StringFormat 这条是对的） | 仅硬编码下标 | |
| 19 | 停机时间 | `Tags[30].CurrentValue, StringFormat=停机时间：{0} min` | Tags[30] | 仅硬编码下标 | |
| 20 | Availability | `AvailabilityRate, StringFormat=Availability：{0:F1}%` | `CalculateAvailability()`（计算函数，需查它读啥 tag） | | |
| 21 | Performance | `PerformanceRate` | `CalculatePerformance()` | | |
| 22 | Quality | `QualityRate` | `CalculateQuality()` | | |

### 1.4 趋势小图（[xaml:37-38](Views/Pages/MonitorView.xaml:37)）

| # | 图 | 当前绑定 | 数据来源 | 你的预期 |
|---|---|---|---|---|
| 23 | 产量趋势 sparkline | `ProductionTrendPath` | 用 `ShiftProductionCount * [0.35, 0.5, 0.68, 0.8, 0.92, 1]` 模拟，**不是真历史** | |
| 24 | OEE/报警/流程 sparkline | `OeeTrendPath` / `AlarmTrendPath` / `FlowStepTrendPath` / `FlowIssueTrendPath` | 同样基于当前值算的合成数据 | |
| 25 | 摘要文本 | `ProductionTrendSummary` / `OeeTrendSummary` / `AlarmTrendSummary` / `FlowRankingSummary` | 字符串拼接当前 KPI | |

---

## 子页 2：IO 监控

### 2.1 切换按钮（[xaml:53-66](Views/Pages/MonitorView.xaml:53)）

| # | 按钮 | 行为 | 你的预期 |
|---|---|---|---|
| 26 | **"输入监控"** | `SwitchIoMonitorTypeCommand("DI")` → `IoMonitorType = "DI"` | |
| 27 | **"输出监控"** | `SwitchIoMonitorTypeCommand("DO")` → `IoMonitorType = "DO"` | |

### 2.2 IO 表格

| # | 内容 | 绑定 | 你的预期 |
|---|---|---|---|
| 28 | 标题 | `IoMonitorTitle` | |
| 29 | 左半表格 | `IoMonitorLeftItems`（每行：状态 Ellipse / 地址 / 注释） | |
| 30 | 右半表格 | `IoMonitorRightItems` | |
| 31 | Button **▲ 上一页** | `IoMonitorPageUpCommand` → `IoMonitorCurrentPage--` | |
| 32 | Button **▼ 下一页** | `IoMonitorPageDownCommand` → `IoMonitorCurrentPage++` | |

---

## 子页 3：OPC UA 通讯调试

### 3.1 工具栏按钮（[xaml:159-161](Views/Pages/MonitorView.xaml:159)）

| # | 按钮 | 行为 | 你的预期 |
|---|---|---|---|
| 33 | **"浏览根节点"** | `LoadOpcUaBrowserRootCommand` → 拉 OPC UA 服务器 ObjectsFolder 树 | |
| 34 | **"读取选中节点"** | `RefreshSelectedOpcUaNodeCommand` → 读 SelectedOpcUaBrowseNode 的当前值 | |
| 35 | **"加入变量表"** | `AddSelectedOpcUaNodeAsTagCommand` → 把选中节点添加到 Tags 集合 | |

### 3.2 节点详情面板（[xaml:190-208](Views/Pages/MonitorView.xaml:190)） ⚠ 配色 bug

详情面板背景是 `#FFFFFF`（白），但 6 处 `Text` 用 `Foreground="White"`（白字白底，看不到）：
- `节点详情` 标题（行 192）
- `显示名` 值、`节点类型` 值、`数据类型` 值、`状态` 值、`时间戳` 值

| # | 字段 | 绑定 | bug | 你的预期 |
|---|---|---|---|---|
| 36 | 显示名 | `SelectedOpcUaBrowseNode.DisplayName` | ⚠ 白底白字 | |
| 37 | NodeId | `SelectedOpcUaBrowseNode.NodeId`（字色 `#BFDBFE` 浅蓝，可见） | | |
| 38 | 节点类型 | `SelectedOpcUaBrowseNode.NodeClass` | ⚠ 白底白字 | |
| 39 | 数据类型 | `SelectedOpcUaBrowseNode.DataType` | ⚠ 白底白字 | |
| 40 | 当前值 | `SelectedOpcUaNodeValue`（字色 `#86EFAC` 浅绿，可见） | | |
| 41 | 状态 | `SelectedOpcUaNodeStatus` | ⚠ 白底白字 | |
| 42 | 时间戳 | `SelectedOpcUaNodeTimestamp` | ⚠ 白底白字 | |

---

## 子页 4：程序监控 / Trace

### 4.1 主流程步号 Trace 区控制（[xaml:311-319](Views/Pages/MonitorView.xaml:311)）

| # | 控件 | 绑定 | 行为 | 你的预期 |
|---|---|---|---|---|
| 43 | CheckBox "主线1" | `ProgramMonitorTraceShowLine1` | 切显主流程步号曲线 | |
| 44 | CheckBox "主线2" | `ProgramMonitorTraceShowLine2` | 切显子流程 2 | |
| 45 | CheckBox "主线3" | `ProgramMonitorTraceShowLine3` | 切显子流程 3 | |
| 46 | ComboBox "光标跟随" | `ProgramMonitorTraceFlowOptions` / `SelectedProgramMonitorTraceFlow` | 选光标跟随哪条流程 | |
| 47 | Slider "缩放" | `ProgramMonitorTraceWindowMinutes`（1-30 分钟） | 调时间窗口 | |

### 4.2 工具栏按钮（[xaml:328-339](Views/Pages/MonitorView.xaml:328)）

| # | 按钮 | 行为 | 你的预期 |
|---|---|---|---|
| 48 | **"暂停采样"** | `PauseProgramMonitorTraceCommand` → `ProgramMonitorTracePaused = true` | |
| 49 | **"继续采样"** | `ResumeProgramMonitorTraceCommand` → `Paused = false`, `ReplayMode = false` | |
| 50 | **"历史回放"** | `EnterProgramMonitorTraceReplayCommand` → 进入回放模式（无历史时弹提示） | |
| 51 | **"回到实时"** | `ReturnProgramMonitorTraceToRealtimeCommand` → 等价于"继续采样" | |
| 52 | **"导出CSV"** | `ExportProgramMonitorTraceCsvCommand` → 弹保存对话框，导出 trace 数据为 CSV | |
| 53 | **"保存会话"** | `SaveProgramMonitorTraceSessionCommand` → 保存当前 trace 会话快照 | |
| 54 | **"加载会话"** | `LoadProgramMonitorTraceSessionCommand` → 加载 trace 会话快照 | |
| 55 | TextBox "定位时间" | `ProgramMonitorTraceLocateTime`（如 `12:34:56`） | | |
| 56 | **"定位"** | `LocateProgramMonitorTraceTimeCommand` → 跳到 LocateTime 对应的 trace 位置 | |
| 57 | Slider "回放" | `ProgramMonitorTraceReplayPosition` (0..ReplayMaximum) | 回放进度 | |

### 4.3 流程异常表格 + 跳转按钮（[xaml:345-378](Views/Pages/MonitorView.xaml:345)）

| # | 控件 | 绑定 | 行为 | 你的预期 |
|---|---|---|---|---|
| 58 | DataGrid "流程步" | `FlowStepsView`（列：流程/步号/开始/结束/耗时/结果/关联报警） | | |
| 59 | 行内 Button **"看报警"** | `JumpToAlarmByKeywordCommand(RelatedAlarm)` → 跳报警页 + 高亮关键字 | | |
| 60 | Button **"跳转报警页"** | `JumpToAlarmPageCommand` → Navigate("报警画面") | | |
| 61 | Button **"跳转审计页"** | `JumpToAuditPageCommand` → Navigate("操作审计") | | |
| 62 | Button **"导出异常报告"** | `ExportFlowIssueReportCommand` | | |
| 63 | DataGrid "流程异常汇总" | `FlowIssueSummaries`（分类/对象/指标/结论/联动） | | |

---

## 我已经看到的 bug（你确认要不要顺手修）

- **A**：1.3 节 KPI 详情卡里 5 个 `Text="班次：{Binding X}"` 字面量 bug（行 35）→ 这 5 行根本不会显示动态数据，永远是字面量字符串。
- **B**：1.3 节 7 个 `Tags[N].CurrentValue` 索引硬编码（行 35-36）→ 只要 tags.json 顺序变，UPH/节拍/运行时间/停机时间立刻错。
- **C**：1.2 / 1.3 节大量 fallback 假数据（如 `WO-20260404-01`、`产品A`、1500/1246/460 等数字）→ tag 缺失时会显示这些假值，误导。
- **D**：3.2 节 OPC UA 节点详情面板 6 处白底白字（行 192-206）。

---

## 待你回答的关键问题（影响下一步改法）

1. **生产 KPI 数据源**：当前用了 `Production_GoodCount` / `Shift_ProductionCount` / `Production_TargetCount` / `WorkOrder_No` / `Recipe_Name` 这套 tag 名，但你的 PLC 上**没这些 tag**（PLC 用的是 `Application.DB8003_Count.OK.Total` 这种结构）。问：
   - **a**) 这些 KPI 是要从 `Application.DB8003_Count.*`（OK / NG / Total / Yield）取吗？还是你打算另起 `DB8090_Other` 之类的位置？
   - **b**) "工单号"和"配方"两个文本是不是你不打算从 PLC 取，而是从应用内部取（比如 `SelectedRecipeName`）？如果是，那 fallback 的假数据要不要去掉、改成显示空值或"未选择"？

2. **Tags[N] 索引硬编码**：UPH / 节拍 / 运行/停机时间这 4 个，你预期对应到 PLC 上的哪个变量？还是这本来就是 demo 阶段塞的占位、你打算彻底删掉？

3. **趋势小图（1.4 节 23-24）**：现在画的是基于当前值算出来的"假趋势"，不是真历史。你期望它读哪个真历史源？`trend-history.csv`？或者 `Application.DB8003_Count.*` 里的某些字段？

只要回答这 3 个问题 + 在表里"预期"列填差异，我就能批量改。其他 60 行不动也行。
