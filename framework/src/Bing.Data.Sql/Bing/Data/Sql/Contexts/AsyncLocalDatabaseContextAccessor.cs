namespace Bing.Data.Sql;

/// <summary>
/// 基于 <see cref="AsyncLocal{T}"/> 的数据库上下文访问器
/// </summary>
public sealed class AsyncLocalDatabaseContextAccessor : IDatabaseContextAccessor
{
    /// <summary>
    /// 当前上下文持有器
    /// </summary>
    private static readonly AsyncLocal<DatabaseContextHolder> CurrentHolder = new();

    /// <summary>
    /// 当前数据库上下文
    /// </summary>
    public DatabaseContext Current
    {
        get => CurrentHolder.Value?.Context;
        set
        {
            var holder = CurrentHolder.Value;
            if (holder != null)
                holder.Context = null;
            if (value != null)
                CurrentHolder.Value = new DatabaseContextHolder { Context = value };
        }
    }

    /// <summary>
    /// 数据库上下文持有器
    /// </summary>
    private sealed class DatabaseContextHolder
    {
        /// <summary>
        /// 数据库上下文
        /// </summary>
        public DatabaseContext Context { get; set; }
    }
}
