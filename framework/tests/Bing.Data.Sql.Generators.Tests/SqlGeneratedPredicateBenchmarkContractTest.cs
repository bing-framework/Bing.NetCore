using Bing.Data.Sql;
using Bing.Data.Sql.Benchmarks.Models;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using System.Data.Common;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed class SqlGeneratedPredicateBenchmarkContractTest
{
    private static readonly SqlBuilderServices Services = new();

    /// <summary>纯 Where 的逐调用桥接和完整模板应保持 SQL/参数语义一致，并区分缓存路径。</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(11)]
    public void RequiredWhere_BenchmarkPair_ShouldPreserveSqlParametersAndTemplateReuse(int minimum)
    {
        var expectedSql = "Select `r`.`Id` \r\nFrom `PredicateBenchmarkRows` As `r` \r\nWhere `r`.`Id`>=@_p_0";
        var executor = new Mock<ISqlQueryPlanExecutor>();
        SqlQueryPlan plan = null;
        executor.As<ISqlGeneratedPlanExecutor>()
            .Setup(x => x.ToGeneratedList(It.IsAny<SqlQueryPlan>(),
                It.IsAny<Func<DbDataReader, PredicateBenchmarkRow>>(), It.IsAny<int?>()))
            .Callback<SqlQueryPlan, Func<DbDataReader, PredicateBenchmarkRow>, int?>((value, _, _) => plan = value)
            .Returns(new List<PredicateBenchmarkRow>());

        var bridge = GeneratedPredicateWorkload.RequiredWhereBridge(
            executor.Object, new SqliteBuilder(Services), minimum);
        var template = GeneratedPredicateWorkload.RequiredWhereTemplate(
            executor.Object, new SqliteBuilder(Services), minimum);
        GeneratedPredicateWorkload.RequiredWhereBridgeToList(executor.Object, new SqliteBuilder(Services), minimum);
        var bridgePlan = plan;
        GeneratedPredicateWorkload.RequiredWhereTemplateToList(executor.Object, new SqliteBuilder(Services), minimum);
        var templatePlan = plan;
        var warmedTemplate = GeneratedPredicateWorkload.RequiredWhereTemplate(
            executor.Object, new SqliteBuilder(Services), minimum + 1);

        Assert.Equal(expectedSql, bridge);
        Assert.Equal(expectedSql, template);
        Assert.Equal(expectedSql, bridgePlan.CommandText);
        Assert.Equal(expectedSql, templatePlan.CommandText);
        Assert.Equal(minimum, Assert.Single(Assert.IsType<SqlParam[]>(bridgePlan.Parameters)).Value);
        Assert.Equal(minimum, Assert.Single(Assert.IsType<SqlParam[]>(templatePlan.Parameters)).Value);
        Assert.NotSame(bridge, GeneratedPredicateWorkload.RequiredWhereBridge(
            executor.Object, new SqliteBuilder(Services), minimum + 1));
        Assert.Same(template, warmedTemplate);
    }

    /// <summary>排除列配对基准应比较相同 SQL，并确认模板路径确实复用形状。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExcludedColumns_BenchmarkPair_ShouldRenderSameSqlAndReuseTemplate(bool enabled)
    {
        var executor = new Mock<ISqlQueryPlanExecutor>().Object;
        string Bridge() => GeneratedPredicateWorkload.ExcludedBridge(executor, new SqliteBuilder(Services), enabled, 11);
        string Template() => GeneratedPredicateWorkload.ExcludedTemplate(executor, new SqliteBuilder(Services), enabled, 11);
        var first = Template();
        var second = Template();
        var expected = "Select `r`.`Id` \r\nFrom `ExcludedPredicateBenchmarkRows` As `r`" +
            (enabled ? " \r\nWhere `r`.`Id`>=@_p_0" : "");
        Assert.Equal(expected, Bridge());
        Assert.Equal(expected, first);
        Assert.Same(first, second);
    }

    [Theory]
    [InlineData(false, "Select `r`.`Id` \r\nFrom `PredicateBenchmarkRows` As `r`")]
    [InlineData(true, "Select `r`.`Id` \r\nFrom `PredicateBenchmarkRows` As `r` \r\nWhere `r`.`Id`>=@_p_0")]
    public void TemplatePredicates_ShouldRenderExpectedSql(
        bool enabled, string expectedSql)
    {
        AssertTemplate(GeneratedPredicateWorkload.InlineTemplate, enabled, expectedSql);
        AssertTemplate(GeneratedPredicateWorkload.CachedTemplate, enabled, expectedSql);
    }

    [Theory]
    [InlineData(false, 3, "Select `r`.`Id` \r\nFrom `PredicateBenchmarkRows` As `r`", false)]
    [InlineData(true, 3, "Select `r`.`Id` \r\nFrom `PredicateBenchmarkRows` As `r` \r\nWhere `r`.`Id`>=@_p_0", true)]
    [InlineData(false, 11, "Select `r`.`Id` \r\nFrom `PredicateBenchmarkRows` As `r`", false)]
    [InlineData(true, 11, "Select `r`.`Id` \r\nFrom `PredicateBenchmarkRows` As `r` \r\nWhere `r`.`Id`>=@_p_0", true)]
    public void ExplicitValuePredicates_ShouldBindCurrentValue(
        bool enabled, int minimum, string expectedSql, bool expectsParameter)
    {
        AssertValue(GeneratedPredicateWorkload.InlineValue, GeneratedPredicateExecutionWorkload.InlineValue,
            enabled, minimum, expectedSql, expectsParameter);
        AssertValue(GeneratedPredicateWorkload.CachedValue, GeneratedPredicateExecutionWorkload.CachedValue,
            enabled, minimum, expectedSql, expectsParameter);
    }

    [Fact]
    public void TemplatePredicates_WhenShapeIsWarm_ShouldReuseSqlStringInstance()
    {
        var inlineFirst = RenderTemplate(GeneratedPredicateWorkload.InlineTemplate, true);
        var inlineSecond = RenderTemplate(GeneratedPredicateWorkload.InlineTemplate, true);
        var cachedFirst = RenderTemplate(GeneratedPredicateWorkload.CachedTemplate, true);
        var cachedSecond = RenderTemplate(GeneratedPredicateWorkload.CachedTemplate, true);

        Assert.Same(inlineFirst.Sql, inlineSecond.Sql);
        Assert.Same(cachedFirst.Sql, cachedSecond.Sql);
        Assert.Equal(inlineFirst.Sql, cachedFirst.Sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupedColumns_BenchmarkPair_ShouldRenderSameSqlAndReuseShape(bool enabled)
    {
        var executor = new Mock<ISqlQueryPlanExecutor>().Object;
        string Run(Func<ISqlQueryPlanExecutor, ISqlBuilder, bool, int, string> workload, int value) =>
            workload(executor, new SqliteBuilder(Services), enabled, value);
        var inline = Run(GeneratedPredicateWorkload.InlineGrouped, 3);
        var cached = Run(GeneratedPredicateWorkload.CachedGrouped, 3);
        var expected = "Select `r`.`Id` \r\nFrom `PredicateBenchmarkRows` As `r`" +
            (enabled ? " \r\nWhere `r`.`Id`>=@_p_0" : "") +
            " \r\nGroup By `r`.`Id` \r\nOrder By `r`.`Id`";

        Assert.Equal(expected, inline);
        Assert.Equal(expected, cached);
        Assert.Same(inline, Run(GeneratedPredicateWorkload.InlineGrouped, 11));
        Assert.Same(cached, Run(GeneratedPredicateWorkload.CachedGrouped, 11));
    }

    [Theory]
    [InlineData(false, "Select `r`.`Id` \r\nFrom `PredicateBenchmarkRows` As `r`")]
    [InlineData(true, "Select `r`.`Id` \r\nFrom `PredicateBenchmarkRows` As `r` \r\nWhere `r`.`Id`>=@_p_0")]
    public void TemplatePredicates_WhenExecuted_ShouldBindExpectedParameters(bool enabled, string expectedSql)
    {
        AssertTemplatePlan(GeneratedPredicateWorkload.InlineTemplateToList, enabled, expectedSql);
        AssertTemplatePlan(GeneratedPredicateWorkload.CachedTemplateToList, enabled, expectedSql);
    }

    private static void AssertTemplate(
        Func<ISqlQueryPlanExecutor, ISqlBuilder, bool, string> workload,
        bool enabled, string expectedSql)
    {
        var result = RenderTemplate(workload, enabled);

        Assert.Equal(expectedSql, result.Sql);
    }

    private static void AssertTemplatePlan(
        Func<ISqlQueryPlanExecutor, ISqlBuilder, bool, List<PredicateBenchmarkRow>> workload,
        bool enabled, string expectedSql)
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        SqlQueryPlan plan = null;
        executor.As<ISqlGeneratedPlanExecutor>()
            .Setup(x => x.ToGeneratedList(It.IsAny<SqlQueryPlan>(),
                It.IsAny<Func<DbDataReader, PredicateBenchmarkRow>>(), It.IsAny<int?>()))
            .Callback<SqlQueryPlan, Func<DbDataReader, PredicateBenchmarkRow>, int?>((value, _, _) => plan = value)
            .Returns(new List<PredicateBenchmarkRow>());

        workload(executor.Object, new SqliteBuilder(Services), enabled);

        Assert.NotNull(plan);
        Assert.Equal(expectedSql, plan.CommandText);
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(enabled ? 1 : 0, parameters.Length);
        if (enabled)
        {
            var parameter = Assert.Single(parameters);
            Assert.Equal("@_p_0", parameter.Name);
            Assert.Equal(7, parameter.Value);
        }
    }

    private static void AssertValue(
        Func<ISqlQueryPlanExecutor, ISqlBuilder, bool, int, string> workload,
        Func<ISqlQueryPlanExecutor, ISqlBuilder, bool, int, List<PredicateBenchmarkRow>> execute,
        bool enabled, int minimum, string expectedSql, bool expectsParameter)
    {
        var executor = new Mock<ISqlQueryPlanExecutor>();
        SqlQueryPlan plan = null;
        executor.As<ISqlGeneratedPlanExecutor>()
            .Setup(x => x.ToGeneratedList(It.IsAny<SqlQueryPlan>(),
                It.IsAny<Func<DbDataReader, PredicateBenchmarkRow>>(), It.IsAny<int?>()))
            .Callback<SqlQueryPlan, Func<DbDataReader, PredicateBenchmarkRow>, int?>((value, _, _) => plan = value)
            .Returns(new List<PredicateBenchmarkRow>());
        var sql = workload(executor.Object, new SqliteBuilder(Services), enabled, minimum);
        execute(executor.Object, new SqliteBuilder(Services), enabled, minimum);

        Assert.Equal(expectedSql, sql);
        Assert.NotNull(plan);
        Assert.Equal(expectedSql, plan.CommandText);
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(expectsParameter ? 1 : 0, parameters.Length);
        if (expectsParameter)
        {
            var parameter = Assert.Single(parameters);
            Assert.Equal("@_p_0", parameter.Name);
            Assert.Equal(minimum, parameter.Value);
        }
    }

    private static (string Sql, SqliteBuilder Builder) RenderTemplate(
        Func<ISqlQueryPlanExecutor, ISqlBuilder, bool, string> workload, bool enabled)
    {
        var builder = new SqliteBuilder(Services);
        var sql = workload(new Mock<ISqlQueryPlanExecutor>().Object, builder, enabled);
        return (sql, builder);
    }
}
