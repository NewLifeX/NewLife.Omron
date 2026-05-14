using NewLife.Omron.Protocols;

namespace XUnitTest.Protocols;

/// <summary>HostLinkMessage 帧编码/解码测试</summary>
public class HostLinkMessageTests
{
    #region FCS 校验

    [Fact(DisplayName = "FCS 计算：已知帧验证")]
    public void CalculateFcs_KnownFrame()
    {
        // @ 0 0 R D 0 0 0 0 0 0 0 A  的 FCS
        // '@'(0x40)^'0'(0x30)^'0'(0x30)^'R'(0x52)^'D'(0x44)^'0'^'0'^'0'^'0'^'0'^'0'^'0'^'A'(0x41)
        var frame = "@00RD0000000A";
        var fcs = HostLinkMessage.CalculateFcs(frame);

        // 预期 FCS 为 0x40^0x30^0x30^0x52^0x44^0x30^0x30^0x30^0x30^0x30^0x30^0x30^0x41
        Byte expected = 0;
        foreach (var ch in frame)
            expected ^= (Byte)ch;

        Assert.Equal(expected, fcs);
    }

    [Fact(DisplayName = "FCS 计算：空字符串返回 0")]
    public void CalculateFcs_EmptyString_ReturnsZero()
    {
        var fcs = HostLinkMessage.CalculateFcs("");
        Assert.Equal((Byte)0, fcs);
    }

    [Fact(DisplayName = "FCS 计算：单字符返回字符值")]
    public void CalculateFcs_SingleChar()
    {
        var fcs = HostLinkMessage.CalculateFcs("@");
        Assert.Equal((Byte)'@', fcs);
    }

    #endregion

    #region 帧序列化 (ToBytes)

    [Fact(DisplayName = "ToBytes：RD 命令帧格式正确")]
    public void ToBytes_ReadDmCommand()
    {
        var msg = new HostLinkMessage(0, "RD", "00000001");
        var bytes = msg.ToBytes();
        var str = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("@00RD00000001", str);
        Assert.Contains("*", str);
        Assert.EndsWith("*\r", str);
        // 长度：@ + 00 + RD + 00000001 + FCS(2) + * + CR = 17字节
        Assert.Equal(17, bytes.Length);
    }

    [Fact(DisplayName = "ToBytes：WD 命令帧格式正确")]
    public void ToBytes_WriteDmCommand()
    {
        var msg = new HostLinkMessage(0, "WD", "00000001ABCD");
        var bytes = msg.ToBytes();
        var str = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("@00WD", str);
        Assert.EndsWith("*\r", str);
    }

    [Fact(DisplayName = "ToBytes：单元号两位补零")]
    public void ToBytes_UnitNoPaddedToTwoDigits()
    {
        var msg = new HostLinkMessage(5, "TS", "00");
        var bytes = msg.ToBytes();
        var str = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("@05TS00", str);
    }

    [Fact(DisplayName = "ToBytes：单元号最大值31")]
    public void ToBytes_UnitNoMax()
    {
        var msg = new HostLinkMessage(31, "RR", "00000001");
        var bytes = msg.ToBytes();
        var str = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("@31RR", str);
    }

    [Fact(DisplayName = "ToBytes：FCS 嵌入正确位置")]
    public void ToBytes_FcsAtCorrectPosition()
    {
        var msg = new HostLinkMessage(0, "TS", "00");
        var bytes = msg.ToBytes();
        var str = System.Text.Encoding.ASCII.GetString(bytes);

        // 格式: @00TS00{FCS}*CR
        // FCS 在 * 前两个字符
        var starIdx = str.IndexOf('*');
        Assert.True(starIdx > 2);

        // 验证 FCS 正确
        var frameForFcs = str[..^4]; // 去掉 FCS(2)+*(1)+CR(1) → 去掉末尾4字符
        var expectedFcs = HostLinkMessage.CalculateFcs(frameForFcs);
        var actualFcsStr = str[(starIdx - 2)..starIdx];
        var actualFcs = Convert.ToByte(actualFcsStr, 16);

        Assert.Equal(expectedFcs, actualFcs);
    }

    #endregion

    #region 帧解析 (ParseResponse)

    [Fact(DisplayName = "ParseResponse：成功响应解析正确")]
    public void ParseResponse_SuccessResponse()
    {
        // 构建已知正确的响应帧
        var msg = new HostLinkMessage(0, "RD", null) { EndCode = 0x00, ResponseData = "00640000" };
        // 手工构造响应帧字符串
        var frameContent = "@00RD0000640000";
        var fcs = HostLinkMessage.CalculateFcs(frameContent);
        var frame = $"{frameContent}{fcs:X2}*\r";
        var bytes = System.Text.Encoding.ASCII.GetBytes(frame);

        var parsed = HostLinkMessage.ParseResponse(bytes);

        Assert.Equal(0, parsed.UnitNo);
        Assert.Equal("RD", parsed.HeaderCode);
        Assert.Equal(0x00, parsed.EndCode);
        Assert.True(parsed.IsSuccess);
    }

    [Fact(DisplayName = "ParseResponse：错误响应解析 EndCode")]
    public void ParseResponse_ErrorResponse()
    {
        var frameContent = "@00RD04";
        var fcs = HostLinkMessage.CalculateFcs(frameContent);
        var frame = $"{frameContent}{fcs:X2}*\r";
        var bytes = System.Text.Encoding.ASCII.GetBytes(frame);

        var parsed = HostLinkMessage.ParseResponse(bytes);

        Assert.Equal(0x04, parsed.EndCode);
        Assert.False(parsed.IsSuccess);
    }

    [Fact(DisplayName = "ParseResponse：数据不足应抛出异常")]
    public void ParseResponse_InsufficientData_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => HostLinkMessage.ParseResponse(new Byte[3]));
        Assert.Throws<ArgumentException>(() => HostLinkMessage.ParseResponse(null));
    }

    [Fact(DisplayName = "ParseResponse：缺少 @ 起始符应抛出异常")]
    public void ParseResponse_MissingAtSign_ShouldThrow()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes("00RD0000*\r");
        Assert.Throws<InvalidOperationException>(() => HostLinkMessage.ParseResponse(bytes));
    }

    [Fact(DisplayName = "ParseResponse：缺少 * 结束符应抛出异常")]
    public void ParseResponse_MissingAsterisk_ShouldThrow()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes("@00RD000064\r");
        Assert.Throws<InvalidOperationException>(() => HostLinkMessage.ParseResponse(bytes));
    }

    #endregion

    #region 错误码映射

    [Theory(DisplayName = "错误码映射：常见错误码返回正确描述")]
    [InlineData(0x00, "正常")]
    [InlineData(0x01, "不在RUN模式")]
    [InlineData(0x02, "不在MONITOR模式")]
    [InlineData(0x04, "地址超出范围")]
    [InlineData(0x0B, "不在PROGRAM模式")]
    [InlineData(0x13, "FCS错误")]
    [InlineData(0x14, "格式错误")]
    [InlineData(0x15, "入口号数据错误")]
    [InlineData(0x16, "命令不支持")]
    [InlineData(0x18, "帧长度错误")]
    [InlineData(0x19, "不可执行")]
    [InlineData(0x20, "不能读取")]
    [InlineData(0x21, "不能写入")]
    public void GetErrorMessage_KnownCodes(Byte endCode, String expectedPrefix)
    {
        var msg = new HostLinkMessage { EndCode = endCode };
        var error = msg.GetErrorMessage();

        Assert.Contains(expectedPrefix, error);
    }

    [Fact(DisplayName = "错误码：未知错误码返回十六进制描述")]
    public void GetErrorMessage_UnknownCode()
    {
        var msg = new HostLinkMessage { EndCode = 0xFF };
        var error = msg.GetErrorMessage();

        Assert.Contains("FF", error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 辅助方法

    [Fact(DisplayName = "BytesToHex：字节数组转十六进制字符串")]
    public void BytesToHex_ConvertsCorrectly()
    {
        var bytes = new Byte[] { 0x00, 0x0A, 0xAB, 0xFF };
        var hex = HostLinkMessage.BytesToHex(bytes);

        Assert.Equal("000AABFF", hex);
    }

    [Fact(DisplayName = "HexToBytes：十六进制字符串转字节数组")]
    public void HexToBytes_ConvertsCorrectly()
    {
        var bytes = HostLinkMessage.HexToBytes("000AABFF");

        Assert.Equal(4, bytes.Length);
        Assert.Equal(0x00, bytes[0]);
        Assert.Equal(0x0A, bytes[1]);
        Assert.Equal(0xAB, bytes[2]);
        Assert.Equal(0xFF, bytes[3]);
    }

    [Fact(DisplayName = "HexToBytes：空字符串返回空数组")]
    public void HexToBytes_EmptyString_ReturnsEmpty()
    {
        var bytes = HostLinkMessage.HexToBytes("");
        Assert.Empty(bytes);

        var bytes2 = HostLinkMessage.HexToBytes(null);
        Assert.Empty(bytes2);
    }

    [Fact(DisplayName = "BytesToHex 和 HexToBytes 往返一致性")]
    public void BytesHexRoundTrip()
    {
        var original = new Byte[] { 0x12, 0x34, 0x56, 0x78, 0xAB, 0xCD, 0xEF };
        var hex = HostLinkMessage.BytesToHex(original);
        var restored = HostLinkMessage.HexToBytes(hex);

        Assert.Equal(original, restored);
    }

    [Fact(DisplayName = "ToString 返回不含 FCS 的帧描述")]
    public void ToString_ReturnsReadableDescription()
    {
        var msg = new HostLinkMessage(0, "RD", "00000001");
        var str = msg.ToString();

        Assert.Contains("RD", str);
        Assert.Contains("00000001", str);
    }

    #endregion
}
