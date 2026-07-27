using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 字符串表引用解析器。
/// </summary>
public interface ISqlTableReferenceParser
{
    /// <summary>
    /// 解析表名、别名和可选架构。
    /// </summary>
    SqlTableName Parse(string table, string alias = null, string schema = null);
}