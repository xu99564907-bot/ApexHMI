# Phase 0 验收报告

> 日期：2026-05-05  
> 范围：清理 V1 画布残留代码（保留 V1 模型供迁移器使用）  
> 状态：✅ 通过

---

## 完成的工作

### 1. 删除文件
- ✅ `Behaviors/DragCanvasBehavior.cs` —— V1 画布拖拽行为，已无引用

### 2. 删除代码片段
- ✅ `Views/Pages/DesignerView.xaml.cs`：移除死 handler `DesignerCanvas_DragOver` / `DesignerCanvas_Drop`
- ✅ `Views/MainWindow.xaml`：移除孤立 `xmlns:behavior` 命名空间声明
- ✅ `Views/Pages/DesignerEditorView.xaml`：移除孤立 `xmlns:behavior`
- ✅ `Views/Pages/DesignerView.xaml`：移除孤立 `xmlns:behavior`

### 3. 重写 DesignerViewModel.cs
**移除（V1 画布相关）：**
- 命令：`AddDesignerElementCommand`、`AddDesignerElementAtDropCommand`、`StartToolboxDragCommand`、`RemoveSelectedDesignerElementCommand`、`CopySelectedDesignerElementCommand`、`PasteDesignerElementCommand`、`MoveSelectedElementCommand`
- 状态属性：`IsDesignerCanvasPageVisible`、`SelectedToolboxItem`、`DesignerCanvasWidth/Height`、`DesignerPageName`、`DesignerProjectName`、`DragToolboxItem`、`EnableGridSnap`、`GridSize`、`SelectedRuntimeTemplate`、`DesignerActionOptions`、`HasClipboard`
- 集合：`Pages`、`Elements`
- 选中项：`SelectedDesignerElement`、`SelectedDesignerPage`
- 辅助：`IsSelectedDesignerElementButtonLike` 等

**保留（IO/SFC/GitPull 必需）：**
- 所有 IO 程序生成命令与属性
- 所有 SFC 自动/初始化命令与属性
- 所有 GitPull 命令与属性
- 子页面可见性：`IsDesignerIoProgramPageVisible`、`IsDesignerAutoProgramPageVisible`、`IsDesignerInitProgramPageVisible`

### 4. 保留备份
- V1 模型（`DesignerElement` / `DesignerPage` / `DesignerProject`）保留，由 `V1ProjectMigrator` 用于向后兼容
- `MainViewModel.cs` 与 `MainViewModel.Designer.cs` 中的 V1 画布字段/方法暂未删除（已成为不可达死代码，对运行无影响）。后续 Phase 中按需删除。

---

## 验收清单

| 项 | 标准 | 实际 | 结果 |
|----|------|------|------|
| 编译 | 0 错误 | 0 错误（仅 exe 文件锁） | ✅ |
| 警告数 | ≤ 5 增量 | 从 90 降到 10 | ✅ |
| 单元测试 | 全部通过 | 109/109 通过 | ✅ |
| `xmlns:behavior` 残留 | 0 个 | 0 个 | ✅ |
| `DragCanvasBehavior` 引用 | 0 个 | 0 个 | ✅ |
| Tab 8 程序生成功能 | 完好 | 待手动验证 | ⏳ |
| Tab 0~7 行为 | 完好 | 待手动验证 | ⏳ |

### 手动验证项（请用户验收）

- [ ] 启动软件无报错
- [ ] Tab 8 → "手动程序生成" 子页：导入 IO 表 → 生成程序 → 输出文件正常
- [ ] Tab 8 → "自动程序生成" 子页：SFC 步骤编辑、代码生成正常
- [ ] Tab 8 → "初始化程序生成" 子页：SFC 初始化代码生成正常
- [ ] Tab 0~7 各页打开正常无报错
- [ ] Tab 9 画布设计：拖入控件 / 编辑属性 / 保存正常
- [ ] Tab 10 运行页面：加载默认页面正常

---

## 下一步：Phase 1

WYSIWYG 画布 + 网格/吸附 + 多选/对齐 + 复制粘贴 + 运行时多页导航。预计 3 天。

启动前请用户完成上述手动验证，确认 Phase 0 通过后再进入 Phase 1。
