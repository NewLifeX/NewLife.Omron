using System;
using System.Linq;

namespace NewLife.Omron.Protocols;

/// <summary>FINS地址解析</summary>
/// <remarks>
/// 支持欧姆龙PLC所有存储区的地址解析。
/// 地址格式: {区域前缀}{地址}[.{位偏移}]
/// 示例: D100, CIO100.5, W200, H10, TIM5, CNT3, EM0:100
/// </remarks>
public class FinsAddress
{
    /// <summary>存储区类型（字访问代码）</summary>
    public Byte MemoryType { get; set; }

    /// <summary>地址</summary>
    public UInt16 Address { get; set; }

    /// <summary>位偏移</summary>
    public Byte BitOffset { get; set; }

    /// <summary>是否为位访问地址</summary>
    public Boolean IsBit { get; set; }

    /// <summary>获取位访问区域代码</summary>
    public Byte BitAreaCode => MemoryAreaHelper.GetBitAreaCode(MemoryType);

    /// <summary>
    /// 解析地址字符串
    /// </summary>
    /// <param name="address">地址字符串。支持格式: D100, CIO100.5, W100, H100, A100, DM100, EM0:100, TIM5, CNT3, IR0, DR0</param>
    /// <returns>解析后的地址对象</returns>
    public static FinsAddress Parse(String address)
    {
        if (String.IsNullOrEmpty(address))
            throw new ArgumentException("地址不能为空");

        address = address.Trim().ToUpper();

        var result = new FinsAddress();

        // 识别存储区类型
        if (address.StartsWith("CIO"))
        {
            result.MemoryType = (Byte)MemoryArea.CIO_Word;
            address = address[3..];
        }
        else if (address.StartsWith("WR"))
        {
            result.MemoryType = (Byte)MemoryArea.WR_Word;
            address = address[2..];
        }
        else if (address.StartsWith("HR"))
        {
            result.MemoryType = (Byte)MemoryArea.HR_Word;
            address = address[2..];
        }
        else if (address.StartsWith("AR"))
        {
            result.MemoryType = (Byte)MemoryArea.AR_Word;
            address = address[2..];
        }
        else if (address.StartsWith("DM"))
        {
            result.MemoryType = (Byte)MemoryArea.DM_Word;
            address = address[2..];
        }
        else if (address.StartsWith("TIM"))
        {
            result.MemoryType = (Byte)MemoryArea.TIM_Word;
            address = address[3..];
        }
        else if (address.StartsWith("CNT"))
        {
            result.MemoryType = (Byte)MemoryArea.TIM_Word; // TIM/CNT共用区域代码
            address = address[3..];
        }
        else if (address.StartsWith("EM"))
        {
            // EM Bank 格式: EM0:100 或 EM100（当前Bank）
            address = address[2..];
            var colonIdx = address.IndexOf(':');
            if (colonIdx > 0)
            {
                var bank = Int32.Parse(address[..colonIdx]);
                result.MemoryType = (Byte)(0xA0 + bank);
                address = address[(colonIdx + 1)..];
            }
            else
            {
                // 无 Bank 编号时默认为 Bank0（EM0 区域代码 0xA0）
                result.MemoryType = (Byte)MemoryArea.EM0_Word;
            }
        }
        else if (address.StartsWith("IR"))
        {
            result.MemoryType = (Byte)MemoryArea.IR_Word;
            address = address[2..];
        }
        else if (address.StartsWith("DR"))
        {
            result.MemoryType = (Byte)MemoryArea.DR_Word;
            address = address[2..];
        }
        else if (address.StartsWith("W"))
        {
            result.MemoryType = (Byte)MemoryArea.WR_Word;
            address = address[1..];
        }
        else if (address.StartsWith("H"))
        {
            result.MemoryType = (Byte)MemoryArea.HR_Word;
            address = address[1..];
        }
        else if (address.StartsWith("A"))
        {
            result.MemoryType = (Byte)MemoryArea.AR_Word;
            address = address[1..];
        }
        else if (address.StartsWith("D"))
        {
            result.MemoryType = (Byte)MemoryArea.DM_Word;
            address = address[1..];
        }
        else if (address.StartsWith("C"))
        {
            // C 前缀兴1Byte：Channel I/O，兼容 C/CV 系列，区域代码 0x80
            result.MemoryType = (Byte)MemoryArea.IO_Word;
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
                result.IsBit = true;
            }
        }

        return result;
    }

    /// <summary>转换为字节数组（用于FINS字访问命令）</summary>
    public Byte[] ToBytes()
    {
        return
        [
            MemoryType,
            (Byte)(Address >> 8),
            (Byte)(Address & 0xFF),
            BitOffset
        ];
    }

    /// <summary>转换为位访问字节数组（用于FINS位操作命令）</summary>
    public Byte[] ToBitBytes()
    {
        return
        [
            BitAreaCode,
            (Byte)(Address >> 8),
            (Byte)(Address & 0xFF),
            BitOffset
        ];
    }

    /// <summary>返回描述字符串</summary>
    public override String ToString()
    {
        var area = MemoryType switch
        {
            (Byte)MemoryArea.CIO_Word => "CIO",
            (Byte)MemoryArea.WR_Word => "WR",
            (Byte)MemoryArea.HR_Word => "HR",
            (Byte)MemoryArea.AR_Word => "AR",
            (Byte)MemoryArea.DM_Word => "DM",
            (Byte)MemoryArea.TIM_Word => "TIM",
            (Byte)MemoryArea.IR_Word => "IR",
            (Byte)MemoryArea.DR_Word => "DR",
            (Byte)MemoryArea.EM_Current_Word => "EM",
            >= (Byte)MemoryArea.EM0_Word and <= (Byte)MemoryArea.EM3_Word =>
                $"EM{MemoryType - (Byte)MemoryArea.EM0_Word}",
            _ => $"0x{MemoryType:X2}"
        };

        return IsBit ? $"{area}{Address}.{BitOffset}" : $"{area}{Address}";
    }
}
