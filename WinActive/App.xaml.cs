using System.Windows;
using WinActive.Core;

namespace WinActive;

public partial class App : Application
{
    private Mutex? instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // 同一桌面会话中，每种权限只保留一个实例。
        var scope = WindowsLicensing.IsElevated ? "Elevated" : "Standard";
        instance = new Mutex(true, $@"Local\WinActive.Desktop.{scope}", out var first);
        if (!first)
        {
            Shutdown();
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        instance?.Dispose();
        base.OnExit(e);
    }
}
