using NewLife.Omron.Protocols;
using Xunit;

namespace XUnitTest.Protocols;

/// <summary>FinsAddress解析测试</summary>
public class FinsAddressTests
{
    [Theory(DisplayName = "解析标准地址")]
    [InlineData("D100", 0x82, 100, 0, false)]
    [InlineData("DM200", 0x82, 200, 0, false)]
    [InlineData("CIO50", 0xB0, 50, 0, false)]
    [InlineData("W100", 0xB1, 100, 0, false)]
    [InlineData("WR100", 0xB1, 100, 0, false)]
    [InlineData("H10", 0xB2, 10, 0, false)]
    [InlineData("HR10", 0xB2, 10, 0, false)]
    [InlineData("A50", 0xB3, 50, 0, false)]
    [InlineData("AR50", 0xB3, 50, 0, false)]
    [InlineData("TIM5", 0x89, 5, 0, false)]
    [InlineData("CNT3", 0x89, 3, 0, false)]
    [InlineData("IR0", 0xDC, 0, 0, false)]
    [InlineData("DR0", 0xBC, 0, 0, false)]
    public void ParseStandardAddress(String address, Byte expectedType, UInt16 expectedAddr, Byte expectedBit, Boolean expectedIsBit)
    {
        var result = FinsAddress.Parse(address);

        Assert.Equal(expectedType, result.MemoryType);
        Assert.Equal(expectedAddr, result.Address);
        Assert.Equal(expectedBit, result.BitOffset);
        Assert.Equal(expectedIsBit, result.IsBit);
    }

    [Theory(DisplayName = "解析位地址")]
    [InlineData("CIO100.5", 0xB0, 100, 5, true)]
    [InlineData("D200.3", 0x82, 200, 3, true)]
    [InlineData("W50.0", 0xB1, 50, 0, true)]
    [InlineData("H10.15", 0xB2, 10, 15, true)]
    public void ParseBitAddress(String address, Byte expectedType, UInt16 expectedAddr, Byte expectedBit, Boolean expectedIsBit)
    {
        var result = FinsAddress.Parse(address);

        Assert.Equal(expectedType, result.MemoryType);
        Assert.Equal(expectedAddr, result.Address);
        Assert.Equal(expectedBit, result.BitOffset);
        Assert.Equal(expectedIsBit, result.IsBit);
    }

    [Theory(DisplayName = "解析EM Bank地址")]
    [InlineData("EM0:100", 0xA0, 100)]
    [InlineData("EM1:200", 0xA1, 200)]
    [InlineData("EM2:50", 0xA2, 50)]
    [InlineData("EM3:0", 0xA3, 0)]
    [InlineData("EM100", 0xA0, 100)]
    public void ParseEmBankAddress(String address, Byte expectedType, UInt16 expectedAddr)
    {
        var result = FinsAddress.Parse(address);

        Assert.Equal(expectedType, result.MemoryType);
        Assert.Equal(expectedAddr, result.Address);
    }

    [Fact(DisplayName = "空地址应抛出异常")]
    public void ParseEmptyAddressShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => FinsAddress.Parse(""));
        Assert.Throws<ArgumentException>(() => FinsAddress.Parse(null));
    }

    [Fact(DisplayName = "不支持的地址类型应抛出异常")]
    public void ParseUnsupportedAddressShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => FinsAddress.Parse("X100"));
    }

    [Theory(DisplayName = "ToBytes生成正确字节")]
    [InlineData("D100", new Byte[] { 0x82, 0x00, 0x64, 0x00 })]
    [InlineData("CIO50", new Byte[] { 0xB0, 0x00, 0x32, 0x00 })]
    public void ToBytesProducesCorrectBytes(String address, Byte[] expected)
    {
        var addr = FinsAddress.Parse(address);
        var bytes = addr.ToBytes();

        Assert.Equal(expected, bytes);
    }

    [Theory(DisplayName = "ToBitBytes生成正确位访问字节")]
    [InlineData("CIO100.5", new Byte[] { 0x30, 0x00, 0x64, 0x05 })]
    [InlineData("D200.3", new Byte[] { 0x02, 0x00, 0xC8, 0x03 })]
    public void ToBitBytesProducesCorrectBytes(String address, Byte[] expected)
    {
        var addr = FinsAddress.Parse(address);
        var bytes = addr.ToBitBytes();

        Assert.Equal(expected, bytes);
    }

    [Theory(DisplayName = "ToString返回可读字符串")]
    [InlineData("D100", "DM100")]
    [InlineData("CIO50", "CIO50")]
    [InlineData("CIO100.5", "CIO100.5")]
    public void ToStringReturnsReadableString(String address, String expected)
    {
        var addr = FinsAddress.Parse(address);
        Assert.Equal(expected, addr.ToString());
    }
}
