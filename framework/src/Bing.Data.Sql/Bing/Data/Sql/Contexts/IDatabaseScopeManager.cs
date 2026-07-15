namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文作用域管理器
/// </summary>
public interface IDatabaseScopeManager
{
    /// <summary>
    /// 使用指定数据库上下文
    /// </summary>
    /// <param name="dbKey">数据库标识</param>
    /// <returns>数据库上下文作用域</returns>
    IDatabaseScope Use(string dbKey);

    /// <summary>
    /// 使用指定数据库上下文
    /// </summary>
    /// <param name="options">数据库上下文作用域选项</param>
    /// <returns>数据库上下文作用域</returns>
    IDatabaseScope Use(DatabaseScopeOptions options);

}
