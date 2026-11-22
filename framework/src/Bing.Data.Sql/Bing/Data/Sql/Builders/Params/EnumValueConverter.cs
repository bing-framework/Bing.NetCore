using System.Globalization;

namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 在不截断数值的前提下构造枚举值。
/// </summary>
internal static class EnumValueConverter
{
    /// <summary>
    /// 将整数型 Provider 值按枚举底层类型的范围转换为枚举值。
    /// </summary>
    /// <param name="enumType">目标枚举类型。</param>
    /// <param name="value">Provider 返回的数值或枚举值。</param>
    /// <returns>转换后的枚举值；不要求数值对应已命名成员。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enumType"/> 或 <paramref name="value"/> 为 null。</exception>
    /// <exception cref="ArgumentException"><paramref name="enumType"/> 不是枚举类型。</exception>
    /// <exception cref="InvalidCastException"><paramref name="value"/> 不是整数型数值。</exception>
    /// <exception cref="OverflowException">数值超出枚举底层类型的范围。</exception>
    public static object ToEnumChecked(Type enumType, object value)
    {
        if (enumType == null)
            throw new ArgumentNullException(nameof(enumType));
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        if (!enumType.IsEnum)
            throw new ArgumentException("目标类型必须是枚举类型。", nameof(enumType));

        var numericValue = NormalizeIntegralValue(value);
        var underlyingType = Enum.GetUnderlyingType(enumType);
        var convertedValue = Convert.ChangeType(numericValue, underlyingType, CultureInfo.InvariantCulture);
        return Enum.ToObject(enumType, convertedValue);
    }

    private static object NormalizeIntegralValue(object value)
    {
        if (value is Enum enumValue)
            return Convert.ChangeType(enumValue, Enum.GetUnderlyingType(enumValue.GetType()), CultureInfo.InvariantCulture);

        return value switch
        {
            sbyte or byte or short or ushort or int or uint or long or ulong or char => value,
            decimal number when decimal.Truncate(number) == number => number,
            float number when !float.IsNaN(number) && !float.IsInfinity(number) && number % 1f == 0f => number,
            double number when !double.IsNaN(number) && !double.IsInfinity(number) && number % 1d == 0d => number,
            decimal or float or double => throw new InvalidCastException("枚举 Provider 值必须是整数。"),
            _ => throw new InvalidCastException($"类型 {value.GetType()} 不能转换为枚举值。")
        };
    }
}
