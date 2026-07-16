namespace Bing.Data.Sql;

/// <summary>
/// SQL 读取偏好作用域管理器。
/// </summary>
public interface IReadPreferenceScopeManager
{
    /// <summary>
    /// 在当前异步执行上下文中使用指定读取偏好。
    /// </summary>
    /// <param name="readPreference">读取偏好。</param>
    /// <returns>读取偏好作用域。</returns>
    IDatabaseScope Use(SqlReadPreference readPreference);
}