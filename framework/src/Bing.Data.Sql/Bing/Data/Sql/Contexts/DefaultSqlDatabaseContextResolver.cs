using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// 默认 SQL 数据库上下文解析器
/// </summary>
public sealed class DefaultSqlDatabaseContextResolver : ISqlDatabaseContextResolver
{
    /// <summary>
    /// 数据库上下文访问器
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _options;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlDatabaseContextResolver"/>类型的实例
    /// </summary>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="options">Sql 元数据配置</param>
    public DefaultSqlDatabaseContextResolver(IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions options = null)
    {
        _databaseContextAccessor = databaseContextAccessor;
        _options = options ?? new SqlMetadataOptions();
    }

    /// <inheritdoc />
    public DatabaseContext Resolve(SqlOptions options = null)
    {
        var context = options.GetDatabaseContext() ?? _databaseContextAccessor?.Current ?? _options.DefaultDatabaseContext;
        if (context != null)
            return Normalize(context, options);
        return null;
    }

    /// <summary>
    /// 标准化数据库上下文
    /// </summary>
    /// <param name="context">数据库上下文</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>数据库上下文</returns>
    private static DatabaseContext Normalize(DatabaseContext context, SqlOptions options)
    {
        var result = SqlOptionsDatabaseContextExtensions.Clone(context);
        return result;
    }
}
