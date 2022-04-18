using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using NewLife.Configuration;

namespace NewLife.Omron.Config
{
    /// <summary>
    /// 欧姆龙PLC配置
    /// </summary>
    [Config("Omron","xml")]
    [DisplayName("欧姆龙PLC配置")]
    public class OmronConfig : Config<OmronConfig>
    {
        /// <summary>
        /// 授权码
        /// </summary>
        [Description("授权码")]
        public String AuthorizationCode { get; set; }

        /// <summary>
        /// 授权失败提示语
        /// </summary>
        [Description("授权失败提示语")]
        public String Description { get; set; }

    }
}
