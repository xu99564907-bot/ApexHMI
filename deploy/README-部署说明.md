# ApexHMI 现场部署包

> 版本：v0.8（M7 阶段构建）
> 适用：Windows 10 1903+ / Windows 11 / Windows Server 2019+

---

## 📦 包含内容

```
ApexHMI-Deploy/
├── ApexHMI/                              ← 主程序（35 MB）
│   ├── ApexHMI.exe
│   ├── *.dll （30+ NuGet 包）
│   ├── config/                            ← 配置文件
│   ├── data/                              ← 运行时数据（首次启动自动建）
│   ├── docs/User-Manual.md                ← 帮助菜单调用此文档
│   └── runtimes/win-x64/                  ← SQLite native
├── deps/                                  ← 依赖安装包
│   ├── ndp48-x86-x64-allos-enu.exe        ← .NET 4.8 离线安装包（~64 MB）
│   ├── MicrosoftEdgeWebview2Setup.exe     ← WebView2 Bootstrapper（~2 MB）
│   └── PortableGit/                       ← MinGit 便携版（~50 MB）
├── deploy.bat                             ← 自动部署脚本（首次跑）
├── 启动 ApexHMI.bat                       ← 日常启动
└── README-部署说明.md                     ← 本文档
```

总大小约 **130 MB**。

---

## 🚀 部署步骤

### 首次部署（每台目标机器一次）

1. 把 `ApexHMI-Deploy` 整个目录拷到目标机器，**推荐放 `C:\ApexHMI\`** 或 `D:\软件\ApexHMI\`
2. **右键 `deploy.bat` → 以管理员身份运行**
3. 脚本依次检查 / 安装：
   - ✅ .NET Framework 4.8（缺则自动装 — 可能需重启）
   - ✅ Microsoft Edge WebView2 Runtime（缺则自动装）
   - ✅ Git（系统有则用，否则把内置 MinGit 加入 PATH）
4. 安装完成后自动启动 ApexHMI

### 日常启动

直接双击 `启动 ApexHMI.bat`，或者直接双击 `ApexHMI\ApexHMI.exe`。

### 桌面快捷方式（可选）

右键 `ApexHMI\ApexHMI.exe` → 发送到 → 桌面快捷方式

---

## ⚙️ 部署后配置

首次启动后建议立即配置：

1. **PLC OPC UA 连接**
   - 主界面右下角"通讯状态"显示"未连接"
   - 编辑 `ApexHMI\config\connection.json`（或软件内 OPC UA 浏览器配置）

2. **Git 仓库**
   - 进入「设计器 → 手动程序生成」 → 左栏「Git 代码拉取」展开
   - 填仓库地址 + 本地保存目录 + 私有仓库填 Token
   - 详细见 `ApexHMI\docs\User-Manual.md` 第五章

3. **管理员账户**
   - 默认账户在 `config/users.json`（如有）；首次登录可能强制改密码

---

## 🔧 故障排查

### 启动闪退 / 无反应

1. 检查 .NET 4.8 是否真的装好：
   ```cmd
   reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release
   ```
   返回的数字 ≥ `528040` 即 OK
2. 查 `ApexHMI\logs\crash\` 看崩溃日志（自动生成）

### HTML 浏览器 / PDF 视图 控件黑屏

WebView2 Runtime 没装或损坏。手动跑 `deps\MicrosoftEdgeWebview2Setup.exe`。

### Git 拉取按钮无反应

`where git` 命令在 cmd 里看是否有输出。无 → MinGit 路径没生效。
- 重开命令窗口（PATH 环境变量需重新加载）
- 或者手动在系统环境变量加 `deps\PortableGit\cmd`

### SQLite 数据库报错

首次启动 ApexHMI 会在 `data\` 创建 `audit.db` / `trend.db`。如报权限错误，把目录所有权改为当前用户。

---

## 🔄 升级

收到新版后：
1. 备份 `ApexHMI\config\` 和 `ApexHMI\data\` 两个目录
2. 删 `ApexHMI\` 整个目录
3. 解压新版 `ApexHMI\` 目录
4. 把备份的 `config\` 和 `data\` 拷回
5. 双击 `启动 ApexHMI.bat`

`deps\` 目录通常不需要换（除非新版要求更高 .NET 版本）。

---

## ❓ 常见问题

**Q：能不能不装 .NET 4.8？**
A：不能。ApexHMI 是 .NET Framework 4.8 程序，没有 runtime 起不来。Windows 10 1903 及以后版本系统自带，老 Windows 必须装。

**Q：能不能不装 WebView2？**
A：可以，只影响 HTML 浏览器 / PDF 视图两个控件。其他功能（PLC 通讯、设计器、报警、配方、趋势）都正常。

**Q：能不能不用内置 Git？**
A：可以，前提是目标机已装 Git（`git --version` 有输出）。如果完全不用 Git 同步功能，整个 deps\PortableGit\ 可删除省 50 MB。

**Q：把 deps/ 全删了能跑吗？**
A：能，只要前面 3 项依赖已装。deps/ 只是离线安装包，部署后可删除。

**Q：能跨架构吗（如 ARM）？**
A：不能。当前编译 x64 + SQLite native 是 x64 版。如需 ARM 需重新编译。

---

## 📞 联系

如发现 bug 或需新功能，提 issue 到内部仓库或联系开发团队。
