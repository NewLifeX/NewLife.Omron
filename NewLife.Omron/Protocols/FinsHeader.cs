using System;

namespace NewLife.Omron.Protocols;

/// <summary>
/// FINS协议头部
/// </summary>
public class FinsHeader
{
    /// <summary>信息控制字段 Information Control Field</summary>
    public Byte ICF { get; set; } = 0x80;

    /// <summary>保留字段 Reserved</summary>
    public Byte RSV { get; set; } = 0x00;

    /// <summary>网关计数 Gateway Count</summary>
    public Byte GCT { get; set; } = 0x02;

    /// <summary>目标网络地址 Destination Network Address</summary>
    public Byte DNA { get; set; } = 0x00;

    /// <summary>目标节点地址 Destination Node Address</summary>
    public Byte DA1 { get; set; } = 0x00;

    /// <summary>目标单元地址 Destination Unit Address</summary>
    public Byte DA2 { get; set; } = 0x00;

    /// <summary>源网络地址 Source Network Address</summary>
    public Byte SNA { get; set; } = 0x00;

    /// <summary>源节点地址 Source Node Address</summary>
    public Byte SA1 { get; set; } = 0x00;

    /// <summary>源单元地址 Source Unit Address</summary>
    public Byte SA2 { get; set; } = 0x00;

    /// <summary>服务ID Service ID</summary>
    public Byte SID { get; set; } = 0x00;

    /// <summary>
    /// 转换为字节数组
    /// </summary>
    public Byte[] ToBytes()
    {
        return new[]
        {
            ICF, RSV, GCT, DNA, DA1, DA2, SNA, SA1, SA2, SID
        };
    }

    /// <summary>
    /// 从字节数组解析
    /// </summary>
    public static FinsHeader Parse(Byte[] data, Int32 offset = 0)
    {
        if (data == null || data.Length < offset + 10)
            throw new ArgumentException("数据长度不足");

        return new FinsHeader
        {
            ICF = data[offset + 0],
            RSV = data[offset + 1],
            GCT = data[offset + 2],
            DNA = data[offset + 3],
            DA1 = data[offset + 4],
            DA2 = data[offset + 5],
            SNA = data[offset + 6],
            SA1 = data[offset + 7],
            SA2 = data[offset + 8],
            SID = data[offset + 9]
        };
    }
}
