using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// 默认 SQL 数据源解析器
/// </summary>
public sealed class DefaultSqlDataSourceResolver : ISqlDataSourceResolver
{
    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _options;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlDataSourceResolver"/>类型的实例
    /// </summary>
    /// <param name="options">Sql 元数据配置</param>
    public DefaultSqlDataSourceResolver(SqlMetadataOptions options = null) =>
        _options = options ?? new SqlMetadataOptions();

    /// <inheritdoc />
    public SqlDataSourceDescriptor Resolve(string dbKey = null, DatabaseScopeOptions options = null)
    {
        var hasRequestedKey = !string.IsNullOrWhiteSpace(dbKey) || !string.IsNullOrWhiteSpace(options?.DbKey);
        var key = ResolveKey(dbKey, options);
        if (!string.IsNullOrWhiteSpace(key) && _options.DataSources.DataSources.TryGetValue(key, out var dataSource))
            return ApplyReadPreference(Merge(dataSource, options), options);
        if (!string.IsNullOrWhiteSpace(key) && _options.Databases.Count > 0)
        {
            var descriptor = ResolveLegacyDescriptor(key, options);
            if (descriptor != null)
                return ApplyReadPreference(Merge(ToDataSourceDescriptor(descriptor), options), options);
        }
        if (hasRequestedKey)
            return ApplyReadPreference(CreateDefaultDataSource(key, options), options);
        var defaultDataSource = ResolveDefaultDataSource(options);
        if (defaultDataSource != null)
            return ApplyReadPreference(Merge(defaultDataSource, options), options);
        return ApplyReadPreference(CreateDefaultDataSource(key, options), options);
    }

    /// <summary>
    /// 解析数据源键
    /// </summary>
    private string ResolveKey(string dbKey, DatabaseScopeOptions options)
    {
        if (!string.IsNullOrWhiteSpace(dbKey))
            return dbKey;
        if (!string.IsNullOrWhiteSpace(options?.DbKey))
            return options.DbKey;
        if (!string.IsNullOrWhiteSpace(_options.DataSources.DefaultDataSourceKey))
            return _options.DataSources.DefaultDataSourceKey;
        if (!string.IsNullOrWhiteSpace(_options.DefaultDatabaseContext?.DataSourceKey))
            return _options.DefaultDatabaseContext.DataSourceKey;
        return _options.DefaultDatabaseContext?.DbKey ?? ConnectionStringCollection.DefaultConnectionStringName;
    }

    /// <summary>
    /// 解析旧数据库描述信息
    /// </summary>
    private DatabaseDescriptor ResolveLegacyDescriptor(string dbKey, DatabaseScopeOptions options)
    {
        var databaseType = options?.DatabaseType ?? _options.DefaultDatabaseContext?.DatabaseType ?? DatabaseType.SqlServer;
        var role = options?.Role ?? _options.DefaultDatabaseContext?.Role ?? DatabaseRole.Default;
        if (_options.Databases.TryGetValue(SqlMetadataOptions.GetDatabaseDescriptorKey(dbKey, databaseType, role), out var descriptor))
            return descriptor;
        foreach (var item in _options.Databases.Values)
        {
            if (string.Equals(item.DbKey, dbKey, StringComparison.OrdinalIgnoreCase))
                return item;
        }
        return null;
    }

    /// <summary>
    /// 解析默认数据源
    /// </summary>
    private SqlDataSourceDescriptor ResolveDefaultDataSource(DatabaseScopeOptions options)
    {
        if (!string.IsNullOrWhiteSpace(_options.DataSources.DefaultDataSourceKey) &&
            _options.DataSources.DataSources.TryGetValue(_options.DataSources.DefaultDataSourceKey, out var dataSource))
            return dataSource;
        if (_options.DefaultDatabaseContext == null)
            return null;
        var descriptor = ResolveLegacyDescriptor(_options.DefaultDatabaseContext.DbKey, options) ?? new DatabaseDescriptor
        {
            DbKey = _options.DefaultDatabaseContext.DbKey,
            DatabaseType = _options.DefaultDatabaseContext.DatabaseType,
            Role = _options.DefaultDatabaseContext.Role,
            ReadOnly = _options.DefaultDatabaseContext.ReadOnly
        };
        return ToDataSourceDescriptor(descriptor);
    }

    /// <summary>
    /// 合并数据源与作用域选项
    /// </summary>
    private static SqlDataSourceDescriptor Merge(SqlDataSourceDescriptor descriptor, DatabaseScopeOptions options)
    {
        if (descriptor == null)
            return null;
        return new SqlDataSourceDescriptor
        {
            Key = descriptor.Key,
            DbKey = string.IsNullOrWhiteSpace(descriptor.DbKey) ? descriptor.Key : descriptor.DbKey,
            DatabaseType = options?.DatabaseType ?? descriptor.DatabaseType,
            ConnectionStringName = descriptor.ConnectionStringName,
            ConnectionString = descriptor.ConnectionString,
            IsReadOnly = options?.ReadOnly ?? descriptor.IsReadOnly,
            MappingProfile = string.IsNullOrWhiteSpace(options?.MappingProfile) ? descriptor.MappingProfile : options.MappingProfile,
            PrimaryReadStrategy = descriptor.PrimaryReadStrategy,
            PrimaryDataSourceKey = descriptor.PrimaryDataSourceKey
        };
    }

    /// <summary>
    /// 应用读取偏好
    /// </summary>
    /// <param name="descriptor">数据源描述信息</param>
    /// <param name="options">作用域选项</param>
    /// <returns>数据源描述信息</returns>
    private SqlDataSourceDescriptor ApplyReadPreference(SqlDataSourceDescriptor descriptor, DatabaseScopeOptions options)
    {
        if (descriptor == null || options?.ReadPreference != SqlReadPreference.Primary)
            return descriptor;
        if (descriptor.PrimaryReadStrategy == PrimaryReadStrategy.None)
            return descriptor;
        if (descriptor.PrimaryReadStrategy == PrimaryReadStrategy.Transaction)
            throw new NotSupportedException("PrimaryReadStrategy.Transaction 尚未支持自动事务读取，请显式创建事务或改用 PrimaryDataSource 策略");
        if (string.IsNullOrWhiteSpace(descriptor.PrimaryDataSourceKey))
            throw new InvalidOperationException($"数据源 {descriptor.Key} 未配置主库数据源键");
        if (!_options.DataSources.DataSources.TryGetValue(descriptor.PrimaryDataSourceKey, out var primary))
            throw new InvalidOperationException($"未找到主库数据源 {descriptor.PrimaryDataSourceKey}");
        return Merge(primary, new DatabaseScopeOptions
        {
            DbKey = primary.DbKey,
            Role = options.Role,
            ReadOnly = options.ReadOnly,
            MappingProfile = string.IsNullOrWhiteSpace(options.MappingProfile) ? descriptor.MappingProfile : options.MappingProfile
        });
    }

    /// <summary>
    /// 转换旧数据库描述为数据源描述
    /// </summary>
    private static SqlDataSourceDescriptor ToDataSourceDescriptor(DatabaseDescriptor descriptor)
    {
        if (descriptor == null)
            return null;
        return new SqlDataSourceDescriptor
        {
            Key = descriptor.DbKey,
            DbKey = descriptor.DbKey,
            DatabaseType = descriptor.DatabaseType,
            ConnectionStringName = descriptor.DbKey,
            ConnectionString = descriptor.ConnectionString,
            IsReadOnly = descriptor.ReadOnly
        };
    }

    /// <summary>
    /// 创建默认数据源
    /// </summary>
    private static SqlDataSourceDescriptor CreateDefaultDataSource(string key, DatabaseScopeOptions options)
    {
        var dbKey = string.IsNullOrWhiteSpace(key) ? ConnectionStringCollection.DefaultConnectionStringName : key;
        return new SqlDataSourceDescriptor
        {
            Key = dbKey,
            DbKey = dbKey,
            DatabaseType = options?.DatabaseType ?? DatabaseType.SqlServer,
            ConnectionStringName = dbKey,
            IsReadOnly = options?.ReadOnly ?? false,
            MappingProfile = options?.MappingProfile
        };
    }
}