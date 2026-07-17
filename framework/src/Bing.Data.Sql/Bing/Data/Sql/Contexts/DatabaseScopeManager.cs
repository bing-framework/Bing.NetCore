using Bing.Data;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文作用域管理器
/// </summary>
public sealed class DatabaseScopeManager : IDatabaseScopeManager
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
    /// SQL 数据源解析器
    /// </summary>
    private readonly ISqlDataSourceResolver _dataSourceResolver;

    /// <summary>
    /// 数据库上下文快照工厂。
    /// </summary>
    private readonly IDatabaseContextSnapshotFactory _snapshotFactory;

    /// <summary>
    /// 初始化一个<see cref="DatabaseScopeManager"/>类型的实例
    /// </summary>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="options">Sql 元数据配置</param>
    /// <param name="dataSourceResolver">SQL 数据源解析器</param>
    /// <param name="snapshotFactory">数据库上下文快照工厂。</param>
    public DatabaseScopeManager(IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions options = null,
        ISqlDataSourceResolver dataSourceResolver = null, IDatabaseContextSnapshotFactory snapshotFactory = null)
    {
        _databaseContextAccessor = databaseContextAccessor ?? throw new ArgumentNullException(nameof(databaseContextAccessor));
        _options = options ?? new SqlMetadataOptions();
        _dataSourceResolver = dataSourceResolver ?? new DefaultSqlDataSourceResolver(_options);
        _snapshotFactory = snapshotFactory ?? new DefaultDatabaseContextSnapshotFactory();
    }

    /// <inheritdoc />
    public IDatabaseScope Use(string dbKey) => Use(new DatabaseScopeOptions { DbKey = dbKey });

    /// <inheritdoc />
    public IDatabaseScope Use(DatabaseScopeOptions options)
    {
        options ??= new DatabaseScopeOptions();
        var parent = _databaseContextAccessor.Current;
        var readPreference = options.ReadPreference ?? parent?.ReadPreference ?? SqlReadPreference.Default;
        var resolvedOptions = new DatabaseScopeOptions
        {
            DbKey = options.DbKey,
            TenantId = options.TenantId,
            ReadPreference = readPreference
        };
        var dataSource = _dataSourceResolver.Resolve(resolvedOptions.DbKey, resolvedOptions);
        var context = new DatabaseContext
        {
            DbKey = dataSource.Key,
            DataSource = dataSource,
            TenantId = options.TenantId ?? parent?.TenantId,
            MappingProfile = dataSource.MappingProfile ?? parent?.MappingProfile ?? _options.DefaultDatabaseContext?.MappingProfile,
            ReadPreference = readPreference
        };
        return DatabaseContextScopeStack.Enter(_databaseContextAccessor, context, _snapshotFactory);
    }
}
