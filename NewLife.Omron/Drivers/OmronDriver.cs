using System.ComponentModel;
using NewLife.IoT;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Omron.Protocols;
using NewLife.Serialization;

namespace NewLife.Omron.Drivers;

/// <summary>
/// 欧姆龙PLC驱动
/// </summary>
[Driver("OmronPLC")]
[DisplayName("欧姆龙PLC")]
public class OmronDriver : DriverBase
{
    private FinsClient _finsClient;

    /// <summary>
    /// 打开通道数量
    /// </summary>
    private Int32 _nodes;

    #region 方法
    /// <summary>
    /// 创建驱动参数对象，可序列化成Xml/Json作为该协议的参数模板
    /// </summary>
    /// <returns></returns>
    protected override IDriverParameter OnCreateParameter() => new OmronParameter
    {
        Address = "127.0.0.1:9600",
        DA2 = 0,
        ByteOrder = "CDAB",
    };

    /// <summary>
    /// 从点位中解析地址
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public virtual String GetAddress(IPoint point)
    {
        if (point == null) throw new ArgumentException("点位信息不能为空！");

        // 去掉冒号后面的位域
        var addr = point.Address;
        var p = addr.IndexOfAny(new[] { ':', '.' });
        if (p > 0) addr = addr[..p];

        return addr;
    }

    /// <summary>
    /// 打开通道。一个Omron FINS设备可能分为多个通道读取,需要共用Tcp连接，以不同节点区分
    /// </summary>
    /// <param name="device">通道</param>
    /// <param name="parameter">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public override Task<INode> OpenAsync(IDevice device, IDriverParameter? parameter, CancellationToken cancellationToken = default)
    {
        var pm = parameter as OmronParameter;
        var address = pm?.Address;
        if (address.IsNullOrEmpty()) throw new ArgumentException("参数中未指定地址address");

        var p = address.IndexOfAny(new[] { ':', '.' });
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
                    _finsClient = new FinsClient
                    {
                        ConnectTimeOut = 2000,
                        IpAddress = address[..p],
                        Port = address[(p + 1)..].ToInt(),
                        DA2 = pm.DA2,
                    };

                    // 设置字节序
                    if (!pm.ByteOrder.IsNullOrEmpty() && Enum.TryParse<DataFormat>(pm.ByteOrder, out var format))
                        _finsClient.DataFormat = format;

                    // 连接服务器
                    _finsClient.Connect();
                }
            }
        }

        Interlocked.Increment(ref _nodes);

        return TaskEx.FromResult(node as INode);
    }

    /// <summary>
    /// 关闭设备驱动
    /// </summary>
    /// <param name="node"></param>
    /// <param name="cancellationToken">取消令牌</param>
    public override Task CloseAsync(INode node, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Decrement(ref _nodes) <= 0)
        {
            _finsClient?.Close();
            _finsClient.TryDispose();
            _finsClient = null;
        }

        return TaskEx.CompletedTask;
    }

    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="node">节点对象，可存储站号等信息，仅驱动自己识别</param>
    /// <param name="points">点位集合，Address属性地址示例：D100、C100、W100、H100</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public override Task<ReadResult> ReadAsync(INode node, IPoint[] points, CancellationToken cancellationToken = default)
    {
        if (points == null || points.Length == 0)
            return TaskEx.FromResult(ReadResult.Success([], []));

        var values = new Object?[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            var addr = GetAddress(points[i]);
            // FINS协议以字（2字节）为单位读取，需将字节数转换为字数
            var wordCount = (UInt16)((points[i].Length + 1) / 2);
            values[i] = _finsClient.Read(addr, wordCount);
        }

        return TaskEx.FromResult(ReadResult.Success(points, values));
    }

    /// <summary>
    /// 写入数据
    /// </summary>
    /// <param name="node">节点对象，可存储站号等信息，仅驱动自己识别</param>
    /// <param name="requests">写入请求数组，每项含目标点位和值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public override Task<WriteResult> WriteAsync(INode node, WriteRequest[] requests, CancellationToken cancellationToken = default)
    {
        foreach (var request in requests)
        {
            var point = request.Point;
            var value = request.Value;
            var addr = GetAddress(point);

            // 先做类型转换，不支持的类型直接抛出 ArgumentException（此处不依赖 _finsClient）
            var transform = _finsClient?.Transform ?? new ByteTransform();
            Byte[] data = value switch
            {
                Int32 v1 => transform.TransByte(v1),
                Int16 v2 => transform.TransByte(v2),
                UInt32 v3 => transform.TransByte(v3),
                UInt16 v4 => transform.TransByte(v4),
                Single v5 => transform.TransByte(v5),
                Double v6 => transform.TransByte(v6),
                String v7 => System.Text.Encoding.UTF8.GetBytes(v7), // UTF-8编码支持中文等多字节字符
                Boolean v8 => new Byte[] { (Byte)(v8 ? 1 : 0), 0x00 }, // FINS要求字对齐，Bool占1字节补充填充字节
                Byte[] v9 => v9,
                _ => throw new ArgumentException($"暂不支持写入该类型数据: {value?.GetType().Name}"),
            };

            _finsClient.Write(addr, data);
        }

        return TaskEx.FromResult(WriteResult.SuccessBatch(requests.Length));
    }
    #endregion
}