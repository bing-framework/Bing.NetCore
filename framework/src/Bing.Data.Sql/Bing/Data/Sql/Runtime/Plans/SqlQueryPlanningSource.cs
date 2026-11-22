using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// 查询派生所需的 Builder 规划源。
/// </summary>
/// <remarks>
/// 查询计划只保存该封装，不直接暴露可变 Builder。每次派生 Count/Page 时返回新的 Builder 副本，
/// 避免一次派生修改后影响后续派生或并行读取。
/// </remarks>
internal sealed class SqlQueryPlanningSource
{
    private readonly ISqlBuilder _builder;
    private readonly SqlParam[] _mutableParameters;

    /// <summary>
    /// 初始化规划源。
    /// </summary>
    /// <param name="builder">查询的独立 Builder 来源。</param>
    /// <param name="snapshot">查询冻结时的 SQL 和参数。</param>
    internal SqlQueryPlanningSource(ISqlBuilder builder, SqlBuilderExecutionSnapshot snapshot)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _mutableParameters = (snapshot ?? throw new ArgumentNullException(nameof(snapshot))).CreateMutableParameterSnapshot();
    }

    /// <summary>
    /// 创建用于一次派生规划的独立 Builder。
    /// </summary>
    /// <returns>不与规划源共享可变状态的 Builder 副本。</returns>
    internal ISqlBuilder CreateBuilder()
    {
        var builder = _builder.Clone();
        if (builder is not ISqlCommonPartAccessor accessor)
            return builder;
        var manager = accessor.ParameterManager;
        foreach (var source in _mutableParameters)
            manager.Restore(SqlParameterSnapshot.CloneSqlParameter(source));
        return builder;
    }
}
