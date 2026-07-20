using Bing.Data.Enums;
using Bing.Data.Sql.Configs;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 工厂基类。
/// </summary>
public abstract class SqlFactoryBase
{
    /// <summary>
    /// 服务提供程序。
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 数据库上下文访问器。
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// SQL 元数据配置。
    /// </summary>
    private readonly SqlMetadataOptions _metadataOptions;

    /// <summary>
    /// SQL 实现类型解析器。
    /// </summary>
    private readonly ISqlImplementationTypeResolver _implementationTypeResolver;

    /// <summary>
    /// SQL 数据源解析器。
    /// </summary>
    private readonly ISqlDataSourceResolver _dataSourceResolver;

    /// <summary>
    /// SQL 连接字符串解析器。
    /// </summary>
    private readonly ISqlConnectionStringResolver _connectionStringResolver;

    /// <summary>
    /// 初始化一个<see cref="SqlFactoryBase"/>类型的实例。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
    /// <param name="metadataOptions">SQL 元数据配置。</param>
    /// <param name="implementationTypeResolver">SQL 实现类型解析器。</param>
    /// <param name="dataSourceResolver">SQL 数据源解析器。</param>
    /// <param name="connectionStringResolver">SQL 连接字符串解析器。</param>
    protected SqlFactoryBase(IServiceProvider serviceProvider,
        IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions metadataOptions = null,
        ISqlImplementationTypeResolver implementationTypeResolver = null,
        ISqlDataSourceResolver dataSourceResolver = null,
        ISqlConnectionStringResolver connectionStringResolver = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _databaseContextAccessor = databaseContextAccessor;
        _metadataOptions = metadataOptions ?? new SqlMetadataOptions();
        _implementationTypeResolver = implementationTypeResolver;
        _dataSourceResolver = dataSourceResolver ?? new DefaultSqlDataSourceResolver(_metadataOptions);
        _connectionStringResolver = connectionStringResolver ??
                                    _serviceProvider.GetService<ISqlConnectionStringResolver>() ??
                                    new DefaultSqlConnectionStringResolver(
                                        _serviceProvider.GetService<ConnectionStringCollection>());
    }

    /// <summary>
    /// 创建数据库上下文。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>数据库上下文。</returns>
    protected DatabaseContext CreateContext(string dbKey)
    {
        var current = _databaseContextAccessor?.Current ?? _metadataOptions.DefaultDatabaseContext;
        var options = new DatabaseScopeOptions
        {
            DbKey = dbKey,
            TenantId = current?.TenantId,
            ReadPreference = current?.ReadPreference ?? SqlReadPreference.Default
        };
        var dataSource = _dataSourceResolver.Resolve(dbKey, options);
        return CreateContext(dataSource, options.TenantId, current?.MappingProfile,
            options.ReadPreference ?? SqlReadPreference.Default);
    }

    /// <summary>
    /// 获取当前数据库上下文。
    /// </summary>
    /// <param name="serviceType">服务类型。</param>
    /// <returns>数据库上下文。</returns>
    protected DatabaseContext GetCurrentContext(Type serviceType)
    {
        var current = _databaseContextAccessor?.Current ?? _metadataOptions.DefaultDatabaseContext;
        if (current?.DataSource != null)
            return Clone(current);
        var options = new DatabaseScopeOptions
        {
            DbKey = current?.DbKey,
            TenantId = current?.TenantId,
            ReadPreference = current?.ReadPreference ?? SqlReadPreference.Default
        };
        var dataSource = _dataSourceResolver.Resolve(current?.DbKey, options);
        return CreateContext(dataSource, options.TenantId, current?.MappingProfile,
            options.ReadPreference ?? SqlReadPreference.Default);
    }

    /// <summary>
    /// 创建事务使用的主库数据库上下文。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>数据库上下文。</returns>
    protected DatabaseContext CreateTransactionContext(string dbKey)
    {
        var current = _databaseContextAccessor?.Current ?? _metadataOptions.DefaultDatabaseContext;
        var requestedDbKey = string.IsNullOrWhiteSpace(dbKey) ? current?.DbKey : dbKey;
        var options = new DatabaseScopeOptions
        {
            DbKey = requestedDbKey,
            TenantId = current?.TenantId,
            ReadPreference = SqlReadPreference.Primary
        };
        var dataSource = _dataSourceResolver.Resolve(requestedDbKey, options);
        return CreateContext(dataSource, options.TenantId, current?.MappingProfile,
            options.ReadPreference ?? SqlReadPreference.Default);
    }

    /// <summary>
    /// 创建实例。
    /// </summary>
    /// <typeparam name="TService">服务类型。</typeparam>
    /// <param name="context">数据库上下文。</param>
    /// <returns>服务实例。</returns>
    protected TService CreateInstance<TService>(DatabaseContext context) where TService : class
    {
        var databaseType = context?.DataSource?.DatabaseType;
        var implementationType = GetImplementationType(typeof(TService), databaseType);
        var options = CreateOptions(implementationType, context);
        return (TService)ActivatorUtilities.CreateInstance(_serviceProvider, implementationType, _serviceProvider,
            options);
    }

    /// <summary>
    /// 获取实现类型。
    /// </summary>
    /// <typeparam name="TService">服务类型。</typeparam>
    /// <returns>实现类型。</returns>
    protected Type GetImplementationType<TService>() where TService : class
    {
        return GetImplementationType(typeof(TService));
    }

    /// <summary>
    /// 获取实现类型。
    /// </summary>
    /// <param name="serviceType">服务类型。</param>
    /// <param name="databaseType">数据库类型。</param>
    /// <returns>实现类型。</returns>
    protected Type GetImplementationType(Type serviceType, DatabaseType? databaseType = null)
    {
        var implementationType = _implementationTypeResolver?.Resolve(serviceType, databaseType);
        if (implementationType != null)
            return implementationType;
        if (serviceType.IsAbstract == false && serviceType.IsInterface == false)
            return serviceType;
        throw new InvalidOperationException($"未注册类型 {serviceType.FullName} 在数据库类型 {databaseType?.ToString() ?? "<未指定>"} 下的 SQL 实现类型");
    }

    /// <summary>
    /// 创建 SQL 配置。
    /// </summary>
    /// <param name="implementationType">实现类型。</param>
    /// <param name="context">数据库上下文。</param>
    /// <returns>SQL 配置。</returns>
    protected SqlOptions CreateOptions(Type implementationType, DatabaseContext context)
    {
        var template = GetTemplateOptions(implementationType);
        var dataSource = context?.DataSource ?? _dataSourceResolver.Resolve(context?.DbKey);
        var optionsType = typeof(SqlOptions<>).MakeGenericType(implementationType);
        var options = (SqlOptions)Activator.CreateInstance(optionsType);
        CopyOptions(template, options);
        options.DatabaseType = dataSource.DatabaseType;
        options.SetDatabaseContext(CreateContext(dataSource, context?.TenantId, context?.MappingProfile,
            context?.ReadPreference ?? SqlReadPreference.Default));
        var connectionString = ResolveConnectionString(dataSource, template);
        if (string.IsNullOrWhiteSpace(connectionString) == false)
        {
            options.ConnectionString = connectionString;
            options.Connection = null;
        }
        return options;
    }

    /// <summary>
    /// 获取模板 SQL 配置。
    /// </summary>
    /// <param name="implementationType">实现类型。</param>
    /// <returns>模板 SQL 配置。</returns>
    protected SqlOptions GetTemplateOptions(Type implementationType) =>
        _serviceProvider.GetService(typeof(SqlOptions<>).MakeGenericType(implementationType)) as SqlOptions;

    /// <summary>
    /// 创建数据库上下文。
    /// </summary>
    /// <param name="dataSource">数据源描述。</param>
    /// <param name="tenantId">租户标识。</param>
    /// <param name="mappingProfile">映射配置名称。</param>
    /// <param name="readPreference">读取偏好。</param>
    /// <returns>数据库上下文。</returns>
    private DatabaseContext CreateContext(SqlDataSourceDescriptor dataSource, string tenantId,
        string mappingProfile, SqlReadPreference readPreference)
    {
        if (dataSource == null)
            throw new InvalidOperationException("SQL 数据源描述不能为空");
        return DatabaseContextSnapshot.Create(new DatabaseContext
        {
            DbKey = dataSource.Key,
            TenantId = tenantId,
            MappingProfile = string.IsNullOrWhiteSpace(dataSource.MappingProfile) ? mappingProfile : dataSource.MappingProfile,
            ReadPreference = readPreference,
            DataSource = dataSource
        });
    }

    /// <summary>
    /// 克隆数据库上下文。
    /// </summary>
    /// <param name="context">数据库上下文。</param>
    /// <returns>数据库上下文。</returns>
    private DatabaseContext Clone(DatabaseContext context)
    {
        return DatabaseContextSnapshot.Create(context);
    }

    /// <summary>
    /// 解析连接字符串。
    /// </summary>
    /// <param name="dataSource">数据源描述。</param>
    /// <param name="template">模板 SQL 配置。</param>
    /// <returns>连接字符串。</returns>
    private string ResolveConnectionString(SqlDataSourceDescriptor dataSource, SqlOptions template)
    {
        if (string.IsNullOrWhiteSpace(dataSource?.ConnectionString) == false ||
            string.IsNullOrWhiteSpace(dataSource?.ConnectionStringName) == false)
            return _connectionStringResolver.Resolve(dataSource);
        if (template?.Connection != null)
            return null;
        return _connectionStringResolver.Resolve(dataSource);
    }

    /// <summary>
    /// 复制 SQL 配置。
    /// </summary>
    /// <param name="source">源配置。</param>
    /// <param name="target">目标配置。</param>
    private static void CopyOptions(SqlOptions source, SqlOptions target)
    {
        if (source == null || target == null)
            return;
        foreach (var property in target.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(t => t.CanWrite))
        {
            var sourceProperty = source.GetType().GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public);
            if (sourceProperty == null || sourceProperty.CanRead == false)
                continue;
            property.SetValue(target, sourceProperty.GetValue(source));
        }
    }
}
