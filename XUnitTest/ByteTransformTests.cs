using System;
using System.ComponentModel;
using NewLife.Omron.Protocols;

namespace XUnitTest;

/// <summary>字节转换测试</summary>
[DisplayName("ByteTransform字节转换")]
public class ByteTransformTests
{
    #region Int16

    [Fact]
    [DisplayName("CDAB格式读取Int16")]
    public void TransInt16_CDAB()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.CDAB };
        // 大端：高字节在前，0x0001 = 1
        var data = new Byte[] { 0x00, 0x01 };
        var result = bt.TransInt16(data, 0);
        Assert.Equal((Int16)1, result);
    }

    [Fact]
    [DisplayName("ABCD格式读取Int16")]
    public void TransInt16_ABCD()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.ABCD };
        var data = new Byte[] { 0x00, 0x01 };
        Assert.Equal((Int16)1, bt.TransInt16(data, 0));
    }

    [Fact]
    [DisplayName("BADC格式读取Int16")]
    public void TransInt16_BADC()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.BADC };
        // 小端：低字节在前，0x01 0x00 = 1
        var data = new Byte[] { 0x01, 0x00 };
        Assert.Equal((Int16)1, bt.TransInt16(data, 0));
    }

    [Fact]
    [DisplayName("DCBA格式读取Int16")]
    public void TransInt16_DCBA()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.DCBA };
        var data = new Byte[] { 0x01, 0x00 };
        Assert.Equal((Int16)1, bt.TransInt16(data, 0));
    }

    [Fact]
    [DisplayName("Int16负数读取")]
    public void TransInt16_Negative()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.CDAB };
        // -1 in big-endian = 0xFF 0xFF
        var data = new Byte[] { 0xFF, 0xFF };
        Assert.Equal((Int16)(-1), bt.TransInt16(data, 0));
    }

    [Fact]
    [DisplayName("Int16数据不足抛出异常")]
    public void TransInt16_InsufficientData_Throws()
    {
        var bt = new ByteTransform();
        Assert.Throws<ArgumentException>(() => bt.TransInt16(new Byte[] { 0x01 }, 0));
    }

    #endregion

    #region UInt16

    [Fact]
    [DisplayName("CDAB格式读取UInt16")]
    public void TransUInt16_CDAB()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.CDAB };
        var data = new Byte[] { 0x00, 0x64 }; // 100 大端
        Assert.Equal((UInt16)100, bt.TransUInt16(data, 0));
    }

    [Fact]
    [DisplayName("BADC格式读取UInt16")]
    public void TransUInt16_BADC()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.BADC };
        var data = new Byte[] { 0x64, 0x00 }; // 100 小端
        Assert.Equal((UInt16)100, bt.TransUInt16(data, 0));
    }

    [Fact]
    [DisplayName("UInt16最大值读取")]
    public void TransUInt16_MaxValue()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.CDAB };
        var data = new Byte[] { 0xFF, 0xFF };
        Assert.Equal(UInt16.MaxValue, bt.TransUInt16(data, 0));
    }

    #endregion

    #region Int32

    [Fact]
    [DisplayName("ABCD格式读取Int32")]
    public void TransInt32_ABCD()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.ABCD };
        // 0x00000001 大端 = 0x00, 0x00, 0x00, 0x01
        var data = new Byte[] { 0x00, 0x00, 0x00, 0x01 };
        Assert.Equal(1, bt.TransInt32(data, 0));
    }

    [Fact]
    [DisplayName("CDAB格式读取Int32")]
    public void TransInt32_CDAB()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.CDAB };
        // CDAB: 值0x12345678在内存中存储为 [C,D,A,B] = [0x56, 0x78, 0x12, 0x34]
        var data = new Byte[] { 0x56, 0x78, 0x12, 0x34 };
        Assert.Equal(0x12345678, bt.TransInt32(data, 0));
    }

    [Fact]
    [DisplayName("BADC格式读取Int32")]
    public void TransInt32_BADC()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.BADC };
        // BADC: 值0x12345678在内存中存储为 [B,A,D,C] = [0x34, 0x12, 0x78, 0x56]
        var data = new Byte[] { 0x34, 0x12, 0x78, 0x56 };
        Assert.Equal(0x12345678, bt.TransInt32(data, 0));
    }

    [Fact]
    [DisplayName("DCBA格式读取Int32")]
    public void TransInt32_DCBA()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.DCBA };
        // DCBA: 小端，直接按内存顺序
        // BitConverter.ToInt32({data[0], data[1], data[2], data[3]}, 0)
        // 在little-endian机器上，0x01 0x00 0x00 0x00 = 1
        var data = new Byte[] { 0x01, 0x00, 0x00, 0x00 };
        Assert.Equal(1, bt.TransInt32(data, 0));
    }

    [Fact]
    [DisplayName("Int32数据不足抛出异常")]
    public void TransInt32_InsufficientData_Throws()
    {
        var bt = new ByteTransform();
        Assert.Throws<ArgumentException>(() => bt.TransInt32(new Byte[] { 0x01, 0x02, 0x03 }, 0));
    }

    #endregion

    #region Float

    [Fact]
    [DisplayName("ABCD格式读取Float")]
    public void TransSingle_ABCD()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.ABCD };
        // 1.0f in IEEE754 big-endian: 0x3F, 0x80, 0x00, 0x00
        var data = new Byte[] { 0x3F, 0x80, 0x00, 0x00 };
        Assert.Equal(1.0f, bt.TransSingle(data, 0));
    }

    [Fact]
    [DisplayName("DCBA格式读取Float")]
    public void TransSingle_DCBA()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.DCBA };
        // 1.0f in IEEE754 little-endian: 0x00, 0x00, 0x80, 0x3F
        var data = new Byte[] { 0x00, 0x00, 0x80, 0x3F };
        Assert.Equal(1.0f, bt.TransSingle(data, 0));
    }

    [Fact]
    [DisplayName("Float数据不足抛出异常")]
    public void TransSingle_InsufficientData_Throws()
    {
        var bt = new ByteTransform();
        Assert.Throws<ArgumentException>(() => bt.TransSingle(new Byte[] { 0x3F, 0x80 }, 0));
    }

    #endregion

    #region Double

    [Fact]
    [DisplayName("ABCD格式读取Double")]
    public void TransDouble_ABCD()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.ABCD };
        // 1.0d in IEEE754 big-endian: 3F F0 00 00 00 00 00 00
        var data = new Byte[] { 0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        Assert.Equal(1.0d, bt.TransDouble(data, 0));
    }

    [Fact]
    [DisplayName("DCBA格式读取Double")]
    public void TransDouble_DCBA()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.DCBA };
        // 1.0d in IEEE754 little-endian: 00 00 00 00 00 00 F0 3F
        var data = new Byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F };
        Assert.Equal(1.0d, bt.TransDouble(data, 0));
    }

    [Fact]
    [DisplayName("Double数据不足抛出异常")]
    public void TransDouble_InsufficientData_Throws()
    {
        var bt = new ByteTransform();
        Assert.Throws<ArgumentException>(() => bt.TransDouble(new Byte[] { 0x3F, 0xF0, 0x00, 0x00 }, 0));
    }

    #endregion

    #region 往返测试（TransByte + Trans反向）

    [Fact]
    [DisplayName("Int16往返转换 CDAB")]
    public void RoundTrip_Int16_CDAB()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.CDAB };
        var value = (Int16)(-12345);
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransInt16(bytes, 0));
    }

    [Fact]
    [DisplayName("UInt16往返转换 BADC")]
    public void RoundTrip_UInt16_BADC()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.BADC };
        var value = (UInt16)60000;
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransUInt16(bytes, 0));
    }

    [Fact]
    [DisplayName("Int32往返转换 ABCD")]
    public void RoundTrip_Int32_ABCD()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.ABCD };
        var value = -100000;
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransInt32(bytes, 0));
    }

    [Fact]
    [DisplayName("Int32往返转换 CDAB")]
    public void RoundTrip_Int32_CDAB()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.CDAB };
        var value = 0x12345678;
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransInt32(bytes, 0));
    }

    [Fact]
    [DisplayName("Int32往返转换 BADC")]
    public void RoundTrip_Int32_BADC()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.BADC };
        var value = 0x12345678;
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransInt32(bytes, 0));
    }

    [Fact]
    [DisplayName("Int32往返转换 DCBA")]
    public void RoundTrip_Int32_DCBA()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.DCBA };
        var value = 0x12345678;
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransInt32(bytes, 0));
    }

    [Fact]
    [DisplayName("UInt32往返转换 CDAB")]
    public void RoundTrip_UInt32_CDAB()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.CDAB };
        var value = 0xABCDEF01u;
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransUInt32(bytes, 0));
    }

    [Fact]
    [DisplayName("Float往返转换 CDAB")]
    public void RoundTrip_Float_CDAB()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.CDAB };
        var value = 3.14159f;
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransSingle(bytes, 0));
    }

    [Fact]
    [DisplayName("Float往返转换 ABCD")]
    public void RoundTrip_Float_ABCD()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.ABCD };
        var value = -1.5f;
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransSingle(bytes, 0));
    }

    [Fact]
    [DisplayName("Double往返转换 CDAB")]
    public void RoundTrip_Double_CDAB()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.CDAB };
        var value = 3.141592653589793d;
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransDouble(bytes, 0));
    }

    [Fact]
    [DisplayName("Double往返转换 ABCD")]
    public void RoundTrip_Double_ABCD()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.ABCD };
        var value = -2.71828d;
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransDouble(bytes, 0));
    }

    [Fact]
    [DisplayName("Double往返转换 DCBA")]
    public void RoundTrip_Double_DCBA()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.DCBA };
        var value = 1234567.89d;
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransDouble(bytes, 0));
    }

    [Fact]
    [DisplayName("Double往返转换 BADC")]
    public void RoundTrip_Double_BADC()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.BADC };
        var value = 9876.543d;
        var bytes = bt.TransByte(value);
        Assert.Equal(value, bt.TransDouble(bytes, 0));
    }

    #endregion

    #region 偏移量测试

    [Fact]
    [DisplayName("带偏移量读取Int16")]
    public void TransInt16_WithOffset()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.CDAB };
        var data = new Byte[] { 0xFF, 0xFF, 0x00, 0x01 }; // offset=2处是1
        Assert.Equal((Int16)1, bt.TransInt16(data, 2));
    }

    [Fact]
    [DisplayName("带偏移量读取Int32")]
    public void TransInt32_WithOffset()
    {
        var bt = new ByteTransform { DataFormat = DataFormat.ABCD };
        var data = new Byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 };
        Assert.Equal(1, bt.TransInt32(data, 4));
    }

    #endregion
}
