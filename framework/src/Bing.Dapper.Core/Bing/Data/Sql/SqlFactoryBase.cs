using System.Reflection;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Configs;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 工厂基类
/// </summary>
public abstract class SqlFactoryBase
{
    /// <summary>
    /// 服务提供程序
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 数据库上下文访问器
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// 数据库描述解析器
    /// </summary>
    private readonly IDatabaseDescriptorResolver _databaseDescriptorResolver;

    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _metadataOptions;

    /// <summary>
    /// SQL 实现类型解析器
    /// </summary>
    private readonly ISqlImplementationTypeResolver _implementationTypeResolver;

    /// <summary>
    /// SQL 数据源解析器
    /// </summary>
    private readonly ISqlDataSourceResolver _dataSourceResolver;

    /// <summary>
    /// 初始化一个<see cref="SqlFactoryBase"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="databaseDescriptorResolver">数据库描述解析器</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <param name="implementationTypeResolver">SQL 实现类型解析器</param>
    /// <param name="dataSourceResolver">SQL 数据源解析器</param>
    protected SqlFactoryBase(IServiceProvider serviceProvider,
        IDatabaseContextAccessor databaseContextAccessor = null,
        IDatabaseDescriptorResolver databaseDescriptorResolver = null,
        SqlMetadataOptions metadataOptions = null,
        ISqlImplementationTypeResolver implementationTypeResolver = null,
        ISqlDataSourceResolver dataSourceResolver = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _databaseContextAccessor = databaseContextAccessor;
        _databaseDescriptorResolver = databaseDescriptorResolver;
        _metadataOptions = metadataOptions ?? new SqlMetadataOptions();
        _implementationTypeResolver = implementationTypeResolver;
        _dataSourceResolver = dataSourceResolver ?? new DefaultSqlDataSourceResolver(_metadataOptions);
    }

    /// <summary>
    /// 创建数据库上下文
    /// </summary>
    /// <param name="dbKey">数据库键</param>
    /// <returns>数据库上下文</returns>
    protected DatabaseContext CreateContext(string dbKey)
    {
        var current = _databaseContextAccessor?.Current ?? _metadataOptions.DefaultDatabaseContext;
        var options = new DatabaseScopeOptions
        {
            DbKey = dbKey,
            TenantId = current?.TenantId,
            ReadPreference = current?.ReadPreference ?? SqlReadPreference.Default,
            MappingProfile = current?.MappingProfile,
            Role = current?.Role ?? DatabaseRole.Default,
            ReadOnly = current?.ReadOnly
        };
        var dataSource = _dataSourceResolver.Resolve(dbKey, options);
        return new DatabaseContext
        {
            DbKey = string.IsNullOrWhiteSpace(dataSource.DbKey) ? dataSource.Key : dataSource.DbKey,
            DataSourceKey = dataSource.Key,
            DataSource = dataSource,
            DatabaseType = dataSource.DatabaseType,
            Role = options.Role,
            TenantId = options.TenantId,
            ReadOnly = dataSource.IsReadOnly,
            MappingVersion = dataSource.MappingProfile ?? current?.MappingVersion,
            MappingProfile = dataSource.MappingProfile ?? current?.MappingProfile,
            ReadPreference = options.ReadPreference
        };
    }

    /// <summary>
    /// 创建数据库上下文
    /// </summary>
    /// <param name="dbKey">数据库键</param>
    /// <param name="databaseType">数据库类型</param>
    /// <param name="role">数据库角色</param>
    /// <returns>数据库上下文</returns>
    protected DatabaseContext CreateContext(string dbKey, DatabaseType databaseType, DatabaseRole role)
    {
        var current = _databaseContextAccessor?.Current ?? _metadataOptions.DefaultDatabaseContext;
        return new DatabaseContext
        {
            DbKey = string.IsNullOrWhiteSpace(dbKey)
                ? current?.DbKey ?? ConnectionStringCollection.DefaultConnectionStringName
                : dbKey,
            DatabaseType = databaseType,
            Role = role,
            TenantId = current?.TenantId,
            ReadOnly = current?.ReadOnly ?? false,
            MappingVersion = current?.MappingVersion,
            MappingProfile = current?.MappingProfile,
            ReadPreference = current?.ReadPreference ?? SqlReadPreference.Default
        };
    }

    /// <summary>
    /// 获取当前数据库上下文
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <returns>数据库上下文</returns>
    protected DatabaseContext GetCurrentContext(Type serviceType)
    {
        var current = _databaseContextAccessor?.Current ?? _metadataOptions.DefaultDatabaseContext;
        if (current != null)
        {
            return new DatabaseContext
            {
                DbKey = string.IsNullOrWhiteSpace(current.DbKey)
                    ? ConnectionStringCollection.DefaultConnectionStringName
                    : current.DbKey,
                DatabaseType = current.DatabaseType,
                Role = current.Role,
                TenantId = current.TenantId,
                ReadOnly = current.ReadOnly,
                MappingVersion = current.MappingVersion,
                MappingProfile = current.MappingProfile,
                DataSourceKey = current.DataSourceKey,
                DataSource = current.DataSource,
                ReadPreference = current.ReadPreference
            };
        }

        var implementationType = GetImplementationType(serviceType);
        var template = GetTemplateOptions(implementationType);
        return new DatabaseContext
        {
            DbKey = ConnectionStringCollection.DefaultConnectionStringName,
            DatabaseType = template?.DatabaseType ?? DatabaseType.SqlServer,
            Role = DatabaseRole.Default
        };
    }

    /// <summary>
    /// 创建实例
    /// </summary>
    /// <typeparam name="TService">服务类型</typeparam>
    /// <param name="context">数据库上下文</param>
    /// <returns>服务实例</returns>
    protected TService CreateInstance<TService>(DatabaseContext context) where TService : class
    {
        var implementationType = GetImplementationType(typeof(TService), context?.DatabaseType);
        var options = CreateOptions(implementationType, context);
        return (TService)ActivatorUtilities.CreateInstance(_serviceProvider, implementationType, _serviceProvider,
            options);
    }

    /// <summary>
    /// 获取实现类型
    /// </summary>
    /// <typeparam name="TService">服务类型</typeparam>
    /// <returns>实现类型</returns>
    protected Type GetImplementationType<TService>() where TService : class
    {
        return GetImplementationType(typeof(TService));
    }

    /// <summary>
    /// 获取实现类型
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="databaseType">数据库类型</param>
    /// <returns>实现类型</returns>
    protected Type GetImplementationType(Type serviceType, DatabaseType? databaseType = null)
    {
        var implementationType = _implementationTypeResolver?.Resolve(serviceType, databaseType);
        if (implementationType != null)
            return implementationType;
        if (serviceType.IsAbstract == false && serviceType.IsInterface == false)
            return serviceType;

        // 回退到旧逻辑会实例化服务，仅在没有显式实现类型映射时使用。
        var service = _serviceProvider.GetService(serviceType);
        if (service == null)
            throw new InvalidOperationException($"未注册类型 {serviceType.FullName} 的实现");
        return service.GetType();
    }

    /// <summary>
    /// 创建 Sql 配置
    /// </summary>
    /// <param name="implementationType">实现类型</param>
    /// <param name="context">数据库上下文</param>
    /// <returns>Sql 配置</returns>
    protected SqlOptions CreateOptions(Type implementationType, DatabaseContext context)
    {
        var template = GetTemplateOptions(implementationType);
        var descriptor = ResolveDescriptor(context, template);
        var optionsType = typeof(SqlOptions<>).MakeGenericType(implementationType);
        var options = (SqlOptions)Activator.CreateInstance(optionsType);
        CopyOptions(template, options);
        options.DatabaseType = descriptor.DatabaseType;
        options.SetDatabaseContext(new DatabaseContext
        {
            DbKey = descriptor.DbKey,
            DatabaseType = descriptor.DatabaseType,
            Role = descriptor.Role,
            TenantId = context?.TenantId,
            ReadOnly = descriptor.ReadOnly,
            MappingVersion = context?.MappingVersion,
            MappingProfile = context?.MappingProfile,
            DataSourceKey = context?.DataSourceKey,
            DataSource = context?.DataSource,
            ReadPreference = context?.ReadPreference ?? SqlReadPreference.Default
        });
        if (string.IsNullOrWhiteSpace(descriptor.ConnectionString) == false)
        {
            options.ConnectionString = descriptor.ConnectionString;
            options.Connection = null;
        }

        return options;
    }

    /// <summary>
    /// 获取模板 Sql 配置
    /// </summary>
    /// <param name="implementationType">实现类型</param>
    /// <returns>模板 Sql 配置</returns>
    protected SqlOptions GetTemplateOptions(Type implementationType) =>
        _serviceProvider.GetService(typeof(SqlOptions<>).MakeGenericType(implementationType)) as SqlOptions;

    /// <summary>
    /// 解析数据库描述信息
    /// </summary>
    /// <param name="context">数据库上下文</param>
    /// <param name="template">模板 Sql 配置</param>
    /// <returns>数据库描述信息</returns>
    protected DatabaseDescriptor ResolveDescriptor(DatabaseContext context, SqlOptions template)
    {
        var databaseContext = context ?? new DatabaseContext
        {
            DbKey = ConnectionStringCollection.DefaultConnectionStringName,
            DatabaseType = template?.DatabaseType ?? DatabaseType.SqlServer,
            Role = DatabaseRole.Default
        };
        var descriptor = _databaseDescriptorResolver?.Resolve(databaseContext) ?? new DatabaseDescriptor
        {
            DbKey = databaseContext.DbKey,
            DatabaseType = databaseContext.DatabaseType,
            Role = databaseContext.Role,
            ReadOnly = databaseContext.ReadOnly
        };
        if (string.IsNullOrWhiteSpace(descriptor.ConnectionString))
            descriptor.ConnectionString = template?.ConnectionString;
        if (string.IsNullOrWhiteSpace(descriptor.DbKey))
            descriptor.DbKey = databaseContext.DbKey;
        return descriptor;
    }

    /// <summary>
    /// 复制 Sql 配置
    /// </summary>
    /// <param name="source">源配置</param>
    /// <param name="target">目标配置</param>
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