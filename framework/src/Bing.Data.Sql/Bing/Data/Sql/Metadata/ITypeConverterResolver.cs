using Bing.Data.Enums;
using Bing.Data.Metadata;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 数据库类型转换器解析器
/// </summary>
public interface ITypeConverterResolver
{
    /// <summary>
    /// 解析数据库类型转换器
    /// </summary>
    /// <param name="databaseType">数据库类型</param>
    /// <returns>数据类型转换器</returns>
    ITypeConverter Resolve(DatabaseType databaseType);
}