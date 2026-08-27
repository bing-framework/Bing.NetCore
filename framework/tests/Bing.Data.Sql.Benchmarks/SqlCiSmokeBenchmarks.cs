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
    private ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// 初始化轻量 SQL smoke 执行器。
    /// </summary>
    [GlobalSetup]
    public void Setup() => _executor = new SqlServerBuilder().CreateQueryExecutorForBenchmark();

    /// <summary>
    /// 验证 CI 可完成最小 Raw Fluent SQL 构建。
    /// </summary>
    [Benchmark]
    public string BuildRawQuery() => SqlQueryRuntimeFactory.CreateQuery(_executor, new SqlServerBuilder())
        .Select("Id")
        .From("samples")
        .Where("Id", 1)
        .ToSql();
}

internal static class SqlCiSmokeBenchmarkExtensions
{
    public static ISqlQueryPlanExecutor CreateQueryExecutorForBenchmark(this ISqlBuilder builder) =>
        DispatchProxy.Create<ISqlQueryPlanExecutor, NoOpExecutor>();

    private class NoOpExecutor : DispatchProxy
    {
        protected override object Invoke(System.Reflection.MethodInfo targetMethod, object[] args) =>
            targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }
}