using System.ComponentModel;

namespace Bing.Tests.Samples;

/// <summary>
/// 枚举测试样例
/// </summary>
public enum EnumSample
{
    /// <summary>
    /// 表示第一种样例枚举值。
    /// </summary>
    A = 1,
    /// <summary>
    /// 表示第二种样例枚举值。
    /// </summary>
    [Description("B2")]
    B = 2,
    /// <summary>
    /// 表示第三种样例枚举值。
    /// </summary>
    [Description("C3")]
    C = 3,
    /// <summary>
    /// 表示第四种样例枚举值。
    /// </summary>
    [Description("D4")]
    D = 4,
    /// <summary>
    /// 表示第五种样例枚举值。
    /// </summary>
    [Description("E5")]
    E = 5
}
