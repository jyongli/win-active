using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace WinActive.Core;

public sealed class WindowsLicensing : IWindowsLicensing
{
    private const string ProductQuery = """
        SELECT * FROM SoftwareLicensingProduct
        WHERE ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND LicenseIsAddon=FALSE
        """;

    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    // 根据当前 EditionID 过滤 WMI 产品，避免将残留的其他版本或 Office 授权当成本机状态。
    public LicenseState ReadState(string? keySuffix = null)
    {
        var (name, edition, build) = ReadSystem();
        using var query = Search(ProductQuery + " AND PartialProductKey IS NOT NULL");
        using var products = query.Get();
        var status = LicenseStatus.Unlicensed;
        string? channel = null;
        uint reason = 0;

        foreach (ManagementObject product in products)
        {
            using (product)
            {
                if (!SameEdition(product, edition) ||
                    (keySuffix is not null &&
                     !string.Equals(product["PartialProductKey"] as string, keySuffix, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var currentStatus = (LicenseStatus)Convert.ToInt32(product["LicenseStatus"]);
                if (channel is null || currentStatus == LicenseStatus.Licensed)
                {
                    status = Enum.IsDefined(currentStatus) ? currentStatus : LicenseStatus.Unknown;
                    channel = product["ProductKeyChannel"] as string ?? "未知渠道";
                    reason = Convert.ToUInt32(product["LicenseStatusReason"]);
                }

                if (status == LicenseStatus.Licensed)
                    break;
            }
        }

        return new(name, edition, build, status, channel, reason);
    }

    public void InstallKey(string key)
    {
        EnsureElevated();
        using var service = First("SELECT * FROM SoftwareLicensingService");
        using var input = service.GetMethodParameters("InstallProductKey");
        input["ProductKey"] = key;
        Invoke(service, "InstallProductKey", "安装产品密钥", input);
    }

    public void Activate(string keySuffix)
    {
        EnsureElevated();
        using var service = First("SELECT * FROM SoftwareLicensingService");
        Invoke(service, "RefreshLicenseStatus", "刷新授权状态");
        var (_, edition, _) = ReadSystem();
        using var query = Search(ProductQuery + " AND PartialProductKey IS NOT NULL");
        using var products = query.Get();
        foreach (ManagementObject product in products)
        {
            using (product)
            {
                if (!SameEdition(product, edition) ||
                    !string.Equals(product["PartialProductKey"] as string, keySuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (Convert.ToInt32(product["LicenseStatus"]) != (int)LicenseStatus.Licensed)
                    Invoke(product, "Activate", "联机激活");

                Invoke(service, "RefreshLicenseStatus", "刷新授权状态");
                return;
            }
        }

        throw new InvalidOperationException("当前 Windows 版本未找到刚安装的密钥，版本变更请使用系统升级功能。");
    }

    public static void OpenActivationSettings()
    {
        using var process = Process.Start(new ProcessStartInfo("ms-settings:activation") { UseShellExecute = true });
    }

    // 将版本升级交给 Windows，自带界面负责提示兼容性、重启与完成状态。
    public static void OpenEditionUpgrade()
    {
        using var process = Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, "changepk.exe")) { UseShellExecute = true });
    }

    private static (string Name, string Edition, int Build) ReadSystem()
    {
        using var os = First("SELECT Caption, BuildNumber FROM Win32_OperatingSystem");
        var edition = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "EditionID", null) as string;
        if (string.IsNullOrWhiteSpace(edition))
            throw new InvalidOperationException("无法读取 Windows EditionID。");
        return ((string)os["Caption"], edition, Convert.ToInt32(os["BuildNumber"]));
    }

    private static bool SameEdition(ManagementObject product, string edition) =>
        string.Equals(product["LicenseFamily"] as string, edition, StringComparison.OrdinalIgnoreCase);

    private static ManagementObjectSearcher Search(string query) => new(new ManagementScope(@"root\cimv2"), new ObjectQuery(query),
        new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(8) });

    private static ManagementObject First(string query)
    {
        using var searcher = Search(query);
        using var results = searcher.Get();
        using var iterator = results.GetEnumerator();
        if (!iterator.MoveNext())
            throw new InvalidOperationException("Windows 未返回所需的 WMI 对象。");
        return (ManagementObject)iterator.Current;
    }

    private static void Invoke(ManagementObject target, string method, string action,
        ManagementBaseObject? input = null)
    {
        try
        {
            using var output = target.InvokeMethod(method, input, null);
            var result = Convert.ToUInt32(output["ReturnValue"]);
            if (result != 0)
                throw new InvalidOperationException($"{action}失败（0x{result:X8}）。");
        }
        catch (COMException exception)
        {
            throw new InvalidOperationException($"{action}失败（0x{exception.HResult:X8}）。", exception);
        }
        catch (ManagementException exception)
        {
            throw new InvalidOperationException($"{action}失败（WMI {exception.ErrorCode}）。", exception);
        }
    }

    private static void EnsureElevated()
    {
        if (!IsElevated)
            throw new UnauthorizedAccessException("此操作需要管理员权限。");
    }
}
