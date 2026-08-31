namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文作用域管理器
/// </summary>
public interface IDatabaseScopeManager
{
    /// <summary>
    /// 在当前异步执行流中进入指定数据库的数据上下文作用域。
    /// </summary>
    /// <param name="dbKey">要使用的数据库标识。</param>
    /// <returns>释放后恢复父数据库上下文的作用域。</returns>
    IDatabaseScope Use(string dbKey);

    /// <summary>
    /// 在当前异步执行流中按指定选项进入数据库上下文作用域。
    /// </summary>
    /// <param name="options">数据库标识和读取偏好等作用域选项；可以为 <c>null</c>。</param>
    /// <returns>释放后恢复父数据库上下文的作用域。</returns>
    IDatabaseScope Use(DatabaseScopeOptions options);
}
