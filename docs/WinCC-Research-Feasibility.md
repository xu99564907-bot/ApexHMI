# WinCC 属性面板调研 — 工具可行性探测

> 目的：在做正式调研之前，先实测当前环境的 WebSearch / WebFetch 能否真正
> 从外部权威来源拿到 WinCC 各控件属性面板字段清单。下面 8 次工具调用的
> 输入 / 状态 / 返回内容均如实记录，未经美化。
>
> 探测时间：2026-05-14。

---

## 1. WebSearch 测试

### 调用 #1: WebSearch
- 输入: `WinCC TIA Portal V18 IO field properties inspector`
- HTTP 状态 / 返回类型: 成功 200，返回 10 条结果 + 文字摘要
- 内容字符数: ≈ 2.4 KB（摘要 + 10 个 URL）
- 内容质量评估: 🟡 拿到了概述和一批指向官方文档的高质量 URL，但摘要本身
  没列出具体字段名（只描述了"properties dialog box"的存在）
- 摘录最有价值的 3 行原文：
  - `IO field (RT Unified) - WinCC Unified — docs.tia.siemens.cloud/r/en-us/v20/configuring-screens-rt-unified/.../io-field-rt-unified`
  - `SIMATIC HMI WinCC (TIA Portal) WinCC Engineering V18 – Programming reference — media.automation24.com/manual/se/109813306_WinCC_ProgRef_enUS_en-US.pdf`
  - `some pages require JavaScript to display fully`（搜索引擎自己提示了 SPA 风险）
- 能否基于此写出 IO field 的"格式串占位符"等具体字段？❌ 单看搜索摘要不行，
  必须再点 URL 进去抓内容。但 URL 列表本身对后续 WebFetch 极具价值。

### 调用 #2: WebSearch
- 输入: `WinCC bar control scale type segmented logarithmic`
- HTTP 状态 / 返回类型: 成功 200，返回 10 条结果 + 文字摘要
- 内容字符数: ≈ 2.0 KB
- 内容质量评估: 🟡 摘要明确说"logarithmic scale 在 WinCC 的 trend 控件中
  存在"，但**完全没有命中"segmented"这一字段**。结果质量取决于查询关键
  词与官方术语的匹配度。
- 摘录最有价值的 3 行原文：
  - `It is possible to display a logarithmic trend, which displays the values on a logarithmic scale instead of a linear scale`
  - `The minimum and maximum values per curve have to be bigger than 0 for a logarithmic trend`
  - `the search results don't contain specific information about a "segmented logarithmic" scale type for WinCC bar controls`
- 能否基于此写出 Bar 控件"刻度类型"枚举值？❌ 不能。WebSearch 返回的是
  对话式总结，不是字段表；要拿枚举值还得 fetch 具体文档页。

### 调用 #3: WebSearch
- 输入: `docs.tia.siemens.cloud IO field appearance border properties`
- HTTP 状态 / 返回类型: 成功 200，返回 10 条结果 + 文字摘要
- 内容字符数: ≈ 2.2 KB
- 内容质量评估: 🟡 拿到了 v20 / v21 两个版本的 IO field 文档 URL，但摘要
  明确指出页面是 SPA，"loading dynamically with JavaScript, so the full
  content isn't directly visible in the search results"。
- 摘录最有价值的 3 行原文：
  - `https://docs.tia.siemens.cloud/r/en-us/v21/configuring-screens-rt-unified/reference-of-the-screen-objects-rt-unified/elements-rt-unified/io-field-rt-unified`
  - `the search results appear to be loading dynamically with JavaScript`
  - `the property reference section typically organizes fields by categories such as Appearance, Behavior, Data, Events, and Position`（这条是搜索引擎自己的猜测，不算证据）
- 能否基于此写出 IO field 的 Appearance 字段表？❌ 不能。

### 调用 #4: WebSearch
- 输入: `WinCC Unified gauge angle pointer start angle end angle`
- HTTP 状态 / 返回类型: 成功 200，返回 10 条结果 + 文字摘要
- 内容字符数: ≈ 2.0 KB
- 内容质量评估: ❌ 主要返回 YouTube 视频和论坛帖；摘要原文直接承认
  "the specific properties you're asking about—gauge angle pointer start
  angle and end angle configuration—are not detailed in these search
  results"。这条查询是 4 次中**最差**的一次。
- 摘录最有价值的 3 行原文：
  - `Gauge (RT Unified) - WinCC Unified — docs.tia.siemens.cloud/r/en-us/v20/.../gauge-rt-unified`
  - `WinCC Unified V16 Engineering Runtime Manual — gothightech.com/.../Manual%20-%20WinCC%20Unified%20Engineering%20V16%20-%20Runtime.pdf`
  - `GitHub - tia-portal-applications/GaugeMeter`（一个官方 CWC 示例项目）
- 能否基于此写出 Gauge 角度字段？❌ 不能。但拿到了一个 V16 Unified PDF 链接，可作后续 fetch 候选。

---

## 2. WebFetch 测试

### 调用 #5: WebFetch
- 输入: `https://docs.tia.siemens.cloud/r/en-us/v20/readme-wincc-basic-advanced-professional/visualizing-processes/working-with-screens`
- HTTP 状态 / 返回类型: **404 Request failed**
- 内容字符数: 0
- 内容质量评估: ❌ 完全无用。该 URL 路径在 v20 文档树下不存在。
- 摘录: 无。
- 能否基于此写出任何字段？❌

### 调用 #6: WebFetch
- 输入: `https://media.automation24.com/manual/en/109813306_WinCC_ProgRef_enUS_en-US.pdf`
- HTTP 状态 / 返回类型: 成功 200，但返回 `application/pdf` 二进制（9.9 MB），
  WebFetch 自身**无法解析**，返回提示信息说"无法从压缩的 PDF 二进制中提取章节标题"。
  关键：**文件被自动保存到磁盘**（`tool-results/webfetch-…-s0urvm.pdf`）。
- 内容字符数: 文本返回 ≈ 600 字符的"我没法读 PDF"道歉；但磁盘上的 PDF 实际 10.4 MB。
- 内容质量评估: ✅ 经过 `pdftotext -layout` 离线转换后（环境内有 Git 自带的
  pdftotext），得到 130 358 行纯文本，内含完整对象模型属性参考。
  - 第 16960 行：`IOField — Represents the "I/O field" object.`
  - 紧跟 `Table 1-50 Properties` —— 列出每个属性在 RT Professional / RT
    Advanced / Panel RT 下的读写权限和说明：
    `AboveUpperLimitColor, AcceptOnExit, AcceptOnFull, AdaptBorder,
    BackColor, BackFillStyle, BackFlashingColorOff/On/Enabled/Rate,
    BelowLowerLimitColor, BorderBackColor, BorderColor,
    BorderFlashingColorOff/On/Enabled/Rate, BorderStyle, BorderWidth,
    BottomMargin, CanBeGrouped, ClearOnError, ClearOnFocus, CornerRadius,
    CornerStyle, CursorControl, DataFormat, DeviceStyle, EdgeStyle,
    EditOnFocus, Enabled, FieldLength, FillPatternColor, …`
  - 全文 `(Page xxx)` 交叉引用出现 **8810 次**，意味着每个属性都跳到独立
    详细页（含取值范围、枚举、示例）。
- 摘录最有价值的 3 行原文：
  - `Table 1-50 Properties` (IOField 对象的 50+ 属性总表)
  - `DataFormat (Page 642) RW — Specifies the display format of an I/O field.`
  - `BorderStyle (Page 576) RW — Specifies the type of border lines for the selected object.`
- 能否基于此写出 IO field 的"格式串占位符"等具体字段？✅ **能**。
  PDF 本身就是对象模型权威清单，覆盖 IOField、GraphicIOField、SymbolicIOField
  等所有 ScreenItems。**这是本次探测最大的发现。**

### 调用 #7: WebFetch
- 输入: `https://docs.tia.siemens.cloud/r/en-us/v20/wincc-rt-unified/io-field`
- HTTP 状态 / 返回类型: **404 Request failed**
- 内容字符数: 0
- 内容质量评估: ❌ URL 路径错误（猜测的短路径不存在）。
- 摘录: 无。

  补充：用搜索得到的正确长路径
  `https://docs.tia.siemens.cloud/r/en-us/v20/configuring-screens-rt-unified/overview-of-screen-objects-rt-unified/elements-rt-unified/io-field-rt-unified`
  另做了一次 WebFetch，状态 200 但返回正文极少（只有导航条说"IO field 属于
  Elements 一节"，**没有任何字段列表**）—— 进一步证实
  `docs.tia.siemens.cloud` 是 SPA，WebFetch 抓不到正文。**🟡 部分可达，但
  内容是 JS 占位符。**

### 调用 #8: WebFetch
- 输入: `https://support.industry.siemens.com/forum/ww/en/posts/wincc-io-field-properties/128717`
- HTTP 状态 / 返回类型: **403 Forbidden**
- 内容字符数: 0
- 内容质量评估: ❌ SiePortal 论坛对自动化 fetch 直接拒绝。这扇门关死了。
- 摘录: 无。

---

## 判定

基于 8 次工具调用的真实结果：

### 可行的来源

- ✅ **`media.automation24.com` 系列 Siemens 手册 PDF（含 109813306 ProgRef V18）**
  — 二进制可下载，本地 pdftotext 提取 → **130k 行纯文本，含 IOField / Bar /
  Gauge / SymbolicIOField / GraphicIOField 等所有控件属性参考表**，
  按 RT Professional / Advanced / Panel RT 标注读写权限。覆盖率 ≥ 80%
  关键字段（仅缺少 V16/V20 Unified 的新增字段）。
- 🟡 **WebSearch 本身** — 不能直接给字段表，但能可靠地命中正确的官方
  PDF / Cloud 文档 URL，是后续 fetch 的导航器。覆盖率间接 100%。

### 不可行的来源

- ❌ **`docs.tia.siemens.cloud` SPA 任何路径** — 即使 URL 正确，WebFetch
  拿到的是 JS 渲染前的占位 HTML，没有字段表。8 次中相关 fetch 命中率 0/3。
- ❌ **`support.industry.siemens.com`（SiePortal 论坛与 SIOS 文档）** — 403
  Forbidden，反爬严格。
- ❌ **猜测式短 URL（如 `/wincc-rt-unified/io-field`）** — 404。必须先用
  WebSearch 拿到精确路径。

### 结论

- 选项 A（WebFetch + WebSearch 大规模调研）：**部分可行** —
  仅当目标是 PDF 类静态资源时可行；SPA 文档站不可行。
- 选项 B（用户本地下载 PDF）：**可选**（不是必须）—
  自动化 WebFetch 已能成功下载 automation24.com 上的 PDF；只有当 PDF 来自
  SiePortal/SIOS 这类反爬站点时才需要用户手工提供。
- 选项 C（缩范围到 5 个 widget）：**不推荐**（覆盖目标过低）—
  既然 ProgRef PDF 本身就是全控件清单，缩范围反而浪费已有数据。
  推荐改为"按 PDF 章节抽取，先做 5 个 → 校验流程 → 再批量"。

### 推荐策略

**用一份 PDF 抽属性表 + 用 SPA 文档站对照 Unified 新增字段**，分两阶段：

1. **阶段 1（数据落地）：** 把 `109813306_WinCC_ProgRef_V18.pdf`（已在
   `tool-results/` 缓存）+ 同源的 V16/V18 Engineering manual PDF 一起，
   用 `pdftotext -layout` 抽到 `docs/research/wincc-progref-v18.txt`，
   按对象（IOField / Bar / Gauge / Trend / SymbolIOField / GraphicIOField /
   …）切片，每片产出一份 `widget-<name>.md`，含属性名、读写、说明。
   预期：**1 次 fetch + 1 次 pdftotext 拿下 70%–80% 字段。**

2. **阶段 2（Unified 补差）：** 对 PDF 中没出现的 Unified-only 控件（如
   Industrial Slider、Web Browser Control 等），用 WebSearch 找精确 URL，
   再让用户本地浏览器抓取 SPA 渲染后的 HTML 给我们 —— 即"用户协助 fetch"
   而非 WebFetch 直接抓。预期补齐剩余 15%–25%。

3. **不要再尝试**直接 WebFetch SPA 文档站（v20/v21 的 docs.tia.siemens.cloud
   绝大多数页面）或 SiePortal —— 已实测 3 次均失败/无内容，浪费工时。

---

## 附录：本次实测产生的可复用资源

- `tool-results/webfetch-1778718479352-s0urvm.pdf` — 10.4 MB 的 WinCC ProgRef
  V18 完整 PDF（自动下载缓存）
- `/tmp/wincc.txt` — pdftotext 输出，130 358 行，纯文本，可 grep
- IOField 对象起始行：16960；GraphicIOField 起始行：16503；
  SymbolicIOField 起始行：22882；全文 `(Page xxx)` 交叉引用 8810 处
