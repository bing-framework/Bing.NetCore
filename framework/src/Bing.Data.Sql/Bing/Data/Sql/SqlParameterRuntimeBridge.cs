namespace Bing.Data.Sql;

/// <summary>
/// SQL 参数运行时协作入口。
/// </summary>
internal static class SqlParameterRuntimeBridge
{
    /// <summary>
    /// 创建默认 SQL 参数解析器。
    /// </summary>
    /// <returns>默认参数解析器的公开抽象。</returns>
    public static ISqlParameterResolver CreateDefaultResolver() => new DefaultSqlParameterResolver();
}