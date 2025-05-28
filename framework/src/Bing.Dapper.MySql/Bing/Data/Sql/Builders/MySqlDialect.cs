using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// MySql方言
/// </summary>
public class MySqlDialect : DialectBase
{
    /// <summary>
    /// 封闭构造函数
    /// </summary>
    private MySqlDialect() { }

    /// <summary>
    /// MySql方言实例
    /// </summary>
    public static IDialect Instance => new MySqlDialect();

    /// <inheritdoc />
    public override char OpeningIdentifier => '`';

    /// <inheritdoc />
    public override char ClosingIdentifier => '`';
}
