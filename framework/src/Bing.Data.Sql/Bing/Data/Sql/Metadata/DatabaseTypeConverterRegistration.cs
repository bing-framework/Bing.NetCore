using Bing.Data.Enums;
using Bing.Data.Metadata;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 数据库类型转换器注册项
/// </summary>
public sealed class DatabaseTypeConverterRegistration
{
    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 数据类型转换器
    /// </summary>
    public ITypeConverter Converter { get; set; }
}