namespace Bing.Data.Sql.Mutations;

/// <summary>
/// 更新实体时使用的列筛选和安全选项。
/// </summary>
public class SqlUpdateOptions
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
    /// 并发令牌不匹配时的处理方式。默认抛出异常。
    /// </summary>
    public SqlConcurrencyConflictBehavior ConcurrencyConflictBehavior { get; init; } =
        SqlConcurrencyConflictBehavior.Throw;

    /// <summary>
    /// 尝试获取指定属性的并发原始值。
    /// </summary>
    /// <param name="propertyName">CLR 属性名。</param>
    /// <param name="value">已配置的原始值。</param>
    /// <returns>存在已配置值时返回 <c>true</c>。</returns>
    internal virtual bool TryGetOriginalValue(string propertyName, out object value)
    {
        value = null;
        return false;
    }
}