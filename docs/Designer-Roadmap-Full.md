# 画布设计器 — 完整开发路线图（对标 WinCC）

> 目标：从当前简易设计器演进到接近 WinCC TIA Portal Comfort/Advanced 水平的工业 HMI 画布设计器。
> 本路线分 **10 个阶段**，预计总工期 **8-12 周**（按全职单人估算）。每阶段独立可发布、有验收标准。
> 配套设计稿见 [Designer-Inspector-V2-Mockup.md](Designer-Inspector-V2-Mockup.md)。

---

## 阶段概览

| 阶段 | 主题 | 工期 | 关键产出 |
|---|---|---|---|
| **P0** | 基础清洗 | 2-3 天 | 删除业务控件 / 清理工具箱 |
| **P1** | Inspector V2 + 事件模型 | 4-5 天 | 4 Tab 面板、事件多动作链 |
| **P2** | 动画系统 V2 | 3-4 天 | 6 类标准动画 |
| **P3** | 基础对象控件 | 5-7 天 | 文本/矩形/圆/线/I/O 域三件套 |
| **P4** | 交互元素控件 | 5-7 天 | 开关/棒图/量规/滑块/时钟 |
| **P5** | 高级控件 - 数据 | 7-10 天 | 趋势/报警/表格视图升级 |
| **P6** | 设计师生产力 | 5-7 天 | 样式系统 / 多语言 / 库 |
| **P7** | Faceplate 系统 | 7-10 天 | 自定义复合控件 + 项目库 |
| **P8** | 高级控件 - 业务 | 5-7 天 | 配方/用户管理/系统诊断 |
| **P9** | 高级控件 - 媒体/分析 | 5-7 天 | HTML/PDF/媒体/摄像头/f(x) |
| **P10** | 运行时与质量 | 7-10 天 | 性能/采集周期/审计/打包 |

总计 50-77 天（10-15 周）。可分两个里程碑发布：

- **里程碑 M1（P0-P5）**：4-5 周，达成"可用的专业 HMI 设计器"，覆盖 80% 工程场景
- **里程碑 M2（P6-P10）**：4-7 周，达成"对标 WinCC 的完整设计器"

---

## P0 — 基础清洗（2-3 天）

### 目标
彻底清理"业务控件混在基础设计器里"的历史包袱。把所有业务复合控件移出工具箱，只留下"原子级"基础控件位（在 P3 中重做）。

### 待删除清单（业务复合控件）

| 控件 TypeId | 文件位置 | 替代方案 |
|---|---|---|
| `manual-cylinder-block` | `Views/Runtime/Widgets/ManualCylinderBlockWidget.*` + VM | 留在手动操作页内部使用；从画布工具箱移除；P7 中升级为 Faceplate |
| `manual-axis-block` | `Views/Runtime/Widgets/ManualAxisBlockWidget.*` | 同上 |
| `manual-robot-block` | `Views/Runtime/Widgets/ManualRobotBlockWidget.*` | 同上 |
| `manual-stopper-block` | `Views/Runtime/Widgets/ManualStopperBlockWidget.*` | 同上 |
| `cylinder`、`axis`、`robot`、`stopper`（旧别名） | `WidgetRegistry.cs` 兼容映射 | 全部移除 |
| `motor`、`indicator`、`value-display`（StatusWidget 系列） | `Views/Runtime/Widgets/StatusWidget.*` | P3 中用 I/O 域 + Appearance 动画取代 |
| `tag-value` | `Views/Runtime/Widgets/TagValueWidget.*` | P3 中合并到 I/O 域 |
| `alarm-banner`、`alarm-list` | `AlarmBannerWidget`、`AlarmListWidget` | P5 重做为「报警视图」 |
| `page-button` | `Views/Runtime/Widgets/...` | 用普通按钮 + navigate 替代（已支持） |

### 保留 / 升级

| 保留 | 处理 |
|---|---|
| `button` | 保留，P1 升级为多事件多动作模型 |
| `text` | 保留，P3 升级（多语言支持） |

### 清理工作

- 移除 `WidgetRegistry.cs` 对应注册项
- 移除属性面板里业务专有字段（device-name 之类）
- 在手动操作页内部直接 new 这些 VM/View，不再走 widget 工厂
- 工程文件（`projects/_sample/project.json`）里的旧 widget 实例自动迁移：清除画布上业务 widget，留空位提示用户重做
- 检查并清理对应单元测试

### 验收

- 工具箱只剩「按钮 + 文本」两个能用的（其他控件在 P3-P5 重做）
- 手动操作 Tab 页正常工作（业务控件还在那里跑）
- 加载旧工程不报错（业务 widget 静默忽略并日志告警）

---

## P1 — Inspector V2 + 事件模型升级（4-5 天）✅ 已完成 2026-05-13

### 目标
重做属性面板为 TIA Portal 风格（4 Tab + 左分类树），同步升级数据模型支持事件多动作链。

### 任务

**1. UI 框架（2 天）**
- 顶部 4 Tab：`属性 / 动画 / 事件 / 文本`
- 左侧分类树宽 90px，每 Tab 各有自己的左导航
- 右侧字段卡片区，两列布局（标签 90px | 编辑控件 *）
- 视觉规范见 Mockup 第 8 节

**2. 数据模型重构（1 天）**
```csharp
// 新增
class ActionStep { string FunctionId; Dictionary<string,string> Args; }
class WidgetInstance {
    Dictionary<string, List<ActionStep>> Events { get; set; }
    // ... 旧字段保留，序列化时迁移
}
```

**3. 事件 Tab UI（1 天）**
- 左侧 6 个事件触发点：单击 / 按下 / 释放 / 激活 / 取消激活 / 数值更改
- 每个事件标数字徽章 `(N)` 显示动作数
- 右侧动作卡片列表：↑↓ 排序、× 删除、＋ 新建
- ＋ 新建弹「系统函数选择菜单」（参见 Mockup 第 4.2 节）

**4. 系统函数库初版（0.5 天）**
- 编辑位：set-bit / reset-bit / toggle-bit / set-while-press
- 写入变量：write-bool / write-int / write-float / increment / decrement
- 画面：navigate / back / popup
- 报警：ack-current / clear-buffer
- 其它：show-dialog / play-sound

**5. 兼容迁移（0.5 天）**
- 加载旧文件时 `ActionType + ActionParam` → `Events["click"] = [{FunctionId, Args}]`
- 写入时只用新 schema；schemaVersion 字段标记

### 验收
- 属性面板顶部能切 Tab
- 事件 Tab 能给"单击"挂 3 个动作并按顺序执行
- 旧工程加载后按钮点击行为不变
- 数据模型版本号写入工程文件

---

## P2 — 动画系统 V2（3-4 天）  ✅ 已完成 2026-05-13

### 目标
把当前简单的"状态动画"列表升级为 WinCC 的 6 类标准动画。

### 模型

```csharp
class AppearanceAnimation {  // 范围 / 多位 / 单位 三种 match 模式
    string TagId;
    AnimationMatchType Type;
    List<AppearanceRow> Rows;
}
class AppearanceRow { string RangeFrom, RangeTo; int BitIndex; 
                     string Background, Foreground; bool Blink; }
class VisibilityAnimation { string TagId; VisibilityMode Mode; ... }
class MoveAnimation { 
    MoveType Type;  // Horizontal/Vertical/Direct/Diagonal
    string TagIdX, TagIdY;
    double RangeMinX, RangeMaxX, PixelStartX, PixelEndX;
    // 同理 Y
}
class WidgetInstance {
    AppearanceAnimation? Appearance;
    VisibilityAnimation? Visibility;
    MoveAnimation? Movement;  // 单选一种
}
```

### 任务

**1. 动画 Tab UI（1.5 天）**
- 左导航：总览 / 变量连接 / 外观 / 可见性 / 水平移动 / 垂直移动 / 直接移动 / 对角线移动
- 总览面板：列出已启用动画，状态图标
- 外观面板：变量名 + 类型(范围/多位/单位) + 表格(范围/背景/前景/闪烁) + 增删行
- 可见性面板：变量 + 条件 + 否则隐藏/禁用
- 移动面板：四种 movement 单选 + 变量映射

**2. 运行时支持（1 天）**
- AppearanceAnimation：值变化时按 Rows 找匹配行，修改控件 BG/FG/Blink
- VisibilityAnimation：值变化时设置 Visibility / IsEnabled
- MoveAnimation：值变化时计算 Canvas.Left/Top
- 闪烁实现：DispatcherTimer 全局 600ms 周期，遍历所有 Blink=true 的控件切换 Opacity

**3. 数据迁移（0.5 天）**
旧 `Animations` 列表按 `TargetProperty` 拆分：
- `TargetProperty="background"` → AppearanceRow 表
- `TargetProperty="visibility"` → VisibilityAnimation
- `TargetProperty="x"/"y"` → MoveAnimation

**4. 字段动态化图标 🔶（1 天）**
- 每个颜色字段、文本字段、几何字段右侧加小三角图标
- 灰=静态、橙=已绑变量
- 点击弹小卡片：静态值 / 绑变量 / 绑脚本
- 绑变量后实际写入 AppearanceAnimation 等结构（统一通过动画系统驱动）

### 验收
- 拖个矩形上去，绑外观动画"值=1 绿色、值=2 红色闪烁"，运行时切换 PLC 值能看到效果
- 可见性动画：PLC 值 True 时显示、False 时消失
- 移动动画：滑块变量从 0→100，矩形从左移到右
- 旧动画规则加载后按钮颜色变化逻辑保持

---

## P3 — 基础对象控件（5-7 天） ✅ 已完成 2026-05-13

### 目标
重做 WinCC 「Basic Objects」组所有基础原子控件。

### 控件清单

| 控件 | 工期 | 关键属性 |
|---|---|---|
| **文本域** Text Field | 0.5 天 | 文本 / 字体 / 对齐 / 多语言占位 / 边框 |
| **矩形** Rectangle | 0.5 天 | 几何 / 填充 / 边框 / 圆角 / 阴影 |
| **椭圆** Ellipse | 0.5 天 | 同上（圆=椭圆等宽高） |
| **直线** Line | 0.5 天 | 起止点 / 线宽 / 颜色 / 虚线模式 / 箭头 |
| **折线** Polyline | 1 天 | 顶点编辑器（双击进入顶点模式） |
| **多边形** Polygon | 1 天 | 同折线，闭合 + 填充 |
| **图形视图** Graphic View | 0.5 天 | 图像源 / 拉伸 / 透明 / 占位 |
| **数字 I/O 域** Numeric I/O Field | 1 天 | 模式(In/Out/双向) / 格式 / 小数位 / 限值 / 变量 |
| **符号 I/O 域** Symbolic I/O Field | 0.5 天 | 模式 / 文本列表 / 变量 |
| **图形 I/O 域** Graphic I/O Field | 0.5 天 | 模式 / 图形列表 / 变量 |
| **日期时间域** Date/Time Field | 0.5 天 | 格式 / 模式 / 变量 |

### 配套支持

- **文本列表 / 图形列表** 资源（P6 中正式引入，P3 暂用 inline 输入）
- 折线 / 多边形顶点编辑模式：双击控件进入"顶点拖拽态"，画布顶部出"完成 / 添加顶点"工具栏

### 验收
- 工具箱按 WinCC 分组：基本对象 / 元素 / 控件
- 拖每个控件到画布能正常显示
- 数字 I/O 域：输入模式可点击输入值并写回 PLC，输出模式只显示
- 符号 I/O 域：INT 变量值=1 显示"运行"，=2 显示"停止"
- 图形 I/O 域：BOOL 变量切换两张图片

---

## P4 — 交互元素控件（5-7 天） ✅ 2026-05-13

### 控件清单

| 控件 | 工期 | 关键属性 |
|---|---|---|
| **开关** Switch | 1 天 | 双态切换 + 自定义 On/Off 文本图形 |
| **圆形按钮** Round Button | 0.3 天 | 与按钮共用模型，外观为圆 |
| **棒图** Bar | 1 天 | Min/Max / 方向 / 阈值刻度 / 分段色 |
| **量规** Gauge | 1 天 | 圆盘指针表 / 警戒区间 / 单位 / 分度 |
| **滑块** Slider | 0.5 天 | Min/Max / 步长 / 方向 / 标签 |
| **滚动条** Scroll Bar | 0.5 天 | 类似滑块 |
| **时钟** Clock | 0.5 天 | 模拟 / 数字 / 时区 |
| **组合框** ComboBox | 0.5 天 | 文本列表 → 变量 |
| **列表框** ListBox | 0.5 天 | 同上多选 |
| **复选框** CheckBox | 0.3 天 | 位变量绑定 |
| **单选** OptionGroup | 0.5 天 | 多选一，写整数值 |

### 配套

- **变量拖放生成控件** 交互（Mockup 第 7.2 节）
  - 拖 BOOL → 弹「按钮 / 指示灯 / 开关 / 复选框」
  - 拖 INT/REAL → 弹「I/O 域 / 棒图 / 量规 / 滑块 / 数值显示」
  - 拖 STRING → 弹「文本域 / 符号 I/O 域」

### 验收
- 拖一个 INT 变量到画布弹候选菜单
- 棒图 / 量规绑变量后实时跟随 PLC 值变化
- 开关 / 滑块写回 PLC

---

## P5 — 高级控件 数据类（7-10 天）✅ 已完成 2026-05-13

> **里程碑 M1（P0-P5）已达成 🎉** — "可用的专业 HMI 设计器" 完成，覆盖 80% 工程场景。
> 4 个 P5 控件：趋势视图（OxyPlot 2.1.0）/ 报警视图（对接 MainViewModel.CurrentAlarms）/ 表格视图（static+tag-array）/ 画面窗口（嵌入式）。


### 目标
工业 HMI 三大刚需高级控件：趋势 / 报警 / 表格。

### 任务

**1. 趋势视图（3-4 天）**
- 基于 OxyPlot 或 LiveCharts2
- 实时模式：滚动时间窗，订阅 N 个变量
- 历史模式：从 SQLite 历史归档库读取
- 配置：趋势集合（每条线一个变量+颜色+线宽）/ Y 轴范围 / 时间轴 / 工具栏（缩放/滚动/暂停/导出 CSV）
- 设计时占位渲染

**2. 报警视图升级（2-3 天）**
- 现 AlarmList → AlarmView 替换
- 列定制（时间 / 级别 / 文本 / 状态 / 编号 / 确认人...）
- 过滤器（按等级 / 按时间段 / 按关键字）
- 工具栏：确认所选 / 确认全部 / 导出 / 暂停滚动
- 双击行 → 跳转到报警详情画面

**3. 表格视图（2-3 天）**
- 通用 DataGrid 控件
- 列绑定（来源：变量列表 / 数组 / SQL）
- 行编辑 / 排序 / 筛选
- 工具栏：增/删/导出
- 用例：参数表批量编辑

**4. 画面窗口（0.5 天）**
- 内嵌另一画面到当前画面（用作复用页眉/页脚 / 弹窗）
- 引用画面 RouteKey + 模态/非模态 + 自定义大小

### 验收
- 趋势视图绑 3 个变量，PLC 改值后曲线实时跟随
- 报警视图能确认报警、导出 CSV
- 表格视图能编辑参数数组并写回 PLC
- 画面窗口能嵌入子画面，子画面里按钮也能正常工作

---

## P6 — 设计师生产力（5-7 天）✅ 已完成 2026-05-13

> P6 五个子阶段（A 样式 / B 多语言 / C 库 / D 符号库 / E 列表资源）全部完成，
> M2 第一阶段达成。控件可写 `{style:...}` / `{text:...}` / `{textList:...}` 引用工程级资源。

### 任务

**1. 全局样式系统 Style（1.5 天）**
- 项目级 Styles 配置：色板 / 字体集 / 边框预设
- 控件属性可"引用样式"而非具体值
- 改全局，所有引用处联动
- UI：项目设置面板新增「样式」Tab

**2. 多语言文本系统（2 天）**
- 项目级 Texts 资源（语言 × Key → 文本）
- 文本字段、Tooltip、弹窗文字全部支持挂 Text Key
- 「文本」Tab 编辑当前控件所有可见文本的多语言列
- 运行时按全局语言切换刷新

**3. 项目库 + 全局库（1.5 天）**
- 项目库：当前工程内的图形 / 模板 / Faceplate
- 全局库：跨工程的资产仓库
- 工具箱新增"库"面板，拖入即生成实例
- 库资产带版本号

**4. 符号库 + 图形资源（1 天）**
- 内置符号集（阀/电机/管路/泵/罐/箭头等 SVG）
- 用户上传位图资源
- 图形 I/O 域、Graphic View 引用资源

**5. 文本列表 / 图形列表 资源管理（0.5 天）**
- 资源管理面板：定义文本列表（INT→文字）和图形列表（INT→图）
- 符号 I/O 域、图形 I/O 域选择已有列表

### 验收
- 改一处主蓝色，所有引用该样式的控件颜色全变
- 切语言，文本域自动变成对应语言
- 拖一个项目库里的"电机图形"到画布即生成实例
- 符号 I/O 域可选择已定义的"运行状态列表"

---

## P7 — Faceplate 系统（7-10 天）  ✅ 2026-05-13（精简方案完成）

> **精简实施说明**：5 个子阶段（A/B/C/D/E/F）全部入库，构建 0 错误：
> - P7A：数据模型 `Models/RuntimeUi/Faceplate.cs` + Document.Faceplates + WidgetInstance.FaceplateVersion + Migration 初始化
> - P7B：渲染引擎 — FaceplateResolver、FaceplateChildDataContext、WidgetRegistry 适配 `faceplate:<id>` 前缀、嵌套深度/循环保护（>5 / 重复 Id → 占位）
> - P7D：实例化 + 属性面板 — 工具箱"我的 Faceplate"分组、Apply 默认值 + 尺寸 + 版本、接口属性 Expander（TextBox 通用 + TagAddress 自动补全；颜色/布尔/PageRoute 类型化编辑器 TODO v2.0）
> - P7E：版本管理（简化版）— 版本不一致 → 橙色 banner + 升级按钮；新增 key 用 DefaultValue 填充，删除 key 保留；SemVer Major 弹窗 / 类型变更检查 / 批量升级 TODO v2.0
> - P7F：4 个内置 Faceplate（气缸 / 轴 / 机械手 / 挡停）极简骨架（3-7 widget），加载时自动注入；不复刻 P0 删除前的复杂气缸卡片
> - P7C：Faceplate 编辑器 —— 不新建独立 Section，复用 DesignerEditorView 加 ToggleButton 切换模式：左栏页面树↔Faceplate 列表 切换，画布 SelectedPage = SelectedFaceplate.InnerScreen，元数据 Expander 编辑 Name/Version/Category/IconKind + 接口属性增删改

### 目标
WinCC 最强生产力工具。把"气缸/轴/机械手"这类复合控件抽象成**可复用 / 有接口属性 / 有版本号**的封装单元。

### 任务

**1. Faceplate 数据模型（2 天）**
```csharp
class Faceplate {
    string Id;
    string Name;
    string Version;
    string Category;            // "气缸" / "轴" / 自定义
    List<FaceplateProperty> InterfaceProperties;  // 接口属性，实例化时配
    PageDefinition InnerScreen; // 内部画面（可嵌套 widget）
    Dictionary<string, string> DefaultBindings;   // 接口属性→内部变量的映射
}
class FaceplateInstance : WidgetInstance {
    string FaceplateId;
    string FaceplateVersion;
    Dictionary<string,string> PropertyValues;  // 接口属性的实参
}
```

**2. Faceplate 编辑器（2-3 天）**
- 新页面：设计器子 Tab "Faceplate 编辑"
- 类似画布设计但作用域是单个 Faceplate
- 左侧：接口属性表（添加/编辑/类型/默认值）
- 中间：内部画面画布（可拖普通 widget 进来）
- 内部 widget 的属性可绑到接口属性（占位变量）

**3. Faceplate 实例化（1 天）**
- 工具箱出现"我的 Faceplate"分组
- 拖入画布生成实例，弹接口属性配置面板
- 实例渲染：把接口属性传给内部画面，递归构建 widget 树

**4. 版本管理（1 天）**
- Faceplate 升级版本时，已存在的实例显示"有新版本可用"
- 用户可选"升级到新版本"或"保留旧版本"
- 接口属性新增/删除时的兼容策略

**5. 内置 Faceplate 重做（2-3 天）**
- 把 P0 删除的气缸/轴/机械手/挡停业务控件，用 Faceplate 体系重做
- 放入"内置 Faceplate"分组
- 接口属性：device-name, prefix-route, optional 数组下标等

### 验收
- 用户能创建一个"我的气缸控件"，定义 3 个接口属性
- 在画布上拖 5 个实例，每个填不同接口属性，独立工作
- Faceplate v1.0 → v1.1 升级后老实例不破

---

## P7.5 — 属性面板 Schema 化（穿插）  ✅ 2026-05-13

### 目标
把"控件类型 → 属性键 → 编辑器"的散落逻辑收敛为 Schema，提升新增控件 / 维护属性面板的效率。

### 内容
1. **Schema 模型**：`PropertyEditorType`（9 类） + `PropertyDescriptor`（Key/DisplayName/EditorType/DefaultValue/Category/EnumOptions）+ `WidgetSchema`
2. **类型编辑器骨架**：9 个 DataTemplate（String/MultilineString/Number/Integer/Boolean/Color/TagAddress/Enum/PageRoute/Json + TextListRef/GraphicListRef/Font 占位），由 `PropertyEditorTemplateSelector` 按枚举选模板
3. **颜色编辑器**：色块预览 + hex 输入 + Popup（8 常用工业色 + 6 样式引用 `{style:colors/*}` + 自定义 hex 输入）
4. **Schema-driven 属性面板**：DesignerEditorViewModel 加 `GroupedPropertyEditors`，按 Category 分组到 Expander；Schema 找不到时回退旧 generic 面板
5. **创建时默认值初始化**：`WidgetEditorService.AddWidget` 用 schema.DefaultValue 补齐 Properties
6. **10 个高频 widget schema 覆盖**：text / rectangle / ellipse / button / io-numeric / io-symbolic / switch / bar / gauge / trend-view
7. **Faceplate 接口属性接入**：`FaceplatePropertyType → PropertyEditorType` 映射，"接口属性" Expander 走同一套 schema 编辑器
8. **旧 UI 清理**：外观/属性 Expander 删除；数据绑定 / 事件 / 状态动画 三个旧 Expander Visibility=Collapsed

### 未完
- 中频 17 个 widget schema（line/polyline/polygon/graphic-view/io-graphic/datetime/slider/scrollbar/clock/combobox/listbox/checkbox/optiongroup/round-button/alarm-view/table-view/screen-window）— 走 fallback 通用编辑器
- TextListRef / GraphicListRef / Font 编辑器目前回退到 String —— v2.0 升级专用选择器
- JSON 编辑器目前是多行 TextBox —— v2.0 升级语法高亮

---

## P8 — 高级控件 业务类（5-7 天） ✅ 2026-05-13

### 控件清单

| 控件 | 工期 |
|---|---|
| **配方视图** Recipe View | 2-3 天 |
| **用户视图** User View | 1-2 天 |
| **系统诊断视图** System Diagnostic View | 1-2 天 |
| **报警指示器** Alarm Indicator | 0.5 天 |
| **状态/强制** Status/Force（调试用） | 0.5 天 |

### 配方视图（重点）

- 数据模型：Recipe = 一组变量值的快照
  - 字段定义（名称/类型/默认/限值）
  - 数据集（多个具名快照 = 不同产品）
- 视图：左选配方、右数据集列表 + 工具栏（读出 PLC / 写入 PLC / 保存 / 导出 CSV / 导入）
- 持久化：SQLite 或 JSON 文件

### 用户视图

- 用户/角色管理界面（增/删/改密码/分配角色）
- 与已有的权限系统对接

### 系统诊断视图

- 通讯连接状态（OPC UA 服务器在线情况、订阅率、错误日志）
- PLC 状态（运行/停止/错误码）
- HMI 自身资源（CPU/内存/磁盘）

### 验收
- 配方视图能在 PLC 和 HMI 间双向同步配方数据集
- 用户视图能添加用户、分配角色
- 系统诊断视图实时显示通讯状态

---

## P9 — 高级控件 媒体/分析（5-7 天）✅ 已完成 2026-05-13

### 控件清单

| 控件 | 工期 | 实现方式 |
|---|---|---|
| **HTML 浏览器** | 0.5 天 | WebView2 内嵌 |
| **PDF 视图** | 0.5 天 | WebView2 + pdf.js 或 PDFium |
| **媒体播放器** | 0.5 天 | MediaElement |
| **摄像头视图** | 1-2 天 | RTSP / MJPEG，Emgu.CV 或 LibVLCSharp |
| **f(x) XY 趋势** | 2 天 | OxyPlot ScatterSeries |
| **报表视图** | 1-2 天 | 模板 + 数据源；导出 PDF / Excel |

### 验收
- 摄像头视图能显示 IP 摄像头 RTSP 流
- f(x) XY 趋势：X=轴位置、Y=压力，描出曲线
- 报表视图：按模板生成日报 PDF

---

## P10 — 运行时与质量（7-10 天）✅ 已完成 2026-05-13

> **实际完成情况（ROI 排序，8 阶段 A–H + 文档 I）**
>
> | 阶段 | 标题 | 状态 |
> |---|---|---|
> | A | 中频 17 widget schema 补全 + 移除 DefaultProperties | ✅ |
> | B | IUserService 扩展（增删改角色） | ✅ |
> | C | OPC UA write-string 支持 | ✅ |
> | D | 项目导出 / 导入（zip 打包） | ✅ |
> | E | 属性面板多选编辑（交集 + 批量改） | ✅ |
> | F | 离线模拟（SimTag1..5 / SimBool1..3 / SimText1 假数据） | ✅ |
> | G | 撤销 / 重做覆盖动画 / 事件链 / 多选 | ✅ |
> | H | 历史归档 SQLite + 趋势历史模式接通（7 天滚动） | ✅ |
> | I | 文档收尾 + Roadmap + v3.0 待办 | ✅ |
>
> 未做项延期至 v3.0：采集周期分级、数组/UDT 路径绑定增强、性能优化（虚拟化）、报表模板编辑器。

### 任务

**1. 采集周期分级（2 天）**
- 变量级别配置：连续 / 显示时 / 按需 / 变化时（订阅）
- 默认推荐"显示时"以节省总线带宽
- OPC UA Subscription 优化：订阅 N 个变量共享一个 Session

**2. 历史归档系统（2-3 天）**
- 归档 Tag 配置：哪些变量需要历史
- 归档周期：1s / 1min / 1h / 变化时
- SQLite 时序表，自动滚动+压缩
- 提供 ArchiveQuery API 给趋势/报表用

**3. 区域指针支持（如需，1 天）**
- S7 用户群可选；CODESYS 项目可跳过

**4. 数组 / UDT 路径绑定（1.5 天）**
- Tag 路径支持 `Tag[i].Member` 完整语法
- 自动补全识别数组下标 + 结构成员

**5. 多选编辑（1 天）**
- Inspector 选中多 widget 时显示交集属性
- 修改一处所有联动
- 不一致字段显示"—— 多个值 ——"

**6. 撤销 / 重做完善（1 天）**
- 当前已部分支持，补全所有操作（动画修改、事件链修改、Faceplate 实例化等）
- 命令模式 + Action 历史栈

**7. 性能优化（1-2 天）**
- 大量 widget 时画布渲染（虚拟化、按视口剔除）
- 属性面板字段动态生成（已有 Property 列表，确认无 O(n²) 通知风暴）
- 启动加载（已有 Bulk 集合 + Dispatcher 后台加载，复查其他模块）

**8. 项目导出 / 导入（1 天）**
- 整个工程打包为 zip 包（含变量表 / Faceplate 库 / 资源）
- 客户现场可"复制工程"快速部署

**9. 离线模拟（1 天）**
- 设计时不连 PLC 也能演示控件状态
- 假数据生成器（如棒图自动正弦摆动、报警随机生成）
- 演示给客户看用

### 验收
- 启动加载 100k 变量 < 3 秒
- 1000 个 widget 的画布拖动流畅
- 工程导出 → 另一台机导入 → 完全一致运行

---

## 跨阶段持续项

以下不属于某个阶段，但贯穿整个开发：

### 测试
- **每阶段配套单元测试**：核心数据模型、动画引擎、事件链运行时
- **集成测试**：完整工程的保存/加载/迁移
- **UI 自动化测试**：关键路径用 FlaUI 之类做回归

### 文档
- 用户手册：每阶段交付时同步写"控件参考"
- 开发者文档：Faceplate / 自定义动作的扩展指南
- 视频教程：关键操作录屏

### 国际化
- P6 引入多语言后，所有 UI 文本走资源文件
- 初期支持中 / 英两种

### 兼容性
- 每个 schema 变更都加 version 字段
- 加载旧版本自动迁移并备份原文件

---

## 风险与依赖

| 风险 | 影响 | 对策 |
|---|---|---|
| OPC UA 性能扛不住大量订阅 | 高 | P10 重点优化；预留时间 |
| 趋势视图大数据卡 | 中 | 用成熟库 OxyPlot/LiveCharts2，不要自己写 |
| Faceplate 嵌套递归 bug | 中 | P7 测试覆盖；限制嵌套层数 |
| 旧工程迁移失败 | 中 | 每次 schema 变更前自动备份；写迁移工具 |
| 工期估算过乐观 | 中 | 每阶段留 20% buffer；M1 完成后重新估 M2 |

---

## 里程碑发布

### M1（4-5 周）— "可用专业 HMI 设计器"
完成 P0-P5。能做大部分常规 HMI 项目（按钮、I/O 域、棒图、报警、趋势），属性面板和事件模型已对标 WinCC。

### M2（4-7 周）— "对标 WinCC 的完整设计器"  ✅ 收官 2026-05-13

完成 P6-P10。M2 交付内容（实测）：

- **26+ 控件**：基础 10（text/rectangle/ellipse/line/polyline/polygon/button/round-button/graphic-view/clock）
  + 数据 6（io-numeric/io-symbolic/io-graphic/datetime/combobox/listbox）
  + 控制 5（switch/checkbox/optiongroup/slider/scrollbar）
  + 可视化 3（bar/gauge/trend-view）
  + 业务 5（recipe-view/user-view/diagnostic-view/alarm-indicator/status-force/alarm-view/table-view/screen-window）
  + 媒体/分析 5（html-browser/pdf-view/media-player/xy-trend/report-view）
- **数据模型**：事件多动作链 + 系统函数目录 + 4 类移动动画 + 外观/可见性动画
- **Faceplate**：复合控件模板 + 接口属性 + 版本号 + 内置 4 个示例
- **样式 / 多语言**：StyleDefinitions（色板/字体）+ TextResources + StyleResolver
- **库**：项目库 + 全局库 + Symbol 库 + 工程级 ListResources
- **配方 / 报表**：RecipeService + ReportTemplate
- **运行时**：RoleBasedAccessGuard + RuntimeDataBindingService + 撤销/重做
- **质量**：27 widget schema 覆盖 + 多选编辑 + zip 打包 + 离线模拟 + SQLite 历史归档（7 天滚动）

### 远期（v3.0）— 超越 WinCC 的差异化

#### M2 延期到 v3.0 的项

- **采集周期分级**：变量级别 连续 / 显示时 / 按需 / 变化时；OPC UA Subscription 共享 Session 优化
- **数组 / UDT 路径绑定增强**：`Tag[i].Member` 自动补全 + 类型推断
- **性能优化**：1000+ widget 画布虚拟化、按视口剔除；属性面板 O(n²) 通知规避
- **报表模板编辑器**：可视化编辑（当前仅 widget 通过 templateId 引用）
- **区域指针**：S7 用户群可选

#### 全新方向

- 接入 AI 辅助：自然语言生成控件 / 自动布局 / 自动绑定 Tag
- 云端协作：多人同时编辑工程，OT 冲突合并
- Web 运行时：浏览器中跑 HMI 画面（Blazor / WebGL）
- 移动端：手机/平板查看与简易操作（MAUI / RN）
- 真实历史报表：从 SQLite 后端导出 Excel/PDF
- 大屏可视化：多屏拼接 + 数据驾驶舱模板

---

## 启动建议

立即开始 **P0 + P1 + P2** 三个阶段（约 10 天），完成后能看到：
- 工具箱被清洗、不再混乱
- 属性面板对标 WinCC 的四 Tab 模型
- 事件多动作链能用
- 6 类标准动画能用

这是个能给客户演示"我们设计器升级了"的最小可见成果。然后根据反馈决定 P3-P5 的细节侧重。

---

## 待你最终确认

- [ ] 路线图整体认可？是否漏了你关心的功能
- [ ] P0 删除业务控件的范围（特别是 motor / indicator / value-display / alarm-banner）是否过激？要不要保留过渡？
- [ ] M1 / M2 里程碑划分接受？
- [ ] 我现在开始 P0 ？

回复「按这个走 P0 开干」我立即开 PR-0；或者你指出要调整的点（增减阶段、改顺序、改范围），我修订后再确认。
