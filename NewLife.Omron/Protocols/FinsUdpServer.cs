using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;

namespace NewLife.Omron.Protocols;

/// <summary>FINS/UDP 模拟服务器</summary>
/// <remarks>
/// 模拟欧姆龙 PLC 的 FINS/UDP 协议通信，用于开发调试和单元测试。
/// UDP 无握手，直接收发原始 FINS 帧（Header[10] + Command[2] + Data[N]）。
/// 
/// 使用示例：
/// <code>
/// using var server = new FinsUdpServer();
/// server.Start();
/// 
/// using var client = new FinsUdpClient("127.0.0.1", server.Port);
/// client.Open();
/// client.WriteInt32("D100", 12345);
/// var value = client.ReadInt32("D100");
/// </code>
/// </remarks>
public class FinsUdpServer : IDisposable
{
    #region 属性

    private const Int32 DefaultAreaSize = 20000;

    private UdpClient _udp;
    private CancellationTokenSource _cts;
    private readonly Dictionary<Byte, Byte[]> _areas = new();
    private readonly Object _lock = new();

    /// <summary>监听端口。Start 后可获取实际端口</summary>
    public Int32 Port { get; private set; }

    /// <summary>服务端节点地址。默认 1</summary>
    public Byte NodeAddress { get; set; } = 1;

    /// <summary>是否正在运行</summary>
    public Boolean IsRunning => _udp != null;

    /// <summary>CPU 运行模式</summary>
    public CpuMode CpuMode { get; set; } = CpuMode.Run;

    /// <summary>CPU 型号</summary>
    public String CpuModel { get; set; } = "FinsUdpServer";

    /// <summary>CPU 版本</summary>
    public String CpuVersion { get; set; } = "V1.0";

    /// <summary>时钟值。为 null 时返回当前系统时间</summary>
    public DateTime? Clock { get; set; }

    #endregion

    #region 方法

    /// <summary>启动服务</summary>
    /// <param name="port">监听端口。0 表示随机分配</param>
    public void Start(Int32 port = 0)
    {
        if (_udp != null) return;

        var endpoint = new IPEndPoint(IPAddress.Any, port);
        _udp = new UdpClient(endpoint);
        Port = ((IPEndPoint)_udp.Client.LocalEndPoint).Port;
        _cts = new CancellationTokenSource();

        var token = _cts.Token;
        Task.Factory.StartNew(() => ReceiveLoop(token), TaskCreationOptions.LongRunning);

        XTrace.WriteLine($"FinsUdpServer已启动: 端口{Port}, 节点地址{NodeAddress}");
    }

    /// <summary>停止服务</summary>
    public void Stop()
    {
        _cts?.Cancel();
        try { _udp?.Close(); } catch { }
        _udp = null;
    }

    private void ReceiveLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                var data = _udp.Receive(ref remote);

                if (data == null || data.Length < 12) continue;

                var responseFins = ProcessFinsFrame(data);
                _udp.Send(responseFins, responseFins.Length, remote);
            }
            catch (SocketException) when (token.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { XTrace.WriteLine($"FinsUdpServer接收异常: {ex.Message}"); }
        }
    }

    #endregion

    #region 命令处理

    /// <summary>处理 FINS 帧并返回响应帧</summary>
    /// <param name="finsFrame">请求 FINS 帧: Header(10) + Command(2) + Data(N)</param>
    /// <returns>响应 FINS 帧: Header(10) + Command(2) + EndCode(2) + Data(M)</returns>
    private Byte[] ProcessFinsFrame(Byte[] finsFrame)
    {
        if (finsFrame.Length < 12) return BuildErrorResponse(finsFrame, 0x1001);

        var reqHeader = FinsHeader.Parse(finsFrame, 0);
        var cmd = FinsCommand.Parse(finsFrame, 10);
        var dataLen = finsFrame.Length - 12;
        Byte[] data = null;
        if (dataLen > 0)
        {
            data = new Byte[dataLen];
            Array.Copy(finsFrame, 12, data, 0, dataLen);
        }

        Byte[] responseData = null;
        UInt16 endCode = 0x0000;

        try
        {
            var cmdCode = (cmd.MRC << 8) | cmd.SRC;
            switch (cmdCode)
            {
                // 存储区操作
                case 0x0101: responseData = ProcessMemoryRead(data); break;
                case 0x0102: ProcessMemoryWrite(data); break;
                case 0x0103: ProcessMemoryFill(data); break;
                case 0x0104: responseData = ProcessMultipleRead(data); break;
                case 0x0105: ProcessTransfer(data); break;

                // CPU 控制
                case 0x0401: CpuMode = data != null && data.Length >= 3 ? (CpuMode)data[2] : CpuMode.Run; break;
                case 0x0402: CpuMode = CpuMode.Program; break;

                // 设备信息
                case 0x0501: responseData = BuildControllerDataResponse(); break;
                case 0x0601: responseData = BuildControllerStatusResponse(); break;
                case 0x0620: responseData = BuildCycleTimeResponse(); break;

                // 时钟
                case 0x0701: responseData = BuildClockReadResponse(); break;
                case 0x0702: ProcessClockWrite(data); break;

                // 错误处理
                case 0x2101: break;
                case 0x2102: responseData = new Byte[0]; break;
                case 0x2103: break;

                default: endCode = 0x0401; break; // 未定义命令
            }
        }
        catch
        {
            endCode = 0x1001; // 命令格式错误
        }

        return BuildResponse(reqHeader, cmd, endCode, responseData);
    }

    /// <summary>构建 FINS 响应帧</summary>
    private static Byte[] BuildResponse(FinsHeader reqHeader, FinsCommand cmd, UInt16 endCode, Byte[] responseData)
    {
        // 响应头：交换源/目标地址
        var respHeader = new FinsHeader
        {
            ICF = 0xC0,
            RSV = 0x00,
            GCT = reqHeader.GCT,
            DNA = reqHeader.SNA,
            DA1 = reqHeader.SA1,
            DA2 = reqHeader.SA2,
            SNA = reqHeader.DNA,
            SA1 = reqHeader.DA1,
            SA2 = reqHeader.DA2,
            SID = reqHeader.SID
        };

        var hdr = respHeader.ToBytes();
        var cmdBytes = cmd.ToBytes();
        var rd = responseData ?? new Byte[0];

        var result = new Byte[10 + 2 + 2 + rd.Length];
        Array.Copy(hdr, 0, result, 0, 10);
        Array.Copy(cmdBytes, 0, result, 10, 2);
        result[12] = (Byte)(endCode >> 8);
        result[13] = (Byte)(endCode & 0xFF);
        if (rd.Length > 0) Array.Copy(rd, 0, result, 14, rd.Length);

        return result;
    }

    /// <summary>构建错误响应帧</summary>
    private static Byte[] BuildErrorResponse(Byte[] finsFrame, UInt16 endCode)
    {
        var result = new Byte[14];
        if (finsFrame.Length >= 10) Array.Copy(finsFrame, 0, result, 0, 10);
        if (finsFrame.Length >= 12) Array.Copy(finsFrame, 10, result, 10, 2);
        result[12] = (Byte)(endCode >> 8);
        result[13] = (Byte)(endCode & 0xFF);
        return result;
    }

    #endregion

    #region 存储区命令

    /// <summary>处理存储区读取 (01:01)</summary>
    private Byte[] ProcessMemoryRead(Byte[] data)
    {
        var areaCode = data[0];
        var address = (UInt16)((data[1] << 8) | data[2]);
        var bitOffset = data[3];
        var length = (UInt16)((data[4] << 8) | data[5]);

        if (MemoryAreaHelper.IsBitArea(areaCode))
        {
            var wordArea = ToWordAreaCode(areaCode);
            var result = new Byte[length];
            lock (_lock)
            {
                var area = GetOrCreateArea(wordArea);
                for (var i = 0; i < length; i++)
                {
                    var bit = bitOffset + i;
                    var word = address + bit / 16;
                    var bitInWord = bit % 16;
                    var off = word * 2;
                    if (off + 1 < area.Length)
                    {
                        var wordVal = (UInt16)((area[off] << 8) | area[off + 1]);
                        result[i] = (Byte)((wordVal >> bitInWord) & 1);
                    }
                }
            }
            return result;
        }
        else
        {
            var byteCount = length * 2;
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
    }

    /// <summary>处理存储区写入 (01:02)</summary>
    private void ProcessMemoryWrite(Byte[] data)
    {
        var areaCode = data[0];
        var address = (UInt16)((data[1] << 8) | data[2]);
        var bitOffset = data[3];
        var length = (UInt16)((data[4] << 8) | data[5]);

        if (MemoryAreaHelper.IsBitArea(areaCode))
        {
            var wordArea = ToWordAreaCode(areaCode);
            lock (_lock)
            {
                for (var i = 0; i < length; i++)
                {
                    var bit = bitOffset + i;
                    var word = address + bit / 16;
                    var bitInWord = bit % 16;
                    var off = word * 2;
                    EnsureAreaSize(wordArea, off + 2);
                    var area = GetOrCreateArea(wordArea);
                    var wordVal = (UInt16)((area[off] << 8) | area[off + 1]);
                    if (data[6 + i] != 0)
                        wordVal |= (UInt16)(1 << bitInWord);
                    else
                        wordVal &= (UInt16)~(1 << bitInWord);
                    area[off] = (Byte)(wordVal >> 8);
                    area[off + 1] = (Byte)(wordVal & 0xFF);
                }
            }
        }
        else
        {
            var byteCount = length * 2;
            lock (_lock)
            {
                var off = address * 2;
                EnsureAreaSize(areaCode, off + byteCount);
                var area = GetOrCreateArea(areaCode);
                Array.Copy(data, 6, area, off, byteCount);
            }
        }
    }

    /// <summary>处理存储区填充 (01:03)</summary>
    private void ProcessMemoryFill(Byte[] data)
    {
        var areaCode = data[0];
        var address = (UInt16)((data[1] << 8) | data[2]);
        var length = (UInt16)((data[4] << 8) | data[5]);
        var fillHi = data[6];
        var fillLo = data[7];

        lock (_lock)
        {
            var off = address * 2;
            EnsureAreaSize(areaCode, off + length * 2);
            var area = GetOrCreateArea(areaCode);
            for (var i = 0; i < length; i++)
            {
                area[off + i * 2] = fillHi;
                area[off + i * 2 + 1] = fillLo;
            }
        }
    }

    /// <summary>处理多区域读取 (01:04)</summary>
    private Byte[] ProcessMultipleRead(Byte[] data)
    {
        var count = data.Length / 4;
        var result = new Byte[count * 3];

        lock (_lock)
        {
            for (var i = 0; i < count; i++)
            {
                var areaCode = data[i * 4];
                var address = (UInt16)((data[i * 4 + 1] << 8) | data[i * 4 + 2]);

                result[i * 3] = areaCode;
                var area = GetOrCreateArea(areaCode);
                var off = address * 2;
                if (off + 2 <= area.Length)
                {
                    result[i * 3 + 1] = area[off];
                    result[i * 3 + 2] = area[off + 1];
                }
            }
        }
        return result;
    }

    /// <summary>处理存储区传送 (01:05)</summary>
    private void ProcessTransfer(Byte[] data)
    {
        var srcArea = data[0];
        var srcAddr = (UInt16)((data[1] << 8) | data[2]);
        var dstArea = data[4];
        var dstAddr = (UInt16)((data[5] << 8) | data[6]);
        var length = (UInt16)((data[8] << 8) | data[9]);
        var byteCount = length * 2;

        lock (_lock)
        {
            var srcOff = srcAddr * 2;
            var dstOff = dstAddr * 2;
            var srcAreaData = GetOrCreateArea(srcArea);

            var temp = new Byte[byteCount];
            var copyLen = Math.Min(byteCount, Math.Max(0, srcAreaData.Length - srcOff));
            if (copyLen > 0) Array.Copy(srcAreaData, srcOff, temp, 0, copyLen);

            EnsureAreaSize(dstArea, dstOff + byteCount);
            var dstAreaData = GetOrCreateArea(dstArea);
            Array.Copy(temp, 0, dstAreaData, dstOff, byteCount);
        }
    }

    #endregion

    #region 设备命令

    /// <summary>构建 CPU 数据响应 (05:01)</summary>
    private Byte[] BuildControllerDataResponse()
    {
        var result = new Byte[64];
        var model = Encoding.ASCII.GetBytes(CpuModel ?? "FinsUdpServer");
        Array.Copy(model, 0, result, 0, Math.Min(model.Length, 20));
        var ver = Encoding.ASCII.GetBytes(CpuVersion ?? "V1.0");
        Array.Copy(ver, 0, result, 20, Math.Min(ver.Length, 4));
        var sysVer = Encoding.ASCII.GetBytes("V1.0");
        Array.Copy(sysVer, 0, result, 24, 4);
        return result;
    }

    /// <summary>构建 CPU 状态响应 (06:01)</summary>
    private Byte[] BuildControllerStatusResponse()
    {
        var result = new Byte[8];
        result[0] = (Byte)CpuMode;
        return result;
    }

    /// <summary>构建扫描周期响应 (06:20)</summary>
    private static Byte[] BuildCycleTimeResponse()
    {
        var result = new Byte[12];
        WriteUInt32BE(result, 0, 100);
        WriteUInt32BE(result, 4, 200);
        WriteUInt32BE(result, 8, 50);
        return result;
    }

    /// <summary>构建时钟读取响应 (07:01)</summary>
    private Byte[] BuildClockReadResponse()
    {
        var dt = Clock ?? DateTime.Now;
        return new Byte[]
        {
            ToBcd((Byte)(dt.Year % 100)),
            ToBcd((Byte)dt.Month),
            ToBcd((Byte)dt.Day),
            ToBcd((Byte)dt.Hour),
            ToBcd((Byte)dt.Minute),
            ToBcd((Byte)dt.Second),
            (Byte)dt.DayOfWeek
        };
    }

    /// <summary>处理时钟写入 (07:02)</summary>
    private void ProcessClockWrite(Byte[] data)
    {
        if (data == null || data.Length < 7) return;

        var year = 2000 + FinsMessage.FromBcd(data[0]);
        var month = FinsMessage.FromBcd(data[1]);
        var day = FinsMessage.FromBcd(data[2]);
        var hour = FinsMessage.FromBcd(data[3]);
        var minute = FinsMessage.FromBcd(data[4]);
        var second = FinsMessage.FromBcd(data[5]);

        Clock = new DateTime(year, month, day, hour, minute, second);
    }

    #endregion

    #region 存储区辅助

    /// <summary>获取或创建存储区</summary>
    private Byte[] GetOrCreateArea(Byte wordAreaCode)
    {
        if (!_areas.TryGetValue(wordAreaCode, out var area))
        {
            area = new Byte[DefaultAreaSize];
            _areas[wordAreaCode] = area;
        }
        return area;
    }

    /// <summary>确保存储区大小足够</summary>
    private void EnsureAreaSize(Byte wordAreaCode, Int32 requiredBytes)
    {
        var area = GetOrCreateArea(wordAreaCode);
        if (area.Length < requiredBytes)
        {
            var newSize = Math.Max(requiredBytes + 2000, area.Length * 2);
            var newArea = new Byte[newSize];
            Array.Copy(area, 0, newArea, 0, area.Length);
            _areas[wordAreaCode] = newArea;
        }
    }

    /// <summary>读取存储区数据（用于测试验证）</summary>
    /// <param name="wordAreaCode">字访问区域代码，如 0x82 (DM)</param>
    /// <param name="address">起始地址</param>
    /// <param name="wordCount">读取字数</param>
    /// <returns>字节数组</returns>
    public Byte[] ReadMemory(Byte wordAreaCode, UInt16 address, UInt16 wordCount)
    {
        var byteCount = wordCount * 2;
        var result = new Byte[byteCount];

        lock (_lock)
        {
            var area = GetOrCreateArea(wordAreaCode);
            var off = address * 2;
            var copyLen = Math.Min(byteCount, Math.Max(0, area.Length - off));
            if (copyLen > 0) Array.Copy(area, off, result, 0, copyLen);
        }

        return result;
    }

    /// <summary>写入存储区数据（用于测试准备）</summary>
    /// <param name="wordAreaCode">字访问区域代码，如 0x82 (DM)</param>
    /// <param name="address">起始地址</param>
    /// <param name="data">写入数据</param>
    public void WriteMemory(Byte wordAreaCode, UInt16 address, Byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        lock (_lock)
        {
            var off = address * 2;
            EnsureAreaSize(wordAreaCode, off + data.Length);
            var area = GetOrCreateArea(wordAreaCode);
            Array.Copy(data, 0, area, off, data.Length);
        }
    }

    /// <summary>位区域代码转字区域代码</summary>
    private static Byte ToWordAreaCode(Byte bitAreaCode) => bitAreaCode switch
    {
        (Byte)MemoryArea.CIO_Bit => (Byte)MemoryArea.CIO_Word,
        (Byte)MemoryArea.WR_Bit => (Byte)MemoryArea.WR_Word,
        (Byte)MemoryArea.HR_Bit => (Byte)MemoryArea.HR_Word,
        (Byte)MemoryArea.AR_Bit => (Byte)MemoryArea.AR_Word,
        (Byte)MemoryArea.DM_Bit => (Byte)MemoryArea.DM_Word,
        (Byte)MemoryArea.TIM_Bit => (Byte)MemoryArea.TIM_Word,
        (Byte)MemoryArea.EM0_Bit => (Byte)MemoryArea.EM0_Word,
        (Byte)MemoryArea.EM1_Bit => (Byte)MemoryArea.EM1_Word,
        (Byte)MemoryArea.EM2_Bit => (Byte)MemoryArea.EM2_Word,
        (Byte)MemoryArea.EM3_Bit => (Byte)MemoryArea.EM3_Word,
        _ => bitAreaCode
    };

    /// <summary>大端序写入 UInt32</summary>
    private static void WriteUInt32BE(Byte[] buf, Int32 offset, UInt32 value)
    {
        buf[offset] = (Byte)(value >> 24);
        buf[offset + 1] = (Byte)(value >> 16);
        buf[offset + 2] = (Byte)(value >> 8);
        buf[offset + 3] = (Byte)(value);
    }

    /// <summary>十进制转 BCD 码</summary>
    private static Byte ToBcd(Byte value) => (Byte)((value / 10 << 4) | (value % 10));

    #endregion

    #region 日志

    /// <summary>释放资源</summary>
    public void Dispose() => Stop();

    #endregion
}
