namespace Bing.Data.Sql;

/// <summary>首次渲染确定的 Select 查询边界，不引用 Builder、Clause 或运行时服务。</summary>
internal sealed class SqlQuerySelectShape
{
    private static readonly string CountPrefix = "Select Count(*) " + Environment.NewLine;
    private static readonly string WrappedCountPrefix = CountPrefix + "From (";

    internal SqlQuerySelectShape(int queryStart, int fromStart, int sourceEnd, int queryEnd, bool wrapCount,
        string countAlias, string offsetParameter, string limitParameter, bool hasPage, string pageOrder,
        bool hasOrder = false, SqlQueryOrder order = null, int orderEnd = 0)
    {
        QueryStart = queryStart;
        FromStart = fromStart;
        SourceEnd = sourceEnd;
        QueryEnd = queryEnd;
        WrapCount = wrapCount;
        CountAlias = countAlias;
        OffsetParameter = offsetParameter;
        LimitParameter = limitParameter;
        HasPage = hasPage;
        PageOrder = pageOrder;
        HasOrder = hasPage || hasOrder;
        Order = order;
        OrderEnd = orderEnd;
    }

    /// <summary>主 Select 起点；此前的 CTE 声明始终留在最外层。</summary>
    internal int QueryStart { get; }
    internal int FromStart { get; }
    internal int SourceEnd { get; }
    internal int QueryEnd { get; }
    internal bool WrapCount { get; }
    internal string CountAlias { get; }
    internal string OffsetParameter { get; }
    internal string LimitParameter { get; }
    internal bool HasPage { get; }
    internal string PageOrder { get; }
    internal bool HasOrder { get; }
    internal SqlQueryOrder Order { get; }
    internal int OrderEnd { get; }

    /// <summary>供参数重命名扫描一次性调整的文本边界。</summary>
    internal int[] GetBoundaries() => new[] { QueryStart, FromStart, SourceEnd, QueryEnd, OrderEnd };

    /// <summary>沿用查询结构，只替换文本位置及已经重命名的外层参数身份。</summary>
    internal SqlQuerySelectShape WithScope(int[] boundaries, IReadOnlyDictionary<string, string> names) =>
        new(boundaries[0], boundaries[1], boundaries[2], boundaries[3], WrapCount, CountAlias,
            BindingName(OffsetParameter, names), BindingName(LimitParameter, names), HasPage,
            PageOrder, HasOrder, Order?.WithScope(names), boundaries[4]);

    private static string BindingName(string name, IReadOnlyDictionary<string, string> names)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        // 片段的公开参数使用无前缀名称；SQL 占位符仍由分页描述保存方言前缀。
        var token = "@" + name.TrimStart('@', ':', '?');
        return names.TryGetValue(token, out var renamed) ? renamed.Substring(1) : name;
    }

    /// <summary>沿用已有排序时，复用或追加分页均不需要重新解析排序列。</summary>
    internal bool CanReusePage(string order) => HasOrder &&
        (string.IsNullOrWhiteSpace(order) ||
         PageOrder != null && string.Equals(PageOrder, order.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>新增分页只改变 SQL 末端与外层分页参数边界。</summary>
    internal SqlQuerySelectShape WithPage(int queryEnd, string offsetParameter, string limitParameter,
        SqlQueryOrder order, int orderEnd) =>
        new(QueryStart, FromStart, SourceEnd, queryEnd, WrapCount, CountAlias, offsetParameter, limitParameter, true,
            ReferenceEquals(order, Order) ? PageOrder : null, true, order, orderEnd);

    /// <summary>按渲染边界移除外层排序和分页，保留嵌套查询自己的结构。</summary>
    internal SqlBuilderExecutionSnapshot DeriveCount(SqlBuilderExecutionSnapshot snapshot)
    {
        var sql = WrapCount
            ? WrappedCountPrefix + snapshot.Sql.Substring(QueryStart, SourceEnd - QueryStart) + ")" + CountAlias
            : CountPrefix + snapshot.Sql.Substring(FromStart, SourceEnd - FromStart);
        if (QueryStart != 0)
            sql = snapshot.Sql.Substring(0, QueryStart) + sql;
        return snapshot.WithoutPage(sql, OffsetParameter, LimitParameter);
    }
}
