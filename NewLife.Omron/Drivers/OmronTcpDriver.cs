using System;
using System.Collections.Generic;
using System.Text;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Profinet.Omron;
using NewLife.IoT.Drivers;
using NewLife.IoT.Protocols;
using NewLife.IoT.ThingModels;
//using NewLife.Omron.Protocols;

namespace NewLife.Omron.Drivers
{
    public class OmronTcpDriver : ModbusTcpDriver
    {
        private OmronFinsNet omronFinsNet = null;

        private DataFormat dataFormat = DataFormat.DCBA;

        public OmronTcpDriver()
        {
            omronFinsNet = new OmronFinsNet();
            omronFinsNet.ConnectTimeOut = 2000;
        }

        public override INode Open(IChannel channel, IDictionary<String, Object> parameters)
        {
            var address = parameters["Address"] as String;
            if (address.IsNullOrEmpty()) throw new ArgumentException("参数中未指定地址address");

            var i = address.IndexOf(':');
            if (i < 0) throw new ArgumentException($"参数中地址address格式错误:{address}");


            omronFinsNet.IpAddress = address[..i];
            omronFinsNet.Port =  address[(i+1)..].ToInt();
            omronFinsNet.DA2 = 0;
            omronFinsNet.ByteTransform.DataFormat = dataFormat;

            var connect = omronFinsNet.ConnectServer();

            //var node = base.Open(channel, parameters);

            //var data = new ushort[initData.Length/2];
            //Buffer.BlockCopy(initData, 0, data, 0, initData.Length);

            //_modbus.Write(FunctionCodes.WriteCoils, 1, 1, data);

            if (!connect.IsSuccess) throw new Exception($"连接失败：{connect.Message}");

            return (INode)null;
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="node">节点对象，可存储站号等信息，仅驱动自己识别</param>
        /// <param name="point">点位，Address属性地址示例：D100、C100、W100、H100</param>
        /// <param name="value">数据</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public override Object Write(INode node, IPoint point, Object value)
        {
            var addr = point.Address; // "D100";
            OperateResult res = null;
            if (value is Int32 v1)
            {
                res = omronFinsNet.Write(addr, v1);
            }
            else if (value is String v2)
            {
                res = omronFinsNet.Write(addr, v2);
            }
            else if (value is Boolean v3)
            {
                res = omronFinsNet.Write(addr, v3);
            }
            else if (value is Byte[] v4)
            {
                res = omronFinsNet.Write(addr, v4);
            }
            else
                throw new ArgumentException("暂不支持写入该类型数据！");

            return (object)res;
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="node">节点对象，可存储站号等信息，仅驱动自己识别</param>
        /// <param name="points">点位集合，Address属性地址示例：D100、C100、W100、H100</param>
        /// <returns></returns>
        public override IDictionary<String, Object> Read(INode node, IPoint[] points)
        {
            var dic = new Dictionary<String, Object>();

            if (points == null || points.Length < 1) return dic;

            foreach (var point in points)
            {
                var name = point.Name;
                var addr = point.Address;
                var length = point.Length;
                var data = omronFinsNet.Read(addr, (ushort)(length / 2));
                dic[name] = data.Content;
            }

            return dic;
        }
    }
}
