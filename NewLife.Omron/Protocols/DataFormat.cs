using System;

namespace NewLife.Omron.Protocols;

/// <summary>
/// 数据格式
/// </summary>
public enum DataFormat
{
    /// <summary>ABCD格式</summary>
    ABCD,

    /// <summary>BADC格式</summary>
    BADC,

    /// <summary>CDAB格式</summary>
    CDAB,

    /// <summary>DCBA格式</summary>
    DCBA
}
