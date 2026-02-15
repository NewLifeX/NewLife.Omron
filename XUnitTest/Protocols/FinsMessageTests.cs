using NewLife.Omron.Protocols;
using Xunit;

namespace XUnitTest.Protocols;

/// <summary>FinsMessage构建和解析测试</summary>
public class FinsMessageTests
{
    [Fact(DisplayName = "构建字读取请求")]
    public void BuildReadRequest()
    {
        var addr = FinsAddress.Parse("D100");
        var msg = FinsMessage.BuildReadRequest(addr, 10);

        Assert.Equal(0x01, msg.Command.MRC);
        Assert.Equal(0x01, msg.Command.SRC);
        Assert.NotNull(msg.Data);
        Assert.Equal(6, msg.Data.Length);

        // 验证地址部分
        Assert.Equal(0x82, msg.Data[0]); // DM区
        Assert.Equal(0x00, msg.Data[1]); // 地址高
        Assert.Equal(0x64, msg.Data[2]); // 地址低 (100)
        Assert.Equal(0x00, msg.Data[3]); // 位偏移

        // 验证长度部分
        Assert.Equal(0x00, msg.Data[4]); // 长度高
        Assert.Equal(0x0A, msg.Data[5]); // 长度低 (10)
    }

    [Fact(DisplayName = "构建位读取请求使用位区代码")]
    public void BuildBitReadRequestUsesBitAreaCode()
    {
        var addr = FinsAddress.Parse("CIO100.5");
        var msg = FinsMessage.BuildBitReadRequest(addr, 1);

        Assert.Equal(0x01, msg.Command.MRC);
        Assert.Equal(0x01, msg.Command.SRC);
        Assert.Equal(0x30, msg.Data[0]); // CIO位区代码
        Assert.Equal(0x05, msg.Data[3]); // 位偏移
    }

    [Fact(DisplayName = "构建字写入请求")]
    public void BuildWriteRequest()
    {
        var addr = FinsAddress.Parse("D100");
        var data = new Byte[] { 0x00, 0x0A, 0x00, 0x14 };
        var msg = FinsMessage.BuildWriteRequest(addr, data);

        Assert.Equal(0x01, msg.Command.MRC);
        Assert.Equal(0x02, msg.Command.SRC);
        Assert.Equal(10, msg.Data.Length); // 地址(4) + 长度(2) + 数据(4)

        // 验证字长度
        Assert.Equal(0x00, msg.Data[4]);
        Assert.Equal(0x02, msg.Data[5]); // 4字节 = 2字

        // 验证写入数据
        Assert.Equal(0x00, msg.Data[6]);
        Assert.Equal(0x0A, msg.Data[7]);
    }

    [Fact(DisplayName = "写入奇数长度数据应抛出异常")]
    public void WriteOddLengthShouldThrow()
    {
        var addr = FinsAddress.Parse("D100");
        Assert.Throws<ArgumentException>(() => FinsMessage.BuildWriteRequest(addr, new Byte[3]));
    }

    [Fact(DisplayName = "构建位写入请求")]
    public void BuildBitWriteRequest()
    {
        var addr = FinsAddress.Parse("CIO100.5");
        var msg = FinsMessage.BuildBitWriteRequest(addr, new Byte[] { 0x01 });

        Assert.Equal(0x01, msg.Command.MRC);
        Assert.Equal(0x02, msg.Command.SRC);
        Assert.Equal(0x30, msg.Data[0]); // CIO位区
        Assert.Equal(7, msg.Data.Length); // 地址(4) + 长度(2) + 数据(1)
    }

    [Fact(DisplayName = "构建填充请求")]
    public void BuildFillRequest()
    {
        var addr = FinsAddress.Parse("D100");
        var msg = FinsMessage.BuildFillRequest(addr, 50, 0x1234);

        Assert.Equal(0x01, msg.Command.MRC);
        Assert.Equal(0x03, msg.Command.SRC);
        Assert.Equal(8, msg.Data.Length); // 地址(4) + 长度(2) + 填充值(2)
        Assert.Equal(0x12, msg.Data[6]);
        Assert.Equal(0x34, msg.Data[7]);
    }

    [Fact(DisplayName = "构建多区域读取请求")]
    public void BuildMultipleReadRequest()
    {
        var addrs = new[]
        {
            FinsAddress.Parse("D100"),
            FinsAddress.Parse("D200"),
            FinsAddress.Parse("CIO50")
        };
        var msg = FinsMessage.BuildMultipleReadRequest(addrs);

        Assert.Equal(0x01, msg.Command.MRC);
        Assert.Equal(0x04, msg.Command.SRC);
        Assert.Equal(12, msg.Data.Length); // 3 * 4字节
    }

    [Fact(DisplayName = "构建传送请求")]
    public void BuildTransferRequest()
    {
        var src = FinsAddress.Parse("D100");
        var dst = FinsAddress.Parse("D200");
        var msg = FinsMessage.BuildTransferRequest(src, dst, 10);

        Assert.Equal(0x01, msg.Command.MRC);
        Assert.Equal(0x05, msg.Command.SRC);
        Assert.Equal(10, msg.Data.Length); // 源(4) + 目标(4) + 长度(2)
    }

    [Fact(DisplayName = "构建CPU运行请求")]
    public void BuildRunRequest()
    {
        var msg = FinsMessage.BuildRunRequest(0x04);

        Assert.Equal(0x04, msg.Command.MRC);
        Assert.Equal(0x01, msg.Command.SRC);
        Assert.NotNull(msg.Data);
        Assert.Equal(4, msg.Data.Length);
        Assert.Equal(0x04, msg.Data[2]); // RUN模式
    }

    [Fact(DisplayName = "构建CPU停止请求")]
    public void BuildStopRequest()
    {
        var msg = FinsMessage.BuildStopRequest();

        Assert.Equal(0x04, msg.Command.MRC);
        Assert.Equal(0x02, msg.Command.SRC);
    }

    [Fact(DisplayName = "构建时钟写入请求BCD编码正确")]
    public void BuildClockWriteRequestBcd()
    {
        var dt = new DateTime(2025, 7, 15, 10, 30, 45);
        var msg = FinsMessage.BuildClockWriteRequest(dt);

        Assert.Equal(0x07, msg.Command.MRC);
        Assert.Equal(0x02, msg.Command.SRC);
        Assert.NotNull(msg.Data);
        Assert.Equal(7, msg.Data.Length);
        Assert.Equal(0x25, msg.Data[0]); // 年: 25 -> BCD 0x25
        Assert.Equal(0x07, msg.Data[1]); // 月: 7 -> BCD 0x07
        Assert.Equal(0x15, msg.Data[2]); // 日: 15 -> BCD 0x15
        Assert.Equal(0x10, msg.Data[3]); // 时: 10 -> BCD 0x10
        Assert.Equal(0x30, msg.Data[4]); // 分: 30 -> BCD 0x30
        Assert.Equal(0x45, msg.Data[5]); // 秒: 45 -> BCD 0x45
    }

    [Fact(DisplayName = "ToBytes包含正确的头部和命令")]
    public void ToBytesContainsHeaderAndCommand()
    {
        var addr = FinsAddress.Parse("D100");
        var msg = FinsMessage.BuildReadRequest(addr, 1);
        msg.Header.DA1 = 0x10;
        msg.Header.SA1 = 0x05;

        var bytes = msg.ToBytes();

        // 头部10字节 + 命令2字节 + 数据6字节 = 18字节
        Assert.Equal(18, bytes.Length);
        Assert.Equal(0x80, bytes[0]); // ICF
        Assert.Equal(0x10, bytes[4]); // DA1
        Assert.Equal(0x05, bytes[7]); // SA1
        Assert.Equal(0x01, bytes[10]); // MRC
        Assert.Equal(0x01, bytes[11]); // SRC
    }

    [Fact(DisplayName = "解析成功响应")]
    public void ParseSuccessResponse()
    {
        // 构建模拟响应: 头部(10) + 命令(2) + 结束码(2) + 数据(4)
        var data = new Byte[18];
        data[0] = 0xC0; // ICF (response)
        data[10] = 0x01; // MRC
        data[11] = 0x01; // SRC
        data[12] = 0x00; // EndCode高
        data[13] = 0x00; // EndCode低
        data[14] = 0x00; data[15] = 0x0A; // 数据
        data[16] = 0x00; data[17] = 0x14;

        var msg = FinsMessage.ParseResponse(data);

        Assert.True(msg.IsSuccess);
        Assert.Equal((UInt16)0x0000, msg.EndCode);
        Assert.Equal(4, msg.Data.Length);
        Assert.Equal("成功", msg.GetErrorMessage());
    }

    [Fact(DisplayName = "解析错误响应")]
    public void ParseErrorResponse()
    {
        var data = new Byte[14];
        data[0] = 0xC0;
        data[10] = 0x01;
        data[11] = 0x01;
        data[12] = 0x10; // EndCode: 0x1001 命令格式错误
        data[13] = 0x01;

        var msg = FinsMessage.ParseResponse(data);

        Assert.False(msg.IsSuccess);
        Assert.Equal((UInt16)0x1001, msg.EndCode);
        Assert.Equal("命令格式错误", msg.GetErrorMessage());
    }

    [Fact(DisplayName = "解析数据不足应抛出异常")]
    public void ParseInsufficientDataShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => FinsMessage.ParseResponse(new Byte[10]));
    }

    [Theory]
    [InlineData(0x0000, "成功")]
    [InlineData(0x0205, "响应超时")]
    [InlineData(0x0401, "未定义的命令")]
    [InlineData(0x1001, "命令格式错误")]
    [InlineData(0x2002, "被保护")]
    [InlineData(0x9999, "未知错误代码: 0x9999")]
        public void ErrorMessageMapping(UInt16 endCode, String expectedMessage)
    {
        var msg = new FinsMessage { EndCode = endCode };
        Assert.Equal(expectedMessage, msg.GetErrorMessage());
    }

    [Fact(DisplayName = "BCD编码往返正确")]
    public void BcdRoundTrip()
    {
        for (Byte i = 0; i < 100; i++)
        {
            var bcd = (Byte)((i / 10 << 4) | (i % 10));
            var decoded = FinsMessage.FromBcd(bcd);
            Assert.Equal(i, decoded);
        }
    }
}
