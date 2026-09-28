namespace SnnuAutoLogin.Logging;

/// <summary>
/// 极简文件日志：按天一个文件，写入 %APPDATA%\SnnuAutoLogin\logs\，保留 7 天。
/// 硬约束：任何级别都不得记录密码；账号一律经 <see cref="Mask"/> 打码后才可入日志。
/// </summary>
public static class Log
{
    private static readonly object Lock = new();
    private static string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SnnuAutoLogin", "logs");

    /// <summary>仅供单元测试重定向日志目录。</summary>
    public static void SetDirectory(string dir) => _dir = dir;

    public static void Info(string message) => Write("INFO ", message);

    public static void Warn(string message) => Write("WARN ", message);

    public static void Error(string message) => Write("ERROR", message);

    /// <summary>条件编译：Release 构建下连调用点的字符串插值都不会执行。</summary>
    [System.Diagnostics.Conditional("DEBUG")]
    public static void Debug(string message) => Write("DEBUG", message);

    /// <summary>账号打码：保留前 3 后 2 位（如 425****09@unicom）。</summary>
    public static string Mask(string? account)
    {
        if (string.IsNullOrEmpty(account))
        {
            return "<空>";
        }
        // 账号形如 学号 或 学号@运营商，仅打码学号部分
        var at = account.IndexOf('@');
        var id = at > 0 ? account[..at] : account;
        var suffix = at > 0 ? account[at..] : string.Empty;
        if (id.Length <= 5)
        {
            return new string('*', id.Length) + suffix;
        }
        return string.Concat(id.AsSpan(0, 3), new string('*', id.Length - 5), id.AsSpan(id.Length - 2, 2), suffix);
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(_dir);
                var file = Path.Combine(_dir, $"app-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
                PurgeOldLogs();
            }
        }
        catch
        {
            // 日志失败不得影响主流程
        }
    }

    private static void PurgeOldLogs()
    {
        var cutoff = DateTime.Now.AddDays(-7);
        foreach (var file in Directory.EnumerateFiles(_dir, "app-*.log"))
        {
            if (File.GetLastWriteTime(file) < cutoff)
            {
                try { File.Delete(file); } catch { /* 忽略清理失败 */ }
            }
        }
    }
}
