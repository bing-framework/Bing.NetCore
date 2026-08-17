using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;

namespace Bing.Data.Sql.Builders.Filters;

/// <summary>
/// 逻辑删除过滤器
/// </summary>
public class IsDeletedFilter : ISqlFilter, ISqlDataBoundaryContributor
{
    /// <summary>
    /// 过滤
    /// </summary>
    /// <param name="context">Sql查询执行上下文</param>
    public void Filter(SqlFilterContext context)
    {
        if (context == null || context.IsEnabled<ISoftDelete>() == false)
            return;
        foreach (var source in context.Sources)
            Filter(context, source);
    }

    /// <summary>
    /// 过滤
    /// </summary>
    /// <param name="context">Sql查询执行上下文</param>
    /// <param name="source">当前查询图中的结构化来源。</param>
    private void Filter(SqlFilterContext context, SqlFilterSource source)
    {
        if (source?.EntityType == null || typeof(ISoftDelete).IsAssignableFrom(source.EntityType) == false)
            return;
        context.AddPredicate(source, context.GetColumn(source, nameof(ISoftDelete.IsDeleted)), false);
    }

    /// <inheritdoc />
    public bool ShouldApply(SqlDataBoundaryContext context) => context?.EntityType != null &&
        typeof(ISoftDelete).IsAssignableFrom(context.EntityType) && context.IsEnabled<ISoftDelete>() &&
        context.Operation != SqlDataBoundaryOperation.Purge;

    /// <inheritdoc />
    public void Apply(SqlDataBoundaryContext context)
    {
        if (ShouldApply(context))
            context.AddEquals(nameof(ISoftDelete.IsDeleted), context.Operation == SqlDataBoundaryOperation.Restore);
    }
}
