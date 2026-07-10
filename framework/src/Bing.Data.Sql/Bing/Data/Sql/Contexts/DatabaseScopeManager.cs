using Bing.Data;
using Bing.Data.Enums;
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
            DbKey = string.IsNullOrWhiteSpace(dataSource.DbKey) ? dataSource.Key : dataSource.DbKey,
            DataSourceKey = dataSource.Key,
            DataSource = dataSource,
            DatabaseType = dataSource.DatabaseType,
            Role = options.Role,
            TenantId = options.TenantId ?? parent?.TenantId,
            ReadOnly = dataSource.IsReadOnly,
            MappingVersion = dataSource.MappingProfile ?? parent?.MappingVersion ?? _options.DefaultDatabaseContext?.MappingVersion,
            MappingProfile = dataSource.MappingProfile ?? parent?.MappingProfile ?? _options.DefaultDatabaseContext?.MappingProfile,
            ReadPreference = options.ReadPreference
        };
        return new DatabaseScope(_databaseContextAccessor, parent);
    }

    /// <summary>
    /// 使用指定数据库上下文
    /// </summary>
    /// <param name="dbKey">数据库标识</param>
    /// <param name="databaseType">数据库类型</param>
    /// <param name="role">数据库角色</param>
    /// <param name="tenantId">租户标识</param>
    /// <param name="readOnly">是否只读</param>
    /// <param name="mappingVersion">映射版本</param>
    /// <returns>数据库上下文作用域</returns>
    public IDatabaseScope Use(string dbKey, DatabaseType databaseType, DatabaseRole role = DatabaseRole.Default,
        string tenantId = null, bool readOnly = false, string mappingVersion = null)
    {
        return Use(new DatabaseScopeOptions
        {
            DbKey = dbKey,
            DatabaseType = databaseType,
            Role = role,
            TenantId = tenantId,
            ReadOnly = readOnly,
            MappingProfile = mappingVersion
        });
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
