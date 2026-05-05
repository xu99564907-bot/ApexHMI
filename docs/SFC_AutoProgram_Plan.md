# 自动程序生成器改造计划（SFC 流程图模式）

## 背景

当前自动程序生成只产生空骨架（无设备指令、无完成条件）。
目标：用户在流程图式编辑器中选设备+动作，软件自动生成完整可用的 PLC CASE 程序。

---

## 数据基础（已有）

| 数据源 | 内容 | 用途 |
|--------|------|------|
| `ManualCylinderBlocks` | 气缸列表（Index、DisplayName、TagName） | SFC 气缸下拉选项 |
| `ManualAxisBlocks` | 轴列表（Index、DisplayName、PointOptions） | SFC 轴下拉选项 |
| `IoOperationNumber` | OP10、OP80... | 推算 DriveDb 名称 |
| `_controlDbMultiplier/Offset/driveDbOffset` | DB 号计算参数 | DriveDb = DB(OP*N+offset+driveOffset) |

---

## Phase 1：步骤编辑器（表格 + 代码生成） ← 当前实现目标

**新增文件：**
- `Models/Sfc/SfcStep.cs` — 步骤数据模型
- `Models/Sfc/SfcDeviceOption.cs` — 设备下拉选项
- `Services/SfcCodeGeneratorService.cs` — 代码生成逻辑

**修改文件：**
- `ViewModels/MainViewModel.cs` — 新增 SfcSteps、SelectedSfcStep 等属性
- `ViewModels/MainViewModel.Designer.cs` — 新增 SFC 命令和生成逻辑
- `Views/Pages/DesignerView.xaml` — 替换自动程序生成 UI

### 步骤数据模型（SfcStep）

```
StepNo           // 步序号：10, 20, 30...
DeviceType       // 设备类型：Cylinder / Axis / Motor / Vacuum / Wait / Custom
DeviceIndex      // 设备序号（CylCtrl[N] 的 N）
DeviceName       // 显示名（自动从 ManualCylinderBlocks 填入）
ActionType       // 动作：Extend / Retract / MoveToPoint / Home / Start / Stop / VacOn / VacOff / Timer / Condition / Custom
PointIndex       // 位置点（仅 Axis+MoveToPoint）
CompletionCond   // 完成条件（自动填，可手改）
NextStep         // 下一步步序号或 END
CustomCommand    // 自定义指令（DeviceType=Custom 时）
CustomCondition  // 自定义条件（DeviceType=Custom 时）
```

### 动作 → 生成代码映射

| 设备 | 动作 | 指令 | 完成条件 |
|------|------|------|---------|
| Cylinder | Extend | `CylCtrl[N].Cmd.Extend := TRUE` | `CylCtrl[N].Status.InWork` |
| Cylinder | Retract | `CylCtrl[N].Cmd.Extend := FALSE` | `CylCtrl[N].Status.InHome` |
| Axis | MoveToPoint | `AxisCtrl[N].Ctrl.PointSelect := P;`<br>`AxisCtrl[N].Cmd.MoveAbs := TRUE` | `AxisCtrl[N].Status.InPos` |
| Axis | Home | `AxisCtrl[N].Cmd.Home := TRUE` | `AxisCtrl[N].Status.HomeOK` |
| Motor | Start | `MotorCtrl[N].Cmd.Start := TRUE` | `MotorCtrl[N].Status.Running` |
| Motor | Stop | `MotorCtrl[N].Cmd.Start := FALSE` | `NOT MotorCtrl[N].Status.Running` |
| Vacuum | VacOn | `VacCtrl[N].Cmd.VacOn := TRUE` | `VacCtrl[N].Status.VacOK` |
| Vacuum | VacOff | `VacCtrl[N].Cmd.VacOn := FALSE` | `NOT VacCtrl[N].Status.VacOK` |
| Wait | Timer | `T_SN(IN:=TRUE, PT:=T#NMS)` | `T_SN.Q` |
| Wait | Condition | （无指令） | 用户填写 |
| Custom | Custom | 用户填写 | 用户填写 |

### 生成代码格式

```pascal
CASE Auto[StationNo].Step OF
    10:
        Auto[1].Comment := "上料气缸 伸出";
        DB1060_DriveControl.CylCtrl[1].Cmd.Extend := TRUE;
        IF DB1060_DriveControl.CylCtrl[1].Status.InWork THEN
            Auto[1].Step := 20;
        END_IF
    20:
        Auto[1].Comment := "轴1 移动到点位2";
        DB1060_DriveControl.AxisCtrl[1].Ctrl.PointSelect := 2;
        DB1060_DriveControl.AxisCtrl[1].Cmd.MoveAbs := TRUE;
        IF DB1060_DriveControl.AxisCtrl[1].Status.InPos THEN
            Auto[1].Step := 30;
        END_IF
    ...
    1000:
        Auto[1].Comment := "自动运行结束";
        Auto[1].Running := FALSE;
        Auto[1].Step := 0;
END_CASE
```

---

## Phase 2：流程图可视化（只读）

- Canvas + ItemsControl 渲染矩形节点（步骤）
- 叠加 Path 连线（箭头，从 NextStep 关系自动计算坐标）
- 点击节点同步左侧步骤列表选中项
- 节点位置自动纵向排列

---

## Phase 3：画布可交互

- 拖拽节点改变位置
- 右键菜单：插入/删除/设为判断节点
- 判断节点（菱形）：双出口（True/False 各连一个 NextStep）
- 生成 IF-ELSE 分支代码

---

## 阶段工作量估算

| 阶段 | 估计 | 核心价值 |
|------|------|---------|
| Phase 1 | ~1天 | 生成可用代码 |
| Phase 2 | ~1天 | 流程图预览 |
| Phase 3 | ~1.5天 | 完整 SFC 体验 |
