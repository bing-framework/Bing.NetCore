using Bing.Data;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据库上下文解析器
/// </summary>
public interface ISqlDatabaseContextResolver
{
    /// <summary>
    /// 按调用级配置、当前上下文和默认配置的优先级解析当前 SQL 使用的数据库上下文。
    /// </summary>
    /// <param name="options">可提供调用级数据库上下文的 SQL 配置；可以为 <c>null</c>。</param>
    /// <returns>可安全供调用方使用的数据库上下文副本；没有可用上下文时返回 <c>null</c>。</returns>
    DatabaseContext Resolve(SqlOptions options = null);
}
