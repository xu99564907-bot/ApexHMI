#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ApexHMI.Models;
using Serilog;

namespace ApexHMI.Services;

/// <summary>
/// CODESYS / 汇川 InoProShop 导出的 SymbolConfiguration XML（Device.Application.xml）解析器。
/// XML schema: http://www.3s-software.com/schemas/Symbolconfiguration.xsd
///
/// 解析链路（确定性、零候选探测）：
/// 1. 读 &lt;Header&gt;&lt;ProjectInfo devicename appname/&gt; → 得到 OPC UA symbol 前缀
///    `|var|&lt;devicename&gt;.&lt;appname&gt;.`
/// 2. 读 &lt;TypeList&gt; 把每个 TypeSimple / TypeArray / TypeUserDef 索引到 dict
/// 3. 从 &lt;NodeList&gt;&lt;Node name="Application"&gt; 子节点开始递归
///    - 容器 Node（无 type 属性）→ 递归子节点
///    - 叶 Node（带 type）→ 按类型递归展开：
///        Simple  → 一个叶子
///        Array   → 对 [min..max] 每个 i，路径加 [i] 再展开 basetype
///        Struct  → 对每个成员，路径加 .Member 再展开 member type
/// 4. 每个叶子拼出完整 NodeId： ns=4;s=|var|&lt;dev&gt;.&lt;app&gt;.&lt;path&gt;
/// </summary>
public sealed class PlcVariableImportService
{
    public const string DefaultFileName = "Device.Application.xml";

    /// <summary>OPC UA 命名空间索引。CODESYS / InoProShop 默认 4，UaExpert 实测一致。</summary>
    public const ushort OpcUaNamespaceIndex = 4;

    /// <summary>
    /// OPC UA symbol 前缀里的"设备名"。汇川 InoProShop 运行时固定暴露为 "Inovance-PLC"，
    /// 即使 XML 里 &lt;ProjectInfo devicename/&gt; 是 "Device" 也忽略，一律用这个常量。
    /// </summary>
    public const string OpcUaDeviceName = "Inovance-PLC";

    /// <summary>递归深度上限（防御嵌套类型循环）。</summary>
    private const int MaxRecursionDepth = 16;

    /// <summary>单次导入叶子上限（防御 Space 类大数组爆炸）。</summary>
    private const int MaxLeafCount = 200_000;

    public string? ResolveDefaultPath(string? plcRoot)
    {
        if (string.IsNullOrWhiteSpace(plcRoot) || !Directory.Exists(plcRoot)) return null;

        var direct = Path.Combine(plcRoot!, DefaultFileName);
        if (File.Exists(direct)) return direct;

        try
        {
            return Directory.EnumerateFiles(plcRoot!, DefaultFileName, SearchOption.AllDirectories)
                            .FirstOrDefault();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PlcVariableImport: 搜索 {File} 失败 root={Root}", DefaultFileName, plcRoot);
            return null;
        }
    }

    public IReadOnlyList<TagItem> LoadFromFile(string xmlPath)
    {
        if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath))
            return Array.Empty<TagItem>();

        try
        {
            var doc = XDocument.Load(xmlPath);
            var root = doc.Root;
            if (root is null)
            {
                Log.Warning("PlcVariableImport: XML 根节点为空 path={Path}", xmlPath);
                return Array.Empty<TagItem>();
            }

            var ns = root.GetDefaultNamespace();

            // 1) 读项目信息。devicename 一律用固定常量 "Inovance-PLC"（OPC UA 运行时暴露名），
            //    XML 里的 <ProjectInfo devicename/> 仅记入日志做参考。
            var projInfo = root.Element(ns + "Header")?.Element(ns + "ProjectInfo");
            var xmlDeviceName = projInfo?.Attribute("devicename")?.Value ?? "(unknown)";
            var deviceName = OpcUaDeviceName;
            var appName    = projInfo?.Attribute("appname")?.Value ?? "Application";

            // 2) 索引类型表
            var types = ParseTypeList(root.Element(ns + "TypeList"), ns);

            // 3) 递归 NodeList
            var nodeList = root.Element(ns + "NodeList");
            if (nodeList is null)
            {
                Log.Warning("PlcVariableImport: 未找到 NodeList path={Path}", xmlPath);
                return Array.Empty<TagItem>();
            }

            var tags = new List<TagItem>();
            foreach (var appNode in nodeList.Elements(ns + "Node"))
            {
                // 根 Node name 通常 == appName ("Application")，已包含在 prefix 里 → 只走子节点
                var rootName = appNode.Attribute("name")?.Value ?? string.Empty;
                var startPath = string.Equals(rootName, appName, StringComparison.OrdinalIgnoreCase)
                                  ? string.Empty
                                  : rootName;

                foreach (var child in appNode.Elements(ns + "Node"))
                {
                    if (tags.Count >= MaxLeafCount) break;
                    WalkNode(child, startPath, types, deviceName, appName, tags, ns, depth: 0);
                }
            }

            Log.Information(
                "PlcVariableImport: 解析完成 path={Path} xmlDevice={XmlDevice} useDevice={UseDevice} app={App} types={Types} tags={Tags}",
                xmlPath, xmlDeviceName, deviceName, appName, types.Count, tags.Count);

            return tags;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PlcVariableImport: 解析 {Path} 失败", xmlPath);
            return Array.Empty<TagItem>();
        }
    }

    // ============================================================
    // 类型表索引
    // ============================================================

    private abstract record ParsedType(string Name, string IecName);
    private sealed record SimpleType(string Name, string IecName, string Class) : ParsedType(Name, IecName);
    private sealed record ArrayType(string Name, string IecName, string BaseType, int MinRange, int MaxRange) : ParsedType(Name, IecName);
    private sealed record StructType(string Name, string IecName, string PouClass,
                                     IReadOnlyList<(string MemberName, string MemberType)> Members)
        : ParsedType(Name, IecName);

    private static Dictionary<string, ParsedType> ParseTypeList(XElement? typeList, XNamespace ns)
    {
        var dict = new Dictionary<string, ParsedType>(StringComparer.OrdinalIgnoreCase);
        if (typeList is null) return dict;

        foreach (var el in typeList.Elements())
        {
            var name = el.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name)) continue;
            var iec = el.Attribute("iecname")?.Value ?? name!;

            switch (el.Name.LocalName)
            {
                case "TypeSimple":
                {
                    var cls = el.Attribute("typeclass")?.Value ?? "Unknown";
                    dict[name!] = new SimpleType(name!, iec, cls);
                    break;
                }
                case "TypeArray":
                {
                    var basetype = el.Attribute("basetype")?.Value ?? "";
                    var dim = el.Element(ns + "ArrayDim");
                    var min = TryParseInt(dim?.Attribute("minrange")?.Value, 0);
                    var max = TryParseInt(dim?.Attribute("maxrange")?.Value, -1);
                    dict[name!] = new ArrayType(name!, iec, basetype, min, max);
                    break;
                }
                case "TypeUserDef":
                {
                    var pou = el.Attribute("pouclass")?.Value ?? "STRUCTURE";
                    var members = new List<(string, string)>();
                    foreach (var m in el.Elements(ns + "UserDefElement"))
                    {
                        var mname = m.Attribute("iecname")?.Value;
                        var mtype = m.Attribute("type")?.Value;
                        if (!string.IsNullOrEmpty(mname) && !string.IsNullOrEmpty(mtype))
                            members.Add((mname!, mtype!));
                    }
                    dict[name!] = new StructType(name!, iec, pou, members);
                    break;
                }
            }
        }
        return dict;
    }

    private static int TryParseInt(string? s, int fallback) =>
        int.TryParse(s, out var v) ? v : fallback;

    // ============================================================
    // 节点遍历 + 类型展开
    // ============================================================

    private static void WalkNode(XElement node, string parentPath,
                                 Dictionary<string, ParsedType> types,
                                 string deviceName, string appName,
                                 List<TagItem> tags, XNamespace ns, int depth)
    {
        if (depth > MaxRecursionDepth || tags.Count >= MaxLeafCount) return;

        var name = node.Attribute("name")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(name)) return;

        var path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}.{name}";
        var typeAttr = node.Attribute("type")?.Value;
        var access  = node.Attribute("access")?.Value ?? string.Empty;
        var comment = node.Element(ns + "Comment")?.Value?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(typeAttr))
        {
            // 容器 Node：递归子节点
            foreach (var child in node.Elements(ns + "Node"))
            {
                if (tags.Count >= MaxLeafCount) return;
                WalkNode(child, path, types, deviceName, appName, tags, ns, depth + 1);
            }
            return;
        }

        ExpandType(path, typeAttr, types, deviceName, appName, access, comment, tags, depth);
    }

    private static void ExpandType(string path, string typeName,
                                   Dictionary<string, ParsedType> types,
                                   string deviceName, string appName,
                                   string access, string comment,
                                   List<TagItem> tags, int depth)
    {
        if (depth > MaxRecursionDepth || tags.Count >= MaxLeafCount) return;

        // 跳过明显非数据的成员（CODESYS 结构体常见的字节填充字段）
        if (path.EndsWith(".Space", StringComparison.Ordinal) ||
            System.Text.RegularExpressions.Regex.IsMatch(path, @"\.Space\[\d+\]$"))
            return;

        if (!types.TryGetValue(typeName, out var t))
        {
            // 未在 TypeList 中找到的（外部类型 / 未导出）→ 当未知叶子处理，仍可订阅试一下
            EmitLeaf(path, typeName, deviceName, appName, access, comment, tags);
            return;
        }

        switch (t)
        {
            case SimpleType simple:
                // POINTER 不可订阅
                if (string.Equals(simple.Class, "Pointer", StringComparison.OrdinalIgnoreCase)) return;
                EmitLeaf(path, simple.IecName, deviceName, appName, access, comment, tags);
                break;

            case ArrayType arr:
                if (string.IsNullOrEmpty(arr.BaseType) || arr.MaxRange < arr.MinRange) return;
                for (int i = arr.MinRange; i <= arr.MaxRange; i++)
                {
                    if (tags.Count >= MaxLeafCount) return;
                    ExpandType($"{path}[{i}]", arr.BaseType, types, deviceName, appName, access, comment, tags, depth + 1);
                }
                break;

            case StructType st:
                // FUNCTION_BLOCK 类型成员有些是内部状态，但 IO/Q 也是 BOOL 可读，按 STRUCTURE 处理
                foreach (var (mname, mtype) in st.Members)
                {
                    if (tags.Count >= MaxLeafCount) return;
                    // CODESYS 字节填充字段：跳过
                    if (string.Equals(mname, "Space", StringComparison.Ordinal)) continue;
                    ExpandType($"{path}.{mname}", mtype, types, deviceName, appName, access, comment, tags, depth + 1);
                }
                break;
        }
    }

    private static void EmitLeaf(string path, string iecType,
                                 string deviceName, string appName,
                                 string access, string comment,
                                 List<TagItem> tags)
    {
        var nodeId = $"ns={OpcUaNamespaceIndex};s=|var|{deviceName}.{appName}.{path}";
        var dataType = NormalizeIecType(iecType);
        var writable = access.IndexOf("Write", StringComparison.OrdinalIgnoreCase) >= 0;

        tags.Add(new TagItem
        {
            Name        = path,
            NodeId      = nodeId,
            DataType    = dataType,
            Category    = ResolveCategory(path),
            Group       = TopLevelGroup(path),
            Direction   = writable ? "Output" : "Input",
            CurrentValue = string.Empty,
            Description = comment,
            IsWritable  = writable,
            IsAlarm     = path.IndexOf("Fault", StringComparison.OrdinalIgnoreCase) >= 0
                       || path.IndexOf("Alarm", StringComparison.OrdinalIgnoreCase) >= 0,
        });
    }

    /// <summary>把 IEC iecname（BOOL / INT / REAL / STRING(20) / ARRAY... / Str_xxx）规范化为 .NET 友好的类型名。</summary>
    private static string NormalizeIecType(string iec)
    {
        if (string.IsNullOrWhiteSpace(iec)) return "STRING";
        return iec.Trim().ToUpperInvariant() switch
        {
            "BOOL"   => "Boolean",
            "BYTE"   => "Byte",
            "WORD"   => "UInt16",
            "DWORD"  => "UInt32",
            "INT"    => "Int16",
            "DINT"   => "Int32",
            "UINT"   => "UInt16",
            "UDINT"  => "UInt32",
            "REAL"   => "Single",
            "LREAL"  => "Double",
            "TIME"   => "TimeSpan",
            _        => iec,   // STRING / WSTRING / Str_xxx / 其他保留原样
        };
    }

    private static string ResolveCategory(string path)
    {
        if (path.IndexOf("Fault", StringComparison.OrdinalIgnoreCase) >= 0) return "Alarm";
        if (path.IndexOf("CylCtrl", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("VacCtrl", StringComparison.OrdinalIgnoreCase) >= 0) return "Cylinder";
        if (path.IndexOf("AxisCtrl", StringComparison.OrdinalIgnoreCase) >= 0) return "Axis";
        if (path.IndexOf("Sensor", StringComparison.OrdinalIgnoreCase) >= 0) return "IO";
        if (path.IndexOf("Recipe", StringComparison.OrdinalIgnoreCase) >= 0) return "Recipe";
        if (path.IndexOf("Communication", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("TcpClient", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("TcpSever", StringComparison.OrdinalIgnoreCase) >= 0) return "Communication";
        return "General";
    }

    private static string TopLevelGroup(string path)
    {
        var idx = path.IndexOf('.');
        return idx > 0 ? path.Substring(0, idx) : path;
    }
}
