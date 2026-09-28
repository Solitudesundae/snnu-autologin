using System.Text;
using SnnuAutoLogin.Core;
using SnnuAutoLogin.Logging;
using SnnuAutoLogin.Security;
using SnnuAutoLogin.Ui;

namespace SnnuAutoLogin;

internal static class Program
{
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    private static void Main()
    {
        // GB2312 解码依赖 CodePages 编码提供程序（门户全部页面为 GB2312）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 全局异常兜底：托盘程序无控制台，异常必须落日志否则无从排查
        Application.ThreadException += (_, e) => Log.Error($"UI 线程异常: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error($"未处理异常: {e.ExceptionObject}");

        // 单实例：二次启动直接退出，避免双托盘/重复登录竞争
        _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\SnnuAutoLogin_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        var configStore = new ConfigStore();
        var tray = new TrayContext(configStore);

        // 开机自启场景要求登录后立刻完成首次检测（≤5 秒），故先启动托盘再无条件触发一轮检测
        Application.Run(tray);
        _singleInstanceMutex.ReleaseMutex();
    }
}
