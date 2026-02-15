using System;
using System.IO;
using System.Text;
using System.Threading;
using NewLife.Log;

namespace NewLife.Omron.Protocols;

/// <summary>HostLink协议客户端</summary>
/// <remarks>
/// 实现欧姆龙PLC的HostLink C-mode协议通信。
/// 支持通过串口(RS-232C/422)或TCP（串口服务器）进行通信。
/// 
/// 使用示例 - 串口模式:
/// <code>
/// var serial = new SerialPort("COM1", 9600, Parity.Even, 7, StopBits.Two);
/// serial.Open();
/// using var client = new HostLinkClient(serial.BaseStream);
/// var data = client.Read("D100", 10);
/// </code>
/// 
/// 使用示例 - TCP模式（串口服务器）:
/// <code>
/// var tcp = new TcpClient("192.168.1.100", 4001);
/// using var client = new HostLinkClient(tcp.GetStream());
/// var data = client.Read("D100", 10);
/// </code>
/// </remarks>
public partial class HostLinkClient : IDisposable
{
    #region 属性

    private Stream _stream;
    private readonly Object _lock = new();
    private Boolean _ownStream;

    /// <summary>单元号 (0-31)，默认0</summary>
    public Byte UnitNo { get; set; }

    /// <summary>接收超时(毫秒)</summary>
    public Int32 ReceiveTimeOut { get; set; } = 5000;

    /// <summary>数据转换格式</summary>
    public DataFormat DataFormat { get; set; } = DataFormat.CDAB;

    /// <summary>字节转换器</summary>
    public ByteTransform Transform { get; set; }

    /// <summary>是否已打开</summary>
    public Boolean IsOpened => _stream != null;

    #endregion

    #region 构造

    /// <summary>从流实例化HostLink客户端</summary>
    /// <param name="stream">通信流（串口流或网络流）</param>
    /// <param name="ownStream">是否拥有流的所有权（Dispose时是否关闭流）</param>
    public HostLinkClient(Stream stream, Boolean ownStream = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _ownStream = ownStream;
        Transform = new ByteTransform { DataFormat = DataFormat };
    }

    /// <summary>通过TCP连接实例化HostLink客户端（适用于串口服务器）</summary>
    /// <param name="ipAddress">IP地址</param>
    /// <param name="port">端口</param>
    public HostLinkClient(String ipAddress, Int32 port)
    {
        var tcp = new System.Net.Sockets.TcpClient();
        tcp.Connect(ipAddress, port);
        _stream = tcp.GetStream();
        _ownStream = true;
        Transform = new ByteTransform { DataFormat = DataFormat };

        XTrace.WriteLine($"HostLink/TCP连接成功: {ipAddress}:{port}");
    }

    #endregion

    #region 存储区操作

    /// <summary>读取数据（字访问）</summary>
    /// <param name="address">地址字符串，如 D100, CIO200, H10</param>
    /// <param name="length">读取字数</param>
    /// <returns>读取到的字节数组</returns>
    public Byte[] Read(String address, UInt16 length)
    {
        var addr = FinsAddress.Parse(address);
        var headerCode = GetReadHeaderCode(addr.MemoryType);
        var data = $"{addr.Address:D4}{length:D4}";

        var response = SendCommand(headerCode, data);

        return HostLinkMessage.HexToBytes(response);
    }

    /// <summary>写入数据（字访问）</summary>
    /// <param name="address">地址字符串，如 D100, CIO200, H10</param>
    /// <param name="data">写入数据</param>
    public void Write(String address, Byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var addr = FinsAddress.Parse(address);
        var headerCode = GetWriteHeaderCode(addr.MemoryType);
        var cmdData = $"{addr.Address:D4}" + HostLinkMessage.BytesToHex(data);

        SendCommand(headerCode, cmdData);
    }

    /// <summary>读取CPU状态</summary>
    /// <returns>CPU状态数据字节数组</returns>
    public Byte[] ReadCpuStatus()
    {
        var response = SendCommand("SC", null);
        return HostLinkMessage.HexToBytes(response);
    }

    /// <summary>切换CPU模式</summary>
    /// <param name="mode">模式。00=PROGRAM, 02=MONITOR, 04=RUN</param>
    public void SetCpuMode(String mode)
    {
        SendCommand("SC", mode);
    }

    /// <summary>测试通信</summary>
    /// <param name="testData">测试数据（ASCII文本）</param>
    /// <returns>返回的测试数据（应与输入一致）</returns>
    public String Test(String testData = "00")
    {
        return SendCommand("TS", testData);
    }

    /// <summary>通过HostLink FINS网关发送FINS命令</summary>
    /// <param name="finsMessage">FINS消息</param>
    /// <returns>FINS响应消息</returns>
    public FinsMessage SendFinsCommand(FinsMessage finsMessage)
    {
        if (finsMessage == null) throw new ArgumentNullException(nameof(finsMessage));

        // HostLink FINS 网关命令 (FA)
        var finsFrame = finsMessage.ToBytes();
        var hexData = HostLinkMessage.BytesToHex(finsFrame);
        var response = SendCommand("FA", hexData);

        // 解析FINS响应
        var responseBytes = HostLinkMessage.HexToBytes(response);
        return FinsMessage.ParseResponse(responseBytes);
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

    /// <summary>发送HostLink命令并接收响应</summary>
    /// <param name="headerCode">命令码</param>
    /// <param name="data">命令数据</param>
    /// <returns>响应数据部分</returns>
    private String SendCommand(String headerCode, String data)
    {
        lock (_lock)
        {
            if (_stream == null) throw new InvalidOperationException("通信流未打开");

            var request = new HostLinkMessage(UnitNo, headerCode, data);
            var requestBytes = request.ToBytes();

            // 发送
            _stream.Write(requestBytes, 0, requestBytes.Length);
            _stream.Flush();

            // 接收（读取到 * + CR 为止）
            var responseBytes = ReadResponse();

            // 解析
            var response = HostLinkMessage.ParseResponse(responseBytes);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"HostLink命令 {headerCode} 失败: {response.GetErrorMessage()}");

            return response.ResponseData;
        }
    }

    /// <summary>读取响应数据（读到 *CR 结束）</summary>
    /// <returns>完整的响应字节数组</returns>
    private Byte[] ReadResponse()
    {
        var buffer = new Byte[4096];
        var offset = 0;
        var startTime = DateTime.Now;

        while (offset < buffer.Length)
        {
            if ((DateTime.Now - startTime).TotalMilliseconds > ReceiveTimeOut)
                throw new TimeoutException("HostLink接收响应超时");

            var bytesAvailable = false;
            try
            {
                // 对于串口流和网络流都尝试直接读取
                if (_stream is System.Net.Sockets.NetworkStream ns)
                    bytesAvailable = ns.DataAvailable;
                else
                    bytesAvailable = true; // 串口流直接尝试读取
            }
            catch { bytesAvailable = true; }

            if (bytesAvailable)
            {
                var n = _stream.Read(buffer, offset, buffer.Length - offset);
                if (n <= 0)
                {
                    Thread.Sleep(10);
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

                    // 兼容仅以 * 结尾的情况
                    if (buffer[offset - 1] == (Byte)'*')
                    {
                        var result = new Byte[offset];
                        Array.Copy(buffer, 0, result, 0, offset);
                        return result;
                    }
                }
            }
            else
            {
                Thread.Sleep(10);
            }
        }

        throw new InvalidOperationException("HostLink响应数据超出缓冲区");
    }

    /// <summary>获取读取命令头码</summary>
    /// <param name="memoryType">存储区类型</param>
    /// <returns>HostLink命令头码</returns>
    private static String GetReadHeaderCode(Byte memoryType) => memoryType switch
    {
        (Byte)MemoryArea.CIO_Word or (Byte)MemoryArea.CIO_Bit => "RR",
        (Byte)MemoryArea.DM_Word or (Byte)MemoryArea.DM_Bit => "RD",
        (Byte)MemoryArea.HR_Word or (Byte)MemoryArea.HR_Bit => "RH",
        (Byte)MemoryArea.AR_Word or (Byte)MemoryArea.AR_Bit => "RJ",
        (Byte)MemoryArea.TIM_Word or (Byte)MemoryArea.TIM_Bit => "RC",
        (Byte)MemoryArea.WR_Word or (Byte)MemoryArea.WR_Bit => "RR",
        >= (Byte)MemoryArea.EM0_Word and <= (Byte)MemoryArea.EM3_Word => "RE",
        _ => throw new ArgumentException($"HostLink不支持的存储区类型: 0x{memoryType:X2}")
    };

    /// <summary>获取写入命令头码</summary>
    /// <param name="memoryType">存储区类型</param>
    /// <returns>HostLink命令头码</returns>
    private static String GetWriteHeaderCode(Byte memoryType) => memoryType switch
    {
        (Byte)MemoryArea.CIO_Word or (Byte)MemoryArea.CIO_Bit => "WR",
        (Byte)MemoryArea.DM_Word or (Byte)MemoryArea.DM_Bit => "WD",
        (Byte)MemoryArea.HR_Word or (Byte)MemoryArea.HR_Bit => "WH",
        (Byte)MemoryArea.AR_Word or (Byte)MemoryArea.AR_Bit => "WJ",
        (Byte)MemoryArea.TIM_Word or (Byte)MemoryArea.TIM_Bit => "WC",
        (Byte)MemoryArea.WR_Word or (Byte)MemoryArea.WR_Bit => "WR",
        >= (Byte)MemoryArea.EM0_Word and <= (Byte)MemoryArea.EM3_Word => "WE",
        _ => throw new ArgumentException($"HostLink不支持的存储区类型: 0x{memoryType:X2}")
    };

    /// <summary>关闭连接</summary>
    public void Close()
    {
        try
        {
            if (_ownStream)
                _stream?.Close();
        }
        catch { }
        finally
        {
            _stream = null;
        }
    }

    #endregion

    #region 日志

    /// <summary>释放资源</summary>
    public void Dispose() => Close();

    #endregion
}
