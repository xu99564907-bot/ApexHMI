## 变更描述

<!-- 简要描述此 PR 做了什么 -->

## 关联任务

<!-- 引用开发任务编号，例如 P0-LOG-01 -->

## 变更类型

- [ ] 新功能
- [ ] Bug 修复
- [ ] 重构（无功能变更）
- [ ] 文档更新
- [ ] 构建/CI 变更
- [ ] 其他（请说明）

## 测试

- [ ] `dotnet build` 通过
- [ ] `dotnet test` 通过
- [ ] 手动启动 ApexHMI.exe 验证通过

## 自查清单

- [ ] 无 `async void` 事件处理器外使用
- [ ] 异常被记录（Serilog），无空 catch
- [ ] 密码/凭据使用 SecretProtector 保护
- [ ] ObservableCollection 操作在 UI 线程
- [ ] 新增 public API 包含 XML 注释
