using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Tests.Configs;

/// <summary>
/// 提供数据访问测试使用的固定字段值。
/// </summary>
public class TestConfig
{
    /// <summary>
    /// 获取用于测试的固定 GUID 标识。
    /// </summary>
    public static Guid Id { get; } = "08255312-DF8E-4F0C-AD4D-638B3A63D889".ToGuid();
    /// <summary>
    /// 获取用于测试的字符串值。
    /// </summary>
    public static string Value { get; } = "abc";
    /// <summary>
    /// 获取用于测试的整数值。
    /// </summary>
    public static int IntValue { get; } = 123;
    /// <summary>
    /// 获取用于测试的长整数值。
    /// </summary>
    public static long LongValue { get; } = 123456789;
    /// <summary>
    /// 获取用于测试的 GUID 值。
    /// </summary>
    public static Guid GuidValue { get; } = "04255312-DF8E-4F0C-AD4D-638B3A63D868".ToGuid();
    /// <summary>
    /// 获取用于测试的布尔值。
    /// </summary>
    public static bool BoolValue { get; } = true;
    /// <summary>
    /// 获取用于测试的单精度浮点值。
    /// </summary>
    public static float FloatValue { get; } = 123.45F;
    /// <summary>
    /// 获取用于测试的双精度浮点值。
    /// </summary>
    public static double DoubleValue { get; } = 12345.67;
    /// <summary>
    /// 获取用于测试的十进制值。
    /// </summary>
    public static decimal DecimalValue { get; } = 12345.67M;

    /// <summary>
    /// 获取用于测试的可空日期时间值。
    /// </summary>
    public static DateTime? DateTimeValue { get; } = Conv.ToDateOrNull("2012-12-12 12:12:12");
}
