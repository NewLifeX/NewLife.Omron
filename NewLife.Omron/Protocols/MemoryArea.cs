namespace NewLife.Omron.Protocols;

/// <summary>FINS存储区类型</summary>
/// <remarks>
/// 定义欧姆龙PLC所有可访问的存储区域。
/// 每个区域有字访问和位访问两种模式，使用不同的区域代码。
/// </remarks>
public enum MemoryArea : Byte
{
    #region 位访问区域

    /// <summary>CIO位区。Core I/O 位访问</summary>
    CIO_Bit = 0x30,

    /// <summary>C系列IO位区。C/CV系列兼容的 Channel I/O 位访问，区域代码 0x00</summary>
    IO_Bit = 0x00,

    /// <summary>WR位区。Work Area 位访问</summary>
    WR_Bit = 0x31,

    /// <summary>HR位区。Holding Area 位访问</summary>
    HR_Bit = 0x32,

    /// <summary>AR位区。Auxiliary Area 位访问</summary>
    AR_Bit = 0x33,

    /// <summary>DM位区。Data Memory 位访问</summary>
    DM_Bit = 0x02,

    /// <summary>TIM状态位区。定时器/计数器完成标志位访问</summary>
    TIM_Bit = 0x09,

    /// <summary>Task Flag位区。任务标志位访问</summary>
    Task_Bit = 0x46,

    #endregion

    #region 字访问区域

    /// <summary>CIO字区。Core I/O 字访问</summary>
    CIO_Word = 0xB0,

    /// <summary>C系列IO字区。C/CV系列兼容的 Channel I/O 字访问，区域代码 0x80</summary>
    IO_Word = 0x80,

    /// <summary>WR字区。Work Area 字访问</summary>
    WR_Word = 0xB1,

    /// <summary>HR字区。Holding Area 字访问</summary>
    HR_Word = 0xB2,

    /// <summary>AR字区。Auxiliary Area 字访问</summary>
    AR_Word = 0xB3,

    /// <summary>DM字区。Data Memory 字访问</summary>
    DM_Word = 0x82,

    /// <summary>TIM/CNT PV字区。定时器/计数器当前值字访问</summary>
    TIM_Word = 0x89,

    /// <summary>IR字区。索引寄存器字访问</summary>
    IR_Word = 0xDC,

    /// <summary>DR字区。数据寄存器字访问</summary>
    DR_Word = 0xBC,

    #endregion

    #region EM Bank 字访问

    /// <summary>EM0字区。扩展数据存储器 Bank0 字访问</summary>
    EM0_Word = 0xA0,

    /// <summary>EM1字区。扩展数据存储器 Bank1 字访问</summary>
    EM1_Word = 0xA1,

    /// <summary>EM2字区。扩展数据存储器 Bank2 字访问</summary>
    EM2_Word = 0xA2,

    /// <summary>EM3字区。扩展数据存储器 Bank3 字访问</summary>
    EM3_Word = 0xA3,

    /// <summary>EM当前Bank字区。扩展数据存储器当前Bank字访问</summary>
    EM_Current_Word = 0x98,

    #endregion

    #region EM Bank 位访问

    /// <summary>EM0位区。扩展数据存储器 Bank0 位访问</summary>
    EM0_Bit = 0x20,

    /// <summary>EM1位区。扩展数据存储器 Bank1 位访问</summary>
    EM1_Bit = 0x21,

    /// <summary>EM2位区。扩展数据存储器 Bank2 位访问</summary>
    EM2_Bit = 0x22,

    /// <summary>EM3位区。扩展数据存储器 Bank3 位访问</summary>
    EM3_Bit = 0x23,

    #endregion
}

/// <summary>存储区辅助方法</summary>
public static class MemoryAreaHelper
{
    /// <summary>获取字访问区域对应的位访问区域代码</summary>
    /// <param name="wordArea">字访问区域代码</param>
    /// <returns>位访问区域代码</returns>
    public static Byte GetBitAreaCode(Byte wordArea) => wordArea switch
    {
        (Byte)MemoryArea.CIO_Word => (Byte)MemoryArea.CIO_Bit,
        (Byte)MemoryArea.IO_Word  => (Byte)MemoryArea.IO_Bit,
        (Byte)MemoryArea.WR_Word => (Byte)MemoryArea.WR_Bit,
        (Byte)MemoryArea.HR_Word => (Byte)MemoryArea.HR_Bit,
        (Byte)MemoryArea.AR_Word => (Byte)MemoryArea.AR_Bit,
        (Byte)MemoryArea.DM_Word => (Byte)MemoryArea.DM_Bit,
        (Byte)MemoryArea.TIM_Word => (Byte)MemoryArea.TIM_Bit,
        (Byte)MemoryArea.EM0_Word => (Byte)MemoryArea.EM0_Bit,
        (Byte)MemoryArea.EM1_Word => (Byte)MemoryArea.EM1_Bit,
        (Byte)MemoryArea.EM2_Word => (Byte)MemoryArea.EM2_Bit,
        (Byte)MemoryArea.EM3_Word => (Byte)MemoryArea.EM3_Bit,
        _ => throw new ArgumentException($"不支持的字区域代码: 0x{wordArea:X2}")
    };

    /// <summary>判断区域代码是否为位访问区域</summary>
    /// <param name="areaCode">区域代码</param>
    /// <returns>是否为位访问区域</returns>
    public static Boolean IsBitArea(Byte areaCode) => areaCode switch
    {
        (Byte)MemoryArea.CIO_Bit or
        (Byte)MemoryArea.WR_Bit or
        (Byte)MemoryArea.HR_Bit or
        (Byte)MemoryArea.AR_Bit or
        (Byte)MemoryArea.DM_Bit or
        (Byte)MemoryArea.TIM_Bit or
        (Byte)MemoryArea.Task_Bit or
        (Byte)MemoryArea.EM0_Bit or
        (Byte)MemoryArea.EM1_Bit or
        (Byte)MemoryArea.EM2_Bit or
        (Byte)MemoryArea.EM3_Bit => true,
        _ => false
    };
}
