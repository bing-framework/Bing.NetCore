namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// SQL 子句的通用行为。
/// </summary>
public interface ISqlClause : ISqlContent
{
    /// <summary>
    /// 清空当前子句保存的全部可变状态。
    /// </summary>
    void Clear();
}
