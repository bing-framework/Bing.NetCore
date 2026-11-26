using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Bing.Data.Sql.Benchmarks;

namespace Bing.Data.Sql.Benchmarks.DryHost;

/// <summary>
/// 独立程序集中的 SQL 投影 DryJob 宿主，避免正式基准项目的多个 evidence 副本干扰 BDN 项目解析。
/// </summary>
[MemoryDiagnoser(displayGenColumns: true)]
[DryJob]
public class SqlProjectionDryHostBenchmarks
{
    /// <summary>
    /// 保存投影基准场景集。
    /// </summary>
    private SqlProjectionBenchmarks _scenarios;

    /// <summary>
    /// 获取或设置类型化来源数量。
    /// </summary>
    [Params(1, 5, 20)]
    public int SourceCount { get; set; }

    /// <summary>
    /// 初始化与正式场景相同的投影工作负载。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _scenarios = new SqlProjectionBenchmarks { SourceCount = SourceCount };
        _scenarios.Setup();
    }

    /// <summary>
    /// 验证全列排除场景可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string RenderSelectAllWithSmallExclusions() => _scenarios.RenderSelectAllWithSmallExclusions();

    /// <summary>
    /// 验证连续 Select 累计场景可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string RenderCumulativeProjection() => _scenarios.RenderCumulativeProjection();

    /// <summary>
    /// 验证重复投影去重场景可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string RenderDeduplicatedProjection() => _scenarios.RenderDeduplicatedProjection();

    /// <summary>
    /// 验证 ReplaceSelect 场景可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string RenderReplacedProjection() => _scenarios.RenderReplacedProjection();

    /// <summary>
    /// 验证克隆后独立投影场景可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string CloneAndChangeProjection() => _scenarios.CloneAndChangeProjection();

    /// <summary>
    /// 验证重复 SQL 渲染场景可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string RenderCachedSql() => _scenarios.RenderCachedSql();

    /// <summary>
    /// 验证无排除规则的全列投影场景可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string Additional_RenderSelectAllWithoutExclusions() => _scenarios.RenderSelectAllWithoutExclusions();

    /// <summary>
    /// 验证大量排除规则的全列投影场景可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string Additional_RenderSelectAllWithManyExclusions() => _scenarios.RenderSelectAllWithManyExclusions();

    /// <summary>
    /// 验证参数名命中来源别名的场景可执行。
    /// </summary>
    /// <returns>参数名匹配来源别名后渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string Additional_ResolveParameterAliasMatch() => _scenarios.ResolveParameterAliasMatch();

    /// <summary>
    /// 验证参数名未命中后的回退或歧义回滚场景可执行。
    /// </summary>
    /// <returns>单源回退成功或多源歧义未改变 SQL 时为 true，否则为 false。</returns>
    [Benchmark]
    public bool Additional_ValidateUnmatchedParameterBindingWithoutStateChange() =>
        _scenarios.ValidateUnmatchedParameterBindingWithoutStateChange();

    /// <summary>
    /// 验证预建表达式的连续同类型 Join 场景可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string Additional_RenderContinuousSameTypeJoinProjection() =>
        _scenarios.RenderContinuousSameTypeJoinProjection();

    /// <summary>
    /// 验证 Count/Sum 汇总投影场景可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string Additional_RenderAggregateProjection() => _scenarios.RenderAggregateProjection();

    /// <summary>
    /// 验证聚合上下文直接成员写法可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string Additional_RenderAggregateProjectionWithDirectMembers() =>
        _scenarios.RenderAggregateProjectionWithDirectMembers();

    /// <summary>
    /// 验证静态 SqlFn 聚合写法可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string Additional_RenderAggregateProjectionWithSqlFn() =>
        _scenarios.RenderAggregateProjectionWithSqlFn();

    /// <summary>
    /// 验证关闭自动别名后显式来源的克隆投影可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string Additional_CloneAndChangeProjectionWithAliasGenerationDisabled() =>
        _scenarios.CloneAndChangeProjectionWithAliasGenerationDisabled();

    /// <summary>
    /// 验证关闭自动别名后重复 SQL 渲染可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string Additional_RenderCachedSqlWithAliasGenerationDisabled() =>
        _scenarios.RenderCachedSqlWithAliasGenerationDisabled();

    /// <summary>
    /// 验证映射 profile 变化时克隆投影保持上下文的场景可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string Additional_CloneAndChangeProjectionAcrossMappingProfiles() =>
        _scenarios.CloneAndChangeProjectionAcrossMappingProfiles();

    /// <summary>
    /// 验证不同实体类型的来源按类型唯一绑定场景可执行。
    /// </summary>
    /// <returns>构建或渲染得到的完整 SQL 文本。</returns>
    [Benchmark]
    public string Additional_RenderHeterogeneousSourceProjection() =>
        _scenarios.RenderHeterogeneousSourceProjection();
}
