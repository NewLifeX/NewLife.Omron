using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;

namespace NewLife.Omron.Protocols;

/// <summary>FINS/TCP客户端 - 异步方法</summary>
public partial class FinsClient
{
    private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);

    #region 异步连接

    /// <summary>异步连接服务器</summary>
    public async Task ConnectAsync()
    {
        if (_client?.Connected == true) return;

        try
        {
            _client = new TcpClient
            {
                ReceiveTimeout = ReceiveTimeOut,
                SendTimeout = ConnectTimeOut
            };

            // 异步连接（兼容 net45+）
            var connectTask = Task.Factory.FromAsync(
                _client.BeginConnect(IpAddress, Port, null, null),
                _client.EndConnect);

            var timeoutTask = Task.Delay(ConnectTimeOut);
            if (await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false) == timeoutTask)
            {
                _client?.Close();
                throw new TimeoutException($"连接超时: {IpAddress}:{Port}");
            }

            await connectTask.ConfigureAwait(false);
            _stream = _client.GetStream();

            // 执行异步FINS握手
            await PerformHandshakeAsync().ConfigureAwait(false);

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

    /// <summary>异步执行FINS握手</summary>
    private async Task PerformHandshakeAsync()
    {
        var handshake = new Byte[]
        {
            0x46, 0x49, 0x4E, 0x53,
            0x00, 0x00, 0x00, 0x0C,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        await _stream.WriteAsync(handshake, 0, handshake.Length).ConfigureAwait(false);
        await _stream.FlushAsync().ConfigureAwait(false);

        var response = new Byte[24];
        await ReadExactlyAsync(response, 0, 24).ConfigureAwait(false);

        if (response[0] != 0x46 || response[1] != 0x49 || response[2] != 0x4E || response[3] != 0x53)
            throw new InvalidOperationException("FINS握手响应格式错误");

        var errorCode = (response[12] << 24) | (response[13] << 16) | (response[14] << 8) | response[15];
        if (errorCode != 0)
            throw new InvalidOperationException($"FINS握手失败，错误代码: 0x{errorCode:X8}");

        SourceNodeAddress = response[19];
        ServerNodeAddress = response[23];
        XTrace.WriteLine($"FINS握手成功, 客户端节点地址: {SourceNodeAddress}, 服务器节点地址: {ServerNodeAddress}");
    }

    /// <summary>异步精确读取指定字节数</summary>
    /// <param name="buffer">缓冲区</param>
    /// <param name="offset">缓冲区偏移</param>
    /// <param name="count">要读取的字节数</param>
    private async Task ReadExactlyAsync(Byte[] buffer, Int32 offset, Int32 count)
    {
        var readCount = 0;
        using var cts = new CancellationTokenSource(ReceiveTimeOut);

        try
        {
            while (readCount < count)
            {
                var n = await _stream.ReadAsync(buffer, offset + readCount, count - readCount, cts.Token).ConfigureAwait(false);
                if (n <= 0) throw new InvalidOperationException("连接已关闭");
                readCount += n;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException("接收数据超时");
        }
    }

    #endregion

    #region 异步通信

    /// <summary>异步发送FINS消息并接收响应（含自动重连）</summary>
    /// <param name="request">请求消息</param>
    /// <returns>响应消息</returns>
    private async Task<FinsMessage> SendAndReceiveAsync(FinsMessage request)
    {
        if (_client?.Connected != true)
        {
            if (!AutoReconnect)
                throw new InvalidOperationException("未连接到服务器");

            await ReconnectAsync().ConfigureAwait(false);
        }

        for (var retry = 0; retry <= (AutoReconnect ? MaxReconnectRetries : 0); retry++)
        {
            if (retry > 0)
            {
                XTrace.WriteLine($"FINS通信失败，第{retry}次重连重试...");
                try
                {
                    await ReconnectAsync().ConfigureAwait(false);
                    request.Header.SA1 = SourceNodeAddress;
                    request.Header.DA1 = ServerNodeAddress;
                }
                catch (Exception ex2)
                {
                    XTrace.WriteLine($"重连失败: {ex2.Message}");
                    if (retry >= MaxReconnectRetries) throw;
                    await Task.Delay(1000 * retry).ConfigureAwait(false);
                    continue;
                }
            }

            try
            {
                return await SendAndReceiveCoreAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex) when (retry < MaxReconnectRetries && AutoReconnect && IsConnectionError(ex))
            {
                XTrace.WriteLine($"FINS通信异常: {ex.Message}");
                try { Close(); } catch { }
            }
        }

        throw new InvalidOperationException("通信失败，已超过最大重试次数");
    }

    /// <summary>异步重连到服务器</summary>
    private async Task ReconnectAsync()
    {
        Close();
        await ConnectAsync().ConfigureAwait(false);
    }

    /// <summary>异步发送FINS消息并接收响应（核心实现）</summary>
    /// <param name="request">请求消息</param>
    /// <returns>响应消息</returns>
    private async Task<FinsMessage> SendAndReceiveCoreAsync(FinsMessage request)
    {
        if (_client?.Connected != true)
            throw new InvalidOperationException("未连接到服务器");

        try
        {
            var finsFrame = request.ToBytes();
            var totalLength = 8 + finsFrame.Length;

            var tcpHeader = new Byte[16];
            tcpHeader[0] = 0x46;
            tcpHeader[1] = 0x49;
            tcpHeader[2] = 0x4E;
            tcpHeader[3] = 0x53;
            tcpHeader[4] = (Byte)((totalLength >> 24) & 0xFF);
            tcpHeader[5] = (Byte)((totalLength >> 16) & 0xFF);
            tcpHeader[6] = (Byte)((totalLength >> 8) & 0xFF);
            tcpHeader[7] = (Byte)(totalLength & 0xFF);
            tcpHeader[8] = 0x00;
            tcpHeader[9] = 0x00;
            tcpHeader[10] = 0x00;
            tcpHeader[11] = 0x02;
            tcpHeader[12] = 0x00;
            tcpHeader[13] = 0x00;
            tcpHeader[14] = 0x00;
            tcpHeader[15] = 0x00;

            await _stream.WriteAsync(tcpHeader, 0, tcpHeader.Length).ConfigureAwait(false);
            await _stream.WriteAsync(finsFrame, 0, finsFrame.Length).ConfigureAwait(false);
            await _stream.FlushAsync().ConfigureAwait(false);

            // 接收 FINS/TCP 响应头
            var responseHeader = new Byte[16];
            await ReadExactlyAsync(responseHeader, 0, 16).ConfigureAwait(false);

            if (responseHeader[0] != 0x46 || responseHeader[1] != 0x49 ||
                responseHeader[2] != 0x4E || responseHeader[3] != 0x53)
                throw new InvalidOperationException("响应头格式错误：FINS标识不匹配");

            var responseLength = (responseHeader[4] << 24) | (responseHeader[5] << 16) |
                               (responseHeader[6] << 8) | responseHeader[7];

            var tcpErrorCode = (responseHeader[12] << 24) | (responseHeader[13] << 16) |
                              (responseHeader[14] << 8) | responseHeader[15];
            if (tcpErrorCode != 0)
                throw new InvalidOperationException($"FINS/TCP错误: 0x{tcpErrorCode:X8}");

            var finsDataLength = responseLength - 8;
            if (finsDataLength <= 0)
                throw new InvalidOperationException("响应中无FINS帧数据");

            var responseData = new Byte[finsDataLength];
            await ReadExactlyAsync(responseData, 0, finsDataLength).ConfigureAwait(false);

            return FinsMessage.ParseResponse(responseData);
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not TimeoutException)
        {
            throw new InvalidOperationException($"通信失败: {ex.Message}", ex);
        }
    }

    /// <summary>异步发送命令请求并验证结果</summary>
    /// <param name="request">请求消息</param>
    /// <param name="errorPrefix">错误前缀</param>
    /// <returns>响应消息</returns>
    private async Task<FinsMessage> SendCommandAsync(FinsMessage request, String errorPrefix)
    {
        await _asyncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            request.Header.SA1 = SourceNodeAddress;
            request.Header.DA1 = ServerNodeAddress;
            request.Header.SID = GetNextServiceId();

            var response = await SendAndReceiveAsync(request).ConfigureAwait(false);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"{errorPrefix}: {response.GetErrorMessage()}");

            return response;
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    #endregion

    #region 异步存储区操作

    /// <summary>异步读取数据（字访问）</summary>
    /// <param name="address">地址字符串，如 D100, CIO200</param>
    /// <param name="length">读取字数</param>
    /// <returns>读取到的字节数组</returns>
    public async Task<Byte[]> ReadAsync(String address, UInt16 length)
    {
        var addr = FinsAddress.Parse(address);
        var request = FinsMessage.BuildReadRequest(addr, length, DA2);
        var response = await SendCommandAsync(request, "读取失败").ConfigureAwait(false);
        return response.Data ?? new Byte[0];
    }

    /// <summary>异步写入数据（字访问）</summary>
    /// <param name="address">地址字符串，如 D100, CIO200</param>
    /// <param name="data">写入数据</param>
    public async Task WriteAsync(String address, Byte[] data)
    {
        var addr = FinsAddress.Parse(address);
        var request = FinsMessage.BuildWriteRequest(addr, data, DA2);
        await SendCommandAsync(request, "写入失败").ConfigureAwait(false);
    }

    /// <summary>异步读取位数据</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <param name="length">读取位数</param>
    /// <returns>位值数组</returns>
    public async Task<Byte[]> ReadBitAsync(String address, UInt16 length = 1)
    {
        var addr = FinsAddress.Parse(address);
        addr.IsBit = true;
        var request = FinsMessage.BuildBitReadRequest(addr, length, DA2);
        var response = await SendCommandAsync(request, "读取位失败").ConfigureAwait(false);
        return response.Data ?? new Byte[0];
    }

    /// <summary>异步写入位数据</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <param name="values">位值数组</param>
    public async Task WriteBitAsync(String address, Byte[] values)
    {
        var addr = FinsAddress.Parse(address);
        addr.IsBit = true;
        var request = FinsMessage.BuildBitWriteRequest(addr, values, DA2);
        await SendCommandAsync(request, "写入位失败").ConfigureAwait(false);
    }

    /// <summary>异步读取单个布尔值</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <returns>布尔值</returns>
    public async Task<Boolean> ReadBoolAsync(String address)
    {
        var data = await ReadBitAsync(address, 1).ConfigureAwait(false);
        return data.Length > 0 && data[0] != 0;
    }

    /// <summary>异步写入单个布尔值</summary>
    /// <param name="address">地址字符串（含位偏移），如 CIO100.5</param>
    /// <param name="value">布尔值</param>
    public Task WriteBoolAsync(String address, Boolean value) =>
        WriteBitAsync(address, new Byte[] { value ? (Byte)1 : (Byte)0 });

    /// <summary>异步存储区填充</summary>
    /// <param name="address">起始地址</param>
    /// <param name="length">填充字数</param>
    /// <param name="fillValue">填充值</param>
    public async Task FillAsync(String address, UInt16 length, UInt16 fillValue)
    {
        var addr = FinsAddress.Parse(address);
        var request = FinsMessage.BuildFillRequest(addr, length, fillValue, DA2);
        await SendCommandAsync(request, "填充失败").ConfigureAwait(false);
    }

    /// <summary>异步多区域读取</summary>
    /// <param name="addresses">地址字符串数组</param>
    /// <returns>每个地址对应的字数据</returns>
    public async Task<Byte[]> MultipleReadAsync(String[] addresses)
    {
        var addrs = new FinsAddress[addresses.Length];
        for (var i = 0; i < addresses.Length; i++)
        {
            addrs[i] = FinsAddress.Parse(addresses[i]);
        }

        var request = FinsMessage.BuildMultipleReadRequest(addrs, DA2);
        var response = await SendCommandAsync(request, "多区域读取失败").ConfigureAwait(false);
        return response.Data ?? new Byte[0];
    }

    /// <summary>异步存储区传送</summary>
    /// <param name="source">源地址</param>
    /// <param name="destination">目标地址</param>
    /// <param name="length">传送字数</param>
    public async Task TransferAsync(String source, String destination, UInt16 length)
    {
        var srcAddr = FinsAddress.Parse(source);
        var dstAddr = FinsAddress.Parse(destination);
        var request = FinsMessage.BuildTransferRequest(srcAddr, dstAddr, length, DA2);
        await SendCommandAsync(request, "存储区传送失败").ConfigureAwait(false);
    }

    /// <summary>异步批量读取（自动合并相邻地址以减少通信次数）</summary>
    /// <param name="addresses">地址字符串数组</param>
    /// <param name="lengths">每个地址对应的读取字数</param>
    /// <returns>每个地址对应的字节数组</returns>
    public async Task<Byte[][]> BatchReadAsync(String[] addresses, UInt16[] lengths)
    {
        if (addresses == null) throw new ArgumentNullException(nameof(addresses));
        if (lengths == null) throw new ArgumentNullException(nameof(lengths));
        if (addresses.Length != lengths.Length)
            throw new ArgumentException("地址数组与长度数组元素数量不一致");

        var items = new (FinsAddress Addr, UInt16 Length, Int32 Index)[addresses.Length];
        for (var i = 0; i < addresses.Length; i++)
        {
            items[i] = (FinsAddress.Parse(addresses[i]), lengths[i], i);
        }

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

            for (var j = i2 + 1; j < items.Length; j++)
            {
                if (items[j].Addr.MemoryType != start.Addr.MemoryType) break;
                if (items[j].Addr.Address > mergedEnd) break;

                var newEnd = (UInt16)(items[j].Addr.Address + items[j].Length);
                if (newEnd > mergedEnd) mergedEnd = newEnd;
                lastIdx = j;
            }

            var totalWords = (UInt16)(mergedEnd - start.Addr.Address);
            var allData = await ReadAsync(items[i2].Addr.ToString(), totalWords).ConfigureAwait(false);

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

    #region 异步类型化读写

    /// <summary>异步读取Int16值</summary>
    /// <param name="address">地址</param>
    /// <returns>Int16值</returns>
    public async Task<Int16> ReadInt16Async(String address)
    {
        var data = await ReadAsync(address, 1).ConfigureAwait(false);
        return Transform.TransInt16(data, 0);
    }

    /// <summary>异步读取UInt16值</summary>
    /// <param name="address">地址</param>
    /// <returns>UInt16值</returns>
    public async Task<UInt16> ReadUInt16Async(String address)
    {
        var data = await ReadAsync(address, 1).ConfigureAwait(false);
        return Transform.TransUInt16(data, 0);
    }

    /// <summary>异步读取Int32值</summary>
    /// <param name="address">地址</param>
    /// <returns>Int32值</returns>
    public async Task<Int32> ReadInt32Async(String address)
    {
        var data = await ReadAsync(address, 2).ConfigureAwait(false);
        return Transform.TransInt32(data, 0);
    }

    /// <summary>异步读取UInt32值</summary>
    /// <param name="address">地址</param>
    /// <returns>UInt32值</returns>
    public async Task<UInt32> ReadUInt32Async(String address)
    {
        var data = await ReadAsync(address, 2).ConfigureAwait(false);
        return Transform.TransUInt32(data, 0);
    }

    /// <summary>异步读取Int64值</summary>
    /// <param name="address">地址</param>
    /// <returns>Int64值</returns>
    public async Task<Int64> ReadInt64Async(String address)
    {
        var data = await ReadAsync(address, 4).ConfigureAwait(false);
        return Transform.TransInt64(data, 0);
    }

    /// <summary>异步读取UInt64值</summary>
    /// <param name="address">地址</param>
    /// <returns>UInt64值</returns>
    public async Task<UInt64> ReadUInt64Async(String address)
    {
        var data = await ReadAsync(address, 4).ConfigureAwait(false);
        return Transform.TransUInt64(data, 0);
    }

    /// <summary>异步读取Float值</summary>
    /// <param name="address">地址</param>
    /// <returns>Single值</returns>
    public async Task<Single> ReadFloatAsync(String address)
    {
        var data = await ReadAsync(address, 2).ConfigureAwait(false);
        return Transform.TransSingle(data, 0);
    }

    /// <summary>异步读取Double值</summary>
    /// <param name="address">地址</param>
    /// <returns>Double值</returns>
    public async Task<Double> ReadDoubleAsync(String address)
    {
        var data = await ReadAsync(address, 4).ConfigureAwait(false);
        return Transform.TransDouble(data, 0);
    }

    /// <summary>异步读取字符串</summary>
    /// <param name="address">地址</param>
    /// <param name="length">读取字数</param>
    /// <returns>字符串</returns>
    public async Task<String> ReadStringAsync(String address, UInt16 length)
    {
        var data = await ReadAsync(address, length).ConfigureAwait(false);
        return Transform.TransString(data, 0, data.Length);
    }

    /// <summary>异步写入Int16值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public Task WriteInt16Async(String address, Int16 value) => WriteAsync(address, Transform.TransByte(value));

    /// <summary>异步写入UInt16值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public Task WriteUInt16Async(String address, UInt16 value) => WriteAsync(address, Transform.TransByte(value));

    /// <summary>异步写入Int32值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public Task WriteInt32Async(String address, Int32 value) => WriteAsync(address, Transform.TransByte(value));

    /// <summary>异步写入UInt32值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public Task WriteUInt32Async(String address, UInt32 value) => WriteAsync(address, Transform.TransByte(value));

    /// <summary>异步写入Int64值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public Task WriteInt64Async(String address, Int64 value) => WriteAsync(address, Transform.TransByte(value));

    /// <summary>异步写入UInt64值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public Task WriteUInt64Async(String address, UInt64 value) => WriteAsync(address, Transform.TransByte(value));

    /// <summary>异步写入Float值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public Task WriteFloatAsync(String address, Single value) => WriteAsync(address, Transform.TransByte(value));

    /// <summary>异步写入Double值</summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public Task WriteDoubleAsync(String address, Double value) => WriteAsync(address, Transform.TransByte(value));

    /// <summary>异步写入字符串</summary>
    /// <param name="address">地址</param>
    /// <param name="value">字符串值</param>
    public Task WriteStringAsync(String address, String value) => WriteAsync(address, Transform.TransByte(value));

    #endregion

    #region 异步设备操作

    /// <summary>异步启动PLC运行</summary>
    /// <param name="mode">运行模式。0x04=RUN, 0x02=MONITOR, 默认RUN</param>
    public async Task PlcRunAsync(Byte mode = 0x04)
    {
        var request = FinsMessage.BuildRunRequest(mode, DA2);
        await SendCommandAsync(request, "PLC运行命令失败").ConfigureAwait(false);
    }

    /// <summary>异步停止PLC</summary>
    public async Task PlcStopAsync()
    {
        var request = FinsMessage.BuildStopRequest(DA2);
        await SendCommandAsync(request, "PLC停止命令失败").ConfigureAwait(false);
    }

    /// <summary>异步读取CPU单元数据</summary>
    /// <returns>CPU单元数据</returns>
    public async Task<CpuUnitData> ReadCpuUnitDataAsync()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.ControllerDataRead, null, DA2);
        var response = await SendCommandAsync(request, "读取CPU数据失败").ConfigureAwait(false);
        return CpuUnitData.Parse(response.Data);
    }

    /// <summary>异步读取CPU单元状态</summary>
    /// <returns>CPU单元状态</returns>
    public async Task<CpuUnitStatus> ReadCpuUnitStatusAsync()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.ControllerStatusRead, null, DA2);
        var response = await SendCommandAsync(request, "读取CPU状态失败").ConfigureAwait(false);
        return CpuUnitStatus.Parse(response.Data);
    }

    /// <summary>异步读取PLC时钟</summary>
    /// <returns>PLC当前时间</returns>
    public async Task<DateTime> ReadClockAsync()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.ClockRead, null, DA2);
        var response = await SendCommandAsync(request, "读取时钟失败").ConfigureAwait(false);

        var data = response.Data;
        if (data == null || data.Length < 7)
            throw new InvalidOperationException("时钟数据长度不足");

        var year = 2000 + FinsMessage.FromBcd(data[0]);
        var month = FinsMessage.FromBcd(data[1]);
        var day = FinsMessage.FromBcd(data[2]);
        var hour = FinsMessage.FromBcd(data[3]);
        var minute = FinsMessage.FromBcd(data[4]);
        var second = FinsMessage.FromBcd(data[5]);

        return new DateTime(year, month, day, hour, minute, second);
    }

    /// <summary>异步设置PLC时钟</summary>
    /// <param name="dateTime">要设置的时间</param>
    public async Task WriteClockAsync(DateTime dateTime)
    {
        var request = FinsMessage.BuildClockWriteRequest(dateTime, DA2);
        await SendCommandAsync(request, "设置时钟失败").ConfigureAwait(false);
    }

    /// <summary>异步读取扫描周期</summary>
    /// <returns>平均/最大/最小扫描周期（单位：0.1ms）</returns>
    public async Task<(UInt32 Average, UInt32 Max, UInt32 Min)> ReadCycleTimeAsync()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.CycleTimeRead, null, DA2);
        var response = await SendCommandAsync(request, "读取扫描周期失败").ConfigureAwait(false);

        var data = response.Data;
        if (data == null || data.Length < 12)
            throw new InvalidOperationException("扫描周期数据长度不足");

        var avg = (UInt32)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
        var max = (UInt32)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);
        var min = (UInt32)((data[8] << 24) | (data[9] << 16) | (data[10] << 8) | data[11]);

        return (avg, max, min);
    }

    /// <summary>异步清除PLC错误</summary>
    /// <param name="errorCode">要清除的错误代码</param>
    public async Task ClearErrorAsync(UInt16 errorCode)
    {
        var data = new Byte[] { (Byte)(errorCode >> 8), (Byte)(errorCode & 0xFF) };
        var request = FinsMessage.BuildCommandRequest(FinsCommand.ErrorClear, data, DA2);
        await SendCommandAsync(request, "清除错误失败").ConfigureAwait(false);
    }

    /// <summary>异步读取错误日志</summary>
    /// <param name="startRecord">起始记录号</param>
    /// <param name="count">读取记录数</param>
    /// <returns>错误日志数据</returns>
    public async Task<Byte[]> ReadErrorLogAsync(UInt16 startRecord, UInt16 count)
    {
        var request = FinsMessage.BuildErrorLogReadRequest(startRecord, count, DA2);
        var response = await SendCommandAsync(request, "读取错误日志失败").ConfigureAwait(false);
        return response.Data ?? new Byte[0];
    }

    /// <summary>异步清除错误日志</summary>
    public async Task ClearErrorLogAsync()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.ErrorLogClear, null, DA2);
        await SendCommandAsync(request, "清除错误日志失败").ConfigureAwait(false);
    }

    /// <summary>异步读取参数区</summary>
    /// <param name="areaCode">参数区代码</param>
    /// <param name="beginWord">起始字</param>
    /// <param name="count">读取字数</param>
    /// <returns>参数数据</returns>
    public async Task<Byte[]> ReadParameterAreaAsync(UInt16 areaCode, UInt16 beginWord, UInt16 count)
    {
        var data = new Byte[6];
        data[0] = (Byte)(areaCode >> 8);
        data[1] = (Byte)(areaCode & 0xFF);
        data[2] = (Byte)(beginWord >> 8);
        data[3] = (Byte)(beginWord & 0xFF);
        data[4] = (Byte)(count >> 8);
        data[5] = (Byte)(count & 0xFF);

        var request = FinsMessage.BuildCommandRequest(FinsCommand.ParameterAreaRead, data, DA2);
        var response = await SendCommandAsync(request, "读取参数区失败").ConfigureAwait(false);
        return response.Data ?? new Byte[0];
    }

    /// <summary>异步写入参数区</summary>
    /// <param name="areaCode">参数区代码</param>
    /// <param name="beginWord">起始字</param>
    /// <param name="writeData">写入数据</param>
    public async Task WriteParameterAreaAsync(UInt16 areaCode, UInt16 beginWord, Byte[] writeData)
    {
        if (writeData == null) throw new ArgumentNullException(nameof(writeData));

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
        await SendCommandAsync(request, "写入参数区失败").ConfigureAwait(false);
    }

    /// <summary>异步清除参数区</summary>
    /// <param name="areaCode">参数区代码</param>
    public async Task ClearParameterAreaAsync(UInt16 areaCode)
    {
        var data = new Byte[] { (Byte)(areaCode >> 8), (Byte)(areaCode & 0xFF) };
        var request = FinsMessage.BuildCommandRequest(FinsCommand.ParameterAreaClear, data, DA2);
        await SendCommandAsync(request, "清除参数区失败").ConfigureAwait(false);
    }

    /// <summary>异步读取程序区</summary>
    /// <param name="programNo">程序号</param>
    /// <param name="beginWord">起始字</param>
    /// <param name="count">读取字数</param>
    /// <returns>程序数据</returns>
    public async Task<Byte[]> ReadProgramAreaAsync(UInt16 programNo, UInt32 beginWord, UInt16 count)
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
        var response = await SendCommandAsync(request, "读取程序区失败").ConfigureAwait(false);
        return response.Data ?? new Byte[0];
    }

    /// <summary>异步写入程序区</summary>
    /// <param name="programNo">程序号</param>
    /// <param name="beginWord">起始字</param>
    /// <param name="writeData">程序数据</param>
    public async Task WriteProgramAreaAsync(UInt16 programNo, UInt32 beginWord, Byte[] writeData)
    {
        if (writeData == null) throw new ArgumentNullException(nameof(writeData));

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
        await SendCommandAsync(request, "写入程序区失败").ConfigureAwait(false);
    }

    /// <summary>异步清除程序区</summary>
    /// <param name="programNo">程序号</param>
    public async Task ClearProgramAreaAsync(UInt16 programNo)
    {
        var data = new Byte[] { (Byte)(programNo >> 8), (Byte)(programNo & 0xFF) };
        var request = FinsMessage.BuildCommandRequest(FinsCommand.ProgramAreaClear, data, DA2);
        await SendCommandAsync(request, "清除程序区失败").ConfigureAwait(false);
    }

    /// <summary>异步读取连接数据</summary>
    /// <param name="unitAddress">单元地址</param>
    /// <param name="startNo">起始编号</param>
    /// <param name="count">读取数量</param>
    /// <returns>连接数据</returns>
    public async Task<Byte[]> ReadConnectionDataAsync(UInt16 unitAddress, UInt16 startNo, Byte count)
    {
        var data = new Byte[5];
        data[0] = (Byte)(unitAddress >> 8);
        data[1] = (Byte)(unitAddress & 0xFF);
        data[2] = (Byte)(startNo >> 8);
        data[3] = (Byte)(startNo & 0xFF);
        data[4] = count;

        var request = FinsMessage.BuildCommandRequest(FinsCommand.ConnectionDataRead, data, DA2);
        var response = await SendCommandAsync(request, "读取连接数据失败").ConfigureAwait(false);
        return response.Data ?? new Byte[0];
    }

    /// <summary>异步读取网络状态</summary>
    /// <returns>网络状态数据</returns>
    public async Task<Byte[]> ReadNetworkStatusAsync()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.NetworkStatusRead, null, DA2);
        var response = await SendCommandAsync(request, "读取网络状态失败").ConfigureAwait(false);
        return response.Data ?? new Byte[0];
    }

    /// <summary>异步读取消息</summary>
    /// <returns>消息数据</returns>
    public async Task<Byte[]> ReadMessageAsync()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.MessageRead, null, DA2);
        var response = await SendCommandAsync(request, "读取消息失败").ConfigureAwait(false);
        return response.Data ?? new Byte[0];
    }

    /// <summary>异步获取访问权</summary>
    public async Task AcquireAccessRightAsync()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.AccessRightAcquire, null, DA2);
        await SendCommandAsync(request, "获取访问权失败").ConfigureAwait(false);
    }

    /// <summary>异步强制获取访问权</summary>
    public async Task ForcedAcquireAccessRightAsync()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.AccessRightForcedAcquire, null, DA2);
        await SendCommandAsync(request, "强制获取访问权失败").ConfigureAwait(false);
    }

    /// <summary>异步释放访问权</summary>
    public async Task ReleaseAccessRightAsync()
    {
        var request = FinsMessage.BuildCommandRequest(FinsCommand.AccessRightRelease, null, DA2);
        await SendCommandAsync(request, "释放访问权失败").ConfigureAwait(false);
    }

    #endregion
}
