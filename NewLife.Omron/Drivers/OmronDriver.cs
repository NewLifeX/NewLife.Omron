using System.ComponentModel;
using HslCommunication.Core;
using HslCommunication.Profinet.Omron;
using NewLife.IoT;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Omron.Config;
using NewLife.Serialization;

namespace NewLife.Omron.Drivers
{
    /// <summary>
    /// 欧姆龙PLC驱动
    /// </summary>
    [Driver("OmronPLC")]
    [DisplayName("欧姆龙PLC")]
    public class OmronDriver : DisposeBase, IDriver, ILogFeature, ITracerFeature
    {
        static OmronDriver()
        {
            var cfg = OmronConfig.Current;
            if (cfg.AuthorizationCode.IsNullOrWhiteSpace())
            {
                XTrace.WriteLine("欧姆龙PLC授权码为空！请到Config/OmronConfig中进行设置。");
            }
            else if (!HslCommunication.Authorization.SetAuthorizationCode(cfg.AuthorizationCode))
            {
                XTrace.WriteLine("欧姆龙PLC授权成功！");
            }
            else
            {
                XTrace.WriteLine("欧姆龙PLC授权失败！只能使用8个小时。");
            }
        }

        ///// <summary>
        ///// 数据顺序
        ///// </summary>
        //private readonly DataFormat dataFormat = DataFormat.CDAB;

        private OmronFinsNet _omronFinsNet;

        /// <summary>
        /// 打开通道数量
        /// </summary>
        private Int32 _nodes;

        #region 方法
        /// <summary>
        /// 创建驱动参数对象，可序列化成Xml/Json作为该协议的参数模板
        /// </summary>
        /// <returns></returns>
        public virtual IDriverParameter CreateParameter() => new OmronParameter
        {
            Address = "127.0.0.1:9600",
            DA2 = 0,
            DataFormat = "CDAB",
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
        /// 打开通道。一个ModbusTcp设备可能分为多个通道读取，需要共用Tcp连接，以不同节点区分
        /// </summary>
        /// <param name="device">通道</param>
        /// <param name="parameters">参数</param>
        /// <returns></returns>
        public virtual INode Open(IDevice device, IDictionary<String, Object> parameters)
        {
            var pm = JsonHelper.Convert<OmronParameter>(parameters);
            var address = pm.Address;
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

            if (_omronFinsNet == null)
            {
                lock (this)
                {
                    if (_omronFinsNet == null)
                    {
                        _omronFinsNet = new OmronFinsNet
                        {
                            ConnectTimeOut = 2000,

                            IpAddress = address[..p],
                            Port = address[(p + 1)..].ToInt(),
                            DA2 = pm.DA2,
                        };

                        if (!pm.DataFormat.IsNullOrEmpty() && Enum.TryParse(typeof(DataFormat), pm.DataFormat, out var format))
                        {
                            _omronFinsNet.ByteTransform.DataFormat = (DataFormat)format;
                        }

                        var connect = _omronFinsNet.ConnectServer();

                        if (!connect.IsSuccess) throw new Exception($"连接失败：{connect.Message}");
                    }
                }
            }

            Interlocked.Increment(ref _nodes);

            return node;
        }

        /// <summary>
        /// 关闭设备驱动
        /// </summary>
        /// <param name="node"></param>
        public void Close(INode node)
        {
            if (Interlocked.Decrement(ref _nodes) <= 0)
            {
                _omronFinsNet?.ConnectClose();
                _omronFinsNet.TryDispose();
                _omronFinsNet = null;
            }
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
                var addr = GetAddress(point);
                var data = _omronFinsNet.Read(addr, (UInt16)point.Length);
                if (!data.IsSuccess) throw new Exception($"读取数据失败：{data.ToJson()}");

                dic[point.Name] = data.Content;
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
            var addr = GetAddress(point);
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
        #endregion

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