using System.ComponentModel;

namespace Bing.Tests.Samples;

/// <summary>
/// 枚举测试样例
/// </summary>
public enum EnumSample
{
    /// <summary>
    /// 表示枚举值 A。
    /// </summary>
    A = 1,
    /// <summary>
    /// 表示枚举值 B。
    /// </summary>
    [Description("B2")]
    B = 2,
    /// <summary>
    /// 表示枚举值 C。
    /// </summary>
    [Description("C3")]
    C = 3,
    /// <summary>
    /// 表示枚举值 D。
    /// </summary>
    [Description("D4")]
    D = 4,
    /// <summary>
    /// 表示枚举值 E。
    /// </summary>
    [Description("E5")]
    E = 5
}
