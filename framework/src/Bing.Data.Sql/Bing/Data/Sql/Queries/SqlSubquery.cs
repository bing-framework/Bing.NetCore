using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 由严格 DTO 投影冻结的类型化派生表。
/// </summary>
/// <typeparam name="TProjection">派生表公开的投影类型。</typeparam>
public sealed class SqlSubquery<TProjection> where TProjection : class
{
    /// <summary>
    /// 初始化类型化派生表。
    /// </summary>
    /// <param name="builder">已冻结投影和参数的独立 SQL Builder。</param>
    /// <param name="alias">派生表别名。</param>
    /// <param name="projectedMembers">允许由外层 Lambda 引用的 DTO 成员名称。</param>
    internal SqlSubquery(ISqlBuilder builder, string alias, IReadOnlyCollection<string> projectedMembers)
    {
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("派生表别名不能为空。", nameof(alias));
        Alias = alias;
        ProjectedMembers = projectedMembers ?? throw new ArgumentNullException(nameof(projectedMembers));
    }

    /// <summary>
    /// 派生表在外层查询中的别名。
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// 已冻结的派生查询 Builder。
    /// </summary>
    internal ISqlBuilder Builder { get; }

    /// <summary>
    /// 允许由外层 Lambda 引用的 DTO 成员名称。
    /// </summary>
    internal IReadOnlyCollection<string> ProjectedMembers { get; }
}
