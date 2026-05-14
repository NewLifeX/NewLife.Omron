using System;
using NewLife.IoT.ThingModels;

namespace NewLife.Omron.Protocols;

/// <summary>欧姆龙客户端基类</summary>
/// <remarks>
/// 提供所有欧姆龙PLC客户端的共性功能，包括字节转换、超时配置等。
/// 子类包括：FinsClient(FINS/TCP)、FinsUdpClient(FINS/UDP)、HostLinkClient(HostLink串口/网络)。
/// </remarks>
public abstract class OmronClientBase : IDisposable
{
    #region 属性

    /// <summary>线程锁</summary>
    protected readonly Object _lock = new();

    /// <summary>接收超时(毫秒)</summary>
    public Int32 ReceiveTimeOut { get; set; } = 5000;

    /// <summary>字节序</summary>
    public ByteOrder ByteOrder
    {
        get => Transform.ByteOrder;
        set => Transform.ByteOrder = value;
    }

    /// <summary>字节转换器</summary>
    public ByteTransform Transform { get; set; }

    #endregion

    #region 构造

    /// <summary>实例化欧姆龙客户端基类</summary>
    protected OmronClientBase()
    {
        Transform = new ByteTransform { ByteOrder = ByteOrder.CDAB };
    }

    #endregion

    #region 类型化读写（抽象方法）

    /// <summary>读取数据（字访问）</summary>
    /// <param name="address">地址字符串</param>
    /// <param name="length">读取字数</param>
    /// <returns>读取到的字节数组</returns>
    public abstract Byte[] Read(String address, UInt16 length);

    /// <summary>写入数据（字访问）</summary>
    /// <param name="address">地址字符串</param>
    /// <param name="data">写入数据</param>
    public abstract void Write(String address, Byte[] data);

    #endregion

    #region 类型化读取

    /// <summary>读取Int16值</summary>
    /// <param name="address">地址</param>
    /// <returns>Int16值</returns>
    public Int16 ReadInt16(String address)
    {
        var data = Read(address, 1);
        return Transform.TransInt16(data, 0);
    }

    /// <summary>读取UInt16值</summary>
    /// <param name="address">地址</param>
    /// <returns>UInt16值</returns>
    public UInt16 ReadUInt16(String address)
    {
        var data = Read(address, 1);
        return Transform.TransUInt16(data, 0);
    }

    /// <summary>读取Int32值</summary>
    /// <param name="address">地址</param>
    /// <returns>Int32值</returns>
    public Int32 ReadInt32(String address)
    {
        var data = Read(address, 2);
        return Transform.TransInt32(data, 0);
    }

    /// <summary>读取UInt32值</summary>
    /// <param name="address">地址</param>
    /// <returns>UInt32值</returns>
    public UInt32 ReadUInt32(String address)
    {
        var data = Read(address, 2);
        return Transform.TransUInt32(data, 0);
    }

    /// <summary>读取Int64值</summary>
    /// <param name="address">地址</param>
    /// <returns>Int64值</returns>
    public Int64 ReadInt64(String address)
    {
        var data = Read(address, 4);
        return Transform.TransInt64(data, 0);
    }

    /// <summary>读取UInt64值</summary>
    /// <param name="address">地址</param>
    /// <returns>UInt64值</returns>
    public UInt64 ReadUInt64(String address)
    {
        var data = Read(address, 4);
        return Transform.TransUInt64(data, 0);
    }

    /// <summary>读取Single值</summary>
    /// <param name="address">地址</param>
    /// <returns>Single值</returns>
    public Single ReadSingle(String address)
    {
        var data = Read(address, 2);
        return Transform.TransSingle(data, 0);
    }

    /// <summary>读取Double值</summary>
    /// <param name="address">地址</param>
    /// <returns>Double值</returns>
    public Double ReadDouble(String address)
    {
        var data = Read(address, 4);
        return Transform.TransDouble(data, 0);
    }

    /// <summary>读取字符串</summary>
    /// <param name="address">地址</param>
    /// <param name="length">读取字数（每字 2 字节）</param>
    /// <returns>字符串</returns>
    public String ReadString(String address, UInt16 length)
    {
        var data = Read(address, length);
        return Transform.TransString(data, 0, data.Length);
    }

    #endregion

    #region 类型化写入

    /// <summary>写入Int16值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteInt16(String address, Int16 value) => Write(address, Transform.TransByte(value));

    /// <summary>写入UInt16值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteUInt16(String address, UInt16 value) => Write(address, Transform.TransByte(value));

    /// <summary>写入Int32值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteInt32(String address, Int32 value) => Write(address, Transform.TransByte(value));

    /// <summary>写入UInt32值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteUInt32(String address, UInt32 value) => Write(address, Transform.TransByte(value));

    /// <summary>写入Int64值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteInt64(String address, Int64 value) => Write(address, Transform.TransByte(value));

    /// <summary>写入UInt64值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteUInt64(String address, UInt64 value) => Write(address, Transform.TransByte(value));

    /// <summary>写入Single值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteSingle(String address, Single value) => Write(address, Transform.TransByte(value));

    /// <summary>写入Double值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteDouble(String address, Double value) => Write(address, Transform.TransByte(value));

    /// <summary>写入字符串（ASCII编码，字对齐）</summary>
    /// <param name="address">地址</param>
    /// <param name="value">字符串</param>
    public void WriteString(String address, String value) => Write(address, Transform.TransByte(value));

    #endregion

    #region IDisposable

    /// <summary>释放资源</summary>
    public abstract void Dispose();

    #endregion
}
