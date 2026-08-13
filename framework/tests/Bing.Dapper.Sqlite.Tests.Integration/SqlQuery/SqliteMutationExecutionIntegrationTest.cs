using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Mutations;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// SQLite 实体写入执行集成测试。
/// </summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteMutationExecutionIntegrationTest : IAsyncLifetime
{
    /// <summary>
    /// SQLite 集成测试数据库固定装置。
    /// </summary>
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化一个<see cref="SqliteMutationExecutionIntegrationTest"/>类型的实例。
    /// </summary>
    /// <param name="fixture">SQLite 集成测试数据库固定装置。</param>
    public SqliteMutationExecutionIntegrationTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 测试目的：实体 Insert、Update 和 Delete 应使用映射列、Identity 排除和并发条件完成真实写入。
    /// </summary>
    [Fact]
    public async Task MutationExecutor_WhenEntityHasIdentityKeyAndConcurrencyColumn_ShouldExecuteCrud()
    {
        // Arrange
        var entity = new MutationSample { Name = "created", Amount = 12.5m, SecretText = "v1" };
        using var executor = _fixture.CreateExecutor();

        // Act
        var inserted = await executor.InsertAsync(entity);
        using var identityQuery = _fixture.CreateQuery();
        entity.Id = identityQuery.Query<int>().Select("Id").From("samples").Where("Name", "created").Scalar();
        entity.Name = "updated";
        var updated = executor.Update(entity, new SqlUpdateOptions<MutationSample>
        {
            IncludeProperties = new[] { nameof(MutationSample.Name) }
        }.Original(item => item.SecretText, "v1"));
        var deleted = executor.Delete(entity, new SqlDeleteOptions<MutationSample>()
            .Original(item => item.SecretText, "v1"));

        // Assert
        Assert.Equal(1, inserted);
        Assert.Equal(1, updated);
        Assert.Equal(1, deleted);
        Assert.Equal(0, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：默认并发策略下原始值不匹配时删除应抛出异常且保留数据。
    /// </summary>
    [Fact]
    public async Task Delete_WhenConcurrencyOriginalValueDoesNotMatch_ShouldThrowAndNotDeleteRow()
    {
        // Arrange
        var entity = new MutationSample { Name = "protected", Amount = 1m, SecretText = "v1" };
        using var executor = _fixture.CreateExecutor();
        await executor.InsertAsync(entity);
        using var identityQuery = _fixture.CreateQuery();
        entity.Id = identityQuery.Query<int>().Select("Id").From("samples").Where("Name", "protected").Scalar();

        // Act
        var exception = Assert.Throws<Bing.Exceptions.ConcurrencyException>(() => executor.Delete(entity,
            new SqlDeleteOptions<MutationSample>().Original(item => item.SecretText, "other")));

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(1, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：带并发列的 Combined Delete 应按整个批次校验两行影响数并完成真实删除。
    /// </summary>
    [Fact]
    public async Task DeleteBatch_WhenCombinedConcurrencyCommandMatchesAllRows_ShouldDeleteAllRows()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        var first = new MutationSample { Name = "delete-first", Amount = 1m, SecretText = "v1" };
        var second = new MutationSample { Name = "delete-second", Amount = 2m, SecretText = "v2" };
        await executor.InsertBatchAsync(new[] { first, second });
        using (var query = _fixture.CreateQuery())
            first.Id = query.Query<int>().Select("Id").From("samples").Where("Name", first.Name).Scalar();
        using (var query = _fixture.CreateQuery())
            second.Id = query.Query<int>().Select("Id").From("samples").Where("Name", second.Name).Scalar();

        // Act
        var affectedRows = await executor.DeleteBatchAsync(new[] { first, second }, new SqlBatchDeleteOptions
        {
            BatchSize = 2,
            UseTransaction = true
        });

        // Assert
        Assert.Equal(2, affectedRows);
        Assert.Equal(0, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：Auto 策略应在 SQLite 支持多行 Values 时自动组合 Insert，并按 BatchSize 分片保持最终影响行数。
    /// </summary>
    [Fact]
    public async Task InsertBatch_WhenEntitiesAreProvided_ShouldInsertAllEntities()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        var entities = new[]
        {
            new MutationSample { Name = "batch-1", Amount = 1m, SecretText = "v1" },
            new MutationSample { Name = "batch-2", Amount = 2m, SecretText = "v1" },
            new MutationSample { Name = "batch-3", Amount = 3m, SecretText = "v1" }
        };

        // Act
        var affectedRows = await executor.InsertBatchAsync(entities, new SqlBatchInsertOptions
        {
            BatchSize = 2,
            UseTransaction = true
        });

        // Assert
        Assert.Equal(3, affectedRows);
        Assert.Equal(3, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：显式 Combined 策略应将同一分片中的实体作为多行 Values 插入，并返回实际影响行数。
    /// </summary>
    [Fact]
    public async Task InsertBatch_WhenCombinedStrategyIsSelected_ShouldInsertAllEntitiesByChunk()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        var entities = new[]
        {
            new MutationSample { Name = "combined-1", Amount = 1m, SecretText = "v1" },
            new MutationSample { Name = "combined-2", Amount = 2m, SecretText = "v1" },
            new MutationSample { Name = "combined-3", Amount = 3m, SecretText = "v1" }
        };

        // Act
        var affectedRows = await executor.InsertBatchAsync(entities, new SqlBatchInsertOptions
        {
            BatchSize = 2,
            Strategy = SqlBatchInsertStrategy.MultiRowValues,
            UseTransaction = true
        });

        // Assert
        Assert.Equal(3, affectedRows);
        Assert.Equal(3, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：批量 Update 的乐观并发不匹配应返回实际受影响行数，而非实体数量。
    /// </summary>
    [Fact]
    public async Task UpdateBatch_WhenOneConcurrencyTokenDoesNotMatch_ShouldReturnActualAffectedRows()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        var first = new MutationSample { Name = "first", Amount = 1m, SecretText = "v1" };
        var second = new MutationSample { Name = "second", Amount = 2m, SecretText = "v1" };
        await executor.InsertBatchAsync(new[] { first, second });
        using var identityQuery = _fixture.CreateQuery();
        first.Id = identityQuery.Query<int>().Select("Id").From("samples").Where("Name", "first").Scalar();
        second.Id = identityQuery.Query<int>().Select("Id").From("samples").Where("Name", "second").Scalar();
        first.Name = "first-updated";
        second.Name = "second-not-updated";
        second.SecretText = "other";

        // Act
        var affectedRows = executor.UpdateBatch(new[] { first, second }, new SqlBatchUpdateOptions
        {
            BatchSize = 1,
            UpdateOptions = new SqlUpdateOptions
            {
                IncludeProperties = new[] { nameof(MutationSample.Name) },
                ConcurrencyConflictBehavior = SqlConcurrencyConflictBehavior.ReturnAffectedRows
            }
        });

        // Assert
        Assert.Equal(1, affectedRows);
        Assert.Equal(2, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：空批量集合应直接返回零行影响数，不创建事务或发起数据库命令。
    /// </summary>
    [Fact]
    public async Task DeleteBatch_WhenEntitySetIsEmpty_ShouldReturnZero()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();

        // Act
        var affectedRows = await executor.DeleteBatchAsync(Array.Empty<MutationSample>());

        // Assert
        Assert.Equal(0, affectedRows);
        Assert.Equal(0, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：统一 Builder 的 Insert Select、Update 和 Delete 应通过 Executor 执行真实参数化写入。
    /// </summary>
    [Fact]
    public async Task ExecuteMutation_WhenUnifiedMutationBuildersAreConfigured_ShouldExecuteCrud()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert Into Orders (Id, TenantId, Name) Values (@Id, @TenantId, @Name)",
            new { Id = 1, TenantId = "tenant-a", Name = "copied" });
        var insertSelect = executor.CreateBuilder()
            .InsertInto("samples")
            .Columns("Name")
            .Select("Name")
            .From("Orders")
            .Where("TenantId", "tenant-a");

        // Act
        var inserted = await executor.ExecuteMutationAsync(insertSelect.ToSqlWriteCommand());
        var updated = executor.ExecuteMutation(insertSelect.New()
            .Update(new SqlTableReference { TableName = "samples" })
            .Set("SecretText", "v2")
            .Where("Name", "copied")
            .ToSqlWriteCommand());
        var deleted = await executor.ExecuteMutationAsync(insertSelect.New()
            .DeleteFrom(new SqlTableReference { TableName = "samples" })
            .Where("SecretText", "v2")
            .ToSqlWriteCommand());

        // Assert
        Assert.Equal(1, inserted);
        Assert.Equal(1, updated);
        Assert.Equal(1, deleted);
        Assert.Equal(0, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：项目固定的 SQLite 运行时版本必须满足 Returning 所需的 3.35 最低版本。
    /// </summary>
    [Fact]
    public async Task ReturningRuntime_WhenBundledSqliteIsUsed_ShouldBeAtLeast335()
    {
        // Arrange
        await using var connection = new SqliteConnection(_fixture.FirstConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "Select sqlite_version()";

        // Act
        var version = Version.Parse(Convert.ToString(await command.ExecuteScalarAsync()));

        // Assert
        Assert.True(version >= new Version(3, 35, 0), $"当前 SQLite 版本 {version} 不支持 Returning。");
    }

    /// <summary>
    /// 测试目的：SQLite 多行 Insert Values Returning 应物化所有数据库生成标识和名称。
    /// </summary>
    [Fact]
    public async Task ExecuteQueryAsync_WhenInsertValuesReturningIsConfigured_ShouldMaterializeRows()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        IEnumerable<IReadOnlyList<object>> values = new IReadOnlyList<object>[]
        {
            new object[] { "returning-first", 1m, "v1" },
            new object[] { "returning-second", 2m, "v2" }
        };
        var builder = executor.CreateBuilder()
            .InsertInto(new SqlTableReference { TableName = "samples" })
            .Columns("Name", "Amount", "SecretText")
            .Values(values)
            .Returning<SqliteReturningRow>(row => new { row.Id, row.Name });

        // Act
        var rows = (await executor.ExecuteReturningAsync<SqliteReturningRow>(builder.ToSqlWriteCommand()))
            .OrderBy(row => row.Id).ToArray();

        // Assert
        Assert.Equal(new[] { "returning-first", "returning-second" }, rows.Select(row => row.Name));
        Assert.All(rows, row => Assert.True(row.Id > 0));
        Assert.Equal(2, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：同一冻结写入命令应可重复通过同步 Returning 执行，每次均使用独立参数和结果物化。
    /// </summary>
    [Fact]
    public async Task ExecuteReturningQuery_WhenSqlWriteCommandIsReused_ShouldMaterializeEachExecution()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        var command = executor.CreateBuilder()
            .InsertInto(new SqlTableReference { TableName = "samples" })
            .Columns("Name", "Amount", "SecretText")
            .Values("returning-repeat", 1m, "v1")
            .Returning<SqliteReturningRow>(row => new { row.Id, row.Name })
            .ToSqlWriteCommand();

        // Act
        var first = Assert.Single(executor.ExecuteReturning<SqliteReturningRow>(command));
        var second = Assert.Single(executor.ExecuteReturning<SqliteReturningRow>(command));

        // Assert
        Assert.True(first.Id > 0);
        Assert.True(second.Id > first.Id);
        Assert.Equal("returning-repeat", first.Name);
        Assert.Equal("returning-repeat", second.Name);
        Assert.Equal(2, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：SQLite Insert Select Returning 应返回插入到目标表的数据库生成行。
    /// </summary>
    [Fact]
    public async Task ExecuteQueryAsync_WhenInsertSelectReturningIsConfigured_ShouldMaterializeRows()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert Into Orders (Id, TenantId, Name) Values (@Id, @TenantId, @Name)",
            new { Id = 1, TenantId = "tenant-returning", Name = "copied-returning" });
        var builder = executor.CreateBuilder()
            .InsertInto(new SqlTableReference { TableName = "samples" })
            .Columns("Name")
            .Select("Name")
            .From("Orders")
            .Where("TenantId", "tenant-returning")
            .Returning<SqliteReturningRow>(row => new { row.Id, row.Name });

        // Act
        var row = Assert.Single(await executor.ExecuteReturningAsync<SqliteReturningRow>(
            builder.ToSqlWriteCommand()));

        // Assert
        Assert.True(row.Id > 0);
        Assert.Equal("copied-returning", row.Name);
    }

    /// <summary>
    /// 测试目的：SQLite Update Returning 应物化更新后的目标行。
    /// </summary>
    [Fact]
    public async Task ExecuteQueryAsync_WhenUpdateReturningIsConfigured_ShouldMaterializeUpdatedRow()
    {
        // Arrange
        await _fixture.InsertSampleAsync("before-update", 1m, "v1");
        using var executor = _fixture.CreateExecutor();
        var builder = executor.CreateBuilder()
            .Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "after-update")
            .Where("Name", "before-update")
            .Returning<SqliteReturningRow>(row => new { row.Id, row.Name });

        // Act
        var row = Assert.Single(await executor.ExecuteReturningAsync<SqliteReturningRow>(
            builder.ToSqlWriteCommand()));

        // Assert
        Assert.True(row.Id > 0);
        Assert.Equal("after-update", row.Name);
        Assert.Equal(new[] { "after-update" }, await _fixture.ReadNamesAsync());
    }

    /// <summary>
    /// 测试目的：SQLite Delete Returning 应物化删除前的目标行并从表中移除数据。
    /// </summary>
    [Fact]
    public async Task ExecuteQueryAsync_WhenDeleteReturningIsConfigured_ShouldMaterializeDeletedRow()
    {
        // Arrange
        await _fixture.InsertSampleAsync("deleted-returning", 1m, "v1");
        using var executor = _fixture.CreateExecutor();
        var builder = executor.CreateBuilder()
            .DeleteFrom(new SqlTableReference { TableName = "samples" })
            .Where("Name", "deleted-returning")
            .Returning<SqliteReturningRow>(row => new { row.Id, row.Name });

        // Act
        var row = Assert.Single(await executor.ExecuteReturningAsync<SqliteReturningRow>(
            builder.ToSqlWriteCommand()));

        // Assert
        Assert.True(row.Id > 0);
        Assert.Equal("deleted-returning", row.Name);
        Assert.Equal(0, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：写入命令创建应拒绝 Select 状态的 Builder，避免把查询状态传入写入执行器。
    /// </summary>
    [Fact]
    public async Task ToSqlWriteCommand_WhenBuilderIsSelect_ShouldThrow()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        var builder = executor.CreateBuilder().Select("Id").From("samples");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => builder.ToSqlWriteCommand());

        // Assert
        Assert.Contains("写入命令必须包含 Insert、Update 或 Delete 操作。", exception.Message);
        Assert.Equal(0, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：非查询 Execute 不得静默丢弃 Mutation Returning 结果集。
    /// </summary>
    [Fact]
    public async Task ExecuteMutationAsync_WhenBuilderHasReturning_ShouldRejectBeforeProviderRendering()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        var builder = executor.CreateBuilder()
            .InsertInto("samples")
            .Columns("Name")
            .Values("returning")
            .Returning("Id");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteMutationAsync(builder.ToSqlWriteCommand()));

        // Assert
        Assert.Equal("包含 Returning 的 Mutation 必须通过查询结果 API 执行。", exception.Message);
        Assert.Equal(0, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：查询结果 API 不得把缺少 Returning 的 Mutation 当作查询执行。
    /// </summary>
    [Fact]
    public async Task ExecuteReturningQueryAsync_WhenMutationHasNoReturning_ShouldRejectBeforeExecution()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        var builder = executor.CreateBuilder().DeleteFrom(new SqlTableReference { TableName = "samples" }).AllowAllRows();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteReturningAsync<int>(builder.ToSqlWriteCommand()));

        // Assert
        Assert.Equal("Mutation 必须配置 Returning 后才能通过查询结果 API 执行。", exception.Message);
        Assert.Equal(0, await _fixture.CountAsync());
    }

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 映射到 SQLite 样例表的实体。
    /// </summary>
    [Table("samples")]
    private sealed class MutationSample
    {
        /// <summary>
        /// 数据库生成的主键。
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 样例名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 样例金额。
        /// </summary>
        public decimal? Amount { get; set; }

        /// <summary>
        /// 乐观并发令牌。
        /// </summary>
        [ConcurrencyCheck]
        public string SecretText { get; set; }
    }

    /// <summary>
    /// SQLite Returning 物化模型。
    /// </summary>
    [Table("samples")]
    private sealed class SqliteReturningRow
    {
        /// <summary>标识。</summary>
        [Column("Id")]
        public int Id { get; set; }

        /// <summary>名称。</summary>
        [Column("Name")]
        public string Name { get; set; }
    }

}