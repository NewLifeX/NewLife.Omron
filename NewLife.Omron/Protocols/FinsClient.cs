using System;
using System.Net.Sockets;
using System.Threading;
using NewLife.IoT.ThingModels;
using NewLife.Log;

namespace NewLife.Omron.Protocols;

/// <summary>FINS/TCP客户端</summary>
/// <remarks>
/// 实现欧姆龙PLC的FINS/TCP协议通信。
/// FINS/TCP帧格式: FINS(4) + Length(4) + Command(4) + ErrorCode(4) + FinsFrame(N)
/// </remarks>
public partial class FinsClient : IDisposable
{
    #region 属性

    private TcpClient _client;
    private NetworkStream _stream;
    private readonly Object _lock = new();
    private Byte _serviceId;

    /// <summary>IP地址</summary>
    public String IpAddress { get; set; }

    /// <summary>端口</summary>
    public Int32 Port { get; set; } = 9600;

    /// <summary>连接超时(毫秒)</summary>
    public Int32 ConnectTimeOut { get; set; } = 2000;

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

    /// <summary>源节点地址 (FINS握手后获取)</summary>
    public Byte SourceNodeAddress { get; private set; }

    /// <summary>服务端节点地址</summary>
    public Byte ServerNodeAddress { get; private set; }

    /// <summary>是否已连接</summary>
    public Boolean IsConnected => _client?.Connected == true;

    /// <summary>是否启用自动重连。默认true</summary>
    public Boolean AutoReconnect { get; set; } = true;

    /// <summary>重连最大重试次数。默认3</summary>
    public Int32 MaxReconnectRetries { get; set; } = 3;

    /// <summary>字节转换器</summary>
    public ByteTransform Transform { get; set; }

    #endregion

    #region 构造

    /// <summary>实例化FINS/TCP客户端</summary>
    public FinsClient()
    {
        Transform = new ByteTransform { ByteOrder = ByteOrder.CDAB };
    }

    /// <summary>实例化FINS/TCP客户端</summary>
    /// <param name="ipAddress">IP地址</param>
    /// <param name="port">端口，默认9600</param>
    public FinsClient(String ipAddress, Int32 port = 9600) : this()
    {
        IpAddress = ipAddress;
        Port = port;
    }

    #endregion

    #region 连接

    /// <summary>连接服务器</summary>
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

            // 连接到服务器
            var result = _client.BeginConnect(IpAddress, Port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(ConnectTimeOut));

            if (!success)
            {
                _client?.Close();
                throw new TimeoutException($"连接超时: {IpAddress}:{Port}");
            }

            _client.EndConnect(result);
            _stream = _client.GetStream();

            // 执行FINS握手
            PerformHandshake();

            // 同步字节转换器的字节序
            Transform.ByteOrder = ByteOrder;

            XTrace.WriteLine($"FINS连接成功: {IpAddress}:{Port}, 源节点地址: {SourceNodeAddress}");
        }
        catch (Exception ex)
        {
            _client?.Close();
            _client = null;
            throw new InvalidOperationException($"连接失败: {ex.Message}", ex);
        }
    }

    /// <summary>执行FINS握手</summary>
    private void PerformHandshake()
    {
        // FINS/TCP握手请求:
        // FINS(4) + Length(4)=0x0C + Command(4)=0x00000000 + ErrorCode(4)=0x00000000
        var handshake = new Byte[]
        {
            0x46, 0x49, 0x4E, 0x53,  // "FINS"
            0x00, 0x00, 0x00, 0x0C,  // 长度: 12字节 (Command + ErrorCode + ClientNode)
            0x00, 0x00, 0x00, 0x00,  // 命令: 客户端节点地址数据发送
            0x00, 0x00, 0x00, 0x00,  // 错误代码: 正常
            0x00, 0x00, 0x00, 0x00   // 客户端节点地址: 0=自动分配
        };

        _stream.Write(handshake, 0, handshake.Length);

        // 接收握手响应 (24字节)
        var response = new Byte[24];
        var readCount = 0;
        var startTime = DateTime.Now;

        while (readCount < 24)
        {
            if ((DateTime.Now - startTime).TotalMilliseconds > ConnectTimeOut)
                throw new TimeoutException("FINS握手超时");

            if (_stream.DataAvailable)
            {
                var count = _stream.Read(response, readCount, 24 - readCount);
                readCount += count;
            }
            else
            {
                Thread.Sleep(10);
            }
        }

        // 验证响应
        if (response[0] != 0x46 || response[1] != 0x49 || response[2] != 0x4E || response[3] != 0x53)
            throw new InvalidOperationException("FINS握手响应格式错误");

        // 检查错误代码
        var errorCode = (response[12] << 24) | (response[13] << 16) | (response[14] << 8) | response[15];
        if (errorCode != 0)
            throw new InvalidOperationException($"FINS握手失败，错误代码: 0x{errorCode:X8}");

        // 提取节点地址
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
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取失败: {response.GetErrorMessage()}");

            return response.Data ?? [];
        }
    }

    /// <summary>写入数据（字访问）</summary>
    /// <param name="address">地址字符串，如 D100, CIO200</param>
    /// <param name="data">写入数据（字节数组，长度必须为偶数）</param>
    public void Write(String address, Byte[] data)
    {
        lock (_lock)
        {
            var addr = FinsAddress.Parse(address);
            var request = FinsMessage.BuildWriteRequest(addr, data, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"写入失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>读取位数据</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <param name="length">读取位数</param>
    /// <returns>位值数组（每个元素0或1）</returns>
    public Byte[] ReadBit(String address, UInt16 length = 1)
    {
        lock (_lock)
        {
            var addr = FinsAddress.Parse(address);
            addr.IsBit = true;
            var request = FinsMessage.BuildBitReadRequest(addr, length, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取位失败: {response.GetErrorMessage()}");

            return response.Data ?? [];
        }
    }

    /// <summary>写入位数据</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <param name="values">位值数组（每个元素0或1）</param>
    public void WriteBit(String address, Byte[] values)
    {
        lock (_lock)
        {
            var addr = FinsAddress.Parse(address);
            addr.IsBit = true;
            var request = FinsMessage.BuildBitWriteRequest(addr, values, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"写入位失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>写入单个布尔值</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <param name="value">布尔值</param>
    public void WriteBool(String address, Boolean value) => WriteBit(address, [value ? (Byte)1 : (Byte)0]);

    /// <summary>读取单个布尔值</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <returns>布尔值</returns>
    public Boolean ReadBool(String address)
    {
        var data = ReadBit(address, 1);
        return data.Length > 0 && data[0] != 0;
    }

    /// <summary>存储区填充</summary>
    /// <param name="address">起始地址</param>
    /// <param name="length">填充字数</param>
    /// <param name="fillValue">填充值</param>
    public void Fill(String address, UInt16 length, UInt16 fillValue)
    {
        lock (_lock)
        {
            var addr = FinsAddress.Parse(address);
            var request = FinsMessage.BuildFillRequest(addr, length, fillValue, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"填充失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>多区域读取</summary>
    /// <param name="addresses">地址字符串数组</param>
    /// <returns>每个地址对应的字数据（各2字节）</returns>
    public Byte[] MultipleRead(String[] addresses)
    {
        lock (_lock)
        {
            var addrs = new FinsAddress[addresses.Length];
            for (var i = 0; i < addresses.Length; i++)
            {
                addrs[i] = FinsAddress.Parse(addresses[i]);
            }

            var request = FinsMessage.BuildMultipleReadRequest(addrs, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"多区域读取失败: {response.GetErrorMessage()}");

            return response.Data ?? [];
        }
    }

    /// <summary>存储区传送</summary>
    /// <param name="source">源地址</param>
    /// <param name="destination">目标地址</param>
    /// <param name="length">传送字数</param>
    public void Transfer(String source, String destination, UInt16 length)
    {
        lock (_lock)
        {
            var srcAddr = FinsAddress.Parse(source);
            var dstAddr = FinsAddress.Parse(destination);
            var request = FinsMessage.BuildTransferRequest(srcAddr, dstAddr, length, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"存储区传送失败: {response.GetErrorMessage()}");
        }
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

        // 解析所有地址
        var items = new (FinsAddress Addr, UInt16 Length, Int32 Index)[addresses.Length];
        for (var i = 0; i < addresses.Length; i++)
        {
            items[i] = (FinsAddress.Parse(addresses[i]), lengths[i], i);
        }

        // 按存储区类型和地址排序，便于合并
        Array.Sort(items, (a, b) =>
        {
            var cmp = a.Addr.MemoryType.CompareTo(b.Addr.MemoryType);
            return cmp != 0 ? cmp : a.Addr.Address.CompareTo(b.Addr.Address);
        });

        var results = new Byte[addresses.Length][];

        // 合并连续地址段
        var i2 = 0;
        while (i2 < items.Length)
        {
            var start = items[i2];
            var mergedEnd = (UInt16)(start.Addr.Address + start.Length);
            var lastIdx = i2;

            // 尝试向后合并：同区域且地址连续或重叠
            for (var j = i2 + 1; j < items.Length; j++)
            {
                if (items[j].Addr.MemoryType != start.Addr.MemoryType) break;
                if (items[j].Addr.Address > mergedEnd) break;

                var newEnd = (UInt16)(items[j].Addr.Address + items[j].Length);
                if (newEnd > mergedEnd) mergedEnd = newEnd;
                lastIdx = j;
            }

            // 读取合并后的区域
            var totalWords = (UInt16)(mergedEnd - start.Addr.Address);
            var allData = Read(items[i2].Addr.ToString(), totalWords);

            // 拆分结果到各原始地址
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

    #region 设备操作

    /// <summary>启动PLC运行</summary>
    /// <param name="mode">运行模式。0x04=RUN, 0x02=MONITOR, 默认RUN</param>
    public void PlcRun(Byte mode = 0x04)
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildRunRequest(mode, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"PLC运行命令失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>停止PLC</summary>
    public void PlcStop()
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildStopRequest(DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"PLC停止命令失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>读取CPU单元数据（型号、版本等）</summary>
    /// <returns>CPU单元数据</returns>
    public CpuUnitData ReadCpuUnitData()
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildCommandRequest(FinsCommand.ControllerDataRead, null, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取CPU数据失败: {response.GetErrorMessage()}");

            return CpuUnitData.Parse(response.Data);
        }
    }

    /// <summary>读取CPU单元状态（运行模式、错误状态等）</summary>
    /// <returns>CPU单元状态</returns>
    public CpuUnitStatus ReadCpuUnitStatus()
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildCommandRequest(FinsCommand.ControllerStatusRead, null, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取CPU状态失败: {response.GetErrorMessage()}");

            return CpuUnitStatus.Parse(response.Data);
        }
    }

    /// <summary>读取PLC时钟</summary>
    /// <returns>PLC当前时间</returns>
    public DateTime ReadClock()
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildCommandRequest(FinsCommand.ClockRead, null, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取时钟失败: {response.GetErrorMessage()}");

            var data = response.Data;
            if (data == null || data.Length < 7)
                throw new InvalidOperationException("时钟数据长度不足");

            // BCD解码
            var year = 2000 + FinsMessage.FromBcd(data[0]);
            var month = FinsMessage.FromBcd(data[1]);
            var day = FinsMessage.FromBcd(data[2]);
            var hour = FinsMessage.FromBcd(data[3]);
            var minute = FinsMessage.FromBcd(data[4]);
            var second = FinsMessage.FromBcd(data[5]);

            return new DateTime(year, month, day, hour, minute, second);
        }
    }

    /// <summary>设置PLC时钟</summary>
    /// <param name="dateTime">要设置的时间</param>
    public void WriteClock(DateTime dateTime)
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildClockWriteRequest(dateTime, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"设置时钟失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>读取扫描周期</summary>
    /// <returns>返回3个值：平均扫描周期、最大扫描周期、最小扫描周期（单位：0.1ms）</returns>
    public (UInt32 Average, UInt32 Max, UInt32 Min) ReadCycleTime()
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildCommandRequest(FinsCommand.CycleTimeRead, null, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取扫描周期失败: {response.GetErrorMessage()}");

            var data = response.Data;
            if (data == null || data.Length < 12)
                throw new InvalidOperationException("扫描周期数据长度不足");

            var avg = (UInt32)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
            var max = (UInt32)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);
            var min = (UInt32)((data[8] << 24) | (data[9] << 16) | (data[10] << 8) | data[11]);

            return (avg, max, min);
        }
    }

    /// <summary>清除PLC错误</summary>
    /// <param name="errorCode">要清除的错误代码</param>
    public void ClearError(UInt16 errorCode)
    {
        lock (_lock)
        {
            var data = new Byte[] { (Byte)(errorCode >> 8), (Byte)(errorCode & 0xFF) };
            var request = FinsMessage.BuildCommandRequest(FinsCommand.ErrorClear, data, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"清除错误失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>读取错误日志</summary>
    /// <param name="startRecord">起始记录号</param>
    /// <param name="count">读取记录数</param>
    /// <returns>错误日志数据</returns>
    public Byte[] ReadErrorLog(UInt16 startRecord, UInt16 count)
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildErrorLogReadRequest(startRecord, count, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取错误日志失败: {response.GetErrorMessage()}");

            return response.Data ?? [];
        }
    }

    /// <summary>清除错误日志</summary>
    public void ClearErrorLog()
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildCommandRequest(FinsCommand.ErrorLogClear, null, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"清除错误日志失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>读取参数区</summary>
    /// <param name="areaCode">参数区代码</param>
    /// <param name="beginWord">起始字</param>
    /// <param name="count">读取字数</param>
    /// <returns>参数数据</returns>
    public Byte[] ReadParameterArea(UInt16 areaCode, UInt16 beginWord, UInt16 count)
    {
        lock (_lock)
        {
            var data = new Byte[6];
            data[0] = (Byte)(areaCode >> 8);
            data[1] = (Byte)(areaCode & 0xFF);
            data[2] = (Byte)(beginWord >> 8);
            data[3] = (Byte)(beginWord & 0xFF);
            data[4] = (Byte)(count >> 8);
            data[5] = (Byte)(count & 0xFF);

            var request = FinsMessage.BuildCommandRequest(FinsCommand.ParameterAreaRead, data, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取参数区失败: {response.GetErrorMessage()}");

            return response.Data ?? [];
        }
    }

    /// <summary>写入参数区</summary>
    /// <param name="areaCode">参数区代码</param>
    /// <param name="beginWord">起始字</param>
    /// <param name="writeData">写入数据</param>
    public void WriteParameterArea(UInt16 areaCode, UInt16 beginWord, Byte[] writeData)
    {
        if (writeData == null) throw new ArgumentNullException(nameof(writeData));

        lock (_lock)
        {
            var count = (UInt16)(writeData.Length / 2);
            var data = new Byte[6 + writeData.Length];
            data[0] = (Byte)(areaCode >> 8);
            data[1] = (Byte)(areaCode & 0xFF);
            data[2] = (Byte)(beginWord >> 8);
            data[3] = (Byte)(beginWord & 0xFF);
            data[4] = (Byte)(count >> 8);
            data[5] = (Byte)(count & 0xFF);
            Array.Copy(writeData, 0, data, 6, writeData.Length);

            var request = FinsMessage.BuildCommandRequest(FinsCommand.ParameterAreaWrite, data, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"写入参数区失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>清除参数区</summary>
    /// <param name="areaCode">参数区代码</param>
    public void ClearParameterArea(UInt16 areaCode)
    {
        lock (_lock)
        {
            var data = new Byte[] { (Byte)(areaCode >> 8), (Byte)(areaCode & 0xFF) };
            var request = FinsMessage.BuildCommandRequest(FinsCommand.ParameterAreaClear, data, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"清除参数区失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>读取程序区</summary>
    /// <param name="programNo">程序号</param>
    /// <param name="beginWord">起始字</param>
    /// <param name="count">读取字数</param>
    /// <returns>程序数据</returns>
    public Byte[] ReadProgramArea(UInt16 programNo, UInt32 beginWord, UInt16 count)
    {
        lock (_lock)
        {
            var data = new Byte[8];
            data[0] = (Byte)(programNo >> 8);
            data[1] = (Byte)(programNo & 0xFF);
            data[2] = (Byte)((beginWord >> 24) & 0xFF);
            data[3] = (Byte)((beginWord >> 16) & 0xFF);
            data[4] = (Byte)((beginWord >> 8) & 0xFF);
            data[5] = (Byte)(beginWord & 0xFF);
            data[6] = (Byte)(count >> 8);
            data[7] = (Byte)(count & 0xFF);

            var request = FinsMessage.BuildCommandRequest(FinsCommand.ProgramAreaRead, data, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取程序区失败: {response.GetErrorMessage()}");

            return response.Data ?? [];
        }
    }

    /// <summary>写入程序区</summary>
    /// <param name="programNo">程序号</param>
    /// <param name="beginWord">起始字</param>
    /// <param name="writeData">程序数据</param>
    public void WriteProgramArea(UInt16 programNo, UInt32 beginWord, Byte[] writeData)
    {
        if (writeData == null) throw new ArgumentNullException(nameof(writeData));

        lock (_lock)
        {
            var count = (UInt16)(writeData.Length / 2);
            var data = new Byte[8 + writeData.Length];
            data[0] = (Byte)(programNo >> 8);
            data[1] = (Byte)(programNo & 0xFF);
            data[2] = (Byte)((beginWord >> 24) & 0xFF);
            data[3] = (Byte)((beginWord >> 16) & 0xFF);
            data[4] = (Byte)((beginWord >> 8) & 0xFF);
            data[5] = (Byte)(beginWord & 0xFF);
            data[6] = (Byte)(count >> 8);
            data[7] = (Byte)(count & 0xFF);
            Array.Copy(writeData, 0, data, 8, writeData.Length);

            var request = FinsMessage.BuildCommandRequest(FinsCommand.ProgramAreaWrite, data, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"写入程序区失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>清除程序区</summary>
    /// <param name="programNo">程序号</param>
    public void ClearProgramArea(UInt16 programNo)
    {
        lock (_lock)
        {
            var data = new Byte[] { (Byte)(programNo >> 8), (Byte)(programNo & 0xFF) };
            var request = FinsMessage.BuildCommandRequest(FinsCommand.ProgramAreaClear, data, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"清除程序区失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>读取连接数据</summary>
    /// <param name="unitAddress">单元地址。0=CPU单元</param>
    /// <param name="startNo">起始编号</param>
    /// <param name="count">读取数量</param>
    /// <returns>连接数据</returns>
    public Byte[] ReadConnectionData(UInt16 unitAddress, UInt16 startNo, Byte count)
    {
        lock (_lock)
        {
            var data = new Byte[5];
            data[0] = (Byte)(unitAddress >> 8);
            data[1] = (Byte)(unitAddress & 0xFF);
            data[2] = (Byte)(startNo >> 8);
            data[3] = (Byte)(startNo & 0xFF);
            data[4] = count;

            var request = FinsMessage.BuildCommandRequest(FinsCommand.ConnectionDataRead, data, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取连接数据失败: {response.GetErrorMessage()}");

            return response.Data ?? [];
        }
    }

    /// <summary>读取网络状态</summary>
    /// <returns>网络状态数据</returns>
    public Byte[] ReadNetworkStatus()
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildCommandRequest(FinsCommand.NetworkStatusRead, null, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取网络状态失败: {response.GetErrorMessage()}");

            return response.Data ?? [];
        }
    }

    /// <summary>读取消息</summary>
    /// <returns>消息数据</returns>
    public Byte[] ReadMessage()
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildCommandRequest(FinsCommand.MessageRead, null, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"读取消息失败: {response.GetErrorMessage()}");

            return response.Data ?? [];
        }
    }

    /// <summary>获取访问权</summary>
    public void AcquireAccessRight()
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildCommandRequest(FinsCommand.AccessRightAcquire, null, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"获取访问权失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>强制获取访问权</summary>
    public void ForcedAcquireAccessRight()
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildCommandRequest(FinsCommand.AccessRightForcedAcquire, null, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"强制获取访问权失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>释放访问权</summary>
    public void ReleaseAccessRight()
    {
        lock (_lock)
        {
            var request = FinsMessage.BuildCommandRequest(FinsCommand.AccessRightRelease, null, DA2);
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = SendAndReceive(request);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"释放访问权失败: {response.GetErrorMessage()}");
        }
    }

    #endregion

    #region 方法

    /// <summary>发送FINS消息并接收响应（含自动重连）</summary>
    /// <param name="request">请求消息</param>
    /// <returns>响应消息</returns>
    private FinsMessage SendAndReceive(FinsMessage request)
    {
        // 首次检查连接状态，尝试自动重连
        if (_client?.Connected != true)
        {
            if (!AutoReconnect)
                throw new InvalidOperationException("未连接到服务器");

            Reconnect();
        }

        // 通信失败时自动重连重试
        for (var retry = 0; retry <= (AutoReconnect ? MaxReconnectRetries : 0); retry++)
        {
            if (retry > 0)
            {
                XTrace.WriteLine($"FINS通信失败，第{retry}次重连重试...");
                try
                {
                    Reconnect();
                    // 重连后需要更新请求中的节点地址
                    request.Header.SA1 = SourceNodeAddress;
                    request.Header.DA1 = ServerNodeAddress;
                }
                catch (Exception ex2)
                {
                    XTrace.WriteLine($"重连失败: {ex2.Message}");
                    if (retry >= MaxReconnectRetries) throw;
                    Thread.Sleep(1000 * retry);
                    continue;
                }
            }

            try
            {
                return SendAndReceiveCore(request);
            }
            catch (Exception ex) when (retry < MaxReconnectRetries && AutoReconnect && IsConnectionError(ex))
            {
                XTrace.WriteLine($"FINS通信异常: {ex.Message}");
                // 关闭当前连接，下次循环重连
                try { Close(); } catch { }
            }
        }

        throw new InvalidOperationException("通信失败，已超过最大重试次数");
    }

    /// <summary>判断是否为连接级别错误（需要重连）</summary>
    /// <param name="ex">异常</param>
    /// <returns>是否需要重连</returns>
    private static Boolean IsConnectionError(Exception ex) =>
        ex is System.IO.IOException ||
        ex is SocketException ||
        ex is TimeoutException ||
        (ex is InvalidOperationException ioe && (ioe.Message.Contains("连接已关闭") || ioe.Message.Contains("通信失败")));

    /// <summary>重连到服务器</summary>
    private void Reconnect()
    {
        Close();
        Connect();
    }

    /// <summary>发送FINS消息并接收响应（核心实现）</summary>
    /// <param name="request">请求消息</param>
    /// <returns>响应消息</returns>
    private FinsMessage SendAndReceiveCore(FinsMessage request)
    {
        if (_client?.Connected != true)
            throw new InvalidOperationException("未连接到服务器");

        try
        {
            // 构建 FINS/TCP 帧
            // 格式: FINS(4) + Length(4) + Command(4)=0x00000002 + ErrorCode(4)=0x00000000 + FinsFrame(N)
            var finsFrame = request.ToBytes();
            var totalLength = 8 + finsFrame.Length; // Command(4) + ErrorCode(4) + FinsFrame

            var tcpHeader = new Byte[16];
            tcpHeader[0] = 0x46; // 'F'
            tcpHeader[1] = 0x49; // 'I'
            tcpHeader[2] = 0x4E; // 'N'
            tcpHeader[3] = 0x53; // 'S'
            tcpHeader[4] = (Byte)((totalLength >> 24) & 0xFF);
            tcpHeader[5] = (Byte)((totalLength >> 16) & 0xFF);
            tcpHeader[6] = (Byte)((totalLength >> 8) & 0xFF);
            tcpHeader[7] = (Byte)(totalLength & 0xFF);
            tcpHeader[8] = 0x00;  // Command: FINS Frame Send
            tcpHeader[9] = 0x00;
            tcpHeader[10] = 0x00;
            tcpHeader[11] = 0x02;
            tcpHeader[12] = 0x00; // Error Code: Normal
            tcpHeader[13] = 0x00;
            tcpHeader[14] = 0x00;
            tcpHeader[15] = 0x00;

            // 发送数据
            _stream.Write(tcpHeader, 0, tcpHeader.Length);
            _stream.Write(finsFrame, 0, finsFrame.Length);
            _stream.Flush();

            // 接收 FINS/TCP 响应头 (16字节)
            var responseHeader = new Byte[16];
            ReadExactly(responseHeader, 0, 16);

            // 验证 FINS 标识
            if (responseHeader[0] != 0x46 || responseHeader[1] != 0x49 ||
                responseHeader[2] != 0x4E || responseHeader[3] != 0x53)
                throw new InvalidOperationException("响应头格式错误：FINS标识不匹配");

            // 获取响应总长度
            var responseLength = (responseHeader[4] << 24) | (responseHeader[5] << 16) |
                               (responseHeader[6] << 8) | responseHeader[7];

            // 检查 TCP 层错误代码
            var tcpErrorCode = (responseHeader[12] << 24) | (responseHeader[13] << 16) |
                              (responseHeader[14] << 8) | responseHeader[15];
            if (tcpErrorCode != 0)
                throw new InvalidOperationException($"FINS/TCP错误: 0x{tcpErrorCode:X8}");

            // 读取 FINS 帧数据 (totalLength - Command(4) - ErrorCode(4))
            var finsDataLength = responseLength - 8;
            if (finsDataLength <= 0)
                throw new InvalidOperationException("响应中无FINS帧数据");

            var responseData = new Byte[finsDataLength];
            ReadExactly(responseData, 0, finsDataLength);

            // 解析FINS响应
            return FinsMessage.ParseResponse(responseData);
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not TimeoutException)
        {
            throw new InvalidOperationException($"通信失败: {ex.Message}", ex);
        }
    }

    /// <summary>精确读取指定字节数</summary>
    /// <param name="buffer">缓冲区</param>
    /// <param name="offset">缓冲区偏移</param>
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
                if (n <= 0)
                    throw new InvalidOperationException("连接已关闭");
                readCount += n;
            }
            else
            {
                Thread.Sleep(10);
            }
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
