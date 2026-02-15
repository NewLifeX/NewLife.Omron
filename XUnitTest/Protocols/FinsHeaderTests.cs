using NewLife.Omron.Protocols;
using Xunit;

namespace XUnitTest.Protocols;

/// <summary>FinsHeader序列化测试</summary>
public class FinsHeaderTests
{
    [Fact(DisplayName = "默认头部序列化")]
    public void DefaultHeaderToBytes()
    {
        var header = new FinsHeader();
        var bytes = header.ToBytes();

        Assert.Equal(10, bytes.Length);
        Assert.Equal(0x80, bytes[0]); // ICF
        Assert.Equal(0x00, bytes[1]); // RSV
        Assert.Equal(0x02, bytes[2]); // GCT
    }

    [Fact(DisplayName = "自定义头部序列化")]
    public void CustomHeaderToBytes()
    {
        var header = new FinsHeader
        {
            ICF = 0x80,
            DNA = 0x01,
            DA1 = 0x02,
            DA2 = 0x03,
            SNA = 0x04,
            SA1 = 0x05,
            SA2 = 0x06,
            SID = 0x07
        };
        var bytes = header.ToBytes();

        Assert.Equal(0x01, bytes[3]); // DNA
        Assert.Equal(0x02, bytes[4]); // DA1
        Assert.Equal(0x03, bytes[5]); // DA2
        Assert.Equal(0x04, bytes[6]); // SNA
        Assert.Equal(0x05, bytes[7]); // SA1
        Assert.Equal(0x06, bytes[8]); // SA2
        Assert.Equal(0x07, bytes[9]); // SID
    }

    [Fact(DisplayName = "从字节数组解析")]
    public void ParseFromBytes()
    {
        var data = new Byte[] { 0x80, 0x00, 0x02, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        var header = FinsHeader.Parse(data);

        Assert.Equal(0x80, header.ICF);
        Assert.Equal(0x02, header.GCT);
        Assert.Equal(0x01, header.DNA);
        Assert.Equal(0x02, header.DA1);
        Assert.Equal(0x03, header.DA2);
        Assert.Equal(0x04, header.SNA);
        Assert.Equal(0x05, header.SA1);
        Assert.Equal(0x06, header.SA2);
        Assert.Equal(0x07, header.SID);
    }

    [Fact(DisplayName = "带偏移解析")]
    public void ParseWithOffset()
    {
        var data = new Byte[] { 0xFF, 0xFF, 0x80, 0x00, 0x02, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        var header = FinsHeader.Parse(data, 2);

        Assert.Equal(0x80, header.ICF);
        Assert.Equal(0x07, header.SID);
    }

    [Fact(DisplayName = "数据不足应抛出异常")]
    public void ParseInsufficientDataShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => FinsHeader.Parse(new Byte[5]));
        Assert.Throws<ArgumentException>(() => FinsHeader.Parse(null));
    }

    [Fact(DisplayName = "往返序列化一致")]
    public void RoundTrip()
    {
        var original = new FinsHeader
        {
            ICF = 0xC0,
            DNA = 0x10,
            DA1 = 0x20,
            DA2 = 0x30,
            SNA = 0x40,
            SA1 = 0x50,
            SA2 = 0x60,
            SID = 0x70
        };

        var bytes = original.ToBytes();
        var parsed = FinsHeader.Parse(bytes);

        Assert.Equal(original.ICF, parsed.ICF);
        Assert.Equal(original.DNA, parsed.DNA);
        Assert.Equal(original.DA1, parsed.DA1);
        Assert.Equal(original.DA2, parsed.DA2);
        Assert.Equal(original.SNA, parsed.SNA);
        Assert.Equal(original.SA1, parsed.SA1);
        Assert.Equal(original.SA2, parsed.SA2);
        Assert.Equal(original.SID, parsed.SID);
    }
}
