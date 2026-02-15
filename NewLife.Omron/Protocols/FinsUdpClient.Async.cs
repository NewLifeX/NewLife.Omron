using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NewLife.Omron.Protocols;

/// <summary>FINS/UDP客户端 - 异步方法</summary>
public partial class FinsUdpClient
{
    private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);

    #region 异步通信

    /// <summary>异步发送FINS消息并接收响应</summary>
    /// <param name="request">请求消息</param>
    /// <returns>响应消息</returns>
    private async Task<FinsMessage> SendAndReceiveAsync(FinsMessage request)
    {
        if (_client == null) throw new InvalidOperationException("未打开UDP通信");

        try
        {
            var finsFrame = request.ToBytes();
            await _client.SendAsync(finsFrame, finsFrame.Length).ConfigureAwait(false);

            var result = await _client.ReceiveAsync().ConfigureAwait(false);
            var responseData = result.Buffer;

            if (responseData == null || responseData.Length < 14)
                throw new InvalidOperationException("FINS/UDP响应数据不足");

            return FinsMessage.ParseResponse(responseData);
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not TimeoutException)
        {
            throw new InvalidOperationException($"FINS/UDP通信失败: {ex.Message}", ex);
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
            SetupHeader(request);

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
