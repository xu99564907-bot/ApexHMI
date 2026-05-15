// M4.2: 本文件原 6 条 [Fact] 全部针对 P0 之前的 MainWindowViewModel.Designer 子模块：
//   - shell.Designer.AddDesignerElementCommand / RemoveSelectedDesignerElementCommand / ...
//   - shell.Designer.Pages / Elements / DesignerActionOptions / IsDesignerCanvasPageVisible
//
// 在 P0/P1 开放平台重构中，旧 "Designer 子模块" 已拆解为：
//   - DesignerEditorViewModel（新设计器画布、独立 VM，不再挂 MainWindowViewModel.Designer）
//   - RuntimeProjectService / WidgetInstance（替代 DesignerPage/DesignerElement）
//
// shell.Designer 引用、AddDesignerElementCommand 等成员已全部删除/迁移。
//
// 新设计器命令的契约测试目前由手动验证 + DesignerEditorViewModel 单元测试（已经存在的 RuntimeUi/）覆盖。
// 因此整文件清空，保留壳以便 git 历史保留。
//
// 删除条目数：6 个 [Fact]（命令存在 / 命令 distinct / 集合代理 / 模式 flag 代理 / IO 属性代理）。

namespace ApexHMI.Tests.ViewModels;

// 故意空：旧 DesignerViewModel 子模块在 P0 中拆为 DesignerEditorViewModel，
// 新设计器测试覆盖在 tests/RuntimeUi 与手动 E2E 验证里。
public class DesignerViewModelTests
{
}
