using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Transaction;
using Bing.Dapper;
using Bing.Dapper.MySql;
using Bing.Data.Stores;
using Bing.FreeSQL;
using Bing.Uow;
using FreeSql;
using FreeSql.DataAnnotations;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Bing.FreeSQL.Tests.Data.Stores;

/// <summary>
/// 验证 FreeSQL QueryStoreBase 的 SQL Query 与业务 SQL 工作单元之间的边界。
/// </summary>
public sealed class QueryStoreBaseUnitOfWorkTest
{
    /// <summary>
    /// 验证仓储的 protected Sql 与应用默认 Query 共用业务事务，且释放仓储不关闭借用连接。
    /// </summary>
    [Fact]
    public void StoreSqlAndApplicationQuery_ShouldShareTransactionAndStoreDisposeShouldKeepBorrowedConnectionOpen()
    {
        using var context = new TestContext();
        using var root = context.ScopeManager.Begin();
        using var applicationQuery = context.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();

        var insertedId = applicationQuery.Sql(
            "Insert Into unit_of_work_items(Id, Name) Values (1, 'dapper') Returning Id").Scalar<int>();
        Assert.Equal(1, insertedId);
        Assert.Equal(1, context.Store.ReadCount());

        var borrowedConnection = Assert.Single(context.ConnectionFactory.CreatedConnections);
        context.Store.Dispose();

        Assert.Equal(ConnectionState.Open, borrowedConnection.State);
        Assert.Equal(1, applicationQuery.Sql("Select Count(*) From unit_of_work_items").Scalar<int>());

        root.Rollback();
        root.Dispose();
        Assert.Equal(ConnectionState.Closed, borrowedConnection.State);
        Assert.Empty(context.ReadNames());
    }

    /// <summary>
    /// 验证 FreeSQL 原生 CRUD 在业务 SQL Uow 内仍由 FreeSQL 自己执行，不随 Dapper 事务回滚。
    /// </summary>
    /// <remarks>FreeSQL 与 Dapper 使用两个真实临时文件，避免 SQLite 单文件写锁；此测试不声称两条路径共享物理事务。</remarks>
    [Fact]
    public void FreeSqlNativeCrud_ShouldRemainOutsideBusinessSqlUnitOfWork()
    {
        using var context = new TestContext();
        using var root = context.ScopeManager.Begin();

        var affectedRows = context.UnitOfWork.Orm.Insert(new UnitOfWorkSample
        {
            Id = 1,
            Name = "freesql-native"
        }).ExecuteAffrows();
        Assert.Equal(1, affectedRows);
        context.UnitOfWork.Commit();

        using (var applicationQuery = context.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create())
        {
            var insertedId = applicationQuery.Sql(
                "Insert Into unit_of_work_items(Id, Name) Values (2, 'dapper-transaction') Returning Id")
                .Scalar<int>();
            Assert.Equal(2, insertedId);
        }

        root.Rollback();
        root.Dispose();
        Assert.All(context.ConnectionFactory.CreatedConnections,
            connection => Assert.Equal(ConnectionState.Closed, connection.State));

        Assert.Empty(context.ReadNames());
        Assert.Equal(new[] { "freesql-native" }, context.ReadNativeNames());
    }

    /// <summary>
    /// 建立使用真实临时 SQLite 文件的 FreeSQL 与默认 Dapper Query 测试上下文。
    /// </summary>
    private sealed class TestContext : IDisposable
    {
        private readonly string _databasePath;
        private readonly string _nativeDatabasePath;
        private readonly string _sqliteConnectionString;
        private readonly string _nativeConnectionString;
        private readonly ServiceProvider _rootServiceProvider;
        private readonly IServiceScope _serviceScope;
        private readonly IFreeSql _orm;

        /// <summary>
        /// 初始化 SQLite 数据库、FreeSQL 工作单元和默认 SQL Query 服务。
        /// </summary>
        public TestContext()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.Uow.{Guid.NewGuid():N}.db");
            _nativeDatabasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.Native.{Guid.NewGuid():N}.db");
            _sqliteConnectionString = $"Data Source={_databasePath};Pooling=False";
            _nativeConnectionString = $"Data Source={_nativeDatabasePath};Pooling=False";
            _orm = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, _nativeConnectionString)
                .UseLazyLoading(false)
                .Build();
            _orm.CodeFirst.SyncStructure<UnitOfWorkSample>();
            using (var connection = new SqliteConnection(_sqliteConnectionString))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "Create Table unit_of_work_items (Id Integer Primary Key, Name Text Not Null)";
                command.ExecuteNonQuery();
            }

            ConnectionFactory = new SqliteBackedMySqlConnectionFactory(_sqliteConnectionString);
            var services = new ServiceCollection();
            services.AddMySqlProvider();
            services.AddSqlDataSource("default", DatabaseType.MySql,
                "Server=localhost;Database=freesql_unit_of_work;");
            services.AddUnitOfWorkScopes();
            services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
            services.AddSingleton<ISqlDbConnectionFactoryResolver>(ConnectionFactory);
            services.Replace(ServiceDescriptor.Singleton<ISqlDatabaseIdentityResolver>(
                new SqliteBackedMySqlDatabaseIdentityResolver()));
            _rootServiceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true
            });
            _serviceScope = _rootServiceProvider.CreateScope();
            ServiceProvider = _serviceScope.ServiceProvider;

            UnitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = _orm }, ServiceProvider);
            Store = new TestQueryStore(UnitOfWork, ServiceProvider.GetRequiredService<ISqlQueryFactory>(),
                ServiceProvider.GetRequiredService<IDatabaseContextAccessor>(),
                ServiceProvider.GetRequiredService<SqlMetadataOptions>(),
                ServiceProvider.GetRequiredService<ITypeConverterResolver>());
            ScopeManager = ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        }

        /// <summary>
        /// 获取测试服务提供器。
        /// </summary>
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// 获取测试工作单元。
        /// </summary>
        public TestUnitOfWork UnitOfWork { get; }

        /// <summary>
        /// 获取可执行查询的 FreeSQL 仓储。
        /// </summary>
        public TestQueryStore Store { get; }

        /// <summary>
        /// 获取业务工作单元范围管理器。
        /// </summary>
        public IUnitOfWorkScopeManager ScopeManager { get; }

        /// <summary>
        /// 获取将 MySQL Query 连接到本地 SQLite 文件的测试连接工厂。
        /// </summary>
        public SqliteBackedMySqlConnectionFactory ConnectionFactory { get; }

        /// <summary>
        /// 从独立 SQLite 连接读取已提交记录。
        /// </summary>
        /// <returns>按标识排序的记录名称。</returns>
        public List<string> ReadNames()
            => ReadNames(_sqliteConnectionString);

        /// <summary>
        /// 从 FreeSQL 原生 CRUD 使用的独立 SQLite 文件读取提交行。
        /// </summary>
        /// <returns>按标识排序的记录名称。</returns>
        public List<string> ReadNativeNames() => ReadNames(_nativeConnectionString);

        private static List<string> ReadNames(string connectionString)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "Select Name From unit_of_work_items Order By Id";
            using var reader = command.ExecuteReader();
            var names = new List<string>();
            while (reader.Read())
                names.Add(reader.GetString(0));
            return names;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Store.Dispose();
            UnitOfWork.Dispose();
            _orm.Dispose();
            _serviceScope.Dispose();
            _rootServiceProvider.Dispose();
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
            if (File.Exists(_nativeDatabasePath))
                File.Delete(_nativeDatabasePath);
        }
    }

    /// <summary>
    /// FreeSQL 工作单元测试实现。
    /// </summary>
    private sealed class TestUnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化 FreeSQL 工作单元。
        /// </summary>
        public TestUnitOfWork(FreeSqlWrapper wrapper, IServiceProvider serviceProvider)
            : base(wrapper, serviceProvider)
        {
        }
    }

    /// <summary>
    /// 显式触发基类 protected Sql 的测试仓储。
    /// </summary>
    private sealed class TestQueryStore : QueryStoreBase<UnitOfWorkSample, int>
    {
        /// <summary>
        /// 初始化查询仓储。
        /// </summary>
        public TestQueryStore(TestUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
            IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
            ITypeConverterResolver typeConverterResolver)
            : base(unitOfWork, sqlQueryFactory, databaseContextAccessor, metadataOptions, typeConverterResolver)
        {
        }

        /// <summary>
        /// 使用基类懒创建的 SQL Query 读取当前事务可见的记录数。
        /// </summary>
        public int ReadCount() => Sql.Sql("Select Count(*) From unit_of_work_items").Scalar<int>();
    }

    /// <summary>
    /// 供本地事务测试使用的 FreeSQL 实体。
    /// </summary>
    [Table(Name = "unit_of_work_items")]
    private sealed class UnitOfWorkSample : Bing.Domain.Entities.IKey<int>
    {
        /// <inheritdoc />
        [Column(IsPrimary = true, IsIdentity = false)]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置记录名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 为 MySQL Dapper Query 提供真实临时 SQLite 连接的测试适配器。
    /// </summary>
    private sealed class SqliteBackedMySqlConnectionFactory : ISqlDbConnectionFactoryResolver
    {
        private readonly string _sqliteConnectionString;

        /// <summary>
        /// 初始化测试连接工厂。
        /// </summary>
        public SqliteBackedMySqlConnectionFactory(string sqliteConnectionString) =>
            _sqliteConnectionString = sqliteConnectionString;

        /// <summary>
        /// 获取工厂创建的连接，用于验证仓储释放未关闭 Uow 借用连接。
        /// </summary>
        public List<SqliteConnection> CreatedConnections { get; } = new();

        /// <inheritdoc />
        public IDbConnection Create(string providerKey, string connectionString)
        {
            var connection = new SqliteConnection(_sqliteConnectionString);
            CreatedConnections.Add(connection);
            return connection;
        }
    }

    /// <summary>
    /// 为 MySQL Provider 的 SQLite 测试适配连接提供稳定的逻辑端点身份。
    /// </summary>
    private sealed class SqliteBackedMySqlDatabaseIdentityResolver : ISqlDatabaseIdentityResolver
    {
        private readonly ISqlDatabaseIdentityResolver _defaultResolver = new DefaultSqlDatabaseIdentityResolver();

        /// <inheritdoc />
        public SqlDatabaseIdentity Resolve(DatabaseType databaseType, string connectionString)
        {
            if (databaseType == DatabaseType.MySql)
                return new SqlDatabaseIdentity
                {
                    DatabaseType = DatabaseType.MySql,
                    Server = "freesql-sqlite-adapter",
                    Database = "freesql-uow-test"
                };
            return _defaultResolver.Resolve(databaseType, connectionString);
        }
    }
}
