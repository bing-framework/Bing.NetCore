using System.ComponentModel;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 为独立查询描述创建 SQL Builder 的运行时来源。
/// </summary>
/// <remarks>
/// 该契约仅用于将公开查询描述与具体执行实现解耦，创建结果不得复用根查询的可变 Builder 状态。
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlQueryBuilderSource
{
    /// <summary>
    /// 创建绑定当前数据源、Provider 和查询选项的独立 SQL Builder。
    /// </summary>
    /// <returns>不与根查询共享子句和参数状态的 SQL Builder。</returns>
    ISqlBuilder CreateIndependentSqlBuilder();
}