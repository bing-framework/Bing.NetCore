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
    /// 初始化一个<see cref="EfCoreSqlQueryFactory"/>类型的实例
    /// </summary>
    /// <param name="queryFactory">SQL 查询对象工厂</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">SQL 元数据配置</param>
    /// <param name="typeConverterResolver">数据库类型转换器解析器</param>
    /// <param name="connectionFactoryResolver">独立数据库连接工厂解析器</param>
    public EfCoreSqlQueryFactory(ISqlQueryFactory queryFactory,
        IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions metadataOptions = null,
        ITypeConverterResolver typeConverterResolver = null,
        ISqlDbConnectionFactoryResolver connectionFactoryResolver = null)
    {
        _queryFactory = queryFactory ?? throw new ArgumentNullException(nameof(queryFactory));
        _databaseContextAccessor = databaseContextAccessor;
        _metadataOptions = metadataOptions ?? new SqlMetadataOptions();
        _typeConverterResolver = typeConverterResolver;
        _connectionFactoryResolver = connectionFactoryResolver;
    }

    /// <inheritdoc />
    public ISqlQuery Create(UnitOfWorkBase unitOfWork, EfCoreSqlConnectionMode mode = EfCoreSqlConnectionMode.Shared)
    {
        if (unitOfWork == null)
            throw new ArgumentNullException(nameof(unitOfWork));
        var databaseType = ResolveDatabaseType(unitOfWork);
        var query = CreateQuery(databaseType);
        BindEntityMetadata(query, unitOfWork);
        if (mode == EfCoreSqlConnectionMode.Independent)
        {
            var connection = CreateIndependentConnection(databaseType, unitOfWork.Database.GetConnectionString());
            var independentContext = GetExternalContext(query);
            independentContext.SetOwnedConnection(connection);
            independentContext.SetConnectionSource(SqlConnectionSource.DataSource);
            return query;
        }
        query.SetConnection(unitOfWork.Database.GetDbConnection());
        var externalContext = GetExternalContext(query);
        externalContext.SetConnectionSource(SqlConnectionSource.EntityFrameworkCore);
        externalContext.SetExternalTransactionResolver(() => unitOfWork.Database.CurrentTransaction?.GetDbTransaction());
        return query;
    }

    /// <summary>
    /// 绑定 EF Core 实体元数据
    /// </summary>
    /// <param name="query">SQL 查询对象</param>
    /// <param name="unitOfWork">工作单元</param>
    private void BindEntityMetadata(ISqlQuery query, UnitOfWorkBase unitOfWork)
    {
        var externalContext = GetExternalContext(query);
        externalContext.SetEntityMetadata(unitOfWork);
        externalContext.SetEntityMappingResolver(new DefaultEntityMappingResolver(unitOfWork,
            _databaseContextAccessor, _metadataOptions, _typeConverterResolver));
    }

    /// <summary>
    /// 基于 EF Core Provider 创建独立连接
    /// </summary>
    /// <param name="databaseType">数据库类型</param>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>独立数据库连接</returns>
    private System.Data.IDbConnection CreateIndependentConnection(DatabaseType databaseType, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("EF Core 数据库连接字符串不能为空，无法创建独立 SQL 查询连接。");
        if (_connectionFactoryResolver == null)
            throw new InvalidOperationException("未注册独立 SQL 数据库连接工厂解析器。");
        return _connectionFactoryResolver.Create(databaseType, connectionString);
    }

    /// <summary>
    /// 创建与 EF Core Provider 对应的 SQL 查询对象
    /// </summary>
    /// <param name="databaseType">数据库类型</param>
    /// <returns>SQL 查询对象</returns>
    private ISqlQuery CreateQuery(DatabaseType databaseType)
    {
        var dataSource = _metadataOptions.DataSources.DataSources.Values
            .FirstOrDefault(t => t.DatabaseType == databaseType);
        if (dataSource == null)
            throw new InvalidOperationException($"未注册数据库类型 {databaseType} 的 SQL 数据源。");
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

    /// <summary>
    /// 获取 Query 外部上下文
    /// </summary>
    /// <param name="query">SQL 查询对象</param>
    /// <returns>Query 外部上下文</returns>
    private static ISqlQueryExternalContext GetExternalContext(ISqlQuery query) => query as ISqlQueryExternalContext ??
        throw new InvalidOperationException("SQL 查询对象未实现 ISqlQueryExternalContext，无法绑定 EF Core 上下文。");
}