# 固定页 ↔ PLC Tag 地址对照表

> 用途：把 8 个固定页里代码层面引用到的 OPC UA Tag 名，跟 `sample-tags.csv` 的 NodeId 一一对应。
> 现场部署时把 `Channel1.Device1.*` 替换成实际 PLC 节点 ID 即可。

**默认 NodeId 命名规范**：`ns=2;s=Channel1.Device1.<TagName>`
**默认 Schema**：`sample-tags.csv` 已全量包含下表所有 Tag，import IO 表后可直接 Connect 看到值。

---

## 1. 主界面 HomeView（H1-H7）

| Tag 名 | 类型 | 方向 | 用途 | 来源 |
|---|---|---|---|---|
| `Device_Start` | Bool | Out | 设备启动状态 → DeviceStatusText（H1）| MainViewModel.cs |
| `Mode_Auto` / `Mode_Manual` / `Mode_Debug` / `Mode_DryRun` / `Mode_BypassStation` | Bool | Out | 模式 chip 显示 + ModeText | MainViewModel.cs |
| `Production_Count` / `Production_GoodCount` / `Production_NgCount` / `Production_TargetCount` | Int | Out | 6 KPI 卡：总产/良/不良/目标 | MainViewModel.cs |
| `Production_Availability` / `Production_Performance` / `Production_Quality` | Double | Out | OEE 三项分量 | MainViewModel.cs |
| `Shift_ProductionCount` / `Shift_GoodCount` / `Shift_NgCount` | Int | Out | H1 班次进度环形 / H3 mini sparkline | MainViewModel.cs |
| `Daily_ProductionCount` / `Daily_GoodCount` / `Daily_NgCount` | Int | Out | 日累计 | MainViewModel.cs |
| `Cycle_Time` / `Ideal_Cycle_Time` | Double | Out | G3 节拍 chip | MainViewModel.cs |
| `Machine_RunTimeMin` / `Machine_StopTimeMin` | Int | Out | OEE Availability 计算输入 | MainViewModel.cs |
| `Throughput_Hourly` | Int | Out | G3 UPH chip | MainViewModel.cs |
| `WorkOrder_No` / `Recipe_Name` | String | Out | 顶部信息 chip | MainViewModel.cs |
| `Axis1_Alarm` / `Motor1_Fault` / `Alarm_EStop` | Bool | In | 设备状态聚合 + AxisStatusText | MainViewModel.cs |

---

## 2. 监控 MonitorView（M1-M27 / 4 子页）

### 2A 详细生产数据
| Tag | 类型 | 用途 |
|---|---|---|
| `Production_*` / `Shift_*` / `Daily_*` | 同 Home | 4 KPI + 4 趋势小图 |
| `Cycle_Time` / `Throughput_Hourly` | Double / Int | 节拍 / UPH 趋势 |

### 2B IO 监控
- DataGrid 直接绑 `Tags`（运行时 OPC UA Browse 出来的全集）
- 默认列展示 `X_Start` / `Y_RunLamp` 等

### 2C 通讯调试（OPC UA）
- OpcUaBrowserNodes 由 OPC UA 服务器 Browse 给出，**不在 sample-tags.csv 里固定**
- M16 收藏的节点写到 `config/opc-favorites.json`
- M18 写入测试通过 `OpcUaService.WriteTagAsync(TagItem, value)`

### 2D 程序监控
- 流程步号通过 `FlowSteps` 集合维护（HMI 端 / 不直接读 PLC tag），关联报警来自 `AlarmHistory`

---

## 3. 手动操作 ManualView（MA1-MA10）

| Tag | 类型 | 用途 |
|---|---|---|
| `Device_Start` | Bool | 手动模式可用性判断 |
| `Alarm_EStop` | Bool | 急停联锁 |
| **气缸**（每个气缸 block 自带 root tag）| | |
| `<root>.Status.InHome` / `<root>.Status.InWork` | Bool | 位置传感器（取代 Cylinder_FwdLS/BwdLS 通用占位）|
| `<root>.Status.Error` | Bool | **气缸报警**（MA2 卡片红框联动）|
| `<root>.Status.ErrorID` | UInt16 | **气缸报警代码**（详情文案查表）|
| `<root>.Cmd.ManuToHome` / `.Cmd.ManuToWork` | Bool | 手动指令 |
| `<root>.DevStatus.Valve_Home` / `.Valve_Work` | Bool | 阀门状态指示 |
| `<root>.Parm.*` | 各类 | 参数（运动时间 / 超时等）|
| `<root>.Output.*` | Bool | 输出反馈 |
| **轴 / 机械手 / 挡停** | | |
| `Axis1_Enable` / `Axis1_Pos` / `Axis1_Alarm` / `Axis1_AlarmReset` | Bool/Double | 轴控制 |
| `Robot_Run` / `Robot_Pause` | Bool | 机械手指令 |
| `Stopper_Up` | Bool | 挡停升起 |
| `Motor1_Fault` / `Motor1_Reset` | Bool | 电机故障/复位 |

气缸 `<root>` 由 IO 表导入时自动生成（如 `G_OP30_Cyl01`），写入 `config/manual-cylinders.json`。

---

## 4. 参数设定 ParameterView（P1-P10）

| 来源 | 说明 |
|---|---|
| `config/parameters.json` | 参数元数据持久化 |
| `ParameterItem.Value` ↔ PLC tag | 加载/保存通过 `IParameterService`（不直接 Get/Set 单 Tag）|
| **联锁规则**（自动允许手动气缸 / 挡停 / 机械手 / 轴）| 通过 `GetBoolParameter("名称")` 取，不映射到 PLC tag |

---

## 5. 报警 AlarmView（A1-A10）

| Tag | 类型 | 用途 |
|---|---|---|
| `Alarm_EStop` / `Alarm_AirLow` / `Alarm_VacuumTimeout` / `Alarm_ServoOverload` | Bool / IsAlarm=true | 触发 `RaiseOrUpdateAlarm` |
| `Axis1_Alarm` / `Motor1_Fault` | Bool / IsAlarm=true | 同上 |
| `<cylinder>.Status.Error` + `.ErrorID` | Bool + UInt16 | 气缸报警与代码 |

报警历史持久化到 `config/alarm-history.json`，**不实时存到 PLC**。

---

## 6. 配方 RecipeView（R1-R9）

| Tag | 类型 | 用途 |
|---|---|---|
| `Recipe_Name` | String | `ApplyRecipe` 时 SetTagValue 通知 PLC 当前配方名 |
| `Device_Start` | Bool | R4 切换向导先 SetTagValue=False 停机 |

参数本体不直接写 PLC tag，是写 `ParameterItem.Value` 然后由参数页统一保存。

---

## 7. 程序生成 DesignerView（D1-D9）

- 不直接读写 PLC tag，只处理 IO 表导入 / ST 文件生成
- D1 校验 / D2 diff / D3 多工位 / D6 git 代理 / D8 IO 表内联编辑 / D9 ST 预览

---

## 8. 履历 AuditView（AU1-AU8）

- 完全本地数据（`OperationAudits` 集合）+ `config/audit-archive/*.zip`
- 不读 PLC tag

---

## 9. 登录 LoginView（L4/L5/L6）

- `config/users.json`（IUserService）+ `config/permissions.json`
- 不读 PLC tag

---

## 10. 生产计数 CountView（Phase 1）

| Tag | 类型 | 用途 |
|---|---|---|
| `Production_GoodCount` / `Production_NgCount` | Int | 差值法计数源（HMI 端 SQLite 账本） |
| `Production_Count` / `Production_TargetCount` | Int | 总产量与目标 |

后续 PLC 简化合约后会读：`DB8003_Count.OK.Total` / `DB8003_Count.NG.Total` 替代上述（等联调）。

---

## 11. 缺失检查（已在 csv 中补齐）

之前 `sample-tags.csv` 只 4 条，下面 40 个 Tag 是代码引用但 csv 没有的，本次已全部补到 csv：

```
Device_Start, Mode_Auto/Manual/Debug/DryRun/BypassStation,
Axis1_Enable/Pos/Alarm/AlarmReset,
Cylinder_Extend/FwdLS/BwdLS,
Motor1_Fault/Reset,
Robot_Run/Pause, Stopper_Up,
Alarm_AirLow/VacuumTimeout/ServoOverload,
Production_Count/Good/Ng/Target/Availability/Performance/Quality,
Shift_ProductionCount/Good/Ng,
Daily_ProductionCount/Good/Ng,
WorkOrder_No, Recipe_Name,
Cycle_Time, Ideal_Cycle_Time,
Machine_RunTimeMin, Machine_StopTimeMin,
Throughput_Hourly
```

现场部署只要把 `Channel1.Device1.` 改成实际 OPC UA 服务器路径，所有页面就能"一键对得上"。

---

## 12. 启动验证流程

1. PLC OPC UA 服务器先把上述 Tag 全部建好（数据类型 + 读写权限对照表 9-10 列）
2. 启动 HMI → 加载 `sample-tags.csv`（或现场实际 csv）
3. 点"连接" → Tag 列表全部有效，主界面 KPI 全部活
4. 触发一次 `Alarm_EStop=true` → 报警页 + 顶部红条 + 事件中心铃铛红 1
5. 点击各个固定页 → 不再有"--"占位，所有 KPI / 状态都有数值
