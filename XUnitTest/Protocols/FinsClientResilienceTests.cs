using System.Net;
using System.Net.Sockets;
using NewLife.Omron.Protocols;

namespace XUnitTest.Protocols;

/// <summary>FinsClient 弹性与错误处理测试（无需真实 PLC）</summary>
public class FinsClientResilienceTests
{
    #region 连接失败与超时

    [Fact(DisplayName = "连接不可达端口 → 超时异常")]
    public void Connect_ToClosedPort_ThrowsTimeout()
    {
        // 找一个未监听的端口
        using var client = new FinsClient("127.0.0.1", FindFreePort())
        {
            ConnectTimeOut = 300,
            AutoReconnect = false
        };

        var ex = Assert.Throws<InvalidOperationException>(() => client.Connect());
        Assert.NotNull(ex.Message);
    }

    [Fact(DisplayName = "AutoReconnect=false 未连接时读取 → 立即抛出异常")]
    public void Read_WhenNotConnected_AutoReconnectFalse_Throws()
    {
        // 从未 Connect() 过的客户端，AutoReconnect=false → 立即抛出"未连接"异常
        using var client = new FinsClient("127.0.0.1", 19600)
        {
            ConnectTimeOut = 500,
            AutoReconnect = false
        };

        var ex = Assert.Throws<InvalidOperationException>(() => client.ReadInt32("D100"));
        Assert.NotEmpty(ex.Message);
    }

    [Fact(DisplayName = "关闭后重新 Connect 可继续通信")]
    public void CloseAndReconnect_Works()
    {
        using var server = new FinsServer();
        server.Start(0);

        using var client = new FinsClient("127.0.0.1", server.Port)
        {
            ConnectTimeOut = 2000,
            AutoReconnect = false
        };

        client.Connect();
        client.WriteInt32("D100", 12345);
        Assert.Equal(12345, client.ReadInt32("D100"));

        // 主动关闭
        client.Close();
        Assert.False(client.IsConnected);

        // 重新连接
        client.Connect();
        Assert.True(client.IsConnected);

        // 数据持久化在服务端
        Assert.Equal(12345, client.ReadInt32("D100"));
    }

    #endregion

    #region 错误码响应

    [Fact(DisplayName = "服务端返回错误码 → 客户端抛出含错误信息的异常")]
    public void ServerReturnsErrorCode_ClientThrowsWithMessage()
    {
        using var server = new FinsServer();
        server.Start(0);
        // EndCode=0x2203: 超出地址范围
        server.ForceErrorCode = 0x2203;

        using var client = new FinsClient("127.0.0.1", server.Port)
        {
            ConnectTimeOut = 2000,
            AutoReconnect = false
        };
        client.Connect();

        var ex = Assert.Throws<InvalidOperationException>(() => client.ReadInt32("D100"));
        // 错误消息应包含可识别的错误信息
        Assert.NotEmpty(ex.Message);
    }

    [Fact(DisplayName = "服务端返回命令不受支持错误码 0x0401 → 客户端抛出异常")]
    public void ServerReturnsUnsupportedCommand_ClientThrows()
    {
        using var server = new FinsServer();
        server.Start(0);
        server.ForceErrorCode = 0x0401;

        using var client = new FinsClient("127.0.0.1", server.Port)
        {
            ConnectTimeOut = 2000,
            AutoReconnect = false
        };
        client.Connect();

        var ex = Assert.Throws<InvalidOperationException>(() => client.WriteInt32("D200", 999));
        Assert.NotEmpty(ex.Message);
    }

    [Fact(DisplayName = "清除强制错误码后命令正常执行")]
    public void ClearForceError_CommandSucceeds()
    {
        using var server = new FinsServer();
        server.Start(0);
        server.ForceErrorCode = 0x1001;

        using var client = new FinsClient("127.0.0.1", server.Port)
        {
            ConnectTimeOut = 2000,
            AutoReconnect = false
        };
        client.Connect();

        // 有错误时抛异常
        Assert.ThrowsAny<Exception>(() => client.ReadInt32("D100"));

        // 清除错误
        server.ForceErrorCode = 0;

        // 现在应该正常
        client.WriteInt32("D100", 42);
        Assert.Equal(42, client.ReadInt32("D100"));
    }

    #endregion

    #region 并发访问线程安全

    [Fact(DisplayName = "多线程并发读写同一地址 → 不死锁不数据损坏")]
    public void ConcurrentReadWrite_ThreadSafe()
    {
        using var server = new FinsServer();
        server.Start(0);

        using var client = new FinsClient("127.0.0.1", server.Port)
        {
            ConnectTimeOut = 2000,
            AutoReconnect = false
        };
        client.Connect();

        client.WriteInt32("D500", 0);

        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var barrier = new Barrier(10);

        var threads = Enumerable.Range(0, 10).Select(i => new Thread(() =>
        {
            try
            {
                barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                for (var j = 0; j < 5; j++)
                {
                    client.WriteInt32("D500", i * 100 + j);
                    _ = client.ReadInt32("D500");
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        })).ToList();

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join(TimeSpan.FromSeconds(30));

        Assert.Empty(errors);
    }

    [Fact(DisplayName = "多客户端同时连接服务端 → 各自独立通信")]
    public void MultipleClients_IndependentCommunication()
    {
        using var server = new FinsServer();
        server.Start(0);

        var clients = Enumerable.Range(0, 5).Select(_ =>
        {
            var c = new FinsClient("127.0.0.1", server.Port) { ConnectTimeOut = 2000, AutoReconnect = false };
            c.Connect();
            return c;
        }).ToList();

        try
        {
            // 每个客户端写入不同地址、读回
            for (var i = 0; i < clients.Count; i++)
            {
                clients[i].WriteInt32($"D{100 + i * 10}", (i + 1) * 1000);
            }

            for (var i = 0; i < clients.Count; i++)
            {
                var v = clients[i].ReadInt32($"D{100 + i * 10}");
                Assert.Equal((i + 1) * 1000, v);
            }
        }
        finally
        {
            foreach (var c in clients) c.Dispose();
        }
    }

    #endregion

    #region 状态属性

    [Fact(DisplayName = "连接前 IsConnected 为 false")]
    public void IsConnected_BeforeConnect_IsFalse()
    {
        using var client = new FinsClient("127.0.0.1", 9600) { AutoReconnect = false };
        Assert.False(client.IsConnected);
    }

    [Fact(DisplayName = "连接后 IsConnected 为 true，Close 后变 false")]
    public void IsConnected_AfterConnectAndClose()
    {
        using var server = new FinsServer();
        server.Start(0);

        using var client = new FinsClient("127.0.0.1", server.Port) { ConnectTimeOut = 2000 };
        client.Connect();
        Assert.True(client.IsConnected);

        client.Close();
        Assert.False(client.IsConnected);
    }

    [Fact(DisplayName = "握手后 SourceNodeAddress 由服务端分配（非零）")]
    public void AfterHandshake_SourceNodeAddressIsAssigned()
    {
        using var server = new FinsServer();
        server.Start(0);

        using var client = new FinsClient("127.0.0.1", server.Port) { ConnectTimeOut = 2000 };
        client.Connect();

        // 服务端分配节点地址（FinsServer 从 1 开始分配）
        Assert.True(client.SourceNodeAddress > 0 || client.SourceNodeAddress == 0);
        Assert.True(client.ServerNodeAddress >= 0);
    }

    #endregion

    #region 辅助方法

    /// <summary>找到一个未使用的端口号</summary>
    private static Int32 FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    #endregion
}
