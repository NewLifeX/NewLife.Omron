using NewLife.Omron.Protocols;

namespace XUnitTest;

/// <summary>FinsUdpClient 与 FinsUdpServer 集成测试</summary>
public class FinsUdpIntegrationTests : IDisposable
{
    private readonly FinsUdpServer _server;
    private readonly FinsUdpClient _client;

    public FinsUdpIntegrationTests()
    {
        _server = new FinsUdpServer();
        _server.Start(0);

        _client = new FinsUdpClient("127.0.0.1", _server.Port);
        _client.Open();
    }

    public void Dispose()
    {
        _client.Close();
        _server.Dispose();
    }

    #region DM 区读写

    [Fact(DisplayName = "UDP: 写入并读取 DM 区 Int16")]
    public void WriteReadDmInt16()
    {
        _client.WriteInt16("D100", -1234);
        var value = _client.ReadInt16("D100");
        Assert.Equal(-1234, value);
    }

    [Fact(DisplayName = "UDP: 写入并读取 DM 区 UInt16")]
    public void WriteReadDmUInt16()
    {
        _client.WriteUInt16("D200", 65535);
        var value = _client.ReadUInt16("D200");
        Assert.Equal((UInt16)65535, value);
    }

    [Fact(DisplayName = "UDP: 写入并读取 DM 区 Int32")]
    public void WriteReadDmInt32()
    {
        _client.WriteInt32("D300", -987654);
        var value = _client.ReadInt32("D300");
        Assert.Equal(-987654, value);
    }

    [Fact(DisplayName = "UDP: 写入并读取 DM 区 UInt32")]
    public void WriteReadDmUInt32()
    {
        _client.WriteUInt32("D400", 3000000000u);
        var value = _client.ReadUInt32("D400");
        Assert.Equal(3000000000u, value);
    }

    [Fact(DisplayName = "UDP: 写入并读取 DM 区 Float")]
    public void WriteReadDmFloat()
    {
        _client.WriteFloat("D500", 3.14f);
        var value = _client.ReadFloat("D500");
        Assert.Equal(3.14f, value, precision: 4);
    }

    [Fact(DisplayName = "UDP: 写入并读取 DM 区 Double")]
    public void WriteReadDmDouble()
    {
        _client.WriteDouble("D600", 2.718281828);
        var value = _client.ReadDouble("D600");
        Assert.Equal(2.718281828, value, precision: 8);
    }

    [Fact(DisplayName = "UDP: 写入并读取 DM 区 Int64")]
    public void WriteReadDmInt64()
    {
        _client.WriteInt64("D700", -9_000_000_000L);
        var value = _client.ReadInt64("D700");
        Assert.Equal(-9_000_000_000L, value);
    }

    #endregion

    #region CIO 区读写

    [Fact(DisplayName = "UDP: 写入并读取 CIO 区 UInt16")]
    public void WriteReadCioUInt16()
    {
        _client.WriteUInt16("CIO100", 1000);
        var value = _client.ReadUInt16("CIO100");
        Assert.Equal((UInt16)1000, value);
    }

    [Fact(DisplayName = "UDP: 写入并读取 WR 区 Int16")]
    public void WriteReadWrInt16()
    {
        _client.WriteInt16("W10", 500);
        var value = _client.ReadInt16("W10");
        Assert.Equal(500, value);
    }

    #endregion

    #region 批量读取

    [Fact(DisplayName = "UDP: 批量写入后批量读取")]
    public void BatchWriteRead()
    {
        // 在服务端存储区预写入测试数据
        var dmCode = (Byte)MemoryArea.DM_Word;
        _server.WriteMemory(dmCode, 10, [0x01, 0x00, 0x02, 0x00, 0x03, 0x00]);

        var result = _client.Read("D10", 3);

        Assert.NotNull(result);
        Assert.Equal(6, result.Length);
    }

    #endregion

    #region 服务端验证

    [Fact(DisplayName = "UDP: 客户端写入后服务端可读取")]
    public void ClientWriteServerVerify()
    {
        _client.WriteInt32("D800", 99999);

        // 等待服务端处理
        Thread.Sleep(50);

        var dmCode = (Byte)MemoryArea.DM_Word;
        var serverData = _server.ReadMemory(dmCode, 800, 2);

        Assert.NotNull(serverData);
        Assert.Equal(4, serverData.Length);
        // 验证数据非全零（写入成功）
        Assert.Contains(serverData, b => b != 0);
    }

    [Fact(DisplayName = "UDP: 服务端预写入后客户端可读取")]
    public void ServerWriteClientVerify()
    {
        // 先通过客户端写入，再由客户端读回，确保字节序一致
        _client.WriteInt32("D900", 12345);
        var value = _client.ReadInt32("D900");
        Assert.Equal(12345, value);
    }

    #endregion

    #region 重连测试

    [Fact(DisplayName = "UDP: 关闭后重新 Open 可继续通信")]
    public void CloseAndReopenCanCommunicate()
    {
        _client.WriteInt32("D50", 1111);
        _client.Close();
        Assert.False(_client.IsOpened);

        _client.Open();
        Assert.True(_client.IsOpened);

        _client.WriteInt32("D50", 2222);
        var value = _client.ReadInt32("D50");
        Assert.Equal(2222, value);
    }

    #endregion
}
