using NewLife.Omron.Protocols;

namespace XUnitTest.Protocols;

/// <summary>CpuUnitData 和 CpuUnitStatus 模型测试</summary>
public class CpuModelTests
{
    [Fact(DisplayName = "CpuUnitData 解析")]
    public void ParseCpuUnitData()
    {
        // 构造模拟数据：型号(20) + 版本(4) + 系统版本(4) + 容量(2)
        var data = new Byte[30];
        var model = System.Text.Encoding.ASCII.GetBytes("CJ2M-CPU31");
        Array.Copy(model, 0, data, 0, model.Length);

        var version = System.Text.Encoding.ASCII.GetBytes("V4.1");
        Array.Copy(version, 0, data, 20, version.Length);

        var sysVer = System.Text.Encoding.ASCII.GetBytes("V3.0");
        Array.Copy(sysVer, 0, data, 24, sysVer.Length);

        data[28] = 0x00;
        data[29] = 0x64; // 容量 100

        var cpu = CpuUnitData.Parse(data);

        Assert.Equal("CJ2M-CPU31", cpu.Model);
        Assert.Equal("V4.1", cpu.Version);
        Assert.Equal("V3.0", cpu.SystemVersion);
        Assert.Equal((UInt16)100, cpu.AreaDataCapacity);
    }

    [Fact(DisplayName = "CpuUnitData 数据不足应抛出异常")]
    public void ParseCpuUnitDataInsufficientShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => CpuUnitData.Parse(new Byte[10]));
        Assert.Throws<ArgumentException>(() => CpuUnitData.Parse(null));
    }

    [Fact(DisplayName = "CpuUnitStatus 解析 Run 模式")]
    public void ParseCpuUnitStatusRunMode()
    {
        var data = new Byte[8];
        data[0] = 0x04; // Run 模式
        data[1] = 0x00; // 无致命错误

        var status = CpuUnitStatus.Parse(data);

        Assert.Equal(CpuMode.Run, status.Mode);
        Assert.False(status.FatalError);
    }

    [Fact(DisplayName = "CpuUnitStatus 解析错误状态")]
    public void ParseCpuUnitStatusError()
    {
        var data = new Byte[8];
        data[0] = 0x00; // Program 模式
        data[1] = 0x01; // 致命错误
        data[2] = 0x00;
        data[3] = 0x10; // 致命错误数据
        data[4] = 0x01; // 非致命错误
        data[5] = 0x20;

        var status = CpuUnitStatus.Parse(data);

        Assert.Equal(CpuMode.Program, status.Mode);
        Assert.True(status.FatalError);
        Assert.Equal((UInt16)0x0010, status.FatalErrorData);
        Assert.True(status.NonFatalError);
    }

    [Fact(DisplayName = "CpuUnitStatus 数据不足应抛出异常")]
    public void ParseCpuUnitStatusInsufficientShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => CpuUnitStatus.Parse(new Byte[3]));
        Assert.Throws<ArgumentException>(() => CpuUnitStatus.Parse(null));
    }

    [Fact(DisplayName = "CpuMode 枚举值正确")]
    public void CpuModeValues()
    {
        Assert.Equal((Byte)0x00, (Byte)CpuMode.Program);
        Assert.Equal((Byte)0x01, (Byte)CpuMode.Debug);
        Assert.Equal((Byte)0x02, (Byte)CpuMode.Monitor);
        Assert.Equal((Byte)0x04, (Byte)CpuMode.Run);
    }
}
