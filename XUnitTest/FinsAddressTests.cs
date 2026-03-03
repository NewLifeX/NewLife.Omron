using System;
using System.ComponentModel;
using NewLife.Omron.Protocols;

namespace XUnitTest;

/// <summary>FINS地址解析测试</summary>
[DisplayName("FinsAddress地址解析")]
public class FinsAddressTests
{
    #region DM区

    [Fact]
    [DisplayName("解析D前缀DM区地址")]
    public void Parse_D_Area()
    {
        var addr = FinsAddress.Parse("D100");
        Assert.Equal(0x82, addr.MemoryType);
        Assert.Equal((UInt16)100, addr.Address);
        Assert.Equal(0, addr.BitOffset);
    }

    [Fact]
    [DisplayName("解析DM前缀DM区地址")]
    public void Parse_DM_Area()
    {
        var addr = FinsAddress.Parse("DM200");
        Assert.Equal(0x82, addr.MemoryType);
        Assert.Equal((UInt16)200, addr.Address);
        Assert.Equal(0, addr.BitOffset);
    }

    [Fact]
    [DisplayName("解析DM区地址大小写不敏感")]
    public void Parse_DM_CaseInsensitive()
    {
        var addr1 = FinsAddress.Parse("d100");
        var addr2 = FinsAddress.Parse("D100");
        Assert.Equal(addr1.MemoryType, addr2.MemoryType);
        Assert.Equal(addr1.Address, addr2.Address);
    }

    #endregion

    #region CIO区

    [Fact]
    [DisplayName("解析CIO区地址")]
    public void Parse_CIO_Area()
    {
        var addr = FinsAddress.Parse("CIO50");
        Assert.Equal(0xB0, addr.MemoryType);
        Assert.Equal((UInt16)50, addr.Address);
    }

    #endregion

    #region WR区

    [Fact]
    [DisplayName("解析W前缀WR区地址")]
    public void Parse_W_Area()
    {
        var addr = FinsAddress.Parse("W10");
        Assert.Equal(0xB1, addr.MemoryType);
        Assert.Equal((UInt16)10, addr.Address);
    }

    [Fact]
    [DisplayName("解析WR前缀WR区地址")]
    public void Parse_WR_Area()
    {
        var addr = FinsAddress.Parse("WR10");
        Assert.Equal(0xB1, addr.MemoryType);
        Assert.Equal((UInt16)10, addr.Address);
    }

    #endregion

    #region HR区

    [Fact]
    [DisplayName("解析H前缀HR区地址")]
    public void Parse_H_Area()
    {
        var addr = FinsAddress.Parse("H5");
        Assert.Equal(0xB2, addr.MemoryType);
        Assert.Equal((UInt16)5, addr.Address);
    }

    [Fact]
    [DisplayName("解析HR前缀HR区地址")]
    public void Parse_HR_Area()
    {
        var addr = FinsAddress.Parse("HR5");
        Assert.Equal(0xB2, addr.MemoryType);
        Assert.Equal((UInt16)5, addr.Address);
    }

    #endregion

    #region AR区

    [Fact]
    [DisplayName("解析A前缀AR区地址")]
    public void Parse_A_Area()
    {
        var addr = FinsAddress.Parse("A3");
        Assert.Equal(0xB3, addr.MemoryType);
        Assert.Equal((UInt16)3, addr.Address);
    }

    [Fact]
    [DisplayName("解析AR前缀AR区地址")]
    public void Parse_AR_Area()
    {
        var addr = FinsAddress.Parse("AR3");
        Assert.Equal(0xB3, addr.MemoryType);
        Assert.Equal((UInt16)3, addr.Address);
    }

    #endregion

    #region EM区

    [Fact]
    [DisplayName("解析EM区地址")]
    public void Parse_EM_Area()
    {
        var addr = FinsAddress.Parse("EM100");
        Assert.Equal(0xA0, addr.MemoryType);
        Assert.Equal((UInt16)100, addr.Address);
    }

    #endregion

    #region C区 (CIO位区)

    [Fact]
    [DisplayName("解析C前缀CIO位区地址")]
    public void Parse_C_Area()
    {
        var addr = FinsAddress.Parse("C20");
        Assert.Equal(0x80, addr.MemoryType);
        Assert.Equal((UInt16)20, addr.Address);
    }

    #endregion

    #region 位偏移

    [Fact]
    [DisplayName("解析带位偏移的地址")]
    public void Parse_WithBitOffset()
    {
        var addr = FinsAddress.Parse("D100.5");
        Assert.Equal(0x82, addr.MemoryType);
        Assert.Equal((UInt16)100, addr.Address);
        Assert.Equal(5, addr.BitOffset);
    }

    [Fact]
    [DisplayName("解析不带位偏移的地址默认BitOffset为0")]
    public void Parse_WithoutBitOffset_DefaultsToZero()
    {
        var addr = FinsAddress.Parse("D100");
        Assert.Equal(0, addr.BitOffset);
    }

    #endregion

    #region ToBytes

    [Fact]
    [DisplayName("地址转字节数组")]
    public void ToBytes_DM_Area()
    {
        var addr = new FinsAddress
        {
            MemoryType = 0x82,
            Address = 100,
            BitOffset = 0
        };
        var bytes = addr.ToBytes();
        Assert.Equal(4, bytes.Length);
        Assert.Equal(0x82, bytes[0]);
        Assert.Equal(0x00, bytes[1]); // 高字节
        Assert.Equal(0x64, bytes[2]); // 低字节 (100 = 0x64)
        Assert.Equal(0x00, bytes[3]); // 位偏移
    }

    [Fact]
    [DisplayName("带位偏移的地址转字节数组")]
    public void ToBytes_WithBitOffset()
    {
        var addr = FinsAddress.Parse("D200.7");
        var bytes = addr.ToBytes();
        Assert.Equal(0x82, bytes[0]);
        Assert.Equal(0x00, bytes[1]);
        Assert.Equal(0xC8, bytes[2]); // 200 = 0xC8
        Assert.Equal(7, bytes[3]);
    }

    #endregion

    #region 错误处理

    [Fact]
    [DisplayName("空地址抛出异常")]
    public void Parse_EmptyAddress_Throws()
    {
        Assert.Throws<ArgumentException>(() => FinsAddress.Parse(""));
    }

    [Fact]
    [DisplayName("null地址抛出异常")]
    public void Parse_NullAddress_Throws()
    {
        Assert.Throws<ArgumentException>(() => FinsAddress.Parse(null));
    }

    [Fact]
    [DisplayName("不支持的地址类型抛出异常")]
    public void Parse_UnsupportedArea_Throws()
    {
        Assert.Throws<ArgumentException>(() => FinsAddress.Parse("X100"));
    }

    #endregion

    #region 地址数值边界

    [Fact]
    [DisplayName("地址为0")]
    public void Parse_AddressZero()
    {
        var addr = FinsAddress.Parse("D0");
        Assert.Equal((UInt16)0, addr.Address);
    }

    [Fact]
    [DisplayName("地址为较大值")]
    public void Parse_LargeAddress()
    {
        var addr = FinsAddress.Parse("D32767");
        Assert.Equal((UInt16)32767, addr.Address);
    }

    #endregion
}
