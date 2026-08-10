using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// EF Core SQL 查询工厂
/// </summary>
public sealed class EfCoreSqlQueryFactory : IEfCoreSqlQueryFactory
{
    /// <summary>
    /// SQL 查询对象工厂
    /// </summary>
    private readonly ISqlQueryFactory _queryFactory;

    /// <summary>
    /// 数据库上下文访问器
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// SQL 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _metadataOptions;

    /// <summary>
    /// 数据库类型转换器解析器
    /// </summary>
    private readonly ITypeConverterResolver _typeConverterResolver;

    /// <summary>
    /// 独立数据库连接工厂解析器
    /// </summary>
    private readonly ISqlDbConnectionFactoryResolver _connectionFactoryResolver;

    /// <summary>
    /// SQL 连接字符串解析器
    /// </summary>
    private readonly ISqlConnectionStringResolver _connectionStringResolver;

    /// <summary>
    /// SQL 数据源解析器。
    /// </summary>
    private readonly ISqlDataSourceResolver _dataSourceResolver;

    /// <summary>
    /// SQL 数据库物理身份解析器。
    /// </summary>
    private readonly ISqlDatabaseIdentityResolver _databaseIdentityResolver;

    /// <summary>
    /// 初始化一个<see cref="EfCoreSqlQueryFactory"/>类型的实例
    /// </summary>
    /// <param name="queryFactory">SQL 查询对象工厂</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">SQL 元数据配置</param>
    /// <param name="typeConverterResolver">数据库类型转换器解析器</param>
    /// <param name="connectionFactoryResolver">独立数据库连接工厂解析器</param>
    /// <param name="connectionStringResolver">SQL 连接字符串解析器</param>
    /// <param name="dataSourceResolver">SQL 数据源解析器</param>
    /// <param name="databaseIdentityResolver">SQL 数据库物理身份解析器。</param>
    public EfCoreSqlQueryFactory(ISqlQueryFactory queryFactory,
        IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions metadataOptions = null,
        ITypeConverterResolver typeConverterResolver = null,
        ISqlDbConnectionFactoryResolver connectionFactoryResolver = null,
        ISqlConnectionStringResolver connectionStringResolver = null,
        ISqlDataSourceResolver dataSourceResolver = null,
        ISqlDatabaseIdentityResolver databaseIdentityResolver = null)
    {
        _queryFactory = queryFactory ?? throw new ArgumentNullException(nameof(queryFactory));
        _databaseContextAccessor = databaseContextAccessor;
        _metadataOptions = metadataOptions ?? new SqlMetadataOptions();
        _typeConverterResolver = typeConverterResolver;
        _connectionFactoryResolver = connectionFactoryResolver;
        _connectionStringResolver = connectionStringResolver ?? new DefaultSqlConnectionStringResolver();
        _dataSourceResolver = dataSourceResolver ?? new DefaultSqlDataSourceResolver(_metadataOptions);
        _databaseIdentityResolver = databaseIdentityResolver ?? new DefaultSqlDatabaseIdentityResolver();
    }

    /// <inheritdoc />
    public ISqlQuery Create(UnitOfWorkBase unitOfWork, EfCoreSqlConnectionMode mode = EfCoreSqlConnectionMode.Shared,
        string dbKey = null)
    {
        if (unitOfWork == null)
            throw new ArgumentNullException(nameof(unitOfWork));
        var databaseType = ResolveDatabaseType(unitOfWork);
        var dataSource = ResolveDataSource(databaseType, dbKey);
        if (mode == EfCoreSqlConnectionMode.Shared)
            EnsureSharedConnectionMatches(unitOfWork, dataSource);
        var query = CreateQuery(dataSource);
        try
        {
            BindEntityMetadata(query, unitOfWork);
            ApplyDatabaseContext(query, dataSource);
            if (mode == EfCoreSqlConnectionMode.Independent)
            {
                var connectionString = ResolveIndependentConnectionString(dataSource);
                var connection = CreateIndependentConnection(dataSource, connectionString);
                try
                {
                    SqlQueryRuntimeBridge.BindOwnedConnection(query, connection, SqlConnectionSource.DataSource);
                }
                catch
                {
                    connection.Dispose();
                    throw;
                }
                return query;
            }
            SqlQueryRuntimeBridge.BindExternalConnection(query, unitOfWork.Database.GetDbConnection(),
                SqlConnectionSource.EntityFrameworkCore);
            SqlQueryRuntimeBridge.BindExternalTransactionResolver(query,
                () => unitOfWork.Database.CurrentTransaction?.GetDbTransaction());
            return query;
        }
        catch
        {
            query.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 应用数据库上下文
    /// </summary>
    /// <param name="query">SQL 查询对象</param>
    /// <param name="dataSource">数据源</param>
    private void ApplyDatabaseContext(ISqlQuery query, SqlDataSourceDescriptor dataSource)
    {
        if (query == null || dataSource == null)
            return;
        var current = _databaseContextAccessor?.Current;
        SqlQueryRuntimeBridge.BindDatabaseContext(query, new DatabaseContext
        {
            DbKey = dataSource.Key,
            DataSource = dataSource,
            TenantId = current?.TenantId,
            MappingProfile = dataSource.MappingProfile ?? current?.MappingProfile,
            ReadPreference = current?.ReadPreference ?? SqlReadPreference.Default
        });
    }

    /// <summary>
    /// 绑定 EF Core 实体元数据
    /// </summary>
    /// <param name="query">SQL 查询对象</param>
    /// <param name="unitOfWork">工作单元</param>
    private void BindEntityMetadata(ISqlQuery query, UnitOfWorkBase unitOfWork)
    {
        var metadataProvider = new CompositeEntityModelMetadataProvider(new IEntityModelMetadataProvider[]
        {
            new EfCoreEntityModelMetadataProvider(unitOfWork.Model)
        });
        SqlQueryRuntimeBridge.BindEntityMappingResolver(query, new DefaultEntityMappingResolver(
            databaseContextAccessor: _databaseContextAccessor, options: _metadataOptions,
            typeConverterResolver: _typeConverterResolver, entityModelMetadataProvider: metadataProvider));
    }

    /// <summary>
    /// 基于 EF Core Provider 创建独立连接
    /// </summary>
    /// <param name="dataSource">已解析 SQL 数据源。</param>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>独立数据库连接</returns>
    private System.Data.IDbConnection CreateIndependentConnection(SqlDataSourceDescriptor dataSource,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("EF Core 数据库连接字符串不能为空，无法创建独立 SQL 查询连接。");
        if (_connectionFactoryResolver == null)
            throw new InvalidOperationException("未注册独立 SQL 数据库连接工厂解析器。");
        var providerKey = dataSource?.ProviderKey?.Trim() ?? ResolveOfficialProviderKey(dataSource?.DatabaseType);
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new InvalidOperationException(
                "EF Core Independent 模式的数据源必须配置 SQL Provider Key，不能仅使用数据库类型解析独立连接工厂。");
        return _connectionFactoryResolver.Create(providerKey, connectionString);
    }

    /// <summary>
    /// 解析官方数据库类型对应的 Provider Key。
    /// </summary>
    /// <param name="databaseType">数据源声明的数据库类型。</param>
    /// <returns>官方 Provider Key；未知或自定义类型返回 <c>null</c>。</returns>
    private static string ResolveOfficialProviderKey(DatabaseType? databaseType) => databaseType switch
    {
        DatabaseType.MySql or DatabaseType.Doris => "bing.mysql",
        DatabaseType.PgSql => "bing.postgresql",
        DatabaseType.SqlServer => "bing.sqlserver",
        DatabaseType.Sqlite => "bing.sqlite",
        DatabaseType.Oracle => "bing.oracle",
        _ => null
    };

    /// <summary>
    /// 解析独立连接字符串
    /// </summary>
    /// <param name="dataSource">数据源</param>
    /// <returns>连接字符串</returns>
    private string ResolveIndependentConnectionString(SqlDataSourceDescriptor dataSource)
    {
        return _connectionStringResolver.Resolve(dataSource);
    }

    /// <summary>
    /// 确保 Shared 模式的最终数据源与当前 DbContext 指向同一物理数据库
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="dataSource">最终解析的数据源。</param>
    private void EnsureSharedConnectionMatches(UnitOfWorkBase unitOfWork, SqlDataSourceDescriptor dataSource)
    {
        var dataSourceConnectionString = _connectionStringResolver.Resolve(dataSource);
        var dbContextConnectionString = unitOfWork.Database.GetDbConnection().ConnectionString;
        var dataSourceIdentity = _databaseIdentityResolver.Resolve(dataSource.DatabaseType, dataSourceConnectionString);
        var dbContextIdentity = _databaseIdentityResolver.Resolve(dataSource.DatabaseType, dbContextConnectionString);
        if (dataSourceIdentity.IsExclusiveMemory || dbContextIdentity.IsExclusiveMemory)
            throw new InvalidOperationException(
                "SQLite 独占内存数据库不能安全地用于 Shared 模式。请使用 Independent 模式或配置命名的 file: 共享内存数据库。");
        if (dataSourceIdentity.IsComparable == false || dbContextIdentity.IsComparable == false)
            throw new InvalidOperationException(
                "SQL 数据源或当前 DbContext 的物理身份无法安全比较，Shared 模式不能复用该连接，请使用 Independent 模式或配置可解析的连接终结点。");
        if (dataSourceIdentity.Equals(dbContextIdentity))
            return;
        throw new InvalidOperationException(
            $"SQL 数据源 {dataSource.Key} 与当前 DbContext 指向不同的物理数据库，Shared 模式不能复用该连接，请使用 {nameof(EfCoreSqlConnectionMode.Independent)} 模式。");
    }

    /// <summary>
    /// 解析 SQL 数据源
    /// </summary>
    /// <param name="databaseType">数据库类型</param>
    /// <param name="dbKey">数据源标识</param>
    /// <returns>SQL 数据源</returns>
    private SqlDataSourceDescriptor ResolveDataSource(DatabaseType databaseType, string dbKey)
    {
        var current = _databaseContextAccessor?.Current;
        var requestedDbKey = string.IsNullOrWhiteSpace(dbKey) == false
            ? dbKey
            : current?.DbKey;
        if (string.IsNullOrWhiteSpace(requestedDbKey))
            requestedDbKey = _metadataOptions.DefaultDatabaseContext?.DbKey;
        var options = new DatabaseScopeOptions
        {
            DbKey = requestedDbKey,
            TenantId = current?.TenantId,
            ReadPreference = current?.ReadPreference ?? SqlReadPreference.Default
        };
        var dataSource = _dataSourceResolver.Resolve(requestedDbKey, options);
        if (dataSource.DatabaseType != databaseType)
            throw new InvalidOperationException($"SQL 数据源 {dataSource.Key} 的数据库类型 {dataSource.DatabaseType} 与 EF Core Provider 对应的数据库类型 {databaseType} 不一致。");
        return dataSource;
    }

    /// <summary>
    /// 创建与 EF Core Provider 对应的 SQL 查询对象
    /// </summary>
    /// <param name="dataSource">数据源</param>
    /// <returns>SQL 查询对象</returns>
    private ISqlQuery CreateQuery(SqlDataSourceDescriptor dataSource)
    {
        if (dataSource == null)
            throw new InvalidOperationException("未解析到可用的 SQL 数据源。");
        return _queryFactory.Create<ISqlQuery>(dataSource.Key);
    }

    /// <summary>
    /// 解析 EF Core Provider 的数据库类型
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <returns>数据库类型</returns>
    private static DatabaseType ResolveDatabaseType(UnitOfWorkBase unitOfWork)
    {
        var providerName = unitOfWork.Database.ProviderName;
        if (string.Equals(providerName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
            return DatabaseType.Sqlite;
        if (string.Equals(providerName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            return DatabaseType.SqlServer;
        if (string.Equals(providerName, "Pomelo.EntityFrameworkCore.MySql", StringComparison.Ordinal) ||
            string.Equals(providerName, "MySql.EntityFrameworkCore", StringComparison.Ordinal))
            return DatabaseType.MySql;
        if (string.Equals(providerName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            return DatabaseType.PgSql;
        if (string.Equals(providerName, "Oracle.EntityFrameworkCore", StringComparison.Ordinal))
            return DatabaseType.Oracle;
        throw new NotSupportedException($"不支持 EF Core Provider {providerName ?? "<未指定>"} 的 SQL 查询集成。");
    }

}