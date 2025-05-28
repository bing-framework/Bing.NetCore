using Bing.Extensions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bing.EntityFrameworkCore.ValueConverters;

/// <summary>
/// 字符串值转换器，去除前后空格
/// </summary>
public class TrimStringValueConverter : ValueConverter<string, string>
{
    /// <summary>
    /// 初始化一个<see cref="TrimStringValueConverter"/>类型的实例
    /// </summary>
    public TrimStringValueConverter() : base(value => value.SafeString(), value => value)
    {
    }
}
