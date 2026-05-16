# ApexHMI 编码规范

## 时间戳：持久化用 UTC、显示转本地

> 引入版本：M7.3。
> 背景：HMI 现场跨时区部署或跨服务器复制 SQLite/JSON 时，Local 时间戳会因时区漂移产生
> 错乱（甚至同一台机器调整夏令时也会出现"未来记录"）。
> 解决：**所有持久化路径只写 UTC，UI 显示侧统一 `.ToLocalTime()` 转回**。

### 适用范围

涉及"写到磁盘 / 写到 SQLite / 写到 JSON / 跨进程共享"的时间戳，必须使用 UTC：

| 服务 / 字段 | 写入位置 | 来源时间 |
| --- | --- | --- |
| `AuditServiceSqlite.LogOperationAsync` | `data/audit.db` audit_log.timestamp | `DateTimeOffset.UtcNow` |
| `AuditService`（CSV fallback） | `data/audit.csv` 第 1 列 | `DateTime.UtcNow` |
| `UserService.Authenticate` | `config/users.json` LastLoginAt | `DateTime.UtcNow` |
| `UserService.ChangePassword` | `config/users.json` PasswordChangedAt | `DateTime.UtcNow` |
| `AccountLockoutService.RegisterFailure` | `data/audit.db` account_lockout.locked_until_unix_ms / last_failure_unix_ms | `DateTime.UtcNow` |
| `SessionManager._lastActivity` | （仅内存，但为统一基准） | `DateTime.UtcNow` |
| `TrendHistoryService.LogValue` | `data/trend_history.db` tag_history.ts | `DateTime.UtcNow` |

### 例外：保留 Local 的字段

| 服务 / 字段 | 原因 |
| --- | --- |
| `ProductionCountService` 班次/日切边界 | 生产业务的"今日产量""本班产量"以**现场本地时间**为准；切到 UTC 会破坏班次窗口。 |

### UI 显示侧

- DataGrid / TextBlock 绑定 UTC `DateTime` 字段时，要么在 ViewModel 暴露 `XxxLocal` 投影（`get => Xxx?.ToLocalTime()`），
  要么走 ValueConverter；不要直接绑定 UTC 字段然后用 `StringFormat`。
- 范例：`Models/UserAccount.cs` 的 `LastLoginAtLocal`、`AuditServiceSqlite.QueryAsync`
  返回时已转 `LocalDateTime`。

### 编码守则

1. 提交涉及 `DateTime.Now` 的代码前，自问"这个值会被写到磁盘 / DB / 跨进程吗"。
   - 是 → 必须 `DateTime.UtcNow`
   - 否（仅内存比较 / 仅本地显示） → 可保留 `DateTime.Now`，但建议统一 `UtcNow` 减少认知负担
2. 反序列化时若 `DateTime.Kind == Unspecified`，按 **Local** 解释（System.Text.Json 默认行为）；
   M7.3 之后写入的字段 Kind 必为 Utc，旧数据可能为 Local，调用方应做 `Kind` 检查。
3. SQLite 推荐用 `INTEGER`（Unix ms）存时间戳，避开 SQLite 文本时间的时区歧义；
   读出后 `DateTimeOffset.FromUnixTimeMilliseconds(x).LocalDateTime` 直接给 UI。
