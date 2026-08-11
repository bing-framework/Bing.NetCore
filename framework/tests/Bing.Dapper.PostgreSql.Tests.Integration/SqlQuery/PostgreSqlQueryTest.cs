using System.Data;
using System.Text.Json;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Test.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// PostgreSQL SQL 查询真实执行集成测试。
/// </summary>
[Collection(PostgreSqlIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "PostgreSql")]
public sealed partial class PostgreSqlQueryTest : IAsyncLifetime
{
    /// <summary>
    /// PostgreSQL 集成测试数据库固定装置。
    /// </summary>
    private readonly PostgreSqlIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化一个<see cref="PostgreSqlQueryTest"/>类型的实例。
    /// </summary>
    /// <param name="fixture">PostgreSQL 集成测试数据库固定装置。</param>
    public PostgreSqlQueryTest(PostgreSqlIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 测试目的：PostgreSQL 连接可用时，异步标量查询应返回预期值。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task GetValue_SelectOne_ShouldReturnOne()
    {
        // Arrange
        using var query = _fixture.CreateQuery();
        // Act
        var result = await query.Query<int>().AppendSelect("1").AppendFrom("(Select 1 as Value) t").ScalarAsync();

        // Assert
        Assert.Equal(1, result);
    }

    /// <summary>
    /// 测试目的：参数化列表查询应只返回 UUID 集合对应的 PostgreSQL 记录。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteQuery_ShouldReturnRowsForParameterizedUuidList()
    {
        // Arrange
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        await InsertProductAsync(firstId, "list-first");
        await InsertProductAsync(secondId, "list-second");
        using var query = _fixture.CreateQuery();
        var description = CreateProductDescription(query).In("id", new object[] { firstId, secondId });

        // Act
        var result = await description.ToListAsync();

        // Assert
        Assert.Equal(new[] { firstId, secondId }.OrderBy(id => id), result.Select(product => product.Id).OrderBy(id => id));
    }

    /// <summary>
    /// 测试目的：单行查询应映射 PostgreSQL 的 UUID、数值、时间和 JSONB 字段。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteSingle_ShouldReturnMappedPostgreSqlTypes()
    {
        // Arrange
        var id = Guid.NewGuid();
        var occurredAt = new DateTime(2026, 7, 22, 12, 30, 45);
        const decimal amount = 123456789012.123456m;
        const string payload = "{\"name\":\"Bing\",\"enabled\":true}";
        await InsertProductAsync(id, "single", "single-name", amount, occurredAt, payload);
        using var query = _fixture.CreateQuery();
        var description = CreateProductDescription(query).AppendWhere("id=@id").AddParam("id", id);

        // Act
        var result = description.FirstOrDefault();

        // Assert
        Assert.Equal(id, result.Id);
        Assert.Equal("single", result.Code);
        Assert.Equal("single-name", result.Name);
        Assert.Equal(amount, result.Amount);
        Assert.Equal(occurredAt, result.OccurredAt);
        using var document = JsonDocument.Parse(result.Payload);
        Assert.Equal("Bing", document.RootElement.GetProperty("name").GetString());
        Assert.True(document.RootElement.GetProperty("enabled").GetBoolean());
    }

    /// <summary>
    /// 测试目的：标量查询应返回真实 PostgreSQL 记录数量。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteScalar_ShouldReturnActualCount()
    {
        // Arrange
        await InsertProductAsync(Guid.NewGuid(), "scalar-first");
        await InsertProductAsync(Guid.NewGuid(), "scalar-second");
        // Act
        using var query = _fixture.CreateQuery();
        var result = query.Query<int>().CountAll().From("public.integration_products").Scalar();

        // Assert
        Assert.Equal(2, result);
    }

    /// <summary>
    /// 测试目的：显式空参数应以 PostgreSQL NULL 保存，而非转换为空字符串。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteSql_ShouldPersistExplicitNullParameter()
    {
        // Arrange
        var id = Guid.NewGuid();
        await InsertProductAsync(id, "null", null);
        using var query = _fixture.CreateQuery();
        var description = CreateProductDescription(query).AppendWhere("id=@id").AddParam("id", id);

        // Act
        var result = await description.FirstOrDefaultAsync();

        // Assert
        Assert.Null(result.Name);
    }

    /// <summary>
    /// 测试目的：部分实体参数映射不应丢失未映射的 PostgreSQL 命令参数。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteSql_ShouldKeepUnmappedParametersWhenPartiallyMapped()
    {
        // Arrange
        var id = Guid.NewGuid();
        var occurredAt = new DateTime(2026, 7, 22, 13, 0, 0);
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync<IntegrationProduct>(
            "Insert Into public.integration_products(id,code,name,amount,occurred_at) Values(@id,@code,@name,@amount,@occurredAt)",
            new { id, code = "partial", name = "unmapped-name", amount = 9.5m, occurredAt },
            map => map.Map("code", product => product.Code));
        using var query = _fixture.CreateQuery();
        var description = CreateProductDescription(query).AppendWhere("id=@id").AddParam("id", id);

        // Act
        var result = description.FirstOrDefault();

        // Assert
        Assert.Equal("partial", result.Code);
        Assert.Equal("unmapped-name", result.Name);
        Assert.Equal(9.5m, result.Amount);
    }

    /// <summary>
    /// 测试目的：UUID 数组参数应在 PostgreSQL ANY 条件中被正确绑定。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteQuery_ShouldBindUuidArrayParameter()
    {
        // Arrange
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        await InsertProductAsync(firstId, "array-first");
        await InsertProductAsync(secondId, "array-second");
        using var query = _fixture.CreateQuery();
        var description = query.Query<int>().CountAll().From("public.integration_products")
            .AppendWhere("id=Any(@ids)").AddParam("ids", new[] { firstId, secondId });

        // Act
        var result = await description.ScalarAsync();

        // Assert
        Assert.Equal(2, result);
    }

    /// <summary>
    /// 测试目的：分页查询应执行 PostgreSQL COUNT、LIMIT 和 OFFSET 路径并返回指定页。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task PagerQueryAsync_ShouldReturnOrderedPageAndTotalCount()
    {
        // Arrange
        foreach (var code in new[] { "page-1", "page-2", "page-3", "page-4", "page-5" })
            await InsertProductAsync(Guid.NewGuid(), code);
        using var query = _fixture.CreateQuery();
        var description = CreateProductDescription(query);
        var pager = new Pager(2, 2, order: "code");

        // Act
        var result = await description.ToPageAsync(pager);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(new[] { "page-3", "page-4" }, result.Data.Select(product => product.Code));
    }

    /// <summary>
    /// 测试目的：非缓冲同步查询在返回前应物化全部结果并释放 PostgreSQL 连接资源。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteQuery_ShouldMaterializeRowsWhenBufferedFalse()
    {
        // Arrange
        await SeedProductsAsync();
        List<IntegrationProduct> result;
        using (var query = _fixture.CreateQuery())
            result = CreateProductDescription(query).AsEnumerable().ToList();

        // Act
        await InsertProductAsync(Guid.NewGuid(), "after-buffered-false");

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(4, await CountProductsAsync());
    }

    /// <summary>
    /// 测试目的：非缓冲异步查询在返回前应物化全部 PostgreSQL 结果。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteQueryAsync_ShouldMaterializeRowsWhenBufferedFalse()
    {
        // Arrange
        await SeedProductsAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var result = new List<IntegrationProduct>();
        await foreach (var product in CreateProductDescription(query).AsAsyncEnumerable())
            result.Add(product);

        // Assert
        Assert.Equal(new[] { "stream-1", "stream-2", "stream-3" }, result.Select(product => product.Code));
        await InsertProductAsync(Guid.NewGuid(), "after-async-buffered-false");
        Assert.Equal(4, await CountProductsAsync());
    }

    /// <summary>
    /// 测试目的：同步流式查询完整枚举后应返回有序记录并释放资源。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task StreamQuery_ShouldReturnAllRowsAndReleaseResources()
    {
        // Arrange
        await SeedProductsAsync();
        using var query = _fixture.CreateQuery();

        // Act
        var result = CreateProductDescription(query).AsEnumerable().Select(product => product.Code).ToList();

        // Assert
        Assert.Equal(new[] { "stream-1", "stream-2", "stream-3" }, result);
        await InsertProductAsync(Guid.NewGuid(), "after-stream-complete");
        Assert.Equal(4, await CountProductsAsync());
    }

    /// <summary>
    /// 测试目的：异步流式查询完整枚举后应返回有序 PostgreSQL 记录。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task StreamAsync_ShouldReturnAllRows()
    {
        // Arrange
        await SeedProductsAsync();
        using var query = _fixture.CreateQuery();
        var result = new List<string>();

        // Act
        await foreach (var product in CreateProductDescription(query).AsAsyncEnumerable())
            result.Add(product.Code);

        // Assert
        Assert.Equal(new[] { "stream-1", "stream-2", "stream-3" }, result);
    }

    /// <summary>
    /// 测试目的：同步流式查询提前停止时应释放 Reader，使后续 PostgreSQL 写入可执行。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task StreamQuery_ShouldReleaseReaderWhenEnumerationStopsEarly()
    {
        // Arrange
        await SeedProductsAsync();
        using (var query = _fixture.CreateQuery())
        using (var enumerator = CreateProductDescription(query).AsEnumerable().GetEnumerator())
            Assert.True(enumerator.MoveNext());

        // Act
        await InsertProductAsync(Guid.NewGuid(), "after-stream-early-stop");

        // Assert
        Assert.Equal(4, await CountProductsAsync());
    }

    /// <summary>
    /// 测试目的：异步流式查询取消时应抛出取消异常并释放 PostgreSQL 资源。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task StreamAsync_ShouldReleaseResourcesWhenCancelled()
    {
        // Arrange
        await SeedProductsAsync();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        using var query = _fixture.CreateQuery();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in CreateProductDescription(query)
                               .AsAsyncEnumerable(cancellationToken: cancellationTokenSource.Token))
            {
            }
        });

        // Assert
        await InsertProductAsync(Guid.NewGuid(), "after-stream-cancel");
        Assert.Equal(4, await CountProductsAsync());
    }

    /// <summary>
    /// 测试目的：提交 PostgreSQL 事务后，作用域外应读取到已写入记录。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task TransactionScope_ShouldPersistDataAfterCommit()
    {
        // Arrange
        using (var scope = _fixture.GetTransactionScopeFactory().Begin(PostgreSqlIntegrationDatabaseFixture.PrimaryDatabaseKey))
        using (var executor = scope.CreateExecutor())
        {
            executor.ExecuteSql("Insert Into public.integration_products(id,code,name,amount,occurred_at) Values(@id,@code,@name,@amount,@occurredAt)",
                CreateProductParameters(Guid.NewGuid(), "committed", "committed-name", 1m, new DateTime(2026, 7, 22)));
            scope.Commit();
        }

        // Act
        var count = await CountProductsAsync();

        // Assert
        Assert.Equal(1, count);
    }

    /// <summary>
    /// 测试目的：显式回滚 PostgreSQL 事务后，作用域外不应读取到已写入记录。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task TransactionScope_ShouldNotPersistDataAfterRollback()
    {
        // Arrange
        using (var scope = _fixture.GetTransactionScopeFactory().Begin(PostgreSqlIntegrationDatabaseFixture.PrimaryDatabaseKey))
        using (var executor = scope.CreateExecutor())
        {
            executor.ExecuteSql("Insert Into public.integration_products(id,code,name,amount,occurred_at) Values(@id,@code,@name,@amount,@occurredAt)",
                CreateProductParameters(Guid.NewGuid(), "rolled-back", "rolled-back-name", 1m, new DateTime(2026, 7, 22)));
            scope.Rollback();
        }

        // Act
        var count = await CountProductsAsync();

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>
    /// 测试目的：未完成的 PostgreSQL 事务作用域释放时应自动回滚。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task TransactionScope_ShouldRollbackWhenDisposedWithoutCompletion()
    {
        // Arrange
        using (var scope = _fixture.GetTransactionScopeFactory().Begin(PostgreSqlIntegrationDatabaseFixture.PrimaryDatabaseKey))
        using (var executor = scope.CreateExecutor())
            executor.ExecuteSql("Insert Into public.integration_products(id,code,name,amount,occurred_at) Values(@id,@code,@name,@amount,@occurredAt)",
                CreateProductParameters(Guid.NewGuid(), "implicit-rollback", "implicit-rollback-name", 1m, new DateTime(2026, 7, 22)));

        // Act
        var count = await CountProductsAsync();

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>
    /// 测试目的：同一 PostgreSQL 事务作用域的查询和执行器应共享未提交数据。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public void TransactionScope_ShouldShareTransactionBetweenQueryAndExecutor()
    {
        // Arrange
        using var scope = _fixture.GetTransactionScopeFactory().Begin(PostgreSqlIntegrationDatabaseFixture.PrimaryDatabaseKey);
        using var executor = scope.CreateExecutor();
        using var query = scope.CreateQuery();
        executor.ExecuteSql("Insert Into public.integration_products(id,code,name,amount,occurred_at) Values(@id,@code,@name,@amount,@occurredAt)",
            CreateProductParameters(Guid.NewGuid(), "shared", "shared-name", 1m, new DateTime(2026, 7, 22)));
        // Act
        var count = query.Query<int>().CountAll().From("public.integration_products").Scalar();
        scope.Commit();

        // Assert
        Assert.Equal(1, count);
    }

    /// <summary>
    /// 测试目的：异步 PostgreSQL 事务提交和回滚应更新完成状态并保持数据可见性正确。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task TransactionScopeAsync_ShouldCommitAndRollbackWithCompletionState()
    {
        // Arrange
        await using (var commitScope = await _fixture.GetTransactionScopeFactory()
                         .BeginAsync(PostgreSqlIntegrationDatabaseFixture.PrimaryDatabaseKey, IsolationLevel.Serializable))
        using (var executor = commitScope.CreateExecutor())
        {
            await executor.ExecuteSqlAsync("Insert Into public.integration_products(id,code,name,amount,occurred_at) Values(@id,@code,@name,@amount,@occurredAt)",
                CreateProductParameters(Guid.NewGuid(), "async-committed", "async-committed-name", 1m,
                    new DateTime(2026, 7, 22)));
            await commitScope.CommitAsync();
            Assert.True(commitScope.IsCompleted);
        }

        await using (var rollbackScope = await _fixture.GetTransactionScopeFactory()
                         .BeginAsync(PostgreSqlIntegrationDatabaseFixture.PrimaryDatabaseKey, IsolationLevel.Serializable))
        using (var executor = rollbackScope.CreateExecutor())
        {
            await executor.ExecuteSqlAsync("Insert Into public.integration_products(id,code,name,amount,occurred_at) Values(@id,@code,@name,@amount,@occurredAt)",
                CreateProductParameters(Guid.NewGuid(), "async-rolled-back", "async-rolled-back-name", 1m,
                    new DateTime(2026, 7, 22)));
            await rollbackScope.RollbackAsync();
            Assert.True(rollbackScope.IsCompleted);
        }

        // Act
        var result = await CountProductsAsync();

        // Assert
        Assert.Equal(1, result);
    }

    /// <summary>
    /// 测试目的：显式数据源键、嵌套数据库作用域和事务固定上下文应保持 PostgreSQL 数据隔离。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task DataSourcesAndScopes_ShouldKeepPostgreSqlRoutingIsolated()
    {
        // Arrange
        var manager = _fixture.GetDatabaseScopeManager();
        var executorFactory = _fixture.ServiceProvider.GetRequiredService<ISqlExecutorFactory>();
        using (manager.Use(PostgreSqlIntegrationDatabaseFixture.PrimaryDatabaseKey))
        {
            using (var primaryExecutor = executorFactory.Create())
                await primaryExecutor.ExecuteSqlAsync("Insert Into integration_samples(name) Values(@name)",
                    new { name = "primary-before" });
            using (manager.Use(PostgreSqlIntegrationDatabaseFixture.ReportingDatabaseKey))
            using (var reportingExecutor = executorFactory.Create())
                await reportingExecutor.ExecuteSqlAsync("Insert Into integration_samples(name) Values(@name)",
                    new { name = "reporting" });
            using (var restoredPrimaryExecutor = executorFactory.Create())
                await restoredPrimaryExecutor.ExecuteSqlAsync("Insert Into integration_samples(name) Values(@name)",
                    new { name = "primary-after" });
            using (var scope = _fixture.GetTransactionScopeFactory().Begin())
            {
                using (manager.Use(PostgreSqlIntegrationDatabaseFixture.ReportingDatabaseKey))
                using (var transactionExecutor = scope.CreateExecutor())
                    await transactionExecutor.ExecuteSqlAsync("Insert Into integration_samples(name) Values(@name)",
                        new { name = "primary-transaction" });
                scope.Commit();
            }
        }

        // Act
        var primary = await _fixture.ReadSampleNamesAsync(PostgreSqlIntegrationDatabaseFixture.PrimaryDatabaseKey);
        var reporting = await _fixture.ReadSampleNamesAsync(PostgreSqlIntegrationDatabaseFixture.ReportingDatabaseKey);

        // Assert
        Assert.Equal(new[] { "primary-before", "primary-after", "primary-transaction" }, primary);
        Assert.Equal(new[] { "reporting" }, reporting);
        Assert.Null(_fixture.ServiceProvider.GetRequiredService<IDatabaseContextAccessor>().Current);
    }

    /// <summary>
    /// 测试目的：未知 PostgreSQL 数据源键应失败关闭，不能回退到默认数据源。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public void GetConnectionString_WhenDatabaseKeyIsUnknown_ShouldThrow()
    {
        // Act
        var exception = Assert.Throws<KeyNotFoundException>(() => _fixture.GetConnectionString("unknown"));

        // Assert
        Assert.Contains("unknown", exception.Message);
    }

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 创建产品独立查询描述。
    /// </summary>
    /// <param name="query">承载连接和事务资源的根查询。</param>
    /// <returns>不修改根 Builder 的产品查询描述。</returns>
    private static SqlQuery<IntegrationProduct> CreateProductDescription(ISqlQuery query) =>
        query.Query<IntegrationProduct>()
            .AppendSelect("id As Id,code As Code,name As Name,amount As Amount,occurred_at As OccurredAt,payload::text As Payload")
            .From("public.integration_products")
            .OrderBy("code");

    /// <summary>
    /// 写入 PostgreSQL 产品测试数据。
    /// </summary>
    /// <param name="id">产品标识。</param>
    /// <param name="code">产品编码。</param>
    /// <param name="name">产品名称。</param>
    /// <param name="amount">产品金额。</param>
    /// <param name="occurredAt">发生时间。</param>
    /// <param name="payload">JSONB 载荷。</param>
    /// <returns>异步写入任务。</returns>
    private async Task InsertProductAsync(Guid id, string code, string name = "name", decimal? amount = 1m,
        DateTime? occurredAt = null, string payload = null, string userId = null)
    {
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert Into public.integration_products(id,code,user_id,name,amount,occurred_at,payload) Values(@id,@code,@userId,@name,@amount,@occurredAt,@payload)",
            CreateProductParameters(id, code, name, amount, occurredAt ?? new DateTime(2026, 7, 22), payload, userId));
    }

    /// <summary>
    /// 创建带 PostgreSQL Provider 类型元数据的产品参数。
    /// </summary>
    /// <param name="id">产品标识。</param>
    /// <param name="code">产品编码。</param>
    /// <param name="name">产品名称。</param>
    /// <param name="amount">产品金额。</param>
    /// <param name="occurredAt">发生时间。</param>
    /// <param name="payload">JSONB 载荷。</param>
    /// <returns>产品参数集合。</returns>
    private static SqlParam[] CreateProductParameters(Guid id, string code, string name, decimal? amount,
        DateTime occurredAt, string payload = null, string userId = null) =>
    [
        new SqlParam("id", id, DbType.Guid) { ProviderTypeName = "Uuid" },
        new SqlParam("code", code, DbType.String) { ProviderTypeName = "Text" },
        new SqlParam("userId", userId, DbType.String) { ProviderTypeName = "Text" },
        new SqlParam("name", name, DbType.String) { ProviderTypeName = "Text" },
        new SqlParam("amount", amount, DbType.Decimal) { ProviderTypeName = "Numeric" },
        new SqlParam("occurredAt", occurredAt, DbType.DateTime) { ProviderTypeName = "Timestamp" },
        new SqlParam("payload", payload, DbType.String) { ProviderTypeName = "Jsonb" }
    ];

    /// <summary>
    /// 写入流式测试产品数据。
    /// </summary>
    /// <returns>异步写入任务。</returns>
    private async Task SeedProductsAsync()
    {
        await InsertProductAsync(Guid.NewGuid(), "stream-1");
        await InsertProductAsync(Guid.NewGuid(), "stream-2");
        await InsertProductAsync(Guid.NewGuid(), "stream-3");
    }

    /// <summary>
    /// 统计主数据源中的产品记录。
    /// </summary>
    /// <returns>产品记录数量。</returns>
    private async Task<int> CountProductsAsync()
    {
        using var query = _fixture.CreateQuery();
        return await query.Query<int>().CountAll().From("public.integration_products").ScalarAsync();
    }

    /// <summary>
    /// PostgreSQL 产品查询样例实体。
    /// </summary>
    private sealed class IntegrationProduct
    {
        /// <summary>
        /// 产品标识。
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 产品编码。
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 产品名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 产品金额。
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// 产品发生时间。
        /// </summary>
        public DateTime OccurredAt { get; set; }

        /// <summary>
        /// 产品 JSONB 载荷。
        /// </summary>
        public string Payload { get; set; }
    }
}
