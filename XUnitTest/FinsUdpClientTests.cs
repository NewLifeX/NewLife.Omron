using NewLife.IoT.ThingModels;
using NewLife.Omron.Protocols;

namespace XUnitTest;

/// <summary>FinsUdpClient 属性与初始化测试</summary>
public class FinsUdpClientTests
{
    #region 默认属性值

    [Fact(DisplayName = "默认端口为 9600")]
    public void DefaultPort()
    {
        using var client = new FinsUdpClient();
        Assert.Equal(9600, client.Port);
    }

    [Fact(DisplayName = "默认接收超时为 5000ms")]
    public void DefaultReceiveTimeout()
    {
        using var client = new FinsUdpClient();
        Assert.Equal(5000, client.ReceiveTimeOut);
    }

    [Fact(DisplayName = "默认字节序为 CDAB")]
    public void DefaultByteOrder()
    {
        using var client = new FinsUdpClient();
        Assert.Equal(ByteOrder.CDAB, client.ByteOrder);
    }

    [Fact(DisplayName = "默认 DA2 为 0")]
    public void DefaultDA2()
    {
        using var client = new FinsUdpClient();
        Assert.Equal((Byte)0, client.DA2);
    }

    [Fact(DisplayName = "默认未打开")]
    public void DefaultNotOpened()
    {
        using var client = new FinsUdpClient();
        Assert.False(client.IsOpened);
    }

    [Fact(DisplayName = "默认源节点地址为 0")]
    public void DefaultSourceNodeAddress()
    {
        using var client = new FinsUdpClient();
        Assert.Equal((Byte)0, client.SourceNodeAddress);
    }

    #endregion

    #region 构造函数

    [Fact(DisplayName = "IP 构造函数设置 IpAddress 和 Port")]
    public void ConstructorSetsIpAndPort()
    {
        using var client = new FinsUdpClient("192.168.1.10", 9600);
        Assert.Equal("192.168.1.10", client.IpAddress);
        Assert.Equal(9600, client.Port);
    }

    [Fact(DisplayName = "IP 构造函数从 IP 末段推导目标节点地址")]
    public void ConstructorDerivesDestinationNodeFromIp()
    {
        using var client = new FinsUdpClient("192.168.1.25");
        Assert.Equal((Byte)25, client.DestinationNodeAddress);
    }

    [Fact(DisplayName = "IP 构造函数末段为 0 时节点地址为 0")]
    public void ConstructorDerivesNodeAddress_ZeroLastOctet()
    {
        using var client = new FinsUdpClient("192.168.1.0");
        Assert.Equal((Byte)0, client.DestinationNodeAddress);
    }

    [Fact(DisplayName = "IP 构造函数默认端口 9600")]
    public void ConstructorDefaultPort()
    {
        using var client = new FinsUdpClient("127.0.0.1");
        Assert.Equal(9600, client.Port);
    }

    #endregion

    #region 属性设置

    [Fact(DisplayName = "可设置 IpAddress")]
    public void CanSetIpAddress()
    {
        using var client = new FinsUdpClient();
        client.IpAddress = "10.0.0.1";
        Assert.Equal("10.0.0.1", client.IpAddress);
    }

    [Fact(DisplayName = "可设置 Port")]
    public void CanSetPort()
    {
        using var client = new FinsUdpClient();
        client.Port = 9601;
        Assert.Equal(9601, client.Port);
    }

    [Fact(DisplayName = "可设置接收超时")]
    public void CanSetReceiveTimeout()
    {
        using var client = new FinsUdpClient();
        client.ReceiveTimeOut = 2000;
        Assert.Equal(2000, client.ReceiveTimeOut);
    }

    [Fact(DisplayName = "可设置字节序")]
    public void CanSetByteOrder()
    {
        using var client = new FinsUdpClient();
        client.ByteOrder = ByteOrder.ABCD;
        Assert.Equal(ByteOrder.ABCD, client.ByteOrder);
    }

    [Fact(DisplayName = "可设置 DA2")]
    public void CanSetDA2()
    {
        using var client = new FinsUdpClient();
        client.DA2 = 1;
        Assert.Equal((Byte)1, client.DA2);
    }

    [Fact(DisplayName = "可设置源节点地址")]
    public void CanSetSourceNodeAddress()
    {
        using var client = new FinsUdpClient();
        client.SourceNodeAddress = 20;
        Assert.Equal((Byte)20, client.SourceNodeAddress);
    }

    [Fact(DisplayName = "可设置目标节点地址")]
    public void CanSetDestinationNodeAddress()
    {
        using var client = new FinsUdpClient();
        client.DestinationNodeAddress = 1;
        Assert.Equal((Byte)1, client.DestinationNodeAddress);
    }

    #endregion

    #region 生命周期

    [Fact(DisplayName = "Close 未打开时不抛出异常")]
    public void CloseWhenNotOpened_DoesNotThrow()
    {
        using var client = new FinsUdpClient();
        client.Close(); // 不应抛出异常
    }

    [Fact(DisplayName = "多次 Close 不抛出异常")]
    public void MultipleCloseCallsDoNotThrow()
    {
        using var client = new FinsUdpClient();
        client.Close();
        client.Close();
        client.Close();
    }

    [Fact(DisplayName = "Dispose 等价于 Close")]
    public void DisposeEquivalentToClose()
    {
        var client = new FinsUdpClient();
        client.Dispose(); // 不应抛出异常
    }

    [Fact(DisplayName = "Open 时 IsOpened 变为 true")]
    public void OpenSetsIsOpenedTrue()
    {
        // 用 127.0.0.1 但不绑定到真实 PLC（只测状态）
        using var client = new FinsUdpClient("127.0.0.1", 9600);
        client.Open();
        Assert.True(client.IsOpened);
        client.Close();
    }

    [Fact(DisplayName = "Close 后 IsOpened 变为 false")]
    public void CloseSetsIsOpenedFalse()
    {
        using var client = new FinsUdpClient("127.0.0.1", 9600);
        client.Open();
        client.Close();
        Assert.False(client.IsOpened);
    }

    [Fact(DisplayName = "重复 Open 不抛出异常")]
    public void DoubleOpenDoesNotThrow()
    {
        using var client = new FinsUdpClient("127.0.0.1", 9600);
        client.Open();
        client.Open(); // 第二次 Open 应幂等
        client.Close();
    }

    #endregion

    #region Transform

    [Fact(DisplayName = "Transform 不为 null")]
    public void TransformNotNull()
    {
        using var client = new FinsUdpClient();
        Assert.NotNull(client.Transform);
    }

    [Fact(DisplayName = "ByteOrder 属性与 Transform.ByteOrder 同步")]
    public void ByteOrderSyncWithTransform()
    {
        using var client = new FinsUdpClient();
        client.ByteOrder = ByteOrder.DCBA;
        Assert.Equal(ByteOrder.DCBA, client.Transform.ByteOrder);
    }

    #endregion
}
