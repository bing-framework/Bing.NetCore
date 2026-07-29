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
            query.AppendSelect("Name").From("reporting.samples");

            var result = query.ExecuteScalar<string>();

            // Assert
            Assert.Equal("attached", result);
            scope.Commit();
        }
    }

    /// <summary>
    /// 测试目的：SQLite 真实执行诊断应保留作用域映射配置，并仅在显式启用后输出租户标识。
    /// </summary>
    [Fact]
    public async Task ExecuteSql_WhenDiagnosticsEnabled_ShouldPublishPinnedScopeContext()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqliteDiagnosticObserver(item => message = item);
        var manager = _fixture.GetDatabaseScopeManager();

        // Act
        using (manager.Use(new DatabaseScopeOptions { DbKey = "first", TenantId = "tenant-sqlite" }))
        using (var executor = _fixture.CreateExecutor("first"))
        {
            executor.Config(options => options.IncludeTenantIdInDiagnostics = true);
            await executor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)", new { name = "diagnostic" });
        }

        // Assert
        Assert.NotNull(message);
        Assert.Equal("first", message.Connection.DbKey);
        Assert.Equal("first-profile", message.MappingProfile);
        Assert.Equal("tenant-sqlite", message.TenantId);
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
        query.AppendSelect("Count(*)").AppendFrom("samples");

        // Act
        using (manager.Use("second"))
            query.ExecuteScalar<int>();

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
        query.AppendSelect("Id,Name,Amount").AppendFrom("samples")
            .In("Name", new object[] { "first", "third" });

        var result = query.ExecuteQuery<Sample>();

        Assert.Equal(new[] { "first", "third" }, result.Select(t => t.Name));
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
        query.AppendSelect("Count(*)").AppendFrom("samples");

        var result = query.ExecuteScalar<int>();

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
        query.AppendSelect("Id,Name,Amount").AppendFrom("samples").AppendWhere("Name=@name").AddParam("name", "single");

        var result = query.ExecuteSingle<Sample>();

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
        query.Select("o.Id,o.Name")
            .AppendFrom("(Select * From Orders Where TenantId=@TenantId) o")
            .AddParam("TenantId", "tenant-1");

        // Act
        var firstSql = query.GetBuilder().ToSql();
        var secondSql = query.GetBuilder().ToSql();

        // Assert
        Assert.Equal("Select `o`.`Id`,`o`.`Name` \r\nFrom (Select * From Orders Where TenantId=@TenantId) o", firstSql);
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(new[] { "@TenantId" }, query.GetParams().Keys);
        Assert.Equal("tenant-1", query.GetParam("TenantId"));

        // Act
        var result = query.ExecuteQuery<OrderSample>();

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
        query.Select("o.Id,o.Name")
            .AppendFrom("(Select * From Orders Where TenantId=@TenantId) o")
            .AddParam("TenantId", "tenant-1")
            .Where("o.Name", "second");

        // Act
        var sql = query.GetBuilder().ToSql();
        var result = query.ExecuteSingle<OrderSample>();

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
        query.AppendSelect("Name").From<SqliteStructuredTableSample>();

        // Act
        var result = query.ExecuteScalar<string>();

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
        using var query = CreateSamplesQuery();

        var result = query.ExecuteQuery<Sample>(buffered: false);

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
        using var query = CreateSamplesQuery();

        var result = await query.ExecuteQueryAsync<Sample>(buffered: false);

        Assert.Equal(3, result.Count);
    }

    /// <summary>
    /// 测试 - SQLite缓冲和非缓冲列表结果应一致。
    /// </summary>
    [Fact]
    public async Task ExecuteQuery_ShouldReturnSameRowsForBufferedModes()
    {
        await SeedAsync();
        using var bufferedQuery = CreateSamplesQuery();
        using var nonBufferedQuery = CreateSamplesQuery();

        var buffered = bufferedQuery.ExecuteQuery<Sample>(buffered: true);
        var nonBuffered = nonBufferedQuery.ExecuteQuery<Sample>(buffered: false);

        Assert.Equal(buffered.Select(t => t.Name), nonBuffered.Select(t => t.Name));
    }

    /// <summary>
    /// 测试 - SQLite非缓冲列表查询仍应返回完整结果。
    /// </summary>
    [Fact]
    public async Task ExecuteQuery_ShouldMaterializeAllRowsWhenBufferedFalse()
    {
        await SeedAsync();
        using var query = CreateSamplesQuery();

        var result = query.ExecuteQuery<Sample>(buffered: false);

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
        using (var query = CreateSamplesQuery())
            result = query.ExecuteQuery<Sample>(buffered: false);

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
        using var query = CreateSamplesQuery();

        var result = query.StreamQuery<Sample>().Select(t => t.Name).ToList();

        Assert.Equal(new[] { "one", "two", "three" }, result);
    }

    /// <summary>
    /// 测试 - SQLite异步流式查询应完整返回全部结果。
    /// </summary>
    [Fact]
    public async Task StreamAsync_ShouldReturnAllRows()
    {
        await SeedAsync();
        using var query = CreateSamplesQuery();
        var result = new List<string>();

        await foreach (var sample in query.StreamAsync<Sample>())
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
        using (var query = CreateSamplesQuery())
        using (var enumerator = query.StreamQuery<Sample>().GetEnumerator())
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
        using (var enumerator = query.StreamQuery<Sample>().GetEnumerator())
        {
            Assert.True(enumerator.MoveNext());
            var exception = Assert.Throws<InvalidOperationException>(() => query.ExecuteScalar());
            Assert.Equal("同一个 SQL Query 或 Executor 实例不支持并发执行，请为每个操作创建独立实例。",
                exception.Message);
        }

        query.AppendSelect("Count(*)").AppendFrom("samples");
        Assert.Equal(3, query.ExecuteScalar<int>());
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
            await foreach (var _ in query.StreamAsync<Sample>(cancellationToken: cancellationTokenSource.Token))
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
            await foreach (var _ in query.StreamAsync<Sample>(cancellationToken: cancellationTokenSource.Token))
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
            await foreach (var _ in query.StreamAsync<Sample>())
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
            query.AppendSelect("Count(*)").AppendFrom("samples");
            Assert.Equal(1, query.ExecuteScalar<int>());
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
                    using var childExecutor = executorFactory.Create<ISqlExecutor>();
                    await childExecutor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)",
                        new { name = "child-exception" });
                    throw new InvalidOperationException("expected child failure");
                }
            });

            Assert.Equal("first", accessor.Current.DbKey);
            using var parentExecutor = executorFactory.Create<ISqlExecutor>();
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
                    using var childExecutor = executorFactory.Create<ISqlExecutor>();
                    await childExecutor.ExecuteSqlAsync("Insert Into samples(Name) Values (@name)",
                        new { name = "child-cancelled" });
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                }
            });

            Assert.Equal("first", accessor.Current.DbKey);
            using var parentExecutor = executorFactory.Create<ISqlExecutor>();
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
            query.AppendSelect("Count(*)").AppendFrom("samples");

            Assert.Equal(1, query.ExecuteScalar<int>());
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
        using var countAllQuery = _fixture.CreateQuery();
        var countAll = countAllQuery.Count(alias: "Total").From("samples", "s").ExecuteScalar<int>();
        using var countColumnQuery = _fixture.CreateQuery();
        var countColumn = countColumnQuery.Count("s.Amount", "AmountCount").From("samples", "s")
            .ExecuteScalar<int>();
        using var distinctCountQuery = _fixture.CreateQuery();
        var distinctCount = distinctCountQuery.Count("s.Name", "NameCount", distinct: true)
            .From("samples", "s")
            .ExecuteScalar<int>();
        using var sumQuery = _fixture.CreateQuery();
        var sum = sumQuery.Sum("s.Amount", "Total").From("samples", "s").ExecuteScalar<decimal>();
        using var averageQuery = _fixture.CreateQuery();
        var average = averageQuery.Avg("s.Amount", "Average", distinct: true).From("samples", "s")
            .ExecuteScalar<decimal>();
        using var maximumQuery = _fixture.CreateQuery();
        var maximum = maximumQuery.Max("s.Amount", "Maximum", distinct: true).From("samples", "s")
            .ExecuteScalar<decimal>();
        using var minimumQuery = _fixture.CreateQuery();
        var minimum = minimumQuery.Min("s.Amount", "Minimum", distinct: true).From("samples", "s")
            .ExecuteScalar<decimal>();

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
        using var rawQuery = _fixture.CreateQuery();
        var rawTotal = rawQuery.AggregateRaw(SqlAggregateFunction.Sum, "Amount * 2", "DoubleTotal")
            .From("samples")
            .ExecuteScalar<decimal>();
        using var expressionQuery = _fixture.CreateQuery();
        var expressionTotal = expressionQuery.AggregateExpression(SqlAggregateFunction.Sum, "[s].[Amount] * 2",
                "DoubleTotal")
            .From("samples", "s")
            .ExecuteScalar<decimal>();
        using var caseQuery = _fixture.CreateQuery();
        var caseCount = caseQuery.AggregateExpression(SqlAggregateFunction.Count,
                "Case When [s].[Amount] Is Not Null Then [s].[Name] End", "NamedCount", distinct: true)
            .From("samples", "s")
            .ExecuteScalar<int>();
        using var dtoQuery = _fixture.CreateQuery();
        var result = dtoQuery.Count("s.Name", "DistinctNameCount", distinct: true)
            .Sum("s.Amount", "DistinctAmount", distinct: true)
            .From("samples", "s")
            .ExecuteSingle<AggregateResult>();

        // Assert
        Assert.Equal(80m, rawTotal);
        Assert.Equal(80m, expressionTotal);
        Assert.Equal(2, caseCount);
        Assert.Equal(2, result.DistinctNameCount);
        Assert.Equal(30m, result.DistinctAmount);
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
        var query = _fixture.CreateQuery();
        query.AppendSelect("Id,Name,Amount").AppendFrom("samples").AppendWhere("1=1 Order By Id");
        return query;
    }

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