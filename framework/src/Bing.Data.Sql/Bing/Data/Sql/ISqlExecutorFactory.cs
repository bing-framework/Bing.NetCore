namespace Bing.Data.Sql;

/// <summary>
/// Sql 执行器工厂
/// </summary>
public interface ISqlExecutorFactory
{
    /// <summary>
    /// 创建 Sql 执行器
    /// </summary>
    /// <param name="dbKey">数据库键</param>
    /// <returns>Sql 执行器</returns>
    ISqlExecutor Create(string dbKey = null);
}