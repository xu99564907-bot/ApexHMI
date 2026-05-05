using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApexHMI.Models.Sfc;

namespace ApexHMI.Services;

public static class SfcCodeGeneratorService
{
    public static readonly IReadOnlyList<string> DeviceTypes =
        new[] { "Cylinder", "Axis", "Motor", "Vacuum", "Wait", "Custom" };

    public static IReadOnlyList<string> GetActionOptions(string deviceType) => deviceType switch
    {
        "Cylinder" => new[] { "ToWork", "ToHome" },
        "Axis"     => new[] { "MoveToPoint", "Home", "Jog+", "Jog-" },
        "Motor"    => new[] { "Start", "Stop" },
        "Vacuum"   => new[] { "VacOn", "VacOff" },
        "Wait"     => new[] { "Timer", "Condition" },
        "Custom"   => new[] { "Custom" },
        _          => Array.Empty<string>()
    };

    public static string GetDeviceTypeLabel(string deviceType) => deviceType switch
    {
        "Cylinder" => "气缸",
        "Axis"     => "轴",
        "Motor"    => "电机",
        "Vacuum"   => "真空",
        "Wait"     => "等待",
        "Custom"   => "自定义",
        _          => deviceType
    };

    public static string GetActionLabel(string actionType) => actionType switch
    {
        "ToWork"      => "到工作位",
        "ToHome"      => "到原位",
        "Extend"      => "到工作位",
        "Retract"     => "到原位",
        "MoveToPoint" => "移动到点位",
        "Home"        => "回原点",
        "Jog+"        => "正向点动",
        "Jog-"        => "负向点动",
        "Start"       => "启动",
        "Stop"        => "停止",
        "VacOn"       => "吸附",
        "VacOff"      => "释放",
        "Timer"       => "延时",
        "Condition"   => "等待条件",
        "Custom"      => "自定义",
        _             => actionType
    };

    public static void AutoFill(SfcStep step, string driveDb)
    {
        step.CompletionCondition = BuildAutoConditionForStep(step, driveDb);
    }

    private static string BuildEnumIndex(string deviceType, int deviceIndex, string deviceName, string opNo)
    {
        if (string.IsNullOrWhiteSpace(opNo)) return deviceIndex.ToString();
        var op = opNo.Trim().ToUpperInvariant().StartsWith("OP")
            ? opNo.Trim().ToUpperInvariant()
            : $"OP{opNo.Trim().ToUpperInvariant()}";
        var (typeCode, keyPrefix) = deviceType switch
        {
            "Cylinder" => ("Cyl",   "CY"),
            "Axis"     => ("Axis",  "AXIS"),
            "Vacuum"   => ("Vac",   "VAC"),
            "Motor"    => ("Motor", "MOTOR"),
            _          => ("", "")
        };
        if (string.IsNullOrEmpty(typeCode)) return deviceIndex.ToString();
        var key = $"{keyPrefix}{deviceIndex:D2}";
        var memberName = string.IsNullOrWhiteSpace(deviceName) ? key : $"{deviceName.Trim()}{key}";
        return $"Enum_{op}_{typeCode}.{memberName}";
    }

    public static string BuildCommandCode(SfcStepAction action, string driveDb, int stepNo = 0, string opNo = "")
    {
        if (action.DeviceType == "Custom")
            return action.CustomCommand;

        var sb = new StringBuilder();
        var prefix = $"\t\t{driveDb}_DriveControl";

        switch (action.DeviceType)
        {
            case "Cylinder":
            {
                var idx = BuildEnumIndex("Cylinder", action.DeviceIndex, action.DeviceName, opNo);
                if (action.ActionType is "ToWork" or "Extend")
                    sb.AppendLine($"{prefix}.CylCtrl[{idx}].Cmd.AutoToWork := TRUE;");
                if (action.ActionType is "ToHome" or "Retract")
                    sb.AppendLine($"{prefix}.CylCtrl[{idx}].Cmd.AutoToHome := TRUE;");
                break;
            }
            case "Axis":
            {
                var idx = BuildEnumIndex("Axis", action.DeviceIndex, action.DeviceName, opNo);
                switch (action.ActionType)
                {
                    case "MoveToPoint":
                        sb.AppendLine($"{prefix}.AxisCtrl[{idx}].Cmd.AutoABS := {action.PointIndex};");
                        break;
                    case "Home":
                        sb.AppendLine($"{prefix}.AxisCtrl[{idx}].Cmd.Home := TRUE;");
                        break;
                    case "Jog+":
                        sb.AppendLine($"{prefix}.AxisCtrl[{idx}].Cmd.JogForward := TRUE;");
                        break;
                    case "Jog-":
                        sb.AppendLine($"{prefix}.AxisCtrl[{idx}].Cmd.JogBackward := TRUE;");
                        break;
                }
                break;
            }
            case "Motor":
            {
                var idx = BuildEnumIndex("Motor", action.DeviceIndex, action.DeviceName, opNo);
                sb.AppendLine(action.ActionType == "Start"
                    ? $"{prefix}.MotorCtrl[{idx}].Cmd.Start := TRUE;"
                    : $"{prefix}.MotorCtrl[{idx}].Cmd.Start := FALSE;");
                break;
            }
            case "Vacuum":
            {
                var idx = BuildEnumIndex("Vacuum", action.DeviceIndex, action.DeviceName, opNo);
                if (action.ActionType == "VacOn")
                    sb.AppendLine($"{prefix}.VacCtrl[{idx}].Cmd.AutoToWork := TRUE;");
                if (action.ActionType == "VacOff")
                    sb.AppendLine($"{prefix}.VacCtrl[{idx}].Cmd.AutoToHome := TRUE;");
                break;
            }
            case "Wait":
                if (action.ActionType == "Timer")
                    sb.AppendLine($"\t\tT_S{stepNo}(IN:=TRUE, PT:=T#{action.PointIndex}MS);");
                break;
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildCompletionCondition(SfcStepAction action, string driveDb, int stepNo = 0, string opNo = "")
    {
        if (action.DeviceType is "Custom" || (action.DeviceType == "Wait" && action.ActionType == "Condition"))
            return action.CustomCondition;

        var prefix = $"{driveDb}_DriveControl";

        var idx = BuildEnumIndex(action.DeviceType, action.DeviceIndex, action.DeviceName, opNo);

        return (action.DeviceType, action.ActionType) switch
        {
            ("Cylinder", "ToWork")      => $"{prefix}.CylCtrl[{idx}].Status.InWork",
            ("Cylinder", "ToHome")      => $"{prefix}.CylCtrl[{idx}].Status.InHome",
            ("Cylinder", "Extend")      => $"{prefix}.CylCtrl[{idx}].Status.InWork",
            ("Cylinder", "Retract")     => $"{prefix}.CylCtrl[{idx}].Status.InHome",
            ("Axis", "MoveToPoint")     => $"{prefix}.AxisCtrl[{idx}].Status.InPoint[{action.PointIndex}]",
            ("Axis", "Home")            => $"{prefix}.AxisCtrl[{idx}].Status.Intialed",
            ("Axis", "Jog+")            => "TRUE",
            ("Axis", "Jog-")            => "TRUE",
            ("Motor", "Start")          => $"{prefix}.MotorCtrl[{idx}].Status.Running",
            ("Motor", "Stop")           => $"NOT {prefix}.MotorCtrl[{idx}].Status.Running",
            ("Vacuum", "VacOn")         => $"{prefix}.VacCtrl[{idx}].Status.InWork",
            ("Vacuum", "VacOff")        => $"NOT {prefix}.VacCtrl[{idx}].Status.InHome",
            ("Wait", "Timer")           => $"T_S{stepNo}.Q",
            _                           => "TRUE"
        };
    }

    private static string BuildAutoConditionForStep(SfcStep step, string driveDb, string opNo = "")
    {
        if (step.Actions.Count == 0) return "TRUE";
        var conditions = step.Actions
            .Select(a => BuildCompletionCondition(a, driveDb, step.StepNo, opNo))
            .Where(c => c != "TRUE" && !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();
        return conditions.Count == 0 ? "TRUE" : string.Join(" AND ", conditions);
    }

    private static string Indent(int level)
    {
        return new string(' ', level * 4);
    }

    private static void AppendConditionalHeader(StringBuilder sb, string keyword, string condition, int indentLevel)
    {
        var indent = Indent(indentLevel);
        var continuationIndent = Indent(indentLevel-1);
        var normalizedCondition = Regex.Replace(
            condition.Trim(),
            @"\s+(AND|OR)\s+",
            match => Environment.NewLine + continuationIndent + match.Groups[1].Value + " ",
            RegexOptions.IgnoreCase);
        var lines = normalizedCondition.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        if (lines.Length == 1)
        {
            sb.AppendLine($"{indent}{keyword} {lines[0]} THEN");
            return;
        }

        sb.AppendLine($"{indent}{keyword} {lines[0]}");
        for (var i = 1; i < lines.Length - 1; i++)
        {
            sb.AppendLine($"{continuationIndent}{lines[i]}");
        }

        sb.AppendLine($"{continuationIndent}{lines[^1]} THEN");
    }

    public static string Generate(
        IEnumerable<SfcStep> steps,
        string driveDb,
        string controlDb,
        int stationNo,
        string programName,
        string opNo = "",
        string projectRoot = "",
        string faultDbBase = "")
    {
        var ordered = steps.OrderBy(s => s.StepNo).ToList();
        var firstStep = ordered.FirstOrDefault()?.StepNo ?? 10;

        // 预建各报警类型的变量名映射，确保与 MergeAlarmDut 一致
        var alarmVarMaps = new Dictionary<string, Dictionary<(int, int), string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Estop"] = BuildAlarmVarMap(ordered, "Estop"),
            ["Stop"]  = BuildAlarmVarMap(ordered, "Stop"),
            ["Run"]   = BuildAlarmVarMap(ordered, "Run"),
        };

        // 构建动态步骤块（模板里 {Steps} 所在位置位于 CASE 内，已缩进 2 级）
        var stepsSb = new StringBuilder();
        foreach (var step in ordered)
        {
            var description = step.Actions.Count == 0
                ? $"STEP {step.StepNo}"
                : string.Join("+", step.Actions.Select(a =>
                {
                    var dl = GetDeviceTypeLabel(a.DeviceType);
                    string al;
                    if (a.DeviceType == "Axis" && a.ActionType == "MoveToPoint"
                        && !string.IsNullOrWhiteSpace(a.SelectedAxisPoint?.Label))
                        al = $"移动到{a.SelectedAxisPoint.Label}";
                    else if (a.DeviceType == "Cylinder" && a.SelectedDeviceOption is { } co)
                        al = a.ActionType is "ToWork" or "Extend"
                            ? (string.IsNullOrWhiteSpace(co.WorkLabel) ? GetActionLabel(a.ActionType) : co.WorkLabel)
                            : a.ActionType is "ToHome" or "Retract"
                                ? (string.IsNullOrWhiteSpace(co.HomeLabel) ? GetActionLabel(a.ActionType) : co.HomeLabel)
                                : GetActionLabel(a.ActionType);
                    else
                        al = GetActionLabel(a.ActionType);
                    var name = string.IsNullOrWhiteSpace(a.DeviceName)
                        ? $"{dl}{a.DeviceIndex}"
                        : a.DeviceName;
                    return $"{name} {al}";
                }));

            stepsSb.AppendLine($"{Indent(2)}{step.StepNo}:");
            stepsSb.AppendLine($"{Indent(3)}Auto[{stationNo}].Comment:=\"{description}\";");

            foreach (var action in step.Actions)
            {
                var cmd = BuildCommandCode(action, driveDb, step.StepNo, opNo);
                if (!string.IsNullOrWhiteSpace(cmd))
                {
                    foreach (var line in cmd.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        stepsSb.AppendLine($"{Indent(3)}{line.Trim()}");
                }
            }

            // 报警条件赋值（需配置 faultDbBase，如 "DB1070"）
            if (!string.IsNullOrWhiteSpace(faultDbBase) && step.AlarmEntries.Count > 0)
            {
                var alarmsByType = step.AlarmEntries
                    .Where(a => !string.IsNullOrWhiteSpace(a.AlarmCondition))
                    .GroupBy(a => a.AlarmType);
                foreach (var group in alarmsByType)
                {
                    var typeMember = group.Key switch { "Estop" => "Estop", "Run" => "Run", _ => "Stop" };
                    var alarmList = group.ToList();
                    var typeMap = alarmVarMaps.TryGetValue(group.Key, out var m) ? m : null;
                    for (int i = 0; i < alarmList.Count; i++)
                    {
                        var varName = typeMap != null && typeMap.TryGetValue((step.StepNo, i + 1), out var n)
                            ? n
                            : $"ST{step.StepNo:D3}_F{(i + 1):D2}";
                        var cond = alarmList[i].AlarmCondition;
                        stepsSb.AppendLine($"{Indent(3)}IF {cond} THEN");
                        stepsSb.AppendLine($"{Indent(4)}{faultDbBase}_Fault.{typeMember}.{varName}:=TRUE;");
                        stepsSb.AppendLine($"{Indent(3)}END_IF");
                    }
                }
            }

            var defCond = string.IsNullOrWhiteSpace(step.CompletionCondition)
                ? BuildAutoConditionForStep(step, driveDb, opNo)
                : step.CompletionCondition;
            var defNext = string.IsNullOrWhiteSpace(step.NextStep) || step.NextStep == "END"
                ? "1000" : step.NextStep;

            if (step.Branches.Count > 0)
            {
                for (int i = 0; i < step.Branches.Count; i++)
                {
                    var branch = step.Branches[i];
                    var target = string.IsNullOrWhiteSpace(branch.TargetStep) || branch.TargetStep == "END"
                        ? "1000" : branch.TargetStep;
                    var cond = string.IsNullOrWhiteSpace(branch.Condition) ? "TRUE" : branch.Condition;
                    AppendConditionalHeader(stepsSb, i == 0 ? "IF" : "ELSIF", cond, 3);
                    stepsSb.AppendLine($"{Indent(4)}Auto[{stationNo}].Step:={target};");
                }
                AppendConditionalHeader(stepsSb, "ELSIF", defCond, 3);
                stepsSb.AppendLine($"{Indent(4)}Auto[{stationNo}].Step:={defNext};");
                stepsSb.AppendLine($"{Indent(3)}END_IF");
            }
            else
            {
                AppendConditionalHeader(stepsSb, "IF", defCond, 3);
                stepsSb.AppendLine($"{Indent(4)}Auto[{stationNo}].Step:={defNext};");
                stepsSb.AppendLine($"{Indent(3)}END_IF");
            }
        }

        // 读取模板（失败时降级为内嵌骨架）
        string template;
        try
        {
            var templatePath = Path.Combine(
                string.IsNullOrWhiteSpace(projectRoot) ? AppDomain.CurrentDomain.BaseDirectory : projectRoot,
                "Templates", "汇川中型PLC", "Auto.txt");
            template = File.ReadAllText(templatePath, Encoding.UTF8);
        }
        catch
        {
            // 降级：最简骨架
            template = "// {ProgramName}\r\n// 生成时间：{GeneratedAt}\r\n\r\n    CASE Auto[{StationNo}].Step OF\r\n{Steps}\r\n    END_CASE\r\n";
        }

        var result = template
            .Replace("{ProgramName}", programName)
            .Replace("{GeneratedAt}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            .Replace("{DriveDb}", driveDb)
            .Replace("{ControlDb}", controlDb)
            .Replace("{StationNo}", stationNo.ToString())
            .Replace("{FirstStep}", firstStep.ToString())
            .Replace("{Steps}", stepsSb.ToString().TrimEnd());

        return result;
    }

    // ========== 初始化程序代码生成 ==========

    /// <summary>
    /// 生成初始化程序（ACT_InitSTxx.st）。
    /// 与 Generate() 逻辑相同，区别：使用 Init[stationNo].Step/Comment 替代 Auto[]，
    /// 并加载 Init.txt 模板（含 {FaultDb} 占位符）。
    /// </summary>
    public static string GenerateInit(
        IEnumerable<SfcStep> steps,
        string driveDb,
        string controlDb,
        string faultDb,
        int stationNo,
        string programName,
        string opNo = "",
        string projectRoot = "")
    {
        var ordered   = steps.OrderBy(s => s.StepNo).ToList();
        var firstStep = ordered.FirstOrDefault()?.StepNo ?? 10;

        var stepsSb = new StringBuilder();
        foreach (var step in ordered)
        {
            var description = step.Actions.Count == 0
                ? $"STEP {step.StepNo}"
                : string.Join("+", step.Actions.Select(a =>
                {
                    var dl = GetDeviceTypeLabel(a.DeviceType);
                    string al;
                    if (a.DeviceType == "Axis" && a.ActionType == "MoveToPoint"
                        && !string.IsNullOrWhiteSpace(a.SelectedAxisPoint?.Label))
                        al = $"移动到{a.SelectedAxisPoint.Label}";
                    else if (a.DeviceType == "Cylinder" && a.SelectedDeviceOption is { } co)
                        al = a.ActionType is "ToWork" or "Extend"
                            ? (string.IsNullOrWhiteSpace(co.WorkLabel) ? GetActionLabel(a.ActionType) : co.WorkLabel)
                            : a.ActionType is "ToHome" or "Retract"
                                ? (string.IsNullOrWhiteSpace(co.HomeLabel) ? GetActionLabel(a.ActionType) : co.HomeLabel)
                                : GetActionLabel(a.ActionType);
                    else
                        al = GetActionLabel(a.ActionType);
                    var name = string.IsNullOrWhiteSpace(a.DeviceName)
                        ? $"{dl}{a.DeviceIndex}"
                        : a.DeviceName;
                    return $"{name} {al}";
                }));

            stepsSb.AppendLine($"{Indent(2)}{step.StepNo}:");
            stepsSb.AppendLine($"{Indent(3)}Init[{stationNo}].Comment:=\"{description}\";");

            foreach (var action in step.Actions)
            {
                var cmd = BuildCommandCode(action, driveDb, step.StepNo, opNo);
                if (!string.IsNullOrWhiteSpace(cmd))
                    foreach (var line in cmd.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        stepsSb.AppendLine($"{Indent(3)}{line.Trim()}");
            }

            var defCond = string.IsNullOrWhiteSpace(step.CompletionCondition)
                ? BuildAutoConditionForStep(step, driveDb, opNo)
                : step.CompletionCondition;
            var defNext = string.IsNullOrWhiteSpace(step.NextStep) || step.NextStep == "END"
                ? "1000" : step.NextStep;

            if (step.Branches.Count > 0)
            {
                for (int i = 0; i < step.Branches.Count; i++)
                {
                    var branch = step.Branches[i];
                    var target = string.IsNullOrWhiteSpace(branch.TargetStep) || branch.TargetStep == "END"
                        ? "1000" : branch.TargetStep;
                    var cond = string.IsNullOrWhiteSpace(branch.Condition) ? "TRUE" : branch.Condition;
                    AppendConditionalHeader(stepsSb, i == 0 ? "IF" : "ELSIF", cond, 3);
                    stepsSb.AppendLine($"{Indent(4)}Init[{stationNo}].Step:={target};");
                }
                AppendConditionalHeader(stepsSb, "ELSIF", defCond, 3);
                stepsSb.AppendLine($"{Indent(4)}Init[{stationNo}].Step:={defNext};");
                stepsSb.AppendLine($"{Indent(3)}END_IF");
            }
            else
            {
                AppendConditionalHeader(stepsSb, "IF", defCond, 3);
                stepsSb.AppendLine($"{Indent(4)}Init[{stationNo}].Step:={defNext};");
                stepsSb.AppendLine($"{Indent(3)}END_IF");
            }
        }

        // 读取模板
        string template;
        try
        {
            var templatePath = Path.Combine(
                string.IsNullOrWhiteSpace(projectRoot) ? AppDomain.CurrentDomain.BaseDirectory : projectRoot,
                "Templates", "汇川中型PLC", "Init.txt");
            template = File.ReadAllText(templatePath, Encoding.UTF8);
        }
        catch
        {
            template =
                "// {ProgramName}\r\n// 生成时间：{GeneratedAt}\r\n\r\n" +
                "IF Init[{StationNo}].Running THEN\r\n" +
                "    CASE Init[{StationNo}].Step OF\r\n" +
                "{Steps}\r\n" +
                "        1000:\r\n            Init[{StationNo}].Comment:=\"初始化完成\";\r\n" +
                "            Init[{StationNo}].Complete:=TRUE;\r\n" +
                "            Init[{StationNo}].Running:=FALSE;\r\n" +
                "            Init[{StationNo}].Step:=0;\r\n" +
                "    ELSE\r\n        ;\r\n    END_CASE\r\nEND_IF\r\n";
        }

        return template
            .Replace("{ProgramName}",  programName)
            .Replace("{GeneratedAt}",  DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            .Replace("{DriveDb}",      driveDb)
            .Replace("{ControlDb}",    controlDb)
            .Replace("{FaultDb}",      faultDb)
            .Replace("{StationNo}",    stationNo.ToString())
            .Replace("{FirstStep}",    firstStep.ToString())
            .Replace("{Steps}",        stepsSb.ToString().TrimEnd());
    }

    // ========== 报警结构体 / GVL 生成 ==========

    private static string NormalizeOpNo(string opNo)
    {
        if (string.IsNullOrWhiteSpace(opNo)) return "OP00";
        var s = opNo.Trim().ToUpperInvariant();
        return s.StartsWith("OP") ? s : $"OP{s}";
    }

    // ========== 中文工业术语配置文件 ==========
    private static readonly string AlarmTermsConfigPath =
        Path.Combine(AppContext.BaseDirectory, "config", "alarm_terms.json");

    private static readonly (string Cn, string En)[] _defaultAlarmTermMap =
    {
        ("紧急停止", "Estop"), ("急停", "Estop"),
        ("安全门", "SafeDoor"), ("光栅", "LightCurtain"), ("光幕", "LightCurtain"),
        ("未到位", "NotInPos"), ("到工作位", "AtWork"), ("在工作位", "AtWork"), ("工作位", "Work"),
        ("到原位", "AtHome"), ("在原位", "AtHome"), ("原位", "Home"), ("到位", "InPos"),
        ("回零", "GoHome"), ("回原", "GoHome"), ("零点", "ZeroPos"),
        ("正限位", "PosLimit"), ("负限位", "NegLimit"), ("限位", "Limit"),
        ("气缸", "Cyl"), ("伸出", "Extend"), ("缩回", "Retract"),
        ("电机", "Motor"), ("真空吸", "VacOn"), ("真空", "Vac"),
        ("传感器", "Sensor"), ("编码器", "Enc"),
        ("压力不足", "PressLow"), ("真空不足", "VacLow"), ("压力", "Press"),
        ("超时", "Timeout"), ("故障", "Fault"), ("异常", "Err"), ("错误", "Err"),
        ("未检测到", "NotDetect"), ("检测到", "Detected"), ("检测", "Detect"), ("检", "Chk"),
        ("上升", "Up"), ("下降", "Down"), ("运动", "Moving"),
        ("吸附", "Adsorb"), ("释放", "Release"),
        ("打开", "Open"), ("关闭", "Close"),
        ("连接", "Connect"), ("断开", "Disconnect"),
        ("锁螺丝", "Screw"), ("螺丝", "Screw"),
        ("上料", "Load"), ("下料", "Unload"),
        ("搬运", "Transfer"), ("取料", "Pick"), ("放料", "Place"),
        ("夹紧", "Clamp"), ("松开", "Unclamp"), ("夹", "Clamp"),
        ("启动", "Start"), ("停止", "Stop"),
        ("报警", "Alarm"), ("错位", "Misalign"),
        ("轴", "Axis"),
        ("没有", "No"), ("无", "No"), ("未", "Not"), ("不在", "NotAt"), ("不", "Not"),
        ("有", "Has"), ("在", "At"),
    };

    // 运行时对照表（可被外部替换）
    private static (string Cn, string En)[] _alarmTermMap = LoadAlarmTermsOrDefault();

    /// <summary>从 config/alarm_terms.json 加载对照表，不存在则用默认值并写出文件。</summary>
    public static (string Cn, string En)[] LoadAlarmTermsOrDefault()
    {
        try
        {
            if (File.Exists(AlarmTermsConfigPath))
            {
                var json = File.ReadAllText(AlarmTermsConfigPath, Encoding.UTF8);
                var records = JsonSerializer.Deserialize<AlarmTermRecord[]>(json);
                if (records is { Length: > 0 })
                    return records.Select(r => (r.Cn, r.En)).ToArray();
            }
        }
        catch { /* 解析失败降级为内置默认值 */ }

        // 首次运行：写出默认值
        SaveAlarmTerms(_defaultAlarmTermMap);
        return ((string Cn, string En)[])_defaultAlarmTermMap.Clone();
    }

    /// <summary>获取当前对照表副本（供 UI 编辑）。</summary>
    public static List<AlarmTermRecord> GetAlarmTerms() =>
        _alarmTermMap.Select(t => new AlarmTermRecord { Cn = t.Cn, En = t.En }).ToList();

    /// <summary>恢复内置默认值并写出配置文件。</summary>
    public static void ResetToDefaultTerms()
    {
        _alarmTermMap = ((string Cn, string En)[])_defaultAlarmTermMap.Clone();
        SaveAlarmTerms(_alarmTermMap);
    }

    /// <summary>从 UI 更新对照表并持久化到 JSON 文件。</summary>
    public static void SetAlarmTerms(IEnumerable<AlarmTermRecord> terms)
    {
        var list = terms.Where(t => !string.IsNullOrWhiteSpace(t.Cn)).ToList();
        _alarmTermMap = list.Select(t => (t.Cn.Trim(), t.En.Trim())).ToArray();
        SaveAlarmTerms(_alarmTermMap);
    }

    private static void SaveAlarmTerms((string Cn, string En)[] map)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AlarmTermsConfigPath)!);
            var records = map.Select(t => new AlarmTermRecord { Cn = t.Cn, En = t.En }).ToArray();
            var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            File.WriteAllText(AlarmTermsConfigPath, json, Encoding.UTF8);
        }
        catch { /* 写入失败不中断程序 */ }
    }

    /// <summary>供 JSON 序列化的对照表条目。</summary>
    public sealed class AlarmTermRecord
    {
        public string Cn { get; set; } = string.Empty;
        public string En { get; set; } = string.Empty;
    }

    /// <summary>
    /// 将报警消息翻译为合法的 IEC 61131-3 标识符。
    /// 先把已知中文术语替换为英文，再去除非法字符；若结果为空则回退为 ST{stepNo}_F{idx}。
    /// </summary>
    public static string BuildAlarmVarName(string? message, int stepNo, int alarmIdx, HashSet<string> usedNames)
    {
        var english = string.Empty;
        if (!string.IsNullOrWhiteSpace(message))
        {
            var text = message.Trim();
            // 先替换长词（已按长度从长到短排列）
            foreach (var (cn, en) in _alarmTermMap)
                text = text.Replace(cn, "_" + en + "_");
            // 去掉非标识符字符（保留字母、数字、下划线）
            text = Regex.Replace(text, @"[^\w]", "_");
            // 合并连续下划线
            text = Regex.Replace(text, @"_{2,}", "_");
            // 去掉首尾下划线
            text = text.Trim('_');
            // 首字符不能为数字
            if (text.Length > 0 && char.IsDigit(text[0]))
                text = "F" + text;
            // 限制长度
            if (text.Length > 28) text = text.Substring(0, 28).TrimEnd('_');
            english = text;
        }

        // 翻译结果为空或过短则回退为步骤+序号
        if (english.Length < 3)
            english = $"ST{stepNo:D3}_F{alarmIdx:D2}";

        // 唯一性保证
        var candidate = english;
        var suffix = 2;
        while (usedNames.Contains(candidate))
            candidate = $"{english}_{suffix++}";
        return candidate;
    }

    /// <summary>
    /// 为所有步骤、指定报警类型，预建 (stepNo, alarmIdx) → varName 映射表，
    /// 确保 Generate() 与 MergeAlarmDut() 使用完全一致的变量名。
    /// </summary>
    public static Dictionary<(int StepNo, int AlarmIdx), string> BuildAlarmVarMap(
        IEnumerable<SfcStep> steps, string alarmType)
    {
        var result = new Dictionary<(int, int), string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MAP", "Space" };
        foreach (var step in steps.OrderBy(s => s.StepNo))
        {
            var alarms = step.AlarmEntries
                .Where(a => a.AlarmType == alarmType && !string.IsNullOrWhiteSpace(a.AlarmCondition))
                .ToList();
            for (int i = 0; i < alarms.Count; i++)
            {
                var name = BuildAlarmVarName(alarms[i].AlarmMessage, step.StepNo, i + 1, used);
                used.Add(name);
                result[(step.StepNo, i + 1)] = name;
            }
        }
        return result;
    }

    /// <summary>
    /// 将新报警条目合并到已有的 DUT 文件内容中（追加模式）。
    /// - 从已有文件逐行提取现有变量名和注释行，保持顺序
    /// - 追加尚未存在的新变量，重新生成完整结构
    /// - existingContent 为 null 或空时，从零创建
    /// </summary>
    public static string MergeAlarmDut(string? existingContent, IEnumerable<SfcStep> steps,
        string opNo, string alarmType)
    {
        var normalized = NormalizeOpNo(opNo);
        var typeName   = $"Str_{normalized}_Fault{alarmType}";
        var stepList   = steps.OrderBy(s => s.StepNo).ToList();

        // 从已有文件提取变量行（保留顺序和注释）
        var existingVarLines = new List<string>();
        var existingVarNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MAP", "Space" };
        int spaceEndIdx = 63;

        if (!string.IsNullOrWhiteSpace(existingContent))
        {
            // 统一换行符，逐行扫描
            var flatLines = existingContent
                .Replace("\r\n", "\n").Replace("\r", "\n")
                .Split('\n');
            foreach (var raw in flatLines)
            {
                var trimmed = raw.Trim();
                // 跳过结构关键字、空行、MAP、Space
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (Regex.IsMatch(trimmed, @"^TYPE\s+",           RegexOptions.IgnoreCase)) continue;
                if (trimmed.Equals("STRUCT",     StringComparison.OrdinalIgnoreCase)) continue;
                if (trimmed.Equals("END_STRUCT", StringComparison.OrdinalIgnoreCase)) continue;
                if (trimmed.Equals("END_TYPE",   StringComparison.OrdinalIgnoreCase)) continue;
                if (Regex.IsMatch(trimmed, @"^MAP\s*:",           RegexOptions.IgnoreCase)) continue;
                if (Regex.IsMatch(trimmed, @"^Space\s*:\s*ARRAY", RegexOptions.IgnoreCase))
                {
                    var m = Regex.Match(trimmed, @"ARRAY\s*\[\s*\d+\s*\.\.\s*(\d+)", RegexOptions.IgnoreCase);
                    if (m.Success) int.TryParse(m.Groups[1].Value, out spaceEndIdx);
                    continue;
                }
                // 提取变量名
                var vm = Regex.Match(trimmed, @"^(\w+)\s*:", RegexOptions.IgnoreCase);
                if (!vm.Success) continue;
                var varName = vm.Groups[1].Value;
                if (existingVarNames.Add(varName))          // Add 返回 true 表示新增（去重）
                    existingVarLines.Add(raw.TrimEnd());    // 保留缩进，去除行尾空白
            }
        }

        // 构建新条目（排除已存在的变量名）
        var allUsed = new HashSet<string>(existingVarNames, StringComparer.OrdinalIgnoreCase);
        var newEntries = new List<(string VarName, string Message)>();

        foreach (var step in stepList)
        {
            var alarms = step.AlarmEntries
                .Where(a => a.AlarmType == alarmType && !string.IsNullOrWhiteSpace(a.AlarmCondition))
                .ToList();
            for (int i = 0; i < alarms.Count; i++)
            {
                var varName = BuildAlarmVarName(alarms[i].AlarmMessage, step.StepNo, i + 1, allUsed);
                if (existingVarNames.Add(varName))
                {
                    allUsed.Add(varName);
                    newEntries.Add((varName, alarms[i].AlarmMessage));
                }
            }
        }

        if (newEntries.Count == 0 && !string.IsNullOrWhiteSpace(existingContent))
            return existingContent; // 无新增，直接返回原文件

        // Space 起始 = 实际变量数（不含 MAP/Space 自身）
        var totalVars = existingVarLines.Count + newEntries.Count;

        // 重新生成完整文件，格式固定且干净
        var sb = new StringBuilder();
        sb.AppendLine($"TYPE {typeName} :");
        sb.AppendLine("STRUCT");
        sb.AppendLine("\tMAP:BOOL;//映射地址勿删");
        sb.AppendLine("\t");
        sb.AppendLine("\t");
        foreach (var line in existingVarLines)
            sb.AppendLine(line);
        foreach (var (varName, msg) in newEntries)
        {
            var comment = string.IsNullOrWhiteSpace(msg) ? string.Empty : $"//{msg}";
            sb.AppendLine($"\t{varName}:BOOL;{comment}");
        }
        sb.AppendLine($"\tSpace:ARRAY[{totalVars}..{spaceEndIdx}] OF BOOL;//预留报警位 ");
        sb.AppendLine();
        sb.AppendLine("END_STRUCT");
        sb.Append("END_TYPE");   // 不加结尾换行，避免出现多余字符

        return sb.ToString();
    }

    /// <summary>生成报警 GVL 文件内容（DBXX70_Fault.ST）—— 保留接口但不建议覆盖已有 GVL</summary>
    public static string GenerateAlarmGvl(IEnumerable<SfcStep> steps, string opNo, string faultDbBase)
    {
        var normalized = NormalizeOpNo(opNo);
        var stepList = steps.ToList();
        var hasEstop = stepList.Any(s => s.AlarmEntries.Any(a => a.AlarmType == "Estop" && !string.IsNullOrWhiteSpace(a.AlarmCondition)));
        var hasStop  = stepList.Any(s => s.AlarmEntries.Any(a => a.AlarmType == "Stop"  && !string.IsNullOrWhiteSpace(a.AlarmCondition)));
        var hasRun   = stepList.Any(s => s.AlarmEntries.Any(a => a.AlarmType == "Run"   && !string.IsNullOrWhiteSpace(a.AlarmCondition)));

        var sb = new StringBuilder();
        sb.AppendLine("VAR_GLOBAL");
        if (hasEstop) sb.AppendLine($"\t{faultDbBase}_FaultEstop : Str_{normalized}_FaultEstop;");
        if (hasStop)  sb.AppendLine($"\t{faultDbBase}_FaultStop : Str_{normalized}_FaultStop;");
        if (hasRun)   sb.AppendLine($"\t{faultDbBase}_FaultRun : Str_{normalized}_FaultRun;");
        sb.AppendLine("END_VAR");
        return sb.ToString();
    }

    /// <summary>返回当前步骤集合中实际用到的报警类型列表</summary>
    public static IReadOnlyList<string> GetUsedAlarmTypes(IEnumerable<SfcStep> steps)
    {
        var used = new HashSet<string>();
        foreach (var step in steps)
            foreach (var alarm in step.AlarmEntries)
                if (!string.IsNullOrWhiteSpace(alarm.AlarmCondition))
                    used.Add(alarm.AlarmType);
        return used.OrderBy(s => s).ToList();
    }
}
