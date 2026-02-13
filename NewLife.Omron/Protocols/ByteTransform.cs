using System;

namespace NewLife.Omron.Protocols;

/// <summary>
/// 字节转换助手
/// </summary>
public class ByteTransform
{
    /// <summary>数据格式</summary>
    public DataFormat DataFormat { get; set; } = DataFormat.CDAB;

    /// <summary>
    /// 转换为Int16
    /// </summary>
    public Int16 TransInt16(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 2)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD => BitConverter.ToInt16(new[] { data[offset + 1], data[offset] }, 0),
            DataFormat.BADC => BitConverter.ToInt16(new[] { data[offset], data[offset + 1] }, 0),
            DataFormat.CDAB => BitConverter.ToInt16(new[] { data[offset + 1], data[offset] }, 0),
            DataFormat.DCBA => BitConverter.ToInt16(new[] { data[offset], data[offset + 1] }, 0),
            _ => BitConverter.ToInt16(new[] { data[offset], data[offset + 1] }, 0)
        };
    }

    /// <summary>
    /// 转换为UInt16
    /// </summary>
    public UInt16 TransUInt16(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 2)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD => BitConverter.ToUInt16(new[] { data[offset + 1], data[offset] }, 0),
            DataFormat.BADC => BitConverter.ToUInt16(new[] { data[offset], data[offset + 1] }, 0),
            DataFormat.CDAB => BitConverter.ToUInt16(new[] { data[offset + 1], data[offset] }, 0),
            DataFormat.DCBA => BitConverter.ToUInt16(new[] { data[offset], data[offset + 1] }, 0),
            _ => BitConverter.ToUInt16(new[] { data[offset], data[offset + 1] }, 0)
        };
    }

    /// <summary>
    /// 转换为Int32
    /// </summary>
    public Int32 TransInt32(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 4)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD => BitConverter.ToInt32(new[] { data[offset + 3], data[offset + 2], data[offset + 1], data[offset] }, 0),
            DataFormat.BADC => BitConverter.ToInt32(new[] { data[offset + 2], data[offset + 3], data[offset], data[offset + 1] }, 0),
            DataFormat.CDAB => BitConverter.ToInt32(new[] { data[offset + 1], data[offset], data[offset + 3], data[offset + 2] }, 0),
            DataFormat.DCBA => BitConverter.ToInt32(new[] { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] }, 0),
            _ => BitConverter.ToInt32(new[] { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] }, 0)
        };
    }

    /// <summary>
    /// 转换为UInt32
    /// </summary>
    public UInt32 TransUInt32(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 4)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD => BitConverter.ToUInt32(new[] { data[offset + 3], data[offset + 2], data[offset + 1], data[offset] }, 0),
            DataFormat.BADC => BitConverter.ToUInt32(new[] { data[offset + 2], data[offset + 3], data[offset], data[offset + 1] }, 0),
            DataFormat.CDAB => BitConverter.ToUInt32(new[] { data[offset + 1], data[offset], data[offset + 3], data[offset + 2] }, 0),
            DataFormat.DCBA => BitConverter.ToUInt32(new[] { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] }, 0),
            _ => BitConverter.ToUInt32(new[] { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] }, 0)
        };
    }

    /// <summary>
    /// 转换为Float
    /// </summary>
    public Single TransSingle(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 4)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD => BitConverter.ToSingle(new[] { data[offset + 3], data[offset + 2], data[offset + 1], data[offset] }, 0),
            DataFormat.BADC => BitConverter.ToSingle(new[] { data[offset + 2], data[offset + 3], data[offset], data[offset + 1] }, 0),
            DataFormat.CDAB => BitConverter.ToSingle(new[] { data[offset + 1], data[offset], data[offset + 3], data[offset + 2] }, 0),
            DataFormat.DCBA => BitConverter.ToSingle(new[] { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] }, 0),
            _ => BitConverter.ToSingle(new[] { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] }, 0)
        };
    }

    /// <summary>
    /// 转换为Double
    /// </summary>
    public Double TransDouble(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 8)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD => BitConverter.ToDouble(new[] { 
                data[offset + 7], data[offset + 6], data[offset + 5], data[offset + 4],
                data[offset + 3], data[offset + 2], data[offset + 1], data[offset] }, 0),
            DataFormat.BADC => BitConverter.ToDouble(new[] { 
                data[offset + 6], data[offset + 7], data[offset + 4], data[offset + 5],
                data[offset + 2], data[offset + 3], data[offset], data[offset + 1] }, 0),
            DataFormat.CDAB => BitConverter.ToDouble(new[] { 
                data[offset + 1], data[offset], data[offset + 3], data[offset + 2],
                data[offset + 5], data[offset + 4], data[offset + 7], data[offset + 6] }, 0),
            DataFormat.DCBA => BitConverter.ToDouble(new[] { 
                data[offset], data[offset + 1], data[offset + 2], data[offset + 3],
                data[offset + 4], data[offset + 5], data[offset + 6], data[offset + 7] }, 0),
            _ => BitConverter.ToDouble(new[] { 
                data[offset], data[offset + 1], data[offset + 2], data[offset + 3],
                data[offset + 4], data[offset + 5], data[offset + 6], data[offset + 7] }, 0)
        };
    }

    /// <summary>
    /// Int16转字节数组
    /// </summary>
    public Byte[] TransByte(Int16 value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD => new[] { bytes[1], bytes[0] },
            DataFormat.BADC => new[] { bytes[0], bytes[1] },
            DataFormat.CDAB => new[] { bytes[1], bytes[0] },
            DataFormat.DCBA => new[] { bytes[0], bytes[1] },
            _ => bytes
        };
    }

    /// <summary>
    /// UInt16转字节数组
    /// </summary>
    public Byte[] TransByte(UInt16 value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD => new[] { bytes[1], bytes[0] },
            DataFormat.BADC => new[] { bytes[0], bytes[1] },
            DataFormat.CDAB => new[] { bytes[1], bytes[0] },
            DataFormat.DCBA => new[] { bytes[0], bytes[1] },
            _ => bytes
        };
    }

    /// <summary>
    /// Int32转字节数组
    /// </summary>
    public Byte[] TransByte(Int32 value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD => new[] { bytes[3], bytes[2], bytes[1], bytes[0] },
            DataFormat.BADC => new[] { bytes[2], bytes[3], bytes[0], bytes[1] },
            DataFormat.CDAB => new[] { bytes[1], bytes[0], bytes[3], bytes[2] },
            DataFormat.DCBA => new[] { bytes[0], bytes[1], bytes[2], bytes[3] },
            _ => bytes
        };
    }

    /// <summary>
    /// UInt32转字节数组
    /// </summary>
    public Byte[] TransByte(UInt32 value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD => new[] { bytes[3], bytes[2], bytes[1], bytes[0] },
            DataFormat.BADC => new[] { bytes[2], bytes[3], bytes[0], bytes[1] },
            DataFormat.CDAB => new[] { bytes[1], bytes[0], bytes[3], bytes[2] },
            DataFormat.DCBA => new[] { bytes[0], bytes[1], bytes[2], bytes[3] },
            _ => bytes
        };
    }

    /// <summary>
    /// Float转字节数组
    /// </summary>
    public Byte[] TransByte(Single value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD => new[] { bytes[3], bytes[2], bytes[1], bytes[0] },
            DataFormat.BADC => new[] { bytes[2], bytes[3], bytes[0], bytes[1] },
            DataFormat.CDAB => new[] { bytes[1], bytes[0], bytes[3], bytes[2] },
            DataFormat.DCBA => new[] { bytes[0], bytes[1], bytes[2], bytes[3] },
            _ => bytes
        };
    }

    /// <summary>
    /// Double转字节数组
    /// </summary>
    public Byte[] TransByte(Double value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD => new[] { 
                bytes[7], bytes[6], bytes[5], bytes[4],
                bytes[3], bytes[2], bytes[1], bytes[0] },
            DataFormat.BADC => new[] { 
                bytes[6], bytes[7], bytes[4], bytes[5],
                bytes[2], bytes[3], bytes[0], bytes[1] },
            DataFormat.CDAB => new[] { 
                bytes[1], bytes[0], bytes[3], bytes[2],
                bytes[5], bytes[4], bytes[7], bytes[6] },
            DataFormat.DCBA => new[] { 
                bytes[0], bytes[1], bytes[2], bytes[3],
                bytes[4], bytes[5], bytes[6], bytes[7] },
            _ => bytes
        };
    }
}
