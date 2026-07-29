namespace Bing.Data.Sql.Mutations;

/// <summary>
/// 删除实体时使用的安全选项。
/// </summary>
public sealed class SqlDeleteOptions
{
    /// <summary>
    /// 并发令牌的原始值来源；未提供时使用实体当前值。
    /// </summary>
    public object OriginalValues { get; init; }

    /// <summary>
    /// 是否允许没有主键条件的全表删除。
    /// </summary>
    public bool AllowAllRows { get; init; }
}