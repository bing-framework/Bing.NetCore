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
    /// 初始化一个<see cref="SqlFactoryBase"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="databaseDescriptorResolver">数据库描述解析器</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    protected SqlFactoryBase(IServiceProvider serviceProvider,
        IDatabaseContextAccessor databaseContextAccessor = null,
        IDatabaseDescriptorResolver databaseDescriptorResolver = null,
        SqlMetadataOptions metadataOptions = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _databaseContextAccessor = databaseContextAccessor;
        _databaseDescriptorResolver = databaseDescriptorResolver;
        _metadataOptions = metadataOptions ?? new SqlMetadataOptions();
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
            MappingVersion = current?.MappingVersion
        };
    }

    /// <summary>
    /// 获取当前数据库上下文
    /// </summary>
    /// <param name="implementationType">实现类型</param>
    /// <returns>数据库上下文</returns>
    protected DatabaseContext GetCurrentContext(Type implementationType)
    {
        var template = GetTemplateOptions(implementationType);
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
                MappingVersion = current.MappingVersion
            };
        }

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
        var implementationType = GetImplementationType<TService>();
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
        var serviceType = typeof(TService);
        if (serviceType.IsAbstract == false && serviceType.IsInterface == false)
            return serviceType;
        var service = _serviceProvider.GetService<TService>();
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