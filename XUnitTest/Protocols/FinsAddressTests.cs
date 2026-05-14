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

    [Theory(DisplayName = "大字地址解析正确")]
    [InlineData("D9999", 0x82, 9999)]
    [InlineData("D19999", 0x82, 19999)]
    [InlineData("CIO9999", 0xB0, 9999)]
    [InlineData("W9999", 0xB1, 9999)]
    [InlineData("HR9999", 0xB2, 9999)]
    public void LargeWordAddress_ParsedCorrectly(String address, Byte expectedAreaCode, UInt16 expectedWord)
    {
        var addr = FinsAddress.Parse(address);
        Assert.Equal(expectedAreaCode, addr.MemoryType);
        Assert.Equal(expectedWord, addr.Address);
    }

    [Theory(DisplayName = "位偏移 0~15 均可解析")]
    [InlineData("CIO100.0", 0)]
    [InlineData("CIO100.7", 7)]
    [InlineData("CIO100.15", 15)]
    [InlineData("D50.0", 0)]
    [InlineData("D50.15", 15)]
    public void BitOffset_AllPositions_Parsed(String address, Byte expectedBit)
    {
        var addr = FinsAddress.Parse(address);
        Assert.Equal(expectedBit, addr.BitOffset);
        Assert.True(addr.IsBit);
    }

    [Theory(DisplayName = "EM Bank 地址解析正确")]
    [InlineData("EM0:0", 0xA0, 0)]
    [InlineData("EM0:9999", 0xA0, 9999)]
    [InlineData("EM1:100", 0xA1, 100)]
    [InlineData("EM3:500", 0xA3, 500)]
    public void EmBankAddress_ParsedCorrectly(String address, Byte expectedAreaCode, UInt16 expectedWord)
    {
        var addr = FinsAddress.Parse(address);
        Assert.Equal(expectedAreaCode, addr.MemoryType);
        Assert.Equal(expectedWord, addr.Address);
    }

    [Theory(DisplayName = "地址字 0 解析正确（起始地址）")]
    [InlineData("D0", 0x82, 0)]
    [InlineData("CIO0", 0xB0, 0)]
    [InlineData("W0", 0xB1, 0)]
    [InlineData("HR0", 0xB2, 0)]
    [InlineData("AR0", 0xB3, 0)]
    public void ZeroWordAddress_ParsedCorrectly(String address, Byte expectedAreaCode, UInt16 expectedWord)
    {
        var addr = FinsAddress.Parse(address);
        Assert.Equal(expectedAreaCode, addr.MemoryType);
        Assert.Equal(expectedWord, addr.Address);
    }

    [Theory(DisplayName = "大地址字节数组编码正确")]
    [InlineData("D9999", new Byte[] { 0x82, 0x27, 0x0F, 0x00 })]
    [InlineData("CIO9999", new Byte[] { 0xB0, 0x27, 0x0F, 0x00 })]
    public void LargeAddress_ToBytes_Correct(String address, Byte[] expected)
    {
        var addr = FinsAddress.Parse(address);
        // ToBytes() 返回: AreaCode(1) + Word_H(1) + Word_L(1) + BitOffset(1)
        var bytes = addr.ToBytes();
        Assert.Equal(expected, bytes);
    }
}
