using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// MySql方言
/// </summary>
public sealed class MySqlDialect : DialectBase
{
    /// <summary>
    /// 初始化一个 <see cref="MySqlDialect"/> 类型的实例。
    /// </summary>
    private MySqlDialect() { }

    /// <summary>
    /// 获取MySql方言实例
    /// </summary>
    public static MySqlDialect Instance { get; } = new();

    /// <inheritdoc />
    public override char OpeningIdentifier => '`';

    /// <inheritdoc />
    public override char ClosingIdentifier => '`';
}
