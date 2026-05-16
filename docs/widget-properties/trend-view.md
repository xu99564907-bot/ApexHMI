# trend-view / WinCC ScreenItem: **OnlineTrendControl**

**来源**：WinCC ProgRef V18，**Table 1-60 Properties**，PDF Page 389 起。VBS Type identifier = `HMIOnlineTrendControl`。

**统计**：WinCC OnlineTrendControl **188 个属性**（去重后约 110 个核心属性 + 80 个 Toolbar/Statusbar/Trend/TimeAxis/ValueAxis 索引方法）；ApexHMI trend-view 现有 **9 个属性**；覆盖率 **4.8%**。

> OnlineTrendControl 是 WinCC 控件中**最复杂**的之一 —— 提供多曲线 + 多时间轴 + 多值轴 + 工具栏 + 状态栏 + 标尺 + 历史/实时双模式 + 完整 OCX 持久化。

---

## 完整属性表（按字母序，仅列出核心字段）

| # | 属性名 | Page | 类别 | ApexHMI 现状 |
|---|---|---|---|---|
| 1 | AllTagTypesAllowed | — | 数据 | ❌ |
| 2 | BackColor | 526 | 外观 | ✅ `backgroundColor` |
| 3 | Base | 1021 | 时间基准 | ❌ |
| 4 | BorderColor | 562 | 边框 | ❌ |
| 5 | BorderWidth | 577 | 边框 | ❌ |
| 6 | Bounds | — | 布局 | ❌ |
| 7 | CanBeGrouped | — | 布局 | ❌ |
| 8 | **Caption** | **587** | 标题 | ⚠️ ApexHMI 缺 |
| 9 | Closeable | 599 | 弹窗 | ❌ |
| 10 | Color | 1128 | 外观 | ❌ |
| 11 | ControlDesignMode | — | 编辑器 | ❌ |
| 12 | Enabled | — | 状态 | ❌ |
| 13 | **ExportFilename / ExportFormat / ExportDelimiter / ExportFileExtension / ExportShowDialog / ExportSelection / ExportParameters** | 681 | 导出 | ❌ |
| 14 | Font | 705 | 字体 | ❌ |
| 15 | Format | 1176 | 数值格式 | ❌ |
| 16 | **GraphDirection** | **717** | 方向 | ❌ |
| 17 | Grid | 1140 | 网格 | ✅ `showGrid` |
| 18 | GridColor | 1126 | 网格 | ❌ |
| 19 | Height | 720 | 布局 | ✅ (layout) |
| 20 | Left | 770 | 布局 | ✅ (layout) |
| 21 | LineColor | 786 | 曲线 | ⚠️ 通过 traces JSON |
| 22 | LineWidth | 790 | 曲线 | ⚠️ 通过 traces JSON |
| 23 | **LoadDataImmediately** | **792** | 行为 | ❌ |
| 24 | Moveable | 825 | 弹窗 | ❌ |
| 25 | OcxGuid / OcxState / OcxStateForEs2Rt | — | OCX 持久化 | — |
| 26 | PercentageAxis / PercentageAxisColor | — | 百分比轴 | ❌ |
| 27 | **PrintJob** | **873** | 打印 | ❌ |
| 28 | **RTPersistence / RTPersistenceType** | **888** | 运行时持久化 | ❌ |
| 29 | **RulerColor / RulerStyle / RulerWidth / ShowRuler / ShowRulerInAxis** | 1138-1139, 938 | 标尺 | ❌ |
| 30 | ShowScrollbars | — | 滚动条 | ❌ |
| 31 | ShowStatisticRuler | — | 统计标尺 | ❌ |
| 32 | **ShowTitle** | **947** | 标题 | ❌ |
| 33 | ShowTrendIcon | — | 图标 | ❌ |
| 34 | Size | 961 | 布局 | ✅ (layout) |
| 35 | Sizeable | 952 | 弹窗 | ❌ |
| 36 | **Statusbar** 系列（17 个字段） | 973-976 | 状态栏 | ❌ |
| 37 | TabIndex / TabIndexAlpha | — | 焦点 | ❌ |
| 38 | TagName | 1093 | 数据 | ⚠️ traces JSON |
| 39 | Text | 970 | 文本 | ❌ |
| 40 | **TimeAxes**（25 个字段）| 1009-1024 | 时间轴 | ⚠️ `timeWindow` 单值 |
| 41 | **TimeBase** | **1024** | 时间基准 | ❌ |
| 42 | **Toolbar** 系列（22 个字段） | 1070-1081 | 工具栏 | ⚠️ `showToolbar` 布尔 |
| 43 | Top | 1083 | 布局 | ✅ (layout) |
| 44 | **Trend** 系列（30+ 字段）| 1092-1124 | 曲线 | ⚠️ traces JSON |
| 45 | **TrendUpperLimit / TrendLowerLimit / TrendUpperLimitColor / TrendLowerLimitColor** | 1121 | 限值 | ❌ |
| 46 | **TrendUncertainColor / TrendUncertainColoring** | 1120 | 坏值上色 | ❌ |
| 47 | TrendFill / TrendFillColor | 1098-1099 | 填充 | ❌ |
| 48 | TrendValueUnit | 1123 | 单位 | ❌ |
| 49 | Value | 1093 | 数据 | — |
| 50 | **ValueAxes**（13 个字段）| 1178-1182 | 值轴 | ⚠️ `yMin/yMax` 单一 |
| 51 | ValueAxisAutoRange | — | 自动量程 | ❌ |
| 52 | ValueAxisScalingType | 1181 | 线性/对数 | ❌ |
| 53 | ValueAxisPrecisions | 1179 | 精度 | ❌ |
| 54 | Visible | 1204 | 状态 | ❌ |
| 55 | Width | 1217 | 布局 | ✅ (layout) |

**子系列展开**（每个都是独立的属性集合）：
- **Toolbar**：ToolbarAlignment/BackColor/UseBackColor/Visible/ButtonActive/ButtonAdd/ButtonClick/ButtonCount/ButtonEnabled/ButtonHotKey/ButtonID/ButtonIndex/ButtonLocked/ButtonName/ButtonRemove/ButtonRename/ButtonRepos/ButtonVisible/Buttons/UseHotKeys/ShowTooltips
- **Statusbar**：StatusbarBackColor/ElementAdd/ElementCount/ElementID/ElementIconId/ElementIndex/ElementName/ElementRepos/ElementText/ElementWidth/Elements/Font/FontColor/ShowTooltips/Text/UseBackColor/Visible
- **TimeAxes**：TimeAxisAdd/Alignment/BeginTime/Color/Count/DateFormat/EndTime/InTrendColor/Index/Label/MeasurePoints/Name/Online/RangeType/Remove/Rename/Repos/ShowDate/TimeFormat/TrendWindow/Visible
- **ValueAxes**：ValueAxisAdd/Alignment/AutoRange/BeginValue/Color/Count/EndValue/InTrendColor/Index/Label/Name/Precisions/Remove/Rename/Repos/ScalingType/TrendWindow/Visible
- **Trend**：TrendAdd/AutoRangeSource/Color/Count/ExtendedColorSet/Fill/FillColor/Index/Label/LineStyle/LineType/LineWidth/LowerLimit/LowerLimitColor/Name/PointColor/PointStyle/PointWidth/Provider/ProviderCLSID/Remove/Rename/Repos/SelectTagName/TagName/TimeAxis/TrendWindow/UncertainColor/UncertainColoring/UpperLimit/UpperLimitColor/ValueAxis/ValueUnit/Visible/Trends
- **TrendWindows**：TrendWindowAdd/Count/FineGrid/Index/Name/Remove/Rename/Repos/Visible/TrendWindows

---

## 关键字段详细说明

### Caption (Page 587) / ShowTitle (Page 947) — ❌ 标题缺失

控件标题文本及显示开关。ApexHMI 当前 trend-view 完全没有标题。

### GraphDirection (Page 717) — ❌

曲线扫描方向：从右→左（新数据在右）或从左→右。**ApexHMI 当前默认左→右且不可改**。

### LoadDataImmediately (Page 792) — ❌

打开屏幕时立即加载历史数据 vs 等用户切换时间窗口。**性能关键**。

### RTPersistence / RTPersistenceType (Page 888) — ❌

运行时是否记住操作员对控件做的调整（如缩放、隐藏曲线）。**操作员体验关键**。

### TimeAxis 系列（25 字段）— ⚠️ ApexHMI 仅 `timeWindow` 数值

WinCC 支持**多时间轴**（不同曲线绑不同时间基准），每个时间轴独立 BeginTime/EndTime/Alignment/Online/DateFormat。**ApexHMI 单一 `timeWindow` 远不够**。

### ValueAxis 系列 — ⚠️ ApexHMI 仅 `yMin/yMax`

WinCC 支持**多值轴**（不同曲线绑不同量纲，左 Y 轴 0-100°C，右 Y 轴 0-10bar）。每个值轴独立 BeginValue/EndValue/AutoRange/ScalingType（线性/对数）/Precisions。**ApexHMI 当前只能单一线性轴**。

### TrendUpperLimit / TrendLowerLimit + Color (Page 1121) — ❌ 越界变色

曲线越限时该段以指定色显示。**生产监控强需求**。

### TrendUncertainColor / TrendUncertainColoring (Page 1120) — ❌ 坏值上色

变量品质坏值时曲线段以特殊色显示。**ApexHMI 当前无品质感知**。

### Toolbar 系列 — ⚠️ ApexHMI 仅 `showToolbar` 布尔

WinCC 工具栏可逐按钮自定义（21 字段）：放大/缩小/导出/打印/暂停/恢复…可启用、隐藏、改快捷键。**ApexHMI 当前工具栏内容固定**。

### Statusbar 系列 — ❌ 状态栏完全缺失

WinCC 底部状态栏显示当前光标位置数值、统计、操作提示。

### ExportFilename / ExportFormat / ExportShowDialog / PrintJob — ❌ 导出/打印

WinCC 可导出 CSV、打印当前曲线。**ApexHMI 完全无导出/打印能力**。

### Ruler 系列（5 字段）— ❌ 标尺

光标处的纵向标尺线，显示穿过曲线的数值。**分析必备**。

---

## 总结：trend-view 缺失分级

### 🔴 严重缺失（生产分析必须）
1. **多 ValueAxis**（不同曲线绑不同量纲）
2. **多 TimeAxis**（不同时间基准）
3. **TrendUpperLimit/LowerLimit + Color**（越界视觉反馈）
4. **TrendUncertainColor**（坏值上色）
5. **Caption / ShowTitle**（标题）
6. **Ruler 系列**（光标标尺——分析必需）
7. **Export 系列**（导出 CSV）
8. **PrintJob**（打印）
9. **GraphDirection**（扫描方向可选）

### 🟡 中度
10. **RTPersistence**（操作员调整持久化）
11. **LoadDataImmediately**（数据加载策略）
12. **Toolbar 自定义**（按钮可配）
13. **Statusbar**（状态栏）
14. **PercentageAxis**（百分比第二轴）
15. **AutoRangeSource / ValueAxisAutoRange**（自动量程）
16. **ScalingType**（线性/对数）
17. **TrendLineStyle / TrendLineWidth / TrendPointStyle/Color/Width**（曲线样式）
18. **TrendFill / TrendFillColor**（面积图）
19. **TrendValueUnit**（单位显示）

### 🟢 装饰/导出
20. Closeable / Moveable / Sizeable（控件本身弹窗化）
21. PrintJob 完整参数
22. ShowTrendIcon

## 与 ApexHMI 现有 `BuildTrendView()` 对照（line 669-687）

| ApexHMI | WinCC | 评估 |
|---|---|---|
| traces (JSON: tag/color/label) | Trend 系列 30+ 字段 | ⚠️ 大幅简化 |
| mode (realtime/history) | TimeAxisRangeType + LoadDataImmediately | ⚠️ 二态简化 |
| timeWindow | TimeAxis 系列 | ⚠️ 单一值 |
| yMin / yMax | ValueAxis BeginValue/EndValue | ⚠️ 单一值轴 |
| showLegend | — | ⚠️ ApexHMI 独有概念 |
| showGrid | Grid (p1140) | ✅ |
| showToolbar | Toolbar Visible | ⚠️ 二态简化 |
| backgroundColor | BackColor (p526) | ✅ |

**结论**：trend-view 与 WinCC 差距最大。当前 ApexHMI 设计能完成"实时单曲线展示"基础场景，但缺乏**工业级分析能力**：多轴、多时间基准、限值上色、标尺、导出、打印、持久化。建议作为 widget 深度补齐的**最高优先级长期项目**。
