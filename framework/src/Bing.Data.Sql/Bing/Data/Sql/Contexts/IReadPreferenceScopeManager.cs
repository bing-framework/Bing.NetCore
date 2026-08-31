namespace Bing.Data.Sql;

/// <summary>
/// SQL 读取偏好作用域管理器。
/// </summary>
public interface IReadPreferenceScopeManager
{
    /// <summary>
    /// 在当前异步执行流中临时覆盖 SQL 读取偏好。
    /// </summary>
    /// <param name="readPreference">要在当前作用域中使用的读取偏好。</param>
    /// <returns>释放后恢复父上下文读取偏好的作用域；没有父上下文时会清除临时上下文。</returns>
    IDatabaseScope Use(SqlReadPreference readPreference);
}