namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文访问器扩展。
/// </summary>
public static class DatabaseContextAccessorExtensions
{
    /// <summary>
    /// 更新当前异步执行上下文中的数据库上下文。
    /// </summary>
    /// <param name="accessor">数据库上下文访问器。</param>
    /// <param name="updater">数据库上下文更新操作。</param>
    /// <returns>更新后写入访问器的数据库上下文快照。</returns>
    public static DatabaseContext Update(this IDatabaseContextAccessor accessor,
        Func<DatabaseContext, DatabaseContext> updater)
    {
        if (accessor == null)
            throw new ArgumentNullException(nameof(accessor));
        if (updater == null)
            throw new ArgumentNullException(nameof(updater));
        var context = updater(accessor.Current ?? new DatabaseContext());
        accessor.Current = context;
        return accessor.Current;
    }
}