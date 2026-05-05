# Phase 1.5 验收报告：业务组件接入 + 路径B

> 日期：2026-05-05  
> 范围：业务复合控件、设计页挂主导航、IO 自动生成可编辑页面、Tab 3 路径B切换

---

## 完成工作清单

### P1.5.1：气缸 widget (`manual-cylinder-block`)
- 新增 `Views/Runtime/Widgets/ManualCylinderBlockWidget.xaml`
- 新增 `ViewModels/Runtime/ManualCylinderBlockWidgetViewModel.cs`
- 复用 Tab 3 完整气缸卡片 UI（标题/工作位/原位按钮/传感器灯/状态提示）
- `IWidgetDataContext.Shell` 暴露 Shell，供业务 widget 访问真实数据
- 旧 `cylinder` typeId 自动升级到新控件（兼容）
- 设计模式属性面板修改 `deviceName` → 卡片即时切换

### P1.5.2：deviceName 下拉框
- `WidgetPropertyItem.IsDeviceNameProp` 标志
- `DesignerEditorViewModel.AvailableDeviceNames` 按 widget TypeId 自动列出真实设备
- XAML 属性面板：`deviceName` 项渲染为可编辑 ComboBox（IsEditable=True 也允许手输）

### P1.5.3：批量生成升级
- `WidgetBlockGenerator.BuildCylinder/Axis/Robot/StopperBlock` 全部改为生成单个业务复合 widget
- `GenerateForDevices` 重载：按真实设备名列表生成
- `DesignerEditorViewModel.ResolveBatchDeviceNames`：cylinder/axis 优先取 Shell 真实设备

### P1.5.4：轴 / 机械手 / 挡停 widget
- `manual-axis-block`：复用 `ManualAxisBlockItem`，简化卡片（位置/速度 + 使能/停止/回原点 + Jog + 状态/报警）
- `manual-robot-block`：直接嵌套 `RobotControl` + `Shell.RobotControlViewModel`
- `manual-stopper-block`：单按钮切换挡停 Tag + 实时值显示
- 旧 `axis/robot/stopper` typeId 自动升级到对应业务控件

### P1.5.5：业务控件第二批
- `alarm-list`：实时报警列表（filterLevel/filterSource/maxRows/onlyActive 过滤），等级色块、时间倒序
- `opc-tag-value`：通用单 Tag 值显示（label + value + unit + format）
- 工具箱分组重排（基础 / 业务复合 / 业务数据 / 旧通用）
- trend-chart 推迟到 Phase 4（需图表库与历史采样）

### P1.5.6：设计页挂主导航
- `PageDefinition` 改为 `ObservableObject`，新增 `ShowInTopNav / NavIcon / NavOrder`
- 设计器页面设置区新增"挂到主导航顶栏"复选框 + 导航图标 + 排序
- `MainWindowViewModel.TopNavUserPages` + `NavigateToUserPageCommand`
- MainWindow 顶栏第 8 列改为 ItemsControl，动态渲染用户页按钮
- Save / Publish 后自动 RefreshTopNavUserPages

### P1.5.7-A：路径 B 第一步：IO 自动生成可编辑页
- 新增 `Services/RuntimeUi/ManualPageAutoGenerator`：从 IO 表生成
  `manual.cylinders / manual.axes / manual.robots / manual.stoppers` 页面
- `PageDefinition.IsUserEdited`：保护用户编辑过的页面（IO 重新导入只追加新设备）
- `MainWindowViewModel.RegenerateManualPages()`：在 IO 程序生成后自动调用
- `DesignerEditorViewModel` 各编辑操作（Add/Drop/Remove/Move/UpdateProperty/BatchGenerate）触发 `MarkPageEdited()`

### P1.5.7-B：Tab 3 设计器布局切换
- `MainWindowViewModel.UseDesignerManualLayout`（默认关闭）
- 独立 `ManualPage : DynamicPageHostViewModel`（与 Tab 10 RuntimePage 隔离）
- `LoadManualPageForCurrentSubSectionAsync`：子页签 → manual.* 页映射加载
- `OnCurrentManualSubSectionChanged` 联动加载
- ManualView.xaml：顶部加 "🎨 使用设计器布局" 复选框；新 DynamicPageHost 容器（开关打开时显示）；旧硬编码 UI 用 DataTrigger 整体折叠

---

## 验收清单

### 准备
1. 启动软件，进 Tab 8 「程序生成」
2. 导入 IO 表 → 点击"生成 IO 程序"
3. 等待完成（应看到日志 "MainWindowViewModel: 手动页面已根据 IO 重新生成"）

### A. 自动生成 manual.* 页验证
- [ ] 进 Tab 9「画布设计」，左栏页面列表多出 `气缸/轴/机械手/挡停`
- [ ] 点击 `气缸` 页 → 画布上自动布局了所有气缸的卡片
- [ ] 点击 `轴` 页 → 显示所有轴卡片
- [ ] 进 Tab 10「运行页面」 → 也能加载这些页面（通过页内跳转或主导航）

### B. 业务 widget 显示真容
- [ ] 工具箱拖一个 `manual-cylinder-block` → 显示完整气缸卡片，与 Tab 3 一致
- [ ] 工具箱拖一个 `manual-axis-block` → 显示轴卡片（位置/速度/按钮）
- [ ] 工具箱拖一个 `manual-robot-block` → 显示 RobotControl
- [ ] 工具箱拖一个 `manual-stopper-block` → 显示挡停按钮 + 实时值
- [ ] 工具箱拖一个 `alarm-list` → 显示当前报警列表
- [ ] 工具箱拖一个 `opc-tag-value` → 显示某 Tag 值

### C. deviceName 下拉
- [ ] 选中气缸 widget → 右栏 `deviceName` 是下拉框，列出所有真实气缸
- [ ] 选中轴 widget → `deviceName` 列出所有真实轴
- [ ] 改 `deviceName` → 画布上卡片立即切换

### D. 用户编辑保护
- [ ] 编辑 `气缸` 页（删一个气缸/改位置/加文字标签），保存
- [ ] 重新执行 IO 导入 + 程序生成
- [ ] 重新进 `气缸` 页 → 之前的编辑保留，新增气缸追加到末尾

### E. 设计页挂主导航
- [ ] 选中任意页 → 勾选"挂到主导航顶栏"，图标填 `Cog`，排序 `1`，保存
- [ ] 主导航顶栏出现该页面图标按钮
- [ ] 点击 → 切换到运行页 + 加载该页面

### F. Tab 3 设计器布局切换
- [ ] 进 Tab 3「手动操作」→ 默认显示原硬编码 UI
- [ ] 顶部勾选"🎨 使用设计器布局" → 切换为 DynamicPageHost
- [ ] 切换 气缸/轴/机械手/挡停 子页签 → DynamicPageHost 自动加载对应 manual.* 页
- [ ] 取消勾选 → 恢复原 UI，所有原功能正常

### G. 回归
- [ ] Tab 0~7 各页打开正常
- [ ] Tab 8 IO/SFC 生成不变
- [ ] Tab 9/10 自身仍正常
- [ ] 编译 0 错误，单测 109/109 通过

---

## Git 提交

| Commit | 说明 |
|--------|------|
| b14addb | OP-P1.5.1 气缸 widget |
| f085f59 | OP-P1.5.1-fix 设计模式属性即时刷新 |
| 39bd44f | OP-P1.5.4 轴/机械手/挡停 widget |
| 8757b43 | OP-P1.5.2 deviceName 下拉 |
| e3fd9dd | OP-P1.5.6 页面挂主导航 |
| ecdbbed | OP-P1.5.7-A 路径B IO 自动生成 |
| 7503bb8 | OP-P1.5.7-B Tab 3 设计器布局切换 |
| 51e7052 | OP-P1.5.5 alarm-list / opc-tag-value |

---

## 已知限制 / 后续

- trend-chart 推迟到 Phase 4
- 业务 widget 不支持自定义颜色/字体（继承业务卡片样式）
- 工具箱仅顺序分组，未做可折叠分类（留至 Phase 2）
- 属性面板 deviceName 之外仍是简单 KV 表（Phase 2 改进）
- Path B 中 ManualPage 与 Tab 10 RuntimePage 是两个独立 VM；如要支持机械手等单实例资源跨 Tab，需后续协调
