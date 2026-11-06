using System.Data;
using System.Runtime.CompilerServices;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// 保存一个 SQL 数据源到 EF Core context 的显式映射。
/// </summary>
internal sealed class EfCoreSqlUnitOfWorkMapping
{
    /// <summary>
    /// 初始化一个 <see cref="EfCoreSqlUnitOfWorkMapping" /> 类型的实例。
    /// </summary>
    /// <param name="contextType">当前 EF Core 工作单元上下文的 CLR 类型。</param>
    /// <param name="dataSourceKey">用于选择数据库连接配置的数据源键。</param>
    public EfCoreSqlUnitOfWorkMapping(Type contextType, string dataSourceKey)
    {
        ContextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
        DataSourceKey = dataSourceKey ?? throw new ArgumentNullException(nameof(dataSourceKey));
    }

    /// <summary>
    /// 获取绑定的 EF Core 工作单元上下文类型。
    /// </summary>
    public Type ContextType { get; }
    /// <summary>
    /// 获取数据源在当前宿主的稳定键。
    /// </summary>
    public string DataSourceKey { get; }
}

/// <summary>
/// 为显式映射的数据源登记唯一 EF Core 连接资源。
/// </summary>
internal sealed class EfCoreSqlUnitOfWorkConnectionProvider : IUnitOfWorkConnectionProvider,
    IEfCoreSqlUnitOfWorkConnectionProvider
{
    /// <summary>
    /// 用于解析 EF Core 工作单元上下文及相关服务。
    /// </summary>
    private readonly IServiceProvider _serviceProvider;
    /// <summary>
    /// 保存 EF Core 上下文的 SQL 工作单元映射。
    /// </summary>
    private readonly IReadOnlyList<EfCoreSqlUnitOfWorkMapping> _mappings;
    /// <summary>
    /// 解析 EF Core 映射使用的数据源描述。
    /// </summary>
    private readonly ISqlDataSourceResolver _dataSourceResolver;
    /// <summary>
    /// 解析数据源对应的连接字符串。
    /// </summary>
    private readonly ISqlConnectionStringResolver _connectionStringResolver;
    /// <summary>
    /// 解析连接对应的数据库标识。
    /// </summary>
    private readonly ISqlDatabaseIdentityResolver _databaseIdentityResolver;
    /// <summary>
    /// 提供当前操作上下文的数据库上下文访问器。
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// 初始化一个 <see cref="EfCoreSqlUnitOfWorkConnectionProvider" /> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">用于解析当前测试服务的服务提供程序。</param>
    /// <param name="mappings">EF Core 工作单元的数据源映射配置集合。</param>
    /// <param name="dataSourceResolver">用于解析 SQL 数据源配置的解析器。</param>
    /// <param name="connectionStringResolver">用于解析数据库连接字符串的解析器。</param>
    /// <param name="databaseContextAccessor">用于访问当前数据库上下文的访问器。</param>
    /// <param name="databaseIdentityResolver">用于解析数据库身份的解析器。</param>
    public EfCoreSqlUnitOfWorkConnectionProvider(IServiceProvider serviceProvider,
        IEnumerable<EfCoreSqlUnitOfWorkMapping> mappings, ISqlDataSourceResolver dataSourceResolver = null,
        ISqlConnectionStringResolver connectionStringResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null,
        ISqlDatabaseIdentityResolver databaseIdentityResolver = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _mappings = (mappings ?? throw new ArgumentNullException(nameof(mappings))).ToArray();
        _dataSourceResolver = dataSourceResolver ?? new DefaultSqlDataSourceResolver();
        _connectionStringResolver = connectionStringResolver ?? new DefaultSqlConnectionStringResolver();
        _databaseIdentityResolver = databaseIdentityResolver ?? new DefaultSqlDatabaseIdentityResolver();
        _databaseContextAccessor = databaseContextAccessor;
    }

    /// <inheritdoc />
    public IUnitOfWorkConnectionResource GetResource(IUnitOfWorkScope scope, string dataSourceKey,
        string providerKey, string tenantId, string connectionString)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        if (string.IsNullOrWhiteSpace(dataSourceKey))
            return null;
        var mapping = FindMapping(dataSourceKey);
        if (mapping == null)
            return null;

        var dataSource = _dataSourceResolver.Resolve(dataSourceKey);
        var configuredConnectionString = _connectionStringResolver.Resolve(dataSource);
        if (!string.Equals(configuredConnectionString, connectionString, StringComparison.Ordinal))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 的冻结连接配置与 EF Core 映射配置不一致。");
        if (!string.IsNullOrWhiteSpace(dataSource.ProviderKey) &&
            !string.Equals(dataSource.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 的冻结 Provider 与 EF Core 映射配置不一致。");
        if (_databaseContextAccessor?.Current?.TenantId != null &&
            !string.Equals(_databaseContextAccessor.Current.TenantId, tenantId, StringComparison.Ordinal))
            throw new InvalidOperationException("SQL Query 的冻结租户与 EF Core context 当前租户不一致。");

        var context = ResolveContext(mapping);
        context.EnsureOperationRootMayUseContext(scope);
        ValidateDatabaseType(context, dataSource.DatabaseType);
        ValidateSourceAdmission(scope, context, dataSource, connectionString, isWrite: false);
        return CreateResource(scope, mapping, context, dataSource, providerKey, tenantId, connectionString);
    }

    /// <inheritdoc />
    public void EnsureMappedContextMatches(UnitOfWorkBase context, string dataSourceKey)
    {
        var mapping = FindMapping(dataSourceKey);
        if (mapping == null)
            return;
        if (!ReferenceEquals(context, ResolveContext(mapping)))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 映射的 EF Core context 与 Shared Query context 不是同一实例。");
    }

    /// <inheritdoc />
    public bool HasMappingForContext(UnitOfWorkBase context) =>
        context != null && _mappings.Any(mapping => mapping.ContextType.IsInstanceOfType(context));

    /// <inheritdoc />
    public void ValidateContextAdmission(IUnitOfWorkScope scope, UnitOfWorkBase context, bool isWrite)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        context.EnsureOperationRootMayUseContext(scope);

        var mappings = _mappings.Where(mapping => mapping.ContextType.IsInstanceOfType(context)).ToArray();
        if (mappings.Length == 0)
            return;
        var dataSourceKey = ResolveContextDataSourceKey(mappings);
        var mapping = mappings.SingleOrDefault(item => string.Equals(item.DataSourceKey, dataSourceKey,
            StringComparison.Ordinal));
        if (mapping == null)
            throw new InvalidOperationException("当前 EF Core context 的显式数据源映射与活动 SQL 数据源不一致。");
        if (!ReferenceEquals(context, ResolveContext(mapping)))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 映射的 EF Core context 与当前 context 不是同一实例。");

        var dataSource = _dataSourceResolver.Resolve(dataSourceKey);
        ValidateDatabaseType(context, dataSource.DatabaseType);
        var connectionString = _connectionStringResolver.Resolve(dataSource);
        ValidateSourceAdmission(scope, context, dataSource, connectionString, isWrite);
    }

    /// <inheritdoc />
    public IUnitOfWorkConnectionResource GetResourceForContext(IUnitOfWorkScope scope, UnitOfWorkBase context)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        context.EnsureOperationRootMayUseContext(scope);

        var mappings = _mappings.Where(mapping => mapping.ContextType.IsInstanceOfType(context)).ToArray();
        if (mappings.Length == 0)
            return null;
        var dataSourceKey = ResolveContextDataSourceKey(mappings);
        var mapping = mappings.SingleOrDefault(item => string.Equals(item.DataSourceKey, dataSourceKey,
            StringComparison.Ordinal));
        if (mapping == null)
            throw new InvalidOperationException("当前 EF Core context 的显式数据源映射与活动 SQL 数据源不一致。");
        if (!ReferenceEquals(context, ResolveContext(mapping)))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 映射的 EF Core context 与当前 context 不是同一实例。");

        var dataSource = _dataSourceResolver.Resolve(dataSourceKey);
        ValidateDatabaseType(context, dataSource.DatabaseType);
        var tenantId = _databaseContextAccessor?.Current?.TenantId;
        var providerKey = string.IsNullOrWhiteSpace(dataSource.ProviderKey)
            ? GetOfficialProviderKey(dataSource.DatabaseType)
            : dataSource.ProviderKey;
        var connectionString = _connectionStringResolver.Resolve(dataSource);
        ValidateSourceAdmission(scope, context, dataSource, connectionString, isWrite: true);
        return CreateResource(scope, mapping, context, dataSource, providerKey, tenantId, connectionString);
    }

    /// <summary>
    /// 校验 EF Core 上下文加入 SQL 工作单元的条件。
    /// </summary>
    /// <param name="scope">当前业务工作单元作用域。</param>
    /// <param name="context">参与操作的 EF Core 上下文。</param>
    /// <param name="dataSource">SQL 数据源描述信息。</param>
    /// <param name="configuredConnectionString">数据源配置的连接字符串。</param>
    /// <param name="isWrite">执行写操作时为 true，仅用于读取时为 false。</param>
    /// <remarks>校验事务能力、读写准入、主库策略和物理数据库身份；不在执行阶段重定向数据源。</remarks>
    private void ValidateSourceAdmission(IUnitOfWorkScope scope, UnitOfWorkBase context,
        SqlDataSourceDescriptor dataSource, string configuredConnectionString, bool isWrite)
    {
        var dataSourceKey = dataSource.Key ?? "<default>";
        if (scope.Options.IsTransactional)
        {
            if (dataSource.IsReadOnly)
                throw new InvalidOperationException($"只读 SQL 数据源 {dataSourceKey} 不能参与事务工作单元。");
            if (!dataSource.SupportsTransactions)
                throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 不支持本地事务。");
        }
        else if (dataSource.PrimaryReadStrategy == PrimaryReadStrategy.Transaction)
        {
            throw new InvalidOperationException($"非事务工作单元不能使用 PrimaryReadStrategy.Transaction：{dataSourceKey}。");
        }

        if (isWrite && dataSource.IsReadOnly)
            throw new InvalidOperationException($"只读 SQL 数据源 {dataSourceKey} 不允许 EF Core 保存。");
        if ((scope.Options.IsTransactional || isWrite) &&
            dataSource.PrimaryReadStrategy == PrimaryReadStrategy.PrimaryDataSource &&
            !string.IsNullOrWhiteSpace(dataSource.PrimaryDataSourceKey) &&
            !string.Equals(dataSource.Key, dataSource.PrimaryDataSourceKey, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"冻结的 SQL 数据源 {dataSourceKey} 是只读副本，当前工作单元不能在执行阶段重定向到主库。");

        var connection = context.Database.GetDbConnection();
        var actualConnectionString = connection.ConnectionString;
        if (string.IsNullOrWhiteSpace(configuredConnectionString) || string.IsNullOrWhiteSpace(actualConnectionString))
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 无法校验 EF Core 物理数据库身份。");

        SqlDatabaseIdentity configuredIdentity;
        SqlDatabaseIdentity contextIdentity;
        try
        {
            configuredIdentity = _databaseIdentityResolver.Resolve(dataSource.DatabaseType, configuredConnectionString);
            contextIdentity = _databaseIdentityResolver.Resolve(dataSource.DatabaseType, actualConnectionString);
        }
        catch
        {
            if (string.Equals(configuredConnectionString, actualConnectionString, StringComparison.Ordinal))
                return;
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 与 EF Core context 的物理数据库身份无法比较。");
        }

        if (configuredIdentity?.IsComparable == true && contextIdentity?.IsComparable == true &&
            !configuredIdentity.IsExclusiveMemory && !contextIdentity.IsExclusiveMemory &&
            configuredIdentity.Equals(contextIdentity))
            return;

        if (string.Equals(configuredConnectionString, actualConnectionString, StringComparison.Ordinal))
            return;
        throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 与 EF Core context 指向不同或不可比较的物理数据库。");
    }

    /// <summary>
    /// 获取或创建当前作用域的 EF Core SQL 连接资源。
    /// </summary>
    /// <param name="scope">当前业务工作单元作用域。</param>
    /// <param name="mapping">数据源与 EF Core 上下文的映射。</param>
    /// <param name="context">参与操作的 EF Core 上下文。</param>
    /// <param name="dataSource">SQL 数据源描述信息。</param>
    /// <param name="providerKey">SQL Provider 注册键。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <param name="connectionString">当前数据源的有效连接字符串。</param>
    /// <returns>作用域内与资源键匹配的连接资源。</returns>
    private IUnitOfWorkConnectionResource CreateResource(IUnitOfWorkScope scope,
        EfCoreSqlUnitOfWorkMapping mapping, UnitOfWorkBase context, SqlDataSourceDescriptor dataSource,
        string providerKey, string tenantId, string connectionString)
    {
        var key = new EfCoreSqlUnitOfWorkResourceKey(mapping, context, dataSource.Key, dataSource.DatabaseType,
            providerKey, tenantId, connectionString);
        return scope.GetOrAddResource(key,
            () => new EfCoreSqlUnitOfWorkResource(key, scope, context));
    }

    /// <summary>
    /// 解析数据源映射指定的 EF Core 上下文。
    /// </summary>
    /// <param name="mapping">数据源与 EF Core 上下文的映射。</param>
    /// <returns>当前依赖注入作用域内的上下文实例；未注册时抛出异常。</returns>
    private UnitOfWorkBase ResolveContext(EfCoreSqlUnitOfWorkMapping mapping)
    {
        var context = _serviceProvider.GetService(mapping.ContextType) as UnitOfWorkBase;
        return context ?? throw new InvalidOperationException(
            $"SQL 数据源 {mapping.DataSourceKey} 映射的 EF Core context {mapping.ContextType.FullName} 未注册到当前 DI scope。");
    }

    /// <summary>
    /// 查找指定数据源的 EF Core 上下文映射。
    /// </summary>
    /// <param name="dataSourceKey">SQL 数据源注册键。</param>
    /// <returns>唯一匹配的映射；没有匹配时为 null，存在多个匹配时抛出异常。</returns>
    private EfCoreSqlUnitOfWorkMapping FindMapping(string dataSourceKey)
    {
        var matches = _mappings.Where(mapping => string.Equals(mapping.DataSourceKey, dataSourceKey,
            StringComparison.Ordinal)).ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException($"SQL 数据源 {dataSourceKey} 存在多个 EF Core context 映射。");
        return matches.SingleOrDefault();
    }

    /// <summary>
    /// 解析当前上下文使用的数据源键。
    /// </summary>
    /// <param name="mappings">当前上下文可用的数据源映射集合。</param>
    /// <returns>环境数据源键、唯一映射键或已映射的默认键；无法确定时为 null。</returns>
    private string ResolveContextDataSourceKey(IReadOnlyList<EfCoreSqlUnitOfWorkMapping> mappings)
    {
        var ambientKey = _databaseContextAccessor?.Current?.DbKey;
        if (!string.IsNullOrWhiteSpace(ambientKey))
            return ambientKey;
        if (mappings.Count == 1)
            return mappings[0].DataSourceKey;

        var defaultKey = _dataSourceResolver.Resolve().Key;
        return mappings.Any(mapping => string.Equals(mapping.DataSourceKey, defaultKey, StringComparison.Ordinal))
            ? defaultKey
            : null;
    }

    /// <summary>
    /// 获取数据库类型对应的官方 SQL Provider 键。
    /// </summary>
    /// <param name="databaseType">SQL 数据库类型。</param>
    /// <returns>受支持数据库对应的 Provider 注册键；未支持的类型抛出异常。</returns>
    private static string GetOfficialProviderKey(DatabaseType databaseType) => databaseType switch
    {
        DatabaseType.MySql => "bing.mysql",
        DatabaseType.PgSql => "bing.postgresql",
        DatabaseType.SqlServer => "bing.sqlserver",
        DatabaseType.Sqlite => "bing.sqlite",
        DatabaseType.Oracle => "bing.oracle",
        _ => throw new NotSupportedException($"未能为数据库类型 {databaseType} 解析 EF Core SQL Provider。")
    };

    /// <summary>
    /// 校验 EF Core Provider 与数据源的数据库类型一致。
    /// </summary>
    /// <param name="context">参与操作的 EF Core 上下文。</param>
    /// <param name="dataSourceDatabaseType">数据源声明的数据库类型。</param>
    private static void ValidateDatabaseType(UnitOfWorkBase context, DatabaseType dataSourceDatabaseType)
    {
        var providerName = context.Database.ProviderName;
        var contextDatabaseType = providerName switch
        {
            "Microsoft.EntityFrameworkCore.Sqlite" => DatabaseType.Sqlite,
            "Microsoft.EntityFrameworkCore.SqlServer" => DatabaseType.SqlServer,
            "Pomelo.EntityFrameworkCore.MySql" or "MySql.EntityFrameworkCore" => DatabaseType.MySql,
            "Npgsql.EntityFrameworkCore.PostgreSQL" => DatabaseType.PgSql,
            "Oracle.EntityFrameworkCore" => DatabaseType.Oracle,
            _ => throw new NotSupportedException($"不支持 EF Core Provider {providerName ?? "<未指定>"} 的 SQL 工作单元映射。")
        };
        if (contextDatabaseType != dataSourceDatabaseType)
            throw new InvalidOperationException($"SQL 数据源数据库类型 {dataSourceDatabaseType} 与 EF Core Provider 类型 {contextDatabaseType} 不一致。");
    }
}

/// <summary>
/// EF Core 映射 provider 的内部校验入口。
/// </summary>
internal interface IEfCoreSqlUnitOfWorkConnectionProvider
{
    /// <summary>
    /// 验证工作单元与数据源映射是否一致。
    /// </summary>
    /// <param name="context">本次操作使用的上下文。</param>
    /// <param name="dataSourceKey">目标数据源映射键。</param>
    void EnsureMappedContextMatches(UnitOfWorkBase context, string dataSourceKey);
    /// <summary>
    /// 检查工作单元是否存在数据源映射。
    /// </summary>
    /// <param name="context">本次操作使用的上下文。</param>
    /// <returns>存在适用于该工作单元的映射时为 true，否则为 false。</returns>
    bool HasMappingForContext(UnitOfWorkBase context);
    /// <summary>
    /// 验证工作单元能否加入当前操作作用域。
    /// </summary>
    /// <param name="scope">操作所属的隔离范围。</param>
    /// <param name="context">本次操作使用的上下文。</param>
    /// <param name="isWrite">为 true 时执行写入准入校验，否则校验读取。</param>
    void ValidateContextAdmission(IUnitOfWorkScope scope, UnitOfWorkBase context, bool isWrite);
    /// <summary>
    /// 获取作用域内工作单元的连接资源。
    /// </summary>
    /// <param name="scope">操作所属的隔离范围。</param>
    /// <param name="context">本次操作使用的上下文。</param>
    /// <returns>当前工作单元对应的连接资源。</returns>
    IUnitOfWorkConnectionResource GetResourceForContext(IUnitOfWorkScope scope, UnitOfWorkBase context);
}

/// <summary>
/// 标识一个 EF context 当前租用的 root 和数据源资源。
/// </summary>
internal sealed class EfCoreSqlUnitOfWorkResourceKey : IEquatable<EfCoreSqlUnitOfWorkResourceKey>
{
    /// <summary>
    /// 保存资源键对应的 EF Core 工作单元映射。
    /// </summary>
    private readonly EfCoreSqlUnitOfWorkMapping _mapping;
    /// <summary>
    /// 保存资源键所属的工作单元实例。
    /// </summary>
    private readonly UnitOfWorkBase _context;
    /// <summary>
    /// 保存资源键的数据源键。
    /// </summary>
    private readonly string _dataSourceKey;
    /// <summary>
    /// 保存资源键的数据库类型。
    /// </summary>
    private readonly DatabaseType _databaseType;
    /// <summary>
    /// 保存资源键的提供程序键。
    /// </summary>
    private readonly string _providerKey;
    /// <summary>
    /// 保存资源键的租户标识。
    /// </summary>
    private readonly string _tenantId;
    /// <summary>
    /// 保存资源键的连接字符串。
    /// </summary>
    private readonly string _connectionString;

    /// <summary>
    /// 初始化一个 <see cref="EfCoreSqlUnitOfWorkResourceKey" /> 类型的实例。
    /// </summary>
    /// <param name="mapping">当前 EF Core 工作单元对应的映射配置。</param>
    /// <param name="context">当前操作关联的上下文或工作单元。</param>
    /// <param name="dataSourceKey">用于选择数据库连接配置的数据源键。</param>
    /// <param name="databaseType">当前连接使用的数据库类型。</param>
    /// <param name="providerKey">数据库提供程序的规范化键。</param>
    /// <param name="tenantId">租户标识。</param>
    /// <param name="connectionString">用于建立数据库连接的连接字符串。</param>
    public EfCoreSqlUnitOfWorkResourceKey(EfCoreSqlUnitOfWorkMapping mapping, UnitOfWorkBase context,
        string dataSourceKey, DatabaseType databaseType, string providerKey, string tenantId,
        string connectionString)
    {
        _mapping = mapping;
        _context = context;
        _dataSourceKey = dataSourceKey;
        _databaseType = databaseType;
        _providerKey = providerKey?.Trim().ToLowerInvariant();
        _tenantId = tenantId;
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public bool Equals(EfCoreSqlUnitOfWorkResourceKey other) => other != null &&
        ReferenceEquals(_mapping, other._mapping) && ReferenceEquals(_context, other._context) &&
        string.Equals(_dataSourceKey, other._dataSourceKey, StringComparison.Ordinal) &&
        _databaseType == other._databaseType &&
        string.Equals(_providerKey, other._providerKey, StringComparison.Ordinal) &&
        string.Equals(_tenantId, other._tenantId, StringComparison.Ordinal) &&
        string.Equals(_connectionString, other._connectionString, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object obj) => Equals(obj as EfCoreSqlUnitOfWorkResourceKey);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = RuntimeHelpers.GetHashCode(_mapping);
            hash = hash * 31 + RuntimeHelpers.GetHashCode(_context);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_dataSourceKey ?? string.Empty);
            hash = hash * 31 + _databaseType.GetHashCode();
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_providerKey ?? string.Empty);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_tenantId ?? string.Empty);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_connectionString ?? string.Empty);
            return hash;
        }
    }
}

/// <summary>
/// 在业务工作单元中独占管理 EF Core 连接和本地事务。
/// </summary>
internal sealed class EfCoreSqlUnitOfWorkResource : IUnitOfWorkConnectionResource
{
    /// <summary>
    /// 串行化 EF Core 连接资源的准备、完成和释放。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);
    /// <summary>
    /// 保存连接资源所属的工作单元作用域。
    /// </summary>
    private readonly IUnitOfWorkScope _scope;
    /// <summary>
    /// 保存连接资源对应的工作单元实例。
    /// </summary>
    private readonly UnitOfWorkBase _context;
    /// <summary>
    /// 保存连接资源操作的数据库门面。
    /// </summary>
    private readonly DatabaseFacade _database;
    /// <summary>
    /// 保存连接资源当前的数据库事务。
    /// </summary>
    private IDbContextTransaction _transaction;
    /// <summary>
    /// 指示连接资源是否已完成准备。
    /// </summary>
    private bool _prepared;
    /// <summary>
    /// 指示数据库连接是否由本资源打开。
    /// </summary>
    private bool _openedHere;
    /// <summary>
    /// 指示连接资源是否已完成提交或回滚。
    /// </summary>
    private bool _completed;
    /// <summary>
    /// 指示连接资源是否已提交。
    /// </summary>
    private bool _committed;
    /// <summary>
    /// 指示连接资源是否已回滚。
    /// </summary>
    private bool _rolledBack;
    /// <summary>
    /// 指示连接资源是否进入失败状态。
    /// </summary>
    private bool _failed;
    /// <summary>
    /// 指示连接资源是否已释放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 初始化一个 <see cref="EfCoreSqlUnitOfWorkResource" /> 类型的实例。
    /// </summary>
    /// <param name="resourceKey">用于识别当前工作单元资源的键。</param>
    /// <param name="scope">实例所属的作用范围。</param>
    /// <param name="context">当前操作关联的上下文或工作单元。</param>
    public EfCoreSqlUnitOfWorkResource(object resourceKey, IUnitOfWorkScope scope, UnitOfWorkBase context)
    {
        ResourceKey = resourceKey ?? throw new ArgumentNullException(nameof(resourceKey));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _database = context.Database;
        Connection = _database.GetDbConnection();
        EfCoreSqlUnitOfWorkContextRegistry.Claim(context, scope.Id, resourceKey,
            context.ServiceProvider.GetService<IDatabaseContextAccessor>()?.Current?.TenantId);
        _context.OnMappedSqlResourceCreated(scope.Id, resourceKey);
    }

    /// <inheritdoc />
    public object ResourceKey { get; }
    /// <inheritdoc />
    public IDbConnection Connection { get; }
    /// <inheritdoc />
    public IDbTransaction Transaction => _transaction?.GetDbTransaction();

    /// <inheritdoc />
    public void EnsureReady()
    {
        _gate.Wait();
        try
        {
            EnsureNotDisposed();
            if (_prepared)
                return;
            if (_failed)
                throw new InvalidOperationException("EF Core 工作单元资源准备失败，不能重试。");
            ValidateNoForeignTransaction();
            try
            {
                if (Connection.State != ConnectionState.Open)
                {
                    _database.OpenConnection();
                    _openedHere = true;
                }
                if (_scope.Options.IsTransactional)
                    _transaction = _scope.Options.IsolationLevel == null
                        ? _database.BeginTransaction()
                        : _database.BeginTransaction(_scope.Options.IsolationLevel.Value);
                _prepared = true;
            }
            catch (Exception exception)
            {
                FailPreparation(exception);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _scope.SetRollbackOnly();
            throw;
        }
        try
        {
            EnsureNotDisposed();
            if (_prepared)
                return;
            if (_failed)
                throw new InvalidOperationException("EF Core 工作单元资源准备失败，不能重试。");
            ValidateNoForeignTransaction();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Connection.State != ConnectionState.Open)
                {
                    await _database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                    _openedHere = true;
                }
                if (_scope.Options.IsTransactional)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _transaction = _scope.Options.IsolationLevel == null
                        ? await _database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
                        : await _database.BeginTransactionAsync(_scope.Options.IsolationLevel.Value,
                            cancellationToken).ConfigureAwait(false);
                }
                _prepared = true;
            }
            catch (Exception exception)
            {
                await FailPreparationAsync(exception).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Commit()
    {
        EnsureNotDisposed();
        if (_completed)
            return;
        _transaction?.Commit();
        _committed = true;
        _completed = true;
        _context.OnMappedSqlResourceCompleted(_scope.Id, ResourceKey, committed: true, rolledBack: false);
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (_completed)
            return;
        if (_transaction != null)
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _committed = true;
        _completed = true;
        _context.OnMappedSqlResourceCompleted(_scope.Id, ResourceKey, committed: true, rolledBack: false);
    }

    /// <inheritdoc />
    public void Rollback()
    {
        EnsureNotDisposed();
        if (_completed)
            return;
        _transaction?.Rollback();
        _rolledBack = true;
        _completed = true;
        _context.OnMappedSqlResourceCompleted(_scope.Id, ResourceKey, committed: false, rolledBack: true);
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (_completed)
            return;
        if (_transaction != null)
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        _rolledBack = true;
        _completed = true;
        _context.OnMappedSqlResourceCompleted(_scope.Id, ResourceKey, committed: false, rolledBack: true);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        var errors = new List<Exception>();
        Capture(errors, () => _transaction?.Dispose());
        if (_openedHere)
            Capture(errors, _database.CloseConnection);
        EfCoreSqlUnitOfWorkContextRegistry.Release(_context, _scope.Id, ResourceKey);
        _context.OnMappedSqlResourceDisposed(_scope.Id, ResourceKey, _committed, _rolledBack);
        ThrowCleanup(errors);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        var errors = new List<Exception>();
        if (_transaction != null)
            await CaptureAsync(errors, () => _transaction.DisposeAsync().AsTask()).ConfigureAwait(false);
        if (_openedHere)
            await CaptureAsync(errors, () => _database.CloseConnectionAsync()).ConfigureAwait(false);
        EfCoreSqlUnitOfWorkContextRegistry.Release(_context, _scope.Id, ResourceKey);
        _context.OnMappedSqlResourceDisposed(_scope.Id, ResourceKey, _committed, _rolledBack);
        ThrowCleanup(errors);
    }

    /// <summary>
    /// 拒绝上下文已有事务或环境事务的连接映射。
    /// </summary>
    private void ValidateNoForeignTransaction()
    {
        if (_database.CurrentTransaction != null)
            throw new InvalidOperationException("EF Core context 已有非当前业务工作单元创建的事务，不能映射为新资源。");
        if (System.Transactions.Transaction.Current != null)
            throw new InvalidOperationException("当前存在环境事务，不能将 EF Core context 映射到新的本地事务资源。");
    }

    /// <summary>
    /// 清理准备失败的连接资源并传播原始异常。
    /// </summary>
    /// <param name="primary">准备过程产生的原始异常。</param>
    /// <remarks>将作用域标记为仅回滚；清理异常与原始异常一起传播。</remarks>
    private void FailPreparation(Exception primary)
    {
        _failed = true;
        _scope.SetRollbackOnly();
        var errors = new List<Exception>();
        Capture(errors, () => _transaction?.Dispose());
        _transaction = null;
        if (_openedHere)
        {
            Capture(errors, _database.CloseConnection);
            _openedHere = false;
        }
        ThrowWithPrimary(primary, errors);
    }

    /// <summary>
    /// 清理准备失败的连接资源并传播原始异常。
    /// </summary>
    /// <param name="primary">准备过程产生的原始异常。</param>
    /// <remarks>将作用域标记为仅回滚；清理异常与原始异常一起传播。</remarks>
    private async Task FailPreparationAsync(Exception primary)
    {
        _failed = true;
        _scope.SetRollbackOnly();
        var errors = new List<Exception>();
        if (_transaction != null)
            await CaptureAsync(errors, () => _transaction.DisposeAsync().AsTask()).ConfigureAwait(false);
        _transaction = null;
        if (_openedHere)
        {
            await CaptureAsync(errors, () => _database.CloseConnectionAsync()).ConfigureAwait(false);
            _openedHere = false;
        }
        ThrowWithPrimary(primary, errors);
    }

    /// <summary>
    /// 检查连接资源尚未释放。
    /// </summary>
    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EfCoreSqlUnitOfWorkResource));
    }

    /// <summary>
    /// 执行清理动作并收集异常。
    /// </summary>
    /// <param name="errors">用于收集或传播清理异常的集合。</param>
    /// <param name="action">待执行的操作。</param>
    private static void Capture(ICollection<Exception> errors, Action action)
    {
        try { action(); }
        catch (Exception exception) { errors.Add(exception); }
    }

    /// <summary>
    /// 执行清理动作并收集异常。
    /// </summary>
    /// <param name="errors">用于收集或传播清理异常的集合。</param>
    /// <param name="action">待执行的操作。</param>
    private static async Task CaptureAsync(ICollection<Exception> errors, Func<Task> action)
    {
        try { await action().ConfigureAwait(false); }
        catch (Exception exception) { errors.Add(exception); }
    }

    /// <summary>
    /// 传播原始异常及收集到的清理异常。
    /// </summary>
    /// <param name="primary">准备过程产生的原始异常。</param>
    /// <param name="errors">用于收集或传播清理异常的集合。</param>
    /// <remarks>没有清理异常时保留原始异常的堆栈；否则将原始异常置于聚合异常首项。</remarks>
    private static void ThrowWithPrimary(Exception primary, ICollection<Exception> errors)
    {
        if (errors.Count == 0)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        var all = new List<Exception> { primary };
        all.AddRange(errors);
        throw new AggregateException(all);
    }

    /// <summary>
    /// 传播收集到的清理异常。
    /// </summary>
    /// <param name="errors">用于收集或传播清理异常的集合。</param>
    /// <remarks>单个异常保留其堆栈，多个异常合并传播，没有异常时直接返回。</remarks>
    private static void ThrowCleanup(ICollection<Exception> errors)
    {
        if (errors.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors.First()).Throw();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }
}

/// <summary>
/// 防止同一个 DbContext 被多个活动 root 或多个数据源租用。
/// </summary>
internal static class EfCoreSqlUnitOfWorkContextRegistry
{
    /// <summary>
    /// 同步已解析工作单元上下文的登记与快照读取。
    /// </summary>
    private static readonly object Sync = new();
    /// <summary>
    /// 保存工作单元到上下文占用记录的弱引用表。
    /// </summary>
    private static readonly ConditionalWeakTable<UnitOfWorkBase, Reservation> Reservations = new();

    /// <summary>
    /// 为工作单元登记上下文资源占用。
    /// </summary>
    /// <param name="context">参与操作的 EF Core 上下文。</param>
    /// <param name="scopeId">占用上下文的工作单元作用域标识。</param>
    /// <param name="resourceKey">连接资源的标识键。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <remarks>相同作用域、资源键和租户的重复占用不产生新登记；其他占用冲突抛出异常。</remarks>
    public static void Claim(UnitOfWorkBase context, Guid scopeId, object resourceKey, string tenantId)
    {
        lock (Sync)
        {
            if (Reservations.TryGetValue(context, out var current))
            {
                if (current.ScopeId == scopeId && Equals(current.ResourceKey, resourceKey) &&
                    string.Equals(current.TenantId, tenantId, StringComparison.Ordinal))
                    return;
                throw new InvalidOperationException("同一个 EF Core context 不能同时加入不同工作单元、数据源或租户资源。");
            }
            Reservations.Add(context, new Reservation(scopeId, resourceKey, tenantId));
        }
    }

    /// <summary>
    /// 释放匹配工作单元和资源键的上下文占用。
    /// </summary>
    /// <param name="context">参与操作的 EF Core 上下文。</param>
    /// <param name="scopeId">占用上下文的工作单元作用域标识。</param>
    /// <param name="resourceKey">连接资源的标识键。</param>
    public static void Release(UnitOfWorkBase context, Guid scopeId, object resourceKey)
    {
        lock (Sync)
        {
            if (Reservations.TryGetValue(context, out var current) && current.ScopeId == scopeId &&
                Equals(current.ResourceKey, resourceKey))
                Reservations.Remove(context);
        }
    }

    /// <summary>
    /// EF Core 数据源绑定的工作单元预留信息。
    /// </summary>
    private sealed class Reservation
    {
        /// <summary>
        /// 初始化一个 <see cref="Reservation" /> 类型的实例。
        /// </summary>
        /// <param name="scopeId">当前工作单元范围的标识。</param>
        /// <param name="resourceKey">用于识别当前工作单元资源的键。</param>
        /// <param name="tenantId">租户标识。</param>
        public Reservation(Guid scopeId, object resourceKey, string tenantId)
        {
            ScopeId = scopeId;
            ResourceKey = resourceKey;
            TenantId = tenantId;
        }

        /// <summary>
        /// 获取工作单元作用域标识。
        /// </summary>
        public Guid ScopeId { get; }
        /// <summary>
        /// 获取数据源资源绑定键。
        /// </summary>
        public object ResourceKey { get; }
        /// <summary>
        /// 获取所属租户标识。
        /// </summary>
        public string TenantId { get; }
    }
}
