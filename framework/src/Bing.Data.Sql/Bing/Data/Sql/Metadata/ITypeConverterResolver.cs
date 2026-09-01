using Bing.Data.Enums;
using Bing.Data.Metadata;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 根据数据库类型解析数据类型转换器。
/// </summary>
public interface ITypeConverterResolver
{
    /// <summary>
    /// 解析数据库类型转换器。
    /// </summary>
    /// <param name="databaseType">要解析转换器的数据库类型。</param>
    /// <returns>对应的数据类型转换器。</returns>
    ITypeConverter Resolve(DatabaseType databaseType);
}