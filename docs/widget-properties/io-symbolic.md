# io-symbolic / WinCC ScreenItem: **SymbolicIOField**

**来源**：WinCC ProgRef V18，**Table 1-99 Properties**，PDF Page 422 起。VBS Type identifier = `HMISymbolicIOField`。

**统计**：WinCC SymbolicIOField **91 个属性**；ApexHMI io-symbolic 现有 **5 个属性**；覆盖率 **5.5%**。

---

## 完整属性表

| # | 属性名 | Page | ApexHMI 现状 |
|---|---|---|---|
| 1 | AcceptOnExit | 491 | ❌ |
| 2 | AdaptBorder | 498 | ❌ |
| 3 | AllTagTypesAllowed | — | ❌ |
| 4 | AskOperationMotive | — | ❌ |
| 5 | **Assignments** | **516** | ⚠️ 部分（`entries` 部分对应）|
| 6 | Authorization | 517 | ❌ |
| 7 | BackColor | 526 | ✅ `background` |
| 8 | BackFillStyle | 531 | ❌ |
| 9 | BackFlashingColorOff | — | ❌ |
| 10 | BackFlashingColorOn | — | ❌ |
| 11 | BackFlashingEnabled | — | ❌ |
| 12 | BackFlashingRate | — | ❌ |
| 13 | BelowLowerLimitColor | — | ❌ |
| 14 | BitNumber | 546 | ❌ |
| 15 | BorderBackColor | — | ❌ |
| 16 | BorderColor | 562 | ❌ |
| 17 | BorderFlashingColorOff | — | ❌ |
| 18 | BorderFlashingColorOn | — | ❌ |
| 19 | BorderFlashingEnabled | — | ❌ |
| 20 | BorderFlashingRate | 570 | ❌ |
| 21 | BorderStyle | 576 | ❌ |
| 22 | BorderWidth | 577 | ❌ |
| 23 | BottomMargin | — | ❌ |
| 24 | CanBeGrouped | — | ❌ |
| 25 | CaptionBackColor | — | ❌ |
| 26 | CaptionColor | 589 | ❌ |
| 27 | CornerRadius | — | ❌ |
| 28 | CornerStyle | 635 | ❌ |
| 29 | CountVisibleItems | — | ❌ |
| 30 | CursorControl | 639 | ❌ |
| 31 | DeviceStyle | — | ❌ |
| 32 | DrawInsideFrame | — | ❌ |
| 33 | EdgeStyle | 657 | ❌ |
| 34 | EditOnFocus | 659 | ❌ |
| 35 | Enabled | 660 | ❌ |
| 36 | EvenRowBackColor | — | ❌ |
| 37 | ExtraHeightOffset | — | ❌ |
| 38 | FillPatternColor | — | ❌ |
| 39 | FitToLargest | — | ❌ |
| 40 | Flashing | — | ❌ |
| 41 | FlashingColorOff | — | ❌ |
| 42 | FlashingColorOn | — | ❌ |
| 43 | FlashingEnabled | — | ❌ |
| 44 | FlashingRate | 699 | ❌ |
| 45 | FontBold | 708 | ❌ |
| 46 | FontItalic | 708 | ❌ |
| 47 | FontName | 709 | ❌ |
| 48 | FontSize | 711 | ❌ |
| 49 | FontUnderline | — | ❌ |
| 50 | ForeColor | 712 | ✅ `foreground` |
| 51 | Height | 720 | ✅ (layout) |
| 52 | HelpText | 724 | ❌ |
| 53 | HorizontalAlignment | — | ❌ |
| 54 | InputValue | 747 | ❌ |
| 55 | ItemBorderStyle | — | ❌ |
| 56 | Layer | 764 | ❌ |
| 57 | Left | 770 | ✅ (layout) |
| 58 | LeftMargin | — | ❌ |
| 59 | LineEndShapeStyle | — | ❌ |
| 60 | Location | — | ❌ |
| 61 | LogOperation | — | ❌ |
| 62 | **Mode** | **824** | ✅ `mode` |
| 63 | Name | — | ✅ (layout) |
| 64 | OnValue | — | ❌ |
| 65 | **ProcessValue** | **874** | ✅ `variable` |
| 66 | RightMargin | — | ❌ |
| 67 | SelectBackColor | — | ❌ |
| 68 | SelectForeColor | — | ❌ |
| 69 | SeparatorBackColor | — | ❌ |
| 70 | SeparatorColor | — | ❌ |
| 71 | SeparatorCornerStyle | — | ❌ |
| 72 | SeparatorStyle | 922 | ❌ |
| 73 | SeparatorWidth | 922 | ❌ |
| 74 | **ShowBadTagState** | **926** | ❌ |
| 75 | ShowDropDownButton | 929 | ❌ |
| 76 | **Style** | **921** | ❌（"List type"） |
| 77 | **TextList** | **1002** | ⚠️ 部分（`entries` 内联表达式）|
| 78 | TextOff | — | ❌ |
| 79 | TextOn | — | ❌ |
| 80 | TextOrientation | — | ❌ |
| 81 | ToolTipText | 1082 | ❌ |
| 82 | Top | 1083 | ✅ (layout) |
| 83 | TopMargin | — | ❌ |
| 84 | Transparency | — | ❌ |
| 85 | UseDesignColorSchema | — | ❌ |
| 86 | VerticalAlignment | — | ❌ |
| 87 | Visible | 1204 | ❌ |
| 88 | Width | 1217 | ✅ (layout) |
| 89 | Activate (方法) | 1250 | — |
| 90 | ActivateDynamic (方法) | 1252 | — |
| 91 | DeactivateDynamic (方法) | 1259 | — |

（按 PDF Table 1-99 原始顺序，方法部分见 Table 1-100 Methods）

---

## 关键字段详细说明

### Assignments (Page 516) — ⚠️ 部分对应 ApexHMI `entries`

**原文**：
> Specifies a list which contains the assignments between the output value and the output text actually to be output. The assignments are dependent on the set list type. You define the list type with the ListType property.
> Access in runtime: Read and write
> Syntax: Object.Assignments[=STRING]

**ApexHMI**：`entries` 字段（DefaultValue=`"0=停止;1=运行"`），表达 value→text 映射，**语义一致**。但 WinCC 的列表类型由 `Style` 属性切换（决定 ListType），ApexHMI 只支持单一 KV 映射。

### TextList (Page 1002) — ⚠️ 文本列表引用

**原文**：
> Returns the text list that supplies the object with values. Access in runtime RT Pro: Read; RT Adv: No access.
> Syntax: Object.TextList[=HmiObjectHandle]

**ApexHMI**：`entries` 字段已支持 `{textList:xxx}` 引用语法，**语义对应**。但 WinCC 是只读 handle，ApexHMI 把 entries 和 textList 引用合并为同一字段。

### Style (Page 921) — ❌ 缺失（List type）

WinCC 区分 "Text list / Decimal / Binary / Bit / Range" 等列表类型；ApexHMI 仅有 value=text 文本列表。**缺失 BitNumber 模式**（按某 bit 取值显示文本）。

### Mode (Page 824) — ✅ 一致

Input/Output/InputOutput 三态，ApexHMI 一致。

### ShowBadTagState (Page 926) — ❌ 缺失

变量品质坏值时灰显控件，**生产环境强需求**（PLC 通信断时给出明显视觉反馈）。

### ShowDropDownButton (Page 929) — ❌ 缺失

是否显示右侧下拉箭头（仅 Input/InputOutput 模式有意义）。ApexHMI 当前下拉行为未定义。

---

## 总结：io-symbolic 缺失分级

### 🔴 严重缺失
1. **Style / BitNumber**（按 Bit 切换显示文本——开关量场景非常常见）
2. **ShowBadTagState**（变量品质反馈）
3. **ShowDropDownButton**（下拉按钮可见性）
4. **Authorization / LogOperation**（权限 + 审计）
5. **AcceptOnExit / EditOnFocus / ClearOnFocus**（输入行为）

### 🟡 中度
6. 边框 / 字体 / 内边距系列（同 IOField）
7. **Enabled / Visible**
8. **SelectBackColor / SelectForeColor**（下拉项高亮）
9. **CaptionColor / CaptionBackColor**（标题色）

### 🟢 装饰类
10. Flashing 全套 / Transparency / UseDesignColorSchema

## 与 ApexHMI 现有 `BuildIoSymbolic()` 对照（line 582-597）

| ApexHMI 字段 | WinCC | 评估 |
|---|---|---|
| variable | ProcessValue (p874) | ✅ |
| mode | Mode (p824) | ✅ |
| entries (KV) | Assignments (p516) + TextList (p1002) | ⚠️ 合并表达 |
| background / foreground | BackColor / ForeColor | ✅ |

**结论**：io-symbolic 比 io-numeric 缺得更狠（仅 5 字段）。最缺：Bit 模式、字体、边框、状态反馈。
