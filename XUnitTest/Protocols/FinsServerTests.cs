using NewLife.Omron.Protocols;
using Xunit;

namespace XUnitTest.Protocols;

/// <summary>FinsServer模拟服务器测试</summary>
public class FinsServerTests : IDisposable
{
    private readonly FinsServer _server;
    private readonly FinsClient _client;

    public FinsServerTests()
    {
        _server = new FinsServer();
        _server.Start();

        _client = new FinsClient("127.0.0.1", _server.Port)
        {
            ConnectTimeOut = 5000,
            ReceiveTimeOut = 5000,
            AutoReconnect = false
        };
        _client.Connect();
    }

    public void Dispose()
    {
        _client?.Close();
        _server?.Dispose();
    }

    #region 连接

    [Fact(DisplayName = "测试基本连接")]
    public void TestConnect()
    {
        Assert.True(_client.IsConnected);
        Assert.True(_client.SourceNodeAddress > 0);
    }

    #endregion

    #region 字读写

    [Fact(DisplayName = "测试DM区字读写")]
    public void TestReadWriteWord()
    {
        _client.Write("D100", new Byte[] { 0x00, 0x0A, 0x00, 0x14 });

        var data = _client.Read("D100", 2);

        Assert.Equal(4, data.Length);
        Assert.Equal(0x00, data[0]);
        Assert.Equal(0x0A, data[1]);
        Assert.Equal(0x00, data[2]);
        Assert.Equal(0x14, data[3]);
    }

    [Fact(DisplayName = "测试CIO区字读写")]
    public void TestReadWriteCIO()
    {
        _client.Write("CIO50", new Byte[] { 0x12, 0x34 });

        var data = _client.Read("CIO50", 1);

        Assert.Equal(2, data.Length);
        Assert.Equal(0x12, data[0]);
        Assert.Equal(0x34, data[1]);
    }

    [Fact(DisplayName = "测试WR区字读写")]
    public void TestReadWriteWR()
    {
        _client.Write("W200", new Byte[] { 0xAB, 0xCD });

        var data = _client.Read("W200", 1);

        Assert.Equal(0xAB, data[0]);
        Assert.Equal(0xCD, data[1]);
    }

    [Fact(DisplayName = "测试HR区字读写")]
    public void TestReadWriteHR()
    {
        _client.Write("H10", new Byte[] { 0x11, 0x22 });

        var data = _client.Read("H10", 1);

        Assert.Equal(0x11, data[0]);
        Assert.Equal(0x22, data[1]);
    }

    [Fact(DisplayName = "测试AR区字读写")]
    public void TestReadWriteAR()
    {
        _client.Write("A5", new Byte[] { 0x33, 0x44 });

        var data = _client.Read("A5", 1);

        Assert.Equal(0x33, data[0]);
        Assert.Equal(0x44, data[1]);
    }

    #endregion

    #region 类型化读写

    [Fact(DisplayName = "测试Int16读写")]
    public void TestReadWriteInt16()
    {
        _client.WriteInt16("D200", 12345);

        var value = _client.ReadInt16("D200");

        Assert.Equal(12345, value);
    }

    [Fact(DisplayName = "测试UInt16读写")]
    public void TestReadWriteUInt16()
    {
        _client.WriteUInt16("D210", 50000);

        var value = _client.ReadUInt16("D210");

        Assert.Equal((UInt16)50000, value);
    }

    [Fact(DisplayName = "测试Int32读写")]
    public void TestReadWriteInt32()
    {
        _client.WriteInt32("D300", 123456789);

        var value = _client.ReadInt32("D300");

        Assert.Equal(123456789, value);
    }

    [Fact(DisplayName = "测试UInt32读写")]
    public void TestReadWriteUInt32()
    {
        _client.WriteUInt32("D310", 3000000000u);

        var value = _client.ReadUInt32("D310");

        Assert.Equal(3000000000u, value);
    }

    [Fact(DisplayName = "测试Float读写")]
    public void TestReadWriteFloat()
    {
        _client.WriteFloat("D400", 3.14f);

        var value = _client.ReadFloat("D400");

        Assert.Equal(3.14f, value);
    }

    [Fact(DisplayName = "测试Double读写")]
    public void TestReadWriteDouble()
    {
        _client.WriteDouble("D500", 3.141592653589793);

        var value = _client.ReadDouble("D500");

        Assert.Equal(3.141592653589793, value);
    }

    [Fact(DisplayName = "测试Int64读写")]
    public void TestReadWriteInt64()
    {
        _client.WriteInt64("D600", 9876543210L);

        var value = _client.ReadInt64("D600");

        Assert.Equal(9876543210L, value);
    }

    [Fact(DisplayName = "测试String读写")]
    public void TestReadWriteString()
    {
        _client.WriteString("D700", "Hello");

        var value = _client.ReadString("D700", 3);

        Assert.Equal("Hello", value);
    }

    #endregion

    #region 位操作

    [Fact(DisplayName = "测试位写入和读取")]
    public void TestReadWriteBit()
    {
        _client.WriteBool("CIO100.5", true);

        var value = _client.ReadBool("CIO100.5");

        Assert.True(value);
    }

    [Fact(DisplayName = "测试位写0")]
    public void TestWriteBitFalse()
    {
        _client.WriteBool("CIO100.3", true);
        _client.WriteBool("CIO100.3", false);

        var value = _client.ReadBool("CIO100.3");

        Assert.False(value);
    }

    [Fact(DisplayName = "测试批量位读写")]
    public void TestReadWriteMultipleBits()
    {
        _client.WriteBit("D50.0", new Byte[] { 1, 0, 1, 1 });

        var bits = _client.ReadBit("D50.0", 4);

        Assert.Equal(4, bits.Length);
        Assert.Equal(1, bits[0]);
        Assert.Equal(0, bits[1]);
        Assert.Equal(1, bits[2]);
        Assert.Equal(1, bits[3]);
    }

    #endregion

    #region 存储区操作

    [Fact(DisplayName = "测试存储区填充")]
    public void TestFill()
    {
        _client.Fill("D100", 10, 0x1234);

        var data = _client.Read("D100", 10);

        Assert.Equal(20, data.Length);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(0x12, data[i * 2]);
            Assert.Equal(0x34, data[i * 2 + 1]);
        }
    }

    [Fact(DisplayName = "测试存储区传送")]
    public void TestTransfer()
    {
        // 先写入源数据
        _client.WriteInt16("D100", 111);
        _client.WriteInt16("D101", 222);
        _client.WriteInt16("D102", 333);

        // 传送 D100-D102 到 D200-D202
        _client.Transfer("D100", "D200", 3);

        // 验证目标数据
        Assert.Equal(111, _client.ReadInt16("D200"));
        Assert.Equal(222, _client.ReadInt16("D201"));
        Assert.Equal(333, _client.ReadInt16("D202"));
    }

    [Fact(DisplayName = "测试多区域读取")]
    public void TestMultipleRead()
    {
        _client.Write("D100", new Byte[] { 0x00, 0x0A });
        _client.Write("D200", new Byte[] { 0x00, 0x14 });

        var data = _client.MultipleRead(new[] { "D100", "D200" });

        Assert.NotNull(data);
        // 每个地址返回 AreaCode(1) + Data(2) = 3 字节
        Assert.Equal(6, data.Length);
    }

    [Fact(DisplayName = "测试批量读取")]
    public void TestBatchRead()
    {
        _client.WriteInt16("D100", 100);
        _client.WriteInt16("D101", 101);
        _client.WriteInt16("D200", 200);

        var results = _client.BatchRead(
            new[] { "D100", "D101", "D200" },
            new UInt16[] { 1, 1, 1 });

        Assert.Equal(3, results.Length);
        Assert.Equal(100, _client.Transform.TransInt16(results[0], 0));
        Assert.Equal(101, _client.Transform.TransInt16(results[1], 0));
        Assert.Equal(200, _client.Transform.TransInt16(results[2], 0));
    }

    #endregion

    #region CPU操作

    [Fact(DisplayName = "测试CPU运行停止")]
    public void TestPlcRunStop()
    {
        _client.PlcStop();
        Assert.Equal(CpuMode.Program, _server.CpuMode);

        _client.PlcRun();
        Assert.Equal(CpuMode.Run, _server.CpuMode);
    }

    [Fact(DisplayName = "测试读取CPU数据")]
    public void TestReadCpuUnitData()
    {
        _server.CpuModel = "CJ2M-CPU31";
        _server.CpuVersion = "V4.1";

        var data = _client.ReadCpuUnitData();

        Assert.Equal("CJ2M-CPU31", data.Model);
        Assert.Equal("V4.1", data.Version);
    }

    [Fact(DisplayName = "测试读取CPU状态")]
    public void TestReadCpuUnitStatus()
    {
        _server.CpuMode = CpuMode.Monitor;

        var status = _client.ReadCpuUnitStatus();

        Assert.Equal(CpuMode.Monitor, status.Mode);
        Assert.False(status.FatalError);
    }

    [Fact(DisplayName = "测试读取扫描周期")]
    public void TestReadCycleTime()
    {
        var (avg, max, min) = _client.ReadCycleTime();

        Assert.True(avg > 0);
        Assert.True(max >= avg);
        Assert.True(min <= avg);
    }

    #endregion

    #region 时钟操作

    [Fact(DisplayName = "测试时钟写入读取")]
    public void TestReadWriteClock()
    {
        var target = new DateTime(2025, 6, 15, 10, 30, 45);
        _client.WriteClock(target);

        var readBack = _client.ReadClock();

        Assert.Equal(target.Year, readBack.Year);
        Assert.Equal(target.Month, readBack.Month);
        Assert.Equal(target.Day, readBack.Day);
        Assert.Equal(target.Hour, readBack.Hour);
        Assert.Equal(target.Minute, readBack.Minute);
        Assert.Equal(target.Second, readBack.Second);
    }

    [Fact(DisplayName = "测试时钟读取默认返回当前时间")]
    public void TestReadClockDefault()
    {
        var before = DateTime.Now;
        var clock = _client.ReadClock();
        var after = DateTime.Now;

        // 读取的时间应在 before 和 after 之间（允许几秒误差）
        Assert.True(clock >= before.AddSeconds(-2));
        Assert.True(clock <= after.AddSeconds(2));
    }

    #endregion

    #region 错误操作

    [Fact(DisplayName = "测试清除错误")]
    public void TestClearError()
    {
        _client.ClearError(0x0000);
    }

    [Fact(DisplayName = "测试清除错误日志")]
    public void TestClearErrorLog()
    {
        _client.ClearErrorLog();
    }

    #endregion

    #region 服务端内存操作

    [Fact(DisplayName = "测试服务端预写入客户端读取")]
    public void TestServerWriteClientRead()
    {
        // 服务端预写入数据
        _server.WriteMemory(0x82, 900, new Byte[] { 0x12, 0x34, 0x56, 0x78 });

        // 客户端读取
        var data = _client.Read("D900", 2);

        Assert.Equal(0x12, data[0]);
        Assert.Equal(0x34, data[1]);
        Assert.Equal(0x56, data[2]);
        Assert.Equal(0x78, data[3]);
    }

    [Fact(DisplayName = "测试客户端写入服务端验证")]
    public void TestClientWriteServerVerify()
    {
        _client.Write("D800", new Byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

        // 服务端直接读取验证
        var data = _server.ReadMemory(0x82, 800, 2);

        Assert.Equal(0xAA, data[0]);
        Assert.Equal(0xBB, data[1]);
        Assert.Equal(0xCC, data[2]);
        Assert.Equal(0xDD, data[3]);
    }

    #endregion

    #region 异步

    [Fact(DisplayName = "测试异步读写")]
    public async Task TestAsyncReadWrite()
    {
        await _client.WriteInt32Async("D1000", 999999);

        var value = await _client.ReadInt32Async("D1000");

        Assert.Equal(999999, value);
    }

    [Fact(DisplayName = "测试异步位操作")]
    public async Task TestAsyncBitReadWrite()
    {
        await _client.WriteBoolAsync("CIO200.7", true);

        var value = await _client.ReadBoolAsync("CIO200.7");

        Assert.True(value);
    }

    #endregion
}
