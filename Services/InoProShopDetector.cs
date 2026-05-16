#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace ApexHMI.Services;

/// <summary>
/// 检测目标机是否安装汇川 InoProShop / InoEdit 编程软件。
/// 生成 .st 程序前调一次，提示用户没装时无法编译/下载。
/// </summary>
public static class InoProShopDetector
{
    public sealed class DetectionResult
    {
        public bool Installed { get; init; }
        public string? InstallPath { get; init; }
        public string? Version { get; init; }
        public string? ProductName { get; init; }   // "InoProShop" / "InoEdit" / "AutoShop"
        public string Source { get; init; } = "";  // "registry" / "filesystem" / "uninstall"
    }

    /// <summary>主检测入口：依次试 Uninstall 注册表 → InstallPath 注册表 → 常见安装路径。</summary>
    public static DetectionResult Detect()
    {
        // 1. Windows Uninstall 注册表（最权威，每个装过的程序都会注册）
        var uninstall = ScanUninstallRegistry();
        if (uninstall.Installed) return uninstall;

        // 2. 厂商专用注册表
        var vendor = ScanVendorRegistry();
        if (vendor.Installed) return vendor;

        // 3. 常见安装路径
        var fs = ScanFilesystem();
        if (fs.Installed) return fs;

        return new DetectionResult { Installed = false };
    }

    private static DetectionResult ScanUninstallRegistry()
    {
        string[] uninstallRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        };

        foreach (var root in uninstallRoots)
        {
            try
            {
                using var rk = Registry.LocalMachine.OpenSubKey(root);
                if (rk is null) continue;
                foreach (var subName in rk.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = rk.OpenSubKey(subName);
                        if (sub is null) continue;
                        var display = sub.GetValue("DisplayName") as string;
                        if (string.IsNullOrEmpty(display)) continue;
                        if (display.IndexOf("InoProShop", StringComparison.OrdinalIgnoreCase) < 0 &&
                            display.IndexOf("InoEdit", StringComparison.OrdinalIgnoreCase) < 0 &&
                            display.IndexOf("AutoShop", StringComparison.OrdinalIgnoreCase) < 0 &&
                            display.IndexOf("Inovance", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        var path = sub.GetValue("InstallLocation") as string;
                        var ver = sub.GetValue("DisplayVersion") as string;
                        return new DetectionResult
                        {
                            Installed = true,
                            InstallPath = path,
                            Version = ver,
                            ProductName = display,
                            Source = $"uninstall:{root}\\{subName}",
                        };
                    }
                    catch { /* 跳过单条注册表读取失败 */ }
                }
            }
            catch { /* 跳过整个 root */ }
        }
        return new DetectionResult { Installed = false };
    }

    private static DetectionResult ScanVendorRegistry()
    {
        string[] vendorRoots =
        {
            @"SOFTWARE\Inovance\InoProShop",
            @"SOFTWARE\WOW6432Node\Inovance\InoProShop",
            @"SOFTWARE\Inovance\InoEdit",
            @"SOFTWARE\WOW6432Node\Inovance\InoEdit",
        };

        foreach (var root in vendorRoots)
        {
            try
            {
                using var rk = Registry.LocalMachine.OpenSubKey(root)
                                ?? Registry.CurrentUser.OpenSubKey(root);
                if (rk is null) continue;
                var path = (rk.GetValue("InstallPath") ?? rk.GetValue("Path")) as string;
                var ver = rk.GetValue("Version") as string;
                if (string.IsNullOrEmpty(path)) continue;
                var productName = root.Contains("InoEdit") ? "InoEdit" : "InoProShop";
                return new DetectionResult
                {
                    Installed = true,
                    InstallPath = path,
                    Version = ver,
                    ProductName = productName,
                    Source = $"vendor:{root}",
                };
            }
            catch { }
        }
        return new DetectionResult { Installed = false };
    }

    private static DetectionResult ScanFilesystem()
    {
        string[] roots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            @"C:\",
            @"D:\",
        };

        string[] subdirs =
        {
            @"Inovance\InoProShop",
            @"Inovance\InoEdit",
            "InoProShop",
            "InoEdit",
            "Inovance",
        };

        string[] exeHints = { "InoProShop.exe", "InoEdit.exe", "AutoShop.exe" };

        foreach (var r in roots.Where(x => !string.IsNullOrEmpty(x)))
        {
            foreach (var s in subdirs)
            {
                try
                {
                    var dir = Path.Combine(r, s);
                    if (!Directory.Exists(dir)) continue;
                    foreach (var hint in exeHints)
                    {
                        var exe = Directory.EnumerateFiles(dir, hint, SearchOption.AllDirectories).FirstOrDefault();
                        if (exe is null) continue;
                        var ver = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe).FileVersion;
                        return new DetectionResult
                        {
                            Installed = true,
                            InstallPath = Path.GetDirectoryName(exe),
                            Version = ver,
                            ProductName = Path.GetFileNameWithoutExtension(exe),
                            Source = $"filesystem:{exe}",
                        };
                    }
                }
                catch { }
            }
        }
        return new DetectionResult { Installed = false };
    }
}
