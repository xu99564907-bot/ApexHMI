# button / WinCC ScreenItem: **Button**

**来源**：WinCC ProgRef V18，**Table 1-11 Properties**，PDF Page 267 起。VBS Type identifier = `HMIButton`。

**统计**：WinCC Button **97 个属性**；ApexHMI button 现有 **8 个属性**；覆盖率 **8.2%**。

> WinCC Button 不仅是 click 触发器 — 它本身是双状态/三状态控件，可绑定 ProcessValue + Toggle + Mode 实现"按钮即开关"。

---

## 完整属性表

| # | 属性名 | Page | ApexHMI 现状 |
|---|---|---|---|
| 1 | AdaptBorder | 498 | ❌ |
| 2 | AllTagTypesAllowed | — | ❌ |
| 3 | Authorization | 517 | ❌ |
| 4 | Back | — | ❌ |
| 5 | BackColor | 526 | ✅ `background` |
| 6 | BackFillStyle | 531 | ❌ |
| 7 | BackFlashingColorOff | — | ❌ |
| 8 | BackFlashingColorOn | — | ❌ |
| 9 | BackFlashingEnabled | — | ❌ |
| 10 | BackFlashingRate | — | ❌ |
| 11 | **BitNumber** | — | ❌ |
| 12 | BorderBackColor | — | ❌ |
| 13 | BorderBrightColor3D | — | ❌ |
| 14 | BorderColor | 562 | ✅ `borderColor` |
| 15 | BorderFlashingColorOff | — | ❌ |
| 16 | BorderFlashingColorOn | — | ❌ |
| 17 | BorderFlashingEnabled | — | ❌ |
| 18 | BorderFlashingRate | — | ❌ |
| 19 | BorderShadeColor3D | — | ❌ |
| 20 | BorderStyle | 576 | ❌ |
| 21 | BorderWidth | 577 | ✅ `borderWidth` |
| 22 | BorderWidth3D | — | ❌ |
| 23 | CanBeGrouped | — | ❌ |
| 24 | CornerRadius | — | ✅ `cornerRadius` |
| 25 | CornerStyle | 635 | ❌ |
| 26 | DeviceStyle | — | ❌ |
| 27 | DrawInsideFrame | — | ❌ |
| 28 | EdgeStyle | 657 | ❌ |
| 29 | Enabled | 660 | ❌ |
| 30 | FillPatternColor | — | ❌ |
| 31 | FillingDirection | — | ❌ |
| 32 | FirstGradientColor | — | ❌ |
| 33 | FirstGradientOffset | — | ❌ |
| 34 | FitToLargest | — | ❌ |
| 35 | Flashing | — | ❌ |
| 36 | FlashingColorOff | — | ❌ |
| 37 | FlashingColorOn | — | ❌ |
| 38 | FlashingEnabled | — | ❌ |
| 39 | FlashingRate | 699 | ❌ |
| 40 | **FocusColor** | **703** | ❌ |
| 41 | **FocusWidth** | **704** | ❌ |
| 42 | Font | — | ❌ |
| 43 | FontBold | 708 | ❌ |
| 44 | FontItalic | 708 | ❌ |
| 45 | FontName | 709 | ❌ |
| 46 | FontSize | 711 | ✅ `fontSize` |
| 47 | FontUnderline | — | ❌ |
| 48 | ForeColor | 712 | ✅ `foreground` |
| 49 | Height | 720 | ✅ (layout) |
| 50 | HelpText | 724 | ❌ |
| 51 | HorizontalAlignment | — | ❌ |
| 52 | **Hotkey** | — | ❌（"No access in runtime"——只编辑期可设置）|
| 53 | Layer | 764 | ❌ |
| 54 | Left | 770 | ✅ (layout) |
| 55 | LineEndShapeStyle | — | ❌ |
| 56 | Location | — | ❌ |
| 57 | MiddleGradientColor | — | ❌ |
| 58 | **Mode** | **824** | ❌（Push/Toggle 类型）|
| 59 | Name | — | ✅ (layout) |
| 60 | **Off** | **1161** | ❌ |
| 61 | **On** | **1161** | ❌ |
| 62 | PictureAlignment | — | ❌ |
| 63 | PictureAreaLeftMargin | — | ❌ |
| 64 | PictureAreaRightMargin | — | ❌ |
| 65 | PictureAreaTopMargin | — | ❌ |
| 66 | PictureAutoSizing | — | ❌ |
| 67 | PictureList | — | ❌ |
| 68 | **PictureOff** | **866** | ❌ |
| 69 | **PictureOn** | **867** | ❌ |
| 70 | **Pressed** | **872** | ❌ |
| 71 | ProcessValue | 874 | ❌（Button 也可绑 Tag）|
| 72 | RelativeFillLevel | — | ❌ |
| 73 | SecondGradientColor | — | ❌ |
| 74 | SecondGradientOffset | — | ❌ |
| 75 | **ShowFillLevel** | **933** | ❌ |
| 76 | Size | — | ✅ (layout) |
| 77 | StyleItem | — | ❌ |
| 78 | StyleSettings | 982 | ❌ |
| 79 | TabIndex | — | ❌ |
| 80 | TabIndexAlpha | — | ❌ |
| 81 | TextAreaBottomMargin | — | ❌ |
| 82 | TextAreaLeftMargin | — | ❌ |
| 83 | **TextOff** | **1002** | ❌ |
| 84 | **TextOn** | **1003** | ❌ |
| 85 | TextOrientation | — | ❌ |
| 86 | **Toggle** | **1052** | ❌ |
| 87 | ToolTipText | 1082 | ❌ |
| 88 | Top | 1083 | ✅ (layout) |
| 89 | Transparency | 1087 | ❌ |
| 90 | UseDesignColorSchema | — | ❌ |
| 91 | UseFirstGradient | — | ❌ |
| 92 | UseSecondGradient | — | ❌ |
| 93 | UseTwoHandOperation | — | ❌ |
| 94 | VerticalAlignment | — | ❌ |
| 95 | Visible | 1204 | ❌ |
| 96 | Width | 1217 | ✅ (layout) |
| 97 | WindowsStyle | 1224 | ❌ |

ApexHMI 已实现：`text` / `pressedBackground` 在 WinCC 中对应 `Pressed`（按下状态）。

---

## 关键字段详细说明

### Mode + Toggle (Page 824, 1052) — ❌ 双状态按钮缺失

WinCC Button 支持 4 种 Mode（Push/Toggle/...）+ Toggle 属性。Toggle=true 时按一次保持按下、再按一次弹起。**ApexHMI 当前 button 只能 Push 模式**（无双稳态）。Switch widget 单独覆盖了双稳态，但语义割裂。

### TextOn / TextOff (Page 1003 / 1002) — ❌ 双态文本

按下/弹起时显示不同文字（如 "启动"/"运行中"）。ApexHMI 仅有 `text`（单一文本）。

### PictureOn / PictureOff (Page 867 / 866) — ❌ 双态图标

按下/弹起时显示不同图标。

### Pressed (Page 872) — ❌

读出按钮当前是否处于按下状态（动态属性，可绑 Tag）。
**ApexHMI 当前 `pressedBackground` 仅是视觉，无 Pressed 状态可读**。

### On / Off (Page 1161) — ❌ ProcessValue 联动

Button 可绑 Tag 写入 On/Off 值。ApexHMI 当前 button 通过 `onClick` 事件触发系统函数，**没有直接 Tag 绑定**——必须通过事件链 + WriteVariable 实现，繁琐。

### Hotkey — ❌（编辑期）

PDF 标注 "No access in runtime"——只在 WinCC Editor 中设置。但 ApexHMI 当前完全没有键盘快捷键绑定。

### FocusColor (Page 703) / FocusWidth (Page 704) — ❌

焦点描边色/宽度（触摸屏关键反馈）。

### ShowFillLevel + FillingDirection + RelativeFillLevel (Page 933) — ❌ 进度按钮

按钮可同时充当进度条：内部填充进度（FillingDirection 决定填充方向，RelativeFillLevel 0-100%）。**很特别的功能** —— 例如"启动加热中... 35%"按钮。

### StyleSettings (Page 982) — ❌ 样式预设

预设视觉风格（材质化按钮）。

### 渐变系列（FirstGradient/SecondGradient/MiddleGradient/UseFirstGradient/UseSecondGradient）— ❌

按钮背景渐变。ApexHMI 当前是纯色。

---

## 总结：button 缺失分级

### 🔴 严重缺失
1. **Mode + Toggle**（双稳态按钮——ApexHMI 必须依赖 switch widget，UI 割裂）
2. **On / Off / ProcessValue**（按钮直接绑 Tag，不必走事件链）
3. **TextOn / TextOff / PictureOn / PictureOff**（双态显示）
4. **Pressed**（按下状态可读）
5. **Authorization**
6. **FocusColor / FocusWidth**（焦点反馈）
7. **Enabled / Visible**

### 🟡 中度
8. **Hotkey**（虽然 PDF 标 "No access in runtime"，但编辑期可设置）
9. **HelpText / ToolTipText**
10. **ShowFillLevel + RelativeFillLevel + FillingDirection**（进度按钮）
11. **PictureAlignment / PictureArea*Margin**（图标布局）
12. **HorizontalAlignment / VerticalAlignment**
13. **字体系列**：FontBold/Italic/Underline/Name

### 🟢 装饰
14. 渐变背景（5 个字段）
15. Flashing 全套
16. 3D 边框系列

## 与 ApexHMI 现有 `BuildButton()` 对照（line 537-551）

| ApexHMI | WinCC | 评估 |
|---|---|---|
| text | TextOn / TextOff (合一) | ⚠️ 缺双态 |
| fontSize | FontSize (p711) | ✅ |
| foreground | ForeColor (p712) | ✅ |
| background | BackColor (p526) | ✅ |
| borderColor | BorderColor (p562) | ✅ |
| borderWidth | BorderWidth (p577) | ✅ |
| cornerRadius | CornerRadius | ✅ |
| pressedBackground | Pressed 状态视觉 | ⚠️ 仅视觉无状态读 |

**结论**：button 视觉相关已不错；但**功能性大缺失**：缺双稳态、缺 Tag 直接绑定、缺双态文本/图标。
