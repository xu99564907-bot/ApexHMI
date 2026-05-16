# Widget Properties — ApexHMI vs WinCC ProgRef V18

**目的**：以西门子 WinCC ProgRef V18（System Manual, 11/2022）官方 PDF 为基准，对 ApexHMI 现有 27 个 widget 的 Property 表进行逐字段比对，识别覆盖率与差异。

**来源**：
- PDF：`docs/refs/WinCC-ProgRef-V18.pdf`（109813306_WinCC_ProgRef_enUS_en-US.pdf，9.85 MB）
- 文本：`docs/refs/WinCC-ProgRef-V18.txt`（pdftotext -layout，130 358 行）
- 索引：`docs/refs/screen-items-index.txt` / `docs/refs/properties-tables-index.txt`
- 抽取 CSV：`docs/refs/props/<widget>.csv`（每行 `PropertyName,PageRef`，PageRef = `-` 表示在 IOField 表中存在但无独立详细页）

## ApexHMI ↔ WinCC ScreenItem 对应一览（27 widget）

| # | ApexHMI widget | WinCC ScreenItem | PDF Table | WinCC 属性数 | ApexHMI 当前属性数 | 详细文档 |
|---|---|---|---|---|---|---|
| 1 | text | TextField | Table 1-104 | （见 _remaining）| 5 | [_remaining](./_remaining.md) |
| 2 | rectangle | Rectangle | Table 1-82 | （见 _remaining）| 7 | [_remaining](./_remaining.md) |
| 3 | ellipse | Ellipse | Table 1-34 | （见 _remaining）| 6 | [_remaining](./_remaining.md) |
| 4 | line | Line | Table 1-52 | （见 _remaining）| 7 | [_remaining](./_remaining.md) |
| 5 | polyline | Polyline | Table 1-70 | （见 _remaining）| 4 | [_remaining](./_remaining.md) |
| 6 | polygon | Polygon | Table 1-68 | （见 _remaining）| 4 | [_remaining](./_remaining.md) |
| 7 | graphic-view | GraphicView | Table 1-46 | （见 _remaining）| 3 | [_remaining](./_remaining.md) |
| 8 | **io-numeric** | **IOField** | **Table 1-50** | **87** | 12 | [io-numeric.md](./io-numeric.md) |
| 9 | **io-symbolic** | **SymbolicIOField** | **Table 1-99** | **91** | 5 | [io-symbolic.md](./io-symbolic.md) |
| 10 | **io-graphic** | **GraphicIOField** | **Table 1-44** | **51** | 4 | [io-graphic.md](./io-graphic.md) |
| 11 | datetime | DateTimeField | Table 1-31 | （见 _remaining）| 5 | [_remaining](./_remaining.md) |
| 12 | **button** | **Button** | **Table 1-11** | **97** | 8 | [button.md](./button.md) |
| 13 | switch | Switch | Table 1-97 | （见 _remaining）| 7 | [_remaining](./_remaining.md) |
| 14 | round-button | RoundButton | Table 1-84 | （见 _remaining）| 5 | [_remaining](./_remaining.md) |
| 15 | **bar** | **Bar** | **Table 1-7** | **107** | 12 | [bar.md](./bar.md) |
| 16 | **gauge** | **Gauge** | **Table 1-42** | **77** | 11 | [gauge.md](./gauge.md) |
| 17 | slider | Slider | Table 1-91 | （见 _remaining）| 9 | [_remaining](./_remaining.md) |
| 18 | scrollbar | (无独立 ScreenItem，复用 Slider) | — | — | 9 | [_remaining](./_remaining.md) |
| 19 | clock | Clock | Table 1-25 | （见 _remaining）| 6 | [_remaining](./_remaining.md) |
| 20 | combobox | ComboBox | Table 1-27 | （见 _remaining）| 2 | [_remaining](./_remaining.md) |
| 21 | listbox | Listbox | Table 1-55 | （见 _remaining）| 2 | [_remaining](./_remaining.md) |
| 22 | checkbox | CheckBox | Table 1-17 | （见 _remaining）| 5 | [_remaining](./_remaining.md) |
| 23 | optiongroup | OptionGroup | Table 1-62 | （见 _remaining）| 3 | [_remaining](./_remaining.md) |
| 24 | **trend-view** | **OnlineTrendControl** | **Table 1-60** | **188** | 9 | [trend-view.md](./trend-view.md) |
| 25 | **alarm-view** | **AlarmControl** + **AlarmView** | **Table 1-1 / 1-3** | **226 + 66** | 5 | [alarm-view.md](./alarm-view.md) |
| 26 | table-view | OnlineTableControl | Table 1-57 | （见 _remaining）| 5 | [_remaining](./_remaining.md) |
| 27 | screen-window | ScreenWindow | Table 1-80 | （见 _remaining）| 3 | [_remaining](./_remaining.md) |

## 高优先 8 个完整文档

按业务影响排序：

1. [io-numeric.md](./io-numeric.md) — 数值 I/O 域（生产数据录入/显示，最高频）
2. [io-symbolic.md](./io-symbolic.md) — 状态/枚举显示（开关量、状态机）
3. [io-graphic.md](./io-graphic.md) — 图形 I/O 域（设备状态图标）
4. [bar.md](./bar.md) — 进度条/液位（料位、压力、温度可视化）
5. [gauge.md](./gauge.md) — 仪表盘（速度、压力、温度仿真表盘）
6. [button.md](./button.md) — 按钮（所有点击操作的基础）
7. [trend-view.md](./trend-view.md) — 趋势图（历史/实时曲线，过程分析必备）
8. [alarm-view.md](./alarm-view.md) — 报警视图（异常事件展示与确认）

## 覆盖率总结

| Widget | WinCC 属性 | ApexHMI 现有 | 覆盖率 |
|---|---|---|---|
| io-numeric | 87 | 12 | 13.8% |
| io-symbolic | 91 | 5 | 5.5% |
| io-graphic | 51 | 4 | 7.8% |
| bar | 107 | 12 | 11.2% |
| gauge | 77 | 11 | 14.3% |
| button | 97 | 8 | 8.2% |
| trend-view | 188 | 9 | 4.8% |
| alarm-view | 226+66 | 5 | 1.7% |

**整体覆盖率 < 15%**。ApexHMI 当前 widget schema 对标 WinCC 仅完成"最小可用"层级，缺少：动态外观（闪烁/限值变色）、安全（操作权限/二次确认）、报警/限值联动、字体/对齐/边距细节、控件状态（焦点/禁用/隐藏）等大类。

详见各 widget 文档与 [_remaining.md](./_remaining.md)。
