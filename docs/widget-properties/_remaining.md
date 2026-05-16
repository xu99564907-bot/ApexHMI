# 剩余 19 个 widget — 简要清单

阶段 1 不为这些 widget 出完整属性详档，仅列出**对应 WinCC ScreenItem**、**PDF Table 编号**、**属性数（估算，含部分描述噪声）**、**ApexHMI 现有字段数**、**最关键差距 1-2 项**。

详细文档延后到阶段 2 / 阶段 3 视优先级补全。

| widget | WinCC ScreenItem | PDF Table | WinCC 属性数（估算）| ApexHMI 现有 | 关键差距 |
|---|---|---|---|---|---|
| **text** | TextField | Table 1-104（line 32278-32671）| ~73 | 5 | 缺：字体加粗/斜体/下划线/字体名、边框、文字旋转 `TextOrientation`、`HorizontalAlignment/VerticalAlignment`、`AutoSize/AdaptBorder`、`Wordwrap`、`UseDesignColorSchema` |
| **rectangle** | Rectangle | Table 1-82（line 26951-）| ~52 | 7 | 缺：填充图案 `FillStyle/FillPatternColor`、`UseFirstGradient/SecondGradient`、边框样式 `BorderStyle/EdgeStyle`、`Flashing 全套` |
| **ellipse** | Ellipse | Table 1-34（line 14714-）| ~68 | 6 | 缺：`StartAngle/EndAngle`（实际 Ellipse 在 WinCC 包括 EllipseSegment 衍生）、填充图案、闪烁 |
| **line** | Line | Table 1-52（line 19416-）| ~101 | 7 | 缺：`LineEndShapeStyle` 起止箭头、`LineWidth` 动态可改、`BorderColor` 与 Line 颜色区分、`Flashing` |
| **polyline** | Polyline | Table 1-70（line 25419-）| ~49 | 4 | 缺：起止箭头、`LineWidth/LineColor` 详细、`UseDesignColorSchema` |
| **polygon** | Polygon | Table 1-68（line 25094-）| ~57 | 4 | 缺：`FillStyle/FillPatternColor`、`BorderColor/Style/Width`、闪烁 |
| **graphic-view** | GraphicView | Table 1-46（line 18233-）| ~56 | 3 | 缺：`PictureSize/PictureAlignment/TransparentColor/UseTransparentColor`、`PictureRotation`、`Stretch` 子类型详细对齐 |
| **datetime** | DateTimeField | Table 1-31（line 14212-）| ~53 | 5 | 缺：`ShowDate / ShowTime` 独立开关、`LongTimeFormat`、`UseTimeBase / TimeBase`（时区）、字体系列、边框系列、`AcceptOnExit / EditOnFocus`（输入行为）、`Type`（系统时间/Tag/Input）独立化 |
| **switch** | Switch | Table 1-97（line 29822-）| ~98 | 7 | 缺：`PictureOff/PictureOn`、`TextOff/TextOn`（双态文本/图）、`Mode` 系列（Tag-binding/Push-button/Toggle）、`Authorization/LogOperation`、`Flashing` |
| **round-button** | RoundButton | Table 1-84（line 27306-）| ~89 | 5 | 缺：`Mode + Toggle + Pressed + On/Off`（同 button.md 的双态问题）、`PictureOn/Off`、`Authorization`、`Hotkey`、`ShowFillLevel` |
| **slider** | Slider | Table 1-91（line 28600-）| ~77 | 9 | 缺：`ScaleColor / ScaleGradation / ShowTickLabels / ShowProcessValue`（刻度细节）、`ProcessValue` 直接绑定（ApexHMI 用 variable 已覆盖）、`Authorization` |
| **scrollbar** | ❌ 无独立 ScreenItem，WinCC 复用 Slider | — | — | 9 | ApexHMI 自行设计，与 WinCC 无对应。建议保留独立 widget。 |
| **clock** | Clock | Table 1-25（line 13265-）| ~51 | 6 | 缺：`HourNeedleHeight/Width`、`MinuteNeedleHeight/Width`、`SecondNeedleHeight/Width`（指针表盘细节）、`ClockFace / FaceColor / FacePicture`（表盘外观）、`Border 系列` |
| **combobox** | ComboBox | Table 1-27（line 13633-）| ~51 | 2 | 缺：**严重缺失**——`Mode / TextList / Assignments`、`Authorization`、`SelectionBackColor/ForeColor`、`ShowDropDownButton`、`CountVisibleItems`、字体、边框、`Enabled/Visible`。当前 ApexHMI 仅 variable + items 两字段 |
| **listbox** | Listbox | Table 1-55（line 20066-）| ~43 | 2 | 缺：同 combobox（仅 variable + items 两字段太少） |
| **checkbox** | CheckBox | Table 1-17（line 11972-）| ~71 | 5 | 缺：`Mode / BitNumber`（按 bit 切换）、`Group`（互斥组）、`Authorization`、字体、边框 |
| **optiongroup** | OptionGroup | Table 1-62（line 24230-）| ~71 | 3 | 缺：`Mode / Style`（垂直/水平/网格）、`SelectedItemBackColor / SelectedItemForeColor`、`ItemBorderStyle`、`Authorization`、字体 |
| **table-view** | OnlineTableControl | Table 1-57（line 20336-）| ~39（核心，另有 Toolbar/Statusbar 数十字段）| 5 | 缺：**严重缺失** —— `Column 系列`（add/remove/visible/sort/width/...）、`RTPersistence`、`AutoScroll`、`Toolbar`、`Statusbar`、`ExportFilename/PrintJob`、`MessageBlock 等对齐`（OnlineTableControl 是历史数据表格，与 AlarmControl 共享大量基础设施）|
| **screen-window** | ScreenWindow | Table 1-80（line 26169-）| ~170（含子窗口管理大量字段）| 3 | 缺：`Caption / ShowTitle`（标题栏）、`Closeable / Moveable / Sizeable`（弹窗化行为）、`AdaptScreen / AdaptSize`（自适应）、`ZoomInScreen`、`Border 系列` |

---

## 待补的"业务类"11 个 widget（ApexHMI 自有，无 WinCC 对应或弱对应）

下列 widget 在 ApexHMI `WidgetSchemaCatalogSeed.cs` 中存在但**无直接 WinCC ScreenItem 对应**，本阶段不分析：

- `recipe-view` → 弱对应 WinCC RecipeView（PDF Page 不在本次抽取范围）
- `user-view` → UserView（Table 1-119）
- `diagnostic-view` → ChannelDiagnose / SystemDiagnoseView
- `alarm-indicator` → 已经被 alarm-view 涵盖（指示器是 alarm-view 的子集 UI）
- `status-force` → StatusForce ScreenItem 存在但 ApexHMI 实现独立
- `html-browser` → HTMLBrowser（Table 1-48）
- `pdf-view` → PDFview
- `media-player` → MediaPlayer
- `xy-trend` → FunctionTrendControl
- `report-view` → 无直接对应（ApexHMI 自创）

阶段 2 视优先级单独分析。

---

## 阶段 1 总体差距三大类（适用于全部 27 widget）

### 1. 安全与审计大类（**全 widget 共通缺失**）
- **Authorization**（操作权限组绑定）
- **LogOperation**（操作审计日志）
- **AskOperationMotive**（操作原因弹窗）
- **UseTwoHandOperation**（双手操作确认，PDF 标 "No access in runtime"——西门子自己未实现）

### 2. 动态外观大类（**P2A 动画系统可承载，需在 schema 暴露**）
- **Flashing / FlashingColorOn/Off / FlashingEnabled / FlashingRate**（背景闪烁）
- **BackFlashing\*** / **BorderFlashing\***（背景/边框闪烁三套独立）
- **Authorization** 失败时的视觉反馈
- **ShowBadTagState**（变量品质坏值时灰显——**生产强需求**）
- **UseTagLimitColors** / **AboveUpperLimitColor / BelowLowerLimitColor**（限值越界自动变色）

### 3. 字体/边框/内边距完整化（**全 widget 共通缺失**）
- **FontName / FontBold / FontItalic / FontUnderline**（仅 FontSize 已实现）
- **BorderColor / BorderWidth / BorderStyle / EdgeStyle / CornerStyle / CornerRadius**
- **TopMargin / BottomMargin / LeftMargin / RightMargin**
- **HorizontalAlignment / VerticalAlignment**
- **Transparency**（独立透明度，不仅是 opacity）
- **UseDesignColorSchema / StyleItem**（全局主题联动）
