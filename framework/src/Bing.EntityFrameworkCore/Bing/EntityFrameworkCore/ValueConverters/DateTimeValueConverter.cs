using Bing.Helpers;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bing.EntityFrameworkCore.ValueConverters;

/// <summary>
/// 日期值转化器，将日期转换为标准化日期
/// </summary>
public class DateTimeValueConverter : ValueConverter<DateTime?, DateTime?>
{
    /// <summary>
    /// 初始化一个<see cref="DateTimeValueConverter"/>类型的实例
    /// </summary>
    public DateTimeValueConverter() : base(date => Normalize(date), date => ToLocalTime(date))
    {
    }

    /// <summary>
    /// 转换为标准化日期
    /// </summary>
    /// <param name="date">本地化日期</param>
    /// <returns>标准化后的日期；输入为空时返回 null。</returns>
    public static DateTime? Normalize(DateTime? date)
    {
        return date.HasValue ? Time.Normalize(date.Value) : null;
    }

    /// <summary>
    /// 转换为本地化日期
    /// </summary>
    /// <param name="date">标准化日期</param>
    /// <returns>本地化后的日期；输入为空时返回 null。</returns>
    public static DateTime? ToLocalTime(DateTime? date) => date.HasValue ? Time.UtcToLocalTime(date.Value) : null;
}
