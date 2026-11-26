using System.Reflection;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    private const string RequiredWhereTemplateSource = """
        using System;
        using System.Collections.Generic;
        using System.Linq.Expressions;
        using Bing.Data.Sql;

        public sealed class RequiredWhereRow
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public static class RequiredWhereConsumer
        {
            private static readonly Expression<Func<RequiredWhereRow, int, bool>> Minimum =
                (row, value) => row.Id >= value;

            public static List<RequiredWhereRow> Cached(ISqlQueryPlanExecutor executor, ISqlBuilder builder,
                string alias, int minimum) =>
                SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<RequiredWhereRow>(alias)
                    .Where<RequiredWhereRow, int>(minimum, Minimum, alias)
                    .Select<RequiredWhereRow>(row => new { row.Id, row.Name }, alias)
                    .ToList<RequiredWhereRow>();

            public static List<RequiredWhereRow> Inline(ISqlQueryPlanExecutor executor, ISqlBuilder builder,
                string alias, int minimum) =>
                SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<RequiredWhereRow>(alias)
                    .Where<RequiredWhereRow, int>(minimum, (row, value) => row.Id >= value, alias)
                    .Select<RequiredWhereRow>(row => new { row.Id, row.Name }, alias)
                    .ToList<RequiredWhereRow>();

            public static List<RequiredWhereRow> DefaultProjection(ISqlQueryPlanExecutor executor, ISqlBuilder builder,
                string alias, int minimum) =>
                SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<RequiredWhereRow>(alias)
                    .Where<RequiredWhereRow, int>(row => row.Id, minimum, alias, Bing.Operator.GreaterEqual)
                    .ToList<RequiredWhereRow>();

            public static List<RequiredWhereRow> Nullable(ISqlQueryPlanExecutor executor, ISqlBuilder builder,
                string alias, string name) =>
                SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<RequiredWhereRow>(alias)
                    .Where<RequiredWhereRow, string>(name, (row, value) => row.Name == value, alias)
                    .Select<RequiredWhereRow>(row => row.Id, alias)
                    .ToList<RequiredWhereRow>();
        }
        """;

    private static readonly SqlBuilderServices RequiredWhereServices = SqlBuilderServices.CreateDefault();
    private static readonly Lazy<(Assembly Assembly, string Source)> RequiredWhereFixture = new(() =>
    {
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(RequiredWhereTemplateSource, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        return (LoadGeneratedAssembly(result), result.GeneratedSource);
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>只有必选条件的完整查询也应生成可复用的模板。</summary>
    [Fact]
    public void RequiredWhereTemplate_WhenGenerated_ShouldCoverInlineCachedAndDefaultProjection()
    {
        var source = RequiredWhereFixture.Value.Source;
        Assert.Equal(7, CountOccurrences(source, "SqlGeneratedQueryTemplate.Apply(source, Template_"));
    }

    /// <summary>文本、同步和异步终结应使用相同的必选条件模板准入规则。</summary>
    [Theory]
    [InlineData("ToSql()")]
    [InlineData("ToList<Row>()")]
    [InlineData("ToListAsync<Row>()")]
    [InlineData("First<Row>()")]
    [InlineData("Single<Row>()")]
    [InlineData("Scalar<int>()")]
    [InlineData("ToPage<Row>()")]
    public void RequiredWhereTemplate_WhenTerminalChanges_ShouldGenerateCompleteChain(string terminal)
    {
        var source = """
            using Bing.Data.Sql;
            public sealed class Row { public int Id { get; set; } }
            public static class Consumer {
                public static object Load(ISqlQueryPlanExecutor executor, ISqlBuilder builder, int minimum) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<Row>("r")
                        .Where<Row, int>(minimum, (row, value) => row.Id >= value, "r")
                        .Select<Row>(row => row.Id, "r").TERMINAL;
            }
            """.Replace("TERMINAL", terminal);
        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.Equal(2, CountOccurrences(result.GeneratedSource, "SqlGeneratedQueryTemplate.Apply(source, Template_"));
    }

    /// <summary>同形状复用 SQL 时必须绑定当前值，别名改变则使用独立形状。</summary>
    [Theory]
    [InlineData("Cached")]
    [InlineData("Inline")]
    public void RequiredWhereTemplate_WhenReused_ShouldKeepValuesAndAliasesIsolated(string method)
    {
        var first = ExecuteRequiredWhere(method, "r", 2);
        var second = ExecuteRequiredWhere(method, "r", 3);
        var otherAlias = ExecuteRequiredWhere(method, "s", 4);

        Assert.Equal("Select `r`.`Id` As `Id`,`r`.`Name` As `Name` \r\nFrom `RequiredWhereRow` As `r` \r\nWhere `r`.`Id`>=@_p_0", first.CommandText);
        Assert.Same(first.CommandText, second.CommandText);
        Assert.Equal("Select `s`.`Id` As `Id`,`s`.`Name` As `Name` \r\nFrom `RequiredWhereRow` As `s` \r\nWhere `s`.`Id`>=@_p_0", otherAlias.CommandText);
        Assert.NotSame(first.CommandText, otherAlias.CommandText);
        Assert.Equal(2, Assert.Single(Assert.IsType<SqlParam[]>(first.Parameters)).Value);
        Assert.Equal(3, Assert.Single(Assert.IsType<SqlParam[]>(second.Parameters)).Value);
        Assert.Equal(4, Assert.Single(Assert.IsType<SqlParam[]>(otherAlias.Parameters)).Value);
    }

    /// <summary>没有显式 Select 的查询仍使用既有的默认列投影。</summary>
    [Fact]
    public void RequiredWhereTemplate_WithoutSelect_ShouldReuseDefaultProjection()
    {
        var first = ExecuteRequiredWhere("DefaultProjection", "r", 2);
        var second = ExecuteRequiredWhere("DefaultProjection", "r", 3);

        Assert.Equal("Select * \r\nFrom `RequiredWhereRow` As `r` \r\nWhere `r`.`Id`>=@_p_0", first.CommandText);
        Assert.Same(first.CommandText, second.CommandText);
        Assert.Equal(3, Assert.Single(Assert.IsType<SqlParam[]>(second.Parameters)).Value);
    }

    /// <summary>空值与非空值的 SQL 和参数布局不能相互污染。</summary>
    [Fact]
    public void RequiredWhereTemplate_WhenValueIsNull_ShouldKeepShapesSeparate()
    {
        var first = ExecuteRequiredWhere("Nullable", "r", null);
        var nonNull = ExecuteRequiredWhere("Nullable", "r", "one");
        var last = ExecuteRequiredWhere("Nullable", "r", null);

        Assert.Equal("Select `r`.`Id` \r\nFrom `RequiredWhereRow` As `r` \r\nWhere `r`.`Name` Is Null", first.CommandText);
        Assert.Equal("Select `r`.`Id` \r\nFrom `RequiredWhereRow` As `r` \r\nWhere `r`.`Name`=@_p_0", nonNull.CommandText);
        Assert.Same(first.CommandText, last.CommandText);
        Assert.Empty(Assert.IsType<SqlParam[]>(first.Parameters));
        Assert.Equal("one", Assert.Single(Assert.IsType<SqlParam[]>(nonNull.Parameters)).Value);
    }

    /// <summary>同一模板并发使用时，各查询保留自己的参数。</summary>
    [Fact]
    public void RequiredWhereTemplate_WhenConcurrent_ShouldKeepRequestParametersIndependent()
    {
        var plans = new SqlQueryPlan[32];
        Parallel.For(0, plans.Length, value => plans[value] = ExecuteRequiredWhere("Cached", "r", value));

        for (var value = 0; value < plans.Length; value++)
        {
            Assert.Equal("Select `r`.`Id` As `Id`,`r`.`Name` As `Name` \r\nFrom `RequiredWhereRow` As `r` \r\nWhere `r`.`Id`>=@_p_0", plans[value].CommandText);
            Assert.Equal(value, Assert.Single(Assert.IsType<SqlParam[]>(plans[value].Parameters)).Value);
        }
        Assert.Same(plans[0].CommandText, plans[^1].CommandText);
    }

    /// <summary>生成的必选条件计划可由真实 SQLite 多次执行并返回各自的结果。</summary>
    [Fact]
    public void RequiredWhereTemplate_WhenExecutedAgainstSqlite_ShouldUseCurrentParameter()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE RequiredWhereRow (Id INTEGER, Name TEXT); " +
            "INSERT INTO RequiredWhereRow VALUES (1, 'one'), (2, 'two'), (3, 'three');";
        command.ExecuteNonQuery();

        var first = Read(2);
        var second = Read(3);
        Assert.Equal("Select `r`.`Id` As `Id`,`r`.`Name` As `Name` \r\nFrom `RequiredWhereRow` As `r` \r\nWhere `r`.`Id`>=@_p_0", first.Sql);
        Assert.Same(first.Sql, second.Sql);
        Assert.Equal(new[] { "2:two", "3:three" }, first.Rows);
        Assert.Equal(new[] { "3:three" }, second.Rows);

        (string Sql, string[] Rows) Read(int minimum)
        {
            var plan = ExecuteRequiredWhere("Cached", "r", minimum);
            command.Parameters.Clear();
            command.CommandText = plan.CommandText;
            foreach (var parameter in Assert.IsType<SqlParam[]>(plan.Parameters))
                command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
            using var reader = command.ExecuteReader();
            var rows = new List<string>();
            while (reader.Read()) rows.Add($"{reader.GetInt32(0)}:{reader.GetString(1)}");
            return (plan.CommandText, rows.ToArray());
        }
    }

    private static SqlQueryPlan ExecuteRequiredWhere(string method, string alias, object value)
    {
        var executor = DispatchProxy.Create<ISqlQueryPlanExecutor, CapturingPlanExecutor>();
        RequiredWhereFixture.Value.Assembly.GetType("RequiredWhereConsumer")!.GetMethod(method)!
            .Invoke(null, new[] { executor, new SqliteBuilder(RequiredWhereServices), alias, value });
        return Assert.IsType<SqlQueryPlan>(((CapturingPlanExecutor)executor).Plan);
    }
}
