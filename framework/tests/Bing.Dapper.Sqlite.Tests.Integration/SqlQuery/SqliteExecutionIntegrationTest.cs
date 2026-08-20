using System.Diagnostics;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Diagnostics;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// SQLite 真实执行集成测试。
/// </summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteExecutionIntegrationTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化一个<see cref="SqliteExecutionIntegrationTest"/>类型的实例。
    /// </summary>
    /// <param name="fixture">SQLite 集成测试数据库固定装置。</param>
    public SqliteExecutionIntegrationTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 测试 - SQLite应执行同步插入命令。
    /// </summary>
    [Fact]
    public async Task ExecuteSql_ShouldInsertRowSynchronously()
    {
        using var executor = _fixture.CreateExecutor();
        executor.ExecuteSql("Insert Into samples(Name, Amount) Values (@name, @amount)", new { name = "sync", amount = 12.5m });

        var names = await _fixture.ReadNamesAsync();

        Assert.Equal(new[] { "sync" }, names);
    }

    /// <summary>
    /// 测试 - SQLite应执行异步插入命令。
    /// </summary>
    [Fact]
    public async Task ExecuteSqlAsync_ShouldInsertRowAsynchronously()
    {
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values (@name, @amount)",
            new { name = "async", amount = 8.75m });

        var names = await _fixture.ReadNamesAsync();

        Assert.Equal(new[] { "async" }, names);
    }

    /// <summary>
    /// 测试目的：异步列表、标量和单实体查询在执行前取消时，应停止执行并释放当前 Query 的执行资源。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenCancellationRequested_ShouldCancelAndReleaseExecutionResources()
    {
        // Arrange
        await SeedAsync();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        using (var listQuery = _fixture.CreateQuery())
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CreateSamplesDescription(listQuery).ToListAsync(cancellationToken: cancellationTokenSource.Token));
        using (var scalarQuery = _fixture.CreateQuery())
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                scalarQuery.Query<int>().AppendSelect("Count(*)").AppendFrom("samples")
                    .ScalarAsync(cancellationToken: cancellationTokenSource.Token));
        using (var scalarExtensionQuery = _fixture.CreateQuery())
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                scalarExtensionQuery.Query<int>().AppendSelect("Count(*)").AppendFrom("samples")
                    .ScalarAsync(cancellationToken: cancellationTokenSource.Token));
        }
        using (var singleQuery = _fixture.CreateQuery())
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateSamplesDescription(singleQuery).FirstOrDefaultAsync(cancellationToken: cancellationTokenSource.Token));
        using (var pagerQuery = _fixture.CreateQuery())
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSamplesDescription(pagerQuery)
                .ToPageAsync(new Pager(1, 2), cancellationToken: cancellationTokenSource.Token));

        // Assert
        await InsertAsync("after-query-cancellation");
        Assert.Equal(4, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：SQLite 应在同一连接内通过受控别名访问附加数据库，并在结束时分离该数据库。
    /// </summary>
    [Fact]
    public async Task AttachDatabase_WhenUsingSameConnection_ShouldQueryAttachedDatabase()
    {
        // Arrange
        using (var executor = _fixture.CreateExecutor("second"))
            await executor.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values (@name, @amount)",
                new { name = "attached", amount = 1m });

        // Act
        using (var scope = _fixture.GetTransactionScopeFactory().Begin("first"))
        using (var executor = scope.CreateExecutor())
        using (var query = scope.CreateQuery())
        {
            await executor.ExecuteSqlAsync("Attach Database @path As reporting",
                new { path = _fixture.SecondDatabasePath });
            var result = query.Query<string>().Select("Name").From("reporting.samples").Scalar();

            // Assert
            Assert.Equal("attached", result);
            scope.Commit();
        }
    }

    /// <summary>
    /// 测试目的：未显式启用租户诊断时，真实 SQLite 执行不应输出环境租户标识。
    /// </summary>
    [Fact]
    public async Task ExecuteSql_WhenTenantDiagnosticsIsNotEnabled_ShouldNotPublishTenantId()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqliteDiagnosticObserver(item => message = item);
        var manager = _fixture.GetDatabaseScopeManager();

        // Act
        using (manager.Use(new DatabaseScopeOptions { DbKey = "first", TenantId = "tenant-hidden" }))
        using (var executor = _fixture.CreateExecutor("first"))
            await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "hidden-tenant" });

        // Assert
        Assert.NotNull(message);
        Assert.Null(message.TenantId);
        Assert.Equal(new[] { "hidden-tenant" }, await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 测试 - Query 创建后切换环境数据源执行时，诊断应仍使用创建时固定的数据库上下文。
    /// </summary>
    [Fact]
    public void ExecuteQuery_WhenAmbientDataSourceChanges_ShouldPublishPinnedQueryContext()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqliteDiagnosticObserver(item => message = item);
        var manager = _fixture.GetDatabaseScopeManager();
        using var query = _fixture.CreateQuery("first");

        // Act
        using (manager.Use("second"))
            query.Query<int>().AppendSelect("Count(*)").AppendFrom("samples").Scalar();

        // Assert
        Assert.NotNull(message);
        Assert.Equal("first", message.Connection.DbKey);
        Assert.Equal("first-profile", message.MappingProfile);
    }

    /// <summary>
    /// 测试 - SQLite应执行参数化列表查询。
    /// </summary>
    [Fact]
    public async Task ExecuteQuery_ShouldQueryParameterizedList()
    {
        await InsertAsync("first");
        await InsertAsync("second");
        await InsertAsync("third");
        using var query = _fixture.CreateQuery();
        var description = query.Query<Sample>().Select("Id,Name,Amount").From("samples")
            .In("Name", new object[] { "first", "third" });

        var result = description.ToList();

        Assert.Equal(new[] { "first", "third" }, result.Select(t => t.Name));
    }

    /// <summary>
    /// 测试 - 空 In 集合必须保留恒假过滤，不能因忽略 Where 条件而返回全表数据。
    /// </summary>
    [Fact]
    public async Task ExecuteQuery_WhenInValuesAreEmpty_ShouldReturnNoRows()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var description = query.Query<Sample>().Select("Id,Name,Amount").From("samples").In("Id", Array.Empty<object>());

        // Act
        var result = description.ToList();

        // Assert
        Assert.Empty(result);
        Assert.Equal(3, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试 - 空 Not In 集合必须保留恒真过滤，并保持原始数据集完整。
    /// </summary>
    [Fact]
    public async Task ExecuteQuery_WhenNotInValuesAreEmpty_ShouldReturnAllRows()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var description = query.Query<Sample>().Select("Id,Name,Amount").From("samples")
            .NotIn("Id", Array.Empty<object>()).OrderBy("Id");

        // Act
        var result = description.ToList();

        // Assert
        Assert.Equal(new[] { "one", "two", "three" }, result.Select(t => t.Name));
        Assert.Equal(3, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试 - SQLite应执行Scalar查询。
    /// </summary>
    [Fact]
    public async Task ExecuteScalar_ShouldReturnActualCount()
    {
        await InsertAsync("first");
        await InsertAsync("second");
        using var query = _fixture.CreateQuery();
        var result = query.Query<int>().AppendSelect("Count(*)").AppendFrom("samples").Scalar();

        Assert.Equal(2, result);
    }

    /// <summary>
    /// 测试 - SQLite应执行Single查询。
    /// </summary>
    [Fact]
    public async Task ExecuteSingle_ShouldReturnActualRow()
    {
        await InsertAsync("single");
        using var query = _fixture.CreateQuery();
        var description = query.Query<Sample>().Select("Id,Name,Amount").From("samples")
            .AppendWhere("Name=@name").AddParam("name", "single");

        var result = description.FirstOrDefault();

        Assert.Equal("single", result.Name);
    }

    /// <summary>
    /// 测试 - SQLite 应执行 AppendFrom 原始子查询并绑定显式参数。
    /// </summary>
    [Fact]
    public async Task AppendFrom_WithRawParameter_ShouldExecuteSuccessfully()
    {
        // Arrange
        using (var executor = _fixture.CreateExecutor())
        {
            await executor.ExecuteSqlAsync("Insert Into Orders(Id, TenantId, Name) Values (@id, @tenantId, @name)",
                new { id = 1, tenantId = "tenant-1", name = "first" });
            await executor.ExecuteSqlAsync("Insert Into Orders(Id, TenantId, Name) Values (@id, @tenantId, @name)",
                new { id = 2, tenantId = "tenant-2", name = "second" });
        }
        using var query = _fixture.CreateQuery();
        var description = query.Query<OrderSample>().Select("o.Id,o.Name")
            .AppendFrom("(Select * From Orders Where TenantId=@TenantId) o")
            .AddParam("TenantId", "tenant-1");

        // Act
        var firstSql = description.ToSql();
        var secondSql = description.ToSql();

        // Assert
        Assert.Equal("Select `o`.`Id`,`o`.`Name` \r\nFrom (Select * From Orders Where TenantId=@TenantId) o", firstSql);
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(new[] { "@TenantId" }, description.GetParams().Keys);
        Assert.Equal("tenant-1", description.GetParam("TenantId"));

        // Act
        var result = description.ToList();

        // Assert
        var order = Assert.Single(result);
        Assert.Equal(1, order.Id);
        Assert.Equal("first", order.Name);
    }

    /// <summary>
    /// 测试 - SQLite 原始子查询参数应与结构化 Where 参数共同真实执行。
    /// </summary>
    [Fact]
    public async Task AppendFrom_WithRawAndStructuredParameters_ShouldExecuteSuccessfully()
    {
        // Arrange
        using (var executor = _fixture.CreateExecutor())
        {
            await executor.ExecuteSqlAsync("Insert Into Orders(Id, TenantId, Name) Values (@id, @tenantId, @name)",
                new { id = 1, tenantId = "tenant-1", name = "first" });
            await executor.ExecuteSqlAsync("Insert Into Orders(Id, TenantId, Name) Values (@id, @tenantId, @name)",
                new { id = 2, tenantId = "tenant-1", name = "second" });
        }
        using var query = _fixture.CreateQuery();
        var description = query.Query<OrderSample>().Select("o.Id,o.Name")
            .AppendFrom("(Select * From Orders Where TenantId=@TenantId) o")
            .AddParam("TenantId", "tenant-1")
            .Where("o.Name", "second");

        // Act
        var sql = description.ToSql();
        var result = description.FirstOrDefault();

        // Assert
        Assert.Equal("Select `o`.`Id`,`o`.`Name` \r\nFrom (Select * From Orders Where TenantId=@TenantId) o \r\nWhere `o`.`Name`=@_p_0", sql);
        Assert.Equal(2, result.Id);
        Assert.Equal("second", result.Name);
    }

    /// <summary>
    /// 测试 - SQLite 类型化 From 应经由实体映射和结构化表引用执行查询。
    /// </summary>
    [Fact]
    public async Task ExecuteQuery_WhenUsingTypedFrom_ShouldUseStructuredTableReference()
    {
        // Arrange
        await InsertAsync("typed-from");
        using var query = _fixture.CreateQuery();
        var description = query.From<SqliteStructuredTableSample>()
            .ClearSelect()
            .Select(sample => sample.Name);

        // Act
        var result = description.Scalar<string>();

        // Assert
        Assert.Equal("typed-from", result);
    }

    /// <summary>
    /// 测试 - SQLite应正确绑定显式空参数。
    /// </summary>
    [Fact]
    public async Task ExecuteSql_ShouldBindExplicitNullParameter()
    {
        using var executor = _fixture.CreateExecutor();
        executor.ExecuteSql<Sample>("Insert Into samples(Name, Amount) Values (@name, @amount)",
            new { name = (string)null, amount = 3.5m }, map => map
                .Add("name", t => t.Name, null)
                .Map("amount", t => t.Amount));

        var names = await _fixture.ReadNamesAsync();

        Assert.Single(names);
        Assert.Null(names[0]);
    }

    /// <summary>
    /// 测试 - SQLite部分参数映射不应丢失未映射参数。
    /// </summary>
    [Fact]
    public async Task ExecuteSql_ShouldKeepUnmappedParametersWhenPartiallyMapped()
    {
        using var executor = _fixture.CreateExecutor();
        executor.ExecuteSql<Sample>("Insert Into samples(Name, Amount) Values (@name, @amount)",
            new { name = "mapped", amount = 17.25m }, map => map.Map("name", t => t.Name));

        await using var connection = new SqliteConnection(_fixture.FirstConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "Select Amount From samples Where Name='mapped'";
        var result = Convert.ToDecimal(await command.ExecuteScalarAsync());

        Assert.Equal(17.25m, result);
    }

    /// <summary>
    /// 测试 - SQLite同步列表查询应支持buffered为false。
    /// </summary>
    [Fact]
    public async Task ExecuteQuery_ShouldSupportBufferedFalse()
    {
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        var result = CreateSamplesDescription(query).AsEnumerable().ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal(3, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试 - SQLite异步列表查询应支持buffered为false。
    /// </summary>
    [Fact]
    public async Task ExecuteQueryAsync_ShouldSupportBufferedFalse()
    {
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        var result = new List<Sample>();
        await foreach (var item in CreateSamplesDescription(query).AsAsyncEnumerable())
            result.Add(item);

        Assert.Equal(3, result.Count);
    }

    /// <summary>
    /// 测试 - SQLite缓冲和非缓冲列表结果应一致。
    /// </summary>
    [Fact]
    public async Task ExecuteQuery_ShouldReturnSameRowsForBufferedModes()
    {
        await SeedAsync();
        using var bufferedQuery = _fixture.CreateQuery();
        using var nonBufferedQuery = _fixture.CreateQuery();

        var buffered = CreateSamplesDescription(bufferedQuery).ToList();
        var nonBuffered = CreateSamplesDescription(nonBufferedQuery).AsEnumerable().ToList();

        Assert.Equal(buffered.Select(t => t.Name), nonBuffered.Select(t => t.Name));
    }

    /// <summary>
    /// 测试 - SQLite非缓冲列表查询仍应返回完整结果。
    /// </summary>
    [Fact]
    public async Task ExecuteQuery_ShouldMaterializeAllRowsWhenBufferedFalse()
    {
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        var result = CreateSamplesDescription(query).AsEnumerable().ToList();

        Assert.Equal(new[] { "one", "two", "three" }, result.Select(t => t.Name));
    }

    /// <summary>
    /// 测试 - SQLite非缓冲列表查询应在方法返回前完成物化。
    /// </summary>
    [Fact]
    public async Task ExecuteQuery_ShouldCompleteMaterializationBeforeReturning()
    {
        await SeedAsync();
        List<Sample> result;
        using (var query = _fixture.CreateQuery())
            result = CreateSamplesDescription(query).AsEnumerable().ToList();

        using var executor = _fixture.CreateExecutor();
        executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = "after-query" });

        Assert.Equal(3, result.Count);
        Assert.Equal(4, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试 - SQLite同步流式查询应完整返回全部结果。
    /// </summary>
    [Fact]
    public async Task StreamQuery_ShouldReturnAllRows()
    {
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        var result = CreateSamplesDescription(query).AsEnumerable().Select(t => t.Name).ToList();

        Assert.Equal(new[] { "one", "two", "three" }, result);
    }

    /// <summary>
    /// 测试 - SQLite异步流式查询应完整返回全部结果。
    /// </summary>
    [Fact]
    public async Task StreamAsync_ShouldReturnAllRows()
    {
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var result = new List<string>();

        await foreach (var sample in CreateSamplesDescription(query).AsAsyncEnumerable())
            result.Add(sample.Name);

        Assert.Equal(new[] { "one", "two", "three" }, result);
    }

    /// <summary>
    /// 测试 - SQLite流式查询提前终止应释放Reader。
    /// </summary>
    [Fact]
    public async Task StreamQuery_ShouldReleaseReaderWhenEnumerationStopsEarly()
    {
        await SeedAsync();
        using (var query = _fixture.CreateQuery())
        using (var enumerator = CreateSamplesDescription(query).AsEnumerable().GetEnumerator())
        {
            Assert.True(enumerator.MoveNext());
            Assert.Equal("one", enumerator.Current.Name);
        }

        await InsertAsync("after-stream");

        Assert.Equal(4, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：同步流式 Reader 存活期间，同一 Query 的其他执行入口必须快速失败；释放枚举器后应可继续执行。
    /// </summary>
    [Fact]
    public async Task StreamQuery_WhenEnumeratorIsActive_ShouldRejectOtherExecutionUntilDisposed()
    {
        // Arrange
        await SeedAsync();
        using var query = CreateSamplesQuery();

        // Act and Assert
        using (var enumerator = CreateSamplesDescription(query).AsEnumerable().GetEnumerator())
        {
            Assert.True(enumerator.MoveNext());
            var exception = Assert.Throws<InvalidOperationException>(() => query.Query<int>()
                .AppendSelect("Count(*)").AppendFrom("samples").Scalar());
            Assert.Equal("同一个 SQL Query 或 Executor 实例不支持并发执行，请为每个操作创建独立实例。",
                exception.Message);
        }

        Assert.Equal(3, query.Query<int>().AppendSelect("Count(*)").AppendFrom("samples").Scalar());
    }

    /// <summary>
    /// 测试 - SQLite流式查询枚举前取消应释放资源。
    /// </summary>
    [Fact]
    public async Task StreamAsync_ShouldReleaseResourcesWhenCancelledBeforeEnumeration()
    {
        await SeedAsync();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        using var query = CreateSamplesQuery();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in CreateSamplesDescription(query)
                               .AsAsyncEnumerable(cancellationToken: cancellationTokenSource.Token))
            {
            }
        });

        await InsertAsync("after-cancel-before-enumeration");
        Assert.Equal(4, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试 - SQLite流式查询枚举过程中取消应释放资源。
    /// </summary>
    [Fact]
    public async Task StreamAsync_ShouldReleaseResourcesWhenCancelledDuringEnumeration()
    {
        await SeedAsync();
        using var cancellationTokenSource = new CancellationTokenSource();
        using var query = CreateSamplesQuery();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in CreateSamplesDescription(query)
                               .AsAsyncEnumerable(cancellationToken: cancellationTokenSource.Token))
                cancellationTokenSource.Cancel();
        });

        await InsertAsync("after-cancel-during-enumeration");
        Assert.Equal(4, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试 - SQLite流式查询结束后同一数据库应可继续写入。
    /// </summary>
    [Fact]
    public async Task StreamAsync_ShouldAllowWriteAfterEnumerationCompletes()
    {
        await SeedAsync();
        using (var query = CreateSamplesQuery())
        {
            await foreach (var _ in CreateSamplesDescription(query).AsAsyncEnumerable())
            {
            }
        }

        await InsertAsync("after-complete-stream");

        Assert.Equal(4, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试 - SQLite事务提交后数据应持久化。
    /// </summary>
    [Fact]
    public async Task TransactionScope_ShouldPersistDataAfterCommit()
    {
        using (var scope = _fixture.GetTransactionScopeFactory().Begin("first"))
        using (var executor = scope.CreateExecutor())
        {
            executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = "committed" });
            scope.Commit();
        }

        Assert.Equal(new[] { "committed" }, await _fixture.ReadNamesAsync());
    }

    /// <summary>
    /// 测试 - SQLite事务回滚后数据不应持久化。
    /// </summary>
    [Fact]
    public async Task TransactionScope_ShouldNotPersistDataAfterRollback()
    {
        using (var scope = _fixture.GetTransactionScopeFactory().Begin("first"))
        using (var executor = scope.CreateExecutor())
        {
            executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = "rolled-back" });
            scope.Rollback();
        }

        Assert.Empty(await _fixture.ReadNamesAsync());
    }

    /// <summary>
    /// 测试 - SQLite未完成事务作用域释放时应自动回滚。
    /// </summary>
    [Fact]
    public async Task TransactionScope_ShouldRollbackWhenDisposedWithoutCompletion()
    {
        using (var scope = _fixture.GetTransactionScopeFactory().Begin("first"))
        using (var executor = scope.CreateExecutor())
            executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = "implicit-rollback" });

        Assert.Empty(await _fixture.ReadNamesAsync());
    }

    /// <summary>
    /// 测试目的：异步事务作用域未完成即释放时，必须通过异步释放路径回滚未提交数据，且连接资源可立即复用。
    /// </summary>
    [Fact]
    public async Task TransactionScope_ShouldRollbackWhenAsyncDisposedWithoutCompletion()
    {
        // Arrange and Act
        await using (var scope = await _fixture.GetTransactionScopeFactory().BeginAsync("first"))
        using (var executor = scope.CreateExecutor())
        {
            await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)",
                new { name = "implicit-async-rollback" });
        }

        // Assert
        Assert.Empty(await _fixture.ReadNamesAsync());

        await InsertAsync("after-implicit-async-rollback");
        Assert.Equal(new[] { "after-implicit-async-rollback" }, await _fixture.ReadNamesAsync());
    }

    /// <summary>
    /// 测试目的：事务作用域子查询持有活动异步流时，异步释放必须被拒绝且不提交或回滚；释放流后可正常回滚。
    /// </summary>
    [Fact]
    public async Task TransactionScope_WhenChildAsyncStreamIsActive_ShouldRejectDisposeAsyncWithoutStateChanges()
    {
        // Arrange
        await SeedAsync();
        await using var scope = await _fixture.GetTransactionScopeFactory().BeginAsync("first");
        using var query = scope.CreateQuery();

        // Act
        await using (var enumerator = CreateSamplesDescription(query).AsAsyncEnumerable().GetAsyncEnumerator())
        {
            Assert.True(await enumerator.MoveNextAsync());
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask());

            // Assert
            Assert.Equal("当前 SQL Query 或 Executor 正在执行，不能释放 Root 对象。", exception.Message);
            Assert.False(scope.IsCompleted);
        }

        await scope.RollbackAsync();

        // Assert
        Assert.True(scope.IsCompleted);
        Assert.Equal(3, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试 - SQLite事务作用域创建的Query和Executor应共享事务。
    /// </summary>
    [Fact]
    public async Task TransactionScope_ShouldShareTransactionBetweenQueryAndExecutor()
    {
        using (var scope = _fixture.GetTransactionScopeFactory().Begin("first"))
        using (var executor = scope.CreateExecutor())
        using (var query = scope.CreateQuery())
        {
            executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = "shared" });
            Assert.Equal(1, query.Query<int>().AppendSelect("Count(*)").AppendFrom("samples").Scalar());
            scope.Commit();
        }

        Assert.Equal(1, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试 - SQLite事务开始后切换环境dbKey不应影响当前事务。
    /// </summary>
    [Fact]
    public async Task TransactionScope_ShouldKeepCapturedDataSourceAfterAmbientScopeChanges()
    {
        var manager = _fixture.GetDatabaseScopeManager();
        using (manager.Use("first"))
        using (var scope = _fixture.GetTransactionScopeFactory().Begin())
        {
            using (manager.Use("second"))
            using (var executor = scope.CreateExecutor())
                executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = "captured" });
            scope.Commit();
        }

        Assert.Equal(new[] { "captured" }, await _fixture.ReadNamesAsync("first"));
        Assert.Empty(await _fixture.ReadNamesAsync("second"));
    }

    /// <summary>
    /// 测试 - SQLite异步事务提交后数据应持久化且作用域应标记为完成。
    /// </summary>
    [Fact]
    public async Task TransactionScope_ShouldPersistDataAfterAsyncCommit()
    {
        await using var scope = await _fixture.GetTransactionScopeFactory().BeginAsync("first");
        using var executor = scope.CreateExecutor();
        await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "async-committed" });

        await scope.CommitAsync();

        Assert.True(scope.IsCompleted);
        Assert.Equal(new[] { "async-committed" }, await _fixture.ReadNamesAsync());
        Assert.Throws<ObjectDisposedException>(() => scope.CreateQuery());
    }

    /// <summary>
    /// 测试 - SQLite事务作用域应保留请求的隔离级别并允许显式异步回滚。
    /// </summary>
    [Fact]
    public async Task TransactionScope_ShouldUseRequestedIsolationLevelAndRollbackAsync()
    {
        await using var scope = await _fixture.GetTransactionScopeFactory().BeginAsync("first",
            System.Data.IsolationLevel.Serializable);
        using var executor = scope.CreateExecutor();
        await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "async-rolled-back" });

        Assert.Equal(System.Data.IsolationLevel.Serializable, scope.IsolationLevel);
        await scope.RollbackAsync();

        Assert.True(scope.IsCompleted);
        Assert.Empty(await _fixture.ReadNamesAsync());
    }

    /// <summary>
    /// 测试 - SQLite事务作用域释放后子对象不应继续执行。
    /// </summary>
    [Fact]
    public void TransactionScope_ShouldRejectCreatingChildrenAfterDisposal()
    {
        var scope = _fixture.GetTransactionScopeFactory().Begin("first");
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scope.CreateExecutor());
    }

    /// <summary>
    /// 测试 - SQLite固定装置应拒绝未知数据源键，防止错误落到默认库。
    /// </summary>
    [Fact]
    public void Fixture_ShouldRejectUnknownDataSourceKey()
    {
        var exception = Assert.Throws<KeyNotFoundException>(() => _fixture.GetConnectionString("unknown"));

        Assert.Contains("unknown", exception.Message);
    }

    /// <summary>
    /// 测试 - SQLite多个数据源应分别插入和查询且数据不串库。
    /// </summary>
    [Fact]
    public async Task DataSources_ShouldKeepDataIsolated()
    {
        await InsertAsync("first", "first");
        await InsertAsync("second", "second");

        Assert.Equal(new[] { "first" }, await _fixture.ReadNamesAsync("first"));
        Assert.Equal(new[] { "second" }, await _fixture.ReadNamesAsync("second"));
    }

    /// <summary>
    /// 测试 - SQLite嵌套数据库作用域释放后应恢复父作用域。
    /// </summary>
    [Fact]
    public async Task DatabaseScope_ShouldRestoreParentAfterNestedScopeDisposed()
    {
        var manager = _fixture.GetDatabaseScopeManager();
        using (manager.Use("first"))
        {
            await InsertAsync("first-scope");
            using (manager.Use("second"))
                await InsertAsync("second-scope", "second");
            await InsertAsync("first-restored");
        }

        Assert.Equal(new[] { "first-scope", "first-restored" }, await _fixture.ReadNamesAsync("first"));
        Assert.Equal(new[] { "second-scope" }, await _fixture.ReadNamesAsync("second"));
    }

    /// <summary>
    /// 测试 - 子数据库作用域异常退出后应恢复父上下文，并将后续写入落到父数据库文件。
    /// </summary>
    [Fact]
    public async Task DatabaseScope_WhenChildThrows_ShouldRestoreParentContextAndTargetFile()
    {
        // Arrange
        var manager = _fixture.GetDatabaseScopeManager();
        var accessor = _fixture.ServiceProvider.GetRequiredService<IDatabaseContextAccessor>();
        var executorFactory = _fixture.ServiceProvider.GetRequiredService<ISqlExecutorFactory>();

        // Act
        using (manager.Use("first"))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                using (manager.Use("second"))
                {
                    Assert.Equal("second", accessor.Current.DbKey);
                    using var childExecutor = executorFactory.Create();
                    await childExecutor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)",
                        new { name = "child-exception" });
                    throw new InvalidOperationException("expected child failure");
                }
            });

            Assert.Equal("first", accessor.Current.DbKey);
            using var parentExecutor = executorFactory.Create();
            await parentExecutor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)",
                new { name = "parent-after-exception" });
        }

        // Assert
        Assert.Equal(new[] { "parent-after-exception" }, await _fixture.ReadNamesAsync("first"));
        Assert.Equal(new[] { "child-exception" }, await _fixture.ReadNamesAsync("second"));
        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 测试 - 子数据库作用域取消退出后应恢复父上下文，并将后续写入落到父数据库文件。
    /// </summary>
    [Fact]
    public async Task DatabaseScope_WhenChildIsCancelled_ShouldRestoreParentContextAndTargetFile()
    {
        // Arrange
        var manager = _fixture.GetDatabaseScopeManager();
        var accessor = _fixture.ServiceProvider.GetRequiredService<IDatabaseContextAccessor>();
        var executorFactory = _fixture.ServiceProvider.GetRequiredService<ISqlExecutorFactory>();
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        using (manager.Use("first"))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                using (manager.Use("second"))
                {
                    Assert.Equal("second", accessor.Current.DbKey);
                    using var childExecutor = executorFactory.Create();
                    await childExecutor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)",
                        new { name = "child-cancelled" });
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                }
            });

            Assert.Equal("first", accessor.Current.DbKey);
            using var parentExecutor = executorFactory.Create();
            await parentExecutor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)",
                new { name = "parent-after-cancellation" });
        }

        // Assert
        Assert.Equal(new[] { "parent-after-cancellation" }, await _fixture.ReadNamesAsync("first"));
        Assert.Equal(new[] { "child-cancelled" }, await _fixture.ReadNamesAsync("second"));
        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 测试 - SQLite并行数据库作用域应保持隔离。
    /// </summary>
    [Fact]
    public async Task DatabaseScope_ShouldKeepParallelOperationsIsolated()
    {
        var manager = _fixture.GetDatabaseScopeManager();
        await Task.WhenAll(
            InsertInScopeAsync(manager, "first", "parallel-first"),
            InsertInScopeAsync(manager, "second", "parallel-second"));

        Assert.Equal(new[] { "parallel-first" }, await _fixture.ReadNamesAsync("first"));
        Assert.Equal(new[] { "parallel-second" }, await _fixture.ReadNamesAsync("second"));
    }

    /// <summary>
    /// 测试 - SQLite Query Factory显式dbKey应使用指定数据源。
    /// </summary>
    [Fact]
    public async Task QueryFactory_ShouldUseExplicitDatabaseKey()
    {
        var manager = _fixture.GetDatabaseScopeManager();
        await InsertAsync("query-factory", "second");
        using (manager.Use("second"))
        using (var query = _fixture.CreateQuery("second"))
        {
            Assert.Equal(1, query.Query<int>().AppendSelect("Count(*)").AppendFrom("samples").Scalar());
        }
        Assert.Empty(await _fixture.ReadNamesAsync("first"));
    }

    /// <summary>
    /// 测试 - SQLite Executor Factory显式dbKey应使用指定数据源。
    /// </summary>
    [Fact]
    public async Task ExecutorFactory_ShouldUseExplicitDatabaseKey()
    {
        await InsertAsync("executor-factory", "second");

        Assert.Empty(await _fixture.ReadNamesAsync("first"));
        Assert.Equal(new[] { "executor-factory" }, await _fixture.ReadNamesAsync("second"));
    }

    /// <summary>
    /// 测试 - SQLite 应真实执行统一 Count、列计数和 Distinct 聚合。
    /// </summary>
    [Fact]
    public async Task Aggregate_WhenDuplicateAndNullValuesExist_ShouldReturnExpectedCountsAndExtremes()
    {
        // Arrange
        await SeedAggregateSamplesAsync();

        // Act
        using var query = _fixture.CreateQuery();
        var countAll = CreateAggregateDescription<int>(query).CountAll("Total").Scalar();
        var countColumn = CreateAggregateDescription<int>(query).CountColumn("s.Amount", "AmountCount").Scalar();
        var distinctCount = CreateAggregateDescription<int>(query).CountColumn("s.Name", "NameCount", distinct: true)
            .Scalar();
        var sum = CreateAggregateDescription<decimal>(query).Sum("s.Amount", "Total").Scalar();
        var average = CreateAggregateDescription<decimal>(query).Avg("s.Amount", "Average", distinct: true)
            .Scalar();
        var maximum = CreateAggregateDescription<decimal>(query).Max("s.Amount", "Maximum", distinct: true)
            .Scalar();
        var minimum = CreateAggregateDescription<decimal>(query).Min("s.Amount", "Minimum", distinct: true)
            .Scalar();

        // Assert
        Assert.Equal(4, countAll);
        Assert.Equal(3, countColumn);
        Assert.Equal(2, distinctCount);
        Assert.Equal(40m, sum);
        Assert.Equal(15m, average);
        Assert.Equal(20m, maximum);
        Assert.Equal(10m, minimum);
    }

    /// <summary>
    /// 测试 - SQLite 应真实执行 Raw、可转换表达式和聚合别名映射。
    /// </summary>
    [Fact]
    public async Task AggregateRawAndExpression_WhenConfigured_ShouldExecuteAndMapAliases()
    {
        // Arrange
        await SeedAggregateSamplesAsync();

        // Act
        using var query = _fixture.CreateQuery();
        var rawTotal = query.Query<decimal>().AggregateRaw(SqlAggregateFunction.Sum, "Amount * 2", "DoubleTotal")
            .From("samples")
            .Scalar();
        var expressionTotal = CreateAggregateDescription<decimal>(query).AggregateExpression(SqlAggregateFunction.Sum,
                "[s].[Amount] * 2", "DoubleTotal")
            .Scalar();
        var caseCount = CreateAggregateDescription<int>(query).AggregateExpression(SqlAggregateFunction.Count,
                "Case When [s].[Amount] Is Not Null Then [s].[Name] End", "NamedCount", distinct: true)
            .Scalar();
        var result = CreateAggregateDescription<AggregateResult>(query)
            .CountColumn("s.Name", "DistinctNameCount", distinct: true)
            .Sum("s.Amount", "DistinctAmount", distinct: true)
            .FirstOrDefault();

        // Assert
        Assert.Equal(80m, rawTotal);
        Assert.Equal(80m, expressionTotal);
        Assert.Equal(2, caseCount);
        Assert.Equal(2, result.DistinctNameCount);
        Assert.Equal(30m, result.DistinctAmount);
    }

    /// <summary>
    /// 测试目的：Root Query 释放后，已有 Fluent、原生文本、根执行和流式描述均不得重新创建连接或继续执行。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenRootQueryDisposed_ShouldRejectAllExecutionEntrypoints()
    {
        // Arrange
        await SeedAsync();
        var rootQuery = _fixture.CreateQuery();
        var fluent = rootQuery.Query<Sample>().Select("Id,Name,Amount").From("samples");
        var text = rootQuery.Sql<Sample>("Select Id,Name,Amount From samples");
        rootQuery.Dispose();
        rootQuery.Dispose();

        // Act and Assert
        Assert.Throws<ObjectDisposedException>(() => rootQuery.Query<Sample>().Select("Id,Name,Amount").From("samples").ToList());
        Assert.Throws<ObjectDisposedException>(() => fluent.ToList());
        Assert.Throws<ObjectDisposedException>(() => text.ToList());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fluent.ToListAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => text.ToListAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await foreach (var _ in fluent.AsAsyncEnumerable())
            {
            }
        });
    }

    /// <summary>
    /// 测试目的：独立 Fluent 查询描述应通过根查询的执行链返回完整列表和正确的 First/Single 基数语义。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenFluentQueryExecuted_ShouldMaterializeAndEnforceCardinality()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var list = await query.Query<Sample>().Select("Id,Name,Amount").From("samples").OrderBy("Id").ToListAsync();
        var first = await query.Query<Sample>().Select("Id,Name,Amount").From("samples").OrderBy("Id").FirstAsync();
        var only = await query.Sql<Sample>("Select Id,Name,Amount From samples Where Name = @name",
            new { name = "two" }).SingleAsync();
        var firstMissing = await query.Sql<Sample>("Select Id,Name,Amount From samples Where Name = @name",
            new { name = "missing" }).FirstOrDefaultAsync();
        var missing = await query.Sql<Sample>("Select Id,Name,Amount From samples Where Name = @name",
            new { name = "missing" }).SingleOrDefaultAsync();

        // Assert
        Assert.Equal(new[] { "one", "two", "three" }, list.Select(item => item.Name));
        Assert.Equal("one", first.Name);
        Assert.Equal("two", only.Name);
        Assert.Null(firstMissing);
        Assert.Null(missing);
        await Assert.ThrowsAsync<InvalidOperationException>(() => query.Query<Sample>()
            .Select("Id,Name,Amount").From("samples").SingleAsync());
    }

    /// <summary>
    /// 测试目的：独立查询描述的同步终结方法应完整物化结果，并保持 First 和 Single 的基数语义。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenSynchronouslyExecuted_ShouldMaterializeAndEnforceCardinality()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var list = query.Query<Sample>().Select("Id,Name,Amount").From("samples").OrderBy("Id").ToList();
        var first = query.Query<Sample>().Select("Id,Name,Amount").From("samples").OrderBy("Id").First();
        var only = query.Sql<Sample>("Select Id,Name,Amount From samples Where Name = @name",
            new { name = "two" }).Single();
        var firstMissing = query.Sql<Sample>("Select Id,Name,Amount From samples Where Name = @name",
            new { name = "missing" }).FirstOrDefault();
        var singleMissing = query.Sql<Sample>("Select Id,Name,Amount From samples Where Name = @name",
            new { name = "missing" }).SingleOrDefault();

        // Assert
        Assert.Equal(new[] { "one", "two", "three" }, list.Select(item => item.Name));
        Assert.Equal("one", first.Name);
        Assert.Equal("two", only.Name);
        Assert.Null(firstMissing);
        Assert.Null(singleMissing);
        Assert.Throws<InvalidOperationException>(() => query.Query<Sample>()
            .Select("Id,Name,Amount").From("samples").Single());
        Assert.Throws<InvalidOperationException>(() => query.Sql<Sample>(
            "Select Id,Name,Amount From samples Where Name = @name", new { name = "missing" }).First());
    }

    /// <summary>
    /// 测试目的：同一 Root 创建的独立查询描述在执行后应保持各自 SQL 和参数状态，确保可顺序复用。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenExecuted_ShouldKeepDescriptionBuildersIsolated()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var selected = query.Query<Sample>().Select("Id,Name,Amount").From("samples").Where("Name", "one");
        var selectedSql = selected.ToSql();
        var selectedParameters = selected.GetParams();
        var countDescription = query.Query<int>().CountAll().From("samples");

        // Act
        var descriptions = selected.ToList();
        var count = countDescription.Scalar();

        // Assert
        Assert.Single(descriptions);
        Assert.Equal("one", descriptions[0].Name);
        Assert.Equal(3, count);
        Assert.Equal(selectedSql, selected.ToSql());
        Assert.Single(selectedParameters);
        Assert.Equal("one", selectedParameters["@_p_0"]);
        Assert.Equal("Select Count(*) \r\nFrom `samples`", countDescription.ToSql());
    }

    /// <summary>
    /// 测试目的：Fluent 查询描述应可直接参与子查询、派生表、CTE 和 Union，调用方无需访问内部 Builder。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenComposedFromDescriptions_ShouldExecuteWithoutBuilderEscapeHatch()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var selectedIds = query.Query<int>().Select("Id").From("samples").Where("Name", "two");
        var selectedNames = query.Query<string>().Select("Name").From("samples").Where("Name", "two");
        var selectedSamples = query.Query<Sample>().Select("Id,Name,Amount").From("samples").Where("Name", "two");

        // Act
        var inResult = query.Query<Sample>().Select("Id,Name,Amount").From("samples").In("Id", selectedIds).ToList();
        var notInResult = query.Query<Sample>().Select("Id,Name,Amount").From("samples").NotIn("Id", selectedIds).ToList();
        var existsResult = query.Query<Sample>().Select("Id,Name,Amount").From("samples").Exists(selectedIds).ToList();
        var notExistsResult = query.Query<Sample>().Select("Id,Name,Amount").From("samples").NotExists(selectedIds).ToList();
        var derivedResult = query.Query<Sample>().Select("selected.Id,selected.Name,selected.Amount")
            .From(selectedSamples, "selected").ToList();
        var cteResult = query.Query<Sample>().With("selected", selectedSamples)
            .Select("Id,Name,Amount").From("selected").ToList();
        var unionResult = query.Query<string>().Select("Name").From("samples").Where("Name", "one")
            .Union(selectedNames).ToList();

        // Assert
        Assert.Equal(new[] { "two" }, inResult.Select(item => item.Name));
        Assert.Equal(new[] { "one", "three" }, notInResult.OrderBy(item => item.Name).Select(item => item.Name));
        Assert.Equal(new[] { "one", "two", "three" }, existsResult.Select(item => item.Name));
        Assert.Empty(notExistsResult);
        Assert.Equal(new[] { "two" }, derivedResult.Select(item => item.Name));
        Assert.Equal(new[] { "two" }, cteResult.Select(item => item.Name));
        Assert.Equal(new[] { "one", "two" }, unionResult.OrderBy(item => item));
    }

    /// <summary>
    /// 测试目的：独立子描述与外层产生连续同名参数时，应分别绑定每个子条件，且组合后可稳定重复渲染和执行。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenInChildUsesSequentialConflictingParameters_ShouldKeepBindingsIsolated()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var child = query.Query<int>().Select("Id").From("samples")
            .Where("Name", "one")
            .Where("Name", "two");
        var description = query.Query<Sample>().Select("Id,Name,Amount").From("samples")
            .Where("Name", "two")
            .In("Id", child);
        var childSql = child.ToSql();

        // Act
        var firstSql = description.ToSql();
        var firstResult = await description.ToListAsync();
        var secondSql = description.ToSql();
        var secondResult = await description.ToListAsync();

        // Assert
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(childSql, child.ToSql());
        Assert.Empty(firstResult);
        Assert.Empty(secondResult);
    }

    /// <summary>
    /// 测试目的：结构化 Fluent 查询描述分页时应独立生成计数和数据页 SQL，且不污染可复用描述的排序和分页状态。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenPaged_ShouldReturnTotalAndKeepDescriptionReusable()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var description = query.Query<Sample>().Select("Id,Name,Amount").From("samples");

        // Act
        var firstPage = description.ToPage(new Pager(1, 2) { Order = "Name" });
        var secondPage = await description.ToPageAsync(new Pager(2, 1) { Order = "Name" });
        var allItems = description.ToList();

        // Assert
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(new[] { "one", "three" }, firstPage.Data.Select(item => item.Name));
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Equal(new[] { "three" }, secondPage.Data.Select(item => item.Name));
        Assert.Equal(new[] { "one", "three", "two" }, allItems.OrderBy(item => item.Name).Select(item => item.Name));
    }

    /// <summary>
    /// 测试目的：Distinct 查询分页应对去重后的投影计数，而不是对来源表原始记录计数。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenDistinctPaged_ShouldCountDistinctRows()
    {
        // Arrange
        await SeedAsync();
        await InsertAsync("one");
        using var query = _fixture.CreateQuery();
        var description = query.Query<string>().Distinct().Select("Name").From("samples");

        // Act
        var page = description.ToPage(new Pager(1, 2) { Order = "Name" });

        // Assert
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new[] { "one", "three" }, page.Data);
    }

    /// <summary>
    /// 测试目的：带 Having 的分组查询分页应以满足 Having 条件的分组数量作为总数，并支持同步和异步执行。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenGroupedHavingPaged_ShouldCountFilteredGroups()
    {
        // Arrange
        await SeedAggregateSamplesAsync();
        using var query = _fixture.CreateQuery();
        var description = query.Query<string>().Select("Name").CountAll("Total").From("samples")
            .GroupBy("Name")
            .HavingRaw("[Total] > 1");

        // Act
        var syncPage = description.ToPage(new Pager(1, 1) { Order = "Name" });
        var asyncPage = await description.ToPageAsync(new Pager(1, 1) { Order = "Name" });

        // Assert
        Assert.Equal(1, syncPage.TotalCount);
        Assert.Equal(new[] { "A" }, syncPage.Data);
        Assert.Equal(1, asyncPage.TotalCount);
        Assert.Equal(new[] { "A" }, asyncPage.Data);
    }

    /// <summary>
    /// 测试目的：无 Group By 的聚合查询分页时，总数应为聚合结果集行数而非来源表行数。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenAggregateWithoutGroupIsPaged_ShouldCountAggregateResultRows()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var description = query.Query<int>().CountAll("Total").From("samples");

        // Act
        var syncPage = description.ToPage(new Pager(1, 1, "Total"));
        var asyncPage = await description.ToPageAsync(new Pager(1, 1, "Total"));

        // Assert
        Assert.Equal(1, syncPage.TotalCount);
        Assert.Equal(new[] { 3 }, syncPage.Data);
        Assert.Equal(1, asyncPage.TotalCount);
        Assert.Equal(new[] { 3 }, asyncPage.Data);
    }

    /// <summary>
    /// 测试目的：Union 与 Union All 分页应分别按去重和保留重复的结果集计算总数。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenUnionPaged_ShouldCountDistinctAndAllRows()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var selectedName = query.Query<string>().Select("Name").From("samples").Where("Name", "one");
        var union = query.Query<string>().Select("Name").From("samples").Where("Name", "one")
            .Union(selectedName);
        var unionAll = query.Query<string>().Select("Name").From("samples").Where("Name", "one")
            .UnionAll(selectedName);

        // Act
        var unionPage = union.ToPage(new Pager(1, 2) { Order = "Name" });
        var unionAllPage = await unionAll.ToPageAsync(new Pager(1, 2) { Order = "Name" });

        // Assert
        Assert.Equal(1, unionPage.TotalCount);
        Assert.Equal(new[] { "one" }, unionPage.Data);
        Assert.Equal(2, unionAllPage.TotalCount);
        Assert.Equal(new[] { "one", "one" }, unionAllPage.Data);
    }

    /// <summary>
    /// 测试目的：CTE 与 Union 组合执行时必须隔离重复参数名，确保 CTE 与集合分支不会覆盖彼此的参数值。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenCteAndUnionAreComposed_ShouldExecuteWithIsolatedParameters()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var cteSource = query.Query<string>().Select("Name").From("samples").Where("Name", "one");
        var unionSource = query.Query<string>().Select("Name").From("samples").Where("Name", "two");
        var description = query.Query<string>().With("selected", cteSource).Select("Name").From("selected")
            .Union(unionSource);

        // Act
        var result = await description.ToListAsync();

        // Assert
        Assert.Equal(new[] { "one", "two" }, result.OrderBy(item => item));
    }

    /// <summary>
    /// 测试目的：递归 CTE 应使用 SQLite 的 With Recursive 语法执行 Union All，并在重复执行时保留参数绑定。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenRecursiveCteUsesUnionAll_ShouldExecuteExpectedSequence()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var recursiveSource = query.Query<int>().Distinct().AppendSelect("1 As Value").From("samples")
            .UnionAll(query.Query<int>().AppendSelect("Value + 1 As Value").From("numbers")
                .AppendWhere("Value < @maxValue").AddParam("maxValue", 3));
        var description = query.Query<int>().With("numbers", recursiveSource)
            .Select("Value").From("numbers").OrderBy("Value");

        // Act
        var sql = description.ToSql();
        var firstResult = await description.ToListAsync();
        var secondResult = await description.ToListAsync();

        // Assert
        Assert.StartsWith("With Recursive", sql);
        Assert.Equal(new[] { 1, 2, 3 }, firstResult);
        Assert.Equal(firstResult, secondResult);
    }

    /// <summary>
    /// 测试目的：邻接表树状查询应通过递归 CTE 返回完整层级、父子关系和稳定深度排序。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenRecursiveCteQueriesHierarchy_ShouldReturnOrderedTree()
    {
        // Arrange
        using (var executor = _fixture.CreateExecutor())
        {
            await executor.ExecuteSqlAsync(
                "Insert Into HierarchyNodes(Id,ParentId,Name) Values (@id,@parentId,@name)",
                new { id = 1, parentId = (int?)null, name = "root" });
            await executor.ExecuteSqlAsync(
                "Insert Into HierarchyNodes(Id,ParentId,Name) Values (@id,@parentId,@name)",
                new { id = 2, parentId = (int?)1, name = "child-a" });
            await executor.ExecuteSqlAsync(
                "Insert Into HierarchyNodes(Id,ParentId,Name) Values (@id,@parentId,@name)",
                new { id = 3, parentId = (int?)1, name = "child-b" });
            await executor.ExecuteSqlAsync(
                "Insert Into HierarchyNodes(Id,ParentId,Name) Values (@id,@parentId,@name)",
                new { id = 4, parentId = (int?)2, name = "leaf" });
        }
        using var query = _fixture.CreateQuery();
        var tree = query.Query<HierarchyNode>().AppendSelect("Id,ParentId,Name,0 As Depth").From("HierarchyNodes")
            .IsNull("ParentId")
            .UnionAll(query.Query<HierarchyNode>().AppendSelect("n.Id,n.ParentId,n.Name,t.Depth + 1 As Depth")
                .From("HierarchyNodes", "n").Join("tree", "t").AppendOn("n.ParentId=t.Id"));
        var description = query.Query<HierarchyNode>().With("tree", tree).Select("Id,ParentId,Name,Depth")
            .From("tree").OrderBy("Depth").OrderBy("Id");

        // Act
        var sql = description.ToSql();
        var firstResult = await description.ToListAsync();
        var secondResult = await description.ToListAsync();

        // Assert
        Assert.StartsWith("With Recursive", sql);
        Assert.Equal(new[] { "root:0", "child-a:1", "child-b:1", "leaf:2" },
            firstResult.Select(item => $"{item.Name}:{item.Depth}"));
        Assert.Equal(new int?[] { null, 1, 1, 2 }, firstResult.Select(item => item.ParentId));
        Assert.Equal(firstResult.Select(item => item.Name), secondResult.Select(item => item.Name));
    }

    /// <summary>
    /// 测试目的：参数化 CTE 作为 Left Join 来源时，应保留 CTE 参数、别名和未匹配行的 DTO 映射。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenParameterizedCteIsLeftJoined_ShouldMapMatchedAndUnmatchedRows()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var samples = await query.Query<Sample>().Select("Id,Name,Amount").From("samples").OrderBy("Id").ToListAsync();
        using (var executor = _fixture.CreateExecutor())
            await executor.ExecuteSqlAsync("Insert Into Orders(Id,TenantId,Name) Values (@id,@tenantId,@name)",
                new { id = samples[0].Id, tenantId = "tenant-1", name = "order-one" });
        var filteredSamples = query.Query<Sample>().Select("Id,Name,Amount").From("samples")
            .In("Name", new[] { "one", "two" });
        var description = query.Query<LambdaJoinResult>().With("selected", filteredSamples)
            .Select("s.Name,o.TenantId").From("selected", "s")
            .LeftJoin("Orders", "o").AppendOn("o.Id=s.Id")
            .OrderBy("s.Id");

        // Act
        var result = await description.ToListAsync();

        // Assert
        Assert.Equal(new[] { "one:tenant-1", "two:" }, result.Select(item => $"{item.Name}:{item.TenantId}"));
    }

    /// <summary>
    /// 测试目的：SQLite 不支持 Right Join 时，描述应在生成 SQL 前拒绝而不进入数据库执行路径。
    /// </summary>
    [Fact]
    public void SqlQueryPlan_WhenRightJoined_ShouldRejectBeforeExecuting()
    {
        // Arrange
        using var query = _fixture.CreateQuery();
        var description = query.Query<OrderSample>().Select("o.Id,o.Name").From("samples", "s")
            .RightJoin("Orders", "o").AppendOn("s.Id=o.Id")
            .OrderBy("o.Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => description.ToSql());

        // Assert
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Right Join。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Intersect 与 Except 分页应在 SQLite 中执行真实集合语义，并通过派生表计算正确总数。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenIntersectAndExceptArePaged_ShouldReturnExpectedRowsAndCounts()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var intersectSource = query.Query<string>().Select("Name").From("samples").Where("Name", "one");
        var exceptSource = query.Query<string>().Select("Name").From("samples").Where("Name", "two");
        var intersect = query.Query<string>().Select("Name").From("samples").Where("Name", "one")
            .Intersect(intersectSource);
        var except = query.Query<string>().Select("Name").From("samples").Where("Name", "one")
            .Union(query.Query<string>().Select("Name").From("samples").Where("Name", "two"))
            .Except(exceptSource);

        // Act
        var intersectPage = intersect.ToPage(new Pager(1, 1) { Order = "Name" });
        var exceptPage = await except.ToPageAsync(new Pager(1, 1) { Order = "Name" });

        // Assert
        Assert.Equal(1, intersectPage.TotalCount);
        Assert.Equal(new[] { "one" }, intersectPage.Data);
        Assert.Equal(1, exceptPage.TotalCount);
        Assert.Equal(new[] { "one" }, exceptPage.Data);
    }

    /// <summary>
    /// 测试目的：纯 CTE 查询应支持自动分页计数，而 CTE 与 Group 或 Union 组合时应明确拒绝不安全的自动计数。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenCtePaged_ShouldSupportSimpleAndRejectComplexAutomaticCount()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var source = query.Query<string>().Select("Name").From("samples").Where("Name", "two");
        var simpleCte = query.Query<string>().With("selected", source).Select("Name").From("selected");
        var groupCte = query.Query<string>().With("selected", source).Select("Name").From("selected")
            .GroupBy("Name");
        var unionCte = query.Query<string>().With("selected", source).Select("Name").From("selected")
            .Union(source);

        // Act
        var syncPage = simpleCte.ToPage(new Pager(1, 1) { Order = "Name" });
        var asyncPage = await simpleCte.ToPageAsync(new Pager(1, 1) { Order = "Name" });
        var groupException = Assert.Throws<NotSupportedException>(() => groupCte.ToPage(new Pager(1, 1)));
        var unionException = await Assert.ThrowsAsync<NotSupportedException>(() => unionCte.ToPageAsync(new Pager(1, 1)));

        // Assert
        Assert.Equal(1, syncPage.TotalCount);
        Assert.Equal(new[] { "two" }, syncPage.Data);
        Assert.Equal(1, asyncPage.TotalCount);
        Assert.Equal(new[] { "two" }, asyncPage.Data);
        Assert.Equal("包含 CTE 的 Union、Group 或 Distinct 查询暂不支持自动分页计数，请预先设置 TotalCount。", groupException.Message);
        Assert.Equal(groupException.Message, unionException.Message);
    }

    /// <summary>
    /// 测试目的：包含 CTE 的复杂去重查询无法在所有方言安全重写计数 SQL 时，应拒绝自动分页而非返回错误总数。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenCteDistinctPaged_ShouldRejectUnsafeAutomaticCount()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var source = query.Query<string>().Select("Name").From("samples");
        var description = query.Query<string>().With("selected", source).Distinct().Select("Name").From("selected");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => description.ToPage(new Pager(1, 1) { Order = "Name" }));

        // Assert
        Assert.Equal("包含 CTE 的 Union、Group 或 Distinct 查询暂不支持自动分页计数，请预先设置 TotalCount。", exception.Message);
    }

    /// <summary>
    /// 测试目的：调用方已明确总数为零时，复杂 CTE 分页不得尝试不安全的自动 Count，仍应执行当前页数据查询。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenComplexCtePageHasKnownZeroTotal_ShouldSkipAutomaticCount()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var source = query.Query<string>().Select("Name").From("samples").Where("Name", "two");
        var description = query.Query<string>().With("selected", source).Select("Name").From("selected")
            .GroupBy("Name");
        var pager = new Pager(1, 1) { Order = "Name", TotalCount = 0 };

        // Act
        var page = description.ToPage(pager);

        // Assert
        Assert.True(pager.IsTotalCountKnown);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(new[] { "two" }, page.Data);
    }

    /// <summary>
    /// 测试目的：独立 Fluent 与原生文本查询应通过标量终结方法返回首行首列，并遵循取消和空结果默认值语义。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenScalarExecuted_ShouldReturnFirstColumnAndRespectCancellation()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var fluentCount = query.Query<int>().CountAll().From("samples").Scalar();
        var textCount = await query.Sql<int>("Select Count(*) From samples Where Name <> @name", new { name = "two" })
            .ScalarAsync();
        var missingAmount = query.Sql<decimal?>("Select Amount From samples Where Name = @name", new { name = "missing" })
            .Scalar();
        cancellationTokenSource.Cancel();

        // Assert
        Assert.Equal(3, fluentCount);
        Assert.Equal(2, textCount);
        Assert.Null(missingAmount);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.Sql<int>("Select Count(*) From samples")
            .ScalarAsync(cancellationToken: cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：独立 Fluent 与原生文本查询应支持同步惰性读取，并在枚举提前终止后归还执行资源。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenSynchronouslyStreamed_ShouldKeepLeaseUntilEnumerationIsDisposed()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var fluentNames = query.Query<Sample>().Select("Id,Name,Amount").From("samples").OrderBy("Id")
            .AsEnumerable().Select(item => item.Name).ToList();
        using (var enumerator = query.Sql<Sample>(
                   "Select Id,Name,Amount From samples Where Name <> @name Order By Id", new { name = "two" })
               .AsEnumerable().GetEnumerator())
        {
            Assert.True(enumerator.MoveNext());
            Assert.Equal("one", enumerator.Current.Name);
            Assert.Throws<InvalidOperationException>(() => query.Sql<int>("Select Count(*) From samples").Scalar());
        }
        var count = query.Sql<int>("Select Count(*) From samples").Scalar();

        // Assert
        Assert.Equal(new[] { "one", "two", "three" }, fluentNames);
        Assert.Equal(3, count);
    }

    /// <summary>
    /// 测试目的：独立 Fluent 与原生文本查询应按默认 Id 分段规则完成双对象映射，并传递异步取消令牌。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenTwoTypeMapped_ShouldMaterializeFluentAndTextResults()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var fluent = query.Query<SamplePair>().Select("s.Id,s.Name,s.Amount,s.Id,s.Name").From("samples", "s")
            .OrderBy("s.Id").ToList<Sample, SampleName>((sample, name) => new SamplePair
            {
                SampleName = sample.Name,
                RelatedName = name.Name
            });
        var text = await query.Sql<SamplePair>(
                "Select s.Id,s.Name,s.Amount,s.Id,s.Name From samples s Where s.Name <> @name Order By s.Id",
                new { name = "two" })
            .ToListAsync<Sample, SampleName>((sample, name) => new SamplePair
            {
                SampleName = sample.Name,
                RelatedName = name.Name
            });
        cancellationTokenSource.Cancel();

        // Assert
        Assert.Equal(new[] { "one:one", "two:two", "three:three" },
            fluent.Select(item => $"{item.SampleName}:{item.RelatedName}"));
        Assert.Equal(new[] { "one:one", "three:three" }, text.Select(item => $"{item.SampleName}:{item.RelatedName}"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.Sql<SamplePair>(
                "Select Id,Name,Amount,Id,Name From samples")
            .ToListAsync<Sample, SampleName>((sample, name) => new SamplePair
            {
                SampleName = sample.Name,
                RelatedName = name.Name
            }, cancellationToken: cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：独立 Fluent 与原生文本查询应按默认 Id 分段规则完成三对象映射。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenThreeTypeMapped_ShouldMaterializeFluentAndTextResults()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var fluent = query.Query<SampleTriple>()
            .Select("s.Id,s.Name,s.Amount,s.Id,s.Name,s.Id,s.Name")
            .From("samples", "s")
            .OrderBy("s.Id")
            .ToList<Sample, SampleName, SampleName>((sample, second, third) => new SampleTriple
            {
                FirstName = sample.Name,
                SecondName = second.Name,
                ThirdName = third.Name
            });
        var text = await query.Sql<SampleTriple>(
                "Select s.Id,s.Name,s.Amount,s.Id,s.Name,s.Id,s.Name From samples s Where s.Name <> @name Order By s.Id",
                new { name = "two" })
            .ToListAsync<Sample, SampleName, SampleName>((sample, second, third) => new SampleTriple
            {
                FirstName = sample.Name,
                SecondName = second.Name,
                ThirdName = third.Name
            });

        // Assert
        Assert.Equal(new[] { "one:one:one", "two:two:two", "three:three:three" },
            fluent.Select(item => $"{item.FirstName}:{item.SecondName}:{item.ThirdName}"));
        Assert.Equal(new[] { "one:one:one", "three:three:three" },
            text.Select(item => $"{item.FirstName}:{item.SecondName}:{item.ThirdName}"));
    }

    /// <summary>
    /// 测试目的：独立 Fluent 与原生文本查询应按默认 Id 分段规则完成四对象映射。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenFourTypeMapped_ShouldMaterializeFluentAndTextResults()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var fluent = query.Query<SampleQuad>()
            .Select("s.Id,s.Name,s.Amount,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name")
            .From("samples", "s")
            .OrderBy("s.Id")
            .ToList<Sample, SampleName, SampleName, SampleName>((sample, second, third, fourth) => new SampleQuad
            {
                FirstName = sample.Name,
                SecondName = second.Name,
                ThirdName = third.Name,
                FourthName = fourth.Name
            });
        var text = await query.Sql<SampleQuad>(
                "Select s.Id,s.Name,s.Amount,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name From samples s Where s.Name <> @name Order By s.Id",
                new { name = "two" })
            .ToListAsync<Sample, SampleName, SampleName, SampleName>((sample, second, third, fourth) => new SampleQuad
            {
                FirstName = sample.Name,
                SecondName = second.Name,
                ThirdName = third.Name,
                FourthName = fourth.Name
            });

        // Assert
        Assert.Equal(new[] { "one:one:one:one", "two:two:two:two", "three:three:three:three" },
            fluent.Select(item => $"{item.FirstName}:{item.SecondName}:{item.ThirdName}:{item.FourthName}"));
        Assert.Equal(new[] { "one:one:one:one", "three:three:three:three" },
            text.Select(item => $"{item.FirstName}:{item.SecondName}:{item.ThirdName}:{item.FourthName}"));
    }

    /// <summary>
    /// 测试目的：独立 Fluent 与原生文本查询应按默认 Id 分段规则完成五对象映射。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenFiveTypeMapped_ShouldMaterializeFluentAndTextResults()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var fluent = query.Query<SampleQuint>()
            .Select("s.Id,s.Name,s.Amount,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name")
            .From("samples", "s")
            .OrderBy("s.Id")
            .ToList<Sample, SampleName, SampleName, SampleName, SampleName>((sample, second, third, fourth, fifth) =>
                new SampleQuint
                {
                    FirstName = sample.Name,
                    SecondName = second.Name,
                    ThirdName = third.Name,
                    FourthName = fourth.Name,
                    FifthName = fifth.Name
                });
        var text = await query.Sql<SampleQuint>(
                "Select s.Id,s.Name,s.Amount,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name From samples s Where s.Name <> @name Order By s.Id",
                new { name = "two" })
            .ToListAsync<Sample, SampleName, SampleName, SampleName, SampleName>((sample, second, third, fourth, fifth) =>
                new SampleQuint
                {
                    FirstName = sample.Name,
                    SecondName = second.Name,
                    ThirdName = third.Name,
                    FourthName = fourth.Name,
                    FifthName = fifth.Name
                });

        // Assert
        Assert.Equal(new[] { "one:one:one:one:one", "two:two:two:two:two", "three:three:three:three:three" },
            fluent.Select(item =>
                $"{item.FirstName}:{item.SecondName}:{item.ThirdName}:{item.FourthName}:{item.FifthName}"));
        Assert.Equal(new[] { "one:one:one:one:one", "three:three:three:three:three" },
            text.Select(item =>
                $"{item.FirstName}:{item.SecondName}:{item.ThirdName}:{item.FourthName}:{item.FifthName}"));
    }

    /// <summary>
    /// 测试目的：独立 Fluent 与原生文本查询应按默认 Id 分段规则完成六对象映射。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenSixTypeMapped_ShouldMaterializeFluentAndTextResults()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var fluent = query.Query<SampleSext>()
            .Select("s.Id,s.Name,s.Amount,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name")
            .From("samples", "s")
            .OrderBy("s.Id")
            .ToList<Sample, SampleName, SampleName, SampleName, SampleName, SampleName>(
                (sample, second, third, fourth, fifth, sixth) => new SampleSext
                {
                    FirstName = sample.Name,
                    SecondName = second.Name,
                    ThirdName = third.Name,
                    FourthName = fourth.Name,
                    FifthName = fifth.Name,
                    SixthName = sixth.Name
                });
        var text = await query.Sql<SampleSext>(
                "Select s.Id,s.Name,s.Amount,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name From samples s Where s.Name <> @name Order By s.Id",
                new { name = "two" })
            .ToListAsync<Sample, SampleName, SampleName, SampleName, SampleName, SampleName>(
                (sample, second, third, fourth, fifth, sixth) => new SampleSext
                {
                    FirstName = sample.Name,
                    SecondName = second.Name,
                    ThirdName = third.Name,
                    FourthName = fourth.Name,
                    FifthName = fifth.Name,
                    SixthName = sixth.Name
                });

        // Assert
        Assert.Equal(new[] { "one:one:one:one:one:one", "two:two:two:two:two:two", "three:three:three:three:three:three" },
            fluent.Select(item =>
                $"{item.FirstName}:{item.SecondName}:{item.ThirdName}:{item.FourthName}:{item.FifthName}:{item.SixthName}"));
        Assert.Equal(new[] { "one:one:one:one:one:one", "three:three:three:three:three:three" },
            text.Select(item =>
                $"{item.FirstName}:{item.SecondName}:{item.ThirdName}:{item.FourthName}:{item.FifthName}:{item.SixthName}"));
    }

    /// <summary>
    /// 测试目的：独立 Fluent 与原生文本查询应按默认 Id 分段规则完成七对象映射。
    /// </summary>
    [Fact]
    public async Task SqlQueryPlan_WhenSevenTypeMapped_ShouldMaterializeFluentAndTextResults()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var fluent = query.Query<SampleSept>()
            .Select("s.Id,s.Name,s.Amount,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name")
            .From("samples", "s")
            .OrderBy("s.Id")
            .ToList<Sample, SampleName, SampleName, SampleName, SampleName, SampleName, SampleName>(
                (sample, second, third, fourth, fifth, sixth, seventh) => new SampleSept
                {
                    FirstName = sample.Name,
                    SecondName = second.Name,
                    ThirdName = third.Name,
                    FourthName = fourth.Name,
                    FifthName = fifth.Name,
                    SixthName = sixth.Name,
                    SeventhName = seventh.Name
                });
        var text = await query.Sql<SampleSept>(
                "Select s.Id,s.Name,s.Amount,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name,s.Id,s.Name From samples s Where s.Name <> @name Order By s.Id",
                new { name = "two" })
            .ToListAsync<Sample, SampleName, SampleName, SampleName, SampleName, SampleName, SampleName>(
                (sample, second, third, fourth, fifth, sixth, seventh) => new SampleSept
                {
                    FirstName = sample.Name,
                    SecondName = second.Name,
                    ThirdName = third.Name,
                    FourthName = fourth.Name,
                    FifthName = fifth.Name,
                    SixthName = sixth.Name,
                    SeventhName = seventh.Name
                });

        // Assert
        Assert.Equal(new[] { "one:one:one:one:one:one:one", "two:two:two:two:two:two:two", "three:three:three:three:three:three:three" },
            fluent.Select(item =>
                $"{item.FirstName}:{item.SecondName}:{item.ThirdName}:{item.FourthName}:{item.FifthName}:{item.SixthName}:{item.SeventhName}"));
        Assert.Equal(new[] { "one:one:one:one:one:one:one", "three:three:three:three:three:three:three" },
            text.Select(item =>
                $"{item.FirstName}:{item.SecondName}:{item.ThirdName}:{item.FourthName}:{item.FifthName}:{item.SixthName}:{item.SeventhName}"));
    }

    /// <summary>
    /// 测试目的：原生 SQL 文本查询应保留参数绑定、异步流释放和取消语义。
    /// </summary>
    [Fact]
    public async Task SqlTextQuery_WhenStreamedOrCancelled_ShouldBindParametersAndReleaseExecutionResources()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var names = new List<string>();

        // Act
        await foreach (var sample in query.Sql<Sample>("Select Id,Name,Amount From samples Where Name <> @name Order By Id",
                           new { name = "two" }).AsAsyncEnumerable())
            names.Add(sample.Name);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Assert
        Assert.Equal(new[] { "one", "three" }, names);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.Sql<Sample>(
            "Select Id,Name,Amount From samples").ToListAsync(cancellationToken: cancellationTokenSource.Token));
        Assert.Equal("one", (await query.Sql<Sample>("Select Id,Name,Amount From samples Order By Id")
            .FirstAsync()).Name);
    }

    /// <summary>
    /// 测试目的：事务作用域创建的根查询执行独立原生计划时，应复用未提交事务中的连接和数据。
    /// </summary>
    [Fact]
    public async Task SqlTextQuery_WhenCreatedInsideTransactionScope_ShouldReadUncommittedScopeData()
    {
        // Arrange
        using var scope = _fixture.GetTransactionScopeFactory().Begin("first");
        using var executor = scope.CreateExecutor();
        using var query = scope.CreateQuery();
        await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "planned" });

        // Act
        var count = await query.Sql<int>("Select Count(*) From samples Where Name = @name", new { name = "planned" })
            .SingleAsync();
        scope.Commit();

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(new[] { "planned" }, await _fixture.ReadNamesAsync());
    }

    /// <summary>
    /// 测试目的：插值 SQL 查询应将插值值作为参数传递，而不是将值拼接到 SQL 文本中。
    /// </summary>
    [Fact]
    public async Task SqlInterpolated_WhenValueContainsQuote_ShouldUseBoundParameter()
    {
        // Arrange
        await InsertAsync("O'Reilly");
        using var query = _fixture.CreateQuery();

        // Act
        var result = await query.SqlInterpolated<Sample>($"Select Id,Name,Amount From samples Where Name = {"O'Reilly"}")
            .SingleAsync();

        // Assert
        Assert.Equal("O'Reilly", result.Name);
    }

    /// <summary>
    /// 测试目的：插值 SQL 应正确还原转义花括号，并忽略仅用于 CLR 格式化的对齐和格式说明，不得影响参数化执行。
    /// </summary>
    [Fact]
    public async Task SqlInterpolated_WhenFormatContainsEscapedBracesAndAlignment_ShouldKeepSqlLiteralAndParameter()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var description = query.SqlInterpolated<Sample>(
            $"Select Id,Name,Amount From samples Where Name = {"one",-10:ignored} And '{{' = '{{'");
        var result = await description.SingleAsync();

        // Assert
        Assert.Contains("Name = @p0", description.CommandText, StringComparison.Ordinal);
        Assert.Contains("'{' = '{'", description.CommandText, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", description.CommandText, StringComparison.Ordinal);
        Assert.Equal("one", result.Name);
    }

    /// <summary>
    /// 测试目的：仅出现在 SQL 字符串中的参数样式不应与插值参数冲突，且字符串文本必须保持不变。
    /// </summary>
    [Fact]
    public async Task SqlInterpolated_WhenTokenAppearsOnlyInStringLiteral_ShouldKeepDefaultParameterName()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var description = query.SqlInterpolated<Sample>(
            $"Select Id,Name,Amount From samples Where Name = {"one"} And '@p0' = '@p0'");
        var result = await description.SingleAsync();

        // Assert
        Assert.Contains("Name = @p0", description.CommandText, StringComparison.Ordinal);
        Assert.Contains("'@p0' = '@p0'", description.CommandText, StringComparison.Ordinal);
        var parameters = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(description.Parameters);
        Assert.Equal("one", Assert.Single(parameters).Value);
        Assert.Equal("one", result.Name);
    }

    /// <summary>
    /// 测试目的：独立 Fluent 和原生文本查询应将自定义分段列传递给 Dapper 多映射。
    /// </summary>
    [Fact]
    public async Task QueryDescriptions_WhenCustomSplitOnConfigured_ShouldMapAtSpecifiedColumn()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var fluent = query.Query<string>()
            .Select("Id,Name,Name")
            .From("samples")
            .OrderBy("Id")
            .SplitOn("Name")
            .ToList<Sample, SampleName>((sample, name) => name.Name);
        var text = await query.Sql<string>("Select Id,Name,Name From samples Order By Id")
            .SplitOn("Name")
            .ToListAsync<Sample, SampleName>((sample, name) => name.Name);

        // Assert
        Assert.Equal(new[] { "one", "two", "three" }, fluent);
        Assert.Equal(new[] { "one", "two", "three" }, text);
    }

    /// <summary>
    /// 测试目的：Lambda 查询应依据实体映射生成投影、来源表和参数化筛选条件。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenEntityPredicateAndPageConfigured_ShouldUseMappedQuery()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var result = await query.From<SqliteStructuredTableSample>()
            .Where(sample => sample.Name != "two")
            .OrderBy(sample => sample.Name)
            .Skip(1)
            .Take(1)
            .SingleAsync();

        // Assert
        Assert.Equal("three", result.Name);
    }

    /// <summary>
    /// 测试目的：Lambda 查询应可将属性投影和聚合配置为指定结果类型，并复用实体映射与参数化执行链。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenProjectionAndAggregateResultTypesConfigured_ShouldExecuteMappedResults()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var names = await query.From<SqliteStructuredTableSample>()
            .OrderBy(sample => sample.Name)
            .Select(sample => sample.Name)
            .ToListAsync<string>();
        var count = await query.From<SqliteStructuredTableSample>()
            .Aggregate(SqlAggregateFunction.Count, sample => sample.Name)
            .ScalarAsync<int>();

        // Assert
        Assert.Equal(new[] { "one", "three", "two" }, names);
        Assert.Equal(3, count);
    }

    /// <summary>
    /// 测试目的：Lambda 自定义 Select 必须替换创建时的默认实体投影，避免重复列并支持直接映射标量结果。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenCustomSelectConfigured_ShouldReplaceDefaultProjection()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var description = query.From<SqliteStructuredTableSample>()
            .Select(sample => sample.Name);
        var result = await description.ToListAsync<string>();

        // Assert
        Assert.Equal("Select `samples`.`Name` \r\nFrom `samples`", description.ToSql());
        Assert.Equal(new[] { "one", "three", "two" }, result.OrderBy(item => item).ToArray());
    }

    /// <summary>
    /// 测试目的：Lambda DTO 成员初始化投影应按目标成员别名执行，并将数据库空值映射到可空属性。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenMemberInitProjectionConfigured_ShouldMapAliasedAndNullableValues()
    {
        // Arrange
        await InsertAggregateSampleAsync("member-init", null);
        using var query = _fixture.CreateQuery();

        // Act
        var description = query.From<SqliteStructuredTableSample>()
            .Where(sample => sample.Name == "member-init")
            .Select(sample => new LambdaMemberInitResult
            {
                DisplayName = sample.Name,
                OptionalAmount = sample.Amount
            });
        var result = await description.SingleAsync<LambdaMemberInitResult>();

        // Assert
        Assert.Equal("Select `samples`.`Name` As `DisplayName`,`samples`.`Amount` As `OptionalAmount` \r\nFrom `samples` \r\nWhere `samples`.`Name`=@_p_0", description.ToSql());
        Assert.Equal("member-init", result.DisplayName);
        Assert.Null(result.OptionalAmount);
    }

    /// <summary>
    /// 测试目的：Lambda 连续 Select 应始终替换前一投影，只有 AppendSelect 才允许显式追加投影列。
    /// </summary>
    [Fact]
    public void Lambda_WhenSelectRepeated_ShouldReplaceAndAppendSelectShouldAppend()
    {
        // Arrange
        using var query = _fixture.CreateQuery();

        // Act
        var replacement = query.From<SqliteStructuredTableSample>()
            .Select(sample => new object[] { sample.Name })
            .Select(sample => new object[] { sample.Name });
        var appended = query.From<SqliteStructuredTableSample>()
            .Select(sample => new object[] { sample.Name })
            .AppendSelect(sample => new object[] { sample.Id });

        // Assert
        Assert.Equal("Select `samples`.`Name` \r\nFrom `samples`", replacement.ToSql());
        Assert.Equal("Select `samples`.`Name`,`samples`.`Id` \r\nFrom `samples`", appended.ToSql());
    }

    /// <summary>
    /// 测试目的：Lambda 查询应在不暴露 Builder 的情况下支持类型化投影、条件和排序，并转换为标量结果描述执行。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenTypedDescriptionCompositionConfigured_ShouldExecuteProjectedResult()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var description = query.From<SqliteStructuredTableSample>("sample")
            .Where(sample => sample.Name == "two")
            .OrderBy(sample => sample.Name)
            .Select(sample => sample.Name);
        var names = await description.ToListAsync<string>();

        // Assert
        Assert.Equal(new[] { "two" }, names);
    }

    /// <summary>
    /// 测试目的：1～10 个类型化来源都应在 SQLite 中真实生成、执行并物化第一来源的强类型投影。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenOneThroughTenTypedSourcesProvided_ShouldExecuteAndMaterializeFirstSource()
    {
        // Arrange
        using (var executor = _fixture.CreateExecutor())
        {
            for (var index = 1; index <= 10; index++)
                await executor.ExecuteSqlAsync($"Insert Into Arity{index:00}(Id,Name) Values (@id,@name)",
                    new { id = 1, name = $"arity-{index:00}" });
        }

        // Act
        using var query1 = _fixture.CreateQuery();
        var result1 = await query1.From<SqliteArity01>()
            .Select(item => new SqliteArityResult { Id = item.Id })
            .ToListAsync<SqliteArityResult>();

        using var query2 = _fixture.CreateQuery();
        var result2 = await query2.From<SqliteArity01, SqliteArity02>()
            .Where((first, second) => first.Id == second.Id)
            .Select((first, second) => new SqliteArityResult { Id = first.Id })
            .ToListAsync<SqliteArityResult>();

        using var query3 = _fixture.CreateQuery();
        var result3 = await query3.From<SqliteArity01, SqliteArity02, SqliteArity03>()
            .Where((first, second, third) => first.Id == second.Id && second.Id == third.Id)
            .Select((first, second, third) => new SqliteArityResult { Id = first.Id })
            .ToListAsync<SqliteArityResult>();

        using var query4 = _fixture.CreateQuery();
        var result4 = await query4.From<SqliteArity01, SqliteArity02, SqliteArity03, SqliteArity04>()
            .Where((first, second, third, fourth) => first.Id == second.Id && third.Id == fourth.Id)
            .Select((first, second, third, fourth) => new SqliteArityResult { Id = first.Id })
            .ToListAsync<SqliteArityResult>();

        using var query5 = _fixture.CreateQuery();
        var result5 = await query5.From<SqliteArity01, SqliteArity02, SqliteArity03, SqliteArity04, SqliteArity05>()
            .Where((first, second, third, fourth, fifth) => first.Id == second.Id && fourth.Id == fifth.Id)
            .Select((first, second, third, fourth, fifth) => new SqliteArityResult { Id = first.Id })
            .ToListAsync<SqliteArityResult>();

        using var query6 = _fixture.CreateQuery();
        var result6 = await query6.From<SqliteArity01, SqliteArity02, SqliteArity03, SqliteArity04, SqliteArity05, SqliteArity06>()
            .Where((first, second, third, fourth, fifth, sixth) => first.Id == second.Id && fifth.Id == sixth.Id)
            .Select((first, second, third, fourth, fifth, sixth) => new SqliteArityResult { Id = first.Id })
            .ToListAsync<SqliteArityResult>();

        using var query7 = _fixture.CreateQuery();
        var result7 = await query7.From<SqliteArity01, SqliteArity02, SqliteArity03, SqliteArity04, SqliteArity05, SqliteArity06, SqliteArity07>()
            .Where((first, second, third, fourth, fifth, sixth, seventh) => first.Id == second.Id && sixth.Id == seventh.Id)
            .Select((first, second, third, fourth, fifth, sixth, seventh) => new SqliteArityResult { Id = first.Id })
            .ToListAsync<SqliteArityResult>();

        using var query8 = _fixture.CreateQuery();
        var result8 = await query8.From<SqliteArity01, SqliteArity02, SqliteArity03, SqliteArity04, SqliteArity05, SqliteArity06, SqliteArity07, SqliteArity08>()
            .Where((first, second, third, fourth, fifth, sixth, seventh, eighth) => first.Id == second.Id && seventh.Id == eighth.Id)
            .Select((first, second, third, fourth, fifth, sixth, seventh, eighth) => new SqliteArityResult { Id = first.Id })
            .ToListAsync<SqliteArityResult>();

        using var query9 = _fixture.CreateQuery();
        var result9 = await query9.From<SqliteArity01, SqliteArity02, SqliteArity03, SqliteArity04, SqliteArity05, SqliteArity06, SqliteArity07, SqliteArity08, SqliteArity09>()
            .Where((first, second, third, fourth, fifth, sixth, seventh, eighth, ninth) => first.Id == second.Id && eighth.Id == ninth.Id)
            .Select((first, second, third, fourth, fifth, sixth, seventh, eighth, ninth) => new SqliteArityResult { Id = first.Id })
            .ToListAsync<SqliteArityResult>();

        using var query10 = _fixture.CreateQuery();
        var description10 = query10.From<SqliteArity01, SqliteArity02, SqliteArity03, SqliteArity04, SqliteArity05, SqliteArity06, SqliteArity07, SqliteArity08, SqliteArity09, SqliteArity10>()
            .Where((first, second, third, fourth, fifth, sixth, seventh, eighth, ninth, tenth) => first.Id == second.Id && ninth.Id == tenth.Id)
            .Select((first, second, third, fourth, fifth, sixth, seventh, eighth, ninth, tenth) => new SqliteArityResult { Id = first.Id });
        var result10 = await description10.ToListAsync<SqliteArityResult>();

        // Assert
        Assert.All(new[] { result1, result2, result3, result4, result5, result6, result7, result8, result9, result10 },
            result => Assert.Equal(new[] { 1 }, result.Select(item => item.Id)));
        Assert.Equal("Select `Arity01`.`Id` As `Id` \r\nFrom `Arity01`, `Arity02`, `Arity03`, `Arity04`, `Arity05`, `Arity06`, `Arity07`, `Arity08`, `Arity09`, `Arity10` \r\nWhere `Arity01`.`Id`=`Arity02`.`Id` And `Arity09`.`Id`=`Arity10`.`Id`", description10.ToSql());
    }

    /// <summary>
    /// 测试目的：Lambda 查询应通过原子类型化 Join 与显式追加投影执行多表 DTO 映射，无需访问 Builder。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenJoinedWithMappedEntity_ShouldExecuteDtoProjection()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var samples = await query.Query<Sample>().Select("Id,Name,Amount").From("samples").OrderBy("Id").ToListAsync();
        using (var executor = _fixture.CreateExecutor())
        {
            await executor.ExecuteSqlAsync("Insert Into Orders(Id,TenantId,Name) Values (@id,@tenantId,@name)",
                new { id = samples[0].Id, tenantId = "tenant-1", name = "order-one" });
            await executor.ExecuteSqlAsync("Insert Into Orders(Id,TenantId,Name) Values (@id,@tenantId,@name)",
                new { id = samples[1].Id, tenantId = "tenant-2", name = "order-two" });
        }

        // Act
        var description = query.From<SqliteStructuredTableSample>("s")
            .Join<SqliteStructuredOrderSample>((sample, order) => sample.Id == order.Id, "o")
            .OrderBy((sample, order) => new object[] { sample.Id })
            .Select((sample, order) => new LambdaJoinResult
            {
                Name = sample.Name,
                TenantId = order.TenantId
            });
        var result = await description.ToListAsync<LambdaJoinResult>();

        // Assert
        Assert.Equal(new[] { "one:tenant-1", "two:tenant-2" },
            result.Select(item => $"{item.Name}:{item.TenantId}"));
        Assert.Equal("Select `s`.`Name` As `Name`,`o`.`TenantId` As `TenantId` \r\nFrom `samples` As `s` \r\nJoin `Orders` As `o` On `s`.`Id`=`o`.`Id` \r\nOrder By `s`.`Id`", description.ToSql());
    }

    /// <summary>
    /// 测试目的：多表 DTO 派生表应在 SQLite 中保留筛选参数和成员别名，并作为外层类型化 Join 来源完成物化。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenDtoSubqueryJoined_ShouldExecuteAndMaterializeProjectedMembers()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var samples = await query.Query<Sample>().Select("Id,Name,Amount").From("samples").OrderBy("Id").ToListAsync();
        using (var executor = _fixture.CreateExecutor())
        {
            await executor.ExecuteSqlAsync("Insert Into Orders(Id,TenantId,Name) Values (@id,@tenantId,@name)",
                new { id = samples[0].Id, tenantId = "tenant-1", name = "order-one" });
            await executor.ExecuteSqlAsync("Insert Into Orders(Id,TenantId,Name) Values (@id,@tenantId,@name)",
                new { id = samples[1].Id, tenantId = "tenant-2", name = "order-two" });
        }
        SqlSubquery<LambdaSubqueryResult> summary = query.From<SqliteStructuredTableSample, SqliteStructuredOrderSample>()
            .Where((sample, order) => sample.Id == order.Id && order.TenantId == "tenant-1")
            .SelectSubquery((sample, order) => new LambdaSubqueryResult
            {
                SampleId = sample.Id,
                TenantId = order.TenantId
            }, "summary");

        // Act
        var description = query.From<SqliteStructuredTableSample, SqliteStructuredOrderSample>()
            .Join(summary, (sample, order, derived) => sample.Id == derived.SampleId && order.Id == derived.SampleId)
            .Select((sample, order, derived) => new LambdaJoinResult
            {
                Name = sample.Name,
                TenantId = derived.TenantId
            });
        var result = await description.ToListAsync<LambdaJoinResult>();

        // Assert
        Assert.Equal(new[] { "one:tenant-1" }, result.Select(item => $"{item.Name}:{item.TenantId}"));
        Assert.Equal("Select `samples`.`Name` As `Name`,`summary`.`TenantId` As `TenantId` \r\nFrom `samples`, `Orders` \r\nJoin (Select `samples`.`Id` As `SampleId`,`Orders`.`TenantId` As `TenantId` \r\nFrom `samples`, `Orders` \r\nWhere `samples`.`Id`=`Orders`.`Id` And `Orders`.`TenantId`=@_p_0) As `summary` On `samples`.`Id`=`summary`.`SampleId` And `Orders`.`Id`=`summary`.`SampleId`", description.ToSql());
    }

    /// <summary>
    /// 测试目的：单表 DTO 派生表应在 SQLite 中通过双表 Lambda Join 完成参数绑定和 DTO 物化。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenSingleSourceDtoSubqueryJoined_ShouldExecuteAndMaterializeProjectedMembers()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var samples = await query.Query<Sample>().Select("Id,Name,Amount").From("samples").OrderBy("Id").ToListAsync();
        using (var executor = _fixture.CreateExecutor())
        {
            await executor.ExecuteSqlAsync("Insert Into Orders(Id,TenantId,Name) Values (@id,@tenantId,@name)",
                new { id = samples[0].Id, tenantId = "tenant-1", name = "order-one" });
            await executor.ExecuteSqlAsync("Insert Into Orders(Id,TenantId,Name) Values (@id,@tenantId,@name)",
                new { id = samples[1].Id, tenantId = "tenant-2", name = "order-two" });
        }
        SqlSubquery<LambdaSubqueryResult> summary = query.From<SqliteStructuredTableSample>()
            .Where(sample => sample.Name == "one")
            .SelectSubquery(sample => new LambdaSubqueryResult { SampleId = sample.Id }, "summary");

        // Act
        var description = query.From<SqliteStructuredOrderSample>()
            .Join(summary, (order, derived) => order.Id == derived.SampleId)
            .Select((order, derived) => new LambdaJoinResult
            {
                Name = order.Name,
                TenantId = order.TenantId
            });
        var result = await description.ToListAsync<LambdaJoinResult>();

        // Assert
        Assert.Equal(new[] { "order-one:tenant-1" }, result.Select(item => $"{item.Name}:{item.TenantId}"));
        Assert.Equal("Select `Orders`.`Name` As `Name`,`Orders`.`TenantId` As `TenantId` \r\nFrom `Orders` \r\nJoin (Select `samples`.`Id` As `SampleId` \r\nFrom `samples` \r\nWhere `samples`.`Name`=@_p_0) As `summary` On `Orders`.`Id`=`summary`.`SampleId`", description.ToSql());
    }

    /// <summary>
    /// 测试目的：单表实体查询应能通过 Cross Join 组合类型化派生表，并在 SQLite 中物化关联的 DTO 结果。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenSingleSourceDtoSubqueryCrossJoined_ShouldExecuteAndMaterializeProjectedMembers()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var sample = await query.Query<Sample>().Select("Id,Name,Amount").From("samples")
            .Where("Name", "one").SingleAsync();
        using (var executor = _fixture.CreateExecutor())
            await executor.ExecuteSqlAsync("Insert Into Orders(Id,TenantId,Name) Values (@id,@tenantId,@name)",
                new { id = sample.Id, tenantId = "tenant-1", name = "order-one" });
        SqlSubquery<LambdaSubqueryResult> summary = query.From<SqliteStructuredTableSample>()
            .Where(item => item.Name == "one")
            .SelectSubquery(item => new LambdaSubqueryResult { SampleId = item.Id }, "summary");

        // Act
        var description = query.From<SqliteStructuredOrderSample>()
            .CrossJoin(summary)
            .Where((order, item) => order.Id == item.SampleId)
            .Select((order, item) => new LambdaJoinResult
            {
                Name = order.Name,
                TenantId = order.TenantId
            });
        var result = await description.ToListAsync<LambdaJoinResult>();

        // Assert
        Assert.Equal(new[] { "order-one:tenant-1" }, result.Select(item => $"{item.Name}:{item.TenantId}"));
        Assert.Equal("Select `Orders`.`Name` As `Name`,`Orders`.`TenantId` As `TenantId` \r\nFrom `Orders` \r\nCross Join (Select `samples`.`Id` As `SampleId` \r\nFrom `samples` \r\nWhere `samples`.`Name`=@_p_0) As `summary` \r\nWhere `Orders`.`Id`=`summary`.`SampleId`", description.ToSql());
    }

    /// <summary>
    /// 测试目的：单表实体查询应能通过 Cross Join 进入双表 Lambda 链，并在 SQLite 中执行和物化 DTO 投影。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenSingleSourceTypedEntityCrossJoined_ShouldExecuteAndMaterializeProjectedMembers()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var sample = await query.Query<Sample>().Select("Id,Name,Amount").From("samples")
            .Where("Name", "one").SingleAsync();
        using (var executor = _fixture.CreateExecutor())
            await executor.ExecuteSqlAsync("Insert Into Orders(Id,TenantId,Name) Values (@id,@tenantId,@name)",
                new { id = sample.Id, tenantId = "tenant-1", name = "order-one" });

        // Act
        var description = query.From<SqliteStructuredTableSample>()
            .CrossJoin<SqliteStructuredOrderSample>()
            .Where((item, order) => item.Id == order.Id)
            .Select((item, order) => new LambdaJoinResult
            {
                Name = item.Name,
                TenantId = order.TenantId
            });
        var result = await description.ToListAsync<LambdaJoinResult>();

        // Assert
        Assert.Equal(new[] { "one:tenant-1" }, result.Select(item => $"{item.Name}:{item.TenantId}"));
        Assert.Equal("Select `samples`.`Name` As `Name`,`Orders`.`TenantId` As `TenantId` \r\nFrom `samples` \r\nCross Join `Orders` \r\nWhere `samples`.`Id`=`Orders`.`Id`", description.ToSql());
    }

    /// <summary>
    /// 测试目的：DTO 派生表作为根来源后，应在 SQLite 中保留成员白名单、参数和投影物化行为。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenDtoSubqueryUsedAsRoot_ShouldExecuteAndMaterializeProjectedMembers()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var expected = await query.Query<Sample>().Select("Id,Name,Amount").From("samples")
            .Where("Name", "two").SingleAsync();
        var summary = query.From<SqliteStructuredTableSample>()
            .Where(sample => sample.Name == "two")
            .SelectSubquery(sample => new LambdaSubqueryResult { SampleId = sample.Id }, "summary");

        // Act
        var description = query.From<LambdaSubqueryResult>(summary)
            .Where(item => item.SampleId > 0)
            .Select(item => new LambdaSubqueryResult { SampleId = item.SampleId });
        var result = await description.ToListAsync<LambdaSubqueryResult>();

        // Assert
        Assert.Equal(new[] { expected.Id }, result.Select(item => item.SampleId).ToArray());
        Assert.Equal("Select `summary`.`SampleId` As `SampleId` \r\nFrom (Select `samples`.`Id` As `SampleId` \r\nFrom `samples` \r\nWhere `samples`.`Name`=@_p_0) As `summary` \r\nWhere `summary`.`SampleId`>@_p_1", description.ToSql());
    }

    /// <summary>
    /// 测试目的：类型化派生根再次冻结后，应能作为新的根来源在 SQLite 中执行并物化最终 DTO 投影。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenDtoSubqueryRootIsReprojected_ShouldExecuteAndMaterializeProjectedMembers()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var expected = await query.Query<Sample>().Select("Id,Name,Amount").From("samples")
            .Where("Name", "two").SingleAsync();
        var summary = query.From<SqliteStructuredTableSample>()
            .Where(sample => sample.Name == "two")
            .SelectSubquery(sample => new LambdaSubqueryResult { SampleId = sample.Id }, "summary");
        var refined = query.From<LambdaSubqueryResult>(summary)
            .Where(item => item.SampleId > 0)
            .SelectSubquery(item => new LambdaSubqueryResult { SampleId = item.SampleId }, "refined");

        // Act
        var description = query.From<LambdaSubqueryResult>(refined)
            .Where(item => item.SampleId > 0)
            .Select(item => new LambdaSubqueryResult { SampleId = item.SampleId });
        var result = await description.ToListAsync<LambdaSubqueryResult>();

        // Assert
        Assert.Equal(new[] { expected.Id }, result.Select(item => item.SampleId).ToArray());
        Assert.Equal("Select `refined`.`SampleId` As `SampleId` \r\nFrom (Select `summary`.`SampleId` As `SampleId` \r\nFrom (Select `samples`.`Id` As `SampleId` \r\nFrom `samples` \r\nWhere `samples`.`Name`=@_p_0) As `summary` \r\nWhere `summary`.`SampleId`>@_p_1) As `refined` \r\nWhere `refined`.`SampleId`>@_p_2", description.ToSql());
    }

    /// <summary>
    /// 测试目的：派生 DTO 根经 Left Join 后再次冻结时，应在 SQLite 中保留未匹配右表行并物化嵌套投影。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenDtoSubqueryRootIsLeftJoinedAndReprojected_ShouldMaterializeMatchedAndUnmatchedRows()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var first = await query.Query<Sample>().Select("Id,Name,Amount").From("samples").OrderBy("Id").FirstAsync();
        using (var executor = _fixture.CreateExecutor())
            await executor.ExecuteSqlAsync("Insert Into Orders(Id,TenantId,Name) Values (@id,@tenantId,@name)",
                new { id = first.Id, tenantId = "tenant-1", name = "order-one" });
        var owner = query.From<SqliteStructuredTableSample>()
            .Where(sample => sample.Id > 0)
            .SelectSubquery(sample => new LambdaSubqueryResult { SampleId = sample.Id }, "owner");
        var refined = query.From(owner)
            .LeftJoin<SqliteStructuredOrderSample>((summary, order) => summary.SampleId == order.Id, "order")
            .SelectSubquery((summary, order) => new LambdaSubqueryResult
            {
                SampleId = summary.SampleId,
                TenantId = order.TenantId
            }, "refined");

        // Act
        var description = query.From(refined)
            .Where(item => item.SampleId > 0)
            .Select(item => new LambdaSubqueryResult
            {
                SampleId = item.SampleId,
                TenantId = item.TenantId
            });
        var result = await description.ToListAsync<LambdaSubqueryResult>();

        // Assert
        Assert.Equal(new[] { $"{first.Id}:tenant-1", "2:", "3:" },
            result.OrderBy(item => item.SampleId).Select(item => $"{item.SampleId}:{item.TenantId}"));
        Assert.Equal("Select `refined`.`SampleId` As `SampleId`,`refined`.`TenantId` As `TenantId` \r\nFrom (Select `owner`.`SampleId` As `SampleId`,`order`.`TenantId` As `TenantId` \r\nFrom (Select `samples`.`Id` As `SampleId` \r\nFrom `samples` \r\nWhere `samples`.`Id`>@_p_0) As `owner` \r\nLeft Join `Orders` As `order` On `owner`.`SampleId`=`order`.`Id`) As `refined` \r\nWhere `refined`.`SampleId`>@_p_1", description.ToSql());
    }

    /// <summary>
    /// 测试目的：DTO 派生表作为根来源后，应能通过 Cross Join 绑定实体成员并在 SQLite 中执行物化。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenDtoSubqueryRootCrossJoined_ShouldExecuteAndMaterializeProjectedMembers()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var expected = await query.Query<Sample>().Select("Id,Name,Amount").From("samples")
            .Where("Name", "one").SingleAsync();
        using (var executor = _fixture.CreateExecutor())
            await executor.ExecuteSqlAsync("Insert Into Orders(Id,TenantId,Name) Values (@id,@tenantId,@name)",
                new { id = expected.Id, tenantId = "tenant-1", name = "order-one" });
        SqlSubquery<LambdaSubqueryResult> summary = query.From<SqliteStructuredTableSample>()
            .Where(sample => sample.Name == "one")
            .SelectSubquery(sample => new LambdaSubqueryResult { SampleId = sample.Id }, "summary");

        // Act
        var description = query.From<LambdaSubqueryResult>(summary)
            .CrossJoin<SqliteStructuredOrderSample>()
            .Where((item, order) => item.SampleId == order.Id)
            .Select((item, order) => new LambdaJoinResult
            {
                Name = order.Name,
                TenantId = order.TenantId
            });
        var result = await description.ToListAsync<LambdaJoinResult>();

        // Assert
        Assert.Equal(new[] { "order-one:tenant-1" }, result.Select(item => $"{item.Name}:{item.TenantId}"));
        Assert.Equal("Select `Orders`.`Name` As `Name`,`Orders`.`TenantId` As `TenantId` \r\nFrom (Select `samples`.`Id` As `SampleId` \r\nFrom `samples` \r\nWhere `samples`.`Name`=@_p_0) As `summary` \r\nCross Join `Orders` \r\nWhere `summary`.`SampleId`=`Orders`.`Id`", description.ToSql());
    }

    /// <summary>
    /// 测试目的：同一实体类型自连接应在 On 条件中使用不同别名，并能执行返回来源表投影。
    /// </summary>
    [Fact]
    public async Task SqlQuery_WhenSelfJoinedWithTypedOn_ShouldUseDistinctAliasesAndExecute()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var description = query.From<SqliteStructuredTableSample>("s")
            .Join<SqliteStructuredTableSample>((left, right) => left.Id == right.Id, "p")
            .OrderBy((left, right) => new object[] { left.Id })
            .Select((left, right) => left.Name);
        var result = await description.ToListAsync<string>();

        // Assert
        Assert.Equal("Select `s`.`Name` \r\nFrom `samples` As `s` \r\nJoin `samples` As `p` On `s`.`Id`=`p`.`Id` \r\nOrder By `s`.`Id`", description.ToSql());
        Assert.Equal(new[] { "one", "two", "three" }, result);
    }

    /// <summary>
    /// 测试目的：Lambda 查询应通过类型化 LeftJoin 保留没有关联订单的主表记录。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenLeftJoinedWithMappedEntity_ShouldKeepUnmatchedRows()
    {
        // Arrange
        await SeedAsync();
        using var query = _fixture.CreateQuery();
        var sample = await query.Query<Sample>().Select("Id,Name,Amount").From("samples").OrderBy("Id").FirstAsync();
        using (var executor = _fixture.CreateExecutor())
            await executor.ExecuteSqlAsync("Insert Into Orders(Id,TenantId,Name) Values (@id,@tenantId,@name)",
            new { id = sample.Id, tenantId = "tenant-1", name = "order-one" });

        // Act
        var result = await query.From<SqliteStructuredTableSample>("s")
            .LeftJoin<SqliteStructuredOrderSample>((sample, order) => sample.Id == order.Id, "o")
            .OrderBy((sample, order) => new object[] { sample.Id })
            .Select((sample, order) => new LambdaJoinResult
            {
                Name = sample.Name,
                TenantId = order.TenantId
            })
            .ToListAsync<LambdaJoinResult>();

        // Assert
        Assert.Equal(new[] { "one:tenant-1", "two:", "three:" },
            result.Select(item => $"{item.Name}:{item.TenantId}"));
    }

    /// <summary>
    /// 测试目的：原始 Fluent 查询应组合字符串 GroupBy 与 Having，并将聚合结果映射为目标标量类型。
    /// </summary>
    [Fact]
    public async Task Fluent_WhenGroupedWithHaving_ShouldExecuteAggregateResults()
    {
        // Arrange
        await SeedAsync();
        await InsertAsync("one");
        using var query = _fixture.CreateQuery();

        // Act
        var description = query.Query<int>()
            .AppendSelect("Count(*)")
            .From("samples")
            .GroupBy("Name")
            .HavingRaw("Count(*) > 1");
        var result = await description.ToListAsync();

        // Assert
        Assert.Equal(new[] { 2 }, result);
        Assert.Equal("Select Count(*) \r\nFrom `samples` \r\nGroup By `Name` Having Count(*) > 1",
            description.ToSql());
    }

    /// <summary>
    /// 测试目的：Lambda 查询应支持去重投影分页，并对去重后的投影返回正确总数与页数据。
    /// </summary>
    [Fact]
    public async Task Lambda_WhenDistinctProjectionPaged_ShouldReturnDistinctPage()
    {
        // Arrange
        await SeedAsync();
        await InsertAsync("one");
        using var query = _fixture.CreateQuery();

        // Act
        var description = query.From<SqliteStructuredTableSample>()
            .Distinct()
            .OrderBy(sample => sample.Name)
            .Select(sample => sample.Name);
        var page = await description.ToPageAsync<string>(new Pager(1, 2) { Order = "Name" });

        // Assert
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new[] { "one", "three" }, page.Data);
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
    }

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 创建样例列表查询。
    /// </summary>
    /// <returns>SQL 查询对象。</returns>
    private ISqlQuery CreateSamplesQuery()
    {
        return _fixture.CreateQuery();
    }

    /// <summary>
    /// 创建样例实体独立查询描述。
    /// </summary>
    /// <param name="query">承载连接和事务资源的根查询。</param>
    /// <returns>按标识排序的样例查询描述。</returns>
    private static SqlFluentQuery<Sample> CreateSamplesDescription(ISqlQuery query) =>
        query.Query<Sample>().Select("Id,Name,Amount").From("samples").OrderBy("Id");

    /// <summary>
    /// 创建聚合独立查询描述。
    /// </summary>
    /// <typeparam name="TResult">聚合结果映射类型。</typeparam>
    /// <param name="query">承载连接和事务资源的根查询。</param>
    /// <returns>带样例表别名的聚合查询描述。</returns>
    private static SqlFluentQuery<TResult> CreateAggregateDescription<TResult>(ISqlQuery query) =>
        query.Query<TResult>().From("samples", "s");

    /// <summary>
    /// 写入一个样例记录。
    /// </summary>
    /// <param name="name">样例名称。</param>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>写入任务。</returns>
    private async Task InsertAsync(string name, string dbKey = "first")
    {
        var manager = _fixture.GetDatabaseScopeManager();
        using (manager.Use(dbKey))
        using (var executor = _fixture.CreateExecutor(dbKey))
            await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name });
    }

    /// <summary>
    /// 写入聚合测试样例记录。
    /// </summary>
    /// <param name="name">样例名称。</param>
    /// <param name="amount">样例金额。</param>
    /// <returns>写入任务。</returns>
    private async Task InsertAggregateSampleAsync(string name, decimal? amount)
    {
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values (@name, @amount)", new { name, amount });
    }

    /// <summary>
    /// 写入包含重复值和空值的聚合测试数据。
    /// </summary>
    /// <returns>写入任务。</returns>
    private async Task SeedAggregateSamplesAsync()
    {
        await InsertAggregateSampleAsync("A", 10m);
        await InsertAggregateSampleAsync("A", 10m);
        await InsertAggregateSampleAsync("B", 20m);
        await InsertAggregateSampleAsync(null, null);
    }

    /// <summary>
    /// 写入流式测试样例记录。
    /// </summary>
    /// <returns>写入任务。</returns>
    private async Task SeedAsync()
    {
        await InsertAsync("one");
        await InsertAsync("two");
        await InsertAsync("three");
    }

    /// <summary>
    /// 在指定数据库作用域中写入样例记录。
    /// </summary>
    /// <param name="manager">数据库作用域管理器。</param>
    /// <param name="dbKey">数据源标识。</param>
    /// <param name="name">样例名称。</param>
    /// <returns>写入任务。</returns>
    private async Task InsertInScopeAsync(IDatabaseScopeManager manager, string dbKey, string name)
    {
        await Task.Yield();
        using (manager.Use(dbKey))
            await InsertAsync(name, dbKey);
    }

    /// <summary>
    /// SQLite 查询样例实体。
    /// </summary>
    private sealed class Sample
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 金额。
        /// </summary>
        public decimal? Amount { get; set; }
    }

    /// <summary>
    /// SQLite 邻接表递归查询投影。
    /// </summary>
    private sealed class HierarchyNode
    {
        /// <summary>
        /// 节点标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 父节点标识；根节点为 <see langword="null"/>。
        /// </summary>
        public int? ParentId { get; set; }

        /// <summary>
        /// 节点名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 从根节点开始计算的层级深度。
        /// </summary>
        public int Depth { get; set; }
    }

    /// <summary>
    /// SQLite 多映射第二段样例实体。
    /// </summary>
    private sealed class SampleName
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
    /// SQLite 双对象多映射结果模型。
    /// </summary>
    private sealed class SamplePair
    {
        /// <summary>
        /// 第一段样例名称。
        /// </summary>
        public string SampleName { get; set; }

        /// <summary>
        /// 第二段样例名称。
        /// </summary>
        public string RelatedName { get; set; }
    }

    /// <summary>
    /// SQLite 三对象多映射结果模型。
    /// </summary>
    private sealed class SampleTriple
    {
        /// <summary>
        /// 第一段样例名称。
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// 第二段样例名称。
        /// </summary>
        public string SecondName { get; set; }

        /// <summary>
        /// 第三段样例名称。
        /// </summary>
        public string ThirdName { get; set; }
    }

    /// <summary>
    /// SQLite 四对象多映射结果模型。
    /// </summary>
    private sealed class SampleQuad
    {
        /// <summary>
        /// 第一段样例名称。
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// 第二段样例名称。
        /// </summary>
        public string SecondName { get; set; }

        /// <summary>
        /// 第三段样例名称。
        /// </summary>
        public string ThirdName { get; set; }

        /// <summary>
        /// 第四段样例名称。
        /// </summary>
        public string FourthName { get; set; }
    }

    /// <summary>
    /// SQLite 五对象多映射结果模型。
    /// </summary>
    private sealed class SampleQuint
    {
        /// <summary>
        /// 第一段样例名称。
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// 第二段样例名称。
        /// </summary>
        public string SecondName { get; set; }

        /// <summary>
        /// 第三段样例名称。
        /// </summary>
        public string ThirdName { get; set; }

        /// <summary>
        /// 第四段样例名称。
        /// </summary>
        public string FourthName { get; set; }

        /// <summary>
        /// 第五段样例名称。
        /// </summary>
        public string FifthName { get; set; }
    }

    /// <summary>
    /// SQLite 六对象多映射结果模型。
    /// </summary>
    private sealed class SampleSext
    {
        /// <summary>
        /// 第一段样例名称。
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// 第二段样例名称。
        /// </summary>
        public string SecondName { get; set; }

        /// <summary>
        /// 第三段样例名称。
        /// </summary>
        public string ThirdName { get; set; }

        /// <summary>
        /// 第四段样例名称。
        /// </summary>
        public string FourthName { get; set; }

        /// <summary>
        /// 第五段样例名称。
        /// </summary>
        public string FifthName { get; set; }

        /// <summary>
        /// 第六段样例名称。
        /// </summary>
        public string SixthName { get; set; }
    }

    /// <summary>
    /// SQLite 七对象多映射结果模型。
    /// </summary>
    private sealed class SampleSept
    {
        /// <summary>
        /// 第一段样例名称。
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// 第二段样例名称。
        /// </summary>
        public string SecondName { get; set; }

        /// <summary>
        /// 第三段样例名称。
        /// </summary>
        public string ThirdName { get; set; }

        /// <summary>
        /// 第四段样例名称。
        /// </summary>
        public string FourthName { get; set; }

        /// <summary>
        /// 第五段样例名称。
        /// </summary>
        public string FifthName { get; set; }

        /// <summary>
        /// 第六段样例名称。
        /// </summary>
        public string SixthName { get; set; }

        /// <summary>
        /// 第七段样例名称。
        /// </summary>
        public string SeventhName { get; set; }
    }

    /// <summary>
    /// SQLite 聚合结果映射模型。
    /// </summary>
    private sealed class AggregateResult
    {
        /// <summary>
        /// 去重后的非空名称数量。
        /// </summary>
        public int DistinctNameCount { get; set; }

        /// <summary>
        /// 去重后的非空金额总和。
        /// </summary>
        public decimal DistinctAmount { get; set; }
    }

    /// <summary>
    /// Lambda 多表投影结果模型。
    /// </summary>
    private sealed class LambdaJoinResult
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 租户标识。
        /// </summary>
        public string TenantId { get; set; }
    }

    /// <summary>
    /// Lambda DTO 派生表的公开成员模型。
    /// </summary>
    private sealed class LambdaSubqueryResult
    {
        /// <summary>
        /// 样例标识。
        /// </summary>
        public int SampleId { get; set; }

        /// <summary>
        /// 租户标识。
        /// </summary>
        public string TenantId { get; set; }
    }

    /// <summary>
    /// Lambda DTO 成员初始化投影结果模型。
    /// </summary>
    private sealed class LambdaMemberInitResult
    {
        /// <summary>
        /// 显示名称。
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 可空金额。
        /// </summary>
        public decimal? OptionalAmount { get; set; }
    }

    /// <summary>
    /// SQLite SQL 诊断观察器。
    /// </summary>
    private sealed class SqliteDiagnosticObserver : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object>>, IDisposable
    {
        private readonly Action<DiagnosticsMessage> _onMessage;
        private readonly IDisposable _allSubscription;
        private IDisposable _listenerSubscription;

        public SqliteDiagnosticObserver(Action<DiagnosticsMessage> onMessage)
        {
            _onMessage = onMessage;
            _allSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == SqlQueryDiagnosticListenerNames.DiagnosticListenerName)
                _listenerSubscription = listener.Subscribe(this);
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key == SqlQueryDiagnosticListenerNames.BeforeExecute && value.Value is DiagnosticsMessage message)
                _onMessage(message);
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void Dispose()
        {
            _listenerSubscription?.Dispose();
            _allSubscription.Dispose();
        }
    }

    /// <summary>
    /// SQLite 原始子查询结果实体。
    /// </summary>
    private sealed class OrderSample
    {
        /// <summary>
        /// 订单标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 订单名称。
        /// </summary>
        public string Name { get; set; }
    }
}