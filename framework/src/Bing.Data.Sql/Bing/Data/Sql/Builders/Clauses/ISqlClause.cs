namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// SQL 子句的通用行为。
/// </summary>
public interface ISqlClause : ISqlContent
{
    /// <summary>
    /// 验证当前子句配置是否可参与 SQL 渲染。
    /// </summary>
    /// <returns>子句状态有效时返回 true；否则返回 false。</returns>
    bool Validate();
}
