using System.Runtime.CompilerServices;
using Bing.Data;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 配置数据库上下文扩展
/// </summary>
public static class SqlOptionsDatabaseContextExtensions
{
    /// <summary>
    /// 数据库上下文存储
    /// </summary>
    private static readonly ConditionalWeakTable<SqlOptions, DatabaseContextHolder> Contexts = new();

    /// <summary>
    /// 设置数据库上下文
    /// </summary>
    /// <param name="options">Sql 配置</param>
    /// <param name="context">数据库上下文</param>
    /// <returns>Sql 配置</returns>
    public static SqlOptions SetDatabaseContext(this SqlOptions options, DatabaseContext context)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        Contexts.Remove(options);
        if (context != null)
            Contexts.Add(options, new DatabaseContextHolder(Clone(context)));
        return options;
    }

    /// <summary>
    /// 获取数据库上下文
    /// </summary>
    /// <param name="options">Sql 配置</param>
    /// <returns>数据库上下文</returns>
    public static DatabaseContext GetDatabaseContext(this SqlOptions options)
    {
        if (options == null)
            return null;
        return Contexts.TryGetValue(options, out var holder) ? Clone(holder.Context) : null;
    }

    /// <summary>
    /// 克隆数据库上下文
    /// </summary>
    /// <param name="context">数据库上下文</param>
    /// <returns>数据库上下文</returns>
    internal static DatabaseContext Clone(DatabaseContext context)
    {
        if (context == null)
            return null;
        return new DatabaseContext
        {
            DbKey = context.DbKey,
            DatabaseType = context.DatabaseType,
            Role = context.Role,
            TenantId = context.TenantId,
            ReadOnly = context.ReadOnly,
            MappingVersion = context.MappingVersion,
            ReadPreference = context.ReadPreference,
            MappingProfile = context.MappingProfile,
            DataSourceKey = context.DataSourceKey,
            DataSource = context.DataSource
        };
    }

    /// <summary>
    /// 数据库上下文持有者
    /// </summary>
    private sealed class DatabaseContextHolder
    {
        /// <summary>
        /// 初始化一个<see cref="DatabaseContextHolder"/>类型的实例
        /// </summary>
        /// <param name="context">数据库上下文</param>
        public DatabaseContextHolder(DatabaseContext context) => Context = context;

        /// <summary>
        /// 数据库上下文
        /// </summary>
        public DatabaseContext Context { get; }
    }
}
