using System;

namespace NewLife.Omron.Protocols;

/// <summary>FINS命令代码</summary>
/// <remarks>
/// 每个FINS命令由主请求码(MRC)和子请求码(SRC)标识。
/// 参考欧姆龙 W342-E1 FINS Commands Reference Manual。
/// </remarks>
public class FinsCommand
{
    /// <summary>主请求码 Main Request Code</summary>
    public Byte MRC { get; set; }

    /// <summary>子请求码 Sub Request Code</summary>
    public Byte SRC { get; set; }

    #region 存储区操作 (MRC=0x01)

    /// <summary>存储区读取 (01:01)</summary>
    public static FinsCommand MemoryAreaRead => new() { MRC = 0x01, SRC = 0x01 };

    /// <summary>存储区写入 (01:02)</summary>
    public static FinsCommand MemoryAreaWrite => new() { MRC = 0x01, SRC = 0x02 };

    /// <summary>存储区填充 (01:03)</summary>
    public static FinsCommand MemoryAreaFill => new() { MRC = 0x01, SRC = 0x03 };

    /// <summary>多区域读取 (01:04)</summary>
    public static FinsCommand MultipleMemoryAreaRead => new() { MRC = 0x01, SRC = 0x04 };

    /// <summary>存储区传送 (01:05)</summary>
    public static FinsCommand MemoryAreaTransfer => new() { MRC = 0x01, SRC = 0x05 };

    /// <summary>读取命令（存储区读取的别名）</summary>
    public static FinsCommand Read => MemoryAreaRead;

    /// <summary>写入命令（存储区写入的别名）</summary>
    public static FinsCommand Write => MemoryAreaWrite;

    #endregion

    #region 参数区操作 (MRC=0x02)

    /// <summary>参数区读取 (02:01)</summary>
    public static FinsCommand ParameterAreaRead => new() { MRC = 0x02, SRC = 0x01 };

    /// <summary>参数区写入 (02:02)</summary>
    public static FinsCommand ParameterAreaWrite => new() { MRC = 0x02, SRC = 0x02 };

    /// <summary>参数区清除 (02:03)</summary>
    public static FinsCommand ParameterAreaClear => new() { MRC = 0x02, SRC = 0x03 };

    #endregion

    #region 程序区操作 (MRC=0x03)

    /// <summary>程序区读取 (03:06)</summary>
    public static FinsCommand ProgramAreaRead => new() { MRC = 0x03, SRC = 0x06 };

    /// <summary>程序区写入 (03:07)</summary>
    public static FinsCommand ProgramAreaWrite => new() { MRC = 0x03, SRC = 0x07 };

    /// <summary>程序区清除 (03:08)</summary>
    public static FinsCommand ProgramAreaClear => new() { MRC = 0x03, SRC = 0x08 };

    #endregion

    #region CPU控制 (MRC=0x04)

    /// <summary>CPU运行 (04:01)</summary>
    public static FinsCommand Run => new() { MRC = 0x04, SRC = 0x01 };

    /// <summary>CPU停止 (04:02)</summary>
    public static FinsCommand Stop => new() { MRC = 0x04, SRC = 0x02 };

    #endregion

    #region 设备信息 (MRC=0x05/0x06)

    /// <summary>控制器数据读取 (05:01)</summary>
    public static FinsCommand ControllerDataRead => new() { MRC = 0x05, SRC = 0x01 };

    /// <summary>连接数据读取 (05:02)</summary>
    public static FinsCommand ConnectionDataRead => new() { MRC = 0x05, SRC = 0x02 };

    /// <summary>控制器状态读取 (06:01)</summary>
    public static FinsCommand ControllerStatusRead => new() { MRC = 0x06, SRC = 0x01 };

    /// <summary>网络状态读取 (06:06)</summary>
    public static FinsCommand NetworkStatusRead => new() { MRC = 0x06, SRC = 0x06 };

    /// <summary>扫描周期读取 (06:20)</summary>
    public static FinsCommand CycleTimeRead => new() { MRC = 0x06, SRC = 0x20 };

    #endregion

    #region 时钟操作 (MRC=0x07)

    /// <summary>时钟读取 (07:01)</summary>
    public static FinsCommand ClockRead => new() { MRC = 0x07, SRC = 0x01 };

    /// <summary>时钟写入 (07:02)</summary>
    public static FinsCommand ClockWrite => new() { MRC = 0x07, SRC = 0x02 };

    #endregion

    #region 消息操作 (MRC=0x09)

    /// <summary>消息读取/清除/FAL读取 (09:20)</summary>
    public static FinsCommand MessageRead => new() { MRC = 0x09, SRC = 0x20 };

    #endregion

    #region 访问控制 (MRC=0x0C)

    /// <summary>获取访问权 (0C:01)</summary>
    public static FinsCommand AccessRightAcquire => new() { MRC = 0x0C, SRC = 0x01 };

    /// <summary>强制获取访问权 (0C:02)</summary>
    public static FinsCommand AccessRightForcedAcquire => new() { MRC = 0x0C, SRC = 0x02 };

    /// <summary>释放访问权 (0C:03)</summary>
    public static FinsCommand AccessRightRelease => new() { MRC = 0x0C, SRC = 0x03 };

    #endregion

    #region 错误处理 (MRC=0x21)

    /// <summary>错误清除 (21:01)</summary>
    public static FinsCommand ErrorClear => new() { MRC = 0x21, SRC = 0x01 };

    /// <summary>错误日志读取 (21:02)</summary>
    public static FinsCommand ErrorLogRead => new() { MRC = 0x21, SRC = 0x02 };

    /// <summary>错误日志清除 (21:03)</summary>
    public static FinsCommand ErrorLogClear => new() { MRC = 0x21, SRC = 0x03 };

    #endregion

    /// <summary>转换为字节数组</summary>
    public Byte[] ToBytes() => [MRC, SRC];

    /// <summary>从字节数组解析</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">起始偏移</param>
    /// <returns>命令对象</returns>
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

    /// <summary>返回描述字符串</summary>
    public override String ToString() => $"{MRC:X2}:{SRC:X2}";
}
