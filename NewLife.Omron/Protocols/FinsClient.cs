using System;
using System.Net.Sockets;
using System.Threading;
using NewLife.IoT.ThingModels;
using NewLife.Log;

namespace NewLife.Omron.Protocols;

/// <summary>FINS/TCP 客户端</summary>
/// <remarks>
/// 实现欧姆龙 PLC 的 FINS/TCP 协议通信，支持自动握手获取节点地址、自动重连、同步 + 异步完整 API。
/// 默认字节序为 CDAB（欧姆龙 PLC 默认格式）。
///
/// 使用示例：
/// <code>
/// using var client = new FinsClient("192.168.1.10", 9600);
/// client.Connect();
/// var value = client.ReadInt32("D100");
/// client.WriteInt16("D200", 1234);
/// </code>
/// </remarks>
public partial class FinsClient : OmronClientBase
{
    #region 字段

    private TcpClient _client;
    private NetworkStream _stream;
    private Byte _serviceId;

    #endregion

    #region 属性

    /// <summary>IP 地址</summary>
    public String IpAddress { get; set; }

    /// <summary>端口。默认 9600</summary>
    public Int32 Port { get; set; } = 9600;

    /// <summary>连接超时（毫秒）。默认 2000</summary>
    public Int32 ConnectTimeOut { get; set; } = 2000;

    /// <summary>目标单元地址。默认 0（CPU 单元）</summary>
    public Byte DA2 { get; set; }

    /// <summary>数据格式（ByteOrder 的别名，用于向后兼容）</summary>
    public DataFormat DataFormat
    {
        get => (DataFormat)(Int32)ByteOrder;
        set => ByteOrder = (ByteOrder)(Int32)value;
    }

    /// <summary>源节点地址。FINS 握手后由服务端分配，只读</summary>
    public Byte SourceNodeAddress { get; private set; }

    /// <summary>服务端节点地址。FINS 握手后获取，只读</summary>
    public Byte ServerNodeAddress { get; private set; }

    /// <summary>是否开启自动重连。默认 true</summary>
    public Boolean AutoReconnect { get; set; } = true;

    /// <summary>最大重连次数。默认 3</summary>
    public Int32 MaxReconnectRetries { get; set; } = 3;

    /// <summary>是否已连接</summary>
    public Boolean IsConnected => _client?.Connected == true;

    #endregion

    #region 构造

    /// <summary>实例化 FINS/TCP 客户端</summary>
    public FinsClient() { }

    /// <summary>实例化 FINS/TCP 客户端</summary>
    /// <param name="ipAddress">IP 地址</param>
    /// <param name="port">端口，默认 9600</param>
    public FinsClient(String ipAddress, Int32 port = 9600)
    {
        IpAddress = ipAddress;
        Port = port;
    }

    #endregion

    #region 连接

    /// <summary>连接服务器（含 FINS 握手）</summary>
    public void Connect()
    {
        if (_client?.Connected == true) return;

        try
        {
            _client = new TcpClient
            {
                ReceiveTimeout = ReceiveTimeOut,
                SendTimeout = ConnectTimeOut
            };

            var result = _client.BeginConnect(IpAddress, Port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(ConnectTimeOut));

            if (!success)
            {
                _client?.Close();
                throw new TimeoutException($"连接超时: {IpAddress}:{Port}");
            }

            _client.EndConnect(result);
            _stream = _client.GetStream();

            PerformHandshake();

            XTrace.WriteLine($"FINS连接成功: {IpAddress}:{Port}, 源节点地址: {SourceNodeAddress}");
        }
        catch (Exception ex)
        {
            _client?.Close();
            _client = null;
            throw new InvalidOperationException($"连接失败: {ex.Message}", ex);
        }
    }

    /// <summary>执行 FINS/TCP 握手，获取客户端/服务端节点地址</summary>
    private void PerformHandshake()
    {
        // FINS/TCP 握手请求（20 字节）：FINS(4) + Length(4) + Command(4) + Error(4) + NodeData(4)
        // Length = 12：表示后续 12 字节（Command + Error + NodeData）
        var handshake = new Byte[]
        {
            0x46, 0x49, 0x4E, 0x53,  // "FINS" 魔术字
            0x00, 0x00, 0x00, 0x0C,  // 数据长度 = 12（Command + Error + NodeData）
            0x00, 0x00, 0x00, 0x00,  // 命令码 = 0（握手）
            0x00, 0x00, 0x00, 0x00,  // 错误码 = 0
            0x00, 0x00, 0x00, 0x00   // 客户端节点数据（全 0，由服务端分配）
        };

        _stream.Write(handshake, 0, handshake.Length);
        _stream.Flush();

        // 握手响应（24 字节）：FINS(4) + Length(4) + Command(4) + Error(4) + NodeData(8)
        // response[19] = 分配的客户端节点地址，response[23] = 服务端节点地址
        var response = new Byte[24];
        ReadExactly(response, 0, 24);

        if (response[0] != 0x46 || response[1] != 0x49 || response[2] != 0x4E || response[3] != 0x53)
            throw new InvalidOperationException("FINS握手响应格式错误：FINS标识不匹配");

        var errorCode = (response[12] << 24) | (response[13] << 16) | (response[14] << 8) | response[15];
        if (errorCode != 0)
            throw new InvalidOperationException($"FINS握手失败，错误代码: 0x{errorCode:X8}");

        SourceNodeAddress = response[19];
        ServerNodeAddress = response[23];
        XTrace.WriteLine($"FINS握手成功, 客户端节点地址: {SourceNodeAddress}, 服务器节点地址: {ServerNodeAddress}");
    }

    /// <summary>关闭连接</summary>
    public void Close()
    {
        try
        {
            _stream?.Close();
            _client?.Close();
        }
        catch { }
        finally
        {
            _stream = null;
            _client = null;
        }
    }

    /// <summary>释放资源</summary>
    public override void Dispose() => Close();

    #endregion

    #region 存储区操作

    /// <summary>读取数据（字访问）</summary>
    /// <param name="address">地址字符串，如 D100、CIO200、W100</param>
    /// <param name="length">读取字数</param>
    /// <returns>读取到的字节数组</returns>
    public override Byte[] Read(String address, UInt16 length)
    {
        var addr = FinsAddress.Parse(address);
        var request = FinsMessage.BuildReadRequest(addr, length, DA2);
        var response = SendCommand(request, "读取失败");
        return response.Data ?? new Byte[0];
    }

    /// <summary>写入数据（字访问）</summary>
    /// <param name="address">地址字符串，如 D100、CIO200、W100</param>
    /// <param name="data">写入数据（字节数必须为偶数）</param>
    public override void Write(String address, Byte[] data)
    {
        var addr = FinsAddress.Parse(address);
        var request = FinsMessage.BuildWriteRequest(addr, data, DA2);
        SendCommand(request, "写入失败");
    }

    /// <summary>读取位数据</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5、D50.3</param>
    /// <param name="length">读取位数</param>
    /// <returns>位值数组（每个字节为 0 或 1）</returns>
    public Byte[] ReadBit(String address, UInt16 length = 1)
    {
        var addr = FinsAddress.Parse(address);
        addr.IsBit = true;
        var request = FinsMessage.BuildBitReadRequest(addr, length, DA2);
        var response = SendCommand(request, "读取位失败");
        return response.Data ?? new Byte[0];
    }

    /// <summary>写入位数据</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5、D50.3</param>
    /// <param name="values">位值数组（每个字节为 0 或 1）</param>
    public void WriteBit(String address, Byte[] values)
    {
        var addr = FinsAddress.Parse(address);
        addr.IsBit = true;
        var request = FinsMessage.BuildBitWriteRequest(addr, values, DA2);
        SendCommand(request, "写入位失败");
    }

    /// <summary>读取单个布尔值（位访问）</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <returns>布尔值</returns>
    public Boolean ReadBool(String address)
    {
        var data = ReadBit(address, 1);
        return data.Length > 0 && data[0] != 0;
    }

    /// <summary>写入单个布尔值（位访问）</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <param name="value">布尔值</param>
    public void WriteBool(String address, Boolean value) =>
        WriteBit(address, [value ? (Byte)1 : (Byte)0]);

    /// <summary>读取 Float 值（ReadSingle 的别名）</summary>
    /// <param name="address">地址字符串</param>
    /// <returns>Single 值</returns>
    public Single ReadFloat(String address) => ReadSingle(address);

    /// <summary>写入 Float 值（WriteSingle 的别名）</summary>
    /// <param name="address">地址字符串</param>
    /// <param name="value">值</param>
    public void WriteFloat(String address, Single value) => WriteSingle(address, value);

    /// <summary>存储区填充</summary>
    /// <param name="address">起始地址</param>
    /// <param name="length">填充字数</param>
    /// <param name="fillValue">填充值（2 字节）</param>
    public void Fill(String address, UInt16 length, UInt16 fillValue)
    {
        var addr = FinsAddress.Parse(address);
        var request = FinsMessage.BuildFillRequest(addr, length, fillValue, DA2);
        SendCommand(request, "填充失败");
    }

    /// <summary>多区域读取（一次请求读取多个不连续地址）</summary>
    /// <param name="addresses">地址字符串数组</param>
    /// <returns>各地址数据拼接的字节数组（AreaCode(1)+Data(2) 每组）</returns>
    public Byte[] MultipleRead(String[] addresses)
    {
        if (addresses == null) throw new ArgumentNullException(nameof(addresses));
        var addrs = new FinsAddress[addresses.Length];
        for (var i = 0; i < addresses.Length; i++)
            addrs[i] = FinsAddress.Parse(addresses[i]);

        var request = FinsMessage.BuildMultipleReadRequest(addrs, DA2);
        var response = SendCommand(request, "多区域读取失败");
        return response.Data ?? new Byte[0];
    }

    /// <summary>存储区传送（PLC 内部数据复制）</summary>
    /// <param name="source">源地址</param>
    /// <param name="destination">目标地址</param>
    /// <param name="length">传送字数</param>
    public void Transfer(String source, String destination, UInt16 length)
    {
        var srcAddr = FinsAddress.Parse(source);
        var dstAddr = FinsAddress.Parse(destination);
        var request = FinsMessage.BuildTransferRequest(srcAddr, dstAddr, length, DA2);
        SendCommand(request, "存储区传送失败");
    }

    /// <summary>批量读取（自动合并相邻地址以减少通信次数）</summary>
    /// <param name="addresses">地址字符串数组</param>
    /// <param name="lengths">每个地址对应的读取字数</param>
    /// <returns>每个地址对应的字节数组</returns>
    public Byte[][] BatchRead(String[] addresses, UInt16[] lengths)
    {
        if (addresses == null) throw new ArgumentNullException(nameof(addresses));
        if (lengths == null) throw new ArgumentNullException(nameof(lengths));
        if (addresses.Length != lengths.Length)
            throw new ArgumentException("地址数组与长度数组元素数量不一致");

        var items = new (FinsAddress Addr, UInt16 Length, Int32 Index)[addresses.Length];
        for (var i = 0; i < addresses.Length; i++)
            items[i] = (FinsAddress.Parse(addresses[i]), lengths[i], i);

        // 按存储区类型和地址排序，以便合并相邻地址
        Array.Sort(items, (a, b) =>
        {
            var cmp = a.Addr.MemoryType.CompareTo(b.Addr.MemoryType);
            return cmp != 0 ? cmp : a.Addr.Address.CompareTo(b.Addr.Address);
        });

        var results = new Byte[addresses.Length][];
        var i2 = 0;

        while (i2 < items.Length)
        {
            var start = items[i2];
            var mergedEnd = (UInt16)(start.Addr.Address + start.Length);
            var lastIdx = i2;

            // 合并相邻地址（同区域且地址连续）
            for (var j = i2 + 1; j < items.Length; j++)
            {
                if (items[j].Addr.MemoryType != start.Addr.MemoryType) break;
                if (items[j].Addr.Address > mergedEnd) break;
                var newEnd = (UInt16)(items[j].Addr.Address + items[j].Length);
                if (newEnd > mergedEnd) mergedEnd = newEnd;
                lastIdx = j;
            }

            var totalWords = (UInt16)(mergedEnd - start.Addr.Address);
            var allData = Read(start.Addr.ToString(), totalWords);

            for (var j = i2; j <= lastIdx; j++)
            {
                var offset = (items[j].Addr.Address - start.Addr.Address) * 2;
                var len = items[j].Length * 2;
                var buf = new Byte[len];
                if (offset + len <= allData.Length)
                    Array.Copy(allData, offset, buf, 0, len);
                results[items[j].Index] = buf;
            }

            i2 = lastIdx + 1;
        }

        return results;
    }

    #endregion

    #region CPU 操作

    /// <summary>启动 PLC 运行</summary>
    /// <param name="mode">运行模式。0x04=RUN（默认），0x02=MONITOR</param>
    public void PlcRun(Byte mode = 0x04)
    {
        var request = FinsMessage.BuildRunRequest(mode, DA2);
        SendCommand(request, "PLC运行命令失败");
    }

    /// <summary>停止 PLC（切换到 PROGRAM 模式）</summary>
    public void PlcStop()
    {
        var request = FinsMessage.BuildStopRequest(DA2);
        SendCommand(request, "PLC停止命令失败");
    }

    /// <summary>读取 CPU 单元数据（型号、版本等）</summary>
    /// <returns>CPU 单元数据</returns>
    public CpuUnitData ReadCpuUnitData()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.ControllerDataRead, null, DA2);
        var response = SendCommand(request, "读取CPU数据失败");
        return CpuUnitData.Parse(response.Data);
    }

    /// <summary>读取 CPU 单元状态（运行模式、错误标志等）</summary>
    /// <returns>CPU 单元状态</returns>
    public CpuUnitStatus ReadCpuUnitStatus()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.ControllerStatusRead, null, DA2);
        var response = SendCommand(request, "读取CPU状态失败");
        return CpuUnitStatus.Parse(response.Data);
    }

    /// <summary>读取扫描周期（平均/最大/最小，单位 0.1 ms）</summary>
    /// <returns>（平均, 最大, 最小）扫描周期元组</returns>
    public (UInt32 Average, UInt32 Max, UInt32 Min) ReadCycleTime()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.CycleTimeRead, null, DA2);
        var response = SendCommand(request, "读取扫描周期失败");
        var data = response.Data;
        if (data == null || data.Length < 12)
            throw new InvalidOperationException("扫描周期数据不足");

        var avg = ((UInt32)data[0] << 24) | ((UInt32)data[1] << 16) | ((UInt32)data[2] << 8) | data[3];
        var max = ((UInt32)data[4] << 24) | ((UInt32)data[5] << 16) | ((UInt32)data[6] << 8) | data[7];
        var min = ((UInt32)data[8] << 24) | ((UInt32)data[9] << 16) | ((UInt32)data[10] << 8) | data[11];
        return (avg, max, min);
    }

    #endregion

    #region 时钟操作

    /// <summary>读取 PLC 时钟</summary>
    /// <returns>PLC 当前时间</returns>
    public DateTime ReadClock()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.ClockRead, null, DA2);
        var response = SendCommand(request, "读取时钟失败");
        var data = response.Data;
        if (data == null || data.Length < 7)
            throw new InvalidOperationException("时钟数据不足");

        var year = FinsMessage.FromBcd(data[0]) + 2000;
        return new DateTime(year,
            FinsMessage.FromBcd(data[1]),
            FinsMessage.FromBcd(data[2]),
            FinsMessage.FromBcd(data[3]),
            FinsMessage.FromBcd(data[4]),
            FinsMessage.FromBcd(data[5]));
    }

    /// <summary>写入 PLC 时钟</summary>
    /// <param name="dateTime">要设置的时间</param>
    public void WriteClock(DateTime dateTime)
    {
        var request = FinsMessage.BuildClockWriteRequest(dateTime, DA2);
        SendCommand(request, "写入时钟失败");
    }

    #endregion

    #region 错误操作

    /// <summary>清除 PLC 错误</summary>
    /// <param name="errorCode">要清除的错误代码</param>
    public void ClearError(UInt16 errorCode)
    {
        var data = new Byte[] { (Byte)(errorCode >> 8), (Byte)(errorCode & 0xFF) };
        var request = FinsMessage.BuildCommandRequest(FinsCommand.ErrorClear, data, DA2);
        SendCommand(request, "清除错误失败");
    }

    /// <summary>清除错误日志</summary>
    public void ClearErrorLog()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.ErrorLogClear, null, DA2);
        SendCommand(request, "清除错误日志失败");
    }

    #endregion

    #region 辅助方法

    /// <summary>发送 FINS 命令并验证响应结果（含自动重连逻辑）</summary>
    /// <param name="request">请求消息</param>
    /// <param name="errorPrefix">错误前缀描述</param>
    /// <returns>响应消息</returns>
    private FinsMessage SendCommand(FinsMessage request, String errorPrefix)
    {
        lock (_lock)
        {
            if (!IsConnected)
            {
                if (!AutoReconnect)
                    throw new InvalidOperationException("未连接到服务器");
                Reconnect();
            }

            for (var retry = 0; retry <= (AutoReconnect ? MaxReconnectRetries : 0); retry++)
            {
                // 重试时先重连
                if (retry > 0)
                {
                    try
                    {
                        Thread.Sleep(1000 * retry);
                        Reconnect();
                    }
                    catch (Exception ex2)
                    {
                        XTrace.WriteLine($"重连失败: {ex2.Message}");
                        if (retry >= MaxReconnectRetries) throw;
                        continue;
                    }
                }

                // 填充路由头
                request.Header.SA1 = SourceNodeAddress;
                request.Header.DA1 = ServerNodeAddress;
                request.Header.SID = GetNextServiceId();

                try
                {
                    var response = SendAndReceive(request);
                    if (!response.IsSuccess)
                        throw new InvalidOperationException($"{errorPrefix}: {response.GetErrorMessage()}");
                    return response;
                }
                catch (Exception ex) when (retry < MaxReconnectRetries && AutoReconnect && IsConnectionError(ex))
                {
                    XTrace.WriteLine($"FINS通信异常（第{retry + 1}次），准备重连: {ex.Message}");
                    try { Close(); } catch { }
                }
            }

            throw new InvalidOperationException($"{errorPrefix}: 已超过最大重试次数");
        }
    }

    /// <summary>重连服务器</summary>
    private void Reconnect()
    {
        Close();
        Connect();
    }

    /// <summary>发送 FINS 请求并接收响应（底层实现，不含重连逻辑）</summary>
    /// <param name="request">请求消息</param>
    /// <returns>响应消息</returns>
    private FinsMessage SendAndReceive(FinsMessage request)
    {
        // 构建 FINS/TCP 帧：FINS(4) + Length(4) + Command(4) + Error(4) + FINSFrame(N)
        // Length = 8 + N（Command + Error + FINSFrame）
        var finsFrame = request.ToBytes();
        var tcpLength = 8 + finsFrame.Length;

        var header = new Byte[16];
        header[0] = 0x46; header[1] = 0x49; header[2] = 0x4E; header[3] = 0x53; // "FINS"
        header[4] = (Byte)((tcpLength >> 24) & 0xFF);
        header[5] = (Byte)((tcpLength >> 16) & 0xFF);
        header[6] = (Byte)((tcpLength >> 8) & 0xFF);
        header[7] = (Byte)(tcpLength & 0xFF);
        // Command = 2（FINS 数据帧）
        header[8] = 0x00; header[9] = 0x00; header[10] = 0x00; header[11] = 0x02;
        // Error = 0
        header[12] = 0x00; header[13] = 0x00; header[14] = 0x00; header[15] = 0x00;

        _stream.Write(header, 0, header.Length);
        _stream.Write(finsFrame, 0, finsFrame.Length);
        _stream.Flush();

        // 接收响应头（16 字节）
        var responseHeader = new Byte[16];
        ReadExactly(responseHeader, 0, 16);

        if (responseHeader[0] != 0x46 || responseHeader[1] != 0x49 ||
            responseHeader[2] != 0x4E || responseHeader[3] != 0x53)
            throw new InvalidOperationException("响应头格式错误：FINS标识不匹配");

        var tcpErrorCode = (responseHeader[12] << 24) | (responseHeader[13] << 16) |
                           (responseHeader[14] << 8) | responseHeader[15];
        if (tcpErrorCode != 0)
            throw new InvalidOperationException($"FINS/TCP层错误: 0x{tcpErrorCode:X8}");

        // Length 字段 = 8 + FINS响应帧长度，FINS帧长度 = Length - 8
        var responseLength = (responseHeader[4] << 24) | (responseHeader[5] << 16) |
                             (responseHeader[6] << 8) | responseHeader[7];
        var finsDataLength = responseLength - 8;
        if (finsDataLength <= 0)
            throw new InvalidOperationException("响应中无FINS帧数据");

        var responseData = new Byte[finsDataLength];
        ReadExactly(responseData, 0, finsDataLength);

        return FinsMessage.ParseResponse(responseData);
    }

    /// <summary>精确读取指定字节数，阻塞直到读完或超时</summary>
    /// <param name="buffer">缓冲区</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="count">要读取的字节数</param>
    private void ReadExactly(Byte[] buffer, Int32 offset, Int32 count)
    {
        var readCount = 0;
        var startTime = DateTime.Now;

        while (readCount < count)
        {
            if ((DateTime.Now - startTime).TotalMilliseconds > ReceiveTimeOut)
                throw new TimeoutException("接收数据超时");

            if (_stream.DataAvailable)
            {
                var n = _stream.Read(buffer, offset + readCount, count - readCount);
                if (n <= 0) throw new InvalidOperationException("连接已关闭");
                readCount += n;
            }
            else
                Thread.Sleep(5);
        }
    }

    /// <summary>获取下一个服务 ID（自动循环）</summary>
    private Byte GetNextServiceId() => ++_serviceId;

    /// <summary>判断异常是否为网络连接错误（可触发自动重连）</summary>
    /// <param name="ex">异常</param>
    /// <returns>是否为连接错误</returns>
    private static Boolean IsConnectionError(Exception ex) =>
        ex is SocketException || ex is System.IO.IOException || ex is TimeoutException;

    #endregion
}
