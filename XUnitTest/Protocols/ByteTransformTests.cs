using NewLife.Omron.Protocols;
using Xunit;

namespace XUnitTest.Protocols;

/// <summary>ByteTransform字节转换测试</summary>
public class ByteTransformTests
{
    #region Int16

    [Theory]
    [InlineData(DataFormat.CDAB, new Byte[] { 0x00, 0x0A }, 10)]
    [InlineData(DataFormat.ABCD, new Byte[] { 0x00, 0x0A }, 10)]
    [InlineData(DataFormat.DCBA, new Byte[] { 0x0A, 0x00 }, 10)]
    [InlineData(DataFormat.BADC, new Byte[] { 0x0A, 0x00 }, 10)]
    public void TransInt16(DataFormat format, Byte[] data, Int16 expected)
    {
        var bt = new ByteTransform { DataFormat = format };
        Assert.Equal(expected, bt.TransInt16(data, 0));
    }

    [Theory]
    [InlineData(DataFormat.CDAB, 10, new Byte[] { 0x00, 0x0A })]
    [InlineData(DataFormat.DCBA, 10, new Byte[] { 0x0A, 0x00 })]
    public void TransByteInt16(DataFormat format, Int16 value, Byte[] expected)
    {
        var bt = new ByteTransform { DataFormat = format };
        Assert.Equal(expected, bt.TransByte(value));
    }

    [Fact]
    public void Int16RoundTrip()
    {
        foreach (var format in Enum.GetValues<DataFormat>())
        {
            var bt = new ByteTransform { DataFormat = format };
            Int16 value = 12345;
            var bytes = bt.TransByte(value);
            var result = bt.TransInt16(bytes, 0);
            Assert.Equal(value, result);
        }
    }

    #endregion

    #region UInt16

    [Fact]
    public void UInt16RoundTrip()
    {
        foreach (var format in Enum.GetValues<DataFormat>())
        {
            var bt = new ByteTransform { DataFormat = format };
            UInt16 value = 54321;
            var bytes = bt.TransByte(value);
            var result = bt.TransUInt16(bytes, 0);
            Assert.Equal(value, result);
        }
    }

    #endregion

    #region Int32

    [Theory]
    [InlineData(DataFormat.ABCD, new Byte[] { 0x00, 0x01, 0x00, 0x00 }, 65536)]
    [InlineData(DataFormat.DCBA, new Byte[] { 0x00, 0x00, 0x01, 0x00 }, 65536)]
    [InlineData(DataFormat.CDAB, new Byte[] { 0x00, 0x00, 0x00, 0x01 }, 65536)]
    [InlineData(DataFormat.BADC, new Byte[] { 0x01, 0x00, 0x00, 0x00 }, 65536)]
    public void TransInt32(DataFormat format, Byte[] data, Int32 expected)
    {
        var bt = new ByteTransform { DataFormat = format };
        Assert.Equal(expected, bt.TransInt32(data, 0));
    }

    [Fact]
    public void Int32RoundTrip()
    {
        foreach (var format in Enum.GetValues<DataFormat>())
        {
            var bt = new ByteTransform { DataFormat = format };
            var value = 123456789;
            var bytes = bt.TransByte(value);
            var result = bt.TransInt32(bytes, 0);
            Assert.Equal(value, result);
        }
    }

    #endregion

    #region UInt32

    [Fact]
    public void UInt32RoundTrip()
    {
        foreach (var format in Enum.GetValues<DataFormat>())
        {
            var bt = new ByteTransform { DataFormat = format };
            UInt32 value = 3000000000;
            var bytes = bt.TransByte(value);
            var result = bt.TransUInt32(bytes, 0);
            Assert.Equal(value, result);
        }
    }

    #endregion

    #region Int64

    [Fact]
    public void Int64RoundTrip()
    {
        foreach (var format in Enum.GetValues<DataFormat>())
        {
            var bt = new ByteTransform { DataFormat = format };
            var value = 1234567890123456789L;
            var bytes = bt.TransByte(value);
            Assert.Equal(8, bytes.Length);
            var result = bt.TransInt64(bytes, 0);
            Assert.Equal(value, result);
        }
    }

    #endregion

    #region UInt64

    [Fact]
    public void UInt64RoundTrip()
    {
        foreach (var format in Enum.GetValues<DataFormat>())
        {
            var bt = new ByteTransform { DataFormat = format };
            UInt64 value = 18000000000000000000;
            var bytes = bt.TransByte(value);
            Assert.Equal(8, bytes.Length);
            var result = bt.TransUInt64(bytes, 0);
            Assert.Equal(value, result);
        }
    }

    #endregion

    #region Single

    [Fact]
    public void SingleRoundTrip()
    {
        foreach (var format in Enum.GetValues<DataFormat>())
        {
            var bt = new ByteTransform { DataFormat = format };
            var value = 3.14f;
            var bytes = bt.TransByte(value);
            Assert.Equal(4, bytes.Length);
            var result = bt.TransSingle(bytes, 0);
            Assert.Equal(value, result);
        }
    }

    #endregion

    #region Double

    [Fact]
    public void DoubleRoundTrip()
    {
        foreach (var format in Enum.GetValues<DataFormat>())
        {
            var bt = new ByteTransform { DataFormat = format };
            var value = 3.141592653589793;
            var bytes = bt.TransByte(value);
            Assert.Equal(8, bytes.Length);
            var result = bt.TransDouble(bytes, 0);
            Assert.Equal(value, result);
        }
    }

    #endregion

    #region Boolean

    [Fact]
    public void BooleanTransform()
    {
        var bt = new ByteTransform();

        Assert.True(bt.TransBoolean(new Byte[] { 0x01 }, 0));
        Assert.True(bt.TransBoolean(new Byte[] { 0xFF }, 0));
        Assert.False(bt.TransBoolean(new Byte[] { 0x00 }, 0));
    }

    #endregion

    #region String

    [Fact]
    public void StringTransform()
    {
        var bt = new ByteTransform();

        // 写入
        var bytes = bt.TransByte("AB");
        Assert.Equal(2, bytes.Length);
        Assert.Equal((Byte)'A', bytes[0]);
        Assert.Equal((Byte)'B', bytes[1]);

        // 读取
        var str = bt.TransString(bytes, 0, bytes.Length);
        Assert.Equal("AB", str);
    }

    [Fact]
    public void StringTransformWordAligned()
    {
        var bt = new ByteTransform();

        // 奇数长度应补零
        var bytes = bt.TransByte("ABC");
        Assert.Equal(4, bytes.Length);
        Assert.Equal(0x00, bytes[3]);
    }

    [Fact]
    public void StringReadTruncatesAtNull()
    {
        var bt = new ByteTransform();
        var data = new Byte[] { (Byte)'H', (Byte)'i', 0x00, 0x00 };
        var str = bt.TransString(data, 0, 4);
        Assert.Equal("Hi", str);
    }

    [Fact]
    public void EmptyStringTransform()
    {
        var bt = new ByteTransform();
        var bytes = bt.TransByte("");
        Assert.Equal(2, bytes.Length);
    }

    #endregion

    #region 边界情况

    [Fact(DisplayName = "数据不足应抛出异常")]
    public void InsufficientDataShouldThrow()
    {
        var bt = new ByteTransform();

        Assert.Throws<ArgumentException>(() => bt.TransInt16(new Byte[1], 0));
        Assert.Throws<ArgumentException>(() => bt.TransUInt16(new Byte[1], 0));
        Assert.Throws<ArgumentException>(() => bt.TransInt32(new Byte[3], 0));
        Assert.Throws<ArgumentException>(() => bt.TransUInt32(new Byte[3], 0));
        Assert.Throws<ArgumentException>(() => bt.TransInt64(new Byte[7], 0));
        Assert.Throws<ArgumentException>(() => bt.TransUInt64(new Byte[7], 0));
        Assert.Throws<ArgumentException>(() => bt.TransSingle(new Byte[3], 0));
        Assert.Throws<ArgumentException>(() => bt.TransDouble(new Byte[7], 0));
        Assert.Throws<ArgumentException>(() => bt.TransBoolean(Array.Empty<Byte>(), 0));
        Assert.Throws<ArgumentException>(() => bt.TransString(new Byte[2], 0, 4));
    }

    [Fact(DisplayName = "Null数据应抛出异常")]
    public void NullDataShouldThrow()
    {
        var bt = new ByteTransform();

        Assert.Throws<ArgumentException>(() => bt.TransInt16(null, 0));
        Assert.Throws<ArgumentException>(() => bt.TransInt32(null, 0));
        Assert.Throws<ArgumentException>(() => bt.TransSingle(null, 0));
        Assert.Throws<ArgumentException>(() => bt.TransDouble(null, 0));
    }

    [Fact(DisplayName = "带偏移读取")]
    public void ReadWithOffset()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.CDAB };
        var data = new Byte[] { 0xFF, 0xFF, 0x00, 0x0A };

        var value = bt.TransInt16(data, 2);
        Assert.Equal(10, value);
    }

    [Fact(DisplayName = "特殊值往返：零值")]
    public void ZeroValueRoundTrip()
    {
        var bt = new ByteTransform();
        Assert.Equal((Int16)0, bt.TransInt16(bt.TransByte((Int16)0), 0));
        Assert.Equal(0, bt.TransInt32(bt.TransByte(0), 0));
        Assert.Equal(0f, bt.TransSingle(bt.TransByte(0f), 0));
        Assert.Equal(0.0, bt.TransDouble(bt.TransByte(0.0), 0));
    }

    [Fact(DisplayName = "特殊值往返：负值")]
    public void NegativeValueRoundTrip()
    {
        var bt = new ByteTransform();
        Assert.Equal((Int16)(-1), bt.TransInt16(bt.TransByte((Int16)(-1)), 0));
        Assert.Equal(-123456, bt.TransInt32(bt.TransByte(-123456), 0));
        Assert.Equal(-3.14f, bt.TransSingle(bt.TransByte(-3.14f), 0));
        Assert.Equal(-1.23456789, bt.TransDouble(bt.TransByte(-1.23456789), 0));
    }

    [Fact(DisplayName = "特殊值往返：最大值")]
    public void MaxValueRoundTrip()
    {
        foreach (var format in Enum.GetValues<DataFormat>())
        {
            var bt = new ByteTransform { DataFormat = format };
            Assert.Equal(Int16.MaxValue, bt.TransInt16(bt.TransByte(Int16.MaxValue), 0));
            Assert.Equal(Int32.MaxValue, bt.TransInt32(bt.TransByte(Int32.MaxValue), 0));
            Assert.Equal(UInt16.MaxValue, bt.TransUInt16(bt.TransByte(UInt16.MaxValue), 0));
            Assert.Equal(UInt32.MaxValue, bt.TransUInt32(bt.TransByte(UInt32.MaxValue), 0));
        }
    }

    #endregion
}
