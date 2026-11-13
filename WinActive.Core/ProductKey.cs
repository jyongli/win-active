using System.Text.RegularExpressions;

namespace WinActive.Core;

public static partial class ProductKey
{
    [GeneratedRegex(@"\A(?:[A-Za-z0-9]{25}|[A-Za-z0-9]{5}(?:-[A-Za-z0-9]{5}){4})\z")]
    private static partial Regex Format();

    // 仅检查输入格式。密钥的有效性及适用版本由 Windows 授权服务判定。
    public static string? Normalize(string input)
    {
        var trimmed = input.Trim();
        if (!Format().IsMatch(trimmed))
            return null;

        var compact = trimmed.Replace("-", "").ToUpperInvariant();
        return string.Join("-", Enumerable.Range(0, 5).Select(group => compact.Substring(group * 5, 5)));
    }
}
