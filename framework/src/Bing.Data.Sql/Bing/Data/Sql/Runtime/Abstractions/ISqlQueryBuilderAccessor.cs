using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 访问独立查询描述专属 SQL Builder 的内部契约。
/// </summary>
/// <remarks>
/// 仅供框架内部参数和子查询扩展使用，避免将 Builder 作为公开查询 API 的逃逸入口。
/// </remarks>
internal interface ISqlQueryBuilderAccessor
{
    /// <summary>
    /// 获取当前查询描述专属的 SQL Builder。
    /// </summary>
    /// <returns>当前查询描述的独立 SQL Builder。</returns>
    ISqlBuilder GetSqlBuilder();
}