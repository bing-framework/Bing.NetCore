namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据源解析器
/// </summary>
public interface ISqlDataSourceResolver
{
    /// <summary>
    /// 按显式数据库标识、作用域选项和默认数据源的优先级解析 SQL 数据源。
    /// </summary>
    /// <param name="dbKey">优先于作用域选项的数据库标识；可以为 <c>null</c>。</param>
    /// <param name="options">提供数据库标识和读取偏好的作用域选项；可以为 <c>null</c>。</param>
    /// <returns>不会影响原始配置的数据源描述副本。</returns>
    /// <exception cref="InvalidOperationException">指定数据源和默认数据源均无法解析时引发。</exception>
    SqlDataSourceDescriptor Resolve(string dbKey = null, DatabaseScopeOptions options = null);
}