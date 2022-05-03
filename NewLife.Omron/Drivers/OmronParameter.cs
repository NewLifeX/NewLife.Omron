using System.ComponentModel;
using NewLife.IoT.Drivers;

namespace NewLife.Omron.Drivers;

/// <summary>Omron参数</summary>
public class OmronParameter : IDriverParameter
{
    /// <summary>地址。例如 127.0.0.1:9600</summary>
    [Description("地址。例如 127.0.0.1:9600")]
    public String Address { get; set; }

    /// <summary>PLC的单元号地址。默认0</summary>
    [Description("PLC的单元号地址。默认0")]
    public Byte DA2 { get; set; }

    /// <summary>数据格式。ABCD/BADC/CDAB/DCBA</summary>
    [Description("数据格式。ABCD/BADC/CDAB/DCBA")]
    public String DataFormat { get; set; }
}