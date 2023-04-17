using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// PostgreSql方言
/// </summary>
public class PostgreSqlDialect : DialectBase
{
    /// <summary>
    /// 封闭构造函数
    /// </summary>
    private PostgreSqlDialect() { }

    /// <summary>
    /// PostgreSql方言实例
    /// </summary>
    public static IDialect Instance => new PostgreSqlDialect();

    /// <inheritdoc />
    public override char OpeningIdentifier => '"';

    /// <inheritdoc />
    public override char ClosingIdentifier => '"';
}
