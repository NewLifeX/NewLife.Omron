using System;
using System.ComponentModel;
using NewLife.IoT.ThingModels;
using NewLife.Omron.Drivers;

namespace XUnitTest;

/// <summary>欧姆龙驱动单元测试</summary>
[DisplayName("OmronDriver驱动测试")]
public class OmronDriverTests
{
    #region GetAddress

    [Fact]
    [DisplayName("GetAddress返回纯地址不带冒号后缀")]
    public void GetAddress_StripColonSuffix()
    {
        var driver = new OmronDriver();
        var point = new TestPoint { Address = "D100:50" };
        var addr = driver.GetAddress(point);
        Assert.Equal("D100", addr);
    }

    [Fact]
    [DisplayName("GetAddress返回纯地址不带点号后缀")]
    public void GetAddress_StripDotSuffix()
    {
        var driver = new OmronDriver();
        var point = new TestPoint { Address = "D100.5" };
        var addr = driver.GetAddress(point);
        Assert.Equal("D100", addr);
    }

    [Fact]
    [DisplayName("GetAddress无后缀时原样返回")]
    public void GetAddress_NoSuffix()
    {
        var driver = new OmronDriver();
        var point = new TestPoint { Address = "DM200" };
        var addr = driver.GetAddress(point);
        Assert.Equal("DM200", addr);
    }

    [Fact]
    [DisplayName("GetAddress支持WR区地址")]
    public void GetAddress_WR_Area()
    {
        var driver = new OmronDriver();
        var point = new TestPoint { Address = "WR10" };
        var addr = driver.GetAddress(point);
        Assert.Equal("WR10", addr);
    }

    [Fact]
    [DisplayName("GetAddress支持HR区地址")]
    public void GetAddress_HR_Area()
    {
        var driver = new OmronDriver();
        var point = new TestPoint { Address = "HR5" };
        var addr = driver.GetAddress(point);
        Assert.Equal("HR5", addr);
    }

    [Fact]
    [DisplayName("GetAddress支持CIO区地址")]
    public void GetAddress_CIO_Area()
    {
        var driver = new OmronDriver();
        var point = new TestPoint { Address = "CIO100" };
        var addr = driver.GetAddress(point);
        Assert.Equal("CIO100", addr);
    }

    [Fact]
    [DisplayName("GetAddress点位为null时抛出异常")]
    public void GetAddress_NullPoint_Throws()
    {
        var driver = new OmronDriver();
        Assert.Throws<ArgumentException>(() => driver.GetAddress(null));
    }

    #endregion

    #region CreateParameter

    [Fact]
    [DisplayName("CreateParameter返回OmronParameter实例")]
    public void CreateParameter_ReturnsOmronParameter()
    {
        var driver = new OmronDriver();
        var pm = driver.CreateParameter(null);
        Assert.IsType<OmronParameter>(pm);
    }

    [Fact]
    [DisplayName("CreateParameter默认地址正确")]
    public void CreateParameter_DefaultAddress()
    {
        var driver = new OmronDriver();
        var pm = driver.CreateParameter(null) as OmronParameter;
        Assert.NotNull(pm);
        Assert.Equal("127.0.0.1:9600", pm.Address);
    }

    [Fact]
    [DisplayName("CreateParameter默认DataFormat为CDAB")]
    public void CreateParameter_DefaultDataFormat()
    {
        var driver = new OmronDriver();
        var pm = driver.CreateParameter(null) as OmronParameter;
        Assert.NotNull(pm);
        Assert.Equal("CDAB", pm.DataFormat);
    }

    [Fact]
    [DisplayName("CreateParameter默认DA2为0")]
    public void CreateParameter_DefaultDA2()
    {
        var driver = new OmronDriver();
        var pm = driver.CreateParameter(null) as OmronParameter;
        Assert.NotNull(pm);
        Assert.Equal(0, pm.DA2);
    }

    #endregion
}

/// <summary>用于测试的点位实现</summary>
public class TestPoint : IPoint
{
    /// <summary>点位名称</summary>
    public String Name { get; set; }

    /// <summary>点位地址</summary>
    public String Address { get; set; }

    /// <summary>点位类型</summary>
    public String Type { get; set; }

    /// <summary>点位长度</summary>
    public Int32 Length { get; set; }
}
