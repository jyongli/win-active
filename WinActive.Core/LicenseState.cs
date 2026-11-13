namespace WinActive.Core;

public enum LicenseStatus
{
    Unknown = -1,
    Unlicensed = 0,
    Licensed = 1,
    InitialGrace = 2,
    AdditionalGrace = 3,
    NonGenuineGrace = 4,
    Notification = 5,
    ExtendedGrace = 6
}

public sealed record LicenseState(
    string WindowsName, string Edition, int Build,
    LicenseStatus Status, string? Channel, uint Reason)
{
    public bool IsLicensed => Status == LicenseStatus.Licensed;
}
