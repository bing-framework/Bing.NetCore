using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using System.Reflection;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// CI 轻量 SQL smoke 基准。该类型只声明 DryJob，不继承完整 FormalHost 参数矩阵。
/// </summary>
[MemoryDiagnoser]
[DryJob]
public class SqlCiSmokeBenchmarks
{
    /// <summary>
    /// 执行轻量 SQL 构建基准的计划执行器。
    /// </summary>
    private ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// 初始化轻量 SQL smoke 执行器。
    /// </summary>
    [GlobalSetup]
    public void Setup() => _executor = new SqlServerBuilder().CreateQueryExecutorForBenchmark();

    /// <summary>
    /// 验证 CI 可完成最小 Raw Fluent SQL 构建。
    /// </summary>
    /// <returns>生成的 SQL 文本。</returns>
    [Benchmark]
    public string BuildRawQuery() => SqlQueryRuntimeFactory.CreateQuery(_executor, new SqlServerBuilder())
        .Select("Id")
        .From("samples")
        .Where("Id", 1)
        .ToSql();
}

/// <summary>
/// 提供 CI smoke 基准所需的查询执行器扩展。
/// </summary>
internal static class SqlCiSmokeBenchmarkExtensions
{
    /// <summary>
    /// 创建无操作的 SQL 查询计划执行器。
    /// </summary>
    /// <param name="builder">调用扩展方法时使用的 SQL Builder。</param>
    /// <returns>不执行数据库操作的查询计划执行器。</returns>
    public static ISqlQueryPlanExecutor CreateQueryExecutorForBenchmark(this ISqlBuilder builder) =>
        DispatchProxy.Create<ISqlQueryPlanExecutor, NoOpExecutor>();

    /// <summary>
    /// 为基准查询提供默认返回值的代理执行器。
    /// </summary>
    private class NoOpExecutor : DispatchProxy
    {
        /// <inheritdoc />
        protected override object Invoke(System.Reflection.MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }
}
