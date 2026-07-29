namespace Bing.Data.Sql.Mutations;

/// <summary>
/// 更新实体时使用的列筛选和安全选项。
/// </summary>
public sealed class SqlUpdateOptions
{
    /// <summary>
    /// 仅更新指定 CLR 属性名对应的列；为空时更新全部可更新列。
    /// </summary>
    public IReadOnlyCollection<string> IncludeProperties { get; init; }

    /// <summary>
    /// 不更新指定 CLR 属性名对应的列。
    /// </summary>
    public IReadOnlyCollection<string> ExcludeProperties { get; init; }

    /// <summary>
    /// 并发令牌的原始值来源；未提供时使用实体当前值。
    /// </summary>
    public object OriginalValues { get; init; }

    /// <summary>
    /// 是否允许没有主键条件的全表更新。
    /// </summary>
    public bool AllowAllRows { get; init; }
}