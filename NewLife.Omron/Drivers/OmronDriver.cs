using System.ComponentModel;
using NewLife.IoT;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Omron.Protocols;
using NewLife.Serialization;

namespace NewLife.Omron.Drivers;

/// <summary>欧姆龙PLC驱动</summary>
/// <remarks>
/// 基于 FINS/TCP 协议实现的欧姆龙PLC通信驱动。
/// 支持 DM/CIO/WR/HR/AR/EM/TIM/CNT 等存储区的读写操作。
/// </remarks>
[Driver("OmronPLC")]
[DisplayName("欧姆龙PLC")]
public class OmronDriver : DriverBase
{
    private FinsClient _finsClient;

    /// <summary>打开通道数量</summary>
    private Int32 _nodes;

    #region 方法

    /// <summary>创建驱动参数对象，可序列化成Xml/Json作为该协议的参数模板</summary>
    /// <param name="parameter">参数字符串</param>
    /// <returns>驱动参数对象</returns>
    public override IDriverParameter CreateParameter(String parameter) => new OmronParameter
    {
        Address = "127.0.0.1:9600",
        DA2 = 0,
        DataFormat = "CDAB",
    };

    /// <summary>从点位中解析地址</summary>
    /// <param name="point">点位信息</param>
    /// <returns>解析后的地址字符串</returns>
    public virtual String GetAddress(IPoint point)
    {
        if (point == null) throw new ArgumentException("点位信息不能为空！");

        // 去掉冒号后面的位域
        var addr = point.Address;
        var p = addr.IndexOf(':');
        if (p > 0) addr = addr[..p];

        return addr;
    }

    /// <summary>打开通道。一个Omron FINS设备可能分为多个通道读取,需要共用Tcp连接，以不同节点区分</summary>
    /// <param name="device">通道</param>
    /// <param name="parameter">参数</param>
    /// <returns>节点对象</returns>
    public override INode Open(IDevice device, IDriverParameter parameter)
    {
        var pm = parameter as OmronParameter;
        var address = pm?.Address;
        if (address.IsNullOrEmpty()) throw new ArgumentException("参数中未指定地址address");

        var p = address.IndexOfAny([':', '.']);
        if (p < 0) throw new ArgumentException($"参数中地址address格式错误:{address}");

        var node = new OmronNode
        {
            Address = address,

            Driver = this,
            Device = device,
            Parameter = pm,
        };

        if (_finsClient == null)
        {
            lock (this)
            {
                if (_finsClient == null)
                {
                    var client = new FinsClient
                    {
                        ConnectTimeOut = 2000,
                        IpAddress = address[..p],
                        Port = address[(p + 1)..].ToInt(),
                        DA2 = pm.DA2,
                    };

                    // 设置数据格式
                    if (!pm.DataFormat.IsNullOrEmpty() && Enum.TryParse<DataFormat>(pm.DataFormat, out var format))
                    {
                        client.DataFormat = format;
                    }

                    // 连接服务器
                    client.Connect();

                    _finsClient = client;
                }
            }
        }

        Interlocked.Increment(ref _nodes);

        return node;
    }

    /// <summary>关闭设备驱动</summary>
    /// <param name="node">节点对象</param>
    public override void Close(INode node)
    {
        if (Interlocked.Decrement(ref _nodes) <= 0)
        {
            _finsClient?.Close();
            _finsClient.TryDispose();
            _finsClient = null;
        }
    }

    /// <summary>读取数据</summary>
    /// <param name="node">节点对象，可存储站号等信息，仅驱动自己识别</param>
    /// <param name="points">点位集合，Address属性地址示例：D100、C100、W100、H100、TIM5、CNT3</param>
    /// <returns>读取结果字典</returns>
    public override IDictionary<String, Object> Read(INode node, IPoint[] points)
    {
        var dic = new Dictionary<String, Object>();

        if (points == null || points.Length == 0) return dic;

        foreach (var point in points)
        {
            var addr = GetAddress(point);
            var data = _finsClient.Read(addr, (UInt16)point.Length);

            dic[point.Name] = data;
        }

        return dic;
    }

    /// <summary>写入数据</summary>
    /// <param name="node">节点对象，可存储站号等信息，仅驱动自己识别</param>
    /// <param name="point">点位，Address属性地址示例：D100、C100、W100、H100、TIM5、CNT3</param>
    /// <param name="value">数据</param>
    /// <returns>写入结果</returns>
    public override Object Write(INode node, IPoint point, Object value)
    {
        var addr = GetAddress(point);
        var transform = _finsClient.Transform;

        Byte[] data = value switch
        {
            Int32 v1 => transform.TransByte(v1),
            Int16 v2 => transform.TransByte(v2),
            UInt32 v3 => transform.TransByte(v3),
            UInt16 v4 => transform.TransByte(v4),
            Single v5 => transform.TransByte(v5),
            Double v6 => transform.TransByte(v6),
            Int64 v7 => transform.TransByte(v7),
            UInt64 v8 => transform.TransByte(v8),
            String v9 => transform.TransByte(v9),
            Boolean v10 => [(Byte)(v10 ? 1 : 0), 0x00],
            Byte[] v11 => v11,
            _ => throw new ArgumentException($"暂不支持写入该类型数据: {value?.GetType().Name}"),
        };

        _finsClient.Write(addr, data);
        return true;
    }

    #endregion
}