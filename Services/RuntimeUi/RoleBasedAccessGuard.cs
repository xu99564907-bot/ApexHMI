using System;
using System.Collections.Generic;
using System.Linq;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using Serilog;

namespace ApexHMI.Services.RuntimeUi;

/// <summary>基于角色的页面访问控制。</summary>
public sealed class RoleBasedAccessGuard
{
    /// <summary>检查指定角色是否有权访问该页面。RequiredRole 为 null 时所有人可访问。</summary>
    public static bool CanAccess(UserRole userRole, string? requiredRole)
    {
        if (string.IsNullOrWhiteSpace(requiredRole)) return true;

        if (!Enum.TryParse<UserRole>(requiredRole, ignoreCase: true, out var required))
        {
            Log.Warning("RoleGuard: 无法识别的 RequiredRole 值 role={RequiredRole}", requiredRole);
            return true; // 未知角色时放行，避免意外锁定页面
        }

        return userRole >= required;
    }

    /// <summary>过滤页面列表，移除用户无权访问的页面。</summary>
    public static IReadOnlyList<PageDefinition> FilterAccessible(UserRole userRole, IEnumerable<PageDefinition> pages)
    {
        return pages.Where(p => CanAccess(userRole, p.RequiredRole)).ToList();
    }

    /// <summary>检查指定角色能否跳转到目标页面。无权访问时返回 false 并给出原因。</summary>
    public static bool CanNavigateTo(UserRole userRole, ProjectDocument doc, string routeKey, out string? reason)
    {
        var page = doc.Pages.FirstOrDefault(p =>
            string.Equals(p.RouteKey, routeKey, StringComparison.OrdinalIgnoreCase));

        if (page is null)
        {
            reason = $"页面不存在：{routeKey}";
            return false;
        }

        if (!CanAccess(userRole, page.RequiredRole))
        {
            var requiredRoleLabel = string.IsNullOrWhiteSpace(page.RequiredRole) ? "无限制" : page.RequiredRole;
            reason = $"权限不足：页面 \"{page.Title}\" 需要 {requiredRoleLabel} 角色，当前为 {userRole}";
            Log.Warning("RoleGuard: 拦截页面访问 page={Title} required={Required} actual={Actual}",
                page.Title, page.RequiredRole, userRole);
            return false;
        }

        reason = null;
        return true;
    }
}
