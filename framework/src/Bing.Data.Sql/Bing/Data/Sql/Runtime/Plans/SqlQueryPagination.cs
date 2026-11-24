using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>冻结分页绑定所需的方言、参数槽位与限额，不保留可变 Builder 或参数容器。</summary>
internal sealed class SqlQueryPagination
{
    /// <summary>作用域包含尚未绑定的分页槽位，保留其方言前缀。</summary>
    internal SqlQueryPagination WithScope(IReadOnlyDictionary<string, string> names) =>
        new(Dialect, OrderDialect, Renderer,
            names.TryGetValue(OffsetParameter, out var offset) ? offset : OffsetParameter,
            names.TryGetValue(LimitParameter, out var limit) ? limit : LimitParameter,
            MaximumParameterCount, LimitProvider);
    internal SqlQueryPagination(IDialect dialect, IDialect orderDialect, ISqlPaginationRenderer renderer, string offsetParameter,
        string limitParameter, int maximumParameterCount, string limitProvider)
    {
        Dialect = dialect;
        OrderDialect = orderDialect;
        Renderer = renderer;
        OffsetParameter = offsetParameter;
        LimitParameter = limitParameter;
        MaximumParameterCount = maximumParameterCount;
        LimitProvider = limitProvider;
    }

    internal IDialect Dialect { get; }
    internal IDialect OrderDialect { get; }
    internal ISqlPaginationRenderer Renderer { get; }
    internal string OffsetParameter { get; }
    internal string LimitParameter { get; }
    private int MaximumParameterCount { get; }
    private string LimitProvider { get; }

    /// <summary>在转换值之前验证缺失槽位，保留参数管理器的首次超限诊断。</summary>
    internal void ValidateAppend(int parameterCount, int addedCount)
    {
        if (MaximumParameterCount - parameterCount >= addedCount)
            return;
        var current = Math.Max(parameterCount, MaximumParameterCount);
        throw new InvalidOperationException(
            $"SQL Provider '{LimitProvider}' 的参数数量超出上限。当前参数数量: {current}；尝试添加后数量: {current + 1}；最大参数数量: {MaximumParameterCount}。");
    }
}
