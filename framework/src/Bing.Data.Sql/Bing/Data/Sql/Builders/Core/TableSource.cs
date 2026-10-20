using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 查询图中的单个表源实例。
/// </summary>
/// <remarks>
/// 表源身份与实体类型分离，使同一实体的多个来源可以在后续的表达式作用域中被独立绑定。
/// </remarks>
public sealed class TableSource
{
    /// <summary>
    /// 初始化一个 <see cref="TableSource"/> 类型的实例。
    /// </summary>
    /// <param name="sourceId">查询图内稳定的来源标识。</param>
    /// <param name="item">延迟渲染的表项。</param>
    /// <param name="entityType">关联实体类型；非实体来源为 null。</param>
    /// <param name="alias">外层查询引用此来源的别名。</param>
    /// <param name="projectedMembers">派生表向外层公开的投影成员。</param>
    internal TableSource(string sourceId, SqlItem item, Type entityType = null, string alias = null,
        IReadOnlyCollection<string> projectedMembers = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("表源标识不能为空。", nameof(sourceId));
        SourceId = sourceId;
        Item = item ?? throw new ArgumentNullException(nameof(item));
        EntityType = entityType;
        Alias = alias;
        ProjectedMembers = projectedMembers;
    }

    /// <summary>
    /// 获取查询图内稳定的来源标识。
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// 获取用于最终 SQL 渲染的表项。
    /// </summary>
    internal SqlItem Item { get; }

    /// <summary>
    /// 获取关联实体类型。
    /// </summary>
    /// <remarks>
    /// 原始、命名和派生来源为 null。
    /// </remarks>
    public Type EntityType { get; }

    /// <summary>
    /// 获取外层查询引用此来源时使用的别名。
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// 获取派生表公开给外层 Lambda 的投影成员。
    /// </summary>
    /// <remarks>
    /// 普通实体来源为 null。
    /// </remarks>
    internal IReadOnlyCollection<string> ProjectedMembers { get; }

    /// <summary>
    /// 获取结构化表引用。
    /// </summary>
    /// <remarks>
    /// 非结构化来源为 null。
    /// </remarks>
    internal SqlTableReference Reference => (Item as StructuredSqlItem)?.Reference;

    /// <summary>
    /// 复制当前表源并保留稳定身份。
    /// </summary>
    /// <returns>当前表源的独立副本。</returns>
    internal TableSource Clone() => new(SourceId, Item.Clone(), EntityType, Alias, ProjectedMembers);
}