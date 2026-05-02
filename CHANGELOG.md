# Changelog

ApexHMI 所有重要变更记录，格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)。

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
