using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Sql Server方言
/// </summary>
public class SqlServerDialect : DialectBase
{
    /// <summary>
    /// 封闭构造函数
    /// </summary>
    private SqlServerDialect() { }

    /// <summary>
    /// Sql Server方言实例
    /// </summary>
    public static IDialect Instance => new SqlServerDialect();
}
