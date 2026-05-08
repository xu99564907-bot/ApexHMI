using System;
using System.IO;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using ApexHMI.Models;
using Serilog;

namespace ApexHMI.Services;

/// <summary>
/// A1 / A10: 报警声音 + 推送（外部通知）。
/// - 声音：Alarm/Error 级别播 SystemSounds.Hand，Warning 播 SystemSounds.Exclamation
/// - 外部推送：把高级别报警追加到 config/alarm-notifications.log，后续 SMTP/IM 网关可监听该文件
///   （正式上线再接 SMTP 或飞书/钉钉 webhook，先保留接入点）
/// </summary>
public sealed class AlarmNotificationService
{
    public void PlaySoundForLevel(string level)
    {
        try
        {
            switch (level)
            {
                case "Alarm":
                case "Error":
                    SystemSounds.Hand.Play();
                    break;
                case "Warning":
                    SystemSounds.Exclamation.Play();
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AlarmNotificationService.PlaySoundForLevel 失败 level={Level}", level);
        }
    }

    public async Task PushAsync(string projectRoot, AlarmRecord alarm)
    {
        try
        {
            var dir = Path.Combine(projectRoot, "config");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "alarm-notifications.log");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{alarm.Level}\t{alarm.Source}\t{alarm.Message}\n";
            using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            var bytes = Encoding.UTF8.GetBytes(line);
            await fs.WriteAsync(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AlarmNotificationService.PushAsync 失败 source={Source}", alarm.Source);
        }
    }
}
