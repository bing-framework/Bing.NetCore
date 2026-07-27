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
    /// <param name="table">调用方提供的表引用文本。</param>
    /// <param name="alias">显式指定的表别名；为 null 时可从表引用文本中解析。</param>
    /// <param name="schema">显式指定的架构名。</param>
    /// <returns>已验证并拆分名称、别名和架构的表引用。</returns>
    SqlTableName Parse(string table, string alias = null, string schema = null);
}