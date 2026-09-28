using Microsoft.Win32;

namespace SnnuAutoLogin;

/// <summary>
/// 开机自启动：HKCU\Software\Microsoft\Windows\CurrentVersion\Run。
/// 选型理由（对比计划任务触发器）：
/// - HKCU Run 写注册表一步完成，无需管理员、无任务计划程序依赖，用户在任务管理器"启动"页可控可禁；
/// - 计划任务（LogonTrigger）适合需要最高权限或系统级会话的场景，本程序 asInvoker + 用户态足够；
/// - 单文件 exe 路径取 Environment.ProcessPath（单文件发布下 Assembly.Location 为空）。
/// </summary>
public static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SnnuAutoLogin";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string existing
            && IsCurrentExecutable(existing);
    }

    /// <summary>启用/禁用自启动。返回最终生效状态。</summary>
    public static bool SetEnabled(bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key == null)
        {
            return false;
        }
        if (enable)
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                return false;
            }
            key.SetValue(ValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        return IsEnabled();
    }

    private static bool IsCurrentExecutable(string registered)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            return false;
        }
        // 忽略两侧引号与大小写、斜杠方向差异
        return string.Equals(registered.Trim().Trim('"'), exe, StringComparison.OrdinalIgnoreCase)
            || string.Equals(registered.Trim().Trim('"').Replace('/', '\\'), exe.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
    }
}
