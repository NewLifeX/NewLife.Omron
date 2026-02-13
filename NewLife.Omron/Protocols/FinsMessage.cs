using System;
using System.Linq;

namespace NewLife.Omron.Protocols;

/// <summary>
/// FINS消息
/// </summary>
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

    /// <summary>
    /// 构建读取请求
    /// </summary>
    public static FinsMessage BuildReadRequest(FinsAddress address, UInt16 length, Byte da2 = 0)
    {
        var msg = new FinsMessage
        {
            Header = new FinsHeader { DA2 = da2 },
            Command = FinsCommand.Read
        };

        // 构建读取命令数据
        var addrBytes = address.ToBytes();
        var data = new Byte[6];
        Array.Copy(addrBytes, 0, data, 0, 4);
        data[4] = (Byte)(length >> 8);
        data[5] = (Byte)(length & 0xFF);

        msg.Data = data;
        return msg;
    }

    /// <summary>
    /// 构建写入请求
    /// </summary>
    public static FinsMessage BuildWriteRequest(FinsAddress address, Byte[] data, Byte da2 = 0)
    {
        var msg = new FinsMessage
        {
            Header = new FinsHeader { DA2 = da2 },
            Command = FinsCommand.Write
        };

        // 验证数据长度（必须是偶数，因为PLC以字为单位）
        if (data.Length % 2 != 0)
            throw new ArgumentException($"写入数据长度必须是偶数（字对齐），当前长度: {data.Length}");

        // 构建写入命令数据
        var addrBytes = address.ToBytes();
        var length = (UInt16)(data.Length / 2); // 字长度

        var cmdData = new Byte[6 + data.Length];
        Array.Copy(addrBytes, 0, cmdData, 0, 4);
        cmdData[4] = (Byte)(length >> 8);
        cmdData[5] = (Byte)(length & 0xFF);
        Array.Copy(data, 0, cmdData, 6, data.Length);

        msg.Data = cmdData;
        return msg;
    }

    /// <summary>
    /// 转换为字节数组
    /// </summary>
    public Byte[] ToBytes()
    {
        var headerBytes = Header.ToBytes();
        var commandBytes = Command.ToBytes();
        var dataBytes = Data ?? Array.Empty<Byte>();

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

    /// <summary>
    /// 从字节数组解析响应
    /// </summary>
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

    /// <summary>
    /// 检查响应是否成功
    /// </summary>
    public Boolean IsSuccess => EndCode == 0x0000;

    /// <summary>
    /// 获取错误消息
    /// </summary>
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
            _ => $"未知错误代码: 0x{EndCode:X4}"
        };
    }
}
