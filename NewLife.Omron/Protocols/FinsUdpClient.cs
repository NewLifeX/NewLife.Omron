using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NewLife.IoT.ThingModels;
using NewLife.Log;

namespace NewLife.Omron.Protocols;

/// <summary>FINS/UDP客户端</summary>
/// <remarks>
/// 实现欧姆龙PLC的FINS/UDP协议通信。
/// FINS/UDP与TCP的区别：
/// 1. 无需FINS/TCP握手，直接发送FINS帧
/// 2. 无FINS/TCP头部（FINS+Length+Command+ErrorCode），直接传输FINS帧
/// 3. 需要手动指定源节点和目标节点地址
/// </remarks>
public partial class FinsUdpClient : IDisposable
{
    #region 属性

    private UdpClient _client;
    private readonly Object _lock = new();
    private Byte _serviceId;

    /// <summary>IP地址</summary>
    public String IpAddress { get; set; }

    /// <summary>端口</summary>
    public Int32 Port { get; set; } = 9600;

    /// <summary>接收超时(毫秒)</summary>
    public Int32 ReceiveTimeOut { get; set; } = 5000;

    /// <summary>目标单元地址</summary>
    public Byte DA2 { get; set; }

    /// <summary>字节序</summary>
    public ByteOrder ByteOrder
    {
        get => Transform.ByteOrder;
        set => Transform.ByteOrder = value;
    }

    /// <summary>源节点地址（需手动指定，通常为IP地址最后一段）</summary>
    public Byte SourceNodeAddress { get; set; }

    /// <summary>目标节点地址（需手动指定，通常为PLC的IP地址最后一段）</summary>
    public Byte DestinationNodeAddress { get; set; }

    /// <summary>是否已打开</summary>
    public Boolean IsOpened => _client != null;

    /// <summary>字节转换器</summary>
    public ByteTransform Transform { get; set; }

    #endregion

    #region 构造

    /// <summary>实例化FINS/UDP客户端</summary>
    public FinsUdpClient()
    {
        Transform = new ByteTransform { ByteOrder = ByteOrder.CDAB };
    }

    /// <summary>实例化FINS/UDP客户端</summary>
    /// <param name="ipAddress">IP地址</param>
    /// <param name="port">端口，默认9600</param>
    public FinsUdpClient(String ipAddress, Int32 port = 9600) : this()
    {
        IpAddress = ipAddress;
        Port = port;

        // 从IP地址自动推导节点地址
        var parts = ipAddress?.Split('.');
        if (parts?.Length == 4 && Byte.TryParse(parts[3], out var lastByte))
            DestinationNodeAddress = lastByte;
    }

    #endregion

    #region 连接

    /// <summary>打开UDP通信</summary>
    public void Open()
    {
        if (_client != null) return;

        _client = new UdpClient();
        _client.Client.ReceiveTimeout = ReceiveTimeOut;
        _client.Connect(IpAddress, Port);

        // 同步字节转换器的字节序
        Transform.ByteOrder = ByteOrder;

        // 从本地端口推导源节点地址（如未手动指定）
        if (SourceNodeAddress == 0)
        {
            var localEp = _client.Client.LocalEndPoint as IPEndPoint;
            if (localEp != null)
            {
                var localParts = localEp.Address.ToString().Split('.');
                if (localParts.Length == 4 && Byte.TryParse(localParts[3], out var last))
                    SourceNodeAddress = last;
            }
        }

        XTrace.WriteLine($"FINS/UDP已打开: {IpAddress}:{Port}, 源节点: {SourceNodeAddress}, 目标节点: {DestinationNodeAddress}");
    }

    /// <summary>关闭UDP通信</summary>
    public void Close()
    {
        try
        {
            _client?.Close();
        }
        catch { }
        finally
        {
            _client = null;
        }
    }

    #endregion

    #region 存储区操作

    /// <summary>读取数据（字访问）</summary>
    /// <param name="address">地址字符串，如 D100, CIO200</param>
    /// <param name="length">读取字数</param>
    /// <returns>读取到的字节数组</returns>
    public Byte[] Read(String address, UInt16 length)
    {
        lock (_lock)
        {
            var addr = FinsAddress.Parse(address);
            var request = FinsMessage.BuildReadRequest(addr, length, DA2);
            SetupHeader(request);

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取失败: {response.GetErrorMessage()}");

            return response.Data ?? [];
        }
    }

    /// <summary>写入数据（字访问）</summary>
    /// <param name="address">地址字符串，如 D100, CIO200</param>
    /// <param name="data">写入数据</param>
    public void Write(String address, Byte[] data)
    {
        lock (_lock)
        {
            var addr = FinsAddress.Parse(address);
            var request = FinsMessage.BuildWriteRequest(addr, data, DA2);
            SetupHeader(request);

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"写入失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>读取位数据</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <param name="length">读取位数</param>
    /// <returns>位值数组</returns>
    public Byte[] ReadBit(String address, UInt16 length = 1)
    {
        lock (_lock)
        {
            var addr = FinsAddress.Parse(address);
            addr.IsBit = true;
            var request = FinsMessage.BuildBitReadRequest(addr, length, DA2);
            SetupHeader(request);

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取位失败: {response.GetErrorMessage()}");

            return response.Data ?? [];
        }
    }

    /// <summary>写入位数据</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <param name="values">位值数组</param>
    public void WriteBit(String address, Byte[] values)
    {
        lock (_lock)
        {
            var addr = FinsAddress.Parse(address);
            addr.IsBit = true;
            var request = FinsMessage.BuildBitWriteRequest(addr, values, DA2);
            SetupHeader(request);

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"写入位失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>读取单个布尔值</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <returns>布尔值</returns>
    public Boolean ReadBool(String address)
    {
        var data = ReadBit(address, 1);
        return data.Length > 0 && data[0] != 0;
    }

    /// <summary>写入单个布尔值</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <param name="value">布尔值</param>
    public void WriteBool(String address, Boolean value) => WriteBit(address, [value ? (Byte)1 : (Byte)0]);

    #endregion

    #region 类型化读写

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

    /// <summary>读取Float值</summary>
    /// <param name="address">地址</param>
    /// <returns>Single值</returns>
    public Single ReadFloat(String address)
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
    /// <param name="length">读取字数</param>
    /// <returns>字符串</returns>
    public String ReadString(String address, UInt16 length)
    {
        var data = Read(address, length);
        return Transform.TransString(data, 0, data.Length);
    }

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

    /// <summary>写入Float值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteFloat(String address, Single value) => Write(address, Transform.TransByte(value));

    /// <summary>写入Double值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public void WriteDouble(String address, Double value) => Write(address, Transform.TransByte(value));

    /// <summary>写入字符串</summary>
    /// <param name="address">地址</param>
    /// <param name="value">字符串值</param>
    public void WriteString(String address, String value) => Write(address, Transform.TransByte(value));

    #endregion

    #region 方法

    /// <summary>设置请求头部节点地址</summary>
    /// <param name="request">请求消息</param>
    private void SetupHeader(FinsMessage request)
    {
        request.Header.SA1 = SourceNodeAddress;
        request.Header.DA1 = DestinationNodeAddress;
        request.Header.SID = GetNextServiceId();
    }

    /// <summary>发送FINS消息并接收响应</summary>
    /// <param name="request">请求消息</param>
    /// <returns>响应消息</returns>
    private FinsMessage SendAndReceive(FinsMessage request)
    {
        if (_client == null) throw new InvalidOperationException("未打开UDP通信");

        try
        {
            // FINS/UDP直接发送FINS帧（无TCP头）
            var finsFrame = request.ToBytes();
            _client.Send(finsFrame, finsFrame.Length);

            // 接收响应
            var remoteEp = new IPEndPoint(System.Net.IPAddress.Any, 0);
            var responseData = _client.Receive(ref remoteEp);

            if (responseData == null || responseData.Length < 14)
                throw new InvalidOperationException("FINS/UDP响应数据不足");

            // 直接解析FINS帧（无TCP头）
            return FinsMessage.ParseResponse(responseData);
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not TimeoutException)
        {
            throw new InvalidOperationException($"FINS/UDP通信失败: {ex.Message}", ex);
        }
    }

    /// <summary>获取下一个服务ID</summary>
    private Byte GetNextServiceId() => ++_serviceId;

    #endregion

    #region 日志

    /// <summary>释放资源</summary>
    public void Dispose() => Close();

    #endregion
}
