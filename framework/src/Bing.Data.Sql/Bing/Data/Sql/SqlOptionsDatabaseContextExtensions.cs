using System.Runtime.CompilerServices;
using Bing.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 为 <see cref="SqlOptions"/> 附加数据库上下文的扩展方法。
/// </summary>
public static class SqlOptionsDatabaseContextExtensions
{
    /// <summary>
    /// 按 <see cref="SqlOptions"/> 实例弱引用关联的数据库上下文存储。
    /// </summary>
    /// <remarks>该表用于附加上下文而不改变配置对象结构，且不会阻止配置实例及其关联上下文被回收。</remarks>
    private static readonly ConditionalWeakTable<SqlOptions, DatabaseContextHolder> Contexts = new();

    /// <summary>
    /// 为 SQL 配置设置数据库上下文快照。
    /// </summary>
    /// <param name="options">要附加上下文的 SQL 配置。</param>
    /// <param name="context">要保存的数据库上下文；为 <c>null</c> 时移除已有上下文。</param>
    /// <returns>当前 SQL 配置，以支持链式调用。</returns>
    public static SqlOptions SetDatabaseContext(this SqlOptions options, DatabaseContext context)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        Contexts.Remove(options);
        if (context != null)
            Contexts.Add(options, new DatabaseContextHolder(DatabaseContextSnapshot.Create(context)));
        return options;
    }

    /// <summary>
    /// 获取 SQL 配置关联的数据库上下文快照。
    /// </summary>
    /// <param name="options">要读取上下文的 SQL 配置。</param>
    /// <returns>独立的数据库上下文快照；未关联上下文或配置为 <c>null</c> 时返回 <c>null</c>。</returns>
    public static DatabaseContext GetDatabaseContext(this SqlOptions options)
    {
        if (options == null)
            return null;
        return Contexts.TryGetValue(options, out var holder) ? DatabaseContextSnapshot.Create(holder.Context) : null;
    }

    /// <summary>
    /// 创建数据库上下文的独立快照。
    /// </summary>
    /// <param name="context">要复制的数据库上下文。</param>
    /// <returns>复制后的数据库上下文快照。</returns>
    internal static DatabaseContext Clone(DatabaseContext context)
    {
        return DatabaseContextSnapshot.Create(context);
    }

    /// <summary>
    /// 保存弱引用表值的数据库上下文快照。
    /// </summary>
    private sealed class DatabaseContextHolder
    {
        /// <summary>
        /// 使用指定上下文快照初始化 <see cref="DatabaseContextHolder"/> 的实例。
        /// </summary>
        /// <param name="context">要保存的数据库上下文快照。</param>
        public DatabaseContextHolder(DatabaseContext context) => Context = context;

        /// <summary>
        /// 获取保存的数据库上下文快照。
        /// </summary>
        public DatabaseContext Context { get; }
    }
}
