using WinActive.Core;

namespace WinActive.Tests;

[TestClass]
public sealed class ProductKeyTests
{
    [TestMethod]
    [DataRow(" abcde-12345-fghij-67890-klmno ", "ABCDE-12345-FGHIJ-67890-KLMNO")]
    [DataRow("ABCDE12345FGHIJ67890KLMNO", "ABCDE-12345-FGHIJ-67890-KLMNO")]
    [DataRow("ABCDE-12345-FGHIJ-67890-KLMN", null)]
    [DataRow("ABCDE;12345;FGHIJ;67890;KLMNO", null)]
    public void Normalize_ValidatesBeforePassingInputToWindows(string input, string? expected)
    {
        Assert.AreEqual(expected, ProductKey.Normalize(input));
    }
}
