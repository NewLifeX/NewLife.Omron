using System;
using System.ComponentModel;
using NewLife.Omron.Protocols;

namespace XUnitTest;

/// <summary>FINS头部测试</summary>
[DisplayName("FinsHeader头部解析")]
public class FinsHeaderTests
{
    [Fact]
    [DisplayName("默认值验证")]
    public void DefaultValues()
    {
        var header = new FinsHeader();
        Assert.Equal(0x80, header.ICF);
        Assert.Equal(0x00, header.RSV);
        Assert.Equal(0x02, header.GCT);
        Assert.Equal(0x00, header.DNA);
        Assert.Equal(0x00, header.DA1);
        Assert.Equal(0x00, header.DA2);
        Assert.Equal(0x00, header.SNA);
        Assert.Equal(0x00, header.SA1);
        Assert.Equal(0x00, header.SA2);
        Assert.Equal(0x00, header.SID);
    }

    [Fact]
    [DisplayName("ToBytes输出10字节")]
    public void ToBytes_Length10()
    {
        var header = new FinsHeader();
        var bytes = header.ToBytes();
        Assert.Equal(10, bytes.Length);
    }

    [Fact]
    [DisplayName("ToBytes字节顺序正确")]
    public void ToBytes_CorrectOrder()
    {
        var header = new FinsHeader
        {
            ICF = 0x80,
            RSV = 0x00,
            GCT = 0x02,
            DNA = 0x01,
            DA1 = 0x02,
            DA2 = 0x03,
            SNA = 0x04,
            SA1 = 0x05,
            SA2 = 0x06,
            SID = 0x07
        };
        var bytes = header.ToBytes();
        Assert.Equal(new Byte[] { 0x80, 0x00, 0x02, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 }, bytes);
    }

    [Fact]
    [DisplayName("Parse解析字节数组")]
    public void Parse_FromBytes()
    {
        var data = new Byte[] { 0x80, 0x00, 0x02, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        var header = FinsHeader.Parse(data, 0);
        Assert.Equal(0x80, header.ICF);
        Assert.Equal(0x00, header.RSV);
        Assert.Equal(0x02, header.GCT);
        Assert.Equal(0x01, header.DNA);
        Assert.Equal(0x02, header.DA1);
        Assert.Equal(0x03, header.DA2);
        Assert.Equal(0x04, header.SNA);
        Assert.Equal(0x05, header.SA1);
        Assert.Equal(0x06, header.SA2);
        Assert.Equal(0x07, header.SID);
    }

    [Fact]
    [DisplayName("Parse带偏移量解析")]
    public void Parse_WithOffset()
    {
        // 前2字节是填充，从offset=2开始解析
        var data = new Byte[] { 0xFF, 0xFF, 0x80, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x01 };
        var header = FinsHeader.Parse(data, 2);
        Assert.Equal(0x80, header.ICF);
        Assert.Equal(0x0A, header.SA1);
        Assert.Equal(0x01, header.SID);
    }

    [Fact]
    [DisplayName("往返转换一致")]
    public void RoundTrip()
    {
        var original = new FinsHeader
        {
            ICF = 0xC0,
            RSV = 0x00,
            GCT = 0x02,
            DNA = 0x00,
            DA1 = 0x0A,
            DA2 = 0x00,
            SNA = 0x00,
            SA1 = 0x05,
            SA2 = 0x00,
            SID = 0x03
        };
        var bytes = original.ToBytes();
        var parsed = FinsHeader.Parse(bytes, 0);
        Assert.Equal(original.ICF, parsed.ICF);
        Assert.Equal(original.DA1, parsed.DA1);
        Assert.Equal(original.SA1, parsed.SA1);
        Assert.Equal(original.SID, parsed.SID);
    }

    [Fact]
    [DisplayName("数据不足抛出异常")]
    public void Parse_InsufficientData_Throws()
    {
        var data = new Byte[] { 0x80, 0x00, 0x02 };
        Assert.Throws<ArgumentException>(() => FinsHeader.Parse(data, 0));
    }

    [Fact]
    [DisplayName("null数据抛出异常")]
    public void Parse_NullData_Throws()
    {
        Assert.Throws<ArgumentException>(() => FinsHeader.Parse(null, 0));
    }
}

/// <summary>FINS命令测试</summary>
[DisplayName("FinsCommand命令解析")]
public class FinsCommandTests
{
    [Fact]
    [DisplayName("Read命令值正确")]
    public void Read_Command()
    {
        var cmd = FinsCommand.Read;
        Assert.Equal(0x01, cmd.MRC);
        Assert.Equal(0x01, cmd.SRC);
    }

    [Fact]
    [DisplayName("Write命令值正确")]
    public void Write_Command()
    {
        var cmd = FinsCommand.Write;
        Assert.Equal(0x01, cmd.MRC);
        Assert.Equal(0x02, cmd.SRC);
    }

    [Fact]
    [DisplayName("ToBytes输出2字节")]
    public void ToBytes_Length2()
    {
        var bytes = FinsCommand.Read.ToBytes();
        Assert.Equal(2, bytes.Length);
    }

    [Fact]
    [DisplayName("ToBytes字节顺序正确")]
    public void ToBytes_CorrectOrder()
    {
        var cmd = FinsCommand.Read;
        var bytes = cmd.ToBytes();
        Assert.Equal(new Byte[] { 0x01, 0x01 }, bytes);
    }

    [Fact]
    [DisplayName("Parse解析字节数组")]
    public void Parse_FromBytes()
    {
        var data = new Byte[] { 0x01, 0x02 };
        var cmd = FinsCommand.Parse(data, 0);
        Assert.Equal(0x01, cmd.MRC);
        Assert.Equal(0x02, cmd.SRC);
    }

    [Fact]
    [DisplayName("Parse带偏移量")]
    public void Parse_WithOffset()
    {
        var data = new Byte[] { 0x00, 0x00, 0x01, 0x01 };
        var cmd = FinsCommand.Parse(data, 2);
        Assert.Equal(0x01, cmd.MRC);
        Assert.Equal(0x01, cmd.SRC);
    }

    [Fact]
    [DisplayName("数据不足抛出异常")]
    public void Parse_InsufficientData_Throws()
    {
        Assert.Throws<ArgumentException>(() => FinsCommand.Parse(new Byte[] { 0x01 }, 0));
    }

    [Fact]
    [DisplayName("往返转换一致")]
    public void RoundTrip()
    {
        var original = FinsCommand.Write;
        var bytes = original.ToBytes();
        var parsed = FinsCommand.Parse(bytes, 0);
        Assert.Equal(original.MRC, parsed.MRC);
        Assert.Equal(original.SRC, parsed.SRC);
    }
}
