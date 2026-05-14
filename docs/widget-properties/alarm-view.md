# alarm-view / WinCC ScreenItems: **AlarmControl + AlarmView**

**来源**：WinCC ProgRef V18，**Table 1-1 Properties**（AlarmControl，RT Pro 高级版）和 **Table 1-3 Properties**（AlarmView，Panel/Comfort/RT Adv 简化版）。AlarmControl 在 PDF Page 247，AlarmView 在 Page 252。

**注**：ApexHMI 当前 `alarm-view` 是统一抽象，对应 WinCC 的两个 ScreenItem。

**统计**：
- **AlarmControl**：226 个属性（含 Toolbar/Statusbar/Message Block/Source/Type 1~10 等大量子系列）
- **AlarmView**：66 个属性（简化版，主要为 RT Adv/Panel 设计）
- ApexHMI alarm-view：**5 个属性**
- 覆盖率：**1.7%**（最低，差距最大）

---

## AlarmControl 完整属性表（226 个，分类汇总）

### 数据源 / 过滤
- **Source1 ~ Source10** (Page 844-850) — 10 个告警源（多 PLC / 多服务器汇总）
- **Type1 ~ Type10** (Page 851-857) — 每个源的类型（Alarm/Event/Warning/Info）
- **MsgFilterSQL** (Page 826) — SQL 过滤
- DefaultMsgFilterSQL / DefaultFilterEom
- Filter / AssignedFilters / AssignedHitlistFilters
- **MessageListType** (Page 820) — Active/Suppressed/Acknowledged/Hitlist
- **AllServer** (Page 510) / ServerNames (p923) — 分布式服务器

### 显示行 / 列 / 表头
- **ColumnTitles** (Page 628) / TitleColor (p1045) / TitleForeColor (p1047) / TitleStyle (p1051) / TitleSort (p1050) / TitleCut (p1046)
- **ColumnResize** (Page 616) / ColumnScrollbar / ColumnTitleAlignment / Columns
- **MessageBlockAdd/Remove/Repos/Visible/Sort/...**（17 字段，Page 805-820）
- **MessageColumnAdd/Visible/Sort/Repos**（Page 818-820）
- **HitlistColumnAdd/Visible/Sort/Repos**（Page 727-732）
- ShowSortButton (p941) / ShowSortIcon (p942) / ShowSortIndex (p942) / DefaultSort (p646) / SortSequence (p954)
- **AutoCompleteColumns / AutoCompleteRows**

### 表格外观（颜色/字体）
- **TableColor** (p988) / TableColor2 (p989) / TableForeColor / TableForeColor2 (p991) / UseTableColor2
- **GridLineColor** (p717) / GridLineWidth (p719) / HorizontalGridLines / VerticalGridLines
- **CellSpaceTop/Bottom/Left/Right** (p591-594)
- LineColor (p786) / LineWidth (p790) / LineStyle / LineBackgroundColor
- Font (p705) / Format (p804) / Icon (p812) / IconSpace (p740)

### 选中行
- **SelectedRowColor** (p909) / SelectedRowForeColor (p909)
- SelectedCellColor (p906) / SelectedCellForeColor (p907)
- SelectionRect (p916) / SelectionRectColor (p917) / SelectionRectWidth (p917) / SelectionType (p918)
- SelectionColoring (p914) / AutoSelectionColors / AutoSelectionRectColor
- SelectedTitleColor (p911) / SelectedTitleForeColor (p912) / UseSelectedTitleColor

### 工具栏 / 状态栏
- **Toolbar*** （22 字段：Add/Remove/Active/Click/HotKey/Locked/Visible/Tooltips/UseBackColor/UseHotKeys/Buttons/...）
- **Statusbar*** （17 字段：ElementAdd/Remove/Repos/Icon/Text/Width/Font/FontColor/Tooltips/Visible/...）

### 行为 / 持久化
- **AutoScroll** (p521)
- **RTPersistence** (p888) / RTPersistenceType (p890)
- **ApplyProjectSettings**
- **DoubleClickAction**
- **PageMode** (p858)
- **PreferredUseOnAck**（确认偏好）

### 导出 / 打印
- **ExportFilename** (p676) / ExportFormat / ExportFileExtension (p675) / ExportDelimiter / ExportFormatGuid (p678) / ExportFormatName (p679) / ExportParameters (p680) / ExportSelection (p681) / ExportShowDialog (p681) / ExportDirectoryname
- **PrintJob** (p873)

### 边框 / 标题 / 弹窗
- BackColor (p526) / Caption (p587) / ShowTitle (p947) / BorderColor / BorderWidth (p577)
- Closeable (p599) / Moveable (p825) / Sizeable (p952)

### 其它
- HitListRelTime / HitListRelTimeFactor / Hitlist / HitlistMaxSourceItems (p733)
- **Number** (p859) / **Zeros** (p808)
- **OperatorAlarms** / OperatorMessageId/Index/Name
- **Warn** (p734)
- DiagnosticsContext / DisplayOptions

---

## AlarmView 完整属性表（66 个，Panel 简化版）

| # | 属性名 | Page | ApexHMI 现状 |
|---|---|---|---|
| 1 | BackColor | 526 | ✅ `background` |
| 2 | Enabled | 660 | ❌ |
| 3 | FilterTag | — | ❌ |
| 4 | FilterText | — | ❌ |
| 5 | FitToSize | — | ❌ |
| 6 | Flashing | — | ❌ |
| 7 | FocusColor | 703 | ❌ |
| 8 | FocusWidth | 704 | ❌ |
| 9 | ForeColor | — | ❌ |
| 10 | GridLineColor | — | ❌ |
| 11 | IsRunningUnderCE | — | ❌（设备能力判断）|
| 12 | LineAlarmView | — | ❌ |
| 13 | MessageAreaHeight / Left / Top / Width | — | ❌ |
| 14 | PaddingTop / Bottom / Left / Right | — | ❌ |
| 15 | PreferredUseOnAck | — | ❌ |
| 16 | S7Device | — | ❌（绑定 PLC 设备）|
| 17 | SelectionBackColor / SelectionForeColor | — | ❌ |
| 18 | ShowAlarmsFromDate | — | ❌ |
| 19 | ShowColumnHeaders | — | ❌ |
| 20 | ShowHelpButton | — | ❌ |
| 21 | ShowMilliseconds | — | ❌ |
| 22 | ShowPendingAlarms | — | ❌ |
| 23 | SortByTimeDirection | — | ❌ |
| 24 | SortByTimeEnabled | — | ❌ |
| 25 | Style | — | ⚠️ `filterCategory` 部分对应 |
| 26 | TableBackColor / TableEvenRowBackColor / TableFont / TableForeColor | — | ❌ |
| 27 | TableHeader* (8 字段) | 993, 995 | ❌ |
| 28 | ToolbarHeight / Left / Top / Width | — | ❌ |
| 29 | UseButtonFirstGradient | — | ❌ |
| 30 | UseDesignColorSchema | — | ❌ |
| 31 | VerticalScrollBarEnabled | — | ❌ |
| 32 | VerticalScrollingEnabled | — | ❌ |
| 33 | ViewType | — | ❌ |

---

## 关键字段详细说明

### Source1 ~ Source10 + Type1 ~ Type10 (Page 844-857) — ❌ 多源汇总

WinCC 支持从最多 10 个 PLC/服务器/数据源汇总告警。ApexHMI 当前**告警源单一固定**（全局 AlarmIndicator）。

### MessageListType (Page 820) — ⚠️ ApexHMI `filterCategory` 部分对应

WinCC 支持：Active（活动）、Suppressed（被抑制）、Acknowledged（已确认）、Hitlist（历史频次）。**ApexHMI 当前 `filterCategory` 仅按 Info/Warning/Error/Alarm 分类**，缺确认/抑制/历史维度。

### MsgFilterSQL (Page 826) — ❌ SQL 过滤

WinCC 允许用 SQL 写复杂过滤（"Severity > 5 AND Source LIKE 'PumpA%'"）。**ApexHMI 当前过滤维度只有 category 单选**。

### MessageBlock 系列（17 字段）— ❌ 消息块自定义

每条告警可由"消息块"组成（时间/源/文本/确认者/...）。WinCC 允许编辑显示哪些块、顺序、字体、颜色。**ApexHMI 当前 `columns` 是 JSON 自由配置**，未对齐到这一抽象。

### Hitlist + HitlistColumn* (Page 727-733) — ❌ 频次/排行

按告警发生频次排行（"今天报警 Top 10"），用于生产分析。**ApexHMI 完全缺**。

### ExportFilename / PrintJob — ❌ 导出/打印

合规审计常需告警记录可导出/打印。

### RTPersistence (Page 888) — ❌ 运行时调整持久化

操作员调整列宽/排序，下次打开仍生效。

### AutoScroll (Page 521) — ❌ 自动滚动

新告警发生时自动滚到顶部/底部。**操作员体验关键**。

### PreferredUseOnAck — ❌ 确认偏好

确认行为可配（鼠标 / 双击 / 按钮）。**ApexHMI 当前 `showAck` 仅是显示开关**，无确认行为可配。

### Toolbar 系列 — ❌ 工具栏（22 字段）

WinCC 告警工具栏可自定义按钮（确认/批量确认/锁定/打印/导出/帮助）。

### TableHeader / SelectedRow 等外观 — ❌

表头、选中行的字体、颜色、背景、边距 — 完全可定制。ApexHMI 仅 `background` 一个全局色。

---

## 总结：alarm-view 缺失分级

### 🔴 严重缺失（生产监控必须）
1. **多 Source / Type**（多 PLC 汇总）
2. **MessageListType 全维度**（Active/Suppressed/Acknowledged/Hitlist）
3. **MsgFilterSQL**（SQL 自定义过滤）
4. **Hitlist**（频次排行）
5. **AutoScroll**（自动滚动）
6. **PreferredUseOnAck**（确认行为可配）
7. **ExportFilename / PrintJob**（导出/打印）
8. **MessageBlock 自定义**（列内容/顺序对齐到 WinCC 抽象）

### 🟡 中度
9. **RTPersistence**（操作员调整持久化）
10. **Toolbar 自定义**（22 字段大类）
11. **Statusbar**（17 字段大类）
12. **ShowMilliseconds / ShowPendingAlarms / ShowColumnHeaders**
13. **SortByTimeDirection / DefaultSort**（排序细节）
14. **Selection 系列**（选中行颜色）
15. **TableHeader 系列**（表头样式）
16. **Padding 系列**（内边距）

### 🟢 高级
17. **Source1~10 / Type1~10**（多源/多类型，组态期较复杂）
18. **ApplyProjectSettings**（项目级模板）
19. **DiagnosticsContext / DisplayOptions**

## 与 ApexHMI 现有 `BuildAlarmView()` 对照（line 292-307）

| ApexHMI | WinCC | 评估 |
|---|---|---|
| filterCategory (All/Info/Warning/Error/Alarm) | MessageListType + DefaultMsgFilterSQL | ⚠️ 严重简化 |
| columns (JSON {field,header,width}) | MessageBlock/MessageColumn 系列 17+ 字段 | ⚠️ 表达自由但失去 WinCC 语义对应 |
| maxRows | 无直接对应（WinCC 用过滤而非行数限制）| ⚠️ 设计不一致 |
| showAck (Boolean) | PreferredUseOnAck + Toolbar Ack 按钮 | ⚠️ 二态简化 |
| background | BackColor (p526) | ✅ |

**结论**：alarm-view 是覆盖率最低的 widget（仅 1.7%）。当前 ApexHMI 设计可完成"展示告警列表 + 点击确认"基础场景，但缺乏**工业告警系统**应有的：多源汇总、SQL 过滤、Hitlist 排行、自动滚动、导出/打印、确认行为可配。建议作为长期补全项目，分阶段：
- **阶段 A**（最小可用增强）：补 AutoScroll、ShowMilliseconds、列宽持久化、Toolbar 内置 5 个按钮
- **阶段 B**（深度对齐）：Source 多源、MessageListType 全维度、MsgFilterSQL
- **阶段 C**（合规）：导出/打印
