using Bing.Dapper.Tests.Infrastructure;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Mutations;
using Bing.Test.Shared;
using Bing.Data.Sql;
using Bing.Data.Sql.Metadata;

namespace Bing.Dapper.Tests.SqlExecutor;

/// <summary>
/// PostgreSQL SQL 执行器真实执行集成测试。
/// </summary>
[Collection(PostgreSqlIntegrationDatabaseCollection.Name)]
public sealed class PostgreSqlExecutorTest : IAsyncLifetime
{
    /// <summary>
    /// PostgreSQL 集成测试数据库固定装置。
    /// </summary>
    private readonly PostgreSqlIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化一个<see cref="PostgreSqlExecutorTest"/>类型的实例。
    /// </summary>
    /// <param name="fixture">PostgreSQL 集成测试数据库固定装置。</param>
    public PostgreSqlExecutorTest(PostgreSqlIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 测试 - PostgreSQL 执行器应返回 Insert、Update、Delete 与无匹配更新的实际影响行数。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteSqlAsync_ShouldReturnAffectedRowsForInsertUpdateAndDelete()
    {
        // Arrange
        var id = Guid.NewGuid();
        using var executor = _fixture.CreateExecutor();

        // Act
        var inserted = await executor.ExecuteSqlAsync(
            "Insert Into public.integration_products(id,code,name,amount,occurred_at) Values(@id,@code,@name,@amount,@occurredAt)",
            new { id, code = "executor", name = "before", amount = 1m, occurredAt = new DateTime(2026, 7, 24) });
        var updated = await executor.ExecuteSqlAsync("Update public.integration_products Set name=@name Where id=@id",
            new { id, name = "after" });
        var deleted = await executor.ExecuteSqlAsync("Delete From public.integration_products Where id=@id", new { id });
        var unmatched = await executor.ExecuteSqlAsync("Update public.integration_products Set name=@name Where id=@id",
            new { id = Guid.NewGuid(), name = "missing" });

        // Assert
        Assert.Equal(1, inserted);
        Assert.Equal(1, updated);
        Assert.Equal(1, deleted);
        Assert.Equal(0, unmatched);
    }

    /// <summary>
    /// 测试目的：PostgreSQL 优化批量 Update 应真实执行 UPDATE FROM VALUES，并在并发令牌未命中时抛出并发异常。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task UpdateBatchAsync_WhenProviderOptimized_ShouldUpdateRowsAndRejectConcurrencyConflict()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        var first = new PostgreSqlMutationProduct
        {
            Id = Guid.NewGuid(), Code = "batch-first", Name = "before-first", Amount = 1m,
            OccurredAt = new DateTime(2026, 7, 24, 0, 0, 0, DateTimeKind.Utc), Version = 1
        };
        var second = new PostgreSqlMutationProduct
        {
            Id = Guid.NewGuid(), Code = "batch-second", Name = "before-second", Amount = 2m,
            OccurredAt = new DateTime(2026, 7, 24, 0, 0, 0, DateTimeKind.Utc), Version = 1
        };
        await executor.InsertBatchAsync(new[] { first, second });
        first.Name = "after-first";
        second.Name = "after-second";
        var options = new SqlBatchUpdateOptions
        {
            Strategy = SqlBatchUpdateStrategy.ProviderOptimized,
            UpdateOptions = new SqlUpdateOptions { IncludeProperties = new[] { nameof(PostgreSqlMutationProduct.Name) } }
        };

        // Act
        var updated = await executor.UpdateBatchAsync(new[] { first, second }, options);
        var conflict = await Assert.ThrowsAsync<Bing.Exceptions.ConcurrencyException>(() => executor.UpdateBatchAsync(
            new[]
            {
                new PostgreSqlMutationProduct
                {
                    Id = first.Id, Code = first.Code, Name = "conflict", Amount = first.Amount,
                    OccurredAt = first.OccurredAt, Version = 2
                }
            }, options));

        // Assert
        Assert.Equal(2, updated);
        Assert.Contains("批量 Update 预期影响 1 行，实际影响 0 行。", conflict.Message);
    }

    /// <summary>
    /// 测试目的：统一 Builder 的结构化 UpdateFrom 应在 PostgreSQL 中真实更新匹配行并返回实际影响行数。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteAsync_WhenUpdateFromBuilderIsConfigured_ShouldUpdateMatchedRow()
    {
        // Arrange
        var id = Guid.NewGuid();
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert Into public.integration_products(id,code,name,amount,occurred_at) Values(@id,@code,@name,@amount,@occurredAt)",
            new { id, code = "update-from", name = "before", amount = 1m, occurredAt = new DateTime(2026, 7, 30) });
        await executor.ExecuteSqlAsync(
            "Insert Into public.integration_product_updates(id,name) Values(@id,@name)",
            new { id, name = "after" });
        var builder = executor.CreateBuilder()
            .Update(new SqlTableReference { Schema = "public", TableName = "integration_products", Alias = "t" })
            .UpdateFrom(new SqlTableReference { Schema = "public", TableName = "integration_product_updates", Alias = "s" })
            .SetFrom("name", "name")
            .WhereFrom("id", "id");

        // Act
        var affectedRows = await executor.ExecuteAsync(builder.ToMutationDescription());
        using var query = _fixture.CreateQuery();
        var name = await query.Sql<string>().Select("name").From("public.integration_products").Where("id", id)
            .ScalarAsync();

        // Assert
        Assert.Equal(1, affectedRows);
        Assert.Equal("after", name);
    }

    /// <summary>
    /// 测试目的：统一 Builder 的结构化 DeleteUsing 应只删除 PostgreSQL 中与来源表匹配的目标行。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteAsync_WhenDeleteUsingBuilderIsConfigured_ShouldDeleteMatchedRow()
    {
        // Arrange
        var matchedId = Guid.NewGuid();
        var unmatchedId = Guid.NewGuid();
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert Into public.integration_products(id,code,name,amount,occurred_at) Values" +
            "(@matchedId,@matchedCode,@name,@amount,@occurredAt)," +
            "(@unmatchedId,@unmatchedCode,@name,@amount,@occurredAt)",
            new
            {
                matchedId, matchedCode = "delete-using-match", unmatchedId, unmatchedCode = "delete-using-keep",
                name = "before", amount = 1m, occurredAt = new DateTime(2026, 7, 30)
            });
        await executor.ExecuteSqlAsync(
            "Insert Into public.integration_product_updates(id,name) Values(@id,@name)",
            new { id = matchedId, name = "delete" });
        var builder = executor.CreateBuilder()
            .DeleteFrom(new SqlTableReference { Schema = "public", TableName = "integration_products", Alias = "t" })
            .DeleteUsing(new SqlTableReference { Schema = "public", TableName = "integration_product_updates", Alias = "s" })
            .WhereUsing("id", "id");

        // Act
        var affectedRows = await executor.ExecuteAsync(builder.ToMutationDescription());
        using var matchedQuery = _fixture.CreateQuery();
        var matchedCount = await matchedQuery.Sql<int>().AppendSelect("Count(*)").From("public.integration_products")
            .Where("id", matchedId).ScalarAsync();
        using var unmatchedQuery = _fixture.CreateQuery();
        var unmatchedCount = await unmatchedQuery.Sql<int>().AppendSelect("Count(*)").From("public.integration_products")
            .Where("id", unmatchedId).ScalarAsync();
        using var sourceQuery = _fixture.CreateQuery();
        var sourceCount = await sourceQuery.Sql<int>().AppendSelect("Count(*)").From("public.integration_product_updates")
            .Where("id", matchedId).ScalarAsync();

        // Assert
        Assert.Equal(1, affectedRows);
        Assert.Equal(0, matchedCount);
        Assert.Equal(1, unmatchedCount);
        Assert.Equal(1, sourceCount);
    }

    /// <summary>
    /// 测试目的：PostgreSQL 多行 Insert Returning 应通过现有查询链完整物化全部返回行。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteQueryAsync_WhenInsertReturningIsConfigured_ShouldMaterializeReturnedRows()
    {
        // Arrange
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        using var executor = _fixture.CreateExecutor();
        IEnumerable<IReadOnlyList<object>> values = new IReadOnlyList<object>[]
        {
            new object[] { firstId, "returning-first", "first", 1m, new DateTime(2026, 7, 30) },
            new object[] { secondId, "returning-second", "second", 2m, new DateTime(2026, 7, 30) }
        };
        var builder = executor.CreateBuilder()
            .InsertInto(new SqlTableReference { Schema = "public", TableName = "integration_products" })
            .Columns("id", "code", "name", "amount", "occurred_at")
            .Values(values)
            .Returning("id", "name");

        // Act
        var rows = await executor.ExecuteReturningQueryAsync<PostgreSqlReturningProduct>(builder.ToMutationDescription());

        // Assert
        Assert.Equal(new[] { firstId, secondId }, rows.Select(row => row.Id));
        Assert.Equal(new[] { "first", "second" }, rows.Select(row => row.Name));
    }

    /// <summary>
    /// PostgreSQL 优化批量 Update 的映射实体。
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.Table("integration_products", Schema = "public")]
    private sealed class PostgreSqlMutationProduct
    {
        /// <summary>主键。</summary>
        [System.ComponentModel.DataAnnotations.Key]
        [System.ComponentModel.DataAnnotations.Schema.Column("id")]
        public Guid Id { get; set; }

        /// <summary>业务编码。</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("code")]
        public string Code { get; set; }

        /// <summary>名称。</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("name")]
        public string Name { get; set; }

        /// <summary>金额。</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("amount")]
        public decimal? Amount { get; set; }

        /// <summary>发生时间。</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column("occurred_at")]
        public DateTime OccurredAt { get; set; }

        /// <summary>并发令牌。</summary>
        [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
        [System.ComponentModel.DataAnnotations.Schema.Column("version")]
        public int Version { get; set; }
    }

    /// <summary>
    /// PostgreSQL Returning 结果模型。
    /// </summary>
    private sealed class PostgreSqlReturningProduct
    {
        /// <summary>主键。</summary>
        public Guid Id { get; set; }

        /// <summary>名称。</summary>
        public string Name { get; set; }
    }
}