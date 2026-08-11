using Bing.Data.Sql;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Configs;
using Bing.Data.Stores;
using Bing.Dapper;
using Bing.Dapper.MySql;
using Bing.FreeSQL;
using Bing.Uow;
using FreeSql;
using FreeSql.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;
using MySqlUnitOfWork = Bing.Uow.UnitOfWork;

namespace Bing.FreeSQL.Tests.Data.Stores;

/// <summary>
/// FreeSQL 查询存储器测试。
/// </summary>
public class QueryStoreBaseTest
{
    /// <summary>
    /// 测试目的：查询存储器应使用显式注入的 SQL Query 工厂，并注入当前 FreeSQL 的实体元数据。
    /// </summary>
    [Fact]
    public void CreateSqlQuery_WhenDependenciesAreExplicit_ShouldUseFactoryAndFreeSqlMetadata()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql, "Server=scope;Database=test;");
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        using var orm = CreateOrm();
        var scopedServices = new RecordingServiceProvider(scope.ServiceProvider);
        using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scopedServices);
        var store = new TestQueryStore(unitOfWork, scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>(),
            scope.ServiceProvider.GetRequiredService<IDatabaseContextAccessor>(),
            scope.ServiceProvider.GetRequiredService<SqlMetadataOptions>(),
            scope.ServiceProvider.GetRequiredService<ITypeConverterResolver>());

        // Act
        var query = store.GetSqlQuery();
        var sql = query.From<StoreSample>().From("s").ToSql();

        // Assert
        Assert.DoesNotContain(typeof(ISqlQuery), scopedServices.RequestedServiceTypes);
        Assert.IsType<MySqlQuery>(query);
        Assert.Equal("Select `s`.`Id` \r\nFrom `scope_items` As `s`", sql);
    }

    /// <summary>
    /// 测试目的：缺少显式 SQL Query 工厂时应在构造边界明确失败，不回退到工作单元服务提供器。
    /// </summary>
    [Fact]
    public void CreateSqlQuery_WhenFactoryIsMissing_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        using var orm = CreateOrm();
        using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, serviceProvider);

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new TestQueryStore(unitOfWork, null,
            new AsyncLocalDatabaseContextAccessor(), new SqlMetadataOptions(), new DefaultTypeConverterResolver()));

        // Assert
        Assert.Equal("sqlQueryFactory", exception.ParamName);
    }

    /// <summary>
    /// 创建不打开外部连接的 MySQL FreeSQL 实例。
    /// </summary>
    /// <returns>仅用于元数据解析的 FreeSQL 实例。</returns>
    private static IFreeSql CreateOrm() => new FreeSqlBuilder()
        .UseConnectionFactory(DataType.MySql, () => new MySqlConnection())
        .Build();

    /// <summary>
    /// 测试工作单元。
    /// </summary>
    private sealed class TestUnitOfWork : MySqlUnitOfWork
    {
        /// <summary>
        /// 初始化一个<see cref="TestUnitOfWork"/>类型的实例。
        /// </summary>
        /// <param name="wrapper">FreeSQL 包装器。</param>
        /// <param name="serviceProvider">服务提供程序。</param>
        public TestUnitOfWork(FreeSqlWrapper wrapper, IServiceProvider serviceProvider)
            : base(wrapper, serviceProvider)
        {
        }
    }

    /// <summary>
    /// 测试查询存储器。
    /// </summary>
    private sealed class TestQueryStore : QueryStoreBase<StoreSample, int>
    {
        /// <summary>
        /// 初始化一个<see cref="TestQueryStore"/>类型的实例。
        /// </summary>
        /// <param name="unitOfWork">工作单元。</param>
        public TestQueryStore(MySqlUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
            IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
            ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory, databaseContextAccessor,
            metadataOptions, typeConverterResolver)
        {
        }

        /// <summary>
        /// 获取 SQL 查询对象。
        /// </summary>
        /// <returns>SQL 查询对象。</returns>
        public ISqlQuery GetSqlQuery() => Sql;
    }

    /// <summary>
    /// 记录服务请求的作用域服务提供程序。
    /// </summary>
    private sealed class RecordingServiceProvider : IServiceProvider
    {
        /// <summary>
        /// 内部服务提供程序。
        /// </summary>
        private readonly IServiceProvider _innerProvider;

        /// <summary>
        /// 已请求的服务类型。
        /// </summary>
        public List<Type> RequestedServiceTypes { get; } = new();

        /// <summary>
        /// 初始化一个<see cref="RecordingServiceProvider"/>类型的实例。
        /// </summary>
        /// <param name="innerProvider">内部服务提供程序。</param>
        public RecordingServiceProvider(IServiceProvider innerProvider) => _innerProvider = innerProvider;

        /// <summary>
        /// 获取服务实例。
        /// </summary>
        /// <param name="serviceType">服务类型。</param>
        /// <returns>服务实例。</returns>
        public object GetService(Type serviceType)
        {
            RequestedServiceTypes.Add(serviceType);
            return _innerProvider.GetService(serviceType);
        }
    }

    /// <summary>
    /// 查询实体。
    /// </summary>
    [Table(Name = "scope_items")]
    private sealed class StoreSample : Bing.Domain.Entities.IKey<int>
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }
    }
}
