using System;
using System.Net.Sockets;
using System.Threading;
using NewLife.Log;

namespace NewLife.Omron.Protocols;

/// <summary>
/// FINS客户端
/// </summary>
public class FinsClient : IDisposable
{
    private TcpClient _client;
    private NetworkStream _stream;
    private readonly Object _lock = new Object();
    private Byte _serviceId = 0;

    /// <summary>IP地址</summary>
    public String IpAddress { get; set; }

    /// <summary>端口</summary>
    public Int32 Port { get; set; } = 9600;

    /// <summary>连接超时(毫秒)</summary>
    public Int32 ConnectTimeOut { get; set; } = 2000;

    /// <summary>接收超时(毫秒)</summary>
    public Int32 ReceiveTimeOut { get; set; } = 5000;

    /// <summary>目标单元地址</summary>
    public Byte DA2 { get; set; } = 0;

    /// <summary>数据转换格式</summary>
    public DataFormat DataFormat { get; set; } = DataFormat.CDAB;

    /// <summary>源节点地址 (FINS握手后获取)</summary>
    private Byte _sourceNodeAddress;

    /// <summary>
    /// 连接服务器
    /// </summary>
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

            XTrace.WriteLine($"FINS连接成功: {IpAddress}:{Port}, 源节点地址: {_sourceNodeAddress}");
        }
        catch (Exception ex)
        {
            _client?.Close();
            _client = null;
            throw new Exception($"连接失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 执行FINS握手
    /// </summary>
    private void PerformHandshake()
    {
        // FINS握手请求: FINS (0x46 0x49 0x4E 0x53) + 4字节数据
        var handshake = new Byte[] 
        { 
            0x46, 0x49, 0x4E, 0x53,  // "FINS"
            0x00, 0x00, 0x00, 0x0C,  // 长度: 12字节
            0x00, 0x00, 0x00, 0x00,  // 命令: 节点地址数据发送
            0x00, 0x00, 0x00, 0x00   // 错误代码: 正常
        };

        _stream.Write(handshake, 0, handshake.Length);

        // 接收握手响应
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
            throw new Exception("FINS握手响应格式错误");

        // 提取源节点地址
        _sourceNodeAddress = response[19];
        XTrace.WriteLine($"FINS握手成功, 客户端节点地址: {_sourceNodeAddress}, 服务器节点地址: {response[23]}");
    }

    /// <summary>
    /// 关闭连接
    /// </summary>
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

    /// <summary>
    /// 读取数据
    /// </summary>
    public Byte[] Read(String address, UInt16 length)
    {
        lock (_lock)
        {
            // 解析地址
            var addr = FinsAddress.Parse(address);

            // 构建读取请求
            var request = FinsMessage.BuildReadRequest(addr, length, DA2);
            request.Header.SA1 = _sourceNodeAddress;
            request.Header.SID = GetNextServiceId();

            // 发送请求并接收响应
            var response = SendAndReceive(request);

            // 检查响应
            if (!response.IsSuccess)
                throw new Exception($"读取失败: {response.GetErrorMessage()}");

            // 返回数据
            return response.Data ?? Array.Empty<Byte>();
        }
    }

    /// <summary>
    /// 写入数据
    /// </summary>
    public void Write(String address, Byte[] data)
    {
        lock (_lock)
        {
            // 解析地址
            var addr = FinsAddress.Parse(address);

            // 构建写入请求
            var request = FinsMessage.BuildWriteRequest(addr, data, DA2);
            request.Header.SA1 = _sourceNodeAddress;
            request.Header.SID = GetNextServiceId();

            // 发送请求并接收响应
            var response = SendAndReceive(request);

            // 检查响应
            if (!response.IsSuccess)
                throw new Exception($"写入失败: {response.GetErrorMessage()}");
        }
    }

    /// <summary>
    /// 发送请求并接收响应
    /// </summary>
    private FinsMessage SendAndReceive(FinsMessage request)
    {
        if (_client?.Connected != true)
            throw new Exception("未连接到服务器");

        try
        {
            // 构建TCP封装 (FINS/TCP)
            var requestData = request.ToBytes();
            var header = new Byte[8];
            header[0] = 0x46; // 'F'
            header[1] = 0x49; // 'I'
            header[2] = 0x4E; // 'N'
            header[3] = 0x53; // 'S'
            header[4] = (Byte)((requestData.Length >> 24) & 0xFF);
            header[5] = (Byte)((requestData.Length >> 16) & 0xFF);
            header[6] = (Byte)((requestData.Length >> 8) & 0xFF);
            header[7] = (Byte)(requestData.Length & 0xFF);

            // 发送数据
            _stream.Write(header, 0, header.Length);
            _stream.Write(requestData, 0, requestData.Length);
            _stream.Flush();

            // 接收响应头
            var responseHeader = new Byte[8];
            ReadExactly(responseHeader, 0, 8);

            // 验证响应头
            if (responseHeader[0] != 0x46 || responseHeader[1] != 0x49 || 
                responseHeader[2] != 0x4E || responseHeader[3] != 0x53)
                throw new Exception("响应头格式错误");

            // 获取响应长度
            var responseLength = (responseHeader[4] << 24) | (responseHeader[5] << 16) |
                               (responseHeader[6] << 8) | responseHeader[7];

            // 接收响应数据
            var responseData = new Byte[responseLength];
            ReadExactly(responseData, 0, responseLength);

            // 解析响应
            return FinsMessage.ParseResponse(responseData);
        }
        catch (Exception ex)
        {
            throw new Exception($"通信失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 精确读取指定字节数
    /// </summary>
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
                    throw new Exception("连接已关闭");
                readCount += n;
            }
            else
            {
                Thread.Sleep(10);
            }
        }
    }

    /// <summary>
    /// 获取下一个服务ID
    /// </summary>
    private Byte GetNextServiceId()
    {
        return _serviceId = (Byte)((_serviceId + 1) % 256);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Close();
    }
}
