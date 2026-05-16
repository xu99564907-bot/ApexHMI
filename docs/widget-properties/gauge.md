# gauge / WinCC ScreenItem: **Gauge**

**来源**：WinCC ProgRef V18，**Table 1-42 Properties**，PDF Page 320 起。VBS Type identifier = `HMIGauge`。

**统计**：WinCC Gauge **77 个属性**；ApexHMI gauge 现有 **11 个属性**；覆盖率 **14.3%**。

---

## 完整属性表

| # | 属性名 | Page | ApexHMI 现状 |
|---|---|---|---|
| 1 | **AngleMax** | **511** | ❌ |
| 2 | **AngleMin** | **512** | ❌ |
| 3 | BackColor | 526 | ❌ |
| 4 | BackFillStyle | 531 | ❌ |
| 5 | BackPicture | 538 | ❌ |
| 6 | BackStyle | 538 | ❌ |
| 7 | BorderBackColor | — | ❌ |
| 8 | BorderColor | — | ❌ |
| 9 | BorderInnerStyle3D | — | ❌ |
| 10 | BorderOuterStyle3D | — | ❌ |
| 11 | BorderWidth | 577 | ❌ |
| 12 | BorderWidth3D | — | ❌ |
| 13 | Bounds | — | ❌ |
| 14 | CanBeGrouped | — | ❌ |
| 15 | CaptionColor | 589 | ❌ |
| 16 | CaptionFont | 589 | ❌ |
| 17 | CaptionText | 590 | ❌ |
| 18 | CaptionTop | 591 | ❌ |
| 19 | CenterColor | 595 | ❌ |
| 20 | CenterSize | 595 | ❌ |
| 21 | CompatibilityMode | — | ❌ |
| 22 | CornerRadius | — | ❌ |
| 23 | **DangerRangeColor** | — | ⚠️ `alarmColor` |
| 24 | **DangerRangeStart** | — | ⚠️ `alarmThreshold` |
| 25 | DangerRangeVisible | — | ❌ |
| 26 | DeviceStyle | — | ❌ |
| 27 | **DialColor** | **649** | ❌ |
| 28 | DialFillStyle | 650 | ❌ |
| 29 | DialPicture | 650 | ❌ |
| 30 | DialSize | 651 | ❌ |
| 31 | EdgeStyle | 657 | ❌ |
| 32 | Enabled | 660 | ❌ |
| 33 | Flashing | 692 | ❌ |
| 34 | Gradation | 716 | ❌ |
| 35 | Height | 720 | ✅ (layout) |
| 36 | InnerDialColor | — | ❌ |
| 37 | InnerDialInnerDistance | — | ❌ |
| 38 | InnerDialOuterDistance | — | ❌ |
| 39 | Layer | 764 | ❌ |
| 40 | Left | 770 | ✅ (layout) |
| 41 | LimitRangeCollection | — | ❌ |
| 42 | Location | — | ❌ |
| 43 | LockSquaredExtent | — | ❌ |
| 44 | **MaximumValue** | — | ✅ `maxValue` |
| 45 | **MinimumValue** | — | ✅ `minValue` |
| 46 | Name | 827 | ✅ (layout) |
| 47 | NeedleHeight | — | ❌ |
| 48 | **NormalRangeColor** | — | ❌ |
| 49 | NormalRangeVisible | — | ❌ |
| 50 | **PointerColor** | **869** | ✅ `foreground`（指针色）|
| 51 | **ProcessValue** | **874** | ✅ `variable` |
| 52 | ScaleLabelColor | — | ❌ |
| 53 | ScaleLabelFont | — | ❌ |
| 54 | ScaleTickColor | — | ❌ |
| 55 | ScaleTickLabelPosition | — | ❌ |
| 56 | ScaleTickLength | — | ❌ |
| 57 | ScaleTickPosition | — | ❌ |
| 58 | ShowDecimalPoint | 928 | ❌ |
| 59 | ShowInnerDial | — | ❌ |
| 60 | ShowLimitRanges | — | ❌ |
| 61 | ShowPeakValuePointer | — | ❌ |
| 62 | Size | — | ✅ (layout) |
| 63 | StyleItem | — | ❌ |
| 64 | TabIndex | — | ❌ |
| 65 | TabIndexAlpha | — | ❌ |
| 66 | Top | 1083 | ✅ (layout) |
| 67 | Transparency | 1087 | ❌ |
| 68 | UnitColor | 1143 | ❌ |
| 69 | UnitFont | 1143 | ❌ |
| 70 | UnitText | 1144 | ❌ |
| 71 | UnitTop | 1144 | ❌ |
| 72 | UseDesignColorSchema | — | ❌ |
| 73 | Visible | 1204 | ❌ |
| 74 | **WarningRangeColor** | — | ⚠️ `warnColor` |
| 75 | **WarningRangeStart** | — | ⚠️ `warnThreshold` |
| 76 | WarningRangeVisible | — | ❌ |
| 77 | Width | 1217 | ✅ (layout) |

---

## 关键字段详细说明

### AngleMin (Page 512) / AngleMax (Page 511) — ❌ 核心缺失

**原文**：
> Specifies the angle of the scale start of the "Gauge" object. Access in runtime: Read and write
> Comments: The start and end of the scale gradations are described with the properties "AngleMin" and "AngleMax" in angle degrees.
> The value of the AngleMin property must always be less than the value of the AngleMax property.
> The zero degree angle is located at the 3 o'clock position on the scale. Positive angle values are counted clockwise.

**关键意义**：决定表盘是 270° 半圆、180° 半圆、还是 90° 扇形。**ApexHMI 当前是固定 270° 半圆**，不可配。

### DialColor (Page 649) / DialFillStyle / DialPicture / DialSize — ❌ 缺失

表盘本体（圆形背板）的颜色/纹理/图片/尺寸。ApexHMI 当前是固定白底。

### 3 级范围带 (NormalRange / WarningRange / DangerRange)

WinCC：每段独立的 Start + Color + Visible 三字段。  
**ApexHMI**：只有 warn/alarm 两级（无 Normal 显示带），且只有单一 Threshold（缺 Start/End 范围带表达）。WinCC 通过 `WarningRangeStart` + 下一档的 `DangerRangeStart` 形成区段。

### LimitRangeCollection — ❌ 高级限值集合（不止 3 档）

可定义任意多档限值带（每档 LowerLimit/UpperLimit/Color/Caption）。ApexHMI 完全缺。

### ScaleTickPosition / ScaleTickLength / ScaleTickLabelPosition — ❌ 缺失

刻度的位置（内/外）、长度、标签位置。**ApexHMI 当前 majorTicks/minorTicks 仅控制数量**，无位置/长度配置。

### Gradation (Page 716) — ❌ 缺失

刻度步长（直接指定相邻主刻度间数值差，而非数量）。

### CenterColor / CenterSize (Page 595) — ❌ 中心轴

指针中心轴的颜色和大小。

### CaptionText / CaptionColor / CaptionFont (Page 589-591) — ❌ 标题

表盘上方的标题文字。**ApexHMI 缺失标题** — 多个 gauge 摆一起时无法标注区分。

### UnitText / UnitColor / UnitFont (Page 1142-1144) — ⚠️ ApexHMI `unit`

ApexHMI 已有 `unit` 字段。但单位文本的样式（颜色/字体/位置）不可独立配置。

### ShowPeakValuePointer — ❌ 缺失

显示历史峰值指针（红线标记），常见于压力表。

### ShowInnerDial / InnerDialColor — ❌ 缺失

双层表盘（外圈刻度+内圈数值显示）。

---

## 总结：gauge 缺失分级

### 🔴 严重缺失
1. **AngleMin / AngleMax**（表盘形状/角度——根本性的外观参数）
2. **ScaleTickPosition / Length / Gradation**（刻度细节）
3. **CaptionText**（标题）
4. **3 级范围带 + LimitRangeCollection**（限值视觉化）
5. **ShowDecimalPoint / Unit 字段化**

### 🟡 中度
6. **DialColor / DialFillStyle / BackPicture**（表盘外观）
7. **CenterColor / CenterSize**（中心轴）
8. **ShowPeakValuePointer**（峰值指针）
9. **ShowInnerDial / InnerDial***（双层表盘）
10. **ScaleLabel*** / **ScaleTick***（刻度文字/线条样式）
11. **Authorization**

### 🟢 装饰
12. Flashing / Transparency / UseDesignColorSchema
13. 3D 边框系列

## 与 ApexHMI 现有 `BuildGauge()` 对照（line 649-666）

| ApexHMI | WinCC | 评估 |
|---|---|---|
| variable | ProcessValue (p874) | ✅ |
| minValue / maxValue | MinimumValue / MaximumValue | ✅ |
| unit | UnitText (p1144) | ✅ |
| warnThreshold / warnColor | WarningRangeStart / Color | ⚠️ |
| alarmThreshold / alarmColor | DangerRangeStart / Color | ⚠️ |
| majorTicks / minorTicks | Gradation 系列（推测）| ⚠️ 不完全对应 |
| foreground | PointerColor (p869) | ✅ |

**结论**：gauge 最需要 **角度可配（AngleMin/Max）+ 标题（CaptionText）+ 刻度细节**。
