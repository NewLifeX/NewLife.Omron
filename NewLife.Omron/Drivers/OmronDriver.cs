using HslCommunication.Core;
using HslCommunication.Profinet.Omron;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;
using NewLife.Log;

namespace NewLife.Omron.Drivers
{
    /// <summary>
    /// 欧姆龙PLC驱动
    /// </summary>
    [Driver("OmronPLC")]
    public class OmronDriver : IDriver
    {
        private OmronFinsNet _omronFinsNet;

        private readonly DataFormat dataFormat = DataFormat.DCBA;

        /// <summary>
        /// 打开通道。一个ModbusTcp设备可能分为多个通道读取，需要共用Tcp连接，以不同节点区分
        /// </summary>
        /// <param name="channel">通道</param>
        /// <param name="parameters">参数</param>
        /// <returns></returns>
        public virtual INode Open(IChannel channel, IDictionary<String, Object> parameters)
        {
            var address = parameters["Address"] as String;
            if (address.IsNullOrEmpty()) throw new ArgumentException("参数中未指定地址address");

            var i = address.IndexOf(':');
            if (i < 0) throw new ArgumentException($"参数中地址address格式错误:{address}");

            var node = new OmronNode
            {
                Address = address,
                Channel = channel,
            };

            if (_omronFinsNet == null)
            {
                lock (this)
                {
                    if (_omronFinsNet == null)
                    {
                        _omronFinsNet = new OmronFinsNet
                        {
                            ConnectTimeOut = 2000,

                            IpAddress = address[..i],
                            Port = address[(i + 1)..].ToInt(),
                            DA2 = 0
                        };
                        _omronFinsNet.ByteTransform.DataFormat = dataFormat;

                        var connect = _omronFinsNet.ConnectServer();

                        //var node = base.Open(channel, parameters);

                        //var data = new ushort[initData.Length/2];
                        //Buffer.BlockCopy(initData, 0, data, 0, initData.Length);

                        //_modbus.Write(FunctionCodes.WriteCoils, 1, 1, data);

                        if (!connect.IsSuccess) throw new Exception($"连接失败：{connect.Message}");
                    }
                }
            }

            return node;
        }

        /// <summary>
        /// 关闭设备驱动
        /// </summary>
        /// <param name="node"></param>
        public void Close(INode node)
        {
            _omronFinsNet.TryDispose();
            _omronFinsNet = null;
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="node">节点对象，可存储站号等信息，仅驱动自己识别</param>
        /// <param name="points">点位集合，Address属性地址示例：D100、C100、W100、H100</param>
        /// <returns></returns>
        public virtual IDictionary<String, Object> Read(INode node, IPoint[] points)
        {
            var dic = new Dictionary<String, Object>();

            if (points == null || points.Length == 0) return dic;

            foreach (var point in points)
            {
                var name = point.Name;
                var addr = point.Address;
                var length = point.Length;
                var data = _omronFinsNet.Read(addr, (UInt16)(length / 2));
                dic[name] = data.Content;
            }

            return dic;
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="node">节点对象，可存储站号等信息，仅驱动自己识别</param>
        /// <param name="point">点位，Address属性地址示例：D100、C100、W100、H100</param>
        /// <param name="value">数据</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public virtual Object Write(INode node, IPoint point, Object value)
        {
            var addr = point.Address; // "D100";
            var res = value switch
            {
                Int32 v1 => _omronFinsNet.Write(addr, v1),
                String v2 => _omronFinsNet.Write(addr, v2),
                Boolean v3 => _omronFinsNet.Write(addr, v3),
                Byte[] v4 => _omronFinsNet.Write(addr, v4),
                _ => throw new ArgumentException("暂不支持写入该类型数据！"),
            };
            return res;
        }

        /// <summary>
        /// 控制设备，特殊功能使用
        /// </summary>
        /// <param name="node"></param>
        /// <param name="parameters"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void Control(INode node, IDictionary<String, Object> parameters) => throw new NotImplementedException();

        #region 日志
        /// <summary>链路追踪</summary>
        public ITracer Tracer { get; set; }

        /// <summary>日志</summary>
        public ILog Log { get; set; }

        /// <summary>写日志</summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
        #endregion
    }
}
