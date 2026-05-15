using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApexHMI.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApexHMI.Tests.Services;

/// <summary>M5.4: AuditServiceSqlite 单元测试。</summary>
public sealed class AuditServiceSqliteTests : IDisposable
{
    private readonly string _tempDir;

    public AuditServiceSqliteTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ApexHMI.AuditTests." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task LogOperationAsync_PersistsToDb()
    {
        var svc = new AuditServiceSqlite(_tempDir);
        await svc.LogOperationAsync("alice", "write-int", "DB1.Speed", true, "value=42");
        await svc.LogOperationAsync("bob",   "write-bool","DB1.Run",   false, "timeout");

        var from = DateTime.Now.AddMinutes(-1);
        var to   = DateTime.Now.AddMinutes(1);
        var rows = await svc.QueryAsync(from, to);

        rows.Should().HaveCount(2);
        rows.Select(r => r.User).Should().Contain(new[] { "alice", "bob" });
        rows.Should().Contain(r => r.Success && r.Action == "write-int" && r.Target == "DB1.Speed");
        rows.Should().Contain(r => !r.Success && r.Action == "write-bool");
    }

    [Fact]
    public async Task QueryAsync_FilterByUser()
    {
        var svc = new AuditServiceSqlite(_tempDir);
        await svc.LogOperationAsync("alice", "a", "T1", true);
        await svc.LogOperationAsync("bob",   "a", "T2", true);
        await svc.LogOperationAsync("alice", "b", "T3", true);

        var rows = await svc.QueryAsync(DateTime.Now.AddMinutes(-1), DateTime.Now.AddMinutes(1), user: "alice");
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.User == "alice");

        var rowsBob = await svc.QueryAsync(DateTime.Now.AddMinutes(-1), DateTime.Now.AddMinutes(1), user: "bob");
        rowsBob.Should().HaveCount(1);
        rowsBob.Single().Target.Should().Be("T2");
    }

    [Fact]
    public async Task QueryAsync_FilterByDateRange()
    {
        var svc = new AuditServiceSqlite(_tempDir);
        await svc.LogOperationAsync("alice", "x", "T", true);

        // 远未来：应该查不到刚才那条
        var future = await svc.QueryAsync(DateTime.Now.AddDays(10), DateTime.Now.AddDays(11));
        future.Should().BeEmpty();

        // 涵盖当前的窗口：应有
        var now = await svc.QueryAsync(DateTime.Now.AddSeconds(-30), DateTime.Now.AddSeconds(30));
        now.Should().HaveCount(1);
    }

    [Fact]
    public async Task QueryAsync_FilterByAction()
    {
        var svc = new AuditServiceSqlite(_tempDir);
        await svc.LogOperationAsync("u", "write-int",  "T1", true);
        await svc.LogOperationAsync("u", "write-bool", "T2", true);
        await svc.LogOperationAsync("u", "write-int",  "T3", true);

        var rows = await svc.QueryAsync(DateTime.Now.AddMinutes(-1), DateTime.Now.AddMinutes(1), action: "write-int");
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Action == "write-int");
    }

    [Fact]
    public async Task MemorySink_ReceivesEachRecord()
    {
        var captured = new System.Collections.Generic.List<(string action, string target, string result, string detail)>();
        var svc = new AuditServiceSqlite(_tempDir, (a, t, r, d) => captured.Add((a, t, r, d)));
        await svc.LogOperationAsync("u", "act-1", "T1", true,  "ok");
        await svc.LogOperationAsync("u", "act-2", "T2", false, "err");

        captured.Should().HaveCount(2);
        captured[0].action.Should().Be("act-1");
        captured[0].result.Should().Be("成功");
        captured[1].result.Should().Be("失败");
    }

    [Fact]
    public async Task Rolling_Removes_Records_Older_Than_90_Days()
    {
        var svc = new AuditServiceSqlite(_tempDir);
        // 先建库 + 写一条当前记录确保表结构与文件就绪
        await svc.LogOperationAsync("seed", "seed", "T", true);

        // 直接 SQL 插入一条 100 天前的记录（绕过服务）
        var dbPath = Path.Combine(_tempDir, "audit.db");
        var oldMs = DateTimeOffset.Now.AddDays(-100).ToUnixTimeMilliseconds();
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO audit_log (timestamp, user, action, target, success, detail)
                                VALUES ($ts, 'ghost', 'old', 'T', 1, 'historic')";
            cmd.Parameters.AddWithValue("$ts", oldMs);
            cmd.ExecuteNonQuery();
        }

        // 校验 100 天前那条确实在
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM audit_log WHERE user='ghost'";
            ((long)cmd.ExecuteScalar()!).Should().Be(1);
        }

        // 把 _lastCleanupTicks 置 0（反射）使 MaybeCleanup 真正跑
        var f = typeof(AuditServiceSqlite).GetField("_lastCleanupTicks",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        f!.SetValue(svc, 0L);

        // 再写一条 → 触发 MaybeCleanup
        await svc.LogOperationAsync("u", "trigger", "T", true);

        // 100 天前的应被清掉
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM audit_log WHERE user='ghost'";
            ((long)cmd.ExecuteScalar()!).Should().Be(0);
        }
    }
}
