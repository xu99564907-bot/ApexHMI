# 生产计数模块设计

> 目标：把传统由 PLC 维护的"分时计数 / 班次合计 / 31 天历史"账本搬到 HMI 侧，PLC 只暴露最少的累计计数器。
> 受益：PLC 程序大幅简化、HMI 历史不限 31 天、报表/查询/分组维度可任意扩展、断网自动补漏。

本文档分两部分：
1. **PLC 接口最小化合约**——给 PLC 同事看，明确要保留/删除什么
2. **HMI 计数模块设计**——给我（实现侧）看，明确组件、数据流、查询接口

---

## Part 1：PLC 接口最小化合约

### 1.1 保留的字段（每个计数源一份：OK / NG）

```
.Total : UDINT     // 累计计数。PLC 内部只 +，永不自动清零
.Clear : BOOL      // 维修复位。HMI 写 TRUE，PLC 复位 Total=0 后自己写回 FALSE
```

OPC UA 路径：
- `Application.DB8003_Count.OK.Total`
- `Application.DB8003_Count.OK.Clear`
- `Application.DB8003_Count.NG.Total`
- `Application.DB8003_Count.NG.Clear`

> 注：传统的 "Total"（OK + NG 累加）实例可以**整个删掉**——HMI 自己 `OK + NG = Total`，不需要 PLC 再维护一份。

### 1.2 可以从 `Str_Count` / `Str_CountToday` 删除的字段

| 字段 | 删除原因 |
|---|---|
| `Execute : BOOL` | 改用 Total 差值法捕获，无需脉冲信号 |
| `Num : UINT` | 同上 |
| `Today.Total / Day / Night` | HMI 按时间戳算 |
| `Today.Hour[0..23]` | HMI 按事件时间归桶算 |
| `Today.TimeOfHour[0..23] / TimeOfMinu` | HMI 按 config 里的班次开始时间算 |
| `Befor[0..30]` | HMI 数据库无限保留 |
| `DateBefor[0..30]` | 同上 |

`FB_Count` 从 ~190 行简化到大约这样：

```pascal
FUNCTION_BLOCK FB_Count
VAR_IN_OUT
    ioCount : Str_Count;   // 现在只有 Total / Clear 两个字段
END_VAR
VAR_INPUT
    iTrigger : BOOL;       // 计数触发信号（来自检测传感器/工位完成信号）
END_VAR
VAR
    Trigger_R : R_TRIG;    // 上升沿
END_VAR

Trigger_R(CLK := iTrigger);
IF Trigger_R.Q THEN
    ioCount.Total := ioCount.Total + 1;
END_IF;

IF ioCount.Clear THEN
    ioCount.Total := 0;
    ioCount.Clear := FALSE;
END_IF;
```

### 1.3 行为约定

- **PLC 重启后 Total 是否保留？** 推荐 `RETAIN PERSISTENT`，避免每次断电从 0 开始扰乱差值法。如果做不到，HMI 会按"newValue < oldValue ⇒ 重启"自动恢复，不会算出负数。
- **Clear 的语义**：仅维修人员调试或新班次手动清零时使用。日常生产**不应该**用 Clear——因为 HMI 已经按时间窗算"今天 / 本班"了，不需要 PLC 端清零。
- **Total 溢出**：UDINT 最大 ~42 亿。即使 1Hz 计数也要 130 年才溢出，按生产实际可忽略。真要溢出 HMI 也能识别（newValue < oldValue 当作 reset 处理）。

---

## Part 2：HMI 计数模块设计

### 2.1 整体架构

```
┌───────────────────────────────────────────────────────────────┐
│ OPC UA Server (PLC)                                           │
│   OK.Total : UDINT     NG.Total : UDINT                       │
└──────┬───────────────────────┬────────────────────────────────┘
       │ MonitoredItem 订阅     │
       ▼                       ▼
┌───────────────────────────────────────────────────────────────┐
│ ProductionCountService（新建，单例）                          │
│  · 监听 OK.Total / NG.Total 值变化                            │
│  · 差值法 → ProductionEvent {ts, source, delta, counter}      │
│  · 写 SQLite (config/production.db)                           │
│  · 提供查询 API（按小时 / 按班次 / 按日 / 任意区间）          │
│  · 提供事件 OnEventInserted （UI 刷新触发）                   │
└──────┬────────────────────────────────────────────────────────┘
       │
       ▼
┌───────────────────────────────────────────────────────────────┐
│ CountViewModel + CountView（新建固定页）                      │
│  · SelectedSource: Total | OK | NG（"Total" 由 OK+NG 合成）   │
│  · Hours[24] 行：调 service.GetHourlyToday(src)               │
│  · DayTotal / NightTotal：调 service.GetShiftTotals(src)      │
│  · History[31]：调 service.GetDailyHistory(src, 31)           │
│  · OnEventInserted → 节流刷新（DispatcherTimer 1s）           │
└───────────────────────────────────────────────────────────────┘
```

### 2.2 SQLite schema

`config/production.db`，使用 `Microsoft.Data.Sqlite` NuGet。

```sql
CREATE TABLE production_event (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    ts_utc        TEXT    NOT NULL,         -- ISO 8601, e.g. '2026-05-07T10:30:15.123Z'
    source        TEXT    NOT NULL,         -- 'OK' | 'NG'
    delta         INTEGER NOT NULL,         -- 本次新增数量
    counter_value INTEGER NOT NULL,         -- PLC 当时的累计值
    note          TEXT    NULL              -- 'reset' / 'gap_recovery' 等标记
);
CREATE INDEX idx_event_ts_source ON production_event(source, ts_utc);
CREATE INDEX idx_event_ts ON production_event(ts_utc);

-- 启用 WAL 模式，写入抖动小、读不阻塞写
PRAGMA journal_mode=WAL;
```

预估容量：1Hz × 2 source × 365 天 ≈ 6300 万行，单文件 ~2 GB。SQLite 单表上亿行性能仍可接受（带索引）。如长期使用可加月度归档（`production_event_archive_YYYYMM` 表）但前 1-2 年不必。

### 2.3 差值法核心代码草图

```csharp
public sealed class ProductionCountService
{
    // 每个 source 的上次读到的 counter
    private readonly Dictionary<string, long?> _lastCounter = new()
    {
        { "OK", null }, { "NG", null }
    };

    public void OnTagValueChanged(string source, string rawValue)
    {
        if (!long.TryParse(rawValue, out var current)) return;

        var prev = _lastCounter[source];
        _lastCounter[source] = current;

        if (prev is null) return;   // 第一次读取，仅记基线，不写事件

        long delta;
        string? note = null;
        if (current >= prev.Value)
        {
            delta = current - prev.Value;
            if (delta == 0) return;          // 没变化不记
        }
        else
        {
            // PLC 重启或溢出：把 current 当新基线，本次不算 delta
            delta = current;                 // 重启后从 0 开始累计的部分
            note = "reset";
        }

        InsertEvent(DateTime.UtcNow, source, delta, current, note);
        EventInserted?.Invoke(source);
    }

    public event Action<string>? EventInserted;
}
```

### 2.4 查询接口

```csharp
public interface IProductionQueryApi
{
    // 今天每小时（按本地时间归桶），按班次开始时间排序的 24 个桶
    IReadOnlyList<HourBucket> GetHourlyToday(string source);

    // 今天白班/夜班合计
    (int day, int night) GetShiftTotals(string source);

    // 最近 N 天每天总和（含今天）
    IReadOnlyList<(DateOnly date, int count)> GetDailyHistory(string source, int days);

    // 任意区间总和
    int GetTotalInRange(string source, DateTime fromUtc, DateTime toUtc);
}

public sealed record HourBucket(int Index, string TimeRangeText, int Count);
```

`GetHourlyToday` 在 SQL 里做小时归桶，但**桶的边界由班次配置决定**（不是固定 0-23）。简化逻辑：

```csharp
// shiftStart = config.DayStart 例如 08:30
var todayShiftStart = combine(today, shiftStart);
var sql = @"
    SELECT
        CAST((julianday(ts_utc) - julianday(@start)) * 24 AS INTEGER) AS hour_idx,
        SUM(delta) AS cnt
    FROM production_event
    WHERE source = @src AND ts_utc >= @start AND ts_utc < @end
    GROUP BY hour_idx";
```

### 2.5 班次配置

`config/shift.json`：

```json
{
    "DayStart":   "08:30",
    "NightStart": "20:30"
}
```

`AppOptions` 增加 `ShiftOptions { DayStart, NightStart }`，由 `IOptions<ShiftOptions>` 注入到 `ProductionCountService`。改班次只改这个文件，不动 PLC、不动数据库 schema。

### 2.6 启动 / 关闭顺序

- **启动**：`Bootstrapper` 注册 `ProductionCountService` 为 singleton；连接 OPC UA 后调 `service.AttachAsync(opcUaService)`，订阅两个 Total tag。
- **关闭**：app shutdown 钩子里 `service.FlushAsync()`，确保 SQLite WAL 刷盘。

### 2.7 历史数据备份（可选，对冲单机风险）

`config/production.db` 默认在程序根目录。两种增强（按需选）：
- **定时复制到共享盘**：每天凌晨复制一份到 `\\fileserver\hmi-backup\<machine>\production-YYYYMMDD.db`
- **写双库**：本地一份 + 远端一份，远端不可达时降级为本地+待发送队列

第一阶段不做，等用起来稳定再说。

---

## 落地步骤

第一阶段（PLC 同事确认接口后才能开工，先不动 HMI）：

1. **PLC 同事**：按 1.1 / 1.2 改 `Str_Count` / `FB_Count`，重新发布 `Device.Application.xml`。
2. **HMI 侧（我做）**：
   - a. 引入 `Microsoft.Data.Sqlite` NuGet
   - b. 新增 `Services/Production/ProductionCountService.cs` + schema 初始化
   - c. 新增 `ViewModels/Modules/CountViewModel.cs` + `Views/Pages/CountView.xaml`
   - d. 在导航和页面模板里挂上 `CountView`
   - e. 在 `Bootstrapper` 注册新服务
3. **联调**：先单 source（OK）跑通"PLC 计数 → SQLite → 分时显示"链路；再加 NG 和 Total 合成；最后接历史 31 天展示。
4. **回收**：跑稳后，配置文件里和 `_resolvedNodeIds` 缓存里那些 `OK.Today.* / NG.Today.* / Befor[*] / DateBefor[*]` 的旧 tag 顺手删掉。

第二阶段（按需扩展）：
- 按配方/工单/班组分桶（`production_event` 加列）
- OEE 报表
- 趋势图导出 PDF
- 班次/日报自动推送

---

## 关键决定点（已确认，2026-05-07）

| # | 问题 | 决定 |
|---|---|---|
| 1 | PLC 端 `Total` 字段是否 `RETAIN PERSISTENT`？ | **是**——断电保持，HMI 差值法在最常见场景下不会触发 reset 路径 |
| 2 | SQLite 文件存放位置？ | **本地**——`config/production.db`，第一版不做远端备份 |
| 3 | "总计数"按钮语义？ | **`OK + NG` 合成**——PLC 不再单独维护 Total 实例 |
| 4 | 是否保留 `Clear` 字段？ | **保留**——给维修复位兜底，日常生产不用 |

---

## 落地依赖（待办）

- [ ] 跟 PLC 同事拉齐 Part 1 接口合约，确认实施时机
- [ ] HMI 侧排期，等其它页面审计完一起做（跟 MonitorView / RecipeView / ManualView / ParameterView 等改动合并到一次大改）

文档定稿。
