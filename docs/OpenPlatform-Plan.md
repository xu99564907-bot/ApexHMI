# 开放平台（HMI 画布设计）开发计划

> 版本：v1.0  
> 制定日期：2026-05-05  
> 目标：让 ApexHMI 在保留 PLC 程序生成能力的同时，具备类似 WinCC / 威纶通 EasyBuilder 的画面组态能力。

---

## 1. 项目目标与四大功能定位

ApexHMI 软件四大功能：

| # | 功能 | 状态 |
|---|------|------|
| 1 | IO 表导入 → 生成手动程序 + SFC 自动/初始化流程 | ✅ 已完成 |
| 2 | GitHub 代码管理 | ✅ 已完成 |
| 3 | IO 表解析 → 自动生成手动操作画面（气缸/轴/机械手/挡停） | ✅ 已完成（**本计划要做"路径 B 升级"**） |
| 4 | 画布设计：无需改代码增删页面、编辑控件、自定义功能 | 🚧 本计划主体 |

---

## 2. 三层架构（核心兼容方案）

```
┌─────────────────────────────────────────────────────────┐
│ 第一层：固化业务页面（Tab 0~7，不可设计）                 │
│   主界面 / 监控 / 配方 / 参数 / 报警 / 登录 / 审计         │
│   保留现状，业务逻辑稳定，不重写为通用 widget              │
└─────────────────────────────────────────────────────────┘
                          ↓ 提取可复用业务能力
┌─────────────────────────────────────────────────────────┐
│ 第二层：业务组件库（暴露成 widget 给设计器使用）           │
│   alarm-list / recipe-status / trend-chart /            │
│   parameter-display / opc-tag-table /                   │
│   manual-cylinder-block / manual-axis-block / 等         │
│   设计器拖出来 = 嵌入一块"活的业务组件"                    │
└─────────────────────────────────────────────────────────┘
                          ↓ 自由组合
┌─────────────────────────────────────────────────────────┐
│ 第三层：用户设计页面（Tab 10 + 可挂主导航）                │
│   用户用基础控件 + 业务组件自由组合                        │
│   产线工位专属画面 / 复合监控大屏 / 自定义工艺画面          │
└─────────────────────────────────────────────────────────┘
```

### 关键约束

- **Tab 0~7 不进入设计器** —— 业务逻辑深、安全敏感（登录/参数/审计），保持硬编码
- **业务能力封装为 widget** —— Tab 1/2/3/5/7 的核心数据和服务，对设计器开放为控件
- **设计页面可挂主导航** —— 用户设计的页面通过 `ShowInTopNav` 标志可成为顶栏按钮
- **Tab 3 走路径 B** —— Tab 3 改造成 V2 运行时入口，IO 导入自动生成的手动画面也变成可编辑的 ProjectDocument 页面

---

## 3. 现状清盘

### 3.1 保留清单

#### V1 时代但仍在用（PLC 程序生成）
- `Services/IoTableImportService.cs`、`IoTableParser.cs`、`CsvImportService.cs`、`XmlImportService.cs`
- `Services/IoProgramGenerationService.cs`、`NamingRulesService.cs`、`GeneratedArtifactSyncService.cs`
- `Services/SfcCodeGeneratorService.cs`、`Models/Sfc/*`
- `MainViewModel.Designer.cs` 中所有 SFC/IO 相关方法（约 40+ 方法保留）
- `MainViewModel.Manual.cs` —— 手动操作页面的所有逻辑（**Tab 3 走路径 B 后部分会变成数据源服务**）

#### V2 开放平台（核心要继续完善）
- 数据模型：`Models/RuntimeUi/ProjectDocument.cs`、`PageDefinition.cs`、`WidgetInstance.cs`、`BindingSpec.cs`、`ThemePreset.cs`
- 服务：`Services/RuntimeUi/ProjectEditorService.cs`、`WidgetEditorService.cs`、`WidgetRegistry.cs`、`WidgetBlockGenerator.cs`、`Services/RuntimeProjectService.cs`、`Services/DataBinding/RuntimeDataBindingService.cs`
- ViewModel：`ViewModels/Modules/DesignerEditorViewModel.cs`、`ViewModels/Runtime/*`
- View：`Views/Pages/DesignerEditorView.xaml`、`Views/Runtime/DynamicPageHost.xaml`、`Views/Runtime/Widgets/*`

#### 基础设施（共用）
OPC UA、配置、Tag/参数、报警、配方、审计、登录、Git、本地化、所有共用模块（HomeView、MonitorView、ManualView 等 Tab 0~7）

### 3.2 删除清单（V1 画布残留）

| 文件 | 说明 |
|------|------|
| `Models/DesignerElement.cs` | V1 控件模型 |
| `Models/DesignerPage.cs` | V1 页面模型 |
| `Models/DesignerProject.cs` | V1 工程模型 |
| `ViewModels/Modules/DesignerViewModel.cs` | V1 透传 ViewModel |
| `Views/Pages/DesignerView.xaml` 中"画布设计"区域 | 拆分后保留 IO/SFC，删除画布部分 |
| `Behaviors/DragCanvasBehavior.cs` | V1 拖拽行为 |
| `MainViewModel.cs` 中：`DesignerPages`、`DesignerElements`、`SelectedDesignerElement`、`DesignerCanvasWidth/Height`、`GridSize`、`EnableGridSnap`、`DragToolboxItem`、`_clipboardElement` 等 V1 字段 |
| `MainViewModel.Designer.cs` 中：所有非 SFC/IO 的 V1 画布方法 |
| `Services/RuntimeUi/V1ProjectMigrator.cs` | 迁移完毕后删除 |
| `tests/.../DesignerViewModelTests.cs`、`DesignerPersistenceServiceTests.cs` | V1 测试 |

### 3.3 解耦工作

1. **DesignerView.xaml 拆分**：拆成 `IoProgramView`、`SfcAutoView`、`SfcInitView` 三个独立视图，"画布设计"块整块删除
2. **MainViewModel 状态字段清理**：`CurrentDesignerSubSection`、`IsDesignerCanvasPageVisible` 等 V1 画布字段
3. **导航项调整**：侧栏"设计器"组下面去掉"画布设计"子项

---

## 4. WinCC 对标差距表

| 能力 | WinCC/威纶通 | V2 现状 | 优先级 |
|------|--------------|---------|--------|
| WYSIWYG 画布 | ✅ | ❌ 占位框 | P1 |
| 多页面工程 | ✅ | ✅ | - |
| 页面树缩略图 | ✅ | ❌ | P3 |
| 运行时页面切换 | ✅ | ❌ 只能跳转按钮 | P1 |
| 控件库分组 | ✅ | ⚠ 平铺 | P2 |
| Tag 浏览器 | ✅ | ⚠ 简单下拉 | P2 |
| 属性面板分组 | ✅ | ⚠ 简单 KV | P2 |
| 状态动画（Tag 驱动属性） | ✅ | ⚠ 仅颜色 | P2 |
| 事件系统（多种事件 + 多个动作） | ✅ | ⚠ 仅 click | P2 |
| 多选 + 对齐 | ✅ | ❌ | P1 |
| 撤销/重做 | ✅ | ✅ | - |
| 复制/粘贴（跨页面） | ✅ | ❌ | P1 |
| 网格/吸附/标尺 | ✅ | ❌ | P1 |
| 业务组件接入 | ⚠（faceplate） | ❌ | **P1.5（新增）** |
| 设计页挂主导航 | ✅ | ❌ | **P1.5（新增）** |
| 路径 B：自动生成可编辑 | -（无对应） | ❌ | **P1.5（新增）** |
| 页面模板 | ✅ | ❌ | P3 |
| 控件级权限 | ✅ | ❌ | P3 |
| i18n 控件文本 | ✅ | ❌ | P3 |
| 运行时全屏 | ✅ | ❌ | P3 |
| 趋势/报警/配方专用控件 | ✅ | ❌ | P1.5 部分 / P4 |
| 脚本（VBS/Macro） | ✅ | ❌ | P4 |

---

## 5. 路径 B：Tab 3 自动生成画面与可编辑融合

### 5.1 当前 Tab 3 工作原理

- `MainViewModel.Manual.cs` 持有 `ManualCylinderBlockItem`、`ManualAxisBlockItem` 等集合
- IO 导入后自动填充这些集合
- `ManualView.xaml` 用 `ItemsControl` 把这些集合渲染成固定布局

### 5.2 改造目标

让 Tab 3 也是一个 V2 运行时（DynamicPageHost），加载的页面是 IO 导入自动生成的 ProjectDocument 页面，用户可以在画布设计里编辑这些页面。

### 5.3 改造步骤

**5.3.1 业务组件 widget 化**
- 新增 widget 类型：
  - `manual-cylinder-block`：单个气缸的手动控制块（前进/退回按钮 + 状态灯 + 报警）
  - `manual-axis-block`：单个轴（位置显示 + 使能/复位按钮）
  - `manual-robot-block`：单个机械手（运行/暂停/复位）
  - `manual-stopper-block`：单个挡停
- 每个 widget 通过 `Properties["deviceName"]` 指定它代表哪个具体设备（如 `"Cyl1"`、`"Axis2"`）
- ViewModel 内部从 `ManualCylinderBlockItem` 等数据源取实时状态、写命令调用 `OpcUaService`

**5.3.2 IO 导入触发自动生成 ProjectDocument 页面**
- 现有 IO 导入流程结尾增加一步：调用新的 `ManualPageAutoGenerator.GenerateAsync(ProjectDocument)`
- 自动生成的页面：
  - `manual.cylinders` —— 列出所有气缸 widget（按 IO 表顺序）
  - `manual.axes` —— 列出所有轴 widget
  - `manual.robots` —— 列出所有机械手 widget
  - `manual.stoppers` —— 列出所有挡停 widget
- 自动生成时**不覆盖用户已有的修改**：检查页面是否已存在且有 `[user-edited]` 标记，若有则只增量添加新设备 widget
- 生成完写回 `project.json`

**5.3.3 Tab 3 切换为 DynamicPageHost**
- 删除 `ManualView.xaml` 的硬编码布局
- 改成包含一个 `DynamicPageHost` + 顶部子页签（气缸 / 轴 / 机械手 / 挡停）
- 子页签切换 = 在 DynamicPageHost 里加载对应 routeKey 的页面
- 保留所有现有的 `MainViewModel.Manual.cs` 命令（启停/复位/紧急），通过 widget 的 ActionType 触发

**5.3.4 用户编辑流程**
- 进设计器 → 选 `manual.cylinders` 页面 → 看到所有气缸 widget
- 用户可以：删除某些气缸 widget、调整布局、改颜色、加标签、加自定义按钮
- 保存后 IO 重新导入时不会覆盖用户的修改（依赖 `[user-edited]` 标记）

### 5.4 兼容性

- 老用户首次升级时：检测 `project.json` 中没有 `manual.*` 页面 → 触发一次自动生成
- 用户也可以在设计器里手动点 "从 IO 表重新生成手动页面"（强制覆盖确认）

---

## 6. 分期实施路线图

| Phase | 工作内容 | 预估工时 | 累计 |
|-------|---------|---------|------|
| P0 | 清理 V1 画布残留 + 拆 DesignerView | 0.5 天 | 0.5 |
| P1 | WYSIWYG 画布 + 网格/吸附 + 多选/对齐 + 复制粘贴 + 运行时多页导航 | 3 天 | 3.5 |
| **P1.5** | **业务组件接入 + 设计页挂主导航 + 路径 B 改造 Tab 3** | **3 天** | **6.5** |
| P2 | 控件库分组 + 属性面板分组 + Tag 浏览器 + 事件系统 + 状态动画 | 4 天 | 10.5 |
| P3 | 页面模板 + i18n 接入 + 控件级权限 + 全屏 | 2 天 | 12.5 |
| P4 | 高级控件（趋势/报警/配方）+ 脚本引擎 | 5+ 天 | 17.5+ |

---

### Phase 0：清理废代码（0.5 天）

#### 工作内容

1. 拆 `Views/Pages/DesignerView.xaml`：
   - 提取 IO 程序生成区到 `Views/Pages/IoProgramView.xaml`
   - 提取 SFC 自动区到 `Views/Pages/SfcAutoView.xaml`
   - 提取 SFC 初始化区到 `Views/Pages/SfcInitView.xaml`
   - 删除 V1 画布设计区（DesignerElement ItemsControl 那块）
   - DesignerView.xaml 改为容器，根据 `CurrentDesignerSubSection` 切换子视图
2. 删除 V1 画布相关文件（见 §3.2 删除清单）
3. 清理 `MainViewModel.cs` 和 `MainViewModel.Designer.cs` 的 V1 字段/方法
4. 更新 `Bootstrapper.cs`、导航项、TabControl
5. 跑全部测试

#### 验收标准
- ✅ 编译 0 错误，新增警告 ≤ 5
- ✅ 所有保留的单元测试通过
- ✅ 手动验证：IO 导入 → 生成程序流程不变
- ✅ 手动验证：SFC 自动/初始化代码生成不变
- ✅ 手动验证：Tab 0~7 行为不变
- ✅ `git grep DesignerElement` / `git grep DesignerPage` / `git grep DesignerProject` 无残留引用（migrator/test 除外）

---

### Phase 1：WYSIWYG 画布 + 运行时导航（3 天）

#### 工作内容

**1.1 设计器画布所见即所得**
- 复用 `IWidgetViewFactory.Create` 在画布上渲染真实 widget
- 加透明覆盖层捕获鼠标用于选中/拖动
- 选中显示蓝色虚框 + 8 个 resize handle

**1.2 网格 / 吸附 / 标尺**
- 画布显示浅色网格（默认 8px，可配）
- 拖动时坐标吸附到网格
- 顶部 / 左侧标尺
- 状态栏显示鼠标坐标 + 选中控件位置/尺寸

**1.3 多选 + 对齐**
- 框选 + Ctrl+点选支持多选
- 工具栏：左/右/顶/底对齐、横/纵均布、相同宽/高
- Delete 删除多选

**1.4 复制 / 粘贴**
- Ctrl+C / Ctrl+V 跨页面
- 拖动时 Ctrl 复制

**1.5 运行时多页导航栏**
- DynamicPageHost 顶部加页面标签栏
- 按用户角色过滤可见页面
- 默认进入 `DefaultPageRouteKey`

**1.6 设计 ↔ 运行 闭环**
- DesignerEditor 顶栏加 "运行预览" 按钮
- DynamicPageHost 顶栏加 "返回设计" 按钮（仅 IsDesignMode 显示）

#### 验收标准
- ✅ 拖入气缸控件，画布立刻显示真容（圆灯 + 标签 + 按钮）
- ✅ 选中后能精确拖到任意网格点
- ✅ 框选 3 个控件 → 左对齐 → X 坐标相等
- ✅ 复制粘贴到另一页，属性、绑定、动作完整
- ✅ 设计 3 个页面 → 切运行 → 顶部 3 个标签可切换
- ✅ 运行时按钮触发 `write-bool` / `navigate` 动作正常

---

### Phase 1.5：业务组件接入 + 路径 B 改造（3 天）

#### 工作内容

**1.5.1 业务组件 widget 化（第一批）**

| TypeId | 复用 | 用途 |
|--------|------|------|
| `manual-cylinder-block` | `ManualCylinderBlockItem` | 单气缸手动控制 |
| `manual-axis-block` | `ManualAxisBlockItem` | 单轴控制 |
| `manual-robot-block` | 现有机械手逻辑 | 单机械手控制 |
| `manual-stopper-block` | 现有挡停逻辑 | 单挡停控制 |
| `alarm-list` | `AlarmService` | 报警实时列表（可配过滤） |
| `opc-tag-value` | `OpcUaService` | 通用 Tag 值显示 |
| `trend-chart` | `TrendHistoryService` | 趋势曲线（多 Tag + 时间窗口） |
| `recipe-status` | `RecipeService` | 当前激活配方信息 |
| `parameter-display` | `ParameterService` | 单参数只读显示 |

**1.5.2 设计器属性面板支持业务组件配置**
- 设备类 widget：`deviceName` 下拉，从 IO 表自动列出
- alarm-list：过滤等级、来源、最大显示条数
- trend-chart：Tag 列表（多选）、时间窗口、Y 轴范围

**1.5.3 设计页面挂载主导航**
- `PageDefinition` 增加 `ShowInTopNav` (bool) + `NavIcon` (string) + `NavOrder` (int)
- DesignerEditor 页面属性能勾选 "显示在主导航"
- MainWindow 顶栏在固定按钮之后动态追加用户设计页面按钮

**1.5.4 路径 B：Tab 3 改造**
- 新增 `Services/RuntimeUi/ManualPageAutoGenerator.cs`：从 IO 表生成 `manual.*` 页面
- IO 导入流程末端调用自动生成
- 改造 `Views/Pages/ManualView.xaml`：删除硬编码布局，改为 DynamicPageHost + 子页签
- 实现 `[user-edited]` 标记机制，避免覆盖用户编辑

#### 验收标准
- ✅ 设计器拖入 alarm-list，运行时显示真实当前报警
- ✅ 设计器拖入 trend-chart 配 3 Tag，运行时显示曲线
- ✅ 设计器拖入 manual-cylinder-block 选 "Cyl1"，运行时按钮可点且写回 PLC
- ✅ 设计页面勾选 "显示在主导航" → 主导航出现按钮 → 点击进入
- ✅ Tab 3 显示自动生成的气缸/轴/机械手/挡停子页签
- ✅ 在画布设计修改 `manual.cylinders` 页面（删 1 个气缸、加文字标签）→ 重新进 Tab 3 修改保留
- ✅ 重新导入 IO 表 → 用户已编辑的页面增量更新（新增设备追加，旧设备布局保留）

---

### Phase 2：控件库 + 属性面板 + 事件（4 天）

#### 工作内容

**2.1 控件库分组**
- 工具箱按分组：基础 / 工业 / 数据 / 可视化 / 容器 / 业务组件
- 新增基础控件：rectangle、image、line
- 新增数据控件：numeric-input、text-input、combo

**2.2 属性面板分组**
- 几何（X/Y/W/H/旋转）
- 外观（颜色/边框/字体/圆角/不透明度）
- 数据绑定（Tag 浏览器 + 数据类型 + 刷新模式）
- 动态/状态（True/False 颜色、值驱动可见、值驱动文本、闪烁）
- 事件（多事件多动作）
- 安全（最小角色、运行时只读）

**2.3 Tag 浏览器对话框**
- 按分类树展开（IO 输入/输出/Axis/Cylinder/Robot/Motor/Mode/...）
- 搜索框（模糊匹配）
- 显示数据类型、当前值、节点 ID
- 双击选中

**2.4 事件 / 动作系统**
- 事件：click、value-changed、page-loaded
- 动作：write-bool、write-int、write-float、navigate、show-dialog
- 一个事件可串多个动作（按顺序执行）

**2.5 状态动画**
- WidgetInstance 增加 `Animations` 列表
- 触发条件（Tag + 比较运算） + 效果（属性 = 值）
- 渲染时实时计算

#### 验收标准
- ✅ 工具箱可折叠/展开分组
- ✅ 属性面板 6 个分组
- ✅ Tag 浏览器分类展开，搜索 "Cyl" 筛出气缸
- ✅ 按钮配 2 个动作（写 Tag + 跳转），运行时按序执行
- ✅ 文本控件配状态动画（Tag=True 红色、False 绿色），运行时颜色跟随
- ✅ 数值输入控件运行时输入并写回 OPC

---

### Phase 3：模板 / i18n / 权限 / 全屏（2 天）

#### 工作内容

**3.1 页面模板**
- 项目级模板 = 特殊页面，所有页面继承其控件
- 模板控件不可在子页编辑，但子页可叠加自己的控件

**3.2 i18n 接入**
- 控件 text/label 支持 `${KEY}` 语法引用资源
- 资源在现有 `LocalizationService` 注册
- 设计器属性面板提供 "绑定文本资源" 入口

**3.3 控件级权限**
- WidgetInstance 增加 `RequiredRole` 字段
- 运行时按当前用户角色显示/禁用

**3.4 运行时全屏**
- F11 切换全屏
- 全屏时隐藏标题栏 / 主导航 / 状态栏
- ESC 退出

#### 验收标准
- ✅ 模板加 Logo+时间，所有页面运行时显示
- ✅ 控件 text 设 `${Cyl1Label}`，切换语言文本随之变化
- ✅ 按钮设角色 = Engineer，操作员登录时禁用
- ✅ F11 进全屏，ESC 退出，期间能正常操作

---

### Phase 4：高级控件 + 脚本（按需，5+ 天）

- 趋势曲线（深度接入 `TrendHistoryService`，多 Y 轴、缩放、导出）
- 报警表格（接入 `AlarmService`，分级排序、确认、过滤）
- 配方表格（接入 `RecipeService`，编辑/上下载）
- 仪表盘 / 进度条 / 旋钮
- 脚本引擎（C# Roslyn 或 JS 嵌入）

---

## 7. 验收流程

### 7.1 验收方式

每期独立验收，做完一期再做下期：

1. **代码验收**
   - 编译 0 错误
   - 新增警告 ≤ 5
   - 所有单元测试通过
   - 新代码单测覆盖率 > 60%
   - Git commit 按 Phase 标签

2. **功能验收（演示式 FAT）**
   - 录屏/截图按验收清单逐项演示
   - 用户逐项打 ✅ / ❌
   - ❌ 项下一轮修复

3. **回归验收**
   - 跑 Tab 0~7 + IO/SFC 生成的回归脚本
   - 确认未影响既有功能

### 7.2 文档交付物

- 每期写 `docs/Phase{N}-Acceptance.md`：
  - 完成功能列表
  - 涉及文件清单
  - 已知问题/限制
  - 验收清单 + 实际结果
- Git commit 标签：`[OP-P0]`、`[OP-P1]`、`[OP-P1.5]` 等

### 7.3 总验收（项目结束）

- 完整文档：`docs/OpenPlatform-UserGuide.md`（用户手册）
- 演示视频：从空项目→设计→运行的完整 demo
- 性能基准：1000 个 widget 的工程加载/渲染时间

---

## 8. 风险与应对

| 风险 | 应对 |
|------|------|
| WYSIWYG 性能问题（大量 widget） | 虚拟化渲染（仅渲染可视区域） |
| 路径 B 兼容老 project.json | 自动迁移 + `[user-edited]` 标记机制 |
| 业务组件改动影响 Tab 0~7 | 业务组件 widget 内部隔离，不修改原有 ViewModel/Service |
| 控件类型扩展导致 widget 库膨胀 | 按需加载 + 工具箱分组 |
| Phase 4 脚本引擎安全风险 | 沙箱化 + 仅 Engineer+ 可编辑脚本 |

---

## 9. 当前推进状态

- [x] 计划制定
- [ ] Phase 0 进行中
- [ ] Phase 1
- [ ] Phase 1.5
- [ ] Phase 2
- [ ] Phase 3
- [ ] Phase 4（按需）

---

**文档变更记录**

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0 | 2026-05-05 | 初版，三层架构 + 路径 B + 五期路线图 |
