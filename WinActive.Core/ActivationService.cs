namespace WinActive.Core;

public interface IWindowsLicensing
{
    LicenseState ReadState(string? keySuffix = null);
    void InstallKey(string key);
    void Activate(string keySuffix);
}

public enum ActivationOutcome { Licensed, Unconfirmed }

public sealed record ActivationResult(ActivationOutcome Outcome, LicenseState State);

public sealed class ActivationFailure(string message, Exception inner) : Exception(message, inner);

public sealed class ActivationService(IWindowsLicensing windows)
{
    private readonly SemaphoreSlim operation = new(1, 1);

    public ActivationResult ActivateWithKey(string input, IProgress<string> progress)
    {
        var key = ProductKey.Normalize(input) ??
                  throw new ArgumentException("请输入 25 位 Windows 产品密钥。", nameof(input));
        EnterOperation();
        try
        {
            return Apply(key, progress);
        }
        finally
        {
            operation.Release();
        }
    }

    private void EnterOperation()
    {
        if (!operation.Wait(0))
            throw new InvalidOperationException("已有授权操作正在进行。");
    }

    private ActivationResult Apply(string key, IProgress<string> progress)
    {
        progress.Report("安装产品密钥");
        windows.InstallKey(key);

        // Windows 未必能恢复原密钥，明确区分安装密钥与最终激活。
        try
        {
            progress.Report("请求 Windows 激活并读取实际状态");
            var suffix = key[^5..];
            windows.Activate(suffix);
            // 旧密钥的激活状态不能作为这次操作成功的依据。
            var applied = windows.ReadState(suffix);
            return applied.IsLicensed
                ? new(ActivationOutcome.Licensed, applied)
                : new(ActivationOutcome.Unconfirmed, windows.ReadState());
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new ActivationFailure($"密钥已安装，后续操作未能确认激活。{exception.Message}", exception);
        }
    }
}
