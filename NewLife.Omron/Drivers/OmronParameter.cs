using NewLife.IoT.Drivers;

namespace NewLife.Omron.Drivers;

/// <summary>Omron参数</summary>
public class OmronParameter : IDriverParameter
{
    /// <summary>地址</summary>
    public String Address { get; set; }

    /// <summary>PLC的单元号地址，通常都为0</summary>
    public Byte DA2 { get; set; }

    /// <summary>数据格式。ABCD/BADC/CDAB/DCBA</summary>
    public String DataFormat { get; set; }
}