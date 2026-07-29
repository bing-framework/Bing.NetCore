using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// 定义可使用新运行上下文创建独立副本的 SQL 子句。
/// </summary>
/// <typeparam name="TClause">克隆后的子句类型。</typeparam>
public interface ISqlClauseCloneable<out TClause>
{
    /// <summary>
    /// 使用重绑定后的运行上下文克隆当前子句。
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <returns>独立的子句副本。</returns>
    TClause Clone(SqlClauseContext context);
}
