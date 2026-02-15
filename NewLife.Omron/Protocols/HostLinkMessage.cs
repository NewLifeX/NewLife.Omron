using System;
using System.Text;

namespace NewLife.Omron.Protocols;

/// <summary>HostLink消息帧</summary>
/// <remarks>
/// 欧姆龙HostLink C-mode协议帧格式:
/// @{UnitNo:2}{HeaderCode:2}{Data:N}{FCS:2}*{CR}
/// 其中 FCS 为 @ 到最后一个数据字符的异或校验和。
/// 
/// 支持的命令:
/// RR - CIO区读取, WR - CIO区写入
/// RD - DM区读取,  WD - DM区写入
/// RH - HR区读取,  WH - HR区写入
/// RC - TIM/CNT PV读取, WC - TIM/CNT PV写入
/// RJ - AR区读取,  WJ - AR区写入
/// RE - EM区读取,  WE - EM区写入
/// SC - CPU状态读取
/// MS - CPU模式切换
/// TS - 测试
/// </remarks>
public class HostLinkMessage
{
    /// <summary>单元号 (0-31)</summary>
    public Byte UnitNo { get; set; }

    /// <summary>头码（命令代码）。如 RR/WR/RD/WD/SC 等</summary>
    public String HeaderCode { get; set; }

    /// <summary>数据内容（ASCII十六进制字符串）</summary>
    public String Data { get; set; }

    /// <summary>结束码（响应帧中）</summary>
    public Byte EndCode { get; set; }

    /// <summary>响应数据</summary>
    public String ResponseData { get; set; }

    #region 构造

    /// <summary>实例化HostLink消息</summary>
    public HostLinkMessage() { }

    /// <summary>实例化HostLink消息</summary>
    /// <param name="unitNo">单元号</param>
    /// <param name="headerCode">命令代码</param>
    /// <param name="data">数据</param>
    public HostLinkMessage(Byte unitNo, String headerCode, String data = null)
    {
        UnitNo = unitNo;
        HeaderCode = headerCode;
        Data = data;
    }

    #endregion

    #region 序列化

    /// <summary>将请求转换为发送字节</summary>
    /// <returns>完整的HostLink帧字节数组</returns>
    public Byte[] ToBytes()
    {
        // 构建帧内容（不含@前缀和FCS*CR后缀，用于计算FCS）
        var sb = new StringBuilder();
        sb.Append('@');
        sb.Append(UnitNo.ToString("D2"));
        sb.Append(HeaderCode ?? "");
        sb.Append(Data ?? "");

        // 计算FCS
        var frame = sb.ToString();
        var fcs = CalculateFcs(frame);
        sb.Append(fcs.ToString("X2"));
        sb.Append("*\r");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    /// <summary>解析响应帧</summary>
    /// <param name="responseBytes">响应字节数组</param>
    /// <returns>解析后的消息</returns>
    public static HostLinkMessage ParseResponse(Byte[] responseBytes)
    {
        if (responseBytes == null || responseBytes.Length < 7)
            throw new ArgumentException("HostLink响应数据长度不足");

        var response = Encoding.ASCII.GetString(responseBytes).TrimEnd('\r', '\n', '\0');

        // 验证起始字符
        if (!response.StartsWith("@"))
            throw new InvalidOperationException("HostLink响应格式错误：缺少@起始符");

        // 查找结束标记 *
        var endIdx = response.IndexOf('*');
        if (endIdx < 0)
            throw new InvalidOperationException("HostLink响应格式错误：缺少*结束符");

        // 提取并验证FCS
        var frameForFcs = response[..^3]; // 去掉 FCS(2) + *(1)
        if (response.Length >= endIdx + 1)
        {
            var fcsStr = response[(endIdx - 2)..endIdx];
            var expectedFcs = CalculateFcs(frameForFcs);
            if (!fcsStr.Equals(expectedFcs.ToString("X2"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"HostLink FCS校验失败: 期望{expectedFcs:X2}, 实际{fcsStr}");
        }

        var msg = new HostLinkMessage
        {
            UnitNo = Byte.Parse(response[1..3]),
            HeaderCode = response[3..5],
        };

        // 解析结束码和数据
        var dataSection = response[5..(endIdx - 2)];
        if (dataSection.Length >= 2)
        {
            msg.EndCode = Convert.ToByte(dataSection[..2], 16);
            msg.ResponseData = dataSection.Length > 2 ? dataSection[2..] : null;
        }

        return msg;
    }

    /// <summary>计算FCS校验和（异或校验）</summary>
    /// <param name="frame">帧字符串（从@到最后一个数据字符）</param>
    /// <returns>FCS值</returns>
    public static Byte CalculateFcs(String frame)
    {
        Byte fcs = 0;
        foreach (var ch in frame)
        {
            fcs ^= (Byte)ch;
        }
        return fcs;
    }

    #endregion

    /// <summary>检查响应是否成功</summary>
    public Boolean IsSuccess => EndCode == 0x00;

    /// <summary>获取错误消息</summary>
    public String GetErrorMessage() => EndCode switch
    {
        0x00 => "正常",
        0x01 => "不在RUN模式",
        0x02 => "不在MONITOR模式",
        0x04 => "地址超出范围",
        0x0B => "不在PROGRAM模式",
        0x13 => "FCS错误",
        0x14 => "格式错误",
        0x15 => "入口号数据错误",
        0x16 => "命令不支持",
        0x18 => "帧长度错误",
        0x19 => "不可执行",
        0x20 => "不能读取",
        0x21 => "不能写入",
        0x23 => "不在MONITOR模式",
        0xA3 => "FCS错误(在数据传输中被中止)",
        0xA4 => "格式错误(在数据传输中被中止)",
        0xA8 => "帧长度错误(在数据传输中被中止)",
        _ => $"未知错误: 0x{EndCode:X2}"
    };

    /// <summary>返回描述字符串</summary>
    public override String ToString() => $"@{UnitNo:D2}{HeaderCode}{Data}";

    #region 辅助

    /// <summary>将字节数组转换为十六进制字符串</summary>
    /// <param name="data">字节数组</param>
    /// <returns>十六进制字符串</returns>
    public static String BytesToHex(Byte[] data)
    {
        if (data == null || data.Length == 0) return "";

        var sb = new StringBuilder(data.Length * 2);
        foreach (var b in data)
        {
            sb.Append(b.ToString("X2"));
        }
        return sb.ToString();
    }

    /// <summary>将十六进制字符串转换为字节数组</summary>
    /// <param name="hex">十六进制字符串</param>
    /// <returns>字节数组</returns>
    public static Byte[] HexToBytes(String hex)
    {
        if (String.IsNullOrEmpty(hex)) return [];

        var bytes = new Byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    #endregion
}
