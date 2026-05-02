using System;
using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace ApexHMI.Services.Security;

/// <summary>
/// 基于 Windows DPAPI (CurrentUser scope) 的字符串加解密。
/// 加密后输出 Base64 字符串，可直接写入 JSON 配置。
///
/// 注意：DPAPI CurrentUser 加密的密文只能在同一 Windows 用户账号下解密。
/// 跨用户 / 跨机器迁移配置时需要重新输入凭据。
/// </summary>
public static class SecretProtector
{
    private const string Prefix = "ENC:";

    /// <summary>加密明文，返回带前缀的 Base64 密文。空值原样返回。</summary>
    public static string? Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return plain;
        if (IsProtected(plain)) return plain; // 已加密则不重复加密

        var bytes = Encoding.UTF8.GetBytes(plain);
        var cipher = ProtectedData.Protect(bytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(cipher);
    }

    /// <summary>解密密文。若输入非加密格式（兼容旧明文配置），原样返回。</summary>
    public static string? Unprotect(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return cipher;
        if (!IsProtected(cipher)) return cipher;

        try
        {
            var bytes = Convert.FromBase64String(cipher!.Substring(Prefix.Length));
            var plain = ProtectedData.Unprotect(bytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DPAPI 解密失败，返回原始密文以便上层重新输入凭据。");
            // 解密失败（可能跨用户 / 数据损坏）：返回原文，由上层决定是否重新输入
            return cipher;
        }
    }

    public static bool IsProtected(string? value)
        => !string.IsNullOrEmpty(value) && value!.StartsWith(Prefix, StringComparison.Ordinal);
}
