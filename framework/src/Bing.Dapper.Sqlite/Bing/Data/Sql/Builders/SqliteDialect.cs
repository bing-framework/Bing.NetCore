using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Sqlite方言
/// </summary>
public sealed class SqliteDialect : DialectBase
{
    /// <summary>
    /// 封闭构造函数
    /// </summary>
    private SqliteDialect() { }

    /// <summary>
    /// Sqlite方言实例
    /// </summary>
    public static SqliteDialect Instance { get; } = new();

    /// <inheritdoc />
    public override char OpeningIdentifier => '`';

    /// <inheritdoc />
    public override char ClosingIdentifier => '`';
}
