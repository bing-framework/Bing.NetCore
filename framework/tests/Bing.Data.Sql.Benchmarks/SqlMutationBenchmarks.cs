using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// 实体 Mutation 计划、命令构建和批次规划性能基准。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlMutationBenchmarks
{
    /// <summary>
    /// 用于测量的实体集合。
    /// </summary>
    private MutationBenchmarkEntity[] _entities;

    /// <summary>
    /// 复用映射解析器的 Builder，用于测量计划缓存命中。
    /// </summary>
    private DefaultSqlEntityMutationCommandBuilder _cacheHitBuilder;

    /// <summary>
    /// PostgreSQL 批量 Update 渲染上下文。
    /// </summary>
    private SqlBatchUpdateRenderContext _postgreSqlUpdateContext;

    /// <summary>
    /// PostgreSQL 批量 Update Renderer。
    /// </summary>
    private readonly PostgreSqlBatchUpdateRenderer _postgreSqlRenderer = new();


    /// <summary>
    /// Mutation 实体数量。
    /// </summary>
    [Params(10, 100, 1000)]
    public int EntityCount { get; set; }

    /// <summary>
    /// 初始化各规模下稳定的实体和缓存命中 Builder。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _entities = Enumerable.Range(1, EntityCount).Select(index => new MutationBenchmarkEntity
        {
            Id = index,
            Name = $"name-{index}",
            Amount = index,
            Version = index
        }).ToArray();
        _cacheHitBuilder = CreateBuilder();
        _cacheHitBuilder.Insert(_entities[0]);
        var postgreSqlBuilder = new DefaultSqlEntityMutationCommandBuilder(PostgreSqlSqlProvider.Instance,
            SqlBuilderServices.CreateDefault());
        _postgreSqlUpdateContext = postgreSqlBuilder.CreateUpdateRenderContext(_entities, new SqlUpdateOptions
        {
            IncludeProperties = new[] { nameof(MutationBenchmarkEntity.Name), nameof(MutationBenchmarkEntity.Amount) }
        });
        _postgreSqlRenderer.Render(_postgreSqlUpdateContext);
    }

    /// <summary>
    /// 测量已缓存实体映射计划的单条 Insert 命令构建。
    /// </summary>
    /// <returns>可执行 Insert 命令快照。</returns>
    [Benchmark(Baseline = true)]
    public SqlWriteCommand BuildInsertPlanCacheHit() => _cacheHitBuilder.Insert(_entities[0]);

    /// <summary>
    /// 测量单条 Update 命令构建及完整参数快照导出。
    /// </summary>
    /// <returns>可执行 Update 命令快照。</returns>
    [Benchmark]
    public SqlWriteCommand BuildUpdateCommand()
    {
        var builder = CreateBuilder();
        return builder.Update(_entities[0], new SqlUpdateOptions
        {
            IncludeProperties = new[] { nameof(MutationBenchmarkEntity.Name), nameof(MutationBenchmarkEntity.Amount) }
        });
    }

    /// <summary>
    /// 测量指定规模的组合多行 Insert 命令构建和参数导出。
    /// </summary>
    /// <returns>可执行组合 Insert 命令快照。</returns>
    [Benchmark]
    public SqlWriteCommand BuildCombinedInsertCommand() => CreateBuilder().InsertCombined(_entities);

    /// <summary>
    /// 测量无额外 SQL 长度限制时的批次规划。
    /// </summary>
    /// <returns>按参数上限分片的批处理计划。</returns>
    [Benchmark]
    public SqlMutationBatchPlan PlanBatchesByParameterLimit() => new SqlMutationBatchPlanner().Plan(
        new SqlMutationBatchPlanContext(EntityCount, parametersPerEntity: 4, maxParameterCount: 200));

    /// <summary>
    /// 测量 PostgreSQL 批量 Update 在编译 Getter 缓存命中时的完整 SQL 与参数渲染。
    /// </summary>
    /// <returns>可执行 PostgreSQL 批量 Update 命令。</returns>
    [Benchmark]
    public SqlWriteCommand RenderPostgreSqlBatchUpdate() =>
        _postgreSqlRenderer.Render(_postgreSqlUpdateContext);


    /// <summary>
    /// 创建使用 MySQL Provider 与独立映射服务的实体 Mutation Builder。
    /// </summary>
    /// <returns>可构建实体 Mutation 命令的 Builder。</returns>
    private static DefaultSqlEntityMutationCommandBuilder CreateBuilder() => new(MySqlSqlProvider.Instance,
        SqlBuilderServices.CreateDefault());

    /// <summary>
    /// Mutation 基准使用的映射实体。
    /// </summary>
    [Table("mutation_benchmark_entities")]
    private sealed class MutationBenchmarkEntity
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 可更新名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 可更新金额。
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// 乐观并发令牌。
        /// </summary>
        [ConcurrencyCheck]
        public int Version { get; set; }
    }
}

/// <summary>
/// PostgreSQL 结构化 UpdateFrom 性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlUpdateFromBenchmarks
{
    private ISqlBuilder _builder;

    /// <summary>
    /// 初始化可重复渲染的 UpdateFrom Builder。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _builder = new PostgreSqlBuilder()
            .Update(new SqlTableReference { Schema = "public", TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { Schema = "public", TableName = "sample_updates", Alias = "s" })
            .SetFrom("Name", "Name")
            .Set("Version", 2)
            .WhereFrom("Id", "Id");
    }

    /// <summary>
    /// 测量结构化 PostgreSQL UpdateFrom 的重复渲染。
    /// </summary>
    /// <returns>渲染后的 UpdateFrom SQL。</returns>
    [Benchmark]
    public string RenderPostgreSqlUpdateFrom() => _builder.ToSql();
}

/// <summary>
/// PostgreSQL 结构化 DeleteUsing 性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlDeleteUsingBenchmarks
{
    private ISqlBuilder _builder;

    /// <summary>
    /// 初始化可重复渲染的 DeleteUsing Builder。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _builder = new PostgreSqlBuilder()
            .DeleteFrom(new SqlTableReference { Schema = "public", TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { Schema = "public", TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id");
    }

    /// <summary>
    /// 测量结构化 PostgreSQL DeleteUsing 的重复渲染。
    /// </summary>
    /// <returns>渲染后的 DeleteUsing SQL。</returns>
    [Benchmark]
    public string RenderPostgreSqlDeleteUsing() => _builder.ToSql();
}

/// <summary>
/// PostgreSQL 结构化 Returning 性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlReturningBenchmarks
{
    private ISqlBuilder _builder;

    /// <summary>
    /// 初始化可重复渲染的 UpdateFrom Returning Builder。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _builder = new PostgreSqlBuilder();
        _builder.Update(new SqlTableReference { Schema = "public", TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { Schema = "public", TableName = "sample_updates", Alias = "s" })
            .SetFrom("Name", "Name")
            .WhereFrom("Id", "Id")
            .Returning("Id", "Name");
    }

    /// <summary>
    /// 测量 PostgreSQL UpdateFrom Returning 的重复渲染。
    /// </summary>
    /// <returns>渲染后的 Returning SQL。</returns>
    [Benchmark]
    public string RenderPostgreSqlReturning() => _builder.ToSql();
}

/// <summary>
/// SQL Server 结构化 Output 性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlServerOutputBenchmarks
{
    private ISqlBuilder _builder;

    /// <summary>
    /// 初始化可重复渲染的 Update Output Builder。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _builder = new SqlServerBuilder();
        _builder.Update(new SqlTableReference { Schema = "dbo", TableName = "samples" })
            .Set("Name", "Bing")
            .Where("Id", 1)
            .Returning("Id", "Name");
    }

    /// <summary>
    /// 测量 SQL Server Update Output 的重复渲染。
    /// </summary>
    /// <returns>渲染后的 Output SQL。</returns>
    [Benchmark]
    public string RenderSqlServerOutput() => _builder.ToSql();
}

/// <summary>
/// SQLite 结构化 Returning 性能基线。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqliteReturningBenchmarks
{
    private ISqlBuilder _builder;

    /// <summary>
    /// 初始化可重复渲染的 Update Returning Builder。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _builder = new SqliteBuilder();
        _builder.Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "Bing")
            .Where("Id", 1)
            .Returning("Id", "Name");
    }

    /// <summary>
    /// 测量 SQLite Update Returning 的重复渲染。
    /// </summary>
    /// <returns>渲染后的 Returning SQL。</returns>
    [Benchmark]
    public string RenderSqliteReturning() => _builder.ToSql();
}