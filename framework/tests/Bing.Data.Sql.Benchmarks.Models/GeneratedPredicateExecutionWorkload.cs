using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Benchmarks.Models;

/// <summary>通过真实终结方法验证显式值查询的执行计划。</summary>
public static class GeneratedPredicateExecutionWorkload
{
    private static readonly Expression<Func<PredicateBenchmarkRow, int, bool>> CachedWithValue =
        (row, minimum) => row.Id >= minimum;

    /// <summary>执行内联条件。</summary>
    public static List<PredicateBenchmarkRow> InlineValue(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .WhereIf<PredicateBenchmarkRow, int>(enabled, minimum, (row, value) => row.Id >= value, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r").ToList<PredicateBenchmarkRow>();

    /// <summary>执行缓存条件。</summary>
    public static List<PredicateBenchmarkRow> CachedValue(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .WhereIf<PredicateBenchmarkRow, int>(enabled, minimum, CachedWithValue, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r").ToList<PredicateBenchmarkRow>();
}
