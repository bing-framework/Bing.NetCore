using System.Linq.Expressions;
using Bing.Data.Sql;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Configs;
using Bing.Data.Transaction;
using Bing.Data.Queries;
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
        var sql = query.From<StoreSample>("s").ToSql();

        // Assert
        Assert.DoesNotContain(typeof(ISqlQuery), scopedServices.RequestedServiceTypes);
        Assert.IsType<MySqlQuery>(query);
        Assert.Equal("Select `s`.`Id`,`s`.`Name` \r\nFrom `scope_items` As `s`", sql);
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
    /// 测试目的：带回调的单项查询必须先执行调用方回调，不能保留未实现的公开路径。
    /// </summary>
    [Fact]
    public void Single_WhenActionThrows_ShouldPropagateActionException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql, "Server=scope;Database=test;");
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        using var orm = CreateOrm();
        using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scope.ServiceProvider);
        using var store = CreateStore(unitOfWork, scope.ServiceProvider);
        var expected = new InvalidOperationException("action failure");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => store.Single(entity => entity.Id == 1,
            _ => throw expected));

        // Assert
        Assert.Same(expected, exception);
    }

    /// <summary>
    /// 测试目的：异步带回调单项查询必须将回调失败保存为任务异常，不能同步抛出或进入未实现路径。
    /// </summary>
    [Fact]
    public async Task SingleAsync_WhenActionThrows_ShouldPropagateActionException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql, "Server=scope;Database=test;");
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        using var orm = CreateOrm();
        using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scope.ServiceProvider);
        using var store = CreateStore(unitOfWork, scope.ServiceProvider);
        var expected = new InvalidOperationException("action failure");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => store.SingleAsync(entity => entity.Id == 1,
            _ => throw expected));

        // Assert
        Assert.Same(expected, exception);
    }

    /// <summary>
    /// 测试目的：带回调的异步单项查询在预取消时不得执行调用方回调或创建查询。
    /// </summary>
    [Fact]
    public async Task SingleAsync_WhenCancellationRequested_ShouldNotInvokeAction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql, "Server=scope;Database=test;");
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        using var orm = CreateOrm();
        using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scope.ServiceProvider);
        using var store = CreateStore(unitOfWork, scope.ServiceProvider);
        var actionInvoked = false;
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SingleAsync(entity => entity.Id == 1,
            query =>
            {
                actionInvoked = true;
                return query;
            }, cancellationTokenSource.Token));
        Assert.False(actionInvoked);
    }

    /// <summary>
    /// 测试目的：释放已创建的 SQL Query 后重复释放必须安全，且不得处置工作单元拥有的 ORM。
    /// </summary>
    [Fact]
    public void Dispose_WhenSqlQueryWasCreated_ShouldBeIdempotent()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql, "Server=scope;Database=test;");
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        using var orm = CreateOrm();
        using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scope.ServiceProvider);
        var store = CreateStore(unitOfWork, scope.ServiceProvider);
        _ = store.GetSqlQuery();

        // Act
        store.Dispose();
        var exception = Record.Exception(store.Dispose);

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// 测试目的：单实体异步更新在令牌已取消时不得修改工作单元跟踪状态或访问 ORM。
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WhenCancellationRequested_ShouldThrowBeforeEntityUpdate()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql, "Server=scope;Database=test;");
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        using var orm = CreateOrm();
        using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scope.ServiceProvider);
        using var store = CreateStoreBase(unitOfWork, scope.ServiceProvider);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.UpdateAsync(new StoreSample { Id = 1 },
            cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：集合添加在预取消时不得枚举调用方集合或访问 FreeSQL 跟踪状态。
    /// </summary>
    [Fact]
    public async Task AddAsync_WhenCancellationRequested_ShouldNotEnumerateEntities()
    {
        // Arrange
        using var storeContext = CreateStoreContext();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.Store.AddAsync(
            new ThrowingEnumerable<StoreSample>(), cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：单实体添加在预取消时不得进入 FreeSQL 跟踪状态。
    /// </summary>
    [Fact]
    public async Task AddAsync_WhenCancellationRequested_ShouldThrowBeforeEntityAdd()
    {
        // Arrange
        using var storeContext = CreateStoreContext();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.Store.AddAsync(
            new StoreSample { Id = 1 }, cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：集合更新在预取消时不得枚举调用方集合或修改工作单元跟踪状态。
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WhenCancellationRequested_ShouldNotEnumerateEntities()
    {
        // Arrange
        using var storeContext = CreateStoreContext();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.Store.UpdateAsync(
            new ThrowingEnumerable<StoreSample>(), cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：按标识删除在预取消时不得发起实体查找或修改跟踪状态。
    /// </summary>
    [Fact]
    public async Task RemoveAsync_WhenCancellationRequested_ShouldThrowBeforeEntityLookup()
    {
        // Arrange
        using var storeContext = CreateStoreContext();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.Store.RemoveAsync(1,
            cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：预取消实体删除必须先于实体标识读取终止，不能访问自定义 Id getter。
    /// </summary>
    [Fact]
    public async Task RemoveAsync_WhenCancellationRequested_ShouldNotReadEntityId()
    {
        // Arrange
        using var storeContext = CreateStoreContext();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var entity = new SideEffectStoreSample();
        using var store = new SideEffectStore(storeContext.UnitOfWork,
            storeContext.Scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>(),
            storeContext.Scope.ServiceProvider.GetRequiredService<IDatabaseContextAccessor>(),
            storeContext.Scope.ServiceProvider.GetRequiredService<SqlMetadataOptions>(),
            storeContext.Scope.ServiceProvider.GetRequiredService<ITypeConverterResolver>());

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.RemoveAsync(entity,
            cancellationTokenSource.Token));

        // Assert
        Assert.Equal(0, entity.IdReadCount);
    }

    /// <summary>
    /// 测试目的：按标识集合删除在预取消时不得枚举调用方集合或发起实体查找。
    /// </summary>
    [Fact]
    public async Task RemoveAsync_WhenCancellationRequested_ShouldNotEnumerateIds()
    {
        // Arrange
        using var storeContext = CreateStoreContext();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.Store.RemoveAsync(
            new ThrowingEnumerable<int>(), cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：实体集合删除在预取消时不得构造标识投影或枚举调用方集合。
    /// </summary>
    [Fact]
    public async Task RemoveAsync_WhenCancellationRequested_ShouldNotEnumerateEntities()
    {
        // Arrange
        using var storeContext = CreateStoreContext();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.Store.RemoveAsync(
            new ThrowingEnumerable<StoreSample>(), cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：按标识异步查询在预取消时不得枚举调用方标识集合。
    /// </summary>
    [Fact]
    public async Task FindByIdsAsync_WhenCancellationRequested_ShouldNotEnumerateIds()
    {
        // Arrange
        using var storeContext = CreateStoreContext();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.QueryStore.FindByIdsAsync(
            new ThrowingEnumerable<int>(), cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：未跟踪按标识异步查询在预取消时不得枚举调用方标识集合。
    /// </summary>
    [Fact]
    public async Task FindByIdsNoTrackingAsync_WhenCancellationRequested_ShouldNotEnumerateIds()
    {
        // Arrange
        using var storeContext = CreateStoreContext();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.QueryStore.FindByIdsNoTrackingAsync(
            new ThrowingEnumerable<int>(), cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：所有带取消令牌的异步查询入口必须在空参数分支和查询构造前观察预取消。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenCancellationRequested_ShouldThrowBeforeEmptyParameterBranches()
    {
        // Arrange
        using var storeContext = CreateStoreContext();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var token = cancellationTokenSource.Token;

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.QueryStore.FindAsync(null, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.QueryStore.FindByIdAsync(null, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.QueryStore.FindByIdNoTrackingAsync(0, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.QueryStore.FindAllAsync(null, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.QueryStore.FindAllNoTrackingAsync(null, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.QueryStore.ExistsAsync(
            (Expression<Func<StoreSample, bool>>)null, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storeContext.QueryStore.CountAsync(null, token));
    }

    /// <summary>
    /// 测试目的：同步批量更新必须在方法返回前完成实体跟踪，随后的工作单元提交不得遗漏更新。
    /// </summary>
    [Fact]
    public async Task Update_WhenEntitiesAreProvided_ShouldTrackChangesBeforeCommit()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddMySqlProvider();
            services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql, "Server=scope;Database=test;");
            services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<StoreSample>();
            orm.Insert(new StoreSample { Id = 1, Name = "before" }).ExecuteAffrows();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scope.ServiceProvider);
            using var store = CreateStoreBase(unitOfWork, scope.ServiceProvider);

            // Act
            var entity = store.FindById(1);
            entity.Name = "after";
            store.Update(new[] { entity });
            await unitOfWork.SaveChangesAsync();

            // Assert
            Assert.Equal("after", orm.Select<StoreSample>().Where(item => item.Id == 1).ToOne().Name);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// 测试目的：分页查询的预取消令牌必须在初始化分页排序或访问 FreeSQL 总数查询前终止操作。
    /// </summary>
    [Fact]
    public async Task PagerQueryAsync_WhenCancellationRequested_ShouldThrowBeforeMutatingPager()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql, "Server=scope;Database=test;");
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        using var orm = CreateOrm();
        using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scope.ServiceProvider);
        using var store = CreateStore(unitOfWork, scope.ServiceProvider);
        var pager = new Bing.Data.Pager(1, 10);
        var originalOrder = pager.Order;
        var query = new PagingQuery(pager);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.PagerQueryAsync(query,
            cancellationTokenSource.Token));
        Assert.Equal(originalOrder, pager.Order);
    }

    /// <summary>
    /// 测试目的：普通异步查询的预取消令牌必须在构造 FreeSQL 查询或进入同步 IQueryable 回退前终止操作。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenCancellationRequested_ShouldThrowBeforeCreatingQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql, "Server=scope;Database=test;");
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        using var orm = CreateOrm();
        using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scope.ServiceProvider);
        using var store = CreateStore(unitOfWork, scope.ServiceProvider);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.QueryAsync(
            new PagingQuery(new Bing.Data.Pager(1, 10)), cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：普通异步查询恢复为 FreeSQL Select 后必须保留查询条件并使用 Provider 原生异步读取物化结果。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenSqliteDataExists_ShouldReturnMatchedEntities()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"Bing.FreeSQL.{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddMySqlProvider();
            services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql, "Server=scope;Database=test;");
            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            using var orm = CreateSqliteOrm(databasePath);
            orm.CodeFirst.SyncStructure<StoreSample>();
            orm.Insert(new StoreSample { Id = 1 }).ExecuteAffrows();
            using var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scope.ServiceProvider);
            using var store = CreateStore(unitOfWork, scope.ServiceProvider);

            // Act
            var result = await store.QueryAsync(new PagingQuery(new Bing.Data.Pager(1, 10)));

            // Assert
            var entity = Assert.Single(result);
            Assert.Equal(1, entity.Id);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static TestQueryStore CreateStore(MySqlUnitOfWork unitOfWork, IServiceProvider serviceProvider)
    {
        return new TestQueryStore(unitOfWork, serviceProvider.GetRequiredService<ISqlQueryFactory>(),
            serviceProvider.GetRequiredService<IDatabaseContextAccessor>(),
            serviceProvider.GetRequiredService<SqlMetadataOptions>(),
            serviceProvider.GetRequiredService<ITypeConverterResolver>());
    }

    /// <summary>
    /// 创建用于写入行为测试的存储器。
    /// </summary>
    /// <param name="unitOfWork">测试工作单元。</param>
    /// <param name="serviceProvider">作用域服务提供程序。</param>
    /// <returns>测试存储器。</returns>
    private static TestStore CreateStoreBase(MySqlUnitOfWork unitOfWork, IServiceProvider serviceProvider)
    {
        return new TestStore(unitOfWork, serviceProvider.GetRequiredService<ISqlQueryFactory>(),
            serviceProvider.GetRequiredService<IDatabaseContextAccessor>(),
            serviceProvider.GetRequiredService<SqlMetadataOptions>(),
            serviceProvider.GetRequiredService<ITypeConverterResolver>());
    }

    /// <summary>
    /// 创建用于集合写入取消测试的存储器上下文。
    /// </summary>
    /// <returns>包含服务作用域、FreeSQL 实例、工作单元和存储器的上下文。</returns>
    private static StoreContext CreateStoreContext()
    {
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.MySql, "Server=scope;Database=test;");
        var serviceProvider = services.BuildServiceProvider();
        var scope = serviceProvider.CreateScope();
        var orm = CreateOrm();
        var unitOfWork = new TestUnitOfWork(new FreeSqlWrapper { Orm = orm }, scope.ServiceProvider);
        var store = CreateStoreBase(unitOfWork, scope.ServiceProvider);
        var queryStore = CreateStore(unitOfWork, scope.ServiceProvider);
        return new StoreContext(serviceProvider, scope, orm, unitOfWork, store, queryStore);
    }

    /// <summary>
    /// 创建不打开外部连接的 MySQL FreeSQL 实例。
    /// </summary>
    /// <returns>仅用于元数据解析的 FreeSQL 实例。</returns>
    private static IFreeSql CreateOrm() => new FreeSqlBuilder()
        .UseConnectionFactory(DataType.MySql, () => new MySqlConnection())
        .Build();

    /// <summary>
    /// 创建使用独立临时文件的 SQLite FreeSQL 实例。
    /// </summary>
    /// <param name="databasePath">临时数据库文件路径。</param>
    /// <returns>用于本地查询验证的 FreeSQL 实例。</returns>
    private static IFreeSql CreateSqliteOrm(string databasePath) => new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, $"Data Source={databasePath}")
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
    /// 测试写入存储器。
    /// </summary>
    private sealed class TestStore : StoreBase<StoreSample, int>
    {
        /// <summary>
        /// 初始化一个 <see cref="TestStore"/> 类型的实例。
        /// </summary>
        /// <param name="unitOfWork">工作单元。</param>
        /// <param name="sqlQueryFactory">SQL 查询对象工厂。</param>
        /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
        /// <param name="metadataOptions">SQL 元数据配置。</param>
        /// <param name="typeConverterResolver">数据类型转换器解析器。</param>
        public TestStore(MySqlUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
            IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
            ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory,
            databaseContextAccessor, metadataOptions, typeConverterResolver)
        {
        }
    }

    /// <summary>
    /// 用于验证实体标识读取顺序的存储器。
    /// </summary>
    private sealed class SideEffectStore : StoreBase<SideEffectStoreSample, int>
    {
        /// <summary>
        /// 初始化一个 <see cref="SideEffectStore"/> 类型的实例。
        /// </summary>
        public SideEffectStore(MySqlUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
            IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
            ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory,
            databaseContextAccessor, metadataOptions, typeConverterResolver)
        {
        }
    }

    /// <summary>
    /// 枚举时抛错的测试集合。
    /// </summary>
    /// <typeparam name="T">集合元素类型。</typeparam>
    private sealed class ThrowingEnumerable<T> : IEnumerable<T>
    {
        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("enumeration must not occur");

        /// <inheritdoc />
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// 集合写入取消测试上下文。
    /// </summary>
    private sealed class StoreContext : IDisposable
    {
        /// <summary>
        /// 初始化一个 <see cref="StoreContext"/> 类型的实例。
        /// </summary>
        public StoreContext(ServiceProvider serviceProvider, IServiceScope scope, IFreeSql orm,
            TestUnitOfWork unitOfWork, TestStore store, TestQueryStore queryStore)
        {
            ServiceProvider = serviceProvider;
            Scope = scope;
            Orm = orm;
            UnitOfWork = unitOfWork;
            Store = store;
            QueryStore = queryStore;
        }

        /// <summary>
        /// 服务提供程序。
        /// </summary>
        private ServiceProvider ServiceProvider { get; }

        /// <summary>
        /// 服务作用域。
        /// </summary>
        public IServiceScope Scope { get; }

        /// <summary>
        /// FreeSQL 实例。
        /// </summary>
        private IFreeSql Orm { get; }

        /// <summary>
        /// 工作单元。
        /// </summary>
        public TestUnitOfWork UnitOfWork { get; }

        /// <summary>
        /// 测试存储器。
        /// </summary>
        public TestStore Store { get; }

        /// <summary>
        /// 测试查询存储器。
        /// </summary>
        public TestQueryStore QueryStore { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            QueryStore.Dispose();
            Store.Dispose();
            UnitOfWork.Dispose();
            Orm.Dispose();
            Scope.Dispose();
            ServiceProvider.Dispose();
        }
    }

    /// <summary>
    /// 分页测试查询对象。
    /// </summary>
    private sealed class PagingQuery : IQueryBase<StoreSample>
    {
        /// <summary>
        /// 分页对象。
        /// </summary>
        private readonly Bing.Data.IPager _pager;

        /// <summary>
        /// 初始化一个 <see cref="PagingQuery"/> 类型的实例。
        /// </summary>
        /// <param name="pager">分页对象。</param>
        public PagingQuery(Bing.Data.IPager pager) => _pager = pager;

        /// <inheritdoc />
        public Expression<Func<StoreSample, bool>> GetCondition() => entity => entity.Id >= 0;

        /// <inheritdoc />
        public string GetOrder() => null;

        /// <inheritdoc />
        public Bing.Data.IPager GetPager() => _pager;
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

        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 读取标识时记录访问次数的测试实体。
    /// </summary>
    private sealed class SideEffectStoreSample : Bing.Domain.Entities.IKey<int>
    {
        /// <summary>
        /// 标识读取次数。
        /// </summary>
        public int IdReadCount { get; private set; }

        /// <inheritdoc />
        public int Id
        {
            get
            {
                IdReadCount++;
                return 1;
            }
        }
    }
}
