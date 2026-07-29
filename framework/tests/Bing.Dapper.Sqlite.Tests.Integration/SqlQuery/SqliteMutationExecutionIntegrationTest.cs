using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Dapper.Tests.Infrastructure;
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
        entity.Id = identityQuery.Select("Id").From("samples").Where("Name", "created").ExecuteScalar<int>();
        entity.Name = "updated";
        var updated = executor.Update(entity, new SqlUpdateOptions
        {
            IncludeProperties = new[] { nameof(MutationSample.Name) },
            OriginalValues = new MutationOriginalValues { SecretText = "v1" }
        });
        var deleted = executor.Delete(entity, new SqlDeleteOptions
        {
            OriginalValues = new MutationOriginalValues { SecretText = "v1" }
        });

        // Assert
        Assert.Equal(1, inserted);
        Assert.Equal(1, updated);
        Assert.Equal(1, deleted);
        Assert.Equal(0, await _fixture.CountAsync());
    }

    /// <summary>
    /// 测试目的：并发原始值不匹配时删除应返回零行，避免删除已被其它操作修改的数据。
    /// </summary>
    [Fact]
    public async Task Delete_WhenConcurrencyOriginalValueDoesNotMatch_ShouldNotDeleteRow()
    {
        // Arrange
        var entity = new MutationSample { Name = "protected", Amount = 1m, SecretText = "v1" };
        using var executor = _fixture.CreateExecutor();
        await executor.InsertAsync(entity);
        using var identityQuery = _fixture.CreateQuery();
        entity.Id = identityQuery.Select("Id").From("samples").Where("Name", "protected").ExecuteScalar<int>();

        // Act
        var affectedRows = executor.Delete(entity, new SqlDeleteOptions
        {
            OriginalValues = new MutationOriginalValues { SecretText = "other" }
        });

        // Assert
        Assert.Equal(0, affectedRows);
        Assert.Equal(1, await _fixture.CountAsync());
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
            Strategy = SqlBatchStrategy.Combined,
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
        first.Id = identityQuery.Select("Id").From("samples").Where("Name", "first").ExecuteScalar<int>();
        second.Id = identityQuery.Select("Id").From("samples").Where("Name", "second").ExecuteScalar<int>();
        first.Name = "first-updated";
        second.Name = "second-not-updated";
        second.SecretText = "other";

        // Act
        var affectedRows = executor.UpdateBatch(new[] { first, second }, new SqlBatchUpdateOptions
        {
            BatchSize = 1,
            UpdateOptions = new SqlUpdateOptions { IncludeProperties = new[] { nameof(MutationSample.Name) } }
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
    /// 仅包含并发原始值的对象。
    /// </summary>
    private sealed class MutationOriginalValues
    {
        /// <summary>
        /// 乐观并发令牌原始值。
        /// </summary>
        public string SecretText { get; set; }
    }
}