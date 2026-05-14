# io-graphic / WinCC ScreenItem: **GraphicIOField**

**来源**：WinCC ProgRef V18，**Table 1-44 Properties**，PDF Page 326 起。VBS Type identifier = `HMIGraphicIOField`。

**统计**：WinCC GraphicIOField **51 个属性**；ApexHMI io-graphic 现有 **4 个属性**；覆盖率 **7.8%**。

---

## 完整属性表

| # | 属性名 | Page | ApexHMI 现状 |
|---|---|---|---|
| 1 | Authorization | 517 | ❌ |
| 2 | BackColor | 526 | ❌ |
| 3 | BorderBackColor | 558 | ❌ |
| 4 | BorderColor | 562 | ❌ |
| 5 | BorderFlashingColorOff | 566 | ❌ |
| 6 | BorderFlashingColorOn | 567 | ❌ |
| 7 | BorderFlashingEnabled | 569 | ❌ |
| 8 | BorderStyle | 576 | ❌ |
| 9 | BorderWidth | 577 | ❌ |
| 10 | Color | 1153 | ❌ |
| 11 | CornerStyle | 635 | ❌ |
| 12 | DrawInsideFrame | 655 | ❌ |
| 13 | EdgeStyle | 657 | ❌ |
| 14 | Enabled | 660 | ❌ |
| 15 | FitToLargest | — | ❌ |
| 16 | FlashTransparentColor | — | ❌ |
| 17 | Flashing | — | ❌ |
| 18 | FlashingEnabled | — | ❌ |
| 19 | FlashingRate | 699 | ❌ |
| 20 | FocusColor | 703 | ❌ |
| 21 | FocusWidth | 704 | ❌ |
| 22 | Height | 720 | ✅ (layout) |
| 23 | HelpText | 724 | ❌ |
| 24 | IsImageMiddleAligned | — | ❌ |
| 25 | Layer | 764 | ❌ |
| 26 | Left | 770 | ✅ (layout) |
| 27 | LineEndShapeStyle | — | ❌ |
| 28 | Location | — | ❌ |
| 29 | Mode | — | ✅ `mode` |
| 30 | Name | — | ✅ (layout) |
| 31 | OnValue | — | ❌ |
| 32 | **PictureList** | — | ⚠️ `entries`（GraphicListRef）|
| 33 | **PictureOff** | **866** | ❌ |
| 34 | **PictureOn** | **867** | ❌ |
| 35 | PictureRotation | — | ❌ |
| 36 | **ProcessValue** | **874** | ✅ `variable` |
| 37 | ScrollBarOrientation | — | ❌ |
| 38 | ShowScrollBar | — | ❌ |
| 39 | Size | — | ✅ (layout) |
| 40 | TabIndex | — | ❌ |
| 41 | TabIndexAlpha | — | ❌ |
| 42 | ToolTipText | 1082 | ❌ |
| 43 | Top | 1083 | ✅ (layout) |
| 44 | Transparency | 1087 | ❌ |
| 45 | TransparentColor | — | ❌ |
| 46 | UseTransparentColor | — | ❌ |
| 47 | Visible | 1204 | ❌ |
| 48 | Width | 1217 | ✅ (layout) |
| 49 | Activate (方法) | 1250 | — |
| 50 | ActivateDynamic (方法) | 1252 | — |
| 51 | DeactivateDynamic (方法) | 1259 | — |

---

## 关键字段详细说明

### PictureList — ⚠️ 部分对应 ApexHMI `entries`

ApexHMI `entries` 已支持 `{graphicList:xxx}` 或 `value=path;...` 语法，**对应 WinCC PictureList**（图形列表引用）+ Assignments（KV 映射）。语义一致但表达方式合并。

### PictureOff (Page 866) / PictureOn (Page 867) — ❌ 缺失

**双状态显式图片**（专用于 Bit 模式）：value=0 显示 PictureOff，value=1 显示 PictureOn。比 PictureList 更轻量。ApexHMI 当前未提供"双图片快捷字段"，必须写 entries=`0=off.png;1=on.png`。

### Mode — ✅ 一致（Input/Output/InputOutput）

### ProcessValue (Page 874) — ✅ ApexHMI `variable`

### PictureRotation — ❌ 缺失

图片旋转角度（绑定到变量可做指针类动画）。ApexHMI 静态控件不支持图片旋转。

### TransparentColor / UseTransparentColor — ❌ 缺失

PNG 等图片中指定某色为透明背景（GIF 时代风格）。**低优先**。

### FocusColor / FocusWidth (Page 703/704) — ❌ 缺失

控件获得焦点时的描边色和宽度。**触摸屏场景重要**——操作员需明确知道当前选中哪个控件。

---

## 总结：io-graphic 缺失分级

### 🔴 严重缺失
1. **PictureOff / PictureOn**（双状态图片快捷字段）
2. **PictureRotation**（图片旋转——指针类动画的基础）
3. **Authorization**（权限）
4. **ShowBadTagState** 等同效——当变量品质坏时显示替代图片

### 🟡 中度
5. **FocusColor / FocusWidth**（焦点反馈，触摸屏关键）
6. **BorderColor / BorderWidth / BorderStyle**
7. **Transparency**
8. **Enabled / Visible**
9. **IsImageMiddleAligned**（图片对齐方式）

### 🟢 装饰
10. Flashing 全套
11. **TransparentColor / UseTransparentColor**

## 与 ApexHMI 现有 `BuildIoGraphic()` 对照（line 134-152）

| ApexHMI 字段 | WinCC | 评估 |
|---|---|---|
| variable | ProcessValue (p874) | ✅ |
| mode | Mode | ✅ |
| entries (GraphicListRef) | PictureList + Assignments | ✅ |
| stretch (None/Fill/Uniform/UniformToFill) | — | ⚠️ ApexHMI 独有，WinCC 由 `IsImageMiddleAligned` + 图片本身决定 |

**结论**：io-graphic 是 ApexHMI 与 WinCC 语义最接近的 widget 之一。但缺**双状态快捷字段**（PictureOn/Off）和**旋转角度**绑定。
