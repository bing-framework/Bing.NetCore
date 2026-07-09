using Bing.Data.Enums;
using Bing.Data.Metadata;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认数据库类型转换器解析器
/// </summary>
public sealed class DefaultTypeConverterResolver : ITypeConverterResolver
{
    /// <summary>
    /// 数据库类型转换器映射
    /// </summary>
    private readonly IReadOnlyDictionary<DatabaseType, ITypeConverter> _converters;

    /// <summary>
    /// 初始化一个<see cref="DefaultTypeConverterResolver"/>类型的实例
    /// </summary>
    /// <param name="registrations">数据库类型转换器注册项集合</param>
    public DefaultTypeConverterResolver(IEnumerable<DatabaseTypeConverterRegistration> registrations = null)
    {
        _converters = registrations?
            .Where(t => t?.Converter != null)
            .GroupBy(t => t.DatabaseType)
            .ToDictionary(t => t.Key, t => t.Last().Converter)
            ?? new Dictionary<DatabaseType, ITypeConverter>();
    }

    /// <summary>
    /// 解析数据库类型转换器
    /// </summary>
    /// <param name="databaseType">数据库类型</param>
    /// <returns>数据类型转换器</returns>
    public ITypeConverter Resolve(DatabaseType databaseType)
    {
        _converters.TryGetValue(databaseType, out var converter);
        return converter;
    }
}