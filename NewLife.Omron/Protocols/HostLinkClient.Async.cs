using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NewLife.Omron.Protocols;

/// <summary>HostLink协议客户端 - 异步方法</summary>
public partial class HostLinkClient
{
    private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);

    #region 异步通信

    /// <summary>异步发送HostLink命令并接收响应</summary>
    /// <param name="headerCode">命令码</param>
    /// <param name="data">命令数据</param>
    /// <returns>响应数据部分</returns>
    private async Task<String> SendCommandAsync(String headerCode, String data)
    {
        await _asyncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_stream == null) throw new InvalidOperationException("通信流未打开");

            var request = new HostLinkMessage(UnitNo, headerCode, data);
            var requestBytes = request.ToBytes();

            await _stream.WriteAsync(requestBytes, 0, requestBytes.Length).ConfigureAwait(false);
            await _stream.FlushAsync().ConfigureAwait(false);

            var responseBytes = await ReadResponseAsync().ConfigureAwait(false);
            var response = HostLinkMessage.ParseResponse(responseBytes);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"HostLink命令 {headerCode} 失败: {response.GetErrorMessage()}");

            return response.ResponseData;
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    /// <summary>异步读取响应数据（读到 *CR 结束）</summary>
    /// <returns>完整的响应字节数组</returns>
    private async Task<Byte[]> ReadResponseAsync()
    {
        var buffer = new Byte[4096];
        var offset = 0;
        using var cts = new CancellationTokenSource(ReceiveTimeOut);

        try
        {
            while (offset < buffer.Length)
            {
                var n = await _stream.ReadAsync(buffer, offset, buffer.Length - offset, cts.Token).ConfigureAwait(false);
                if (n <= 0)
                {
                    await Task.Delay(10, cts.Token).ConfigureAwait(false);
                    continue;
                }

                offset += n;

                // 检查是否收到完整帧（以 *CR 或 * 结尾）
                if (offset >= 2)
                {
                    for (var i = 0; i < offset - 1; i++)
                    {
                        if (buffer[i] == (Byte)'*' && buffer[i + 1] == (Byte)'\r')
                        {
                            var result = new Byte[i + 2];
                            Array.Copy(buffer, 0, result, 0, result.Length);
                            return result;
                        }
                    }

                    if (buffer[offset - 1] == (Byte)'*')
                    {
                        var result = new Byte[offset];
                        Array.Copy(buffer, 0, result, 0, offset);
                        return result;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException("HostLink接收响应超时");
        }

        throw new InvalidOperationException("HostLink响应数据超出缓冲区");
    }

    #endregion

    #region 异步存储区操作

    /// <summary>异步读取数据（字访问）</summary>
    /// <param name="address">地址字符串，如 D100, CIO200, H10</param>
    /// <param name="length">读取字数</param>
    /// <returns>读取到的字节数组</returns>
    public async Task<Byte[]> ReadAsync(String address, UInt16 length)
    {
        var addr = FinsAddress.Parse(address);
        var headerCode = GetReadHeaderCode(addr.MemoryType);
        var data = $"{addr.Address:D4}{length:D4}";
        var response = await SendCommandAsync(headerCode, data).ConfigureAwait(false);
        return HostLinkMessage.HexToBytes(response);
    }

    /// <summary>异步写入数据（字访问）</summary>
    /// <param name="address">地址字符串，如 D100, CIO200, H10</param>
    /// <param name="data">写入数据</param>
    public async Task WriteAsync(String address, Byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var addr = FinsAddress.Parse(address);
        var headerCode = GetWriteHeaderCode(addr.MemoryType);
        var cmdData = $"{addr.Address:D4}" + HostLinkMessage.BytesToHex(data);
        await SendCommandAsync(headerCode, cmdData).ConfigureAwait(false);
    }

    /// <summary>异步读取CPU状态</summary>
    /// <returns>CPU状态数据字节数组</returns>
    public async Task<Byte[]> ReadCpuStatusAsync()
    {
        var response = await SendCommandAsync("SC", null).ConfigureAwait(false);
        return HostLinkMessage.HexToBytes(response);
    }

    /// <summary>异步切换CPU模式</summary>
    /// <param name="mode">模式。00=PROGRAM, 02=MONITOR, 04=RUN</param>
    public async Task SetCpuModeAsync(String mode)
    {
        await SendCommandAsync("SC", mode).ConfigureAwait(false);
    }

    /// <summary>异步测试通信</summary>
    /// <param name="testData">测试数据</param>
    /// <returns>返回的测试数据</returns>
    public async Task<String> TestAsync(String testData = "00")
    {
        return await SendCommandAsync("TS", testData).ConfigureAwait(false);
    }

    /// <summary>异步通过HostLink FINS网关发送FINS命令</summary>
    /// <param name="finsMessage">FINS消息</param>
    /// <returns>FINS响应消息</returns>
    public async Task<FinsMessage> SendFinsCommandAsync(FinsMessage finsMessage)
    {
        if (finsMessage == null) throw new ArgumentNullException(nameof(finsMessage));

        var finsFrame = finsMessage.ToBytes();
        var hexData = HostLinkMessage.BytesToHex(finsFrame);
        var response = await SendCommandAsync("FA", hexData).ConfigureAwait(false);

        var responseBytes = HostLinkMessage.HexToBytes(response);
        return FinsMessage.ParseResponse(responseBytes);
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
}
