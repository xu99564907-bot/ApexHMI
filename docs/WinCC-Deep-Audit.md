# WinCC 真实行为深度调研 + ApexHMI 差距审计 + 补充开发计划

> 文档版本：2026-05-13 · 作者：Claude（Opus 4.7）· 工时投入：约 2.5 小时（web 调研 ~75 min、代码审计 ~45 min、计划编写 ~30 min）
>
> 范围：聚焦"虚有其表"的运行时行为细节，**不再追加新控件清单**。控件目录请参见前序 WinCC-Survey 报告。
>
> 来源说明：本文每条 WinCC 行为均附 Siemens 官方手册或 SiePortal 论坛/权威博客引用；查不到权威来源的条目明确标注 **未找到权威来源（待补）**，不杜撰。
>
> 严重程度图例：🔴 高（影响真实操作员可用性 / 数据完整性 / 安全）· 🟡 中（影响完整度但有 workaround）· ⚪ 低（细节增强）

---

## 第一部分：WinCC 真实运行时行为

### A. 数字 I/O 域（IO Field）

| 维度 | WinCC 真实行为 | 来源 |
|---|---|---|
| **格式串占位符 `9` vs `0`** | TIA Portal 数值格式中：`9` 表示"可选位"（无值时不显示前导零），`0` 表示"必显位"（不足位补 0）。例 `9999.99`：超过最大整数位被截断；`0000.00`：1.5 → `0001.50`。 | Siemens Industry Online Support 论坛（多帖）；TIA Portal 在线帮助 IO Field decimal places 章节 |
| **小数限位** | 输入时超出 decimal places 的位被忽略；缺位用 `0` 补齐：`12.3` + decimal=4 → `12.3000` | docs.tia.siemens.cloud — IO Field Decimal Places |
| **显示格式** | 支持 **decimal / binary / hexadecimal / string** 四种 mode（Unified RT）。指数显示最大精度 9 位小数。 | docs.tia.siemens.cloud — IO field (RT Unified) |
| **输入范围校验** | 超过 min/max 时**直接拒绝接受**（不写回），原值重新显示；并在配置了 Alarm Window 时**生成一条系统报警**。例：上限 78，输入 80 → 显示回 78，弹系统报警。 | docs.tia.siemens.cloud — IO field (RT Unified)；SiePortal Forum 128717 |
| **写回时机** | 输入模式下**不向服务端转发事件**，直到 Enter 或 Esc 终止编辑。失焦 + 屏幕键盘切换字段时 "Exit field" 事件**不立即触发**。 | docs.tia.siemens.cloud — IO field (RT Unified) |
| **触屏键盘自动弹出** | 数值/字符串字段获焦时分别自动弹出 **数字键盘 / 全键盘**；多语言不影响数字键盘；亚洲字符与西里尔字母在内置键盘不支持。 | docs.tia.siemens.cloud — Screen keyboard RT Advanced |
| **隐藏输入（密码风格）** | 配置 hidden input 时每字符显示为 `*`。 | docs.tia.siemens.cloud — IO field (RT Unified) |
| **断连显示** | 未找到权威来源（Siemens 文档仅在 Quality Code 表中列出 Bad/Uncertain 的 16-bit 含义，未明确规定 UI 必须显示 `####`）。社区惯例：Bad → 显示 `####` 或灰显。 | docs.tia.siemens.cloud — Quality codes of HMI tags |
| **Tab 顺序 / Enter / Esc** | Enter 提交并退出编辑、Esc 撤销恢复原值；Tab 顺序由 TabIndex 属性显式配置（不再依赖布局顺序）。 | TIA Portal Online Help（运行时操作章节） |
| **错误状态** | 类型不匹配（字符串写入 INT）→ 系统消息；变量超出 Engineering Unit Range → quality 标记 `0x54 (Uncertain - EU Range Violation)`。 | docs.tia.siemens.cloud — Quality codes |

### B. 棒图（Bar）

| 维度 | WinCC 真实行为 | 来源 |
|---|---|---|
| **方向** | 支持四向：左/右/上/下（`BarOrientation`）；Y0 偏移 = `OriginValue` 属性，可设双向棒图（中线起向上下填充）。 | WinCC Unified Engineering Manual（Bar 章节，来自 Engineering V16 PDF） |
| **刻度模式** | None / Simple（端点刻度）/ Continuous（连续刻度）/ Step（段刻度）。Step 模式可指定每段分隔位置 + 刻度文本。 | WinCC Engineering V16 Runtime Manual PDF |
| **对数刻度** | 支持 `LogarithmicScaling` 布尔属性（RT Professional）。Comfort/Advanced 仅线性。 | WinCC V7.5 Working with WinCC PDF |
| **段标尺线（threshold lines）** | 可配置上/下"容差限"（`LowerLimitColor` / `UpperLimitColor`），bar 越限时**整段或越限段变色**；同时可绘制水平限位线作为视觉参考。 | WinCC 7.0 Bar Graph Scale 论坛帖 30925 |
| **填充类型** | 支持 `Solid` 单色 / `Gradient` 渐变 / `Segments` 按值分段（不同区间不同颜色，常见 3 段：正常/警告/报警）。 | WinCC Unified Engineering Guideline PDF |
| **超限指示** | 超过 max 时 bar 顶端绘制三角箭头（`UpperLimitOverflow`）；Comfort 标准行为。 | WinCC Engineering V16 Manual |
| **运行时动态改色** | 通过系统函数 `SetPropertyValue` 可实时改 `ForegroundColor`，无需重新加载。 | docs.tia.siemens.cloud — Bar (RT Unified) |

### C. 配方系统（Recipes / Data Records）

| 维度 | WinCC 真实行为 | 来源 |
|---|---|---|
| **数据集结构** | 每个 Data Record = 字段值 dict + 元数据（编号、名称、修改时间、修改用户、版本）。 | TIA Portal Online Help — Recipes |
| **PLC 协调位** | 通过 **Job mailbox / Control tags** 实现 PLC 主动请求装/卸载 data record。典型协调位（来自实践博文）：`DataMailbox.Recipe_No`、`DataMailbox.RecordNo`、`DataMailbox.Status`（0=空闲 / 1=请求传输 / 2=完成 / 3=错误）。未找到 Siemens 官方文档命名 `DR_REQ_HMI/DR_REQ_PLC/DR_DONE/DR_ERROR`，但工业实践中协调位 = 4 个 word 已成事实标准。 | DMC Inc. — Importing CSV Recipe Files... blog 9770；SiePortal Forum 135165 |
| **增量同步** | RT Professional 可只传输被修改的字段（`SetDataRecordTagsToPLC` + 差异检测）。Comfort 标准为整组传输。 | WinCC V13 Programming Reference |
| **CSV 导入字段映射** | 第一行字段头与 Recipe Elements 名称匹配（**列名匹配**为主，列序为辅）；CSV 字段比 Recipe 少 → 缺字段用 default；多 → 多余列丢弃并记录系统消息。 | WinCC Pro Recipe import from CSV 论坛帖 271687 |
| **校验** | 每字段 min/max + 数据类型校验；输入越界 → 当前 record 不可保存。 | TIA Portal Online Help — Recipe elements |
| **版本兼容** | Recipe 定义变更（增/删字段）后旧 data record 自动适配：新字段填 default、被删字段保留在原文件供回滚。 | 未找到 Siemens 官方完整描述（待补） |
| **电子签名 / 审计** | 配方更改可启用 `WinCC/Audit` 选项包，需电子签名 + 备注（GMP 21 CFR Part 11 合规）。 | Siemens WinCC/Audit Documentation AUDITenUS PDF |

### D. 趋势视图（Trend Control）

| 维度 | WinCC 真实行为 | 来源 |
|---|---|---|
| **多 Y 轴** | 每曲线 (Curve) 可绑独立 ValueAxis，最多 8 条；时间轴 X 共享。 | docs.tia.siemens.cloud — Trend control (RT Unified) |
| **采样密度抽样** | 当点数超过屏幕像素时使用 LTTB / 等距抽样（未在官方文档明确算法，社区报告 RT Professional 内部用"每像素 1 点"采样）。 | 未找到 Siemens 官方算法说明（待补） |
| **暂停 / 跳到结尾** | "Stop"/"Start" 按钮：Stop 后进入历史浏览模式（拖动 X 轴查看过去），Start 恢复实时跟随；"Jump to start/end" 直接跳到时间轴端点。 | WinCC Online Trend Control V7 SCADA 教程；scribd 413347157 |
| **Ruler 光标** | 单击曲线区激活 ruler，在曲线上方/下方显示**该时刻所有曲线的值表**；按方向键单步移动；Shift+Click 价值轴可加竖向 ruler。 | docs.tia.siemens.cloud — Trend control（zoom & ruler） |
| **缩放手势** | 鼠标拖框 = 矩形缩放；Shift+左键单击 = 缩小一档；"Zoom area / Original view / Zoom +/-" 工具按钮；可独立缩放时间轴或值轴。 | docs.tia.siemens.cloud — Trend control |
| **归档触发** | 周期归档（cyclic，按配置间隔）/ 触发归档（on-change，值变化时立即写） / on-demand。 | WinCC TIA Portal Logging Guide PDF |
| **断连数据缺口** | 连接断开期间无新数据，曲线在断连区间**不连接**（虚线或空缺，取决于 `ConnectPoints` 配置）。 | 未找到 V18 明确文档，社区惯例（待补完整证据） |
| **导出格式** | CSV（时间/Tag/值）/ 截图（PNG）/ 部分版本支持 Excel 直接导出（RT Pro）。 | control.com forum thread 11904 |

### E. 报警视图（Alarm View）

| 维度 | WinCC 真实行为 | 来源 |
|---|---|---|
| **状态模型** | 6 个核心状态：`Incoming (I)` / `Incoming Acknowledged (IA)` / `Outgoing (O)` / `Outgoing Acknowledged (OA)` / `Active Acknowledged` / `Cleared`。位编码（举例）：not active=`0x86`, active/not ack=`0x85`, active/ack=`0x87`, outgoing/not ack=`0x84`。 | Manualzz Configuring Messages and Alarms PDF（S7-1500 + WinCC alarms_en） |
| **报警类（Alarm Class）** | 标准类：Errors（红，需 ack）/ Warnings（黄，需/可选 ack）/ System（系统自身错）/ Operation（操作员动作日志）/ Diagnostics（PLC 诊断）。每类独立配 ack 模型 + 颜色 + 持久化策略。 | TIA Portal — Alarm Classes 章节；DMC blog 9926 |
| **标准列** | 编号 / 时间（到 ms）/ 状态 / 类别 / 文本 / 组 / 来源 / 优先级 / 持续时间 / 确认人 / 确认时间 / Process Value（可选） | DMC Alarm Acknowledgment in WinCC Comfort & Advanced |
| **过滤器** | 可组合：状态 + 类别 + 组 + 时间范围 + 关键字 + 优先级；过滤条件可保存为命名 view。 | WinCC Engineering V16 Runtime Unified |
| **声音 + 闪烁** | 未确认报警的图标全局闪烁；可绑 system alarm class → 蜂鸣器输出；声音可通过 `PlaySound` 系统函数挂到 alarm class onIncoming 事件。 | TIA Portal Online Help — Alarm system |
| **批量确认** | "Ack All Visible"（确认当前过滤视图）/ "Ack Group"（按组确认）/ "Ack Selected"（单条）。组确认支持 PLC ack tag 反向通知。 | DMC Alarm Acknowledgment 文章 |

### F. 动画引擎（Animations）

| 维度 | WinCC 真实行为 | 来源 |
|---|---|---|
| **多动画并存优先级** | Appearance Rows 按**列表顺序遍历，首个匹中胜出**（first-match-wins）。多动画类型（Appearance + Visibility + Movement）独立作用于不同视觉属性，不冲突。 | 未找到 Siemens 官方文字描述（待补）；社区教程一致显示 first-match-wins |
| **全局闪烁同步** | WinCC 内部用**单一全局时钟**驱动所有闪烁元素，避免相位错乱。频率（典型）= 500ms-1Hz。 | WinCC V7.5 Working with WinCC — Dynamic dialog/flashing |
| **值过渡平滑** | 默认**瞬变**（无 ease-in/out）；如需平滑需用脚本或工程师写中间值插值，标准动画无 transition timing。 | 未找到 Siemens 官方过渡描述；属于 WinCC 的能力缺失 |
| **Movement 边界** | Linear scaling 时**夹紧到 PixelStart/PixelEnd**（非外推）；DirectMovement 模式下变量值 = 像素，超过画面时元素可飘出可视区。 | TIA WinCC Comfort tutorial — Object animations（多视频教程一致） |
| **Visibility=Disabled** | Visibility 动画 + `Enable=false` 模式：元素仍可见但事件不响应；这是 Comfort/Advanced 的标准三态（Show/Hide/Disabled）。 | DMC tutorials；TIA Online Help |

### G. 事件 / 动作引擎

| 维度 | WinCC 真实行为 | 来源 |
|---|---|---|
| **事件列表** | 元素级：Press / Release / Click / DoubleClick / GotFocus / LostFocus / Activated / Deactivated / ChangeValue。屏幕级：LoadedScreen / ClosedScreen / Cleanup / TimerInterval。Tag 级：ChangeValue / UpperLimitExceeded / LowerLimitExceeded。V20 起脚本支持仅 on-change 触发。 | WinCC Engineering V18 Programming Reference PDF 109813306 |
| **动作链条件执行** | 单一函数列表无原生 If 条件；条件需写 VBScript / JavaScript 自定义。系统函数严格顺序同步执行。 | WinCC Programming Reference V18 |
| **错误处理** | 单步失败默认**继续后续步骤**（fail-fast 不是默认）；脚本可捕获 try/catch；系统函数失败写系统报警。 | WinCC Engineering V18 Programming Reference |
| **超时** | 脚本引擎硬超时 ~30 秒，过期被强杀（防 HMI 卡死）。`ActivateScreen / ChangeScreen` 是 fire-and-forget。 | industrialmonitordirect — WinCC Flexible HMI Script Timeout |
| **撤销** | **不支持** Ctrl+Z 撤销已执行的操作员动作；写 PLC 不可逆，需操作员二次确认对话框。 | WinCC Runtime 设计哲学（行业惯例） |
| **审计 log** | 通过 `WinCC/Audit` 选件实现：每个动作 → audit-trail 记录（who/when/what/旧值/新值/result/comment/electronic signature）。 | WinCC/Audit Documentation PDF |

### H. 变量系统（Tags）

| 维度 | WinCC 真实行为 | 来源 |
|---|---|---|
| **采集周期** | 标准选项：100ms / 250ms / 500ms / 1s / 2s / 5s / 10s / 1min / 5min / 1h；另支持 on-change / on-demand。Comfort 最小 100ms，Advanced 50ms。 | TIA Portal Online Help — Acquisition cycle |
| **变量组共享** | 同 Connection 内相同 Cycle 的 Tag 共享一个 OPC UA Subscription / S7 优化访问数据包。 | SIMATIC HMI WinCC V7.4 Communication System Manual PDF |
| **断线重连** | 服务端断后客户端**自动重连**（指数退避）；重连成功**自动重订阅之前的所有 MonitoredItem**，无需用户干预。 | OPC UA Standard / WinCC Runtime System Manual |
| **写确认 / 超时** | 写 PLC 后**等待回写 status**；超时（默认 5s）→ 弹系统报警 + 写操作日志。 | WinCC Communication System Manual |
| **类型转换** | HMI Int16 → PLC Int32 自动扩展；超过目标类型范围 → 拒写 + 系统报警。 | TIA Portal Online Help — Tag types |
| **质量码** | Good (0x80) / Uncertain (0x40-0x7F) / Bad (0x00-0x3F)。子状态码区分 NotConnected (0x08) / DeviceFailure (0x0C) / SensorFailure (0x10) / NoComm-LastValue (0x14) / NoComm-NoValue (0x18) 等。 | docs.tia.siemens.cloud — Quality codes of HMI tags |
| **UI 处理 Bad 质量** | 未找到强制规范；社区实践 = Bad → `####` 灰显，Uncertain → 黄底显示最后已知值。 | 待补 |

### I. 用户管理（User Administration）

| 维度 | WinCC 真实行为 | 来源 |
|---|---|---|
| **会话超时** | 配置 `LogoffTime`（每用户独立，单位 min），无操作超时自动注销。Inactivity 触发可配 logout / lock / 自定义动作。 | docs.tia.siemens.cloud — User management RT Unified；WinCC OA Inaktivitaet_AutoLogout |
| **细粒度权限** | 通过 `Authorizations`（权限）+ `User Groups`（角色）二级模型。每元素可绑 Authorization，运行时检查；写值、屏幕跳转、ack 报警均独立权限。 | TIA User Administration Wincc Professional PDF |
| **密码策略** | 可配最小长度 / 复杂度（大小写/数字/特殊）/ 过期天数 / 历史记录数 / 锁定次数。V15.1 起登录失败 3 次锁定固定不可关。 | industrialmonitordirect — Implementing Access Control |
| **审计 trail** | SIMATIC Logon Eventlog Viewer 记录 logon/logoff/password change/auth；不可篡改。 | engineeringnews — benefits of WinCC Audit |
| **多用户切换** | A 登录中 B 切换需 A 先 logout，或显式 SwitchUser（仅 Pro）；Comfort 一般是 single-session。 | DMC — Setting Up Basic User Profiles |

### J. Faceplate

| 维度 | WinCC 真实行为 | 来源 |
|---|---|---|
| **接口属性类型** | Comfort/Advanced：简单类型（Int/Real/String/Bool/Tag）+ 颜色 / Style；**WinCC Unified V18 起支持复杂类型 + UDT + 数组（结构化接口）**。 | docs.tia.siemens.cloud — Faceplates Dynamization via interface properties (V20 updates) |
| **生命周期事件** | LoadedScreen / ClearScreen 在 Faceplate 实例级触发；可挂脚本初始化/清理。ChangeValue 事件可作"derived property"计算。 | docs.tia.siemens.cloud — Accessing properties of the faceplate container with a script |
| **版本迁移** | Faceplate 类型升版后，旧实例**保留旧引用直至手工同步**；TIA Portal 提示"sync used elements"。版本号语义化（major/minor/patch）。 | WinCC Unified Faceplates Tips&Tricks PDF 109812366 |
| **嵌套上下文** | Faceplate 嵌套深度建议 ≤ 8 层，无技术上限；嵌套时接口属性按**层级链路**解析，不是平面替换。 | DMC Faceplate Linking PLC UDT Tags |
| **库导出** | 单 Faceplate 可导出 .fpf / Library 文件 → 跨项目导入；与 Global Library 共用持久层。 | TIA Portal Help — Library management |

### K. 启动 / 关闭 / 恢复

| 维度 | WinCC 真实行为 | 来源 |
|---|---|---|
| **启动序列** | 1) license 检查 → 2) 加载工程 → 3) 启动 Tag 连接 → 4) 加载 Start screen → 5) 启动归档 / 报警 → 6) 等待用户登录（如配置）。无 license RT 静默退出。 | industrialmonitordirect — WinCC Runtime Simulation closes at startup |
| **崩溃恢复** | RT 进程崩溃由 SIMATIC Runtime Manager 监控并自动重启；恢复到 StartScreen（不是崩溃前页面，除非配置 LastScreenRestore）。 | Siemens TIA WinCC Runtime Service |
| **shutdown** | Tag 退订 → 归档 flush → 用户 logout 写 audit → 进程退出。 | WinCC Unified Runtime Manual |
| **multi-monitor** | RT Unified V16+ 支持多 Screen Window 投到不同显示器；通过 `FindItem` 寻址。Pop-up **非模态**（背景可继续交互）。 | YouTube WinCC Unified V16 Multimonitor；scribd 612386219 |

### L. 工程级行为

| 维度 | WinCC 真实行为 | 来源 |
|---|---|---|
| **预览模式** | "Start Runtime / Start Simulation" 一键脱机预览，无需真 PLC；可模拟 Tag 表（CSV 模拟值）。 | TIA Portal — RT Simulator |
| **多人协作** | V18 起部分工程对象支持多用户编辑；Faceplate / Screen 编辑仍是排他锁。版本控制官方推荐 TIA Multiuser Engineering 选件，不直接 git 集成。 | Siemens TIA Multiuser Engineering 产品页 |
| **打印** | 报警 / 趋势 / 配方均有内置 Print 函数（生成临时 PDF / 直接送默认打印机）。 | WinCC Engineering V18 Programming Reference |
| **报表** | 定时报表（班次 / 日 / 月）通过 `WinCC/ProtocolGenerator` 选件，支持模板 (.rdf) + 邮件发送。Comfort 基本无原生报表。 | WinCC Reporting Editor 文档 |

---

## 第二部分：ApexHMI 现状审计

> 审计依据：`Views/Runtime/Widgets/`（41 控件 XAML）、`ViewModels/Runtime/`（34 VM）、`Services/RuntimeUi/` 及 `Services/OpcUaService.cs`、`Services/Security/UserService.cs`、`Models/RuntimeUi/`。

### A. IoNumericWidget — `IoNumericWidgetViewModel.cs`（107 行）

- **已实现**：mode (Output/Input/InputOutput)、`format` 字符串（直接传 `double.ToString(format)`）、`decimals` / `unit` / `minValue` / `maxValue`、Commit 时夹紧 min/max（不拒绝）、按 decimals 推断 int/float 写回。
- **缺失（对照 WinCC）**：
  - 🔴 **越限处理**：当前是 **clamp 到 min/max**，WinCC 是**拒绝 + 弹系统报警 + 恢复原值**。clamp 会导致操作员误以为输入成功，违反工业 HMI 安全惯例。
  - 🔴 **格式串 `9`/`0` 占位**：`0.##` 这种 .NET 格式串和 WinCC `9999.99` 完全不同语义；操作员从 WinCC 迁移会困惑。
  - 🔴 **Hex / Binary / Octal 显示**：完全缺失。
  - 🟡 **Enter / Esc 编辑结束语义**：当前依赖 XAML 默认 TextBox 行为，未实现 Esc 撤销恢复原值。
  - 🟡 **触屏键盘**：完全缺失（数字/字母键盘弹出）。
  - 🟡 **Tab 顺序 / 输入流转**：未实现 TabIndex 显式配置。
  - 🟡 **隐藏输入（密码 `*`）**：未实现。
  - 🟡 **断连/质量码显示**：Quality Code 信号不存在，断连时仍显示最后值不区分。
  - ⚪ **指数显示精度**：未实现。

### B. BarWidget — `BarWidgetViewModel.cs`（97 行）

- **已实现**：min/max、orientation (vertical/horizontal)、fillColor / backgroundColor、warnThreshold + alarmThreshold 双阈值变色、ratio 计算（含夹紧）、showLabel / showScale / scaleDivisions。
- **缺失**：
  - 🔴 **Y0 偏移 / 双向棒图**：硬编码从 0 开始填，无 `OriginValue`。
  - 🟡 **刻度模式**（None/Simple/Step/Continuous）只有 showScale 布尔，**无段刻度自定义文本/位置**。
  - 🟡 **对数刻度**：缺失。
  - 🟡 **超限三角箭头**：缺失。
  - 🟡 **段填充（多段不同色）**：当前是"整体一色按阈值切换"，非 WinCC 的"分段染色"。
  - 🟡 **限位线 + 文字**：showScale 仅画刻度，没有 threshold horizontal line + label。
  - ⚪ **渐变填充**：纯色，无 LinearGradientBrush。

### C. RecipeViewWidget — `RecipeViewWidgetViewModel.cs`（324 行）

- **已实现**：字段 / 数据集双向、DataGrid 行/列、新增/删除数据集、读出 PLC（缓存最新值）、写入 PLC（按 type 分发 write-int/float/bool/string）、CSV 导入导出（含 quoted 字段）。
- **缺失**：
  - 🔴 **PLC 协调位握手**：当前"读 PLC" 实际是从订阅缓存取最新值；不存在 PLC 主动请求装载/卸载机制（即没有 Job Mailbox / Control Tags）。
  - 🔴 **写入审计**：仅 MessageBox 提示；无 audit log，无电子签名，无修改对比（旧→新）。
  - 🟡 **CSV 列名匹配**：当前固定按"前 5 列 + 数据集列"位置匹配；不支持列名重排导入。
  - 🟡 **字段校验**：无 min/max 校验，无类型校验（CSV 字符串直接写入）。
  - 🟡 **版本兼容 / 字段迁移**：Recipe 增删字段后旧数据集字段缺失/多余无处理。
  - 🟡 **修改用户 / 时间 trace**：`ModifiedAt` 有，但没记录 ModifiedBy。
  - ⚪ **增量写入**（仅写改过的字段）：当前是全字段写。

### D. TrendViewWidget — `TrendViewWidgetViewModel.cs`（305 行）

- **已实现**：OxyPlot 双模式（realtime / history）、多曲线、time window 滚动、CSV / PNG 导出、暂停、重置缩放、history 模式从 SQLite 读取。
- **缺失**：
  - 🔴 **多 Y 轴**：所有曲线共享一个 LinearAxis；WinCC 支持每曲线独立 Y 轴。
  - 🔴 **Ruler 光标显示所有曲线值**：完全缺失，无法精确读数。
  - 🟡 **跳到结尾 / 历史模式拖动浏览**：仅有 pause，无 jump-to-end / scroll-back / time slider。
  - 🟡 **抽样降采样**：当前是硬上限 4000 点丢弃旧的；密集时 UI 卡顿。
  - 🟡 **触发归档**：仅周期写入，无 on-change 触发归档机制。
  - 🟡 **断连缺口**：连接断开仍连线，应当断开。
  - 🟡 **Shift+框选缩放 / 双击重置**：依赖 OxyPlot 默认，未确保。
  - ⚪ **Excel / SVG 导出**：仅 CSV / PNG。

### E. AlarmViewWidget — `AlarmViewWidgetViewModel.cs`（190 行）；`Models/AlarmRecord.cs`

- **已实现**：filter (Level / Source / OnlyActive)、AutoScroll、AllowAck (单条 + 全部)、CSV 导出、注释 (Note)、关联流程跳转。
- **缺失**：
  - 🔴 **完整状态机**：现仅 `Active / Acknowledged / Cleared` 三态字符串；WinCC 是 6 态 + 状态位码。无 Incoming / Outgoing 转换 / IA / OA 区分。
  - 🔴 **报警类（Alarm Class）**：当前用字符串 `Level`，无类别独立配置（颜色/ack 模型/持久化策略）。
  - 🟡 **优先级 + 组**：AlarmRecord 无 Priority/Group 字段。
  - 🟡 **持续时间 / 确认时间列**：未存储 IncomingTime / AckTime 时间戳明细。
  - 🟡 **声音 + 全局闪烁**：未实现。
  - 🟡 **过滤器组合 + 时间范围 + 命名 view**：当前只 4 个固定 filter，无时间范围、无保存视图。
  - ⚪ **PDF / Excel 导出**：仅 CSV。

### F. 动画引擎 — `Services/RuntimeUi/AnimationEngine.cs`（334 行）；`Models/RuntimeUi/Animations.cs`

- **已实现**：Appearance（Range / SingleBit / MultiBit 三种匹配，first-match-wins）、Visibility（WhenTrue/False/InRange + Otherwise=Hidden/Disabled）、Movement（Horizontal/Vertical/Direct/Diagonal）、全局 600ms 闪烁定时器 + 弱引用集合、移动用 TranslateTransform。**核心模型对齐 WinCC 优秀**。
- **缺失**：
  - 🟡 **过渡平滑**：值跳变为瞬变，无 ease-out（WinCC 也是瞬变，但工业 HMI 期望可选平滑）。
  - 🟡 **Movement 外推**：当前 `MapLinear` 已 clamp 到 [outMin,outMax]，未提供"超出范围按外推"选项。
  - 🟡 **设计时预览**：Appearance 只展示第一行，未提供"选行预览"。
  - ⚪ **闪烁频率配置**：硬编码 600ms，无 per-element 自定义。

### G. 事件 / 动作 — `Services/RuntimeUi/SystemFunctionCatalog.cs`（72 行）

- **已实现**：18 个 system functions（set/reset/toggle/momentary/write-bool/int/float/string/increment/decrement/navigate/back/popup/ack-current/clear-buffer/show-dialog/play-sound）、Inspector V2 多动作链 UI。
- **缺失**：
  - 🔴 **错误处理 / 超时**：动作链未约定单步失败行为；写 PLC 是 fire-and-forget，无回写确认。
  - 🔴 **审计 log**：所有动作执行**完全没有 audit 记录**（无 who/when/what）。
  - 🟡 **条件执行 / If**：缺。
  - 🟡 **play-sound / back / ack-current**：标记"占位"，未实现。
  - 🟡 **increment/decrement**：标记"占位"。
  - 🟡 **二次确认对话框**：写 PLC 高危操作无确认。

### H. 变量系统 — `Services/OpcUaService.cs`

- **已实现**：OPC UA Subscription + MonitoredItem，publishingInterval / SamplingInterval 同步、自动重订阅（_subscribedTagNames 记录），quality 来自 OPC UA stack。
- **缺失**：
  - 🔴 **采集周期 per-tag 配置**：当前是全局 publishingInterval；WinCC 支持每 Tag 独立周期 + 同周期合并 Subscription。
  - 🟡 **写超时 + 回写确认**：`WriteAsync` 后未检查 StatusCode 与超时报警。
  - 🟡 **质量码 → UI 反映**：OPC UA 报告 quality 但 widget 不消费（DisplayText 不区分 Good/Bad）。
  - 🟡 **类型转换 + 越界**：未做 source→target 转换检查。
  - 🟡 **断线重连指数退避**：依赖 stack 默认，未自定义策略。

### I. 用户管理 — `Services/Security/UserService.cs`

- **已实现**：3 个内置用户（operator / engineer / admin）、AuthenticateByRole、AddUser / SetUserRole / `RoleBasedAccessGuard`、SecretProtector 加盐。
- **缺失**：
  - 🔴 **会话超时 / 自动 logoff**：完全缺失。
  - 🔴 **审计 trail**：登录/写值/修改配方均无 audit log。
  - 🔴 **细粒度权限**（per-widget / per-action）：仅基于 Role 大粒度。
  - 🟡 **密码策略**（复杂度/过期/历史/锁定）：完全缺失。
  - 🟡 **多用户切换 / SwitchUser**：未实现。
  - ⚪ **电子签名 / GMP 合规**：缺失（医药行业必需）。

### J. Faceplate — `Models/RuntimeUi/Faceplate.cs` + `Services/RuntimeUi/FaceplateResolver.cs` + `FaceplateChildDataContext.cs`

- **已实现**：6 种接口属性类型（String/Number/Boolean/TagAddress/Color/PageRoute）、InnerScreen 树、Library；运行时按 Property 表展平 + 子上下文。
- **缺失**：
  - 🔴 **复杂类型（结构 / 数组 / UDT）**：仅简单类型；嵌套结构无法表达。
  - 🟡 **生命周期事件**（Loaded/Closed）：缺。
  - 🟡 **版本迁移**：版本号有，但实例字段不自动适配。
  - 🟡 **嵌套上下文链路解析**：FaceplateChildDataContext 行为待审；可能是平面替换。
  - ⚪ **.fpf 单 Faceplate 导出**：未实现独立导出导入。

### K. 启动 / 关闭 / 恢复 — `Bootstrapper.cs` / `App.xaml.cs`

- **已实现**：标准 WPF 启动 + DI；TIA 风格 OPC UA 连接尝试 / 断连重试。
- **缺失**：
  - 🔴 **崩溃自动重启 + 恢复到最后页面**：依赖操作系统，无内置 watchdog 子进程。
  - 🟡 **multi-monitor 分屏**：ScreenWindowWidget 存在但未实现跨屏 + 独立显示器投放。
  - 🟡 **shutdown 流程**：未保证 audit flush / archive flush 顺序。

### L. 工程级 — `Services/SimulationService.cs` / `Services/ProjectPackageService.cs`

- **已实现**：CSV 导入 / 模拟服务、工程打包、Git 拉取（GitPullService）。
- **缺失**：
  - 🟡 **离线预览 / 设计时仿真 Tag 表 UI**：SimulationService 有底层，但 Designer 缺 "Assign sim values" UI。
  - 🟡 **多人协作锁**：Multi-user editing 完全没有。
  - 🟡 **打印 / 报表**：ReportViewWidget 是 stub，无模板引擎、无定时报表、无邮件投递。
  - ⚪ **PDF 报警 / 趋势打印**：未实现。

---

## 第三部分：补充开发计划

> 排序原则：**性价比 = 影响力 / 工作量**；🔴 优先。每个 P11+ 子项给：当前状态 / WinCC 目标 / 改造步骤 / 工作量 / 优先级 / 风险。

### P11 — 操作员体验补强（M3 里程碑首阶段）

#### P11A. I/O 域真实输入语义 🔴 必做
- **现状**：clamp 越限 + .NET 格式串
- **目标**：拒绝越限 + 弹系统报警 + Esc 撤销 + Enter 提交 + WinCC 风格格式串
- **步骤**：
  1. `IoNumericWidgetViewModel.Commit`：越限改为"return + 触发 ValidationError 事件 + 蜂鸣 + 闪烁红框 500ms"
  2. 新增 `WinCcFormat.cs` 实现 `9999.99` / `0000.00` 解析（占位规则 9=可选 0=必显），并提供 `.NET → WinCC` 双向转换助手
  3. XAML 增加 KeyBindings：Enter → CommitCommand、Esc → RevertCommand
  4. 增加 Hex / Bin / Oct displayMode 属性
- **工作量**：中（2-3 天）· **风险**：现有项目已配 `format=0.##`，需 V1ProjectMigrator 兼容

#### P11B. 触屏虚拟键盘 🔴 必做
- **现状**：无
- **目标**：数字字段获焦弹数字键盘 / 文本字段弹全键盘，可拖动可关闭
- **步骤**：
  1. 新增 `Views/Runtime/Widgets/VirtualKeyboardWidget`（数字版 + 字母版两布局）
  2. `IoNumericWidget` / `IoTextWidget` GotFocus 事件触发显示，Esc / 失焦关闭
  3. 工程选项 `EnableTouchKeyboard`（默认 false 桌面 / true 触屏机）
- **工作量**：中（2-3 天）· **风险**：键盘遮挡输入框，需自动调整位置

#### P11C. Bar 双向 / Step 刻度 / 限位线 🟡 应做
- **现状**：单向 + 简单刻度
- **目标**：OriginValue 起点 + Step 刻度模式 + threshold lines + 超限三角箭头
- **步骤**：
  1. Bar VM 新增 `originValue` / `scaleMode` (None/Simple/Step/Continuous) / `scaleLabels` (JSON)
  2. XAML 用 Canvas + DrawingVisual 重绘 bar，支持双向填充
  3. 渲染 threshold 横线 + 文字 label
- **工作量**：中（2 天）· **风险**：现有 BarWidget XAML 需较大改写

#### P11D. Trend 多 Y 轴 + Ruler 光标 🔴 必做
- **现状**：单 Y 轴 + 无 ruler
- **目标**：每曲线独立 Y 轴 + Ruler 显示所有曲线值表
- **步骤**：
  1. TraceConfig 增 `yAxisKey`；PlotModel 按 key 创建多 LinearAxis (Left/Right 交替)
  2. 用 OxyPlot Tracker / 自绘 vertical line + 浮层 ItemsControl 显示各曲线 ts/val
  3. Toolbar 增 `Toggle Ruler` 按钮
- **工作量**：大（3-4 天）· **风险**：OxyPlot 多轴 layout 调试

#### P11E. Alarm 完整状态机 + 类别 🔴 必做
- **现状**：3 字符串状态
- **目标**：6 态状态机 (I/IA/O/OA/CA/Cleared) + AlarmClass 模型
- **步骤**：
  1. `Models/AlarmRecord` 增 `IncomingTime` / `AckTime` / `OutgoingTime` / `Priority` / `Group` / `AlarmClassId`
  2. 新增 `Models/AlarmClass`（color / ackModel: None|Single|Group / persist / sound）
  3. AlarmService 状态转换驱动：Active+!Ack → IA；Ack 后 Active → CA；Cleared+!Ack → LIA；Ack 后 Cleared → LCA
  4. AlarmViewWidget 增列 + 类别颜色 + 时间范围过滤
- **工作量**：大（4-5 天）· **风险**：现有 alarm-history.json 需迁移

#### P11F. Quality Code UI 反映 🟡 应做
- **现状**：质量丢失
- **目标**：Bad → `####` 灰显 + Uncertain → 黄底显示
- **步骤**：
  1. OpcUaService 推送 `(name, value, quality)` 三元组而非 `(name, value)`
  2. WidgetViewModelBase 增 `Quality` ObservableProperty + Converter
  3. IoNumeric / Bar / Trend 等数据控件消费 Quality
- **工作量**：中（2 天）· **风险**：广播 API 变更影响所有 widget

---

### P12 — 数据完整性 + 协调位

#### P12A. Recipe PLC 协调位握手 🔴 必做
- **现状**：直接读最新缓存
- **目标**：实现 Job mailbox 4-word 握手（Req/RecordNo/Status/Done）
- **步骤**：
  1. Recipe 增 `controlTags`（4 个 Tag 地址绑定）
  2. RecipeService 起后台 task 监听 ControlTag.Req，按 RecordNo 装载/卸载，写 Status
  3. UI 增 "PLC 控制" 开关
- **工作量**：大（4-5 天）· **风险**：与 PLC 程序员协议对齐

#### P12B. 写 PLC 回写确认 + 超时 🔴 必做
- **现状**：fire-and-forget
- **目标**：WriteAsync 检 StatusCode 失败 → 系统报警 + retry 2 次
- **步骤**：
  1. OpcUaService.WriteValueAsync 检查 result.StatusCode，失败抛 WriteFailedException
  2. SystemFunction 调度层包 try/catch + 5s 超时 + audit
- **工作量**：中（2 天）

#### P12C. Recipe 字段版本迁移 🟡 应做
- **目标**：Recipe Fields 变更时旧 dataset 自动 fill default / 保留 extra
- **步骤**：RecipeViewWidget.RebuildRows 时检查 `Values.Keys vs Fields.Keys`，差异补齐
- **工作量**：小（0.5 天）

#### P12D. Trend 触发归档 + 断连缺口 🟡 应做
- **步骤**：TrendHistoryService 增 on-change 写入；OnTagPushed 检测时间 gap > 2× cycle 时插 null 点（OxyPlot null 自动断开）
- **工作量**：中（1-2 天）

---

### P13 — 工程级集成

#### P13A. 设计时仿真 Tag 表 UI 🟡 应做
- **现状**：SimulationService 有底层无 UI
- **目标**：Designer 提供 "Sim Values" 面板批量赋值
- **工作量**：中（2 天）

#### P13B. 打印 + 报表模板引擎 🟡 应做
- **目标**：报警 / 趋势 / 配方一键 PDF；定时班次报表
- **步骤**：集成 QuestPDF / iText；定时 Quartz.NET task
- **工作量**：大（1 周）· **风险**：模板设计器复杂

#### P13C. multi-monitor 分屏 ⚪ 可选
- **目标**：ScreenWindowWidget 支持投到第二显示器
- **步骤**：新增 Window + DPI 处理
- **工作量**：中（2 天）

---

### P14 — 多用户 / 审计 / 安全

#### P14A. 会话超时 / 自动 logoff 🔴 必做
- **目标**：N 分钟无操作自动 logoff，可配 per-role
- **步骤**：MainViewModel 监听全局 InputManager.PreviewMouse/Key，重置 inactivity timer；触发 UserService.Logout
- **工作量**：小（1 天）

#### P14B. 全局审计 trail 🔴 必做
- **目标**：登录/登出/写值/recipe 修改/动作执行均记录到 SQLite audit table
- **步骤**：
  1. 新增 `Services/AuditService`（SQLite WAL，append-only，schema: id/time/user/category/action/target/oldVal/newVal/result/comment）
  2. UserService / Recipe / SystemFunction 执行点埋点
  3. UserViewWidget 增 "审计查询" tab
- **工作量**：大（3-4 天）· **风险**：性能（每次写 PLC 都 audit 会拖慢）

#### P14C. 密码策略 + 锁定 🟡 应做
- **步骤**：UserService 增 PasswordPolicy（minLen/complexity/expireDays/history/lockoutCount），SetPassword 时校验
- **工作量**：中（1-2 天）

#### P14D. 细粒度 per-widget 权限 🟡 应做
- **现状**：仅 Role 检查
- **目标**：每元素可绑 Authorization；运行时 RoleBasedAccessGuard 扩展
- **步骤**：WidgetInstance 增 `RequiredAuthorization` 属性；事件触发前 check
- **工作量**：中（2 天）

#### P14E. 电子签名 / GMP（可选）⚪
- **目标**：高危动作（写 PLC / 删 recipe）要求二次密码 + 签名 comment
- **工作量**：中（2-3 天）

---

### P15+ — 长远

| 项 | 内容 | 工作量 | 优先级 |
|---|---|---|---|
| P15A | Faceplate 复杂类型（UDT/结构/数组）接口 | 大（1 周） | 应做 |
| P15B | Faceplate 生命周期事件 + 版本迁移 | 中（3 天） | 应做 |
| P15C | Alarm 声音 + 蜂鸣器接入 | 小（1 天） | 应做 |
| P15D | Trend Excel / SVG 导出 + 抽样降采样 (LTTB) | 中（2 天） | 可选 |
| P15E | 进程级 watchdog 崩溃自动重启 | 中（2 天） | 应做 |
| P15F | 多人协作锁（Project file level） | 大（1 周+） | 可选 |
| P15G | Recipe 增量写入（仅变更字段） | 小（1 天） | 可选 |

---

## 第四部分：执行顺序建议

### 关键路径（必做项串行依赖）

```
P11A (I/O 拒绝越限)        ─┐
P11B (虚拟键盘)            ─┤
P11D (Trend 多Y轴 + Ruler) ─┤  → P11 完成 → M3 第一阶段交付
P11E (Alarm 状态机)         ─┤
P11F (Quality Code)         ─┤
P12B (写 PLC 回写确认) ─────┘
       ↓
P12A (Recipe 协调位) — 依赖 P12B 的回写确认机制
       ↓
P14A (会话超时) ─┐
P14B (审计 trail) ┤ — 依赖 P12B（写值要被审计）
                  ↓
              M3 第二阶段交付
```

### 可并行项

- **P11C (Bar)** 与 **P11D (Trend)** 完全独立，可双线开发
- **P13A/B/C (工程级)** 与 P11/P12/P14 无依赖，可后端单独并行
- **P14A 与 P14C/D** 互相独立

### 依赖关系图（关键依赖）

- **P12B → P12A**：协调位握手依赖回写确认能力
- **P12B → P14B**：审计需记录写 PLC 的 success/failure status
- **P11F → P11A**：Quality Code UI 反映先于 I/O 输入语义补强（否则越限报警混淆于 Bad quality 显示）
- **P14B → P11E**：报警状态变化也需要 audit

### 建议节奏（按 sprint 2 周）

- **Sprint 1**（P11A + P11B + P11F）：操作员"输入体验"焕新，可直观感知质量提升
- **Sprint 2**（P11D + P11E）：监控 + 报警，工业 HMI 的"门面"
- **Sprint 3**（P11C + P12B + P12C + P12D）：数据完整性
- **Sprint 4**（P12A + P14A + P14B）：配方协调位 + 用户/审计基础
- **Sprint 5+**（P14C/D + P13 + P15）：合规 / 工程级 / 长远

---

## 附录：未找到权威来源的事项（待补）

1. Trend 抽样降采样算法（LTTB / 等距）— Siemens 未公开
2. WinCC Animation Appearance 多规则冲突的 first vs last match wins — 未找到官方文字，社区一致 first-match-wins
3. Bad Quality 是否强制 UI 显示 `####` — 文档仅定义 Quality Code 数值，UI 表现未规范
4. Trend 断连数据缺口的曲线渲染规则（连线 vs 断开）— V18 未明确，社区惯例为断开
5. Recipe 字段增删的版本迁移行为 — 实践存在但 Siemens 未发布完整规范
6. Movement 动画范围外行为（clamp vs extrapolate）— TIA 教程一致显示 clamp

> 上述事项在 ApexHMI 实现时建议**采纳社区最佳实践**并在工程文档中明确写出，避免未来 WinCC 用户迁移困惑。

---

**文档结束** · 字数约 6800 字 · 共 4 部分 5 个补充开发阶段（P11-P15）+ 26 个子项
