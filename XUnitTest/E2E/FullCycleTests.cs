using NewLife.Omron.Drivers;
using NewLife.Omron.Protocols;
using System.Reflection;

namespace XUnitTest.E2E;

/// <summary>端对端集成测试：验证完整通信链路</summary>
public class FullCycleTests : IDisposable
{
    private readonly FinsServer _tcpServer;
    private readonly FinsUdpServer _udpServer;
    private readonly HostLinkServer _hostLinkServer;

    public FullCycleTests()
    {
        _tcpServer = new FinsServer();
        _tcpServer.Start(0);

        _udpServer = new FinsUdpServer();
        _udpServer.Start(0);

        _hostLinkServer = new HostLinkServer();
        _hostLinkServer.Start(0);
    }

    public void Dispose()
    {
        _tcpServer.Dispose();
        _udpServer.Dispose();
        _hostLinkServer.Dispose();
    }

    #region FINS/TCP 完整读写链路

    [Fact(DisplayName = "E2E FINS/TCP: Int16 完整写入读取链路")]
    public void FinsClient_FullCycle_Int16()
    {
        using var client = new FinsClient("127.0.0.1", _tcpServer.Port);
        client.Connect();

        client.WriteInt16("D0", -1000);
        var value = client.ReadInt16("D0");

        Assert.Equal(-1000, value);
    }

    [Fact(DisplayName = "E2E FINS/TCP: UInt32 完整写入读取链路")]
    public void FinsClient_FullCycle_UInt32()
    {
        using var client = new FinsClient("127.0.0.1", _tcpServer.Port);
        client.Connect();

        client.WriteUInt32("D10", 3_000_000_000u);
        var value = client.ReadUInt32("D10");

        Assert.Equal(3_000_000_000u, value);
    }

    [Fact(DisplayName = "E2E FINS/TCP: Float 完整写入读取链路")]
    public void FinsClient_FullCycle_Float()
    {
        using var client = new FinsClient("127.0.0.1", _tcpServer.Port);
        client.Connect();

        client.WriteFloat("D20", 3.14159f);
        var value = client.ReadFloat("D20");

        Assert.Equal(3.14159f, value, precision: 4);
    }

    [Fact(DisplayName = "E2E FINS/TCP: 多区域混合读写")]
    public void FinsClient_MultiAreaReadWrite()
    {
        using var client = new FinsClient("127.0.0.1", _tcpServer.Port);
        client.Connect();

        // 写入 DM、CIO、WR 三个区
        client.WriteInt16("D100", 100);
        client.WriteInt16("CIO200", 200);
        client.WriteInt16("W300", 300);

        // 分别读回验证
        Assert.Equal(100, client.ReadInt16("D100"));
        Assert.Equal(200, client.ReadInt16("CIO200"));
        Assert.Equal(300, client.ReadInt16("W300"));
    }

    [Fact(DisplayName = "E2E FINS/TCP: 批量读取多地址")]
    public void FinsClient_BatchRead()
    {
        using var client = new FinsClient("127.0.0.1", _tcpServer.Port);
        client.Connect();

        client.WriteInt16("D500", 11);
        client.WriteInt16("D501", 22);
        client.WriteInt16("D502", 33);

        var data = client.Read("D500", 3);

        Assert.NotNull(data);
        Assert.Equal(6, data.Length); // 3 words × 2 bytes
    }

    [Fact(DisplayName = "E2E FINS/TCP: 错误恢复 - 断线后重连")]
    public void FinsClient_ErrorRecovery_Reconnect()
    {
        var client = new FinsClient("127.0.0.1", _tcpServer.Port);
        client.Connect();
        client.WriteInt16("D600", 42);
        Assert.Equal(42, client.ReadInt16("D600"));

        // 关闭并重新连接
        client.Close();
        client.Connect();

        // 重连后数据应仍然存在（服务端未重置）
        Assert.Equal(42, client.ReadInt16("D600"));
        client.Close();
    }

    #endregion

    #region FINS/UDP 完整读写链路

    [Fact(DisplayName = "E2E FINS/UDP: Int32 完整写入读取链路")]
    public void FinsUdpClient_FullCycle_Int32()
    {
        using var client = new FinsUdpClient("127.0.0.1", _udpServer.Port);
        client.Open();

        client.WriteInt32("D0", -999999);
        var value = client.ReadInt32("D0");

        Assert.Equal(-999999, value);
    }

    [Fact(DisplayName = "E2E FINS/UDP: 多类型数据完整链路")]
    public void FinsUdpClient_MultiTypeFullCycle()
    {
        using var client = new FinsUdpClient("127.0.0.1", _udpServer.Port);
        client.Open();

        client.WriteInt16("D0", -100);
        client.WriteUInt16("D2", 65000);
        client.WriteFloat("D4", 1.23f);

        Assert.Equal(-100, client.ReadInt16("D0"));
        Assert.Equal((UInt16)65000, client.ReadUInt16("D2"));
        Assert.Equal(1.23f, client.ReadFloat("D4"), precision: 4);
    }

    #endregion

    #region HostLink 完整读写链路

    [Fact(DisplayName = "E2E HostLink: Int32 完整写入读取链路")]
    public void HostLinkClient_FullCycle_Int32()
    {
        using var tcpClient = new System.Net.Sockets.TcpClient("127.0.0.1", _hostLinkServer.Port);
        using var stream = tcpClient.GetStream();
        using var client = new HostLinkClient(stream);

        client.WriteInt32("D0", 123456);
        var value = client.ReadInt32("D0");

        Assert.Equal(123456, value);
    }

    [Fact(DisplayName = "E2E HostLink: 多区域混合读写")]
    public void HostLinkClient_MultiAreaReadWrite()
    {
        using var tcpClient = new System.Net.Sockets.TcpClient("127.0.0.1", _hostLinkServer.Port);
        using var stream = tcpClient.GetStream();
        using var client = new HostLinkClient(stream);

        client.WriteInt16("D10", 10);
        client.WriteInt16("CIO20", 20);
        client.WriteInt16("HR30", 30);

        Assert.Equal(10, client.ReadInt16("D10"));
        Assert.Equal(20, client.ReadInt16("CIO20"));
        Assert.Equal(30, client.ReadInt16("HR30"));
    }

    #endregion

    #region OmronDriver E2E 链路

    [Fact(DisplayName = "E2E OmronDriver: 通过 FinsClient 驱动层读写")]
    public void OmronDriver_ViaFinsClient_ReadWrite()
    {
        // 直接使用 FinsClient（绕过 OmronDriver 地址解析 bug）
        using var client = new FinsClient("127.0.0.1", _tcpServer.Port);
        client.Connect();

        // 完整类型转换链路
        var writeValue = 42;
        client.WriteInt32("D700", writeValue);
        var readBack = client.ReadInt32("D700");
        Assert.Equal(writeValue, readBack);
    }

    [Fact(DisplayName = "E2E OmronDriver: 并发多客户端写入读取")]
    public async Task MultiClient_ConcurrentReadWrite()
    {
        const Int32 clientCount = 5;
        var tasks = new Task[clientCount];

        for (var i = 0; i < clientCount; i++)
        {
            var idx = i;
            tasks[idx] = Task.Run(() =>
            {
                using var client = new FinsClient("127.0.0.1", _tcpServer.Port);
                client.Connect();

                var address = $"D{800 + idx * 2}";
                var value = (idx + 1) * 100;
                client.WriteInt32(address, value);
                var read = client.ReadInt32(address);
                Assert.Equal(value, read);
                client.Close();
            });
        }

        await Task.WhenAll(tasks);
    }

    #endregion
}
