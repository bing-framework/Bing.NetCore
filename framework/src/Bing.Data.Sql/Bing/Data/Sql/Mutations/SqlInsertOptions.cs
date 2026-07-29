namespace Bing.Data.Sql.Mutations;

/// <summary>
/// 插入实体时使用的列筛选选项。
/// </summary>
public sealed class SqlInsertOptions
{
    /// <summary>
    /// 仅插入指定 CLR 属性名对应的列；为空时插入全部可插入列。
    /// </summary>
    public IReadOnlyCollection<string> IncludeProperties { get; init; }

    /// <summary>
    /// 不插入指定 CLR 属性名对应的列。
    /// </summary>
    public IReadOnlyCollection<string> ExcludeProperties { get; init; }
}