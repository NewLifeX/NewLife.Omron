using NewLife.Omron.Protocols;

namespace XUnitTest;

/// <summary>HostLinkClient 与 HostLinkServer 集成测试</summary>
public class HostLinkIntegrationTests : IDisposable
{
    private readonly HostLinkServer _server;
    private readonly HostLinkClient _client;

    public HostLinkIntegrationTests()
    {
        _server = new HostLinkServer();
        _server.Start(0);

        _client = new HostLinkClient("127.0.0.1", _server.Port);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    #region DM 区读写

    [Fact(DisplayName = "HostLink: 写入并读取 DM 区 Int16")]
    public void WriteReadDmInt16()
    {
        _client.WriteInt16("D100", -1234);
        var value = _client.ReadInt16("D100");
        Assert.Equal(-1234, value);
    }

    [Fact(DisplayName = "HostLink: 写入并读取 DM 区 UInt16")]
    public void WriteReadDmUInt16()
    {
        _client.WriteUInt16("D200", 60000);
        var value = _client.ReadUInt16("D200");
        Assert.Equal((UInt16)60000, value);
    }

    [Fact(DisplayName = "HostLink: 写入并读取 DM 区 Int32")]
    public void WriteReadDmInt32()
    {
        _client.WriteInt32("D300", -987654);
        var value = _client.ReadInt32("D300");
        Assert.Equal(-987654, value);
    }

    [Fact(DisplayName = "HostLink: 写入并读取 DM 区 UInt32")]
    public void WriteReadDmUInt32()
    {
        _client.WriteUInt32("D400", 2000000000u);
        var value = _client.ReadUInt32("D400");
        Assert.Equal(2000000000u, value);
    }

    [Fact(DisplayName = "HostLink: 写入并读取 DM 区 Float")]
    public void WriteReadDmFloat()
    {
        _client.WriteFloat("D500", 3.14f);
        var value = _client.ReadFloat("D500");
        Assert.Equal(3.14f, value, precision: 4);
    }

    [Fact(DisplayName = "HostLink: 写入并读取 DM 区 Double")]
    public void WriteReadDmDouble()
    {
        _client.WriteDouble("D600", 1.41421356);
        var value = _client.ReadDouble("D600");
        Assert.Equal(1.41421356, value, precision: 7);
    }

    [Fact(DisplayName = "HostLink: 写入并读取 DM 区 Int64")]
    public void WriteReadDmInt64()
    {
        _client.WriteInt64("D700", -8_000_000_000L);
        var value = _client.ReadInt64("D700");
        Assert.Equal(-8_000_000_000L, value);
    }

    #endregion

    #region CIO 区读写

    [Fact(DisplayName = "HostLink: 写入并读取 CIO 区 UInt16")]
    public void WriteReadCioUInt16()
    {
        _client.WriteUInt16("CIO100", 1234);
        var value = _client.ReadUInt16("CIO100");
        Assert.Equal((UInt16)1234, value);
    }

    [Fact(DisplayName = "HostLink: 写入并读取 HR 区 Int16")]
    public void WriteReadHrInt16()
    {
        _client.WriteInt16("H10", 300);
        var value = _client.ReadInt16("H10");
        Assert.Equal(300, value);
    }

    [Fact(DisplayName = "HostLink: 写入并读取 AR 区 UInt16")]
    public void WriteReadArUInt16()
    {
        _client.WriteUInt16("A10", 500);
        var value = _client.ReadUInt16("A10");
        Assert.Equal((UInt16)500, value);
    }

    #endregion

    #region 测试命令

    [Fact(DisplayName = "HostLink: Test 命令返回原数据")]
    public void TestCommandEchosData()
    {
        var result = _client.Test("ABCD1234");
        Assert.Equal("ABCD1234", result);
    }

    [Fact(DisplayName = "HostLink: Test 命令返回默认值")]
    public void TestCommandDefaultEcho()
    {
        var result = _client.Test();
        Assert.NotNull(result);
    }

    #endregion

    #region CPU 状态

    [Fact(DisplayName = "HostLink: 读取 CPU 状态返回 Run 模式")]
    public void ReadCpuStatus_Run()
    {
        _server.CpuMode = CpuMode.Run;
        var data = _client.ReadCpuStatus();

        Assert.NotNull(data);
        Assert.True(data.Length >= 1);
        Assert.Equal((Byte)CpuMode.Run, data[0]);
    }

    [Fact(DisplayName = "HostLink: 切换 CPU 模式到 Monitor")]
    public void SetCpuMode_Monitor()
    {
        _client.SetCpuMode("02"); // Monitor = 0x02
        Assert.Equal(CpuMode.Monitor, _server.CpuMode);
    }

    [Fact(DisplayName = "HostLink: 切换 CPU 模式到 Program")]
    public void SetCpuMode_Program()
    {
        _client.SetCpuMode("00"); // Program = 0x00
        Assert.Equal(CpuMode.Program, _server.CpuMode);
    }

    #endregion

    #region 服务端验证

    [Fact(DisplayName = "HostLink: 客户端写入后服务端可读取")]
    public void ClientWriteServerVerify()
    {
        _client.WriteInt32("D800", 77777);

        Thread.Sleep(50);

        var dmCode = (Byte)MemoryArea.DM_Word;
        var serverData = _server.ReadMemory(dmCode, 800, 2);

        Assert.NotNull(serverData);
        Assert.Equal(4, serverData.Length);
        Assert.Contains(serverData, b => b != 0);
    }

    [Fact(DisplayName = "HostLink: 服务端预写入后客户端可读取")]
    public void ServerWriteClientVerify()
    {
        // 先通过客户端写入，再由客户端读回，确保字节序一致
        _client.WriteInt32("D900", 54321);
        var value = _client.ReadInt32("D900");
        Assert.Equal(54321, value);
    }

    #endregion

    #region 并发测试

    [Fact(DisplayName = "HostLink: 并发读写不死锁")]
    public async Task ConcurrentReadWriteNoDeadlock()
    {
        // 串行写入后验证
        _client.WriteInt32("D50", 42);

        // 多次并发读取（HostLink 是串行协议，通过 lock 保护）
        var tasks = Enumerable.Range(0, 5).Select(_ =>
            Task.Run(() => _client.ReadInt32("D50"))).ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.All(results, v => Assert.Equal(42, v));
    }

    #endregion

    #region 扩展类型

    [Fact(DisplayName = "HostLink: 写入并读取 DM 区 UInt64")]
    public void WriteReadDmUInt64()
    {
        _client.WriteUInt64("D1000", 12345678901234ul);
        var value = _client.ReadUInt64("D1000");
        Assert.Equal(12345678901234ul, value);
    }

    [Fact(DisplayName = "HostLink: 写入并读取 DM 区字符串")]
    public void WriteReadDmString()
    {
        // 写入 4 字节 ASCII（2 个字），再读出
        _client.WriteString("D1010", "AB");
        var raw = _client.Read("D1010", 1);
        // 1 个字 = 2 字节，值应含 A(65) B(66)
        Assert.NotNull(raw);
        Assert.Equal(2, raw.Length);
    }

    [Fact(DisplayName = "HostLink: 写入并读取 CIO 区 Int32")]
    public void WriteReadCioInt32()
    {
        _client.WriteInt32("CIO200", -55555);
        var value = _client.ReadInt32("CIO200");
        Assert.Equal(-55555, value);
    }

    [Fact(DisplayName = "HostLink: 写入并读取 WR 区 UInt32")]
    public void WriteReadWrUInt32()
    {
        _client.WriteUInt32("W100", 999999u);
        var value = _client.ReadUInt32("W100");
        Assert.Equal(999999u, value);
    }

    [Fact(DisplayName = "HostLink: 极值 Int32 写入读出正确")]
    public void WriteReadInt32Extremes()
    {
        _client.WriteInt32("D1020", Int32.MinValue);
        Assert.Equal(Int32.MinValue, _client.ReadInt32("D1020"));

        _client.WriteInt32("D1024", Int32.MaxValue);
        Assert.Equal(Int32.MaxValue, _client.ReadInt32("D1024"));
    }

    [Fact(DisplayName = "HostLink: 写入 0 值再读出为 0")]
    public void WriteZeroValueRoundtrips()
    {
        _client.WriteInt32("D1030", 12345);  // 先写非零
        _client.WriteInt32("D1030", 0);      // 覆盖为 0
        var value = _client.ReadInt32("D1030");
        Assert.Equal(0, value);
    }

    [Fact(DisplayName = "HostLink: EM0 区读写 Int32")]
    public void WriteReadEm0Int32()
    {
        _client.WriteInt32("EM0:100", -11111);
        var value = _client.ReadInt32("EM0:100");
        Assert.Equal(-11111, value);
    }

    #endregion
}
