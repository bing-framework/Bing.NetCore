namespace Bing.Data.Sql.Mutations;

/// <summary>
/// 插入实体时使用的列筛选选项。
/// </summary>
public sealed class SqlInsertOptions
{
    /// <summary>
    /// 获取或初始化仅插入指定 CLR 属性名对应的列。
    /// </summary>
    /// <remarks>
    /// 为空时插入全部可插入列。
    /// </remarks>
    public IReadOnlyCollection<string> IncludeProperties { get; init; }

    /// <summary>
    /// 获取或初始化不插入指定 CLR 属性名对应的列。
    /// </summary>
    public IReadOnlyCollection<string> ExcludeProperties { get; init; }
}