using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// MySql方言
/// </summary>
public sealed class MySqlDialect : DialectBase
{
    /// <summary>
    /// 封闭构造函数
    /// </summary>
    private MySqlDialect() { }

    /// <summary>
    /// MySql方言实例
    /// </summary>
    public static MySqlDialect Instance { get; } = new();

    /// <inheritdoc />
    public override char OpeningIdentifier => '`';

    /// <inheritdoc />
    public override char ClosingIdentifier => '`';
}
