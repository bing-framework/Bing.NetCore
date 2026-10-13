using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Sql Server方言
/// </summary>
public sealed class SqlServerDialect : DialectBase
{
    /// <summary>
    /// 初始化一个 <see cref="SqlServerDialect"/> 类型的实例。
    /// </summary>
    private SqlServerDialect() { }

    /// <summary>
    /// 获取Sql Server方言实例
    /// </summary>
    public static SqlServerDialect Instance { get; } = new();
}
