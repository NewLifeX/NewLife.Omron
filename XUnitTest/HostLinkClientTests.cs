using System.IO;
using NewLife.IoT.ThingModels;
using NewLife.Omron.Protocols;

namespace XUnitTest;

/// <summary>HostLinkClient 属性与初始化测试</summary>
public class HostLinkClientTests
{
    #region 默认属性值

    [Fact(DisplayName = "默认单元号为 0")]
    public void DefaultUnitNo()
    {
        using var stream = new MemoryStream();
        using var client = new HostLinkClient(stream);
        Assert.Equal((Byte)0, client.UnitNo);
    }

    [Fact(DisplayName = "默认接收超时为 5000ms")]
    public void DefaultReceiveTimeout()
    {
        using var stream = new MemoryStream();
        using var client = new HostLinkClient(stream);
        Assert.Equal(5000, client.ReceiveTimeOut);
    }

    [Fact(DisplayName = "默认字节序为 CDAB")]
    public void DefaultByteOrder()
    {
        using var stream = new MemoryStream();
        using var client = new HostLinkClient(stream);
        Assert.Equal(ByteOrder.CDAB, client.ByteOrder);
    }

    [Fact(DisplayName = "从流构造后 IsOpened 为 true")]
    public void IsOpenedWhenConstructedWithStream()
    {
        using var stream = new MemoryStream();
        using var client = new HostLinkClient(stream);
        Assert.True(client.IsOpened);
    }

    #endregion

    #region 属性设置

    [Fact(DisplayName = "可设置单元号")]
    public void CanSetUnitNo()
    {
        using var stream = new MemoryStream();
        using var client = new HostLinkClient(stream);
        client.UnitNo = 5;
        Assert.Equal((Byte)5, client.UnitNo);
    }

    [Fact(DisplayName = "可设置接收超时")]
    public void CanSetReceiveTimeout()
    {
        using var stream = new MemoryStream();
        using var client = new HostLinkClient(stream);
        client.ReceiveTimeOut = 2000;
        Assert.Equal(2000, client.ReceiveTimeOut);
    }

    [Fact(DisplayName = "可设置字节序")]
    public void CanSetByteOrder()
    {
        using var stream = new MemoryStream();
        using var client = new HostLinkClient(stream);
        client.ByteOrder = ByteOrder.ABCD;
        Assert.Equal(ByteOrder.ABCD, client.ByteOrder);
    }

    #endregion

    #region 构造函数验证

    [Fact(DisplayName = "Stream 为 null 应抛出 ArgumentNullException")]
    public void NullStreamShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new HostLinkClient(null));
    }

    [Fact(DisplayName = "ownStream=false 时 Dispose 不关闭外部流")]
    public void DisposeWithOwnStreamFalse_DoesNotCloseStream()
    {
        var stream = new MemoryStream();
        var client = new HostLinkClient(stream, ownStream: false);
        client.Dispose();
        // 流依然可写
        Assert.True(stream.CanWrite);
    }

    [Fact(DisplayName = "ownStream=true 时 Dispose 关闭流")]
    public void DisposeWithOwnStreamTrue_ClosesStream()
    {
        var stream = new MemoryStream();
        var client = new HostLinkClient(stream, ownStream: true);
        client.Dispose();
        // 流已关闭
        Assert.False(stream.CanWrite);
    }

    #endregion

    #region Transform

    [Fact(DisplayName = "Transform 不为 null")]
    public void TransformNotNull()
    {
        using var stream = new MemoryStream();
        using var client = new HostLinkClient(stream);
        Assert.NotNull(client.Transform);
    }

    [Fact(DisplayName = "ByteOrder 属性与 Transform.ByteOrder 同步")]
    public void ByteOrderSyncWithTransform()
    {
        using var stream = new MemoryStream();
        using var client = new HostLinkClient(stream);
        client.ByteOrder = ByteOrder.BADC;
        Assert.Equal(ByteOrder.BADC, client.Transform.ByteOrder);
    }

    #endregion
}
