using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Sqlite方言
/// </summary>
public sealed class SqliteDialect : DialectBase
{
    /// <summary>
    /// 初始化一个 <see cref="SqliteDialect"/> 类型的实例。
    /// </summary>
    private SqliteDialect() { }

    /// <summary>
    /// 获取Sqlite方言实例
    /// </summary>
    public static SqliteDialect Instance { get; } = new();

    /// <inheritdoc />
    public override char OpeningIdentifier => '`';

    /// <inheritdoc />
    public override char ClosingIdentifier => '`';
}
