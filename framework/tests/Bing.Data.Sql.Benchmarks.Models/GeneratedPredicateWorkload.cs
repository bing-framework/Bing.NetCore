using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Benchmarks.Models;

/// <summary>缓存查询条件的基准实体。</summary>
[Table("PredicateBenchmarkRows")]
public sealed class PredicateBenchmarkRow
{
    /// <summary>实体编号。</summary>
    public int Id { get; set; }
}

/// <summary>用于对比自动投影排除列的基准实体。</summary>
[Table("ExcludedPredicateBenchmarkRows")]
public sealed class ExcludedPredicateBenchmarkRow
{
    /// <summary>实体编号。</summary>
    public int Id { get; set; }
    /// <summary>不参与投影的列。</summary>
    public string Secret { get; set; }
}

/// <summary>用于比较生成查询调用的基准负载。</summary>
public static class GeneratedPredicateWorkload
{
    private static readonly Expression<Func<PredicateBenchmarkRow, bool>> CachedSingle = row => row.Id >= 7;
    private static readonly Expression<Func<PredicateBenchmarkRow, int, bool>> CachedWithValue =
        (row, minimum) => row.Id >= minimum;
    private static readonly Expression<Func<PredicateBenchmarkRow, object>> ProjectionShape =
        row => new { Total = SqlFn.Count(row.Id) };
    private static readonly Expression<Func<PredicateBenchmarkRow, object>> GroupColumn = row => row.Id;
    private static readonly Expression<Func<ExcludedPredicateBenchmarkRow, int, bool>> ExcludedMinimum =
        (row, minimum) => row.Id >= minimum;
    private static readonly Expression<Func<ExcludedPredicateBenchmarkRow, object>> ExcludedSecret = row => row.Secret;

    /// <summary>复用生成桥接逐次构建排除列查询。</summary>
    public static string ExcludedBridge(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum)
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<ExcludedPredicateBenchmarkRow>("r");
        return query.WhereIf<ExcludedPredicateBenchmarkRow, int>(enabled, minimum, ExcludedMinimum, "r")
            .SelectAll().ExcludeSelect<ExcludedPredicateBenchmarkRow>(ExcludedSecret, "r").ToSql();
    }

    /// <summary>通过完整调用链复用排除列查询模板。</summary>
    public static string ExcludedTemplate(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<ExcludedPredicateBenchmarkRow>("r")
            .WhereIf<ExcludedPredicateBenchmarkRow, int>(enabled, minimum, ExcludedMinimum, "r")
            .SelectAll().ExcludeSelect<ExcludedPredicateBenchmarkRow>(ExcludedSecret, "r").ToSql();

    /// <summary>使用内联条件构造单参数查询模板。</summary>
    public static string InlineTemplate(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .WhereIf<PredicateBenchmarkRow>(enabled, row => row.Id >= 7, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r").ToSql();

    /// <summary>使用缓存条件构造单参数查询模板。</summary>
    public static string CachedTemplate(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .WhereIf<PredicateBenchmarkRow>(enabled, CachedSingle, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r").ToSql();

    /// <summary>使用内联条件与当前值生成查询。</summary>
    public static string InlineValue(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .WhereIf<PredicateBenchmarkRow, int>(enabled, minimum, (row, value) => row.Id >= value, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r").ToSql();

    /// <summary>使用缓存条件与当前值生成查询。</summary>
    public static string CachedValue(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .WhereIf<PredicateBenchmarkRow, int>(enabled, minimum, CachedWithValue, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r").ToSql();

    /// <summary>以逐调用生成桥接构造必需 Where 条件。</summary>
    public static string RequiredWhereBridge(ISqlQueryPlanExecutor executor, ISqlBuilder builder, int minimum)
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r");
        return query.Where<PredicateBenchmarkRow, int>(minimum, CachedWithValue, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r").ToSql();
    }

    /// <summary>通过完整调用链构造同一必需 Where 条件模板。</summary>
    public static string RequiredWhereTemplate(ISqlQueryPlanExecutor executor, ISqlBuilder builder, int minimum) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .Where<PredicateBenchmarkRow, int>(minimum, CachedWithValue, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r").ToSql();

    /// <summary>执行逐调用生成桥接的必需 Where 查询。</summary>
    public static List<PredicateBenchmarkRow> RequiredWhereBridgeToList(
        ISqlQueryPlanExecutor executor, ISqlBuilder builder, int minimum)
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r");
        return query.Where<PredicateBenchmarkRow, int>(minimum, CachedWithValue, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r").ToList<PredicateBenchmarkRow>();
    }

    /// <summary>执行完整查询模板中的必需 Where 查询。</summary>
    public static List<PredicateBenchmarkRow> RequiredWhereTemplateToList(
        ISqlQueryPlanExecutor executor, ISqlBuilder builder, int minimum) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .Where<PredicateBenchmarkRow, int>(minimum, CachedWithValue, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r").ToList<PredicateBenchmarkRow>();

    /// <summary>以内联聚合投影构造查询；谓词字段与配对场景共用。</summary>
    public static string InlineProjection(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .WhereIf<PredicateBenchmarkRow, int>(enabled, minimum, CachedWithValue, "r")
            .Select<PredicateBenchmarkRow>(row => new { Total = SqlFn.Count(row.Id) }, "r").ToSql();

    /// <summary>复用静态只读聚合投影构造同形查询。</summary>
    public static string CachedProjection(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .WhereIf<PredicateBenchmarkRow, int>(enabled, minimum, CachedWithValue, "r")
            .Select<PredicateBenchmarkRow>(ProjectionShape, "r").ToSql();

    /// <summary>以内联分组和排序列构造查询模板。</summary>
    public static string InlineGrouped(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .WhereIf<PredicateBenchmarkRow, int>(enabled, minimum, CachedWithValue, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r")
            .GroupBy<PredicateBenchmarkRow>(row => row.Id, "r")
            .OrderBy<PredicateBenchmarkRow>(row => row.Id, false, "r").ToSql();

    /// <summary>使用静态只读分组和排序列构造相同查询模板。</summary>
    public static string CachedGrouped(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .WhereIf<PredicateBenchmarkRow, int>(enabled, minimum, CachedWithValue, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r")
            .GroupBy<PredicateBenchmarkRow>(GroupColumn, "r")
            .OrderBy<PredicateBenchmarkRow>(GroupColumn, false, "r").ToSql();

    /// <summary>执行内联条件的模板查询，用于核对执行计划。</summary>
    public static List<PredicateBenchmarkRow> InlineTemplateToList(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .WhereIf<PredicateBenchmarkRow>(enabled, row => row.Id >= 7, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r").ToList<PredicateBenchmarkRow>();

    /// <summary>执行缓存条件的模板查询，用于核对执行计划。</summary>
    public static List<PredicateBenchmarkRow> CachedTemplateToList(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<PredicateBenchmarkRow>("r")
            .WhereIf<PredicateBenchmarkRow>(enabled, CachedSingle, "r")
            .Select<PredicateBenchmarkRow>(row => row.Id, "r").ToList<PredicateBenchmarkRow>();
}
