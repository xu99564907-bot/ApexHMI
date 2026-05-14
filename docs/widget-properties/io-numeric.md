# io-numeric / WinCC ScreenItem: **IOField**

**来源**：WinCC ProgRef V18（Siemens, 11/2022），**Table 1-50 Properties**，PDF 文本（layout 版）行号 16970-17252；详细字段说明位于 PDF Page 332 起的 IOField 章节及各属性独立详细页。

**对应关系**：ApexHMI `io-numeric` 对应 WinCC **IOField**（数值/字符串/日期 I/O 域）。VBS Type identifier = `HMIIOField`。

**统计**：WinCC IOField **87 个属性**；ApexHMI io-numeric 现有 **12 个属性**；覆盖率 **13.8%**。

---

## 完整属性表（按 PDF 字母序）

属性后括号为详细页号；`-` 表示该属性在 Table 1-50 中列出但当前 RT 版本均不支持（仍是 IOField 类的合法属性）。RT 版本读写权限请直接查 PDF Page 332-336。

| #  | 属性名 | Page | 类别（推测） | ApexHMI 现状 |
|---|---|---|---|---|
| 1  | AboveUpperLimitColor | 490 | 限值/外观 | ❌ |
| 2  | AcceptOnExit | 491 | 输入行为 | ❌ |
| 3  | AcceptOnFull | 491 | 输入行为 | ❌ |
| 4  | AdaptBorder | 498 | 外观/布局 | ❌ |
| 5  | AllTagTypesAllowed | — | 数据 | ❌ |
| 6  | AskOperationMotive | — | 安全/审计 | ❌ |
| 7  | Authorization | — | 安全（操作权限）| ❌ |
| 8  | BackColor | 526 | 外观 | ✅ `background` |
| 9  | BackFillStyle | 531 | 外观 | ❌ |
| 10 | BackFlashingColorOff | — | 闪烁 | ❌ |
| 11 | BackFlashingColorOn | — | 闪烁 | ❌ |
| 12 | BackFlashingEnabled | — | 闪烁 | ❌ |
| 13 | BackFlashingRate | — | 闪烁 | ❌ |
| 14 | BelowLowerLimitColor | — | 限值/外观 | ❌ |
| 15 | BorderBackColor | — | 边框 | ❌ |
| 16 | BorderColor | — | 边框 | ❌ |
| 17 | BorderFlashingColorOff | — | 边框闪烁 | ❌ |
| 18 | BorderFlashingColorOn | — | 边框闪烁 | ❌ |
| 19 | BorderFlashingEnabled | — | 边框闪烁 | ❌ |
| 20 | BorderFlashingRate | — | 边框闪烁 | ❌ |
| 21 | BorderStyle | 576 | 边框 | ❌ |
| 22 | BorderWidth | 577 | 边框 | ❌ |
| 23 | BorderWidth3D | — | 3D 边框 | ❌ |
| 24 | BottomMargin | — | 内边距 | ❌ |
| 25 | CanBeGrouped | — | 布局 | ❌ |
| 26 | ClearOnError | 598 | 输入行为 | ❌ |
| 27 | ClearOnFocus | 598 | 输入行为 | ❌ |
| 28 | CornerRadius | — | 外观 | ❌ |
| 29 | CornerStyle | 635 | 外观 | ❌ |
| 30 | CursorControl | 639 | 输入行为 | ❌ |
| 31 | **DataFormat** | **642** | 格式 | ⚠️ 部分（见详细说明）|
| 32 | EdgeStyle | 657 | 边框 | ❌ |
| 33 | EditOnFocus | 659 | 输入行为 | ❌ |
| 34 | Enabled | 660 | 状态 | ❌ |
| 35 | FieldLength | — | 输入约束 | ❌ |
| 36 | FillPatternColor | — | 外观 | ❌ |
| 37 | FitToLargest | — | 布局 | ❌ |
| 38 | Flashing | — | 闪烁 | ❌ |
| 39 | FlashingColorOff | — | 闪烁 | ❌ |
| 40 | FlashingColorOn | — | 闪烁 | ❌ |
| 41 | FlashingEnabled | — | 闪烁 | ❌ |
| 42 | FlashingRate | 699 | 闪烁 | ❌ |
| 43 | Font | — | 字体 | ❌ |
| 44 | FontBold | 708 | 字体 | ❌ |
| 45 | FontItalic | 708 | 字体 | ❌ |
| 46 | FontName | 709 | 字体 | ❌ |
| 47 | FontSize | 711 | 字体 | ✅ `fontSize` |
| 48 | FontUnderline | — | 字体 | ❌ |
| 49 | ForeColor | 712 | 外观 | ✅ `foreground` |
| 50 | FormatPattern | — | 格式（输出）| ⚠️ 部分（见详细说明）|
| 51 | Height | 720 | 布局 | ✅ (由 layout 提供) |
| 52 | HelpText | 724 | 帮助 | ❌ |
| 53 | **HiddenInput** | **725** | 输入（密码）| ❌ |
| 54 | HorizontalAlignment | — | 文本对齐 | ✅ `textAlign` |
| 55 | InputValue | 747 | 数据 | ❌ |
| 56 | Layer | 764 | 布局 | ❌ |
| 57 | Left | 770 | 布局 | ✅ (layout) |
| 58 | LeftMargin | — | 内边距 | ❌ |
| 59 | LineEndShapeStyle | — | 外观 | ❌ |
| 60 | LineWrap | — | 文本 | ❌ |
| 61 | Location | — | 布局 | ❌ |
| 62 | **LogOperation** | **794** | 审计 | ❌ |
| 63 | LowerLimit | 795 | 限值（输入校验）| ✅ `minValue` |
| 64 | **Mode** | **824** | 字段类型 | ⚠️ 部分（见详细说明）|
| 65 | Name | — | 标识 | ✅ (由 layout) |
| 66 | **ProcessValue** | **874** | 数据 | ✅ `variable` |
| 67 | RightMargin | — | 内边距 | ❌ |
| 68 | ShiftDecimalPoint | — | 格式 | ❌ |
| 69 | ShowBadTagState | — | 数据 | ❌ |
| 70 | ShowLeadingZeros | — | 格式 | ❌ |
| 71 | Size | — | 布局 | ✅ (layout) |
| 72 | StyleItem | — | 样式 | ❌ |
| 73 | TabIndex | — | 焦点 | ❌ |
| 74 | TabIndexAlpha | — | 焦点 | ❌ |
| 75 | TextOrientation | — | 文本 | ❌ |
| 76 | ToolTipText | 1082 | 帮助 | ❌ |
| 77 | Top | 1083 | 布局 | ✅ (layout) |
| 78 | TopMargin | — | 内边距 | ❌ |
| 79 | Transparency | 1087 | 外观 | ❌ |
| 80 | Unit | 1142 | 格式 | ✅ `unit` |
| 81 | UpperLimit | 1145 | 限值 | ✅ `maxValue` |
| 82 | UseDesignColorSchema | — | 主题 | ❌ |
| 83 | UseTagLimitColors | — | 限值/主题 | ❌ |
| 84 | UseTwoHandOperation | — | 安全 | ❌（"No access in runtime"，PDF p1162） |
| 85 | VerticalAlignment | — | 文本对齐 | ❌ |
| 86 | Visible | 1204 | 状态 | ❌ |
| 87 | Width | 1217 | 布局 | ✅ (layout) |

---

## 关键字段详细说明（节选 PDF 原文）

### DataFormat (Page 642) — ⚠️ ApexHMI 当前实现不充分

**原文**（节选 PDF 行 44492-44514）：
> Description: Returns the display format. Access in runtime: Read and write
> Syntax: Object.DataFormat[=IOFieldDataFormat]
> Object: Required. An object of the type "ScreenItem" with the format: IOField
> IOFieldDataFormat: A value or a constant that returns the display format.
> Value 0 / Designation Binary / The content is shown in "Binary" data format.
> Value 1 / Designation Decimal / The content is shown in "Decimal" data format.
> Value 2 / Designation String / Represents character strings.
> Value 3 / Designation Hexadecimal / The content is shown in "Hexadecimal" data format.

**ApexHMI 现状**：当前 `format` 字段（DefaultValue=`"0.##"`）按 .NET 数值格式串解释 —— 等价于 WinCC 的 `FormatPattern` 而非 `DataFormat`。**`DataFormat` 完全缺失**（无 Binary/Decimal/Hexadecimal/String 切换能力）。

**修正建议**：新增 `dataFormat` 枚举字段（Decimal / Binary / Hexadecimal / String / DateTime），保留 `format` 作为输出格式串。

---

### Mode (Page 824) — ⚠️ ApexHMI 当前语义不一致

**WinCC Mode**：字段类型枚举（Input / Output / TwoStates / etc，决定输入/输出方向 + 控件外观）。ApexHMI 现有 `mode` 字段（Input / Output / InputOutput）**语义部分一致**——都表达 I/O 方向，但 WinCC 还混入了"Two states / Three states"等显示样式，ApexHMI 未覆盖。

---

### ProcessValue (Page 874) — ✅ ApexHMI 已实现为 `variable`

**原文**：Specifies the default for the value to be displayed. (Read and write)
**ApexHMI**：`variable`（TagAddress 类型）。基本对应。但 WinCC 还区分 `ProcessValue`（显示值）与 `InputValue`（输入缓冲值，Page 747）；ApexHMI 用单一 `variable` 表达。⚠️ 输入未提交时 ApexHMI 无显式 `InputValue` 字段，运行时绑定可能丢失"未确认输入"语义。

---

### AcceptOnExit (Page 491) — ❌ 缺失

**原文**：Specifies whether the input field will be confirmed automatically when it is left. (Read and write)

工业 HMI 场景常见行为：操作员点离开 I/O 域时自动写入。**ApexHMI 缺**——当前只能"按 Enter 写入"。

---

### ClearOnError (Page 598) — ❌ 缺失

**原文**：Specifies whether an invalid input in this object will be deleted automatically. (Read and write)

输入越界时自动清空。**生产环境强需求**——目前 ApexHMI 在 minValue/maxValue 校验失败后行为未规范。

---

### ClearOnFocus (Page 598) — ❌ 缺失

**原文**：Specifies whether the field entry will be deleted as soon as the I/O field is activated.

聚焦即清空（便于直接输入新值）。

---

### HiddenInput (Page 725) — ❌ 缺失

**原文**：Specifies whether the input value or a * for each character will be shown during the input.

密码/敏感字段输入时显示 `*`。**ApexHMI 完全没有**密码字段支持。

---

### LogOperation (Page 794) — ❌ 缺失

**原文**：Specifies whether the reason for operating this object is logged.

操作审计：写入时是否记录到审计日志。**ApexHMI 当前无审计字段** —— 在等保/制造业 MES 集成场景是硬需求。

---

### Authorization — ❌ 缺失

**原文**：Specifies the operating rights of the selected object in runtime.

操作权限组绑定（需登录用户具备某 group 才能写入）。**ApexHMI 当前没有 Widget 级权限**（仅有页级路由权限）。

---

### LowerLimit (Page 795) / UpperLimit (Page 1145) — ✅ 已实现为 minValue / maxValue

但 WinCC 还区分 `AboveUpperLimitColor` / `BelowLowerLimitColor` / `UseTagLimitColors` —— 越界时改变控件颜色提示。**ApexHMI 仅做拒收，无视觉反馈**。

---

### Flashing 系列（10 个属性） — ❌ 全部缺失

WinCC 提供完整的"背景闪烁 / 边框闪烁 / 文字闪烁"配置（颜色 On/Off + 速率 + 使能 + 限值越界自动触发）。**ApexHMI 当前无闪烁机制** —— 但 P2A 阶段动画系统已上线，闪烁可基于动画引擎实现，需要在 schema 中暴露。

---

### Font 系列（FontName/Size/Bold/Italic/Underline） — 仅 FontSize 已实现

当前 ApexHMI 只有 `fontSize`，缺 FontName / FontBold / FontItalic / FontUnderline。

---

### 内边距系列（Top/Bottom/Left/RightMargin） — ❌ 全部缺失

控件文本到边框的内边距。ApexHMI 现在文本紧贴边框。

---

## 总结：io-numeric 缺失分级

### 🔴 严重缺失（生产必须，立即修）
1. **DataFormat**（Binary/Dec/Hex/String 显示格式切换）
2. **ClearOnError / ClearOnFocus / AcceptOnExit**（输入行为）
3. **Authorization + LogOperation**（操作权限 + 审计日志）
4. **HiddenInput**（密码字段）
5. **AboveUpperLimitColor / BelowLowerLimitColor / UseTagLimitColors**（越界视觉反馈）
6. **ShowBadTagState**（变量品质坏值时灰显）

### 🟡 中度缺失（影响美观/体验）
7. **Font 系列**：FontName / FontBold / FontItalic / FontUnderline
8. **边框系列**：BorderColor / BorderWidth / BorderStyle / EdgeStyle / CornerStyle / CornerRadius
9. **内边距**：Top/Bottom/Left/RightMargin
10. **VerticalAlignment**
11. **HelpText / ToolTipText**（用户帮助）
12. **Visible / Enabled**（动态可见性/启用，**P2A 动画系统已部分覆盖**）
13. **Transparency**

### 🟢 轻度缺失（动画/装饰类）
14. **Flashing 全套**（10 个，可基于 P2A 动画引擎构造高阶 schema）
15. **BackFillStyle / FillPatternColor**（填充样式）
16. **UseDesignColorSchema**（全局主题联动）

### ⚪ 暂不需要（西门子专属/低价值）
- `AllTagTypesAllowed` / `TabIndexAlpha` / `LineEndShapeStyle` / `BorderWidth3D`
- `UseTwoHandOperation`（PDF p1162 标注 "No access in runtime"——西门子自己也未启用）

---

## 与 ApexHMI 现有 `BuildIoNumeric()` 对照（Services/RuntimeUi/WidgetSchemaCatalogSeed.cs:554-579）

| ApexHMI 字段 | WinCC 对应 | 评估 |
|---|---|---|
| variable | ProcessValue (p874) | ✅ 一致 |
| mode (Input/Output/InputOutput) | Mode (p824) | ⚠️ 语义部分对齐 |
| format ("0.##") | FormatPattern | ✅ 一致（但应与 DataFormat 区分）|
| decimals | ShiftDecimalPoint | ⚠️ 语义近似，但 WinCC 用整数位/小数位分别约束 |
| unit | Unit (p1142) | ✅ 一致 |
| minValue / maxValue | LowerLimit / UpperLimit | ✅ 一致 |
| background / foreground | BackColor / ForeColor | ✅ 一致 |
| fontSize | FontSize | ✅ 一致 |
| textAlign | HorizontalAlignment | ✅ 一致 |

**建议下一步**（阶段 2 任务）：
- 拆分 `format` 为 `dataFormat`（枚举）+ `formatPattern`（字符串）
- 补 `clearOnError` / `clearOnFocus` / `acceptOnExit` / `hiddenInput` 4 个布尔字段
- 补 `authorization` / `logOperation` 字段（依赖用户系统）
- 补 `aboveUpperLimitColor` / `belowLowerLimitColor`（依赖限值越界事件）
- 字体系列补齐（fontName/Bold/Italic/Underline）
- 边框系列补齐（borderColor/borderWidth/borderStyle/cornerRadius）
