using System;
using System.Linq;

namespace NewLife.Omron.Protocols;

/// <summary>
/// FINS地址解析
/// </summary>
public class FinsAddress
{
    /// <summary>存储区类型</summary>
    public Byte MemoryType { get; set; }

    /// <summary>地址</summary>
    public UInt16 Address { get; set; }

    /// <summary>位偏移</summary>
    public Byte BitOffset { get; set; }

    /// <summary>
    /// 解析地址字符串
    /// 支持格式: D100, CIO100, W100, H100, A100, DM100等
    /// </summary>
    public static FinsAddress Parse(String address)
    {
        if (String.IsNullOrEmpty(address))
            throw new ArgumentException("地址不能为空");

        address = address.Trim().ToUpper();

        var result = new FinsAddress();

        // 识别存储区类型
        if (address.StartsWith("CIO"))
        {
            result.MemoryType = 0xB0; // CIO区
            address = address[3..];
        }
        else if (address.StartsWith("WR") || address.StartsWith("W"))
        {
            result.MemoryType = 0xB1; // WR区
            address = address.StartsWith("WR") ? address[2..] : address[1..];
        }
        else if (address.StartsWith("HR") || address.StartsWith("H"))
        {
            result.MemoryType = 0xB2; // HR区
            address = address.StartsWith("HR") ? address[2..] : address[1..];
        }
        else if (address.StartsWith("AR") || address.StartsWith("A"))
        {
            result.MemoryType = 0xB3; // AR区
            address = address.StartsWith("AR") ? address[2..] : address[1..];
        }
        else if (address.StartsWith("DM") || address.StartsWith("D"))
        {
            result.MemoryType = 0x82; // DM区
            address = address.StartsWith("DM") ? address[2..] : address[1..];
        }
        else if (address.StartsWith("EM"))
        {
            result.MemoryType = 0xA0; // EM区
            address = address[2..];
        }
        else if (address.StartsWith("C"))
        {
            result.MemoryType = 0x80; // CIO位区
            address = address[1..];
        }
        else
        {
            throw new ArgumentException($"不支持的地址类型: {address}");
        }

        // 解析地址和位偏移
        var parts = address.Split('.');
        if (parts.Length > 0)
        {
            result.Address = UInt16.Parse(parts[0]);
            if (parts.Length > 1)
            {
                result.BitOffset = Byte.Parse(parts[1]);
            }
        }

        return result;
    }

    /// <summary>
    /// 转换为字节数组 (用于FINS命令)
    /// </summary>
    public Byte[] ToBytes()
    {
        return new[]
        {
            MemoryType,
            (Byte)(Address >> 8),
            (Byte)(Address & 0xFF),
            BitOffset
        };
    }
}
