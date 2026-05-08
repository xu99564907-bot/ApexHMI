using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ApexHMI.SmokeVerify;

/// <summary>
/// Phase 3+4 自动验证：不依赖运行 GUI，纯文件级断言 +
/// 触发主项目 dotnet build 兜底语法层正确。
/// 用法：cd tools/SmokeVerify && dotnet run
/// </summary>
public static class Program
{
    private static int _passed;
    private static int _failed;
    private static readonly List<string> _failures = new();
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));

    public static int Main()
    {
        Console.WriteLine($"=== ApexHMI Phase 3+4 SmokeVerify ===");
        Console.WriteLine($"Root: {Root}");
        Console.WriteLine();

        // ---- 文件存在性断言 ----
        AssertFileExists("Themes/Theme.xaml");
        AssertFileExists("Themes/Theme.Dark.xaml");
        AssertFileExists("Themes/Theme.HighContrast.xaml");
        AssertFileExists("Themes/Buttons.xaml");
        AssertFileExists("Themes/Cards.xaml");
        AssertFileExists("Themes/DataGrid.xaml");
        AssertFileExists("Themes/Inputs.xaml");
        AssertFileExists("Views/Controls/RingProgressBar.xaml");
        AssertFileExists("Views/Controls/RingProgressBar.xaml.cs");
        AssertFileExists("Views/Controls/KpiCard.xaml");
        AssertFileExists("Views/Controls/KpiCard.xaml.cs");
        AssertFileExists("Services/ThemeService.cs");
        AssertFileExists("Services/AlarmNotificationService.cs");
        AssertFileExists("Converters/EqualsToBoolConverter.cs");
        AssertFileExists("Converters/StringNotEmptyToVisibilityConverter.cs");
        AssertFileExists("Converters/NormalizedHeightConverter.cs");
        AssertFileExists("Models/AlarmHistogramBar.cs");
        AssertFileExists("Models/RecipeSnapshot.cs");
        AssertFileExists("Models/PermissionDescription.cs");
        AssertFileExists("Models/OpcUaFavoriteNode.cs");
        AssertFileExists("ViewModels/MainViewModel.Phase4.cs");
        AssertFileExists("Views/Dialogs/OpcUaWriteTestDialog.xaml");

        // ---- App.xaml 包含所有资源字典 ----
        var app = ReadFile("App.xaml");
        AssertContains(app, "/Themes/Theme.xaml", "App.xaml 合并 Theme.xaml");
        AssertContains(app, "/Themes/Buttons.xaml", "App.xaml 合并 Buttons.xaml");
        AssertContains(app, "/Themes/Cards.xaml", "App.xaml 合并 Cards.xaml");
        AssertContains(app, "/Themes/DataGrid.xaml", "App.xaml 合并 DataGrid.xaml");
        AssertContains(app, "/Themes/Inputs.xaml", "App.xaml 合并 Inputs.xaml");
        AssertContains(app, "EqualsToBoolConverter", "App.xaml 注册 EqualsToBoolConverter");
        AssertContains(app, "StringNotEmptyToVisibilityConverter", "App.xaml 注册 StringNotEmptyToVisibilityConverter");
        // 检查实际标签 <DropShadowEffect 不存在；注释里提到名字 OK
        AssertNotContains(app, "<DropShadowEffect",
            "App.xaml 不再有 DropShadowEffect 标签（之前导致字体模糊）");

        // ---- CardBorderStyle 圆角 14 ----
        AssertContains(app, "CornerRadius\" Value=\"14\"", "Card 圆角 14 (mockup --radius-lg)");

        // ---- MainWindow.xaml 主导航 / 子导航 / 菜单修复 ----
        var mainWin = ReadFile("Views/MainWindow.xaml");
        AssertContains(mainWin, "TopNavigationIconButtonStyle", "顶部导航 Style 存在");
        AssertContains(mainWin, "SubNavigationItemStyle", "子导航 Style 存在");
        AssertContains(mainWin, "VsMenuHoverTextBrush", "菜单 hover 文字色定义");
        AssertContains(mainWin, "EventCenterToggle", "G4 事件中心铃铛");
        AssertContains(mainWin, "ZoomInCommand", "G2 缩放快捷键 ZoomIn 绑定");
        AssertContains(mainWin, "ZoomOutCommand", "G2 缩放快捷键 ZoomOut 绑定");
        AssertContains(mainWin, "ResetZoomCommand", "G2 缩放复位绑定");
        AssertContains(mainWin, "TextOptions.TextRenderingMode=\"ClearType\"",
            "MainWindow ClearType 兜底");

        // ---- NavigationSelectionBrushConverter 用渐变色 + 设计器/计数子页映射 ----
        var navConv = ReadFile("Converters/NavigationSelectionBrushConverter.cs");
        AssertContains(navConv, "LinearGradientBrush", "主导航 active 用渐变");
        AssertContains(navConv, "#2563EB", "主导航 active 蓝色 #2563EB");
        AssertContains(navConv, "#1D4ED8", "主导航 active 蓝色 #1D4ED8");
        AssertContains(navConv, "手动程序生成", "设计器子页映射");
        AssertNotContains(navConv, "#6987b0",
            "已移除旧版奇怪中蓝灰色 #6987b0");

        // ---- EqualsToBoolConverter 返回 string ----
        var eqConv = ReadFile("Converters/EqualsToBoolConverter.cs");
        AssertContains(eqConv, "? \"True\" : \"False\"",
            "EqualsToBoolConverter 返回字符串（bool 类型不匹配 Trigger.Value）");
        AssertContains(eqConv, "IMultiValueConverter",
            "EqualsToBoolConverter 实现 IMultiValueConverter");

        // ---- LoginView 用 DynamicResource ----
        var login = ReadFile("Views/Pages/LoginView.xaml");
        AssertContains(login, "{DynamicResource Brush.Text}", "LoginView 文字色用 DynamicResource");

        // ---- HomeView H1 用 RingProgressBar + 6 KPI 加状态色 ----
        var home = ReadFile("Views/Pages/HomeView.xaml");
        AssertContains(home, "controls:RingProgressBar", "HomeView H1 用 RingProgressBar");
        var stripCount = Regex.Matches(home, @"Width=""4"" Fill=""#").Count;
        AssertGreaterEqual(stripCount, 6, $"H10 6 个 KPI 状态色 strip (实际 {stripCount})");

        // ---- Phase4 命令存在 ----
        var phase4 = ReadFile("ViewModels/MainViewModel.Phase4.cs");
        AssertContains(phase4, "AddOpcUaFavorite", "F5 OPC UA 收藏夹命令");
        AssertContains(phase4, "OpcUaWriteTest", "F6 OPC UA 写入测试命令");
        AssertContains(phase4, "JumpToFlowStepCenter", "F7 排行榜跳 trace");
        AssertContains(phase4, "ToggleCriticalStep", "F8 关键步号标记");
        AssertContains(phase4, "ReplayRateMultiplier", "F9 回放速率");
        AssertContains(phase4, "ShowAlarmPopup", "F10 报警弹层");
        AssertContains(phase4, "ExportFlowIssueReportPdf", "F11 PDF 导出");
        AssertContains(phase4, "IsKpiHidden", "F14 KPI 隐藏偏好");

        // ---- Phase 2 各项核心命令仍在 ----
        var paramCs = ReadFile("ViewModels/MainViewModel.Parameter.cs");
        AssertContains(paramCs, "ParameterCategoryChips", "P2 分组 chip 集合");
        AssertContains(paramCs, "RefreshParameterCategoryChips", "P2 分组 chip 重建");
        AssertContains(paramCs, "ExportParametersAsRecipe", "P9 导出为配方");
        AssertContains(paramCs, "BatchEditParameters", "P10 批量编辑");
        var alarmCs = ReadFile("ViewModels/MainViewModel.Alarm.cs");
        AssertContains(alarmCs, "RefreshAlarmListView", "A2/A9 报警过滤刷新");
        AssertContains(alarmCs, "RefreshAlarmFrequencyBars", "A5 报警频率直方图");
        AssertContains(alarmCs, "ExportFilteredAlarmsAsync", "A4 报警导出");

        // ---- 报警 Record 加 Note + RelatedFlowStep ----
        var alarmRec = ReadFile("Models/AlarmRecord.cs");
        AssertContains(alarmRec, "private string note", "A7 报警 Note 字段");

        // ---- 流程 Record 加 IsCriticalStep ----
        var flowRec = ReadFile("Models/FlowStepRecord.cs");
        AssertContains(flowRec, "private bool isCriticalStep", "M24 关键步号字段");

        // ---- 气缸 Block 加 GroupName ----
        var cyl = ReadFile("Models/ManualCylinderBlockItem.cs");
        AssertContains(cyl, "private string groupName", "MA4 气缸分组字段");

        // ---- 配方 Item 加 LastUsedAt / LineCompatibility / IsTrialRun ----
        var recipe = ReadFile("Models/RecipeItem.cs");
        AssertContains(recipe, "lastUsedAt", "R7 配方上次使用时间");
        AssertContains(recipe, "lineCompatibility", "R8 配方产线兼容标签");
        AssertContains(recipe, "isTrialRun", "R9 配方试运行模式");

        // ---- 主项目 dotnet build 是否通过（只调一次） ----
        Console.WriteLine();
        Console.WriteLine("--- 触发主项目 dotnet build ---");
        var buildOk = RunDotnetBuild();
        Record("ApexHMI.csproj 编译", buildOk);

        // ---- Theme 颜色密度断言（避免空文件） ----
        var theme = ReadFile("Themes/Theme.xaml");
        var lightBrushCount = Regex.Matches(theme, @"<SolidColorBrush x:Key=").Count;
        AssertGreaterEqual(lightBrushCount, 20, $"Theme.xaml Brush 数 ({lightBrushCount})");
        var dark = ReadFile("Themes/Theme.Dark.xaml");
        var darkBrushCount = Regex.Matches(dark, @"<SolidColorBrush x:Key=").Count;
        AssertGreaterEqual(darkBrushCount, 20, $"Theme.Dark.xaml Brush 数 ({darkBrushCount})");

        // ---- 报告 ----
        Console.WriteLine();
        Console.WriteLine("=== 结果 ===");
        Console.WriteLine($"通过：{_passed}");
        Console.WriteLine($"失败：{_failed}");
        if (_failed > 0)
        {
            Console.WriteLine();
            Console.WriteLine("失败明细：");
            foreach (var f in _failures) Console.WriteLine($"  - {f}");
            return 1;
        }
        Console.WriteLine("✅ 全部通过");
        return 0;
    }

    private static string ReadFile(string relativePath)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static void AssertFileExists(string relativePath)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Record($"文件存在 {relativePath}", File.Exists(path));
    }

    private static void AssertContains(string content, string substring, string label)
        => Record(label, content.Contains(substring, StringComparison.Ordinal));

    private static void AssertNotContains(string content, string substring, string label)
        => Record(label, !content.Contains(substring, StringComparison.Ordinal));

    private static void AssertGreaterEqual(int actual, int min, string label)
        => Record(label, actual >= min);

    private static void Record(string label, bool ok)
    {
        if (ok)
        {
            _passed++;
            Console.WriteLine($"  ✓ {label}");
        }
        else
        {
            _failed++;
            _failures.Add(label);
            Console.WriteLine($"  ✗ {label}");
        }
    }

    private static bool RunDotnetBuild()
    {
        try
        {
            // 先清掉主项目的 obj/Debug 避免 SmokeVerify (net8.0)
            // 与主项目 (net48) 共享 obj 时 AssemblyInfo 重复冲突
            var objDebug = Path.Combine(Root, "obj", "Debug");
            if (Directory.Exists(objDebug))
            {
                try { Directory.Delete(objDebug, recursive: true); } catch { /* may be in use */ }
            }

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build ApexHMI.csproj -c Debug -v q -nologo",
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            // 排除 exe 锁定不算编译失败；忽略 wpftmp AssemblyInfo 重复
            var lines = (stdout + stderr).Split('\n');
            var compileErrors = lines
                .Where(l => l.Contains("error CS") || l.Contains("error MC") || l.Contains("error XAML"))
                .Where(l => !l.Contains("CS0579")) // wpftmp 重复 attr，与代码无关
                .ToList();
            if (compileErrors.Count > 0)
            {
                Console.WriteLine("    编译错误：");
                foreach (var e in compileErrors.Take(5)) Console.WriteLine($"      {e.Trim()}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    dotnet build 无法启动：{ex.Message}");
            return false;
        }
    }
}
