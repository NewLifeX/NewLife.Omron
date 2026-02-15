using System;
using System.Linq;

namespace NewLife.Omron.Protocols;

/// <summary>FINS消息</summary>
/// <remarks>
/// FINS消息由头部、命令码和数据三部分组成。
/// 对于响应消息，还包含结束代码(EndCode)表示操作结果。
/// </remarks>
public class FinsMessage
{
    /// <summary>头部</summary>
    public FinsHeader Header { get; set; } = new FinsHeader();

    /// <summary>命令</summary>
    public FinsCommand Command { get; set; }

    /// <summary>数据</summary>
    public Byte[] Data { get; set; }

    /// <summary>结束代码 (响应)</summary>
    public UInt16 EndCode { get; set; }

    #region 存储区操作

    /// <summary>构建字读取请求</summary>
    /// <param name="address">地址</param>
    /// <param name="length">读取字数</param>
    /// <param name="da2">目标单元地址</param>
    /// <returns>FINS消息</returns>
    public static FinsMessage BuildReadRequest(FinsAddress address, UInt16 length, Byte da2 = 0)
    {
        var msg = new FinsMessage
        {
            Header = new FinsHeader { DA2 = da2 },
            Command = FinsCommand.MemoryAreaRead
        };

        // 构建读取命令数据：地址(4) + 长度(2)
        var addrBytes = address.ToBytes();
        var data = new Byte[6];
        Array.Copy(addrBytes, 0, data, 0, 4);
        data[4] = (Byte)(length >> 8);
        data[5] = (Byte)(length & 0xFF);

        msg.Data = data;
        return msg;
    }

    /// <summary>构建位读取请求</summary>
    /// <param name="address">地址（含位偏移）</param>
    /// <param name="length">读取位数</param>
    /// <param name="da2">目标单元地址</param>
    /// <returns>FINS消息</returns>
    public static FinsMessage BuildBitReadRequest(FinsAddress address, UInt16 length, Byte da2 = 0)
    {
        var msg = new FinsMessage
        {
            Header = new FinsHeader { DA2 = da2 },
            Command = FinsCommand.MemoryAreaRead
        };

        var addrBytes = address.ToBitBytes();
        var data = new Byte[6];
        Array.Copy(addrBytes, 0, data, 0, 4);
        data[4] = (Byte)(length >> 8);
        data[5] = (Byte)(length & 0xFF);

        msg.Data = data;
        return msg;
    }

    /// <summary>构建字写入请求</summary>
    /// <param name="address">地址</param>
    /// <param name="data">写入数据</param>
    /// <param name="da2">目标单元地址</param>
    /// <returns>FINS消息</returns>
    public static FinsMessage BuildWriteRequest(FinsAddress address, Byte[] data, Byte da2 = 0)
    {
        var msg = new FinsMessage
        {
            Header = new FinsHeader { DA2 = da2 },
            Command = FinsCommand.MemoryAreaWrite
        };

        // 验证数据长度（必须是偶数，因为PLC以字为单位）
        if (data.Length % 2 != 0)
            throw new ArgumentException($"写入数据长度必须是偶数（字对齐），当前长度: {data.Length}");

        // 构建写入命令数据：地址(4) + 长度(2) + 数据(N)
        var addrBytes = address.ToBytes();
        var length = (UInt16)(data.Length / 2);

        var cmdData = new Byte[6 + data.Length];
        Array.Copy(addrBytes, 0, cmdData, 0, 4);
        cmdData[4] = (Byte)(length >> 8);
        cmdData[5] = (Byte)(length & 0xFF);
        Array.Copy(data, 0, cmdData, 6, data.Length);

        msg.Data = cmdData;
        return msg;
    }

    /// <summary>构建位写入请求</summary>
    /// <param name="address">地址（含位偏移）</param>
    /// <param name="values">位值数组（每个元素0或1）</param>
    /// <param name="da2">目标单元地址</param>
    /// <returns>FINS消息</returns>
    public static FinsMessage BuildBitWriteRequest(FinsAddress address, Byte[] values, Byte da2 = 0)
    {
        var msg = new FinsMessage
        {
            Header = new FinsHeader { DA2 = da2 },
            Command = FinsCommand.MemoryAreaWrite
        };

        // 构建位写入命令数据：地址(4) + 长度(2) + 数据(N)
        var addrBytes = address.ToBitBytes();
        var length = (UInt16)values.Length;

        var cmdData = new Byte[6 + values.Length];
        Array.Copy(addrBytes, 0, cmdData, 0, 4);
        cmdData[4] = (Byte)(length >> 8);
        cmdData[5] = (Byte)(length & 0xFF);
        Array.Copy(values, 0, cmdData, 6, values.Length);

        msg.Data = cmdData;
        return msg;
    }

    /// <summary>构建存储区填充请求</summary>
    /// <param name="address">起始地址</param>
    /// <param name="length">填充字数</param>
    /// <param name="fillValue">填充值（2字节）</param>
    /// <param name="da2">目标单元地址</param>
    /// <returns>FINS消息</returns>
    public static FinsMessage BuildFillRequest(FinsAddress address, UInt16 length, UInt16 fillValue, Byte da2 = 0)
    {
        var msg = new FinsMessage
        {
            Header = new FinsHeader { DA2 = da2 },
            Command = FinsCommand.MemoryAreaFill
        };

        // 地址(4) + 长度(2) + 填充值(2)
        var addrBytes = address.ToBytes();
        var data = new Byte[8];
        Array.Copy(addrBytes, 0, data, 0, 4);
        data[4] = (Byte)(length >> 8);
        data[5] = (Byte)(length & 0xFF);
        data[6] = (Byte)(fillValue >> 8);
        data[7] = (Byte)(fillValue & 0xFF);

        msg.Data = data;
        return msg;
    }

    /// <summary>构建多区域读取请求</summary>
    /// <param name="addresses">地址列表</param>
    /// <param name="da2">目标单元地址</param>
    /// <returns>FINS消息</returns>
    public static FinsMessage BuildMultipleReadRequest(FinsAddress[] addresses, Byte da2 = 0)
    {
        var msg = new FinsMessage
        {
            Header = new FinsHeader { DA2 = da2 },
            Command = FinsCommand.MultipleMemoryAreaRead
        };

        // 每个地址4字节
        var data = new Byte[addresses.Length * 4];
        for (var i = 0; i < addresses.Length; i++)
        {
            var addrBytes = addresses[i].ToBytes();
            Array.Copy(addrBytes, 0, data, i * 4, 4);
        }

        msg.Data = data;
        return msg;
    }

    /// <summary>构建存储区传送请求</summary>
    /// <param name="source">源地址</param>
    /// <param name="destination">目标地址</param>
    /// <param name="length">传送字数</param>
    /// <param name="da2">目标单元地址</param>
    /// <returns>FINS消息</returns>
    public static FinsMessage BuildTransferRequest(FinsAddress source, FinsAddress destination, UInt16 length, Byte da2 = 0)
    {
        var msg = new FinsMessage
        {
            Header = new FinsHeader { DA2 = da2 },
            Command = FinsCommand.MemoryAreaTransfer
        };

        // 源地址(4) + 目标地址(4) + 长度(2)
        var srcBytes = source.ToBytes();
        var dstBytes = destination.ToBytes();
        var data = new Byte[10];
        Array.Copy(srcBytes, 0, data, 0, 4);
        Array.Copy(dstBytes, 0, data, 4, 4);
        data[8] = (Byte)(length >> 8);
        data[9] = (Byte)(length & 0xFF);

        msg.Data = data;
        return msg;
    }

    #endregion

    #region 设备操作

    /// <summary>构建无数据命令请求（用于Run/Stop/ClockRead/ErrorClear等）</summary>
    /// <param name="command">命令</param>
    /// <param name="data">可选的命令参数数据</param>
    /// <param name="da2">目标单元地址</param>
    /// <returns>FINS消息</returns>
    public static FinsMessage BuildCommandRequest(FinsCommand command, Byte[] data = null, Byte da2 = 0)
    {
        return new FinsMessage
        {
            Header = new FinsHeader { DA2 = da2 },
            Command = command,
            Data = data
        };
    }

    /// <summary>构建CPU运行请求</summary>
    /// <param name="mode">运行模式。0x04=RUN, 0x02=MONITOR</param>
    /// <param name="da2">目标单元地址</param>
    /// <returns>FINS消息</returns>
    public static FinsMessage BuildRunRequest(Byte mode = 0x04, Byte da2 = 0) =>
        BuildCommandRequest(FinsCommand.Run, [0xFF, 0xFF, mode, 0x00], da2);

    /// <summary>构建CPU停止请求</summary>
    /// <param name="da2">目标单元地址</param>
    /// <returns>FINS消息</returns>
    public static FinsMessage BuildStopRequest(Byte da2 = 0) =>
        BuildCommandRequest(FinsCommand.Stop, null, da2);

    /// <summary>构建时钟写入请求</summary>
    /// <param name="dateTime">要设置的时间</param>
    /// <param name="da2">目标单元地址</param>
    /// <returns>FINS消息</returns>
    public static FinsMessage BuildClockWriteRequest(DateTime dateTime, Byte da2 = 0)
    {
        // BCD编码：年(2) 月(1) 日(1) 时(1) 分(1) 秒(1) 星期(1)
        var data = new Byte[7];
        var year = dateTime.Year % 100;
        data[0] = ToBcd((Byte)year);
        data[1] = ToBcd((Byte)dateTime.Month);
        data[2] = ToBcd((Byte)dateTime.Day);
        data[3] = ToBcd((Byte)dateTime.Hour);
        data[4] = ToBcd((Byte)dateTime.Minute);
        data[5] = ToBcd((Byte)dateTime.Second);
        data[6] = (Byte)dateTime.DayOfWeek;

        return BuildCommandRequest(FinsCommand.ClockWrite, data, da2);
    }

    /// <summary>构建错误日志读取请求</summary>
    /// <param name="startRecord">起始记录号</param>
    /// <param name="count">读取记录数</param>
    /// <param name="da2">目标单元地址</param>
    /// <returns>FINS消息</returns>
    public static FinsMessage BuildErrorLogReadRequest(UInt16 startRecord, UInt16 count, Byte da2 = 0)
    {
        var data = new Byte[4];
        data[0] = (Byte)(startRecord >> 8);
        data[1] = (Byte)(startRecord & 0xFF);
        data[2] = (Byte)(count >> 8);
        data[3] = (Byte)(count & 0xFF);

        return BuildCommandRequest(FinsCommand.ErrorLogRead, data, da2);
    }

    #endregion

    #region 序列化

    /// <summary>转换为字节数组</summary>
    public Byte[] ToBytes()
    {
        var headerBytes = Header.ToBytes();
        var commandBytes = Command.ToBytes();
        var dataBytes = Data ?? [];

        var result = new Byte[headerBytes.Length + commandBytes.Length + dataBytes.Length];
        var offset = 0;

        Array.Copy(headerBytes, 0, result, offset, headerBytes.Length);
        offset += headerBytes.Length;

        Array.Copy(commandBytes, 0, result, offset, commandBytes.Length);
        offset += commandBytes.Length;

        if (dataBytes.Length > 0)
        {
            Array.Copy(dataBytes, 0, result, offset, dataBytes.Length);
        }

        return result;
    }

    /// <summary>从字节数组解析响应</summary>
    /// <param name="data">响应数据（FINS帧，不含TCP头）</param>
    /// <returns>解析后的消息</returns>
    public static FinsMessage ParseResponse(Byte[] data)
    {
        if (data == null || data.Length < 14)
            throw new ArgumentException("响应数据长度不足");

        var msg = new FinsMessage
        {
            Header = FinsHeader.Parse(data, 0),
            Command = FinsCommand.Parse(data, 10)
        };

        // 解析结束代码
        msg.EndCode = (UInt16)((data[12] << 8) | data[13]);

        // 提取响应数据
        if (data.Length > 14)
        {
            msg.Data = new Byte[data.Length - 14];
            Array.Copy(data, 14, msg.Data, 0, msg.Data.Length);
        }

        return msg;
    }

    #endregion

    /// <summary>检查响应是否成功</summary>
    public Boolean IsSuccess => EndCode == 0x0000;

    /// <summary>获取错误消息</summary>
    public String GetErrorMessage()
    {
        return EndCode switch
        {
            0x0000 => "成功",
            0x0001 => "服务被取消",
            0x0101 => "本地节点不在网络中",
            0x0102 => "令牌超时",
            0x0103 => "重试失败",
            0x0104 => "发送帧数超过最大值",
            0x0105 => "节点地址范围错误",
            0x0106 => "节点地址重复",
            0x0201 => "目标节点不在网络中",
            0x0202 => "没有可用单元",
            0x0203 => "第三个节点不存在",
            0x0204 => "目标节点繁忙",
            0x0205 => "响应超时",
            0x0301 => "通信控制器错误",
            0x0302 => "CPU单元错误",
            0x0303 => "控制器板错误",
            0x0304 => "单元号错误",
            0x0401 => "未定义的命令",
            0x0402 => "不支持的命令",
            0x0501 => "目标地址设置错误",
            0x0502 => "路由表错误",
            0x0503 => "路由表未注册",
            0x0504 => "路由错误",
            0x1001 => "命令格式错误",
            0x1002 => "参数错误",
            0x1003 => "读取长度过长",
            0x1004 => "写入长度过长",
            0x1101 => "程序区域错误",
            0x1102 => "访问大小错误",
            0x1103 => "地址范围错误",
            0x1104 => "地址超出范围",
            0x2002 => "被保护",
            0x2003 => "不能进入指定的模式",
            0x2004 => "PLC正在运行",
            0x2005 => "PLC已停止",
            0x2006 => "程序不存在",
            0x2007 => "文件不存在",
            0x2101 => "内存错误",
            0x2502 => "访问权已被占用",
            _ => $"未知错误代码: 0x{EndCode:X4}"
        };
    }

    #region 辅助

    /// <summary>十进制转BCD码</summary>
    /// <param name="value">十进制值(0-99)</param>
    /// <returns>BCD编码值</returns>
    private static Byte ToBcd(Byte value) => (Byte)((value / 10 << 4) | (value % 10));

    /// <summary>BCD码转十进制</summary>
    /// <param name="bcd">BCD编码值</param>
    /// <returns>十进制值</returns>
    public static Byte FromBcd(Byte bcd) => (Byte)((bcd >> 4) * 10 + (bcd & 0x0F));

    #endregion
}
