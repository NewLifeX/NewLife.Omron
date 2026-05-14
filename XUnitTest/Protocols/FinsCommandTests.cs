using NewLife.Omron.Protocols;

namespace XUnitTest.Protocols;

/// <summary>FinsCommand 命令码测试</summary>
public class FinsCommandTests
{
    [Fact(DisplayName = "预定义存储区命令码正确")]
    public void PredefinedCommandCodes()
    {
        Assert.Equal(0x01, FinsCommand.MemoryAreaRead.MRC);
        Assert.Equal(0x01, FinsCommand.MemoryAreaRead.SRC);

        Assert.Equal(0x01, FinsCommand.MemoryAreaWrite.MRC);
        Assert.Equal(0x02, FinsCommand.MemoryAreaWrite.SRC);

        Assert.Equal(0x01, FinsCommand.MemoryAreaFill.MRC);
        Assert.Equal(0x03, FinsCommand.MemoryAreaFill.SRC);

        Assert.Equal(0x01, FinsCommand.MultipleMemoryAreaRead.MRC);
        Assert.Equal(0x04, FinsCommand.MultipleMemoryAreaRead.SRC);

        Assert.Equal(0x01, FinsCommand.MemoryAreaTransfer.MRC);
        Assert.Equal(0x05, FinsCommand.MemoryAreaTransfer.SRC);
    }

    [Fact(DisplayName = "CPU 控制命令码正确")]
    public void CpuControlCommandCodes()
    {
        Assert.Equal(0x04, FinsCommand.Run.MRC);
        Assert.Equal(0x01, FinsCommand.Run.SRC);

        Assert.Equal(0x04, FinsCommand.Stop.MRC);
        Assert.Equal(0x02, FinsCommand.Stop.SRC);
    }

    [Fact(DisplayName = "设备信息命令码正确")]
    public void DeviceInfoCommandCodes()
    {
        Assert.Equal(0x05, FinsCommand.ControllerDataRead.MRC);
        Assert.Equal(0x01, FinsCommand.ControllerDataRead.SRC);

        Assert.Equal(0x06, FinsCommand.ControllerStatusRead.MRC);
        Assert.Equal(0x01, FinsCommand.ControllerStatusRead.SRC);

        Assert.Equal(0x06, FinsCommand.CycleTimeRead.MRC);
        Assert.Equal(0x20, FinsCommand.CycleTimeRead.SRC);
    }

    [Fact(DisplayName = "时钟命令码正确")]
    public void ClockCommandCodes()
    {
        Assert.Equal(0x07, FinsCommand.ClockRead.MRC);
        Assert.Equal(0x01, FinsCommand.ClockRead.SRC);

        Assert.Equal(0x07, FinsCommand.ClockWrite.MRC);
        Assert.Equal(0x02, FinsCommand.ClockWrite.SRC);
    }

    [Fact(DisplayName = "错误管理命令码正确")]
    public void ErrorCommandCodes()
    {
        Assert.Equal(0x21, FinsCommand.ErrorClear.MRC);
        Assert.Equal(0x01, FinsCommand.ErrorClear.SRC);

        Assert.Equal(0x21, FinsCommand.ErrorLogRead.MRC);
        Assert.Equal(0x02, FinsCommand.ErrorLogRead.SRC);

        Assert.Equal(0x21, FinsCommand.ErrorLogClear.MRC);
        Assert.Equal(0x03, FinsCommand.ErrorLogClear.SRC);
    }

    [Fact(DisplayName = "ToBytes 生成 2 字节")]
    public void ToBytesProduces2Bytes()
    {
        var cmd = FinsCommand.MemoryAreaRead;
        var bytes = cmd.ToBytes();

        Assert.Equal(2, bytes.Length);
        Assert.Equal(0x01, bytes[0]);
        Assert.Equal(0x01, bytes[1]);
    }

    [Fact(DisplayName = "Parse 从字节解析命令")]
    public void ParseFromBytes()
    {
        var data = new Byte[] { 0x04, 0x02 };
        var cmd = FinsCommand.Parse(data);

        Assert.Equal(0x04, cmd.MRC);
        Assert.Equal(0x02, cmd.SRC);
    }

    [Fact(DisplayName = "Parse 数据不足应抛出异常")]
    public void ParseInsufficientDataShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => FinsCommand.Parse(new Byte[1]));
    }

    [Fact(DisplayName = "Read/Write 为对应命令的别名")]
    public void ReadWriteAreAliases()
    {
        var read = FinsCommand.Read;
        var memRead = FinsCommand.MemoryAreaRead;
        Assert.Equal(memRead.MRC, read.MRC);
        Assert.Equal(memRead.SRC, read.SRC);

        var write = FinsCommand.Write;
        var memWrite = FinsCommand.MemoryAreaWrite;
        Assert.Equal(memWrite.MRC, write.MRC);
        Assert.Equal(memWrite.SRC, write.SRC);
    }

    [Fact(DisplayName = "ToString 返回格式化字符串")]
    public void ToStringReturnsFormatted()
    {
        var cmd = FinsCommand.MemoryAreaRead;
        Assert.Equal("01:01", cmd.ToString());
    }
}
