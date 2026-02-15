using System;

namespace NewLife.Omron.Protocols;

/// <summary>字节转换助手</summary>
/// <remarks>
/// 处理欧姆龙PLC大端字节序与.NET小端字节序之间的转换。
/// 支持四种字节序格式: ABCD(大端)、BADC(字内交换)、CDAB(双字交换)、DCBA(小端)。
/// </remarks>
public class ByteTransform
{
    /// <summary>数据格式</summary>
    public DataFormat DataFormat { get; set; } = DataFormat.CDAB;

    #region 读取转换

    /// <summary>转换为Int16</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>Int16值</returns>
    public Int16 TransInt16(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 2)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD or DataFormat.CDAB => BitConverter.ToInt16([data[offset + 1], data[offset]], 0),
            DataFormat.BADC or DataFormat.DCBA => BitConverter.ToInt16([data[offset], data[offset + 1]], 0),
            _ => BitConverter.ToInt16([data[offset], data[offset + 1]], 0)
        };
    }

    /// <summary>转换为UInt16</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>UInt16值</returns>
    public UInt16 TransUInt16(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 2)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD or DataFormat.CDAB => BitConverter.ToUInt16([data[offset + 1], data[offset]], 0),
            DataFormat.BADC or DataFormat.DCBA => BitConverter.ToUInt16([data[offset], data[offset + 1]], 0),
            _ => BitConverter.ToUInt16([data[offset], data[offset + 1]], 0)
        };
    }

    /// <summary>转换为Int32</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>Int32值</returns>
    public Int32 TransInt32(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 4)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD => BitConverter.ToInt32([data[offset + 3], data[offset + 2], data[offset + 1], data[offset]], 0),
            DataFormat.BADC => BitConverter.ToInt32([data[offset + 2], data[offset + 3], data[offset], data[offset + 1]], 0),
            DataFormat.CDAB => BitConverter.ToInt32([data[offset + 1], data[offset], data[offset + 3], data[offset + 2]], 0),
            DataFormat.DCBA => BitConverter.ToInt32([data[offset], data[offset + 1], data[offset + 2], data[offset + 3]], 0),
            _ => BitConverter.ToInt32([data[offset], data[offset + 1], data[offset + 2], data[offset + 3]], 0)
        };
    }

    /// <summary>转换为UInt32</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>UInt32值</returns>
    public UInt32 TransUInt32(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 4)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD => BitConverter.ToUInt32([data[offset + 3], data[offset + 2], data[offset + 1], data[offset]], 0),
            DataFormat.BADC => BitConverter.ToUInt32([data[offset + 2], data[offset + 3], data[offset], data[offset + 1]], 0),
            DataFormat.CDAB => BitConverter.ToUInt32([data[offset + 1], data[offset], data[offset + 3], data[offset + 2]], 0),
            DataFormat.DCBA => BitConverter.ToUInt32([data[offset], data[offset + 1], data[offset + 2], data[offset + 3]], 0),
            _ => BitConverter.ToUInt32([data[offset], data[offset + 1], data[offset + 2], data[offset + 3]], 0)
        };
    }

    /// <summary>转换为Int64</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>Int64值</returns>
    public Int64 TransInt64(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 8)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD => BitConverter.ToInt64([
                data[offset + 7], data[offset + 6], data[offset + 5], data[offset + 4],
                data[offset + 3], data[offset + 2], data[offset + 1], data[offset] ], 0),
            DataFormat.BADC => BitConverter.ToInt64([
                data[offset + 6], data[offset + 7], data[offset + 4], data[offset + 5],
                data[offset + 2], data[offset + 3], data[offset], data[offset + 1] ], 0),
            DataFormat.CDAB => BitConverter.ToInt64([
                data[offset + 1], data[offset], data[offset + 3], data[offset + 2],
                data[offset + 5], data[offset + 4], data[offset + 7], data[offset + 6] ], 0),
            DataFormat.DCBA => BitConverter.ToInt64([
                data[offset], data[offset + 1], data[offset + 2], data[offset + 3],
                data[offset + 4], data[offset + 5], data[offset + 6], data[offset + 7] ], 0),
            _ => BitConverter.ToInt64(data, offset)
        };
    }

    /// <summary>转换为UInt64</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>UInt64值</returns>
    public UInt64 TransUInt64(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 8)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD => BitConverter.ToUInt64([
                data[offset + 7], data[offset + 6], data[offset + 5], data[offset + 4],
                data[offset + 3], data[offset + 2], data[offset + 1], data[offset] ], 0),
            DataFormat.BADC => BitConverter.ToUInt64([
                data[offset + 6], data[offset + 7], data[offset + 4], data[offset + 5],
                data[offset + 2], data[offset + 3], data[offset], data[offset + 1] ], 0),
            DataFormat.CDAB => BitConverter.ToUInt64([
                data[offset + 1], data[offset], data[offset + 3], data[offset + 2],
                data[offset + 5], data[offset + 4], data[offset + 7], data[offset + 6] ], 0),
            DataFormat.DCBA => BitConverter.ToUInt64([
                data[offset], data[offset + 1], data[offset + 2], data[offset + 3],
                data[offset + 4], data[offset + 5], data[offset + 6], data[offset + 7] ], 0),
            _ => BitConverter.ToUInt64(data, offset)
        };
    }

    /// <summary>转换为Float</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>Single值</returns>
    public Single TransSingle(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 4)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD => BitConverter.ToSingle([data[offset + 3], data[offset + 2], data[offset + 1], data[offset]], 0),
            DataFormat.BADC => BitConverter.ToSingle([data[offset + 2], data[offset + 3], data[offset], data[offset + 1]], 0),
            DataFormat.CDAB => BitConverter.ToSingle([data[offset + 1], data[offset], data[offset + 3], data[offset + 2]], 0),
            DataFormat.DCBA => BitConverter.ToSingle([data[offset], data[offset + 1], data[offset + 2], data[offset + 3]], 0),
            _ => BitConverter.ToSingle([data[offset], data[offset + 1], data[offset + 2], data[offset + 3]], 0)
        };
    }

    /// <summary>转换为Double</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>Double值</returns>
    public Double TransDouble(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 8)
            throw new ArgumentException("数据长度不足");

        return DataFormat switch
        {
            DataFormat.ABCD => BitConverter.ToDouble([
                data[offset + 7], data[offset + 6], data[offset + 5], data[offset + 4],
                data[offset + 3], data[offset + 2], data[offset + 1], data[offset] ], 0),
            DataFormat.BADC => BitConverter.ToDouble([
                data[offset + 6], data[offset + 7], data[offset + 4], data[offset + 5],
                data[offset + 2], data[offset + 3], data[offset], data[offset + 1] ], 0),
            DataFormat.CDAB => BitConverter.ToDouble([
                data[offset + 1], data[offset], data[offset + 3], data[offset + 2],
                data[offset + 5], data[offset + 4], data[offset + 7], data[offset + 6] ], 0),
            DataFormat.DCBA => BitConverter.ToDouble([
                data[offset], data[offset + 1], data[offset + 2], data[offset + 3],
                data[offset + 4], data[offset + 5], data[offset + 6], data[offset + 7] ], 0),
            _ => BitConverter.ToDouble([
                data[offset], data[offset + 1], data[offset + 2], data[offset + 3],
                data[offset + 4], data[offset + 5], data[offset + 6], data[offset + 7] ], 0)
        };
    }

    /// <summary>转换为Boolean</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>Boolean值</returns>
    public Boolean TransBoolean(Byte[] data, Int32 offset)
    {
        if (data == null || data.Length < offset + 1)
            throw new ArgumentException("数据长度不足");

        return data[offset] != 0;
    }

    /// <summary>转换为字符串</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="length">字节长度</param>
    /// <returns>ASCII字符串</returns>
    public String TransString(Byte[] data, Int32 offset, Int32 length)
    {
        if (data == null || data.Length < offset + length)
            throw new ArgumentException("数据长度不足");

        // 查找末尾的空字符
        var actualLength = length;
        for (var i = offset; i < offset + length; i++)
        {
            if (data[i] == 0x00) { actualLength = i - offset; break; }
        }

        return System.Text.Encoding.ASCII.GetString(data, offset, actualLength);
    }

    #endregion

    #region 写入转换

    /// <summary>Int16转字节数组</summary>
    /// <param name="value">值</param>
    /// <returns>字节数组</returns>
    public Byte[] TransByte(Int16 value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD or DataFormat.CDAB => [bytes[1], bytes[0]],
            DataFormat.BADC or DataFormat.DCBA => [bytes[0], bytes[1]],
            _ => bytes
        };
    }

    /// <summary>UInt16转字节数组</summary>
    /// <param name="value">值</param>
    /// <returns>字节数组</returns>
    public Byte[] TransByte(UInt16 value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD or DataFormat.CDAB => [bytes[1], bytes[0]],
            DataFormat.BADC or DataFormat.DCBA => [bytes[0], bytes[1]],
            _ => bytes
        };
    }

    /// <summary>Int32转字节数组</summary>
    /// <param name="value">值</param>
    /// <returns>字节数组</returns>
    public Byte[] TransByte(Int32 value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD => [bytes[3], bytes[2], bytes[1], bytes[0]],
            DataFormat.BADC => [bytes[2], bytes[3], bytes[0], bytes[1]],
            DataFormat.CDAB => [bytes[1], bytes[0], bytes[3], bytes[2]],
            DataFormat.DCBA => [bytes[0], bytes[1], bytes[2], bytes[3]],
            _ => bytes
        };
    }

    /// <summary>UInt32转字节数组</summary>
    /// <param name="value">值</param>
    /// <returns>字节数组</returns>
    public Byte[] TransByte(UInt32 value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD => [bytes[3], bytes[2], bytes[1], bytes[0]],
            DataFormat.BADC => [bytes[2], bytes[3], bytes[0], bytes[1]],
            DataFormat.CDAB => [bytes[1], bytes[0], bytes[3], bytes[2]],
            DataFormat.DCBA => [bytes[0], bytes[1], bytes[2], bytes[3]],
            _ => bytes
        };
    }

    /// <summary>Int64转字节数组</summary>
    /// <param name="value">值</param>
    /// <returns>字节数组</returns>
    public Byte[] TransByte(Int64 value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD => [
                bytes[7], bytes[6], bytes[5], bytes[4],
                bytes[3], bytes[2], bytes[1], bytes[0] ],
            DataFormat.BADC => [
                bytes[6], bytes[7], bytes[4], bytes[5],
                bytes[2], bytes[3], bytes[0], bytes[1] ],
            DataFormat.CDAB => [
                bytes[1], bytes[0], bytes[3], bytes[2],
                bytes[5], bytes[4], bytes[7], bytes[6] ],
            DataFormat.DCBA => [
                bytes[0], bytes[1], bytes[2], bytes[3],
                bytes[4], bytes[5], bytes[6], bytes[7] ],
            _ => bytes
        };
    }

    /// <summary>UInt64转字节数组</summary>
    /// <param name="value">值</param>
    /// <returns>字节数组</returns>
    public Byte[] TransByte(UInt64 value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD => [
                bytes[7], bytes[6], bytes[5], bytes[4],
                bytes[3], bytes[2], bytes[1], bytes[0] ],
            DataFormat.BADC => [
                bytes[6], bytes[7], bytes[4], bytes[5],
                bytes[2], bytes[3], bytes[0], bytes[1] ],
            DataFormat.CDAB => [
                bytes[1], bytes[0], bytes[3], bytes[2],
                bytes[5], bytes[4], bytes[7], bytes[6] ],
            DataFormat.DCBA => [
                bytes[0], bytes[1], bytes[2], bytes[3],
                bytes[4], bytes[5], bytes[6], bytes[7] ],
            _ => bytes
        };
    }

    /// <summary>Float转字节数组</summary>
    /// <param name="value">值</param>
    /// <returns>字节数组</returns>
    public Byte[] TransByte(Single value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD => [bytes[3], bytes[2], bytes[1], bytes[0]],
            DataFormat.BADC => [bytes[2], bytes[3], bytes[0], bytes[1]],
            DataFormat.CDAB => [bytes[1], bytes[0], bytes[3], bytes[2]],
            DataFormat.DCBA => [bytes[0], bytes[1], bytes[2], bytes[3]],
            _ => bytes
        };
    }

    /// <summary>Double转字节数组</summary>
    /// <param name="value">值</param>
    /// <returns>字节数组</returns>
    public Byte[] TransByte(Double value)
    {
        var bytes = BitConverter.GetBytes(value);
        return DataFormat switch
        {
            DataFormat.ABCD => [
                bytes[7], bytes[6], bytes[5], bytes[4],
                bytes[3], bytes[2], bytes[1], bytes[0] ],
            DataFormat.BADC => [
                bytes[6], bytes[7], bytes[4], bytes[5],
                bytes[2], bytes[3], bytes[0], bytes[1] ],
            DataFormat.CDAB => [
                bytes[1], bytes[0], bytes[3], bytes[2],
                bytes[5], bytes[4], bytes[7], bytes[6] ],
            DataFormat.DCBA => [
                bytes[0], bytes[1], bytes[2], bytes[3],
                bytes[4], bytes[5], bytes[6], bytes[7] ],
            _ => bytes
        };
    }

    /// <summary>字符串转字节数组（ASCII编码，字对齐）</summary>
    /// <param name="value">字符串</param>
    /// <returns>字节数组（长度为偶数）</returns>
    public Byte[] TransByte(String value)
    {
        if (String.IsNullOrEmpty(value)) return new Byte[2];

        var bytes = System.Text.Encoding.ASCII.GetBytes(value);

        // 确保字对齐（长度为偶数）
        if (bytes.Length % 2 != 0)
        {
            var aligned = new Byte[bytes.Length + 1];
            Array.Copy(bytes, 0, aligned, 0, bytes.Length);
            return aligned;
        }

        return bytes;
    }

    #endregion
}
