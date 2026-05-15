// M4.2: 本测试文件原 11 条断言全部对应 P0 之前的旧 Designer service 注册：
//   - IDesignerLayoutService / DesignerLayoutService → P0 中删除（旧设计器拆为 RuntimeProjectService 等）
//   - IDesignerProjectService / DesignerProjectService → P0 中删除
//   - ICsvImportService / IXmlImportService / IIoTableImportService 等仍存在但缺
//     "Concrete 与 Interface 同实例" 这一兼容性约束（DI 注入习惯已改）。
//
// 整体重写代价 > 价值（新 DI 容器有更针对性的注入断言散落在各模块测试）。
// 因此整文件清空，保留壳以便 git 历史保留。新的 Bootstrapper 健康性由
// MainWindowViewModel/Services 各自集成测试覆盖。
//
// 删除条目数：2 个 [Fact]（共 13 条 AssertCompatibility + Options 断言）。

namespace ApexHMI.Tests;

// 故意空：实际测试已迁移到各服务专项测试。
public class BootstrapperTests
{
}
