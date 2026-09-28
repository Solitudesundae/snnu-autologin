using System.Runtime.CompilerServices;
using System.Text;
using SnnuAutoLogin.Logging;

namespace SnnuAutoLogin.Tests;

/// <summary>测试宿主初始化：注册 GB2312 编码提供程序，并把静态日志重定向到临时目录，避免污染真实 %APPDATA%。</summary>
internal static class TestInit
{
    [ModuleInitializer]
    internal static void Init()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Log.SetDirectory(Path.Combine(Path.GetTempPath(), "snnu_test_logs_" + Guid.NewGuid().ToString("N")));
    }
}

/// <summary>门户 GB2312 编码工具。</summary>
internal static class Gb
{
    public static readonly Encoding Encoding = Encoding.GetEncoding("GB2312");

    public static byte[] Bytes(string s) => Encoding.GetBytes(s);
}
