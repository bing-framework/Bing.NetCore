namespace Bing.Data.Sql;

/// <summary>
/// 基于 <see cref="AsyncLocal{T}"/> 的数据库上下文访问器
/// </summary>
public sealed class AsyncLocalDatabaseContextAccessor : IDatabaseContextAccessor
{
    /// <summary>
    /// 当前数据库上下文快照。读取该属性返回独立副本，直接修改返回值不会写回当前异步执行上下文，应通过设置该属性或 <see cref="DatabaseContextAccessorExtensions.Update"/> 更新。
    /// </summary>
    private readonly AsyncLocal<DatabaseContext> _current = new();

    /// <summary>
    /// 当前数据库上下文
    /// </summary>
    public DatabaseContext Current
    {
        get => DatabaseContextSnapshot.Create(_current.Value);
        set => _current.Value = DatabaseContextSnapshot.Create(value);
    }
}
