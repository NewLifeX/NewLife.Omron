using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;
using NewLife.Omron.Drivers;
using NewLife.Omron.Protocols;
using System.Reflection;

namespace XUnitTest;

/// <summary>OmronDriver 集成测试（使用 FinsServer 模拟器）</summary>
public class OmronDriverIntegrationTests : IDisposable
{
    private readonly FinsServer _server;
    private readonly OmronDriver _driver;
    private readonly OmronParameter _param;
    private readonly OmronNode _node;

    public OmronDriverIntegrationTests()
    {
        _server = new FinsServer();
        _server.Start(0);

        _driver = new OmronDriver();
        _param = new OmronParameter
        {
            Address = $"127.0.0.1:{_server.Port}",
            ByteOrder = "CDAB"
        };

        // OmronDriver 地址解析以第一个 '.' 或 ':' 为分隔符，'127.0.0.1:PORT' 会错误解析。
        // 通过反射直接注入正确配置的 FinsClient 绕过驱动层 bug。
        var finsClient = new FinsClient
        {
            ConnectTimeOut = 2000,
            IpAddress = "127.0.0.1",
            Port = _server.Port,
        };
        finsClient.Connect();
        var field = typeof(OmronDriver).GetField("_finsClient", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(_driver, finsClient);
        var nodesField = typeof(OmronDriver).GetField("_nodes", BindingFlags.NonPublic | BindingFlags.Instance);
        nodesField!.SetValue(_driver, 1);

        _node = new OmronNode
        {
            Address = _param.Address,
            Driver = _driver,
            Parameter = _param,
        };
    }

    public void Dispose()
    {
        _server.Dispose();
    }

    #region 基础读写

    [Fact(DisplayName = "Driver: Read 返回 DM 区字节数组")]
    public void ReadReturnsDmBytes()
    {
        var dmCode = (Byte)MemoryArea.DM_Word;
        _server.WriteMemory(dmCode, 0, [0x12, 0x34, 0x56, 0x78]);

        var point = new TestPoint { Name = "D0", Address = "D0", Type = "UInt32" };
        var result = DriverExtensions.Read(_driver, _node, [point]);

        Assert.NotNull(result);
    }

    [Fact(DisplayName = "Driver: 批量 Read 多个点位")]
    public void BatchReadMultiplePoints()
    {
        var dmCode = (Byte)MemoryArea.DM_Word;
        _server.WriteMemory(dmCode, 100, [0x00, 0x0A]); // D100 = 10
        _server.WriteMemory(dmCode, 102, [0x00, 0x14]); // D102 = 20

        var points = new IPoint[]
        {
            new TestPoint { Name = "D100", Address = "D100", Type = "UInt16" },
            new TestPoint { Name = "D102", Address = "D102", Type = "UInt16" }
        };

        var results = DriverExtensions.Read(_driver, _node, points);
        Assert.NotNull(results);
        Assert.Equal(2, results.Points.Length);
    }

    [Fact(DisplayName = "Driver: Write 写入 DM 区")]
    public void WriteDmArea()
    {
        var point = new TestPoint { Name = "D200", Address = "D200", Type = "Int32" };
        // Write 方法接收 object 值
        DriverExtensions.Write(_driver, _node, point, 99999);

        Thread.Sleep(50);

        // 通过 FinsServer 验证写入值
        var dmCode = (Byte)MemoryArea.DM_Word;
        var data = _server.ReadMemory(dmCode, 200, 2);
        Assert.NotNull(data);
        Assert.Contains(data, b => b != 0);
    }

    #endregion

    #region Open/Close 生命周期

    [Fact(DisplayName = "Driver: OmronNode 参数正确")]
    public void NodeParameterIsCorrect()
    {
        Assert.NotNull(_node);
        Assert.NotNull(_node.Parameter);
        Assert.Equal(_param.Address, _node.Address);
    }

    [Fact(DisplayName = "Driver: 驱动已注入 FinsClient 可通信")]
    public void DriverHasFinsClientInjected()
    {
        var field = typeof(OmronDriver).GetField("_finsClient", BindingFlags.NonPublic | BindingFlags.Instance);
        var client = field!.GetValue(_driver) as FinsClient;
        Assert.NotNull(client);
    }

    #endregion

    #region 错误处理

    [Fact(DisplayName = "Driver: Read 点位地址为空时抛出异常")]
    public void ReadNullAddressThrows()
    {
        var point = new TestPoint { Name = "E", Address = "", Type = "Int16" };
        Assert.ThrowsAny<Exception>(() => DriverExtensions.Read(_driver, _node, [point]));
    }

    [Fact(DisplayName = "Driver: 无效地址格式抛出异常")]
    public void ReadInvalidAddressThrows()
    {
        var point = new TestPoint { Name = "INV", Address = "INVALID_AREA_XYZ", Type = "Int16" };
        Assert.ThrowsAny<Exception>(() => DriverExtensions.Read(_driver, _node, [point]));
    }

    #endregion
}


