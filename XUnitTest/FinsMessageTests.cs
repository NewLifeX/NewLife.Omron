using System;
using System.ComponentModel;
using NewLife.Omron.Protocols;

namespace XUnitTest;

/// <summary>FINS消息测试</summary>
[DisplayName("FinsMessage消息构建与解析")]
public class FinsMessageTests
{
    #region BuildReadRequest

    [Fact]
    [DisplayName("构建读取请求基本结构")]
    public void BuildReadRequest_BasicStructure()
    {
        var addr = FinsAddress.Parse("D100");
        var msg = FinsMessage.BuildReadRequest(addr, 2);

        Assert.Equal(FinsCommand.Read.MRC, msg.Command.MRC);
        Assert.Equal(FinsCommand.Read.SRC, msg.Command.SRC);
        Assert.NotNull(msg.Data);
        Assert.Equal(6, msg.Data.Length);
    }

    [Fact]
    [DisplayName("读取请求含正确的存储区类型")]
    public void BuildReadRequest_MemoryTypeInData()
    {
        var addr = FinsAddress.Parse("D100");
        var msg = FinsMessage.BuildReadRequest(addr, 10);

        // data[0] = MemoryType, data[1..2] = Address (big-endian), data[3] = BitOffset
        Assert.Equal(0x82, msg.Data[0]);    // DM区
        Assert.Equal(0x00, msg.Data[1]);    // 高字节
        Assert.Equal(0x64, msg.Data[2]);    // 低字节 (100 = 0x64)
        Assert.Equal(0x00, msg.Data[3]);    // 位偏移
    }

    [Fact]
    [DisplayName("读取请求含正确的字数")]
    public void BuildReadRequest_WordCountInData()
    {
        var addr = FinsAddress.Parse("D100");
        var msg = FinsMessage.BuildReadRequest(addr, 5);

        // data[4..5] = word count (big-endian)
        Assert.Equal(0x00, msg.Data[4]);
        Assert.Equal(0x05, msg.Data[5]);
    }

    [Fact]
    [DisplayName("读取请求DA2参数")]
    public void BuildReadRequest_DA2()
    {
        var addr = FinsAddress.Parse("D0");
        var msg = FinsMessage.BuildReadRequest(addr, 1, 0xFE);

        Assert.Equal(0xFE, msg.Header.DA2);
    }

    [Fact]
    [DisplayName("读取请求大字数值编码")]
    public void BuildReadRequest_LargeWordCount()
    {
        var addr = FinsAddress.Parse("D0");
        var msg = FinsMessage.BuildReadRequest(addr, 0x0123);

        Assert.Equal(0x01, msg.Data[4]);
        Assert.Equal(0x23, msg.Data[5]);
    }

    #endregion

    #region BuildWriteRequest

    [Fact]
    [DisplayName("构建写入请求基本结构")]
    public void BuildWriteRequest_BasicStructure()
    {
        var addr = FinsAddress.Parse("D100");
        var data = new Byte[] { 0x00, 0x01 }; // 1个字
        var msg = FinsMessage.BuildWriteRequest(addr, data);

        Assert.Equal(FinsCommand.Write.MRC, msg.Command.MRC);
        Assert.Equal(FinsCommand.Write.SRC, msg.Command.SRC);
        Assert.NotNull(msg.Data);
        Assert.Equal(8, msg.Data.Length); // 4 (addr) + 2 (wordCount) + 2 (data)
    }

    [Fact]
    [DisplayName("写入请求含正确的字数")]
    public void BuildWriteRequest_WordCount()
    {
        var addr = FinsAddress.Parse("D100");
        var data = new Byte[] { 0x00, 0x01, 0x00, 0x02 }; // 2个字
        var msg = FinsMessage.BuildWriteRequest(addr, data);

        // data[4..5] = word count = 2
        Assert.Equal(0x00, msg.Data[4]);
        Assert.Equal(0x02, msg.Data[5]);
    }

    [Fact]
    [DisplayName("写入请求数据被正确嵌入")]
    public void BuildWriteRequest_DataEmbedded()
    {
        var addr = FinsAddress.Parse("D0");
        var writeData = new Byte[] { 0xAB, 0xCD };
        var msg = FinsMessage.BuildWriteRequest(addr, writeData);

        // data[6..7] = actual write data
        Assert.Equal(0xAB, msg.Data[6]);
        Assert.Equal(0xCD, msg.Data[7]);
    }

    [Fact]
    [DisplayName("奇数字节长度抛出异常")]
    public void BuildWriteRequest_OddLength_Throws()
    {
        var addr = FinsAddress.Parse("D100");
        var data = new Byte[] { 0x01, 0x02, 0x03 }; // 3字节，奇数
        Assert.Throws<ArgumentException>(() => FinsMessage.BuildWriteRequest(addr, data));
    }

    [Fact]
    [DisplayName("写入请求DA2参数")]
    public void BuildWriteRequest_DA2()
    {
        var addr = FinsAddress.Parse("D0");
        var data = new Byte[] { 0x00, 0x01 };
        var msg = FinsMessage.BuildWriteRequest(addr, data, 0x05);

        Assert.Equal(0x05, msg.Header.DA2);
    }

    #endregion

    #region ToBytes

    [Fact]
    [DisplayName("ToBytes输出包含头部+命令+数据")]
    public void ToBytes_IncludesHeaderCommandData()
    {
        var addr = FinsAddress.Parse("D100");
        var msg = FinsMessage.BuildReadRequest(addr, 2);
        var bytes = msg.ToBytes();

        // 至少 10 (header) + 2 (command) + 6 (data) = 18 bytes
        Assert.True(bytes.Length >= 18);
        // 前10字节是header，第10-11字节是command
        Assert.Equal(0x01, bytes[10]); // MRC
        Assert.Equal(0x01, bytes[11]); // SRC (read)
    }

    [Fact]
    [DisplayName("ToBytes无数据时正常输出")]
    public void ToBytes_NoData()
    {
        var msg = new FinsMessage
        {
            Header = new FinsHeader(),
            Command = FinsCommand.Read,
            Data = null
        };
        var bytes = msg.ToBytes();
        Assert.Equal(12, bytes.Length); // 10 + 2
    }

    #endregion

    #region ParseResponse

    [Fact]
    [DisplayName("ParseResponse解析成功响应")]
    public void ParseResponse_SuccessResponse()
    {
        // 构造一个合法的响应字节数组
        // 10 bytes header + 2 bytes command + 2 bytes endcode + 4 bytes data
        var response = new Byte[18];
        // Header
        response[0] = 0xC0; // ICF (response)
        response[1] = 0x00; // RSV
        response[2] = 0x02; // GCT
        response[3] = 0x00; // DNA
        response[4] = 0x00; // DA1
        response[5] = 0x00; // DA2
        response[6] = 0x00; // SNA
        response[7] = 0x00; // SA1
        response[8] = 0x00; // SA2
        response[9] = 0x01; // SID
        // Command (echo back)
        response[10] = 0x01; // MRC
        response[11] = 0x01; // SRC
        // EndCode = 0x0000 (success)
        response[12] = 0x00;
        response[13] = 0x00;
        // Data (2 words = 4 bytes)
        response[14] = 0x00;
        response[15] = 0x01;
        response[16] = 0x00;
        response[17] = 0x02;

        var msg = FinsMessage.ParseResponse(response);
        Assert.True(msg.IsSuccess);
        Assert.Equal(0x0000, msg.EndCode);
        Assert.NotNull(msg.Data);
        Assert.Equal(4, msg.Data.Length);
        Assert.Equal(0x00, msg.Data[0]);
        Assert.Equal(0x01, msg.Data[1]);
    }

    [Fact]
    [DisplayName("ParseResponse解析失败响应")]
    public void ParseResponse_FailureResponse()
    {
        var response = new Byte[14];
        // Header (10 bytes)
        response[0] = 0xC0;
        response[1] = 0x00;
        response[2] = 0x02;
        // ... rest zeros
        // Command
        response[10] = 0x01;
        response[11] = 0x01;
        // EndCode = 0x1003 (读取长度过长)
        response[12] = 0x10;
        response[13] = 0x03;

        var msg = FinsMessage.ParseResponse(response);
        Assert.False(msg.IsSuccess);
        Assert.Equal(0x1003, msg.EndCode);
    }

    [Fact]
    [DisplayName("ParseResponse无附加数据时Data为null")]
    public void ParseResponse_NoData_DataIsNull()
    {
        var response = new Byte[14];
        response[10] = 0x01;
        response[11] = 0x02;
        // EndCode = 0 (success)

        var msg = FinsMessage.ParseResponse(response);
        Assert.Null(msg.Data);
    }

    [Fact]
    [DisplayName("ParseResponse数据不足抛出异常")]
    public void ParseResponse_InsufficientData_Throws()
    {
        Assert.Throws<ArgumentException>(() => FinsMessage.ParseResponse(new Byte[10]));
    }

    [Fact]
    [DisplayName("ParseResponse null数据抛出异常")]
    public void ParseResponse_NullData_Throws()
    {
        Assert.Throws<ArgumentException>(() => FinsMessage.ParseResponse(null));
    }

    #endregion

    #region IsSuccess & GetErrorMessage

    [Fact]
    [DisplayName("成功响应IsSuccess为true")]
    public void IsSuccess_True_WhenEndCodeZero()
    {
        var msg = new FinsMessage { EndCode = 0x0000 };
        Assert.True(msg.IsSuccess);
    }

    [Fact]
    [DisplayName("失败响应IsSuccess为false")]
    public void IsSuccess_False_WhenEndCodeNonZero()
    {
        var msg = new FinsMessage { EndCode = 0x0101 };
        Assert.False(msg.IsSuccess);
    }

    [Theory]
    [InlineData(0x0000, "成功")]
    [InlineData(0x0001, "服务被取消")]
    [InlineData(0x0101, "本地节点不在网络中")]
    [InlineData(0x0102, "令牌超时")]
    [InlineData(0x0103, "重试失败")]
    [InlineData(0x0104, "发送帧数超过最大值")]
    [InlineData(0x0105, "节点地址范围错误")]
    [InlineData(0x0106, "节点地址重复")]
    [InlineData(0x0201, "目标节点不在网络中")]
    [InlineData(0x0202, "没有可用单元")]
    [InlineData(0x0203, "第三个节点不存在")]
    [InlineData(0x0204, "目标节点繁忙")]
    [InlineData(0x0205, "响应超时")]
    [InlineData(0x0301, "通信控制器错误")]
    [InlineData(0x0302, "CPU单元错误")]
    [InlineData(0x0303, "控制器板错误")]
    [InlineData(0x0304, "单元号错误")]
    [InlineData(0x0401, "未定义的命令")]
    [InlineData(0x0402, "不支持的命令")]
    [InlineData(0x0501, "目标地址设置错误")]
    [InlineData(0x0502, "路由表错误")]
    [InlineData(0x0503, "路由表未注册")]
    [InlineData(0x0504, "路由错误")]
    [InlineData(0x1001, "命令格式错误")]
    [InlineData(0x1002, "参数错误")]
    [InlineData(0x1003, "读取长度过长")]
    [InlineData(0x1004, "写入长度过长")]
    [InlineData(0x1101, "程序区域错误")]
    [InlineData(0x1102, "访问大小错误")]
    [InlineData(0x1103, "地址范围错误")]
    [InlineData(0x1104, "地址超出范围")]
    [InlineData(0x2002, "被保护")]
    [InlineData(0x2003, "不能进入指定的模式")]
    [InlineData(0x2004, "PLC正在运行")]
    [InlineData(0x2005, "PLC已停止")]
    [InlineData(0x2006, "程序不存在")]
    [InlineData(0x2007, "文件不存在")]
    [InlineData(0x2101, "内存错误")]
    [DisplayName("GetErrorMessage返回正确错误描述")]
    public void GetErrorMessage_KnownCodes(UInt16 endCode, String expectedMessage)
    {
        var msg = new FinsMessage { EndCode = endCode };
        Assert.Equal(expectedMessage, msg.GetErrorMessage());
    }

    [Fact]
    [DisplayName("未知错误码返回包含错误码的描述")]
    public void GetErrorMessage_UnknownCode()
    {
        var msg = new FinsMessage { EndCode = 0xFFFF };
        var errorMsg = msg.GetErrorMessage();
        Assert.Contains("FFFF", errorMsg, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
