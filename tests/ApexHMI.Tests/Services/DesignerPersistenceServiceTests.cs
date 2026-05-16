// M4.2: 本文件原 6 条 [Fact] 全部针对 P0 之前的旧设计器持久化：
//   - DesignerLayoutService / DesignerPage / DesignerElement
//   - DesignerProjectService / DesignerProject
// 在 P0/B1 重构中，新开放平台运行时拆分为 RuntimeProjectService + WidgetInstance + ProjectDocument，
// 旧 DesignerXxx 类型已彻底删除。
//
// 新模型的持久化测试由 RuntimeProjectServiceTests + ProjectPackageServiceTests 覆盖。
// 因此整文件清空，保留壳以便 git 历史保留。
//
// 删除条目数：6 个 [Fact]（Layout 3 + Project 3）。

namespace ApexHMI.Tests.Services;

// 故意空：旧 DesignerLayoutService/DesignerProjectService 已删除，新模型由
// RuntimeProjectServiceTests + ProjectPackageServiceTests 覆盖。
public class DesignerPersistenceServiceTests
{
}
