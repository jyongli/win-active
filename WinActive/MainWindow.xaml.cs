using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation.Peers;
using WinActive.Core;

namespace WinActive;

public partial class MainWindow : Window
{
    private readonly WindowsLicensing windows = new();
    private readonly ActivationService activation;
    private readonly bool elevated = WindowsLicensing.IsElevated;
    private bool busy;
    private bool writing;

    public MainWindow()
    {
        InitializeComponent();
        activation = new ActivationService(windows);
        AccessText.Text = elevated ? "管理员权限" : "标准权限";
        ElevateButton.Visibility = elevated ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // 正在执行授权操作时，防止窗口关闭导致操作中断。
        e.Cancel = writing;
    }

    private async Task RefreshAsync()
    {
        if (busy)
            return;

        SetBusy(true);
        ShowFeedback("正在读取授权状态…");
        try
        {
            ShowState(await ReadStateAsync());
            ShowFeedback(elevated ? "授权状态已更新。" : "授权状态已更新。执行激活时需要管理员权限。");
        }
        catch (Exception exception)
        {
            ShowUnavailableState();
            ShowFeedback($"无法读取授权状态。{Describe(exception)}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Key_Click(object sender, RoutedEventArgs e)
    {
        if (!CanWrite())
            return;

        if (ProductKey.Normalize(KeyInput.Password) is not { } key)
        {
            ShowFeedback("请输入 25 位产品密钥，可保留分隔短横线。");
            KeyInput.Focus();
            return;
        }

        if (MessageBox.Show(this, "Windows 将安装输入的产品密钥并尝试联机激活。继续？",
                "安装产品密钥", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        KeyInput.Clear();
        await RunActivationAsync(key);
    }

    private bool CanWrite()
    {
        if (elevated)
            return true;

        ShowFeedback("请先从左侧以管理员身份打开，再执行激活。");
        return false;
    }

    private async Task RunActivationAsync(string key)
    {
        if (busy)
            return;

        SetBusy(true);
        writing = true;
        var progress = new Progress<string>(ShowFeedback);
        try
        {
            var result = await Task.Run(() => activation.ActivateWithKey(key, progress));
            ShowState(result.State);
            ShowFeedback(result.Outcome switch
            {
                ActivationOutcome.Licensed => "Windows 已确认激活。",
                _ => "密钥已安装，Windows 尚未确认激活。可刷新状态或打开系统激活设置。"
            });
        }
        catch (Exception exception)
        {
            // 失败也重读实际状态，可能已经完成了部分 Windows 写入。
            try
            {
                ShowState(await ReadStateAsync());
            }
            catch (Exception)
            {
                ShowUnavailableState();
            }
            ShowFeedback(Describe(exception));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private Task<LicenseState> ReadStateAsync() =>
        Task.Run(() => windows.ReadState()).WaitAsync(TimeSpan.FromSeconds(12));

    private void ShowState(LicenseState current)
    {
        StateText.Text = current.Status switch
        {
            LicenseStatus.Licensed => "已激活",
            LicenseStatus.Unlicensed => "未激活",
            LicenseStatus.InitialGrace => "初始宽限期",
            LicenseStatus.AdditionalGrace => "附加宽限期",
            LicenseStatus.NonGenuineGrace => "非正版宽限期",
            LicenseStatus.Notification => "需要激活",
            LicenseStatus.ExtendedGrace => "延长宽限期",
            _ => "状态未知"
        };
        var channel = current.Channel is { } name ? $" · {name}" : "";
        var reason = current.Reason != 0 && !current.IsLicensed ? $" · 0x{current.Reason:X8}" : "";
        EditionText.Text = $"{current.WindowsName} · {current.Edition}{channel}{reason}";
        SidebarBuild.Text = $"Build {current.Build} · {RuntimeInformation.OSArchitecture}";
        UIElementAutomationPeer.FromElement(StateText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void ShowUnavailableState()
    {
        StateText.Text = "状态待确认";
        EditionText.Text = "请刷新状态或打开 Windows 激活设置。";
        SidebarBuild.Text = "—";
    }

    private void SetBusy(bool value)
    {
        if (!value)
            writing = false;

        busy = value;
        ProgressBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !value;
        UpgradeButton.IsEnabled = !value;
        SettingsButton.IsEnabled = !value;
        ElevateButton.IsEnabled = !value;
        KeyInput.IsEnabled = !value;
        UpdateActions();
    }

    private void UpdateActions()
    {
        using var key = KeyInput.SecurePassword;
        KeyButton.IsEnabled = !busy && key.Length > 0;
    }

    private void KeyInput_Changed(object sender, RoutedEventArgs e) => UpdateActions();

    private void ShowFeedback(string message)
    {
        Feedback.Text = message;
        UIElementAutomationPeer.FromElement(Feedback)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void Elevate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Environment.ProcessPath ?? throw new InvalidOperationException("无法定位程序文件。");
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas" });
            Application.Current.Shutdown();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            ShowFeedback("已取消管理员权限请求。");
        }
        catch (Exception exception)
        {
            ShowFeedback($"无法以管理员身份打开。{Describe(exception)}");
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e) =>
        RunSystemUi(WindowsLicensing.OpenActivationSettings);

    private void Upgrade_Click(object sender, RoutedEventArgs e) =>
        RunSystemUi(WindowsLicensing.OpenEditionUpgrade);

    private void RunSystemUi(Action action)
    {
        try
        {
            action();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            ShowFeedback("已取消系统权限请求。");
        }
        catch (Exception exception)
        {
            ShowFeedback($"无法打开 Windows 授权界面。{Describe(exception)}");
        }
    }

    private static string Describe(Exception exception) => exception switch
    {
        TimeoutException => "等待 Windows 授权服务响应超时。",
        ActivationFailure or InvalidDataException or InvalidOperationException or
            UnauthorizedAccessException or PlatformNotSupportedException or FileNotFoundException => exception.Message,
        _ => $"Windows 返回错误（0x{exception.HResult:X8}）。"
    };
}
