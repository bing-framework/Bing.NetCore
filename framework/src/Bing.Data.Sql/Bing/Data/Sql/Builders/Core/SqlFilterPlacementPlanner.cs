namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 规划过滤谓词放置位置。
/// </summary>
internal static class SqlFilterPlacementPlanner
{
    /// <summary>
    /// 规划过滤谓词放置位置。
    /// </summary>
    /// <param name="context">收集来源、拓扑和谓词贡献的过滤器上下文。</param>
    /// <returns>按谓词提交顺序排列的放置决定。</returns>
    /// <exception cref="NotSupportedException">Right Join 的目标不是结构化来源时抛出。</exception>
    public static IReadOnlyList<SqlFilterPlacement> Plan(SqlFilterContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        return context.GetPredicates().Select(predicate => Plan(context.Joins, predicate)).ToArray();
    }

    /// <summary>
    /// 规划过滤谓词放置位置。
    /// </summary>
    /// <param name="joins">按 SQL 顺序排列的 Join 拓扑。</param>
    /// <param name="predicate">待放置谓词。</param>
    /// <returns>来源预过滤、最终 Where 或特定 Join On 的放置决定。</returns>
    /// <exception cref="NotSupportedException">Right Join 的目标不是结构化来源时抛出。</exception>
    private static SqlFilterPlacement Plan(IReadOnlyList<SqlFilterJoin> joins, SqlFilterPredicate predicate)
    {
        // 必须先检查完整拓扑，避免较早的 Left/Inner On 放置跳过后续 Full Join。
        if (joins.Any(join => join.Kind == SqlFilterJoinKind.Full &&
            (join.RightSourceId == predicate.SourceId || join.LeftSourceIds.Contains(predicate.SourceId))))
            return new SqlFilterPlacement(predicate, prefilterSource: true);

        foreach (var join in joins)
        {
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

            // Right Join 的右侧是保留输入：继续检查后续连接。
            // 后续 Right Join 可将它转为非保留输入；始终保留时则在最终 Where 过滤。
        }
        return new SqlFilterPlacement(predicate);
    }
}
