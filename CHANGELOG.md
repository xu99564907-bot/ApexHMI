# Changelog

ApexHMI 所有重要变更记录，格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)。

## [V0.8] - 2026-05-09

**首次纳入版本号管理**。标题栏显示 `ApexHMI V0.8`，
csproj 用 `<Version>0.8.0</Version>` + `<AssemblyVersion>` + `<FileVersion>` 三连记录。

### 已完成总览（97 项 / 25+ commit）
- **Phase 2 全部完成**（83 项 = H1-H7 + M3-M27 + MA2-MA10 + P1-P10 + A1-A10 + R1-R9 + D1-D9 + AU1-AU8 + L4-L6）
- **Phase 3 视觉重构骨架 + G5 主题切换**（Theme.Dark / Theme.HighContrast / RingProgressBar / KpiCard / 428 处 hex → DynamicResource / 字体模糊修复 / 菜单 + 主导航 + 子导航 active 视觉对齐 mockup）
- **Phase 4 锦上添花 11 项**（G2 缩放 / G4 事件中心 / H10 KPI 状态色 / M16 OPC 收藏 / M18 写入测试 / M23 跳转 / M24 关键步 / M25 回放速率 / M26 报警弹层 / M27 PDF / MA4 气缸分组 / H12 KPI 隐藏）
- **PLC 联调准备**：解析 InoProShop test13.Device.Application.xml；sample-tags.template.csv 用 `{OP00/02/03/05/50/70}` 占位符；TagNodeIdResolver + IoOperationNumber 变化时自动重 resolve；43 Tag 全部跟工位
- **测试**：tools/SmokeVerify 73 项断言全过，主项目 0 编译错误

### 后续版本规划
- **V0.9** 计划：Phase 1 联调（PLC 简化合约 DB`{OP}`03_Count 落地）+ M14 DI/DO Tab + F13 G10 权限边界 50+ 按钮统一
- **V1.0** 目标：现场试运行验证 + 视觉细节微调 + 性能/稳定性回归

## [Unreleased]

### Added
- 引入 Serilog 结构化日志（P0-LOG）
- 全局异常拦截与崩溃报告（P0-EXC）
- 凭据加密 SecretProtector（P0-SEC）
- 统一异步事件处理模板 AsyncEventHandler（P0-ASY）
- 服务层接口抽取 + DI 注册（P1-SVC）
- OpcUaService 拆分为 3 个独立类（P1-OPC）
- 线程模型规范化文档 + 同步原语审计（P1-THR）
- MonitorPage / AlarmPage / RecipePage / ParameterPage ViewModel 拆分（P1-VM）
- xUnit 测试项目 + Service 层 60% 覆盖率（P2-TEST）
- GitHub Actions CI 流程（P2-TEST-06）
- 配置合并为 appsettings.json + JSON Schema（P2-CFG）
- ObservableCollection 虚拟化 + DispatcherTimer 审计（P2-UI）
- Roslynator + StyleCop 代码分析器（P3-CI-01）
- PR 模板与编辑器配置（P3-CI-02）
- 国际化 ResX 资源文件 zh-CN/en-US + LocalizationService + LocExtension（P3-I18N-01）
- MainWindow / HomeView XAML 字符串迁移至 LocExtension（P3-I18N-02）

### Fixed
- ConfigurationService DecryptSecrets 调用位置错误（BUG-CONFIG-01）
- FlowLogCsvService 不识别引号内换行（BUG-FLOWCSV-01）
- Converters/RobotConverters.cs NotImplementedException → Binding.DoNothing（P2-UI-03）

### Changed
- async void 事件处理器全面改造（P0-ASY）
- OpcUaService 同步原语统一为 ConcurrentDictionary（P1-THR）
- 魔法数迁移至配置类（P2-CFG-04）
