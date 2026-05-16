#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApexHMI.Interfaces;
using ApexHMI.Models.RuntimeUi;
using Serilog;

namespace ApexHMI.Services;

/// <summary>
/// M5.1: Recipe 与 PLC 数据交换的 4-word 握手协议执行器。
/// <para>对标 WinCC <c>Job Mailbox</c>。避免 fire-and-forget 写半截被 PLC 误读。</para>
/// <para>写入流程：</para>
/// <list type="number">
///   <item>HMI 写 <c>ReqHmiTag = 2</c>（写入请求）</item>
///   <item>HMI 写所有字段</item>
///   <item>HMI 等 <c>DoneTag = 1</c>（轮询 latestRead 缓存，100ms 间隔）</item>
///   <item>HMI 读 <c>ErrorTag</c>：0=成功，其它=失败错误码</item>
///   <item>HMI 写 <c>ReqHmiTag = 0</c>（释放）</item>
/// </list>
/// <para>读出流程：</para>
/// <list type="number">
///   <item>HMI 写 <c>ReqHmiTag = 1</c>（读出请求）</item>
///   <item>HMI 等 <c>DoneTag = 1</c></item>
///   <item>调用方读取所有字段（已在 latestRead 缓存）</item>
///   <item>HMI 写 <c>ReqHmiTag = 0</c></item>
/// </list>
/// <para>所有 PLC 写都通过 <paramref name="writeInt"/> 异步回调，由调用方走真实
/// <c>IDataBindingService.WriteAsyncWithConfirm</c>（含审计）。</para>
/// </summary>
public sealed class RecipeJobCoordinator
{
    private readonly IAuditService _audit;

    public RecipeJobCoordinator(IAuditService audit)
    {
        _audit = audit;
    }

    /// <summary>写入流程总执行入口。</summary>
    /// <param name="recipe">配方（含 Mailbox 配置 + Fields）</param>
    /// <param name="dataset">要写入的数据集</param>
    /// <param name="writeInt">把整数值写到指定 Tag 的回调（返回 true=成功）</param>
    /// <param name="writeField">把字段值按 <see cref="RecipeFieldType"/> 写到 PLC 的回调（返回 true=成功）</param>
    /// <param name="readLatest">读取最新缓存值的回调（订阅过的 tag 才有值）</param>
    /// <param name="user">用户名，用于审计</param>
    /// <param name="ct">取消令牌（用于"取消"按钮）</param>
    public async Task<RecipeJobResult> WriteDatasetAsync(
        Recipe recipe,
        RecipeDataset dataset,
        Func<string, int, Task<bool>> writeInt,
        Func<RecipeField, string, Task<bool>> writeField,
        Func<string, string?> readLatest,
        string user,
        CancellationToken ct = default)
    {
        var m = recipe.Mailbox;
        var target = $"recipe={recipe.Name} dataset={dataset.Name}";
        await _audit.LogOperationAsync(user, "recipe-write-start", target, true,
            $"useMailbox=true timeout={m.TimeoutSeconds}s fields={recipe.Fields.Count}");

        try
        {
            // 1) ReqHmiTag = 2
            if (!await writeInt(m.ReqHmiTag, 2).ConfigureAwait(false))
            {
                return await FinishAsync(user, target, false, "无法写 ReqHmiTag=2（占位失败）", writeInt, m).ConfigureAwait(false);
            }

            // 2) 写所有字段
            int okFields = 0;
            foreach (var f in recipe.Fields)
            {
                if (ct.IsCancellationRequested)
                    return await FinishAsync(user, target, false, "用户取消", writeInt, m).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(f.TagAddress)) continue;
                if (!dataset.Values.TryGetValue(f.Key, out var val)) continue;
                if (await writeField(f, val).ConfigureAwait(false)) okFields++;
            }

            // 3) 等 Done = 1
            var doneOk = await WaitForDoneAsync(m, readLatest, ct).ConfigureAwait(false);
            if (!doneOk)
            {
                return await FinishAsync(user, target, false,
                    ct.IsCancellationRequested ? "用户取消" : $"PLC Done 超时（>{m.TimeoutSeconds}s）",
                    writeInt, m).ConfigureAwait(false);
            }

            // 4) 读 Error
            var errStr = readLatest(m.ErrorTag);
            int errCode = 0;
            int.TryParse(errStr, out errCode);
            if (errCode != 0)
            {
                return await FinishAsync(user, target, false, $"PLC 报错 ErrorCode={errCode}", writeInt, m).ConfigureAwait(false);
            }

            return await FinishAsync(user, target, true, $"okFields={okFields}", writeInt, m).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "RecipeJobCoordinator: WriteDatasetAsync 异常");
            return await FinishAsync(user, target, false, "异常：" + ex.Message, writeInt, m).ConfigureAwait(false);
        }
    }

    /// <summary>读出流程总执行入口。回调 <paramref name="onFieldRead"/> 在 Done 后被逐字段调用以把缓存值塞回 dataset。</summary>
    public async Task<RecipeJobResult> ReadDatasetAsync(
        Recipe recipe,
        RecipeDataset dataset,
        Func<string, int, Task<bool>> writeInt,
        Func<string, string?> readLatest,
        Action<RecipeField, string> onFieldRead,
        string user,
        CancellationToken ct = default)
    {
        var m = recipe.Mailbox;
        var target = $"recipe={recipe.Name} dataset={dataset.Name}";
        await _audit.LogOperationAsync(user, "recipe-read-start", target, true,
            $"useMailbox=true timeout={m.TimeoutSeconds}s fields={recipe.Fields.Count}");

        try
        {
            if (!await writeInt(m.ReqHmiTag, 1).ConfigureAwait(false))
                return await FinishAsync(user, target, false, "无法写 ReqHmiTag=1（占位失败）", writeInt, m).ConfigureAwait(false);

            var doneOk = await WaitForDoneAsync(m, readLatest, ct).ConfigureAwait(false);
            if (!doneOk)
                return await FinishAsync(user, target, false,
                    ct.IsCancellationRequested ? "用户取消" : $"PLC Done 超时（>{m.TimeoutSeconds}s）",
                    writeInt, m).ConfigureAwait(false);

            var errStr = readLatest(m.ErrorTag);
            int errCode = 0;
            int.TryParse(errStr, out errCode);
            if (errCode != 0)
                return await FinishAsync(user, target, false, $"PLC 报错 ErrorCode={errCode}", writeInt, m).ConfigureAwait(false);

            int n = 0;
            foreach (var f in recipe.Fields)
            {
                if (string.IsNullOrWhiteSpace(f.TagAddress)) continue;
                var v = readLatest(f.TagAddress);
                if (v is null) continue;
                onFieldRead(f, v);
                n++;
            }

            return await FinishAsync(user, target, true, $"readFields={n}", writeInt, m).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "RecipeJobCoordinator: ReadDatasetAsync 异常");
            return await FinishAsync(user, target, false, "异常：" + ex.Message, writeInt, m).ConfigureAwait(false);
        }
    }

    private static async Task<bool> WaitForDoneAsync(RecipeJobMailbox m, Func<string, string?> readLatest, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(m.DoneTag)) return false;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var timeoutMs = Math.Max(1, m.TimeoutSeconds) * 1000;
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (ct.IsCancellationRequested) return false;
            var v = readLatest(m.DoneTag);
            if (!string.IsNullOrEmpty(v) && (v == "1" || string.Equals(v, "True", StringComparison.OrdinalIgnoreCase)))
                return true;
            try { await Task.Delay(100, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return false; }
        }
        return false;
    }

    private async Task<RecipeJobResult> FinishAsync(string user, string target, bool success, string detail,
        Func<string, int, Task<bool>> writeInt, RecipeJobMailbox m)
    {
        // 释放：ReqHmiTag = 0（即便失败也要释放，让 PLC 复位）
        try { await writeInt(m.ReqHmiTag, 0).ConfigureAwait(false); } catch { /* 不抛 */ }
        var action = success ? "recipe-job-success" : "recipe-job-failed";
        try { await _audit.LogOperationAsync(user, action, target, success, detail).ConfigureAwait(false); }
        catch (Exception ex) { Log.Debug(ex, "审计写入失败"); }
        return new RecipeJobResult(success, detail);
    }
}

/// <summary>M5.1 Job Mailbox 执行结果。</summary>
public readonly record struct RecipeJobResult(bool Success, string Detail);
