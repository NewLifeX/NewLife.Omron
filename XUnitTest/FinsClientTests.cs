using System;
using System.ComponentModel;
using NewLife.Omron.Protocols;

namespace XUnitTest;

/// <summary>FINS客户端单元测试</summary>
[DisplayName("FinsClient客户端测试")]
public class FinsClientTests
{
    #region 默认属性值

    [Fact]
    [DisplayName("默认端口为9600")]
    public void DefaultPort_Is9600()
    {
        var client = new FinsClient();
        Assert.Equal(9600, client.Port);
    }

    [Fact]
    [DisplayName("默认连接超时为2000ms")]
    public void DefaultConnectTimeOut_Is2000()
    {
        var client = new FinsClient();
        Assert.Equal(2000, client.ConnectTimeOut);
    }

    [Fact]
    [DisplayName("默认接收超时为5000ms")]
    public void DefaultReceiveTimeOut_Is5000()
    {
        var client = new FinsClient();
        Assert.Equal(5000, client.ReceiveTimeOut);
    }

    [Fact]
    [DisplayName("默认DA2为0")]
    public void DefaultDA2_IsZero()
    {
        var client = new FinsClient();
        Assert.Equal(0, client.DA2);
    }

    [Fact]
    [DisplayName("默认DataFormat为CDAB")]
    public void DefaultDataFormat_IsCDAB()
    {
        var client = new FinsClient();
        Assert.Equal(DataFormat.CDAB, client.DataFormat);
    }

    [Fact]
    [DisplayName("默认IpAddress为null")]
    public void DefaultIpAddress_IsNull()
    {
        var client = new FinsClient();
        Assert.Null(client.IpAddress);
    }

    #endregion

    #region 属性设置

    [Fact]
    [DisplayName("IpAddress可以设置")]
    public void IpAddress_CanBeSet()
    {
        var client = new FinsClient { IpAddress = "192.168.1.100" };
        Assert.Equal("192.168.1.100", client.IpAddress);
    }

    [Fact]
    [DisplayName("Port可以设置")]
    public void Port_CanBeSet()
    {
        var client = new FinsClient { Port = 12345 };
        Assert.Equal(12345, client.Port);
    }

    [Fact]
    [DisplayName("ConnectTimeOut可以设置")]
    public void ConnectTimeOut_CanBeSet()
    {
        var client = new FinsClient { ConnectTimeOut = 5000 };
        Assert.Equal(5000, client.ConnectTimeOut);
    }

    [Fact]
    [DisplayName("ReceiveTimeOut可以设置")]
    public void ReceiveTimeOut_CanBeSet()
    {
        var client = new FinsClient { ReceiveTimeOut = 10000 };
        Assert.Equal(10000, client.ReceiveTimeOut);
    }

    [Fact]
    [DisplayName("DA2可以设置")]
    public void DA2_CanBeSet()
    {
        var client = new FinsClient { DA2 = 0xFE };
        Assert.Equal(0xFE, client.DA2);
    }

    [Fact]
    [DisplayName("DataFormat可以设置为ABCD")]
    public void DataFormat_CanBeSetToABCD()
    {
        var client = new FinsClient { DataFormat = DataFormat.ABCD };
        Assert.Equal(DataFormat.ABCD, client.DataFormat);
    }

    [Fact]
    [DisplayName("DataFormat可以设置为BADC")]
    public void DataFormat_CanBeSetToBADC()
    {
        var client = new FinsClient { DataFormat = DataFormat.BADC };
        Assert.Equal(DataFormat.BADC, client.DataFormat);
    }

    [Fact]
    [DisplayName("DataFormat可以设置为DCBA")]
    public void DataFormat_CanBeSetToDCBA()
    {
        var client = new FinsClient { DataFormat = DataFormat.DCBA };
        Assert.Equal(DataFormat.DCBA, client.DataFormat);
    }

    #endregion

    #region 生命周期

    [Fact]
    [DisplayName("未连接时Close不抛出异常")]
    public void Close_WithoutConnection_NoThrow()
    {
        var client = new FinsClient();
        var ex = Record.Exception(() => client.Close());
        Assert.Null(ex);
    }

    [Fact]
    [DisplayName("未连接时Dispose不抛出异常")]
    public void Dispose_WithoutConnection_NoThrow()
    {
        var client = new FinsClient();
        var ex = Record.Exception(() => client.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    [DisplayName("多次Close不抛出异常")]
    public void Close_MultipleTimesNoThrow()
    {
        var client = new FinsClient();
        var ex = Record.Exception(() =>
        {
            client.Close();
            client.Close();
        });
        Assert.Null(ex);
    }

    [Fact]
    [DisplayName("using语句释放不抛出异常")]
    public void UsingDispose_NoThrow()
    {
        var ex = Record.Exception(() =>
        {
            using var client = new FinsClient
            {
                IpAddress = "127.0.0.1",
                Port = 9600,
                ConnectTimeOut = 100
            };
        });
        Assert.Null(ex);
    }

    #endregion

    #region 连接错误

    [Fact]
    [DisplayName("连接不可用端口抛出异常")]
    public void Connect_Unavailable_Port_ThrowsException()
    {
        // 使用本机回环地址 + 明确不监听的端口，触发可预测的连接失败
        // 先用 TcpListener 分配临时端口后立即关闭，确保该端口没有服务在监听
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var client = new FinsClient
        {
            IpAddress = "127.0.0.1",
            Port = port,
            ConnectTimeOut = 500
        };
        // 连接应失败并抛出异常
        var ex = Assert.ThrowsAny<Exception>(() => client.Connect());
        Assert.NotNull(ex);
    }

    #endregion
}
