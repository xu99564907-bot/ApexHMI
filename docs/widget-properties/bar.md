# bar / WinCC ScreenItem: **Bar**

**来源**：WinCC ProgRef V18，**Table 1-7 Properties**，PDF Page 259 起。VBS Type identifier = `HMIBar`。

**统计**：WinCC Bar **107 个属性**；ApexHMI bar 现有 **12 个属性**；覆盖率 **11.2%**。

> Bar 是属性最丰富的 widget 之一 — WinCC Bar 是工业组态的"瑞士军刀"：液位/温度/压力可视化、5 级限值（Alarm/Warning/Limit4/Limit5/Tolerance）、刻度/数值标注、趋势指示器，全部内置。

---

## 完整属性表

| # | 属性名 | Page | ApexHMI 现状 |
|---|---|---|---|
| 1 | **AlarmLowerLimit** | **503** | ⚠️ `alarmThreshold`（仅单边）|
| 2 | **AlarmLowerLimitColor** | **504** | ⚠️ `alarmColor`（无上下区分）|
| 3 | **AlarmUpperLimit** | **506** | ⚠️ `alarmThreshold` |
| 4 | **AlarmUpperLimitColor** | **507** | ⚠️ `alarmColor` |
| 5 | AllTagTypesAllowed | — | ❌ |
| 6 | Authorization | 517 | ❌ |
| 7 | AverageLast15Values | — | ❌ |
| 8 | BackColor | 526 | ✅ `backgroundColor` |
| 9 | BackFillStyle | 531 | ❌ |
| 10 | BackFlashingColorOff | — | ❌ |
| 11 | BackFlashingColorOn | — | ❌ |
| 12 | BackFlashingEnabled | — | ❌ |
| 13 | BackFlashingRate | — | ❌ |
| 14 | BarBackColor | 539 | ❌ |
| 15 | BarBackFillStyle | — | ❌ |
| 16 | BarBackFlashingColorOff | — | ❌ |
| 17 | BarBackFlashingColorOn | — | ❌ |
| 18 | BarBackFlashingEnabled | — | ❌ |
| 19 | BarBackFlashingRate | — | ❌ |
| 20 | BarEdgeStyle | — | ❌ |
| 21 | **BarOrientation** | — | ✅ `orientation` |
| 22 | BorderBackColor | — | ❌ |
| 23 | BorderColor | 562 | ❌ |
| 24 | BorderFlashingColorOff | — | ❌ |
| 25 | BorderFlashingColorOn | — | ❌ |
| 26 | BorderFlashingEnabled | — | ❌ |
| 27 | BorderFlashingRate | — | ❌ |
| 28 | BorderStyle | 576 | ❌ |
| 29 | BorderWidth | 577 | ❌ |
| 30 | Color | 1057 | ✅ `fillColor`（推测对应）|
| 31 | **ColorChangeHysteresis** | **601** | ❌ |
| 32 | CornerStyle | 635 | ❌ |
| 33 | CountDivisions | — | ❌ |
| 34 | CountSubDivisions | — | ❌ |
| 35 | DeviceStyle | — | ❌ |
| 36 | DrawInsideFrame | — | ❌ |
| 37 | EdgeStyle | 657 | ❌ |
| 38 | Enabled | 660 | ❌ |
| 39 | FillPatternColor | — | ❌ |
| 40 | Flashing | — | ❌ |
| 41 | FlashingColorOff | — | ❌ |
| 42 | FlashingColorOn | — | ❌ |
| 43 | FlashingEnabled | — | ❌ |
| 44 | FlashingRate | — | ❌ |
| 45 | FontBold | 708 | ❌ |
| 46 | FontName | 709 | ❌ |
| 47 | FontSize | 711 | ❌ |
| 48 | ForeColor | 712 | ❌ |
| 49 | ForeColorTransparency | — | ❌ |
| 50 | Height | 720 | ✅ (layout) |
| 51 | InnerHeight | — | ❌ |
| 52 | IntegerDigits | 748 | ❌ |
| 53 | LargeTickLabelingStep | — | ❌ |
| 54 | LargeTicksBold | — | ❌ |
| 55 | LargeTicksSize | — | ❌ |
| 56 | Layer | 764 | ❌ |
| 57 | Left | 770 | ✅ (layout) |
| 58 | **Limit4LowerLimit / Limit4UpperLimit + Color** | — | ❌ |
| 59 | **Limit5LowerLimit / Limit5UpperLimit + Color** | — | ❌ |
| 60 | **LimitRangeCollection** | — | ❌ |
| 61 | LineEndShapeStyle | 787 | ❌ |
| 62 | **MaximumValue** | **799** | ✅ `maxValue` |
| 63 | **MinimumValue** | **822** | ✅ `minValue` |
| 64 | Precision | 871 | ❌ |
| 65 | **ProcessValue** | **874** | ✅ `variable` |
| 66 | ScaleColor | 892 | ❌ |
| 67 | ScaleGradation | 893 | ❌ |
| 68 | ScalePosition | 896 | ❌ |
| 69 | ScaleStart | — | ❌ |
| 70 | ScalingType | 899 | ❌ |
| 71 | SegmentColoring | — | ❌ |
| 72 | ShowBadTagState | — | ❌ |
| 73 | ShowLargeTicksOnly | — | ❌ |
| 74 | ShowLimitLines | — | ❌ |
| 75 | ShowLimitMarkers | — | ❌ |
| 76 | ShowLimitRanges | — | ❌ |
| 77 | ShowProcessValue | — | ⚠️ `showLabel` 部分对应 |
| 78 | **ShowScale** | **940** | ✅ `showScale` |
| 79 | ShowTickLabels | — | ❌ |
| 80 | ShowTrendIndicator | — | ❌ |
| 81 | Size | — | ✅ (layout) |
| 82 | StartValue | 958 | ❌ |
| 83 | StyleItem | — | ❌ |
| 84 | TabIndex | — | ❌ |
| 85 | TabIndexAlpha | — | ❌ |
| 86 | ToleranceLowerLimit | 1054 | ❌ |
| 87 | ToleranceUpperLimit | — | ❌ |
| 88 | ToolTipText | 1082 | ❌ |
| 89 | Top | 1083 | ✅ (layout) |
| 90 | Transparency | 1087 | ❌ |
| 91 | TrendIndicatorColor | — | ❌ |
| 92 | Unit | 1142 | ❌ |
| 93 | UseAutoScaling | — | ❌ |
| 94 | UseDesignColorSchema | — | ❌ |
| 95 | UseExponentialFormat | — | ❌ |
| 96 | Visible | 1204 | ❌ |
| 97 | **WarningLowerLimit** | — | ⚠️ `warnThreshold` |
| 98 | **WarningUpperLimit** | — | ⚠️ `warnThreshold` |
| 99 | Width | 1217 | ✅ (layout) |
| 100 | ZeroPoint | 1249 | ❌ |
| (+) | + 边框闪烁 / Limit4/5 颜色等冗余字段 | | — |

---

## 关键字段详细说明

### AlarmLowerLimit (Page 503) — ⚠️ ApexHMI 缺上下限区分

**原文**：
> Specifies the low limit at which the alarm is triggered. Access in runtime: Read and write
> Syntax: Object.AlarmLowerLimit[=DOUBLE]
> Comments: The type of evaluation (percentage or absolute) is defined using the "AlarmLowerLimitRelative" property. The "AlarmLowerLimitEnable" property defines whether or not monitoring of this limit is enabled.

**ApexHMI**：仅 `alarmThreshold`（单一阈值，无上下区分），`alarmColor` 单一颜色。WinCC 提供 `AlarmLowerLimit/UpperLimit + Color + Enable + Relative` 完整组（共 8 字段），还支持百分比/绝对值切换。

### ColorChangeHysteresis (Page 601) — ❌ 缺失（防抖关键）

**原文**：
> Specifies hysteresis as a percentage of the display value. The "ColorChangeHysteresisEnable" property must have the value TRUE so that the hysteresis can be calculated.

**关键场景**：值在阈值附近抖动时，颜色不会反复闪烁。**ApexHMI 完全缺失** — 当前如果 PLC 数据抖动，会触发颜色反复切换，用户体验差。

### 5 级限值系统 (Alarm / Warning / Limit4 / Limit5 / Tolerance)

WinCC Bar 提供 5 个独立可命名限值带，每个有 Lower/Upper/Color/Enable 字段。ApexHMI 当前只有 2 级（warn + alarm）。

### ScaleGradation / ScalePosition / ScalingType (892/893/896/899) — ❌ 全部缺失

- ScaleGradation：刻度间距
- ScalePosition：刻度位置（左/右/双侧）
- ScalingType：线性/对数
- LargeTicksSize / LargeTickLabelingStep：主/副刻度配置

**ApexHMI 当前 `showScale` 只是布尔开关**，无任何可调参数。

### ShowTrendIndicator / TrendIndicatorColor — ❌ 缺失

Bar 顶端显示"上升↑/下降↓"小三角图标，让操作员一眼看出趋势方向。**简单但很有用**。

### ZeroPoint (Page 1249) — ❌ 缺失

零点位置可不在 MinimumValue（如 -50~+50，零点在中间）。ApexHMI 缺。

### StartValue (Page 958) — ❌ 缺失

填充起始值（不一定从 MinimumValue 开始）。

### Unit (p1142) / Precision (p871) / IntegerDigits (p748) — ❌ 全部缺失

值标签显示时的单位、精度、整数位约束。ApexHMI 当前 `showLabel` 只显示原值，无格式化能力。

### ShowProcessValue / ShowTickLabels / ShowLimitMarkers / ShowLimitLines / ShowLimitRanges

WinCC 提供 5 个独立显示开关；ApexHMI 仅 1 个 `showLabel`，无法细粒度控制。

---

## 总结：bar 缺失分级

### 🔴 严重缺失（工业必须）
1. **限值上下区分**（AlarmLowerLimit/UpperLimit 各自独立）
2. **ColorChangeHysteresis**（防抖动颜色切换 — **必须**）
3. **5 级限值系统**（Limit4/Limit5/Tolerance 区段）
4. **ScaleGradation / ScalePosition / ScalingType**（刻度细节）
5. **Unit / Precision / IntegerDigits**（值标签格式化）
6. **ShowBadTagState**（变量品质反馈）

### 🟡 中度
7. **ShowTrendIndicator / TrendIndicatorColor**（趋势箭头）
8. **ZeroPoint / StartValue**（零点/起点）
9. **ShowProcessValue / ShowTickLabels / ShowLimitMarkers / ShowLimitLines / ShowLimitRanges**（显示项分项控制）
10. **BarBackColor**（条本身的背景，与控件外背景分离）
11. **Authorization**
12. **UseAutoScaling**（自动量程）
13. **AverageLast15Values**（平均滤波显示）

### 🟢 装饰
14. Flashing 全套（Bar/Border/Bar-Back 三套）
15. 边框系列
16. 字体系列
17. Transparency / FillPatternColor

## 与 ApexHMI 现有 `BuildBar()` 对照（line 624-646）

| ApexHMI 字段 | WinCC | 评估 |
|---|---|---|
| variable | ProcessValue (p874) | ✅ |
| minValue / maxValue | MinimumValue / MaximumValue (p822/799) | ✅ |
| orientation | BarOrientation | ✅ |
| fillColor | Color (p1057) | ✅ |
| backgroundColor | BackColor (p526) | ⚠️ 应该是 BarBackColor（条背景）vs BackColor（控件背景）|
| warnThreshold / warnColor | WarningLowerLimit + Color | ⚠️ 缺上下区分 |
| alarmThreshold / alarmColor | AlarmLowerLimit + Color | ⚠️ 缺上下区分 |
| showLabel | ShowProcessValue | ⚠️ 名字误导，且无格式化 |
| showScale | ShowScale (p940) | ✅ |

**结论**：bar 是**最该深度补齐**的 widget（生产用得最多，差距最大）。建议下一步优先：(1) 限值上下区分 (2) Hysteresis (3) 标签格式化 (4) 刻度细节。
