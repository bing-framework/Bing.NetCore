using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Mutations;

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
    }

    /// <summary>
    /// 测量已缓存实体映射计划的单条 Insert 命令构建。
    /// </summary>
    /// <returns>可执行 Insert 命令快照。</returns>
    [Benchmark(Baseline = true)]
    public SqlMutationCommand BuildInsertPlanCacheHit() => _cacheHitBuilder.Insert(_entities[0]);

    /// <summary>
    /// 测量单条 Update 命令构建及完整参数快照导出。
    /// </summary>
    /// <returns>可执行 Update 命令快照。</returns>
    [Benchmark]
    public SqlMutationCommand BuildUpdateCommand()
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
    public SqlMutationCommand BuildCombinedInsertCommand() => CreateBuilder().InsertCombined(_entities);

    /// <summary>
    /// 测量无额外 SQL 长度限制时的批次规划。
    /// </summary>
    /// <returns>按参数上限分片的批处理计划。</returns>
    [Benchmark]
    public SqlMutationBatchPlan PlanBatchesByParameterLimit() => new SqlMutationBatchPlanner().Plan(
        new SqlMutationBatchPlanContext(EntityCount, parametersPerEntity: 4, maxParameterCount: 200));

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