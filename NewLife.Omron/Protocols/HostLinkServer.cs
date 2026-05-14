using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;

namespace NewLife.Omron.Protocols;

/// <summary>HostLink C-mode 模拟服务器</summary>
/// <remarks>
/// 模拟欧姆龙 PLC 的 HostLink C-mode 协议通信，用于开发调试和单元测试。
/// 支持通过 TCP 接受 HostLink 命令帧，处理存储区读写、CPU 状态查询等。
/// 
/// 帧格式：@{UnitNo:2}{HeaderCode:2}{Data:N}{FCS:2}*{CR}
/// 
/// 使用示例：
/// <code>
/// using var server = new HostLinkServer();
/// server.Start();
/// 
/// using var client = new HostLinkClient("127.0.0.1", server.Port);
/// client.WriteInt32("D100", 12345);
/// var value = client.ReadInt32("D100");
/// </code>
/// </remarks>
public class HostLinkServer : IDisposable
{
    #region 属性

    private const Int32 DefaultAreaSize = 20000;

    private TcpListener _listener;
    private CancellationTokenSource _cts;
    private readonly Dictionary<Byte, Byte[]> _areas = new();
    private readonly Object _lock = new();

    /// <summary>监听端口。Start 后可获取实际端口</summary>
    public Int32 Port { get; private set; }

    /// <summary>是否正在运行</summary>
    public Boolean IsRunning => _listener != null;

    /// <summary>CPU 运行模式</summary>
    public CpuMode CpuMode { get; set; } = CpuMode.Run;

    #endregion

    #region 方法

    /// <summary>启动服务</summary>
    /// <param name="port">监听端口。0 表示随机分配</param>
    public void Start(Int32 port = 0)
    {
        if (_listener != null) return;

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.Server.LocalEndPoint).Port;
        _cts = new CancellationTokenSource();

        var token = _cts.Token;
        Task.Factory.StartNew(() => AcceptLoop(token), TaskCreationOptions.LongRunning);

        XTrace.WriteLine($"HostLinkServer已启动: 端口{Port}");
    }

    /// <summary>停止服务</summary>
    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        _listener = null;
    }

    private void AcceptLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = _listener.AcceptTcpClient();
                var t = token;
                Task.Factory.StartNew(() => HandleClient(client, t), TaskCreationOptions.LongRunning);
            }
            catch (SocketException) when (token.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { XTrace.WriteLine($"HostLinkServer接受连接异常: {ex.Message}"); }
        }
    }

    private void HandleClient(TcpClient tcp, CancellationToken token)
    {
        using (tcp)
        {
            try
            {
                var stream = tcp.GetStream();
                stream.ReadTimeout = 30_000;
                var buffer = new Byte[4096];

                while (!token.IsCancellationRequested && tcp.Connected)
                {
                    // 读取完整帧（以 *\r 结尾）
                    var frameBytes = ReadFrame(stream, buffer, token);
                    if (frameBytes == null) break;

                    // 构建响应
                    var responseBytes = ProcessFrame(frameBytes);
                    if (responseBytes != null)
                    {
                        stream.Write(responseBytes, 0, responseBytes.Length);
                        stream.Flush();
                    }
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }
    }

    /// <summary>读取一个完整的 HostLink 帧（以 *\r 结尾）</summary>
    private static Byte[] ReadFrame(NetworkStream stream, Byte[] buffer, CancellationToken token)
    {
        var offset = 0;
        while (!token.IsCancellationRequested && offset < buffer.Length)
        {
            var n = stream.Read(buffer, offset, buffer.Length - offset);
            if (n <= 0) return null;
            offset += n;

            // 查找 *\r 结束标记
            for (var i = 0; i < offset - 1; i++)
            {
                if (buffer[i] == (Byte)'*' && buffer[i + 1] == (Byte)'\r')
                {
                    var result = new Byte[i + 2];
                    Array.Copy(buffer, 0, result, 0, result.Length);
                    return result;
                }
            }
        }
        return null;
    }

    #endregion

    #region 命令处理

    /// <summary>处理一个 HostLink 帧，返回响应帧字节</summary>
    /// <param name="frameBytes">接收到的帧字节（含 @ 起始符）</param>
    /// <returns>响应帧字节，或 null 表示无需响应</returns>
    private Byte[] ProcessFrame(Byte[] frameBytes)
    {
        var frame = Encoding.ASCII.GetString(frameBytes).TrimEnd('\r', '\n', '\0');

        // 验证起始符
        if (!frame.StartsWith("@")) return null;
        var endIdx = frame.IndexOf('*');
        if (endIdx < 0) return null;

        // 解析帧结构
        if (frame.Length < 7) return BuildErrorResponse(0, "TS", 0x14); // 格式错误

        Byte unitNo;
        try { unitNo = Byte.Parse(frame[1..3]); }
        catch { return BuildErrorResponse(0, "TS", 0x14); }

        var headerCode = frame[3..5];
        // data 部分在 HeaderCode 之后，FCS 之前（即 endIdx-2 之前）
        var dataSection = endIdx > 7 ? frame[5..(endIdx - 2)] : "";

        // 处理命令
        Byte endCode = 0x00;
        String responseData = null;

        try
        {
            switch (headerCode)
            {
                case "RR": responseData = ProcessRead(dataSection, (Byte)MemoryArea.CIO_Word); break;
                case "WR": ProcessWrite(dataSection, (Byte)MemoryArea.CIO_Word); break;
                case "RD": responseData = ProcessRead(dataSection, (Byte)MemoryArea.DM_Word); break;
                case "WD": ProcessWrite(dataSection, (Byte)MemoryArea.DM_Word); break;
                case "RH": responseData = ProcessRead(dataSection, (Byte)MemoryArea.HR_Word); break;
                case "WH": ProcessWrite(dataSection, (Byte)MemoryArea.HR_Word); break;
                case "RJ": responseData = ProcessRead(dataSection, (Byte)MemoryArea.AR_Word); break;
                case "WJ": ProcessWrite(dataSection, (Byte)MemoryArea.AR_Word); break;
                case "RC": responseData = ProcessRead(dataSection, (Byte)MemoryArea.TIM_Word); break;
                case "WC": ProcessWrite(dataSection, (Byte)MemoryArea.TIM_Word); break;
                case "RE": responseData = ProcessRead(dataSection, (Byte)MemoryArea.EM0_Word); break;
                case "WE": ProcessWrite(dataSection, (Byte)MemoryArea.EM0_Word); break;
                case "SC": (endCode, responseData) = ProcessSc(dataSection); break;
                case "TS": responseData = dataSection; break; // 测试命令：原样回应
                default: endCode = 0x16; break; // 命令不支持
            }
        }
        catch (ArgumentException)
        {
            endCode = 0x04; // 地址超出范围
        }
        catch
        {
            endCode = 0x14; // 格式错误
        }

        return BuildResponse(unitNo, headerCode, endCode, responseData);
    }

    /// <summary>处理读取命令</summary>
    /// <param name="dataSection">命令数据部分</param>
    /// <param name="areaCode">存储区代码</param>
    /// <returns>十六进制编码的读取数据</returns>
    private String ProcessRead(String dataSection, Byte areaCode)
    {
        if (dataSection.Length < 8) throw new ArgumentException("读取命令数据格式错误");

        var address = UInt16.Parse(dataSection[..4]);
        var wordCount = UInt16.Parse(dataSection[4..8]);

        var bytes = ReadMemoryInternal(areaCode, address, wordCount);
        return HostLinkMessage.BytesToHex(bytes);
    }

    /// <summary>处理写入命令</summary>
    /// <param name="dataSection">命令数据部分</param>
    /// <param name="areaCode">存储区代码</param>
    private void ProcessWrite(String dataSection, Byte areaCode)
    {
        if (dataSection.Length < 4) throw new ArgumentException("写入命令数据格式错误");

        var address = UInt16.Parse(dataSection[..4]);
        var hexData = dataSection[4..];
        var data = HostLinkMessage.HexToBytes(hexData);

        WriteMemoryInternal(areaCode, address, data);
    }

    /// <summary>处理 SC 命令（CPU 状态读取或模式切换）</summary>
    /// <param name="dataSection">命令数据部分</param>
    /// <returns>结束码和响应数据</returns>
    private (Byte endCode, String responseData) ProcessSc(String dataSection)
    {
        if (String.IsNullOrEmpty(dataSection))
        {
            // SC 不带参数 → 读取 CPU 状态
            var statusByte = (Byte)CpuMode;
            return (0x00, $"{statusByte:X2}00000000000000");
        }
        else
        {
            // SC 带模式码 → 切换模式
            var modeCode = Convert.ToByte(dataSection[..2], 16);
            CpuMode = (CpuMode)modeCode;
            return (0x00, null);
        }
    }

    /// <summary>构建响应帧字节</summary>
    private static Byte[] BuildResponse(Byte unitNo, String headerCode, Byte endCode, String responseData)
    {
        var sb = new StringBuilder();
        sb.Append('@');
        sb.Append(unitNo.ToString("D2"));
        sb.Append(headerCode);
        sb.Append(endCode.ToString("X2"));
        if (!String.IsNullOrEmpty(responseData))
            sb.Append(responseData);

        var frameForFcs = sb.ToString();
        var fcs = HostLinkMessage.CalculateFcs(frameForFcs);
        sb.Append(fcs.ToString("X2"));
        sb.Append("*\r");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    /// <summary>构建错误响应帧</summary>
    private static Byte[] BuildErrorResponse(Byte unitNo, String headerCode, Byte endCode)
        => BuildResponse(unitNo, headerCode, endCode, null);

    #endregion

    #region 存储区

    /// <summary>读取存储区（内部使用，在锁保护下执行）</summary>
    private Byte[] ReadMemoryInternal(Byte areaCode, UInt16 address, UInt16 wordCount)
    {
        var byteCount = wordCount * 2;
        var result = new Byte[byteCount];

        lock (_lock)
        {
            var area = GetOrCreateArea(areaCode);
            var off = address * 2;
            var copyLen = Math.Min(byteCount, Math.Max(0, area.Length - off));
            if (copyLen > 0) Array.Copy(area, off, result, 0, copyLen);
        }

        return result;
    }

    /// <summary>写入存储区（内部使用，在锁保护下执行）</summary>
    private void WriteMemoryInternal(Byte areaCode, UInt16 address, Byte[] data)
    {
        lock (_lock)
        {
            var off = address * 2;
            EnsureAreaSize(areaCode, off + data.Length);
            var area = GetOrCreateArea(areaCode);
            Array.Copy(data, 0, area, off, data.Length);
        }
    }

    /// <summary>获取或创建存储区</summary>
    private Byte[] GetOrCreateArea(Byte areaCode)
    {
        if (!_areas.TryGetValue(areaCode, out var area))
        {
            area = new Byte[DefaultAreaSize];
            _areas[areaCode] = area;
        }
        return area;
    }

    /// <summary>确保存储区大小足够</summary>
    private void EnsureAreaSize(Byte areaCode, Int32 requiredBytes)
    {
        var area = GetOrCreateArea(areaCode);
        if (area.Length < requiredBytes)
        {
            var newSize = Math.Max(requiredBytes + 2000, area.Length * 2);
            var newArea = new Byte[newSize];
            Array.Copy(area, 0, newArea, 0, area.Length);
            _areas[areaCode] = newArea;
        }
    }

    /// <summary>读取存储区数据（用于测试验证）</summary>
    /// <param name="areaCode">存储区代码，如 0x82 (DM)</param>
    /// <param name="address">起始地址</param>
    /// <param name="wordCount">读取字数</param>
    /// <returns>字节数组</returns>
    public Byte[] ReadMemory(Byte areaCode, UInt16 address, UInt16 wordCount)
        => ReadMemoryInternal(areaCode, address, wordCount);

    /// <summary>写入存储区数据（用于测试准备）</summary>
    /// <param name="areaCode">存储区代码，如 0x82 (DM)</param>
    /// <param name="address">起始地址</param>
    /// <param name="data">写入数据</param>
    public void WriteMemory(Byte areaCode, UInt16 address, Byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        WriteMemoryInternal(areaCode, address, data);
    }

    #endregion

    #region 日志

    /// <summary>释放资源</summary>
    public void Dispose() => Stop();

    #endregion
}
