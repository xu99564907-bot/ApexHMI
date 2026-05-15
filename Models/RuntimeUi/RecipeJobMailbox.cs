#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>
/// M5.1: WinCC 风格 Recipe 与 PLC 数据交换的 4-word 握手协议参数。
/// <para>挂在 <see cref="Recipe"/> 上。当 <see cref="Recipe.UseJobMailbox"/> = true 时，
/// "读出 PLC" / "写入 PLC" 会走 <c>RecipeJobCoordinator</c> 而非 fire-and-forget 直接写。</para>
/// <para>协议：</para>
/// <list type="bullet">
///   <item><c>ReqHmiTag</c>  (HMI → PLC) — 0 空闲 / 1 读出 / 2 写入</item>
///   <item><c>ReqPlcTag</c>  (PLC → HMI) — PLC 端发起请求 (0 / 1 / 2)</item>
///   <item><c>DoneTag</c>    (PLC → HMI) — 1 = 完成, 0 = 进行中</item>
///   <item><c>ErrorTag</c>   (PLC → HMI) — 错误码 (0 = 无错)</item>
/// </list>
/// </summary>
public partial class RecipeJobMailbox : ObservableObject
{
    [ObservableProperty] private string _reqHmiTag = "";
    [ObservableProperty] private string _reqPlcTag = "";
    [ObservableProperty] private string _doneTag = "";
    [ObservableProperty] private string _errorTag = "";

    /// <summary>等待 PLC Done = 1 的最大秒数；超时则失败并清 ReqHmiTag。</summary>
    [ObservableProperty] private int _timeoutSeconds = 10;
}
