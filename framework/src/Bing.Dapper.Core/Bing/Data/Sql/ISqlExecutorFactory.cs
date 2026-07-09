using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 执行器工厂
/// </summary>
public interface ISqlExecutorFactory
{
    /// <summary>
    /// 创建 Sql 执行器
    /// </summary>
    /// <typeparam name="TExecutor">Sql 执行器类型</typeparam>
    /// <param name="dbKey">数据库键</param>
    /// <param name="databaseType">数据库类型</param>
    /// <param name="role">数据库角色</param>
    /// <returns>Sql 执行器</returns>
    TExecutor Create<TExecutor>(string dbKey, DatabaseType databaseType, DatabaseRole role = DatabaseRole.Default)
        where TExecutor : class, ISqlExecutor;

    /// <summary>
    /// 基于当前数据库上下文创建 Sql 执行器
    /// </summary>
    /// <typeparam name="TExecutor">Sql 执行器类型</typeparam>
    /// <returns>Sql 执行器</returns>
    TExecutor Create<TExecutor>() where TExecutor : class, ISqlExecutor;
}