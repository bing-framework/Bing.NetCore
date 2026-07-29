namespace Bing.Data.Sql;

/// <summary>
/// 创建多结果集查询执行器的工厂。
/// </summary>
public interface ISqlMultipleQueryExecutorFactory
{
    /// <summary>
    /// 为指定数据源创建多结果集查询执行器。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>独立的多结果集查询执行器。</returns>
    ISqlMultipleQueryExecutor Create(string dbKey);

    /// <summary>
    /// 为当前数据库上下文创建多结果集查询执行器。
    /// </summary>
    /// <returns>独立的多结果集查询执行器。</returns>
    ISqlMultipleQueryExecutor Create();
}