namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 根据完整 Join 拓扑为来源级过滤谓词选择不会改变外连接保留语义的位置。
/// </summary>
internal static class SqlFilterPlacementPlanner
{
    /// <summary>
    /// 规划全部过滤谓词的最终放置位置。
    /// </summary>
    /// <param name="context">收集来源、拓扑和谓词贡献的过滤器上下文。</param>
    /// <returns>按谓词提交顺序排列的放置决定。</returns>
    /// <exception cref="NotSupportedException">需要预过滤派生表而当前渲染器尚不支持时抛出。</exception>
    public static IReadOnlyList<SqlFilterPlacement> Plan(SqlFilterContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        return context.GetPredicates().Select(predicate => Plan(context.Joins, predicate)).ToArray();
    }

    /// <summary>
    /// 为一个来源级谓词选择最终放置位置。
    /// </summary>
    /// <param name="joins">按 SQL 顺序排列的 Join 拓扑。</param>
    /// <param name="predicate">待放置谓词。</param>
    /// <returns>最终 Where 或特定 Join On 的放置决定。</returns>
    /// <exception cref="NotSupportedException">谓词必须通过派生表预过滤才能保留语义时抛出。</exception>
    private static SqlFilterPlacement Plan(IReadOnlyList<SqlFilterJoin> joins, SqlFilterPredicate predicate)
    {
        foreach (var join in joins)
        {
            if (join.Kind == SqlFilterJoinKind.Full && (join.RightSourceId == predicate.SourceId ||
                                                        join.LeftSourceIds.Contains(predicate.SourceId)))
                throw new NotSupportedException($"无法安全将全局过滤器应用到 Full Join 来源 {predicate.SourceId}。当前 Provider 渲染器不支持预过滤派生表。");

            if (join.Kind == SqlFilterJoinKind.Left && join.RightSourceId == predicate.SourceId)
                return new SqlFilterPlacement(predicate, join.RightSourceId);

            if (join.Kind == SqlFilterJoinKind.Inner && join.RightSourceId == predicate.SourceId)
                return new SqlFilterPlacement(predicate, join.RightSourceId);

            if (join.Kind == SqlFilterJoinKind.Right && join.LeftSourceIds.Contains(predicate.SourceId))
            {
                if (string.IsNullOrWhiteSpace(join.RightSourceId))
                    throw new NotSupportedException($"无法安全将全局过滤器应用到 Right Join 来源 {predicate.SourceId}。Join 右侧不是结构化来源。");
                return new SqlFilterPlacement(predicate, join.RightSourceId);
            }

            if (join.Kind == SqlFilterJoinKind.Right && join.RightSourceId == predicate.SourceId)
                throw new NotSupportedException($"无法安全将全局过滤器应用到 Right Join 保留来源 {predicate.SourceId}。当前 Provider 渲染器不支持预过滤派生表。");
        }
        return new SqlFilterPlacement(predicate);
    }
}
