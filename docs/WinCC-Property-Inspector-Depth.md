# WinCC 属性面板（Inspector）字段深度调研 + ApexHMI 覆盖率审计 + 补充开发计划

> 文档版本：2026-05-13 · 作者：Claude（Opus 4.7）· 工时投入：约 2.5 小时（WinCC Inspector 字段调研 ~80 min、ApexHMI Schema 代码逐控件审计 ~35 min、计划编写 ~40 min）
>
> **范围**：本文聚焦"Inspector 面板字段清单深度"——即工程师双击某个控件后弹出的 Properties / Animations / Events / Texts 四 Tab 中，**每个 Category 下到底有哪些字段**。
>
> **与 `WinCC-Deep-Audit.md` 的边界**：
> - 该文档：WinCC **运行时行为**（如 IO Field 格式串 `9` vs `0`、PLC 协调位、Alarm Window quality code 等）
> - 本文档：WinCC **组态时 Inspector 面板可见字段**（编辑器里有几个 Category、每个 Category 列了几个字段、字段类型）
>
> **来源**：Siemens TIA Portal V18 Online Help（docs.tia.siemens.cloud）、WinCC RT Advanced/Unified Engineering Manual PDF（V15.1 / V16 / V17 / V18 系列）、SiePortal 论坛实战截图、SolisPLC/DMC/PLCSPACE 等社区教程 Inspector 截图。
>
> **图例**：🔴 高（操作员看不到设计细节 / 工程师无法定制） · 🟡 中（常用但有 workaround） · ⚪ 低（罕用增强）

---

## 第一部分：WinCC Inspector 面板通用结构

### 1.1 四 Tab 总体布局

WinCC TIA Portal V18 Comfort/Advanced/Unified 编辑器中，选中任意 Screen Object 后底部 Inspector 面板出现 4 个 Tab：

| Tab | 中文 | 子节点（Category） | 作用 |
|---|---|---|---|
| Properties | 属性 | 见 §1.2 | 静态外观 + 行为参数 |
| Animations | 动画 | 见 §1.3 | 数据驱动的运行时属性变化 |
| Events | 事件 | 见 §1.4 | 用户/系统事件 → 函数列表 |
| Texts | 文本 | 见 §1.5 | 多语言字符串本地化 |

### 1.2 Properties Tab 通用 Category 体系

下表列出 WinCC 各控件 Inspector 中**高频出现**的 Category 及其典型字段。并非所有 Category 都出现在每个控件上——这是 WinCC 字段超 50 个的来源。

| Category | 中文 | 典型字段（按控件子集出现） | 出现频次 |
|---|---|---|---|
| **General** | 常规 | Mode（输入/输出/输入输出/双向）、Process value（TagAddress）、Type（十进制/二进制/十六进制/字符串）、Format pattern、Decimal places、Field length、Hidden input、Substitute value、Process tag、Min/Max | I/O 类、可编辑控件 |
| **Appearance** | 外观 | Background color、Foreground color、Fill pattern（实色/透明/横线/斜线/网格/垂直渐变/水平渐变/8 种 Hatch）、Corner radius、Opacity、Cursor control、Hover color | 几乎所有 |
| **Fill style** | 填充样式 | Fill type（None/Solid/Pattern/Hatch/Gradient）、Pattern color、Pattern background、Gradient start/end color、Gradient direction | 矩形/椭圆/多边形/Bar |
| **Design** | 设计 | Style/Skin（继承项目主题）、Color scheme name、Override skin (bool) | Unified 全部 |
| **Layout** | 布局 | Position X、Position Y、Width、Height、Fit to content (bool)、Use object size (bool)、Adapt size to content、Margin top/bottom/left/right | 所有 |
| **Text format** | 文本格式 | Font family、Font size、Font weight（Normal/Bold/SemiBold/Light）、Italic、Underline、Strikethrough、Horizontal alignment（Left/Center/Right）、Vertical alignment（Top/Middle/Bottom）、Word wrap、Text orientation（0/90/180/270°）、Auto-size、Margin (4 个) | 文本/IO/按钮/Table |
| **Text elements** | 文本元素 | Text on / Text off / Text disabled / Caption（多状态文本，每状态对应独立子节） | Button/Switch/Checkbox |
| **Limits** | 限值 | High limit (4 级)、Low limit (4 级)、High range color、Low range color、Substitute value、Limit type（绝对值/百分比/Tag） | IO Field / Bar / Gauge / Slider |
| **Style/Design** | 样式/设计 | Style name、Bar pattern（Solid/Pattern/Gradient/Glass/3D）、3D effect、Frame width、Frame style（None/Solid/Inset/Outset/Sunken/Raised） | Bar / Gauge / Button |
| **Miscellaneous** | 其他 | Object name、Object ID（只读）、Tooltip text、Layer (0-31)、Active layer (bool)、Tab order/TabIndex、Display name、Group name | 所有 |
| **Security** | 安全 | Authorization (Enum: 权限组)、Display only when authorized (bool)、Operator control enable (bool)、Visibility while no rights (Enum) | 可操作控件 |
| **Flashing** | 闪烁 | Flashing（None/Standard/Strong）、Background color when flashing、Foreground color when flashing、Border color when flashing、Flashing frequency（Slow/Medium/Fast）、Flashing in idle state (bool) | 几乎所有 |
| **Format** | 格式 | Format pattern（如 `999.99` 或 .NET `0.##`）、Decimal places、Thousands separator、Negative sign position、Trailing zero、Numeric system（Decimal/Binary/Hex/Octal）、Exponential notation | IO Numeric / DateTime / Clock |
| **Text list** | 文本列表 | Text list (TextListRef)、Value/Text 条目编辑器、Default text、Default value | IO Symbolic / Combobox / Listbox |
| **Border** | 边框 | Border weight (0-10)、Border style（实线/虚线/点线/双线/Inset/Outset/Sunken/Raised）、Border color、Border background color、3D effect (bool)、Corner radius (Border 专属) | 矩形/IO/按钮/Table |
| **States** | 状态 | State definitions（每状态独立 BG/FG/Text/Border 颜色配置块）、State count（2~10）、Default state | Switch / Button / Symbol |
| **Bar / Scale** | 标尺 | Scale type（None/Simple/Continuous/Step）、Tick count、Tick interval、Major tick length、Minor tick length、Label division、Logarithmic scaling、Scale alignment（Left/Right/Both） | Bar / Gauge / Slider |
| **Pointer** | 指针 | Pointer color、Pointer width、Pointer style（Triangle/Needle/Arrow/Line）、Pointer center color、Pointer shadow | Gauge |
| **Toolbar** | 工具栏 | Show toolbar、Toolbar buttons（多选列表）、Toolbar position（Top/Bottom/Left/Right）、Toolbar background | TrendView / AlarmView / TableView |
| **Columns** | 列 | Column configuration（每列：Header / Field / Width / Visible / Sortable / Align / Format）、Column count、Default sort column、Default sort direction | Table/Alarm/Recipe/User |
| **Time axis** | 时间轴 | Time range、Time format、Time axis label、Tick distance、Display gap behavior、Now-line color | TrendView |
| **Value axis** | 数值轴 | Y min、Y max、Y label、Y scale type（Linear/Log）、Y tick count、Gridline color、Auto-range | TrendView / Bar / Gauge / XY |

### 1.3 Animations Tab 通用类型

WinCC 把动画分为两大族，**每族下面又细分多种**。每个动画类型可以独立 Add 一份（一个控件最多挂十几条动画）：

| 动画族 | 类型 | 字段 |
|---|---|---|
| **外观 (Appearance)** | Appearance animation | Tag、Range list（每行 From/To 值范围 → BG color / FG color / Flashing / 闪烁色），多行表格 |
| | Visibility | Tag、Range（From/To）+ Visible/Invisible 二选一、Object accessible (bool) |
| | Diagonal movement | 动态字体 / 缩放 / 旋转（Unified 特有） |
| **移动 (Movement)** | Direct movement | Tag X、Tag Y（数据驱动绝对坐标） |
| | Horizontal movement | Tag、Range From/To、Pixel offset From/To |
| | Vertical movement | Tag、Range From/To、Pixel offset From/To |
| | Diagonal movement | Tag X + Tag Y + range + 起止像素 |
| **控件级专属** | TrendView animation | Tag → 高亮 trace / 切换 X-axis 时间窗口 |
| | Bar animation | Tag → 切换 fill pattern |

### 1.4 Events Tab 通用事件列表

每个事件下可以挂 **0~N 个系统函数 + 用户脚本调用**，组成函数列表。事件清单（按控件分组）：

| 通用事件 | 触发时机 | 主要控件 |
|---|---|---|
| Click | 鼠标单击/触屏单击 | Button / 几何对象 / Symbol |
| Press | 鼠标按下 | Button / Switch |
| Release | 鼠标释放 | Button / Switch |
| Activate | 控件获得焦点 | 所有可交互 |
| Deactivate | 控件失去焦点 | 所有可交互 |
| Value changed | 值变化（编辑提交后） | IO Field / Slider / Combobox / Checkbox |
| Switch on / Switch off | 状态切换 | Switch / Checkbox |
| Toggle | 状态翻转 | Switch / Checkbox |
| Activate screen object | 屏幕对象激活 | Screen Window |
| Mouse Click / Press / Release | 鼠标专属 | 所有 |
| Loop while pressed | 按住循环 | Button |
| Time interval | 定时（Cyclic continuous / Cyclic on operation） | Screen-level 仅 |
| Loaded / Cleared | 画面加载 / 卸载 | Screen-level 仅 |
| Tag value changed | Tag 值变 → 函数 | 全局 |

### 1.5 Texts Tab

只列出**控件中所有出现 String 类字段**的多语言版本：
- Tooltip text (Multi-language)
- Caption / Text on / Text off / Text disabled
- X label / Y label
- Header texts (Table 每列)
- Substitute value (字符串模式)
- Static text (Text 控件本身)

---

## 第二部分：每个控件的完整字段清单（27 个 widget）

> 字段标注约定：字段名 / 类型 / 默认值 / 含义。
> 类型缩写：S=String、N=Number、I=Integer、B=Boolean、C=Color、E=Enum、T=TagAddress、PR=PageRoute、TL=TextListRef、GL=GraphicListRef、J=Json、ML=Multi-language。

---

### 1) text / WinCC 对应：Text Field（静态文本域）

**Properties Tab**

#### 常规 (General)
- Text / S / "文本" / 显示内容
- Adapt to size / B / false / 文本框尺寸是否随内容自适应

#### 外观 (Appearance)
- Background color / C / Transparent
- Foreground color / C / #0F172A
- Fill pattern / E / Solid（None/Solid/Pattern/Hatch/Gradient）
- Background fill color / C / #FFFFFF
- Hatch style / E / None（8 种）
- Transparent background / B / true

#### 文本格式 (Text format)
- Font family / S / Tahoma
- Font size / N / 14
- Bold / B / false
- Italic / B / false
- Underline / B / false
- Strikethrough / B / false
- Horizontal alignment / E / Left（Left/Center/Right）
- Vertical alignment / E / Middle（Top/Middle/Bottom）
- Word wrap / B / false
- Text orientation / E / 0°（0/90/180/270）
- Margin top / I / 0
- Margin bottom / I / 0
- Margin left / I / 2
- Margin right / I / 2

#### 边框 (Border)
- Border weight / I / 0
- Border style / E / Solid（None/Solid/Dashed/Dotted/Double）
- Border color / C / #000000
- Border background color / C / Transparent
- 3D effect / B / false
- Corner radius / I / 0

#### 布局 (Layout)
- Position X / I / 0
- Position Y / I / 0
- Width / I / 100
- Height / I / 24

#### 闪烁 (Flashing)
- Flashing / E / None（None/Standard/Strong）
- Background color when flashing / C
- Foreground color when flashing / C
- Flashing frequency / E / Medium（Slow/Medium/Fast）

#### 其他 (Miscellaneous)
- Object name / S / Text_1
- Tooltip text / ML / ""
- Layer / I / 0
- Tab order / I / 0
- Display name / S

#### 安全 (Security)
- Authorization / E / None
- Display only when authorized / B / false
- Visibility while no rights / E / Disabled

**Animations Tab**：Appearance / Visibility / Horizontal / Vertical / Diagonal / Direct movement
**Events Tab**：Click / Press / Release / Activate / Deactivate / Mouse Click/Press/Release
**Texts Tab**：Text、Tooltip 多语言

**ApexHMI 当前覆盖**：7 / 约 45（**16%**）
**关键缺失**：边框（6 项）、文本格式（10 项：family/italic/underline/wrap/orientation/4 个 margin/strike）、闪烁（4 项）、安全（3 项）、其他（5 项）、填充样式（5 项）。
**严重程度**：🔴 高

---

### 2) rectangle / WinCC 对应：Rectangle

**Properties Tab**

#### 外观 (Appearance) + 填充样式 (Fill style)
- Background color / C / #3B82F6
- Foreground color (Pattern) / C
- Fill pattern / E / Solid（None/Solid/8 种 Hatch/Gradient horizontal/Gradient vertical/Gradient radial）
- Gradient start color / C
- Gradient end color / C
- Gradient direction / E / Top→Bottom
- Hatch density / N / 50%
- Filling direction / E / Vertical up（用于动态填充百分比）
- Fill level / N / 100（0~100；动态填充比例）
- Opacity / N / 1

#### 边框 (Border)
- Border weight / I / 1
- Border style / E / Solid
- Border color / C / #1E40AF
- Border background color / C
- 3D effect / B / false
- Corner radius X / I / 0
- Corner radius Y / I / 0

#### 布局 (Layout)
- Position X / Y / Width / Height
- Rotation / N / 0

#### 闪烁 (Flashing) — 同 §1
#### 其他 (Miscellaneous) — 同 §1
#### 安全 (Security) — 同 §1

**Animations**：Appearance / Visibility / 4 类移动
**Events**：Click / Press / Release / Mouse 三件
**Texts**：Tooltip

**ApexHMI 当前覆盖**：5 / 约 30（**17%**）
**关键缺失**：填充样式 6 项（pattern/gradient/hatch）、Fill level（动态填充）、边框 5 项、Rotation、闪烁 4 项、安全 3 项。
**严重程度**：🔴 高

---

### 3) ellipse / WinCC 对应：Ellipse

字段与 Rectangle 类似，去掉 Corner radius，加上：
- Start angle / N / 0（用于扇形截断）
- End angle / N / 360
- Radius X / I
- Radius Y / I

#### 外观 / 填充样式 / 边框 / 闪烁 / 其他 / 安全 — 同 Rectangle

**ApexHMI 当前覆盖**：4 / 约 28（**14%**）
**严重程度**：🔴 高

---

### 4) line / WinCC 对应：Line

#### 外观 (Appearance)
- Stroke color / C / #1F2937
- Stroke weight / I / 2
- Line style / E / Solid（Solid/Dashed/Dotted/DashDot/DashDotDot）
- Line cap / E / Flat（Flat/Round/Square）
- Begin style / E / None（None/Arrow/Filled/Open）
- End style / E / None
- Begin width / I / 6
- End width / I / 6
- Opacity / N / 1

#### 布局
- Start X / Y / End X / Y / Rotation
- X1/Y1/X2/Y2

#### 闪烁 / 其他 / 安全 — 通用

**Animations**：Appearance / Visibility / 4 类移动
**Events**：Mouse Click / Press / Release

**ApexHMI 当前覆盖**：7 / 约 22（**32%**）
**关键缺失**：Line style（5 选项）、Line cap、箭头 4 项、闪烁、安全。
**严重程度**：🟡 中

---

### 5) polyline / WinCC 对应：Polyline

字段与 Line 类似，多一个 Points 数组编辑器。
- Points / S（坐标对列表）
- Stroke / Stroke weight / Line style / Begin style / End style / Begin width / End width / Opacity

**ApexHMI 当前覆盖**：5 / 约 22（**23%**）
**严重程度**：🟡 中

---

### 6) polygon / WinCC 对应：Polygon

封闭多边形 = Polyline + Fill。
- Points
- Fill / Fill pattern / Hatch / Gradient（同 Rectangle）
- Stroke / Stroke weight / Line style
- Opacity / Rotation

**ApexHMI 当前覆盖**：6 / 约 26（**23%**）
**严重程度**：🟡 中

---

### 7) graphic-view / WinCC 对应：Graphic View（图形视图）

#### 常规
- Graphic / S / ""（图形集引用 或 静态文件）
- Background graphic / S
- Adapt graphic / B / true
- Fit graphic to object size / B / true
- Stretch mode / E / Uniform（None/Fill/Uniform/UniformToFill）

#### 外观
- Background color / C / Transparent
- Transparent color / C（用于实现透明图）
- Use transparent color / B / false
- Opacity / N / 1

#### 边框 / 布局 / 闪烁 / 其他 / 安全 — 通用

**Animations**：Appearance / Visibility / 4 移动 / **Graphic exchange**（运行时根据 Tag 切换图）
**Events**：Click / Mouse 三件

**ApexHMI 当前覆盖**：3 / 约 24（**13%**）
**严重程度**：🔴 高（**透明色、Graphic exchange 动画完全缺失**）

---

### 8) io-numeric / WinCC 对应：IO Field（数字 I/O 域）

**Properties Tab**

#### 常规 (General)
- Mode / E / Output（Input/Output/Two-sided/Bidirectional）
- Process value / T
- Type / E / Decimal（Decimal/Binary/Hexadecimal/String）
- Format pattern / S / "999.99"（占位符 `9`/`0`）
- Decimal places / I / 2（0~12）
- Field length / I / 6（1~40）
- Hidden input / B / false（密码风格）
- Substitute value / S / ""
- Apply on EXIT / B / true

#### 外观 (Appearance)
- Background color / C / #FFFFFF
- Foreground color / C / #0F172A
- Fill pattern / E / Solid
- Corner radius / I / 0
- Cursor control / B / false

#### 边框 (Border)
- Border weight / I / 1
- Border style / E / Solid
- Border color / C / #CBD5E1
- Border background color / C / Transparent
- 3D effect / B / true

#### 布局 (Layout)
- X / Y / Width / Height / Fit to content / Tab order

#### 文本格式 (Text format)
- Font family / Font size / Bold / Italic / Underline / Strikethrough
- Horizontal alignment / E / Right
- Vertical alignment / E / Middle
- Margin top/bottom/left/right

#### 限值 (Limits)
- High limit value / N
- High limit color / C / #DC2626
- High range color / C
- Low limit value / N
- Low limit color / C / #2563EB
- Substitute value when overshoot / S

#### 闪烁 (Flashing) — 通用 4 项

#### 其他 (Miscellaneous)
- Object name / Object ID / Tooltip / Layer / Tab order

#### 安全 (Security)
- Authorization / E
- Display only when authorized / B
- Operator control enable / B
- Enable acoustic signal / B

**Animations**：Appearance / Visibility / Movement / **Output value animation**
**Events**：Click / Press / Release / Activate / Deactivate / Input finished / Value changed / Switch / Enter / Esc
**Texts**：Tooltip / Substitute value

**ApexHMI 当前覆盖**：11 / 约 57（**19%**）
**关键缺失**：Type（4 选项）、Field length、Hidden input、Substitute value、边框 5 项、文本格式 10 项、限值色 4 项、闪烁 4 项、安全 4 项、其他 4 项。
**严重程度**：🔴 高

---

### 9) io-symbolic / WinCC 对应：Symbolic IO Field

#### 常规
- Mode / E / Output（Input/Output/Two-sided）
- Process value / T
- Text list / TL
- Number of visible items / I / 3
- Drop-down list direction / E / Below
- Apply on EXIT / B / true

#### 外观 / 边框 / 文本格式 / 闪烁 / 其他 / 安全 — 通用

#### 文本列表 (Text list)
- Default text / S
- Default value / I
- Items（编辑器）

**ApexHMI 当前覆盖**：5 / 约 38（**13%**）
**严重程度**：🔴 高

---

### 10) io-graphic / WinCC 对应：Graphic IO Field

字段与 io-symbolic 类似，但 Text list → Graphic list（GL）。
- Mode、Process value、Graphic list / GL、Default graphic、Default value、Fit to size、Stretch mode

**ApexHMI 当前覆盖**：4 / 约 30（**13%**）
**严重程度**：🔴 高

---

### 11) datetime / WinCC 对应：Date/Time Field

#### 常规
- Mode / E / SystemTime（SystemTime/Tag/Input）
- Process value / T
- Format / S / "yyyy-MM-dd HH:mm:ss"
- Long date format / B / false
- Display milliseconds / B / false
- Show time / B / true
- Show date / B / true
- Apply on EXIT / B / true

#### 外观 / 边框 / 文本格式 / 闪烁 / 其他 / 安全 — 通用

**Animations**：Appearance / Visibility / Movement
**Events**：Click / Activate / Deactivate / Value changed / Input finished

**ApexHMI 当前覆盖**：5 / 约 35（**14%**）
**严重程度**：🔴 高

---

### 12) button / WinCC 对应：Button

#### 常规
- Mode / E / Text（Text/Graphic/Both/Invisible）
- Hotkey / S / ""

#### 文本元素 (Text elements)
- Text on / ML / "按钮"
- Text off / ML / ""
- Text disabled / ML / ""
- Text pressed / ML / ""

#### 外观（每状态独立）
- Background color (Off/On/Pressed/Disabled/Hover) × 5
- Foreground color × 5
- Fill pattern × 5

#### 文本格式（同 §1.2）

#### 边框 + 3D
- Border weight、Style、Color、3D effect (3D Inset/Outset)

#### 布局 — 通用

#### 图形（Mode=Graphic 时）
- Graphic on / Graphic off / Graphic pressed / Graphic disabled
- Fit graphic to size / B
- Stretch / E

#### 闪烁 / 其他 / 安全
- Authorization / Operator control enable / Acoustic signal / Confirm operation / B

**Animations**：Appearance / Visibility / 4 移动
**Events**：Click / Press / Release / Activate / Deactivate / Loop while pressed / Mouse 三件 / Right click

**ApexHMI 当前覆盖**：8 / 约 50（**16%**）
**关键缺失**：Mode（Text/Graphic/Both）、Hotkey、4 状态文本、4 状态图形、Disabled/Hover 状态色、Loop while pressed、Confirm operation、安全 4 项。
**严重程度**：🔴 高

---

### 13) round-button / WinCC 对应：Round Button

字段 = Button - 边框 - 文本格式 + 圆形几何：
- Center color、Margin color、Bevel size、Bevel color light/dark
- Mode、Text on/off、Color on/off/pressed/disabled
- Hotkey、Operator control enable、Confirm

**ApexHMI 当前覆盖**：5 / 约 32（**16%**）
**严重程度**：🔴 高

---

### 14) switch / WinCC 对应：Switch

#### 常规
- Mode / E / bistable（bistable/momentary/invisible）
- Process value / T
- Type / E / SwitchText（SwitchText/SwitchGraphic/SwitchOff/SwitchOn）

#### 文本元素
- Text on / Text off / ML

#### 外观
- Background on / off / C
- Foreground on / off / C
- Slider color
- Pattern

#### 图形（Type=SwitchGraphic）
- Graphic on / Graphic off / Stretch

#### 布局 + 边框 + 闪烁 + 其他 + 安全 — 通用

**Animations**：Appearance / Visibility / Movement
**Events**：Switch on / Switch off / Toggle / Click / Press / Release / Activate / Deactivate

**ApexHMI 当前覆盖**：7 / 约 38（**18%**）
**严重程度**：🔴 高

---

### 15) bar / WinCC 对应：Bar

#### 常规
- Process tag / T
- Min value / N / 0
- Max value / N / 100
- Origin value / N / 0（双向棒图中线）
- Logarithmic scaling / B / false（仅 RT Pro）

#### 外观 + 填充
- Background color / C
- Bar pattern / E / Solid（Solid/Pattern/Gradient/Glass/3D）
- Bar color / C
- Gradient end color
- Border width / Color
- 3D effect / B / true

#### Bar / Scale (标尺)
- Scale type / E / Continuous（None/Simple/Continuous/Step）
- Scale alignment / E / Left（Left/Right/Both）
- Tick count / I / 10
- Sub-ticks / I / 5
- Large tick distance / N / 10
- Show scale labels / B / true
- Label color / C
- Label format / S

#### 限值（4 级）
- High alarm limit / High warning limit / Low warning limit / Low alarm limit / N
- High alarm color / High warning color / Normal color / Low warning color / Low alarm color / C
- Limit indication / E / Color change（Color/Marker/Both）
- Show limit lines / B / true
- Limit line style / E / Dashed

#### 方向
- Orientation / E / Vertical（Up/Down/Left/Right）
- Bar direction / E / Bottom-Up

#### 边框 / 文本格式（标签）/ 闪烁 / 其他 / 安全 — 通用

**Animations**：Appearance / Visibility / Movement / **Bar fill animation**
**Events**：Click / Mouse / Press / Release

**ApexHMI 当前覆盖**：12 / 约 55（**22%**）
**关键缺失**：Origin value（双向）、Bar pattern（5 种）、Scale type、3 个 sub-scale 字段、4 级限值（仅有 warn/alarm 两级）、限值线、3D 效果。
**严重程度**：🔴 高

---

### 16) gauge / WinCC 对应：Gauge

#### 常规
- Process tag / T
- Min / Max / Origin value
- Unit / Caption

#### 圆盘 (Disc)
- Disc background color
- Disc border color
- Center color
- Bezel color
- Bezel width
- Disc style / E（Round/Half/Quarter）

#### 指针 (Pointer)
- Pointer color / C
- Pointer width / I
- Pointer style / E（Triangle/Needle/Arrow/Line）
- Pointer center color
- Pointer shadow / B / true

#### 标尺 (Scale)
- Start angle / End angle / N
- Major tick count / Minor tick count / I
- Tick color / Tick label color
- Tick label format
- Scale radius factor

#### 警戒区间 (Limit zones)
- High alarm / High warning / Low warning / Low alarm
- High alarm color / High warning color / Normal color / Low warning color / Low alarm color
- Zone arc width / B / true

#### 标签
- Show value label / B / true
- Show min/max labels / B / true
- Show unit / B / true
- Font family / size / weight

#### 闪烁 / 其他 / 安全 — 通用

**ApexHMI 当前覆盖**：11 / 约 55（**20%**）
**关键缺失**：Disc 6 项、指针 4 项（width/style/center/shadow）、Start/End angle、警戒区间 4 级、标签 3 项。
**严重程度**：🔴 高

---

### 17) slider / WinCC 对应：Slider

#### 常规
- Process tag / T
- Min / Max value / Step size
- Continuous update / B / false（拖动即写）
- Apply on release / B / true

#### 外观
- Trough color
- Thumb color / Thumb size / Thumb style（Round/Square/Arrow）
- Active fill color
- 3D effect

#### 标尺（同 Bar）
- Scale type / Tick count / Tick distance / Show labels / Label format

#### 限值（同 Bar 4 级）

#### 方向 / 布局 / 文本格式（标签）/ 闪烁 / 其他 / 安全

**Events**：Value changed / Activate / Deactivate / Click / Press / Release

**ApexHMI 当前覆盖**：9 / 约 42（**21%**）
**严重程度**：🔴 高

---

### 18) scrollbar / WinCC 对应：Scroll Bar

类似 Slider 但简化。
- Process tag / Min / Max / Step / Page step / Orientation / Trough color / Thumb color / Button color / Show buttons / B

**ApexHMI 当前覆盖**：9 / 约 25（**36%**）
**严重程度**：🟡 中

---

### 19) clock / WinCC 对应：Clock（模拟/数字时钟）

#### 常规
- Mode / E / Digital（Digital/Analog）
- Use system time / B / true
- Tag / T（非系统时间时）
- Time zone

#### 数字时钟字段
- Format / S
- Show date / B / true
- Show time / B / true
- Long format / B / false

#### 模拟时钟字段
- Show seconds hand / B / true
- Hour hand color / N
- Minute hand color
- Second hand color
- Hand widths × 3
- Dial background
- Dial border
- Tick color / Tick label color
- Show numbers / B / true

#### 外观 / 文本格式 / 闪烁 / 其他 / 安全 — 通用

**ApexHMI 当前覆盖**：6 / 约 30（**20%**）
**严重程度**：🔴 高

---

### 20) combobox / WinCC 对应：Combo Box

#### 常规
- Process tag / T
- Text list / TL
- Number of visible items / I / 5
- Drop direction / E / Down
- Default text / Default value

#### 外观 / 边框 / 文本格式 / 闪烁 / 其他 / 安全 — 通用

**Events**：Value changed / Activate / Deactivate / Click

**ApexHMI 当前覆盖**：2 / 约 32（**6%**）
**严重程度**：🔴 高（**最严重之一**）

---

### 21) listbox / WinCC 对应：List Box

字段同 Combo Box，再加：
- Multi-select / B / false
- Selected color / C
- Item height / I
- Scroll bar visibility / E（Auto/Always/Never）

**ApexHMI 当前覆盖**：2 / 约 32（**6%**）
**严重程度**：🔴 高（**最严重**）

---

### 22) checkbox / WinCC 对应：Check Box

#### 常规
- Process tag / Number of boxes / I / 1
- Caption / ML
- Box style / E（Square/Round）
- Selected value / Unselected value

#### 外观（每状态）
- Checked color / Unchecked color / Disabled color
- Foreground color
- Check mark color

#### 文本格式 / 闪烁 / 其他 / 安全 — 通用

**Events**：Switch on / Switch off / Toggle / Value changed

**ApexHMI 当前覆盖**：5 / 约 30（**17%**）
**严重程度**：🔴 高

---

### 23) optiongroup / WinCC 对应：Option Group (Radio)

字段同 Checkbox，多：
- Number of options / I / 3
- Default option / I
- Options text list / TL
- Arrangement / E（Vertical/Horizontal/Grid）
- Option spacing / I

**ApexHMI 当前覆盖**：3 / 约 30（**10%**）
**严重程度**：🔴 高

---

### 24) trend-view / WinCC 对应：Trend View

#### 常规
- Trend list / J（每 trend：tag、color、line style、line width、marker style、Y-axis、visible）
- Mode / E（realtime/history）
- Time range / N
- Trigger tag / T

#### 外观
- Background / Foreground / Grid color / Title bar color
- Show title / Show legend / Show toolbar / Show ruler / Show status bar

#### 时间轴
- Time format / Tick distance / Time axis label / Now-line / Show gap

#### 数值轴（每轴）
- Y axis count / I / 1（最多 4）
- Y min / Y max（每轴）
- Y axis label / Y axis color / Linear/Log

#### 工具栏
- Toolbar buttons / J（多选：Start/Stop/Zoom/Pan/Ruler/Print/Export）
- Toolbar position / E

#### 限值
- Show high/low limit lines / B
- Limit line color

#### 其他 / 安全 — 通用

**ApexHMI 当前覆盖**：9 / 约 50（**18%**）
**严重程度**：🔴 高

---

### 25) alarm-view / WinCC 对应：Alarm View

#### 常规
- Display mode / E（Current/History/Filtered）
- Filter / J（status、class、priority、source、time range）
- Update mode / E（Automatic/Manual）
- Show button bar / B / true

#### 列配置
- Column list / J（每列：field、header、width、visible、sortable、align、format）
- Column order
- Sort column / Sort direction

#### 外观（每状态）
- Background / Foreground 普通
- BG/FG 来 / 走 / 确认 各 3 色（共 8 状态色）
- Row alternating color
- Selected row color

#### 文本格式 / 工具栏（Ack/Print/Export/Filter 等按钮）/ 其他 / 安全

**ApexHMI 当前覆盖**：5 / 约 45（**11%**）
**严重程度**：🔴 高

---

### 26) table-view / WinCC 对应：（自定义表格，约对应 Recipe View 表格部分）

#### 数据
- Data source / S
- Column list / J
- Row count / Max rows

#### 外观 / 文本格式 / 边框
- BG / Header BG / Header FG / Row alt / Selected row
- Show header / Show grid lines / Grid color
- Row height / I

#### 其他 / 安全 — 通用

**ApexHMI 当前覆盖**：5 / 约 28（**18%**）
**严重程度**：🔴 高

---

### 27) screen-window / WinCC 对应：Screen Window（嵌入画面）

#### 常规
- Screen name / PR
- Scroll bars / E（Auto/Always/Never）
- Adapt size / B / true
- Show borders / Show title bar
- Independent of Z order / B

#### 外观 / 边框 / 闪烁 / 其他 / 安全 — 通用

**ApexHMI 当前覆盖**：4 / 约 22（**18%**）
**严重程度**：🟡 中

---

### 28~32) 业务高级类（recipe-view / user-view / diagnostic-view / alarm-indicator / status-force）

以下控件 WinCC 中以 **复合控件 + 大量工具栏开关** 为主，每个常见 30-50 字段。

- **recipe-view**：Recipe selector、Dataset list、Field editor、Toolbar 10+ 按钮（New/Save/Save As/Delete/Synch/PLC Write/PLC Read/Import/Export/Rename）、Edit mode / B、Show row numbers、Column count、各列 width/visible、Field validation message、Lock during PLC transfer。当前 6 / 约 35（**17%**）🔴
- **user-view**：User list columns（Name/Group/Logon since/Password expiration）、Allow create/delete/edit、Show online users only、Locked accounts visible、Color per group。当前 4 / 约 25（**16%**）🔴
- **diagnostic-view**：Section visibility (4 个)、Refresh interval、Show CPU / Memory / Comm load、Detailed mode、Auto-clear errors。当前 5 / 约 18（**28%**）🟡
- **alarm-indicator**：Indicator graphic、Number display / B、Filter（class/priority/group）、Blink、Sound、Confirm on click、Auto-navigate page、Position relative-to-screen。当前 5 / 约 22（**23%**）🟡
- **status-force**：Tag list / column config、Read interval、Allow force / B、Confirm before force、Show quality column、History buffer、Format per tag。当前 4 / 约 20（**20%**）🟡

### 33~37) 媒体/分析（html-browser / pdf-view / media-player / xy-trend / report-view）

- **html-browser**：URL、Show toolbar、Show navigation buttons、Allow scripts / B、User agent、Cache mode、Cookie policy、Initial zoom、Encoding。当前 4 / 约 15（**27%**）🟡
- **pdf-view**：File path、Fit mode（Width/Page/Custom）、Show toolbar、Show thumbnails / Outline、Initial page、Allow print/copy、Encryption password。当前 3 / 约 14（**21%**）🟡
- **media-player**：Source、Auto play / Loop / Volume、Show controls、Aspect ratio / E、Buffer size、Subtitle path、Hardware acceleration、Play rate。当前 5 / 约 18（**28%**）🟡
- **xy-trend**：X/Y tags、Mode（Scatter/Line/Both）、Point style/size、Line style/width、X/Y min/max + autoscale、X/Y label/title、Grid、Legend、Max points、Trail length、Color、Limit lines。当前 11 / 约 36（**31%**）🟡
- **report-view**：Template id、Auto refresh / interval、Show toolbar、Page navigation、Print/Export button、Filter parameters / J、Page size、Margin、Orientation。当前 3 / 约 20（**15%**）🔴

---

## 第三部分：补充开发计划（属性面板深化）

### 3.1 当前覆盖率汇总

| Widget | 字段数 | WinCC 完整 | 覆盖率 | 严重 |
|---|---:|---:|---:|---|
| text | 7 | 45 | 16% | 🔴 |
| rectangle | 5 | 30 | 17% | 🔴 |
| ellipse | 4 | 28 | 14% | 🔴 |
| line | 7 | 22 | 32% | 🟡 |
| polyline | 5 | 22 | 23% | 🟡 |
| polygon | 6 | 26 | 23% | 🟡 |
| graphic-view | 3 | 24 | 13% | 🔴 |
| io-numeric | 11 | 57 | 19% | 🔴 |
| io-symbolic | 5 | 38 | 13% | 🔴 |
| io-graphic | 4 | 30 | 13% | 🔴 |
| datetime | 5 | 35 | 14% | 🔴 |
| button | 8 | 50 | 16% | 🔴 |
| round-button | 5 | 32 | 16% | 🔴 |
| switch | 7 | 38 | 18% | 🔴 |
| bar | 12 | 55 | 22% | 🔴 |
| gauge | 11 | 55 | 20% | 🔴 |
| slider | 9 | 42 | 21% | 🔴 |
| scrollbar | 9 | 25 | 36% | 🟡 |
| clock | 6 | 30 | 20% | 🔴 |
| combobox | 2 | 32 | **6%** | 🔴 |
| listbox | 2 | 32 | **6%** | 🔴 |
| checkbox | 5 | 30 | 17% | 🔴 |
| optiongroup | 3 | 30 | 10% | 🔴 |
| trend-view | 9 | 50 | 18% | 🔴 |
| alarm-view | 5 | 45 | 11% | 🔴 |
| table-view | 5 | 28 | 18% | 🔴 |
| screen-window | 4 | 22 | 18% | 🟡 |
| recipe-view | 6 | 35 | 17% | 🔴 |
| user-view | 4 | 25 | 16% | 🔴 |
| diagnostic-view | 5 | 18 | 28% | 🟡 |
| alarm-indicator | 5 | 22 | 23% | 🟡 |
| status-force | 4 | 20 | 20% | 🟡 |
| html-browser | 4 | 15 | 27% | 🟡 |
| pdf-view | 3 | 14 | 21% | 🟡 |
| media-player | 5 | 18 | 28% | 🟡 |
| xy-trend | 11 | 36 | 31% | 🟡 |
| report-view | 3 | 20 | 15% | 🔴 |

**总计**：当前 213 字段 / WinCC 完整 1153 字段 → **平均覆盖率 18.5%**。
（注：以 27 个核心 widget 计平均；含 P8/P9 业务/媒体类合计 37 个）

### 3.2 排前 5 最严重缺失（最优先补全）

1. **combobox / listbox（6%）** — 完全没有边框、文本格式、闪烁、安全，只有 variable + items 两条。常用业务必备。
2. **optiongroup（10%）** — 仅 3 个字段，缺所有状态色、行/列布局、Default option。
3. **alarm-view（11%）** — 状态色（来/走/确认 共 8 状态色）缺失；列字段类型支持极弱。
4. **graphic-view（13%）** — 缺透明色、Graphic exchange 动画、Fit/Stretch 模式不全。
5. **io-graphic / io-symbolic（13%）** — 默认值、Drop direction、可视项数、限值闪烁全缺。

### 3.3 排前 5 最值得补全的通用 Category

> **每补一个通用 Category，所有 37 个 widget 同时受益**，性价比最高。

1. **边框 (Border)** — 6 字段 × 37 widget = 222 个 propertyDescriptor 一次到位。
2. **文本格式 (Text format)** — 10 字段（family/italic/underline/strike/wrap/orientation/4 margin），适用文本/IO/按钮/Table 等 20+ widget。
3. **闪烁 (Flashing)** — 4 字段（flashing/BG flash/FG flash/frequency），适用所有可视控件，是 WinCC 操作员视觉报警核心。
4. **安全 (Security)** — 4 字段（authorization/visibility/operator enable/confirm operation），P10C 已规划但未深入。
5. **其他 (Miscellaneous)** — 4 字段（object name/object id 只读/tooltip/layer），其中 layer (0~31) 与 Z-order 控制相关，目前完全缺失。

### 3.4 分阶段开发计划

#### P7.5+1 — 通用 Category 补全（一次实现，全 widget 受益）

| 子项 | 标题 | 当前覆盖 | WinCC 完整字段数 | 推荐补全字段 | 工作量 | 优先级 |
|---|---|---|---|---|---|---|
| P7.5+1.1 | 边框 Category | 部分 widget 已有 borderColor/borderWidth | 6（weight/style/color/bg/3D/corner） | 全 6 项 | M（1 天） | 🔴 高 |
| P7.5+1.2 | 文本格式 Category | 仅 fontSize/textAlign | 10+ | family/italic/underline/strike/wrap/orientation/4 margin | M（1 天） | 🔴 高 |
| P7.5+1.3 | 闪烁 Category | 完全缺失 | 4 | flashing/BG flash/FG flash/frequency | S（0.5 天） | 🔴 高 |
| P7.5+1.4 | 安全 Category | 完全缺失 | 4 | authorization/visibility/operator enable/confirm | S（0.5 天） | 🔴 高 |
| P7.5+1.5 | 其他 Category | 部分有 tooltip | 5 | object name/object id 只读/tooltip ML/layer/tab order | S（0.5 天） | 🟡 中 |
| P7.5+1.6 | 填充样式 Category | 仅 fill 单色 | 8 | pattern/hatch 8 种/gradient start/end/方向/hatch density | M（1 天） | 🟡 中 |

合计：**约 4.5 天**，新增约 37 字段 × 30 widget = **~1100 PropertyDescriptor**，平均覆盖率可由 18.5% → **45%**。

#### P7.5+2 — 控件专属字段补全

| 控件 | 当前 | 推荐补全 | 工作量 | 优先级 |
|---|---|---|---|---|
| io-numeric | 11 | Type（Decimal/Bin/Hex/String 4 选）、Field length、Hidden input、Substitute value、Apply on EXIT、4 级限值色、Cursor control | M（1 天） | 🔴 |
| bar | 12 | Origin value（双向）、Bar pattern（5 种）、4 级限值、限值线显示、Scale type/alignment/sub-ticks、3D 效果 | L（2 天） | 🔴 |
| gauge | 11 | Disc style（圆/半圆/季圆）、Bezel color/width、4 级警戒区间、指针 style/width/center/shadow、Start/End angle、Sub-tick | L（2 天） | 🔴 |
| trend-view | 9 | 多 Y 轴（最多 4）、工具栏按钮多选、Ruler/Status bar、Limit lines、Trigger tag、Trace 字段扩展 | L（2 天） | 🔴 |
| alarm-view | 5 | 8 状态色（来/走/确认 各 3）、Filter 配置 J、Column visible/sortable/format、工具栏按钮多选 | L（2 天） | 🔴 |
| recipe-view | 6 | 工具栏按钮多选（10+）、Field validation message、Lock during transfer、Edit mode、Show row numbers | M（1 天） | 🔴 |
| button | 8 | Mode（Text/Graphic/Both/Invisible）、Hotkey、4 状态文本 ML、4 状态图形、Disabled/Hover 状态色、Loop while pressed、Confirm operation | M（1.5 天） | 🔴 |
| combobox/listbox | 2 | Number of visible items、Drop direction、Default text/value、Multi-select、Selected color、Item height | S（0.5 天 × 2） | 🔴 |
| io-symbolic/io-graphic | 5/4 | Default text/value、Drop direction、Apply on EXIT、Stretch（io-graphic）、Fit | S（0.5 天 × 2） | 🔴 |
| clock | 6 | 模拟时钟 6 项（hand color × 3/widths/dial/border/tick）、Time zone、Use system time | M（1 天） | 🟡 |
| checkbox/optiongroup | 5/3 | Number of boxes/options、Default option、Arrangement、Selected/Unselected color、Disabled color、Check mark color、Option spacing | S（0.5 天 × 2） | 🟡 |

合计：**约 16 天**，新增 ~350 字段，平均覆盖率 45% → **75%**。

#### P7.5+3 — 动画 / 事件 Tab 深化

当前 P2A/P2B 已实现：外观、可见性、4 类移动动画 + 事件多动作链。WinCC 仍有以下缺口：

| 子项 | 标题 | 当前 | 推荐补全 | 工作量 |
|---|---|---|---|---|
| P7.5+3.1 | Appearance 动画范围表 | 单色映射 | Range list（From/To 区间 + BG/FG/Flashing 各行表格） | M（1 天） |
| P7.5+3.2 | Graphic exchange 动画 | 缺失 | Graphic-view 专属：Tag → Graphic list 行 → 切图 | M（1 天） |
| P7.5+3.3 | Bar fill 动画 | 缺失 | Bar/Rectangle 动态 fill level (0~100%) | S（0.5 天） |
| P7.5+3.4 | Direct movement 动画 | 部分实现 | 双 Tag X + Y 绝对坐标（与 Horizontal/Vertical 区别） | S（0.5 天） |
| P7.5+3.5 | Events 通用补全 | 单击/按下/释放/激活/取消激活 | Loop while pressed、Right click、Mouse Click/Press/Release（鼠标专属）、Switch on/off、Toggle | S（0.5 天） |
| P7.5+3.6 | Screen-level 事件 | 缺失 | Loaded / Cleared / Time interval (cyclic continuous / cyclic on operation) | M（1 天） |
| P7.5+3.7 | Tag-level 事件 | 部分实现 | Tag value changed → 全局触发函数 | S（0.5 天） |

合计：**约 5 天**，动画族从 6 种 → 9 种、事件从 8 种 → 15 种。

### 3.5 总工作量与里程碑

| 阶段 | 工作量 | 字段覆盖率 | 里程碑 |
|---|---:|---:|---|
| 当前 P7.5 | — | 18.5% | 已完成 |
| P7.5+1 通用 Category 补全 | 4.5 天 | **45%** | M1：所有 widget 进入"可商业演示"梯度 |
| P7.5+2 控件专属补全 | 16 天 | **75%** | M2：核心 10 个高频控件达到 WinCC 等价深度 |
| P7.5+3 动画/事件深化 | 5 天 | — | M3：动画族 9 种 + 事件 15 种，与 WinCC 持平 |
| **合计** | **25.5 天** | **75%** | — |

### 3.6 备注

- **"100% 覆盖"非目标**：WinCC 部分历史字段（如 RT Pro 专属 Logarithmic scaling、Step 标尺）业内使用极少，不必强求。本计划目标 **75%**。
- **未找到权威来源的字段已标注**：Disc 6 项的部分子字段（Bezel）、Bar fill 动画字段范围编辑器具体形态以 WinCC V18 Online Help 局部章节为准，存在版本差异。
- **本文档与 `WinCC-Deep-Audit.md` 互补**：本文聚焦"组态时 Inspector 字段"，该文聚焦"运行时行为"。两者合并构成对 WinCC 完整对标的两面。
