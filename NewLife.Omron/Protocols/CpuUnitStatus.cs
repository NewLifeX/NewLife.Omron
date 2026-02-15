namespace NewLife.Omron.Protocols;

/// <summary>CPU单元状态</summary>
/// <remarks>对应 FINS 命令 06:01 Controller Status Read 的响应</remarks>
public class CpuUnitStatus
{
    /// <summary>运行模式</summary>
    public CpuMode Mode { get; set; }

    /// <summary>是否致命错误</summary>
    public Boolean FatalError { get; set; }

    /// <summary>是否非致命错误</summary>
    public Boolean NonFatalError { get; set; }

    /// <summary>致命错误数据（2字节）</summary>
    public UInt16 FatalErrorData { get; set; }

    /// <summary>非致命错误数据（2字节）</summary>
    public UInt16 NonFatalErrorData { get; set; }

    /// <summary>消息标志（2字节）</summary>
    public UInt16 MessageFlags { get; set; }

    /// <summary>
    /// 从响应字节数组解析
    /// </summary>
    /// <param name="data">响应数据（不含命令码和结束码）</param>
    /// <returns>CPU单元状态</returns>
    public static CpuUnitStatus Parse(Byte[] data)
    {
        if (data == null || data.Length < 6)
            throw new ArgumentException("CPU状态数据长度不足");

        var result = new CpuUnitStatus();

        // 运行状态
        result.Mode = (CpuMode)data[0];

        // 致命错误标志
        result.FatalError = data[1] != 0;

        // 致命错误数据
        result.FatalErrorData = (UInt16)((data[2] << 8) | data[3]);

        // 非致命错误数据
        if (data.Length >= 6)
        {
            result.NonFatalError = data[4] != 0;
            result.NonFatalErrorData = (UInt16)((data[4] << 8) | data[5]);
        }

        // 消息标志
        if (data.Length >= 8)
        {
            result.MessageFlags = (UInt16)((data[6] << 8) | data[7]);
        }

        return result;
    }

    /// <summary>返回描述字符串</summary>
    public override String ToString() => $"模式={Mode}, 致命错误={FatalError}, 非致命错误={NonFatalError}";
}

/// <summary>CPU运行模式</summary>
public enum CpuMode : Byte
{
    /// <summary>编程模式</summary>
    Program = 0x00,

    /// <summary>调试模式</summary>
    Debug = 0x01,

    /// <summary>监控模式</summary>
    Monitor = 0x02,

    /// <summary>运行模式</summary>
    Run = 0x04
}
