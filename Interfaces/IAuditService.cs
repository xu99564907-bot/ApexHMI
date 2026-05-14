using System.Threading.Tasks;

namespace ApexHMI.Interfaces;

/// <summary>
/// M3.2: 运行时操作审计抽象。替代 B3 中通过反射调 MainViewModel.AddAudit 的耦合写法。
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// 记录一条操作审计。
    /// </summary>
    /// <param name="user">操作用户（登录名/角色名）</param>
    /// <param name="action">动作描述（如 "写 PLC"、"按钮点击"）</param>
    /// <param name="target">目标对象（如 tagId、widget id）</param>
    /// <param name="success">是否成功</param>
    /// <param name="detail">详情（错误码 / 写入值 / 耗时）</param>
    Task LogOperationAsync(string user, string action, string target, bool success, string? detail = null);
}
