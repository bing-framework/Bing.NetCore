using System.Collections.Concurrent;
using System.Reflection;
using System.Data.Common;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    private const string ExplicitValueTemplateSource = """
        using System.Collections.Generic;
        using Bing.Data.Sql;
        using System.ComponentModel.DataAnnotations.Schema;

        [Table("ExplicitValueRows")]
        public sealed class ExplicitValueRow
        {
            public int Id { get; set; }
            public long LongId { get; set; }
            public int ParentId { get; set; }
        }

        public static class ExplicitValueTemplateConsumer
        {
            public static List<ExplicitValueRow> Inline(
                ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
                SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder)
                    .From<ExplicitValueRow>("r")
                    .WhereIf<ExplicitValueRow, int>(enabled, minimum,
                        (row, value) => row.Id >= value, "r")
                    .Select<ExplicitValueRow>(row => row.Id, "r")
                    .ToList<ExplicitValueRow>();

            public static List<ExplicitValueRow> Cached(
                ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
                SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder)
                    .From<ExplicitValueRow>("r")
                    .WhereIf<ExplicitValueRow, int>(enabled, minimum,
                        SharedExplicitValuePredicate.Minimum, "r")
                    .Select<ExplicitValueRow>(row => row.Id, "r")
                    .ToList<ExplicitValueRow>();

            public static List<ExplicitValueRow> PromotedValue(
                ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
                SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder)
                    .From<ExplicitValueRow>("r")
                    .WhereIf<ExplicitValueRow, int>(enabled, minimum,
                        (row, value) => row.LongId >= value, "r")
                    .Select<ExplicitValueRow>(row => row.Id, "r")
                    .ToList<ExplicitValueRow>();
        }
        """;

    private const string SharedExplicitValuePredicateSource = """
        using System;
        using System.Linq.Expressions;

        public static class SharedExplicitValuePredicate
        {
            public static readonly Expression<Func<ExplicitValueRow, int, bool>> Minimum =
                (row, value) => row.Id >= value;
        }
        """;

    private const string StrictAotProperty = "build_property.BingSqlAotStrict";
    private static readonly SqlBuilderServices ExplicitValueTemplateServices = new();
    private static readonly Lazy<GeneratedExplicitValueFixture> ExplicitValueTemplateFixture = new(() =>
    {
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { [StrictAotProperty] = "true" });
        var result = RunGenerator(ExplicitValueTemplateSource,
            analyzerConfigOptionsProvider: options,
            additionalSource: SharedExplicitValuePredicateSource);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        return new GeneratedExplicitValueFixture(LoadGeneratedAssembly(result), result.GeneratedSource);
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>内联和静态只读显式值 WhereIf 查询应将完整链接入查询模板。</summary>
    [Fact]
    public void Generate_WhenExplicitValueWhereIfEndsAtToList_ShouldEmitInlineAndCachedTemplates()
    {
        var generatedSource = ExplicitValueTemplateFixture.Value.GeneratedSource;

        Assert.Contains("SqlGeneratedQueryTemplate.Operation(", generatedSource, StringComparison.Ordinal);
        Assert.Equal(4, CountOccurrences(generatedSource, "SqlGeneratedQueryTemplate.Apply(source, Template_"));
        Assert.Contains(", (long)@value)", generatedSource, StringComparison.Ordinal);
    }

    /// <summary>六种比较和显式值在左侧时均生成正确的模板、完整 SQL 及当前参数。</summary>
    [Fact]
    public void ExplicitValueWhereIf_WhenCompared_ShouldPreserveOperatorsAndParameterDirection()
    {
        var comparisons = new[]
        {
            (Name: "Equal", Expression: "row.Id == value", Sql: "="),
            (Name: "NotEqual", Expression: "row.Id != value", Sql: "<>"),
            (Name: "Greater", Expression: "row.Id > value", Sql: ">"),
            (Name: "GreaterEqual", Expression: "row.Id >= value", Sql: ">="),
            (Name: "Less", Expression: "row.Id < value", Sql: "<"),
            (Name: "LessEqual", Expression: "row.Id <= value", Sql: "<="),
            (Name: "ValueFirst", Expression: "value < row.Id", Sql: ">")
        };
        var methods = string.Join(Environment.NewLine, comparisons.Select(comparison => $$"""
            public static List<ComparisonRow> {{comparison.Name}}(ISqlQueryPlanExecutor executor, ISqlBuilder builder, int minimum) =>
                SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<ComparisonRow>("r")
                    .WhereIf<ComparisonRow, int>(true, minimum, (row, value) => {{comparison.Expression}}, "r")
                    .Select<ComparisonRow>(row => row.Id, "r").ToList<ComparisonRow>();
            """));
        var source = $$"""
            using System.Collections.Generic;
            using Bing.Data.Sql;
            public sealed class ComparisonRow { public int Id { get; set; } }
            public static class ComparisonConsumer
            {
                {{methods}}
                public static List<ComparisonRow> Required(ISqlQueryPlanExecutor executor, ISqlBuilder builder, int minimum) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<ComparisonRow>("r")
                        .Where<ComparisonRow, int>(minimum, (row, value) => row.Id >= value, "r")
                        .WhereIf<ComparisonRow, int>(false, minimum, (row, value) => row.Id < value, "r")
                        .Select<ComparisonRow>(row => row.Id, "r").ToList<ComparisonRow>();
                public static List<ComparisonRow> OnlyRequired(ISqlQueryPlanExecutor executor, ISqlBuilder builder, int minimum) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<ComparisonRow>("r")
                        .Where<ComparisonRow, int>(minimum, (row, value) => row.Id >= value, "r")
                        .Select<ComparisonRow>(row => row.Id, "r").ToList<ComparisonRow>();
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { [StrictAotProperty] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Equal(comparisons.Length * 2 + 5,
            CountOccurrences(result.GeneratedSource, "SqlGeneratedQueryTemplate.Apply(source, Template_"));
        var consumer = LoadGeneratedAssembly(result).GetType("ComparisonConsumer")!;

        SqlQueryPlan Execute(MethodInfo method, int value)
        {
            var executor = DispatchProxy.Create<ISqlQueryPlanExecutor, CapturingPlanExecutor>();
            method.Invoke(null, new object[] { executor, new SqliteBuilder(ExplicitValueTemplateServices), value });
            return Assert.IsType<SqlQueryPlan>(((CapturingPlanExecutor)executor).Plan);
        }

        foreach (var comparison in comparisons.Append((Name: "Required", Expression: "", Sql: ">="))
                     .Append((Name: "OnlyRequired", Expression: "", Sql: ">=")))
        {
            var method = consumer.GetMethod(comparison.Name)!;
            var first = Execute(method, 3);
            var second = Execute(method, 11);
            Assert.Equal($"Select `r`.`Id` \r\nFrom `ComparisonRow` As `r` \r\nWhere `r`.`Id`{comparison.Sql}@_p_0", first.CommandText);
            Assert.Same(first.CommandText, second.CommandText);
            Assert.Equal(3, Assert.Single(Assert.IsType<SqlParam[]>(first.Parameters)).Value);
            Assert.Equal(11, Assert.Single(Assert.IsType<SqlParam[]>(second.Parameters)).Value);
        }
    }

    /// <summary>显式值、条件开关和数值提升应反映到实际执行计划的 SQL 与参数中。</summary>
    [Theory]
    [InlineData("Inline", false, 3, "Select `r`.`Id` \r\nFrom `ExplicitValueRows` As `r`", false, false)]
    [InlineData("Inline", true, 3, "Select `r`.`Id` \r\nFrom `ExplicitValueRows` As `r` \r\nWhere `r`.`Id`>=@_p_0", true, false)]
    [InlineData("Inline", true, 11, "Select `r`.`Id` \r\nFrom `ExplicitValueRows` As `r` \r\nWhere `r`.`Id`>=@_p_0", true, false)]
    [InlineData("Cached", false, 3, "Select `r`.`Id` \r\nFrom `ExplicitValueRows` As `r`", false, false)]
    [InlineData("Cached", true, 3, "Select `r`.`Id` \r\nFrom `ExplicitValueRows` As `r` \r\nWhere `r`.`Id`>=@_p_0", true, false)]
    [InlineData("Cached", true, 11, "Select `r`.`Id` \r\nFrom `ExplicitValueRows` As `r` \r\nWhere `r`.`Id`>=@_p_0", true, false)]
    [InlineData("PromotedValue", true, 11, "Select `r`.`Id` \r\nFrom `ExplicitValueRows` As `r` \r\nWhere `r`.`LongId`>=@_p_0", true, true)]
    public void ExplicitValueWhereIf_WhenExecuted_ShouldBindCurrentPlanParameters(
        string methodName, bool enabled, int minimum, string expectedSql, bool expectsParameter,
        bool expectsLongParameter)
    {
        var plan = ExecuteExplicitValueQuery(methodName, enabled, minimum);

        Assert.Equal(expectedSql, plan.CommandText);
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(expectsParameter ? 1 : 0, parameters.Length);
        if (expectsParameter)
        {
            var parameter = Assert.Single(parameters);
            Assert.Equal("@_p_0", parameter.Name);
            Assert.Equal(expectsLongParameter ? (object)(long)minimum : minimum, parameter.Value);
        }
    }

    /// <summary>同一显式值查询形状升温后复用 SQL 字符串，同时各请求仍绑定自己的值。</summary>
    [Theory]
    [InlineData("Inline")]
    [InlineData("Cached")]
    public void ExplicitValueWhereIf_WhenShapeIsWarm_ShouldReuseSqlAndKeepValuesPerQuery(string methodName)
    {
        var first = ExecuteExplicitValueQuery(methodName, true, 3);
        var second = ExecuteExplicitValueQuery(methodName, true, 11);

        Assert.Same(first.CommandText, second.CommandText);
        Assert.Equal(3L, Convert.ToInt64(Assert.Single(Assert.IsType<SqlParam[]>(first.Parameters)).Value));
        Assert.Equal(11L, Convert.ToInt64(Assert.Single(Assert.IsType<SqlParam[]>(second.Parameters)).Value));
    }

    /// <summary>并发执行相同缓存模板时，各查询计划不能串用显式值。</summary>
    [Fact]
    public void CachedExplicitValueWhereIf_WhenExecutedConcurrently_ShouldKeepParametersIsolated()
    {
        var values = Enumerable.Range(1, 32).ToArray();
        var plans = new ConcurrentDictionary<int, SqlQueryPlan>();

        Parallel.ForEach(values, value =>
            plans[value] = ExecuteExplicitValueQuery("Cached", true, value));

        foreach (var value in values)
        {
            var parameters = Assert.IsType<SqlParam[]>(plans[value].Parameters);
            Assert.Equal(value, Assert.Single(parameters).Value);
        }
    }

    /// <summary>空值与非空值具有不同 SQL 形状，不得复用错误的参数布局。</summary>
    [Theory]
    [InlineData("Equal", null, "Select `r`.`Name` \r\nFrom `NullableRows` As `r` \r\nWhere `r`.`Name` Is Null", false)]
    [InlineData("Equal", "alice", "Select `r`.`Name` \r\nFrom `NullableRows` As `r` \r\nWhere `r`.`Name`=@_p_0", true)]
    [InlineData("NotEqual", null, "Select `r`.`Name` \r\nFrom `NullableRows` As `r` \r\nWhere `r`.`Name` Is Not Null", false)]
    [InlineData("NotEqual", "alice", "Select `r`.`Name` \r\nFrom `NullableRows` As `r` \r\nWhere `r`.`Name`<>@_p_0", true)]
    public void ExplicitValueWhereIf_WhenValueIsNull_ShouldUseTheMatchingShape(
        string methodName, string value, string expectedSql, bool hasParameter)
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;
            using System.ComponentModel.DataAnnotations.Schema;
            [Table("NullableRows")]
            public sealed class NullableRow { public string Name { get; set; } }
            public static class NullableConsumer
            {
                public static List<NullableRow> Equal(ISqlQueryPlanExecutor executor, ISqlBuilder builder, string name) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<NullableRow>("r")
                        .WhereIf<NullableRow, string>(true, name, (row, current) => row.Name == current, "r")
                        .Select<NullableRow>(row => row.Name, "r").ToList<NullableRow>();
                public static List<NullableRow> NotEqual(ISqlQueryPlanExecutor executor, ISqlBuilder builder, string name) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<NullableRow>("r")
                        .WhereIf<NullableRow, string>(true, name, (row, current) => row.Name != current, "r")
                        .Select<NullableRow>(row => row.Name, "r").ToList<NullableRow>();
            }
            """;
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.Contains("SqlGeneratedQueryTemplate.Apply(source, Template_", result.GeneratedSource, StringComparison.Ordinal);
        var method = LoadGeneratedAssembly(result).GetType("NullableConsumer")!.GetMethod(methodName)!;
        SqlQueryPlan Execute(string current)
        {
            var executor = DispatchProxy.Create<ISqlQueryPlanExecutor, CapturingPlanExecutor>();
            method.Invoke(null, new object[] { executor, new SqliteBuilder(ExplicitValueTemplateServices), current });
            return Assert.IsType<SqlQueryPlan>(((CapturingPlanExecutor)executor).Plan);
        }
        var first = Execute(value);
        var other = Execute(value == null ? "other" : null);
        var last = Execute(value);

        Assert.Equal(expectedSql, first.CommandText);
        Assert.Same(first.CommandText, last.CommandText);
        Assert.NotEqual(first.CommandText, other.CommandText);
        var parameters = Assert.IsType<SqlParam[]>(last.Parameters);
        Assert.Equal(hasParameter ? 1 : 0, parameters.Length);
        if (hasParameter) Assert.Equal(value, Assert.Single(parameters).Value);
    }

    /// <summary>不同别名使用不同 SQL 形状，重复别名仍复用模板并绑定当前值。</summary>
    [Fact]
    public void ExplicitValueWhereIf_WhenAliasChanges_ShouldKeepShapesAndParametersIndependent()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;
            public sealed class AliasRow { public int Id { get; set; } }
            public static class AliasConsumer
            {
                public static List<AliasRow> Load(ISqlQueryPlanExecutor executor, ISqlBuilder builder, string alias, int minimum) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<AliasRow>(alias)
                        .WhereIf<AliasRow, int>(true, minimum, (row, value) => row.Id >= value, alias)
                        .Select<AliasRow>(row => row.Id, alias).ToList<AliasRow>();
            }
            """;
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.Contains("SqlGeneratedQueryTemplate.Apply(source, Template_", result.GeneratedSource, StringComparison.Ordinal);
        var method = LoadGeneratedAssembly(result).GetType("AliasConsumer")!.GetMethod("Load")!;
        SqlQueryPlan Execute(string alias, int minimum)
        {
            var executor = DispatchProxy.Create<ISqlQueryPlanExecutor, CapturingPlanExecutor>();
            method.Invoke(null, new object[] { executor, new SqliteBuilder(ExplicitValueTemplateServices), alias, minimum });
            return Assert.IsType<SqlQueryPlan>(((CapturingPlanExecutor)executor).Plan);
        }

        var first = Execute("r", 3);
        var different = Execute("x", 7);
        var last = Execute("r", 11);

        Assert.Equal("Select `r`.`Id` \r\nFrom `AliasRow` As `r` \r\nWhere `r`.`Id`>=@_p_0", first.CommandText);
        Assert.Equal("Select `x`.`Id` \r\nFrom `AliasRow` As `x` \r\nWhere `x`.`Id`>=@_p_0", different.CommandText);
        Assert.Same(first.CommandText, last.CommandText);
        Assert.Equal(7, Assert.Single(Assert.IsType<SqlParam[]>(different.Parameters)).Value);
        Assert.Equal(11, Assert.Single(Assert.IsType<SqlParam[]>(last.Parameters)).Value);
    }

    /// <summary>生成的计划在真实 SQLite 上按本次条件和值返回结果。</summary>
    [Theory]
    [InlineData("Inline", true, 2, 2, 2)]
    [InlineData("Cached", true, 3, 1, 3)]
    [InlineData("Cached", false, 99, 3, 1)]
    public void ExplicitValueWhereIf_WhenExecutedAgainstSqlite_ShouldUseCurrentPlan(
        string methodName, bool enabled, int minimum, int expectedCount, int expectedFirst)
    {
        var plan = ExecuteExplicitValueQuery(methodName, enabled, minimum);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE ExplicitValueRows (Id INTEGER); INSERT INTO ExplicitValueRows VALUES (1), (2), (3);";
        command.ExecuteNonQuery();
        command.CommandText = plan.CommandText;
        foreach (var parameter in Assert.IsType<SqlParam[]>(plan.Parameters))
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        using var reader = command.ExecuteReader();
        var ids = new List<int>();
        while (reader.Read()) ids.Add(reader.GetInt32(0));

        Assert.Equal(expectedCount, ids.Count);
        Assert.Equal(expectedFirst, ids[0]);
    }

    /// <summary>复合及集合显式值谓词应保留原生成桥接回退，不伪装成简单模板操作。</summary>
    [Fact]
    public void Generate_WhenExplicitValuePredicateIsComposite_ShouldPreserveBridgeFallback()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Data.Sql;
            public sealed class ExplicitFallbackRow
            {
                public int Id { get; set; }
                public int ParentId { get; set; }
            }
            public static class ExplicitFallbackConsumer
            {
                public static List<ExplicitFallbackRow> Composite(
                    ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder)
                        .From<ExplicitFallbackRow>("r")
                        .WhereIf<ExplicitFallbackRow, int>(enabled, minimum,
                            (row, value) => row.Id >= value && row.ParentId < value, "r")
                        .Select<ExplicitFallbackRow>(row => row.Id, "r")
                        .ToList<ExplicitFallbackRow>();

            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { [StrictAotProperty] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedQueryBridge.WherePredicate(", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlGeneratedQueryTemplate.Apply(source, Template_", result.GeneratedSource,
            StringComparison.Ordinal);
    }

    private static SqlQueryPlan ExecuteExplicitValueQuery(string methodName, bool enabled, int minimum)
    {
        var fixture = ExplicitValueTemplateFixture.Value;
        var consumer = fixture.Assembly.GetType("ExplicitValueTemplateConsumer")!;
        var executor = DispatchProxy.Create<ISqlQueryPlanExecutor, CapturingPlanExecutor>();
        var capture = (CapturingPlanExecutor)executor;
        var builder = new SqliteBuilder(ExplicitValueTemplateServices);

        consumer.GetMethod(methodName)!.Invoke(null, new object[] { executor, builder, enabled, minimum });

        return Assert.IsType<SqlQueryPlan>(capture.Plan);
    }

    private static int CountOccurrences(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private sealed record GeneratedExplicitValueFixture(Assembly Assembly, string GeneratedSource);

    public class CapturingPlanExecutor : DispatchProxy, ISqlGeneratedPlanExecutor
    {
        internal SqlQueryPlan Plan { get; private set; }

        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            throw new NotSupportedException($"未预期调用执行器方法：{targetMethod.Name}");

        public List<TResult> ToGeneratedList<TResult>(SqlQueryPlan plan, Func<DbDataReader, TResult> read,
            int? timeout)
        {
            Plan = plan;
            return new List<TResult>();
        }

        public Task<List<TResult>> ToGeneratedListAsync<TResult>(SqlQueryPlan plan,
            Func<DbDataReader, TResult> read, int? timeout, CancellationToken cancellationToken) =>
            throw new NotSupportedException("此测试只调用同步查询。");
    }
}
