using WinActive.Core;

namespace WinActive.Tests;

[TestClass]
public sealed class ActivationTests
{
    private static readonly IProgress<string> Silent = new Progress<string>();
    private const string Key = "ABCDE-12345-FGHIJ-67890-KLMNO";

    [TestMethod]
    public void ActivateWithKey_DoesNotCountAnotherKeyAsSuccess()
    {
        var windows = new FakeWindows { Status = LicenseStatus.Licensed };
        var service = new ActivationService(windows);
        var result = service.ActivateWithKey(Key, Silent);
        Assert.AreEqual(ActivationOutcome.Unconfirmed, result.Outcome);
        Assert.AreEqual(LicenseStatus.Licensed, result.State.Status);
    }

    [TestMethod]
    public void ActivateWithKey_ReportsPartialChangeIfActivationFails()
    {
        var windows = new FakeWindows { FailActivation = true };
        var service = new ActivationService(windows);
        var error = Assert.ThrowsExactly<ActivationFailure>(() => service.ActivateWithKey(Key, Silent));
        StringAssert.Contains(error.Message, "密钥已安装");
        Assert.AreEqual(Key, windows.InstalledKey);
    }

    private sealed class FakeWindows : IWindowsLicensing
    {
        public string? InstalledKey { get; private set; }
        public bool FailActivation { get; init; }
        public LicenseStatus Status { get; init; } = LicenseStatus.Unlicensed;

        public LicenseState ReadState(string? keySuffix = null) => new(
            "Windows 11", "Professional", 26100,
            keySuffix is null ? Status : LicenseStatus.Unlicensed, null, 0);

        public void InstallKey(string key) => InstalledKey = key;
        public void Activate(string keySuffix)
        {
            if (FailActivation)
                throw new InvalidOperationException("Windows activation server error");
        }
    }
}
