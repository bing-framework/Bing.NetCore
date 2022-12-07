using Bing.Data.Sql.Builders.Core;

namespace Bing.Datas.Dapper.MySql;

/// <summary>
/// MySql方言
/// </summary>
public class MySqlDialect : DialectBase
{
    /// <summary>
    /// 起始转义标识符
    /// </summary>
    public override char OpeningIdentifier => '`';

    /// <summary>
    /// 结束转义标识符
    /// </summary>
    public override char ClosingIdentifier => '`';
}