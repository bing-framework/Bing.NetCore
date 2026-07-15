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
    /// 初始化一个<see cref="DatabaseScopeManager"/>类型的实例
    /// </summary>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="options">Sql 元数据配置</param>
    /// <param name="dataSourceResolver">SQL 数据源解析器</param>
    public DatabaseScopeManager(IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions options = null,
        ISqlDataSourceResolver dataSourceResolver = null)
    {
        _databaseContextAccessor = databaseContextAccessor ?? throw new ArgumentNullException(nameof(databaseContextAccessor));
        _options = options ?? new SqlMetadataOptions();
        _dataSourceResolver = dataSourceResolver ?? new DefaultSqlDataSourceResolver(_options);
    }

    /// <inheritdoc />
    public IDatabaseScope Use(string dbKey) => Use(new DatabaseScopeOptions { DbKey = dbKey });

    /// <inheritdoc />
    public IDatabaseScope Use(DatabaseScopeOptions options)
    {
        options ??= new DatabaseScopeOptions();
        var parent = _databaseContextAccessor.Current;
        var dataSource = _dataSourceResolver.Resolve(options.DbKey, options);
        _databaseContextAccessor.Current = new DatabaseContext
        {
            DbKey = dataSource.Key,
            DataSource = dataSource,
            TenantId = options.TenantId ?? parent?.TenantId,
            MappingProfile = dataSource.MappingProfile ?? parent?.MappingProfile ?? _options.DefaultDatabaseContext?.MappingProfile,
            ReadPreference = options.ReadPreference
        };
        return new DatabaseScope(_databaseContextAccessor, parent);
    }

    /// <summary>
    /// 数据库上下文作用域
    /// </summary>
    private sealed class DatabaseScope : IDatabaseScope
    {
        /// <summary>
        /// 数据库上下文访问器
        /// </summary>
        private readonly IDatabaseContextAccessor _databaseContextAccessor;

        /// <summary>
        /// 父级数据库上下文
        /// </summary>
        private readonly DatabaseContext _parent;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化一个<see cref="DatabaseScope"/>类型的实例
        /// </summary>
        /// <param name="databaseContextAccessor">数据库上下文访问器</param>
        /// <param name="parent">父级数据库上下文</param>
        public DatabaseScope(IDatabaseContextAccessor databaseContextAccessor, DatabaseContext parent)
        {
            _databaseContextAccessor = databaseContextAccessor;
            _parent = parent;
        }

        /// <summary>
        /// 释放作用域并恢复上级数据库上下文
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _databaseContextAccessor.Current = _parent;
            _disposed = true;
        }
    }
}
