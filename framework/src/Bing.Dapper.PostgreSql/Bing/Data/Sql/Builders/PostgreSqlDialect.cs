using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// PostgreSql方言
/// </summary>
public sealed class PostgreSqlDialect : DialectBase
{
    /// <summary>
    /// 初始化一个 <see cref="PostgreSqlDialect"/> 类型的实例。
    /// </summary>
    private PostgreSqlDialect() { }

    /// <summary>
    /// 获取PostgreSql方言实例
    /// </summary>
    public static PostgreSqlDialect Instance { get; } = new();

    /// <inheritdoc />
    public override char OpeningIdentifier => '"';

    /// <inheritdoc />
    public override char ClosingIdentifier => '"';
}
