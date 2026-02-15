using System;

namespace NewLife.Omron.Protocols;

/// <summary>
/// FINS命令代码
/// </summary>
public class FinsCommand
{
    /// <summary>主请求码 Main Request Code</summary>
    public Byte MRC { get; set; }

    /// <summary>子请求码 Sub Request Code</summary>
    public Byte SRC { get; set; }

    /// <summary>读取命令</summary>
    public static FinsCommand Read => new() { MRC = 0x01, SRC = 0x01 };

    /// <summary>写入命令</summary>
    public static FinsCommand Write => new() { MRC = 0x01, SRC = 0x02 };

    /// <summary>
    /// 转换为字节数组
    /// </summary>
    public Byte[] ToBytes() => new[] { MRC, SRC };

    /// <summary>
    /// 从字节数组解析
    /// </summary>
    public static FinsCommand Parse(Byte[] data, Int32 offset = 0)
    {
        if (data == null || data.Length < offset + 2)
            throw new ArgumentException("数据长度不足");

        return new FinsCommand
        {
            MRC = data[offset + 0],
            SRC = data[offset + 1]
        };
    }
}
