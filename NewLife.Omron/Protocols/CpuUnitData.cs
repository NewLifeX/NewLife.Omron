namespace NewLife.Omron.Protocols;

/// <summary>CPU单元数据</summary>
/// <remarks>对应 FINS 命令 05:01 Controller Data Read 的响应</remarks>
public class CpuUnitData
{
    /// <summary>CPU型号</summary>
    public String Model { get; set; }

    /// <summary>CPU版本</summary>
    public String Version { get; set; }

    /// <summary>系统版本</summary>
    public String SystemVersion { get; set; }

    /// <summary>区域数据容量（字）</summary>
    public UInt16 AreaDataCapacity { get; set; }

    /// <summary>程序容量（千字）</summary>
    public UInt16 ProgramCapacity { get; set; }

    /// <summary>IOM大小</summary>
    public UInt16 IomSize { get; set; }

    /// <summary>DM区容量（字）</summary>
    public UInt16 DmCapacity { get; set; }

    /// <summary>定时器/计数器数量</summary>
    public UInt16 TimerCounterCount { get; set; }

    /// <summary>EM Bank数量</summary>
    public Byte EmBankCount { get; set; }

    /// <summary>
    /// 从响应字节数组解析
    /// </summary>
    /// <param name="data">响应数据（不含命令码和结束码）</param>
    /// <returns>CPU单元数据</returns>
    public static CpuUnitData Parse(Byte[] data)
    {
        if (data == null || data.Length < 30)
            throw new ArgumentException("CPU单元数据长度不足");

        var result = new CpuUnitData();

        // 型号（前20字节ASCII）
        var modelEnd = 20;
        for (var i = 0; i < 20; i++)
        {
            if (data[i] == 0x00) { modelEnd = i; break; }
        }
        result.Model = System.Text.Encoding.ASCII.GetString(data, 0, modelEnd).Trim();

        // 版本（第20-23字节ASCII）
        result.Version = System.Text.Encoding.ASCII.GetString(data, 20, 4).Trim();

        // 系统版本（第24-27字节ASCII）
        if (data.Length >= 28)
            result.SystemVersion = System.Text.Encoding.ASCII.GetString(data, 24, 4).Trim();

        // 后续字段根据具体PLC型号可能有差异
        if (data.Length >= 30)
        {
            result.AreaDataCapacity = (UInt16)((data[28] << 8) | data[29]);
        }

        return result;
    }

    /// <summary>返回描述字符串</summary>
    public override String ToString() => $"CPU型号={Model}, 版本={Version}, 系统版本={SystemVersion}";
}
