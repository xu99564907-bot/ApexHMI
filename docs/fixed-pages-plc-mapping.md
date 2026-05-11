# 固定页 ↔ PLC OPC UA NodeId 对照表

> 数据源：`PLC/test13/test13.Device.Application.xml` (CODESYS InoProShop Symbol Configuration)
> Project: `test13` · Device: `Device` · App: `Application`

## 一、产线工位 ↔ DB 编号映射

InoProShop 这套 PLC 工程把每个工位拆成 6 个 DB，编号规则 `DB<工位号>xx_用途`：

| 工位 | Control | Recipe | **Count** | Comm | DriveControl | Fault |
|---|---|---|---|---|---|---|
| **OP10**（主控/上料）| DB1000 | DB1002 | DB1003 | DB1005 | DB1050 | DB1070 |
| OP11 | DB1100 | DB1102 | DB1103 | DB1105 | DB1150 | DB1170 |
| OP20 | DB2000 | DB2002 | DB2003 | DB2005 | DB2050 | DB2070 |
| **OP30** | DB3000 | DB3002 | DB3003 | DB3005 | DB3050 | DB3070 |
| **OP40** | DB4000 | DB4002 | DB4003 | DB4005 | DB4050 | DB4070 |
| OP60 | DB6000 | – | – | – | – | – |
| OP70 | DB7000 | DB7002 | DB7003 | DB7005 | DB7050 | DB7070 |
| **OP80**（终端/出料/总计数）| DB8000 | DB8002 | **DB8003** | DB8005 | DB8050 | DB8070 |

> redesign-proposals.md 提的 **DB8003_Count.OK.Total / NG.Total** 就是 **OP80 终端工位**的产线总良品 / 总不良。

## 二、关键结构（来自 XML TypeUserDef）

### DB`xx`00_Control
```
.HMI    : Str_HMI    { Jump[2], Num, Estop, Cyl, Vac, Axis, Stop, Run, SpaceButton[100] }
.Machine: Str_Machine { OP_Emg_Stop, OP_StartButton, OP_StopButton, OP_ResetButton,
                        OP_AutoManual, AirPresure, RedLightHouse, YeLightHouse,
                        GrLightHouse, Buzzer, StartLamp, StopLamp, ResetLamp,
                        Speed, CycleTime, PowerOnTime, RunTime, EfficencyResult,
                        EfficencyClear, ... }
.Mode   : Str_Mode    { Cmd, Status, Out }
  Cmd:    AutoManualButton, RunButton, StopButton, ResetButton, CycleEndButton,
          StepButton, HomingButton, LampTest, Debug_BUZButton, SafetyDoorShieild
  Status: ManualMode, AutoMode, RunMode, StopMode, CycleMode, StepMode, DryMode,
          ClearMode, OfflineMode, HandMode, StationMode, AutoRunning, StepRunning,
          HomeOK, EStop, Fault, Warning
  Out:    HomeStart, ErrorReset, Stop, AutoRun_R, AutoRun_F, HomeOK_R, HomeOK_F
```

### DB`xx`02_Recipe
```
.DownLoad, .ParameterChangePmt, .RecipeChangePmt,
.RecipeName, .RecipeNameTemp, .RecipeNum, .RecipeNumTemp, .RecipeOk,
.TypeData[0..20], .TypeDataCur
```

### DB`xx`03_Count
```
.Clear  : BOOL  (清除计数)
.OK     : Str_Count { Execute, Clear, Num, Total: UDINT, Today, Befor[30], DateBefor[30] }
.NG     : Str_Count
.Total  : Str_Count
.Yield  : REAL (合格率%)
```

### DB`xx`50_DriveControl（统一组件库）
```
.AxisCtrl[0..16]  : Str_Axis     (Cmd / Parm / Status / DevStatus)
.CylCtrl[0..63]   : Str_Cylinder (Cmd: ManuToHome/ManuToWork  Status: InHome/InWork/Error/ErrorID)
.VacCtrl[0..32]   : Str_Cylinder (复用气缸结构)
.Sensor[0..32]    : Str_Sensor
.Motor[1..10]     : Str_Motor    (Cmd / DevStatus)
.Stopper[1..20]   : Str_Stopper
.Kuka[1..2]       : Str_Kuka6    (机械手 EXT_START / MOVE_ENABLE / PGNO_VALID...)
.Epson[1..10]     : Str_Epson
.HeatCtrl[1..1]   : Str_Heating
.PalletCtrl[0..3] : Str_Palletizing
```

### DB`xx`70_Fault
```
.EstopTotal, .RunTotal, .StopTotal
.Estop      : Str_OP{xx}_FaultEstop  { MAP, EstopButton, SafeDoor, LightCurtain, Space[3..63] }
.Run        : Str_OP{xx}_FaultRun
.Stop       : Str_OP{xx}_Faultstop   { MAP, Cyl[128], Vac[64], Axis[64], Sensor[128],
                                        InitialTimeOut, SafeDoorOpen, BeforStation_*, ... }
```

## 三、HMI Tag ↔ NodeId 映射

NodeId 格式：`ns=4;s=|var|Application.<DB路径>.<字段>`

### 1) 主界面 / 监控 / 报警 — 主控来自 OP10

| HMI Tag | InoProShop NodeId | 类型 | 说明 |
|---|---|---|---|
| `X_Start` | `\|var\|Application.DB1000_Control.Machine.OP_StartButton` | BOOL | 启动按钮 |
| `Y_RunLamp` | `\|var\|Application.DB1000_Control.Machine.StartLamp` | BOOL | 运行指示灯 |
| `Device_Start` | `\|var\|Application.DB1000_Control.Mode.Out.AutoRun_R` | BOOL | 自动运行触发 |
| `Mode_Auto` | `\|var\|Application.DB1000_Control.Mode.Status.AutoMode` | BOOL | 自动模式 |
| `Mode_Manual` | `\|var\|Application.DB1000_Control.Mode.Status.ManualMode` | BOOL | 手动 |
| `Mode_Debug` | `\|var\|Application.DB1000_Control.Mode.Status.StepMode` | BOOL | 单步 |
| `Mode_DryRun` | `\|var\|Application.DB1000_Control.Mode.Status.DryMode` | BOOL | 空运行 |
| `Mode_BypassStation` | `\|var\|Application.DB1000_Control.Mode.Status.StationMode` | BOOL | 跳过工位 |
| `Cycle_Time` | `\|var\|Application.DB1000_Control.Machine.CycleTime.Average` | REAL | 平均节拍秒 |
| `Ideal_Cycle_Time` | `\|var\|Application.DB1000_Control.Machine.CycleTime.Plan` | REAL | 理论节拍秒 |
| `Machine_RunTimeMin` | `\|var\|Application.DB1000_Control.Machine.RunTime` | UDINT | 累计运行分钟 |
| `Machine_StopTimeMin` | `\|var\|Application.DB1000_Control.Machine.PowerOnTime` | UDINT | 累计上电分钟 |
| `Production_Availability` | `\|var\|Application.DB1000_Control.Machine.EfficencyResult` | REAL | 设备利用率% |
| `Production_Performance` | `\|var\|Application.DB1000_Control.Machine.Speed` | REAL | 当前生产速度 |
| `Recipe_Name` | `\|var\|Application.DB1002_Recipe.RecipeName` | STRING(20) | 已加载配方名 |
| `WorkOrder_No` | `\|var\|Application.DB1002_Recipe.RecipeNameTemp` | STRING(20) | HMI 头部显示 |

### 2) 报警 — 来自 OP10 Fault + Mode

| HMI Tag | InoProShop NodeId | 说明 |
|---|---|---|
| `Alarm_EStop` | `\|var\|Application.DB1070_Fault.Estop.EstopButton` | 急停按钮触发 |
| `Alarm_AirLow` | `\|var\|Application.DB1000_Control.Machine.AirPresure` | 气压低（false=低）|
| `Alarm_VacuumTimeout` | `\|var\|Application.DB1050_DriveControl.VacCtrl[1].Status.Error` | 真空建立超时 |
| `Alarm_ServoOverload` | `\|var\|Application.DB1050_DriveControl.AxisCtrl[1].Status.Error` | 伺服过载 |

### 3) 计数 / OEE — 来自 OP80 终端

| HMI Tag | InoProShop NodeId | 说明 |
|---|---|---|
| `Production_Count` / `Daily_ProductionCount` | `\|var\|Application.DB8003_Count.Total.Total` | 产线总产量 |
| `Production_GoodCount` / `Daily_GoodCount` | `\|var\|Application.DB8003_Count.OK.Total` | 产线良品 |
| `Production_NgCount` / `Daily_NgCount` | `\|var\|Application.DB8003_Count.NG.Total` | 产线不良 |
| `Production_TargetCount` | `\|var\|Application.DB8003_Count.Total.Num` | 目标 |
| `Production_Quality` | `\|var\|Application.DB8003_Count.Yield` | 合格率% |
| `Shift_ProductionCount` | `\|var\|Application.DB8003_Count.Total.Today.Total` | 本班次总 |
| `Shift_GoodCount` | `\|var\|Application.DB8003_Count.OK.Today.Total` | 本班次良 |
| `Shift_NgCount` | `\|var\|Application.DB8003_Count.NG.Today.Total` | 本班次不良 |
| `Throughput_Hourly` | `\|var\|Application.DB8003_Count.OK.Befor[0]` | 小时 UPH（环形 0 元素）|

### 4) 设备组件 — 来自 OP10 DriveControl

| HMI Tag | InoProShop NodeId | 说明 |
|---|---|---|
| `Axis1_Pos` | `\|var\|Application.DB1050_DriveControl.AxisCtrl[1].Status.ActPosition` | 轴1位置 |
| `Axis1_Enable` | `\|var\|Application.DB1050_DriveControl.AxisCtrl[1].DevStatus.PowerOn` | 使能 |
| `Axis1_Alarm` | `\|var\|Application.DB1050_DriveControl.AxisCtrl[1].Status.Error` | 报警 |
| `Axis1_AlarmReset` | `\|var\|Application.DB1050_DriveControl.AxisCtrl[1].Cmd.ErrorReset` | 复位 |
| `Cylinder_Extend` | `\|var\|Application.DB1050_DriveControl.CylCtrl[1].Cmd.ManuToWork` | 伸出指令 |
| `Cylinder_FwdLS` | `\|var\|Application.DB1050_DriveControl.CylCtrl[1].Status.InWork` | 伸到位 |
| `Cylinder_BwdLS` | `\|var\|Application.DB1050_DriveControl.CylCtrl[1].Status.InHome` | 退到位 |
| 气缸报警（动态） | `\|var\|Application.DB{xx}50_DriveControl.CylCtrl[i].Status.Error` | **每个气缸都有** |
| 气缸代码（动态） | `\|var\|Application.DB{xx}50_DriveControl.CylCtrl[i].Status.ErrorID` | **每个气缸都有** |
| `Motor1_Fault` | `\|var\|Application.DB1050_DriveControl.Motor[1].DevStatus.Fault` | 电机故障 |
| `Motor1_Reset` | `\|var\|Application.DB1050_DriveControl.Motor[1].Cmd.Reset` | 电机复位 |
| `Robot_Run` | `\|var\|Application.DB1050_DriveControl.Kuka[1].Cmd.EXT_START` | KUKA EXT_START |
| `Robot_Pause` | `\|var\|Application.DB1050_DriveControl.Kuka[1].Cmd.MOVE_ENABLE` | KUKA MOVE_ENABLE |
| `Stopper_Up` | `\|var\|Application.DB1050_DriveControl.Stopper[1].Cmd.ManuToWork` | 挡停升起 |

## 四、多工位扩展规则

代码里所有 `Get*Tag("XXX")` 调用看到的 Tag 名当前是单一 PLC 视角。多工位生产时把 OP10 的 NodeId 模板按下表替换：

```
OP10 → DB10{50,70} → 主控 / 上料工位
OP30 → DB30{50,70} → 工位 30
OP40 → DB40{50,70} → 工位 40
OP80 → DB80{03,50,70} → 终端 / 总计数（已用作 Production_*）
```

如需 HMI 端按工位分页显示，建议：
1. 在代码层加 `OP30_` / `OP40_` 前缀 (`OP30_Axis1_Pos`)
2. csv 里每个工位单独一行
3. ManualCylinderBlockItem.GroupName（Phase 4 已加）填 "OP30" / "OP40" / "OP80"

## 五、气缸 / 轴 root 节点（动态生成）

代码里 `ManualCylinderBlockItem` 的 root tag 命名规则：

```
气缸：Application.DB{xx}50_DriveControl.CylCtrl[{index}]
轴  ：Application.DB{xx}50_DriveControl.AxisCtrl[{index}]
机械手：Application.DB{xx}50_DriveControl.Kuka[{index}]
挡停：Application.DB{xx}50_DriveControl.Stopper[{index}]
真空：Application.DB{xx}50_DriveControl.VacCtrl[{index}]
```

之前 ManualView 用的 `<root>.Status.Error` / `.Status.ErrorID` 在这里就是：
- `Application.DB1050_DriveControl.CylCtrl[3].Status.Error`
- `Application.DB1050_DriveControl.CylCtrl[3].Status.ErrorID`

## 六、CycleTime 子结构

`T_Str_CycleTime` 没在 XML 顶层 TypeList 直接列出，但 `Str_Machine.CycleTime` 占 12 字节，典型字段：

```
.Plan    : REAL   (理论节拍秒)
.Average : REAL   (平均节拍秒)
.Last    : REAL   (上一件节拍)
```

如果 PLC 端字段名不同，调整 csv 中 `Cycle_Time` / `Ideal_Cycle_Time` 的 NodeId 末段即可。

## 七、占位符 → 动态工位号

⚠ **DB 编号不是固定的**！每次"手动程序生成"页填的工位号变化时，对应所有 DB 也跟着变。
公式（来自 `MainViewModel.Designer.cs`）：

```
controlDb = opNo * 100 + controlDbOffset       (默认 controlDbOffset=0)
recipeDb  = controlDb + 2
countDb   = controlDb + 3
commDb    = controlDb + 5
driveDb   = controlDb + driveDbOffset           (默认 driveDbOffset=50)
faultDb   = controlDb + 70
```

**所以 csv 里 NodeId 用占位符模板，加载时按 IoOperationNumber 实时替换**：

| 模板占位符 | 含义 | OP10 时 | OP30 时 | OP80 时 |
|---|---|---|---|---|
| `{OP}` | 工位数字 | 10 | 30 | 80 |
| `{OP00}` | Control DB | 1000 | 3000 | 8000 |
| `{OP02}` | Recipe DB | 1002 | 3002 | 8002 |
| `{OP03}` | Count DB | 1003 | 3003 | 8003 |
| `{OP05}` | Comm DB | 1005 | 3005 | 8005 |
| `{OP50}` | DriveControl DB | 1050 | 3050 | 8050 |
| `{OP70}` | Fault DB | 1070 | 3070 | 8070 |
| `{OP_TERM}` | 终端工位（固定 80）| 80 | 80 | 80 |
| `{TERM00}` ~ `{TERM70}` | 终端各 DB | 8000~8070 | 同左 | 同左 |

**模板示例**：

```
ns=4;s=|var|Application.DB{OP00}_Control.Mode.Status.AutoMode
   ↓ IoOperationNumber="OP30"
ns=4;s=|var|Application.DB3000_Control.Mode.Status.AutoMode

ns=4;s=|var|Application.DB{OP50}_DriveControl.CylCtrl[1].Status.Error
   ↓ IoOperationNumber="OP40"
ns=4;s=|var|Application.DB4050_DriveControl.CylCtrl[1].Status.Error

ns=4;s=|var|Application.DB{TERM03}_Count.OK.Total       (产线总良品)
   ↓ 任何时候
ns=4;s=|var|Application.DB8003_Count.OK.Total
```

**实现位置**：
- 占位符解析：[Services/TagNodeIdResolver.cs](../Services/TagNodeIdResolver.cs)
- 加载 csv 时调用：[MainViewModel.Monitor.cs](../ViewModels/MainViewModel.Monitor.cs) `ImportTagsAsync`
- 工位号变化自动重 resolve：[MainViewModel.cs](../ViewModels/MainViewModel.cs) `OnIoOperationNumberChanged`

模板文件：[sample-tags.template.csv](../sample-tags.template.csv)（39 条 Tag 全部用占位符）

## 八、连接验证

1. 启动 InoProShop OPC UA Server（默认端口 4840）
2. HMI → 监控-通讯调试 → Browse `Application.DB1000_Control`
3. 应看到 `HMI / Machine / Mode` 三个子节点
4. 加载 `sample-tags.csv`（待解锁更新；现可手工在 OPC UA 浏览器复制 NodeId）
5. 主界面 KPI 全部活，急停按钮按下 `Alarm_EStop` 会变 true → 报警页有记录
