using NewLife.Omron.Protocols;
using Xunit;

namespace XUnitTest.Protocols;

/// <summary>MemoryArea枚举和辅助方法测试</summary>
public class MemoryAreaTests
{
    [Theory]
    [InlineData(MemoryArea.CIO_Word, 0xB0)]
    [InlineData(MemoryArea.WR_Word, 0xB1)]
    [InlineData(MemoryArea.HR_Word, 0xB2)]
    [InlineData(MemoryArea.AR_Word, 0xB3)]
    [InlineData(MemoryArea.DM_Word, 0x82)]
    [InlineData(MemoryArea.TIM_Word, 0x89)]
    [InlineData(MemoryArea.IR_Word, 0xDC)]
    [InlineData(MemoryArea.DR_Word, 0xBC)]
    [InlineData(MemoryArea.EM0_Word, 0xA0)]
    [InlineData(MemoryArea.EM_Current_Word, 0x98)]
        public void WordAreaCodeValues(MemoryArea area, Byte expected)
    {
        Assert.Equal(expected, (Byte)area);
    }

    [Theory]
    [InlineData(MemoryArea.CIO_Bit, 0x30)]
    [InlineData(MemoryArea.WR_Bit, 0x31)]
    [InlineData(MemoryArea.HR_Bit, 0x32)]
    [InlineData(MemoryArea.AR_Bit, 0x33)]
    [InlineData(MemoryArea.DM_Bit, 0x02)]
    [InlineData(MemoryArea.TIM_Bit, 0x09)]
    [InlineData(MemoryArea.Task_Bit, 0x46)]
    [InlineData(MemoryArea.EM0_Bit, 0x20)]
        public void BitAreaCodeValues(MemoryArea area, Byte expected)
    {
        Assert.Equal(expected, (Byte)area);
    }

    [Theory]
    [InlineData(0xB0, 0x30)] // CIO
    [InlineData(0xB1, 0x31)] // WR
    [InlineData(0xB2, 0x32)] // HR
    [InlineData(0xB3, 0x33)] // AR
    [InlineData(0x82, 0x02)] // DM
    [InlineData(0x89, 0x09)] // TIM
    [InlineData(0xA0, 0x20)] // EM0
    [InlineData(0xA1, 0x21)] // EM1
        public void GetBitAreaCodeMapping(Byte wordArea, Byte expectedBitArea)
    {
        Assert.Equal(expectedBitArea, MemoryAreaHelper.GetBitAreaCode(wordArea));
    }

    [Fact(DisplayName = "不支持的字区域调用GetBitAreaCode应抛出异常")]
    public void GetBitAreaCodeUnsupportedShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => MemoryAreaHelper.GetBitAreaCode(0xFF));
    }

    [Theory]
    [InlineData(0x30, true)]  // CIO Bit
    [InlineData(0x31, true)]  // WR Bit
    [InlineData(0x02, true)]  // DM Bit
    [InlineData(0x09, true)]  // TIM Bit
    [InlineData(0x46, true)]  // Task Bit
    [InlineData(0xB0, false)] // CIO Word
    [InlineData(0x82, false)] // DM Word
    [InlineData(0x89, false)] // TIM Word
    [InlineData(0xFF, false)] // Unknown
        public void IsBitAreaCheck(Byte areaCode, Boolean expected)
    {
        Assert.Equal(expected, MemoryAreaHelper.IsBitArea(areaCode));
    }
}
