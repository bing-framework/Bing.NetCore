using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 解析 Fluent 操作源实际持有的查询子句访问器。
/// </summary>
/// <remarks>
/// 独立查询描述仅在程序集内部通过 <see cref="ISqlQueryBuilderAccessor"/> 暴露其 Builder；普通 Builder
/// 仍直接使用公开的 <see cref="ISqlQueryClauseAccessor"/> SPI。
/// </remarks>
internal static class SqlQueryOperationAccessor
{
    /// <summary>
    /// 获取操作源实际使用的 SQL Builder。
    /// </summary>
    /// <param name="source">Fluent 操作源。</param>
    /// <returns>独立查询描述的内部 Builder，或传入的 Builder。</returns>
    internal static ISqlBuilder GetBuilder(object source) => source is ISqlQueryBuilderAccessor accessor
        ? accessor.GetSqlBuilder()
        : source as ISqlBuilder;

    /// <summary>
    /// 获取操作源实际使用的查询子句访问器。
    /// </summary>
    /// <param name="source">Fluent 操作源。</param>
    /// <returns>独立查询描述专属 Builder 或原始 Builder 的 Clause 访问器。</returns>
    internal static ISqlQueryClauseAccessor GetClauseAccessor(object source)
    {
        var accessor = GetBuilder(source) as ISqlQueryClauseAccessor ?? source as ISqlQueryClauseAccessor;
        if (accessor != null)
            return accessor;
        throw new InvalidOperationException(
            $"Fluent 操作源 '{source?.GetType().FullName}' 必须实现 {nameof(ISqlQueryClauseAccessor)} 或使用 {nameof(ISqlBuilder)}。");
    }
}